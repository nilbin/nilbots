import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
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

test('the carried Core is a sphere levitating on the interpolated carrier pose', () => {
  const replay = arcRelayReplay();
  const time = 1.5;
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
  assert.equal(rig.position.x, carrier.x + 0.5);
  assert.equal(rig.position.z, carrier.y + 0.5);
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
      ['arc-steal'],
      ['arc-bank'],
      ['arc-pulse'],
    ],
  );
});
