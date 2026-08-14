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

test('distinct simultaneous projectile releases stay audible and receive tiny start offsets', () => {
  const replay = replayWithEvents([
    { type: 'Shot', slot: 3 },
    { type: 'Shot', slot: 9 },
  ]);
  const releases = replayAudioEventsAt(replay, 0);

  assert.deepEqual(
    releases.map((event) => event.cue),
    ['projectile', 'projectile'],
  );
  assert.deepEqual(
    releases.map((event) => event.tickOffset),
    [0.08, 0.12],
    'a volley keeps both voices without phase-locking identical buffers',
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

test('declared attacks share one release cue for projectile and fizzle while cancellation stays silent', () => {
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
    ['projectile'],
    'a matured fizzle or miss uses the same neutral release as a projectile',
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

test('healing tiles stay shared while V2 medic repairs own a signature cue', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const owner = tick.before.actors[0]!.identity;
  const target = tick.before.actors[1]!.identity;
  const zoneHeal = {
    ...structuredClone(template),
    eventId: 'zone-heal',
    type: 'game-mode',
    arcRelayFact: {
      kind: 'zone-healed',
      actor: target,
      amount: 1,
      newHealth: 2,
      position: { x: 0, y: 1 },
    },
  } as ReplayCausalEvent;
  const medicRepair = {
    ...structuredClone(template),
    eventId: 'medic-repair',
    type: 'game-mode',
    arcRelayFact: {
      kind: 'signature-repair',
      operationId: 'repair-operation',
      signatureId: 'repair-beam',
      ownerActor: owner,
      targetActor: target,
      amount: 1,
      newHealth: 3,
      position: { x: replay.map.width - 1, y: 1 },
    },
  } as ReplayCausalEvent;

  tick.events = [zoneHeal];
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => [event.cue, event.pan]),
    [['healing', -1]],
    'a healing station sounds at the healed body',
  );

  tick.events = [medicRepair];
  replay.signaturePresentationId = 'arc-relay-signatures-v2';
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => [event.cue, event.pan]),
    [['signature-repair', 1]],
    'a V2 medic repair owns its skill identity at the repaired body',
  );

  tick.events = [zoneHeal, medicRepair];
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => [event.cue, event.pan]),
    [
      ['healing', -1],
      ['signature-repair', 1],
    ],
    'a station and medic remain distinct while retaining their event positions',
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

test('grammar-3 signatures schedule their co-located presentation bindings', () => {
  const expected = [
    ['vector-dash', 'completed', 'signature-motion'],
    ['prism-wall', 'started', 'signature-construct'],
    ['tractor-hook', 'launched', 'signature-tether'],
    ['survey-flare', 'landed', 'signature-survey'],
    ['null-field', 'activated', 'signature-null'],
    ['exchange', 'completed', 'signature-exchange'],
    ['hardlight-block', 'started', 'signature-construct'],
    ['target-paint', 'acquired', 'signature-target'],
    ['kinetic-burst', 'completed', 'signature-kinetic'],
    ['smoke-canister', 'landed', 'signature-smoke'],
  ] as const;

  for (const [signatureId, reason, cue] of expected) {
    const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
    replay.signaturePresentationId = 'arc-relay-signatures-v1';
    const tick = replay.ticks[0]!;
    const template = tick.events[0]!;
    const owner = tick.before.actors[0]!.identity;
    tick.events = [
      {
        ...structuredClone(template),
        eventId: `signature:${signatureId}:${reason}`,
        type: 'game-mode',
        sourceActor: owner,
        projectileId: null,
        arcRelayFact: {
          kind: 'signature-changed',
          operationId: `operation:${signatureId}`,
          signatureId,
          ownerActor: owner,
          phase: reason === 'started' ? 'active' : null,
          reason,
        },
      } as ReplayCausalEvent,
    ];

    assert.deepEqual(
      replayAudioEventsAt(replay, 0).map((event) => event.cue),
      [cue],
      `${signatureId} ${reason}`,
    );
  }
});

test('V2 gives formerly shared signatures distinct authoritative cues', () => {
  const expected = [
    ['hardlight-block', 'started', 'signature-mason'],
    ['falling-star', 'completed', 'signature-falling-star'],
    ['trip-node', 'started', 'signature-trip-deploy'],
    ['trip-node', 'triggered', 'signature-trip-trigger'],
    ['arc-toss', 'launched', 'signature-arc-toss'],
    ['rail-line', 'started', 'signature-rail-charge'],
    ['rail-line', 'completed', 'signature-rail-release'],
    ['sentinel-seed', 'deployed', 'signature-sentinel-deploy'],
  ] as const;

  for (const [signatureId, reason, cue] of expected) {
    const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
    replay.signaturePresentationId = 'arc-relay-signatures-v2';
    const tick = replay.ticks[0]!;
    const template = tick.events[0]!;
    const owner = tick.before.actors[0]!.identity;
    tick.events = [
      {
        ...structuredClone(template),
        eventId: `v2:${signatureId}:${reason}`,
        type: 'game-mode',
        sourceActor: owner,
        projectileId: null,
        arcRelayFact: {
          kind: 'signature-changed',
          operationId: `operation:${signatureId}`,
          signatureId,
          ownerActor: owner,
          phase: reason === 'started' ? 'tell' : null,
          reason,
        },
      } as ReplayCausalEvent,
    ];

    assert.deepEqual(
      replayAudioEventsAt(replay, 0).map((event) => event.cue),
      [cue],
      `${signatureId} ${reason}`,
    );
  }
});

test('V2 Rail Line replaces generic attack layers and keeps concrete armor contact', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  replay.signaturePresentationId = 'arc-relay-signatures-v2';
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const owner = tick.before.actors[0]!.identity;
  const target = tick.before.actors[1]!.identity;
  const strike = {
    shooter: owner,
    resolveAtTick: tick.tick,
    origin: { x: 2, y: 1 },
    centralHeading: 'east' as const,
    target,
    tiles: [{ x: 3, y: 1 }],
  };
  const signatureEvent = (
    reason: 'started' | 'completed',
  ): ReplayCausalEvent =>
    ({
      ...structuredClone(template),
      eventId: `rail-line:${reason}`,
      type: 'game-mode',
      sourceActor: owner,
      projectileId: null,
      arcRelayFact: {
        kind: 'signature-changed',
        operationId: 'rail-line-operation',
        signatureId: 'rail-line',
        ownerActor: owner,
        phase: reason === 'started' ? 'tell' : null,
        reason,
      },
    }) as ReplayCausalEvent;

  tick.events = [signatureEvent('started')];
  tick.before.mode = arcMode([]);
  tick.after.mode = arcMode([{ ...strike, resolveAtTick: tick.tick + 1 }]);
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['signature-rail-charge'],
    'the rail charge replaces the shared attack wind-up',
  );

  tick.events = [
    signatureEvent('completed'),
    {
      ...structuredClone(template),
      eventId: 'rail-line:damage',
      type: 'game-mode',
      sourceActor: owner,
      targetActor: target,
      projectileId: null,
      arcRelayFact: {
        kind: 'signature-damage',
        operationId: 'rail-line-operation',
        signatureId: 'rail-line',
        ownerActor: owner,
        targetActor: target,
        amount: 1,
        newHealth: 1,
        position: { x: 3, y: 1 },
      },
    } as ReplayCausalEvent,
  ];
  tick.before.mode = arcMode([strike]);
  tick.after.mode = arcMode([]);
  tick.projectileTraversals = [
    {
      projectileId: 'rail-line-operation',
      ownerActor: owner,
      ownerActorKey: owner.actorKey,
      launchDirection: 'east',
      from: { x: 2, y: 1 },
      path: [{ x: 3, y: 1 }],
      heading: 'east',
      shotProgram: null,
      programmedPath: null,
      trigger: 'attack-launch',
      attackProfileId: 'rail-line',
    },
  ];
  replay.contract = {
    kind: 'v3-generic',
    rawContract: {
      rules: {
        attackProfiles: [
          {
            id: 'rail-line',
            projectile: { mode: 'instant-ray', strikeWindupTicks: 1 },
          },
        ],
      },
    },
  } as unknown as ReplayModel['contract'];
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['impact', 'signature-rail-release'],
    'the rail release replaces the generic projectile while a real hit keeps armor impact',
  );
});

