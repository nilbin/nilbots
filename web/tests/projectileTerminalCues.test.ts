import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import type * as THREE from 'three';
import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayProjectileTraversal,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import {
  buildOverlays,
  loadReplayJson,
  projectileDissipationsAt,
  projectileTerminalsInTick,
} from './.harness/harness.entry.js';

const generic = loadReplayJson(
  readFileSync(
    join(
      import.meta.dirname,
      '..',
      '..',
      'tests',
      'BotArena.Engine.Tests',
      'Fixtures',
      'generic-replay-v3.json',
    ),
    'utf8',
  ),
).replay;

const classLooks = [
  ['trident-spark', 'fork-burst', 3],
  ['rebound-diamond', 'facet-bloom', 4],
  ['lattice-rivet', 'lattice-unweave', 6],
] as const;

type Terminal = NonNullable<ReplayProjectileTraversal['terminal']>;

function terminalReplay(
  terminal: Terminal,
  eventType: 'damage' | 'destruction' | 'projectile-deflected' | null = null,
  projectileLookId = 'trident-spark',
): ReplayModel {
  const replay = structuredClone(generic) as ReplayModel;
  replay.forms[0]!.projectileLookId = projectileLookId;
  const tick = replay.ticks[0]!;
  const traversal = tick.projectileTraversals[0]!;
  traversal.terminal = terminal;
  tick.after.projectiles = (tick.after.projectiles ?? []).filter(
    (projectile) => projectile.projectileId !== traversal.projectileId,
  );
  if (eventType !== null) {
    const template = structuredClone(
      generic.ticks[1]!.events.find((event) => event.type === 'damage')!,
    ) as ReplayCausalEvent;
    const target = tick.before.actors.find(
      (actor) => actor.actorKey !== traversal.ownerActor.actorKey,
    )!;
    tick.events.push({
      ...template,
      eventId: `synthetic:${eventType}`,
      tick: 0,
      ordinal: tick.events.length,
      type: eventType,
      sourceActor: traversal.ownerActor,
      targetActor: target.identity,
      projectileId: traversal.projectileId,
      from: traversal.from,
      to: traversal.path.at(-1) ?? traversal.from,
      toFacing:
        eventType === 'projectile-deflected' ? target.facing : null,
      amount: eventType === 'damage' ? 1 : null,
      newHealth:
        eventType === 'damage' ? Math.max(0, target.health - 1) : 0,
    });
  }
  return replay;
}

function visibleNodes(
  overlays: ReturnType<typeof buildOverlays>,
  predicate: (node: THREE.Object3D) => boolean,
): THREE.Object3D[] {
  const found: THREE.Object3D[] = [];
  overlays.group.traverse((node) => {
    if (node.visible && predicate(node)) found.push(node);
  });
  return found;
}

function terminalSnapshot(root: THREE.Object3D): unknown {
  return root.children.map((child) => ({
    position: child.position.toArray(),
    rotation: child.rotation.toArray(),
    scale: child.scale.toArray(),
    visible: child.visible,
  }));
}

test('terminal classification never turns contacts or cleanup into a miss', () => {
  const cases = [
    [
      { kind: 'actor-contact', targetActorId: { teamId: 1, unitId: 0, lifeId: 0 }, appliedDamage: true },
      'damage',
      'damage-hit',
    ],
    [
      { kind: 'actor-contact', targetActorId: { teamId: 1, unitId: 0, lifeId: 0 }, appliedDamage: true },
      'destruction',
      'destruction',
    ],
    [
      { kind: 'actor-contact', targetActorId: { teamId: 1, unitId: 0, lifeId: 0 }, appliedDamage: false },
      null,
      'guard-absorbed',
    ],
    [
      { kind: 'actor-contact', targetActorId: { teamId: 1, unitId: 0, lifeId: 0 }, appliedDamage: false },
      'projectile-deflected',
      'deflected',
    ],
    [{ kind: 'range-exhausted' }, null, 'spent'],
    [{ kind: 'wall-or-path-exhausted' }, null, 'spent'],
    [
      { kind: 'lifecycle-placement-purge', position: { x: 2, y: 3 } },
      null,
      'purged',
    ],
    [
      { kind: 'participant-disqualification', participantId: 10 },
      null,
      'purged',
    ],
  ] as const;

  for (const [terminal, eventType, expected] of cases) {
    const replay = terminalReplay(
      terminal as Terminal,
      eventType,
    );
    const [classified] = projectileTerminalsInTick(replay, 0);
    assert.equal(classified?.kind, expected);
    assert.equal(
      projectileDissipationsAt(replay, 1.25).length,
      0,
      `${expected} does not leak into the following generation-3 tick`,
    );
    assert.equal(
      projectileDissipationsAt(replay, 0.82).length,
      expected === 'spent' ? 1 : 0,
      `${expected} has the correct harmless-dissipation eligibility`,
    );
  }

  const legacy = terminalReplay({ kind: 'range-exhausted' });
  legacy.sourceVersion = 2;
  delete legacy.ticks[0]!.projectileTraversals[0]!.terminal;
  assert.equal(projectileDissipationsAt(legacy, 0.82).length, 0);
  assert.equal(
    projectileDissipationsAt(legacy, 1.25).length,
    1,
    'a terminal-less older replay retains the historical following-tick fallback',
  );
});

