import {
  replayDuelIdentity,
  replayFrontlineIdentity,
  replayParticipantKey,
  replayTeamKey,
} from './replayModel';
import type * as Model from './replayModel';
import type * as V1 from './replayWireV1';
import type * as V2 from './replayWireV2';

export type ReplayWireDocument =
  | V1.ReplayV1Document
  | V2.ReplayV2Document;

export type DecodedReplay =
  | {
      replayVersion: 1;
      /** The validated input object, retained by identity and never mutated. */
      wire: V1.ReplayV1Document;
      replay: Model.ReplayModel;
    }
  | {
      replayVersion: 2;
      /** The validated input object, retained by identity and never mutated. */
      wire: V2.ReplayV2Document;
      replay: Model.ReplayModel;
    };

export class ReplayDecodeError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ReplayDecodeError';
  }
}

/**
 * Validates and normalizes an already-parsed replay without serializing,
 * reordering, cloning, or otherwise changing the wire object.
 *
 * A caller that owns the original JSON bytes can therefore verify the backend
 * hash before calling this function, then retain `wire` for diagnostics.
 */
export function decodeReplay(input: unknown): DecodedReplay {
  const version = replayVersionOf(input);
  if (version === 1) {
    validateReplayV1(input);
    const wire = input;
    return {
      replayVersion: 1,
      wire,
      replay: normalizeReplayV1(wire),
    };
  }

  validateReplayV2(input);
  const wire = input;
  return {
    replayVersion: 2,
    wire,
    replay: normalizeReplayV2(wire),
  };
}

/** Version-dispatching convenience when the validated wire object is not needed. */
export function normalizeReplay(input: unknown): Model.ReplayModel {
  return decodeReplay(input).replay;
}

export function validateReplayWire(input: unknown): ReplayWireDocument {
  const version = replayVersionOf(input);
  if (version === 1) {
    validateReplayV1(input);
    return input;
  }
  validateReplayV2(input);
  return input;
}

function replayVersionOf(input: unknown): 1 | 2 {
  const root = record(input, 'replay');
  const header = record(required(root, 'header', 'replay'), 'replay.header');
  const version = integer(
    required(header, 'replayVersion', 'replay.header'),
    'replay.header.replayVersion',
  );

  if (version === 1 || version === 2) return version;

  throw new ReplayDecodeError(
    `replay.header.replayVersion: unsupported replay version ${version}`,
  );
}

type Validator = (value: unknown, path: string) => void;
type Shape = Readonly<Record<string, Validator>>;

const own = (value: object, key: string): boolean =>
  Object.prototype.hasOwnProperty.call(value, key);

function record(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    fail(path, 'expected an object');
  }
  return value as Record<string, unknown>;
}

function required(
  value: Record<string, unknown>,
  key: string,
  path: string,
): unknown {
  if (!own(value, key)) fail(`${path}.${key}`, 'missing required property');
  return value[key];
}

function fail(path: string, message: string): never {
  throw new ReplayDecodeError(`${path}: ${message}`);
}

const stringValue: Validator = (value, path) => {
  if (typeof value !== 'string') fail(path, 'expected a string');
};

const nonEmptyString: Validator = (value, path) => {
  stringValue(value, path);
  if ((value as string).length === 0) fail(path, 'must not be empty');
};

const integerValue: Validator = (value, path) => {
  integer(value, path);
};

const jsonIntegerValue: Validator = (value, path) => {
  if (typeof value !== 'number' || !Number.isInteger(value)) {
    fail(path, 'expected an integer');
  }
};

const booleanValue: Validator = (value, path) => {
  if (typeof value !== 'boolean') fail(path, 'expected a boolean');
};

const nullValue: Validator = (value, path) => {
  if (value !== null) fail(path, 'expected null');
};

function integer(value: unknown, path: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value)) {
    fail(path, 'expected a safe integer');
  }
  return value;
}

function oneOf(...values: readonly string[]): Validator {
  const allowed = new Set(values);
  return (value, path) => {
    if (typeof value !== 'string' || !allowed.has(value)) {
      fail(path, `expected one of ${values.join(', ')}`);
    }
  };
}

function literal(expected: string | number | boolean): Validator {
  return (value, path) => {
    if (value !== expected) fail(path, `expected ${JSON.stringify(expected)}`);
  };
}

function nullable(validator: Validator): Validator {
  return (value, path) => {
    if (value !== null) validator(value, path);
  };
}

function arrayOf(validator: Validator): Validator {
  return (value, path) => {
    if (!Array.isArray(value)) fail(path, 'expected an array');
    value.forEach((item, index) => validator(item, `${path}[${index}]`));
  };
}

function tuple2(validator: Validator): Validator {
  return (value, path) => {
    if (!Array.isArray(value) || value.length !== 2) {
      fail(path, 'expected a two-item tuple');
    }
    validator(value[0], `${path}[0]`);
    validator(value[1], `${path}[1]`);
  };
}

function strictObject(requiredShape: Shape, optionalShape: Shape = {}): Validator {
  const allowed = new Set([
    ...Object.keys(requiredShape),
    ...Object.keys(optionalShape),
  ]);
  return (value, path) => {
    const item = record(value, path);
    for (const key of Object.keys(item)) {
      if (!allowed.has(key)) fail(`${path}.${key}`, 'unknown property');
    }
    for (const [key, validator] of Object.entries(requiredShape)) {
      validator(required(item, key, path), `${path}.${key}`);
    }
    for (const [key, validator] of Object.entries(optionalShape)) {
      if (own(item, key)) validator(item[key], `${path}.${key}`);
    }
  };
}

const v1Direction = oneOf('North', 'East', 'South', 'West');
const v1Heading = oneOf(
  'North',
  'East',
  'South',
  'West',
  'NorthEast',
  'SouthEast',
  'SouthWest',
  'NorthWest',
);
const v1Action = oneOf(
  'Wait',
  'MoveForward',
  'TurnLeft',
  'TurnRight',
  'Shoot',
  'StrafeLeft',
  'StrafeRight',
);
const v1ActionResult = oneOf(
  'None',
  'Success',
  'Blocked',
  'OnCooldown',
  'Faulted',
);
const v1Status = oneOf('Active', 'Destroyed', 'Disqualified');
const v1EventType = oneOf(
  'Turn',
  'Move',
  'MoveBlocked',
  'Shot',
  'Damage',
  'Destroyed',
  'Fault',
  'Disqualified',
);
const positionTuple = tuple2(integerValue);
const positionObject = strictObject({
  x: integerValue,
  y: integerValue,
});
const v1ShotProgram = strictObject({
  initialAimOffset: integerValue,
  bendDirection: integerValue,
  bendAfterTiles: integerValue,
  bendEveryTiles: integerValue,
  bendCount: integerValue,
});
const v1Participant = strictObject(
  {
    slot: integerValue,
    name: stringValue,
    runtimeKind: stringValue,
    artifactHash: stringValue,
    accent: stringValue,
    spawnX: integerValue,
    spawnY: integerValue,
    spawnFacing: v1Direction,
  },
  {
    lookId: stringValue,
    projectileLookId: stringValue,
  },
);
const v1MapWallGroup = strictObject({
  family: stringValue,
  tiles: arrayOf(positionObject),
});
const v1MapPresentation = strictObject({
  boundaryWall: stringValue,
  interiorWall: stringValue,
  wallGroups: arrayOf(v1MapWallGroup),
});
const v1ShotProgramLimits = strictObject({
  maxInitialAimOctants: integerValue,
  maxBendAfterTiles: integerValue,
  maxBendEveryTiles: integerValue,
  maxBendCount: integerValue,
  maxPathTiles: integerValue,
  launchTiles: integerValue,
  tilesPerAdvance: integerValue,
});
const v1Header = strictObject(
  {
    replayVersion: literal(1),
    engineVersion: stringValue,
    gameRulesVersion: stringValue,
    runtimeProtocolVersion: stringValue,
    runtimeConfigurationVersion: stringValue,
    mapId: stringValue,
    mapVersion: integerValue,
    mapWidth: integerValue,
    mapHeight: integerValue,
    mapTiles: arrayOf(stringValue),
    seed: jsonIntegerValue,
    maxTicks: integerValue,
    visionRange: integerValue,
    participants: arrayOf(v1Participant),
  },
  {
    themeId: stringValue,
    presentation: v1MapPresentation,
    maxHealth: integerValue,
    visionCone: booleanValue,
    zoneTiles: arrayOf(positionTuple),
    controlPressureLimit: integerValue,
    controlBySoleOccupancy: booleanValue,
    controlOvertimeStartTick: integerValue,
    controlOvertimePressureLimit: integerValue,
    controlOvertimePressureGain: integerValue,
    controlOvertimeStopsDecay: booleanValue,
    programmedShots: booleanValue,
    programmedShotLimits: v1ShotProgramLimits,
  },
);
const v1GameEvent = strictObject(
  { type: v1EventType },
  {
    slot: integerValue,
    fromX: integerValue,
    fromY: integerValue,
    toX: integerValue,
    toY: integerValue,
    fromFacing: v1Direction,
    toFacing: v1Direction,
    hitSlot: integerValue,
    targetSlot: integerValue,
    amount: integerValue,
    newHealth: integerValue,
    message: stringValue,
  },
);
const v1VisibleEnemy = strictObject({
  slot: integerValue,
  x: integerValue,
  y: integerValue,
  facing: v1Direction,
  health: integerValue,
});
const v1HeardSound = strictObject({
  type: v1EventType,
  bearing: integerValue,
  distance: integerValue,
});
const v1BotTick = strictObject(
  {
    slot: integerValue,
    chosenAction: v1Action,
    validatedAction: v1Action,
    result: v1ActionResult,
    faulted: booleanValue,
    visibleTiles: arrayOf(positionTuple),
    visibleEnemies: arrayOf(v1VisibleEnemy),
  },
  {
    shotProgram: v1ShotProgram,
    debug: stringValue,
    heardSounds: arrayOf(v1HeardSound),
  },
);
const v1BotState = strictObject(
  {
    slot: integerValue,
    x: integerValue,
    y: integerValue,
    facing: v1Direction,
    health: integerValue,
    cooldown: integerValue,
    status: v1Status,
  },
  {
    energy: integerValue,
    zoneTicks: integerValue,
  },
);
const v1Projectile = strictObject(
  {
    x: integerValue,
    y: integerValue,
    direction: v1Direction,
    ownerSlot: integerValue,
  },
  {
    ticksUntilAdvance: integerValue,
    remainingTiles: integerValue,
    tilesPerAdvance: integerValue,
    id: integerValue,
    heading: v1Heading,
    programmedPath: arrayOf(positionTuple),
  },
);
const v1Traversal = strictObject(
  {
    id: integerValue,
    ownerSlot: integerValue,
    direction: v1Direction,
    fromX: integerValue,
    fromY: integerValue,
    path: arrayOf(positionTuple),
  },
  {
    heading: v1Heading,
    programmedPath: arrayOf(positionTuple),
  },
);
const v1TickSchema = strictObject(
  {
    tick: integerValue,
    bots: arrayOf(v1BotTick),
    events: arrayOf(v1GameEvent),
    state: arrayOf(v1BotState),
  },
  {
    projectiles: arrayOf(v1Projectile),
    projectileTraversals: arrayOf(v1Traversal),
    controlPressure: integerValue,
  },
);
const v1BotResult = strictObject(
  {
    slot: integerValue,
    outcome: oneOf('Win', 'Loss', 'Draw'),
    finalHealth: integerValue,
    damageDealt: integerValue,
    faults: integerValue,
    finalStatus: v1Status,
  },
  { zoneTicks: integerValue },
);
const v1Result = strictObject(
  {
    reason: oneOf('Elimination', 'Disqualification', 'MaxTicks', 'Domination'),
    endTick: integerValue,
    bots: arrayOf(v1BotResult),
  },
  {
    winnerSlot: integerValue,
    controlPressure: integerValue,
  },
);

