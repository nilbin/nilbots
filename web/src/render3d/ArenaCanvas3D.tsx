import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import { buildArena, CAMERA_PITCH } from './arenaScene';
import { buildActors } from './arenaActors';
import { buildOverlays } from './arenaOverlays';
import {
  ArenaCamera,
  focusFrame,
  focusPointsAt,
  type ArenaFrame,
  type ArenaFraming,
} from '../render/arenaCamera';
import { attachCameraGestures } from '../render/cameraGestures';

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
  showVisibility,
  onSelectUnit,
  onUnavailable,
  autoFit = true,
  onManualCamera,
}: {
  replay: ReplayModel;
  time: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  showVisibility: boolean;
  onSelectUnit: (unitKey: ReplayStableUnitKey | null) => void;
  onUnavailable: () => void;
  /** Follow the action. On by default; a gesture or the chrome's toggle turns it off. */
  autoFit?: boolean;
  /** Fired when a gesture takes the camera, so the chrome can show auto-fit as off. */
  onManualCamera?: () => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  // All of these go through refs for the same reason `time` does: they change while a
  // replay is open, and putting them in the effect's dependencies would tear down the
  // renderer and rebuild the entire scene every time someone clicked a bot card.
  const frameState = useRef({
    time,
    selectedUnitKey,
    showVisibility,
    onSelectUnit,
    onUnavailable,
    autoFit,
    onManualCamera,
  });
  frameState.current = {
    time,
    selectedUnitKey,
    showVisibility,
    onSelectUnit,
    onUnavailable,
    autoFit,
    onManualCamera,
  };

  useEffect(() => {
    const container = host.current;
    if (!container) return;

    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({
        antialias: true,
        powerPreference: 'high-performance',
      });
    } catch {
      // WebGL can be disabled, blocked, or out of contexts. The dimensional renderer is
      // optional, so failure must restore the always-available Canvas2D viewer.
      frameState.current.onUnavailable();
      return;
    }
    // Capped at 2: beyond that the shadow map and fill rate cost more than the sharpness
    // is worth, and a 3× phone would otherwise render nine times the pixels of a 1×.
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    // PCFSoftShadowMap is deprecated as of r185 and silently downgrades to this one while
    // warning on every mount; naming it is the same picture without the console noise.
    renderer.shadowMap.type = THREE.PCFShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    container.appendChild(renderer.domElement);
    renderer.domElement.style.display = 'block';
    renderer.domElement.style.width = '100%';
    renderer.domElement.style.height = '100%';

    const arena = buildArena(replay);
    const actors = buildActors(replay);
    const overlays = buildOverlays(replay);
    arena.scene.add(actors.group);
    arena.scene.add(overlays.group);

    const mapWidth = replay.map.width;
    const mapHeight = replay.map.height;
    // Where the camera sits with no shake applied. Kept so a knock is an offset from a
    // fixed point; nudging the live position instead lets rounding walk the camera away.
    const framed = new THREE.Vector3();
    const looking = new THREE.Vector3(mapWidth / 2, 0, mapHeight / 2);

    let framing: ArenaFraming = { mapWidth, mapHeight, aspect: 1 };
    let camera: ArenaCamera | null = null;

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
      const span = Math.max(frame.width / arena.camera.aspect, frame.height);
      const distance = (span / 2) / Math.tan((arena.camera.fov * Math.PI) / 360);
      // A shallow tilt: steep enough that walls show a face and cast across the floor,
      // shallow enough that the far half of the arena is not hidden behind the near walls.
      const pitch = CAMERA_PITCH;
      looking.set(frame.x, 0, frame.y);
      framed.set(
        frame.x,
        Math.sin(pitch) * distance * 1.02,
        frame.y + Math.cos(pitch) * distance * 1.02,
      );
    };

    const resize = () => {
      const width = container.clientWidth;
      const height = container.clientHeight;
      if (width === 0 || height === 0) return;

      renderer.setSize(width, height, false);
      arena.camera.aspect = width / height;
      arena.camera.updateProjectionMatrix();
      framing = { mapWidth, mapHeight, aspect: width / height };
      if (camera === null) camera = new ArenaCamera(framing);
      else camera.reframe(framing);
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
    let lastFit = frameState.current.autoFit;
    const draw = (stamp: number) => {
      const {
        time: now,
        selectedUnitKey: followed,
        showVisibility: fov,
        autoFit: fit,
      } = frameState.current;
      actors.update(now, followed, fov);
      overlays.update(now, followed, fov);

      if (camera) {
        // On the toggle *changing*, never on a mismatch. A gesture drops auto-fit inside
        // the camera immediately and reports it upward, and React delivers that state a
        // frame or two later — so a camera that reacted to the mismatch would spend those
        // frames re-engaging the fit and undoing the gesture that had just been made.
        if (fit !== lastFit) {
          lastFit = fit;
          if (fit) camera.engage();
          else camera.showEverything(framing);
        }
        if (camera.auto)
          camera.aim(focusFrame(focusPointsAt(replay, now, followed), framing));
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
      animation = requestAnimationFrame(draw);
    };
    animation = requestAnimationFrame(draw);

    return () => {
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
  }, [replay]);

  return (
    <div
      ref={host}
      className="absolute inset-0 cursor-pointer"
      role="img"
      aria-label="nilbots match playback"
    />
  );
}
