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
  const afterActor: ReplayV2ActorId = { teamId: 0, unitId: 0, lifeId: 1 };
  const before = worldV2(beforeActor, 0);
  const after = worldV2(afterActor, 1);

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
            teamCount: 1,
            participantCount: 1,
            unitSlotCount: 1,
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
            teamCount: 1,
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
            },
            lifecycle: {
              primeRespawnTicks: 2,
              childRebuildTicks: 3,
              fabricationUnlockTicks: [],
            },
            anchor: {
              windupTicks: 2,
              healthGain: 2,
              irreversibleForLife: true,
            },
            alliedCombat: {
              friendlyFireEnabled: false,
              alliedProjectilesBlock: false,
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
            phases: ['freeze-observations', 'resolve-match-completion'],
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
          spawns: [{ teamId: 0, x: 0, y: 1, facing: 'east' }],
          objectiveTiles: [],
          frontline: {
            positions: [{ positionIndex: 0, tiles: [[1, 1]] }],
            teamHomes: [
              {
                teamId: 0,
                primeSpawn: { x: 0, y: 1, facing: 'east' },
                protectedSpawnPad: [[0, 1]],
              },
            ],
            anchorForbiddenTiles: [],
          },
        },
        topology: {
          teamCount: 1,
          participantCount: 1,
          unitSlotCount: 1,
          initialLifeCount: 1,
          teams: [{ teamId: 0 }],
          participants: [{ participantId: 0, teamId: 0 }],
          unitSlots: [
            { teamId: 0, unitId: 0, controllerParticipantId: 0 },
          ],
          initialLives: [
            { teamId: 0, unitId: 0, lifeId: 0, formId: 'prime' },
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
      ],
    },
    ticks: [
      {
        tick: 0,
        tickStart: {
          state: before,
          activeActors: [beforeActor],
          lifecycleEvents: [],
        },
        actors: [
          {
            actorId: beforeActor,
            lifeStart: {
              schemaVersion: 1,
              runtimeContractVersion: 1,
              actorId: beforeActor,
              participantId: 0,
              actorRandomSeed: JS_UNSAFE_DECIMAL,
              spawnReason: 'initial',
              matchContractFingerprint: 'contract-fingerprint',
            },
            observation: {
              schemaVersion: 1,
              tick: 0,
              matchContractFingerprint: 'contract-fingerprint',
              teamPerception: 'immediate-union',
              self: observedSelf(beforeActor),
              teamUnits: [
                {
                  teamId: 0,
                  unitId: 0,
                  formId: 'prime',
                  lifecycleStatus: 'active',
                  activeActorId: beforeActor,
                  respawnAtTick: null,
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
              actorId: beforeActor,
              chosenActionId: 'wait',
              chosenActionCode: 0,
              chosenPayload: null,
              validatedActionId: 'wait',
              validatedActionCode: 0,
              validatedPayload: null,
              result: 'success',
            },
          },
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
          finalHealth: 5,
          damageDealt: JS_UNSAFE_DECIMAL,
          finalLifecycleStatus: 'active',
        },
      ],
    },
    replayHash: '0'.repeat(64),
    partial: false,
  };
}

function observedSelf(actorId: ReplayV2ActorId) {
  return {
    actorId,
    formId: 'prime',
    position: { x: 0, y: 1 },
    facing: 'east' as const,
    health: 5,
    cooldown: 0,
    energy: null,
    previousActionResult: 'none' as const,
  };
}

function worldV2(
  actorId: ReplayV2ActorId,
  nextTick: number,
): ReplayV2WorldState {
  const life: ReplayV2LifeState = {
    actorId,
    position: { x: nextTick, y: 1 },
    facing: 'east',
    health: 5,
    cooldown: 0,
    energy: null,
    damageDealt: JS_UNSAFE_DECIMAL,
    previousActionResult: nextTick === 0 ? 'none' : 'success',
    spawnedAtTick: nextTick,
  };
  return {
    teams: [
      {
        teamId: 0,
        damageDealt: JS_UNSAFE_DECIMAL,
        units: [
          {
            teamId: 0,
            unitId: 0,
            formId: 'prime',
            lifecycleStatus: 'active',
            respawnAtTick: null,
            damageDealt: JS_UNSAFE_DECIMAL,
            activeLife: life,
          },
        ],
      },
    ],
    projectiles: [
      {
        projectileId: JS_UNSAFE_DECIMAL,
        ownerActorId: actorId,
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