const v2Direction = oneOf('north', 'east', 'south', 'west');
const v2Heading = oneOf(
  'north',
  'north-east',
  'east',
  'south-east',
  'south',
  'south-west',
  'west',
  'north-west',
);
const v2ActionResult = oneOf(
  'none',
  'success',
  'blocked',
  'on-cooldown',
  'faulted',
);
const v2Lifecycle = oneOf('active', 'respawning');
const v2TeamPerception = oneOf('individual', 'immediate-union');
const v2ActionParameterKind = oneOf(
  'shot-program',
  'direction',
  'unit-target',
  'form-target',
);
const v2EventType = oneOf(
  'respawned',
  'turn',
  'move',
  'move-blocked',
  'shot',
  'damage',
  'destroyed',
  'frontline-progress-changed',
  'frontline-position-advanced',
  'base-breached',
);
const v2ObservedEventType = oneOf(
  'respawned',
  'turn',
  'move',
  'move-blocked',
  'shot',
  'damage',
  'destroyed',
  'fault',
  'disqualified',
  'frontline-progress-changed',
  'frontline-position-advanced',
  'base-breached',
);
const actorId = strictObject({
  teamId: integerValue,
  unitId: integerValue,
  lifeId: integerValue,
});
const unitTarget = strictObject({
  teamId: integerValue,
  unitId: integerValue,
});
const v2ShotProgram = strictObject({
  initialAimOffset: integerValue,
  bendDirection: integerValue,
  bendAfterTiles: integerValue,
  bendEveryTiles: integerValue,
  bendCount: integerValue,
});
const canonicalSeed: Validator = (value, path) => {
  if (
    typeof value !== 'string' ||
    !/^(0|[1-9][0-9]*)$/.test(value) ||
    BigInt(value) > 18_446_744_073_709_551_615n
  ) {
    fail(path, 'expected a canonical unsigned 64-bit decimal string');
  }
};
const canonicalProjectileId: Validator = (value, path) => {
  if (
    typeof value !== 'string' ||
    !/^(0|[1-9][0-9]*)$/.test(value) ||
    BigInt(value) > 9_223_372_036_854_775_807n
  ) {
    fail(path, 'expected a canonical non-negative signed 64-bit decimal string');
  }
};
const canonicalSignedLong: Validator = (value, path) => {
  if (typeof value !== 'string' || !/^(0|-?[1-9][0-9]*)$/.test(value)) {
    fail(path, 'expected a canonical signed 64-bit decimal string');
  }
  const parsed = BigInt(value as string);
  if (
    parsed < -9_223_372_036_854_775_808n ||
    parsed > 9_223_372_036_854_775_807n
  ) {
    fail(path, 'expected a canonical signed 64-bit decimal string');
  }
};
function aliasHandle(prefix: string): Validator {
  return (value, path) => {
    if (
      typeof value !== 'string' ||
      !new RegExp(`^${prefix}-(0|[1-9][0-9]*)$`).test(value)
    ) {
      fail(path, `expected canonical ${prefix}-N handle`);
    }
    const ordinal = Number(value.slice(prefix.length + 1));
    if (!Number.isSafeInteger(ordinal) || ordinal > 2_147_483_647) {
      fail(path, `expected canonical ${prefix}-N handle`);
    }
  };
}
const enemyLifeHandle = aliasHandle('enemy-life');
const projectileHandle = aliasHandle('projectile');
const eventHandle = aliasHandle('event');
const sha256: Validator = (value, path) => {
  if (typeof value !== 'string' || !/^[0-9a-f]{64}$/.test(value)) {
    fail(path, 'expected lowercase SHA-256 hex');
  }
};

const v2MatchLimits = strictObject({
  maxTicks: integerValue,
  faultLimit: integerValue,
  teamCount: integerValue,
  participantCount: integerValue,
  unitSlotCount: integerValue,
  initialUnitsPerTeam: integerValue,
  maxUnitsPerTeam: integerValue,
  destructionEndsMatch: booleanValue,
  respawnsEnabled: booleanValue,
});
const v2ObjectiveOvertime = strictObject({
  startTick: integerValue,
  pressureLimit: integerValue,
  pressureGain: integerValue,
  stopsDecay: booleanValue,
});
const v2ObjectiveRules = strictObject({
  mode: oneOf('none', 'zone-ticks', 'shared-pressure', 'frontline'),
  zoneControlEnabled: booleanValue,
  zoneDominationTicks: integerValue,
  zoneExclusiveAccrual: booleanValue,
  sharedPressureEnabled: booleanValue,
  controlBySoleOccupancy: booleanValue,
  controlPressureLimit: integerValue,
  controlPressureGain: integerValue,
  controlPressureDecayInterval: integerValue,
  overtime: v2ObjectiveOvertime,
  maxTickTiebreakers: arrayOf(
    oneOf('objective', 'health', 'damage-dealt'),
  ),
});
const v2FrontlineDefinition = strictObject({
  teamCount: integerValue,
  participantsPerTeam: integerValue,
  frontlinePositionCount: integerValue,
  initialUnitsPerTeam: integerValue,
  maxUnitsPerTeam: integerValue,
  teamPerception: v2TeamPerception,
  capture: strictObject({
    threshold: integerValue,
    gainPerSoleTeamTick: integerValue,
    decayAmount: integerValue,
    decayIntervalTicks: integerValue,
    redeployPauseTicks: integerValue,
    pushesToBreach: integerValue,
  }),
  lifecycle: strictObject({
    primeRespawnTicks: integerValue,
    childRebuildTicks: integerValue,
    fabricationUnlockTicks: arrayOf(integerValue),
  }),
  anchor: strictObject({
    windupTicks: integerValue,
    healthGain: integerValue,
    irreversibleForLife: booleanValue,
  }),
  alliedCombat: strictObject({
    friendlyFireEnabled: booleanValue,
    alliedProjectilesBlock: booleanValue,
  }),
});
const v2EnergyRules = strictObject({
  enabled: booleanValue,
  maxEnergy: integerValue,
  shotEnergyCost: integerValue,
  regenerationIntervalTicks: integerValue,
  regenerationAmount: integerValue,
});
const v2Form = strictObject({
  id: stringValue,
  maxHealth: integerValue,
  visionRange: integerValue,
  shootCooldownTicks: integerValue,
  omnidirectionalVision: booleanValue,
  omnidirectionalShooting: booleanValue,
  movementLayer: literal('ground'),
  objectiveWeight: integerValue,
  canMove: booleanValue,
  canShoot: booleanValue,
  allowsProgrammedShots: booleanValue,
  allowedActionIds: arrayOf(stringValue),
});
const v2ActionDefinition = strictObject({
  id: stringValue,
  code: integerValue,
  kind: oneOf('wait', 'movement', 'rotation', 'attack'),
  parameterKinds: arrayOf(v2ActionParameterKind),
  enabled: booleanValue,
});
const v2ProjectileRules = strictObject({
  mode: oneOf('instant-ray', 'discrete'),
  damagePerHit: integerValue,
  maxTravelTiles: integerValue,
  shootCooldownTicks: integerValue,
  ticksPerAdvance: integerValue,
  tilesPerAdvance: integerValue,
  launchTiles: integerValue,
  advancesOnLaunchTick: booleanValue,
  damageAppliedSimultaneously: booleanValue,
});
const v2ShotProgramRules = strictObject({
  enabled: booleanValue,
  headingSectors: integerValue,
  bendStepOctants: integerValue,
  minInitialAimOctants: integerValue,
  maxInitialAimOctants: integerValue,
  aimOnlyProgram: strictObject({
    bendDirection: integerValue,
    bendAfterTiles: integerValue,
    bendEveryTiles: integerValue,
    bendCount: integerValue,
  }),
  allowedCurvedBendDirections: arrayOf(integerValue),
  minBendAfterTiles: integerValue,
  maxBendAfterTiles: integerValue,
  minBendEveryTiles: integerValue,
  maxBendEveryTiles: integerValue,
  minBendCount: integerValue,
  maxBendCount: integerValue,
  launchTiles: integerValue,
  payloadOptional: booleanValue,
  defaultProgram: v2ShotProgram,
  invalidPayloadResult: nullable(oneOf('blocked', 'faulted', 'rejected')),
  unsupportedPayloadResult: oneOf('blocked', 'faulted', 'rejected'),
  diagonalCornersMustBeClear: booleanValue,
});
const v2VisionRules = strictObject({
  range: integerValue,
  distanceMetric: literal('chebyshev'),
  shape: oneOf('omnidirectional', 'facing-quadrant'),
  omnidirectionalProximityRange: integerValue,
  lineOfSight: literal('corner-strict-supercover'),
  hearingRadius: integerValue,
  hearingBearingSectors: integerValue,
  hearingDistanceBandUpperBounds: arrayOf(integerValue),
  loudEventTypes: arrayOf(
    oneOf(
      'turn',
      'move',
      'move-blocked',
      'shot',
      'damage',
      'destroyed',
      'fault',
      'disqualified',
    ),
  ),
});
const v2CollisionRules = strictObject({
  unitsBlockWalls: booleanValue,
  unitsBlockUnits: booleanValue,
  sameDestinationMovesBlockAll: booleanValue,
  swapMovesBlocked: booleanValue,
  followingVacatedUnitAllowed: booleanValue,
  projectilesBlockMovement: booleanValue,
  movingOntoProjectileCausesHit: booleanValue,
  wallsConsumeProjectiles: booleanValue,
  projectilesIgnoreOwner: booleanValue,
  projectilesStopOnFirstNonOwnerUnit: booleanValue,
  projectilesCollideWithProjectiles: booleanValue,
});
const v2TickResolutionRules = strictObject({
  observationsUsePreTickState: booleanValue,
  decisionsResolveAsJointStep: booleanValue,
  phases: arrayOf(
    oneOf(
      'freeze-observations',
      'collect-joint-decisions',
      'validate-actions',
      'rotate',
      'move',
      'advance-existing-projectiles',
      'launch-shots-and-apply-damage',
      'update-cooldowns-and-energy',
      'apply-runtime-faults',
      'update-objective',
      'resolve-match-completion',
      'apply-tick-start-lifecycle',
      'queue-destroyed-lives',
    ),
  ),
});
const v2Rules = strictObject(
  {
    schemaVersion: integerValue,
    rulesetId: stringValue,
    rulesFingerprint: stringValue,
    limits: v2MatchLimits,
    objective: v2ObjectiveRules,
    energy: v2EnergyRules,
    forms: arrayOf(v2Form),
    actions: arrayOf(v2ActionDefinition),
    projectiles: v2ProjectileRules,
    shotPrograms: v2ShotProgramRules,
    vision: v2VisionRules,
    collisions: v2CollisionRules,
    tickResolution: v2TickResolutionRules,
  },
  { frontlineDefinition: v2FrontlineDefinition },
);
const v2MapSpawn = strictObject({
  teamId: integerValue,
  x: integerValue,
  y: integerValue,
  facing: v2Direction,
});
const v2FrontlineMap = strictObject({
  positions: arrayOf(
    strictObject({
      positionIndex: integerValue,
      tiles: arrayOf(positionTuple),
    }),
  ),
  teamHomes: arrayOf(
    strictObject({
      teamId: integerValue,
      primeSpawn: strictObject({
        x: integerValue,
        y: integerValue,
        facing: v2Direction,
      }),
      protectedSpawnPad: arrayOf(positionTuple),
    }),
  ),
  anchorForbiddenTiles: arrayOf(positionTuple),
});
const v2Map = strictObject(
  {
    schemaVersion: integerValue,
    mapId: stringValue,
    mapVersion: integerValue,
    mapFingerprint: stringValue,
    formatVersion: integerValue,
    width: integerValue,
    height: integerValue,
    tileRows: arrayOf(stringValue),
    spawns: arrayOf(v2MapSpawn),
    objectiveTiles: arrayOf(positionTuple),
  },
  { frontline: v2FrontlineMap },
);
const v2Topology = strictObject({
  teamCount: integerValue,
  participantCount: integerValue,
  unitSlotCount: integerValue,
  initialLifeCount: integerValue,
  teams: arrayOf(strictObject({ teamId: integerValue })),
  participants: arrayOf(
    strictObject({
      participantId: integerValue,
      teamId: integerValue,
    }),
  ),
  unitSlots: arrayOf(
    strictObject({
      teamId: integerValue,
      unitId: integerValue,
      controllerParticipantId: integerValue,
    }),
  ),
  initialLives: arrayOf(
    strictObject({
      teamId: integerValue,
      unitId: integerValue,
      lifeId: integerValue,
      formId: stringValue,
    }),
  ),
});
const v2Contract = strictObject({
  schemaVersion: integerValue,
  matchContractFingerprint: stringValue,
  rules: v2Rules,
  map: v2Map,
  topology: v2Topology,
});
const v2Participant = strictObject({
  participantId: integerValue,
  teamId: integerValue,
  name: stringValue,
  runtimeKind: stringValue,
  artifactHash: stringValue,
  accent: stringValue,
  lookId: nullable(stringValue),
  projectileLookId: nullable(stringValue),
});
const v2Presentation = strictObject({
  themeId: nullable(stringValue),
  map: nullable(
    strictObject({
      boundaryWall: stringValue,
      interiorWall: stringValue,
      wallGroups: arrayOf(
        strictObject({
          family: stringValue,
          tiles: arrayOf(positionObject),
        }),
      ),
    }),
  ),
});
const v2Header = strictObject({
  replayVersion: literal(2),
  engineVersion: stringValue,
  gameRulesVersion: stringValue,
  actorRuntime: strictObject({
    family: stringValue,
    version: integerValue,
    matchStartSchemaVersion: integerValue,
    observationSchemaVersion: integerValue,
    decisionSchemaVersion: integerValue,
  }),
  seed: canonicalSeed,
  contract: v2Contract,
  presentation: nullable(v2Presentation),
  participants: arrayOf(v2Participant),
});

