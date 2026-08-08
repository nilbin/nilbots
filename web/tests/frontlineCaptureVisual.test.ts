import assert from 'node:assert/strict';
import test from 'node:test';
import { frontlineCaptureVisual } from '../src/render/frontlineCaptureVisual.ts';
import type {
  FrontlineControlPresentation,
  TickPresentation,
  UnitPresentation,
} from '../src/replayPresentation.ts';

test('Frontline capture visual states keep incumbent credit distinct from challenger erosion', () => {
  assert.deepEqual(reading().state, 'neutral');

  const building = reading({
    claimantTeamId: 0,
    captureProgress: 4,
    captureTeamId: 0,
  });
  assert.equal(building.state, 'building');
  assert.equal(building.progressDirection, 'building');
  assert.equal(building.progressFraction, 0.4);
  assert.equal(building.claimantAccent, '#22d3ee');
  assert.equal(building.challengerAccent, null);

  const erosion = reading({
    claimantTeamId: 0,
    captureProgress: 4,
    captureTeamId: 1,
  });
  assert.equal(erosion.state, 'eroding');
  assert.equal(erosion.progressDirection, 'eroding');
  assert.equal(erosion.claimantTeamId, 0);
  assert.equal(erosion.captureTeamId, 1);
  assert.equal(erosion.challengerTeamId, 1);
  assert.equal(erosion.claimantAccent, '#22d3ee');
  assert.equal(erosion.challengerAccent, '#fb7185');

  const contested = reading({
    claimantTeamId: 0,
    captureProgress: 4,
    captureContested: true,
  });
  assert.equal(contested.state, 'contested');
  assert.equal(contested.progressDirection, 'frozen');
  assert.equal(contested.progressFraction, 0.4);

  const paused = reading({
    claimantTeamId: 0,
    captureProgress: 4,
    captureTeamId: 0,
    capturePaused: true,
  });
  assert.equal(paused.progressDirection, 'frozen');
  assert.equal(paused.capturePaused, true);
});

test('Frontline ratchet visual uses exact owner and distinguishes early from late hold', () => {
  const early = reading({
    activePositionIndex: 3,
    holdOwnerTeamId: 1,
    holdEndsAtTick: 141,
    holdRemainingTicks: 40,
    holdDurationTicks: 40,
  });
  assert.equal(early.state, 'holding');
  assert.equal(early.holdOwnerTeamId, 1);
  assert.equal(early.holdEndsAtTick, 141);
  assert.equal(early.holdAccent, '#fb7185');
  assert.equal(early.holdFraction, 1);

  const late = reading({
    activePositionIndex: 3,
    holdOwnerTeamId: 1,
    holdEndsAtTick: 141,
    holdRemainingTicks: 5,
    holdDurationTicks: 40,
  });
  assert.equal(late.state, 'holding');
  assert.equal(late.holdRemainingTicks, 5);
  assert.equal(late.holdFraction, 0.125);
});

function reading({
  activePositionIndex = 2,
  claimantTeamId = null,
  captureProgress = 0,
  captureTeamId = null,
  captureContested = false,
  capturePaused = false,
  holdOwnerTeamId = null,
  holdEndsAtTick = null,
  holdRemainingTicks = null,
  holdDurationTicks = null,
}: {
  activePositionIndex?: number;
  claimantTeamId?: number | null;
  captureProgress?: number;
  captureTeamId?: number | null;
  captureContested?: boolean;
  capturePaused?: boolean;
  holdOwnerTeamId?: number | null;
  holdEndsAtTick?: number | null;
  holdRemainingTicks?: number | null;
  holdDurationTicks?: number | null;
} = {}) {
  const presentation: TickPresentation = {
    tick: 100,
    objective: {
      kind: 'frontline',
      activePositionIndex,
      positionCount: 5,
      claimingTeamId: claimantTeamId,
      captureProgress,
      captureThreshold: 10,
      controlResumesAtTick: 0,
      captureTeamId,
      captureContested,
      capturePaused,
      holdOwnerTeamId,
      holdEndsAtTick,
      holdRemainingTicks,
      holdDurationTicks,
      winnerTeamId: null,
      phase: '',
    } satisfies FrontlineControlPresentation,
    units: [
      unit(0, '#22d3ee'),
      unit(1, '#fb7185'),
    ],
  };
  const visual = frontlineCaptureVisual(presentation);
  assert.ok(visual);
  return visual;
}

function unit(
  teamId: number,
  accent: string,
): UnitPresentation {
  return {
    unitKey: `frontline:${teamId}:unit:0`,
    actorKey: `frontline:${teamId}:unit:0:life:0`,
    teamId,
    unitId: 0,
    lifeId: 0,
    participantId: teamId,
    legacySlot: null,
    name: `team-${teamId}`,
    accent,
    lookLabel: 'fixture',
    runtimeKind: 'in-process',
    formId: 'mobile',
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
    actionId: null,
    actionLaunchHeading: null,
    actionResult: null,
    debug: null,
    visibleTiles: 0,
    visibleEnemies: [],
  };
}
