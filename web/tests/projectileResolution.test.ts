import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import type { ReplayModel } from '../src/replayModel.ts';
import {
  buildOverlays,
  loadReplayJson,
  spentBoltsAt,
} from './.harness/harness.entry.js';

const frontline = loadReplayJson(
  readFileSync(
    join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'),
    'utf8',
  ),
).replay;

function withProjectileTerminal(
  kind: 'hit' | 'miss' | 'deflected' | 'legacy',
): { replay: ReplayModel; projectileId: string } {
  const replay = structuredClone(frontline) as ReplayModel;
  const death = replay.ticks[10]!;
  const traversal = death.projectileTraversals[0]!;
  const projectileId = traversal.projectileId;
  death.after.projectiles = (death.after.projectiles ?? []).filter(
    (projectile) => projectile.projectileId !== projectileId,
  );
  death.events = death.events.filter(
    (event) => event.projectileId !== projectileId,
  );

  if (kind === 'legacy') {
    replay.sourceVersion = 1;
  } else if (kind !== 'miss') {
    const template = replay.ticks
      .flatMap((tick) => tick.events)
      .find((event) => event.sourceActor !== null);
    assert.ok(template);
    const terminal =
      traversal.path[traversal.path.length - 1] ?? traversal.from;
    const guard = death.before.actors.find(
      (actor) => actor.actorKey !== traversal.ownerActor.actorKey,
    );
    assert.ok(guard);
    death.events.push({
      ...template,
      eventId: `presentation-test-${kind}`,
      tick: death.tick,
      ordinal: death.events.length,
      type: kind === 'hit' ? 'damage' : 'projectile-deflected',
      sourceActor: traversal.ownerActor,
      targetActor: kind === 'deflected' ? guard.identity : template.targetActor,
      projectileId,
      from: terminal,
      to: terminal,
      toFacing: kind === 'deflected' ? guard.facing : template.toFacing,
    });
  }

  return { replay, projectileId };
}

test('projectile terminals use exact replay causality without legacy inference', () => {
  for (const expected of ['hit', 'miss', 'deflected', 'legacy'] as const) {
    const { replay, projectileId } = withProjectileTerminal(expected);
    const spent = spentBoltsAt(replay, 11.4).find(
      (bolt) => bolt.id === projectileId,
    );
    assert.ok(spent);
    assert.equal(spent.resolution, expected);
    assert.ok(spent.age > 0 && spent.age < 1);
  }
});

test('unattributed v2 combat terminals preserve the conservative legacy finish', () => {
  for (const type of ['damage', 'projectile-deflected'] as const) {
    const { replay, projectileId } = withProjectileTerminal('miss');
    const death = replay.ticks[10]!;
    const template = replay.ticks
      .flatMap((tick) => tick.events)
      .find((event) => event.sourceActor !== null);
    assert.ok(template);
    death.events.push({
      ...template,
      eventId: `presentation-test-unattributed-${type}`,
      tick: death.tick,
      ordinal: death.events.length,
      type,
      projectileId: null,
    });

    const spent = spentBoltsAt(replay, 11.4).find(
      (bolt) => bolt.id === projectileId,
    );
    assert.ok(spent);
    assert.equal(spent.resolution, 'legacy');
  }

  const exact = withProjectileTerminal('hit');
  const death = exact.replay.ticks[10]!;
  const template = death.events.find(
    (event) =>
      event.type === 'damage' &&
      event.projectileId === exact.projectileId,
  );
  assert.ok(template);
  death.events.push({
    ...template,
    eventId: 'presentation-test-unattributed-beside-exact',
    ordinal: death.events.length,
    type: 'projectile-deflected',
    projectileId: null,
  });
  const spent = spentBoltsAt(exact.replay, 11.4).find(
    (bolt) => bolt.id === exact.projectileId,
  );
  assert.ok(spent);
  assert.equal(spent.resolution, 'hit');
});

test('WebGL gives exact hits and misses different projectile-owned finishes', () => {
  const hit = withProjectileTerminal('hit');
  const hitOverlays = buildOverlays(hit.replay);
  hitOverlays.update(11.4, null, false);
  let visibleHit = false;
  let visibleMiss = false;
  hitOverlays.group.traverse((node) => {
    if (!node.visible) return;
    if (node.userData.cue === 'projectile-hit') visibleHit = true;
    if (node.userData.cue === 'projectile-miss') visibleMiss = true;
  });
  assert.equal(visibleHit, true);
  assert.equal(visibleMiss, false);
  hitOverlays.dispose();

  const miss = withProjectileTerminal('miss');
  const missOverlays = buildOverlays(miss.replay);
  missOverlays.update(11.4, null, false);
  visibleHit = false;
  visibleMiss = false;
  missOverlays.group.traverse((node) => {
    if (!node.visible) return;
    if (node.userData.cue === 'projectile-hit') visibleHit = true;
    if (node.userData.cue === 'projectile-miss') visibleMiss = true;
  });
  assert.equal(visibleHit, false);
  assert.equal(visibleMiss, true);
  missOverlays.dispose();
});

test('a deflection owns the contact instant without a duplicate terminal finish', () => {
  const deflected = withProjectileTerminal('deflected');
  const overlays = buildOverlays(deflected.replay);

  overlays.update(10.8, null, false);
  let visibleArc = false;
  let visibleContact = false;
  let visibleTerminal = false;
  overlays.group.traverse((node) => {
    if (!node.visible) return;
    if (node.userData.cue === 'deflect-arc') visibleArc = true;
    if (node.userData.cue === 'deflect-contact') visibleContact = true;
    if (
      node.userData.cue === 'projectile-hit' ||
      node.userData.cue === 'projectile-miss'
    ) {
      visibleTerminal = true;
    }
  });
  assert.equal(visibleArc, true);
  assert.equal(visibleContact, true);
  assert.equal(visibleTerminal, false);

  overlays.update(11.4, null, false);
  visibleTerminal = false;
  overlays.group.traverse((node) => {
    if (
      node.visible &&
      (node.userData.cue === 'projectile-hit' ||
        node.userData.cue === 'projectile-miss')
    ) {
      visibleTerminal = true;
    }
  });
  assert.equal(visibleTerminal, false);
  overlays.dispose();
});
