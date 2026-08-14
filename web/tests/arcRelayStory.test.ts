import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import test from 'node:test';
import * as THREE from 'three';
import {
  arcOriginAccent,
  buildOverlays,
  createPresenter,
  defaultPlaybackSpeed,
  posesAt,
  signedTravelByActor,
} from './.harness/harness.entry.js';
import { replayAudioEventsAt } from '../src/audio/replayAudioEvents.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import type {
  ReplayArcRelayFact,
  ReplayArcRelayModeState,
  ReplayCausalEvent,
  ReplayModel,
} from '../src/replayModel.ts';
import { ARC_CORE_NEUTRAL_PALETTE } from '../src/presentation/arcCorePalette.ts';

function arcRelayReplay(): ReplayModel {
  const replay = loadReplayJson(
    readFileSync(
      new URL('./fixtures/generic-mind-replay-v3.json', import.meta.url),
      'utf8',
    ),
  ).replay;
  assert.equal(replay.contract.kind, 'v3-generic');
  if (replay.contract.kind !== 'v3-generic') return replay;

  replay.contract.modeKind = 'arc-relay';
  replay.contract.modeId = 'arc-relay';
  replay.contract.mode = {
    kind: 'arc-relay',
    modeId: 'arc-relay',
    pendingRearmTicks: 8,
    coreRelocationIntervalTicks: 3,
    coresPerPulse: 3,
    pulsesToDestroyReactor: 3,
    orderedWellRegionIds: ['well-centre'],
  };
  replay.contract.rawContract.rules.gameMode = {
    kind: 'arc-relay',
    modeId: 'arc-relay',
    pendingRearmTicks: 8,
    coreRelocationIntervalTicks: 3,
    coresPerPulse: 3,
    pulsesToDestroyReactor: 3,
    orderedWellRegionIds: ['well-centre'],
    wells: [{
      wellId: 'well-centre',
      firstBirthTick: 0,
      cadenceTicks: 20,
      finalBirthTick: 100,
    }],
    victory: {
      kind: 'arc-relay',
      timeoutRanking: [],
      pulsesToDestroyReactor: 3,
    },
    scoreCatalog: [],
  } as typeof replay.contract.rawContract.rules.gameMode;
  replay.map.objectiveTiles = [];

  const actors = replay.ticks[0]!.after.actors.map((actor) => actor.identity);
  assert.equal(actors.length, 2);
  const coreId = { sourceWellId: 'well-centre', sourceOrdinal: 1 };
  const facts: (ReplayArcRelayFact | null)[] = [
    { kind: 'core-born', coreId, position: { x: 11, y: 7 } },
    {
      kind: 'core-picked-up',
      coreId,
      carrierActor: actors[0]!,
      position: { x: 8, y: 7 },
      nextRelocationTick: 4,
    },
    {
      kind: 'core-dropped',
      coreId,
      sourceActor: actors[0]!,
      position: { x: 10, y: 7 },
      nextRelocationTick: 5,
      dropKind: 'damage',
    },
    {
      kind: 'core-picked-up',
      coreId,
      carrierActor: actors[1]!,
      position: { x: 12, y: 7 },
      nextRelocationTick: 6,
    },
    {
      kind: 'core-banked',
      coreId,
      carrierActor: actors[1]!,
      teamId: 1,
      position: { x: 20, y: 7 },
      chargePips: 3,
    },
    {
      kind: 'pulse',
      teamId: 1,
      pulseOrdinal: 2,
      opposingReactorIntegrity: 1,
    },
  ];

  for (let index = 0; index < replay.ticks.length; index += 1) {
    const carrier = index === 1 ? actors[0]! : index === 3 ? actors[1]! : null;
    const mode: ReplayArcRelayModeState = {
      kind: 'arc-relay',
      modeId: 'arc-relay',
      wells: [{
        wellId: 'well-centre',
        position: { x: 11, y: 7 },
        nextScheduledBirthTick: 20,
        outstandingCoreId: index < 4 ? coreId : null,
        pendingCharge: false,
        rearmCompletesAtTick: null,
      }],
      reactors: [
        { teamId: 0, position: { x: 2, y: 7 }, chargePips: 2, integritySegments: 2 },
        { teamId: 1, position: { x: 20, y: 7 }, chargePips: 1, integritySegments: 3 },
      ],
      visibleCores: index < 4 ? [{
        coreId,
        position: index === 0 ? { x: 11, y: 7 } : index === 1 ? { x: 8, y: 7 } : index === 2 ? { x: 10, y: 7 } : { x: 12, y: 7 },
        disposition: carrier ? 'carried' : 'loose',
        carrierActor: carrier,
        nextRelocationTick: index + 3,
        flightTarget: null,
        flightCompletesAtTick: null,
      }] : [],
      visibleSignatures: [],
      latestPulseTeamId: index >= 5 ? 1 : null,
      latestPulseTick: index >= 5 ? 5 : null,
      pendingStrikes: [],
    };
    replay.ticks[index]!.before.mode = mode;
    replay.ticks[index]!.after.mode = mode;
    replay.ticks[index]!.before.objective = legacyObjective();
    replay.ticks[index]!.after.objective = legacyObjective();
    const fact = facts[index] ?? null;
    replay.ticks[index]!.events = fact ? [arcEvent(index, fact)] : [];
  }
  return replay;
}

