import { arcOriginAccent } from '../replayPresentation';
import * as THREE from 'three';
import { SCRAP_ACCENT } from '../presentation/scrapAccent';
import type {
  ReplayActorIdentity,
  ReplayArcRelayFact,
  ReplayModel,
  ReplayPosition,
  ReplayStableUnitKey,
} from '../replayModel';
import { isAttackEvent, isDestructionEvent } from '../replayModel';
import { frontlineCaptureVisual } from '../render/frontlineCaptureVisual';
import {
  arrivalsAt,
  directionAngle,
  posesAt,
  spentBoltsAt,
  strikeAimsAt,
  strikeSlashesAt,
} from '../render/interpolate';
import { PROJECTILE_HOVER } from './arenaActors';
import { unitAccent } from '../render/unitPresentation';
import {
  createPresenter,
  type TickPresentation,
} from '../replayPresentation';
import { STATE_SETTLES_AT, settledStateTick } from '../render/tickClock';
import type { ViewerEntrantPresentation } from '../components/Viewer';
import { buildArcRelayEffects } from './arcRelayEffects';
import {
  allUnitVisibleTilesAt,
  teamVisionAt,
  teamVisionSeesActor,
} from '../render/teamVision';
import { playsAt } from '../presentation/playAwareness';
import { ARC_CORE_NEUTRAL_PALETTE } from '../presentation/arcCorePalette';
import { ARC_SIGNATURE_PRESENTATION_ID } from '../render/arcSignaturePresentationId';
import { ARC_TOSS_RELEASE_TICK_OFFSET } from '../render/signatures/definitions/arcToss';
import { arenaTheme } from '../render/arenaThemes';
import { objectiveModel } from './lookModel';
import { isolateModelFromPointLights } from './modelLighting';
import { configureWalkableObjectiveRendering } from './occlusionLayers';

/**
 * What the arena knows and what it is doing to itself: fog of war, the objective zone, and
 * the flash of a shot landing.
 *
 * These are the parts of the flat renderer that are *not* objects — it draws them by
 * compositing over the finished frame, which a scene graph cannot do. Each becomes a thing
 * in the world instead, which is mostly an improvement (a flash lights the floor it is on)
 * and in one place a compromise, noted where it happens.
 */

/** How far the camera is thrown by a direct kill, in tiles. */
const SHAKE_REACH = 0.045;

function disposeObjectMaterials(object: THREE.Object3D): void {
  const materials = new Set<THREE.Material>();
  object.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    for (const material of Array.isArray(mesh.material) ? mesh.material : [mesh.material])
      materials.add(material);
  });
  for (const material of materials) material.dispose();
}


export interface ArenaOverlays {
  group: THREE.Group;
  update: (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
    highlightedUnitKeys?: readonly ReplayStableUnitKey[],
    /** Master fog switch. Off leaves the arena unmasked entirely. */
    showFog?: boolean,
  ) => void;
  /** Camera offset for the knock of an impact at this instant. */
  shake: (time: number) => { x: number; y: number };
  dispose: () => void;
}

