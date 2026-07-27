import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import type { ReplayDocument } from '../types';
import { buildArena } from './arenaScene';
import { buildActors } from './arenaActors';

/**
 * The 2.5D arena.
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
}: {
  replay: ReplayDocument;
  time: number;
}) {
  const host = useRef<HTMLDivElement>(null);
  const timeRef = useRef(time);
  timeRef.current = time;

  useEffect(() => {
    const container = host.current;
    if (!container) return;

    const renderer = new THREE.WebGLRenderer({
      antialias: true,
      powerPreference: 'high-performance',
    });
    // Capped at 2: beyond that the shadow map and fill rate cost more than the sharpness
    // is worth, and a 3× phone would otherwise render nine times the pixels of a 1×.
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    container.appendChild(renderer.domElement);
    renderer.domElement.style.display = 'block';
    renderer.domElement.style.width = '100%';
    renderer.domElement.style.height = '100%';

    const arena = buildArena(replay);
    const actors = buildActors(replay);
    arena.scene.add(actors.group);

    const { mapWidth, mapHeight } = replay.header;
    const centre = new THREE.Vector3(mapWidth / 2, 0, mapHeight / 2);

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
      const pitch = THREE.MathUtils.degToRad(58);
      arena.camera.position.set(
        centre.x,
        centre.y + Math.sin(pitch) * distance * 1.02,
        centre.z + Math.cos(pitch) * distance * 1.02,
      );
      arena.camera.lookAt(centre);
      arena.camera.updateProjectionMatrix();
    };

    frame();
    const observer = new ResizeObserver(frame);
    observer.observe(container);

    let animation = 0;
    const draw = () => {
      actors.update(timeRef.current);
      renderer.render(arena.scene, arena.camera);
      animation = requestAnimationFrame(draw);
    };
    animation = requestAnimationFrame(draw);

    return () => {
      cancelAnimationFrame(animation);
      observer.disconnect();
      actors.dispose();
      arena.dispose();
      // The GL context is a real resource and browsers cap how many exist at once; losing
      // one per replay would eventually stop the page rendering anything at all.
      renderer.dispose();
      renderer.forceContextLoss();
      container.removeChild(renderer.domElement);
    };
  }, [replay]);

  return <div ref={host} className="absolute inset-0" role="img" aria-label="nilbots match playback" />;
}