function legacyObjective() {
  return {
    kind: 'legacy' as const,
    mode: 'none' as const,
    controlPressure: null,
    zoneTicks: [],
    completeness: 'legacy-derived' as const,
  };
}

function arcEvent(tick: number, fact: ReplayArcRelayFact): ReplayCausalEvent {
  return {
    eventId: `arc:${tick}`,
    tick,
    ordinal: 0,
    type: 'game-mode',
    teamId: null,
    unitId: null,
    sourceActor: null,
    targetActor: null,
    projectileId: null,
    from: null,
    to: null,
    fromFacing: null,
    toFacing: null,
    projectileHeading: null,
    fromFormId: null,
    toFormId: null,
    formTransitionStartedAtTick: null,
    formTransitionCompletesAtTick: null,
    actionPayload: null,
    actionId: null,
    actionCode: null,
    actionResult: null,
    amount: null,
    newHealth: null,
    lifecycleStatus: null,
    spawnReason: null,
    respawnAtTick: null,
    unlockAtTick: null,
    rebuildReadyAtTick: null,
    path: [],
    heading: null,
    shotProgram: null,
    programmedPath: null,
    arcRelayFact: fact,
  };
}

test('Arc Relay starts at full speed (owner ruling: half-speed read as sluggish)', () => {
  assert.equal(defaultPlaybackSpeed(arcRelayReplay()), 1);
});

test('Arc Relay presents the carrier and every possession beat', () => {
  const presenter = createPresenter(arcRelayReplay());
  assert.equal(presenter.at(1).arcRelay?.cue.headline.includes('PULSE CORE'), true);
  assert.equal(presenter.at(1).arcRelay?.cores[0]?.carrierTeamId, 0);
  assert.deepEqual(
    [0, 1, 2, 3, 4, 5].map((tick) => presenter.at(tick).arcRelay?.beat?.kind),
    ['birth', 'pickup', 'drop', 'steal', 'bank', 'pulse'],
  );
});

test('the depth-pack story explains 1/2/4 banks, double Pulses, and the next wave', () => {
  const replay = arcRelayReplay();
  assert.equal(replay.contract.kind, 'v3-generic');
  if (replay.contract.kind !== 'v3-generic' || replay.contract.mode.kind !== 'arc-relay') return;
  replay.contract.mode.teamRelativeCoreCharge = { ownWell: 1, centreWell: 2, enemyWell: 4 };
  replay.contract.mode.respawnWavePeriodTicks = 30;
  replay.contract.mode.respawnMinimumDeadTicks = 15;
  const rawMode = replay.contract.rawContract.rules.gameMode;
  assert.equal(rawMode.kind, 'arc-relay');
  if (rawMode.kind === 'arc-relay') {
    rawMode.teamRelativeCoreCharge = { ownWell: 1, centreWell: 2, enemyWell: 4 };
    rawMode.respawnWavePeriodTicks = 30;
    rawMode.respawnMinimumDeadTicks = 15;
  }
  for (const tick of replay.ticks) {
    for (const snapshot of [tick.before, tick.after]) {
      assert.equal(snapshot.mode?.kind, 'arc-relay');
      if (snapshot.mode?.kind !== 'arc-relay' || !('wells' in snapshot.mode)) continue;
      snapshot.mode.wells = [
        { wellId: 'north', position: { x: 5, y: 3 }, nextScheduledBirthTick: 30, outstandingCoreId: null, pendingCharge: false, rearmCompletesAtTick: null },
        ...snapshot.mode.wells,
        { wellId: 'south', position: { x: 18, y: 11 }, nextScheduledBirthTick: 30, outstandingCoreId: null, pendingCharge: false, rearmCompletesAtTick: null },
      ];
      snapshot.mode.nextRespawnWaveTick = 30;
      snapshot.mode.respawnWaveTeams = [
        { teamId: 0, liveBodies: 6, respawnsNextWave: 2 },
        { teamId: 1, liveBodies: 7, respawnsNextWave: 1 },
      ];
    }
  }
  const bank = replay.ticks[4]!.events[0]!.arcRelayFact;
  assert.equal(bank?.kind, 'core-banked');
  if (bank?.kind === 'core-banked') {
    bank.chargeAwarded = 4;
    bank.chargePips = 0;
  }

  const presenter = createPresenter(replay);
  const carried = presenter.at(1).arcRelay;
  assert.equal(carried?.cores[0]?.bankValue, 2);
  assert.equal(carried?.cores[0]?.pulsesOnBank, 1);
  assert.equal(carried?.cores[0]?.remainderOnBank, 1);
  assert.deepEqual(carried?.wells.find((well) => well.wellId === 'north')?.teamValues.map((entry) => entry.value), [1, 4]);
  assert.deepEqual(carried?.wells.find((well) => well.wellId === 'south')?.teamValues.map((entry) => entry.value), [4, 1]);
  assert.equal(carried?.respawnWave?.ticksRemaining, 29);
  assert.match(presenter.at(4).arcRelay?.beat?.detail ?? '', /\+4.*2 Pulses.*0\/3 held/);
});

