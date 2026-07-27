import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  bridgeSupportsReplay,
  errorBridgeMessage,
  hostedBridgeVersion,
  loadedBridgeMessage,
  readyBridgeMessage,
  replayBridgeMessage,
  selectedBridgeMessage,
  tickBridgeMessage,
} from '../src/hostedBridge.ts';
import { loadReplayJson, loadReplayObject } from '../src/replayIngress.ts';
import type { TickPresentation } from '../src/replayPresentation.ts';
import { replayV1FixtureInput } from './support/replayFixtureInputs.ts';

const replayV1 = loadReplayObject(replayV1FixtureInput()).replay;
const replayV2 = loadReplayJson(
  readFileSync(
    new URL('./fixtures/frontline-replay-v2.json', import.meta.url),
    'utf8',
  ),
).replay;
const replayV3 = loadReplayJson(
  readFileSync(
    new URL(
      '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
      import.meta.url,
    ),
    'utf8',
  ),
).replay;

test('no bridge query retains bridge-v1 message and slot compatibility', () => {
  assert.equal(hostedBridgeVersion('?standalone'), 1);
  assert.deepEqual(readyBridgeMessage(1), { type: 'ready' });
  assert.deepEqual(loadedBridgeMessage(1), { type: 'loaded' });

  const replayMessage = replayBridgeMessage(1, replayV1, 1, 3);
  assert.equal('bridgeVersion' in replayMessage, false);
  assert.deepEqual(
    replayMessage.header.participants.map((participant) => participant.slot),
    [3, 9],
  );

  const tickMessage = tickBridgeMessage(1, legacyPresentation());
  assert.deepEqual(tickMessage, {
    type: 'tick',
    tick: 0,
    control: null,
    bots: [
      {
        slot: 3,
        name: 'third',
        accent: '#ffffff',
        lookLabel: 'Vanguard',
        runtimeKind: 'wasm',
        status: 'Active',
        health: 3,
        maxHealth: 3,
        cooldown: 0,
        zoneTicks: null,
        holdingZone: false,
        action: 'MoveForward',
        actionResult: 'Success',
        visibleTiles: 1,
        visibleEnemies: [],
      },
    ],
  });
  assert.deepEqual(
    selectedBridgeMessage(1, replayV1, replayV1.units[0]!.unitKey),
    { type: 'selected', slot: 3 },
  );
});

test('bridge-v2 publishes stable units, teams, and team results', () => {
  assert.equal(hostedBridgeVersion('?standalone&bridge=2'), 2);
  assert.deepEqual(readyBridgeMessage(2), {
    type: 'ready',
    bridgeVersion: 2,
  });
  const message = replayBridgeMessage(2, replayV2, 4, 5);

  assert.equal(message.bridgeVersion, 2);
  assert.deepEqual(
    message.header.units.map((unit) => unit.unitKey),
    [
      'frontline:0:unit:0',
      'frontline:0:unit:1',
      'frontline:0:unit:2',
      'frontline:1:unit:0',
      'frontline:1:unit:1',
      'frontline:1:unit:2',
    ],
  );
  assert.deepEqual(
    message.result?.teams.map((team) => team.teamId),
    [0, 1],
  );
  assert.ok(
    message.result?.teams.every(
      (team) =>
        'activeHealth' in team &&
        team.units.every(
          (unit) =>
            unit.unitKey ===
            `frontline:${unit.teamId}:unit:${unit.unitId}`,
      ),
    ),
  );
  assert.deepEqual(message.result?.teams[0], {
    teamId: 0,
    outcome: 'draw',
    activeHealth: 8,
    damageDealt: '0',
    units: [
      {
        unitKey: 'frontline:0:unit:0',
        teamId: 0,
        unitId: 0,
        defaultFormId: 'prime-mobile',
        formId: 'prime-mobile',
        pendingFormTransition: null,
        lifecycleStatus: 'active',
        activeActorKey: 'frontline:0:unit:0:life:0',
        health: 3,
        damageDealt: '0',
      },
      {
        unitKey: 'frontline:0:unit:1',
        teamId: 0,
        unitId: 1,
        defaultFormId: 'child-mobile',
        formId: 'turret',
        pendingFormTransition: null,
        lifecycleStatus: 'active',
        activeActorKey: 'frontline:0:unit:1:life:0',
        health: 5,
        damageDealt: '0',
      },
      {
        unitKey: 'frontline:0:unit:2',
        teamId: 0,
        unitId: 2,
        defaultFormId: 'child-mobile',
        formId: 'child-mobile',
        pendingFormTransition: null,
        lifecycleStatus: 'ready',
        activeActorKey: null,
        health: 0,
        damageDealt: '0',
      },
    ],
  });
  assert.deepEqual(
    selectedBridgeMessage(2, replayV2, replayV2.units[0]!.unitKey),
    {
      type: 'selected',
      bridgeVersion: 2,
      unitKey: 'frontline:0:unit:0',
    },
  );
});

