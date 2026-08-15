import { useEffect, useMemo, useRef } from 'react';
import * as THREE from 'three';
import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import { posesAt } from '../render/interpolate';
import { buildArena } from './arenaScene';
import { buildActors } from './arenaActors';
import { buildOverlays } from './arenaOverlays';
import { orientStatusBib } from './statusBib';
import { configureModelTextureSupport } from './lookModel';
import {
  ArenaCamera,
  directorFrameAt,
  directorMinSpan,
  directorShotInterruptsAt,
  directorShotHoldTicks,
  reviewFocusFrame,
  selectedUnitPointAt,
  selectionFollowFrame,
  strategicOverviewFrame,
  type ArenaFrame,
  type ArenaFraming,
} from '../render/arenaCamera';
import { attachCameraGestures } from '../render/cameraGestures';
import type { ViewerEntrantPresentation } from '../components/Viewer';
import {
  createArenaFramePacer,
  takeArenaFrame,
  type ArenaRenderProfile,
} from '../render/arenaRenderProfile';
import {
  replaySceneIdentity,
  useCurrentReplayView,
} from '../render/useCurrentReplayView';

let sceneMountGeneration = 0;

/**
 * The 3D arena.
 *
 * Same replay, same clock, same textures as the Canvas2D viewer — a different way of
 * putting them on screen. This one is loaded on demand and is not the default, so nothing
 * about the existing viewer changes by its existing.
 *
 * The component owns the renderer and the frame loop; the scene and the actors own the
 * things in it. `time` arrives as a prop and is read through a ref rather than restarting
 * the loop, because playback advances every frame and a re-run effect per frame would
 * rebuild the world sixty times a second.
 */