const v2ObservedSelf = strictObject({
  actorId,
  formId: stringValue,
  position: positionObject,
  facing: v2Direction,
  health: integerValue,
  cooldown: integerValue,
  energy: nullable(integerValue),
  previousActionResult: v2ActionResult,
});
const v2ObservedAlly = v2ObservedSelf;
const v2ObservedEnemyActor = strictObject({
  teamId: integerValue,
  unitId: integerValue,
  lifeHandle: enemyLifeHandle,
});
const v2ObservedEnemy = strictObject({
  actor: v2ObservedEnemyActor,
  formId: stringValue,
  position: positionObject,
  facing: v2Direction,
  health: integerValue,
  observedBy: arrayOf(actorId),
});
const v2ObservedUnit = strictObject({
  teamId: integerValue,
  unitId: integerValue,
  formId: stringValue,
  lifecycleStatus: v2Lifecycle,
  activeActorId: nullable(actorId),
  respawnAtTick: nullable(integerValue),
});
const v2ObservedTile = strictObject({
  position: positionObject,
  isWall: booleanValue,
  observedBy: arrayOf(actorId),
});
const v2ObservedProjectile = strictObject({
  projectileHandle,
  ownerTeamId: integerValue,
  alliedOwnerActorId: nullable(actorId),
  visibleEnemyOwner: nullable(v2ObservedEnemyActor),
  position: positionObject,
  heading: v2Heading,
  tilesPerAdvance: integerValue,
  ticksUntilAdvance: integerValue,
  remainingTiles: integerValue,
  observedBy: arrayOf(actorId),
});
const v2ObservedEvent = strictObject({
  eventHandle,
  sourceTick: integerValue,
  type: v2ObservedEventType,
  teamId: nullable(integerValue),
  alliedActorId: nullable(actorId),
  enemyActor: nullable(v2ObservedEnemyActor),
  projectileHandle: nullable(projectileHandle),
  position: nullable(positionObject),
  facing: nullable(v2Direction),
  amount: nullable(integerValue),
  newHealth: nullable(integerValue),
  observedBy: arrayOf(actorId),
});
const v2ObservedSound = strictObject({
  eventHandle,
  sourceTick: integerValue,
  observerActorId: actorId,
  type: v2ObservedEventType,
  bearing: integerValue,
  distance: integerValue,
});
const v2ObservedObjective = strictObject({
  activePositionIndex: integerValue,
  claimingTeamId: nullable(integerValue),
  captureProgress: integerValue,
  decayTicksElapsed: integerValue,
  controlResumesAtTick: integerValue,
});
const v2ObservedAction = strictObject({
  actionId: stringValue,
  actionCode: integerValue,
  parameterKinds: arrayOf(v2ActionParameterKind),
  enabled: booleanValue,
  available: booleanValue,
  shotProgramAvailable: nullable(booleanValue),
  allowedDirections: nullable(arrayOf(v2Direction)),
  allowedUnitTargets: nullable(arrayOf(unitTarget)),
  allowedFormTargets: nullable(arrayOf(stringValue)),
});
const v2Observation = strictObject({
  schemaVersion: integerValue,
  tick: integerValue,
  matchContractFingerprint: stringValue,
  teamPerception: v2TeamPerception,
  self: v2ObservedSelf,
  teamUnits: arrayOf(v2ObservedUnit),
  allies: arrayOf(v2ObservedAlly),
  enemies: arrayOf(v2ObservedEnemy),
  visibleTiles: arrayOf(v2ObservedTile),
  visibleProjectiles: nullable(arrayOf(v2ObservedProjectile)),
  visibleEvents: arrayOf(v2ObservedEvent),
  heardSounds: nullable(arrayOf(v2ObservedSound)),
  frontlineObjective: nullable(v2ObservedObjective),
  actions: arrayOf(v2ObservedAction),
});
const v2ActionPayload = strictObject({
  shotProgram: nullable(v2ShotProgram),
  direction: nullable(v2Direction),
  unitTarget: nullable(unitTarget),
  formTargetId: nullable(stringValue),
});
const v2ActorDecision = strictObject({
  actionId: nullable(stringValue),
  actionCode: nullable(integerValue),
  payload: nullable(v2ActionPayload),
  debugMessage: nullable(stringValue),
  faulted: booleanValue,
  faultMessage: nullable(stringValue),
});
const v2ActionResolution = strictObject({
  actorId,
  chosenActionId: stringValue,
  chosenActionCode: integerValue,
  chosenPayload: nullable(v2ActionPayload),
  validatedActionId: stringValue,
  validatedActionCode: integerValue,
  validatedPayload: nullable(v2ActionPayload),
  result: v2ActionResult,
});
const v2LifeStart = strictObject({
  schemaVersion: integerValue,
  runtimeContractVersion: integerValue,
  actorId,
  participantId: integerValue,
  actorRandomSeed: canonicalSeed,
  spawnReason: oneOf('initial', 'respawn', 'rebuild', 'fabrication'),
  matchContractFingerprint: stringValue,
});
const v2Aliases = strictObject({
  enemyLives: arrayOf(
    strictObject({
      lifeHandle: enemyLifeHandle,
      actorId,
    }),
  ),
  projectiles: arrayOf(
    strictObject({
      projectileHandle,
      projectileId: canonicalProjectileId,
    }),
  ),
  events: arrayOf(
    strictObject({
      eventHandle,
      eventId: nonEmptyString,
    }),
  ),
});
const v2ActorTurn = strictObject({
  actorId,
  lifeStart: nullable(v2LifeStart),
  observation: v2Observation,
  aliases: v2Aliases,
  runtimeReply: v2ActorDecision,
  acceptedDecision: v2ActorDecision,
  actionResolution: v2ActionResolution,
});
const v2Event = strictObject({
  eventId: nonEmptyString,
  tick: integerValue,
  type: v2EventType,
  teamId: nullable(integerValue),
  sourceActorId: nullable(actorId),
  targetActorId: nullable(actorId),
  projectileId: nullable(canonicalProjectileId),
  from: nullable(positionObject),
  to: nullable(positionObject),
  fromFacing: nullable(v2Direction),
  toFacing: nullable(v2Direction),
  projectileHeading: nullable(v2Heading),
  actionPayload: nullable(v2ActionPayload),
  actionId: nullable(stringValue),
  actionCode: nullable(integerValue),
  actionResult: nullable(v2ActionResult),
  amount: nullable(integerValue),
  newHealth: nullable(integerValue),
  lifecycleStatus: nullable(v2Lifecycle),
  respawnAtTick: nullable(integerValue),
  fromPositionIndex: nullable(integerValue),
  toPositionIndex: nullable(integerValue),
  claimingTeamId: nullable(integerValue),
  captureProgress: nullable(integerValue),
  controlResumesAtTick: nullable(integerValue),
});
const v2Traversal = strictObject({
  projectileId: canonicalProjectileId,
  ownerActorId: actorId,
  launchDirection: v2Direction,
  from: positionObject,
  path: arrayOf(positionObject),
  heading: nullable(v2Heading),
  shotProgram: nullable(v2ShotProgram),
  programmedPath: nullable(arrayOf(positionObject)),
});
const v2LifeState = strictObject({
  actorId,
  position: positionObject,
  facing: v2Direction,
  health: integerValue,
  cooldown: integerValue,
  energy: nullable(integerValue),
  damageDealt: canonicalProjectileId,
  previousActionResult: v2ActionResult,
  spawnedAtTick: integerValue,
});
const v2UnitState = strictObject({
  teamId: integerValue,
  unitId: integerValue,
  formId: stringValue,
  lifecycleStatus: v2Lifecycle,
  respawnAtTick: nullable(integerValue),
  damageDealt: canonicalProjectileId,
  activeLife: nullable(v2LifeState),
});
const v2TeamState = strictObject({
  teamId: integerValue,
  damageDealt: canonicalProjectileId,
  units: arrayOf(v2UnitState),
});
const v2ProjectileState = strictObject({
  projectileId: canonicalProjectileId,
  ownerActorId: actorId,
  position: positionObject,
  launchDirection: v2Direction,
  heading: nullable(v2Heading),
  shotProgram: nullable(v2ShotProgram),
  programmedPath: nullable(arrayOf(positionObject)),
  nextProgrammedPathIndex: integerValue,
  tilesTraveled: integerValue,
  phase: integerValue,
});
const v2Control = strictObject({
  nextTick: integerValue,
  activePositionIndex: integerValue,
  claimingTeamId: nullable(integerValue),
  captureProgress: integerValue,
  decayTicksElapsed: integerValue,
  controlResumesAtTick: integerValue,
  winnerTeamId: nullable(integerValue),
});
const v2World = strictObject({
  teams: arrayOf(v2TeamState),
  projectiles: arrayOf(v2ProjectileState),
  objective: v2Control,
});
const v2Tick = strictObject({
  tick: integerValue,
  tickStart: strictObject({
    state: v2World,
    activeActors: arrayOf(actorId),
    lifecycleEvents: arrayOf(v2Event),
  }),
  actors: arrayOf(v2ActorTurn),
  resolution: strictObject({
    events: arrayOf(v2Event),
    projectileTraversals: arrayOf(v2Traversal),
  }),
  postState: v2World,
});
const v2TeamResult = strictObject({
  teamId: integerValue,
  outcome: oneOf('win', 'loss', 'draw'),
  finalHealth: integerValue,
  damageDealt: canonicalProjectileId,
  finalLifecycleStatus: v2Lifecycle,
});
const v2Result = strictObject({
  winnerTeamId: nullable(integerValue),
  reason: oneOf('base-breach', 'max-ticks'),
  endTick: integerValue,
  territorialScore: canonicalSignedLong,
  objective: v2Control,
  teams: arrayOf(v2TeamResult),
});

export function validateReplayV1(
  input: unknown,
): asserts input is V1.ReplayV1Document {
  const root = record(input, 'replay');
  assertAllowedKeys(
    root,
    ['header', 'ticks', 'result', 'replayHash', 'partial'],
    ['header', 'ticks'],
    'replay',
  );
  v1Header(root.header, 'replay.header');
  arrayOf(v1TickSchema)(root.ticks, 'replay.ticks');

  const isPartial = root.partial === true;
  if (isPartial) {
    if (own(root, 'result')) {
      nullValue(root.result, 'replay.result');
    }
    if (own(root, 'replayHash')) {
      nullValue(root.replayHash, 'replay.replayHash');
    }
  } else {
    if (own(root, 'partial')) {
      fail(
        'replay.partial',
        'replay-v1 complete documents omit the partial property',
      );
    }
    v1Result(required(root, 'result', 'replay'), 'replay.result');
    sha256(required(root, 'replayHash', 'replay'), 'replay.replayHash');
  }

  validateV1Relationships(input as V1.ReplayV1Document);
}

export function validateReplayV2(
  input: unknown,
): asserts input is V2.ReplayV2Document {
  const root = record(input, 'replay');
  assertExactKeys(
    root,
    ['header', 'ticks', 'result', 'replayHash', 'partial'],
    'replay',
  );
  v2Header(root.header, 'replay.header');
  arrayOf(v2Tick)(root.ticks, 'replay.ticks');
  booleanValue(root.partial, 'replay.partial');

  if (root.partial === true) {
    nullValue(root.result, 'replay.result');
    nullValue(root.replayHash, 'replay.replayHash');
  } else {
    literal(false)(root.partial, 'replay.partial');
    v2Result(root.result, 'replay.result');
    sha256(root.replayHash, 'replay.replayHash');
  }

  validateV2Relationships(input as V2.ReplayV2Document);
}

function assertAllowedKeys(
  value: Record<string, unknown>,
  allowedKeys: readonly string[],
  requiredKeys: readonly string[],
  path: string,
): void {
  const allowed = new Set(allowedKeys);
  for (const key of Object.keys(value)) {
    if (!allowed.has(key)) fail(`${path}.${key}`, 'unknown property');
  }
  for (const key of requiredKeys) {
    if (!own(value, key)) fail(`${path}.${key}`, 'missing required property');
  }
}

function assertExactKeys(
  value: Record<string, unknown>,
  keys: readonly string[],
  path: string,
): void {
  assertAllowedKeys(value, keys, keys, path);
}

