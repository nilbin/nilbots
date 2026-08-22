import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import { createCanvas } from '@napi-rs/canvas';
import { drawArena } from './.harness/harness.entry.js';
import { loadReplayObject } from '../src/replayIngress.ts';
import { expandArcRelayBroadcastV1 } from '../src/replayBroadcastV1.ts';
import { validateReplayV3 } from '../src/replayV3Normalize.ts';
import type { ReplayModel } from '../src/replayModel.ts';
import {
  arcWorkshopsFor,
  WORKSHOP_COMPLETION_BEAT_TICKS,
} from '../src/render/arcWorkshopPresentation.ts';

/**
 * Neutral veterancy Workshops, end to end through the arc-relay wire.
 *
 * The substrate is the tracked Tapline broadcast, which carries NO Workshops —
 * so the same fixture proves both halves of the contract: a historical replay
 * still renders exactly as it did, and a spliced Workshop reaches the model,
 * the shared derivation and both renderers.
 */

const BROADCAST_URL = new URL(
  '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
  import.meta.url,
);

const WORKSHOP_TILE = { x: 6, y: 6 };
const REQUIRED_TICKS = 15;
const DORMANCY_TICKS = 45;

type Loose = Record<string, unknown>;

function broadcast(): Loose {
  return JSON.parse(
    gunzipSync(readFileSync(BROADCAST_URL)).toString('utf8'),
  ) as Loose;
}

/** One Workshop published on every world state, in one phase throughout. */
function withWorkshop(
  document: Loose,
  workshop: Loose | null,
  facts: { tick: number; fact: Loose }[] = [],
  dormancyTicks = DORMANCY_TICKS,
): Loose {
  const rules = (
    (document.header as Loose).contract as Loose
  ).rules as Loose;
  const gameMode = rules.gameMode as Loose;
  const binding = ((document.header as Loose).contract as Loose)
    .modeMapBinding as Loose;
  if (workshop) {
    gameMode.veterancyWorkshops = {
      workshopIds: ['north'],
      unlockTick: 4,
      requiredChannelTicks: REQUIRED_TICKS,
      xpAward: 1,
      dormancyTicks,
    };
    binding.veterancyWorkshops = [
      {
        workshopId: 'north',
        trainRegionId: 'veterancy-workshop-north-train',
        contestRegionId: 'veterancy-workshop-north-contest',
      },
    ];
    const worlds = [
      document.initial as unknown[],
      ...(document.worlds as unknown[][]),
    ];
    for (const world of worlds) {
      const mode = (world as unknown[])[7] as Loose;
      mode.workshops = [{ ...workshop }];
    }
  }
  for (const { tick, fact } of facts) {
    (document.events as unknown[][])[tick]!.push({
      eventHandle: `spliced-workshop:${tick}`,
      tick,
      globalOrdinal: `90000${tick}`,
      sourceOrdinal: 99,
      kind: 'arc-relay',
      payload: { kind: 'arc-relay', fact },
      audience: { kind: 'public' },
    });
  }
  return document;
}

function readyWorkshop(): Loose {
  return {
    workshopId: 'north',
    trainingTile: { ...WORKSHOP_TILE },
    phase: 'ready',
    trainingTeamId: null,
    progressTicks: 0,
    requiredTicks: REQUIRED_TICKS,
    readyAtTick: null,
  };
}

function frameHash(replay: ReplayModel, time: number): string {
  const canvas = createCanvas(420, 320);
  const ctx = canvas.getContext('2d');
  drawArena(
    ctx as unknown as CanvasRenderingContext2D,
    replay,
    { time, selectedUnitKey: null, showVisibility: false },
    420,
    320,
  );
  return createHash('sha256')
    .update(Buffer.from(ctx.getImageData(0, 0, 420, 320).data))
    .digest('hex');
}

test('a replay without Workshops normalizes to an empty landmark set', () => {
  const replay = loadReplayObject(broadcast()).replay;
  const workshops = arcWorkshopsFor(replay);
  assert.equal(workshops.present, false);
  assert.equal(workshops.dormancyTicks, null);
  for (const tick of replay.ticks) {
    const mode = tick.after.mode;
    assert.equal(mode?.kind, 'arc-relay');
    if (mode?.kind !== 'arc-relay' || !('workshops' in mode)) continue;
    assert.deepEqual(mode.workshops, []);
  }
});

test('an absent Workshops field draws the exact frame it always did', () => {
  // The whole "additive and inert" claim, as pixels: normalizing a document
  // that never mentioned Workshops must not move a single one.
  const historical = loadReplayObject(broadcast()).replay;
  const withEmptyArray = loadReplayObject(broadcast()).replay;
  for (const tick of withEmptyArray.ticks) {
    const mode = tick.after.mode;
    if (mode?.kind === 'arc-relay' && 'workshops' in mode) mode.workshops = [];
  }
  for (const time of [0, 12, 30]) {
    assert.equal(
      frameHash(withEmptyArray, time),
      frameHash(historical, time),
      `tick ${time} moved without a Workshop in the document`,
    );
  }
});

