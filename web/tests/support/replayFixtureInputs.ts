import type {
  ReplayV1Document,
  ReplayV1PartialDocument,
} from '../../src/replayWireV1.ts';
import type {
  ReplayV2ActorId,
  ReplayV2CompleteDocument,
  ReplayV2ControlState,
  ReplayV2LifeState,
  ReplayV2WorldState,
} from '../../src/replayWireV2.ts';

export const JS_UNSAFE_DECIMAL = '9007199254740993';

export function replayV1FixtureInput(): ReplayV1Document {
  return {
    header: {
      replayVersion: 1,
      engineVersion: 'test',
      gameRulesVersion: 'test',
      runtimeProtocolVersion: '0.1',
      runtimeConfigurationVersion: '0.1',
      mapId: 'test-map',
      mapVersion: 1,
      mapWidth: 3,
      mapHeight: 3,
      mapTiles: ['...', '...', '...'],
      seed: 7,
      maxTicks: 10,
      maxHealth: 3,
      visionRange: 4,
      participants: [
        participantV1(9, 'ninth', 2),
        participantV1(3, 'third', 0),
      ],
    },
    ticks: [
      {
        tick: 0,
        bots: [botTurnV1(9), botTurnV1(3)],
        events: [],
        state: [
          stateV1(9, 2),
          stateV1(3, 0),
        ],
      },
    ],
    result: {
      reason: 'MaxTicks',
      endTick: 1,
      bots: [
        resultV1(9),
        resultV1(3),
      ],
    },
    replayHash: '0'.repeat(64),
  };
}

/** Exact JSON shape emitted by MatchesEndpoints during a replay-v1 broadcast. */
export function replayV1LivePartialFixtureInput(): ReplayV1PartialDocument {
  const complete = replayV1FixtureInput();
  return {
    header: structuredClone(complete.header),
    ticks: structuredClone(complete.ticks),
    partial: true,
  };
}

function participantV1(slot: number, name: string, x: number) {
  return {
    slot,
    name,
    runtimeKind: 'wasm',
    artifactHash: `hash-${name}`,
    accent: '#ffffff',
    spawnX: x,
    spawnY: 1,
    spawnFacing: 'East' as const,
  };
}

function botTurnV1(slot: number) {
  return {
    slot,
    chosenAction: 'Wait' as const,
    validatedAction: 'Wait' as const,
    result: 'Success' as const,
    faulted: false,
    visibleTiles: [[0, 0] as [number, number]],
    visibleEnemies: [],
  };
}

function stateV1(slot: number, x: number) {
  return {
    slot,
    x,
    y: 1,
    facing: 'East' as const,
    health: 3,
    cooldown: 0,
    status: 'Active' as const,
  };
}

function resultV1(slot: number) {
  return {
    slot,
    outcome: 'Draw' as const,
    finalHealth: 3,
    damageDealt: 0,
    faults: 0,
    finalStatus: 'Active' as const,
  };
}