function validateV1Relationships(document: V1.ReplayV1Document): void {
  ensureUnique(
    document.header.participants,
    (participant) => participant.slot,
    'replay.header.participants',
  );
  ensureUnique(document.ticks, (tick) => tick.tick, 'replay.ticks');
  const participantSlots = new Set(
    document.header.participants.map((participant) => participant.slot),
  );

  document.ticks.forEach((tick, tickIndex) => {
    ensureUnique(tick.bots, (bot) => bot.slot, `replay.ticks[${tickIndex}].bots`);
    ensureUnique(
      tick.state,
      (state) => state.slot,
      `replay.ticks[${tickIndex}].state`,
    );
    for (const bot of tick.bots) {
      if (!participantSlots.has(bot.slot)) {
        fail(
          `replay.ticks[${tickIndex}].bots`,
          `unknown participant slot ${bot.slot}`,
        );
      }
    }
    for (const state of tick.state) {
      if (!participantSlots.has(state.slot)) {
        fail(
          `replay.ticks[${tickIndex}].state`,
          `unknown participant slot ${state.slot}`,
        );
      }
    }
  });
}

function validateV2Relationships(document: V2.ReplayV2Document): void {
  const { contract } = document.header;
  const { topology } = contract;

  ensureUnique(topology.teams, (team) => team.teamId, 'replay.header.contract.topology.teams');
  ensureUnique(
    topology.participants,
    (participant) => participant.participantId,
    'replay.header.contract.topology.participants',
  );
  ensureUnique(
    topology.unitSlots,
    (unit) => `${unit.teamId}:${unit.unitId}`,
    'replay.header.contract.topology.unitSlots',
  );
  ensureUnique(
    topology.initialLives,
    (life) => actorIdValue(life),
    'replay.header.contract.topology.initialLives',
  );
  ensureUnique(
    document.header.participants,
    (participant) => participant.participantId,
    'replay.header.participants',
  );
  ensureUnique(document.ticks, (tick) => tick.tick, 'replay.ticks');

  if (
    topology.teamCount !== topology.teams.length ||
    topology.participantCount !== topology.participants.length ||
    topology.unitSlotCount !== topology.unitSlots.length ||
    topology.initialLifeCount !== topology.initialLives.length
  ) {
    fail(
      'replay.header.contract.topology',
      'declared counts must match collection lengths',
    );
  }

  const topologyParticipants = [...topology.participants].sort(
    compareParticipant,
  );
  const metadataParticipants = [...document.header.participants].sort(
    compareParticipant,
  );
  if (
    topologyParticipants.length !== metadataParticipants.length ||
    topologyParticipants.some(
      (participant, index) =>
        participant.participantId !==
          metadataParticipants[index]?.participantId ||
        participant.teamId !== metadataParticipants[index]?.teamId,
    )
  ) {
    fail(
      'replay.header.participants',
      'must exactly match contract topology participant/team identities',
    );
  }

  const teamIds = new Set(topology.teams.map((team) => team.teamId));
  const participantsById = new Map(
    topology.participants.map((participant) => [
      participant.participantId,
      participant,
    ]),
  );
  const unitKeys = new Set(
    topology.unitSlots.map((unit) => `${unit.teamId}:${unit.unitId}`),
  );
  const unitControllers = new Map(
    topology.unitSlots.map((unit) => [
      `${unit.teamId}:${unit.unitId}`,
      unit.controllerParticipantId,
    ]),
  );

  for (const participant of topology.participants) {
    if (!teamIds.has(participant.teamId)) {
      fail(
        'replay.header.contract.topology.participants',
        `participant ${participant.participantId} references unknown team ${participant.teamId}`,
      );
    }
  }
  for (const unit of topology.unitSlots) {
    const participant = participantsById.get(unit.controllerParticipantId);
    if (!teamIds.has(unit.teamId) || participant?.teamId !== unit.teamId) {
      fail(
        'replay.header.contract.topology.unitSlots',
        `unit ${unit.teamId}:${unit.unitId} has an invalid controller`,
      );
    }
  }
  for (const life of topology.initialLives) {
    if (!unitKeys.has(`${life.teamId}:${life.unitId}`)) {
      fail(
        'replay.header.contract.topology.initialLives',
        `life ${actorIdValue(life)} references an unknown unit`,
      );
    }
  }

  const allEventIds = new Set<string>();
  document.ticks.forEach((tick, tickIndex) => {
    validateV2WorldRelationships(
      tick.tickStart.state,
      unitKeys,
      `replay.ticks[${tickIndex}].tickStart.state`,
    );
    validateV2WorldRelationships(
      tick.postState,
      unitKeys,
      `replay.ticks[${tickIndex}].postState`,
    );

    const active = tick.tickStart.activeActors.map(actorIdValue).sort();
    const turns = tick.actors.map((turn) => actorIdValue(turn.actorId)).sort();
    const stateActors = tick.tickStart.state.teams
      .flatMap((team) => team.units)
      .flatMap((unit) => (unit.activeLife ? [actorIdValue(unit.activeLife.actorId)] : []))
      .sort();
    if (!sameStrings(active, turns) || !sameStrings(active, stateActors)) {
      fail(
        `replay.ticks[${tickIndex}]`,
        'activeActors, actor turns, and tick-start active lives must match exactly',
      );
    }

    ensureUnique(
      tick.tickStart.activeActors,
      actorIdValue,
      `replay.ticks[${tickIndex}].tickStart.activeActors`,
    );
    ensureUnique(
      tick.actors,
      (turn) => actorIdValue(turn.actorId),
      `replay.ticks[${tickIndex}].actors`,
    );

    tick.actors.forEach((turn, actorIndex) => {
      const identity = actorIdValue(turn.actorId);
      const actorPath = `replay.ticks[${tickIndex}].actors[${actorIndex}]`;
      if (
        identity !== actorIdValue(turn.observation.self.actorId) ||
        identity !== actorIdValue(turn.actionResolution.actorId) ||
        turn.observation.tick !== tick.tick ||
        turn.observation.matchContractFingerprint !==
          contract.matchContractFingerprint
      ) {
        fail(
          actorPath,
          'actor identity, chronology, or contract fingerprint is inconsistent',
        );
      }
      if (
        turn.observation.schemaVersion !==
        document.header.actorRuntime.observationSchemaVersion
      ) {
        fail(
          `${actorPath}.observation.schemaVersion`,
          'does not match header.actorRuntime',
        );
      }
      if (turn.lifeStart) {
        const expectedParticipant = unitControllers.get(
          `${turn.actorId.teamId}:${turn.actorId.unitId}`,
        );
        if (
          actorIdValue(turn.lifeStart.actorId) !== identity ||
          turn.lifeStart.participantId !== expectedParticipant ||
          turn.lifeStart.runtimeContractVersion !==
            document.header.actorRuntime.version ||
          turn.lifeStart.schemaVersion !==
            document.header.actorRuntime.matchStartSchemaVersion ||
          turn.lifeStart.matchContractFingerprint !==
            contract.matchContractFingerprint
        ) {
          fail(
            `${actorPath}.lifeStart`,
            'life-start identity or actor-runtime contract is inconsistent',
          );
        }
      }
      validateV2Aliases(turn, actorPath);
    });

    const tickEvents = [
      ...tick.tickStart.lifecycleEvents,
      ...tick.resolution.events,
    ];
    tickEvents.forEach((event, eventIndex) => {
      if (event.tick !== tick.tick) {
        fail(
          `replay.ticks[${tickIndex}].events[${eventIndex}]`,
          'event tick does not match containing tick',
        );
      }
      if (allEventIds.has(event.eventId)) {
        fail(
          `replay.ticks[${tickIndex}].events[${eventIndex}].eventId`,
          `duplicate event ID ${event.eventId}`,
        );
      }
      allEventIds.add(event.eventId);
    });
  });
}

function validateV2Aliases(
  turn: V2.ReplayV2ActorTurn,
  path: string,
): void {
  ensureUnique(
    turn.aliases.enemyLives,
    (alias) => alias.lifeHandle,
    `${path}.aliases.enemyLives`,
  );
  ensureUnique(
    turn.aliases.enemyLives,
    (alias) => actorIdValue(alias.actorId),
    `${path}.aliases.enemyLives`,
  );
  ensureUnique(
    turn.aliases.projectiles,
    (alias) => alias.projectileHandle,
    `${path}.aliases.projectiles`,
  );
  ensureUnique(
    turn.aliases.projectiles,
    (alias) => alias.projectileId,
    `${path}.aliases.projectiles`,
  );
  ensureUnique(
    turn.aliases.events,
    (alias) => alias.eventHandle,
    `${path}.aliases.events`,
  );
  ensureUnique(
    turn.aliases.events,
    (alias) => alias.eventId,
    `${path}.aliases.events`,
  );

  const lives = new Map(
    turn.aliases.enemyLives.map((alias) => [
      alias.lifeHandle,
      alias.actorId,
    ]),
  );
  const projectiles = new Set(
    turn.aliases.projectiles.map((alias) => alias.projectileHandle),
  );
  const events = new Set(
    turn.aliases.events.map((alias) => alias.eventHandle),
  );
  const requireEnemy = (
    actor: V2.ReplayV2ObservedEnemyActorRef,
    actorPath: string,
  ) => {
    const exact = lives.get(actor.lifeHandle);
    if (
      !exact ||
      exact.teamId !== actor.teamId ||
      exact.unitId !== actor.unitId
    ) {
      fail(actorPath, 'enemy life handle has no matching alias');
    }
  };

  turn.observation.enemies.forEach((enemy, index) =>
    requireEnemy(enemy.actor, `${path}.observation.enemies[${index}].actor`),
  );
  turn.observation.visibleProjectiles?.forEach((projectile, index) => {
    if (!projectiles.has(projectile.projectileHandle)) {
      fail(
        `${path}.observation.visibleProjectiles[${index}].projectileHandle`,
        'projectile handle has no matching alias',
      );
    }
    if (projectile.visibleEnemyOwner) {
      requireEnemy(
        projectile.visibleEnemyOwner,
        `${path}.observation.visibleProjectiles[${index}].visibleEnemyOwner`,
      );
    }
  });
  turn.observation.visibleEvents.forEach((event, index) => {
    if (!events.has(event.eventHandle)) {
      fail(
        `${path}.observation.visibleEvents[${index}].eventHandle`,
        'event handle has no matching alias',
      );
    }
    if (
      event.projectileHandle &&
      !projectiles.has(event.projectileHandle)
    ) {
      fail(
        `${path}.observation.visibleEvents[${index}].projectileHandle`,
        'projectile handle has no matching alias',
      );
    }
    if (event.enemyActor) {
      requireEnemy(
        event.enemyActor,
        `${path}.observation.visibleEvents[${index}].enemyActor`,
      );
    }
  });
  turn.observation.heardSounds?.forEach((sound, index) => {
    if (!events.has(sound.eventHandle)) {
      fail(
        `${path}.observation.heardSounds[${index}].eventHandle`,
        'event handle has no matching alias',
      );
    }
  });
}

function validateV2WorldRelationships(
  world: V2.ReplayV2WorldState,
  topologyUnitKeys: ReadonlySet<string>,
  path: string,
): void {
  ensureUnique(world.teams, (team) => team.teamId, `${path}.teams`);
  ensureUnique(
    world.projectiles,
    (projectile) => projectile.projectileId,
    `${path}.projectiles`,
  );
  const actorKeys: string[] = [];
  for (const team of world.teams) {
    ensureUnique(team.units, (unit) => unit.unitId, `${path}.teams.units`);
    for (const unit of team.units) {
      if (
        unit.teamId !== team.teamId ||
        !topologyUnitKeys.has(`${unit.teamId}:${unit.unitId}`)
      ) {
        fail(
          `${path}.teams`,
          `unit ${unit.teamId}:${unit.unitId} is inconsistent with topology`,
        );
      }
      if (unit.activeLife) {
        const actor = unit.activeLife.actorId;
        if (actor.teamId !== unit.teamId || actor.unitId !== unit.unitId) {
          fail(`${path}.teams.units.activeLife`, 'actor/unit identity mismatch');
        }
        actorKeys.push(actorIdValue(actor));
      }
    }
  }
  if (new Set(actorKeys).size !== actorKeys.length) {
    fail(path, 'active actor identities must be unique');
  }
}

function ensureUnique<T>(
  values: readonly T[],
  key: (value: T) => string | number,
  path: string,
): void {
  const seen = new Set<string | number>();
  for (const value of values) {
    const itemKey = key(value);
    if (seen.has(itemKey)) fail(path, `duplicate identity ${itemKey}`);
    seen.add(itemKey);
  }
}

