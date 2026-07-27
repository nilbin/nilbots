import * as THREE from 'three';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { participantForUnit, visualIndexForUnit } from '../replayParticipants';
import { spentBoltsAt } from '../render/interpolate';
import { PROJECTILE_HOVER } from './arenaActors';
import { presentationAccent, botLook } from '../render/arenaThemes';

/**
 * What the arena knows and what it is doing to itself: fog of war, the objective zone, and
 * the flash of a shot landing.
 *
 * These are the parts of the flat renderer that are *not* objects — it draws them by
 * compositing over the finished frame, which a scene graph cannot do. Each becomes a thing
 * in the world instead, which is mostly an improvement (a flash lights the floor it is on)
 * and in one place a compromise, noted where it happens.
 */

/** How dark an unseen tile goes. Matches the flat renderer's fog composite. */
const FOG_STRENGTH = 0.82;

/** How far the camera is thrown by a direct kill, in tiles. */
const SHAKE_REACH = 0.14;


export interface ArenaOverlays {
  group: THREE.Group;
  update: (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
  ) => void;
  /** Camera offset for the knock of an impact at this instant. */
  shake: (time: number) => { x: number; y: number };
  dispose: () => void;
}

export function buildOverlays(replay: ReplayModel): ArenaOverlays {
  const { width: mapWidth, height: mapHeight } = replay.map;
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];

  const fog = buildFog(mapWidth, mapHeight, disposables);
  group.add(fog.mesh);

  const zone = buildZone(replay, disposables);
  if (zone) group.add(zone);

  const flashes = buildFlashes(replay, disposables);
  group.add(flashes.group);

  const spent = buildSpentBolts(replay, disposables);
  group.add(spent.group);

  let painted = '';

  const update = (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
  ) => {
    const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const fraction = Math.max(0, Math.min(time - tick, 1));

    const source =
      showVisibility && selectedUnitKey !== null
        ? replay.ticks[tick]?.actorTurns.find(
            (turn) => turn.actor.unitKey === selectedUnitKey,
          )
        : undefined;

    // Repainting the mask is a loop over every tile on the map, and the playhead crosses
    // dozens of frames per tick — so it only happens when the answer can have changed.
    const signature = `${source ? tick : -1}:${selectedUnitKey}`;
    if (signature !== painted) {
      painted = signature;
      fog.paint(source?.observation.visibleTiles);
    }

    flashes.update(tick, fraction);
    spent.update(time);
  };

  /**
   * How hard the arena is knocked at this instant.
   *
   * Derived from the tick rather than accumulated, like everything else here: playback can
   * be scrubbed backwards into a tick where nothing was hit, and a decaying variable would
   * still be ringing from a kill that has not happened yet.
   *
   * **Only impacts shake, and a kill shakes harder.** A camera that jolts on every shot
   * stops distinguishing anything, which is the same reasoning the flat renderer records.
   */
  const shake = (time: number) => {
    const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const fraction = Math.max(0, Math.min(time - tick, 1));
    let strength = 0;
    for (const event of replay.ticks[tick]?.events ?? []) {
      if (event.type === 'Destroyed') strength = Math.max(strength, 1);
      else if (event.type === 'Damage') strength = Math.max(strength, 0.45);
    }
    // Impacts land late in the tick — the same 0.6 the flash uses — so the knock starts
    // when the hit is seen rather than when the tick begins.
    const since = (fraction - 0.6) / 0.4;
    if (strength === 0 || since < 0 || since > 1) return { x: 0, y: 0 };

    const decay = (1 - since) ** 2;
    const amplitude = SHAKE_REACH * strength * decay;
    // Two incommensurate frequencies, so it reads as a knock rather than a wobble.
    return {
      x: Math.sin(since * Math.PI * 7.3) * amplitude,
      y: Math.cos(since * Math.PI * 5.1) * amplitude * 0.7,
    };
  };

  return {
    group,
    update,
    shake,
    dispose: () => {
      for (const item of disposables) item.dispose();
    },
  };
}

/**
 * Fog as a mask over the floor.
 *
 * One texture with a texel per tile, laid over the arena and resampled smoothly, rather
 * than a quad per hidden tile — the whole mask is one draw call and one upload per tick,
 * and the linear filter gives the soft boundary the flat renderer gets from a blur.
 *
 * **It darkens the floor, not the walls.** A horizontal plane can only be positioned to
 * align with one height, and putting it above the walls would slide it off the floor
 * beneath by most of a tile at this camera pitch. The floor is the right choice: walls are
 * static terrain that both players have always known about, whereas the floor is where the
 * information actually is — and unseen *bots* and *bolts* are hidden by the actors
 * themselves, which is the part that would otherwise be a lie.
 */