test('V2 signature crowd priority keeps the two most important skill moments', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  replay.signaturePresentationId = 'arc-relay-signatures-v2';
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const actors = tick.before.actors.map((actor) => actor.identity);
  const event = (
    owner: (typeof actors)[number],
    signatureId: string,
    reason: string,
  ): ReplayCausalEvent =>
    ({
      ...structuredClone(template),
      eventId: `${signatureId}:${reason}`,
      type: 'game-mode',
      sourceActor: owner,
      projectileId: null,
      arcRelayFact: {
        kind: 'signature-changed',
        operationId: `${signatureId}:operation`,
        signatureId,
        ownerActor: owner,
        phase: null,
        reason,
      },
    }) as ReplayCausalEvent;
  const thirdActor = {
    ...actors[0]!,
    actorKey: `${actors[0]!.actorKey}:third-signature`,
    unitKey: `${actors[0]!.unitKey}:third-signature`,
    unitId: actors[0]!.unitId + 100,
  };
  tick.events = [
    event(actors[0]!, 'falling-star', 'completed'),
    event(actors[1]!, 'arc-toss', 'launched'),
    event(thirdActor, 'trip-node', 'started'),
  ];

  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((audioEvent) => audioEvent.cue),
    ['signature-falling-star', 'signature-arc-toss'],
    'priority 7 and 6 survive the two-voice budget ahead of priority 4 deployment',
  );
});

