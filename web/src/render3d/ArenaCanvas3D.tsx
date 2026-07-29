import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import { buildArena, CAMERA_PITCH } from './arenaScene';
import { buildActors } from './arenaActors';
import { buildOverlays } from './arenaOverlays';

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
}: {
  replay: ReplayModel;
  time: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  showVisibility: boolean;
  onSelectUnit: (unitKey: ReplayStableUnitKey | null) => void;
  onUnavailable: () => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  // All three go through refs for the same reason `time` does: they change while a replay
  // is open, and putting them in the effect's dependencies would tear down the renderer and
  // rebuild the entire scene every time someone clicked a bot card.
  const frameState = useRef({
    time,
    selectedUnitKey,
    showVisibility,
    onSelectUnit,
    onUnavailable,
  });
  frameState.current = {
    time,
    selectedUnitKey,
    showVisibility,
    onSelectUnit,
    onUnavailable,
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
    const centre = new THREE.Vector3(mapWidth / 2, 0, mapHeight / 2);
    // Where the camera sits with no shake applied. Kept so a knock is an offset from a
    // fixed point; nudging the live position instead lets rounding walk the camera away.
    const framed = new THREE.Vector3();

    /**
     * Frame the whole map, from behind and above.
     *
     * Recomputed on resize because the distance needed depends on the aspect ratio: a
     * letterboxed phone in landscape needs to back off much further than a desktop window
     * to fit the same arena, and a fixed distance would crop the ends off one or waste
     * half the screen on the other.
     */
    const frame = () => {
      const width = container.clientWidth;
      const height = container.clientHeight;
      if (width === 0 || height === 0) return;

      renderer.setSize(width, height, false);
      arena.camera.aspect = width / height;

      const span = Math.max(mapWidth / arena.camera.aspect, mapHeight);
      const distance = (span / 2) / Math.tan((arena.camera.fov * Math.PI) / 360);
      // A shallow tilt: steep enough that walls show a face and cast across the floor,
      // shallow enough that the far half of the arena is not hidden behind the near walls.
      const pitch = CAMERA_PITCH;
      arena.camera.position.set(
        centre.x,
        centre.y + Math.sin(pitch) * distance * 1.02,
        centre.z + Math.cos(pitch) * distance * 1.02,
      );
      arena.camera.lookAt(centre);
      arena.camera.updateProjectionMatrix();
      framed.copy(arena.camera.position);
    };

    frame();
    const observer = new ResizeObserver(frame);
    observer.observe(container);

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
    const draw = () => {
      const {
        time: now,
        selectedUnitKey: followed,
        showVisibility: fov,
      } = frameState.current;
      actors.update(now, followed, fov);
      overlays.update(now, followed, fov);
      // A knock on impact, and a harder one on a kill — nothing else shakes, because a
      // camera that moves on every shot stops meaning anything. Applied as an offset from
      // the framed position rather than by moving the camera, so it cannot accumulate.
      const knock = overlays.shake(now);
      arena.camera.position.set(
        framed.x + knock.x,
        framed.y + knock.y,
        framed.z + knock.x * 0.6,
      );
      renderer.render(arena.scene, arena.camera);
      animation = requestAnimationFrame(draw);
    };
    animation = requestAnimationFrame(draw);

    return () => {
      cancelAnimationFrame(animation);
      observer.disconnect();
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
