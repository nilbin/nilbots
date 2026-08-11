import assert from 'node:assert/strict';
import test from 'node:test';
import { replayAudioEventsAt } from '../src/audio/replayAudioEvents.ts';
import { loadReplayObject } from '../src/replayIngress.ts';
import type {
  ReplayArcRelayModeState,
  ReplayCausalEvent,
  ReplayModel,
} from '../src/replayModel.ts';
import type { ReplayV1GameEvent } from '../src/replayWireV1.ts';
import { replayV1FixtureInput } from './support/replayFixtureInputs.ts';

test('a projectile launch remains distinct from its concrete outcomes', () => {
  const replay = replayWithEvents([
    { type: 'Shot', slot: 3 },
    { type: 'Damage', targetSlot: 9, amount: 1, newHealth: 1 },
    { type: 'Destroyed', slot: 9 },
  ]);
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['projectile', 'impact', 'destroyed'],
  );
});

test('a simultaneous volley becomes one calm cue per category', () => {
  const replay = replayWithEvents([
    { type: 'Damage', targetSlot: 9, amount: 1, newHealth: 1 },
  ]);
  const tick = replay.ticks[0]!;
  const damage = tick.events[0]!;
  tick.events = [
    {
      ...structuredClone(damage),
      from: { x: 0, y: 1 },
      to: { x: 0, y: 1 },
    },
    {
      ...structuredClone(damage),
      eventId: 'second-damage',
      from: { x: replay.map.width - 1, y: 1 },
      to: { x: replay.map.width - 1, y: 1 },
    },
  ];

  const events = replayAudioEventsAt(replay, 0);
  assert.deepEqual(events.map((event) => event.cue), ['impact']);
  assert.equal(
    events[0]!.pan,
    0,
    'the volley shares its left/right centre of energy',
  );
});

test('the 3D instant-ray traversal gets one launch cue without retriggering discrete travel', () => {
  function withTraversal(mode: 'instant-ray' | 'discrete'): ReplayModel {
    const replay = replayWithEvents([]);
    const tick = replay.ticks[0]!;
    const owner = tick.before.actors[0]!.identity;
    tick.projectileTraversals = [
      {
        projectileId: 'presentation-test',
        ownerActor: owner,
        ownerActorKey: owner.actorKey,
        launchDirection: 'east',
        from: { x: 0, y: 1 },
        path: [{ x: 1, y: 1 }],
        heading: 'east',
        shotProgram: null,
        programmedPath: null,
        attackProfileId: 'presentation-test',
      },
    ];
    replay.contract = {
      kind: 'v3-generic',
      rawContract: {
        rules: {
          attackProfiles: [
            {
              id: 'presentation-test',
              projectile: {
                mode,
                strikeWindupTicks: mode === 'instant-ray' ? 1 : undefined,
              },
            },
          ],
        },
      },
    } as unknown as ReplayModel['contract'];
    return replay;
  }

  assert.deepEqual(
    replayAudioEventsAt(withTraversal('instant-ray'), 0).map(
      (event) => event.cue,
    ),
    ['projectile'],
    'the authoritative traversal speaks even without a separate attack event',
  );
  assert.deepEqual(
    replayAudioEventsAt(withTraversal('discrete'), 0),
    [],
    'an advancing persistent projectile does not sound like a new launch',
  );
});

test('match completion does not reuse the entitlement-unlock review cue', () => {
  const wire = replayV1FixtureInput();
  wire.ticks[0]!.events = [{ type: 'Destroyed', slot: 9 }];
  wire.result.winnerSlot = 3;
  wire.result.reason = 'Elimination';
  wire.result.endTick = 0;
  const replay = loadReplayObject(wire).replay;
  const events = replayAudioEventsAt(replay, 0);

  assert.deepEqual(
    events.map((event) => event.cue),
    ['destroyed'],
  );
  assert.equal(
    events[0]!.tickOffset,
    0.55,
    'destruction sounds on the exact boundary where both renderers begin collapse',
  );
});

test('draws and non-events do not invent audio from replay state', () => {
  const wire = replayV1FixtureInput();
  wire.ticks[0]!.events = [];
  delete wire.result.winnerSlot;
  wire.result.reason = 'MaxTicks';
  wire.result.endTick = 0;
  const replay = loadReplayObject(wire).replay;

  assert.deepEqual(replayAudioEventsAt(replay, 0), []);
  assert.deepEqual(replayAudioEventsAt(replay, 99), []);
});