test('V2 Arc Toss replaces departure drop audio and Sentinel bolts sound once at launch', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  replay.signaturePresentationId = 'arc-relay-signatures-v2';
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const owner = tick.before.actors[0]!.identity;
  tick.events = [
    {
      ...structuredClone(template),
      eventId: 'arc-toss-launch',
      type: 'game-mode',
      sourceActor: owner,
      projectileId: null,
      arcRelayFact: {
        kind: 'signature-changed',
        operationId: 'arc-toss-operation',
        signatureId: 'arc-toss',
        ownerActor: owner,
        phase: 'in-flight',
        reason: 'launched',
      },
    } as ReplayCausalEvent,
    {
      ...structuredClone(template),
      eventId: 'arc-toss-departure',
      type: 'game-mode',
      sourceActor: owner,
      projectileId: null,
      arcRelayFact: {
        kind: 'core-dropped',
        coreId: { sourceWellId: 'centre', sourceOrdinal: 0 },
        sourceActor: owner,
        position: { x: 2, y: 1 },
        nextRelocationTick: 2,
        dropKind: 'signature-departure',
      },
    } as ReplayCausalEvent,
  ];
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['signature-arc-toss'],
  );

  tick.events = [];
  tick.projectileTraversals = [
    {
      projectileId: 'sentinel-bolt',
      ownerActor: owner,
      ownerActorKey: owner.actorKey,
      launchDirection: 'east',
      from: { x: 2, y: 1 },
      path: [{ x: 3, y: 1 }],
      heading: 'east',
      shotProgram: null,
      programmedPath: null,
      trigger: 'attack-launch',
      attackProfileId: 'sentinel-bolt',
    },
    {
      projectileId: 'sentinel-bolt',
      ownerActor: owner,
      ownerActorKey: owner.actorKey,
      launchDirection: 'east',
      from: { x: 3, y: 1 },
      path: [{ x: 4, y: 1 }],
      heading: 'east',
      shotProgram: null,
      programmedPath: null,
      trigger: 'projectile-advance',
      attackProfileId: 'sentinel-bolt',
    },
  ];
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['signature-sentinel-fire'],
  );
});