export function replayV2FixtureInput(): ReplayV2CompleteDocument {
  const beforeActor: ReplayV2ActorId = { teamId: 0, unitId: 0, lifeId: 0 };
  const otherActor: ReplayV2ActorId = { teamId: 1, unitId: 0, lifeId: 0 };
  const afterActor = beforeActor;
  const actors = [beforeActor, otherActor];
  const before = worldV2(actors, 0);
  const after = worldV2(actors, 1);

  return {
    header: {
      replayVersion: 2,
      engineVersion: 'test',
      gameRulesVersion: 'frontline-test',
      actorRuntime: {
        family: 'nilbots-actor',
        version: 1,
        matchStartSchemaVersion: 1,
        observationSchemaVersion: 1,
        decisionSchemaVersion: 1,
      },
      seed: JS_UNSAFE_DECIMAL,
      contract: {
        schemaVersion: 1,
        matchContractFingerprint: 'contract-fingerprint',
        rules: {
          schemaVersion: 1,
          rulesetId: 'frontline-test',
          rulesFingerprint: 'rules-fingerprint',
          limits: {
            maxTicks: 10,
            faultLimit: 0,
            teamCount: 2,
            participantCount: 2,
            unitSlotCount: 2,
            initialUnitsPerTeam: 1,
            maxUnitsPerTeam: 1,
            destructionEndsMatch: false,
            respawnsEnabled: true,
          },
          objective: {
            mode: 'frontline',
            zoneControlEnabled: false,
            zoneDominationTicks: 0,
            zoneExclusiveAccrual: false,
            sharedPressureEnabled: false,
            controlBySoleOccupancy: true,
            controlPressureLimit: 0,
            controlPressureGain: 0,
            controlPressureDecayInterval: 0,
            overtime: {
              startTick: 0,
              pressureLimit: 0,
              pressureGain: 0,
              stopsDecay: false,
            },
            maxTickTiebreakers: ['objective', 'health', 'damage-dealt'],
          },
          frontlineDefinition: {
            teamCount: 2,
            participantsPerTeam: 1,
            frontlinePositionCount: 1,
            initialUnitsPerTeam: 1,
            maxUnitsPerTeam: 1,
            teamPerception: 'immediate-union',
            capture: {
              threshold: 3,
              gainPerSoleTeamTick: 1,
              decayAmount: 1,
              decayIntervalTicks: 1,
              redeployPauseTicks: 1,
              pushesToBreach: 1,
              presence: 'binary-positive-weight-per-team-no-stacking',
              nonSolePresence: 'decay-existing-claim',
              counterCapture: 'erode-to-neutral-before-claim',
            },
            victory: {
              initialPosition: 'centre-position-index',
              teamAdvances: [
                { teamId: 0, positionIndexDelta: 1 },
                { teamId: 1, positionIndexDelta: -1 },
              ],
              completionPrecedence: 'base-breach-before-max-ticks',
              timeoutResolution:
                'signed-position-threshold-plus-claim-zero-draw-no-tiebreakers',
            },
            lifecycle: {
              primeRespawnTicks: 2,
              childRebuildTicks: 3,
              fabricationUnlockTicks: [],
            },
            deployment: {
              primeDefaultFormId: 'prime',
              childDefaultFormId: 'child',
              destructionTransitionClock:
                'tick-start-at-destroyed-tick-plus-one-plus-delay',
              primeReturn: 'automatic-at-authored-prime-spawn',
              childReturn: 'ready-then-explicit-fabrication',
              newLife:
                'fresh-runtime-form-defaults-home-facing-can-act-on-creation-tick',
              primeSpawnReservation: 'permanent-against-own-children',
              protectedPad:
                'enemy-ground-entry-blocked-no-damage-immunity-no-projectile-blocking',
            },
            fabrication: {
              enabled: false,
              actionId: 'fabricate',
              fabricatorUnitId: 0,
              fabricatorFormId: 'prime',
              targetPolicy: 'own-ready-child-slot',
              activationRegion: 'own-protected-spawn-pad',
              consumesTick: true,
              spawnDelayTicks: 1,
              capacityEvaluation:
                'post-movement-during-queue-fabrications',
              spawnRegion:
                'own-protected-spawn-pad-excluding-prime-spawn',
              spawnSelection:
                'first-unoccupied-unreserved-canonical-y-x',
              spawnFacing: 'own-prime-spawn-facing',
              unavailableSpawnResult: 'blocked',
              requiresExplicitRefabricationAfterRebuild: true,
            },
            anchor: {
              windupTicks: 2,
              healthGain: 2,
              irreversibleForLife: true,
            },
            alliedCombat: {
              friendlyFireEnabled: false,
              alliedProjectilesBlock: false,
              projectileAttribution:
                'exact-firing-life-persists-credits-stable-unit-by-actual-health-removed',
            },
          },
          energy: {
            enabled: false,
            maxEnergy: 0,
            shotEnergyCost: 0,
            regenerationIntervalTicks: 0,
            regenerationAmount: 0,
          },
          forms: [
            {
              id: 'child',
              maxHealth: 3,
              visionRange: 6,
              shootCooldownTicks: 2,
              omnidirectionalVision: false,
              omnidirectionalShooting: false,
              movementLayer: 'ground',
              objectiveWeight: 1,
              canMove: true,
              canShoot: true,
              allowsProgrammedShots: true,
              allowedActionIds: ['wait'],
            },
            {
              id: 'prime',
              maxHealth: 5,
              visionRange: 6,
              shootCooldownTicks: 2,
              omnidirectionalVision: false,
              omnidirectionalShooting: false,
              movementLayer: 'ground',
              objectiveWeight: 1,
              canMove: true,
              canShoot: true,
              allowsProgrammedShots: true,
              allowedActionIds: ['wait'],
            },
          ],
          actions: [
            {
              id: 'wait',
              code: 0,
              kind: 'wait',
              parameterKinds: [],
              enabled: true,
            },
          ],
          projectiles: {
            mode: 'discrete',
            damagePerHit: 1,
            maxTravelTiles: 5,
            shootCooldownTicks: 2,
            ticksPerAdvance: 1,
            tilesPerAdvance: 1,
            launchTiles: 1,
            advancesOnLaunchTick: true,
            damageAppliedSimultaneously: true,
          },
          shotPrograms: {
            enabled: true,
            headingSectors: 8,
            bendStepOctants: 1,
            minInitialAimOctants: -1,
            maxInitialAimOctants: 1,
            aimOnlyProgram: {
              bendDirection: 0,
              bendAfterTiles: 0,
              bendEveryTiles: 0,
              bendCount: 0,
            },
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: 3,
            minBendEveryTiles: 1,
            maxBendEveryTiles: 3,
            minBendCount: 0,
            maxBendCount: 2,
            launchTiles: 1,
            payloadOptional: true,
            defaultProgram: {
              initialAimOffset: 0,
              bendDirection: 0,
              bendAfterTiles: 0,
              bendEveryTiles: 0,
              bendCount: 0,
            },
            invalidPayloadResult: null,
            unsupportedPayloadResult: 'blocked',
            diagonalCornersMustBeClear: true,
          },
          vision: {
            range: 6,
            distanceMetric: 'chebyshev',
            shape: 'facing-quadrant',
            omnidirectionalProximityRange: 1,
            lineOfSight: 'corner-strict-supercover',
            hearingRadius: 4,
            hearingBearingSectors: 8,
            hearingDistanceBandUpperBounds: [1, 3, 4],
            loudEventTypes: ['shot'],
          },
          collisions: {
            unitsBlockWalls: true,
            unitsBlockUnits: true,
            sameDestinationMovesBlockAll: true,
            swapMovesBlocked: true,
            followingVacatedUnitAllowed: false,
            projectilesBlockMovement: false,
            movingOntoProjectileCausesHit: true,
            wallsConsumeProjectiles: true,
            projectilesIgnoreOwner: true,
            projectilesStopOnFirstNonOwnerUnit: true,
            projectilesCollideWithProjectiles: false,
          },
          tickResolution: {
            observationsUsePreTickState: true,
            decisionsResolveAsJointStep: true,
            phases: [
              'queue-fabrications',
              'freeze-observations',
              'resolve-match-completion',
            ],
          },
        },
        map: {
          schemaVersion: 1,
          mapId: 'frontline-test-map',
          mapVersion: 1,
          mapFingerprint: 'map-fingerprint',
          formatVersion: 2,
          width: 3,
          height: 3,
          tileRows: ['...', '...', '...'],
          spawns: [
            { teamId: 0, x: 0, y: 1, facing: 'east' },
            { teamId: 1, x: 2, y: 1, facing: 'west' },
          ],
          objectiveTiles: [],
          frontline: {
            positions: [{ positionIndex: 0, tiles: [[1, 1]] }],
            teamHomes: [
              {
                teamId: 0,
                primeSpawn: { x: 0, y: 1, facing: 'east' },
                protectedSpawnPad: [[0, 1]],
              },
              {
                teamId: 1,
                primeSpawn: { x: 2, y: 1, facing: 'west' },
                protectedSpawnPad: [[2, 1]],
              },
            ],
            anchorForbiddenTiles: [],
          },
        },
        topology: {
          teamCount: 2,
          participantCount: 2,
          unitSlotCount: 2,
          initialLifeCount: 2,
          teams: [{ teamId: 0 }, { teamId: 1 }],
          participants: [
            { participantId: 0, teamId: 0 },
            { participantId: 1, teamId: 1 },
          ],
          unitSlots: [
            { teamId: 0, unitId: 0, controllerParticipantId: 0 },
            { teamId: 1, unitId: 0, controllerParticipantId: 1 },
          ],
          initialLives: [
            { teamId: 0, unitId: 0, lifeId: 0, formId: 'prime' },
            { teamId: 1, unitId: 0, lifeId: 0, formId: 'prime' },
          ],
        },
      },
      presentation: null,
      participants: [
        {
          participantId: 0,
          teamId: 0,
          name: 'alpha',
          runtimeKind: 'test',
          artifactHash: 'artifact',
          accent: '#ffffff',
          lookId: null,
          projectileLookId: null,
        },
        {
          participantId: 1,
          teamId: 1,
          name: 'beta',
          runtimeKind: 'test',
          artifactHash: 'artifact-beta',
          accent: '#000000',
          lookId: null,
          projectileLookId: null,
        },
      ],
    },
    ticks: [
      {
        tick: 0,
        tickStart: {
          state: before,
          activeActors: actors,
          lifecycleEvents: [],
        },
        actors: [
          actorTurnV2(beforeActor, 0),
          actorTurnV2(otherActor, 1),
        ],
        resolution: {
          events: [],
          projectileTraversals: [
            {
              projectileId: JS_UNSAFE_DECIMAL,
              ownerActorId: beforeActor,
              launchDirection: 'east',
              from: { x: 1, y: 1 },
              path: [],
              heading: null,
              shotProgram: null,
              programmedPath: null,
            },
          ],
        },
        postState: after,
      },
    ],
    result: {
      winnerTeamId: null,
      reason: 'max-ticks',
      endTick: 0,
      territorialScore: `-${JS_UNSAFE_DECIMAL}`,
      objective: controlV2(1),
      teams: [
        {
          teamId: 0,
          outcome: 'draw',
          activeHealth: 5,
          damageDealt: JS_UNSAFE_DECIMAL,
          units: [
            {
              teamId: 0,
              unitId: 0,
              formId: 'prime',
              lifecycleStatus: 'active',
              activeActorId: afterActor,
              health: 5,
              damageDealt: JS_UNSAFE_DECIMAL,
            },
          ],
        },
        {
          teamId: 1,
          outcome: 'draw',
          activeHealth: 5,
          damageDealt: JS_UNSAFE_DECIMAL,
          units: [
            {
              teamId: 1,
              unitId: 0,
              formId: 'prime',
              lifecycleStatus: 'active',
              activeActorId: otherActor,
              health: 5,
              damageDealt: JS_UNSAFE_DECIMAL,
            },
          ],
        },
      ],
    },
    replayHash: '0'.repeat(64),
    partial: false,
  };
}