function buildFog(
  mapWidth: number,
  mapHeight: number,
  disposables: { dispose: () => void }[],
): {
  mesh: THREE.Mesh;
  paint: (visible: readonly { position: { x: number; y: number } }[] | undefined) => void;
} {
  const data = new Uint8Array(mapWidth * mapHeight * 4);
  const texture = new THREE.DataTexture(data, mapWidth, mapHeight, THREE.RGBAFormat);
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.needsUpdate = true;

  const geometry = new THREE.PlaneGeometry(mapWidth, mapHeight);
  geometry.rotateX(-Math.PI / 2);
  geometry.translate(mapWidth / 2, 0, mapHeight / 2);
  const material = new THREE.MeshBasicMaterial({
    map: texture,
    transparent: true,
    depthWrite: false,
  });
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.y = 0.03;
  mesh.visible = false;
  disposables.push(geometry, material, texture);

  const paint = (
    visible: readonly { position: { x: number; y: number } }[] | undefined,
  ) => {
    mesh.visible = visible !== undefined;
    if (!visible) return;

    const seen = new Set(visible.map(({ position }) => `${position.x},${position.y}`));
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++) {
        // The plane's V axis runs opposite the map's Y, so rows are written bottom-up.
        const texel = ((mapHeight - 1 - y) * mapWidth + x) * 4;
        data[texel] = 0;
        data[texel + 1] = 0;
        data[texel + 2] = 0;
        data[texel + 3] = seen.has(`${x},${y}`) ? 0 : Math.round(FOG_STRENGTH * 255);
      }
    }
    texture.needsUpdate = true;
  };

  return { mesh, paint };
}

/** The objective zone, where the rules have one. Static for the match. */
function buildZone(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): THREE.Mesh | null {
  const tiles = replay.map.objectiveTiles;
  if (tiles.length === 0) return null;

  const parts = tiles.map((tile) => {
    const quad = new THREE.PlaneGeometry(1, 1);
    quad.rotateX(-Math.PI / 2);
    quad.translate(tile.x + 0.5, 0, tile.y + 0.5);
    return quad;
  });
  const geometry = mergeQuads(parts);
  for (const part of parts) part.dispose();

  const material = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#22d3ee'),
    transparent: true,
    opacity: 0.14,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.y = 0.024;
  disposables.push(geometry, material);
  return mesh;
}

/**
 * The flash of a shot leaving a barrel and of one landing.
 *
 * Pooled and driven by the tick's own events rather than accumulated over time, for the
 * same reason the flat renderer re-derives its impact knock every frame: playback can be
 * scrubbed, paused, or replayed from any point, and anything remembered between frames
 * would survive a jump backwards into a tick where it never happened.
 */
function buildFlashes(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (tick: number, fraction: number) => void } {
  const group = new THREE.Group();
  const geometry = new THREE.PlaneGeometry(1, 1);
  geometry.rotateX(-Math.PI / 2);
  disposables.push(geometry);

  const accents = accentsByUnit(replay);
  const impact = new THREE.Color('#ffd9a0');

  const pool: THREE.Mesh[] = [];
  const material = new THREE.MeshBasicMaterial({
    map: flare(),
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });
  disposables.push(material);
  if (material.map) disposables.push(material.map);

  const borrow = (index: number) => {
    while (pool.length <= index) {
      // Cloned so each flash carries its own colour and opacity while sharing one texture.
      const mesh = new THREE.Mesh(geometry, material.clone());
      mesh.visible = false;
      group.add(mesh);
      pool.push(mesh);
    }
    return pool[index];
  };

  const update = (tick: number, fraction: number) => {
    const events = replay.ticks[tick]?.events ?? [];
    let used = 0;
    for (const event of events) {
      const flash =
        event.type === 'shot'
          ? {
              at: event.from,
              colour: accents.get(event.sourceActor?.unitKey ?? ('' as ReplayStableUnitKey)),
              size: 1.5,
              life: 0.45,
            }
          : event.type === 'damage' || event.type === 'destroyed'
            ? { at: event.to, colour: impact, size: 2.4, life: 0.8 }
            : null;
      if (!flash || !flash.at) continue;

      // Bright at the instant it happens and gone by the end of its life, so a shot reads
      // as an event rather than a lamp that switches on for the tick.
      const decay = 1 - Math.min(1, fraction / flash.life);
      if (decay <= 0) continue;

      const mesh = borrow(used++);
      mesh.visible = true;
      mesh.position.set(flash.at.x + 0.5, 0.05, flash.at.y + 0.5);
      mesh.scale.setScalar(flash.size * (0.6 + 0.4 * (1 - decay)));
      const own = mesh.material as THREE.MeshBasicMaterial;
      own.color.copy(flash.colour ?? impact);
      own.opacity = decay * decay;
    }
    for (let index = used; index < pool.length; index++) pool[index].visible = false;
  };

  return { group, update };
}