test('signature audio remains silent for legacy packages, cancellation, and maintenance', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const owner = tick.before.actors[0]!.identity;
  const signatureEvent = (reason: string): ReplayCausalEvent =>
    ({
      ...structuredClone(template),
      eventId: `signature:null-field:${reason}`,
      type: 'game-mode',
      sourceActor: owner,
      projectileId: null,
      arcRelayFact: {
        kind: 'signature-changed',
        operationId: 'operation:null-field',
        signatureId: 'null-field',
        ownerActor: owner,
        phase: reason === 'activated' ? 'active' : null,
        reason,
      },
    }) as ReplayCausalEvent;

  tick.events = [signatureEvent('activated')];
  replay.signaturePresentationId = null;
  assert.deepEqual(replayAudioEventsAt(replay, 0), []);

  replay.signaturePresentationId = 'future-signatures-v9';
  assert.deepEqual(replayAudioEventsAt(replay, 0), []);

  replay.signaturePresentationId = 'arc-relay-signatures-v1';
  tick.events = [signatureEvent('target-out-of-range')];
  assert.deepEqual(replayAudioEventsAt(replay, 0), []);
  tick.events = [signatureEvent('expired')];
  assert.deepEqual(replayAudioEventsAt(replay, 0), []);
});

test('Tractor Hook replaces the generic release and signature bursts cap at two voices', () => {
  const replay = replayWithEvents([{ type: 'Shot', slot: 3 }]);
  replay.signaturePresentationId = 'arc-relay-signatures-v1';
  const tick = replay.ticks[0]!;
  const template = tick.events[0]!;
  const baseActors = tick.before.actors.map((actor) => actor.identity);
  const actors = [
    baseActors[0]!,
    baseActors[1]!,
    {
      ...baseActors[0]!,
      actorKey: `${baseActors[0]!.actorKey}:audio-third`,
      unitKey: `${baseActors[0]!.unitKey}:audio-third`,
      unitId: baseActors[0]!.unitId + 100,
    },
  ];
  const signature = (
    owner: (typeof actors)[number],
    signatureId: string,
    reason: string,
    operationId: string,
  ): ReplayCausalEvent =>
    ({
      ...structuredClone(template),
      eventId: `signature:${operationId}`,
      type: 'game-mode',
      sourceActor: owner,
      projectileId: null,
      arcRelayFact: {
        kind: 'signature-changed',
        operationId,
        signatureId,
        ownerActor: owner,
        phase: null,
        reason,
      },
    }) as ReplayCausalEvent;

  const hookOperation = 'hook-operation';
  tick.events = [
    signature(actors[0]!, 'tractor-hook', 'launched', hookOperation),
  ];
  tick.projectileTraversals = [
    {
      projectileId: hookOperation,
      ownerActor: actors[0]!,
      ownerActorKey: actors[0]!.actorKey,
      launchDirection: 'east',
      from: { x: 0, y: 1 },
      path: [{ x: 1, y: 1 }],
      heading: 'east',
      shotProgram: null,
      programmedPath: null,
      attackProfileId: 'hook-bolt',
    },
  ];
  replay.contract = {
    kind: 'v3-generic',
    rawContract: {
      rules: {
        attackProfiles: [
          {
            id: 'hook-bolt',
            projectile: { mode: 'instant-ray', strikeWindupTicks: 1 },
          },
        ],
      },
    },
  } as unknown as ReplayModel['contract'];
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['signature-tether'],
    'the hook launch does not layer an ordinary projectile release',
  );

  tick.projectileTraversals = [];
  tick.events = [
    signature(actors[0]!, 'vector-dash', 'completed', 'dash'),
    signature(actors[1]!, 'exchange', 'completed', 'exchange'),
    signature(actors[2]!, 'kinetic-burst', 'completed', 'kinetic'),
  ];
  const voices = replayAudioEventsAt(replay, 0);
  assert.equal(voices.length, 2);
  assert.ok(voices.every((event) => event.cue.startsWith('signature-')));
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