test('a slow carrier glides through its forced relocation hold without predicting the next tile', () => {
  const replay = arcRelayReplay();
  assert.equal(replay.contract.kind, 'v3-generic');
  if (replay.contract.kind !== 'v3-generic') return;
  replay.contract.mode.coreRelocationIntervalTicks = 2;
  if (replay.contract.rawContract.rules.gameMode.kind === 'arc-relay')
    replay.contract.rawContract.rules.gameMode.coreRelocationIntervalTicks = 2;

  const actor = replay.ticks[0]!.before.actors[0]!;
  const actorKey = actor.actorKey;
  const coreId = { sourceWellId: 'well-centre', sourceOrdinal: 9 };
  const place = (tick: number, side: 'before' | 'after', x: number) => {
    const state = replay.ticks[tick]![side];
    const body = state.actors.find((candidate) => candidate.actorKey === actorKey);
    assert.ok(body);
    body.position = { x, y: 7 };
    const nextRelocationTick = tick < 2 ? 2 : 4;
    assert.equal(state.mode?.kind, 'arc-relay');
    if (state.mode?.kind !== 'arc-relay' || !('visibleCores' in state.mode)) return;
    state.mode.visibleCores = [{
      coreId,
      position: { x, y: 7 },
      disposition: 'carried',
      carrierActor: actor.identity,
      nextRelocationTick,
      flightTarget: null,
      flightCompletesAtTick: null,
    }];
  };

  place(0, 'before', 1);
  place(0, 'after', 2);
  place(1, 'before', 2);
  place(1, 'after', 2);
  // The following move deliberately turns around. Nothing sampled in the forced hold may
  // anticipate it; the renderer only finishes the already-revealed first relocation.
  place(2, 'before', 2);
  place(2, 'after', 1);

  const pose = (time: number) =>
    posesAt(replay, time).find((candidate) => candidate.actorKey === actorKey)!;
  assert.equal(pose(0).x, 1);
  assert.equal(pose(1).x, 1.5, 'crosses the occupancy edge on the move boundary');
  assert.equal(pose(1.5).x, 1.75, 'keeps constant travel through the forced hold');
  assert.equal(pose(2).x, 2, 'reaches the recorded tile before the next move');
  assert.ok(pose(0.999).x < 1.5, 'never enters the destination tile early');
  assert.ok(pose(1.001).x > 1.5, 'enters the destination after the exact boundary');
  assert.equal(pose(0.5).motionX, 0.5);
  assert.equal(pose(1.5).motionX, 0.5, 'lean and tread motion follow the visible glide');
  const travel = signedTravelByActor(replay).get(actorKey);
  assert.ok(travel);
  assert.equal(Math.abs(travel[1]!), 0.5, 'wheels cover the rendered half-tile move');
  assert.equal(Math.abs(travel[2]!), 1, 'wheels keep rolling through the forced hold');
  const epsilon = 0.001;
  assert.ok(
    Math.abs(
      (pose(1).x - pose(1 - epsilon).x) -
        (pose(1 + epsilon).x - pose(1).x),
    ) < 1e-9,
    'the forced hold does not introduce a tile-centre dwell',
  );
  assert.ok(
    Math.abs(
      Math.abs(pose(2).x - pose(2 - epsilon).x) -
        Math.abs(pose(2 + epsilon).x - pose(2).x),
    ) < 1e-9,
    'a newly revealed reversal changes direction without a dead frame',
  );

  const holdState = replay.ticks[1]!.after;
  assert.equal(holdState.mode?.kind, 'arc-relay');
  if (holdState.mode?.kind === 'arc-relay' && 'visibleCores' in holdState.mode) {
    const receiver = holdState.actors.find(
      (candidate) => candidate.actorKey !== actorKey,
    );
    assert.ok(receiver);
    holdState.mode.visibleCores[0]!.carrierActor = receiver.identity;
    assert.equal(
      pose(1.5).x,
      1.75,
      'a handoff moves possession but cannot snap the revealed glide to its centre',
    );
  }
});