test('bridge-v1 rejects replay-v2 with only the stable error envelope', () => {
  assert.equal(bridgeSupportsReplay(1, replayV2), false);
  assert.deepEqual(replayBridgeMessage(1, replayV2, 4, 5), {
    type: 'error',
    reason: 'unsupported-replay-version',
  });
  assert.deepEqual(
    errorBridgeMessage(1, 'unsupported-replay-version'),
    { type: 'error', reason: 'unsupported-replay-version' },
  );
});

test('bridge-v3 carries generic source identity, mode, scores, and tick state', () => {
  assert.equal(hostedBridgeVersion('?standalone&bridge=3'), 3);
  assert.deepEqual(readyBridgeMessage(3), {
    type: 'ready',
    bridgeVersion: 3,
  });
  assert.equal(bridgeSupportsReplay(2, replayV3), false);
  assert.equal(bridgeSupportsReplay(3, replayV3), true);

  const message = replayBridgeMessage(3, replayV3, 2, 3);
  assert.equal(message.bridgeVersion, 3);
  assert.deepEqual(message.header.mode, {
    kind: 'deathmatch',
    id: 'deathmatch',
  });
  assert.deepEqual(
    message.header.units.map((unit) => unit.unitKey),
    ['generic:0:unit:0', 'generic:1:unit:0'],
  );
  assert.deepEqual(message.result?.teams[0]?.scores, [
    { channel: 'kills', value: '0' },
    { channel: 'deaths', value: '0' },
    { channel: 'damage-dealt', value: '1' },
    { channel: 'active-health', value: '2' },
  ]);
  assert.deepEqual(message.result?.mode, replayV3.result?.mode);

  const tick = tickBridgeMessage(
    3,
    legacyPresentation(),
    replayV3.initialWorld ?? undefined,
  );
  assert.equal(tick.bridgeVersion, 3);
  assert.deepEqual(tick.mode, {
    kind: 'deathmatch',
    modeId: 'deathmatch',
  });
  assert.deepEqual(tick.scoreboard?.teams[0]?.scores[0], {
    channel: 'kills',
    value: '0',
  });
});

function legacyPresentation(): TickPresentation {
  return {
    tick: 0,
    objective: null,
    units: [
      {
        unitKey: 'duel:3:unit:0',
        actorKey: 'duel:3:unit:0:life:0',
        teamId: 3,
        unitId: 0,
        lifeId: 0,
        participantId: 3,
        legacySlot: 3,
        name: 'third',
        accent: '#ffffff',
        lookLabel: 'Vanguard',
        runtimeKind: 'wasm',
        formId: 'legacy-mobile',
        canMove: true,
        omnidirectionalVision: false,
        omnidirectionalShooting: false,
        status: 'active',
        respawnAtTick: null,
        unlockAtTick: null,
        rebuildReadyAtTick: null,
        fabricationAtTick: null,
        reservedSpawn: null,
        pendingSpawnReason: null,
        pendingFormTransition: null,
        health: 3,
        maxHealth: 3,
        cooldown: 0,
        energy: null,
        zoneTicks: null,
        holdingObjective: false,
        actionId: 'move-forward',
        actionLaunchHeading: null,
        actionResult: 'success',
        debug: null,
        visibleTiles: 1,
        visibleEnemies: [],
      },
    ],
  };
}