/**
 * The puff a bolt leaves when it runs out of range.
 *
 * A ring rather than a blob: a bolt dissipating outward reads as something coming apart,
 * where a fading dot reads as the renderer losing track of it. It expands and thins on the
 * same curve, so it is gone by the end of the tick it is drawn in — long enough to see,
 * short enough that a busy exchange is not full of ghosts.
 */
function buildSpentBolts(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (time: number) => void } {
  const group = new THREE.Group();
  const geometry = new THREE.RingGeometry(0.12, 0.34, 24);
  geometry.rotateX(-Math.PI / 2);
  disposables.push(geometry);

  const accents = accentsByUnit(replay);

  const pool: THREE.Mesh[] = [];
  const borrow = (index: number) => {
    while (pool.length <= index) {
      const material = new THREE.MeshBasicMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.visible = false;
      group.add(mesh);
      pool.push(mesh);
      disposables.push(material);
    }
    return pool[index];
  };

  const update = (time: number) => {
    let used = 0;
    for (const bolt of spentBoltsAt(replay, time)) {
      const mesh = borrow(used++);
      mesh.visible = true;
      mesh.position.set(bolt.x + 0.5, PROJECTILE_HOVER, bolt.y + 0.5);
      mesh.scale.setScalar(0.6 + bolt.age * 2.2);
      const material = mesh.material as THREE.MeshBasicMaterial;
      material.color.copy(accents.get(bolt.ownerUnitKey) ?? impactWhite);
      material.opacity = (1 - bolt.age) ** 2 * 0.9;
    }
    for (let index = used; index < pool.length; index++) pool[index].visible = false;
  };

  return { group, update };
}

const impactWhite = new THREE.Color('#ffd9a0');

/**
 * Every unit's accent, by stable key.
 *
 * Built once per replay rather than resolved per event: a busy tick can carry a dozen
 * flashes, and each lookup walks the participant roster.
 */
function accentsByUnit(replay: ReplayModel): Map<ReplayStableUnitKey, THREE.Color> {
  const accents = new Map<ReplayStableUnitKey, THREE.Color>();
  for (const unit of replay.units) {
    const participant = participantForUnit(replay, unit.unitKey);
    accents.set(
      unit.unitKey,
      new THREE.Color(
        presentationAccent(
          botLook(participant?.lookId ?? undefined, visualIndexForUnit(replay, unit.unitKey)),
          participant?.accent ?? '#38bdf8',
        ),
      ),
    );
  }
  return accents;
}

/** A soft round flare, drawn rather than shipped. */
function flare(): THREE.Texture | null {
  if (typeof document === 'undefined') return null;
  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) return null;

  const gradient = context.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  gradient.addColorStop(0, 'rgba(255, 255, 255, 1)');
  gradient.addColorStop(0.25, 'rgba(255, 255, 255, 0.55)');
  gradient.addColorStop(1, 'rgba(255, 255, 255, 0)');
  context.fillStyle = gradient;
  context.fillRect(0, 0, size, size);

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

/** Concatenate plain position/normal/uv quads into one buffer. */
function mergeQuads(parts: readonly THREE.BufferGeometry[]): THREE.BufferGeometry {
  const merged = new THREE.BufferGeometry();
  for (const name of ['position', 'normal', 'uv'] as const) {
    const arrays = parts.map((part) => part.attributes[name].array as Float32Array);
    const combined = new Float32Array(arrays.reduce((sum, array) => sum + array.length, 0));
    let offset = 0;
    for (const array of arrays) {
      combined.set(array, offset);
      offset += array.length;
    }
    merged.setAttribute(
      name,
      new THREE.BufferAttribute(combined, parts[0].attributes[name].itemSize),
    );
  }
  const indices: number[] = [];
  let vertexOffset = 0;
  for (const part of parts) {
    const index = part.getIndex()!;
    for (let i = 0; i < index.count; i++) indices.push(index.getX(i) + vertexOffset);
    vertexOffset += part.attributes.position.count;
  }
  merged.setIndex(indices);
  return merged;
}