test('the picked-up Core approaches the interpolated carrier pose continuously', () => {
  const replay = arcRelayReplay();
  // Late in tick 1 rather than half way through it. Possession is end-of-tick state and
  // the carrier is drawn gliding across the tick that took it, so a Core handed over at
  // the *start* of that glide arrives in the hull before the hull arrives at the Core --
  // four tiles early for a Kestrel's vector-dash (owner review 2026-08-10). The sphere
  // rides the interpolated pose, which is what this pins; it just may not board early.
  const time = 1.9;
  const story = createPresenter(replay).at(1).arcRelay;
  assert.ok(story);
  const core = story.cores[0];
  assert.ok(core?.carrierUnitKey);
  // The lane owns the sphere (owner ruling 2026-08-05): a Core reads as its
  // origin wherever it is; possession stays readable through the carrier
  // underneath and the tether.
  const accent = arcOriginAccent(core.sourceWellId);
  assert.ok(accent);
  const carrier = posesAt(replay, time).find(
    (pose) => pose.unitKey === core.carrierUnitKey,
  );
  assert.ok(carrier);

  const overlays = buildOverlays(replay);
  overlays.update(time, null, false);
  const objects: THREE.Object3D[] = [];
  overlays.group.traverse((node) => objects.push(node));
  const rig = objects.find((node) => node.userData.kind === 'arc-relay-core');
  const sphere = objects.find(
    (node) => node.userData.kind === 'arc-relay-core-sphere',
  ) as THREE.Mesh | undefined;
  const glow = objects.find(
    (node) => node.userData.kind === 'arc-relay-core-glow',
  ) as THREE.Sprite | undefined;

  assert.ok(rig);
  assert.ok(sphere);
  assert.ok(glow);
  assert.equal(sphere.geometry.type, 'SphereGeometry');
  assert.equal(
    (sphere.material as THREE.Material).type,
    'MeshLambertMaterial',
    'the energy Core has no glossy specular material',
  );
  assert.equal(sphere.castShadow, false, 'the luminous Core has no hard cast shadow');
  assert.ok(
    Math.abs(rig.position.x - (carrier.x + 0.5)) < 0.2,
    'the last pickup frames settle continuously toward the carrier',
  );
  assert.ok(
    Math.abs(rig.position.z - (carrier.y + 0.5)) < 0.2,
    'the last pickup frames stay aligned with the carrier lane',
  );
  assert.equal(sphere.position.x, 0, 'no neighbouring-tile orbit');
  assert.equal(sphere.position.z, 0, 'stays centred over the carrier');
  assert.ok(sphere.position.y > 0.85, `levitates above the hull (${sphere.position.y})`);
  assert.equal(glow.position.y, sphere.position.y);
  assert.ok(glow.scale.x > 0.6, 'soft glow extends beyond the sphere');
  assert.equal(
    (sphere.material as THREE.MeshLambertMaterial).emissive.getHexString(),
    new THREE.Color(accent).getHexString(),
    'the carried sphere keeps its origin-lane colour',
  );
  assert.equal(
    (glow.material as THREE.SpriteMaterial).color.getHexString(),
    new THREE.Color(accent).getHexString(),
    'the carried glow keeps its origin-lane colour',
  );

  // ...and the other half of the same rule: half way through the tick that takes it, the
  // Core is still where it was, not already in transit inside the carrier.
  overlays.update(1.5, null, false);
  const midGlide: THREE.Object3D[] = [];
  overlays.group.traverse((node) => midGlide.push(node));
  const midRig = midGlide.find(
    (node) =>
      node.visible &&
      node.userData.kind === 'arc-relay-core' &&
      node.userData.coreMotion === 'pickup',
  );
  assert.ok(midRig);
  assert.ok(
    midRig.position.x > carrier.x + 0.5 && midRig.position.x < 8.5,
    'the sphere levitates toward the carrier rather than snapping into possession',
  );

  overlays.dispose();
});

test('a born Core is the same glowing sphere hovering over its authoritative well', () => {
  const replay = arcRelayReplay();
  const core = createPresenter(replay).at(0).arcRelay?.cores[0];
  assert.ok(core);
  assert.equal(core.disposition, 'loose');

  const overlays = buildOverlays(replay);
  overlays.update(0, null, false);
  const objects: THREE.Object3D[] = [];
  overlays.group.traverse((node) => objects.push(node));
  const rig = objects.find(
    (node) => node.userData.kind === 'arc-relay-core',
  );
  const sphere = objects.find(
    (node) => node.userData.kind === 'arc-relay-core-sphere',
  ) as THREE.Mesh | undefined;
  const glow = objects.find(
    (node) => node.userData.kind === 'arc-relay-core-glow',
  ) as THREE.Sprite | undefined;

  assert.ok(rig);
  assert.ok(sphere);
  assert.ok(glow);
  assert.equal(rig.position.x, core.position.x + 0.5);
  assert.equal(rig.position.z, core.position.y + 0.5);
  assert.ok(sphere.position.y > 0.3, 'the loose sphere hovers visibly above the well');
  assert.equal(
    (sphere.material as THREE.MeshLambertMaterial).emissive.getHexString(),
    new THREE.Color(ARC_CORE_NEUTRAL_PALETTE.emissive).getHexString(),
    'a loose Core uses neutral energy rather than a team hue',
  );
  assert.equal(
    (glow.material as THREE.SpriteMaterial).color.getHexString(),
    new THREE.Color(ARC_CORE_NEUTRAL_PALETTE.glow).getHexString(),
    'a loose Core keeps the neutral glow',
  );
  const teamAccents = createPresenter(replay).at(0).arcRelay?.reactors
    .map((reactor) => new THREE.Color(reactor.accent).getHexString()) ?? [];
  assert.equal(teamAccents.length, 2);
  assert.equal(new Set(teamAccents).size, 2, 'the fixture exposes two team hues');
  assert.ok(
    !teamAccents.includes(
      new THREE.Color(ARC_CORE_NEUTRAL_PALETTE.emissive).getHexString(),
    ),
    'neutral Core emission is not either team colour',
  );
  assert.ok(
    !teamAccents.includes(
      new THREE.Color(ARC_CORE_NEUTRAL_PALETTE.glow).getHexString(),
    ),
    'neutral Core glow is not either team colour',
  );

  overlays.dispose();
});

