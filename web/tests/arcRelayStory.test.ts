import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  createPresenter,
  defaultPlaybackSpeed,
} from './.harness/harness.entry.js';
import { loadReplayJson } from '../src/replayIngress.ts';
import type {
  ReplayArcRelayFact,
  ReplayArcRelayModeState,
  ReplayCausalEvent,
  ReplayModel,
} from '../src/replayModel.ts';

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

test('Arc Relay defaults to a human-readable half-speed first watch', () => {
  assert.equal(defaultPlaybackSpeed(arcRelayReplay()), 0.5);
});

test('Arc Relay presents the carrier and all five spectator beats', () => {
  const presenter = createPresenter(arcRelayReplay());
  assert.equal(presenter.at(1).arcRelay?.cue.headline.includes('PULSE CORE'), true);
  assert.equal(presenter.at(1).arcRelay?.cores[0]?.carrierTeamId, 0);
  assert.deepEqual(
    [0, 2, 3, 4, 5].map((tick) => presenter.at(tick).arcRelay?.beat?.kind),
    ['birth', 'drop', 'steal', 'bank', 'pulse'],
  );
});