test('a published Workshop reaches the model, the reading, and the frame', () => {
  const replay = loadReplayObject(
    withWorkshop(broadcast(), readyWorkshop()),
  ).replay;
  const mode = replay.ticks[8]?.after.mode;
  assert.equal(mode?.kind, 'arc-relay');
  if (mode?.kind !== 'arc-relay') return;
  assert.equal(mode.workshops.length, 1);
  assert.deepEqual(mode.workshops[0]?.trainingTile, WORKSHOP_TILE);
  assert.equal(mode.workshops[0]?.phase, 'ready');

  const index = arcWorkshopsFor(replay);
  assert.equal(index.present, true);
  assert.equal(index.dormancyTicks, DORMANCY_TICKS);
  assert.equal(index.requiredChannelTicks, REQUIRED_TICKS);
  const reading = index.visualsAt(mode.workshops, 8)[0]!;
  assert.equal(reading.phase, 'ready');
  assert.equal(reading.progressFraction, 0);
  assert.equal(reading.dormantRemainingTicks, null);
  assert.equal(reading.completion, null);

  const plain = loadReplayObject(broadcast()).replay;
  assert.notEqual(
    frameHash(replay, 8),
    frameHash(plain, 8),
    'a ready Workshop must be visible on the floor',
  );
});

test('phase and progress are read from the published state, not invented', () => {
  const training = loadReplayObject(
    withWorkshop(broadcast(), {
      ...readyWorkshop(),
      phase: 'training',
      trainingTeamId: 1,
      progressTicks: 9,
    }),
  ).replay;
  const trainingMode = training.ticks[8]!.after.mode;
  if (trainingMode?.kind !== 'arc-relay') throw new Error('expected arc-relay');
  const trainingRead = arcWorkshopsFor(training).visualsAt(
    trainingMode.workshops,
    8,
  )[0]!;
  assert.equal(trainingRead.trainingTeamId, 1);
  assert.equal(trainingRead.progressTicks, 9);
  assert.equal(trainingRead.progressFraction, 9 / REQUIRED_TICKS);
  assert.equal(trainingRead.dormantFraction, null);

  const dormant = loadReplayObject(
    withWorkshop(broadcast(), {
      ...readyWorkshop(),
      phase: 'dormant',
      readyAtTick: 40,
    }),
  ).replay;
  const dormantMode = dormant.ticks[8]!.after.mode;
  if (dormantMode?.kind !== 'arc-relay') throw new Error('expected arc-relay');
  const dormantIndex = arcWorkshopsFor(dormant);
  const dormantRead = dormantIndex.visualsAt(dormantMode.workshops, 10)[0]!;
  // The countdown is exactly `readyAtTick - now`, never a rules re-derivation.
  assert.equal(dormantRead.dormantRemainingTicks, 30);
  assert.equal(dormantRead.trainingTeamId, null);
  assert.equal(
    dormantRead.dormantFraction,
    1 - 30 / DORMANCY_TICKS,
  );

  // A dormant bench and a channelling one must not look the same.
  assert.notEqual(frameHash(training, 8), frameHash(dormant, 8));
});

test('zero dormancy presents the completion boundary as immediately ready', () => {
  const replay = loadReplayObject(
    withWorkshop(
      broadcast(),
      {
        ...readyWorkshop(),
        phase: 'dormant',
        readyAtTick: 9,
      },
      [],
      0,
    ),
  ).replay;
  const mode = replay.ticks[8]!.after.mode;
  if (mode?.kind !== 'arc-relay') throw new Error('expected arc-relay');

  const index = arcWorkshopsFor(replay);
  const reading = index.visualsAt(mode.workshops, 8)[0]!;
  assert.equal(index.dormancyTicks, 0);
  assert.equal(reading.phase, 'ready');
  assert.equal(reading.dormantRemainingTicks, null);
  assert.equal(reading.dormantFraction, null);

  const readyReplay = loadReplayObject(
    withWorkshop(broadcast(), readyWorkshop(), [], 0),
  ).replay;
  assert.equal(
    frameHash(replay, 8),
    frameHash(readyReplay, 8),
    'zero-dormancy completion must not dim or count down the Workshop',
  );
});