test('Ember landmarks turn Arc homes, healing, and well charge into foundry machinery', () => {
  const replay = arcRelayReplay();
  assert.equal(replay.contract.kind, 'v3-generic');
  if (replay.contract.kind !== 'v3-generic') return;
  replay.map.presentation = {
    themeId: 'ember-forge',
    boundaryWall: 'perimeter',
    interiorWall: 'cover',
    wallGroups: [],
  };
  replay.map.frontline = null;
  const spawns = replay.contract.map.spawns;
  assert.equal(new Set(spawns.map((spawn) => spawn.teamId)).size, 2);
  replay.map.regions = [
    ...spawns.map((spawn) => ({
      regionId: `home-team-${spawn.teamId}`,
      tiles: [{ ...spawn.position }],
    })),
    { regionId: 'heal-north', tiles: [{ x: 11, y: 6 }] },
  ];
  const rules = replay.contract.rawContract.rules.gameMode;
  assert.equal(rules.kind, 'arc-relay');
  if (rules.kind !== 'arc-relay') return;
  rules.wells = [{
    wellId: 'well-centre',
    firstBirthTick: 0,
    cadenceTicks: 4,
    finalBirthTick: 20,
  }];
  for (const tick of replay.ticks) {
    for (const world of [tick.before, tick.after]) {
      assert.equal(world.mode?.kind, 'arc-relay');
      if (world.mode?.kind === 'arc-relay')
        world.mode.wells[0]!.nextScheduledBirthTick = 4;
    }
  }

  const overlays = buildOverlays(replay);
  overlays.update(2, null, false);
  const objects: THREE.Object3D[] = [];
  overlays.group.traverse((node) => objects.push(node));
  const spawnPads = objects.filter(
    (node) => node.userData.kind === 'arc-relay-spawn-pad',
  );
  const spawnSockets = objects.filter(
    (node) => node.userData.kind === 'arc-relay-spawn-socket',
  );
  const heal = objects.find(
    (node) => node.userData.kind === 'arc-relay-heal-pad',
  );
  const coolant = objects.find(
    (node) => node.userData.kind === 'arc-relay-heal-coolant',
  );
  const well = objects.find(
    (node) => node.userData.kind === 'arc-relay-well',
  );
  const reactors = objects.filter(
    (node) => node.userData.kind === 'arc-relay-reactor',
  );
  const progress = objects.find(
    (node) => node.userData.kind === 'arc-relay-well-birth-progress',
  ) as THREE.Mesh | undefined;

  assert.equal(spawnPads.length, spawns.length);
  assert.ok(spawnPads.every((pad) => pad.userData.themeId === 'ember-forge'));
  assert.equal(spawnSockets.length, spawns.length);
  assert.equal(heal?.userData.themeId, 'ember-forge');
  assert.ok(coolant, 'the Ember heal tile has a raised coolant reservoir');
  assert.equal(well?.userData.themeId, 'ember-forge');
  assert.equal(reactors.length, 2);
  assert.ok(reactors.every((reactor) => reactor.userData.themeId === 'ember-forge'));
  const reactorStates = createPresenter(replay).at(2).arcRelay?.reactors ?? [];
  assert.equal(reactorStates.length, 2);
  for (const reactor of reactors) {
    const teamId = reactor.userData.teamId as number;
    const state = reactorStates.find((candidate) => candidate.teamId === teamId);
    assert.ok(state);
    const descendants: THREE.Object3D[] = [];
    reactor.traverse((node) => descendants.push(node));
    const underGlow = descendants.find(
      (node) => node.userData.kind === 'arc-relay-reactor-underglow',
    ) as THREE.Mesh | undefined;
    const aperture = descendants.find(
      (node) => node.userData.kind === 'arc-relay-reactor-aperture-mask',
    ) as THREE.Mesh | undefined;
    const underLight = descendants.find(
      (node) => node.userData.kind === 'arc-relay-reactor-underlight',
    );
    assert.ok(underGlow);
    assert.ok(aperture);
    assert.equal(underLight, undefined, 'the neutral fixture owns no scene light');
    const glowMaterial = underGlow.material as THREE.MeshBasicMaterial;
    const apertureMaterial = aperture.material as THREE.MeshBasicMaterial;
    const accent = new THREE.Color(state.accent).getHexString();
    assert.equal(glowMaterial.color.getHexString(), accent);
    assert.equal(glowMaterial.blending, THREE.AdditiveBlending);
    assert.equal(glowMaterial.depthWrite, false);
    assert.ok(
      underGlow.position.y > aperture.position.y && underGlow.position.y < 0.03,
      'the team pool sits just above its dark aperture cap',
    );
    assert.ok(glowMaterial.opacity >= 0.2 && glowMaterial.opacity <= 0.4);
    assert.equal(apertureMaterial.depthTest, true);
    assert.equal(apertureMaterial.depthWrite, true);
    assert.equal(apertureMaterial.transparent, false);
  }
  assert.ok(progress);
  assert.equal(progress.userData.progress, 0.5);
  assert.equal(progress.userData.phase, 'scheduled-birth');
  assert.equal(progress.geometry.drawRange.count, 32 * 6);
  assert.ok(
    (progress.material as THREE.MeshBasicMaterial).opacity > 0.65,
    'the exact next-birth clock appears as a bright well charge arc',
  );

  overlays.dispose();
});

