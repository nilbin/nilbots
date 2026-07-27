import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  decodeReplay,
  decodeReplayJson,
} from '../src/replayNormalize.ts';
import type {
  ReplayV2CompleteDocument,
  ReplayV2Document,
} from '../src/replayWireV2.ts';
import { replayV2FixtureInput } from './support/replayFixtureInputs.ts';

const JS_UNSAFE_SEED = '9007199254740993';

function readEngineFixture(name: string): {
  raw: string;
  parsed: unknown;
} {
  const raw = readFileSync(
    new URL(`./fixtures/${name}`, import.meta.url),
    'utf8',
  );
  return { raw, parsed: JSON.parse(raw) as unknown };
}

test('engine-authored finalized replay-v2 decodes without reserialization', () => {
  const { raw } = readEngineFixture('frontline-replay-v2.json');
  const decoded = decodeReplayJson(raw);
  const replay = decoded.replay;

  assert.ok(raw.includes(`"seed":"${JS_UNSAFE_SEED}"`));
  assert.equal(decoded.replayVersion, 2);
  assert.equal(replay.sourceVersion, 2);
  assert.equal(replay.seed, JS_UNSAFE_SEED);
  assert.equal(replay.seedExact, true);
  assert.equal(replay.partial, false);
  assert.match(replay.replayHash ?? '', /^[0-9a-f]{64}$/);
  assert.equal(replay.ticks.length, 12);
  assert.equal(replay.units.length, 6);

  assert.deepEqual(
    replay.ticks[0]?.before.actors.map((actor) => actor.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.equal(
    replay.ticks[2]?.before.actors.find(
      (actor) =>
        actor.identity.teamId === 0 &&
        actor.identity.unitId === 1,
    )?.actorKey,
    'frontline:0:unit:1:life:0',
  );
  assert.equal(
    replay.ticks[2]?.actorTurns.find(
      (turn) => turn.actor.teamId === 0 && turn.actor.unitId === 1,
    )?.lifeStart?.spawnReason,
    'fabrication',
  );

  const formEvents = replay.ticks[9]!.events.filter(
    (event) =>
      event.type === 'form-transition-started' ||
      event.type === 'form-changed',
  );
  assert.deepEqual(
    formEvents.map((event) => ({
      type: event.type,
      source: event.sourceActor?.actorKey,
      fromFormId: event.fromFormId,
      toFormId: event.toFormId,
      startedAtTick: event.formTransitionStartedAtTick,
      completesAtTick: event.formTransitionCompletesAtTick,
      health: event.newHealth,
    })),
    [
      {
        type: 'form-transition-started',
        source: 'frontline:0:unit:1:life:0',
        fromFormId: 'child-mobile',
        toFormId: 'turret',
        startedAtTick: 9,
        completesAtTick: 9,
        health: 3,
      },
      {
        type: 'form-transition-started',
        source: 'frontline:1:unit:1:life:0',
        fromFormId: 'child-mobile',
        toFormId: 'turret',
        startedAtTick: 9,
        completesAtTick: 9,
        health: 3,
      },
      {
        type: 'form-changed',
        source: 'frontline:0:unit:1:life:0',
        fromFormId: 'child-mobile',
        toFormId: 'turret',
        startedAtTick: 9,
        completesAtTick: 9,
        health: 5,
      },
      {
        type: 'form-changed',
        source: 'frontline:1:unit:1:life:0',
        fromFormId: 'child-mobile',
        toFormId: 'turret',
        startedAtTick: 9,
        completesAtTick: 9,
        health: 5,
      },
    ],
  );
  const anchored = replay.ticks[9]!.after.units.find(
    (unit) => unit.teamId === 0 && unit.unitId === 1,
  );
  const anchoredActor = replay.ticks[9]!.after.actors.find(
    (actor) =>
      actor.identity.teamId === 0 &&
      actor.identity.unitId === 1,
  );
  assert.equal(anchored?.defaultFormId, 'child-mobile');
  assert.equal(anchored?.formId, 'turret');
  assert.equal(anchoredActor?.formId, 'turret');
  assert.equal(anchoredActor?.health, 5);
  assert.equal(anchoredActor?.pendingFormTransition, null);

  const turretShot = replay.ticks[10]!.events.find(
    (event) =>
      event.type === 'shot' &&
      event.sourceActor?.teamId === 0 &&
      event.sourceActor.unitId === 1,
  );
  assert.equal(turretShot?.actionId, 'shoot-direction');
  assert.equal(turretShot?.actionPayload?.launchHeading, 'east');
  assert.equal(turretShot?.projectileHeading, 'east');
  assert.equal(turretShot?.fromFacing, 'east');
  assert.equal(turretShot?.toFacing, 'east');
  assert.deepEqual(
    replay.ticks[10]!.projectileTraversals.find(
      (traversal) => traversal.projectileId === turretShot?.projectileId,
    ),
    {
      projectileId: '0',
      ownerActor: {
        kind: 'frontline',
        teamId: 0,
        unitId: 1,
        lifeId: 0,
        unitKey: 'frontline:0:unit:1',
        actorKey: 'frontline:0:unit:1:life:0',
      },
      ownerActorKey: 'frontline:0:unit:1:life:0',
      launchDirection: 'east',
      from: { x: 3, y: 6 },
      path: [{ x: 4, y: 6 }],
      heading: 'east',
      shotProgram: null,
      programmedPath: null,
    },
  );
  const observedShot = replay.ticks[11]!.actorTurns[0]!.observation
    .visibleEvents.find((event) => event.type === 'shot');
  assert.equal(observedShot?.actionId, 'shoot-direction');
  assert.equal(observedShot?.projectileHeading, 'east');

  const childResult = replay.result?.teams[0]?.units.find(
    (unit) => unit.unitId === 1,
  );
  assert.equal(childResult?.defaultFormId, 'child-mobile');
  assert.equal(childResult?.formId, 'turret');
  assert.equal(childResult?.activeActorKey, 'frontline:0:unit:1:life:0');
  assert.equal(childResult?.health, 5);
  assert.equal(childResult?.pendingFormTransition, null);
});

test('engine-authored tick-zero failure decodes as a hashless empty prefix', () => {
  const { raw } = readEngineFixture(
    'frontline-replay-v2-partial-zero-tick.json',
  );
  const decoded = decodeReplayJson(raw);
  const replay = decoded.replay;

  assert.ok(raw.includes('"partial":true'));
  assert.equal(decoded.replayVersion, 2);
  assert.equal(replay.sourceVersion, 2);
  assert.equal(replay.seed, JS_UNSAFE_SEED);
  assert.equal(replay.seedExact, true);
  assert.equal(replay.partial, true);
  assert.equal(replay.replayHash, null);
  assert.equal(replay.result, null);
  assert.equal(replay.initialWorld, null);
  assert.deepEqual(replay.ticks, []);
});

test('replay-v2 rejects P6 transition, turret, and observed-event causal drift', () => {
  const wrongCompletionOrder = finalizedV2Fixture();
  const orderedEvents =
    wrongCompletionOrder.ticks[9]!.resolution.events;
  [orderedEvents[1], orderedEvents[2]] = [
    orderedEvents[2]!,
    orderedEvents[1]!,
  ];
  assert.throws(
    () => decodeReplay(wrongCompletionOrder),
    /form-change completions must be the final.*suffix/,
  );

  const brokenContinuity = finalizedV2Fixture();
  brokenContinuity.ticks[9]!.postState.teams[0]!.units[1]!
    .activeLife!.cooldown = 1;
  assert.throws(
    () => decodeReplay(brokenContinuity),
    /pending transitions must preserve same-life state/,
  );

  const wrongLaunchTile = finalizedV2Fixture();
  wrongLaunchTile.ticks[10]!.resolution.events.find(
    (event) =>
      event.type === 'shot' &&
      event.sourceActorId?.teamId === 0,
  )!.to!.x += 1;
  assert.throws(
    () => decodeReplay(wrongLaunchTile),
    /absolute-heading launch tile/,
  );

  const curvedTurretTraversal = finalizedV2Fixture();
  curvedTurretTraversal.ticks[10]!.resolution
    .projectileTraversals[0]!.path[0]!.y += 1;
  assert.throws(
    () => decodeReplay(curvedTurretTraversal),
    /one-tile straight non-programmed traversal/,
  );

  const missingTurretPersistence = finalizedV2Fixture();
  missingTurretPersistence.ticks[10]!.postState.projectiles =
    missingTurretPersistence.ticks[10]!.postState.projectiles.filter(
      (projectile) => projectile.projectileId !== '0',
    );
  assert.throws(
    () => decodeReplay(missingTurretPersistence),
    /projectile persistence and spawn contact/,
  );

  const bypassedTurretCooldown = finalizedV2Fixture();
  bypassedTurretCooldown.ticks[10]!.postState.teams[0]!.units[1]!
    .activeLife!.cooldown = 0;
  assert.throws(
    () => decodeReplay(bypassedTurretCooldown),
    /surviving turret fire must preserve its exact life/,
  );

  const inventedStableUnitDamage = finalizedV2Fixture();
  inventedStableUnitDamage.ticks[10]!.postState.teams[0]!.units[1]!
    .damageDealt = '1';
  assert.throws(
    () => decodeReplay(inventedStableUnitDamage),
    /must credit its stable unit by actual health removed/,
  );

  const falseObservedTransitionHealth = finalizedV2Fixture();
  falseObservedTransitionHealth.ticks[10]!.actors[0]!.observation
    .visibleEvents.find(
      (event) => event.type === 'form-changed',
    )!.newHealth = 4;
  assert.throws(
    () => decodeReplay(falseObservedTransitionHealth),
    /observed event state, action, heading, and form causality/,
  );

  const falseObservedShotFacing = finalizedV2Fixture();
  falseObservedShotFacing.ticks[11]!.actors[0]!.observation
    .visibleEvents.find((event) => event.type === 'shot')!
    .facing = 'north';
  assert.throws(
    () => decodeReplay(falseObservedShotFacing),
    /observed event state, action, heading, and form causality/,
  );
});

test('replay-v2 binds Damage, Destroyed, and damage ledgers to exact causality', () => {
  assert.doesNotThrow(() => decodeReplay(damageV2Fixture()));

  const impossibleHealthChain = damageV2Fixture();
  impossibleHealthChain.ticks[0]!.resolution.events[0]!.newHealth = 5;
  assert.throws(
    () => decodeReplay(impossibleHealthChain),
    /exact per-target health chain/,
  );

  const changedTraversalOwner = damageV2Fixture();
  changedTraversalOwner.ticks[0]!.resolution
    .projectileTraversals[0]!.ownerActorId = {
      teamId: 1,
      unitId: 0,
      lifeId: 0,
    };
  assert.throws(
    () => decodeReplay(changedTraversalOwner),
    /changed its exact firing-life owner/,
  );

  const undeclaredProjectileOwner = damageV2Fixture();
  const forgedOwner = {
    teamId: 99,
    unitId: 99,
    lifeId: 0,
  };
  undeclaredProjectileOwner.ticks[0]!.tickStart.state
    .projectiles[0]!.ownerActorId = structuredClone(forgedOwner);
  undeclaredProjectileOwner.ticks[0]!.postState
    .projectiles[0]!.ownerActorId = structuredClone(forgedOwner);
  undeclaredProjectileOwner.ticks[0]!.resolution
    .projectileTraversals[0]!.ownerActorId =
    structuredClone(forgedOwner);
  undeclaredProjectileOwner.ticks[0]!.resolution.events[0]!
    .sourceActorId = structuredClone(forgedOwner);
  assert.throws(
    () => decodeReplay(undeclaredProjectileOwner),
    /projectile owner must reference a stable unit in contract topology/,
  );

  const wrongProjectileSource = damageV2Fixture();
  wrongProjectileSource.ticks[0]!.resolution.events[0]!
    .sourceActorId = {
      teamId: 1,
      unitId: 0,
      lifeId: 0,
    };
  assert.throws(
    () => decodeReplay(wrongProjectileSource),
    /projectile's exact firing life/,
  );

  const forgedPostHealth = damageV2Fixture();
  forgedPostHealth.ticks[0]!.postState.teams[1]!.units[0]!
    .activeLife!.health = 5;
  assert.throws(
    () => decodeReplay(forgedPostHealth),
    /post-state health must equal its exact Damage chain/,
  );

  const inventedNoDamageHealing = finalizedV2Fixture();
  inventedNoDamageHealing.ticks[0]!.postState.teams[0]!.units[0]!
    .activeLife!.health = 2;
  assert.throws(
    () => decodeReplay(inventedNoDamageHealing),
    /post-state health must equal its exact Damage chain/,
  );

  const survivingDestroyed = damageV2Fixture();
  survivingDestroyed.ticks[0]!.resolution.events.push(
    destroyedEventForDamageFixture(survivingDestroyed),
  );
  assert.throws(
    () => decodeReplay(survivingDestroyed),
    /surviving health cannot emit Destroyed/,
  );

  const missingFatalDestruction = damageV2Fixture();
  makeDamageLethal(missingFatalDestruction);
  assert.throws(
    () => decodeReplay(missingFatalDestruction),
    /zero-health target must emit one later Destroyed event/,
  );

  const forgedReplacementLife = damageV2Fixture();
  makeDamageLethal(forgedReplacementLife);
  forgedReplacementLife.ticks[0]!.resolution.events.push(
    destroyedEventForDamageFixture(forgedReplacementLife),
  );
  assert.throws(
    () => decodeReplay(forgedReplacementLife),
    /exact Prime respawn or child rebuild reset/,
  );

  const oldFiringLife = damageV2Fixture();
  const tick = oldFiringLife.ticks[0]!;
  const oldOwner = {
    teamId: 0,
    unitId: 0,
    lifeId: 99,
  };
  tick.tickStart.state.projectiles[0]!.ownerActorId =
    structuredClone(oldOwner);
  tick.resolution.projectileTraversals[0]!.ownerActorId =
    structuredClone(oldOwner);
  tick.postState.projectiles[0]!.ownerActorId =
    structuredClone(oldOwner);
  tick.resolution.events[0]!.sourceActorId =
    structuredClone(oldOwner);
  tick.postState.teams[0]!.units[0]!.activeLife!.damageDealt =
    tick.tickStart.state.teams[0]!.units[0]!.activeLife!.damageDealt;
  assert.doesNotThrow(() => decodeReplay(oldFiringLife));
});

test('replay-v2 preserves N=3 Wait-only pending state and terminal future state', () => {
  const due = pendingP6Fixture(11);
  const decodedDue = decodeReplay(due).replay;
  const pendingActor = decodedDue.ticks[9]!.after.actors.find(
    (actor) =>
      actor.identity.teamId === 0 &&
      actor.identity.unitId === 1,
  )!;
  assert.equal(pendingActor.formId, 'child-mobile');
  assert.deepEqual(pendingActor.pendingFormTransition, {
    fromFormId: 'child-mobile',
    toFormId: 'turret',
    startedAtTick: 9,
    completesAtTick: 11,
  });
  const pendingTurn = decodedDue.ticks[10]!.actorTurns.find(
    (turn) => turn.actor.teamId === 0 && turn.actor.unitId === 1,
  )!;
  assert.deepEqual(
    pendingTurn.observation.actions
      ?.filter((action) => action.available)
      .map((action) => action.actionId),
    ['wait'],
  );
  assert.equal(
    decodedDue.ticks[10]!.after.actors.find(
      (actor) =>
        actor.identity.teamId === 0 &&
        actor.identity.unitId === 1,
    )?.formId,
    'child-mobile',
  );
  assert.equal(
    decodedDue.ticks[11]!.after.actors.find(
      (actor) =>
        actor.identity.teamId === 0 &&
        actor.identity.unitId === 1,
    )?.formId,
    'turret',
  );

  const illegalPendingMask = pendingP6Fixture(11);
  illegalPendingMask.ticks[10]!.actors.find(
    (turn) => turn.actorId.teamId === 0 && turn.actorId.unitId === 1,
  )!.observation.actions.find(
    (action) => action.actionId === 'transform',
  )!.available = true;
  assert.throws(
    () => decodeReplay(illegalPendingMask),
    /pending form transitions must expose Wait as the only available action/,
  );

  const terminal = decodeReplay(pendingP6Fixture(12)).replay;
  const terminalChild = terminal.result?.teams[0]?.units.find(
    (unit) => unit.unitId === 1,
  )!;
  assert.equal(terminalChild.formId, 'child-mobile');
  assert.deepEqual(terminalChild.pendingFormTransition, {
    fromFormId: 'child-mobile',
    toFormId: 'turret',
    startedAtTick: 9,
    completesAtTick: 12,
  });
  assert.equal(
    terminal.ticks[11]!.events.some(
      (event) =>
        event.type === 'form-changed' ||
        event.type === 'form-transition-cancelled',
    ),
    false,
  );
});

test('engine-authored replay-v2 remains valid when wire ticks arrive out of order', () => {
  const input = finalizedV2Fixture();
  input.ticks.reverse();

  const replay = decodeReplay(input).replay;

  assert.deepEqual(
    replay.ticks.map((tick) => tick.tick),
    Array.from({ length: 12 }, (_, tick) => tick),
  );
});

test('replay-v2 rejects tick gaps and nonzero starts', () => {
  const nonzero = finalizedV2Fixture();
  nonzero.ticks = nonzero.ticks.slice(1);
  assert.throws(
    () => decodeReplay(nonzero),
    /start at zero and be contiguous/,
  );

  const gap = finalizedV2Fixture();
  gap.ticks.splice(1, 1);
  assert.throws(
    () => decodeReplay(gap),
    /start at zero and be contiguous/,
  );
});

test('replay-v2 requires lifeStart exactly on each actor life first turn', () => {
  const missing = finalizedV2Fixture();
  missing.ticks[0]!.actors[0]!.lifeStart = null;
  assert.throws(
    () => decodeReplay(missing),
    /lifeStart.*first turn/,
  );

  const repeated = finalizedV2Fixture();
  const firstRespawn = repeated.ticks[2]!.actors.find(
    (turn) => turn.actorId.teamId === 0 && turn.actorId.unitId === 1,
  )!;
  const nextTurn = repeated.ticks[3]!.actors.find(
    (turn) =>
      turn.actorId.teamId === firstRespawn.actorId.teamId &&
      turn.actorId.unitId === firstRespawn.actorId.unitId &&
      turn.actorId.lifeId === firstRespawn.actorId.lifeId,
  )!;
  nextTurn.lifeStart = structuredClone(firstRespawn.lifeStart);
  assert.throws(
    () => decodeReplay(repeated),
    /lifeStart.*first turn/,
  );
});

test('replay-v2 enforces initial deployment and cross-tick lifecycle continuity', () => {
  const duplicateInitialSlot = finalizedV2Fixture();
  duplicateInitialSlot.header.contract.topology.initialLives.push({
    ...duplicateInitialSlot.header.contract.topology.initialLives[0]!,
    lifeId: 99,
  });
  duplicateInitialSlot.header.contract.topology.initialLifeCount += 1;
  assert.throws(
    () => decodeReplay(duplicateInitialSlot),
    /multiple initial lives for stable unit/,
  );

  const unannouncedWorldChange = finalizedV2Fixture();
  unannouncedWorldChange.ticks[3]!.tickStart.state.teams[0]!.damageDealt =
    '1';
  assert.throws(
    () => decodeReplay(unannouncedWorldChange),
    /prior tick's post-state/,
  );

  const invalidRespawnLife = finalizedV2Fixture();
  invalidRespawnLife.ticks[2]!.tickStart.state.teams[0]!.units[1]!
    .activeLife!.cooldown = 1;
  assert.throws(
    () => decodeReplay(invalidRespawnLife),
    /lifecycle transition for unit 0:1 does not match its event/,
  );
});

test('replay-v2 rejects empty payload envelopes at every payload boundary', () => {
  const emptyPayload = () => ({
    shotProgram: null,
    direction: null,
    launchHeading: null,
    unitTarget: null,
    formTargetId: null,
  });
  const mutations: ((
    input: ReplayV2CompleteDocument,
  ) => void)[] = [
    (input) => {
      input.ticks[0]!.actors[0]!.runtimeReply.payload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.actors[0]!.acceptedDecision.payload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.actors[0]!.actionResolution.chosenPayload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.actors[0]!.actionResolution.validatedPayload =
        emptyPayload();
    },
    (input) => {
      input.ticks[2]!.resolution.events[0]!.actionPayload =
        emptyPayload();
    },
  ];

  for (const mutate of mutations) {
    const input = finalizedV2Fixture();
    mutate(input);
    assert.throws(
      () => decodeReplay(input),
      /empty action payload must canonicalize to null/,
    );
  }
});

test('replay-v2 requires accepted selectors and payloads to equal chosen resolution', () => {
  const selectorMismatch = finalizedV2Fixture();
  const selectorTurn = selectorMismatch.ticks[0]!.actors[0]!;
  selectorTurn.acceptedDecision = {
    ...selectorTurn.acceptedDecision,
    actionId: 'move-forward',
    actionCode: 1,
    payload: null,
  };
  assert.throws(
    () => decodeReplay(selectorMismatch),
    /selector and payload must equal the chosen action resolution/,
  );

  const payloadMismatch = finalizedV2Fixture();
  const payload =
    payloadMismatch.ticks[1]!.actors[0]!.acceptedDecision.payload!;
  payload.unitTarget = { teamId: 0, unitId: 2 };
  assert.throws(
    () => decodeReplay(payloadMismatch),
    /selector and payload must equal the chosen action resolution/,
  );
});

test('replay-v2 rejects terminal and objective team drift', () => {
  const missingTeam = finalizedV2Fixture();
  missingTeam.result.teams.pop();
  assert.throws(
    () => decodeReplay(missingTeam),
    /cover exactly the topology team IDs/,
  );

  const unknownWinner = finalizedV2Fixture();
  unknownWinner.result.winnerTeamId = 99;
  assert.throws(
    () => decodeReplay(unknownWinner),
    /winnerTeamId.*topology team/,
  );

  const unknownObjectiveTeam = finalizedV2Fixture();
  unknownObjectiveTeam.ticks[0]!.tickStart.state.objective.claimingTeamId =
    99;
  assert.throws(
    () => decodeReplay(unknownObjectiveTeam),
    /claimingTeamId.*topology team/,
  );

  const objectiveDrift = finalizedV2Fixture();
  objectiveDrift.result.objective.captureProgress += 1;
  assert.throws(
    () => decodeReplay(objectiveDrift),
    /final post-state objective/,
  );

  const missingUnit = finalizedV2Fixture();
  missingUnit.result.teams[0]!.units = [];
  assert.throws(
    () => decodeReplay(missingUnit),
    /cover exactly the topology units/,
  );

  const staleUnit = finalizedV2Fixture();
  staleUnit.result.teams[0]!.units[0]!.health -= 1;
  assert.throws(
    () => decodeReplay(staleUnit),
    /differs from the final world/,
  );
});

test('replay-v2 rejects dangling alias sidecars', () => {
  const input = finalizedV2Fixture();
  input.ticks[0]!.actors[0]!.aliases.events.push({
    eventHandle: 'event-99',
    eventId: 'resolution:999:0',
  });

  assert.throws(
    () => decodeReplay(input),
    /aliases\.events.*exactly match event handles/,
  );
});

function finalizedV2Fixture(): ReplayV2CompleteDocument {
  const { parsed } = readEngineFixture('frontline-replay-v2.json');
  const document = parsed as ReplayV2Document;
  assert.equal(document.partial, false);
  return document as ReplayV2CompleteDocument;
}

function damageV2Fixture(): ReplayV2CompleteDocument {
  const input = structuredClone(
    replayV2FixtureInput(),
  ) as unknown as ReplayV2CompleteDocument;
  const tick = input.ticks[0]!;
  const projectile = tick.tickStart.state.projectiles[0]!;
  const source = projectile.ownerActorId;
  const targetUnit = tick.tickStart.state.teams
    .find((team) => team.teamId !== source.teamId)!
    .units[0]!;
  const target = targetUnit.activeLife!;
  tick.resolution.events = [
    {
      eventId: 'resolution:0:damage',
      tick: 0,
      type: 'damage',
      teamId: target.actorId.teamId,
      unitId: target.actorId.unitId,
      sourceActorId: structuredClone(source),
      targetActorId: structuredClone(target.actorId),
      projectileId: projectile.projectileId,
      from: structuredClone(target.position),
      to: structuredClone(target.position),
      fromFacing: null,
      toFacing: null,
      projectileHeading: null,
      actionId: null,
      actionCode: null,
      actionPayload: null,
      actionResult: null,
      fromFormId: null,
      toFormId: null,
      formTransitionStartedAtTick: null,
      formTransitionCompletesAtTick: null,
      amount: 1,
      newHealth: target.health - 1,
      lifecycleStatus: null,
      spawnReason: null,
      respawnAtTick: null,
      unlockAtTick: null,
      rebuildReadyAtTick: null,
      fabricationAtTick: null,
      fromPositionIndex: null,
      toPositionIndex: null,
      claimingTeamId: null,
      captureProgress: null,
      controlResumesAtTick: null,
    },
  ];

  const creditedDamage = (
    BigInt(
      tick.tickStart.state.teams[source.teamId]!.damageDealt,
    ) + 1n
  ).toString();
  const sourcePostUnit =
    tick.postState.teams[source.teamId]!.units[source.unitId]!;
  tick.postState.teams[source.teamId]!.damageDealt =
    creditedDamage;
  sourcePostUnit.damageDealt = creditedDamage;
  sourcePostUnit.activeLife!.damageDealt = creditedDamage;
  const targetPostUnit =
    tick.postState.teams[target.actorId.teamId]!
      .units[target.actorId.unitId]!;
  targetPostUnit.activeLife!.health = target.health - 1;

  const sourceResult = input.result.teams.find(
    (team) => team.teamId === source.teamId,
  )!;
  sourceResult.damageDealt = creditedDamage;
  sourceResult.units.find(
    (unit) => unit.unitId === source.unitId,
  )!.damageDealt = creditedDamage;
  const targetResult = input.result.teams.find(
    (team) => team.teamId === target.actorId.teamId,
  )!;
  targetResult.activeHealth -= 1;
  targetResult.units.find(
    (unit) => unit.unitId === target.actorId.unitId,
  )!.health = target.health - 1;
  return input;
}

function makeDamageLethal(
  input: ReplayV2CompleteDocument,
): void {
  const target = input.ticks[0]!.tickStart.state.teams[1]!
    .units[0]!.activeLife!;
  input.header.contract.rules.projectiles.damagePerHit =
    target.health;
  input.ticks[0]!.resolution.events[0]!.amount =
    target.health;
  input.ticks[0]!.resolution.events[0]!.newHealth = 0;
}

function destroyedEventForDamageFixture(
  input: ReplayV2CompleteDocument,
): ReplayV2CompleteDocument['ticks'][number]['resolution']['events'][number] {
  const tick = input.ticks[0]!;
  const damage = tick.resolution.events[0]!;
  const target = damage.targetActorId!;
  const dueTick =
    tick.tick +
    1 +
    input.header.contract.rules.frontlineDefinition!.lifecycle
      .primeRespawnTicks;
  return {
    ...structuredClone(damage),
    eventId: 'resolution:0:destroyed',
    type: 'destroyed',
    amount: null,
    newHealth: 0,
    lifecycleStatus: 'respawning',
    respawnAtTick: dueTick,
  };
}

function pendingP6Fixture(
  completesAtTick: 11 | 12,
): ReplayV2CompleteDocument {
  const input = finalizedV2Fixture();
  const anchor =
    input.header.contract.rules.frontlineDefinition!.anchor;
  anchor.windupTicks = completesAtTick - 9 + 1;
  const transition = {
    fromFormId: anchor.sourceFormId,
    toFormId: anchor.targetFormId,
    startedAtTick: 9,
    completesAtTick,
  };
  const tick9 = input.ticks[9]!;
  const originalChanges = tick9.resolution.events.filter(
    (event) => event.type === 'form-changed',
  );
  tick9.resolution.events = tick9.resolution.events
    .filter((event) => event.type === 'form-transition-started')
    .map((event) => ({
      ...event,
      formTransitionCompletesAtTick: completesAtTick,
    }));
  setPendingChildren(tick9.postState, transition);

  const tick10 = input.ticks[10]!;
  tick10.tickStart.state = structuredClone(tick9.postState);
  tick10.resolution.events = [];
  tick10.resolution.projectileTraversals = [];
  tick10.postState.projectiles = [];
  setPendingChildren(tick10.postState, transition);
  setPendingChildTurnsToWait(tick10, transition);
  syncPendingObservations(tick10, transition);
  for (const turn of tick10.actors) {
    turn.observation.visibleEvents =
      turn.observation.visibleEvents
        .filter(
          (event) => event.type === 'form-transition-started',
        )
        .map((event) => ({
          ...event,
          formTransitionCompletesAtTick: completesAtTick,
        }));
    const visibleHandles = new Set(
      turn.observation.visibleEvents.map(
        (event) => event.eventHandle,
      ),
    );
    turn.aliases.events = turn.aliases.events.filter((alias) =>
      visibleHandles.has(alias.eventHandle),
    );
  }

  const tick11 = input.ticks[11]!;
  tick11.tickStart.state = structuredClone(tick10.postState);
  tick11.resolution.projectileTraversals = [];
  if (completesAtTick === 11) {
    tick11.resolution.events = originalChanges.map(
      (event, index) => ({
        ...event,
        eventId: `resolution:11:${index}`,
        tick: 11,
        formTransitionStartedAtTick: 9,
        formTransitionCompletesAtTick: 11,
      }),
    );
    tick11.postState.projectiles = [];
  } else {
    tick11.resolution.events = [];
    tick11.postState = structuredClone(tick11.tickStart.state);
    tick11.postState.objective.nextTick = 12;
    setPendingChildren(tick11.postState, transition);
  }
  setPendingChildTurnsToWait(tick11, transition);
  syncPendingObservations(tick11, transition);
  for (const turn of tick11.actors) {
    turn.observation.visibleEvents = [];
    if (turn.observation.visibleProjectiles !== null) {
      turn.observation.visibleProjectiles = [];
    }
    if (turn.observation.heardSounds !== null) {
      turn.observation.heardSounds = [];
    }
    turn.aliases.enemyLives = [];
    turn.aliases.projectiles = [];
    turn.aliases.events = [];
  }

  if (completesAtTick === 12) {
    for (const team of input.result.teams) {
      team.activeHealth = 6;
      const child = team.units.find((unit) => unit.unitId === 1)!;
      child.formId = 'child-mobile';
      child.pendingFormTransition = structuredClone(transition);
      child.health = 3;
    }
  }
  return input;
}

function setPendingChildren(
  world: ReplayV2CompleteDocument['ticks'][number]['postState'],
  transition: {
    fromFormId: string;
    toFormId: string;
    startedAtTick: number;
    completesAtTick: number;
  },
): void {
  for (const team of world.teams) {
    const life = team.units.find(
      (unit) => unit.unitId === 1,
    )!.activeLife!;
    life.formId = transition.fromFormId;
    life.pendingFormTransition = structuredClone(transition);
    life.health = 3;
    life.cooldown = 0;
    life.previousActionResult = 'success';
  }
}

function setPendingChildTurnsToWait(
  tick: ReplayV2CompleteDocument['ticks'][number],
  transition: {
    fromFormId: string;
    toFormId: string;
    startedAtTick: number;
    completesAtTick: number;
  },
): void {
  for (const turn of tick.actors.filter(
    (candidate) => candidate.actorId.unitId === 1,
  )) {
    turn.runtimeReply = {
      actionId: 'wait',
      actionCode: 0,
      payload: null,
      debugMessage: null,
      faulted: false,
      faultMessage: null,
    };
    turn.acceptedDecision = structuredClone(turn.runtimeReply);
    turn.actionResolution = {
      actorId: structuredClone(turn.actorId),
      chosenActionId: 'wait',
      chosenActionCode: 0,
      chosenPayload: null,
      validatedActionId: 'wait',
      validatedActionCode: 0,
      validatedPayload: null,
      result: 'success',
    };
    turn.observation.self.formId = transition.fromFormId;
    turn.observation.self.pendingFormTransition =
      structuredClone(transition);
    for (const action of turn.observation.actions) {
      action.available = action.actionId === 'wait';
      if (action.allowedFormTargets !== null) {
        action.allowedFormTargets = [];
      }
      if (action.allowedProjectileHeadings !== null) {
        action.allowedProjectileHeadings = [];
      }
    }
  }
}

function syncPendingObservations(
  tick: ReplayV2CompleteDocument['ticks'][number],
  transition: {
    fromFormId: string;
    toFormId: string;
    startedAtTick: number;
    completesAtTick: number;
  },
): void {
  for (const turn of tick.actors) {
    const team = tick.tickStart.state.teams.find(
      (candidate) => candidate.teamId === turn.actorId.teamId,
    )!;
    const self = team.units.find(
      (unit) => unit.unitId === turn.actorId.unitId,
    )!.activeLife!;
    Object.assign(turn.observation.self, {
      formId: self.formId,
      pendingFormTransition: structuredClone(
        self.pendingFormTransition,
      ),
      position: structuredClone(self.position),
      facing: self.facing,
      health: self.health,
      cooldown: self.cooldown,
      energy: self.energy,
      previousActionResult: self.previousActionResult,
    });
    for (const observedUnit of turn.observation.teamUnits) {
      const authoritative = team.units.find(
        (unit) => unit.unitId === observedUnit.unitId,
      )!;
      observedUnit.formId =
        authoritative.activeLife?.formId ??
        authoritative.defaultFormId;
      observedUnit.lifecycleStatus =
        authoritative.lifecycleStatus;
      observedUnit.activeActorId = structuredClone(
        authoritative.activeLife?.actorId ?? null,
      );
    }
    for (const ally of turn.observation.allies) {
      const life = team.units.find(
        (unit) =>
          unit.activeLife?.actorId.unitId === ally.actorId.unitId &&
          unit.activeLife.actorId.lifeId === ally.actorId.lifeId,
      )!.activeLife!;
      ally.formId = life.formId;
      ally.pendingFormTransition = structuredClone(
        life.pendingFormTransition,
      );
      ally.position = structuredClone(life.position);
      ally.facing = life.facing;
      ally.health = life.health;
      ally.cooldown = life.cooldown;
      ally.energy = life.energy;
      ally.previousActionResult = life.previousActionResult;
    }
  }
  void transition;
}