test('declared attacks speak wind-up and projectile while unresolved outcomes stay silent', () => {
  const declaring = replayWithEvents([]);
  const declarationTick = declaring.ticks[0]!;
  const shooter = declarationTick.before.actors[0]!.identity;
  const strike = {
    shooter,
    resolveAtTick: declarationTick.tick + 1,
    origin: { x: 2, y: 1 },
    centralHeading: 'east' as const,
    target: null,
    tiles: [{ x: 3, y: 1 }],
  };
  declarationTick.before.mode = arcMode([]);
  declarationTick.after.mode = arcMode([strike]);
  assert.deepEqual(
    replayAudioEventsAt(declaring, 0).map((event) => event.cue),
    ['attack-windup'],
  );

  const launching = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  const launchTick = launching.ticks[0]!;
  const launchShooter = launchTick.events[0]!.sourceActor!;
  const launchStrike = {
    ...strike,
    shooter: launchShooter,
    resolveAtTick: launchTick.tick,
  };
  launchTick.before.mode = arcMode([launchStrike]);
  launchTick.after.mode = arcMode([]);
  assert.deepEqual(
    replayAudioEventsAt(launching, 0).map((event) => event.cue),
    ['projectile'],
    'a resolved declared attack emits one projectile, not a fizzle plus a launch',
  );

  const fizzled = replayWithEvents([]);
  const fizzleTick = fizzled.ticks[0]!;
  fizzleTick.before.mode = arcMode([
    { ...strike, resolveAtTick: fizzleTick.tick },
  ]);
  fizzleTick.after.mode = arcMode([]);
  assert.deepEqual(
    replayAudioEventsAt(fizzled, 0).map((event) => event.cue),
    [],
    'a fizzle or miss remains visual-only for now',
  );

  const cancelled = replayWithEvents([]);
  const cancelTick = cancelled.ticks[0]!;
  cancelTick.before.mode = arcMode([
    { ...strike, resolveAtTick: cancelTick.tick + 2 },
  ]);
  cancelTick.after.mode = arcMode([]);
  assert.deepEqual(
    replayAudioEventsAt(cancelled, 0).map((event) => event.cue),
    [],
    'a voluntary cancellation remains visual-only for now',
  );
});

test('foreground events mask concurrent wind-ups', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  const tick = replay.ticks[0]!;
  const firstShooter = tick.before.actors[0]!.identity;
  const secondShooter = tick.before.actors[1]!.identity;
  const makeStrike = (
    shooter: typeof firstShooter,
    originX: number,
  ): ReplayArcRelayModeState['pendingStrikes'][number] => ({
    shooter,
    resolveAtTick: tick.tick + 1,
    origin: { x: originX, y: 1 },
    centralHeading: 'east',
    target: null,
    tiles: [{ x: Math.min(replay.map.width - 1, originX + 1), y: 1 }],
  });
  tick.before.mode = arcMode([]);
  tick.after.mode = arcMode([
    makeStrike(firstShooter, 0),
    makeStrike(secondShooter, replay.map.width - 1),
  ]);

  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['projectile'],
    'the resolved projectile represents the tick instead of layering two new wind-ups over it',
  );

  tick.events = [];
  const [windup] = replayAudioEventsAt(replay, 0);
  assert.equal(windup?.cue, 'attack-windup');
  assert.equal(
    windup?.pan,
    0,
    'two simultaneous declarations share one centred cue',
  );
});

test('arrival, deflection, and level-up use their own authoritative cues', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const actor = template.sourceActor!;
  const arrival = {
    ...structuredClone(template),
    eventId: 'arrival',
    type: 'life-spawned',
  } as ReplayCausalEvent;
  const deflection = {
    ...structuredClone(template),
    eventId: 'deflection',
    type: 'projectile-deflected',
    from: null,
    to: { x: replay.map.width - 1, y: 0 },
  } as ReplayCausalEvent;
  const levelTwo = {
    ...structuredClone(template),
    eventId: 'level-two',
    type: 'game-mode',
    arcRelayFact: {
      kind: 'leveled-up',
      actor,
      level: 2,
      position: { x: 1, y: 1 },
    },
  } as ReplayCausalEvent;
  const levelThree = {
    ...structuredClone(levelTwo),
    eventId: 'level-three',
    arcRelayFact: {
      kind: 'leveled-up',
      actor,
      level: 3,
      position: { x: 2, y: 1 },
    },
  } as ReplayCausalEvent;
  tick.lifecycleEvents = [arrival];
  tick.events = [deflection, levelTwo, levelThree];

  const events = replayAudioEventsAt(replay, 0);
  assert.deepEqual(
    events.map((event) => event.cue),
    ['deflect', 'arrival', 'level-up'],
  );
  assert.equal(
    events.filter((event) => event.cue === 'level-up').length,
    1,
    'several level facts for one actor coalesce into one celebratory cue',
  );
  assert.equal(events[0]!.pan, 1, 'the guard owns the deflection sound');
});

function replayWithEvents(events: ReplayV1GameEvent[]) {
  const wire = replayV1FixtureInput();
  wire.ticks[0]!.events = events;
  return loadReplayObject(wire).replay;
}

function arcMode(
  pendingStrikes: ReplayArcRelayModeState['pendingStrikes'],
): ReplayArcRelayModeState {
  return {
    kind: 'arc-relay',
    modeId: 'arc-relay',
    wells: [],
    reactors: [],
    visibleCores: [],
    visibleSignatures: [],
    latestPulseTeamId: null,
    latestPulseTick: null,
    pendingStrikes,
  };
}