test('an airborne Core stays neutral until an authoritative pickup', () => {
  const replay = arcRelayReplay();
  const mode = replay.ticks[0]!.after.mode;
  assert.equal(mode?.kind, 'arc-relay');
  assert.ok(mode && 'visibleCores' in mode);
  const core = mode.visibleCores[0];
  assert.ok(core);
  core.disposition = 'in-flight';
  core.flightTarget = { x: core.position.x + 2, y: core.position.y };
  core.flightCompletesAtTick = 1;

  const overlays = buildOverlays(replay);
  overlays.update(0.5, null, false);
  const objects: THREE.Object3D[] = [];
  overlays.group.traverse((node) => objects.push(node));
  const sphere = objects.find(
    (node) => node.userData.kind === 'arc-relay-core-sphere',
  ) as THREE.Mesh | undefined;
  const glow = objects.find(
    (node) => node.userData.kind === 'arc-relay-core-glow',
  ) as THREE.Sprite | undefined;
  assert.ok(sphere);
  assert.ok(glow);
  assert.equal(
    (sphere.material as THREE.MeshLambertMaterial).emissive.getHexString(),
    new THREE.Color(ARC_CORE_NEUTRAL_PALETTE.emissive).getHexString(),
  );
  assert.equal(
    (glow.material as THREE.SpriteMaterial).color.getHexString(),
    new THREE.Color(ARC_CORE_NEUTRAL_PALETTE.glow).getHexString(),
  );
  overlays.dispose();
});

test('the 3D renderer moves the one authoritative Arc Toss Core through its public arc', () => {
  const replay = arcRelayReplay();
  const mode = replay.ticks[0]!.after.mode;
  assert.equal(mode?.kind, 'arc-relay');
  assert.ok(mode && 'visibleCores' in mode);
  const core = mode.visibleCores[0]!;
  const source = { ...core.position };
  const destination = { x: source.x + 4, y: source.y };
  core.disposition = 'in-flight';
  core.flightTarget = destination;
  core.flightCompletesAtTick = 2;
  mode.visibleSignatures = [{
    operationId: 'arc-toss-test',
    signatureId: 'arc-toss',
    signatureKind: 'arc-toss',
    ownerActor: replay.ticks[0]!.after.actors[0]!.identity,
    ownerTeamId: 0,
    phase: 'in-flight',
    startedTick: -1,
    completesAtTick: 2,
    endsAtTick: null,
    positions: [destination, source],
    targetActor: null,
    remainingCapacity: 0,
    suppressed: false,
  }];
  replay.ticks[0]!.before.mode = mode;
  const next = structuredClone(mode);
  next.visibleCores[0]!.flightCompletesAtTick = 2;
  next.visibleSignatures[0]!.startedTick = -1;
  replay.ticks[1]!.before.mode = next;
  replay.ticks[1]!.after.mode = next;

  const story = createPresenter(replay).at(1).arcRelay;
  assert.equal(story?.cores[0]?.flightStartedAtTick, 0);
  const overlays = buildOverlays(replay);
  const flightAt = (time: number) => {
    overlays.update(time, null, false);
    const objects: THREE.Object3D[] = [];
    overlays.group.traverse((node) => objects.push(node));
    const rig = objects.find(
      (node) => node.visible && node.userData.coreMotion === 'arc-toss',
    );
    const sphere = rig?.children.find(
      (node) => node.userData.kind === 'arc-relay-core-sphere',
    ) as THREE.Mesh | undefined;
    assert.ok(rig);
    assert.ok(sphere);
    return { rig, sphere };
  };
  const launch = flightAt(0);
  assert.equal(launch.rig.position.x, source.x + 0.5);
  assert.equal(launch.rig.position.z, source.y + 0.5);
  assert.equal(launch.sphere.position.y, 1.18);
  const originAccent = arcOriginAccent(core.coreId.sourceWellId);
  assert.equal(
    (launch.sphere.material as THREE.MeshLambertMaterial).emissive.getHexString(),
    new THREE.Color(originAccent).getHexString(),
  );
  const { rig, sphere } = flightAt(1);
  assert.ok(rig);
  assert.ok(sphere);
  assert.equal(rig.position.x, source.x + 2 + 0.5);
  assert.equal(rig.position.z, source.y + 0.5);
  assert.ok(sphere.position.y > 1.3, 'the real Core rises through a visible ballistic apex');
  assert.equal(
    (sphere.material as THREE.MeshLambertMaterial).emissive.getHexString(),
    new THREE.Color(originAccent).getHexString(),
    'Arc Toss keeps the same origin-lane Core treatment for the complete flight',
  );
  overlays.dispose();
});