function actorTurnV2(actorId: ReplayV2ActorId, participantId: number) {
  return {
    actorId,
    lifeStart: {
      schemaVersion: 1,
      runtimeContractVersion: 1,
      actorId,
      participantId,
      actorRandomSeed: JS_UNSAFE_DECIMAL,
      spawnReason: 'initial' as const,
      matchContractFingerprint: 'contract-fingerprint',
    },
    observation: {
      schemaVersion: 1,
      tick: 0,
      matchContractFingerprint: 'contract-fingerprint',
      teamPerception: 'immediate-union' as const,
      self: observedSelf(actorId),
      teamUnits: [
        {
          teamId: actorId.teamId,
          unitId: 0,
          formId: 'prime',
          lifecycleStatus: 'active' as const,
          activeActorId: actorId,
          respawnAtTick: null,
          unlockAtTick: null,
          rebuildReadyAtTick: null,
          fabricationAtTick: null,
        },
      ],
      allies: [],
      enemies: [],
      visibleTiles: [],
      visibleProjectiles: null,
      visibleEvents: [],
      heardSounds: [],
      frontlineObjective: {
        activePositionIndex: 0,
        claimingTeamId: null,
        captureProgress: 0,
        decayTicksElapsed: 0,
        controlResumesAtTick: 0,
      },
      actions: [
        {
          actionId: 'wait',
          actionCode: 0,
          parameterKinds: [],
          enabled: true,
          available: true,
          shotProgramAvailable: null,
          allowedDirections: null,
          allowedUnitTargets: [],
          allowedFormTargets: null,
        },
      ],
    },
    aliases: {
      enemyLives: [],
      projectiles: [],
      events: [],
    },
    runtimeReply: {
      actionId: 'wait',
      actionCode: 0,
      payload: null,
      debugMessage: null,
      faulted: false,
      faultMessage: null,
    },
    acceptedDecision: {
      actionId: 'wait',
      actionCode: 0,
      payload: null,
      debugMessage: null,
      faulted: false,
      faultMessage: null,
    },
    actionResolution: {
      actorId,
      chosenActionId: 'wait',
      chosenActionCode: 0,
      chosenPayload: null,
      validatedActionId: 'wait',
      validatedActionCode: 0,
      validatedPayload: null,
      result: 'success' as const,
    },
  };
}