test('every class terminal cue has authored replay-time motion and scrub purity', () => {
  for (const [lookId, cue, pieceCount] of classLooks) {
    const replay = terminalReplay(
      {
        kind: 'actor-contact',
        targetActorId: { teamId: 1, unitId: 0, lifeId: 0 },
        appliedDamage: true,
      },
      'damage',
      lookId,
    );
    const overlays = buildOverlays(replay);

    overlays.update(0.72, null, false);
    const [root] = visibleNodes(
      overlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' &&
        node.userData.terminalCue === cue,
    );
    assert.ok(root, `${lookId} emits ${cue}`);
    assert.equal(root.userData.terminalKind, 'damage-hit');
    assert.equal(
      root.children.filter((child) => child.userData.pieceIndex !== undefined)
        .length,
      pieceCount,
    );
    const firstPiece = root.children.find(
      (child) => child.userData.pieceIndex === 0,
    ) as THREE.Mesh;
    assert.equal(
      (firstPiece.material as THREE.MeshBasicMaterial).color.getHexString(),
      '38bdf8',
      `${cue} keeps team color renderer-owned`,
    );
    const early = terminalSnapshot(root);

    overlays.update(0.9, null, false);
    const [laterRoot] = visibleNodes(
      overlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' &&
        node.userData.terminalCue === cue,
    );
    assert.ok(laterRoot);
    const later = terminalSnapshot(laterRoot);
    assert.notDeepEqual(later, early, `${cue} visibly animates`);

    overlays.update(0.1, null, false);
    assert.equal(
      visibleNodes(
        overlays,
        (node) => node.userData.cue === 'projectile-terminal',
      ).length,
      0,
      `${cue} does not leak before its authoritative contact`,
    );
    overlays.update(0.72, null, false);
    const [revisited] = visibleNodes(
      overlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' &&
        node.userData.terminalCue === cue,
    );
    assert.ok(revisited);
    assert.deepEqual(
      terminalSnapshot(revisited),
      early,
      `${cue} is identical after a backwards scrub`,
    );
    overlays.dispose();
  }
});

test('a persistent projectile keeps its exact firing-form cue', () => {
  const replay = structuredClone(generic) as ReplayModel;
  const mobile = replay.forms[0]!;
  mobile.projectileLookId = 'lattice-rivet';
  replay.forms.push({
    ...structuredClone(mobile),
    formId: 'firing-form',
    projectileLookId: 'trident-spark',
  });
  const ownerKey = replay.ticks[0]!.projectileTraversals[0]!.ownerActor.actorKey;
  for (const actor of [
    ...replay.ticks[0]!.before.actors,
    ...replay.ticks[0]!.after.actors,
  ]) {
    if (actor.actorKey === ownerKey) actor.formId = 'firing-form';
  }
  // The same life changes presentation form before its old projectile contacts. Resolving
  // at contact would incorrectly turn this into Lattice's six-node terminal.
  for (const actor of [
    ...replay.ticks[1]!.before.actors,
    ...replay.ticks[1]!.after.actors,
  ]) {
    if (actor.actorKey === ownerKey) actor.formId = 'mobile';
  }

  const overlays = buildOverlays(replay);
  overlays.update(1.82, null, false);
  const roots = visibleNodes(
    overlays,
    (node) =>
      node.userData.cue === 'projectile-terminal' &&
      node.userData.projectileId === '0',
  );
  assert.equal(roots.length, 1);
  assert.equal(roots[0]!.userData.terminalCue, 'fork-burst');
  overlays.dispose();
});