function actorIdValue(
  actor: Pick<V2.ReplayV2ActorId, 'teamId' | 'unitId' | 'lifeId'>,
): string {
  return `${actor.teamId}:${actor.unitId}:${actor.lifeId}`;
}

function sameStrings(left: readonly string[], right: readonly string[]): boolean {
  return (
    left.length === right.length &&
    left.every((value, index) => value === right[index])
  );
}

function compareParticipant(
  left:
    | V2.ReplayV2TopologyParticipant
    | V2.ReplayV2ParticipantController,
  right:
    | V2.ReplayV2TopologyParticipant
    | V2.ReplayV2ParticipantController,
): number {
  return left.participantId - right.participantId;
}

export function normalizeReplayV1(
  document: V1.ReplayV1Document,
): Model.ReplayModel {
  const participants = [...document.header.participants]
    .sort((left, right) => left.slot - right.slot)
    .map<Model.ReplayParticipantController>((participant) => ({
      participantKey: replayParticipantKey(participant.slot),
      participantId: participant.slot,
      teamKey: replayTeamKey(participant.slot),
      teamId: participant.slot,
      name: participant.name,
      runtimeKind: participant.runtimeKind,
      artifactHash: participant.artifactHash,
      accent: participant.accent,
      lookId: participant.lookId ?? null,
      projectileLookId: participant.projectileLookId ?? null,
    }));

  const units = participants.map<Model.ReplayStableUnit>((participant) => {
    const identity = replayDuelIdentity(participant.teamId);
    return {
      unitKey: identity.unitKey,
      teamKey: participant.teamKey,
      teamId: participant.teamId,
      unitId: 0,
      controllerParticipantKey: participant.participantKey,
      controllerParticipantId: participant.participantId,
      initialActorKey: identity.actorKey,
      initialLifeId: 0,
      initialFormId: 'legacy-mobile',
    };
  });

  const teams = participants.map<Model.ReplayTeam>((participant) => ({
    teamKey: participant.teamKey,
    teamId: participant.teamId,
    participantKeys: [participant.participantKey],
    unitKeys: [replayDuelIdentity(participant.teamId).unitKey],
  }));

  const maxHealth = v1MaxHealth(document);
  const forms: Model.ReplayForm[] = [
    {
      formId: 'legacy-mobile',
      maxHealth,
      visionRange: document.header.visionRange,
      shootCooldownTicks: null,
      omnidirectionalVision: document.header.visionCone !== true,
      omnidirectionalShooting: false,
      movementLayer: 'ground',
      objectiveWeight: 1,
      canMove: true,
      canShoot: true,
      allowsProgrammedShots: document.header.programmedShots === true,
      allowedActionIds: null,
      completeness: 'legacy-derived',
    },
  ];

  const map: Model.ReplayMap = {
    mapId: document.header.mapId,
    mapVersion: document.header.mapVersion,
    formatVersion: 1,
    width: document.header.mapWidth,
    height: document.header.mapHeight,
    tileRows: [...document.header.mapTiles],
    objectiveTiles: (document.header.zoneTiles ?? []).map(positionFromTuple),
    frontline: null,
    presentation:
      document.header.themeId === undefined &&
      document.header.presentation === undefined
        ? null
        : {
            themeId: document.header.themeId ?? null,
            boundaryWall:
              document.header.presentation?.boundaryWall ?? null,
            interiorWall:
              document.header.presentation?.interiorWall ?? null,
            wallGroups:
              document.header.presentation?.wallGroups
                .map((group) => ({
                  family: group.family,
                  tiles: group.tiles
                    .map(copyPosition)
                    .sort(comparePosition),
                }))
                .sort((left, right) =>
                  left.family.localeCompare(right.family),
                ) ?? null,
          },
  };

  const initialWorld = v1InitialWorld(document, maxHealth);
  const normalizedTicks: Model.ReplayTick[] = [];
  let before = initialWorld;
  const ticks = [...document.ticks].sort((left, right) => left.tick - right.tick);
  for (const tick of ticks) {
    const after = v1WorldAfterTick(document.header, tick);
    normalizedTicks.push(v1Tick(document.header, tick, before, after));
    before = after;
  }

  const terminal = document.result
    ? v1TerminalResult(
        document.header,
        document.result,
        normalizedTicks.at(-1)?.after ?? initialWorld,
      )
    : null;

  return {
    sourceVersion: 1,
    versions: {
      engineVersion: document.header.engineVersion,
      gameRulesVersion: document.header.gameRulesVersion,
      runtimeProtocolVersion: document.header.runtimeProtocolVersion,
      runtimeConfigurationVersion:
        document.header.runtimeConfigurationVersion,
      actorRuntime: null,
    },
    seed: String(document.header.seed),
    partial: 'partial' in document && document.partial === true,
    replayHash: document.replayHash ?? null,
    matchContractFingerprint: null,
    map,
    forms,
    participants,
    teams,
    units,
    initialWorld,
    ticks: normalizedTicks,
    result: terminal,
  };
}

function v1MaxHealth(document: V1.ReplayV1Document): number {
  if (document.header.maxHealth !== undefined) return document.header.maxHealth;
  const observed = document.ticks.flatMap((tick) =>
    tick.state.map((state) => state.health),
  );
  const final = document.result?.bots.map((bot) => bot.finalHealth) ?? [];
  return Math.max(3, ...observed, ...final);
}

function v1InitialWorld(
  document: V1.ReplayV1Document,
  maxHealth: number,
): Model.ReplayWorldSnapshot {
  const states: V1.ReplayV1BotState[] = document.header.participants.map(
    (participant) => ({
      slot: participant.slot,
      x: participant.spawnX,
      y: participant.spawnY,
      facing: participant.spawnFacing,
      health: maxHealth,
      cooldown: 0,
      status: 'Active',
    }),
  );
  return v1World(document.header, states, undefined, undefined);
}

function v1WorldAfterTick(
  header: V1.ReplayV1Header,
  tick: V1.ReplayV1Tick,
): Model.ReplayWorldSnapshot {
  const results = new Map(tick.bots.map((bot) => [bot.slot, bot.result]));
  return v1World(
    header,
    tick.state,
    tick.projectiles,
    tick.controlPressure,
    results,
  );
}

function v1World(
  header: V1.ReplayV1Header,
  states: readonly V1.ReplayV1BotState[],
  projectiles: readonly V1.ReplayV1Projectile[] | undefined,
  controlPressure: number | undefined,
  actionResults: ReadonlyMap<number, V1.ReplayV1ActionResult> = new Map(),
): Model.ReplayWorldSnapshot {
  const stateBySlot = new Map(states.map((state) => [state.slot, state]));
  const participantSlots = [...header.participants]
    .map((participant) => participant.slot)
    .sort(compareNumber);
  const actors = [...states]
    .sort((left, right) => left.slot - right.slot)
    .map<Model.ReplayActorState>((state) => {
      const identity = replayDuelIdentity(state.slot);
      return {
        identity,
        actorKey: identity.actorKey,
        unitKey: identity.unitKey,
        formId: 'legacy-mobile',
        position: { x: state.x, y: state.y },
        facing: directionFromV1(state.facing),
        health: state.health,
        cooldown: state.cooldown,
        energy: state.energy ?? null,
        damageDealt: null,
        previousActionResult: actionResultFromV1(
          actionResults.get(state.slot) ?? 'None',
        ),
        spawnedAtTick: 0,
        status: lifecycleFromV1(state.status),
      };
    });
  const units = participantSlots.map<Model.ReplayUnitState>((slot) => {
    const identity = replayDuelIdentity(slot);
    const state = stateBySlot.get(slot);
    return {
      unitKey: identity.unitKey,
      teamKey: replayTeamKey(slot),
      teamId: slot,
      unitId: 0,
      formId: 'legacy-mobile',
      lifecycleStatus: state ? lifecycleFromV1(state.status) : 'destroyed',
      respawnAtTick: null,
      damageDealt: null,
      activeActorKey: state ? identity.actorKey : null,
    };
  });
  const teams = participantSlots.map<Model.ReplayTeamState>((slot) => ({
    teamKey: replayTeamKey(slot),
    teamId: slot,
    damageDealt: null,
    unitKeys: [replayDuelIdentity(slot).unitKey],
  }));
  return {
    completeness: 'legacy-derived',
    teams,
    units,
    actors,
    projectiles:
      projectiles === undefined
        ? null
        : [...projectiles]
            .sort((left, right) => (left.id ?? 0) - (right.id ?? 0))
            .map(projectileFromV1),
    objective: legacyObjective(header, states, controlPressure),
  };
}

function legacyObjective(
  header: V1.ReplayV1Header,
  states: readonly V1.ReplayV1BotState[],
  controlPressure: number | undefined,
): Model.ReplayLegacyObjectiveState {
  const mode =
    header.controlPressureLimit !== undefined
      ? 'shared-pressure'
      : header.zoneTiles !== undefined
        ? 'zone-ticks'
        : 'none';
  return {
    kind: 'legacy',
    mode,
    controlPressure: controlPressure ?? (mode === 'shared-pressure' ? 0 : null),
    zoneTicks: [...states]
      .filter(
        (state): state is V1.ReplayV1BotState & { zoneTicks: number } =>
          state.zoneTicks !== undefined,
      )
      .sort((left, right) => left.slot - right.slot)
      .map((state) => ({
        unitKey: replayDuelIdentity(state.slot).unitKey,
        ticks: state.zoneTicks,
      })),
    completeness: 'legacy-derived',
  };
}

function projectileFromV1(
  projectile: V1.ReplayV1Projectile,
): Model.ReplayProjectileState {
  const owner = replayDuelIdentity(projectile.ownerSlot);
  return {
    projectileId: String(projectile.id ?? 0),
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    position: { x: projectile.x, y: projectile.y },
    launchDirection: directionFromV1(projectile.direction),
    heading: projectile.heading
      ? headingFromV1(projectile.heading)
      : directionFromV1(projectile.direction),
    shotProgram: null,
    programmedPath:
      projectile.programmedPath?.map(positionFromTuple) ?? null,
    ticksUntilAdvance: projectile.ticksUntilAdvance ?? null,
    remainingTiles: projectile.remainingTiles ?? null,
    tilesPerAdvance: projectile.tilesPerAdvance ?? null,
    nextProgrammedPathIndex: null,
    tilesTraveled: null,
    phase: null,
  };
}

function v1Tick(
  header: V1.ReplayV1Header,
  tick: V1.ReplayV1Tick,
  before: Model.ReplayWorldSnapshot,
  after: Model.ReplayWorldSnapshot,
): Model.ReplayTick {
  const actorByKey = new Map(
    before.actors.map((actor) => [actor.actorKey, actor]),
  );
  return {
    tick: tick.tick,
    before,
    activeActorKeys: before.actors
      .filter((actor) => actor.status === 'active')
      .map((actor) => actor.actorKey)
      .sort(),
    lifecycleEvents: [],
    actorTurns: [...tick.bots]
      .sort((left, right) => left.slot - right.slot)
      .map((bot) => v1ActorTurn(header, tick.tick, bot, actorByKey)),
    events: tick.events.map((event, ordinal) =>
      causalEventFromV1(tick.tick, ordinal, event),
    ),
    projectileTraversals: (tick.projectileTraversals ?? []).map(
      traversalFromV1,
    ),
    after,
  };
}

