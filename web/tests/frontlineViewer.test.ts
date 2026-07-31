import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { createCanvas } from '@napi-rs/canvas';
import {
  createPresenter,
  drawArena,
  posesAt,
} from './.harness/harness.entry.js';
import { loadReplayJson, loadReplayObject } from '../src/replayIngress.ts';
import { participantForActor } from '../src/replayParticipants.ts';
import type { ReplayModel } from '../src/replayModel.ts';
import type { ReplayV3Document } from '../src/replayWireV3.ts';
import { adaptReplayV3ToFrontline } from './support/replayFixtureInputs.ts';

const replay = loadReplayJson(
  readFileSync(
    new URL('./fixtures/frontline-replay-v2.json', import.meta.url),
    'utf8',
  ),
).replay;

test('generic replay-v3 presents exact stable units and actor lives', () => {
  const generic = loadReplayJson(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ).replay;
  const presenter = createPresenter(generic);
  const opening = presenter.at(0);

  assert.equal(presenter.tickCount, 2);
  assert.equal(presenter.maxHealth, 3);
  assert.deepEqual(
    opening.units.map((unit) => ({
      unitKey: unit.unitKey,
      actorKey: unit.actorKey,
      participantId: unit.participantId,
      actionId: unit.actionId,
    })),
    [
      {
        unitKey: 'generic:0:unit:0',
        actorKey: 'generic:0:unit:0:life:0',
        participantId: 10,
        actionId: 'shoot',
      },
      {
        unitKey: 'generic:1:unit:0',
        actorKey: 'generic:1:unit:0:life:0',
        participantId: 20,
        actionId: 'shoot',
      },
    ],
  );
});

test('generic Frontline replay-v3 presents contract tuning and the derived breach winner', () => {
  const source = JSON.parse(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ) as ReplayV3Document;
  const frontline = loadReplayObject(
    adaptReplayV3ToFrontline(source, 'base-breach'),
  ).replay;
  const final = createPresenter(frontline).at(frontline.ticks.length - 1);

  assert.deepEqual(final.objective, {
    kind: 'frontline',
    activePositionIndex: 2,
    positionCount: 3,
    claimingTeamId: null,
    captureProgress: 0,
    captureThreshold: 3,
    controlResumesAtTick: 0,
    captureTeamId: null,
    captureContested: false,
    capturePaused: false,
    holdOwnerTeamId: null,
    holdEndsAtTick: null,
    holdRemainingTicks: null,
    holdDurationTicks: null,
    winnerTeamId: 0,
    phase: 'participant-10 BREACHES',
    // A ruleset that declares neither the channel nor an economy reads
    // exactly as it did before either existed: every added fact is off, and
    // the renderers that key off them draw nothing.
    channel: false,
    channelGainCap: null,
    channelGain: null,
    channelingUnitCount: 0,
    screeningUnitCount: 0,
    captureRevert: null,
  });
  assert.equal(final.economy, null);
  assert.deepEqual(
    final.units.map((unit) => [unit.carriedScrap, unit.channelRole]),
    final.units.map(() => [0, null]),
  );
});

test('Frontline presentation carries the exact ratchet clocks and derives its countdown', () => {
  const source = JSON.parse(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ) as ReplayV3Document;
  const held = loadReplayObject(
    adaptReplayV3ToFrontline(source),
  ).replay;
  if (held.contract.kind !== 'v3-generic') {
    assert.fail('expected a generic replay-v3 contract');
  }
  const mode = held.contract.rawContract.rules.gameMode;
  if (mode.kind !== 'frontline') {
    assert.fail('expected a Frontline mode contract');
  }
  mode.capture.redeployPolicy =
    'advance-immediately-then-deny-enemy-regression-past-the-high-water-mark-through-configured-hold-ticks';
  mode.capture.ratchetHoldTicks = 40;

  const control = held.ticks[0]!.after.objective;
  if (control.kind !== 'frontline') {
    assert.fail('expected Frontline objective state');
  }
  control.activePositionIndex = 2;
  control.controlResumesAtTick = 6;
  control.holdOwnerTeamId = 0;
  control.holdEndsAtTick = control.nextTick + 40;

  const objective = createPresenter(held).at(0).objective;
  assert.equal(objective?.kind, 'frontline');
  if (objective?.kind !== 'frontline') return;
  assert.deepEqual(
    {
      activePositionIndex: objective.activePositionIndex,
      holdOwnerTeamId: objective.holdOwnerTeamId,
      holdEndsAtTick: objective.holdEndsAtTick,
      holdRemainingTicks: objective.holdRemainingTicks,
      holdDurationTicks: objective.holdDurationTicks,
      phase: objective.phase,
    },
    {
      activePositionIndex: 2,
      holdOwnerTeamId: 0,
      holdEndsAtTick: control.nextTick + 40,
      holdRemainingTicks: 40,
      holdDurationTicks: 40,
      phase: 'participant-10 RATCHET · 40 TICKS · REDEPLOY T6',
    },
  );
});

