import * as THREE from 'three';
import type {
  ReplayModel,
  ReplayPosition,
  ReplayStableUnitKey,
} from '../replayModel';
import { isAttackEvent, isDestructionEvent } from '../replayModel';
import { arrivalsAt, spentBoltsAt } from '../render/interpolate';
import { PROJECTILE_HOVER } from './arenaActors';
import { unitAccent } from '../render/unitPresentation';
import {
  createPresenter,
  type TickPresentation,
} from '../replayPresentation';

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
  const mapWidth = replay.map.width;
  const mapHeight = replay.map.height;
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];
  const presenter = createPresenter(replay);

  const fog = buildFog(mapWidth, mapHeight, disposables);
  group.add(fog.mesh);

  const spawnPads = buildSpawnPads(replay, disposables);
  group.add(spawnPads.group);

  const objective = buildObjective(replay, disposables);
  group.add(objective.group);

  const lifecycle = buildLifecycleCues(replay, disposables);
  group.add(lifecycle.group);

  const flashes = buildFlashes(replay, disposables);
  group.add(flashes.group);

  const impacts = buildImpacts(replay, disposables);
  group.add(impacts.group);

  const spent = buildSpentBolts(replay, disposables);
  group.add(spent.group);

  const absorptions = buildAbsorptions(replay, disposables);
  group.add(absorptions.group);

  const arrivals = buildArrivals(replay, disposables);
  group.add(arrivals.group);

  let painted = '';

  const update = (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
  ) => {
    const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const fraction = Math.max(0, Math.min(time - tick, 1));
    const presentation = presenter.at(tick);

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
      fog.paint(
        source?.observation.visibleTiles.map(
          ({ position }) => position,
        ),
      );
    }

    spawnPads.update(presentation);
    objective.update(presentation, time);
    lifecycle.update(presentation, time);
    flashes.update(tick, fraction);
    impacts.update(tick, fraction);
    spent.update(time);
    absorptions.update(tick, fraction);
    arrivals.update(time);
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
      if (isDestructionEvent(event.type)) strength = Math.max(strength, 1);
      else if (event.type === 'damage')
        strength = Math.max(strength, 0.45);
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
  paint: (visible: readonly ReplayPosition[] | undefined) => void;
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

  const paint = (visible: readonly ReplayPosition[] | undefined) => {
    mesh.visible = visible !== undefined;
    if (!visible) return;

    const seen = new Set(
      visible.map((position) => `${position.x},${position.y}`),
    );
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

/**
 * Authored Frontline homes as inset service pads.
 *
 * The replay map owns every tile. These meshes are deliberately flat, non-colliding
 * presentation layered over the continuous floor: a dark neutral bed, an exposed-edge
 * seal, and small service hatches. Team identity is applied here from the presentation,
 * never baked into a map texture or asset.
 */
function buildSpawnPads(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (presentation: TickPresentation) => void;
} {
  const group = new THREE.Group();
  group.userData.kind = 'frontline-spawn-pads';
  const homes = replay.map.frontline?.teamHomes ?? [];
  const entries = homes
    .filter((home) => home.protectedSpawnPad.length > 0)
    .map((home) => {
      const pad = new THREE.Group();
      pad.userData.kind = 'frontline-spawn-pad';
      pad.userData.teamId = home.teamId;

      const bedGeometry = tiledInsetGeometry(home.protectedSpawnPad, 0.055);
      const bedMaterial = new THREE.MeshBasicMaterial({
        color: '#211b18',
        transparent: true,
        opacity: 0.34,
        depthWrite: false,
      });
      const bed = new THREE.Mesh(bedGeometry, bedMaterial);
      bed.position.y = 0.012;
      pad.add(bed);

      const sealGeometry = tileBoundaryGeometry(
        home.protectedSpawnPad,
        0.055,
        0.045,
      );
      const sealMaterial = new THREE.MeshBasicMaterial({
        color: '#b27a43',
        transparent: true,
        opacity: 0.23,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const seal = new THREE.Mesh(sealGeometry, sealMaterial);
      seal.position.y = 0.018;
      pad.add(seal);

      const hatchGeometry = serviceHatchGeometry(home.protectedSpawnPad);
      const hatchMaterial = new THREE.MeshBasicMaterial({
        color: '#b27a43',
        transparent: true,
        opacity: 0.14,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const hatches = new THREE.Mesh(hatchGeometry, hatchMaterial);
      hatches.position.y = 0.02;
      pad.add(hatches);

      group.add(pad);
      disposables.push(
        bedGeometry,
        bedMaterial,
        sealGeometry,
        sealMaterial,
        hatchGeometry,
        hatchMaterial,
      );
      return { teamId: home.teamId, pad, sealMaterial, hatchMaterial };
    });

  const update = (presentation: TickPresentation) => {
    for (const entry of entries) {
      const accent =
        presentation.units.find((unit) => unit.teamId === entry.teamId)
          ?.accent ?? '#b27a43';
      entry.sealMaterial.color.set(accent);
      entry.hatchMaterial.color.set(accent);
      entry.pad.userData.accent = accent;
    }
  };

  return { group, update };
}

/**
 * Objective geometry for both normalized formats.
 *
 * A duel has one static zone. Frontline keeps every authored position visible as a faint
 * strategic landmark and promotes only the authoritative active position each tick, so a
 * five-position map reads as a lane rather than a zone teleporting around the floor.
 */
function buildObjective(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (presentation: TickPresentation, time: number) => void;
} {
  const group = new THREE.Group();
  const frontline = replay.map.frontline;
  const positions = frontline?.positions ?? [
    {
      positionIndex: 0,
      tiles: replay.map.objectiveTiles,
    },
  ];
  group.userData.positionCount = positions.length;

  if (!frontline) {
    if (replay.map.objectiveTiles.length === 0)
      return { group, update: () => {} };
    const geometry = tiledGeometry(replay.map.objectiveTiles);
    const material = new THREE.MeshBasicMaterial({
      color: new THREE.Color('#22d3ee'),
      transparent: true,
      opacity: 0.14,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.y = 0.024;
    mesh.userData.positionIndex = 0;
    mesh.userData.active = true;
    group.add(mesh);
    disposables.push(geometry, material);
    return { group, update: () => {} };
  }

  const entries = positions
    .filter((position) => position.tiles.length > 0)
    .map((position) => {
      const field = new THREE.Group();
      field.userData.kind = 'frontline-capture-field';
      field.userData.positionIndex = position.positionIndex;
      field.userData.active = false;
      field.userData.state = 'inactive';

      const bedGeometry = tiledInsetGeometry(position.tiles, 0.075);
      const bedMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color('#241c17'),
        transparent: true,
        opacity: 0.11,
        depthWrite: false,
      });
      const bed = new THREE.Mesh(bedGeometry, bedMaterial);
      bed.position.y = 0.014;
      field.add(bed);

      const boundaryGeometry = tileBoundaryGeometry(position.tiles, 0.075, 0.05);
      const signalGeometry = captureSignalGeometry(position.tiles);
      const signalMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color('#b8844f'),
        transparent: true,
        opacity: 0.055,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const boundary = new THREE.Mesh(boundaryGeometry, signalMaterial);
      boundary.position.y = 0.021;
      field.add(boundary);
      const signal = new THREE.Mesh(signalGeometry, signalMaterial);
      signal.position.y = 0.023;
      field.add(signal);

      group.add(field);
      disposables.push(
        bedGeometry,
        bedMaterial,
        boundaryGeometry,
        signalGeometry,
        signalMaterial,
      );
      return {
        positionIndex: position.positionIndex,
        field,
        bedMaterial,
        signalMaterial,
      };
    });

  const update = (presentation: TickPresentation, time: number) => {
    const objective =
      presentation.objective?.kind === 'frontline'
        ? presentation.objective
        : null;
    if (!objective) {
      for (const entry of entries) {
        entry.field.userData.active = false;
        entry.field.userData.state = 'inactive';
        entry.bedMaterial.opacity = 0.11;
        entry.signalMaterial.opacity = 0.055;
        entry.signalMaterial.color.set('#b8844f');
      }
      return;
    }
    const activePositionIndex = objective.activePositionIndex;
    const presentTeams = new Set(
      presentation.units
        .filter(
          (unit) => unit.status === 'active' && unit.holdingObjective,
        )
        .map((unit) => unit.teamId),
    );
    const contested = presentTeams.size > 1;
    const claimingAccent =
      objective?.claimingTeamId === null ||
      objective?.claimingTeamId === undefined
        ? null
        : presentation.units.find(
            (unit) => unit.teamId === objective.claimingTeamId,
          )?.accent ?? null;
    const solePresentTeamId =
      presentTeams.size === 1 ? [...presentTeams][0] : null;
    const presenceAccent =
      solePresentTeamId === null
        ? null
        : presentation.units.find(
            (unit) => unit.teamId === solePresentTeamId,
          )?.accent ?? null;
    const progress =
      objective.captureThreshold > 0
        ? Math.min(1, objective.captureProgress / objective.captureThreshold)
        : 0;
    const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 2.2);

    for (const entry of entries) {
      const active = entry.positionIndex === activePositionIndex;
      const state = !active
        ? 'inactive'
        : contested
          ? 'contested'
          : claimingAccent
            ? 'claiming'
            : 'neutral';
      entry.field.userData.active = active;
      entry.field.userData.state = state;
      entry.field.userData.captureProgress = active
        ? objective.captureProgress
        : 0;
      entry.bedMaterial.opacity = active ? 0.2 : 0.11;
      entry.signalMaterial.opacity = !active
        ? 0.055
        : contested
          ? 0.46 + pulse * 0.18
          : claimingAccent || presenceAccent
            ? 0.32 + progress * 0.28
            : 0.25;
      entry.signalMaterial.color.set(
        active && contested
          ? '#f4c477'
          : active && (claimingAccent || presenceAccent)
            ? (claimingAccent ?? presenceAccent)!
            : '#b8844f',
      );
    }
  };

  return { group, update };
}

/**
 * Exact spawn-pad signals for stable units without an active life.
 *
 * Rendering no chassis is the complete truth for Locked, rebuilding and Ready children:
 * none has a position yet. A ring is safe only for queued fabrication's reserved tile or
 * the Prime's authored automatic return, where the normalized contract fixes the place.
 */
function buildLifecycleCues(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (presentation: TickPresentation, time: number) => void;
} {
  const group = new THREE.Group();
  const geometry = new THREE.RingGeometry(0.23, 0.43, 28);
  geometry.rotateX(-Math.PI / 2);
  disposables.push(geometry);

  const cues = new Map<
    ReplayStableUnitKey,
    {
      mesh: THREE.Mesh;
      material: THREE.MeshBasicMaterial;
    }
  >();
  for (const unit of replay.units) {
    const material = new THREE.MeshBasicMaterial({
      color: accentForUnit(replay, unit.unitKey),
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.visible = false;
    mesh.position.y = 0.038;
    mesh.userData.unitKey = unit.unitKey;
    group.add(mesh);
    cues.set(unit.unitKey, { mesh, material });
    disposables.push(material);
  }

  const update = (presentation: TickPresentation, time: number) => {
    if (replay.initialWorld === null && replay.ticks.length === 0) {
      for (const cue of cues.values()) cue.mesh.visible = false;
      return;
    }
    for (const unit of presentation.units) {
      const cue = cues.get(unit.unitKey);
      if (!cue) continue;
      const position =
        unit.reservedSpawn ??
        lifecyclePadFor(
          replay,
          unit.teamId,
          unit.unitId,
          unit.status,
        );
      const absent = unit.actorKey === null && unit.status !== 'active';
      cue.mesh.visible = absent && position !== null;
      cue.mesh.userData.lifecycleStatus = unit.status;
      if (!cue.mesh.visible || !position) continue;

      cue.mesh.position.x = position.x + 0.5;
      cue.mesh.position.z = position.y + 0.5;
      const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 3);
      const baseOpacity =
        unit.status === 'fabrication-queued'
          ? 0.5
          : unit.status === 'ready'
            ? 0.34
            : unit.status === 'respawning'
              ? 0.24
              : unit.status === 'rebuilding'
                ? 0.16
                : 0.08;
      cue.material.opacity =
        unit.status === 'fabrication-queued'
          ? baseOpacity + pulse * 0.28
          : baseOpacity;
      cue.mesh.scale.setScalar(
        unit.status === 'fabrication-queued' ? 0.9 + pulse * 0.24 : 1,
      );
    }
  };

  return { group, update };
}

function lifecyclePadFor(
  replay: ReplayModel,
  teamId: number,
  unitId: number,
  status: string,
): ReplayPosition | null {
  const home = replay.map.frontline?.teamHomes.find(
    (candidate) => candidate.teamId === teamId,
  );
  if (!home) return null;
  // Prime return is pinned to its authored spawn. Child rebuild/Ready/Locked states
  // deliberately have no position until fabrication reserves one, so drawing them on an
  // arbitrary free pad would invent gameplay state the replay never supplied.
  return unitId === 0 && status === 'respawning'
    ? home.primeSpawn
    : null;
}

function tiledGeometry(
  tiles: readonly ReplayPosition[],
): THREE.BufferGeometry {
  const parts = tiles.map(({ x, y }) => {
    const quad = new THREE.PlaneGeometry(1, 1);
    quad.rotateX(-Math.PI / 2);
    quad.translate(x + 0.5, 0, y + 0.5);
    return quad;
  });
  const geometry = mergeQuads(parts);
  for (const part of parts) part.dispose();
  return geometry;
}

function tiledInsetGeometry(
  tiles: readonly ReplayPosition[],
  inset: number,
): THREE.BufferGeometry {
  const size = 1 - inset * 2;
  const parts = tiles.map(({ x, y }) => {
    const quad = new THREE.PlaneGeometry(size, size);
    quad.rotateX(-Math.PI / 2);
    quad.translate(x + 0.5, 0, y + 0.5);
    return quad;
  });
  const geometry = mergeQuads(parts);
  for (const part of parts) part.dispose();
  return geometry;
}

/** Exposed edges only, so a multi-tile field reads as one authored footprint. */
function tileBoundaryGeometry(
  tiles: readonly ReplayPosition[],
  inset: number,
  thickness: number,
): THREE.BufferGeometry {
  const occupied = new Set(tiles.map(({ x, y }) => `${x},${y}`));
  const parts: THREE.PlaneGeometry[] = [];
  const horizontalLength = 1 - inset * 2;
  const verticalLength = 1 - inset * 2;
  const add = (
    width: number,
    depth: number,
    x: number,
    y: number,
  ) => {
    const quad = new THREE.PlaneGeometry(width, depth);
    quad.rotateX(-Math.PI / 2);
    quad.translate(x, 0, y);
    parts.push(quad);
  };
  for (const tile of tiles) {
    if (!occupied.has(`${tile.x},${tile.y - 1}`))
      add(
        horizontalLength,
        thickness,
        tile.x + 0.5,
        tile.y + inset + thickness / 2,
      );
    if (!occupied.has(`${tile.x},${tile.y + 1}`))
      add(
        horizontalLength,
        thickness,
        tile.x + 0.5,
        tile.y + 1 - inset - thickness / 2,
      );
    if (!occupied.has(`${tile.x - 1},${tile.y}`))
      add(
        thickness,
        verticalLength,
        tile.x + inset + thickness / 2,
        tile.y + 0.5,
      );
    if (!occupied.has(`${tile.x + 1},${tile.y}`))
      add(
        thickness,
        verticalLength,
        tile.x + 1 - inset - thickness / 2,
        tile.y + 0.5,
      );
  }
  const geometry = mergeQuads(parts);
  for (const part of parts) part.dispose();
  return geometry;
}

function serviceHatchGeometry(
  tiles: readonly ReplayPosition[],
): THREE.BufferGeometry {
  const parts = tiles.flatMap(({ x, y }) =>
    [-0.055, 0.055].map((offset) => {
      const slit = new THREE.PlaneGeometry(0.28, 0.028);
      slit.rotateX(-Math.PI / 2);
      slit.translate(x + 0.5, 0, y + 0.5 + offset);
      return slit;
    }),
  );
  const geometry = mergeQuads(parts);
  for (const part of parts) part.dispose();
  return geometry;
}

function captureSignalGeometry(
  tiles: readonly ReplayPosition[],
): THREE.BufferGeometry {
  const parts = tiles.map(({ x, y }) => {
    const ring = new THREE.RingGeometry(0.19, 0.245, 6);
    ring.rotateX(-Math.PI / 2);
    ring.rotateY(Math.PI / 6);
    ring.translate(x + 0.5, 0, y + 0.5);
    return ring;
  });
  const geometry = mergeQuads(parts);
  for (const part of parts) part.dispose();
  return geometry;
}

/**
 * The flash of a shot leaving a barrel and of one landing.
 *
 * Pooled and driven by the tick's own events rather than accumulated over time, for the
 * same reason the flat renderer re-derives its impact knock every frame: playback can be
 * scrubbed, paused, or replayed from any point, and anything remembered between frames
 * would survive a jump backwards into a tick where it never happened.
 */
/**
 * A bolt arriving on something.
 *
 * The soft flare alone is light, not an event — it is also what a muzzle produces, so a hit
 * and a shot read alike. A hit is a shockwave: a hard ring thrown outwards from the point
 * of contact, fast then slowing, spent inside the tick. A kill throws two, offset in time
 * and reach, because one expanding circle reads as a bubble and two read as something
 * coming apart.
 */
function buildImpacts(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (tick: number, fraction: number) => void } {
  const group = new THREE.Group();
  // Thin relative to its radius, so the expansion reads as a wave rather than a growing disc.
  const geometry = new THREE.RingGeometry(0.78, 1, 40);
  geometry.rotateX(-Math.PI / 2);
  disposables.push(geometry);

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
      // Named, because two different things here are rings on the floor — this and a bolt
      // dissipating — and telling them apart by geometry type is a guess.
      mesh.userData.kind = 'impact';
      mesh.visible = false;
      group.add(mesh);
      pool.push(mesh);
      disposables.push(material);
    }
    return pool[index];
  };

  const hot = new THREE.Color('#fff1d0');
  const fatal = new THREE.Color('#ffb057');

  const update = (tick: number, fraction: number) => {
    let used = 0;
    for (const event of replay.ticks[tick]?.events ?? []) {
      const killing = isDestructionEvent(event.type);
      if (event.type !== 'damage' && !killing) continue;
      // Where the hit landed. The model records that in `from`, which is what the flat
      // renderer has always drawn its impact at.
      const at = event.from;
      if (!at) continue;

      // Impacts land late in the tick, on the same 0.6 the hit flash and the camera knock
      // use, so the whole reaction to a bolt arriving happens at one instant.
      const since = (fraction - 0.6) / 0.4;
      if (since < 0 || since > 1) continue;

      for (const wave of killing ? [0, 0.35] : [0]) {
        const age = (since - wave) / (1 - wave);
        if (age < 0 || age > 1) continue;
        const mesh = borrow(used++);
        mesh.visible = true;
        mesh.position.set(at.x + 0.5, 0.06, at.y + 0.5);
        // Fast at first and slowing, which is what a shockwave does and what a linear
        // expansion conspicuously does not.
        mesh.scale.setScalar(0.25 + (killing ? 2.6 : 1.35) * Math.sqrt(age));
        const material = mesh.material as THREE.MeshBasicMaterial;
        material.color.copy(killing ? fatal : hot);
        material.opacity = (1 - age) ** 1.8 * (killing ? 0.95 : 0.8);
      }
    }
    for (let index = used; index < pool.length; index++) pool[index].visible = false;
  };

  return { group, update };
}

/** Which way a guard was pointing, as a rotation about the vertical axis. */
const GUARD_FACING: Record<string, number> = {
  east: 0,
  south: -Math.PI / 2,
  west: Math.PI,
  north: Math.PI / 2,
};

/**
 * A bolt dying on an aegis shell.
 *
 * Everything else that stops a projectile here expands: an impact throws a shockwave, a
 * kill throws two, a spent bolt dissipates outward. This one must not, because the whole
 * content of the event is that *nothing was transferred* — so the guarded quadrant simply
 * rings, at its own radius, and goes out. It is also drawn on the defender's facing rather
 * than on the bolt's bearing, which makes every absorption a restatement of where the
 * shield is and, by omission, where it is not.
 *
 * The bolt gets its own end: a small hard flare exactly on the contact tile, so it reads
 * as having died there rather than as having been forgotten by the renderer.
 *
 * The slate's deflection ruling would invert the sentence: a shell that launches a
 * team-flipped bolt back has not nullified anything. The return bolt needs no work here —
 * it is an ordinary projectile owned by the guard — but the contact flare would have to
 * read as a bounce rather than a stop. See the flat renderer's `drawAbsorption`.
 */
function buildAbsorptions(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (tick: number, fraction: number) => void } {
  const group = new THREE.Group();
  // Exactly the quadrant the guard covers, at the radius the plate stands on.
  const geometry = new THREE.RingGeometry(0.42, 0.94, 24, 1, -Math.PI / 4, Math.PI / 2);
  geometry.rotateX(-Math.PI / 2);
  const sparkGeometry = new THREE.RingGeometry(0.04, 0.2, 16);
  sparkGeometry.rotateX(-Math.PI / 2);
  disposables.push(geometry, sparkGeometry);

  const arcs: THREE.Mesh[] = [];
  const sparks: THREE.Mesh[] = [];
  const borrow = (pool: THREE.Mesh[], shape: THREE.BufferGeometry, index: number, cue: string) => {
    while (pool.length <= index) {
      const material = new THREE.MeshBasicMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(shape, material);
      mesh.userData.cue = cue;
      mesh.visible = false;
      group.add(mesh);
      pool.push(mesh);
      disposables.push(material);
    }
    return pool[index];
  };

  const ring = new THREE.Color('#e6f6ff');

  const update = (tick: number, fraction: number) => {
    let usedArcs = 0;
    let usedSparks = 0;
    const current = replay.ticks[tick];
    for (const event of current?.events ?? []) {
      if (event.type !== 'projectile-deflected') continue;
      // The same late-tick window every other contact uses, so an absorption and a hit
      // landing on the same tick happen at the same instant.
      const since = (fraction - 0.6) / 0.4;
      if (since < 0 || since > 1) continue;
      const contact = event.to ?? event.from;
      const guard = event.targetActor;
      const at =
        guard === undefined || guard === null
          ? null
          : ((current?.after.actors ?? []).find(
              (actor) => actor.actorKey === guard.actorKey,
            )?.position ??
            (current?.before.actors ?? []).find(
              (actor) => actor.actorKey === guard.actorKey,
            )?.position ??
            null);

      if (at && event.toFacing !== null) {
        const arc = borrow(arcs, geometry, usedArcs++, 'deflect-arc');
        arc.visible = true;
        arc.position.set(at.x + 0.5, 0.07, at.y + 0.5);
        arc.rotation.y = GUARD_FACING[event.toFacing] ?? 0;
        // It rings; it does not grow. A hair of swell only, so the plate reads as struck
        // rather than as a second shockwave.
        arc.scale.setScalar(1 + since * 0.09);
        const material = arc.material as THREE.MeshBasicMaterial;
        material.color
          .copy(ring)
          .lerp(
            new THREE.Color(
              guard ? accentForUnit(replay, guard.unitKey) : '#22d3ee',
            ),
            since,
          );
        material.opacity = (1 - since) ** 1.4 * 0.95;
      }

      if (contact) {
        // The handoff: the spark leaves the plate back along the reversed
        // approach while its color flips from the shooter's accent to the
        // guard's — the ownership flip, said in paint. The return bolt itself
        // is an ordinary projectile the pipeline already draws.
        const spark = borrow(sparks, sparkGeometry, usedSparks++, 'deflect-contact');
        spark.visible = true;
        const approach = event.from;
        const hasRun =
          approach && (approach.x !== contact.x || approach.y !== contact.y);
        const run = hasRun ? since * 0.6 : 0;
        const dx = hasRun ? Math.sign(approach.x - contact.x) : 0;
        const dy = hasRun ? Math.sign(approach.y - contact.y) : 0;
        spark.position.set(
          contact.x + 0.5 + dx * run,
          PROJECTILE_HOVER,
          contact.y + 0.5 + dy * run,
        );
        spark.scale.setScalar(1.5 * (1 - since * 0.55));
        const material = spark.material as THREE.MeshBasicMaterial;
        material.color.set(
          event.sourceActor
            ? accentForUnit(replay, event.sourceActor.unitKey)
            : '#22d3ee',
        );
        if (guard) {
          material.color.lerp(
            new THREE.Color(accentForUnit(replay, guard.unitKey)),
            since,
          );
        }
        material.opacity = (1 - since) ** 2 * 0.9;
      }
    }
    for (let index = usedArcs; index < arcs.length; index++)
      arcs[index].visible = false;
    for (let index = usedSparks; index < sparks.length; index++)
      sparks[index].visible = false;
  };

  return { group, update };
}

function buildFlashes(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (tick: number, fraction: number) => void } {
  const group = new THREE.Group();
  const geometry = new THREE.PlaneGeometry(1, 1);
  geometry.rotateX(-Math.PI / 2);
  disposables.push(geometry);

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
      mesh.userData.cue = 'event-flash';
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
        isAttackEvent(event.type) && event.from
          ? {
              position: event.from,
              colour: new THREE.Color(
                event.sourceActor
                  ? accentForUnit(replay, event.sourceActor.unitKey)
                  : '#22d3ee',
              ),
              size: 1.5,
              life: 0.45,
            }
          : (event.type === 'damage' ||
                isDestructionEvent(event.type)) &&
              (event.to ?? event.from)
            ? {
                // Replay-v2 carries the impact tile in `to`; normalized replay-v1
                // historically carries the same authoritative tile in `from`.
                position: event.to ?? event.from!,
                colour: impact,
                size: 2.4,
                life: 0.8,
              }
            : null;
      if (!flash) continue;

      // Bright at the instant it happens and gone by the end of its life, so a shot reads
      // as an event rather than a lamp that switches on for the tick.
      const decay = 1 - Math.min(1, fraction / flash.life);
      if (decay <= 0) continue;

      const mesh = borrow(used++);
      mesh.visible = true;
      mesh.userData.eventType = event.type;
      mesh.position.set(
        flash.position.x + 0.5,
        0.05,
        flash.position.y + 0.5,
      );
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
      material.color.set(
        accentForUnit(replay, bolt.ownerActor.unitKey),
      );
      material.opacity = (1 - bolt.age) ** 2 * 0.9;
    }
    for (let index = used; index < pool.length; index++) pool[index].visible = false;
  };

  return { group, update };
}

/**
 * A life materializing — the arena's one effect that runs *inward*.
 *
 * Everything else here expands. An impact throws a shockwave, a kill throws two, a spent
 * bolt dissipates. That vocabulary is consistent and it means one thing: something came
 * apart here. An arrival is the opposite sentence, so it is drawn as the opposite motion —
 * a wide ring closing onto the pad and landing as a flash under a body that is scaling up
 * out of the floor (`arenaActors` owns that half). Nothing about it is borrowed from a
 * destruction, which is exactly why the two can never be confused at a glance.
 *
 * It reads doubly under a forward rally, where fabricated bodies arrive at the front line
 * rather than safely behind it: without this, a machine simply exists mid-fight one frame
 * after it did not.
 */
function buildArrivals(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (time: number) => void } {
  const group = new THREE.Group();
  // Thick relative to its radius, so a *closing* ring reads as material arriving rather
  // than as the thin wave an impact throws off.
  const geometry = new THREE.RingGeometry(0.62, 1, 40);
  geometry.rotateX(-Math.PI / 2);
  const coreGeometry = new THREE.RingGeometry(0, 0.5, 24);
  coreGeometry.rotateX(-Math.PI / 2);
  disposables.push(geometry, coreGeometry);

  const rings: THREE.Mesh[] = [];
  const cores: THREE.Mesh[] = [];
  const borrow = (
    pool: THREE.Mesh[],
    shape: THREE.BufferGeometry,
    index: number,
    cue: string,
  ) => {
    while (pool.length <= index) {
      const material = new THREE.MeshBasicMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(shape, material);
      mesh.userData.cue = cue;
      mesh.visible = false;
      group.add(mesh);
      pool.push(mesh);
      disposables.push(material);
    }
    return pool[index];
  };

  const update = (time: number) => {
    let usedRings = 0;
    let usedCores = 0;
    for (const arrival of arrivalsAt(replay, time)) {
      const accent = accentForUnit(replay, arrival.unitKey);
      // Fast then settling, the same curve the flat renderer condenses on.
      const closing = 1 - (1 - arrival.age) ** 3;

      const ring = borrow(rings, geometry, usedRings++, 'arrival');
      ring.visible = true;
      ring.position.set(arrival.x + 0.5, 0.07, arrival.y + 0.5);
      ring.scale.setScalar(2.3 - 1.75 * closing);
      // Turning as it closes, so the collapse has a direction rather than merely a size.
      ring.rotation.y = closing * Math.PI * 0.6;
      const ringMaterial = ring.material as THREE.MeshBasicMaterial;
      ringMaterial.color.set(accent);
      ringMaterial.opacity = 0.25 + 0.65 * closing;

      const landing = Math.max(0, (arrival.age - 0.55) / 0.45);
      if (landing <= 0) continue;
      const core = borrow(cores, coreGeometry, usedCores++, 'arrival-landing');
      core.visible = true;
      core.position.set(arrival.x + 0.5, 0.05, arrival.y + 0.5);
      const bloom = Math.sin(landing * Math.PI);
      core.scale.setScalar(1.1 + 0.6 * bloom);
      const coreMaterial = core.material as THREE.MeshBasicMaterial;
      coreMaterial.color.set(accent);
      coreMaterial.opacity = 0.7 * bloom;
    }
    for (let index = usedRings; index < rings.length; index++)
      rings[index].visible = false;
    for (let index = usedCores; index < cores.length; index++)
      cores[index].visible = false;
  };

  return { group, update };
}

function accentForUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
): string {
  return unitAccent(replay, unitKey);
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