test('a completion fact carries its trainee, its award, and a decaying beat', () => {
  const replay = loadReplayObject(
    withWorkshop(broadcast(), readyWorkshop(), [
      {
        tick: 12,
        fact: {
          kind: 'workshop-training-completed',
          workshopId: 'north',
          teamId: 0,
          traineeActorId: { teamId: 0, unitId: 2, lifeId: 0 },
          xpAward: 1,
          readyAgainTick: 58,
          trainingTile: { ...WORKSHOP_TILE },
        },
      },
      {
        tick: 20,
        fact: {
          kind: 'workshop-training-interrupted',
          workshopId: 'north',
          teamId: 1,
          reason: 'damaged',
          progressLost: 7,
          trainingTile: { ...WORKSHOP_TILE },
        },
      },
    ]),
  ).replay;

  const facts = replay.ticks[12]!.events
    .map((event) => event.arcRelayFact)
    .filter((fact) => fact?.kind === 'workshop-training-completed');
  assert.equal(facts.length, 1);
  const completed = facts[0]!;
  if (completed.kind !== 'workshop-training-completed') return;
  // The engine's own attribution survives normalization; nothing else in the
  // viewer knows who trained.
  assert.equal(completed.traineeActor.teamId, 0);
  assert.equal(completed.traineeActor.unitId, 2);
  assert.equal(completed.traineeActor.lifeId, 0);
  assert.equal(completed.xpAward, 1);
  assert.equal(completed.readyAgainTick, 58);

  const mode = replay.ticks[12]!.after.mode;
  if (mode?.kind !== 'arc-relay') throw new Error('expected arc-relay');
  const index = arcWorkshopsFor(replay);
  const atFact = index.visualsAt(mode.workshops, 12)[0]!;
  assert.equal(atFact.completion?.teamId, 0);
  assert.equal(atFact.completion?.xpAward, 1);
  assert.equal(atFact.completion?.strength, 1);

  // It outlives its tick — a one-tick flash is subliminal — and then stops.
  const midway = index.visualsAt(mode.workshops, 12.9)[0]!;
  assert.ok(midway.completion !== null);
  assert.ok(midway.completion!.strength < 1);
  assert.ok(midway.completion!.strength > 0);
  assert.equal(
    index.visualsAt(mode.workshops, 12 + WORKSHOP_COMPLETION_BEAT_TICKS)[0]!
      .completion,
    null,
  );
  // And it never pre-rolls: the beat does not exist before its fact.
  assert.equal(index.visualsAt(mode.workshops, 11.99)[0]!.completion, null);

  const interrupted = index.visualsAt(
    (replay.ticks[20]!.after.mode as typeof mode).workshops,
    20,
  )[0]!;
  assert.equal(interrupted.interruption?.reason, 'damaged');
  assert.equal(interrupted.interruption?.progressLost, 7);
  assert.equal(interrupted.interruption?.teamId, 1);
});

test('a Workshop that disagrees with itself is rejected at the wire', () => {
  // Positive control first: without it, a guard that rejected everything would
  // read as a passing test. A broadcast expands into a document the full
  // canonical validator still refuses for unrelated reasons (it carries no
  // turns), so the assertion is that a well-formed Workshop is not what it
  // objects to.
  for (const sound of [
    readyWorkshop(),
    {
      ...readyWorkshop(),
      phase: 'training',
      trainingTeamId: 1,
      progressTicks: REQUIRED_TICKS,
    },
    { ...readyWorkshop(), phase: 'contested' },
    { ...readyWorkshop(), phase: 'dormant', readyAtTick: 61 },
  ]) {
    const failure = firstValidationFailure(withWorkshop(broadcast(), sound));
    assert.ok(
      failure === null || !failure.includes('workshops'),
      `rejected a well-formed Workshop: ${JSON.stringify(sound)} → ${failure}`,
    );
  }

  // Phase, team and countdown are one published fact. A viewer that accepted a
  // "ready" bench owned by a team would draw a channel nobody is running.
  for (const broken of [
    { ...readyWorkshop(), trainingTeamId: 0 },
    { ...readyWorkshop(), phase: 'training', trainingTeamId: null, progressTicks: 3 },
    { ...readyWorkshop(), phase: 'training', trainingTeamId: 0, progressTicks: 0 },
    { ...readyWorkshop(), progressTicks: 4 },
    { ...readyWorkshop(), readyAtTick: 30 },
    { ...readyWorkshop(), phase: 'dormant', readyAtTick: null },
    { ...readyWorkshop(), phase: 'idle' },
    { ...readyWorkshop(), progressTicks: REQUIRED_TICKS + 1 },
  ]) {
    const failure = firstValidationFailure(withWorkshop(broadcast(), broken));
    assert.match(
      failure ?? '',
      /workshops\[0\]/,
      `accepted a contradictory Workshop: ${JSON.stringify(broken)}`,
    );
  }
});

function firstValidationFailure(document: Loose): string | null {
  const wire = expandArcRelayBroadcastV1(
    document as unknown as Parameters<typeof expandArcRelayBroadcastV1>[0],
  );
  try {
    validateReplayV3(wire, (path, message) => {
      throw new Error(`${path}: ${message}`);
    });
  } catch (error) {
    return (error as Error).message;
  }
  return null;
}