test('Frontline presentation resolves net objective weight instead of treating every two-team presence as frozen', () => {
  const source = JSON.parse(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ) as ReplayV3Document;
  const weighted = loadReplayObject(
    adaptReplayV3ToFrontline(source),
  ).replay;
  if (weighted.contract.kind !== 'v3-generic') {
    assert.fail('expected a generic replay-v3 contract');
  }
  const mode = weighted.contract.rawContract.rules.gameMode;
  if (mode.kind !== 'frontline') {
    assert.fail('expected a Frontline mode contract');
  }
  mode.capture.controlPolicy =
    'net-positive-objective-weight-difference-scales-gain-non-positive-applies-configured-decay-opposition-erodes-to-neutral';

  const baseForm = weighted.forms.find(
    (form) => form.formId === 'mobile',
  );
  assert.ok(baseForm);
  baseForm.objectiveWeight = 1;
  weighted.forms.push({
    ...baseForm,
    formId: 'heavy-mobile',
    objectiveWeight: 2,
  });

  const active = weighted.map.frontline?.positions.find(
    (position) => position.positionIndex === 1,
  );
  assert.ok(active);
  active.tiles = [
    { x: 4, y: 3 },
    { x: 5, y: 3 },
  ];
  const actors = weighted.ticks[0]!.after.actors;
  const teamZero = actors.find(
    (actor) => actor.identity.teamId === 0,
  );
  const teamOne = actors.find(
    (actor) => actor.identity.teamId === 1,
  );
  assert.ok(teamZero);
  assert.ok(teamOne);
  teamZero.formId = 'heavy-mobile';
  teamZero.position = { x: 4, y: 3 };
  teamOne.formId = 'mobile';
  teamOne.position = { x: 5, y: 3 };

  const net = createPresenter(weighted).at(0).objective;
  assert.equal(net?.kind, 'frontline');
  if (net?.kind !== 'frontline') return;
  assert.equal(net.captureTeamId, 0);
  assert.equal(net.captureContested, false);

  mode.capture.controlPolicy =
    'binary-positive-weight-per-team-no-stacking-non-sole-applies-configured-decay-opposition-erodes-to-neutral';
  const binary = createPresenter(weighted).at(0).objective;
  assert.equal(binary?.kind, 'frontline');
  if (binary?.kind !== 'frontline') return;
  assert.equal(binary.captureTeamId, null);
  assert.equal(binary.captureContested, true);
});

test('generic replay-v3 derives form mobility from allowed actions, not ground occupancy', () => {
  const generic = loadReplayJson(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-frontline-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ).replay;
  const form = generic.forms.find((candidate) => candidate.formId === 'mobile');

  assert.ok(form);
  assert.equal(form.movementLayer, 'ground');
  assert.equal(form.canMove, false);
  assert.equal(form.canShoot, true);
  if (generic.contract.kind !== 'v3-generic') {
    assert.fail('expected a generic replay-v3 contract');
  }
  const contractForm = generic.contract.rules.forms.find(
    (candidate) => candidate.id === 'mobile',
  );
  assert.equal(contractForm?.canMove, false);
  assert.equal(contractForm?.canShoot, true);
});