export function buildOverlays(
  replay: ReplayModel,
  entrants?: readonly ViewerEntrantPresentation[],
  terrain?: THREE.Object3D,
): ArenaOverlays {
  const mapWidth = replay.map.width;
  const mapHeight = replay.map.height;
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];
  const presenter = createPresenter(replay);
  const theme = arenaTheme(replay.map.presentation?.themeId ?? undefined);

  const fog = buildFog(
    mapWidth,
    mapHeight,
    theme.environment3d.fog.unseenTerrainDarkness,
    theme.environment3d.fog.visibleTerrainLift,
    disposables,
  );
  group.add(fog.mesh);
  if (terrain) applyTerrainFog(terrain, fog);

  const spawnPads = buildSpawnPads(replay, theme.id, disposables);
  group.add(spawnPads.group);

  const objective = buildObjective(replay, disposables);
  group.add(objective.group);

  const arcRelay = buildArcRelayStory(
    replay,
    theme.id,
    disposables,
    entrants,
  );
  group.add(arcRelay.group);

  const arcRelayEffects = buildArcRelayEffects(replay, disposables);
  group.add(arcRelayEffects.group);

  const playAwareness = buildPlayAwareness(replay, disposables);
  group.add(playAwareness.group);

  const scrap = buildScrapPiles(disposables);
  group.add(scrap.group);

  const lifecycle = buildLifecycleCues(replay, disposables);
  group.add(lifecycle.group);

  const flashes = buildFlashes(replay, disposables);
  group.add(flashes.group);

  const impacts = buildImpacts(replay, disposables);
  group.add(impacts.group);

  const spent = buildSpentBolts(replay, disposables);
  group.add(spent.group);

  const strikeSlashes = buildStrikeSlashes(replay, disposables);
  group.add(strikeSlashes.group);

  const strikeAims = buildStrikeAims(replay, disposables);
  group.add(strikeAims.group);

  const absorptions = buildAbsorptions(replay, disposables);
  group.add(absorptions.group);

  const arrivals = buildArrivals(replay, disposables);
  group.add(arrivals.group);

  // Last in, so the lens draws over the story it is explaining rather than
  // under it. It is instrumentation: when it is on, it wins.
  const pathingLens = buildPathingLens(replay, disposables);
  group.add(pathingLens.group);

  let painted = '';

  const update = (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
    highlightedUnitKeys: readonly ReplayStableUnitKey[] = [],
    showFog = true,
  ) => {
    const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const fraction = Math.max(0, Math.min(time - tick, 1));
    const presentation = presenter.at(tick);
    // Possession is the one part of this that rides a moving hull, so it is the one part
    // that needs the hull's clock: end-of-tick state read at the start of a glide hands a
    // Core over before its carrier has arrived. Static furniture -- pads, objective,
    // lifecycle marks -- is not attached to anything in flight and stays on this tick.
    const settled = presenter.at(
      settledStateTick(tick, fraction, replay.ticks.length),
    );

    const teamVision = teamVisionAt(
      replay,
      replay.ticks[tick],
      selectedUnitKey,
      showVisibility,
    );

    // With no selected perspective, the whole terrain follows the union of every unit's
    // published sight. Selecting a body narrows that same mask to its team; neither path
    // invents visibility geometry in the renderer.
    //
    // `showFog` is a master switch over both of those, not a third perspective: it asks
    // whether the arena is masked at all. Turning it off is the reviewer's floodlight —
    // useful for reading geometry and traffic, and deliberately not the default, because
    // the honest view of a match is the one its bots actually had.
    const terrainVision = !showFog
      ? null
      : teamVision?.visibleTiles ?? allUnitVisibleTilesAt(replay.ticks[tick]);

    // Repainting the mask is a loop over every tile on the map, and the playhead crosses
    // dozens of frames per tick — so it only happens when the answer can have changed.
    // The switch is part of the signature: flipping it must repaint even mid-tick.
    const signature = !showFog
      ? 'no-fog'
      : terrainVision === null
      ? 'unpublished'
      : `${tick}:${teamVision ? `team-${teamVision.teamId}` : 'all-units'}`;
    if (signature !== painted) {
      painted = signature;
      fog.paint(
        terrainVision
          ? [...terrainVision].map((key) => {
              const [x, y] = key.split(',').map(Number);
              return { x: x!, y: y! };
            })
          : undefined,
        terrainVision !== null,
      );
    }

    spawnPads.update(presentation, time);
    objective.update(presentation, time);
    arcRelay.update(settled, time);
    arcRelayEffects.update(time);
    playAwareness.update(
      time,
      teamVision,
      highlightedUnitKeys,
    );
    scrap.update(presentation, time);
    lifecycle.update(presentation, time);
    flashes.update(tick, fraction);
    impacts.update(tick, fraction);
    spent.update(time);
    strikeSlashes.update(time);
    strikeAims.update(time);
    absorptions.update(tick, fraction);
    arrivals.update(time);
    pathingLens.update(time, presentation, selectedUnitKey);
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
 * Published coordinated-play brackets and sigils on the ground plane.
 * Operation identity/phase comes only from the entrant's bounded role tag;
 * contact comes only from an authoritative event at the current tick.
 */
function buildPlayAwareness(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (
    time: number,
    vision: ReturnType<typeof teamVisionAt>,
    highlightedUnitKeys: readonly ReplayStableUnitKey[],
  ) => void;
} {
  const group = new THREE.Group();
  group.userData.kind = 'play-awareness';
  const bracketGeometry = new THREE.BufferGeometry();
  const points: number[] = [];
  const inner = 0.29;
  const outer = 0.47;
  for (const [sx, sz] of [
    [-1, -1], [1, -1], [1, 1], [-1, 1],
  ] as const) {
    points.push(
      sx * inner, 0, sz * outer,
      sx * outer, 0, sz * outer,
      sx * outer, 0, sz * outer,
      sx * outer, 0, sz * inner,
    );
  }
  bracketGeometry.setAttribute(
    'position',
    new THREE.Float32BufferAttribute(points, 3),
  );
  const sigilGeometry = new THREE.RingGeometry(0.1, 0.145, 4, 1, Math.PI / 4);
  sigilGeometry.rotateX(-Math.PI / 2);
  const contactGeometry = new THREE.RingGeometry(0.32, 0.37, 36);
  contactGeometry.rotateX(-Math.PI / 2);
  disposables.push(bracketGeometry, sigilGeometry, contactGeometry);

  const brackets = Array.from({ length: 16 }, () => {
    const material = new THREE.LineBasicMaterial({
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    const line = new THREE.LineSegments(bracketGeometry, material);
    line.visible = false;
    line.position.y = 0.072;
    group.add(line);
    disposables.push(material);
    return { line, material };
  });
  const sigils = Array.from({ length: 4 }, () => {
    const material = new THREE.MeshBasicMaterial({
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const mesh = new THREE.Mesh(sigilGeometry, material);
    mesh.visible = false;
    mesh.position.y = 0.078;
    group.add(mesh);
    disposables.push(material);
    return { mesh, material };
  });
  const contacts = Array.from({ length: 4 }, () => {
    const material = new THREE.MeshBasicMaterial({
      color: '#eff6fa',
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const mesh = new THREE.Mesh(contactGeometry, material);
    mesh.visible = false;
    mesh.position.y = 0.084;
    group.add(mesh);
    disposables.push(material);
    return { mesh, material };
  });

  const update = (
    time: number,
    vision: ReturnType<typeof teamVisionAt>,
    highlightedUnitKeys: readonly ReplayStableUnitKey[],
  ) => {
    for (const entry of brackets) entry.line.visible = false;
    for (const entry of sigils) entry.mesh.visible = false;
    for (const entry of contacts) entry.mesh.visible = false;
    const tickIndex = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const poses = posesAt(replay, time);
    const selected = new Set(highlightedUnitKeys);
    const frame = playsAt(replay, tickIndex);
    const primary = new Map<number, string>();
    for (const play of frame)
      if (!primary.has(play.teamId)) primary.set(play.teamId, play.activationKey);
    let bracketIndex = 0;
    let sigilIndex = 0;
    let contactIndex = 0;

    for (const play of frame) {
      const visible = play.participants
        .map((participant) => poses.find((pose) =>
          pose.actorKey === participant.actorKey &&
          pose.status === 'active' &&
          teamVisionSeesActor(vision, pose),
        ))
        .filter((pose): pose is NonNullable<typeof pose> => Boolean(pose));
      if (visible.length === 0) continue;
      const accent = unitAccent(replay, visible[0]!.unitKey);
      const selectedPlay = visible.some((pose) => selected.has(pose.unitKey));
      const full = selectedPlay || primary.get(play.teamId) === play.activationKey;
      const age = Math.max(0, time - play.phaseStartedTick);
      if (full) {
        for (const pose of visible) {
          const slot = brackets[bracketIndex++];
          if (!slot) break;
          slot.line.visible = true;
          slot.line.position.set(pose.x + 0.5, 0.072, pose.y + 0.5);
          const outward = play.phase === 'recovery'
            ? 1 + Math.min(0.32, age * 0.07)
            : 1;
          slot.line.scale.setScalar(outward);
          slot.material.color.set(accent);
          slot.material.opacity = play.phase === 'preparing'
            ? 0.42 + 0.12 * Math.sin(time * Math.PI * 2)
            : play.phase === 'recovery'
              ? Math.max(0.18, 0.72 - age * 0.12)
              : selectedPlay ? 0.96 : 0.78;
        }
      }

      const sigil = sigils[sigilIndex++];
      if (sigil) {
        sigil.mesh.visible = true;
        sigil.mesh.position.set(
          visible.reduce((total, pose) => total + pose.x + 0.5, 0) / visible.length,
          0.078,
          visible.reduce((total, pose) => total + pose.y + 0.5, 0) / visible.length,
        );
        sigil.mesh.scale.setScalar(full ? 1 : 0.68);
        sigil.material.color.set(accent);
        sigil.material.opacity = play.phase === 'committed' && age < 1
          ? 0.92
          : full ? 0.68 : 0.38;
      }

      if (play.contact) {
        const contact = contacts[contactIndex++];
        if (!contact) continue;
        const event = replay.ticks[tickIndex]?.events.find(
          (candidate) => candidate.eventId === play.contact!.eventId,
        );
        const position = event?.to ?? event?.from;
        contact.mesh.visible = true;
        contact.mesh.position.set(
          position?.x ?? visible.reduce((sum, pose) => sum + pose.x, 0) / visible.length + 0.5,
          0.084,
          position?.y ?? visible.reduce((sum, pose) => sum + pose.y, 0) / visible.length + 0.5,
        );
        const fraction = Math.max(0, Math.min(time - tickIndex, 1));
        contact.mesh.scale.setScalar(1 + fraction * 1.5);
        contact.material.opacity = (1 - fraction) * 0.9;
      }
    }
  };

  return { group, update };
}

/** Arc Relay names broad home regions instead of carrying Frontline's home-pad view. */
function arcRelayHomes(
  replay: ReplayModel,
): NonNullable<ReplayModel['map']['frontline']>['teamHomes'] {
  if (replay.contract.kind !== 'v3-generic' ||
    replay.contract.modeKind !== 'arc-relay')
    return [];
  return replay.map.regions
    .filter((region) => region.regionId.startsWith('home-'))
    .flatMap((region) => {
      const keys = new Set(region.tiles.map((tile) => `${tile.x}:${tile.y}`));
      const spawn = replay.contract.map.spawns.find((candidate) =>
        keys.has(`${candidate.position.x}:${candidate.position.y}`),
      );
      if (!spawn) return [];
      return [{
        teamId: spawn.teamId,
        primeSpawn: { ...spawn.position, facing: spawn.facing },
        protectedSpawnPad: region.tiles,
      }];
    });
}

/**
 * Fog as one shared texture with two spectator scopes.
 *
 * One texture with a texel per tile, laid over the arena and resampled smoothly, rather
 * than a quad per hidden tile — the whole mask is one draw call and one upload per tick,
 * and the linear filter gives the soft boundary the flat renderer gets from a blur.
 *
 * With a unit selected, floor and raised terrain sample the same team mask from fragment
 * world position. Without a selection, both sample the cross-team union: any active unit
 * can light terrain, while unseen ground remains substantially fogged. The horizontal
 * plane remains only as a fallback for callers that do not supply terrain; lifting it
 * above walls would shift its mask through the MOBA camera and slice the wrong geometry.
 */
function buildFog(
  mapWidth: number,
  mapHeight: number,
  unseenTerrainDarkness: number,
  visibleTerrainLift: number,
  disposables: { dispose: () => void }[],
): {
  mesh: THREE.Mesh;
  paint: (
    visible: readonly ReplayPosition[] | undefined,
    floorFogged: boolean,
  ) => void;
  texture: THREE.DataTexture;
  enabled: { value: number };
  mapSize: THREE.Vector2;
  darkness: { value: number };
  visibleLift: { value: number };
  floorCovered: { value: boolean };
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
    opacity: unseenTerrainDarkness,
    transparent: true,
    depthWrite: false,
  });
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.y = 0.03;
  mesh.visible = false;
  disposables.push(geometry, material, texture);
  const enabled = { value: 0 };
  const mapSize = new THREE.Vector2(mapWidth, mapHeight);
  const darkness = { value: unseenTerrainDarkness };
  const visibleLift = { value: visibleTerrainLift };
  const floorCovered = { value: false };

  const paint = (
    visible: readonly ReplayPosition[] | undefined,
    floorFogged: boolean,
  ) => {
    mesh.visible =
      !floorCovered.value && floorFogged && visible !== undefined;
    enabled.value = visible === undefined ? 0 : 1;
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
        // Alpha is the authoritative unseen fraction, independent of presentation
        // strength. The plane and terrain shader apply the theme-owned darkness.
        data[texel + 3] = seen.has(`${x},${y}`) ? 0 : 255;
      }
    }
    texture.needsUpdate = true;
  };

  return {
    mesh,
    paint,
    texture,
    enabled,
    mapSize,
    darkness,
    visibleLift,
    floorCovered,
  };
}

type FogMask = ReturnType<typeof buildFog>;

/** Raised terrain roots whose descendants receive the team-vision mask. */
function isFoggedTerrain(node: THREE.Object3D): boolean {
  const kind = node.userData.kind;
  return typeof kind === 'string' &&
    (kind === 'arena-floor' ||
      kind.startsWith('arena-wall-') ||
      kind.startsWith('arena-woodland-'));
}

/**
 * Teach every raised-terrain material to read the same one-texel-per-tile mask as the
 * floor plane. Instanced trees need their instance transform applied before world-space
 * sampling; sampling object coordinates would fog every tree from the origin tile.
 */
function applyTerrainFog(root: THREE.Object3D, fog: FogMask): void {
  const materials = new Set<THREE.Material>();
  const visit = (node: THREE.Object3D, inherited: boolean) => {
    const fogged = inherited || isFoggedTerrain(node);
    if (fogged && node instanceof THREE.Mesh) {
      if (node.userData.kind === 'arena-floor') fog.floorCovered.value = true;
      const nodeMaterials = Array.isArray(node.material)
        ? node.material
        : [node.material];
      for (const material of nodeMaterials) materials.add(material);
    }
    for (const child of node.children) visit(child, fogged);
  };
  visit(root, false);

  for (const material of materials) {
    const beforeCompile = material.onBeforeCompile.bind(material);
    const priorCacheKey = material.customProgramCacheKey.bind(material);
    material.onBeforeCompile = (shader, renderer) => {
      beforeCompile(shader, renderer);
      shader.uniforms.arenaVisionFogMap = { value: fog.texture };
      shader.uniforms.arenaVisionFogEnabled = fog.enabled;
      shader.uniforms.arenaVisionFogMapSize = { value: fog.mapSize };
      shader.uniforms.arenaVisionFogDarkness = fog.darkness;
      shader.uniforms.arenaVisionVisibleLift = fog.visibleLift;
      shader.vertexShader = shader.vertexShader
        .replace(
          'void main() {',
          'varying vec3 vArenaVisionFogWorldPosition;\nvoid main() {',
        )
        .replace(
          '#include <project_vertex>',
          `vec4 arenaVisionFogPosition = vec4( transformed, 1.0 );
#ifdef USE_BATCHING
  arenaVisionFogPosition = batchingMatrix * arenaVisionFogPosition;
#endif
#ifdef USE_INSTANCING
  arenaVisionFogPosition = instanceMatrix * arenaVisionFogPosition;
#endif
vArenaVisionFogWorldPosition = ( modelMatrix * arenaVisionFogPosition ).xyz;
#include <project_vertex>`,
        );
      shader.fragmentShader = shader.fragmentShader
        .replace(
          'void main() {',
          `uniform sampler2D arenaVisionFogMap;
uniform float arenaVisionFogEnabled;
uniform vec2 arenaVisionFogMapSize;
uniform float arenaVisionFogDarkness;
uniform float arenaVisionVisibleLift;
varying vec3 vArenaVisionFogWorldPosition;
void main() {`,
        )
        .replace(
          '#include <opaque_fragment>',
          `#include <opaque_fragment>
vec2 arenaVisionFogUv = clamp(
  vec2(
    vArenaVisionFogWorldPosition.x / arenaVisionFogMapSize.x,
    1.0 - vArenaVisionFogWorldPosition.z / arenaVisionFogMapSize.y
  ),
  vec2(0.0),
  vec2(1.0)
);
float arenaVisionFogMask = texture2D(
  arenaVisionFogMap,
  arenaVisionFogUv
).a;
float arenaVisionFogUnseen = arenaVisionFogMask * arenaVisionFogEnabled;
float arenaVisionFogSeen =
  (1.0 - arenaVisionFogMask) * arenaVisionFogEnabled;
gl_FragColor.rgb *=
  (1.0 - arenaVisionFogDarkness * arenaVisionFogUnseen) *
  (1.0 + arenaVisionVisibleLift * arenaVisionFogSeen);`,
        );
    };
    material.customProgramCacheKey = () =>
      `${priorCacheKey()}:arena-vision-fog-v2`;
    material.userData.arenaVisionFog = fog;
    material.needsUpdate = true;
  }
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
  themeId: string,
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (presentation: TickPresentation, time: number) => void;
} {
  const group = new THREE.Group();
  const arcRelayPad = replay.map.frontline === null &&
    replay.contract.kind === 'v3-generic' &&
    replay.contract.modeKind === 'arc-relay';
  group.userData.kind = arcRelayPad
    ? 'arc-relay-spawn-pads'
    : 'frontline-spawn-pads';
  const homes = replay.map.frontline?.teamHomes ?? arcRelayHomes(replay);
  const emberForge = themeId === 'ember-forge';
  const lumenBastion = themeId === 'lumen-bastion';
  const arcArchitecture = emberForge || lumenBastion;
  const spawnSocketGeometry = arcRelayPad && arcArchitecture
    ? new THREE.CylinderGeometry(0.25, 0.32, 0.065, 8)
    : null;
  const spawnSocketPlateGeometry = arcRelayPad && arcArchitecture
    ? new THREE.CylinderGeometry(0.19, 0.22, 0.024, 8)
    : null;
  const spawnSocketSignalGeometry = arcRelayPad && arcArchitecture
    ? new THREE.BoxGeometry(0.16, 0.018, 0.035)
    : null;
  if (
    spawnSocketGeometry &&
    spawnSocketPlateGeometry &&
    spawnSocketSignalGeometry
  )
    disposables.push(
      spawnSocketGeometry,
      spawnSocketPlateGeometry,
      spawnSocketSignalGeometry,
    );
  const entries = homes
    .filter((home) => home.protectedSpawnPad.length > 0)
    .map((home) => {
      const pad = new THREE.Group();
      pad.userData.kind = arcRelayPad
        ? 'arc-relay-spawn-pad'
        : 'frontline-spawn-pad';
      pad.userData.themeId = themeId;
      pad.userData.teamId = home.teamId;

      const bedGeometry = tiledInsetGeometry(home.protectedSpawnPad, 0.055);
      const bedMaterial = new THREE.MeshBasicMaterial({
        color: emberForge ? '#241d20' : '#211b18',
        transparent: true,
        opacity: emberForge ? 0.56 : 0.34,
        depthWrite: false,
      });
      const bed = new THREE.Mesh(bedGeometry, bedMaterial);
      bed.position.y = 0.012;
      if (!arcRelayPad) pad.add(bed);

      const sealGeometry = tileBoundaryGeometry(
        home.protectedSpawnPad,
        0.055,
        0.045,
      );
      const sealMaterial = new THREE.MeshBasicMaterial({
        color: emberForge ? '#a63b4b' : '#b27a43',
        transparent: true,
        opacity: emberForge ? 0.34 : 0.23,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const seal = new THREE.Mesh(sealGeometry, sealMaterial);
      seal.position.y = 0.018;
      if (!arcRelayPad) pad.add(seal);

      const hatchGeometry = serviceHatchGeometry(home.protectedSpawnPad);
      const hatchMaterial = new THREE.MeshBasicMaterial({
        color: emberForge ? '#d08b54' : '#b27a43',
        transparent: true,
        opacity: emberForge ? 0.23 : 0.14,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const hatches = new THREE.Mesh(hatchGeometry, hatchMaterial);
      hatches.position.y = 0.02;
      if (!arcRelayPad) pad.add(hatches);

      // Arc Relay returns each stable slot to an authored anchor within its
      // broader home. Hard-surface themes make those exact anchors physical:
      // low octagonal sockets without outlining the broad home region. One
      // small directional status lamp carries team identity.
      let socketSignalMaterial: THREE.MeshStandardMaterial | null = null;
      if (
        spawnSocketGeometry &&
        spawnSocketPlateGeometry &&
        spawnSocketSignalGeometry
      ) {
        const homeKeys = new Set(
          home.protectedSpawnPad.map((tile) => `${tile.x}:${tile.y}`),
        );
        const socketBodyMaterial = new THREE.MeshStandardMaterial({
          color: lumenBastion ? '#d7dbe1' : '#2c2c31',
          roughness: 0.64,
          metalness: lumenBastion ? 0.28 : 0.72,
        });
        const socketPlateMaterial = new THREE.MeshStandardMaterial({
          color: lumenBastion ? '#f0f1f3' : '#494348',
          roughness: 0.72,
          metalness: lumenBastion ? 0.16 : 0.58,
        });
        socketSignalMaterial = new THREE.MeshStandardMaterial({
          color: '#a63b4b',
          emissive: '#a63b4b',
          emissiveIntensity: 0.72,
          roughness: 0.48,
          metalness: 0.68,
        });
        for (const spawn of replay.contract.map.spawns.filter(
          (candidate) => candidate.teamId === home.teamId &&
            homeKeys.has(`${candidate.position.x}:${candidate.position.y}`),
        )) {
          const socket = new THREE.Group();
          socket.userData.kind = 'arc-relay-spawn-socket';
          socket.userData.teamId = home.teamId;
          socket.position.set(
            spawn.position.x + 0.5,
            0.058,
            spawn.position.y + 0.5,
          );
          const body = new THREE.Mesh(spawnSocketGeometry, socketBodyMaterial);
          body.castShadow = true;
          body.receiveShadow = true;
          const plate = new THREE.Mesh(
            spawnSocketPlateGeometry,
            socketPlateMaterial,
          );
          plate.position.y = 0.043;
          const signal = new THREE.Mesh(
            spawnSocketSignalGeometry,
            socketSignalMaterial,
          );
          signal.position.set(0, 0.062, -0.12);
          socket.rotation.y = directionAngle(spawn.facing);
          socket.add(body, plate, signal);
          pad.add(socket);
        }
        disposables.push(
          socketBodyMaterial,
          socketPlateMaterial,
          socketSignalMaterial,
        );
      }

      // The purchase beat. A tier is bought out of the bank and applied to the
      // team's Prime lives, so the honest place to say it is the ground those
      // lives come back to: the pad's own footprint lights up in scrap's
      // colour for the length of the beat and goes out. No new geometry, no
      // toast over the arena, and it cannot be confused with a spawn — a
      // reservation ring is a circle on one tile, this is the whole pad.
      const forgeGeometry = tiledInsetGeometry(home.protectedSpawnPad, 0.02);
      const forgeMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color(SCRAP_ACCENT),
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const forge = new THREE.Mesh(forgeGeometry, forgeMaterial);
      forge.position.y = 0.026;
      forge.userData.kind = 'scrap-purchase-flash';
      forge.userData.teamId = home.teamId;
      pad.add(forge);

      group.add(pad);
      disposables.push(
        bedGeometry,
        bedMaterial,
        sealGeometry,
        sealMaterial,
        hatchGeometry,
        hatchMaterial,
        forgeGeometry,
        forgeMaterial,
      );
      return {
        teamId: home.teamId,
        pad,
        sealMaterial,
        hatchMaterial,
        socketSignalMaterial,
        forge,
        forgeMaterial,
      };
    });

  const update = (presentation: TickPresentation, time: number) => {
    for (const entry of entries) {
      const accent =
        presentation.units.find((unit) => unit.teamId === entry.teamId)
          ?.accent ?? '#b27a43';
      entry.sealMaterial.color.set(accent);
      entry.hatchMaterial.color.set(accent);
      if (entry.socketSignalMaterial) {
        entry.socketSignalMaterial.color.set(accent);
        entry.socketSignalMaterial.emissive.set(accent);
      }
      entry.pad.userData.accent = accent;

      const purchase = presentation.economy?.purchases.find(
        (entryPurchase) => entryPurchase.teamId === entry.teamId,
      );
      entry.forge.userData.purchase = purchase
        ? `${purchase.trackId}:${purchase.tier}`
        : null;
      // Struck hard on the tick it settles and released over the beat, with a
      // fast flicker on top so it reads as a forge rather than a fade.
      const flicker = 0.72 + 0.28 * Math.abs(Math.sin(time * Math.PI * 5));
      entry.forgeMaterial.opacity = purchase
        ? 0.1 + 0.34 * purchase.strength * flicker
        : 0;
    }
  };

  return { group, update };
}

/**
 * Loose scrap on the floor.
 *
 * A pile has to say three things at arena scale — *here*, *how much*, and *not
 * for much longer* — and it has to say them without becoming a fifth kind of
 * glowing ring, because the floor already carries capture arcs, spawn
 * reservations, arrival rings and impact waves.
 *
 * So it is an object rather than a marking: a small faceted ingot standing a
 * little off the tile, turning slowly, over a flat wash of its own light. Size
 * carries the amount, but gently — a six-scrap deposit is a third larger than a
 * one-scrap wreck, not six times — because the number that matters to a
 * spectator is "worth crossing the map for" rather than the integer. Expiry
 * takes the light out of it first and the ingot last, and the final quarter
 * blinks, which is the one thing on this floor that blinks.
 *
 * Pooled and driven entirely by the tick's own state, like every other overlay
 * here: nothing is remembered between frames, so scrubbing backwards into a
 * tick where a pile had not landed yet shows no pile.
 */
function buildScrapPiles(
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (presentation: TickPresentation, time: number) => void;
} {
  const group = new THREE.Group();
  group.userData.kind = 'scrap-piles';
  // An octahedron reads as a cut ingot from a raised camera and needs eight
  // triangles to do it.
  const ingotGeometry = new THREE.OctahedronGeometry(0.17, 0);
  const washGeometry = new THREE.CircleGeometry(0.42, 20);
  washGeometry.rotateX(-Math.PI / 2);
  const collarGeometry = new THREE.RingGeometry(0.2, 0.26, 6);
  collarGeometry.rotateX(-Math.PI / 2);
  disposables.push(ingotGeometry, washGeometry, collarGeometry);

  const colour = new THREE.Color(SCRAP_ACCENT);
  const piles: {
    group: THREE.Group;
    ingot: THREE.Mesh;
    ingotMaterial: THREE.MeshStandardMaterial;
    washMaterial: THREE.MeshBasicMaterial;
    collar: THREE.Mesh;
    collarMaterial: THREE.MeshBasicMaterial;
  }[] = [];

  const borrow = (index: number) => {
    while (piles.length <= index) {
      const pile = new THREE.Group();
      pile.userData.kind = 'scrap-pile';
      const ingotMaterial = new THREE.MeshStandardMaterial({
        color: colour.clone().multiplyScalar(0.55),
        emissive: colour,
        emissiveIntensity: 0.9,
        roughness: 0.35,
        metalness: 0.75,
        transparent: true,
      });
      const ingot = new THREE.Mesh(ingotGeometry, ingotMaterial);
      ingot.castShadow = true;
      pile.add(ingot);

      const washMaterial = new THREE.MeshBasicMaterial({
        color: colour,
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const wash = new THREE.Mesh(washGeometry, washMaterial);
      wash.position.y = 0.015;
      pile.add(wash);

      // The collar is the clock: a hexagonal ring that shrinks and dims as the
      // pile's 80 ticks run out, so "about to vanish" is legible without
      // reading a number.
      const collarMaterial = new THREE.MeshBasicMaterial({
        color: colour,
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const collar = new THREE.Mesh(collarGeometry, collarMaterial);
      collar.position.y = 0.028;
      pile.add(collar);

      pile.visible = false;
      group.add(pile);
      piles.push({
        group: pile,
        ingot,
        ingotMaterial,
        washMaterial,
        collar,
        collarMaterial,
      });
      disposables.push(ingotMaterial, washMaterial, collarMaterial);
    }
    return piles[index];
  };

  const update = (presentation: TickPresentation, time: number) => {
    const economy = presentation.economy;
    let used = 0;
    for (const pile of economy?.piles ?? []) {
      const slot = borrow(used++);
      slot.group.visible = true;
      slot.group.position.set(pile.position.x + 0.5, 0, pile.position.y + 0.5);
      slot.group.userData.amount = pile.amount;
      slot.group.userData.vein = pile.vein;
      slot.group.userData.remainingTicks = pile.remainingTicks;

      // Amount, compressed: a wreck and a full deposit differ by a third
      // rather than by six times, because the tile is one tile either way.
      const bulk = 1 + 0.42 * Math.min(1, Math.log2(1 + pile.amount) / 3);
      // The last quarter blinks. Everything else on this floor pulses; only
      // this goes out and comes back, which is what "leaving" looks like.
      const blink = pile.expiring
        ? 0.35 + 0.65 * (0.5 + 0.5 * Math.sin(time * Math.PI * 6))
        : 1;
      const alive = 0.35 + 0.65 * pile.lifeFraction;

      slot.ingot.position.y = 0.19 + 0.035 * Math.sin(time * Math.PI * 1.4);
      slot.ingot.rotation.y = time * Math.PI * 0.35;
      slot.ingot.rotation.x = 0.42;
      slot.ingot.scale.setScalar(bulk);
      slot.ingotMaterial.emissiveIntensity = 0.55 + 0.85 * alive * blink;
      slot.ingotMaterial.opacity = 0.55 + 0.45 * blink;

      slot.washMaterial.opacity = 0.1 + 0.24 * alive * blink;
      slot.collar.scale.setScalar(bulk * (0.75 + 0.35 * pile.lifeFraction));
      slot.collar.rotation.y = -time * Math.PI * 0.22;
      slot.collarMaterial.opacity = 0.14 + 0.4 * pile.lifeFraction * blink;
    }
    for (let index = used; index < piles.length; index++)
      piles[index].group.visible = false;
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
      const boundaryMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color('#b8844f'),
        transparent: true,
        opacity: 0.055,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const boundary = new THREE.Mesh(boundaryGeometry, boundaryMaterial);
      boundary.position.y = 0.021;
      boundary.userData.kind = 'frontline-capture-boundary';
      field.add(boundary);
      const signalMaterial = boundaryMaterial.clone();
      const signal = new THREE.Mesh(signalGeometry, signalMaterial);
      signal.position.y = 0.023;
      signal.userData.kind = 'frontline-capture-signal';
      field.add(signal);

      // Whole-footprint tint makes a captured ratchet team-readable before
      // the eye has to parse a small signal. It remains flat and translucent,
      // so bots win and no raised collision is implied.
      const ownershipGeometry = tiledInsetGeometry(position.tiles, 0.115);
      const ownershipMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color('#b8844f'),
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const ownership = new THREE.Mesh(
        ownershipGeometry,
        ownershipMaterial,
      );
      ownership.position.y = 0.018;
      ownership.userData.kind = 'frontline-capture-ownership';
      field.add(ownership);

      // Every authored tile gets the same progress arc. Instancing keeps the
      // draw count bounded while drawRange turns the exact 0..threshold value
      // into length instead of the old, ambiguous opacity change.
      const progressGeometry = captureProgressGeometry(0.285, 0.345);
      const progressMaterial = captureArcMaterial();
      const progress = captureRingInstances(
        position.tiles,
        progressGeometry,
        progressMaterial,
      );
      progress.position.y = 0.031;
      progress.userData.kind = 'frontline-capture-progress';
      field.add(progress);

      // During erosion this short counter-rotating challenger arc sits outside
      // the incumbent's stored-progress arc. It deliberately grants no filled
      // challenger credit before the authoritative claimant flips.
      const erosionGeometry = captureProgressGeometry(0.385, 0.43);
      setCaptureArcFraction(erosionGeometry, 0.24);
      const erosionMaterial = captureArcMaterial();
      const erosion = captureRingInstances(
        position.tiles,
        erosionGeometry,
        erosionMaterial,
      );
      erosion.position.y = 0.034;
      erosion.userData.kind = 'frontline-capture-erosion';
      field.add(erosion);

      // The knockback. Under the channel a hit on a body standing here takes
      // back the whole run's work, and a bar that simply got shorter between
      // two frames says nothing about that — so the length the meter *had*
      // stays on screen for the beat, hot and flashing, outside the length it
      // now has. The eye reads the gap.
      const revertGeometry = captureProgressGeometry(0.285, 0.345);
      const revertMaterial = captureArcMaterial();
      const revert = captureRingInstances(
        position.tiles,
        revertGeometry,
        revertMaterial,
      );
      revert.position.y = 0.033;
      revert.userData.kind = 'frontline-capture-revert';
      field.add(revert);

      // And the whole footprint takes the hit, so an interrupt is visible from
      // wherever the camera happens to be rather than only on the tile the
      // bolt landed on.
      const interruptGeometry = tiledInsetGeometry(position.tiles, 0.075);
      const interruptMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color('#ffd9a1'),
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const interrupt = new THREE.Mesh(
        interruptGeometry,
        interruptMaterial,
      );
      interrupt.position.y = 0.026;
      interrupt.userData.kind = 'frontline-capture-interrupt';
      field.add(interrupt);

      // The outer ratchet arc counts down from the contract-declared hold
      // duration and pulses in the exact hold owner's runtime accent.
      const holdGeometry = captureProgressGeometry(0.455, 0.505);
      const holdMaterial = captureArcMaterial();
      const hold = captureRingInstances(
        position.tiles,
        holdGeometry,
        holdMaterial,
      );
      hold.position.y = 0.037;
      hold.userData.kind = 'frontline-capture-hold';
      field.add(hold);

      group.add(field);
      disposables.push(
        bedGeometry,
        bedMaterial,
        boundaryGeometry,
        boundaryMaterial,
        signalGeometry,
        signalMaterial,
        ownershipGeometry,
        ownershipMaterial,
        progressGeometry,
        progressMaterial,
        erosionGeometry,
        erosionMaterial,
        holdGeometry,
        holdMaterial,
        revertGeometry,
        revertMaterial,
        interruptGeometry,
        interruptMaterial,
      );
      return {
        positionIndex: position.positionIndex,
        tiles: position.tiles,
        field,
        bedMaterial,
        boundaryMaterial,
        signalMaterial,
        ownershipMaterial,
        progress,
        progressGeometry,
        progressMaterial,
        erosion,
        erosionMaterial,
        erosionSpin: Number.NaN,
        hold,
        holdGeometry,
        holdMaterial,
        holdSpin: Number.NaN,
        revert,
        revertGeometry,
        revertMaterial,
        interruptMaterial,
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
        entry.boundaryMaterial.opacity = 0.055;
        entry.boundaryMaterial.color.set('#b8844f');
        entry.signalMaterial.opacity = 0.055;
        entry.signalMaterial.color.set('#b8844f');
        entry.ownershipMaterial.opacity = 0;
        entry.progressMaterial.opacity = 0;
        entry.erosionMaterial.opacity = 0;
        entry.holdMaterial.opacity = 0;
        entry.revertMaterial.opacity = 0;
        entry.interruptMaterial.opacity = 0;
      }
      return;
    }
    const activePositionIndex = objective.activePositionIndex;
    const visual = frontlineCaptureVisual(presentation);
    if (!visual) return;
    const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 2.2);

    for (const entry of entries) {
      const active = entry.positionIndex === activePositionIndex;
      const state = active ? visual.state : 'inactive';
      entry.field.userData.active = active;
      entry.field.userData.state = state;
      entry.field.userData.captureProgress = active
        ? objective.captureProgress
        : 0;
      entry.field.userData.captureFraction = active
        ? visual.progressFraction
        : 0;
      entry.field.userData.progressDirection = active
        ? visual.progressDirection
        : 'none';
      entry.field.userData.claimantTeamId = active
        ? visual.claimantTeamId
        : null;
      entry.field.userData.challengerTeamId =
        active && visual.progressDirection === 'eroding'
          ? visual.challengerTeamId
          : null;
      entry.field.userData.holdOwnerTeamId = active
        ? visual.holdOwnerTeamId
        : null;
      entry.field.userData.holdEndsAtTick = active
        ? visual.holdEndsAtTick
        : null;
      entry.field.userData.holdRemainingTicks = active
        ? visual.holdRemainingTicks
        : null;
      entry.bedMaterial.opacity = active ? 0.2 : 0.11;
      entry.boundaryMaterial.opacity = !active
        ? 0.055
        : visual.state === 'holding'
          ? 0.82
          : visual.state === 'eroding'
            ? 0.66
            : visual.state === 'contested'
              ? 0.58 + pulse * 0.18
              : visual.claimantAccent
                ? 0.48
                : 0.28;
      entry.signalMaterial.opacity = !active
        ? 0.055
        : visual.state === 'holding'
          ? 0.72 + pulse * 0.2
          : visual.contested
            ? 0.46 + pulse * 0.18
            : visual.claimantAccent
              ? 0.38 + visual.progressFraction * 0.32
              : 0.25;
      const boundaryAccent =
        visual.state === 'eroding'
          ? visual.challengerAccent
          : visual.state === 'holding'
            ? visual.holdAccent
            : visual.claimantAccent;
      entry.boundaryMaterial.color.set(
        active &&
          visual.state === 'holding' &&
          boundaryAccent
          ? boundaryAccent
          : active && visual.contested
            ? '#f4c477'
            : active && boundaryAccent
              ? boundaryAccent
              : '#b8844f',
      );
      entry.signalMaterial.color.set(
        active && visual.contested
          ? '#f4c477'
          : active &&
              (visual.holdAccent || visual.claimantAccent)
            ? (visual.holdAccent ?? visual.claimantAccent)!
            : '#b8844f',
      );

      const ownershipAccent =
        visual.holdAccent ?? visual.claimantAccent;
      entry.ownershipMaterial.color.set(
        ownershipAccent ?? '#b8844f',
      );
      entry.ownershipMaterial.opacity = !active
        ? 0
        : visual.state === 'holding'
          ? 0.2 + pulse * 0.1
          : visual.claimantAccent
            ? visual.state === 'contested'
              ? 0.065
              : 0.07 + visual.progressFraction * 0.07
            : 0;

      setCaptureArcFraction(
        entry.progressGeometry,
        active ? visual.progressFraction : 0,
      );
      entry.progressMaterial.color.set(
        visual.claimantAccent ?? '#b8844f',
      );
      entry.progressMaterial.opacity =
        active && visual.progressFraction > 0
          ? visual.contested
            ? 0.72
            : 0.9
          : 0;

      // Erosion is a drain, and it draws like one: the challenger's arc turns
      // steadily against the incumbent's, at a fixed brightness rather than a
      // pulse, because the thing it reports is happening every tick at the
      // same rate. The hit reaction below is what flashes.
      const eroding =
        active &&
        (visual.progressDirection === 'eroding' ||
          visual.revert?.kind === 'erosion');
      entry.erosionMaterial.color.set(
        visual.challengerAccent ??
          (visual.revert?.kind === 'erosion'
            ? (visual.revertAccent ?? '#b8844f')
            : '#b8844f'),
      );
      entry.erosionMaterial.opacity = eroding ? 0.78 : 0;
      entry.erosionSpin = spinCaptureRings(
        entry.erosion,
        entry.tiles,
        entry.erosionSpin,
        -time * Math.PI * 0.72,
      );

      // The knockback: the length the meter had, held outside the length it
      // has, flashing out over the beat.
      const revert = active ? visual.revert : null;
      setCaptureArcFraction(
        entry.revertGeometry,
        revert ? revert.ghostFraction : 0,
      );
      entry.revertMaterial.color.set(
        revert?.kind === 'interrupt'
          ? '#fff1d0'
          : (visual.revertAccent ?? '#b8844f'),
      );
      entry.revertMaterial.opacity =
        revert === null
          ? 0
          : revert.kind === 'interrupt'
            ? revert.strength * (0.62 + pulse * 0.38)
            : revert.strength * 0.34;
      entry.interruptMaterial.opacity =
        revert?.kind === 'interrupt'
          ? 0.1 + 0.22 * revert.strength * (0.5 + pulse * 0.5)
          : 0;
      entry.field.userData.revertKind = revert?.kind ?? null;
      entry.field.userData.revertAmount = revert?.amount ?? 0;

      setCaptureArcFraction(
        entry.holdGeometry,
        active ? visual.holdFraction : 0,
      );
      entry.holdMaterial.color.set(
        visual.holdAccent ?? '#b8844f',
      );
      entry.holdMaterial.opacity =
        active && visual.state === 'holding'
          ? 0.72 + pulse * 0.26
          : 0;
      entry.holdSpin = spinCaptureRings(
        entry.hold,
        entry.tiles,
        entry.holdSpin,
        time * Math.PI * 0.18,
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

const CAPTURE_ARC_SEGMENTS = 64;

function captureProgressGeometry(
  innerRadius: number,
  outerRadius: number,
): THREE.RingGeometry {
  const geometry = new THREE.RingGeometry(
    innerRadius,
    outerRadius,
    CAPTURE_ARC_SEGMENTS,
  );
  geometry.rotateX(-Math.PI / 2);
  setCaptureArcFraction(geometry, 0);
  return geometry;
}

function captureArcMaterial(): THREE.MeshBasicMaterial {
  return new THREE.MeshBasicMaterial({
    color: new THREE.Color('#b8844f'),
    transparent: true,
    opacity: 0,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });
}

function captureRingInstances(
  tiles: readonly ReplayPosition[],
  geometry: THREE.BufferGeometry,
  material: THREE.Material,
): THREE.InstancedMesh {
  const instances = new THREE.InstancedMesh(
    geometry,
    material,
    tiles.length,
  );
  writeCaptureRingMatrices(instances, tiles, 0);
  return instances;
}

/**
 * Spin the capture arcs **in place**, one tile at a time.
 *
 * `rotation.y` on the mesh is the obvious way to write this and the wrong one: the tile
 * translation lives in each instance matrix, and the mesh sits at the map's origin corner,
 * so turning the mesh swings every arc around tile (0,0) on a radius of however far into
 * the arena its tile happens to be. At ~1.8 revolutions a second that is a ring of light
 * flying across — and off — the map, appearing wherever the playhead lands, which is
 * exactly what it looked like. The rotation has to be composed *before* the translation,
 * per instance, so each arc turns about its own centre.
 *
 * Rewriting the matrices costs a few tiles' worth of arithmetic, so it is skipped when the
 * angle has not moved — a paused viewer writes nothing.
 */
function spinCaptureRings(
  instances: THREE.InstancedMesh,
  tiles: readonly ReplayPosition[],
  current: number,
  angle: number,
): number {
  if (current === angle) return current;
  writeCaptureRingMatrices(instances, tiles, angle);
  return angle;
}

function writeCaptureRingMatrices(
  instances: THREE.InstancedMesh,
  tiles: readonly ReplayPosition[],
  angle: number,
): void {
  const matrix = new THREE.Matrix4();
  tiles.forEach((tile, index) => {
    matrix.makeRotationY(angle);
    matrix.setPosition(tile.x + 0.5, 0, tile.y + 0.5);
    instances.setMatrixAt(index, matrix);
  });
  instances.instanceMatrix.needsUpdate = true;
}

function setCaptureArcFraction(
  geometry: THREE.BufferGeometry,
  fraction: number,
): void {
  const segmentCount =
    fraction <= 0
      ? 0
      : Math.max(
          1,
          Math.min(
            CAPTURE_ARC_SEGMENTS,
            Math.ceil(fraction * CAPTURE_ARC_SEGMENTS),
          ),
        );
  // RingGeometry emits two triangles (six indices) per angular segment.
  geometry.setDrawRange(0, segmentCount * 6);
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
      // Where the hit landed. Replay-v1 and v2 spell that `from`; a
      // generation-3 damage event carries one `position`, which normalizes to
      // `to`. Reading only `from` is why nothing flashed on a v3 replay at
      // all — every hit and every kill was drawn at a null tile and skipped.
      const at = event.from ?? event.to;
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

/** Arc Relay's objective objects and the unmistakable carrier beacon. */
function buildArcRelayStory(
  replay: ReplayModel,
  themeId: string,
  disposables: { dispose: () => void }[],
  entrants?: readonly ViewerEntrantPresentation[],
): {
  group: THREE.Group;
  update: (presentation: TickPresentation, time: number) => void;
} {
  const group = new THREE.Group();
  group.userData.kind = 'arc-relay-story';
  const emberForge = themeId === 'ember-forge';
  const lumenBastion = themeId === 'lumen-bastion';
  const arcArchitecture = emberForge || lumenBastion;
  const arcTossReleaseOffset =
    replay.signaturePresentationId === ARC_SIGNATURE_PRESENTATION_ID
      ? ARC_TOSS_RELEASE_TICK_OFFSET
      : 0;
  const arcRules =
    replay.contract.kind === 'v3-generic' &&
    replay.contract.rawContract.rules.gameMode.kind === 'arc-relay'
      ? replay.contract.rawContract.rules.gameMode
      : null;
  const currentArcStoryAt = createPresenter(replay).at;

  const wellGeometry = new THREE.CylinderGeometry(
    arcArchitecture ? 0.28 : 0.31,
    arcArchitecture ? 0.4 : 0.38,
    arcArchitecture ? 0.14 : 0.09,
    arcArchitecture ? 12 : 24,
  );
  const wellRingGeometry = new THREE.RingGeometry(
    arcArchitecture ? 0.4 : 0.39,
    arcArchitecture ? 0.49 : 0.47,
    arcArchitecture ? 16 : 32,
  );
  wellRingGeometry.rotateX(-Math.PI / 2);
  const reactorGeometry = new THREE.CylinderGeometry(
    0.38,
    0.46,
    arcArchitecture ? 0.22 : 0.18,
    arcArchitecture ? 12 : 28,
  );
  const reactorRingGeometry = new THREE.TorusGeometry(0.49, 0.035, 8, 36);
  reactorRingGeometry.rotateX(Math.PI / 2);
  const crestGeometry = new THREE.CircleGeometry(0.2, 32);
  crestGeometry.rotateX(-Math.PI / 2);
  const pipGeometry = new THREE.SphereGeometry(0.055, 10, 8);
  const integrityGeometry = new THREE.BoxGeometry(0.16, 0.025, 0.1);
  // A shaded sphere, not a bright faceted token. The Core's fixed hover above the hull
  // makes possession legible without making it orbit through a neighbouring tile.
  const coreGeometry = new THREE.SphereGeometry(0.22, 28, 18);
  const beamGeometry = new THREE.CylinderGeometry(0.025, 0.08, 1.05, 12);
  const coreGlowTexture = flare();
  const reactorApertureGeometry = new THREE.CircleGeometry(0.46, 8);
  reactorApertureGeometry.rotateX(-Math.PI / 2);
  const reactorGlowGeometry = new THREE.PlaneGeometry(0.96, 0.96);
  reactorGlowGeometry.rotateX(-Math.PI / 2);
  const sourceTriangleGeometry = new THREE.ConeGeometry(0.17, 0.045, 3);
  const sourceDiamondGeometry = new THREE.BoxGeometry(0.23, 0.045, 0.23);
  sourceDiamondGeometry.rotateY(Math.PI / 4);
  const sourceCircleGeometry = new THREE.TorusGeometry(0.13, 0.028, 7, 24);
  sourceCircleGeometry.rotateX(Math.PI / 2);
  const reactorPylonGeometry = arcArchitecture
    ? new THREE.CylinderGeometry(0.055, 0.1, 0.38, 6)
    : new THREE.BoxGeometry(0.09, 0.38, 0.09);
  const forgeWellClampGeometry = new THREE.BoxGeometry(0.13, 0.15, 0.11);
  const forgeWellRimGeometry = new THREE.TorusGeometry(0.31, 0.045, 6, 12);
  forgeWellRimGeometry.rotateX(Math.PI / 2);
  disposables.push(
    wellGeometry,
    wellRingGeometry,
    reactorGeometry,
    reactorRingGeometry,
    crestGeometry,
    pipGeometry,
    integrityGeometry,
    coreGeometry,
    beamGeometry,
    reactorApertureGeometry,
    reactorGlowGeometry,
    sourceTriangleGeometry,
    sourceDiamondGeometry,
    sourceCircleGeometry,
    reactorPylonGeometry,
    forgeWellClampGeometry,
    forgeWellRimGeometry,
  );
  if (coreGlowTexture) disposables.push(coreGlowTexture);

  // Heal zones are static map regions, so their pads are scenery built once:
  // a soft green plate with a cross, dim enough to read as ground rather
  // than pickup. The channel itself is announced on the bot (its heal ring),
  // so the pad only has to say "this is where that happens".
  const healPulseMaterials: THREE.Material[] = [];
  {
    const padGeometry = arcArchitecture
      ? new THREE.CylinderGeometry(0.34, 0.43, 0.1, 12)
      : new THREE.CircleGeometry(0.34, 28).rotateX(-Math.PI / 2);
    const padRingGeometry = arcArchitecture
      ? new THREE.TorusGeometry(0.3, 0.035, 6, 16).rotateX(Math.PI / 2)
      : new THREE.RingGeometry(0.36, 0.43, 28).rotateX(-Math.PI / 2);
    const coolantGeometry = arcArchitecture
      ? new THREE.CylinderGeometry(0.235, 0.255, 0.035, 12)
      : null;
    const healClampGeometry = arcArchitecture
      ? new THREE.BoxGeometry(0.12, 0.1, 0.13)
      : null;
    const crossBarGeometry = new THREE.BoxGeometry(
      arcArchitecture ? 0.25 : 0.3,
      arcArchitecture ? 0.035 : 0.012,
      arcArchitecture ? 0.075 : 0.09,
    );
    disposables.push(padGeometry, padRingGeometry, crossBarGeometry);
    if (coolantGeometry && healClampGeometry)
      disposables.push(coolantGeometry, healClampGeometry);
    for (const tile of replay.map.regions
      .filter((region) => region.regionId.startsWith('heal-'))
      .flatMap((region) => region.tiles)) {
      const padMaterial = arcArchitecture
        ? new THREE.MeshStandardMaterial({
            color: lumenBastion ? '#dce4e1' : '#242a2b',
            emissive: '#123e2b',
            emissiveIntensity: lumenBastion ? 0.12 : 0.34,
            roughness: 0.62,
            metalness: 0.48,
            transparent: true,
            opacity: 0.92,
            depthWrite: false,
          })
        : new THREE.MeshBasicMaterial({
            color: '#166534',
            transparent: true,
            opacity: 0.55,
            depthWrite: false,
          });
      const ringMaterial = arcArchitecture
        ? new THREE.MeshStandardMaterial({
            color: '#4ade80',
            emissive: '#35a85e',
            emissiveIntensity: 0.8,
            roughness: 0.5,
            metalness: 0.36,
            transparent: true,
            opacity: 0.5,
            depthWrite: false,
          })
        : new THREE.MeshBasicMaterial({
            color: '#4ade80',
            transparent: true,
            opacity: 0.5,
            depthWrite: false,
          });
      const crossMaterial = new THREE.MeshBasicMaterial({
        color: new THREE.Color(arcArchitecture ? '#b7f2c8' : '#86efac'),
        transparent: true,
        opacity: arcArchitecture ? 0.86 : 0.8,
        depthWrite: false,
      });
      const coolantMaterial = arcArchitecture
        ? new THREE.MeshStandardMaterial({
            color: lumenBastion ? '#b9dcc7' : '#173e2c',
            emissive: '#35a85e',
            emissiveIntensity: 0.72,
            roughness: 0.34,
            metalness: 0.32,
          })
        : null;
      const clampMaterial = arcArchitecture
        ? new THREE.MeshStandardMaterial({
            color: lumenBastion ? '#b6bec8' : '#6b2a35',
            roughness: 0.58,
            metalness: 0.7,
          })
        : null;
      disposables.push(padMaterial, ringMaterial, crossMaterial);
      if (coolantMaterial && clampMaterial)
        disposables.push(coolantMaterial, clampMaterial);
      healPulseMaterials.push(ringMaterial);
      const pad = new THREE.Group();
      pad.userData.kind = 'arc-relay-heal-pad';
      pad.userData.themeId = themeId;
      pad.position.set(tile.x + 0.5, 0.018, tile.y + 0.5);
      pad.add(new THREE.Mesh(padGeometry, padMaterial));
      const collar = new THREE.Mesh(padRingGeometry, ringMaterial);
      collar.position.y = arcArchitecture ? 0.102 : 0;
      pad.add(collar);
      if (
        coolantGeometry &&
        coolantMaterial &&
        healClampGeometry &&
        clampMaterial
      ) {
        const coolant = new THREE.Mesh(coolantGeometry, coolantMaterial);
        coolant.position.y = 0.088;
        coolant.userData.kind = 'arc-relay-heal-coolant';
        pad.add(coolant);
        for (let clamp = 0; clamp < 4; clamp++) {
          const angle = clamp * Math.PI / 2;
          const brace = new THREE.Mesh(healClampGeometry, clampMaterial);
          brace.position.set(
            Math.cos(angle) * 0.365,
            0.075,
            Math.sin(angle) * 0.365,
          );
          brace.rotation.y = -angle;
          pad.add(brace);
        }
      }
      const alongX = new THREE.Mesh(crossBarGeometry, crossMaterial);
      alongX.position.y = arcArchitecture ? 0.125 : 0.012;
      const alongZ = new THREE.Mesh(crossBarGeometry, crossMaterial);
      alongZ.rotation.y = Math.PI / 2;
      alongZ.position.y = arcArchitecture ? 0.125 : 0.012;
      pad.add(alongX);
      pad.add(alongZ);
      group.add(pad);
    }
  }

  type WellRig = {
    group: THREE.Group;
    body: THREE.Mesh;
    ring: THREE.Mesh;
    bodyMaterial: THREE.MeshStandardMaterial;
    ringMaterial: THREE.MeshBasicMaterial;
    progress: THREE.Mesh;
    progressGeometry: THREE.RingGeometry;
    progressMaterial: THREE.MeshBasicMaterial;
    sourceGlyphs: THREE.Mesh[];
    valueBadge: THREE.Sprite;
    valueBadgeMaterial: THREE.SpriteMaterial;
    valueBadgeKey: string;
  };
  const wells: WellRig[] = [];
  const well = (index: number): WellRig => {
    while (wells.length <= index) {
      const rig = new THREE.Group();
      rig.userData.kind = 'arc-relay-well';
      rig.userData.themeId = themeId;
      const bodyMaterial = new THREE.MeshStandardMaterial({
        color: lumenBastion ? '#e4e7eb' : emberForge ? '#2b2629' : '#1b2732',
        emissive: lumenBastion ? '#d9ddff' : emberForge ? '#a63b2f' : '#dceaf2',
        emissiveIntensity: arcArchitecture ? 0.24 : 0.35,
        roughness: arcArchitecture ? 0.62 : 0.45,
        metalness: lumenBastion ? 0.24 : emberForge ? 0.72 : 0.65,
      });
      const ringMaterial = new THREE.MeshBasicMaterial({
        color: lumenBastion ? '#f4f3ff' : emberForge ? '#ff9d68' : '#e5f1f7',
        transparent: true,
        opacity: 0.65,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const body = new THREE.Mesh(wellGeometry, bodyMaterial);
      body.position.y = arcArchitecture ? 0.07 : 0.045;
      const fallback = new THREE.Group();
      fallback.userData.kind = 'arc-relay-well-fallback';
      fallback.add(body);
      const ring = new THREE.Mesh(wellRingGeometry, ringMaterial);
      ring.position.y = 0.025;
      const progressGeometry = captureProgressGeometry(0.51, 0.575);
      const progressMaterial = new THREE.MeshBasicMaterial({
        color: lumenBastion ? '#ffffff' : emberForge ? '#ffb078' : '#edf5fb',
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const progress = new THREE.Mesh(progressGeometry, progressMaterial);
      progress.position.y = 0.035;
      progress.rotation.y = -Math.PI / 2;
      progress.userData.kind = 'arc-relay-well-birth-progress';
      progress.userData.progress = 0;
      const sourceGlyphs = [
        sourceTriangleGeometry,
        sourceDiamondGeometry,
        sourceCircleGeometry,
      ].map((geometry) => {
        const mesh = new THREE.Mesh(geometry, ringMaterial);
        mesh.position.y = 0.13;
        rig.add(mesh);
        return mesh;
      });
      const valueBadgeMaterial = new THREE.SpriteMaterial({
        transparent: true,
        depthWrite: false,
        depthTest: false,
      });
      const valueBadge = new THREE.Sprite(valueBadgeMaterial);
      valueBadge.userData.kind = 'arc-relay-well-team-values';
      valueBadge.position.set(0, 0.42, 0);
      valueBadge.scale.set(0.72, 0.27, 1);
      valueBadge.visible = false;
      rig.add(fallback, ring, progress, valueBadge);
      if (arcArchitecture) {
        const trimMaterial = new THREE.MeshStandardMaterial({
          color: lumenBastion ? '#b8c0cc' : '#a63b4b',
          roughness: 0.55,
          metalness: 0.68,
        });
        const rim = new THREE.Mesh(forgeWellRimGeometry, trimMaterial);
        rim.position.y = 0.145;
        fallback.add(rim);
        for (let clamp = 0; clamp < 4; clamp++) {
          const angle = clamp * Math.PI / 2;
          const mesh = new THREE.Mesh(forgeWellClampGeometry, trimMaterial);
          mesh.position.set(
            Math.cos(angle) * 0.36,
            0.075,
            Math.sin(angle) * 0.36,
          );
          mesh.rotation.y = -angle;
          fallback.add(mesh);
        }
        disposables.push(trimMaterial);
      }
      let disposed = false;
      let authoredModel: THREE.Group | null = null;
      void objectiveModel('arc-core-well').then((model) => {
        if (!model) return;
        if (disposed) {
          disposeObjectMaterials(model);
          return;
        }
        authoredModel = model;
        authoredModel.userData.kind = 'arc-relay-well-authored-housing';
        // A Well is a walkable objective tile, not a waist-high prop. Recess the authored
        // housing into the arena so a bot crossing the tile stays visually in front of it.
        authoredModel.position.y = -0.045;
        configureWalkableObjectiveRendering(authoredModel);
        fallback.visible = false;
        for (const glyph of sourceGlyphs) glyph.position.y = 0.105;
        rig.add(authoredModel);
      });
      rig.visible = false;
      group.add(rig);
      wells.push({
        group: rig,
        body,
        ring,
        bodyMaterial,
        ringMaterial,
        progress,
        progressGeometry,
        progressMaterial,
        sourceGlyphs,
        valueBadge,
        valueBadgeMaterial,
        valueBadgeKey: '',
      });
      disposables.push(
        bodyMaterial,
        ringMaterial,
        progressGeometry,
        progressMaterial,
        valueBadgeMaterial,
        {
          dispose: () => {
            disposed = true;
            valueBadgeMaterial.map?.dispose();
            if (authoredModel) disposeObjectMaterials(authoredModel);
          },
        },
      );
    }
    return wells[index]!;
  };

  type ReactorRig = {
    group: THREE.Group;
    material: THREE.MeshStandardMaterial;
    ringMaterial: THREE.MeshBasicMaterial;
    aperture: THREE.Mesh;
    apertureMaterial: THREE.MeshBasicMaterial;
    underGlow: THREE.Mesh;
    underGlowMaterial: THREE.MeshBasicMaterial;
    integrity: THREE.Mesh[];
    integrityMaterials: THREE.MeshBasicMaterial[];
    charge: THREE.Mesh[];
    chargeMaterials: THREE.MeshBasicMaterial[];
    crest: THREE.Mesh;
  };
  const reactors: ReactorRig[] = [];
  const reactor = (index: number): ReactorRig => {
    while (reactors.length <= index) {
      const rig = new THREE.Group();
      rig.userData.kind = 'arc-relay-reactor';
      rig.userData.themeId = themeId;
      let disposed = false;
      const material = new THREE.MeshStandardMaterial({
        color: lumenBastion ? '#dfe3e8' : emberForge ? '#2a2529' : '#101820',
        emissive: lumenBastion ? '#d9ddff' : emberForge ? '#8f3241' : '#64748b',
        emissiveIntensity: arcArchitecture ? 0.34 : 0.45,
        roughness: arcArchitecture ? 0.58 : 0.38,
        metalness: lumenBastion ? 0.28 : emberForge ? 0.78 : 0.72,
      });
      const ringMaterial = new THREE.MeshBasicMaterial({
        transparent: true,
        opacity: 0.75,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const underGlowMaterial = new THREE.MeshBasicMaterial({
        map: coreGlowTexture,
        transparent: true,
        opacity: 0.3,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      // This opaque cap sits above actor floor pools but below the Reactor's own glow.
      // It writes depth through the open Meshy aperture, so a nearby bot cannot turn the
      // socket into a second team-coloured state indication.
      const apertureMaterial = new THREE.MeshBasicMaterial({
        color: '#05070a',
        depthTest: true,
        depthWrite: true,
        side: THREE.DoubleSide,
      });
      const aperture = new THREE.Mesh(reactorApertureGeometry, apertureMaterial);
      aperture.position.y = 0.014;
      aperture.userData.kind = 'arc-relay-reactor-aperture-mask';
      const underGlow = new THREE.Mesh(reactorGlowGeometry, underGlowMaterial);
      underGlow.position.y = 0.018;
      underGlow.userData.kind = 'arc-relay-reactor-underglow';
      const fallback = new THREE.Group();
      fallback.userData.kind = 'arc-relay-reactor-fallback';
      const body = new THREE.Mesh(reactorGeometry, material);
      body.position.y = arcArchitecture ? 0.11 : 0.09;
      const ring = new THREE.Mesh(reactorRingGeometry, ringMaterial);
      ring.position.y = 0.035;
      ring.scale.setScalar(1.35);
      const crestMaterial = new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false });
      const crest = new THREE.Mesh(crestGeometry, crestMaterial);
      crest.position.set(-0.27, 0.425, 0);
      const stateIndicators = new THREE.Group();
      stateIndicators.userData.kind = 'arc-relay-reactor-state-indicators';
      fallback.add(body);
      stateIndicators.add(crest);
      rig.add(aperture, underGlow, fallback, ring, stateIndicators);
      for (const [index, [x, z]] of [
        [-0.37, -0.37],
        [0.37, -0.37],
        [0.37, 0.37],
        [-0.37, 0.37],
      ].entries()) {
        const pylon = new THREE.Mesh(reactorPylonGeometry, material);
        pylon.position.set(x, 0.19, z);
        pylon.rotation.y = index * Math.PI / 2;
        fallback.add(pylon);
      }
      let authoredModel: THREE.Group | null = null;
      void objectiveModel('arc-team-reactor').then((model) => {
        if (!model) return;
        if (disposed) {
          disposeObjectMaterials(model);
          return;
        }
        authoredModel = model;
        authoredModel.userData.kind = 'arc-relay-reactor-authored-housing';
        // Most of the 0.17-tile Meshy housing sits below the floor. Only its thin upper rim
        // remains visible, so a hovering bot can truthfully skim across the walkable tile.
        authoredModel.position.y = -0.1;
        configureWalkableObjectiveRendering(authoredModel);
        isolateModelFromPointLights(authoredModel);
        // The old procedural fixture was tall enough to carry its state readouts at
        // 0.43 tile. Lower the whole readout assembly once the approved floor recess is
        // available, keeping the pips immediately above its rim instead of floating.
        stateIndicators.position.y = -0.24;
        fallback.visible = false;
        rig.add(authoredModel);
      });
      const integrity: THREE.Mesh[] = [];
      const integrityMaterials: THREE.MeshBasicMaterial[] = [];
      const charge: THREE.Mesh[] = [];
      const chargeMaterials: THREE.MeshBasicMaterial[] = [];
      for (let pip = 0; pip < 3; pip++) {
        const outerMaterial = new THREE.MeshBasicMaterial();
        const outer = new THREE.Mesh(integrityGeometry, outerMaterial);
        const integrityPositions = [
          [0.63, 0],
          [-0.04, -0.55],
          [-0.04, 0.55],
        ] as const;
        outer.position.set(integrityPositions[pip]![0], 0.43, integrityPositions[pip]![1]);
        outer.userData.kind = 'arc-relay-reactor-integrity';
        stateIndicators.add(outer);
        integrity.push(outer);
        integrityMaterials.push(outerMaterial);
        const innerMaterial = new THREE.MeshBasicMaterial();
        const inner = new THREE.Mesh(pipGeometry, innerMaterial);
        inner.position.set(0.38, 0.44, (pip - 1) * 0.22);
        inner.userData.kind = 'arc-relay-reactor-charge';
        stateIndicators.add(inner);
        charge.push(inner);
        chargeMaterials.push(innerMaterial);
        disposables.push(outerMaterial, innerMaterial);
      }
      rig.visible = false;
      group.add(rig);
      reactors.push({
        group: rig,
        material,
        ringMaterial,
        aperture,
        apertureMaterial,
        underGlow,
        underGlowMaterial,
        integrity,
        integrityMaterials,
        charge,
        chargeMaterials,
        crest,
      });
      disposables.push(material, ringMaterial, apertureMaterial, underGlowMaterial, crestMaterial, {
        dispose: () => {
          disposed = true;
          if (authoredModel) disposeObjectMaterials(authoredModel);
        },
      });
    }
    return reactors[index]!;
  };

  type CoreRig = {
    group: THREE.Group;
    gem: THREE.Mesh;
    glow: THREE.Sprite;
    light: THREE.PointLight;
    beam: THREE.Mesh;
    gemMaterial: THREE.MeshLambertMaterial;
    glowMaterial: THREE.SpriteMaterial;
    beamMaterial: THREE.MeshBasicMaterial;
    sourceGlyphs: THREE.Mesh[];
  };
  const cores: CoreRig[] = [];
  const core = (index: number): CoreRig => {
    while (cores.length <= index) {
      const rig = new THREE.Group();
      // Lambert keeps enough directional shading to read as a volume but has no glossy
      // specular lobe: this is a self-lit energy Core, not a polished pearl.
      const gemMaterial = new THREE.MeshLambertMaterial({
        // A darker body preserves the sphere's volume under emission. The low-chroma
        // white/lilac light is deliberately distinct from either team's ownership hue.
        color: ARC_CORE_NEUTRAL_PALETTE.body,
        emissive: ARC_CORE_NEUTRAL_PALETTE.emissive,
        emissiveIntensity: 0.68,
      });
      const glowMaterial = new THREE.SpriteMaterial({
        map: coreGlowTexture,
        color: ARC_CORE_NEUTRAL_PALETTE.glow,
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const beamMaterial = new THREE.MeshBasicMaterial({
        transparent: true,
        opacity: 0.22,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const gem = new THREE.Mesh(coreGeometry, gemMaterial);
      gem.castShadow = false;
      gem.userData.kind = 'arc-relay-core-sphere';
      const glow = new THREE.Sprite(glowMaterial);
      glow.userData.kind = 'arc-relay-core-glow';
      const light = new THREE.PointLight(
        ARC_CORE_NEUTRAL_PALETTE.light,
        0,
        2.4,
        2,
      );
      light.userData.kind = 'arc-relay-core-light';
      const beam = new THREE.Mesh(beamGeometry, beamMaterial);
      beam.position.y = 0.54;
      const sourceGlyphs = [
        sourceTriangleGeometry,
        sourceDiamondGeometry,
        sourceCircleGeometry,
      ].map((geometry) => {
        const mesh = new THREE.Mesh(geometry, gemMaterial);
        mesh.scale.setScalar(0.62);
        mesh.position.y = 0.26;
        rig.add(mesh);
        return mesh;
      });
      rig.add(glow, gem, light, beam);
      rig.userData.kind = 'arc-relay-core';
      rig.visible = false;
      group.add(rig);
      cores.push({
        group: rig,
        gem,
        glow,
        light,
        beam,
        gemMaterial,
        glowMaterial,
        beamMaterial,
        sourceGlyphs,
      });
      disposables.push(gemMaterial, glowMaterial, beamMaterial);
    }
    return cores[index]!;
  };

  type CoreTransitionKind =
    | 'pickup'
    | 'arc-toss-catch'
    | 'handoff'
    | 'drop'
    | 'bank';
  interface CoreTransition {
    coreKey: string;
    kind: CoreTransitionKind;
    sourceWellId: string;
    sourceActor: ReplayActorIdentity | null;
    targetActor: ReplayActorIdentity | null;
    position: ReplayPosition;
  }

  const coreKey = (coreId: { sourceWellId: string; sourceOrdinal: number }) =>
    `${coreId.sourceWellId}:${coreId.sourceOrdinal}`;
  const coreTransitionsAt = (tickIndex: number): CoreTransition[] => {
    const tick = replay.ticks[tickIndex];
    const facts = [
      ...(tick?.lifecycleEvents ?? []),
      ...(tick?.events ?? []),
    ].flatMap((event) => event.arcRelayFact ? [event.arcRelayFact] : []);
    const arcTossLandings = new Set(
      facts.flatMap((fact) =>
        fact.kind === 'core-relocated' &&
          fact.relocationKind === 'arc-toss-landing'
          ? [coreKey(fact.coreId)]
          : []),
    );
    const result: CoreTransition[] = [];
    for (const fact of facts) {
      const transition = coreTransition(fact, arcTossLandings);
      if (transition) result.push(transition);
    }
    return result;
  };
  const coreTransition = (
    fact: ReplayArcRelayFact,
    arcTossLandings: ReadonlySet<string>,
  ): CoreTransition | null => {
    switch (fact.kind) {
      case 'core-picked-up':
        return {
          coreKey: coreKey(fact.coreId),
          kind: arcTossLandings.has(coreKey(fact.coreId))
            ? 'arc-toss-catch'
            : 'pickup',
          sourceWellId: fact.coreId.sourceWellId,
          sourceActor: null,
          targetActor: fact.carrierActor,
          position: fact.position,
        };
      case 'core-handed-off':
        return {
          coreKey: coreKey(fact.coreId),
          kind: 'handoff',
          sourceWellId: fact.coreId.sourceWellId,
          sourceActor: fact.sourceActor,
          targetActor: fact.targetActor,
          position: fact.position,
        };
      case 'core-dropped':
        // Arc Toss owns its longer ballistic departure and landing. The generic custody
        // grammar handles ordinary voluntary/destruction drops only.
        if (fact.dropKind === 'signature-departure' ||
            fact.dropKind === 'arc-toss-landing') return null;
        return {
          coreKey: coreKey(fact.coreId),
          kind: 'drop',
          sourceWellId: fact.coreId.sourceWellId,
          sourceActor: fact.sourceActor,
          targetActor: null,
          position: fact.position,
        };
      case 'core-banked':
        return {
          coreKey: coreKey(fact.coreId),
          kind: 'bank',
          sourceWellId: fact.coreId.sourceWellId,
          sourceActor: fact.carrierActor,
          targetActor: null,
          position: fact.position,
        };
      default:
        return null;
    }
  };

  const posePoint = (
    actor: ReplayActorIdentity | null,
    actorPoses: ReturnType<typeof posesAt>,
  ): THREE.Vector3 | null => {
    if (!actor) return null;
    const pose = actorPoses.find((candidate) => candidate.actorKey === actor.actorKey);
    return pose ? new THREE.Vector3(pose.x + 0.5, 1.18, pose.y + 0.5) : null;
  };

  const snapshotActorPoint = (
    actor: ReplayActorIdentity | null,
    tickIndex: number,
    side: 'before' | 'after',
  ): THREE.Vector3 | null => {
    if (!actor) return null;
    const state = replay.ticks[tickIndex]?.[side].actors.find(
      (candidate) => candidate.actorKey === actor.actorKey,
    );
    return state
      ? new THREE.Vector3(state.position.x + 0.5, 1.18, state.position.y + 0.5)
      : null;
  };

  const easeCore = (value: number): number => {
    const t = Math.max(0, Math.min(1, value));
    return t < 0.5 ? 4 * t ** 3 : 1 - (-2 * t + 2) ** 3 / 2;
  };

  /** Small deterministic phase for Core idle motion, independent of pooled draw order. */
  const coreMotionPhase = (key: string): number => {
    let hash = 0;
    for (const character of key) hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
    return (hash % 6283) / 1000;
  };

  const carriedCoreHeight = (key: string, time: number): number =>
    1.18 + Math.sin(time * 2.4 + coreMotionPhase(key)) * 0.035;

  const paintCoreInMotion = (
    rig: CoreRig,
    sourceWellId: string,
    position: THREE.Vector3,
    scale: number,
    carried: boolean,
    pulse: number,
    time: number,
    originTinted = carried,
  ) => {
    const accent = arcOriginAccent(sourceWellId);
    rig.group.visible = true;
    rig.group.position.set(position.x, 0, position.z);
    rig.gem.position.set(0, position.y, 0);
    rig.gem.rotation.y = time * 0.45;
    rig.gem.scale.setScalar(scale);
    rig.glow.position.copy(rig.gem.position);
    rig.light.position.copy(rig.gem.position);
    rig.glow.scale.setScalar((0.72 + pulse * 0.05) * scale);
    if (originTinted) {
      rig.gemMaterial.color.set(accent).multiplyScalar(0.5);
      rig.gemMaterial.emissive.set(accent);
      rig.glowMaterial.color.set(accent);
      rig.light.color.set(accent);
    } else {
      rig.gemMaterial.color.set(ARC_CORE_NEUTRAL_PALETTE.body);
      rig.gemMaterial.emissive.set(ARC_CORE_NEUTRAL_PALETTE.emissive);
      rig.glowMaterial.color.set(ARC_CORE_NEUTRAL_PALETTE.glow);
      rig.light.color.set(ARC_CORE_NEUTRAL_PALETTE.light);
    }
    rig.gemMaterial.emissiveIntensity = originTinted ? 0.8 : 0.7;
    rig.glowMaterial.opacity = 0.15 + pulse * 0.07;
    rig.light.intensity = originTinted ? 2.2 + pulse * 0.6 : 1.6 + pulse * 0.4;
    rig.beam.visible = carried;
    rig.beamMaterial.color.set(accent);
    rig.beamMaterial.opacity = carried ? 0.055 + pulse * 0.045 : 0;
    for (const sourceGlyph of rig.sourceGlyphs) sourceGlyph.visible = false;
  };

  // The declared strike's cone plates are GONE (owner ruling 2026-08): the
  // tracking ray of #215 says the same thing without blinking a wedge of
  // tiles at the spectator, so the windup reads as one line to one body and
  // the red slash owns the landing. `buildStrikeAims` is the whole windup
  // read now; the frozen wedge stays a wire fact, not a floor decal.

  const update = (
    presentation: TickPresentation,
    time: number,
  ) => {
    const story = presentation.arcRelay;
    group.visible = story !== null;
    if (!story) return;

    // The heal pads breathe just enough to read as live machinery.
    for (const [index, material] of healPulseMaterials.entries()) {
      material.opacity =
        0.42 + 0.18 * (0.5 + 0.5 * Math.sin(time * Math.PI * 0.8 + index));
    }

    for (const [index, state] of story.wells.entries()) {
      const rig = well(index);
      rig.group.visible = true;
      rig.group.position.set(state.position.x + 0.5, 0, state.position.y + 0.5);
      const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 1.6 + index);
      const sourceAccent = arcOriginAccent(state.wellId);
      if (arcArchitecture)
        rig.ringMaterial.color
          .set(sourceAccent)
          .lerp(new THREE.Color(lumenBastion ? '#f0efff' : '#ff9560'), 0.48);
      rig.ring.rotation.y = time * 0.35 * (index % 2 === 0 ? 1 : -1);
      rig.ring.scale.setScalar(1 + pulse * 0.08);
      rig.ringMaterial.opacity = state.outstanding ? 0.28 : 0.55 + pulse * 0.28;
      rig.bodyMaterial.emissiveIntensity = state.outstanding
        ? arcArchitecture ? 0.12 : 0.15
        : arcArchitecture ? 0.42 : 0.38;
      const schedule = arcRules?.wells?.find(
        (candidate) => candidate.wellId === state.wellId,
      );
      const scheduledProgress =
        state.nextBirthTick !== null && schedule
          ? Math.max(
              0,
              Math.min(
                1,
                1 -
                  Math.max(0, state.nextBirthTick - time) /
                    Math.max(1, schedule.cadenceTicks),
              ),
            )
          : 0;
      const rearmProgress =
        state.rearmCompletesAtTick !== null
          ? Math.max(
              0,
              Math.min(
                1,
                1 -
                  Math.max(0, state.rearmCompletesAtTick - time) /
                    Math.max(1, arcRules?.pendingRearmTicks ?? 1),
              ),
            )
          : 0;
      const birthProgress = Math.max(scheduledProgress, rearmProgress);
      setCaptureArcFraction(rig.progressGeometry, birthProgress);
      rig.progressMaterial.opacity = birthProgress > 0
        ? 0.68 + pulse * 0.24
        : 0;
      if (arcArchitecture)
        rig.progressMaterial.color
          .set(sourceAccent)
          .lerp(new THREE.Color(lumenBastion ? '#ffffff' : '#ffd0a5'), 0.62);
      rig.progress.userData.progress = birthProgress;
      rig.progress.userData.nextBirthTick = state.nextBirthTick;
      rig.progress.userData.phase = scheduledProgress > 0
        ? 'scheduled-birth'
        : rearmProgress > 0 || state.pendingCharge
          ? 'rearming'
          : 'idle';
      const glyphIndex = state.wellId.includes('north')
        ? 0
        : state.wellId.includes('south')
          ? 1
          : 2;
      for (const [index, glyph] of rig.sourceGlyphs.entries()) {
        glyph.visible = index === glyphIndex;
        glyph.rotation.y = time * 0.18 * (glyphIndex === 1 ? -1 : 1);
      }
      const valueKey = state.teamValues
        .map((value) => `${value.teamId}:${value.value}:${value.accent}`)
        .join('|');
      rig.valueBadge.visible = Boolean(
        arcRules?.teamRelativeCoreCharge && valueKey.length > 0,
      );
      if (rig.valueBadge.visible && rig.valueBadgeKey !== valueKey) {
        rig.valueBadgeMaterial.map?.dispose();
        rig.valueBadgeMaterial.map = wellValueTexture(state.teamValues);
        rig.valueBadgeMaterial.needsUpdate = true;
        rig.valueBadgeKey = valueKey;
      }
    }
    for (let index = story.wells.length; index < wells.length; index++)
      wells[index]!.group.visible = false;

    for (const [index, state] of story.reactors.entries()) {
      const rig = reactor(index);
      rig.group.visible = true;
      rig.group.position.set(state.position.x + 0.5, 0, state.position.y + 0.5);
      rig.group.rotation.y = state.position.x < replay.map.width / 2 ? 0 : Math.PI;
      rig.group.userData.teamId = state.teamId;
      rig.material.emissive.set(state.accent);
      rig.ringMaterial.color.set(state.accent);
      rig.underGlowMaterial.color.set(state.accent);
      const reactorPulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 0.85 + index);
      const chargeLift = state.chargePips * 0.025;
      rig.underGlowMaterial.opacity = 0.2 + reactorPulse * 0.11 + chargeLift;
      rig.underGlow.scale.setScalar(0.94 + reactorPulse * 0.04);
      rig.ringMaterial.opacity = 0.52 + reactorPulse * 0.14;
      const entrant = entrants?.find((value) => value.teamId === state.teamId);
      if (entrant) {
        const material = rig.crest.material as THREE.MeshBasicMaterial;
        if (!material.map) {
          material.map = crestTexture(entrant.crest);
          material.needsUpdate = true;
          disposables.push(material.map);
        }
        rig.crest.visible = true;
      } else {
        rig.crest.visible = false;
      }
      const socketOrder = ['north', 'centre', 'south'];
      const sockets = state.filledSocketWellIds ?? [];
      for (let pip = 0; pip < 3; pip++) {
        rig.integrityMaterials[pip]!.color.set(
          pip < state.integritySegments ? state.accent : '#334155',
        );
        // Threefold sockets light positionally in the lane's hue; the
        // count-based fill remains for every other ruleset (identical at
        // zero charge).
        rig.chargeMaterials[pip]!.color.set(
          sockets.length > 0
            ? sockets.includes(socketOrder[pip]!)
              ? arcOriginAccent(socketOrder[pip]!)
              : '#25313d'
            : pip < state.chargePips
              ? state.accent
              : '#25313d',
        );
      }
    }
    for (let index = story.reactors.length; index < reactors.length; index++)
      reactors[index]!.group.visible = false;

    const actorPoses = posesAt(replay, time);
    const tickIndex = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const tickFraction = Math.max(0, Math.min(1, time - tickIndex));
    const custody = coreTransitionsAt(tickIndex);
    const currentStory = currentArcStoryAt(tickIndex).arcRelay;
    const activeFlights = currentStory?.cores.filter(
      (candidate) => candidate.disposition === 'in-flight' &&
        candidate.flightStartedAtTick !== null &&
        candidate.flightCompletesAtTick !== null &&
        time >= candidate.flightStartedAtTick &&
        time < candidate.flightCompletesAtTick,
    ) ?? [];
    const suppressedCoreKeys = new Set([
      ...activeFlights.map((flight) => flight.key),
      ...custody.map((transition) => transition.coreKey),
    ]);
    let usedCoreRigs = 0;
    for (const state of story.cores) {
      if (suppressedCoreKeys.has(state.key)) continue;
      const rig = core(usedCoreRigs++);
      rig.group.userData.coreKey = state.key;
      rig.group.userData.coreMotion = 'settled';
      const carried = state.disposition === 'carried';
      // The lane owns the sphere (owner ruling 2026-08-05): a Core reads as
      // its origin wherever it is.
      const accent = arcOriginAccent(state.sourceWellId);
      const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 2.1 + usedCoreRigs * 1.7);
      const carrier = carried && state.carrierUnitKey !== null
        ? actorPoses.find((pose) => pose.unitKey === state.carrierUnitKey)
        : null;
      const inFlight = state.disposition === 'in-flight' &&
        state.flightTarget !== null && state.flightCompletesAtTick !== null &&
        state.flightStartedAtTick !== null;
      // The snapshot keeps the launch tile until the authoritative landing. Unlike the
      // old 3D path, move the one real Core through that public interval rather than
      // decorating a stationary sphere with a second fake projectile.
      const flightProgress = inFlight
        ? Math.max(0, Math.min(1,
            (time - state.flightStartedAtTick!) /
              Math.max(1, state.flightCompletesAtTick! - state.flightStartedAtTick!),
          ))
        : 0;
      const flightX = inFlight
        ? state.position.x + (state.flightTarget!.x - state.position.x) * flightProgress
        : state.position.x;
      const flightZ = inFlight
        ? state.position.y + (state.flightTarget!.y - state.position.y) * flightProgress
        : state.position.y;
      const flightArc = inFlight ? Math.sin(flightProgress * Math.PI) : 0;
      rig.group.visible = true;
      rig.group.position.set(
        (carrier?.x ?? flightX) + 0.5,
        0,
        (carrier?.y ?? flightZ) + 0.5,
      );
      rig.gem.position.set(
        0,
        carried
          ? carriedCoreHeight(state.key, time)
          : inFlight
            ? 0.4 + flightArc * 1.12
          : 0.4 + Math.sin(time * 2 + usedCoreRigs) * 0.025,
        0,
      );
      rig.gem.rotation.y = time * 0.45;
      rig.gem.scale.setScalar(
        state.pulseCore
          ? 1.25 + pulse * 0.2
          : inFlight
            ? 1 + flightArc * 0.36
            : 1,
      );
      rig.glow.position.copy(rig.gem.position);
      rig.light.position.copy(rig.gem.position);
      rig.glow.scale.setScalar(
        state.pulseCore
          ? 0.88 + pulse * 0.12
          : inFlight
            ? 0.72 + flightArc * 0.3 + pulse * 0.04
            : 0.72 + pulse * 0.07,
      );
      // Loose and in-flight Cores are neutral. Possession changes the object itself—not
      // merely its tether—so a carrier is identifiable at the wider spectator distance.
      if (carried) {
        rig.gemMaterial.color.set(accent).multiplyScalar(0.5);
        rig.gemMaterial.emissive.set(accent);
        rig.glowMaterial.color.set(accent);
        rig.light.color.set(accent);
      } else {
        rig.gemMaterial.color.set(ARC_CORE_NEUTRAL_PALETTE.body);
        rig.gemMaterial.emissive.set(ARC_CORE_NEUTRAL_PALETTE.emissive);
        rig.glowMaterial.color.set(ARC_CORE_NEUTRAL_PALETTE.glow);
        rig.light.color.set(ARC_CORE_NEUTRAL_PALETTE.light);
      }
      rig.gemMaterial.emissiveIntensity = carried ? 0.8 : 0.7;
      rig.glowMaterial.opacity = carried
        ? state.pulseCore ? 0.32 + pulse * 0.12 : 0.22 + pulse * 0.08
        : 0.14 + pulse * 0.06;
      rig.light.intensity = state.pulseCore
        ? 3 + pulse * 1.4
        : carried ? 2.2 + pulse * 0.6 : 1.6 + pulse * 0.4;
      rig.beam.visible = carried;
      rig.beamMaterial.color.set(accent);
      rig.beamMaterial.opacity = state.pulseCore
        ? 0.17 + pulse * 0.14
        : 0.055 + pulse * 0.045;
      const glyphIndex = state.sourceLabel.toLowerCase().includes('north')
        ? 0
        : state.sourceLabel.toLowerCase().includes('south')
          ? 1
          : 2;
      const atSourceWell = state.disposition === 'loose' && story.wells.some(
        (well) =>
          well.sourceLabel === state.sourceLabel &&
          well.position.x === state.position.x &&
          well.position.y === state.position.y,
      );
      for (const [glyph, sourceGlyph] of rig.sourceGlyphs.entries()) {
        // A live well already carries its own source mark. Repeating it over the Core
        // flattened the orb into a large token; only a dropped loose Core needs this
        // small identity plate beneath the sphere.
        sourceGlyph.visible =
          state.disposition === 'loose' && !atSourceWell && glyph === glyphIndex;
        sourceGlyph.position.set(0, 0.12, 0);
        sourceGlyph.rotation.y = -time * 1.1;
      }
    }
    // Arc Toss changes the public Core to in-flight on its accepted tick, while the v3
    // presentation releases after that tick's combat. Keep the one real sphere at carried
    // height until the release offset, then fly it through the remaining public interval.
    for (const arcTossFlight of activeFlights) {
      const rig = core(usedCoreRigs++);
      rig.group.userData.coreKey = arcTossFlight.key;
      rig.group.userData.coreMotion = 'arc-toss';
      const source = new THREE.Vector3(
        arcTossFlight.position.x + 0.5,
        1.18,
        arcTossFlight.position.y + 0.5,
      );
      const catchTransition = coreTransitionsAt(
        arcTossFlight.flightCompletesAtTick!,
      ).find((transition) =>
        transition.coreKey === arcTossFlight.key &&
        transition.kind === 'arc-toss-catch');
      const target = catchTransition?.position ??
        arcTossFlight.flightTarget ?? arcTossFlight.position;
      const progress = easeCore(
        (time - (
          arcTossFlight.flightStartedAtTick! + arcTossReleaseOffset
        )) /
          Math.max(
            0.001,
            arcTossFlight.flightCompletesAtTick! -
              arcTossFlight.flightStartedAtTick! - arcTossReleaseOffset,
          ),
      );
      const targetHeight = catchTransition
        ? carriedCoreHeight(
            arcTossFlight.key,
            arcTossFlight.flightCompletesAtTick!,
          )
        : 0.4;
      const position = new THREE.Vector3(
        source.x + (target.x + 0.5 - source.x) * progress,
        source.y + (targetHeight - source.y) * progress +
          Math.sin(progress * Math.PI) * 0.94,
        source.z + (target.y + 0.5 - source.z) * progress,
      );
      paintCoreInMotion(
        rig,
        arcTossFlight.sourceWellId,
        position,
        1 + Math.sin(progress * Math.PI) * 0.22,
        false,
        0.5 + 0.5 * Math.sin(time * Math.PI * 2.1),
        time,
        // Tossing relinquishes custody mechanically, but it is still the same visible
        // Core the carrier was holding. Keep its origin-lane treatment through flight
        // without retaining the carrier tether; loose presentation resumes on landing.
        true,
      );
    }
    for (const transition of custody) {
      const rig = core(usedCoreRigs++);
      rig.group.userData.coreKey = transition.coreKey;
      rig.group.userData.coreMotion = transition.kind;
      // An ordinary floor pickup is a levitation gesture, not a discrete state swap. The
      // Arc Toss catch is already at carried height when custody begins; every other
      // transition keeps the shared settle boundary.
      const settledProgress = transition.kind === 'arc-toss-catch'
        ? 1
        : transition.kind === 'pickup'
          ? tickFraction * tickFraction * (3 - 2 * tickFraction)
          : easeCore(tickFraction / STATE_SETTLES_AT);
      const beforeCore = replay.ticks[tickIndex]?.before.mode?.kind === 'arc-relay' &&
          'visibleCores' in replay.ticks[tickIndex]!.before.mode
        ? replay.ticks[tickIndex]!.before.mode.visibleCores.find(
            (candidate) => coreKey(candidate.coreId) === transition.coreKey,
          )
        : null;
      const ground = new THREE.Vector3(
        transition.position.x + 0.5,
        0.4,
        transition.position.y + 0.5,
      );
      // A loose Core may have drifted since the event position was authored, so its
      // visible pre-pickup tile is the honest launch point. An in-flight Core is
      // different: Arc Toss deliberately keeps its launch tile in the public snapshot
      // until the landing fact. Reusing that stale position here made the landed sphere
      // fly the whole route a second time before reaching its ally.
      if (
        beforeCore?.disposition === 'loose' &&
        transition.kind === 'pickup'
      ) {
        ground.set(
          beforeCore.position.x + 0.5,
          0.4,
          beforeCore.position.y + 0.5,
        );
      }
      const eventActorPosition = new THREE.Vector3(
        transition.position.x + 0.5,
        1.18,
        transition.position.y + 0.5,
      );
      const source = transition.sourceActor
        ? snapshotActorPoint(transition.sourceActor, tickIndex, 'before') ??
          eventActorPosition
        : transition.kind !== 'pickup' && beforeCore?.carrierActor
          ? new THREE.Vector3(
              beforeCore.position.x + 0.5,
              1.18,
              beforeCore.position.y + 0.5,
            )
          : ground;
      const target = transition.kind === 'bank'
        ? new THREE.Vector3(ground.x, 0.23, ground.z)
        : (transition.kind === 'pickup' ||
            transition.kind === 'arc-toss-catch') && transition.targetActor
          // The bot renderer deliberately glides across occupancy boundaries instead of
          // snapping to the tick's `after` tile. Pickup and an automatic Arc Toss catch
          // have to meet that same rendered body, otherwise the Core finishes above the
          // authoritative tile and jumps when custody becomes ordinary carried state.
          ? posePoint(transition.targetActor, actorPoses) ??
            snapshotActorPoint(transition.targetActor, tickIndex, 'after') ??
            eventActorPosition
        : transition.targetActor
          ? snapshotActorPoint(transition.targetActor, tickIndex, 'after') ??
            posePoint(transition.targetActor, actorPoses) ?? eventActorPosition
          : ground;
      const position = new THREE.Vector3().lerpVectors(source, target, settledProgress);
      const levitation = transition.kind === 'arc-toss-catch'
        ? 0
        : transition.kind === 'bank'
        ? Math.sin(settledProgress * Math.PI) * 0.42
        : Math.sin(settledProgress * Math.PI) * 0.28;
      position.y += levitation;
      if (transition.kind === 'arc-toss-catch') {
        // Arc Toss and its automatic catch are one continuous visual action. The engine
        // publishes a 2D landing followed by custody in the same tick; neither fact says
        // the Core touched the floor, so meet the receiver at carried height and follow
        // its rendered pose without replaying the ordinary floor-pickup levitation.
        position.y = carriedCoreHeight(transition.coreKey, time);
      } else if (transition.kind === 'pickup') {
        // Join the exact stable carried hover at the boundary. Applying the bob across
        // the whole ascent would move the floor contact; weighting it by pickup progress
        // keeps the start planted while making the final frame continuous.
        position.y +=
          (carriedCoreHeight(transition.coreKey, time) - 1.18) * settledProgress;
      }
      const scale = transition.kind === 'bank'
        ? Math.max(0.08, 1 - settledProgress ** 2 * 0.92)
        : 1 + Math.sin(settledProgress * Math.PI) * 0.12;
      const carried = transition.kind === 'handoff' ||
        transition.kind === 'arc-toss-catch' ||
        (transition.kind === 'pickup' && settledProgress > 0.5) ||
        (transition.kind === 'drop' && settledProgress < 0.5) ||
        transition.kind === 'bank';
      paintCoreInMotion(
        rig,
        transition.sourceWellId,
        position,
        scale,
        carried,
        0.5 + 0.5 * Math.sin(time * Math.PI * 2.1),
        time,
      );
      if (transition.kind === 'bank') {
        const reactor = reactors.find((candidate) =>
          candidate.group.position.distanceToSquared(
            new THREE.Vector3(target.x, 0, target.z),
          ) < 0.01,
        );
        if (reactor)
          reactor.ringMaterial.opacity = Math.max(
            reactor.ringMaterial.opacity,
            Math.sin(settledProgress * Math.PI) * 0.78,
          );
      }
    }
    for (let index = usedCoreRigs; index < cores.length; index++)
      cores[index]!.group.visible = false;
  };

  return { group, update };
}

function crestTexture(crest: ViewerEntrantPresentation['crest']): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 128;
  const ctx = canvas.getContext('2d')!;
  ctx.translate(64, 64);
  ctx.beginPath();
  if (crest.shape === 'diamond') {
    ctx.moveTo(0, -56); ctx.lineTo(56, 0); ctx.lineTo(0, 56); ctx.lineTo(-56, 0);
  } else if (crest.shape === 'hex') {
    ctx.moveTo(-32, -52); ctx.lineTo(32, -52); ctx.lineTo(58, 0); ctx.lineTo(32, 52); ctx.lineTo(-32, 52); ctx.lineTo(-58, 0);
  } else {
    ctx.arc(0, 0, 55, 0, Math.PI * 2);
  }
  ctx.closePath(); ctx.clip();
  ctx.fillStyle = crest.secondary; ctx.fillRect(-64, -64, 128, 128);
  ctx.fillStyle = crest.primary;
  if (crest.pattern === 'split') ctx.fillRect(0, -64, 64, 128);
  else if (crest.pattern === 'band') ctx.fillRect(-64, -18, 128, 36);
  else { ctx.beginPath(); ctx.arc(0, 0, 34, 0, Math.PI * 2); ctx.fill(); }
  ctx.fillStyle = crest.detail;
  ctx.font = '900 48px ui-monospace, monospace';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText(crest.mark.slice(0, 1).toUpperCase(), 0, 4);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 4;
  return texture;
}

function wellValueTexture(
  values: readonly { teamId: number; value: number; accent: string }[],
): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 192;
  canvas.height = 72;
  const ctx = canvas.getContext('2d')!;
  ctx.fillStyle = 'rgba(5, 10, 16, 0.9)';
  ctx.beginPath();
  ctx.roundRect(4, 4, 184, 64, 24);
  ctx.fill();
  const ordered = [...values].sort((left, right) => left.teamId - right.teamId);
  ordered.slice(0, 2).forEach((value, index) => {
    const x = index === 0 ? 51 : 141;
    ctx.strokeStyle = value.accent;
    ctx.lineWidth = 5;
    ctx.beginPath();
    ctx.arc(x, 36, 24, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = '#f8fafc';
    ctx.font = '900 32px ui-monospace, monospace';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(value.value), x, 38);
  });
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 4;
  return texture;
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
        isAttackEvent(event.type) && (event.from ?? event.to)
          ? {
              // The muzzle. Older wires spell it `from`; a generation-3 attack
              // carries its one `origin`, which normalizes to `to`.
              position: event.from ?? event.to!,
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
    for (const bolt of spentBoltsAt(replay, time, 'projectile-traversal')) {
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
 * An explicitly sweeping strike landing — deliberately not a regular projectile.
 *
 * The whole line exists at once: a hot red blade from the shooter through
 * the impact tile, flashing on at the resolve instant and burning out over
 * the tick, plus a disc where it landed. It inherits the aim ray's red so
 * ray → slash reads as one sentence — the line that was tracking becoming
 * the line that landed — while ordinary wind-up attacks keep their
 * team-accent projectile models and dissipation rings. Nothing here travels,
 * because nothing here is dodgeable.
 */
function buildStrikeSlashes(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (time: number) => void } {
  const group = new THREE.Group();
  const segmentGeometry = new THREE.PlaneGeometry(1, 0.16);
  segmentGeometry.rotateX(-Math.PI / 2);
  const impactGeometry = new THREE.CircleGeometry(0.3, 20);
  impactGeometry.rotateX(-Math.PI / 2);
  disposables.push(segmentGeometry, impactGeometry);

  const makePool = (geometry: THREE.BufferGeometry) => {
    const pool: THREE.Mesh[] = [];
    const borrow = (index: number) => {
      while (pool.length <= index) {
        const material = new THREE.MeshBasicMaterial({
          color: '#fda4a4',
          transparent: true,
          depthWrite: false,
          blending: THREE.AdditiveBlending,
          side: THREE.DoubleSide,
        });
        const mesh = new THREE.Mesh(geometry, material);
        mesh.visible = false;
        mesh.position.y = 0.05;
        group.add(mesh);
        pool.push(mesh);
        disposables.push(material);
      }
      return pool[index];
    };
    return { pool, borrow };
  };
  const segments = makePool(segmentGeometry);
  const impacts = makePool(impactGeometry);

  const update = (time: number) => {
    let segmentsUsed = 0;
    let impactsUsed = 0;
    for (const slash of strikeSlashesAt(
      replay,
      time,
      'projectile-traversal',
    )) {
      const heat = (1 - slash.age) ** 2;
      for (let index = 0; index < slash.points.length - 1; index++) {
        const from = slash.points[index];
        const to = slash.points[index + 1];
        const dx = to.x - from.x;
        const dz = to.y - from.y;
        const mesh = segments.borrow(segmentsUsed++);
        mesh.visible = true;
        mesh.position.set(
          (from.x + to.x) / 2 + 0.5,
          0.05,
          (from.y + to.y) / 2 + 0.5,
        );
        mesh.rotation.y = -Math.atan2(dz, dx);
        mesh.scale.set(Math.hypot(dx, dz), 1, 1);
        (mesh.material as THREE.MeshBasicMaterial).opacity = heat * 0.9;
      }
      const landed = slash.points[slash.points.length - 1];
      const impact = impacts.borrow(impactsUsed++);
      impact.visible = true;
      impact.position.set(landed.x + 0.5, 0.05, landed.y + 0.5);
      impact.scale.setScalar(0.8 + slash.age * 0.9);
      (impact.material as THREE.MeshBasicMaterial).opacity = heat * 0.95;
    }
    for (
      let index = segmentsUsed;
      index < segments.pool.length;
      index++
    ) {
      segments.pool[index].visible = false;
    }
    for (let index = impactsUsed; index < impacts.pool.length; index++) {
      impacts.pool[index].visible = false;
    }
  };

  return { group, update };
}

/**
 * The tracking ray of a declared strike in windup (owner direction
 * 2026-08): a thin line from the strike's frozen apex to the body it was
 * for at declare, FOLLOWING that body as it moves. When the anchor steps
 * off the wedge the ray fades to a ghost — the escape read — and the
 * slash then tells the truth about who actually ate the strike. Thinner
 * and dimmer than the slash so windup and landing can never be confused.
 */
function buildStrikeAims(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (time: number) => void } {
  const group = new THREE.Group();
  group.userData.kind = 'strike-aims';
  const geometry = new THREE.PlaneGeometry(1, 0.07);
  geometry.rotateX(-Math.PI / 2);
  disposables.push(geometry);

  const pool: THREE.Mesh[] = [];
  const borrow = (index: number) => {
    while (pool.length <= index) {
      const material = new THREE.MeshBasicMaterial({
        color: '#f87171',
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.visible = false;
      mesh.position.y = 0.045;
      mesh.userData.kind = 'strike-aim';
      group.add(mesh);
      pool.push(mesh);
      disposables.push(material);
    }
    return pool[index];
  };

  const update = (time: number) => {
    let used = 0;
    const tickIndex = Math.max(
      0,
      Math.min(Math.floor(time), replay.ticks.length - 1),
    );
    const mode = replay.ticks[tickIndex]?.after.mode;
    const signatureOwnedAims = mode?.kind === 'arc-relay' &&
        'visibleSignatures' in mode
      ? new Set(
          mode.visibleSignatures
            .filter(
              (signature) =>
                signature.phase === 'tell' &&
                (signature.signatureId === 'tractor-hook' ||
                  signature.signatureId === 'rail-line'),
            )
            .map((signature) => signature.ownerActor.actorKey),
        )
      : new Set<string>();
    for (const aim of strikeAimsAt(replay, time)) {
      // Hook and Rail already paint their complete public tell. Their underlying launch
      // also occupies pendingStrikes, but drawing the generic coral attack ray on top
      // creates two vertically separated lanes for one operation.
      if (signatureOwnedAims.has(aim.shooterActorKey)) continue;
      if (!aim.target) continue;
      const fromX = aim.origin.x + 0.5;
      const fromZ = aim.origin.y + 0.5;
      const toX = aim.target.x + 0.5;
      const toZ = aim.target.y + 0.5;
      const dx = toX - fromX;
      const dz = toZ - fromZ;
      const length = Math.hypot(dx, dz);
      if (length < 0.01) continue;
      const mesh = borrow(used++);
      mesh.visible = true;
      mesh.position.set((fromX + toX) / 2, 0.045, (fromZ + toZ) / 2);
      mesh.rotation.y = -Math.atan2(dz, dx);
      mesh.scale.set(length, 1, 1);
      const pulse = 0.75 + 0.25 * Math.sin(time * Math.PI * 6);
      (mesh.material as THREE.MeshBasicMaterial).opacity = aim.escaped
        ? 0.1
        : (0.2 + 0.25 * aim.urgency) * pulse;
    }
    for (let index = used; index < pool.length; index++) {
      pool[index].visible = false;
    }
  };

  return { group, update };
}

/**
 * The pathing lens: where the SELECTED body is walking, and the step it chose.
 *
 * Owner review 2026-08-09: "I still see a lot of pathing mistakes but can't put
 * a finger on it." That is a legibility problem, not a hunting problem. A body
 * walking confidently at a tile four legs behind it, and a body oscillating
 * between two tiles, look the same from outside — both just shuffle. The
 * destination separates them instantly, and the whole point of drawing it is to
 * turn "something is wrong with the movement" into a scene the owner can name a
 * tick number for.
 *
 * **Selected body only.** Eight of these at once would be a diagram, not an
 * arena, and the one thing the viewer is already good at is answering questions
 * about the body you clicked. It follows the interpolated pose rather than the
 * tick's integer tile, so the line stays attached while the body glides.
 *
 * Three marks, and the third is the one that earns the feature: a hairline to
 * the destination, a ring on it, and a small square on the tile the body's
 * CHOSEN action steps onto. Together they say goal, plan and act — a body whose
 * step points away from its own destination is a pathing bug you can see in one
 * frame, which no amount of watching hulls shuffle would ever have shown.
 *
 * Cyan, not red: this is instrumentation, not violence, and the arena spends
 * `#f87171` on strikes and the engaged mark.
 */
const LENS_COLOR = '#38bdf8';
const LENS_HEIGHT = 0.05;
const LENS_GAUGE = 0.05;
const LENS_TARGET_INNER = 0.26;
const LENS_TARGET_OUTER = 0.34;
const LENS_STEP_SIZE = 0.3;

function buildPathingLens(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): {
  group: THREE.Group;
  update: (
    time: number,
    presentation: TickPresentation,
    selectedUnitKey: ReplayStableUnitKey | null,
  ) => void;
} {
  const group = new THREE.Group();
  group.visible = false;

  const lineGeometry = new THREE.PlaneGeometry(1, LENS_GAUGE);
  lineGeometry.rotateX(-Math.PI / 2);
  const targetGeometry = new THREE.RingGeometry(
    LENS_TARGET_INNER,
    LENS_TARGET_OUTER,
    24,
  );
  targetGeometry.rotateX(-Math.PI / 2);
  const stepGeometry = new THREE.RingGeometry(
    LENS_STEP_SIZE * 0.62,
    LENS_STEP_SIZE,
    4,
  );
  stepGeometry.rotateX(-Math.PI / 2);
  const material = new THREE.MeshBasicMaterial({
    color: LENS_COLOR,
    transparent: true,
    opacity: 0.55,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
  disposables.push(lineGeometry, targetGeometry, stepGeometry, material);

  const line = new THREE.Mesh(lineGeometry, material);
  line.userData.cue = 'pathing-lens-line';
  line.position.y = LENS_HEIGHT;
  const target = new THREE.Mesh(targetGeometry, material);
  target.userData.cue = 'pathing-lens-target';
  target.position.y = LENS_HEIGHT;
  const step = new THREE.Mesh(stepGeometry, material);
  step.userData.cue = 'pathing-lens-step';
  step.position.y = LENS_HEIGHT;
  group.add(line, target, step);

  const update = (
    time: number,
    presentation: TickPresentation,
    selectedUnitKey: ReplayStableUnitKey | null,
  ) => {
    group.visible = false;
    line.visible = false;
    target.visible = false;
    step.visible = false;
    if (selectedUnitKey === null) return;
    const unit = presentation.units.find(
      (candidate) => candidate.unitKey === selectedUnitKey,
    );
    if (!unit || unit.status !== 'active') return;
    if (unit.destination === null && unit.nextStep === null) return;
    const pose = posesAt(replay, time).find(
      (candidate) => candidate.unitKey === selectedUnitKey,
    );
    if (!pose) return;
    group.visible = true;
    const fromX = pose.x + 0.5;
    const fromZ = pose.y + 0.5;
    if (unit.destination !== null) {
      const toX = unit.destination.x + 0.5;
      const toZ = unit.destination.y + 0.5;
      target.visible = true;
      target.position.set(toX, LENS_HEIGHT, toZ);
      const dx = toX - fromX;
      const dz = toZ - fromZ;
      const length = Math.hypot(dx, dz);
      // A body standing on its own destination gets the ring and no line: a
      // zero-length quad renders as a smear, and "arrived" is worth reading.
      if (length > LENS_TARGET_OUTER) {
        line.visible = true;
        line.position.set((fromX + toX) / 2, LENS_HEIGHT, (fromZ + toZ) / 2);
        line.rotation.y = -Math.atan2(dz, dx);
        line.scale.set(length, 1, 1);
      }
    }
    if (unit.nextStep !== null) {
      step.visible = true;
      step.position.set(
        unit.nextStep.x + 0.5,
        LENS_HEIGHT,
        unit.nextStep.y + 0.5,
      );
    }
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
