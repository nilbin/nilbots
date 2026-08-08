import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  teamVisionAt,
  teamVisionSeesActor,
  teamVisionSeesProjectile,
} from '../src/render/teamVision.ts';

function frontlineReplay() {
  return loadReplayJson(
    readFileSync(
      new URL('./fixtures/frontline-replay-v2.json', import.meta.url),
      'utf8',
    ),
  ).replay;
}

test('selecting any bot reveals the union of its whole team vision', () => {
  const replay = frontlineReplay();
  const tick = replay.ticks.find(
    (candidate) =>
      candidate.actorTurns.filter((turn) => turn.actor.teamId === 0).length >= 2,
  );
  assert.ok(tick);
  const teamTurns = tick.actorTurns.filter((turn) => turn.actor.teamId === 0);
  const [scout, wing] = teamTurns;
  assert.ok(scout && wing);

  const tileTemplate = scout.observation.visibleTiles[0];
  assert.ok(tileTemplate);
  scout.observation.visibleTiles = [
    { ...tileTemplate, position: { x: 1, y: 1 } },
  ];
  wing.observation.visibleTiles = [
    { ...tileTemplate, position: { x: 9, y: 7 } },
  ];

  const enemy = tick.actorTurns.find((turn) => turn.actor.teamId === 1)?.actor;
  const observedTemplate = wing.observation.self;
  assert.ok(enemy && observedTemplate);
  scout.observation.enemies = [];
  wing.observation.enemies = [
    {
      ...observedTemplate,
      actor: { kind: 'exact', identity: enemy },
    },
  ];

  const selectedScout = teamVisionAt(replay, tick, scout.actor.unitKey);
  const selectedWing = teamVisionAt(replay, tick, wing.actor.unitKey);
  assert.ok(selectedScout && selectedWing);
  assert.deepEqual(
    [...selectedScout.visibleTiles].sort(),
    ['1,1', '9,7'],
  );
  assert.deepEqual(
    [...selectedWing.visibleTiles].sort(),
    ['1,1', '9,7'],
  );
  assert.equal(teamVisionSeesActor(selectedScout, enemy), true);
  assert.equal(teamVisionSeesActor(selectedWing, scout.actor), true);
});

test('team projectile sight unions exact handles and falls back to union tiles', () => {
  const replay = frontlineReplay();
  const tick = replay.ticks.find(
    (candidate) =>
      candidate.actorTurns.filter((turn) => turn.actor.teamId === 0).length >= 2 &&
      candidate.actorTurns.some(
        (turn) => (turn.observation.visibleProjectiles?.length ?? 0) > 0,
      ),
  );
  assert.ok(tick);
  const [scout, wing] = tick.actorTurns.filter(
    (turn) => turn.actor.teamId === 0,
  );
  assert.ok(scout && wing);
  const projectileTemplate = tick.actorTurns
    .flatMap((turn) => turn.observation.visibleProjectiles ?? [])
    .at(0);
  const tileTemplate = scout.observation.visibleTiles[0];
  assert.ok(projectileTemplate && tileTemplate);

  scout.observation.visibleProjectiles = [
    { ...projectileTemplate, projectileHandle: 'scout-bolt' },
  ];
  scout.aliases.projectiles = [
    { projectileHandle: 'scout-bolt', projectileId: 'bolt-a' },
  ];
  wing.observation.visibleProjectiles = [
    { ...projectileTemplate, projectileHandle: 'wing-bolt' },
  ];
  wing.aliases.projectiles = [
    { projectileHandle: 'wing-bolt', projectileId: 'bolt-b' },
  ];

  const exact = teamVisionAt(replay, tick, scout.actor.unitKey);
  assert.ok(exact);
  assert.deepEqual(
    [...(exact.visibleProjectileIds ?? [])].sort(),
    ['bolt-a', 'bolt-b'],
  );
  assert.equal(teamVisionSeesProjectile(exact, 'bolt-b', 99, 99), true);

  wing.observation.visibleProjectiles = null;
  scout.observation.visibleTiles = [
    { ...tileTemplate, position: { x: 1, y: 1 } },
  ];
  wing.observation.visibleTiles = [
    { ...tileTemplate, position: { x: 7, y: 6 } },
  ];
  const tileFallback = teamVisionAt(replay, tick, scout.actor.unitKey);
  assert.ok(tileFallback);
  assert.equal(tileFallback.visibleProjectileIds, null);
  assert.equal(teamVisionSeesProjectile(tileFallback, 'redacted', 7, 6), true);
  assert.equal(teamVisionSeesProjectile(tileFallback, 'redacted', 8, 6), false);
});

test('a selected team with no active observer gets no omniscient fallback', () => {
  const replay = frontlineReplay();
  const tick = replay.ticks[0]!;
  const selected = replay.units.find((unit) => unit.teamId === 0)!.unitKey;
  tick.actorTurns = tick.actorTurns.filter((turn) => turn.actor.teamId !== 0);

  const vision = teamVisionAt(replay, tick, selected);
  assert.ok(vision);
  assert.equal(vision.visibleTiles.size, 0);
  assert.equal(teamVisionSeesProjectile(vision, 'hidden', 1, 1), false);
  const enemy = tick.actorTurns.find((turn) => turn.actor.teamId === 1)!.actor;
  assert.equal(teamVisionSeesActor(vision, enemy), false);
});

test('legacy compact turns with omitted vision disable fog instead of blacking out the map', () => {
  const replay = frontlineReplay();
  const tick = replay.ticks[0]!;
  const selected = replay.units.find((unit) => unit.teamId === 0)!.unitKey;
  const teamTurns = tick.actorTurns.filter((turn) => turn.actor.teamId === 0);
  assert.ok(teamTurns.length > 0);
  for (const turn of teamTurns) turn.observation.visibleTiles = [];

  assert.equal(teamVisionAt(replay, tick, selected), null);
});