test('actor-life interpolation adds fabricated lives without morphing primes', () => {
  assert.deepEqual(
    posesAt(replay, 0.5).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(
    posesAt(replay, 1.5).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(
    posesAt(replay, 2.25).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:0:unit:1:life:0',
      'frontline:1:unit:0:life:0',
      'frontline:1:unit:1:life:0',
    ],
  );
});

test('Frontline presentation follows stable units through fabrication and anchoring', () => {
  const presenter = createPresenter(replay);
  const opening = presenter.at(0);
  const queued = presenter.at(1);
  const fabricated = presenter.at(2);
  const anchored = presenter.at(9);

  assert.equal(opening.objective?.kind, 'frontline');
  assert.equal(
    opening.objective?.kind === 'frontline'
      ? opening.objective.captureThreshold
      : null,
    3,
  );
  assert.deepEqual(
    opening.units.map((unit) => unit.unitKey),
    queued.units.map((unit) => unit.unitKey),
  );
  assert.deepEqual(
    queued.units.map((unit) => unit.status),
    [
      'active',
      'fabrication-queued',
      'locked',
      'active',
      'fabrication-queued',
      'locked',
    ],
  );
  assert.equal(
    fabricated.units.find(
      (unit) => unit.teamId === 0 && unit.unitId === 1,
    )?.actorKey,
    'frontline:0:unit:1:life:0',
  );
  assert.equal(
    anchored.units.find(
      (unit) => unit.teamId === 0 && unit.unitId === 1,
    )?.formId,
    'turret',
  );

  const retainedDestroyedActor = structuredClone(replay);
  const actor = retainedDestroyedActor.ticks[2]!.after.actors[0]!;
  actor.status = 'destroyed';
  actor.health = 4;
  actor.cooldown = 7;
  actor.energy = 9;
  retainedDestroyedActor.ticks[2]!.after.units[0]!.lifecycleStatus =
    'rebuilding';
  const destroyed = createPresenter(retainedDestroyedActor).at(2).units[0]!;
  assert.equal(destroyed.actorKey, null);
  assert.equal(destroyed.lifeId, null);
  assert.equal(destroyed.health, 0);
  assert.equal(destroyed.cooldown, 0);
  assert.equal(destroyed.energy, null);
});

test('Frontline, stationary 360 forms, and attributed projectiles render', () => {
  const base = frameHash(replay, 2.25);
  const blank = createHash('sha256')
    .update(createCanvas(640, 480).toBuffer('image/png'))
    .digest('hex');
  assert.notEqual(base, blank);

  const turretReplay = structuredClone(replay);
  for (const world of [
    turretReplay.ticks[2]!.before,
    turretReplay.ticks[2]!.after,
  ]) {
    for (const unit of world.units) {
      unit.formId = 'turret';
      if (unit.activeActorKey) {
        const actor = world.actors.find(
          (candidate) => candidate.actorKey === unit.activeActorKey,
        );
        if (actor) actor.formId = 'turret';
      }
    }
    for (const actor of world.actors) actor.formId = 'turret';
  }
  assert.notEqual(frameHash(turretReplay, 2.25), base);

  const projectileReplay = structuredClone(replay);
  const traversal = projectileReplay.ticks[10]!.projectileTraversals[0]!;
  projectileReplay.ticks[8]!.after.projectiles = [
    {
      projectileId: 'old-life-projectile',
      ownerActor: traversal.ownerActor,
      ownerActorKey: traversal.ownerActorKey,
      position: { x: 4, y: 2 },
      launchDirection: traversal.launchDirection,
      heading: traversal.heading,
      shotProgram: traversal.shotProgram,
      programmedPath: traversal.programmedPath,
      ticksUntilAdvance: 1,
      remainingTiles: 2,
      tilesPerAdvance: 1,
      nextProgrammedPathIndex: null,
      tilesTraveled: null,
      phase: null,
    },
  ];
  assert.equal(
    participantForActor(projectileReplay, traversal.ownerActor)?.name,
    'Fixture Zero',
  );
  assert.notEqual(
    frameHash(projectileReplay, 8.25),
    frameHash(replay, 8.25),
  );
});

test('Canvas Frontline fields render neutral, build, erosion, contest, and ratchet as distinct states', () => {
  const base = captureFrameReplay();
  const neutral = captureFrameState(base, {
    weights: [0, 0],
  });
  const building = captureFrameState(base, {
    claimingTeamId: 0,
    captureProgress: 2,
    weights: [2, 1],
    controlPolicy: 'net',
  });
  const eroding = captureFrameState(base, {
    claimingTeamId: 0,
    captureProgress: 2,
    weights: [1, 2],
    controlPolicy: 'net',
  });
  const contested = captureFrameState(base, {
    claimingTeamId: 0,
    captureProgress: 2,
    weights: [1, 1],
    controlPolicy: 'binary',
  });
  const earlyHold = captureFrameState(base, {
    weights: [0, 0],
    holdOwnerTeamId: 1,
    holdRemainingTicks: 40,
  });
  const lateHold = structuredClone(earlyHold);
  const lateObjective = lateHold.ticks[0]!.after.objective;
  assert.equal(lateObjective.kind, 'frontline');
  if (lateObjective.kind !== 'frontline') return;
  lateObjective.holdEndsAtTick = lateObjective.nextTick + 5;

  const hashes = [
    neutral,
    building,
    eroding,
    contested,
    earlyHold,
    lateHold,
  ].map((candidate) => frameHash(candidate, 0.5));
  assert.equal(
    new Set(hashes).size,
    hashes.length,
    'identical bot bodies and one footprint get a distinct Canvas treatment for every exact capture state',
  );
});

test('Canvas draws the channel, its two reverts, and loose scrap as distinct pictures', () => {
  const base = captureFrameReplay();
  // The same claim, on the same footprint, with the same two bodies standing
  // on it — everything that differs between these frames is one of the two new
  // mechanics. If any pair collides, the arena is not saying which happened.
  const channelling = channelState(base, { captureProgress: 4 });
  const interrupted = channelState(base, {
    captureProgress: 1,
    previousProgress: 4,
    damageAt: { x: 3, y: 3 },
  });
  const eroded = channelState(base, {
    captureProgress: 1,
    previousProgress: 4,
  });
  const carrying = channelState(base, {
    captureProgress: 4,
    carried: 4,
  });
  const piled = channelState(base, {
    captureProgress: 4,
    piles: [
      { position: { x: 6, y: 2 }, amount: 6, expiresAtTick: 80 },
      { position: { x: 2, y: 5 }, amount: 1, expiresAtTick: 3 },
    ],
  });

  const hashes = [
    channelling,
    interrupted,
    eroded,
    carrying,
    piled,
  ].map((candidate) => frameHash(candidate, 0.5));
  assert.equal(
    new Set(hashes).size,
    hashes.length,
    'a channel, an interrupt, an erosion, a loaded courier and loose scrap each get their own Canvas treatment',
  );
});

test('same-tick anchoring telegraphs before the body becomes a turret', () => {
  const anchored = structuredClone(replay);
  const tick = anchored.ticks[2]!;
  const before = tick.before.actors[0]!;
  const after = tick.after.actors.find(
    (candidate) => candidate.actorKey === before.actorKey,
  )!;
  before.formId = 'child-mobile';
  before.pendingFormTransition = null;
  after.formId = 'turret';
  after.pendingFormTransition = null;

  const template = anchored.ticks[0]!.events[0]!;
  tick.events = [
    {
      ...template,
      eventId: 'resolution:2:0',
      tick: 2,
      ordinal: 0,
      type: 'form-transition-started',
      teamId: before.identity.teamId,
      unitId: before.identity.unitId,
      sourceActor: before.identity,
      targetActor: null,
      projectileId: null,
      from: { ...before.position },
      to: { ...before.position },
      fromFacing: before.facing,
      toFacing: before.facing,
      projectileHeading: null,
      fromFormId: 'child-mobile',
      toFormId: 'turret',
      formTransitionStartedAtTick: 2,
      formTransitionCompletesAtTick: 2,
      actionPayload: {
        shotProgram: null,
        direction: null,
        launchHeading: null,
        unitKey: null,
        formTargetId: 'turret',
      },
      actionId: 'transform',
      actionCode: 101,
      actionResult: 'success',
      newHealth: after.health,
    },
  ];

  const windingUp = posesAt(anchored, 2.25)[0]!;
  assert.equal(windingUp.formId, 'child-mobile');
  assert.deepEqual(windingUp.pendingFormTransition, {
    fromFormId: 'child-mobile',
    toFormId: 'turret',
    startedAtTick: 2,
    completesAtTick: 2,
  });

  const transformed = posesAt(anchored, 2.99)[0]!;
  assert.equal(transformed.formId, 'turret');
  assert.equal(transformed.pendingFormTransition, null);

  const withoutTelegraph = structuredClone(anchored);
  withoutTelegraph.ticks[2]!.events = [];
  assert.notEqual(
    frameHash(anchored, 2.25),
    frameHash(withoutTelegraph, 2.25),
  );
});

function frameHash(source: ReplayModel, time: number): string {
  const canvas = createCanvas(640, 480);
  const context = canvas.getContext('2d');
  drawArena(
    context as unknown as CanvasRenderingContext2D,
    source,
    { time, selectedUnitKey: null, showVisibility: false },
    640,
    480,
  );
  return createHash('sha256')
    .update(canvas.toBuffer('image/png'))
    .digest('hex');
}

function captureFrameReplay(): ReplayModel {
  const source = JSON.parse(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ) as ReplayV3Document;
  const candidate = loadReplayObject(
    adaptReplayV3ToFrontline(source),
  ).replay;
  if (candidate.contract.kind !== 'v3-generic') {
    assert.fail('expected a generic replay-v3 contract');
  }
  const mode = candidate.contract.rawContract.rules.gameMode;
  if (mode.kind !== 'frontline') {
    assert.fail('expected a Frontline mode contract');
  }
  mode.capture.redeployPolicy =
    'advance-immediately-then-deny-enemy-regression-past-the-high-water-mark-through-configured-hold-ticks';
  mode.capture.ratchetHoldTicks = 40;

  const baseForm = candidate.forms.find(
    (form) => form.formId === 'mobile',
  );
  assert.ok(baseForm);
  candidate.forms.push(
    {
      ...baseForm,
      formId: 'canvas-team-zero',
      objectiveWeight: 1,
    },
    {
      ...baseForm,
      formId: 'canvas-team-one',
      objectiveWeight: 1,
    },
  );

  const definition = candidate.map.frontline;
  assert.ok(definition);
  const activePositionIndex = 1;
  const tiles = [
    { x: 3, y: 3 },
    { x: 4, y: 3 },
  ];
  const position = definition.positions.find(
    (entry) => entry.positionIndex === activePositionIndex,
  );
  assert.ok(position);
  position.tiles = tiles;
  for (const world of [
    candidate.ticks[0]!.before,
    candidate.ticks[0]!.after,
  ]) {
    for (const actor of world.actors) {
      const teamId = actor.identity.teamId;
      actor.position = { ...tiles[teamId]! };
      actor.formId =
        teamId === 0 ? 'canvas-team-zero' : 'canvas-team-one';
    }
  }
  const objective = candidate.ticks[0]!.after.objective;
  assert.equal(objective.kind, 'frontline');
  if (objective.kind !== 'frontline') return candidate;
  objective.activePositionIndex = activePositionIndex;
  objective.claimingTeamId = null;
  objective.captureProgress = 0;
  objective.controlResumesAtTick = objective.nextTick;
  objective.holdOwnerTeamId = null;
  objective.holdEndsAtTick = null;
  return candidate;
}

/**
 * The same one-tick capture fixture, under the channel and the scrap economy.
 *
 * Both mechanics are read from the normalized model, so the states are built
 * there: the previous claim comes from the initial world (which is what tick
 * zero's revert compares against), the interrupt comes from a damage event
 * landing on a claimant standing in the region, and the economy comes from the
 * declared contract block plus the tick's own mode state.
 */
function channelState(
  base: ReplayModel,
  {
    captureProgress,
    previousProgress = captureProgress,
    damageAt = null,
    carried = 0,
    piles = [],
  }: {
    captureProgress: number;
    previousProgress?: number;
    damageAt?: { x: number; y: number } | null;
    carried?: number;
    piles?: {
      position: { x: number; y: number };
      amount: number;
      expiresAtTick: number;
    }[];
  },
): ReplayModel {
  const candidate = captureFrameState(base, {
    claimingTeamId: 0,
    captureProgress,
    weights: [1, 1],
  });
  if (candidate.contract.kind !== 'v3-generic')
    assert.fail('expected a generic replay-v3 contract');
  const mode = candidate.contract.rawContract.rules.gameMode;
  if (mode.kind !== 'frontline')
    assert.fail('expected a Frontline mode contract');
  mode.capture.controlPolicy =
    'stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-opposition-erodes-at-multiple-then-builds';
  mode.capture.stationaryGainMultiplierCap = 2;
  mode.capture.threshold = 8;
  mode.scrapEconomy = {
    veinSites: [{ x: 6, y: 2 }],
    veinFirstSpawnTick: 40,
    veinSpawnIntervalTicks: 40,
    veinLastSpawnTick: 120,
    veinAmount: 6,
    wreckAmount: 1,
    assayAmount: 1,
    carryCapacity: 6,
    pileLifetimeTicks: 80,
    maxSimultaneousPiles: 16,
    bankRegionIds: [],
    upgradeScope: 'prime-slot-lives-only',
    maxTotalTiers: 3,
    purchaseMode: 'invest-action',
    tracks: [
      {
        trackId: 'edge',
        effect: 'mobile-attack-travel-tiles-delta',
        perTierMagnitude: 1,
        maxTier: 2,
        tierCosts: [10, 10],
      },
    ],
  };

  const initial = candidate.initialWorld?.objective;
  if (initial?.kind === 'frontline') {
    initial.claimingTeamId = 0;
    initial.captureProgress = previousProgress;
    initial.activePositionIndex =
      candidate.ticks[0]!.after.objective.kind === 'frontline'
        ? candidate.ticks[0]!.after.objective.activePositionIndex
        : initial.activePositionIndex;
  }

  const tick = candidate.ticks[0]!;
  const claimant = tick.after.actors.find(
    (actor) => actor.identity.teamId === 0,
  )!;
  if (damageAt !== null) {
    const template = tick.events[0]!;
    tick.events = [
      {
        ...template,
        eventId: 'channel:damage',
        type: 'damage',
        teamId: 1,
        sourceActor: null,
        targetActor: claimant.identity,
        from: null,
        to: { ...damageAt },
        amount: previousProgress - captureProgress,
      },
    ];
  } else {
    tick.events = [];
  }

  const modeState = tick.after.mode;
  if (modeState?.kind === 'frontline') {
    modeState.scrapTeams = [
      { teamId: 0, bank: 4, tierLevels: [0] },
      { teamId: 1, bank: 1, tierLevels: [0] },
    ];
    modeState.scrapPiles = piles.map((pile) => ({
      position: { ...pile.position },
      amount: pile.amount,
      expiresAtTick: pile.expiresAtTick,
    }));
  }

  // A load is published by the observation of the tick that *follows* the
  // pickup, so every tick carries it here rather than only the one being
  // drawn.
  for (const each of candidate.ticks)
    for (const turn of each.actorTurns) {
      if (turn.observation.self?.actor.kind === 'exact')
        turn.observation.self.carriedScrap =
          turn.observation.self.actor.identity.teamId === 0 ? carried : 0;
    }

  return candidate;
}

function captureFrameState(
  base: ReplayModel,
  {
    claimingTeamId = null,
    captureProgress = 0,
    weights,
    controlPolicy = 'binary',
    holdOwnerTeamId = null,
    holdRemainingTicks = null,
  }: {
    claimingTeamId?: number | null;
    captureProgress?: number;
    weights: [number, number];
    controlPolicy?: 'binary' | 'net';
    holdOwnerTeamId?: number | null;
    holdRemainingTicks?: number | null;
  },
): ReplayModel {
  const candidate = structuredClone(base);
  if (candidate.contract.kind !== 'v3-generic') {
    assert.fail('expected a generic replay-v3 contract');
  }
  const mode = candidate.contract.rawContract.rules.gameMode;
  if (mode.kind !== 'frontline') {
    assert.fail('expected a Frontline mode contract');
  }
  mode.capture.controlPolicy =
    controlPolicy === 'net'
      ? 'net-positive-objective-weight-difference-scales-gain-non-positive-applies-configured-decay-opposition-erodes-to-neutral'
      : 'binary-positive-weight-per-team-no-stacking-non-sole-applies-configured-decay-opposition-erodes-to-neutral';
  const teamZeroForm = candidate.forms.find(
    (form) => form.formId === 'canvas-team-zero',
  );
  const teamOneForm = candidate.forms.find(
    (form) => form.formId === 'canvas-team-one',
  );
  assert.ok(teamZeroForm);
  assert.ok(teamOneForm);
  teamZeroForm.objectiveWeight = weights[0];
  teamOneForm.objectiveWeight = weights[1];

  const objective = candidate.ticks[0]!.after.objective;
  assert.equal(objective.kind, 'frontline');
  if (objective.kind !== 'frontline') return candidate;
  objective.claimingTeamId = claimingTeamId;
  objective.captureProgress = captureProgress;
  objective.holdOwnerTeamId = holdOwnerTeamId;
  objective.holdEndsAtTick =
    holdRemainingTicks === null
      ? null
      : objective.nextTick + holdRemainingTicks;
  return candidate;
}