function observedSelf(actorId: ReplayV2ActorId) {
  return {
    actorId,
    formId: 'prime',
    position: actorPosition(actorId.teamId, 0),
    facing: actorId.teamId === 0 ? ('east' as const) : ('west' as const),
    health: 5,
    cooldown: 0,
    energy: null,
    previousActionResult: 'none' as const,
  };
}

function worldV2(
  actorIds: ReplayV2ActorId[],
  nextTick: number,
): ReplayV2WorldState {
  return {
    teams: actorIds.map((actorId) => {
      const life: ReplayV2LifeState = {
        actorId,
        position: actorPosition(actorId.teamId, nextTick),
        facing: actorId.teamId === 0 ? 'east' : 'west',
        health: 5,
        cooldown: 0,
        energy: null,
        damageDealt: JS_UNSAFE_DECIMAL,
        previousActionResult: nextTick === 0 ? 'none' : 'success',
        spawnedAtTick: 0,
      };
      return {
        teamId: actorId.teamId,
        damageDealt: JS_UNSAFE_DECIMAL,
        units: [
          {
            teamId: actorId.teamId,
            unitId: 0,
            formId: 'prime',
            lifecycleStatus: 'active',
            respawnAtTick: null,
            unlockAtTick: null,
            rebuildReadyAtTick: null,
            fabricationAtTick: null,
            reservedSpawn: null,
            pendingSpawnReason: null,
            hasSpawned: true,
            nextLifeId: actorId.lifeId + 1,
            damageDealt: JS_UNSAFE_DECIMAL,
            activeLife: life,
          },
        ],
      };
    }),
    projectiles: [
      {
        projectileId: JS_UNSAFE_DECIMAL,
        ownerActorId: actorIds[0]!,
        position: { x: 1, y: 1 },
        launchDirection: 'east',
        heading: null,
        shotProgram: null,
        programmedPath: null,
        nextProgrammedPathIndex: 0,
        tilesTraveled: 0,
        phase: 0,
      },
    ],
    objective: controlV2(nextTick),
  };
}

function actorPosition(teamId: number, nextTick: number) {
  return { x: teamId === 0 ? nextTick : 2, y: 1 };
}

function controlV2(nextTick: number): ReplayV2ControlState {
  return {
    nextTick,
    activePositionIndex: 0,
    claimingTeamId: null,
    captureProgress: 0,
    decayTicksElapsed: 0,
    controlResumesAtTick: 0,
    winnerTeamId: null,
  };
}

export function replayV2ZeroTickPartialFixtureInput() {
  const complete = replayV2FixtureInput();
  return {
    header: structuredClone(complete.header),
    ticks: [],
    result: null,
    replayHash: null,
    partial: true,
  } as const;
}