function v1ActorTurn(
  header: V1.ReplayV1Header,
  tick: number,
  turn: V1.ReplayV1BotTick,
  actors: ReadonlyMap<Model.ReplayActorLifeKey, Model.ReplayActorState>,
): Model.ReplayActorTurn {
  const identity = replayDuelIdentity(turn.slot);
  const selfState = actors.get(identity.actorKey);
  const self = selfState
    ? observedActorFromState(selfState, [identity.actorKey])
    : null;
  const unit: Model.ReplayObservedUnit = {
    unitKey: identity.unitKey,
    teamId: identity.teamId,
    unitId: 0,
    formId: 'legacy-mobile',
    lifecycleStatus: selfState?.status ?? 'destroyed',
    activeActor: selfState?.identity ?? null,
    respawnAtTick: null,
  };
  const submittedProgram = turn.shotProgram
    ? copyShotProgram(turn.shotProgram)
    : null;
  return {
    actor: identity,
    actorKey: identity.actorKey,
    observation: {
      completeness: 'legacy-partial',
      schemaVersion: null,
      tick,
      matchContractFingerprint: null,
      teamPerception: null,
      self,
      teamUnits: [unit],
      allies: [],
      enemies: [...turn.visibleEnemies]
        .sort((left, right) => left.slot - right.slot)
        .map((enemy) => {
          const enemyIdentity = replayDuelIdentity(enemy.slot);
          return {
            actor: { kind: 'exact', identity: enemyIdentity },
            formId: 'legacy-mobile',
            position: { x: enemy.x, y: enemy.y },
            facing: directionFromV1(enemy.facing),
            health: enemy.health,
            cooldown: null,
            energy: null,
            previousActionResult: null,
            observedBy: [identity.actorKey],
          };
        }),
      visibleTiles: turn.visibleTiles
        .map(positionFromTuple)
        .sort(comparePosition)
        .map((position) => ({
          position,
          isWall: header.mapTiles[position.y]?.[position.x] === '#',
          observedBy: [identity.actorKey],
        })),
      visibleProjectiles: null,
      visibleEvents: [],
      heardSounds:
        turn.heardSounds === undefined
          ? null
          : turn.heardSounds.map((sound) => ({
              eventHandle: null,
              sourceTick: null,
              observerActor: identity,
              type: eventTypeFromV1(sound.type),
              bearing: sound.bearing,
              distance: sound.distance,
            })),
      frontlineObjective: null,
      actions: null,
    },
    lifeStart:
      tick === 0
        ? {
            completeness: 'legacy-partial',
            schemaVersion: null,
            runtimeContractVersion: null,
            actor: identity,
            participantId: turn.slot,
            actorRandomSeed: null,
            spawnReason: 'legacy',
            matchContractFingerprint: null,
          }
        : null,
    aliases: {
      completeness: 'legacy-partial',
      enemyLives: [],
      projectiles: [],
      events: [],
    },
    runtimeReply: {
      actionId: actionIdFromV1(turn.chosenAction),
      actionCode: actionCodeFromV1(turn.chosenAction),
      payload: submittedProgram
        ? {
            shotProgram: submittedProgram,
            direction: null,
            unitKey: null,
            formTargetId: null,
          }
        : null,
      debugMessage: turn.debug ?? null,
      faulted: turn.faulted,
      faultMessage: turn.faulted ? (turn.debug ?? null) : null,
    },
    acceptedDecision: {
      actionId: actionIdFromV1(turn.chosenAction),
      actionCode: actionCodeFromV1(turn.chosenAction),
      payload: submittedProgram
        ? {
            shotProgram: submittedProgram,
            direction: null,
            unitKey: null,
            formTargetId: null,
          }
        : null,
      debugMessage: turn.debug ?? null,
      faulted: turn.faulted,
      faultMessage: turn.faulted ? (turn.debug ?? null) : null,
    },
    actionResolution: {
      chosenActionId: actionIdFromV1(turn.chosenAction),
      chosenActionCode: actionCodeFromV1(turn.chosenAction),
      chosenPayload: submittedProgram
        ? {
            shotProgram: submittedProgram,
            direction: null,
            unitKey: null,
            formTargetId: null,
          }
        : null,
      validatedActionId: actionIdFromV1(turn.validatedAction),
      validatedActionCode: actionCodeFromV1(turn.validatedAction),
      validatedPayload: submittedProgram
        ? {
            shotProgram: submittedProgram,
            direction: null,
            unitKey: null,
            formTargetId: null,
          }
        : null,
      result: actionResultFromV1(turn.result),
    },
  };
}

function causalEventFromV1(
  tick: number,
  ordinal: number,
  event: V1.ReplayV1GameEvent,
): Model.ReplayCausalEvent {
  const slotActor =
    event.slot === undefined ? null : replayDuelIdentity(event.slot);
  const explicitTargetSlot =
    event.targetSlot ?? (event.type === 'Shot' ? event.hitSlot : undefined);
  const explicitTarget =
    explicitTargetSlot === undefined
      ? null
      : replayDuelIdentity(explicitTargetSlot);
  const sourceActor =
    event.type === 'Destroyed' || event.type === 'Disqualified'
      ? null
      : slotActor;
  const targetActor =
    event.type === 'Destroyed' || event.type === 'Disqualified'
      ? slotActor
      : explicitTarget;
  return {
    eventId: `v1:${tick}:${ordinal}`,
    tick,
    ordinal,
    type: eventTypeFromV1(event.type),
    teamId: sourceActor?.teamId ?? targetActor?.teamId ?? null,
    sourceActor,
    targetActor,
    projectileId: null,
    from: optionalPosition(event.fromX, event.fromY),
    to: optionalPosition(event.toX, event.toY),
    fromFacing: event.fromFacing
      ? directionFromV1(event.fromFacing)
      : null,
    toFacing: event.toFacing ? directionFromV1(event.toFacing) : null,
    projectileHeading: null,
    actionPayload: null,
    actionId: null,
    actionCode: null,
    actionResult: null,
    amount: event.amount ?? null,
    newHealth: event.newHealth ?? null,
    lifecycleStatus:
      event.type === 'Destroyed'
        ? 'destroyed'
        : event.type === 'Disqualified'
          ? 'disqualified'
          : null,
    respawnAtTick: null,
    fromPositionIndex: null,
    toPositionIndex: null,
    claimingTeamId: null,
    captureProgress: null,
    controlResumesAtTick: null,
    completeness: 'legacy-partial',
  };
}

function traversalFromV1(
  traversal: V1.ReplayV1ProjectileTraversal,
): Model.ReplayProjectileTraversal {
  const owner = replayDuelIdentity(traversal.ownerSlot);
  return {
    projectileId: String(traversal.id),
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    launchDirection: directionFromV1(traversal.direction),
    from: { x: traversal.fromX, y: traversal.fromY },
    path: traversal.path.map(positionFromTuple),
    heading: traversal.heading
      ? headingFromV1(traversal.heading)
      : directionFromV1(traversal.direction),
    shotProgram: null,
    programmedPath:
      traversal.programmedPath?.map(positionFromTuple) ?? null,
  };
}

function v1TerminalResult(
  header: V1.ReplayV1Header,
  result: V1.ReplayV1MatchResult,
  lastWorld: Model.ReplayWorldSnapshot,
): Model.ReplayTerminalResult {
  const objective: Model.ReplayLegacyObjectiveState = {
    kind: 'legacy',
    mode:
      header.controlPressureLimit !== undefined
        ? 'shared-pressure'
        : header.zoneTiles !== undefined
          ? 'zone-ticks'
          : 'none',
    controlPressure:
      result.controlPressure ??
      (lastWorld.objective.kind === 'legacy'
        ? lastWorld.objective.controlPressure
        : null),
    zoneTicks: [...result.bots]
      .filter(
        (bot): bot is V1.ReplayV1BotMatchResult & { zoneTicks: number } =>
          bot.zoneTicks !== undefined,
      )
      .sort((left, right) => left.slot - right.slot)
      .map((bot) => ({
        unitKey: replayDuelIdentity(bot.slot).unitKey,
        ticks: bot.zoneTicks,
      })),
    completeness: 'legacy-derived',
  };
  return {
    winnerTeamId: result.winnerSlot ?? null,
    reason: matchReasonFromV1(result.reason),
    endTick: result.endTick,
    territorialScore: null,
    objective,
    teams: [...result.bots]
      .sort((left, right) => left.slot - right.slot)
      .map((bot) => ({
        teamKey: replayTeamKey(bot.slot),
        teamId: bot.slot,
        outcome: bot.outcome.toLowerCase() as 'win' | 'loss' | 'draw',
        finalHealth: bot.finalHealth,
        damageDealt: String(bot.damageDealt),
        finalLifecycleStatus: lifecycleFromV1(bot.finalStatus),
        faults: bot.faults,
        zoneTicks: bot.zoneTicks ?? null,
      })),
  };
}

function observedActorFromState(
  state: Model.ReplayActorState,
  observedBy: Model.ReplayActorLifeKey[],
): Model.ReplayObservedActor {
  return {
    actor: { kind: 'exact', identity: state.identity },
    formId: state.formId,
    position: copyPosition(state.position),
    facing: state.facing,
    health: state.health,
    cooldown: state.cooldown,
    energy: state.energy,
    previousActionResult: state.previousActionResult,
    observedBy,
  };
}

function optionalPosition(
  x: number | undefined,
  y: number | undefined,
): Model.ReplayPosition | null {
  return x === undefined || y === undefined ? null : { x, y };
}

function positionFromTuple(
  position: readonly [number, number],
): Model.ReplayPosition {
  return { x: position[0], y: position[1] };
}

function copyPosition(
  position: Readonly<Model.ReplayPosition>,
): Model.ReplayPosition {
  return { x: position.x, y: position.y };
}

function comparePosition(
  left: Readonly<Model.ReplayPosition>,
  right: Readonly<Model.ReplayPosition>,
): number {
  return left.y - right.y || left.x - right.x;
}

function compareNumber(left: number, right: number): number {
  return left - right;
}

function directionFromV1(
  direction: V1.ReplayV1Direction,
): Model.ReplayDirection {
  switch (direction) {
    case 'North':
      return 'north';
    case 'East':
      return 'east';
    case 'South':
      return 'south';
    case 'West':
      return 'west';
  }
}

function headingFromV1(
  heading: V1.ReplayV1ProjectileHeading,
): Model.ReplayProjectileHeading {
  switch (heading) {
    case 'North':
      return 'north';
    case 'NorthEast':
      return 'north-east';
    case 'East':
      return 'east';
    case 'SouthEast':
      return 'south-east';
    case 'South':
      return 'south';
    case 'SouthWest':
      return 'south-west';
    case 'West':
      return 'west';
    case 'NorthWest':
      return 'north-west';
  }
}

function actionResultFromV1(
  result: V1.ReplayV1ActionResult,
): Model.ReplayActionResult {
  switch (result) {
    case 'None':
      return 'none';
    case 'Success':
      return 'success';
    case 'Blocked':
      return 'blocked';
    case 'OnCooldown':
      return 'on-cooldown';
    case 'Faulted':
      return 'faulted';
  }
}

function lifecycleFromV1(
  status: V1.ReplayV1BotStatus,
): Model.ReplayUnitLifecycleStatus {
  switch (status) {
    case 'Active':
      return 'active';
    case 'Destroyed':
      return 'destroyed';
    case 'Disqualified':
      return 'disqualified';
  }
}

function actionIdFromV1(action: V1.ReplayV1BotAction): string {
  switch (action) {
    case 'Wait':
      return 'wait';
    case 'MoveForward':
      return 'move-forward';
    case 'TurnLeft':
      return 'turn-left';
    case 'TurnRight':
      return 'turn-right';
    case 'Shoot':
      return 'shoot';
    case 'StrafeLeft':
      return 'strafe-left';
    case 'StrafeRight':
      return 'strafe-right';
  }
}

function actionCodeFromV1(action: V1.ReplayV1BotAction): number {
  switch (action) {
    case 'Wait':
      return 0;
    case 'MoveForward':
      return 1;
    case 'TurnLeft':
      return 2;
    case 'TurnRight':
      return 3;
    case 'Shoot':
      return 4;
    case 'StrafeLeft':
      return 5;
    case 'StrafeRight':
      return 6;
  }
}

function eventTypeFromV1(event: V1.ReplayV1GameEventType): string {
  switch (event) {
    case 'Turn':
      return 'turn';
    case 'Move':
      return 'move';
    case 'MoveBlocked':
      return 'move-blocked';
    case 'Shot':
      return 'shot';
    case 'Damage':
      return 'damage';
    case 'Destroyed':
      return 'destroyed';
    case 'Fault':
      return 'fault';
    case 'Disqualified':
      return 'disqualified';
  }
}

function matchReasonFromV1(reason: V1.ReplayV1MatchResult['reason']): string {
  switch (reason) {
    case 'Elimination':
      return 'elimination';
    case 'Disqualification':
      return 'disqualification';
    case 'MaxTicks':
      return 'max-ticks';
    case 'Domination':
      return 'domination';
  }
}

function copyShotProgram(
  program: V1.ReplayV1ShotProgram | V2.ReplayV2ShotProgram,
): Model.ReplayShotProgram {
  return {
    initialAimOffset: program.initialAimOffset,
    bendDirection: program.bendDirection,
    bendAfterTiles: program.bendAfterTiles,
    bendEveryTiles: program.bendEveryTiles,
    bendCount: program.bendCount,
  };
}

