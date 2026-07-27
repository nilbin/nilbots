import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { decodeReplay } from '../src/replayNormalize.ts';

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
  const { raw, parsed } = readEngineFixture('frontline-replay-v2.json');
  const decoded = decodeReplay(parsed);
  const replay = decoded.replay;

  assert.ok(raw.includes(`"seed":"${JS_UNSAFE_SEED}"`));
  assert.equal(decoded.replayVersion, 2);
  assert.equal(replay.sourceVersion, 2);
  assert.equal(replay.seed, JS_UNSAFE_SEED);
  assert.equal(replay.partial, false);
  assert.match(replay.replayHash ?? '', /^[0-9a-f]{64}$/);
  assert.equal(replay.ticks.length, 4);

  assert.deepEqual(
    replay.ticks[0]?.before.actors.map((actor) => actor.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(replay.ticks[0]?.after.actors, []);
  assert.deepEqual(replay.ticks[1]?.activeActorKeys, []);
  assert.deepEqual(
    replay.ticks[2]?.before.actors.map((actor) => actor.actorKey),
    [
      'frontline:0:unit:0:life:1',
      'frontline:1:unit:0:life:1',
    ],
  );
  assert.ok(
    replay.ticks[0]?.events.filter((event) => event.type === 'destroyed')
      .length === 2,
  );
  assert.ok(
    replay.ticks[2]?.actorTurns.every(
      (turn) => turn.lifeStart?.spawnReason === 'respawn',
    ),
  );
});

test('engine-authored tick-zero failure decodes as a hashless empty prefix', () => {
  const { raw, parsed } = readEngineFixture(
    'frontline-replay-v2-partial-zero-tick.json',
  );
  const decoded = decodeReplay(parsed);
  const replay = decoded.replay;

  assert.ok(raw.includes('"partial":true'));
  assert.equal(decoded.replayVersion, 2);
  assert.equal(replay.sourceVersion, 2);
  assert.equal(replay.seed, JS_UNSAFE_SEED);
  assert.equal(replay.partial, true);
  assert.equal(replay.replayHash, null);
  assert.equal(replay.result, null);
  assert.equal(replay.initialWorld, null);
  assert.deepEqual(replay.ticks, []);
});