export default function ArenaCanvas3D({
  replay,
  time,
  selectedUnitKey,
  highlightedUnitKeys = [],
  showVisibility,
  showFog = true,
  onSelectUnit,
  onUnavailable,
  onReady,
  autoFit = true,
  onManualCamera,
  entrants,
  active,
  renderProfile,
}: {
  replay: ReplayModel;
  time: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  highlightedUnitKeys?: readonly ReplayStableUnitKey[];
  showVisibility: boolean;
  /** Master fog switch; off reveals the whole arena. */
  showFog?: boolean;
  onSelectUnit: (unitKey: ReplayStableUnitKey | null) => void;
  onUnavailable: () => void;
  /**
   * Fired once the scene exists and a frame has actually been rendered.
   *
   * This module is a lazy chunk with three.js inside it, so there is a real stretch between
   * "the viewer decided to use WebGL" and "there is something to look at" that no asset
   * counter can observe. Playback must not be offered during it.
   */
  onReady?: () => void;
  /** Follow the action. On by default; a gesture or the chrome's toggle turns it off. */
  autoFit?: boolean;
  /** Fired when a gesture takes the camera, so the chrome can show auto-fit as off. */
  onManualCamera?: () => void;
  entrants?: readonly ViewerEntrantPresentation[];
  /** Whether replay time is advancing. Idle frames retain camera/selection response. */
  active: boolean;
  renderProfile: ArenaRenderProfile;
}) {
  const host = useRef<HTMLDivElement>(null);
  const currentReplay = useCurrentReplayView(replay);
  const sceneIdentity = useMemo(() => replaySceneIdentity(replay), [replay]);
  const entrantIdentity = useMemo(
    () => JSON.stringify(entrants ?? []),
    [entrants],
  );
  // All of these go through refs for the same reason `time` does: they change while a
  // replay is open, and putting them in the effect's dependencies would tear down the
  // renderer and rebuild the entire scene every time someone clicked a bot card.
  const frameState = useRef({
    time,
    selectedUnitKey,
    highlightedUnitKeys,
    showVisibility,
    showFog,
    onSelectUnit,
    onUnavailable,
    onReady,
    autoFit,
    onManualCamera,
    active,
  });
  frameState.current = {
    time,
    selectedUnitKey,
    highlightedUnitKeys,
    showVisibility,
    showFog,
    onSelectUnit,
    onUnavailable,
    onReady,
    autoFit,
    onManualCamera,
    active,
  };
  useEffect(() => {
    // This stable read-through object follows live causal-prefix replacements without
    // making a new prefix a new WebGL scene. A genuinely different match changes
    // sceneIdentity and still receives a clean renderer/camera lifecycle.
    const replay = currentReplay;
    const container = host.current;
    if (!container) return;

    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({
        antialias: true,
        powerPreference: renderProfile.powerPreference,
        // One bit isolates actor silhouettes from persistent mine/sentry bodies. It lets
        // actors win that overlap without disabling wall depth or flattening construct
        // self-occlusion into an always-on-top transparency trick.
        stencil: true,
      });
    } catch {
      // WebGL can be disabled, blocked, or out of contexts. The dimensional renderer is
      // optional, so failure must restore the always-available Canvas2D viewer.
      frameState.current.onUnavailable();
      return;
    }
    renderer.setPixelRatio(
      Math.min(window.devicePixelRatio || 1, renderProfile.webglMaxPixelRatio),
    );
    renderer.shadowMap.enabled = true;
    // PCFSoftShadowMap is deprecated as of r185 and silently downgrades to this one while
    // warning on every mount; naming it is the same picture without the console noise.
    renderer.shadowMap.type = THREE.PCFShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    configureModelTextureSupport(renderer);
    container.appendChild(renderer.domElement);
    renderer.domElement.style.display = 'block';
    renderer.domElement.style.width = '100%';
    renderer.domElement.style.height = '100%';
    // The gesture contract's other half (index.css): without it the browser
    // claims two-finger touches for page zoom and cancels the pointers, so
    // pinch and drag never reach the camera on a phone.
    renderer.domElement.classList.add('arena-touch');
    renderer.domElement.dataset.renderProfile = renderProfile.id;
    renderer.domElement.dataset.rateLimited = String(
      renderProfile.presentationRateLimited,
    );
    renderer.domElement.dataset.activeFps = String(renderProfile.activeFramesPerSecond);
    renderer.domElement.dataset.idleFps = String(renderProfile.idleFramesPerSecond);
    renderer.domElement.dataset.pixelRatio = String(renderer.getPixelRatio());
    renderer.domElement.dataset.shadowMapSize = String(renderProfile.shadowMapSize);
    renderer.domElement.dataset.sceneMountGeneration = String(
      ++sceneMountGeneration,
    );

    const arena = buildArena(replay, { shadowMapSize: renderProfile.shadowMapSize });
    const actors = buildActors(replay);
    const overlays = buildOverlays(replay, entrants, arena.scene);
    // Bot and construct status bibs are authored as fixed billboards. A theme may add a
    // restrained side bias to the otherwise fixed camera, so turn every shared status
    // stack by the same amount rather than letting one family become slightly edge-on.
    for (const root of [actors.group, overlays.group])
      root.traverse((node) => {
        if (node.userData.cue === 'health-bar')
          orientStatusBib(node, arena.cameraYaw, arena.cameraPitch);
      });
    arena.scene.add(actors.group);
    arena.scene.add(overlays.group);
    window.__BOTARENA_RENDER_PROOF__ = {
      sample: () => {
        const visibleSignatures: Array<{
          operationId: string | null;
          signatureId: string | null;
          phase: string | null;
          visibleMeshes: number;
        }> = [];
        let levelUpBeats = 0;
        overlays.group.traverse((node) => {
          if (!node.visible) return;
          if (node.userData.phase && node.userData.signatureId) {
            let visibleMeshes = 0;
            node.traverse((child) => {
              if (child.visible && (child as THREE.Mesh).isMesh)
                visibleMeshes += 1;
            });
            visibleSignatures.push({
              operationId:
                typeof node.userData.operationId === 'string'
                  ? node.userData.operationId
                  : null,
              signatureId:
                typeof node.userData.signatureId === 'string'
                  ? node.userData.signatureId
                  : null,
              phase:
                typeof node.userData.phase === 'string'
                  ? node.userData.phase
                  : null,
              visibleMeshes,
            });
          }
          if (node.userData.kind === 'arc-relay-level-up-beat')
            levelUpBeats += 1;
        });
        return {
          time: frameState.current.time,
          tickCount: replay.ticks.length,
          replaySeed: replay.seed,
          reducedMotion: window.matchMedia(
            '(prefers-reduced-motion: reduce)',
          ).matches,
          visibleSignatures,
          levelUpBeats,
          cameraFrame: camera ? { ...camera.frame } : null,
        };
      },
      frameSignature: (operationId) => {
        if (!new URLSearchParams(window.location.search).has('proof'))
          return false;
        const state = frameState.current;
        const tickIndex = Math.max(
          0,
          Math.min(Math.floor(state.time), replay.ticks.length - 1),
        );
        const mode = replay.ticks[tickIndex]?.after.mode;
        if (
          !camera ||
          mode?.kind !== 'arc-relay' ||
          !('visibleSignatures' in mode)
        ) return false;
        const signature = mode.visibleSignatures.find(
          (candidate) => candidate.operationId === operationId,
        );
        if (!signature) return false;
        const points = signature.positions.length > 0
          ? signature.positions
          : [
              posesAt(replay, state.time).find(
                (pose) => pose.actorKey === signature.ownerActor.actorKey,
              ),
            ].flatMap((pose) => pose ? [{ x: pose.x, y: pose.y }] : []);
        camera.review(reviewFocusFrame(points, framing));
        look(camera.frame);
        return true;
      },
    };

    const mapWidth = replay.map.width;
    const mapHeight = replay.map.height;
    // Where the camera sits with no shake applied. Kept so a knock is an offset from a
    // fixed point; nudging the live position instead lets rounding walk the camera away.
    const framed = new THREE.Vector3();
    const looking = new THREE.Vector3(mapWidth / 2, 0, mapHeight / 2);

    let framing: ArenaFraming = {
      mapWidth,
      mapHeight,
      aspect: 1,
      viewportWidth: 1,
      viewportHeight: 1,
      minSpan: directorMinSpan(replay),
    };
    let camera: ArenaCamera | null = null;
    let lastProofOperation: string | null = null;

    /**
     * Point the camera at a frame, from behind and above.
     *
     * The distance depends on the aspect ratio as well as the span: a letterboxed phone in
     * landscape has to back off much further than a desktop window to hold the same box, so
     * a fixed distance would crop the ends off one and waste half the screen on the other.
     *
     * This is the whole of the 3D renderer's half of the shared camera — the flat renderer
     * turns the same frame into a tile size and an origin. Neither decides *what* to look
     * at; `arenaCamera` does, once, for both.
     */
    const look = (frame: ArenaFrame) => {
      const yaw = arena.cameraYaw;
      // Rotate the requested ground rectangle into camera space before fitting it. Without
      // this compensation even a small side bias can trim a corner on phone-width views.
      const projectedWidth =
        Math.abs(Math.cos(yaw)) * frame.width +
        Math.abs(Math.sin(yaw)) * frame.height;
      const projectedDepth =
        Math.abs(Math.sin(yaw)) * frame.width +
        Math.abs(Math.cos(yaw)) * frame.height;
      const span = Math.max(
        projectedWidth / arena.camera.aspect,
        projectedDepth,
      );
      const distance = (span / 2) / Math.tan((arena.camera.fov * Math.PI) / 360);
      // A shallow tilt: steep enough that walls show a face and cast across the floor,
      // shallow enough that the far half of the arena is not hidden behind the near walls.
      const pitch = arena.cameraPitch;
      looking.set(frame.x, 0, frame.y);
      const horizontalDistance = Math.cos(pitch) * distance * 1.02;
      framed.set(
        frame.x + Math.sin(yaw) * horizontalDistance,
        Math.sin(pitch) * distance * 1.02,
        frame.y + Math.cos(yaw) * horizontalDistance,
      );
    };

    const resize = () => {
      const width = container.clientWidth;
      const height = container.clientHeight;
      if (width === 0 || height === 0) return;

      renderer.setSize(width, height, false);
      arena.camera.aspect = width / height;
      arena.camera.updateProjectionMatrix();
      framing = {
        mapWidth,
        mapHeight,
        aspect: width / height,
        viewportWidth: width,
        viewportHeight: height,
        minSpan: directorMinSpan(replay),
      };
      if (camera === null) {
        camera = new ArenaCamera(framing);
        const state = frameState.current;
        const followedPoint = selectedUnitPointAt(
          replay,
          state.time,
          state.selectedUnitKey,
        );
        // Born in the state the chrome already claims. Overview holds its authored frame;
        // director starts on its first actual theater rather than spending the opening
        // seconds easing inward from the maximum map view; a retained selection starts on
        // the same follow shot it will continue tracking.
        if (followedPoint) {
          const frame = selectionFollowFrame(followedPoint, framing);
          camera.settle(frame);
          camera.track(frame);
        } else if (!state.autoFit) {
          camera.hold(strategicOverviewFrame(replay, framing));
        } else {
          camera.settle(
            directorFrameAt(
              replay,
              state.time,
              null,
              state.highlightedUnitKeys,
              framing,
              proofOperationAt(),
            ),
          );
        }
      } else camera.reframe(framing);
      look(camera.frame);
    };

    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(container);

    /**
     * Pan and zoom, with the same rule the flat renderer follows — two fingers or a mouse,
     * never one finger, which on a phone belongs to the page and to selection.
     *
     * There are no orbit controls to fight here: the camera has always been pitched at a
     * fixed angle and framed by arithmetic, so this adds an override to that framing rather
     * than a second camera model competing with it.
     */
    const gestures = attachCameraGestures({
      element: renderer.domElement,
      get camera() {
        return camera!;
      },
      framing: () => framing,
      // Pixels per tile at the plane the bots stand on, so a drag moves the ground under
      // the finger by roughly the distance the finger moved.
      scale: () =>
        container.clientHeight /
        Math.max(camera?.frame.height ?? framing.mapHeight, 1e-6),
      // The pitched ground plane projects a tile smaller than the plan-view estimate.
      // A small gain restores decisive travel, while direct tracking removes the much
      // larger source of sluggishness: waiting for the spectator spring after every move.
      dragGain: 1.35,
      directDrag: true,
      onOverride: () => frameState.current.onManualCamera?.(),
    });

    /**
     * Tap a bot to follow it, tap it again or tap the floor to stop — the same contract the
     * flat renderer's canvas offers, so the arena behaves the same whichever is on screen.
     *
     * On `pointerup` rather than `pointerdown`, and only when the pointer barely moved: on a
     * phone the arena is also what you drag to scroll the page, and selecting a bot every
     * time a scroll started would make the panel unusable.
     */
    const raycaster = new THREE.Raycaster();
    let pressed: { x: number; y: number } | null = null;
    const onDown = (event: PointerEvent) => {
      pressed = { x: event.clientX, y: event.clientY };
    };
    const onUp = (event: PointerEvent) => {
      const start = pressed;
      pressed = null;
      if (!start || Math.hypot(event.clientX - start.x, event.clientY - start.y) > 8) return;
      // A drag or a pinch that moved the camera is not a tap on a bot.
      if (gestures.panned()) return;

      const bounds = renderer.domElement.getBoundingClientRect();
      raycaster.setFromCamera(
        new THREE.Vector2(
          ((event.clientX - bounds.left) / bounds.width) * 2 - 1,
          -((event.clientY - bounds.top) / bounds.height) * 2 + 1,
        ),
        arena.camera,
      );
      const hit = actors.pick(raycaster);
      const {
        selectedUnitKey: followed,
        onSelectUnit: select,
      } = frameState.current;
      select(hit === null || hit === followed ? null : hit);
    };
    renderer.domElement.addEventListener('pointerdown', onDown);
    renderer.domElement.addEventListener('pointerup', onUp);
    const onContextLost = (event: Event) => {
      event.preventDefault();
      frameState.current.onUnavailable();
    };
    renderer.domElement.addEventListener(
      'webglcontextlost',
      onContextLost,
    );

    let animation = 0;
    let last: number | null = null;
    const framePacer = createArenaFramePacer();
    let announced = false;
    let lastFit = frameState.current.autoFit;
    let lastSelected = frameState.current.selectedUnitKey;
    const draw = (stamp: number) => {
      const framesPerSecond = frameState.current.active
        ? renderProfile.activeFramesPerSecond
        : renderProfile.idleFramesPerSecond;
      const frameDue = !renderProfile.presentationRateLimited ||
        takeArenaFrame(framePacer, stamp, framesPerSecond);
      if (document.hidden || !frameDue) {
        animation = requestAnimationFrame(draw);
        return;
      }
      const {
        time: now,
        selectedUnitKey: followed,
        showVisibility: fov,
        showFog: fog,
        autoFit: fit,
      } = frameState.current;
      actors.update(now, followed, fov, fog);
      overlays.update(
        now,
        followed,
        fov,
        frameState.current.highlightedUnitKeys,
        fog,
      );

      if (camera) {
        // On the toggle *changing*, never on a mismatch. A gesture drops auto-fit inside
        // the camera immediately and reports it upward, and React delivers that state a
        // frame or two later — so a camera that reacted to the mismatch would spend those
        // frames re-engaging the fit and undoing the gesture that had just been made.
        if (fit !== lastFit) {
          lastFit = fit;
          if (fit) camera.engage();
          // A gesture reports itself upward as auto-fit turning *off*, and the camera has
          // already released and is sitting where the hand left it — so answering that
          // report with the overview would undo the gesture one frame after it was made.
          // Only a camera still on an automatic allegiance is being spoken to here.
          else if (camera.auto || camera.followingSelection)
            camera.showFrame(strategicOverviewFrame(replay, framing));
        }
        // Selecting a body is a camera command: it takes the readable shot and holds it on
        // that machine. Deselecting hands the camera back to whichever mode it was taken from,
        // and a gesture ends the follow like it ends the director's — through `release`,
        // which is why staying in the follow is conditioned on the camera's own allegiance
        // rather than on the selection still being set.
        const reselected = followed !== lastSelected;
        if (reselected) {
          lastSelected = followed;
          if (followed === null) {
            if (fit) camera.engage();
            else camera.showFrame(strategicOverviewFrame(replay, framing));
          }
        }
        if (followed !== null && (reselected || camera.followingSelection)) {
          const point = selectedUnitPointAt(replay, now, followed);
          camera.track(point ? selectionFollowFrame(point, framing) : null);
        }
        if (camera.auto) {
          const proofOperation = proofOperationAt();
          const directorFrame = directorFrameAt(
            replay,
            now,
            followed,
            frameState.current.highlightedUnitKeys,
            framing,
            proofOperation,
          );
          const hold = followed === null ? directorShotHoldTicks(replay) : 0;
          const proofChanged = proofOperation !== null &&
            proofOperation !== lastProofOperation;
          if (
            followed === null &&
            (directorShotInterruptsAt(replay, now) || proofChanged)
          )
            camera.cut(directorFrame, now, hold);
          else camera.aim(directorFrame, now, hold);
          lastProofOperation = proofOperation;
        }
        // Real seconds, so the camera settles at the same rate whatever the playback speed
        // and whatever the frame rate; the spring clamps a huge step of its own accord.
        camera.advance(last === null ? 0 : (stamp - last) / 1000);
        look(camera.frame);
      }
      last = stamp;

      // A knock on impact, and a harder one on a kill — nothing else shakes, because a
      // camera that moves on every shot stops meaning anything. Applied as an offset from
      // the framed position rather than by moving the camera, so it cannot accumulate.
      const knock = overlays.shake(now);
      arena.camera.position.set(
        framed.x + knock.x,
        framed.y + knock.y,
        framed.z + knock.x * 0.6,
      );
      arena.camera.lookAt(looking);
      renderer.render(arena.scene, arena.camera);
      // Announced after the draw, not before it: the point of the signal is that there is
      // now something on screen, and reporting it from the effect would hand back a ready
      // renderer that had not yet drawn a pixel.
      if (!announced) {
        announced = true;
        frameState.current.onReady?.();
      }
      animation = requestAnimationFrame(draw);
    };
    animation = requestAnimationFrame(draw);

    return () => {
      delete window.__BOTARENA_RENDER_PROOF__;
      cancelAnimationFrame(animation);
      observer.disconnect();
      gestures.detach();
      renderer.domElement.removeEventListener('pointerdown', onDown);
      renderer.domElement.removeEventListener('pointerup', onUp);
      renderer.domElement.removeEventListener(
        'webglcontextlost',
        onContextLost,
      );
      actors.dispose();
      overlays.dispose();
      arena.dispose();
      // The GL context is a real resource and browsers cap how many exist at once; losing
      // one per replay would eventually stop the page rendering anything at all.
      renderer.dispose();
      renderer.forceContextLoss();
      container.removeChild(renderer.domElement);
    };
  }, [currentReplay, entrantIdentity, renderProfile, sceneIdentity]);

  return (
    <div
      ref={host}
      className="absolute inset-0 cursor-pointer"
      role="img"
      aria-label="nilbots match playback"
    />
  );
}

/** Review-only preference; the ordinary renderer still computes and draws the shot. */
function proofOperationAt(): string | null {
  const query = new URLSearchParams(window.location.search);
  return query.has('proof') ? query.get('proof-operation') : null;
}