export function normalizeReplayV2(
  document: V2.ReplayV2Document,
): Model.ReplayModel {
  const { contract } = document.header;
  const participants = [...document.header.participants]
    .sort(compareParticipant)
    .map<Model.ReplayParticipantController>((participant) => ({
      participantKey: replayParticipantKey(participant.participantId),
      participantId: participant.participantId,
      teamKey: replayTeamKey(participant.teamId),
      teamId: participant.teamId,
      name: participant.name,
      runtimeKind: participant.runtimeKind,
      artifactHash: participant.artifactHash,
      accent: participant.accent,
      lookId: participant.lookId,
      projectileLookId: participant.projectileLookId,
    }));

  const initialLifeByUnit = new Map(
    contract.topology.initialLives.map((life) => [
      `${life.teamId}:${life.unitId}`,
      life,
    ]),
  );
  const units = [...contract.topology.unitSlots]
    .sort(compareUnitIdentity)
    .map<Model.ReplayStableUnit>((unit) => {
      const initialLife = initialLifeByUnit.get(
        `${unit.teamId}:${unit.unitId}`,
      );
      const initialIdentity = initialLife
        ? replayFrontlineIdentity(
            initialLife.teamId,
            initialLife.unitId,
            initialLife.lifeId,
          )
        : null;
      return {
        unitKey: frontlineUnitKey(unit.teamId, unit.unitId),
        teamKey: replayTeamKey(unit.teamId),
        teamId: unit.teamId,
        unitId: unit.unitId,
        controllerParticipantKey: replayParticipantKey(
          unit.controllerParticipantId,
        ),
        controllerParticipantId: unit.controllerParticipantId,
        initialActorKey: initialIdentity?.actorKey ?? null,
        initialLifeId: initialLife?.lifeId ?? null,
        initialFormId: initialLife?.formId ?? null,
      };
    });

  const teams = [...contract.topology.teams]
    .sort((left, right) => left.teamId - right.teamId)
    .map<Model.ReplayTeam>((team) => ({
      teamKey: replayTeamKey(team.teamId),
      teamId: team.teamId,
      participantKeys: participants
        .filter((participant) => participant.teamId === team.teamId)
        .map((participant) => participant.participantKey),
      unitKeys: units
        .filter((unit) => unit.teamId === team.teamId)
        .map((unit) => unit.unitKey),
    }));

  const forms = [...contract.rules.forms]
    .sort((left, right) => left.id.localeCompare(right.id))
    .map<Model.ReplayForm>((form) => ({
      formId: form.id,
      maxHealth: form.maxHealth,
      visionRange: form.visionRange,
      shootCooldownTicks: form.shootCooldownTicks,
      omnidirectionalVision: form.omnidirectionalVision,
      omnidirectionalShooting: form.omnidirectionalShooting,
      movementLayer: form.movementLayer,
      objectiveWeight: form.objectiveWeight,
      canMove: form.canMove,
      canShoot: form.canShoot,
      allowsProgrammedShots: form.allowsProgrammedShots,
      allowedActionIds: [...form.allowedActionIds].sort(),
      completeness: 'exact',
    }));

  const normalizedTicks = [...document.ticks]
    .sort((left, right) => left.tick - right.tick)
    .map(tickFromV2);
  const initialWorld = normalizedTicks[0]?.before ?? null;

  return {
    sourceVersion: 2,
    versions: {
      engineVersion: document.header.engineVersion,
      gameRulesVersion: document.header.gameRulesVersion,
      runtimeProtocolVersion: null,
      runtimeConfigurationVersion: null,
      actorRuntime: { ...document.header.actorRuntime },
    },
    seed: document.header.seed,
    partial: document.partial,
    replayHash: document.replayHash,
    matchContractFingerprint: contract.matchContractFingerprint,
    map: mapFromV2(document.header),
    forms,
    participants,
    teams,
    units,
    initialWorld,
    ticks: normalizedTicks,
    result: document.result ? resultFromV2(document.result) : null,
  };
}

function mapFromV2(header: V2.ReplayV2Header): Model.ReplayMap {
  const map = header.contract.map;
  return {
    mapId: map.mapId,
    mapVersion: map.mapVersion,
    formatVersion: map.formatVersion,
    width: map.width,
    height: map.height,
    tileRows: [...map.tileRows],
    objectiveTiles: map.objectiveTiles.map(positionFromTuple),
    frontline: map.frontline
      ? {
          positions: [...map.frontline.positions]
            .sort(
              (left, right) => left.positionIndex - right.positionIndex,
            )
            .map((position) => ({
              positionIndex: position.positionIndex,
              tiles: position.tiles
                .map(positionFromTuple)
                .sort(comparePosition),
            })),
          teamHomes: [...map.frontline.teamHomes]
            .sort((left, right) => left.teamId - right.teamId)
            .map((home) => ({
              teamId: home.teamId,
              primeSpawn: {
                x: home.primeSpawn.x,
                y: home.primeSpawn.y,
                facing: home.primeSpawn.facing,
              },
              protectedSpawnPad: home.protectedSpawnPad
                .map(positionFromTuple)
                .sort(comparePosition),
            })),
          anchorForbiddenTiles: map.frontline.anchorForbiddenTiles
            .map(positionFromTuple)
            .sort(comparePosition),
        }
      : null,
    presentation: header.presentation
      ? {
          themeId: header.presentation.themeId,
          boundaryWall: header.presentation.map?.boundaryWall ?? null,
          interiorWall: header.presentation.map?.interiorWall ?? null,
          wallGroups:
            header.presentation.map?.wallGroups
              .map((group) => ({
                family: group.family,
                tiles: group.tiles.map(copyPosition).sort(comparePosition),
              }))
              .sort((left, right) =>
                left.family.localeCompare(right.family),
              ) ?? null,
        }
      : null,
  };
}

function tickFromV2(tick: V2.ReplayV2Tick): Model.ReplayTick {
  return {
    tick: tick.tick,
    before: worldFromV2(tick.tickStart.state),
    activeActorKeys: [...tick.tickStart.activeActors]
      .sort(compareActorIdentity)
      .map((actor) => actorIdentityFromV2(actor).actorKey),
    lifecycleEvents: tick.tickStart.lifecycleEvents.map((event, ordinal) =>
      eventFromV2(event, ordinal),
    ),
    actorTurns: [...tick.actors]
      .sort((left, right) =>
        compareActorIdentity(left.actorId, right.actorId),
      )
      .map(actorTurnFromV2),
    events: tick.resolution.events.map((event, ordinal) =>
      eventFromV2(event, ordinal),
    ),
    projectileTraversals: tick.resolution.projectileTraversals.map(
      traversalFromV2,
    ),
    after: worldFromV2(tick.postState),
  };
}

function worldFromV2(
  world: V2.ReplayV2WorldState,
): Model.ReplayWorldSnapshot {
  const sortedTeams = [...world.teams].sort(
    (left, right) => left.teamId - right.teamId,
  );
  const units = sortedTeams.flatMap((team) =>
    [...team.units]
      .sort((left, right) => left.unitId - right.unitId)
      .map<Model.ReplayUnitState>((unit) => ({
        unitKey: frontlineUnitKey(unit.teamId, unit.unitId),
        teamKey: replayTeamKey(unit.teamId),
        teamId: unit.teamId,
        unitId: unit.unitId,
        formId: unit.formId,
        lifecycleStatus: unit.lifecycleStatus,
        respawnAtTick: unit.respawnAtTick,
        damageDealt: unit.damageDealt,
        activeActorKey: unit.activeLife
          ? actorIdentityFromV2(unit.activeLife.actorId).actorKey
          : null,
      })),
  );
  const actors = sortedTeams
    .flatMap((team) => team.units)
    .flatMap((unit) =>
      unit.activeLife ? [actorStateFromV2(unit, unit.activeLife)] : [],
    )
    .sort((left, right) =>
      compareModelActorIdentity(left.identity, right.identity),
    );
  return {
    completeness: 'exact',
    teams: sortedTeams.map((team) => ({
      teamKey: replayTeamKey(team.teamId),
      teamId: team.teamId,
      damageDealt: team.damageDealt,
      unitKeys: [...team.units]
        .sort((left, right) => left.unitId - right.unitId)
        .map((unit) => frontlineUnitKey(unit.teamId, unit.unitId)),
    })),
    units,
    actors,
    projectiles: [...world.projectiles]
      .sort((left, right) =>
        compareDecimalStrings(left.projectileId, right.projectileId),
      )
      .map(projectileFromV2),
    objective: objectiveFromV2(world.objective),
  };
}

function actorStateFromV2(
  unit: V2.ReplayV2UnitState,
  life: V2.ReplayV2LifeState,
): Model.ReplayActorState {
  const identity = actorIdentityFromV2(life.actorId);
  return {
    identity,
    actorKey: identity.actorKey,
    unitKey: identity.unitKey,
    formId: unit.formId,
    position: copyPosition(life.position),
    facing: life.facing,
    health: life.health,
    cooldown: life.cooldown,
    energy: life.energy,
    damageDealt: life.damageDealt,
    previousActionResult: life.previousActionResult,
    spawnedAtTick: life.spawnedAtTick,
    status: unit.lifecycleStatus,
  };
}

function projectileFromV2(
  projectile: V2.ReplayV2ProjectileState,
): Model.ReplayProjectileState {
  const owner = actorIdentityFromV2(projectile.ownerActorId);
  return {
    projectileId: projectile.projectileId,
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    position: copyPosition(projectile.position),
    launchDirection: projectile.launchDirection,
    heading: projectile.heading,
    shotProgram: projectile.shotProgram
      ? copyShotProgram(projectile.shotProgram)
      : null,
    programmedPath:
      projectile.programmedPath?.map(copyPosition) ?? null,
    ticksUntilAdvance: null,
    remainingTiles: null,
    tilesPerAdvance: null,
    nextProgrammedPathIndex: projectile.nextProgrammedPathIndex,
    tilesTraveled: projectile.tilesTraveled,
    phase: projectile.phase,
  };
}

function objectiveFromV2(
  objective: V2.ReplayV2ControlState,
): Model.ReplayFrontlineObjectiveState {
  return {
    kind: 'frontline',
    nextTick: objective.nextTick,
    activePositionIndex: objective.activePositionIndex,
    claimingTeamId: objective.claimingTeamId,
    captureProgress: objective.captureProgress,
    decayTicksElapsed: objective.decayTicksElapsed,
    controlResumesAtTick: objective.controlResumesAtTick,
    winnerTeamId: objective.winnerTeamId,
    completeness: 'exact',
  };
}

function actorTurnFromV2(
  turn: V2.ReplayV2ActorTurn,
): Model.ReplayActorTurn {
  const actor = actorIdentityFromV2(turn.actorId);
  return {
    actor,
    actorKey: actor.actorKey,
    lifeStart: turn.lifeStart
      ? {
          completeness: 'exact',
          schemaVersion: turn.lifeStart.schemaVersion,
          runtimeContractVersion: turn.lifeStart.runtimeContractVersion,
          actor: actorIdentityFromV2(turn.lifeStart.actorId),
          participantId: turn.lifeStart.participantId,
          actorRandomSeed: turn.lifeStart.actorRandomSeed,
          spawnReason: turn.lifeStart.spawnReason,
          matchContractFingerprint:
            turn.lifeStart.matchContractFingerprint,
        }
      : null,
    observation: observationFromV2(turn.observation),
    aliases: aliasesFromV2(turn.aliases),
    runtimeReply: actorDecisionFromV2(turn.runtimeReply),
    acceptedDecision: actorDecisionFromV2(turn.acceptedDecision),
    actionResolution: {
      chosenActionId: turn.actionResolution.chosenActionId,
      chosenActionCode: turn.actionResolution.chosenActionCode,
      chosenPayload: actionPayloadFromV2(
        turn.actionResolution.chosenPayload,
      ),
      validatedActionId: turn.actionResolution.validatedActionId,
      validatedActionCode: turn.actionResolution.validatedActionCode,
      validatedPayload: actionPayloadFromV2(
        turn.actionResolution.validatedPayload,
      ),
      result: turn.actionResolution.result,
    },
  };
}

function aliasesFromV2(
  aliases: V2.ReplayV2ObservationAliases,
): Model.ReplayObservationAliases {
  return {
    completeness: 'exact',
    enemyLives: [...aliases.enemyLives]
      .sort((left, right) =>
        compareAliasHandles(left.lifeHandle, right.lifeHandle),
      )
      .map((alias) => ({
        lifeHandle: alias.lifeHandle,
        actor: actorIdentityFromV2(alias.actorId),
      })),
    projectiles: [...aliases.projectiles]
      .sort((left, right) =>
        compareAliasHandles(
          left.projectileHandle,
          right.projectileHandle,
        ),
      )
      .map((alias) => ({ ...alias })),
    events: [...aliases.events]
      .sort((left, right) =>
        compareAliasHandles(left.eventHandle, right.eventHandle),
      )
      .map((alias) => ({ ...alias })),
  };
}

