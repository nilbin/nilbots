import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import { loadReplayObject } from '../src/replayIngress.ts';
import type { ArcRelayBroadcastV1 } from '../src/replayBroadcastV1.ts';
import { teamVisionAt } from '../src/render/teamVision.ts';

test('the compact Arc Relay vision column stays one shared snapshot per team', () => {
  const fixture = new URL(
    '../../arena-bots/arc-relay/stock-mind-v0/preflight/broadcast.json.gz',
    import.meta.url,
  );
  const broadcast = JSON.parse(
    gunzipSync(readFileSync(fixture)).toString('utf8'),
  ) as ArcRelayBroadcastV1;
  broadcast.worlds = broadcast.worlds.slice(0, 1);
  broadcast.turns = broadcast.turns.slice(0, 1);
  broadcast.startEvents = broadcast.startEvents.slice(0, 1);
  broadcast.events = broadcast.events.slice(0, 1);
  broadcast.traversals = broadcast.traversals.slice(0, 1);
  broadcast.births = broadcast.births.slice(0, 1);

  const teams = [...new Set(broadcast.turns[0]!.map((turn) => turn[0][0]))];
  broadcast.vision = [
    teams.map((teamId, index) => [
      teamId,
      [[index + 2, index + 3]],
    ]),
  ];

  const { replay } = loadReplayObject(broadcast);
  for (const [index, teamId] of teams.entries()) {
    const turns = replay.ticks[0]!.actorTurns.filter(
      (turn) => turn.actor.teamId === teamId,
    );
    assert.ok(turns.length > 0);
    assert.ok(
      turns.every((turn) => turn.observation.visibleTiles.length === 0),
    );
    const selected = replay.units.find((unit) => unit.teamId === teamId);
    assert.ok(selected);
    const vision = teamVisionAt(replay, replay.ticks[0], selected.unitKey);
    assert.ok(vision);
    assert.deepEqual([...vision.visibleTiles], [`${index + 2},${index + 3}`]);
  }
  assert.equal(replay.ticks[0]!.publishedTeamVision?.length, teams.length);
});