test('harmless class expiry animates separately from hit, guard and deflection', () => {
  for (const [lookId, cue] of classLooks) {
    const replay = terminalReplay(
      { kind: 'range-exhausted' },
      null,
      lookId,
    );
    const overlays = buildOverlays(replay);
    overlays.update(0.82, null, false);
    const [root] = visibleNodes(
      overlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' &&
        node.userData.terminalCue === cue,
    );
    assert.ok(root, `${lookId} has a harmless terminal animation`);
    assert.equal(root.userData.terminalKind, 'dissipation');
    overlays.dispose();
  }

  const spentReplay = terminalReplay({ kind: 'range-exhausted' });
  const spentOverlays = buildOverlays(spentReplay);
  spentOverlays.update(0.82, null, false);
  const [dissipation] = visibleNodes(
    spentOverlays,
    (node) => node.userData.cue === 'projectile-terminal',
  );
  assert.ok(dissipation);
  assert.equal(dissipation.userData.terminalKind, 'dissipation');
  assert.equal(dissipation.userData.terminalCue, 'fork-burst');
  assert.equal(
    visibleNodes(
      spentOverlays,
      (node) => node.userData.cue === 'projectile-dissipation',
    ).length,
    0,
    'an opted-in look does not also receive the generic spent ring',
  );
  spentOverlays.dispose();

  const guardReplay = terminalReplay({
    kind: 'actor-contact',
    targetActorId: { teamId: 1, unitId: 0, lifeId: 0 },
    appliedDamage: false,
  });
  const guardOverlays = buildOverlays(guardReplay);
  guardOverlays.update(0.82, null, false);
  assert.equal(
    visibleNodes(
      guardOverlays,
      (node) => node.userData.cue === 'guard-absorb-contact',
    ).length,
    1,
  );
  assert.equal(
    visibleNodes(
      guardOverlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' ||
        node.userData.kind === 'impact',
    ).length,
    0,
    'a guarded stop is neither a class hit burst nor a damage wave',
  );
  guardOverlays.dispose();

  const deflectedReplay = terminalReplay(
    {
      kind: 'actor-contact',
      targetActorId: { teamId: 1, unitId: 0, lifeId: 0 },
      appliedDamage: false,
    },
    'projectile-deflected',
  );
  const deflectedOverlays = buildOverlays(deflectedReplay);
  deflectedOverlays.update(0.82, null, false);
  assert.ok(
    visibleNodes(
      deflectedOverlays,
      (node) => node.userData.terminalKind === 'deflected',
    ).length >= 1,
  );
  assert.equal(
    visibleNodes(
      deflectedOverlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' ||
        node.userData.kind === 'impact',
    ).length,
    0,
  );
  deflectedOverlays.dispose();
});

test('destruction amplifies the look signature instead of replaying an ordinary hit', () => {
  const contact = {
    kind: 'actor-contact',
    targetActorId: { teamId: 1, unitId: 0, lifeId: 0 },
    appliedDamage: true,
  } as const;
  const damageOverlays = buildOverlays(
    terminalReplay(contact, 'damage'),
  );
  const destructionOverlays = buildOverlays(
    terminalReplay(contact, 'destruction'),
  );
  damageOverlays.update(0.82, null, false);
  destructionOverlays.update(0.82, null, false);
  const [damage] = visibleNodes(
    damageOverlays,
    (node) => node.userData.cue === 'projectile-terminal',
  );
  const [destruction] = visibleNodes(
    destructionOverlays,
    (node) => node.userData.cue === 'projectile-terminal',
  );
  assert.ok(damage);
  assert.ok(destruction);
  const damageRing = damage.children.find(
    (child) => child.userData.part === 'terminal-ring',
  )!;
  const destructionRing = destruction.children.find(
    (child) => child.userData.part === 'terminal-ring',
  )!;
  assert.equal(damage.userData.terminalKind, 'damage-hit');
  assert.equal(destruction.userData.terminalKind, 'destruction');
  assert.ok(
    destructionRing.scale.x > damageRing.scale.x,
    'a kill opens the signature farther than a nonlethal hit',
  );
  damageOverlays.dispose();
  destructionOverlays.dispose();
});

test('terminal cues obey selected-life fog without changing their replay result', () => {
  const replay = terminalReplay(
    {
      kind: 'actor-contact',
      targetActorId: { teamId: 1, unitId: 0, lifeId: 0 },
      appliedDamage: true,
    },
    'damage',
  );
  const terminal = projectileTerminalsInTick(replay, 0)[0]!;
  const selected = 'generic:1:unit:0' as ReplayStableUnitKey;
  const turn = replay.ticks[0]!.actorTurns.find(
    (candidate) => candidate.actor.unitKey === selected,
  )!;
  turn.observation.visibleTiles = turn.observation.visibleTiles.filter(
    ({ position }) =>
      position.x !== terminal.position.x ||
      position.y !== terminal.position.y,
  );

  const overlays = buildOverlays(replay);
  overlays.update(0.82, selected, true);
  assert.equal(
    visibleNodes(
      overlays,
      (node) =>
        node.userData.cue === 'projectile-terminal' ||
        node.userData.kind === 'impact' ||
        (node.userData.cue === 'event-flash' &&
          node.userData.eventType === 'damage'),
    ).length,
    0,
  );
  assert.equal(
    projectileTerminalsInTick(replay, 0)[0]?.kind,
    'damage-hit',
    'fog hides presentation only; it does not reinterpret the terminal',
  );
  assert.deepEqual(
    overlays.shake(0.82),
    { x: 0, y: 0 },
    'an unseen terminal cannot reveal itself through camera knock',
  );
  overlays.dispose();
});