function actorDecisionFromV2(
  decision: V2.ReplayV2ActorDecision,
): Model.ReplayActorDecision {
  return {
    actionId: decision.actionId,
    actionCode: decision.actionCode,
    payload: actionPayloadFromV2(decision.payload),
    debugMessage: decision.debugMessage,
    faulted: decision.faulted,
    faultMessage: decision.faultMessage,
  };
}

function actionPayloadFromV2(
  payload: V2.ReplayV2ActionPayload | null,
): Model.ReplayActionPayload | null {
  return payload
    ? {
        shotProgram: payload.shotProgram
          ? copyShotProgram(payload.shotProgram)
          : null,
        direction: payload.direction,
        unitKey: payload.unitTarget
          ? frontlineUnitKey(
              payload.unitTarget.teamId,
              payload.unitTarget.unitId,
            )
          : null,
        formTargetId: payload.formTargetId,
      }
    : null;
}

function observationFromV2(
  observation: V2.ReplayV2ActorObservation,
): Model.ReplayActorObservation {
  const observer = actorIdentityFromV2(observation.self.actorId);
  return {
    completeness: 'exact',
    schemaVersion: observation.schemaVersion,
    tick: observation.tick,
    matchContractFingerprint: observation.matchContractFingerprint,
    teamPerception: observation.teamPerception,
    self: observedSelfFromV2(observation.self, [observer.actorKey]),
    teamUnits: [...observation.teamUnits]
      .sort(compareUnitIdentity)
      .map((unit) => ({
        unitKey: frontlineUnitKey(unit.teamId, unit.unitId),
        teamId: unit.teamId,
        unitId: unit.unitId,
        formId: unit.formId,
        lifecycleStatus: unit.lifecycleStatus,
        activeActor: unit.activeActorId
          ? actorIdentityFromV2(unit.activeActorId)
          : null,
        respawnAtTick: unit.respawnAtTick,
      })),
    allies: [...observation.allies]
      .sort((left, right) =>
        compareActorIdentity(left.actorId, right.actorId),
      )
      .map((ally) => observedSelfFromV2(ally, [])),
    enemies: [...observation.enemies]
      .sort((left, right) =>
        compareOpaqueEnemyActor(left.actor, right.actor),
      )
      .map((enemy) => ({
        actor: opaqueEnemyActorFromV2(enemy.actor),
        formId: enemy.formId,
        position: copyPosition(enemy.position),
        facing: enemy.facing,
        health: enemy.health,
        cooldown: null,
        energy: null,
        previousActionResult: null,
        observedBy: [...enemy.observedBy]
          .sort(compareActorIdentity)
          .map((actor) => actorIdentityFromV2(actor).actorKey),
      })),
    visibleTiles: [...observation.visibleTiles]
      .sort((left, right) =>
        comparePosition(left.position, right.position),
      )
      .map((tile) => ({
        position: copyPosition(tile.position),
        isWall: tile.isWall,
        observedBy: [...tile.observedBy]
          .sort(compareActorIdentity)
          .map((actor) => actorIdentityFromV2(actor).actorKey),
      })),
    visibleProjectiles:
      observation.visibleProjectiles === null
        ? null
        : [...observation.visibleProjectiles]
            .sort((left, right) =>
              compareAliasHandles(
                left.projectileHandle,
                right.projectileHandle,
              ),
            )
            .map((projectile) => ({
              projectileHandle: projectile.projectileHandle,
              ownerTeamId: projectile.ownerTeamId,
              alliedOwnerActor: projectile.alliedOwnerActorId
                ? actorIdentityFromV2(projectile.alliedOwnerActorId)
                : null,
              visibleEnemyOwner: projectile.visibleEnemyOwner
                ? opaqueEnemyActorFromV2(projectile.visibleEnemyOwner)
                : null,
              position: copyPosition(projectile.position),
              heading: projectile.heading,
              tilesPerAdvance: projectile.tilesPerAdvance,
              ticksUntilAdvance: projectile.ticksUntilAdvance,
              remainingTiles: projectile.remainingTiles,
              observedBy: [...projectile.observedBy]
                .sort(compareActorIdentity)
                .map((actor) => actorIdentityFromV2(actor).actorKey),
            })),
    visibleEvents: [...observation.visibleEvents]
      .sort(
        (left, right) =>
          left.sourceTick - right.sourceTick ||
          compareAliasHandles(left.eventHandle, right.eventHandle),
      )
      .map((event) => ({
        eventHandle: event.eventHandle,
        sourceTick: event.sourceTick,
        type: event.type,
        teamId: event.teamId,
        alliedActor: event.alliedActorId
          ? actorIdentityFromV2(event.alliedActorId)
          : null,
        enemyActor: event.enemyActor
          ? opaqueEnemyActorFromV2(event.enemyActor)
          : null,
        projectileHandle: event.projectileHandle,
        position: event.position ? copyPosition(event.position) : null,
        facing: event.facing,
        amount: event.amount,
        newHealth: event.newHealth,
        observedBy: [...event.observedBy]
          .sort(compareActorIdentity)
          .map((actor) => actorIdentityFromV2(actor).actorKey),
      })),
    heardSounds:
      observation.heardSounds === null
        ? null
        : [...observation.heardSounds]
            .sort(
              (left, right) =>
                left.sourceTick - right.sourceTick ||
                compareAliasHandles(
                  left.eventHandle,
                  right.eventHandle,
                ) ||
                compareActorIdentity(
                  left.observerActorId,
                  right.observerActorId,
                ),
            )
            .map((sound) => ({
              eventHandle: sound.eventHandle,
              sourceTick: sound.sourceTick,
              observerActor: actorIdentityFromV2(
                sound.observerActorId,
              ),
              type: sound.type,
              bearing: sound.bearing,
              distance: sound.distance,
            })),
    frontlineObjective: observation.frontlineObjective
      ? {
          activePositionIndex:
            observation.frontlineObjective.activePositionIndex,
          claimingTeamId: observation.frontlineObjective.claimingTeamId,
          captureProgress: observation.frontlineObjective.captureProgress,
          decayTicksElapsed:
            observation.frontlineObjective.decayTicksElapsed,
          controlResumesAtTick:
            observation.frontlineObjective.controlResumesAtTick,
        }
      : null,
    actions: [...observation.actions]
      .sort(
        (left, right) =>
          left.actionCode - right.actionCode ||
          left.actionId.localeCompare(right.actionId),
      )
      .map((action) => ({
        actionId: action.actionId,
        actionCode: action.actionCode,
        parameterKinds: [...action.parameterKinds],
        enabled: action.enabled,
        available: action.available,
        shotProgramAvailable: action.shotProgramAvailable,
        allowedDirections:
          action.allowedDirections === null
            ? null
            : [...action.allowedDirections],
        allowedUnitKeys:
          action.allowedUnitTargets === null
            ? null
            : [...action.allowedUnitTargets]
                .sort(compareUnitIdentity)
                .map((unit) => frontlineUnitKey(unit.teamId, unit.unitId)),
        allowedFormTargets:
          action.allowedFormTargets === null
            ? null
            : [...action.allowedFormTargets].sort(),
      })),
  };
}

function observedSelfFromV2(
  observed: V2.ReplayV2ObservedSelf | V2.ReplayV2ObservedAlly,
  observedBy: Model.ReplayActorLifeKey[],
): Model.ReplayObservedActor {
  return {
    actor: {
      kind: 'exact',
      identity: actorIdentityFromV2(observed.actorId),
    },
    formId: observed.formId,
    position: copyPosition(observed.position),
    facing: observed.facing,
    health: observed.health,
    cooldown: observed.cooldown,
    energy: observed.energy,
    previousActionResult: observed.previousActionResult,
    observedBy,
  };
}

function eventFromV2(
  event: V2.ReplayV2Event,
  ordinal: number,
): Model.ReplayCausalEvent {
  return {
    eventId: event.eventId,
    tick: event.tick,
    ordinal,
    type: event.type,
    teamId: event.teamId,
    sourceActor: event.sourceActorId
      ? actorIdentityFromV2(event.sourceActorId)
      : null,
    targetActor: event.targetActorId
      ? actorIdentityFromV2(event.targetActorId)
      : null,
    projectileId: event.projectileId,
    from: event.from ? copyPosition(event.from) : null,
    to: event.to ? copyPosition(event.to) : null,
    fromFacing: event.fromFacing,
    toFacing: event.toFacing,
    projectileHeading: event.projectileHeading,
    actionPayload: actionPayloadFromV2(event.actionPayload),
    actionId: event.actionId,
    actionCode: event.actionCode,
    actionResult: event.actionResult,
    amount: event.amount,
    newHealth: event.newHealth,
    lifecycleStatus: event.lifecycleStatus,
    respawnAtTick: event.respawnAtTick,
    fromPositionIndex: event.fromPositionIndex,
    toPositionIndex: event.toPositionIndex,
    claimingTeamId: event.claimingTeamId,
    captureProgress: event.captureProgress,
    controlResumesAtTick: event.controlResumesAtTick,
    completeness: 'exact',
  };
}

function traversalFromV2(
  traversal: V2.ReplayV2ProjectileTraversal,
): Model.ReplayProjectileTraversal {
  const owner = actorIdentityFromV2(traversal.ownerActorId);
  return {
    projectileId: traversal.projectileId,
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    launchDirection: traversal.launchDirection,
    from: copyPosition(traversal.from),
    path: traversal.path.map(copyPosition),
    heading: traversal.heading,
    shotProgram: traversal.shotProgram
      ? copyShotProgram(traversal.shotProgram)
      : null,
    programmedPath:
      traversal.programmedPath?.map(copyPosition) ?? null,
  };
}

function resultFromV2(result: V2.ReplayV2Result): Model.ReplayTerminalResult {
  return {
    winnerTeamId: result.winnerTeamId,
    reason: result.reason,
    endTick: result.endTick,
    territorialScore: result.territorialScore,
    objective: objectiveFromV2(result.objective),
    teams: [...result.teams]
      .sort((left, right) => left.teamId - right.teamId)
      .map((team) => ({
        teamKey: replayTeamKey(team.teamId),
        teamId: team.teamId,
        outcome: team.outcome,
        finalHealth: team.finalHealth,
        damageDealt: team.damageDealt,
        finalLifecycleStatus: team.finalLifecycleStatus,
        faults: null,
        zoneTicks: null,
      })),
  };
}

function actorIdentityFromV2(
  actor: V2.ReplayV2ActorId,
): Model.ReplayFrontlineActorIdentity {
  return replayFrontlineIdentity(actor.teamId, actor.unitId, actor.lifeId);
}

function opaqueEnemyActorFromV2(
  actor: V2.ReplayV2ObservedEnemyActorRef,
): Model.ReplayOpaqueEnemyActorRef {
  return {
    kind: 'opaque-enemy',
    teamId: actor.teamId,
    unitId: actor.unitId,
    lifeHandle: actor.lifeHandle,
  };
}

function frontlineUnitKey(
  teamId: number,
  unitId: number,
): Model.ReplayStableUnitKey {
  return replayFrontlineIdentity(teamId, unitId, 0).unitKey;
}

function compareActorIdentity(
  left: V2.ReplayV2ActorId,
  right: V2.ReplayV2ActorId,
): number {
  return (
    left.teamId - right.teamId ||
    left.unitId - right.unitId ||
    left.lifeId - right.lifeId
  );
}

function compareOpaqueEnemyActor(
  left: V2.ReplayV2ObservedEnemyActorRef,
  right: V2.ReplayV2ObservedEnemyActorRef,
): number {
  return (
    left.teamId - right.teamId ||
    left.unitId - right.unitId ||
    compareAliasHandles(left.lifeHandle, right.lifeHandle)
  );
}

function compareModelActorIdentity(
  left: Model.ReplayActorIdentity,
  right: Model.ReplayActorIdentity,
): number {
  return (
    left.teamId - right.teamId ||
    left.unitId - right.unitId ||
    left.lifeId - right.lifeId
  );
}

function compareUnitIdentity(
  left: Pick<V2.ReplayV2ObservedUnitTarget, 'teamId' | 'unitId'>,
  right: Pick<V2.ReplayV2ObservedUnitTarget, 'teamId' | 'unitId'>,
): number {
  return left.teamId - right.teamId || left.unitId - right.unitId;
}

function compareDecimalStrings(left: string, right: string): number {
  const leftValue = BigInt(left);
  const rightValue = BigInt(right);
  return leftValue < rightValue ? -1 : leftValue > rightValue ? 1 : 0;
}

function compareAliasHandles(left: string, right: string): number {
  const leftOrdinal = Number(left.slice(left.lastIndexOf('-') + 1));
  const rightOrdinal = Number(right.slice(right.lastIndexOf('-') + 1));
  return leftOrdinal - rightOrdinal;
}
