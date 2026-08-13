import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import { loadReplayJson } from '../src/replayIngress.ts';

test('current Tapline broadcast rules and terminal strike cleanup normalize', () => {
  const loaded = loadReplayJson(
    gunzipSync(
      readFileSync(
        new URL(
          '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
          import.meta.url,
        ),
      ),
    ).toString('utf8'),
  );
  assert.equal(loaded.replay.contract.kind, 'v3-generic');
  if (loaded.replay.contract.kind !== 'v3-generic') return;
  const mode = loaded.replay.contract.rawContract.rules.gameMode;
  assert.equal(mode.kind, 'arc-relay');
  if (mode.kind !== 'arc-relay') return;
  assert.equal(mode.rearArcDamageMultiplier, 2);
  assert.equal(mode.veterancyXpPerLevel, 2);
  assert.equal(mode.seedPhasedResolutionOrder, true);
  assert.equal(mode.chiralWellBirths, true);
  assert.equal(mode.healZoneTicksPerHp, 3);
  const terminalMode = loaded.replay.ticks.at(-1)?.after.mode;
  assert.equal(terminalMode?.kind, 'arc-relay');
  if (terminalMode?.kind !== 'arc-relay') return;
  assert.ok('pendingStrikes' in terminalMode);
  assert.ok(terminalMode.pendingStrikes.length > 0);
});