test('an Arc Toss landing with an immediate pickup flows directly into carried height', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const landingTick = replay.ticks.findIndex((tick) => {
    const facts = [...tick.lifecycleEvents, ...tick.events]
      .flatMap((event) => event.arcRelayFact ? [event.arcRelayFact] : []);
    return facts.some(
      (fact) => fact.kind === 'core-relocated' &&
        fact.relocationKind === 'arc-toss-landing',
    ) && facts.some((fact) => fact.kind === 'core-picked-up');
  });
  assert.ok(landingTick >= 0, 'the review replay contains its Arc Toss landing/pickup beat');
  const facts = [
    ...replay.ticks[landingTick]!.lifecycleEvents,
    ...replay.ticks[landingTick]!.events,
  ].flatMap((event) => event.arcRelayFact ? [event.arcRelayFact] : []);
  const pickup = facts.find((fact) => fact.kind === 'core-picked-up');
  assert.ok(pickup && pickup.kind === 'core-picked-up');
  const key = `${pickup.coreId.sourceWellId}:${pickup.coreId.sourceOrdinal}`;
  const beforeMode = replay.ticks[landingTick]!.before.mode;
  assert.equal(beforeMode?.kind, 'arc-relay');
  assert.ok(beforeMode && 'visibleCores' in beforeMode);
  assert.equal(
    beforeMode.visibleCores.find(
      (core) => `${core.coreId.sourceWellId}:${core.coreId.sourceOrdinal}` === key,
    )?.disposition,
    'in-flight',
    'the public pre-landing snapshot still carries the launch position',
  );

  const overlays = buildOverlays(replay);
  const corePointAt = (time: number, motion: string) => {
    overlays.update(time, null, false);
    let rig: THREE.Object3D | null = null;
    overlays.group.traverse((node) => {
      if (node.visible &&
          node.userData.coreKey === key &&
          node.userData.coreMotion === motion)
        rig = node;
    });
    const sphere = rig?.children.find(
      (node) => node.userData.kind === 'arc-relay-core-sphere',
    ) as THREE.Mesh | undefined;
    assert.ok(rig, `${motion} owns Core ${key} at ${time}`);
    assert.ok(sphere);
    overlays.group.updateMatrixWorld(true);
    return new THREE.Vector3().setFromMatrixPosition(sphere.matrixWorld);
  };
  try {
    const justBeforeLanding = corePointAt(landingTick - 0.001, 'arc-toss');
    const start = corePointAt(landingTick, 'arc-toss-catch');
    assert.ok(
      Math.abs(start.x - (pickup.position.x + 0.5)) < 1e-6 &&
        Math.abs(start.z - (pickup.position.y + 0.5)) < 1e-6,
      'landing custody starts over the receiving ally',
    );
    assert.ok(
      start.distanceTo(justBeforeLanding) < 0.01,
      'the toss reaches carried height continuously at the custody boundary',
    );
    assert.ok(start.y > 1.1, 'the caught Core never returns to the floor');

    const middleTime = landingTick + 0.5;
    const middle = corePointAt(middleTime, 'arc-toss-catch');
    const receiver = posesAt(replay, middleTime).find(
      (pose) => pose.actorKey === pickup.carrierActor.actorKey,
    );
    assert.ok(receiver);
    assert.ok(
      middle.y > 1.1,
      'the Core remains at carried height instead of replaying a floor pickup',
    );
    assert.ok(
      Math.abs(middle.x - (receiver.x + 0.5)) < 1e-6 &&
        Math.abs(middle.z - (receiver.y + 0.5)) < 1e-6,
      'the caught Core follows the receiver immediately',
    );

    const end = corePointAt(landingTick + 0.999, 'arc-toss-catch');
    const settled = corePointAt(landingTick + 1, 'settled');
    assert.ok(
      end.distanceTo(settled) < 0.02,
      'the catch meets ordinary carried custody without an end snap',
    );
  } finally {
    overlays.dispose();
  }
});

test('pickup joins a moving carrier continuously at the custody boundary', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const pickupTick = replay.ticks.findIndex((tick) => {
    const pickup = [...tick.lifecycleEvents, ...tick.events]
      .map((event) => event.arcRelayFact)
      .find((fact) => fact?.kind === 'core-picked-up');
    if (!pickup || pickup.kind !== 'core-picked-up') return false;
    const before = tick.before.actors.find(
      (actor) => actor.actorKey === pickup.carrierActor.actorKey,
    );
    const after = tick.after.actors.find(
      (actor) => actor.actorKey === pickup.carrierActor.actorKey,
    );
    return before !== undefined && after !== undefined &&
      (before.position.x !== after.position.x || before.position.y !== after.position.y);
  });
  assert.ok(pickupTick >= 0, 'the review replay contains a moving pickup carrier');
  const pickup = [
    ...replay.ticks[pickupTick]!.lifecycleEvents,
    ...replay.ticks[pickupTick]!.events,
  ].map((event) => event.arcRelayFact)
    .find((fact) => fact?.kind === 'core-picked-up');
  assert.ok(pickup && pickup.kind === 'core-picked-up');
  const key = `${pickup.coreId.sourceWellId}:${pickup.coreId.sourceOrdinal}`;

  const overlays = buildOverlays(replay);
  const corePointAt = (time: number) => {
    overlays.update(time, null, false);
    let rig: THREE.Object3D | null = null;
    overlays.group.traverse((node) => {
      if (node.visible && node.userData.coreKey === key) rig = node;
    });
    assert.ok(rig, `Core ${key} is visible at ${time}`);
    const sphere = rig.children.find(
      (node) => node.userData.kind === 'arc-relay-core-sphere',
    );
    assert.ok(sphere);
    overlays.group.updateMatrixWorld(true);
    return new THREE.Vector3().setFromMatrixPosition(sphere.matrixWorld);
  };
  try {
    const lastPickup = corePointAt(pickupTick + 0.999);
    const firstCarried = corePointAt(pickupTick + 1);
    assert.ok(
      lastPickup.distanceTo(firstCarried) < 0.02,
      `pickup meets the carried pose without an end snap (${lastPickup.distanceTo(firstCarried)})`,
    );
  } finally {
    overlays.dispose();
  }
});

test('Core pickup, drop, handoff, and bank move the authoritative sphere', () => {
  const replay = arcRelayReplay();
  const actors = replay.ticks[0]!.after.actors.map((actor) => actor.identity);
  const born = replay.ticks[0]!.events[0]!.arcRelayFact;
  assert.equal(born?.kind, 'core-born');
  if (born?.kind !== 'core-born') return;
  replay.ticks[3]!.events = [
    arcEvent(3, {
      kind: 'core-handed-off',
      coreId: born.coreId,
      sourceActor: actors[0]!,
      targetActor: actors[1]!,
      position: { x: 12, y: 7 },
      nextRelocationTick: 6,
    }),
  ];

  const overlays = buildOverlays(replay);
  const motionAt = (time: number, expected: string) => {
    overlays.update(time, null, false);
    const objects: THREE.Object3D[] = [];
    overlays.group.traverse((node) => objects.push(node));
    const rig = objects.find(
      (node) => node.visible && node.userData.coreMotion === expected,
    );
    const sphere = rig?.children.find(
      (node) => node.userData.kind === 'arc-relay-core-sphere',
    ) as THREE.Mesh | undefined;
    assert.ok(rig, `${expected} owns a visible Core rig`);
    assert.ok(sphere);
    return { rig, sphere };
  };

  const pickupStart = motionAt(1, 'pickup');
  const pickupStartY = pickupStart.sphere.position.y;
  const pickupMiddle = motionAt(1.425, 'pickup');
  assert.equal(pickupStartY, 0.4);
  assert.ok(pickupMiddle.sphere.position.y > 0.65);
  const pickupSamples = Array.from({ length: 10 }, (_, index) =>
    motionAt(1 + index / 10, 'pickup').sphere.position.y,
  );
  const largestPickupStep = Math.max(
    ...pickupSamples.slice(1).map(
      (height, index) => Math.abs(height - pickupSamples[index]!),
    ),
  );
  assert.ok(
    largestPickupStep < 0.2,
    `pickup has no compressed vertical snap (${largestPickupStep})`,
  );
  assert.ok(pickupSamples.at(-1)! > 1.15, 'pickup reaches carried height smoothly');

  const dropStart = motionAt(2, 'drop');
  const dropStartY = dropStart.sphere.position.y;
  const dropMiddle = motionAt(2.425, 'drop');
  assert.equal(dropStartY, 1.18);
  assert.ok(dropMiddle.sphere.position.y > 0.65);

  const handoffStart = motionAt(3, 'handoff');
  const handoffStartX = handoffStart.rig.position.x;
  const handoffMiddle = motionAt(3.425, 'handoff');
  assert.notEqual(handoffMiddle.rig.position.x, handoffStartX);

  const bankStart = motionAt(4, 'bank');
  const bankStartScale = bankStart.sphere.scale.x;
  const bankMiddle = motionAt(4.425, 'bank');
  assert.ok(bankMiddle.sphere.scale.x < bankStartScale);
  assert.ok(bankMiddle.sphere.position.y > 0.4);
  overlays.dispose();
});

test('every Arc Relay possession beat drives its diegetic audio cue', () => {
  const replay = arcRelayReplay();
  assert.deepEqual(
    [0, 1, 2, 3, 4, 5].map((tick) =>
      replayAudioEventsAt(replay, tick).map((event) => event.cue),
    ),
    [
      ['arc-birth'],
      ['arc-pickup'],
      ['arc-drop'],
      ['arc-pickup'],
      ['arc-bank'],
      ['arc-pulse'],
    ],
  );
});

test('an allied Core handoff has its own family-matched possession cue', () => {
  const replay = arcRelayReplay();
  const actors = replay.ticks[0]!.after.actors.map((actor) => actor.identity);
  const born = replay.ticks[0]!.events[0]!.arcRelayFact;
  assert.equal(born?.kind, 'core-born');
  if (born?.kind !== 'core-born') return;
  replay.ticks[1]!.events = [
    arcEvent(1, {
      kind: 'core-handed-off',
      coreId: born.coreId,
      sourceActor: actors[0]!,
      targetActor: actors[1]!,
      position: { x: 9, y: 7 },
      nextRelocationTick: 4,
    }),
  ];

  assert.deepEqual(
    replayAudioEventsAt(replay, 1).map((event) => event.cue),
    ['arc-handoff'],
  );
});

test('Core lifecycle facts sound when the broadcast places them at tick start', () => {
  const replay = arcRelayReplay();
  const birth = replay.ticks[0]!.events.shift();
  assert.ok(birth);
  replay.ticks[0]!.lifecycleEvents.push(birth);

  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['arc-birth'],
  );
});
