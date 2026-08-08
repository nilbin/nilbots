import {
  replayDuelIdentity,
  replayFrontlineIdentity,
  replayParticipantKey,
  replayTeamKey,
} from './replayModel';
import type * as Model from './replayModel';
import type * as V1 from './replayWireV1';
import type * as V2 from './replayWireV2';
import type * as V3 from './replayWireV3';
import {
  normalizeReplayV3,
  validateReplayV3,
} from './replayV3Normalize';

export type ReplayWireDocument =
  | V1.ReplayV1Document
  | V2.ReplayV2Document
  | V3.ReplayV3Document;

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
    }
  | {
      replayVersion: 3;
      /** The validated input object, retained by identity and never mutated. */
      wire: V3.ReplayV3Document;
      replay: Model.ReplayModel;
    };

export type DecodedReplayJson = DecodedReplay & {
  /** Original JSON text, retained unchanged for upstream hash verification. */
  rawJson: string;
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
  return decodeReplayInternal(input);
}

/**
 * Parses, validates, and normalizes replay JSON while retaining lexical values
 * that JSON.parse cannot represent exactly. Replay-v1 encoded its ulong seed as
 * a JSON number; this is the lossless ingress for those historical documents.
 */
export function decodeReplayJson(json: string): DecodedReplayJson {
  if (typeof json !== 'string') {
    throw new ReplayDecodeError('replay: expected JSON text');
  }

  let input: unknown;
  try {
    input = JSON.parse(json) as unknown;
  } catch (error) {
    const detail = error instanceof Error ? ` (${error.message})` : '';
    throw new ReplayDecodeError(`replay: invalid JSON${detail}`);
  }

  rejectDuplicateJsonProperties(json);
  const version = replayVersionOf(input);
  const v1Seed =
    version === 1
      ? {
          decimal: replayV1SeedLexeme(json),
          exact: true as const,
        }
      : undefined;
  return {
    ...decodeReplayInternal(input, v1Seed),
    rawJson: json,
  };
}

export function normalizeReplayJson(json: string): Model.ReplayModel {
  return decodeReplayJson(json).replay;
}

function decodeReplayInternal(
  input: unknown,
  v1Seed?: V1SeedNormalization,
): DecodedReplay {
  const version = replayVersionOf(input);
  if (version === 1) {
    validateReplayV1(input);
    const wire = input;
    return {
      replayVersion: 1,
      wire,
      replay: normalizeReplayV1Internal(wire, v1Seed),
    };
  }

  if (version === 3) {
    validateReplayV3(input, fail);
    const wire = input;
    return {
      replayVersion: 3,
      wire,
      replay: normalizeReplayV3(wire),
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
  if (version === 3) {
    validateReplayV3(input, fail);
    return input;
  }
  validateReplayV2(input);
  return input;
}

function replayVersionOf(input: unknown): 1 | 2 | 3 {
  const root = record(input, 'replay');
  const header = record(required(root, 'header', 'replay'), 'replay.header');
  const version = integer(
    required(header, 'replayVersion', 'replay.header'),
    'replay.header.replayVersion',
  );

  if (version === 1 || version === 2 || version === 3) return version;

  throw new ReplayDecodeError(
    `replay.header.replayVersion: unsupported replay version ${version}`,
  );
}

interface V1SeedNormalization {
  decimal: string;
  exact: boolean;
}

function replayV1SeedLexeme(json: string): string {
  const rootStart = skipJsonWhitespace(json, 0);
  const header = directJsonProperty(
    json,
    rootStart,
    'header',
    'replay.header',
  );
  if (!header || json[header.start] !== '{') {
    throw new ReplayDecodeError('replay.header: missing required object');
  }
  const seed = directJsonProperty(
    json,
    header.start,
    'seed',
    'replay.header.seed',
  );
  if (!seed) {
    throw new ReplayDecodeError('replay.header.seed: missing required property');
  }
  const lexeme = json.slice(seed.start, seed.end);
  if (
    !/^(0|[1-9][0-9]*)$/.test(lexeme) ||
    BigInt(lexeme) > 18_446_744_073_709_551_615n
  ) {
    throw new ReplayDecodeError(
      'replay.header.seed: expected an unsigned 64-bit JSON integer',
    );
  }
  return lexeme;
}

/**
 * JSON.parse keeps only the last duplicate property. Replay documents are
 * evidence, so accepting that lossy interpretation would make validation
 * depend on parser behavior instead of the supplied bytes.
 */
function rejectDuplicateJsonProperties(json: string): void {
  scanJsonValueForDuplicates(json, skipJsonWhitespace(json, 0), 'replay');
}

function scanJsonValueForDuplicates(
  json: string,
  start: number,
  path: string,
): number {
  if (json[start] === '{') {
    const seen = new Set<string>();
    let index = skipJsonWhitespace(json, start + 1);
    while (json[index] !== '}') {
      const keyEnd = jsonStringEnd(json, index);
      const key = JSON.parse(json.slice(index, keyEnd)) as string;
      if (seen.has(key)) fail(`${path}.${key}`, 'duplicate property');
      seen.add(key);
      index = skipJsonWhitespace(json, keyEnd);
      index = skipJsonWhitespace(json, index + 1);
      index = scanJsonValueForDuplicates(json, index, `${path}.${key}`);
      index = skipJsonWhitespace(json, index);
      if (json[index] === ',') {
        index = skipJsonWhitespace(json, index + 1);
      }
    }
    return index + 1;
  }
  if (json[start] === '[') {
    let index = skipJsonWhitespace(json, start + 1);
    let itemIndex = 0;
    while (json[index] !== ']') {
      index = scanJsonValueForDuplicates(
        json,
        index,
        `${path}[${itemIndex}]`,
      );
      itemIndex += 1;
      index = skipJsonWhitespace(json, index);
      if (json[index] === ',') {
        index = skipJsonWhitespace(json, index + 1);
      }
    }
    return index + 1;
  }
  return jsonValueEnd(json, start);
}

function directJsonProperty(
  json: string,
  objectStart: number,
  propertyName: string,
  path: string,
): { start: number; end: number } | null {
  if (json[objectStart] !== '{') return null;
  let found: { start: number; end: number } | null = null;
  let index = skipJsonWhitespace(json, objectStart + 1);
  while (index < json.length && json[index] !== '}') {
    if (json[index] !== '"') return null;
    const keyEnd = jsonStringEnd(json, index);
    const key = JSON.parse(json.slice(index, keyEnd)) as string;
    index = skipJsonWhitespace(json, keyEnd);
    if (json[index] !== ':') return null;
    const valueStart = skipJsonWhitespace(json, index + 1);
    const valueEnd = jsonValueEnd(json, valueStart);
    if (key === propertyName) {
      if (found) {
        throw new ReplayDecodeError(`${path}: duplicate property`);
      }
      found = { start: valueStart, end: valueEnd };
    }
    index = skipJsonWhitespace(json, valueEnd);
    if (json[index] === ',') {
      index = skipJsonWhitespace(json, index + 1);
    } else if (json[index] !== '}') {
      return null;
    }
  }
  return found;
}

function jsonStringEnd(json: string, start: number): number {
  let index = start + 1;
  while (index < json.length) {
    if (json[index] === '\\') {
      index += 2;
    } else if (json[index] === '"') {
      return index + 1;
    } else {
      index += 1;
    }
  }
  return json.length;
}

function jsonValueEnd(json: string, start: number): number {
  const first = json[start];
  if (first === '"') return jsonStringEnd(json, start);
  if (first === '{' || first === '[') {
    const opening = first;
    const closing = first === '{' ? '}' : ']';
    let depth = 0;
    let index = start;
    while (index < json.length) {
      const character = json[index];
      if (character === '"') {
        index = jsonStringEnd(json, index);
        continue;
      }
      if (character === opening) depth += 1;
      if (character === closing) {
        depth -= 1;
        if (depth === 0) return index + 1;
      }
      index += 1;
    }
    return json.length;
  }

  let index = start;
  while (
    index < json.length &&
    !/[\s,\]}]/.test(json[index] ?? '')
  ) {
    index += 1;
  }
  return index;
}

function skipJsonWhitespace(json: string, start: number): number {
  let index = start;
  while (index < json.length && /\s/.test(json[index] ?? '')) index += 1;
  return index;
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

const v1SeedValue: Validator = (value, path) => {
  if (
    typeof value !== 'number' ||
    !Number.isInteger(value) ||
    value < 0
  ) {
    fail(path, 'expected a non-negative JSON integer');
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
    seed: v1SeedValue,
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
const v2Lifecycle = oneOf(
  'active',
  'respawning',
  'locked',
  'ready',
  'fabrication-queued',
  'rebuilding',
);
const v2SpawnReason = oneOf(
  'initial',
  'respawn',
  'rebuild',
  'fabrication',
);
const v2TeamPerception = oneOf('individual', 'immediate-union');
const v2ActionParameterKind = oneOf(
  'shot-program',
  'direction',
  'unit-target',
  'form-target',
  'projectile-heading',
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
  'fabrication-unlocked',
  'fabrication-queued',
  'fabricated',
  'rebuild-ready',
  'form-transition-started',
  'form-changed',
  'form-transition-cancelled',
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
  'fabrication-unlocked',
  'fabrication-queued',
  'fabricated',
  'rebuild-ready',
  'form-transition-started',
  'form-changed',
  'form-transition-cancelled',
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
    presence: literal('binary-positive-weight-per-team-no-stacking'),
    nonSolePresence: literal('decay-existing-claim'),
    counterCapture: literal('erode-to-neutral-before-claim'),
  }),
  victory: strictObject({
    initialPosition: literal('centre-position-index'),
    teamAdvances: arrayOf(
      strictObject({
        teamId: integerValue,
        positionIndexDelta: integerValue,
      }),
    ),
    completionPrecedence: literal('base-breach-before-max-ticks'),
    timeoutResolution: literal(
      'signed-position-threshold-plus-claim-zero-draw-no-tiebreakers',
    ),
  }),
  lifecycle: strictObject({
    primeRespawnTicks: integerValue,
    childRebuildTicks: integerValue,
    fabricationUnlockTicks: arrayOf(integerValue),
  }),
  deployment: strictObject({
    primeDefaultFormId: nonEmptyString,
    childDefaultFormId: nonEmptyString,
    destructionTransitionClock: literal(
      'tick-start-at-destroyed-tick-plus-one-plus-delay',
    ),
    primeReturn: literal('automatic-at-authored-prime-spawn'),
    childReturn: literal('ready-then-explicit-fabrication'),
    newLife: literal(
      'fresh-runtime-form-defaults-home-facing-can-act-on-creation-tick',
    ),
    primeSpawnReservation: literal('permanent-against-own-children'),
    protectedPad: literal(
      'enemy-ground-entry-blocked-no-damage-immunity-no-projectile-blocking',
    ),
  }),
  fabrication: strictObject({
    enabled: booleanValue,
    actionId: nonEmptyString,
    fabricatorUnitId: integerValue,
    fabricatorFormId: nonEmptyString,
    targetPolicy: literal('own-ready-child-slot'),
    activationRegion: literal('own-protected-spawn-pad'),
    consumesTick: booleanValue,
    spawnDelayTicks: integerValue,
    capacityEvaluation: literal(
      'post-movement-during-queue-fabrications',
    ),
    spawnRegion: literal(
      'own-protected-spawn-pad-excluding-prime-spawn',
    ),
    spawnSelection: literal(
      'first-unoccupied-unreserved-canonical-y-x',
    ),
    spawnFacing: literal('own-prime-spawn-facing'),
    unavailableSpawnResult: oneOf('blocked', 'faulted', 'rejected'),
    requiresExplicitRefabricationAfterRebuild: booleanValue,
  }),
  anchor: strictObject({
    actionId: nonEmptyString,
    sourceFormId: nonEmptyString,
    targetFormId: nonEmptyString,
    windupTicks: integerValue,
    consumesTick: booleanValue,
    completion: literal(
      'end-of-started-tick-plus-windup-minus-one-after-objective',
    ),
    pendingActions: literal('wait-only'),
    survivingDamage: literal('does-not-cancel'),
    death: literal('cancels-with-explicit-event'),
    forbiddenTiles: literal(
      'all-map-anchor-forbidden-tiles-illegal',
    ),
    pendingForm: literal('source-form-until-completion'),
    healthGain: integerValue,
    healthTransition: literal(
      'minimum-target-maximum-and-current-plus-gain',
    ),
    stateContinuity: literal(
      'same-life-runtime-memory-position-facing-cooldown-energy-and-damage',
    ),
    terminal: literal(
      'preserve-future-pending-without-synthetic-cancellation',
    ),
    irreversibleForLife: booleanValue,
  }),
  turretFire: strictObject({
    actionId: nonEmptyString,
    formId: nonEmptyString,
    allowedProjectileHeadings: arrayOf(v2Heading),
    aim: literal('absolute-eight-way-launch-heading'),
    projectile: literal('one-straight-non-programmed-projectile'),
    facing: literal('body-facing-unchanged'),
    range: literal('global-projectile-range'),
    resources: literal('standard-energy-cooldown-and-damage'),
    traversal: literal(
      'standard-traversal-strict-diagonal-corners',
    ),
  }),
  alliedCombat: strictObject({
    friendlyFireEnabled: booleanValue,
    alliedProjectilesBlock: booleanValue,
    projectileAttribution: literal(
      'exact-firing-life-persists-credits-stable-unit-by-actual-health-removed',
    ),
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
  kind: oneOf(
    'wait',
    'movement',
    'rotation',
    'attack',
    'fabrication',
    'transformation',
  ),
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
      'queue-fabrications',
      'start-form-transitions',
      'complete-form-transitions',
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
    protocolVersion: stringValue,
    configurationVersion: stringValue,
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

const v2FormTransitionShape = strictObject({
  fromFormId: nonEmptyString,
  toFormId: nonEmptyString,
  startedAtTick: integerValue,
  completesAtTick: integerValue,
});
const v2FormTransition: Validator = (value, path) => {
  v2FormTransitionShape(value, path);
  const transition = value as V2.ReplayV2FormTransition;
  if (
    transition.fromFormId === transition.toFormId ||
    transition.startedAtTick < 0 ||
    transition.completesAtTick < transition.startedAtTick
  ) {
    fail(path, 'form transition has invalid forms or chronology');
  }
};
const v2ObservedSelf = strictObject({
  actorId,
  formId: stringValue,
  pendingFormTransition: nullable(v2FormTransition),
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
  pendingFormTransition: nullable(v2FormTransition),
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
  unlockAtTick: nullable(integerValue),
  rebuildReadyAtTick: nullable(integerValue),
  fabricationAtTick: nullable(integerValue),
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
  projectileHeading: nullable(v2Heading),
  fromFormId: nullable(stringValue),
  toFormId: nullable(stringValue),
  formTransitionStartedAtTick: nullable(integerValue),
  formTransitionCompletesAtTick: nullable(integerValue),
  actionId: nullable(stringValue),
  actionCode: nullable(integerValue),
  formTargetId: nullable(stringValue),
  actionResult: nullable(v2ActionResult),
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
  allowedProjectileHeadings: nullable(arrayOf(v2Heading)),
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
const v2ActionPayloadShape = strictObject({
  shotProgram: nullable(v2ShotProgram),
  direction: nullable(v2Direction),
  launchHeading: nullable(v2Heading),
  unitTarget: nullable(unitTarget),
  formTargetId: nullable(stringValue),
});
const v2ActionPayload: Validator = (value, path) => {
  v2ActionPayloadShape(value, path);
  const payload = value as V2.ReplayV2ActionPayload;
  if (
    payload.shotProgram === null &&
    payload.direction === null &&
    payload.launchHeading === null &&
    payload.unitTarget === null &&
    payload.formTargetId === null
  ) {
    fail(path, 'empty action payload must canonicalize to null');
  }
};
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
  spawnReason: v2SpawnReason,
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
  unitId: nullable(integerValue),
  sourceActorId: nullable(actorId),
  targetActorId: nullable(actorId),
  projectileId: nullable(canonicalProjectileId),
  from: nullable(positionObject),
  to: nullable(positionObject),
  fromFacing: nullable(v2Direction),
  toFacing: nullable(v2Direction),
  projectileHeading: nullable(v2Heading),
  actionId: nullable(stringValue),
  actionCode: nullable(integerValue),
  actionPayload: nullable(v2ActionPayload),
  actionResult: nullable(v2ActionResult),
  fromFormId: nullable(stringValue),
  toFormId: nullable(stringValue),
  formTransitionStartedAtTick: nullable(integerValue),
  formTransitionCompletesAtTick: nullable(integerValue),
  amount: nullable(integerValue),
  newHealth: nullable(integerValue),
  lifecycleStatus: nullable(v2Lifecycle),
  spawnReason: nullable(v2SpawnReason),
  respawnAtTick: nullable(integerValue),
  unlockAtTick: nullable(integerValue),
  rebuildReadyAtTick: nullable(integerValue),
  fabricationAtTick: nullable(integerValue),
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
  formId: nonEmptyString,
  pendingFormTransition: nullable(v2FormTransition),
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
  defaultFormId: nonEmptyString,
  lifecycleStatus: v2Lifecycle,
  respawnAtTick: nullable(integerValue),
  unlockAtTick: nullable(integerValue),
  rebuildReadyAtTick: nullable(integerValue),
  fabricationAtTick: nullable(integerValue),
  reservedSpawn: nullable(positionObject),
  pendingSpawnReason: nullable(v2SpawnReason),
  hasSpawned: booleanValue,
  nextLifeId: integerValue,
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
const v2UnitResult = strictObject({
  teamId: integerValue,
  unitId: integerValue,
  defaultFormId: nonEmptyString,
  formId: nonEmptyString,
  pendingFormTransition: nullable(v2FormTransition),
  lifecycleStatus: v2Lifecycle,
  activeActorId: nullable(actorId),
  health: integerValue,
  damageDealt: canonicalProjectileId,
});
const v2TeamResult = strictObject({
  teamId: integerValue,
  outcome: oneOf('win', 'loss', 'draw'),
  activeHealth: integerValue,
  damageDealt: canonicalProjectileId,
  units: arrayOf(v2UnitResult),
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
      for (const enemy of bot.visibleEnemies) {
        if (!participantSlots.has(enemy.slot)) {
          fail(
            `replay.ticks[${tickIndex}].bots.visibleEnemies`,
            `unknown participant slot ${enemy.slot}`,
          );
        }
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
    tick.events.forEach((event, eventIndex) => {
      for (const [field, slot] of [
        ['slot', event.slot],
        ['hitSlot', event.hitSlot],
        ['targetSlot', event.targetSlot],
      ] as const) {
        if (slot !== undefined && !participantSlots.has(slot)) {
          fail(
            `replay.ticks[${tickIndex}].events[${eventIndex}].${field}`,
            `unknown participant slot ${slot}`,
          );
        }
      }
    });
    tick.projectiles?.forEach((projectile, projectileIndex) => {
      if (!participantSlots.has(projectile.ownerSlot)) {
        fail(
          `replay.ticks[${tickIndex}].projectiles[${projectileIndex}].ownerSlot`,
          `unknown participant slot ${projectile.ownerSlot}`,
        );
      }
    });
    tick.projectileTraversals?.forEach((traversal, traversalIndex) => {
      if (!participantSlots.has(traversal.ownerSlot)) {
        fail(
          `replay.ticks[${tickIndex}].projectileTraversals[${traversalIndex}].ownerSlot`,
          `unknown participant slot ${traversal.ownerSlot}`,
        );
      }
    });
  });

  if (document.result) {
    if (
      document.result.winnerSlot !== undefined &&
      !participantSlots.has(document.result.winnerSlot)
    ) {
      fail(
        'replay.result.winnerSlot',
        `unknown participant slot ${document.result.winnerSlot}`,
      );
    }
    ensureUnique(
      document.result.bots,
      (bot) => bot.slot,
      'replay.result.bots',
    );
    const resultSlots = document.result.bots
      .map((bot) => bot.slot)
      .sort(compareNumber);
    const expectedSlots = [...participantSlots].sort(compareNumber);
    if (!sameNumbers(resultSlots, expectedSlots)) {
      fail(
        'replay.result.bots',
        'must cover exactly the participant slots',
      );
    }
  }
}

function validateV2Relationships(document: V2.ReplayV2Document): void {
  const { contract } = document.header;
  const { topology } = contract;

  ensureUnique(
    topology.teams,
    (team) => team.teamId,
    'replay.header.contract.topology.teams',
  );
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
  ensureUnique(
    contract.rules.forms,
    (form) => form.id,
    'replay.header.contract.rules.forms',
  );
  ensureUnique(
    contract.rules.actions,
    (action) => action.id,
    'replay.header.contract.rules.actions',
  );
  ensureUnique(
    contract.rules.actions,
    (action) => action.code,
    'replay.header.contract.rules.actions',
  );
  ensureUnique(document.ticks, (tick) => tick.tick, 'replay.ticks');
  const sortedTicks = document.ticks
    .map((tick, inputIndex) => ({ tick, inputIndex }))
    .sort((left, right) => left.tick.tick - right.tick.tick);
  sortedTicks.forEach(({ tick }, index) => {
    if (tick.tick !== index) {
      fail(
        'replay.ticks',
        'tick IDs must start at zero and be contiguous',
      );
    }
  });
  if (!document.partial && sortedTicks.length === 0) {
    fail('replay.ticks', 'a finalized replay-v2 must contain a tick');
  }

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

  const topologyTeamIds = topology.teams
    .map((team) => team.teamId)
    .sort(compareNumber);
  const teamIds = new Set(topologyTeamIds);
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
  const initialLifeIds = new Set(
    topology.initialLives.map(actorIdValue),
  );
  const contractActionsById = new Map(
    contract.rules.actions.map((action) => [action.id, action]),
  );
  const contractFormsById = new Map(
    contract.rules.forms.map((form) => [form.id, form]),
  );
  const contractFormIds = new Set(contractFormsById.keys());

  validateV2FabricationContract(
    contract,
    contractActionsById,
    contractFormsById,
    unitKeys,
  );
  validateV2TransformationContract(
    contract,
    contractActionsById,
    contractFormsById,
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
  const seenActorLives = new Set<string>();
  let priorPostState: V2.ReplayV2WorldState | null = null;
  let priorResolutionEvents: readonly V2.ReplayV2Event[] = [];
  sortedTicks.forEach(({ tick, inputIndex: tickIndex }) => {
    if (
      tick.tickStart.lifecycleEvents.some(
        (event) =>
          event.type === 'form-transition-started' ||
          event.type === 'form-changed' ||
          event.type === 'form-transition-cancelled',
      )
    ) {
      fail(
        `replay.ticks[${tickIndex}].tickStart.lifecycleEvents`,
        'form-transition events belong only to authoritative resolution',
      );
    }
    validateV2WorldRelationships(
      tick.tickStart.state,
      unitKeys,
      topologyTeamIds,
      tick.tick,
      contract.rules.frontlineDefinition ?? null,
      contract.map.frontline ?? null,
      contractFormsById,
      `replay.ticks[${tickIndex}].tickStart.state`,
    );
    validateV2WorldRelationships(
      tick.postState,
      unitKeys,
      topologyTeamIds,
      tick.tick,
      contract.rules.frontlineDefinition ?? null,
      contract.map.frontline ?? null,
      contractFormsById,
      `replay.ticks[${tickIndex}].postState`,
    );
    if (tick.tick === 0) {
      validateV2InitialDeployment(
        contract,
        tick.tickStart.state,
        `replay.ticks[${tickIndex}].tickStart.state`,
      );
    }
    if (priorPostState !== null) {
      if (tick.tickStart.lifecycleEvents.length === 0) {
        if (!sameV2WorldState(priorPostState, tick.tickStart.state)) {
          fail(
            `replay.ticks[${tickIndex}].tickStart.state`,
            "must exactly equal the prior tick's post-state when no lifecycle events occur",
          );
        }
      } else {
        validateV2LifecycleTransition(
          contract,
          priorPostState,
          tick.tickStart.state,
          tick.tickStart.lifecycleEvents,
          tick.tick,
          `replay.ticks[${tickIndex}].tickStart`,
        );
      }
    }
    if (tick.tickStart.state.objective.nextTick !== tick.tick) {
      fail(
        `replay.ticks[${tickIndex}].tickStart.state.objective.nextTick`,
        'must equal the containing tick',
      );
    }
    if (tick.postState.objective.nextTick !== tick.tick + 1) {
      fail(
        `replay.ticks[${tickIndex}].postState.objective.nextTick`,
        'must equal the tick after the containing tick',
      );
    }

    const active = tick.tickStart.activeActors
      .map(actorIdValue)
      .sort(compareOrdinal);
    const turns = tick.actors
      .map((turn) => actorIdValue(turn.actorId))
      .sort(compareOrdinal);
    const stateActors = tick.tickStart.state.teams
      .flatMap((team) => team.units)
      .flatMap((unit) => (unit.activeLife ? [actorIdValue(unit.activeLife.actorId)] : []))
      .sort(compareOrdinal);
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

    const observableEventsById = new Map(
      [
        ...priorResolutionEvents,
        ...tick.tickStart.lifecycleEvents,
      ].map((event) => [event.eventId, event]),
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
      const isFirstTurnForLife = !seenActorLives.has(identity);
      if (isFirstTurnForLife !== (turn.lifeStart !== null)) {
        fail(
          `${actorPath}.lifeStart`,
          "must appear exactly on an actor life's first turn",
        );
      }
      seenActorLives.add(identity);
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
        const isInitialLife = initialLifeIds.has(identity);
        const expectedSpawnReason: V2.ReplayV2LifeStart['spawnReason'] =
          isInitialLife
            ? 'initial'
            : turn.actorId.unitId === 0
              ? 'respawn'
              : turn.actorId.lifeId === 0
                ? 'fabrication'
                : 'rebuild';
        const spawnEvents = tick.tickStart.lifecycleEvents.filter(
          (event) =>
            event.sourceActorId !== null &&
            actorIdValue(event.sourceActorId) === identity &&
            (event.type === 'respawned' ||
              event.type === 'fabricated'),
        );
        if (
          turn.lifeStart.spawnReason !== expectedSpawnReason ||
          (isInitialLife && spawnEvents.length !== 0) ||
          (!isInitialLife &&
            (spawnEvents.length !== 1 ||
              spawnEvents[0]?.spawnReason !== expectedSpawnReason))
        ) {
          fail(
            `${actorPath}.lifeStart.spawnReason`,
            'is inconsistent with actor-life chronology',
          );
        }
      }
      validateV2DecisionSemantics(
        turn,
        contractActionsById,
        unitKeys,
        contractFormIds,
        contract.rules.shotPrograms,
        actorPath,
      );
      validateV2ObservedActionSemantics(
        turn,
        contractActionsById,
        unitKeys,
        contract,
        tick.tickStart.state,
        actorPath,
      );
      validateV2Aliases(
        turn,
        observableEventsById,
        actorPath,
      );
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
      validateV2EventSemantics(
        event,
        unitKeys,
        contractActionsById,
        contractFormIds,
        contract.rules.shotPrograms,
        contract.rules.frontlineDefinition?.fabrication ?? null,
        contract.rules.frontlineDefinition?.anchor ?? null,
        `replay.ticks[${tickIndex}].events[${eventIndex}]`,
      );
    });
    if (contract.rules.frontlineDefinition) {
      validateV2DamageAttributionTick(
        tick,
        contract,
        `replay.ticks[${tickIndex}]`,
      );
      validateV2FormTransitionTick(
        tick,
        contract,
        `replay.ticks[${tickIndex}]`,
      );
      validateV2TurretShotTick(
        tick,
        contract,
        `replay.ticks[${tickIndex}]`,
      );
    }
    priorResolutionEvents = tick.resolution.events;
    priorPostState = tick.postState;
  });

  if (document.result) {
    const resultPath = 'replay.result';
    ensureUnique(
      document.result.teams,
      (team) => team.teamId,
      `${resultPath}.teams`,
    );
    const resultTeamIds = document.result.teams
      .map((team) => team.teamId)
      .sort(compareNumber);
    if (!sameNumbers(resultTeamIds, topologyTeamIds)) {
      fail(
        `${resultPath}.teams`,
        'must cover exactly the topology team IDs',
      );
    }
    const finalTick = sortedTicks.at(-1)?.tick;
    if (!finalTick || document.result.endTick !== finalTick.tick) {
      fail(
        `${resultPath}.endTick`,
        'must equal the final executed tick',
      );
    }
    if (
      finalTick &&
      !sameV2Control(
        document.result.objective,
        finalTick.postState.objective,
      )
    ) {
      fail(
        `${resultPath}.objective`,
        'must equal the final post-state objective',
      );
    }
    if (
      document.result.winnerTeamId !== null &&
      !teamIds.has(document.result.winnerTeamId)
    ) {
      fail(
        `${resultPath}.winnerTeamId`,
        'must reference a topology team',
      );
    }
    validateV2ObjectiveTeamReferences(
      document.result.objective,
      teamIds,
      `${resultPath}.objective`,
    );
    if (
      document.result.reason === 'base-breach' &&
      document.result.winnerTeamId !==
        document.result.objective.winnerTeamId
    ) {
      fail(
        resultPath,
        'a base-breach winner must equal the objective winner',
      );
    }
    if (finalTick) {
      validateV2TerminalUnits(
        document.result,
        finalTick.postState,
        topology.unitSlots,
        resultPath,
      );
    }
  }
}

function validateV2FabricationContract(
  contract: V2.ReplayV2MatchContract,
  actionsById: ReadonlyMap<string, V2.ReplayV2ActionDefinition>,
  formsById: ReadonlyMap<string, V2.ReplayV2FormDefinition>,
  topologyUnitKeys: ReadonlySet<string>,
): void {
  const frontline = contract.rules.frontlineDefinition;
  if (!frontline) return;

  const path =
    'replay.header.contract.rules.frontlineDefinition.fabrication';
  const fabrication = frontline.fabrication;
  ensureUnique(
    frontline.victory.teamAdvances,
    (advance) => advance.teamId,
    'replay.header.contract.rules.frontlineDefinition.victory.teamAdvances',
  );
  const advanceTeams = frontline.victory.teamAdvances
    .map((advance) => advance.teamId)
    .sort(compareNumber);
  const topologyTeams = contract.topology.teams
    .map((team) => team.teamId)
    .sort(compareNumber);
  if (
    !sameNumbers(advanceTeams, topologyTeams) ||
    frontline.victory.teamAdvances.length !== 2 ||
    !sameNumbers(
      frontline.victory.teamAdvances
        .map((advance) => advance.positionIndexDelta)
        .sort(compareNumber),
      [-1, 1],
    )
  ) {
    fail(
      'replay.header.contract.rules.frontlineDefinition.victory.teamAdvances',
      'must map the two topology teams uniquely to -1 and +1',
    );
  }
  if (
    !contract.rules.tickResolution.phases.includes('queue-fabrications')
  ) {
    fail(
      'replay.header.contract.rules.tickResolution.phases',
      'Frontline contracts must publish queue-fabrications',
    );
  }
  const { primeDefaultFormId, childDefaultFormId } =
    frontline.deployment;
  if (
    primeDefaultFormId === childDefaultFormId ||
    !formsById.has(primeDefaultFormId) ||
    !formsById.has(childDefaultFormId) ||
    fabrication.fabricatorFormId !== primeDefaultFormId
  ) {
    fail(
      'replay.header.contract.rules.frontlineDefinition.deployment',
      'default and fabricator form IDs must reference distinct matching contract forms',
    );
  }
  if (!formsById.has(fabrication.fabricatorFormId)) {
    fail(
      `${path}.fabricatorFormId`,
      'must reference a contract form',
    );
  }
  for (const team of contract.topology.teams) {
    if (
      !topologyUnitKeys.has(
        `${team.teamId}:${fabrication.fabricatorUnitId}`,
      )
    ) {
      fail(
        `${path}.fabricatorUnitId`,
        `team ${team.teamId} has no matching fabricator unit`,
      );
    }
  }
  if (!fabrication.enabled) return;

  const action = actionsById.get(fabrication.actionId);
  if (
    !action ||
    !action.enabled ||
    action.kind !== 'fabrication' ||
    action.parameterKinds.length !== 1 ||
    action.parameterKinds[0] !== 'unit-target'
  ) {
    fail(
      `${path}.actionId`,
      'must reference an enabled unit-target fabrication action',
    );
  }
  const form = formsById.get(fabrication.fabricatorFormId);
  if (!form?.allowedActionIds.includes(fabrication.actionId)) {
    fail(
      `${path}.fabricatorFormId`,
      'fabricator form must allow the fabrication action',
    );
  }
}

const canonicalProjectileHeadings: readonly V2.ReplayV2ProjectileHeading[] = [
  'north',
  'north-east',
  'east',
  'south-east',
  'south',
  'south-west',
  'west',
  'north-west',
];

function validateV2TransformationContract(
  contract: V2.ReplayV2MatchContract,
  actionsById: ReadonlyMap<string, V2.ReplayV2ActionDefinition>,
  formsById: ReadonlyMap<string, V2.ReplayV2FormDefinition>,
): void {
  const frontline = contract.rules.frontlineDefinition;
  if (!frontline) return;

  const anchorPath =
    'replay.header.contract.rules.frontlineDefinition.anchor';
  const turretPath =
    'replay.header.contract.rules.frontlineDefinition.turretFire';
  const anchor = frontline.anchor;
  const turretFire = frontline.turretFire;
  const transform = actionsById.get(anchor.actionId);
  const directionalShot = actionsById.get(turretFire.actionId);
  const sourceForm = formsById.get(anchor.sourceFormId);
  const targetForm = formsById.get(anchor.targetFormId);
  const turretForm = formsById.get(turretFire.formId);

  if (
    anchor.actionId !== 'transform' ||
    !transform ||
    transform.code !== 101 ||
    !transform.enabled ||
    transform.kind !== 'transformation' ||
    !sameStrings(transform.parameterKinds, ['form-target'])
  ) {
    fail(
      `${anchorPath}.actionId`,
      'must reference canonical enabled Transform/101 with one form-target parameter',
    );
  }
  if (
    anchor.sourceFormId !==
      frontline.deployment.childDefaultFormId ||
    anchor.sourceFormId === anchor.targetFormId ||
    !sourceForm ||
    !targetForm ||
    !sourceForm.allowedActionIds.includes(anchor.actionId)
  ) {
    fail(
      anchorPath,
      'must transform the child default form into a distinct contract form allowed by the source form',
    );
  }
  if (
    anchor.windupTicks <= 0 ||
    anchor.healthGain < 0 ||
    !anchor.consumesTick ||
    !anchor.irreversibleForLife
  ) {
    fail(
      anchorPath,
      'must publish a positive consuming irreversible windup and non-negative health gain',
    );
  }
  if (
    turretFire.actionId !== 'shoot-direction' ||
    !directionalShot ||
    directionalShot.code !== 102 ||
    !directionalShot.enabled ||
    directionalShot.kind !== 'attack' ||
    !sameStrings(directionalShot.parameterKinds, [
      'projectile-heading',
    ])
  ) {
    fail(
      `${turretPath}.actionId`,
      'must reference canonical enabled ShootDirection/102 with one projectile-heading parameter',
    );
  }
  if (
    turretFire.formId !== anchor.targetFormId ||
    turretForm !== targetForm ||
    !turretForm ||
    turretForm.canMove ||
    !turretForm.canShoot ||
    turretForm.allowsProgrammedShots ||
    !turretForm.omnidirectionalVision ||
    !turretForm.omnidirectionalShooting ||
    turretForm.objectiveWeight !== 0 ||
    !sameStrings(turretForm.allowedActionIds, [
      'shoot-direction',
      'wait',
    ])
  ) {
    fail(
      `${turretPath}.formId`,
      'must be the stationary, omnidirectional, zero-objective-weight target form with exactly ShootDirection and Wait',
    );
  }
  ensureUnique(
    turretFire.allowedProjectileHeadings,
    (heading) => heading,
    `${turretPath}.allowedProjectileHeadings`,
  );
  if (
    !sameStrings(
      turretFire.allowedProjectileHeadings,
      canonicalProjectileHeadings,
    )
  ) {
    fail(
      `${turretPath}.allowedProjectileHeadings`,
      'must contain all eight canonical headings in canonical order',
    );
  }

  const phases = contract.rules.tickResolution.phases;
  const startIndex = phases.indexOf('start-form-transitions');
  const combatIndex = phases.indexOf(
    'launch-shots-and-apply-damage',
  );
  const objectiveIndex = phases.indexOf('update-objective');
  const completionIndex = phases.indexOf(
    'complete-form-transitions',
  );
  if (
    startIndex < 0 ||
    combatIndex < 0 ||
    objectiveIndex < 0 ||
    completionIndex < 0 ||
    startIndex >= combatIndex ||
    completionIndex <= objectiveIndex
  ) {
    fail(
      'replay.header.contract.rules.tickResolution.phases',
      'must start transitions before combat and complete them after objective resolution',
    );
  }
}

function validateV2ObservedActionSemantics(
  turn: V2.ReplayV2ActorTurn,
  actionsById: ReadonlyMap<string, V2.ReplayV2ActionDefinition>,
  topologyUnitKeys: ReadonlySet<string>,
  matchContract: V2.ReplayV2MatchContract,
  world: V2.ReplayV2WorldState,
  path: string,
): void {
  const observed = turn.observation.actions;
  ensureUnique(
    observed,
    (action) => action.actionId,
    `${path}.observation.actions`,
  );
  ensureUnique(
    observed,
    (action) => action.actionCode,
    `${path}.observation.actions`,
  );
  if (
    observed.length !== actionsById.size ||
    observed.some((action) => {
      const contract = actionsById.get(action.actionId);
      return (
        !contract ||
        action.actionCode !== contract.code ||
        action.enabled !== contract.enabled ||
        !sameStrings(action.parameterKinds, contract.parameterKinds)
      );
    })
  ) {
    fail(
      `${path}.observation.actions`,
      'must mirror every public contract action selector',
    );
  }

  const authoritativeTeam = world.teams.find(
    (team) => team.teamId === turn.actorId.teamId,
  );
  const authoritativeUnit = authoritativeTeam?.units.find(
    (unit) => unit.unitId === turn.actorId.unitId,
  );
  const authoritativeLife = authoritativeUnit?.activeLife;
  if (
    !authoritativeTeam ||
    !authoritativeUnit ||
    !authoritativeLife ||
    actorIdValue(authoritativeLife.actorId) !==
      actorIdValue(turn.actorId) ||
    turn.observation.self.formId !== authoritativeLife.formId ||
    !samePosition(
      turn.observation.self.position,
      authoritativeLife.position,
    ) ||
    turn.observation.self.facing !== authoritativeLife.facing ||
    turn.observation.self.health !== authoritativeLife.health ||
    turn.observation.self.cooldown !== authoritativeLife.cooldown ||
    turn.observation.self.energy !== authoritativeLife.energy ||
    turn.observation.self.previousActionResult !==
      authoritativeLife.previousActionResult ||
    !sameV2FormTransition(
      turn.observation.self.pendingFormTransition,
      authoritativeLife.pendingFormTransition,
    )
  ) {
    fail(
      `${path}.observation.self`,
      'must equal the authoritative tick-start life',
    );
  }
  const observedUnits = [...turn.observation.teamUnits].sort(
    compareUnitIdentity,
  );
  const expectedUnits = [...authoritativeTeam.units].sort(
    compareUnitIdentity,
  );
  if (
    observedUnits.length !== expectedUnits.length ||
    observedUnits.some((unit, index) => {
      const expected = expectedUnits[index];
      return (
        !expected ||
        unit.teamId !== expected.teamId ||
        unit.unitId !== expected.unitId ||
        unit.formId !== v2UnitEffectiveFormId(expected) ||
        unit.lifecycleStatus !== expected.lifecycleStatus ||
        !sameNullableActorId(
          unit.activeActorId,
          expected.activeLife?.actorId ?? null,
        ) ||
        unit.respawnAtTick !== expected.respawnAtTick ||
        unit.unlockAtTick !== expected.unlockAtTick ||
        unit.rebuildReadyAtTick !== expected.rebuildReadyAtTick ||
        unit.fabricationAtTick !== expected.fabricationAtTick
      );
    })
  ) {
    fail(
      `${path}.observation.teamUnits`,
      'must equal the authoritative tick-start team units',
    );
  }

  const worldUnits = world.teams.flatMap((team) => team.units);
  const expectedAlliedActorKeys = new Set(
    authoritativeTeam.units
      .flatMap((unit) => unit.activeLife ? [unit.activeLife] : [])
      .filter(
        (life) =>
          actorIdValue(life.actorId) !== actorIdValue(turn.actorId),
      )
      .map((life) => actorIdValue(life.actorId)),
  );
  if (
    turn.observation.allies.length !== expectedAlliedActorKeys.size ||
    turn.observation.allies.some(
      (ally) => !expectedAlliedActorKeys.has(actorIdValue(ally.actorId)),
    )
  ) {
    fail(
      `${path}.observation.allies`,
      'must cover every other authoritative active allied life',
    );
  }
  for (const [index, ally] of turn.observation.allies.entries()) {
    const unit = worldUnits.find(
      (candidate) =>
        candidate.teamId === ally.actorId.teamId &&
        candidate.unitId === ally.actorId.unitId,
    );
    const life = unit?.activeLife;
    if (
      ally.actorId.teamId !== turn.actorId.teamId ||
      actorIdValue(ally.actorId) === actorIdValue(turn.actorId) ||
      !life ||
      actorIdValue(life.actorId) !== actorIdValue(ally.actorId) ||
      ally.formId !== life.formId ||
      !samePosition(ally.position, life.position) ||
      ally.facing !== life.facing ||
      ally.health !== life.health ||
      ally.cooldown !== life.cooldown ||
      ally.energy !== life.energy ||
      ally.previousActionResult !== life.previousActionResult ||
      !sameV2FormTransition(
        ally.pendingFormTransition,
        life.pendingFormTransition,
      )
    ) {
      fail(
        `${path}.observation.allies[${index}]`,
        'must equal its authoritative allied tick-start life',
      );
    }
  }

  const enemyAliases = new Map(
    turn.aliases.enemyLives.map((alias) => [
      alias.lifeHandle,
      alias.actorId,
    ]),
  );
  for (const [index, enemy] of turn.observation.enemies.entries()) {
    const exactActor = enemyAliases.get(enemy.actor.lifeHandle);
    const unit = exactActor
      ? worldUnits.find(
          (candidate) =>
            candidate.teamId === exactActor.teamId &&
            candidate.unitId === exactActor.unitId,
        )
      : null;
    const life = unit?.activeLife;
    if (
      !exactActor ||
      exactActor.teamId === turn.actorId.teamId ||
      exactActor.teamId !== enemy.actor.teamId ||
      exactActor.unitId !== enemy.actor.unitId ||
      !life ||
      actorIdValue(life.actorId) !== actorIdValue(exactActor) ||
      enemy.formId !== life.formId ||
      !samePosition(enemy.position, life.position) ||
      enemy.facing !== life.facing ||
      enemy.health !== life.health ||
      !sameV2FormTransition(
        enemy.pendingFormTransition,
        life.pendingFormTransition,
      )
    ) {
      fail(
        `${path}.observation.enemies[${index}]`,
        'must equal the aliased authoritative enemy tick-start life',
      );
    }
  }

  const currentForm = matchContract.rules.forms.find(
    (form) => form.id === authoritativeLife.formId,
  );
  const frontline = matchContract.rules.frontlineDefinition;
  const home = matchContract.map.frontline?.teamHomes.find(
    (candidate) => candidate.teamId === turn.actorId.teamId,
  );
  const onAnchorForbiddenTile =
    matchContract.map.frontline?.anchorForbiddenTiles.some((position) =>
      sameContractPosition(position, authoritativeLife.position),
    ) ?? false;

  for (const action of observed) {
    const contract = actionsById.get(action.actionId)!;
    action.allowedUnitTargets?.forEach((target) => {
      if (
        !topologyUnitKeys.has(`${target.teamId}:${target.unitId}`)
      ) {
        fail(
          `${path}.observation.actions.allowedUnitTargets`,
          'must reference topology units',
        );
      }
    });
    if (
      contract.parameterKinds.includes('projectile-heading') !==
      (action.allowedProjectileHeadings !== null)
    ) {
      fail(
        `${path}.observation.actions.${action.actionId}.allowedProjectileHeadings`,
        'nullability must exactly match projectile-heading support',
      );
    }
    if (
      contract.parameterKinds.includes('form-target') !==
      (action.allowedFormTargets !== null)
    ) {
      fail(
        `${path}.observation.actions.${action.actionId}.allowedFormTargets`,
        'nullability must exactly match form-target support',
      );
    }
    if (
      action.available &&
      (!contract.enabled ||
        !currentForm?.allowedActionIds.includes(contract.id))
    ) {
      fail(
        `${path}.observation.actions.${action.actionId}`,
        'an action excluded by the active form cannot be available',
      );
    }
    if (authoritativeLife.pendingFormTransition) {
      const expectedAvailable =
        contract.id === 'wait' &&
        contract.enabled &&
        (currentForm?.allowedActionIds.includes(contract.id) ?? false);
      if (action.available !== expectedAvailable) {
        fail(
          `${path}.observation.actions.${action.actionId}`,
          'pending form transitions must expose Wait as the only available action',
        );
      }
    }
    if (contract.id === 'transform') {
      const expectedAvailable =
        contract.enabled &&
        (currentForm?.allowedActionIds.includes(contract.id) ?? false) &&
        authoritativeLife.pendingFormTransition === null &&
        frontline !== undefined &&
        authoritativeLife.formId === frontline.anchor.sourceFormId &&
        !onAnchorForbiddenTile;
      const expectedTargets =
        expectedAvailable && frontline
          ? [frontline.anchor.targetFormId]
          : [];
      if (
        action.available !== expectedAvailable ||
        action.allowedFormTargets === null ||
        !sameStrings(action.allowedFormTargets, expectedTargets) ||
        action.allowedProjectileHeadings !== null
      ) {
        fail(
          `${path}.observation.actions.${action.actionId}`,
          'transform availability must exactly match source form, pending state, and map legality',
        );
      }
    }
    if (contract.id === 'shoot-direction') {
      const expectedAvailable =
        contract.enabled &&
        (currentForm?.allowedActionIds.includes(contract.id) ?? false) &&
        authoritativeLife.pendingFormTransition === null &&
        currentForm?.canShoot === true &&
        authoritativeLife.cooldown === 0 &&
        (!matchContract.rules.energy.enabled ||
          authoritativeLife.energy !== null &&
            authoritativeLife.energy >=
              matchContract.rules.energy.shotEnergyCost);
      const expectedHeadings =
        expectedAvailable && frontline
          ? frontline.turretFire.allowedProjectileHeadings
          : [];
      if (
        action.available !== expectedAvailable ||
        action.allowedProjectileHeadings === null ||
        !sameStrings(
          action.allowedProjectileHeadings,
          expectedHeadings,
        ) ||
        action.allowedFormTargets !== null
      ) {
        fail(
          `${path}.observation.actions.${action.actionId}`,
          'directional fire mask must exactly match turret form, resources, and all eight headings',
        );
      }
    }
    if (contract.kind !== 'fabrication') continue;

    if (!frontline || !home) {
      fail(
        `${path}.observation.actions.${action.actionId}`,
        'fabrication requires Frontline home and rule definitions',
      );
    }
    const onHomePad =
      turn.actorId.unitId === frontline.fabrication.fabricatorUnitId &&
      home.protectedSpawnPad.some((position) =>
        sameContractPosition(position, authoritativeLife.position),
      );
    const expectedTargets = authoritativeTeam.units
      .filter(
        (unit) =>
          onHomePad &&
          unit.unitId > 0 &&
          unit.lifecycleStatus === 'ready',
      )
      .sort(compareUnitIdentity)
      .map((unit) => ({ teamId: unit.teamId, unitId: unit.unitId }));
    const expectedAvailable =
      contract.enabled &&
      (currentForm?.allowedActionIds.includes(contract.id) ?? false) &&
      expectedTargets.length > 0;
    if (
      action.allowedUnitTargets === null ||
      !sameUnitTargets(action.allowedUnitTargets, expectedTargets) ||
      action.available !== expectedAvailable
    ) {
      fail(
        `${path}.observation.actions.${action.actionId}`,
        'fabrication mask must exactly match the authoritative form, home-pad, and Ready-slot state',
      );
    }
  }
}

function validateV2EventSemantics(
  event: V2.ReplayV2Event,
  topologyUnitKeys: ReadonlySet<string>,
  actionsById: ReadonlyMap<string, V2.ReplayV2ActionDefinition>,
  formIds: ReadonlySet<string>,
  shotProgramRules: V2.ReplayV2ShotProgramRules,
  fabrication: V2.ReplayV2FrontlineFabricationDefinition | null,
  anchor: V2.ReplayV2FrontlineAnchorDefinition | null,
  path: string,
): void {
  if (
    event.unitId !== null &&
    (event.teamId === null ||
      !topologyUnitKeys.has(`${event.teamId}:${event.unitId}`))
  ) {
    fail(
      path,
      'event unitId must pair with a topology team/unit identity',
    );
  }
  if ((event.actionId === null) !== (event.actionCode === null)) {
    fail(path, 'event action selector must contain both ID and code');
  }
  if (event.actionId !== null && event.actionCode !== null) {
    const action = resolveV2ContractAction(
      event.actionId,
      event.actionCode,
      actionsById,
      `${path}.actionId`,
    );
    validateV2PayloadForAction(
      event.actionPayload,
      action,
      topologyUnitKeys,
      formIds,
      shotProgramRules,
      event.sourceActorId?.teamId ?? event.teamId ?? -1,
      `${path}.actionPayload`,
    );
  } else if (event.actionPayload !== null) {
    fail(path, 'event cannot carry a payload without an action selector');
  }

  const requireLifecycle = (
    status: V2.ReplayV2LifecycleStatus,
    field: 'unlockAtTick' | 'rebuildReadyAtTick' | 'fabricationAtTick',
  ) => {
    if (
      event.teamId === null ||
      event.unitId === null ||
      event.lifecycleStatus !== status ||
      event[field] !== event.tick
    ) {
      fail(path, `invalid ${event.type} lifecycle payload`);
    }
  };
  const actorMatchesUnit = (
    actor: V2.ReplayV2ActorId | null,
  ): actor is V2.ReplayV2ActorId =>
    actor !== null &&
    actor.teamId === event.teamId &&
    actor.unitId === event.unitId;
  const isFormEvent =
    event.type === 'form-transition-started' ||
    event.type === 'form-changed' ||
    event.type === 'form-transition-cancelled';
  if (isFormEvent) {
    const payload = event.actionPayload;
    if (
      !anchor ||
      event.teamId === null ||
      event.unitId === null ||
      !actorMatchesUnit(event.sourceActorId) ||
      event.targetActorId !== null ||
      event.actionId !== anchor.actionId ||
      event.actionCode !== 101 ||
      event.actionResult !== 'success' ||
      payload === null ||
      payload.formTargetId !== anchor.targetFormId ||
      payload.shotProgram !== null ||
      payload.direction !== null ||
      payload.launchHeading !== null ||
      payload.unitTarget !== null ||
      event.fromFormId !== anchor.sourceFormId ||
      event.toFormId !== anchor.targetFormId ||
      event.formTransitionStartedAtTick === null ||
      event.formTransitionCompletesAtTick !==
        event.formTransitionStartedAtTick +
          anchor.windupTicks -
          1 ||
      event.from === null ||
      event.to === null ||
      !samePosition(event.from, event.to) ||
      event.fromFacing === null ||
      event.toFacing !== event.fromFacing ||
      event.newHealth === null ||
      event.newHealth < 0 ||
      (event.type === 'form-transition-started' &&
        event.tick !== event.formTransitionStartedAtTick) ||
      (event.type === 'form-changed' &&
        event.tick !== event.formTransitionCompletesAtTick) ||
      (event.type === 'form-transition-cancelled' &&
        (event.tick < event.formTransitionStartedAtTick ||
          event.tick > event.formTransitionCompletesAtTick))
    ) {
      fail(path, `invalid ${event.type} transition payload`);
    }
  } else if (
    event.fromFormId !== null ||
    event.toFormId !== null ||
    event.formTransitionStartedAtTick !== null ||
    event.formTransitionCompletesAtTick !== null
  ) {
    fail(path, 'non-form event cannot carry form-transition context');
  }
  if (
    event.type === 'shot' &&
    event.actionId === 'shoot-direction' &&
    (event.actionPayload?.launchHeading === null ||
      event.actionPayload?.launchHeading === undefined ||
      event.projectileHeading !== event.actionPayload.launchHeading ||
      event.fromFacing === null ||
      event.toFacing !== event.fromFacing ||
      event.actionPayload.shotProgram !== null)
  ) {
    fail(path, 'directional Shot must preserve body facing and launch heading');
  }

  switch (event.type) {
    case 'respawned':
      if (
        event.teamId === null ||
        event.unitId !== 0 ||
        !actorMatchesUnit(event.sourceActorId) ||
        event.lifecycleStatus !== 'active' ||
        event.spawnReason !== 'respawn' ||
        event.to === null ||
        event.toFacing === null ||
        event.newHealth === null
      ) {
        fail(path, 'invalid respawned lifecycle payload');
      }
      break;
    case 'destroyed':
      if (
        event.teamId === null ||
        event.unitId === null ||
        !actorMatchesUnit(event.targetActorId) ||
        event.newHealth !== 0 ||
        (event.unitId === 0
          ? event.lifecycleStatus !== 'respawning' ||
            event.respawnAtTick === null ||
            event.respawnAtTick <= event.tick ||
            event.rebuildReadyAtTick !== null
          : event.lifecycleStatus !== 'rebuilding' ||
            event.rebuildReadyAtTick === null ||
            event.rebuildReadyAtTick <= event.tick ||
            event.respawnAtTick !== null)
      ) {
        fail(path, 'invalid destroyed lifecycle payload');
      }
      break;
    case 'fabrication-unlocked':
      if (event.unitId === null || event.unitId <= 0 || event.sourceActorId !== null) {
        fail(path, 'invalid fabrication-unlocked lifecycle payload');
      }
      requireLifecycle('ready', 'unlockAtTick');
      break;
    case 'rebuild-ready':
      if (event.unitId === null || event.unitId <= 0 || event.sourceActorId !== null) {
        fail(path, 'invalid rebuild-ready lifecycle payload');
      }
      requireLifecycle('ready', 'rebuildReadyAtTick');
      break;
    case 'fabricated':
      requireLifecycle('active', 'fabricationAtTick');
      if (
        event.unitId === null ||
        event.unitId <= 0 ||
        !actorMatchesUnit(event.sourceActorId) ||
        event.spawnReason !==
          (event.sourceActorId.lifeId === 0
            ? 'fabrication'
            : 'rebuild') ||
        event.to === null ||
        event.toFacing === null ||
        event.newHealth === null
      ) {
        fail(path, 'fabricated event must identify the spawned life');
      }
      break;
    case 'fabrication-queued': {
      const action =
        event.actionId === null
          ? null
          : actionsById.get(event.actionId) ?? null;
      if (
        event.teamId === null ||
        event.unitId === null ||
        event.unitId <= 0 ||
        event.sourceActorId === null ||
        event.sourceActorId.teamId !== event.teamId ||
        fabrication === null ||
        !fabrication.enabled ||
        event.sourceActorId.unitId !== fabrication.fabricatorUnitId ||
        event.actionId !== fabrication.actionId ||
        action === null ||
        event.actionCode !== action.code ||
        event.actionResult !== 'success' ||
        event.to === null ||
        event.lifecycleStatus !== 'fabrication-queued' ||
        event.fabricationAtTick === null ||
        event.fabricationAtTick !==
          event.tick + fabrication.spawnDelayTicks ||
        (event.spawnReason !== 'fabrication' &&
          event.spawnReason !== 'rebuild') ||
        event.actionPayload === null ||
        event.actionPayload.unitTarget === null ||
        event.actionPayload.unitTarget.teamId !== event.teamId ||
        event.actionPayload.unitTarget.unitId !== event.unitId
      ) {
        fail(path, 'invalid fabrication-queued lifecycle payload');
      }
      break;
    }
  }
}

function validateV2FormTransitionTick(
  tick: V2.ReplayV2Tick,
  matchContract: V2.ReplayV2MatchContract,
  path: string,
): void {
  const frontline = matchContract.rules.frontlineDefinition!;
  const anchor = frontline.anchor;
  const formsById = new Map(
    matchContract.rules.forms.map((form) => [form.id, form]),
  );
  const beforeUnits = new Map(
    tick.tickStart.state.teams
      .flatMap((team) => team.units)
      .map((unit) => [`${unit.teamId}:${unit.unitId}`, unit]),
  );
  const afterUnits = new Map(
    tick.postState.teams
      .flatMap((team) => team.units)
      .map((unit) => [`${unit.teamId}:${unit.unitId}`, unit]),
  );
  const turns = new Map(
    tick.actors.map((turn) => [actorIdValue(turn.actorId), turn]),
  );
  const formEvents = tick.resolution.events.filter(
    (event) =>
      event.type === 'form-transition-started' ||
      event.type === 'form-changed' ||
      event.type === 'form-transition-cancelled',
  );
  const accountedEventIds = new Set<string>();
  const firstChangedIndex = tick.resolution.events.findIndex(
    (event) => event.type === 'form-changed',
  );
  if (
    firstChangedIndex >= 0 &&
    tick.resolution.events
      .slice(firstChangedIndex)
      .some((event) => event.type !== 'form-changed')
  ) {
    fail(
      `${path}.resolution.events`,
      'form-change completions must be the final resolution-event suffix',
    );
  }
  const firstCombatIndex = tick.resolution.events.findIndex(
    (event) =>
      event.type === 'shot' ||
      event.type === 'damage' ||
      event.type === 'destroyed',
  );
  const lastPreStartIndex = tick.resolution.events.reduce(
    (last, event, index) =>
      event.type === 'turn' ||
      event.type === 'move' ||
      event.type === 'move-blocked' ||
      event.type === 'fabrication-queued'
        ? index
        : last,
    -1,
  );
  const lastObjectiveIndex = tick.resolution.events.reduce(
    (last, event, index) =>
      event.type === 'frontline-progress-changed' ||
      event.type === 'frontline-position-advanced' ||
      event.type === 'base-breached'
        ? index
        : last,
    -1,
  );

  for (const beforeUnit of beforeUnits.values()) {
    const beforeLife = beforeUnit.activeLife;
    if (!beforeLife) continue;
    const actorKey = actorIdValue(beforeLife.actorId);
    const afterUnit = afterUnits.get(
      `${beforeUnit.teamId}:${beforeUnit.unitId}`,
    )!;
    const postActiveLife = afterUnit.activeLife;
    const afterLife =
      postActiveLife &&
      actorIdValue(postActiveLife.actorId) === actorKey
        ? postActiveLife
        : null;
    const turn = turns.get(actorKey);
    const actorEvents = formEvents.filter(
      (event) =>
        event.sourceActorId !== null &&
        actorIdValue(event.sourceActorId) === actorKey,
    );
    const started = actorEvents.filter(
      (event) => event.type === 'form-transition-started',
    );
    const changed = actorEvents.filter(
      (event) => event.type === 'form-changed',
    );
    const cancelled = actorEvents.filter(
      (event) => event.type === 'form-transition-cancelled',
    );
    const destroyed = tick.resolution.events.filter(
      (event) =>
        event.type === 'destroyed' &&
        event.targetActorId !== null &&
        actorIdValue(event.targetActorId) === actorKey,
    );
    const acceptedTransform =
      turn?.actionResolution.validatedActionId === anchor.actionId &&
      turn.actionResolution.validatedActionCode === 101 &&
      turn.actionResolution.validatedPayload?.formTargetId ===
        anchor.targetFormId &&
      beforeLife.formId === anchor.sourceFormId &&
      !matchContract.map.frontline!.anchorForbiddenTiles.some(
        (position) =>
          sameContractPosition(position, beforeLife.position),
      ) &&
      turn.actionResolution.result === 'success';
    if (
      started.length > 1 ||
      changed.length > 1 ||
      cancelled.length > 1 ||
      destroyed.length > 1
    ) {
      fail(
        `${path}.resolution.events`,
        `life ${actorKey} has duplicate form or destruction events`,
      );
    }

    let transition = beforeLife.pendingFormTransition;
    if (transition) {
      if (
        !turn ||
        turn.actionResolution.validatedActionId !== 'wait' ||
        started.length !== 0
      ) {
        fail(
          `${path}.actors`,
          'a pending transition must resolve Wait and cannot restart',
        );
      }
    } else if (acceptedTransform) {
      if (started.length !== 1) {
        fail(
          `${path}.resolution.events`,
          'a successful transform must emit exactly one start event',
        );
      }
      const start = started[0]!;
      transition = {
        fromFormId: start.fromFormId!,
        toFormId: start.toFormId!,
        startedAtTick: start.formTransitionStartedAtTick!,
        completesAtTick: start.formTransitionCompletesAtTick!,
      };
      if (
        start.newHealth !== beforeLife.health ||
        start.from === null ||
        !samePosition(start.from, beforeLife.position) ||
        start.fromFacing !== beforeLife.facing
      ) {
        fail(
          `${path}.resolution.events`,
          'transition start must snapshot the source life exactly',
        );
      }
      const startIndex = tick.resolution.events.indexOf(start);
      if (
        startIndex <= lastPreStartIndex ||
        firstCombatIndex >= 0 &&
          startIndex >= firstCombatIndex
      ) {
        fail(
          `${path}.resolution.events`,
          'transition starts must precede projectile combat',
        );
      }
    } else if (actorEvents.length > 0) {
      fail(
        `${path}.resolution.events`,
        'form events require an existing pending transition or successful transform',
      );
    }

    const died = destroyed.length === 1;
    if (died !== (afterLife === null)) {
      fail(
        `${path}.postState`,
        'life destruction events and post-state must agree',
      );
    }

    if (!transition) {
      if (
        afterLife &&
        (afterLife.formId !== beforeLife.formId ||
          afterLife.pendingFormTransition !== null)
      ) {
        fail(
          `${path}.postState`,
          'a surviving life cannot change form without transition causality',
        );
      }
      continue;
    }
    actorEvents.forEach((event) => accountedEventIds.add(event.eventId));
    if (
      transition.fromFormId !== anchor.sourceFormId ||
      transition.toFormId !== anchor.targetFormId
    ) {
      fail(`${path}.resolution.events`, 'transition forms drift from contract');
    }
    if (
      actorEvents.some(
        (event) =>
          event.fromFormId !== transition!.fromFormId ||
          event.toFormId !== transition!.toFormId ||
          event.formTransitionStartedAtTick !==
            transition!.startedAtTick ||
          event.formTransitionCompletesAtTick !==
            transition!.completesAtTick,
      )
    ) {
      fail(
        `${path}.resolution.events`,
        'form-event context must equal the life pending transition',
      );
    }

    if (died) {
      const destroyedIndex = tick.resolution.events.indexOf(
        destroyed[0]!,
      );
      const cancellationIndex = tick.resolution.events.findIndex(
        (event) =>
          event.type === 'form-transition-cancelled' &&
          event.sourceActorId !== null &&
          actorIdValue(event.sourceActorId) === actorKey,
      );
      if (
        destroyedIndex < 0 ||
        cancellationIndex !== destroyedIndex + 1 ||
        cancelled.length !== 1 ||
        changed.length !== 0 ||
        cancelled[0]!.newHealth !== 0 ||
        destroyed[0]!.newHealth !== 0
      ) {
        fail(
          `${path}.resolution.events`,
          'lethal pending transitions require adjacent Destroyed then FormTransitionCancelled',
        );
      }
      continue;
    }

    if (!afterLife) {
      fail(
        `${path}.postState`,
        'a surviving pending transition must retain its active life',
      );
    }
    if (
      !samePosition(afterLife.position, beforeLife.position) ||
      afterLife.facing !== beforeLife.facing ||
      cancelled.length !== 0
    ) {
      fail(
        `${path}.postState`,
        'surviving transitions must preserve the same life, position, and facing',
      );
    }

    const damageEvents = tick.resolution.events.filter(
      (event) =>
        event.type === 'damage' &&
        event.targetActorId !== null &&
        actorIdValue(event.targetActorId) === actorKey,
    );
    const healthBeforeCompletion =
      damageEvents.at(-1)?.newHealth ?? beforeLife.health;
    const due = transition.completesAtTick === tick.tick;
    const targetForm = formsById.get(transition.toFormId);
    const expectedHealth =
      due && targetForm
        ? Math.min(
            targetForm.maxHealth,
            healthBeforeCompletion + anchor.healthGain,
          )
        : healthBeforeCompletion;
    const expectedCooldown = Math.max(0, beforeLife.cooldown - 1);
    let expectedEnergy = beforeLife.energy;
    const energyRules = matchContract.rules.energy;
    if (
      energyRules.enabled &&
      expectedEnergy !== null &&
      expectedEnergy < energyRules.maxEnergy &&
      energyRules.regenerationIntervalTicks > 0 &&
      (tick.tick + 1) % energyRules.regenerationIntervalTicks === 0
    ) {
      expectedEnergy = Math.min(
        energyRules.maxEnergy,
        expectedEnergy + energyRules.regenerationAmount,
      );
    }
    const lifeCreditedDamage = tick.resolution.events
      .filter(
        (event) =>
          event.type === 'damage' &&
          event.sourceActorId !== null &&
          actorIdValue(event.sourceActorId) === actorKey,
      )
      .reduce((total, event) => total + BigInt(event.amount ?? 0), 0n);
    const unitCreditedDamage = tick.resolution.events
      .filter(
        (event) =>
          event.type === 'damage' &&
          event.sourceActorId?.teamId === beforeUnit.teamId &&
          event.sourceActorId.unitId === beforeUnit.unitId,
      )
      .reduce((total, event) => total + BigInt(event.amount ?? 0), 0n);
    const expectedLifeDamage = (
      BigInt(beforeLife.damageDealt) + lifeCreditedDamage
    ).toString();
    const expectedUnitDamage = (
      BigInt(beforeUnit.damageDealt) + unitCreditedDamage
    ).toString();
    if (
      afterLife.spawnedAtTick !== beforeLife.spawnedAtTick ||
      afterLife.cooldown !== expectedCooldown ||
      afterLife.energy !== expectedEnergy ||
      afterLife.previousActionResult !==
        turn?.actionResolution.result ||
      afterLife.health !== expectedHealth ||
      afterLife.damageDealt !== expectedLifeDamage ||
      afterUnit.damageDealt !== expectedUnitDamage
    ) {
      fail(
        `${path}.postState`,
        'pending transitions must preserve same-life state while normal damage, cooldown, energy, and result phases continue',
      );
    }

    if (transition.completesAtTick === tick.tick) {
      const change = changed[0];
      if (
        changed.length !== 1 ||
        !change ||
        targetForm === undefined ||
        afterLife.formId !== transition.toFormId ||
        afterLife.pendingFormTransition !== null ||
        afterLife.health !== expectedHealth ||
        change.newHealth !== expectedHealth ||
        change.from === null ||
        !samePosition(change.from, afterLife.position) ||
        change.fromFacing !== afterLife.facing ||
        tick.resolution.events.indexOf(change) <= lastObjectiveIndex
      ) {
        fail(
          `${path}.postState`,
          'due transition must complete after objective with canonical health gain',
        );
      }
    } else if (
      transition.completesAtTick > tick.tick &&
      (changed.length !== 0 ||
        afterLife.formId !== transition.fromFormId ||
        !sameV2FormTransition(
          afterLife.pendingFormTransition,
          transition,
        ))
    ) {
      fail(
        `${path}.postState`,
        'future transition must remain pending in its source form',
      );
    }
  }

  if (
    formEvents.some((event) => !accountedEventIds.has(event.eventId))
  ) {
    fail(`${path}.resolution.events`, 'contains an orphan form event');
  }
}

function validateV2DamageAttributionTick(
  tick: V2.ReplayV2Tick,
  matchContract: V2.ReplayV2MatchContract,
  path: string,
): void {
  const stableUnitIds = new Set(
    matchContract.topology.unitSlots.map(
      (unit) => `${unit.teamId}:${unit.unitId}`,
    ),
  );
  const hasStableUnit = (actorId: V2.ReplayV2ActorId): boolean =>
    stableUnitIds.has(`${actorId.teamId}:${actorId.unitId}`);
  const tickStartLives = new Map(
    tick.tickStart.state.teams
      .flatMap((team) => team.units)
      .flatMap((unit) => unit.activeLife ? [unit.activeLife] : [])
      .map((life) => [actorIdValue(life.actorId), life]),
  );
  const combatPositions = new Map(
    [...tickStartLives].map(([actorKey, life]) => [
      actorKey,
      { ...life.position },
    ]),
  );
  for (const move of tick.resolution.events) {
    if (
      move.type === 'move' &&
      move.sourceActorId !== null &&
      move.to !== null
    ) {
      const moverKey = actorIdValue(move.sourceActorId);
      if (combatPositions.has(moverKey)) {
        combatPositions.set(moverKey, { ...move.to });
      }
    }
  }

  const projectileOwners = new Map<string, string>();
  for (const projectile of [
    ...tick.tickStart.state.projectiles,
    ...tick.postState.projectiles,
  ]) {
    if (!hasStableUnit(projectile.ownerActorId)) {
      fail(
        `${path}.projectiles`,
        'projectile owner must reference a stable unit in contract topology',
      );
    }
  }
  for (const projectile of tick.tickStart.state.projectiles) {
    projectileOwners.set(
      projectile.projectileId,
      actorIdValue(projectile.ownerActorId),
    );
  }
  for (const traversal of tick.resolution.projectileTraversals) {
    if (!hasStableUnit(traversal.ownerActorId)) {
      fail(
        `${path}.resolution.projectileTraversals`,
        'projectile traversal owner must reference a stable unit in contract topology',
      );
    }
    const ownerKey = actorIdValue(traversal.ownerActorId);
    const existingOwner = projectileOwners.get(
      traversal.projectileId,
    );
    if (
      existingOwner !== undefined &&
      existingOwner !== ownerKey
    ) {
      fail(
        `${path}.resolution.projectileTraversals`,
        'projectile traversal changed its exact firing-life owner',
      );
    }
    projectileOwners.set(traversal.projectileId, ownerKey);
  }

  const remainingHealth = new Map(
    [...tickStartLives].map(([actorKey, life]) => [
      actorKey,
      life.health,
    ]),
  );
  const lastDamageByTarget = new Map<
    string,
    V2.ReplayV2Event
  >();
  const resolutionEvents = tick.resolution.events;
  const damageEvents = resolutionEvents.filter(
    (event) => event.type === 'damage',
  );
  for (const damage of damageEvents) {
    const targetKey =
      damage.targetActorId === null
        ? null
        : actorIdValue(damage.targetActorId);
    const sourceKey =
      damage.sourceActorId === null
        ? null
        : actorIdValue(damage.sourceActorId);
    const priorHealth =
      targetKey === null
        ? undefined
        : remainingHealth.get(targetKey);
    const targetPosition =
      targetKey === null
        ? undefined
        : combatPositions.get(targetKey);
    const projectileOwner =
      damage.projectileId === null
        ? undefined
        : projectileOwners.get(damage.projectileId);
    const expectedAmount =
      priorHealth === undefined
        ? undefined
        : Math.min(
            matchContract.rules.projectiles.damagePerHit,
            priorHealth,
          );
    if (
      damage.targetActorId === null ||
      damage.sourceActorId === null ||
      damage.projectileId === null ||
      damage.amount === null ||
      damage.newHealth === null ||
      targetKey === null ||
      sourceKey === null ||
      priorHealth === undefined ||
      targetPosition === undefined ||
      projectileOwner !== sourceKey ||
      !hasStableUnit(damage.sourceActorId) ||
      damage.teamId !== damage.targetActorId.teamId ||
      damage.from === null ||
      !samePosition(damage.from, targetPosition) ||
      damage.to === null ||
      !samePosition(damage.to, targetPosition) ||
      damage.amount !== expectedAmount ||
      damage.amount <= 0 ||
      damage.newHealth !== priorHealth - damage.amount
    ) {
      fail(
        `${path}.resolution.events`,
        "Damage must form an exact per-target health chain from a projectile's exact firing life",
      );
    }
    remainingHealth.set(targetKey, damage.newHealth);
    lastDamageByTarget.set(targetKey, damage);
  }

  const destroyedEvents = resolutionEvents.filter(
    (event) => event.type === 'destroyed',
  );
  const postLives = new Map(
    tick.postState.teams
      .flatMap((team) => team.units)
      .flatMap((unit) => unit.activeLife ? [unit.activeLife] : [])
      .map((life) => [actorIdValue(life.actorId), life]),
  );
  const beforeUnits = new Map(
    tick.tickStart.state.teams
      .flatMap((team) => team.units)
      .map((unit) => [
        `${unit.teamId}:${unit.unitId}`,
        unit,
      ]),
  );
  const afterUnits = new Map(
    tick.postState.teams
      .flatMap((team) => team.units)
      .map((unit) => [
        `${unit.teamId}:${unit.unitId}`,
        unit,
      ]),
  );
  for (const [actorKey, health] of remainingHealth) {
    const destroyed = destroyedEvents.filter(
      (event) =>
        event.targetActorId !== null &&
        actorIdValue(event.targetActorId) === actorKey,
    );
    if (health > 0) {
      if (destroyed.length !== 0) {
        fail(
          `${path}.resolution.events`,
          'surviving health cannot emit Destroyed',
        );
      }
      const formChangesAfterHealthResolution =
        resolutionEvents.some(
          (event) =>
            event.type === 'form-changed' &&
            event.sourceActorId !== null &&
            actorIdValue(event.sourceActorId) === actorKey,
        );
      if (
        !formChangesAfterHealthResolution &&
        postLives.get(actorKey)?.health !== health
      ) {
        fail(
          `${path}.postState`,
          'surviving post-state health must equal its exact Damage chain',
        );
      }
      continue;
    }

    const destruction = destroyed[0];
    const fatalDamage = lastDamageByTarget.get(actorKey);
    if (
      destroyed.length !== 1 ||
      !destruction ||
      !fatalDamage ||
      (destruction.sourceActorId === null
        ? null
        : actorIdValue(destruction.sourceActorId)) !==
        (fatalDamage.sourceActorId === null
          ? null
          : actorIdValue(fatalDamage.sourceActorId)) ||
      destruction.projectileId !== fatalDamage.projectileId ||
      destruction.newHealth !== 0 ||
      resolutionEvents.indexOf(destruction) <=
        resolutionEvents.indexOf(fatalDamage)
    ) {
      fail(
        `${path}.resolution.events`,
        'zero-health target must emit one later Destroyed event with the exact fatal projectile cause',
      );
    }

    const actorId = destruction.targetActorId!;
    const unitKey = `${actorId.teamId}:${actorId.unitId}`;
    const beforeUnit = beforeUnits.get(unitKey)!;
    const afterUnit = afterUnits.get(unitKey)!;
    const prime = actorId.unitId === 0;
    const lifecycle =
      matchContract.rules.frontlineDefinition!.lifecycle;
    const dueTick =
      tick.tick +
      1 +
      (prime
        ? lifecycle.primeRespawnTicks
        : lifecycle.childRebuildTicks);
    const expectedStatus = prime
      ? 'respawning'
      : 'rebuilding';
    const expectedRespawnAtTick = prime ? dueTick : null;
    const expectedRebuildReadyAtTick = prime ? null : dueTick;
    const combatPosition = combatPositions.get(actorKey)!;
    if (
      destruction.teamId !== actorId.teamId ||
      destruction.unitId !== actorId.unitId ||
      destruction.from === null ||
      !samePosition(destruction.from, combatPosition) ||
      destruction.to === null ||
      !samePosition(destruction.to, combatPosition) ||
      destruction.lifecycleStatus !== expectedStatus ||
      destruction.respawnAtTick !== expectedRespawnAtTick ||
      destruction.rebuildReadyAtTick !==
        expectedRebuildReadyAtTick ||
      afterUnit.activeLife !== null ||
      afterUnit.lifecycleStatus !== expectedStatus ||
      afterUnit.respawnAtTick !== expectedRespawnAtTick ||
      afterUnit.rebuildReadyAtTick !==
        expectedRebuildReadyAtTick ||
      afterUnit.fabricationAtTick !== null ||
      afterUnit.reservedSpawn !== null ||
      afterUnit.pendingSpawnReason !== null ||
      afterUnit.defaultFormId !== beforeUnit.defaultFormId ||
      afterUnit.unlockAtTick !== beforeUnit.unlockAtTick ||
      afterUnit.hasSpawned !== beforeUnit.hasSpawned ||
      afterUnit.nextLifeId !== beforeUnit.nextLifeId
    ) {
      fail(
        `${path}.postState`,
        'destruction must apply the exact Prime respawn or child rebuild reset to its stable unit',
      );
    }
  }
  if (
    destroyedEvents.some(
      (event) =>
        event.targetActorId === null ||
        !remainingHealth.has(actorIdValue(event.targetActorId)),
    )
  ) {
    fail(
      `${path}.resolution.events`,
      'Destroyed must reference a tick-start life',
    );
  }

  for (const [unitKey, beforeUnit] of beforeUnits) {
    const afterUnit = afterUnits.get(unitKey)!;
    const creditedToUnit = damageEvents
      .filter(
        (event) =>
          event.sourceActorId?.teamId === beforeUnit.teamId &&
          event.sourceActorId.unitId === beforeUnit.unitId,
      )
      .reduce((total, event) => total + BigInt(event.amount ?? 0), 0n);
    const expectedUnitDamage = (
      BigInt(beforeUnit.damageDealt) + creditedToUnit
    ).toString();
    if (afterUnit.damageDealt !== expectedUnitDamage) {
      fail(
        `${path}.postState`,
        'damage from every firing life must credit its stable unit by actual health removed',
      );
    }

    const beforeLife = beforeUnit.activeLife;
    const afterLife = afterUnit.activeLife;
    if (
      !beforeLife ||
      !afterLife ||
      actorIdValue(beforeLife.actorId) !==
        actorIdValue(afterLife.actorId)
    ) {
      continue;
    }
    const actorKey = actorIdValue(beforeLife.actorId);
    const creditedToLife = damageEvents
      .filter(
        (event) =>
          event.sourceActorId !== null &&
          actorIdValue(event.sourceActorId) === actorKey,
      )
      .reduce((total, event) => total + BigInt(event.amount ?? 0), 0n);
    const expectedLifeDamage = (
      BigInt(beforeLife.damageDealt) + creditedToLife
    ).toString();
    if (afterLife.damageDealt !== expectedLifeDamage) {
      fail(
        `${path}.postState`,
        'damage must credit only the exact surviving firing life',
      );
    }
  }

  for (const [state, phase] of [
    [tick.tickStart.state, 'tickStart.state'],
    [tick.postState, 'postState'],
  ] as const) {
    for (const team of state.teams) {
      const expectedTeamDamage = team.units
        .reduce(
          (total, unit) => total + BigInt(unit.damageDealt),
          0n,
        )
        .toString();
      if (team.damageDealt !== expectedTeamDamage) {
        fail(
          `${path}.${phase}`,
          'team damage must equal its stable-unit damage aggregate',
        );
      }
    }
  }
}

function validateV2TurretShotTick(
  tick: V2.ReplayV2Tick,
  matchContract: V2.ReplayV2MatchContract,
  path: string,
): void {
  const turretFire =
    matchContract.rules.frontlineDefinition!.turretFire;
  const turretForm = matchContract.rules.forms.find(
    (form) => form.id === turretFire.formId,
  )!;
  const tickStartLives = new Map(
    tick.tickStart.state.teams
      .flatMap((team) => team.units)
      .flatMap((unit) => unit.activeLife ? [unit.activeLife] : [])
      .map((life) => [actorIdValue(life.actorId), life]),
  );
  const combatPositions = new Map(
    [...tickStartLives].map(([actorKey, life]) => [
      actorKey,
      { ...life.position },
    ]),
  );
  for (const move of tick.resolution.events) {
    if (
      move.type === 'move' &&
      move.sourceActorId !== null &&
      move.to !== null
    ) {
      const moverKey = actorIdValue(move.sourceActorId);
      if (combatPositions.has(moverKey)) {
        combatPositions.set(moverKey, { ...move.to });
      }
    }
  }
  const postProjectiles = new Map(
    tick.postState.projectiles.map((projectile) => [
      projectile.projectileId,
      projectile,
    ]),
  );
  const tickStartProjectileIds = new Set(
    tick.tickStart.state.projectiles.map(
      (projectile) => projectile.projectileId,
    ),
  );
  const shotEvents = tick.resolution.events.filter(
    (event) =>
      event.type === 'shot' &&
      event.actionId === turretFire.actionId,
  );
  const shotTurns = tick.actors.filter(
    (turn) =>
      turn.actionResolution.validatedActionId ===
      turretFire.actionId,
  );
  if (shotEvents.length !== shotTurns.length) {
    fail(
      `${path}.resolution.events`,
      'every validated shoot-direction resolution must emit exactly one turret Shot event',
    );
  }

  for (const turn of shotTurns) {
    const actorKey = actorIdValue(turn.actorId);
    const actorShots = shotEvents.filter(
      (event) =>
        event.sourceActorId !== null &&
        actorIdValue(event.sourceActorId) === actorKey,
    );
    const shot = actorShots[0];
    const shooter = tickStartLives.get(actorKey);
    const heading =
      turn.actionResolution.validatedPayload?.launchHeading;
    if (
      actorShots.length !== 1 ||
      !shot ||
      !shooter ||
      shooter.formId !== turretFire.formId ||
      turn.actionResolution.result !== 'success' ||
      heading === null ||
      heading === undefined ||
      shot.projectileHeading !== heading
    ) {
      fail(
        `${path}.resolution.events`,
        'turret Shot must originate from its matching successful active-turret resolution',
      );
    }

    const [dx, dy] = v2HeadingVector(heading);
    const spawn = {
      x: shooter.position.x + dx,
      y: shooter.position.y + dy,
    };
    const wall = (position: V2.ReplayV2Position): boolean =>
      position.x < 0 ||
      position.y < 0 ||
      position.x >= matchContract.map.width ||
      position.y >= matchContract.map.height ||
      matchContract.map.tileRows[position.y]?.[position.x] === '#';
    const blocked =
      wall(spawn) ||
      (dx !== 0 &&
        dy !== 0 &&
        (wall({
          x: shooter.position.x + dx,
          y: shooter.position.y,
        }) ||
          wall({
            x: shooter.position.x,
            y: shooter.position.y + dy,
          })));
    const newOwnerTraversalIds = [
      ...new Set(
        tick.resolution.projectileTraversals
          .filter(
            (traversal) =>
              !tickStartProjectileIds.has(
                traversal.projectileId,
              ) &&
              actorIdValue(traversal.ownerActorId) === actorKey,
          )
          .map((traversal) => traversal.projectileId),
      ),
    ];
    const newOwnerPostProjectileIds = [
      ...new Set(
        tick.postState.projectiles
          .filter(
            (projectile) =>
              !tickStartProjectileIds.has(
                projectile.projectileId,
              ) &&
              actorIdValue(projectile.ownerActorId) === actorKey,
          )
          .map((projectile) => projectile.projectileId),
      ),
    ];
    if (
      shot.from === null ||
      !samePosition(shot.from, shooter.position) ||
      shot.to === null ||
      !samePosition(shot.to, spawn) ||
      shot.fromFacing !== shooter.facing ||
      shot.toFacing !== shooter.facing
    ) {
      fail(
        `${path}.resolution.events`,
        'turret Shot must retain its absolute-heading launch tile and unchanged body facing',
      );
    }

    if (blocked) {
      if (
        shot.projectileId !== null ||
        shot.targetActorId !== null ||
        newOwnerTraversalIds.length !== 0 ||
        newOwnerPostProjectileIds.length !== 0
      ) {
        fail(
          `${path}.resolution`,
          'wall- or corner-blocked turret launch cannot create a projectile or traversal',
        );
      }
    } else {
      if (shot.projectileId === null) {
        fail(
          `${path}.resolution.events`,
          'unblocked turret launch requires a projectile ID',
        );
      }
      const traversals =
        tick.resolution.projectileTraversals.filter(
          (traversal) =>
            traversal.projectileId === shot.projectileId,
        );
      const traversal = traversals[0];
      if (
        traversals.length !== 1 ||
        !traversal ||
        actorIdValue(traversal.ownerActorId) !== actorKey ||
        traversal.launchDirection !== shooter.facing ||
        !samePosition(traversal.from, shooter.position) ||
        traversal.path.length !== 1 ||
        !samePosition(traversal.path[0]!, spawn) ||
        traversal.heading !== heading ||
        traversal.shotProgram !== null ||
        traversal.programmedPath !== null
      ) {
        fail(
          `${path}.resolution.projectileTraversals`,
          'turret launch must create one one-tile straight non-programmed traversal',
        );
      }
      const occupyingLife = [...tickStartLives.values()].find(
        (life) =>
          actorIdValue(life.actorId) !== actorKey &&
          samePosition(
            combatPositions.get(actorIdValue(life.actorId))!,
            spawn,
          ),
      );
      const alliedCombat =
        matchContract.rules.frontlineDefinition!.alliedCombat;
      const ignoredAlly =
        occupyingLife !== undefined &&
        occupyingLife.actorId.teamId === turn.actorId.teamId &&
        !alliedCombat.friendlyFireEnabled &&
        !alliedCombat.alliedProjectilesBlock;
      const contact = ignoredAlly ? undefined : occupyingLife;
      const shouldPersist =
        contact === undefined &&
        matchContract.rules.projectiles.maxTravelTiles !== 1;
      const persisted = postProjectiles.get(shot.projectileId);
      if (
        tickStartProjectileIds.has(shot.projectileId) ||
        !sameStrings(newOwnerTraversalIds, [shot.projectileId]) ||
        !sameStrings(
          newOwnerPostProjectileIds,
          shouldPersist ? [shot.projectileId] : [],
        ) ||
        !sameNullableActorId(
          shot.targetActorId,
          contact?.actorId ?? null,
        ) ||
        (persisted !== undefined) !== shouldPersist ||
        (persisted !== undefined &&
          (actorIdValue(persisted.ownerActorId) !== actorKey ||
            persisted.launchDirection !== shooter.facing ||
            persisted.heading !== heading ||
            persisted.shotProgram !== null ||
            persisted.programmedPath !== null ||
            !samePosition(persisted.position, spawn)))
      ) {
        fail(
          `${path}.postState.projectiles`,
          'turret projectile persistence and spawn contact must match public range and allied-contact rules',
        );
      }
    }

    const postUnit = tick.postState.teams
      .flatMap((team) => team.units)
      .find(
        (unit) =>
          unit.teamId === turn.actorId.teamId &&
          unit.unitId === turn.actorId.unitId,
      )!;
    const postLife =
      postUnit.activeLife &&
      actorIdValue(postUnit.activeLife.actorId) === actorKey
        ? postUnit.activeLife
        : null;
    if (!postLife) continue;

    let expectedEnergy = shooter.energy;
    const energyRules = matchContract.rules.energy;
    if (energyRules.enabled) {
      if (expectedEnergy === null) {
        fail(
          `${path}.tickStart.state`,
          'enabled turret energy requires a tick-start value',
        );
      }
      expectedEnergy -= energyRules.shotEnergyCost;
      if (
        energyRules.regenerationIntervalTicks > 0 &&
        (tick.tick + 1) % energyRules.regenerationIntervalTicks === 0 &&
        expectedEnergy < energyRules.maxEnergy
      ) {
        expectedEnergy = Math.min(
          energyRules.maxEnergy,
          expectedEnergy + energyRules.regenerationAmount,
        );
      }
    }
    const lastDamage = tick.resolution.events
      .filter(
        (event) =>
          event.type === 'damage' &&
          event.targetActorId !== null &&
          actorIdValue(event.targetActorId) === actorKey,
      )
      .at(-1);
    const expectedHealth =
      lastDamage?.newHealth ?? shooter.health;
    const creditedDamage = tick.resolution.events
      .filter(
        (event) =>
          event.type === 'damage' &&
          event.sourceActorId !== null &&
          actorIdValue(event.sourceActorId) === actorKey,
      )
      .reduce((total, event) => total + BigInt(event.amount ?? 0), 0n);
    const expectedDamage = (
      BigInt(shooter.damageDealt) + creditedDamage
    ).toString();
    if (
      postLife.formId !== shooter.formId ||
      !sameV2FormTransition(
        postLife.pendingFormTransition,
        shooter.pendingFormTransition,
      ) ||
      !samePosition(postLife.position, shooter.position) ||
      postLife.facing !== shooter.facing ||
      postLife.health !== expectedHealth ||
      postLife.cooldown !== turretForm.shootCooldownTicks ||
      postLife.energy !== expectedEnergy ||
      postLife.damageDealt !== expectedDamage ||
      postLife.previousActionResult !== 'success' ||
      postLife.spawnedAtTick !== shooter.spawnedAtTick
    ) {
      fail(
        `${path}.postState`,
        'surviving turret fire must preserve its exact life while applying standard health, damage, cooldown, energy, and action-result phases',
      );
    }
  }
}

function v2HeadingVector(
  heading: V2.ReplayV2ProjectileHeading,
): readonly [number, number] {
  switch (heading) {
    case 'north':
      return [0, -1];
    case 'north-east':
      return [1, -1];
    case 'east':
      return [1, 0];
    case 'south-east':
      return [1, 1];
    case 'south':
      return [0, 1];
    case 'south-west':
      return [-1, 1];
    case 'west':
      return [-1, 0];
    case 'north-west':
      return [-1, -1];
  }
}

function validateV2DecisionSemantics(
  turn: V2.ReplayV2ActorTurn,
  actionsById: ReadonlyMap<string, V2.ReplayV2ActionDefinition>,
  topologyUnitKeys: ReadonlySet<string>,
  formIds: ReadonlySet<string>,
  shotProgramRules: V2.ReplayV2ShotProgramRules,
  path: string,
): void {
  const accepted = turn.acceptedDecision;
  if (
    accepted.actionId === null ||
    accepted.actionCode === null ||
    accepted.faulted ||
    accepted.faultMessage !== null
  ) {
    fail(
      `${path}.acceptedDecision`,
      'must have a canonical non-faulted action selector',
    );
  }

  const acceptedAction = resolveV2ContractAction(
    accepted.actionId,
    accepted.actionCode,
    actionsById,
    `${path}.acceptedDecision`,
  );
  const chosenAction = resolveV2ContractAction(
    turn.actionResolution.chosenActionId,
    turn.actionResolution.chosenActionCode,
    actionsById,
    `${path}.actionResolution.chosenActionId`,
  );
  const validatedAction = resolveV2ContractAction(
    turn.actionResolution.validatedActionId,
    turn.actionResolution.validatedActionCode,
    actionsById,
    `${path}.actionResolution.validatedActionId`,
  );
  validateV2PayloadForAction(
    accepted.payload,
    acceptedAction,
    topologyUnitKeys,
    formIds,
    shotProgramRules,
    turn.actorId.teamId,
    `${path}.acceptedDecision.payload`,
  );
  validateV2PayloadForAction(
    turn.actionResolution.chosenPayload,
    chosenAction,
    topologyUnitKeys,
    formIds,
    shotProgramRules,
    turn.actorId.teamId,
    `${path}.actionResolution.chosenPayload`,
  );
  validateV2PayloadForAction(
    turn.actionResolution.validatedPayload,
    validatedAction,
    topologyUnitKeys,
    formIds,
    shotProgramRules,
    turn.actorId.teamId,
    `${path}.actionResolution.validatedPayload`,
  );

  if (
    accepted.actionId !== turn.actionResolution.chosenActionId ||
    accepted.actionCode !== turn.actionResolution.chosenActionCode ||
    !sameV2ActionPayload(
      accepted.payload,
      turn.actionResolution.chosenPayload,
    )
  ) {
    fail(
      `${path}.acceptedDecision`,
      'selector and payload must equal the chosen action resolution',
    );
  }
}

function resolveV2ContractAction(
  actionId: string,
  actionCode: number,
  actionsById: ReadonlyMap<string, V2.ReplayV2ActionDefinition>,
  path: string,
): V2.ReplayV2ActionDefinition {
  const action = actionsById.get(actionId);
  if (!action || action.code !== actionCode) {
    fail(path, 'action ID/code does not match the public contract');
  }
  return action;
}

function validateV2PayloadForAction(
  payload: V2.ReplayV2ActionPayload | null,
  action: V2.ReplayV2ActionDefinition,
  topologyUnitKeys: ReadonlySet<string>,
  formIds: ReadonlySet<string>,
  shotProgramRules: V2.ReplayV2ShotProgramRules,
  actorTeamId: number,
  path: string,
): void {
  if (!payload) {
    if (
      action.kind === 'fabrication' ||
      action.id === 'transform' ||
      action.id === 'shoot-direction'
    ) {
      fail(path, `${action.id} requires an explicit payload`);
    }
    return;
  }
  const kinds = new Set(action.parameterKinds);
  if (
    (payload.shotProgram !== null && !kinds.has('shot-program')) ||
    (payload.direction !== null && !kinds.has('direction')) ||
    (payload.launchHeading !== null &&
      !kinds.has('projectile-heading')) ||
    (payload.unitTarget !== null && !kinds.has('unit-target')) ||
    (payload.formTargetId !== null && !kinds.has('form-target'))
  ) {
    fail(path, `is inconsistent with action ${action.id}`);
  }
  if (
    payload.unitTarget &&
    !topologyUnitKeys.has(
      `${payload.unitTarget.teamId}:${payload.unitTarget.unitId}`,
    )
  ) {
    fail(`${path}.unitTarget`, 'must reference a topology unit');
  }
  if (
    payload.formTargetId !== null &&
    !formIds.has(payload.formTargetId)
  ) {
    fail(`${path}.formTargetId`, 'must reference a contract form');
  }
  if (
    payload.shotProgram !== null &&
    !isV2ShotProgramWithinRules(payload.shotProgram, shotProgramRules)
  ) {
    fail(`${path}.shotProgram`, 'is outside the public shot-program rules');
  }
  if (
    action.kind === 'fabrication' &&
    (payload.unitTarget === null ||
      payload.unitTarget.teamId !== actorTeamId ||
      payload.unitTarget.unitId === 0 ||
      payload.shotProgram !== null ||
      payload.direction !== null ||
      payload.launchHeading !== null ||
      payload.formTargetId !== null)
  ) {
    fail(
      path,
      'fabrication requires only an own-team unit target',
    );
  }
  if (
    action.id === 'transform' &&
    (payload.formTargetId === null ||
      payload.shotProgram !== null ||
      payload.direction !== null ||
      payload.launchHeading !== null ||
      payload.unitTarget !== null)
  ) {
    fail(path, 'transform requires only an explicit form target');
  }
  if (
    action.id === 'shoot-direction' &&
    (payload.launchHeading === null ||
      payload.shotProgram !== null ||
      payload.direction !== null ||
      payload.unitTarget !== null ||
      payload.formTargetId !== null)
  ) {
    fail(
      path,
      'shoot-direction requires only an explicit projectile heading',
    );
  }
}

function isV2ShotProgramWithinRules(
  program: V2.ReplayV2ShotProgram,
  rules: V2.ReplayV2ShotProgramRules,
): boolean {
  if (
    program.initialAimOffset < rules.minInitialAimOctants ||
    program.initialAimOffset > rules.maxInitialAimOctants
  ) {
    return false;
  }
  if (program.bendCount === 0) {
    return (
      program.bendDirection === rules.aimOnlyProgram.bendDirection &&
      program.bendAfterTiles === rules.aimOnlyProgram.bendAfterTiles &&
      program.bendEveryTiles === rules.aimOnlyProgram.bendEveryTiles
    );
  }
  return (
    rules.allowedCurvedBendDirections.includes(program.bendDirection) &&
    program.bendAfterTiles >= rules.minBendAfterTiles &&
    program.bendAfterTiles <= rules.maxBendAfterTiles &&
    program.bendEveryTiles >= rules.minBendEveryTiles &&
    program.bendEveryTiles <= rules.maxBendEveryTiles &&
    program.bendCount >= rules.minBendCount &&
    program.bendCount <= rules.maxBendCount
  );
}

function sameV2ActionPayload(
  left: V2.ReplayV2ActionPayload | null,
  right: V2.ReplayV2ActionPayload | null,
): boolean {
  if (left === null || right === null) return left === right;
  return (
    left.direction === right.direction &&
    left.launchHeading === right.launchHeading &&
    left.formTargetId === right.formTargetId &&
    sameV2UnitTarget(left.unitTarget, right.unitTarget) &&
    sameV2ShotProgram(left.shotProgram, right.shotProgram)
  );
}

function sameV2UnitTarget(
  left: V2.ReplayV2ObservedUnitTarget | null,
  right: V2.ReplayV2ObservedUnitTarget | null,
): boolean {
  if (left === null || right === null) return left === right;
  return left.teamId === right.teamId && left.unitId === right.unitId;
}

function sameUnitTargets(
  left: readonly V2.ReplayV2ObservedUnitTarget[],
  right: readonly V2.ReplayV2ObservedUnitTarget[],
): boolean {
  return (
    left.length === right.length &&
    left.every(
      (target, index) =>
        right[index] !== undefined &&
        sameV2UnitTarget(target, right[index]),
    )
  );
}

function sameNullableActorId(
  left: V2.ReplayV2ActorId | null,
  right: V2.ReplayV2ActorId | null,
): boolean {
  return (
    (left === null && right === null) ||
    (left !== null &&
      right !== null &&
      actorIdValue(left) === actorIdValue(right))
  );
}

function samePosition(
  left: Readonly<Model.ReplayPosition>,
  right: Readonly<Model.ReplayPosition>,
): boolean {
  return left.x === right.x && left.y === right.y;
}

function sameContractPosition(
  left: V2.ReplayV2ContractPosition,
  right: Readonly<Model.ReplayPosition>,
): boolean {
  return left[0] === right.x && left[1] === right.y;
}

function sameV2ShotProgram(
  left: V2.ReplayV2ShotProgram | null,
  right: V2.ReplayV2ShotProgram | null,
): boolean {
  if (left === null || right === null) return left === right;
  return (
    left.initialAimOffset === right.initialAimOffset &&
    left.bendDirection === right.bendDirection &&
    left.bendAfterTiles === right.bendAfterTiles &&
    left.bendEveryTiles === right.bendEveryTiles &&
    left.bendCount === right.bendCount
  );
}

function sameV2WorldState(
  left: V2.ReplayV2WorldState,
  right: V2.ReplayV2WorldState,
): boolean {
  const leftTeams = [...left.teams].sort(
    (first, second) => first.teamId - second.teamId,
  );
  const rightTeams = [...right.teams].sort(
    (first, second) => first.teamId - second.teamId,
  );
  return (
    sameV2Control(left.objective, right.objective) &&
    sameV2ProjectileSequences(left.projectiles, right.projectiles) &&
    leftTeams.length === rightTeams.length &&
    leftTeams.every((team, index) => {
      const other = rightTeams[index];
      if (
        !other ||
        team.teamId !== other.teamId ||
        team.damageDealt !== other.damageDealt
      ) {
        return false;
      }
      const units = [...team.units].sort(compareUnitIdentity);
      const otherUnits = [...other.units].sort(compareUnitIdentity);
      return (
        units.length === otherUnits.length &&
        units.every(
          (unit, unitIndex) =>
            otherUnits[unitIndex] !== undefined &&
            sameV2UnitState(unit, otherUnits[unitIndex]!),
        )
      );
    })
  );
}

function sameV2UnitState(
  left: V2.ReplayV2UnitState,
  right: V2.ReplayV2UnitState,
): boolean {
  return (
    left.teamId === right.teamId &&
    left.unitId === right.unitId &&
    left.defaultFormId === right.defaultFormId &&
    left.lifecycleStatus === right.lifecycleStatus &&
    left.respawnAtTick === right.respawnAtTick &&
    left.unlockAtTick === right.unlockAtTick &&
    left.rebuildReadyAtTick === right.rebuildReadyAtTick &&
    left.fabricationAtTick === right.fabricationAtTick &&
    sameNullablePosition(left.reservedSpawn, right.reservedSpawn) &&
    left.pendingSpawnReason === right.pendingSpawnReason &&
    left.hasSpawned === right.hasSpawned &&
    left.nextLifeId === right.nextLifeId &&
    left.damageDealt === right.damageDealt &&
    sameV2LifeState(left.activeLife, right.activeLife)
  );
}

function sameV2LifeState(
  left: V2.ReplayV2LifeState | null,
  right: V2.ReplayV2LifeState | null,
): boolean {
  if (left === null || right === null) return left === right;
  return (
    actorIdValue(left.actorId) === actorIdValue(right.actorId) &&
    left.formId === right.formId &&
    samePosition(left.position, right.position) &&
    left.facing === right.facing &&
    left.health === right.health &&
    left.cooldown === right.cooldown &&
    left.energy === right.energy &&
    left.damageDealt === right.damageDealt &&
    left.previousActionResult === right.previousActionResult &&
    left.spawnedAtTick === right.spawnedAtTick &&
    sameV2FormTransition(
      left.pendingFormTransition,
      right.pendingFormTransition,
    )
  );
}

function sameV2FormTransition(
  left: V2.ReplayV2FormTransition | null,
  right: V2.ReplayV2FormTransition | null,
): boolean {
  if (left === null || right === null) return left === right;
  return (
    left.fromFormId === right.fromFormId &&
    left.toFormId === right.toFormId &&
    left.startedAtTick === right.startedAtTick &&
    left.completesAtTick === right.completesAtTick
  );
}

function v2UnitEffectiveFormId(unit: V2.ReplayV2UnitState): string {
  return unit.activeLife?.formId ?? unit.defaultFormId;
}

function sameV2ProjectileSequences(
  left: readonly V2.ReplayV2ProjectileState[],
  right: readonly V2.ReplayV2ProjectileState[],
): boolean {
  const leftProjectiles = [...left].sort((first, second) =>
    compareDecimalStrings(first.projectileId, second.projectileId),
  );
  const rightProjectiles = [...right].sort((first, second) =>
    compareDecimalStrings(first.projectileId, second.projectileId),
  );
  return (
    leftProjectiles.length === rightProjectiles.length &&
    leftProjectiles.every(
      (projectile, index) =>
        rightProjectiles[index] !== undefined &&
        sameV2ProjectileState(projectile, rightProjectiles[index]!),
    )
  );
}

function sameV2ProjectileState(
  left: V2.ReplayV2ProjectileState,
  right: V2.ReplayV2ProjectileState,
): boolean {
  return (
    left.projectileId === right.projectileId &&
    actorIdValue(left.ownerActorId) === actorIdValue(right.ownerActorId) &&
    samePosition(left.position, right.position) &&
    left.launchDirection === right.launchDirection &&
    left.heading === right.heading &&
    sameV2ShotProgram(left.shotProgram, right.shotProgram) &&
    sameNullablePositionSequence(
      left.programmedPath,
      right.programmedPath,
    ) &&
    left.nextProgrammedPathIndex === right.nextProgrammedPathIndex &&
    left.tilesTraveled === right.tilesTraveled &&
    left.phase === right.phase
  );
}

function sameNullablePosition(
  left: Readonly<Model.ReplayPosition> | null,
  right: Readonly<Model.ReplayPosition> | null,
): boolean {
  if (left === null || right === null) return left === right;
  return samePosition(left, right);
}

function sameNullablePositionSequence(
  left: readonly V2.ReplayV2Position[] | null,
  right: readonly V2.ReplayV2Position[] | null,
): boolean {
  if (left === null || right === null) return left === right;
  return (
    left.length === right.length &&
    left.every(
      (position, index) =>
        right[index] !== undefined &&
        samePosition(position, right[index]!),
    )
  );
}

function sameV2Control(
  left: V2.ReplayV2ControlState,
  right: V2.ReplayV2ControlState,
): boolean {
  return (
    left.nextTick === right.nextTick &&
    left.activePositionIndex === right.activePositionIndex &&
    left.claimingTeamId === right.claimingTeamId &&
    left.captureProgress === right.captureProgress &&
    left.decayTicksElapsed === right.decayTicksElapsed &&
    left.controlResumesAtTick === right.controlResumesAtTick &&
    left.winnerTeamId === right.winnerTeamId
  );
}

function validateV2TerminalUnits(
  result: V2.ReplayV2Result,
  finalWorld: V2.ReplayV2WorldState,
  topologyUnits: readonly V2.ReplayV2TopologyUnitSlot[],
  path: string,
): void {
  const expectedUnitKeys = new Set(
    topologyUnits.map((unit) => `${unit.teamId}:${unit.unitId}`),
  );
  const actualUnitKeys = new Set<string>();

  for (const teamResult of result.teams) {
    const worldTeam = finalWorld.teams.find(
      (team) => team.teamId === teamResult.teamId,
    );
    if (!worldTeam) {
      fail(`${path}.teams`, 'terminal team is absent from final world');
    }
    ensureUnique(
      teamResult.units,
      (unit) => `${unit.teamId}:${unit.unitId}`,
      `${path}.teams.${teamResult.teamId}.units`,
    );
    if (
      teamResult.damageDealt !== worldTeam.damageDealt ||
      teamResult.activeHealth !==
        worldTeam.units.reduce(
          (sum, unit) => sum + (unit.activeLife?.health ?? 0),
          0,
        )
    ) {
      fail(
        `${path}.teams.${teamResult.teamId}`,
        'aggregate health or damage differs from the final world',
      );
    }

    for (const unitResult of teamResult.units) {
      const key = `${unitResult.teamId}:${unitResult.unitId}`;
      actualUnitKeys.add(key);
      const worldUnit = worldTeam.units.find(
        (unit) =>
          unit.teamId === unitResult.teamId &&
          unit.unitId === unitResult.unitId,
      );
      if (
        unitResult.teamId !== teamResult.teamId ||
        !expectedUnitKeys.has(key) ||
        !worldUnit ||
        unitResult.defaultFormId !== worldUnit.defaultFormId ||
        unitResult.formId !== v2UnitEffectiveFormId(worldUnit) ||
        unitResult.lifecycleStatus !== worldUnit.lifecycleStatus ||
        unitResult.health !== (worldUnit.activeLife?.health ?? 0) ||
        unitResult.damageDealt !== worldUnit.damageDealt ||
        (unitResult.activeActorId === null) !==
          (worldUnit.activeLife === null) ||
        !sameV2FormTransition(
          unitResult.pendingFormTransition,
          worldUnit.activeLife?.pendingFormTransition ?? null,
        ) ||
        (unitResult.activeActorId !== null &&
          worldUnit.activeLife !== null &&
          actorIdValue(unitResult.activeActorId) !==
            actorIdValue(worldUnit.activeLife.actorId))
      ) {
        fail(
          `${path}.teams.${teamResult.teamId}.units`,
          `unit ${key} differs from the final world`,
        );
      }
    }
  }

  if (!sameStringSet(actualUnitKeys, expectedUnitKeys)) {
    fail(`${path}.teams.units`, 'must cover exactly the topology units');
  }
}

function sameNumbers(
  left: readonly number[],
  right: readonly number[],
): boolean {
  return (
    left.length === right.length &&
    left.every((value, index) => value === right[index])
  );
}

function validateV2ObjectiveTeamReferences(
  objective: V2.ReplayV2ControlState,
  topologyTeamIds: ReadonlySet<number>,
  path: string,
): void {
  if (
    objective.claimingTeamId !== null &&
    !topologyTeamIds.has(objective.claimingTeamId)
  ) {
    fail(`${path}.claimingTeamId`, 'must reference a topology team');
  }
  if (
    objective.winnerTeamId !== null &&
    !topologyTeamIds.has(objective.winnerTeamId)
  ) {
    fail(`${path}.winnerTeamId`, 'must reference a topology team');
  }
}

function validateV2Aliases(
  turn: V2.ReplayV2ActorTurn,
  observableEventsById: ReadonlyMap<string, V2.ReplayV2Event>,
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
  const projectiles = new Map(
    turn.aliases.projectiles.map((alias) => [
      alias.projectileHandle,
      alias.projectileId,
    ]),
  );
  const events = new Map(
    turn.aliases.events.map((alias) => [
      alias.eventHandle,
      alias.eventId,
    ]),
  );
  const referencedLives = new Set<string>();
  const referencedProjectiles = new Set<string>();
  const referencedEvents = new Set<string>();
  const requireEnemy = (
    actor: V2.ReplayV2ObservedEnemyActorRef,
    actorPath: string,
  ): V2.ReplayV2ActorId => {
    referencedLives.add(actor.lifeHandle);
    const exact = lives.get(actor.lifeHandle);
    if (
      !exact ||
      exact.teamId !== actor.teamId ||
      exact.unitId !== actor.unitId ||
      actor.teamId === turn.actorId.teamId
    ) {
      fail(
        actorPath,
        'enemy life handle has no non-allied matching alias',
      );
    }
    return exact;
  };
  const requireEvent = (
    eventHandle: string,
    sourceTick: number,
    type: V2.ReplayV2ObservedEventType,
    eventPath: string,
  ): V2.ReplayV2Event => {
    referencedEvents.add(eventHandle);
    const eventId = events.get(eventHandle);
    const authoritative = eventId
      ? observableEventsById.get(eventId)
      : undefined;
    if (
      !authoritative ||
      authoritative.tick !== sourceTick ||
      authoritative.type !== type
    ) {
      fail(
        eventPath,
        'event handle must resolve to an observable authoritative event with exact tick and type',
      );
    }
    return authoritative;
  };

  turn.observation.enemies.forEach((enemy, index) =>
    requireEnemy(enemy.actor, `${path}.observation.enemies[${index}].actor`),
  );
  turn.observation.visibleProjectiles?.forEach((projectile, index) => {
    referencedProjectiles.add(projectile.projectileHandle);
    if (!projectiles.has(projectile.projectileHandle)) {
      fail(
        `${path}.observation.visibleProjectiles[${index}].projectileHandle`,
        'projectile handle has no matching alias',
      );
    }
    const hasAlliedOwner = projectile.alliedOwnerActorId !== null;
    const hasEnemyOwner = projectile.visibleEnemyOwner !== null;
    if (
      hasAlliedOwner === hasEnemyOwner ||
      (hasAlliedOwner &&
        (projectile.ownerTeamId !== turn.actorId.teamId ||
          projectile.alliedOwnerActorId?.teamId !==
            turn.actorId.teamId)) ||
      (hasEnemyOwner &&
        (projectile.ownerTeamId === turn.actorId.teamId ||
          projectile.visibleEnemyOwner?.teamId !==
            projectile.ownerTeamId))
    ) {
      fail(
        `${path}.observation.visibleProjectiles[${index}]`,
        'must expose exactly one owner representation consistent with ownerTeamId',
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
    const eventPath = `${path}.observation.visibleEvents[${index}]`;
    const authoritative = requireEvent(
      event.eventHandle,
      event.sourceTick,
      event.type,
      `${eventPath}.eventHandle`,
    );
    if (
      event.projectileHandle &&
      !projectiles.has(event.projectileHandle)
    ) {
      fail(
        `${path}.observation.visibleEvents[${index}].projectileHandle`,
        'projectile handle has no matching alias',
      );
    }
    if (event.projectileHandle) {
      referencedProjectiles.add(event.projectileHandle);
    }
    if (event.alliedActorId && event.enemyActor) {
      fail(
        `${path}.observation.visibleEvents[${index}]`,
        'cannot expose both allied and enemy actor identities',
      );
    }
    if (event.enemyActor) {
      requireEnemy(
        event.enemyActor,
        `${eventPath}.enemyActor`,
      );
    }
    const expectedActor =
      authoritative.type === 'damage' ||
      authoritative.type === 'destroyed'
        ? authoritative.targetActorId
        : authoritative.sourceActorId;
    if (expectedActor === null) {
      if (
        event.alliedActorId !== null ||
        event.enemyActor !== null
      ) {
        fail(
          eventPath,
          'an actorless authoritative event cannot gain an observed actor',
        );
      }
    } else if (expectedActor.teamId === turn.actorId.teamId) {
      if (
        event.alliedActorId === null ||
        actorIdValue(event.alliedActorId) !==
          actorIdValue(expectedActor) ||
        event.enemyActor !== null
      ) {
        fail(
          eventPath,
          'observed allied event actor must equal its authoritative actor',
        );
      }
    } else if (
      event.enemyActor === null ||
      actorIdValue(
        requireEnemy(event.enemyActor, `${eventPath}.enemyActor`),
      ) !== actorIdValue(expectedActor) ||
      event.alliedActorId !== null
    ) {
      fail(
        eventPath,
        'observed enemy event actor must resolve to its authoritative actor',
      );
    }

    const authoritativeProjectileId = authoritative.projectileId;
    if (
      authoritativeProjectileId === null
        ? event.projectileHandle !== null
        : event.projectileHandle === null ||
          projectiles.get(event.projectileHandle) !==
            authoritativeProjectileId
    ) {
      fail(
        `${eventPath}.projectileHandle`,
        'must resolve exactly to the authoritative event projectile',
      );
    }

    const exposesTransition =
      authoritative.type === 'form-transition-started' ||
      authoritative.type === 'form-changed' ||
      authoritative.type === 'form-transition-cancelled';
    const exposesAction =
      authoritative.type === 'shot' || exposesTransition;
    const expectedPosition = v2ObservedEventPosition(authoritative);
    const exposesFacing =
      authoritative.type === 'turn' ||
      authoritative.type === 'shot' ||
      authoritative.type === 'respawned' ||
      exposesTransition;
    const exposesHealth =
      authoritative.type === 'damage' ||
      authoritative.type === 'destroyed' ||
      authoritative.type === 'respawned' ||
      exposesTransition;
    if (
      event.teamId !== authoritative.teamId ||
      !sameNullablePosition(event.position, expectedPosition) ||
      event.facing !==
        (exposesFacing
          ? authoritative.toFacing ?? authoritative.fromFacing
          : null) ||
      event.projectileHeading !==
        (authoritative.type === 'shot'
          ? authoritative.projectileHeading
          : null) ||
      event.fromFormId !==
        (exposesTransition ? authoritative.fromFormId : null) ||
      event.toFormId !==
        (exposesTransition ? authoritative.toFormId : null) ||
      event.formTransitionStartedAtTick !==
        (exposesTransition
          ? authoritative.formTransitionStartedAtTick
          : null) ||
      event.formTransitionCompletesAtTick !==
        (exposesTransition
          ? authoritative.formTransitionCompletesAtTick
          : null) ||
      event.actionId !==
        (exposesAction ? authoritative.actionId : null) ||
      event.actionCode !==
        (exposesAction ? authoritative.actionCode : null) ||
      event.formTargetId !==
        (exposesTransition
          ? authoritative.actionPayload?.formTargetId ?? null
          : null) ||
      event.actionResult !==
        (exposesAction ? authoritative.actionResult : null) ||
      event.amount !==
        (authoritative.type === 'damage'
          ? authoritative.amount
          : null) ||
      event.newHealth !==
        (exposesHealth ? authoritative.newHealth : null)
    ) {
      fail(
        eventPath,
        'observed event state, action, heading, and form causality must exactly match its authoritative event',
      );
    }
  });
  turn.observation.heardSounds?.forEach((sound, index) => {
    requireEvent(
      sound.eventHandle,
      sound.sourceTick,
      sound.type,
      `${path}.observation.heardSounds[${index}].eventHandle`,
    );
  });
  if (!sameStringSet(new Set(lives.keys()), referencedLives)) {
    fail(
      `${path}.aliases.enemyLives`,
      'must exactly match enemy handles referenced by the observation',
    );
  }
  if (
    !sameStringSet(
      new Set(projectiles.keys()),
      referencedProjectiles,
    )
  ) {
    fail(
      `${path}.aliases.projectiles`,
      'must exactly match projectile handles referenced by the observation',
    );
  }
  if (
    !sameStringSet(new Set(events.keys()), referencedEvents)
  ) {
    fail(
      `${path}.aliases.events`,
      'must exactly match event handles referenced by the observation',
    );
  }
}

function v2ObservedEventPosition(
  event: V2.ReplayV2Event,
): V2.ReplayV2Position | null {
  switch (event.type) {
    case 'respawned':
    case 'fabrication-queued':
    case 'fabricated':
      return event.to;
    case 'turn':
    case 'move':
    case 'move-blocked':
    case 'shot':
    case 'damage':
    case 'destroyed':
    case 'form-transition-started':
    case 'form-changed':
    case 'form-transition-cancelled':
      return event.from;
    case 'fabrication-unlocked':
    case 'rebuild-ready':
    case 'frontline-progress-changed':
    case 'frontline-position-advanced':
    case 'base-breached':
      return null;
  }
}

function sameStringSet(
  left: ReadonlySet<string>,
  right: ReadonlySet<string>,
): boolean {
  if (left.size !== right.size) return false;
  for (const value of left) {
    if (!right.has(value)) return false;
  }
  return true;
}

function validateV2InitialDeployment(
  contract: V2.ReplayV2MatchContract,
  world: V2.ReplayV2WorldState,
  path: string,
): void {
  const frontline = contract.rules.frontlineDefinition;
  if (!frontline) {
    fail(path, 'tick-zero deployment requires Frontline rules');
  }
  const initialLivesByUnit = new Map<
    string,
    V2.ReplayV2TopologyInitialLife
  >();
  for (const life of contract.topology.initialLives) {
    const key = `${life.teamId}:${life.unitId}`;
    if (initialLivesByUnit.has(key)) {
      fail(
        'replay.header.contract.topology.initialLives',
        `contains multiple initial lives for stable unit ${key}`,
      );
    }
    initialLivesByUnit.set(key, life);
  }

  for (const unit of world.teams.flatMap((team) => team.units)) {
    const expectedFormId =
      unit.unitId === 0
        ? frontline.deployment.primeDefaultFormId
        : frontline.deployment.childDefaultFormId;
    const initial = initialLivesByUnit.get(
      `${unit.teamId}:${unit.unitId}`,
    );
    const hasInitialLife = initial !== undefined;
    if (
      unit.defaultFormId !== expectedFormId ||
      (unit.unitId === 0) !== hasInitialLife ||
      (initial !== undefined &&
        (initial.formId !== expectedFormId ||
          unit.activeLife === null ||
          unit.activeLife.formId !== expectedFormId ||
          actorIdValue(unit.activeLife.actorId) !==
            actorIdValue(initial)))
    ) {
      fail(
        `${path}.teams.${unit.teamId}.units.${unit.unitId}`,
        'must use its deployment default form and exact initial-life topology',
      );
    }
  }
}

function validateV2LifecycleTransition(
  contract: V2.ReplayV2MatchContract,
  before: V2.ReplayV2WorldState,
  after: V2.ReplayV2WorldState,
  events: readonly V2.ReplayV2Event[],
  tick: number,
  path: string,
): void {
  const beforeTeams = [...before.teams].sort(
    (left, right) => left.teamId - right.teamId,
  );
  const afterTeams = [...after.teams].sort(
    (left, right) => left.teamId - right.teamId,
  );
  if (
    !sameV2Control(before.objective, after.objective) ||
    !sameV2ProjectileSequences(before.projectiles, after.projectiles) ||
    beforeTeams.length !== afterTeams.length ||
    beforeTeams.some(
      (team, index) =>
        team.teamId !== afterTeams[index]?.teamId ||
        team.damageDealt !== afterTeams[index]?.damageDealt,
    )
  ) {
    fail(
      path,
      'lifecycle application may change only stable-unit lifecycle state',
    );
  }

  const beforeUnits = new Map(
    before.teams
      .flatMap((team) => team.units)
      .map((unit) => [`${unit.teamId}:${unit.unitId}`, unit]),
  );
  const afterUnits = new Map(
    after.teams
      .flatMap((team) => team.units)
      .map((unit) => [`${unit.teamId}:${unit.unitId}`, unit]),
  );
  const transitions = new Map<string, V2.ReplayV2Event>();
  for (const event of events) {
    if (event.teamId === null || event.unitId === null) {
      fail(path, 'every lifecycle event requires a stable team/unit identity');
    }
    const key = `${event.teamId}:${event.unitId}`;
    if (transitions.has(key)) {
      fail(path, `contains duplicate lifecycle transitions for unit ${key}`);
    }
    if (!beforeUnits.has(key) || !afterUnits.has(key)) {
      fail(path, `lifecycle event references unknown stable unit ${key}`);
    }
    transitions.set(key, event);
  }

  for (const [key, prior] of beforeUnits) {
    const current = afterUnits.get(key);
    if (!current) {
      fail(path, `lifecycle transition dropped stable unit ${key}`);
    }
    const event = transitions.get(key);
    if (!event) {
      if (!sameV2UnitState(prior, current)) {
        fail(path, `unit ${key} changed without a lifecycle event`);
      }
      continue;
    }

    let coherent = false;
    switch (event.type) {
      case 'fabrication-unlocked':
        coherent =
          prior.lifecycleStatus === 'locked' &&
          prior.unlockAtTick === tick &&
          sameV2UnitState(current, {
            ...prior,
            lifecycleStatus: 'ready',
          });
        break;
      case 'rebuild-ready':
        coherent =
          prior.lifecycleStatus === 'rebuilding' &&
          prior.rebuildReadyAtTick === tick &&
          sameV2UnitState(current, {
            ...prior,
            lifecycleStatus: 'ready',
            rebuildReadyAtTick: null,
          });
        break;
      case 'fabricated':
        coherent = validateV2SpawnedUnit(
          contract,
          prior,
          current,
          event,
          tick,
          false,
        );
        break;
      case 'respawned':
        coherent = validateV2SpawnedUnit(
          contract,
          prior,
          current,
          event,
          tick,
          true,
        );
        break;
    }
    if (!coherent) {
      fail(path, `lifecycle transition for unit ${key} does not match its event`);
    }
  }
}

function validateV2SpawnedUnit(
  contract: V2.ReplayV2MatchContract,
  before: V2.ReplayV2UnitState,
  after: V2.ReplayV2UnitState,
  event: V2.ReplayV2Event,
  tick: number,
  primeRespawn: boolean,
): boolean {
  const frontline = contract.rules.frontlineDefinition;
  const map = contract.map.frontline;
  const life = after.activeLife;
  if (!frontline || !map || !life) return false;

  const expectedFormId = primeRespawn
    ? frontline.deployment.primeDefaultFormId
    : frontline.deployment.childDefaultFormId;
  const form = contract.rules.forms.find(
    (candidate) => candidate.id === expectedFormId,
  );
  const home = map.teamHomes.find(
    (candidate) => candidate.teamId === after.teamId,
  );
  const expectedPosition = primeRespawn
    ? home?.primeSpawn ?? null
    : before.reservedSpawn;
  const expectedReason = primeRespawn
    ? 'respawn'
    : before.pendingSpawnReason;

  return (
    form !== undefined &&
    home !== undefined &&
    expectedPosition !== null &&
    before.activeLife === null &&
    before.unitId === after.unitId &&
    before.teamId === after.teamId &&
    (primeRespawn ? before.unitId === 0 : before.unitId > 0) &&
    before.lifecycleStatus ===
      (primeRespawn ? 'respawning' : 'fabrication-queued') &&
    (primeRespawn
      ? before.respawnAtTick === tick
      : before.fabricationAtTick === tick) &&
    event.spawnReason === expectedReason &&
    sameNullableActorId(event.sourceActorId, life.actorId) &&
    life.actorId.teamId === after.teamId &&
    life.actorId.unitId === after.unitId &&
    life.actorId.lifeId === before.nextLifeId &&
    samePosition(life.position, expectedPosition) &&
    life.facing === home.primeSpawn.facing &&
    life.health === form.maxHealth &&
    event.to !== null &&
    samePosition(event.to, life.position) &&
    event.toFacing === life.facing &&
    event.newHealth === life.health &&
    life.cooldown === 0 &&
    life.energy ===
      (contract.rules.energy.enabled
        ? contract.rules.energy.maxEnergy
        : null) &&
    life.damageDealt === '0' &&
    life.previousActionResult === 'none' &&
    life.spawnedAtTick === tick &&
    after.lifecycleStatus === 'active' &&
    after.nextLifeId === before.nextLifeId + 1 &&
    after.respawnAtTick === null &&
    after.rebuildReadyAtTick === null &&
    after.fabricationAtTick === null &&
    after.reservedSpawn === null &&
    after.pendingSpawnReason === null &&
    after.hasSpawned &&
    after.damageDealt === before.damageDealt &&
    after.unlockAtTick === before.unlockAtTick &&
    after.defaultFormId === expectedFormId &&
    life.formId === expectedFormId &&
    life.pendingFormTransition === null
  );
}

function validateV2ControlState(
  state: V2.ReplayV2ControlState,
  frontline: V2.ReplayV2FrontlineDefinition,
  topologyTeamIds: readonly number[],
  path: string,
): void {
  const topologyTeams = new Set(topologyTeamIds);
  const winnerAdvance =
    state.winnerTeamId === null
      ? null
      : frontline.victory.teamAdvances.find(
          (advance) => advance.teamId === state.winnerTeamId,
        ) ?? null;
  const invalid =
    state.nextTick < 0 ||
    state.activePositionIndex < 0 ||
    state.activePositionIndex >= frontline.frontlinePositionCount ||
    (state.claimingTeamId !== null &&
      !topologyTeams.has(state.claimingTeamId)) ||
    (state.winnerTeamId !== null &&
      !topologyTeams.has(state.winnerTeamId)) ||
    state.captureProgress < 0 ||
    state.captureProgress >= frontline.capture.threshold ||
    state.decayTicksElapsed < 0 ||
    state.decayTicksElapsed >= frontline.capture.decayIntervalTicks ||
    state.controlResumesAtTick < 0 ||
    (state.claimingTeamId === null &&
      (state.captureProgress !== 0 ||
        state.decayTicksElapsed !== 0)) ||
    (state.claimingTeamId !== null && state.captureProgress === 0) ||
    (state.nextTick < state.controlResumesAtTick &&
      (state.claimingTeamId !== null ||
        state.captureProgress !== 0 ||
        state.decayTicksElapsed !== 0)) ||
    (state.winnerTeamId !== null &&
      (winnerAdvance === null ||
        state.activePositionIndex !==
          (winnerAdvance.positionIndexDelta > 0
            ? frontline.frontlinePositionCount - 1
            : 0) ||
        state.claimingTeamId !== null ||
        state.captureProgress !== 0 ||
        state.decayTicksElapsed !== 0 ||
        state.controlResumesAtTick > state.nextTick));
  if (invalid) {
    fail(path, 'Frontline control state violates canonical invariants');
  }
}

function validateV2WorldRelationships(
  world: V2.ReplayV2WorldState,
  topologyUnitKeys: ReadonlySet<string>,
  topologyTeamIds: readonly number[],
  tick: number,
  frontline: V2.ReplayV2FrontlineDefinition | null,
  frontlineMap: V2.ReplayV2FrontlineMapDefinition | null,
  formsById: ReadonlyMap<string, V2.ReplayV2FormDefinition>,
  path: string,
): void {
  ensureUnique(world.teams, (team) => team.teamId, `${path}.teams`);
  const worldTeamIds = world.teams
    .map((team) => team.teamId)
    .sort(compareNumber);
  if (!sameNumbers(worldTeamIds, topologyTeamIds)) {
    fail(`${path}.teams`, 'must cover exactly the topology team IDs');
  }
  validateV2ObjectiveTeamReferences(
    world.objective,
    new Set(topologyTeamIds),
    `${path}.objective`,
  );
  if (frontline) {
    validateV2ControlState(
      world.objective,
      frontline,
      topologyTeamIds,
      `${path}.objective`,
    );
  }
  ensureUnique(
    world.projectiles,
    (projectile) => projectile.projectileId,
    `${path}.projectiles`,
  );
  const actorKeys: string[] = [];
  const worldUnitKeys: string[] = [];
  const occupied = new Set<string>();
  const reserved = new Set<string>();
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
      worldUnitKeys.push(`${unit.teamId}:${unit.unitId}`);
      if (frontline) {
        const expectedFormId =
          unit.unitId === 0
            ? frontline.deployment.primeDefaultFormId
            : frontline.deployment.childDefaultFormId;
        if (unit.defaultFormId !== expectedFormId) {
          fail(
            `${path}.teams.${team.teamId}.units.${unit.unitId}.defaultFormId`,
            'must equal the deployment default for this stable slot',
          );
        }
      }
      validateV2UnitLifecycle(
        unit,
        tick,
        `${path}.teams.${team.teamId}.units.${unit.unitId}`,
      );
      if (unit.activeLife) {
        const life = unit.activeLife;
        const actor = life.actorId;
        if (actor.teamId !== unit.teamId || actor.unitId !== unit.unitId) {
          fail(`${path}.teams.units.activeLife`, 'actor/unit identity mismatch');
        }
        if (
          frontline &&
          (unit.unitId === 0
            ? life.formId !== unit.defaultFormId
            : life.formId !== unit.defaultFormId &&
              life.formId !== frontline.anchor.targetFormId)
        ) {
          fail(
            `${path}.teams.${team.teamId}.units.${unit.unitId}.activeLife.formId`,
            'must be the stable default or the irreversible Anchor target form',
          );
        }
        const currentForm = formsById.get(life.formId);
        if (
          !currentForm ||
          life.health <= 0 ||
          life.health > currentForm.maxHealth
        ) {
          fail(
            `${path}.teams.${team.teamId}.units.${unit.unitId}.activeLife`,
            'current form and positive health must match the contract form',
          );
        }
        const pending = life.pendingFormTransition;
        if (
          pending &&
          (!frontline ||
            life.formId !== pending.fromFormId ||
            pending.fromFormId !== frontline.anchor.sourceFormId ||
            pending.toFormId !== frontline.anchor.targetFormId ||
            pending.completesAtTick !==
              pending.startedAtTick +
                frontline.anchor.windupTicks -
                1 ||
            pending.startedAtTick < 0 ||
            pending.startedAtTick >= world.objective.nextTick ||
            pending.completesAtTick < world.objective.nextTick ||
            frontlineMap?.anchorForbiddenTiles.some((position) =>
              sameContractPosition(position, life.position),
            ))
        ) {
          fail(
            `${path}.teams.${team.teamId}.units.${unit.unitId}.activeLife.pendingFormTransition`,
            'must preserve the canonical source form and inclusive windup chronology',
          );
        }
        actorKeys.push(actorIdValue(actor));
        const position = `${life.position.x}:${life.position.y}`;
        if (occupied.has(position)) {
          fail(path, 'active unit positions must be unique');
        }
        occupied.add(position);
      }
      if (unit.reservedSpawn) {
        const home = frontlineMap?.teamHomes.find(
          (candidate) => candidate.teamId === unit.teamId,
        );
        const position = `${unit.reservedSpawn.x}:${unit.reservedSpawn.y}`;
        if (
          !home ||
          samePosition(unit.reservedSpawn, home.primeSpawn) ||
          !home.protectedSpawnPad.some((candidate) =>
            sameContractPosition(candidate, unit.reservedSpawn!),
          ) ||
          reserved.has(position)
        ) {
          fail(
            `${path}.teams.${team.teamId}.units.${unit.unitId}.reservedSpawn`,
            'must be a unique non-Prime tile on the unit team protected pad',
          );
        }
        reserved.add(position);
      }
    }
  }
  if (new Set(actorKeys).size !== actorKeys.length) {
    fail(path, 'active actor identities must be unique');
  }
  if (
    !sameStringSet(new Set(worldUnitKeys), new Set(topologyUnitKeys))
  ) {
    fail(`${path}.teams.units`, 'must cover exactly the topology units');
  }
  for (const position of reserved) {
    if (occupied.has(position)) {
      fail(path, 'fabrication reservations must be unoccupied');
    }
  }
}

function validateV2UnitLifecycle(
  unit: V2.ReplayV2UnitState,
  tick: number,
  path: string,
): void {
  const active = unit.activeLife !== null;
  if ((unit.lifecycleStatus === 'active') !== active) {
    fail(
      path,
      'active lifecycle status and activeLife must agree exactly',
    );
  }
  if (
    unit.nextLifeId < 0 ||
    (unit.activeLife !== null &&
      unit.activeLife.actorId.lifeId !== unit.nextLifeId - 1)
  ) {
    fail(`${path}.nextLifeId`, 'is inconsistent with the active life');
  }
  if (
    unit.activeLife &&
    (unit.activeLife.spawnedAtTick < 0 ||
      unit.activeLife.spawnedAtTick > tick)
  ) {
    fail(`${path}.activeLife.spawnedAtTick`, 'is outside world chronology');
  }

  const noFabricationReservation =
    unit.fabricationAtTick === null &&
    unit.reservedSpawn === null &&
    unit.pendingSpawnReason === null;
  switch (unit.lifecycleStatus) {
    case 'active':
      if (
        unit.respawnAtTick !== null ||
        unit.rebuildReadyAtTick !== null ||
        !noFabricationReservation ||
        !unit.hasSpawned
      ) {
        fail(path, 'active unit has stale lifecycle scheduling state');
      }
      break;
    case 'respawning':
      if (
        unit.unitId !== 0 ||
        unit.respawnAtTick === null ||
        unit.respawnAtTick <= tick ||
        unit.rebuildReadyAtTick !== null ||
        !noFabricationReservation ||
        !unit.hasSpawned
      ) {
        fail(path, 'invalid respawn lifecycle scheduling state');
      }
      break;
    case 'locked':
      if (
        unit.unitId === 0 ||
        unit.unlockAtTick === null ||
        unit.unlockAtTick <= tick ||
        unit.respawnAtTick !== null ||
        unit.rebuildReadyAtTick !== null ||
        !noFabricationReservation ||
        unit.hasSpawned ||
        unit.nextLifeId !== 0
      ) {
        fail(path, 'invalid locked lifecycle scheduling state');
      }
      break;
    case 'ready':
      if (
        unit.unitId === 0 ||
        unit.respawnAtTick !== null ||
        unit.rebuildReadyAtTick !== null ||
        !noFabricationReservation
      ) {
        fail(path, 'ready unit has stale lifecycle scheduling state');
      }
      break;
    case 'fabrication-queued':
      if (
        unit.unitId === 0 ||
        unit.respawnAtTick !== null ||
        unit.rebuildReadyAtTick !== null ||
        unit.fabricationAtTick === null ||
        unit.fabricationAtTick <= tick ||
        unit.reservedSpawn === null ||
        unit.pendingSpawnReason === null ||
        unit.pendingSpawnReason !==
          (unit.hasSpawned ? 'rebuild' : 'fabrication')
      ) {
        fail(path, 'invalid queued fabrication lifecycle state');
      }
      break;
    case 'rebuilding':
      if (
        unit.unitId === 0 ||
        unit.respawnAtTick !== null ||
        unit.rebuildReadyAtTick === null ||
        unit.rebuildReadyAtTick <= tick ||
        !noFabricationReservation ||
        !unit.hasSpawned
      ) {
        fail(path, 'invalid rebuilding lifecycle scheduling state');
      }
      break;
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
  return normalizeReplayV1Internal(document);
}

function normalizeReplayV1Internal(
  document: V1.ReplayV1Document,
  seedOverride?: V1SeedNormalization,
): Model.ReplayModel {
  const participants = [...document.header.participants]
    .sort((left, right) => left.slot - right.slot)
    .map<Model.ReplayParticipantController>((participant) => ({
      participantKey: replayParticipantKey(participant.slot),
      participantId: participant.slot,
      teamKey: replayTeamKey(participant.slot),
      teamId: participant.slot,
      classId: null,
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
    classId: null,
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
    regions: [],
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
                  compareOrdinal(left.family, right.family),
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
  const normalizedSeed = seedOverride ?? {
    decimal: String(document.header.seed),
    exact: Number.isSafeInteger(document.header.seed),
  };

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
    seed: normalizedSeed.decimal,
    seedExact: normalizedSeed.exact,
    seedEncoding: 'legacy-json-number',
    partial: 'partial' in document && document.partial === true,
    replayHash: document.replayHash ?? null,
    matchContractFingerprint: null,
    contract: legacyContractFromV1(document.header),
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

function legacyContractFromV1(
  header: V1.ReplayV1Header,
): Model.ReplayLegacyPartialMatchContract {
  const participants = [...header.participants].sort(
    (left, right) => left.slot - right.slot,
  );
  const objectiveMode =
    header.controlPressureLimit !== undefined
      ? 'shared-pressure'
      : header.zoneTiles !== undefined
        ? 'zone-ticks'
        : 'none';

  return {
    kind: 'legacy-partial',
    completeness: 'legacy-partial',
    schemaVersion: null,
    matchContractFingerprint: null,
    rules: {
      schemaVersion: null,
      rulesetId: header.gameRulesVersion,
      rulesFingerprint: null,
      limits: {
        maxTicks: header.maxTicks,
        faultLimit: null,
        teamCount: participants.length,
        participantCount: participants.length,
        unitSlotCount: participants.length,
        initialUnitsPerTeam: 1,
        maxUnitsPerTeam: 1,
        destructionEndsMatch: null,
        respawnsEnabled: null,
      },
      objective: {
        mode: objectiveMode,
        zoneTiles:
          header.zoneTiles === undefined
            ? null
            : header.zoneTiles
                .map(positionFromTuple)
                .sort(comparePosition),
        zoneDominationTicks: null,
        zoneExclusiveAccrual: null,
        sharedPressureEnabled:
          header.controlPressureLimit !== undefined,
        controlBySoleOccupancy:
          header.controlBySoleOccupancy ?? null,
        controlPressureLimit: header.controlPressureLimit ?? null,
        controlPressureGain: null,
        controlPressureDecayInterval: null,
        overtime: {
          startTick: header.controlOvertimeStartTick ?? null,
          pressureLimit:
            header.controlOvertimePressureLimit ?? null,
          pressureGain: header.controlOvertimePressureGain ?? null,
          stopsDecay: header.controlOvertimeStopsDecay ?? null,
        },
        maxTickTiebreakers: null,
      },
      frontlineDefinition: null,
      energy: null,
      forms: null,
      actions: null,
      projectiles: null,
      shotPrograms: {
        enabled: header.programmedShots ?? null,
        limits: header.programmedShotLimits
          ? { ...header.programmedShotLimits }
          : null,
      },
      vision: {
        range: header.visionRange,
        shape:
          header.visionCone === undefined
            ? null
            : header.visionCone
              ? 'facing-quadrant'
              : 'omnidirectional',
        distanceMetric: null,
        omnidirectionalProximityRange: null,
        lineOfSight: null,
        hearingRadius: null,
        hearingBearingSectors: null,
        hearingDistanceBandUpperBounds: null,
        loudEventTypes: null,
      },
      collisions: null,
      tickResolution: null,
      legacyMaxHealth: header.maxHealth ?? null,
    },
    map: {
      schemaVersion: null,
      mapId: header.mapId,
      mapVersion: header.mapVersion,
      mapFingerprint: null,
      formatVersion: null,
      width: header.mapWidth,
      height: header.mapHeight,
      tileRows: [...header.mapTiles],
      spawns: participants.map((participant) => ({
        teamId: participant.slot,
        position: {
          x: participant.spawnX,
          y: participant.spawnY,
        },
        facing: directionFromV1(participant.spawnFacing),
      })),
      objectiveTiles:
        header.zoneTiles === undefined
          ? null
          : header.zoneTiles
              .map(positionFromTuple)
              .sort(comparePosition),
      frontline: null,
    },
    topology: {
      teamCount: participants.length,
      participantCount: participants.length,
      unitSlotCount: participants.length,
      initialLifeCount: participants.length,
      teams: participants.map((participant) => ({
        teamId: participant.slot,
        teamKey: replayTeamKey(participant.slot),
        classId: null,
      })),
      participants: participants.map((participant) => ({
        participantId: participant.slot,
        participantKey: replayParticipantKey(participant.slot),
        teamId: participant.slot,
        teamKey: replayTeamKey(participant.slot),
        classId: null,
      })),
      unitSlots: participants.map((participant) => {
        const actor = replayDuelIdentity(participant.slot);
        return {
          teamId: participant.slot,
          teamKey: replayTeamKey(participant.slot),
          unitId: 0,
          unitKey: actor.unitKey,
          controllerParticipantId: participant.slot,
          controllerParticipantKey: replayParticipantKey(
            participant.slot,
          ),
        };
      }),
      initialLives: participants.map((participant) => {
        const actor = replayDuelIdentity(participant.slot);
        return {
          teamId: participant.slot,
          unitId: 0,
          lifeId: 0,
          actorKey: actor.actorKey,
          unitKey: actor.unitKey,
          formId: 'legacy-mobile',
        };
      }),
    },
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
        pendingFormTransition: null,
        status: lifecycleFromV1(state.status),
      };
    });
  const units = participantSlots.map<Model.ReplayUnitState>((slot) => {
    const identity = replayDuelIdentity(slot);
    const state = stateBySlot.get(slot);
    const lifecycleStatus = state
      ? lifecycleFromV1(state.status)
      : 'destroyed';
    return {
      unitKey: identity.unitKey,
      teamKey: replayTeamKey(slot),
      teamId: slot,
      unitId: 0,
      defaultFormId: 'legacy-mobile',
      formId: 'legacy-mobile',
      lifecycleStatus,
      respawnAtTick: null,
      unlockAtTick: null,
      rebuildReadyAtTick: null,
      fabricationAtTick: null,
      reservedSpawn: null,
      pendingSpawnReason: null,
      hasSpawned: true,
      nextLifeId: 1,
      damageDealt: null,
      activeActorKey:
        lifecycleStatus === 'active' ? identity.actorKey : null,
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
    unlockAtTick: null,
    rebuildReadyAtTick: null,
    fabricationAtTick: null,
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
            classId: null,
            formId: 'legacy-mobile',
            position: { x: enemy.x, y: enemy.y },
            facing: directionFromV1(enemy.facing),
            health: enemy.health,
            cooldown: null,
            energy: null,
            previousActionResult: null,
            pendingFormTransition: null,
            observedBy: [identity.actorKey],
            // Replay-v1 predates every battlefield economy.
            carriedScrap: 0,
            // Per-life generations have no way to publish one.
            roleTag: null,
          };
        }),
      visibleTiles: turn.visibleTiles
        .map(positionFromTuple)
        .sort(comparePosition)
        .map((position) => ({
          position,
          isWall: header.mapTiles[position.y]?.[position.x] === '#',
          observedBy: [identity.actorKey],
          spawnReservation: null,
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
            launchHeading: null,
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
            launchHeading: null,
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
            launchHeading: null,
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
            launchHeading: null,
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
    unitId: sourceActor?.unitId ?? targetActor?.unitId ?? null,
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
    fromFormId: null,
    toFormId: null,
    formTransitionStartedAtTick: null,
    formTransitionCompletesAtTick: null,
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
    spawnReason: null,
    respawnAtTick: null,
    unlockAtTick: null,
    rebuildReadyAtTick: null,
    fabricationAtTick: null,
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
        activeHealth: bot.finalHealth,
        damageDealt: String(bot.damageDealt),
        units: [
          {
            unitKey: replayDuelIdentity(bot.slot).unitKey,
            teamId: bot.slot,
            unitId: 0,
            defaultFormId: 'legacy-mobile',
            formId: 'legacy-mobile',
            lifecycleStatus: lifecycleFromV1(bot.finalStatus),
            activeActor: null,
            activeActorKey: null,
            health: bot.finalHealth,
            damageDealt: String(bot.damageDealt),
            pendingFormTransition: null,
          },
        ],
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
    classId: null,
    formId: state.formId,
    position: copyPosition(state.position),
    facing: state.facing,
    health: state.health,
    cooldown: state.cooldown,
    energy: state.energy,
    previousActionResult: state.previousActionResult,
    pendingFormTransition: state.pendingFormTransition,
    observedBy,
    // Authoritative world lives carry no load in any wire; only a replay-v3
    // observation publishes one.
    carriedScrap: 0,
    // Per-life generations have no way to publish one.
    roleTag: null,
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
      classId: null,
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
      classId: null,
      participantKeys: participants
        .filter((participant) => participant.teamId === team.teamId)
        .map((participant) => participant.participantKey),
      unitKeys: units
        .filter((unit) => unit.teamId === team.teamId)
        .map((unit) => unit.unitKey),
    }));

  const forms = [...contract.rules.forms]
    .sort((left, right) => compareOrdinal(left.id, right.id))
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
    seedExact: true,
    seedEncoding: 'decimal-string',
    partial: document.partial,
    replayHash: document.replayHash,
    matchContractFingerprint: contract.matchContractFingerprint,
    contract: contractFromV2(contract),
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

function contractFromV2(
  contract: V2.ReplayV2MatchContract,
): Model.ReplayExactMatchContract {
  const { rules, map, topology } = contract;
  return {
    kind: 'v2-full',
    completeness: 'exact',
    schemaVersion: contract.schemaVersion,
    matchContractFingerprint: contract.matchContractFingerprint,
    rules: {
      schemaVersion: rules.schemaVersion,
      rulesetId: rules.rulesetId,
      rulesFingerprint: rules.rulesFingerprint,
      limits: { ...rules.limits },
      objective: {
        ...rules.objective,
        overtime: { ...rules.objective.overtime },
        maxTickTiebreakers: [...rules.objective.maxTickTiebreakers],
      },
      frontlineDefinition: rules.frontlineDefinition
        ? {
            ...rules.frontlineDefinition,
            capture: { ...rules.frontlineDefinition.capture },
            victory: {
              ...rules.frontlineDefinition.victory,
              teamAdvances: [
                ...rules.frontlineDefinition.victory.teamAdvances,
              ]
                .sort((left, right) => left.teamId - right.teamId)
                .map((advance) => ({ ...advance })),
            },
            lifecycle: {
              ...rules.frontlineDefinition.lifecycle,
              fabricationUnlockTicks: [
                ...rules.frontlineDefinition.lifecycle
                  .fabricationUnlockTicks,
              ],
            },
            deployment: { ...rules.frontlineDefinition.deployment },
            fabrication: { ...rules.frontlineDefinition.fabrication },
            anchor: { ...rules.frontlineDefinition.anchor },
            turretFire: {
              ...rules.frontlineDefinition.turretFire,
              allowedProjectileHeadings: [
                ...rules.frontlineDefinition.turretFire
                  .allowedProjectileHeadings,
              ],
            },
            alliedCombat: {
              ...rules.frontlineDefinition.alliedCombat,
            },
          }
        : null,
      energy: { ...rules.energy },
      forms: [...rules.forms]
        .sort((left, right) => compareOrdinal(left.id, right.id))
        .map((form) => ({
          ...form,
          allowedActionIds: [...form.allowedActionIds].sort(
            compareOrdinal,
          ),
        })),
      actions: [...rules.actions]
        .sort(
          (left, right) =>
            left.code - right.code ||
            compareOrdinal(left.id, right.id),
        )
        .map((action) => ({
          ...action,
          parameterKinds: [...action.parameterKinds],
        })),
      projectiles: { ...rules.projectiles },
      shotPrograms: {
        ...rules.shotPrograms,
        aimOnlyProgram: { ...rules.shotPrograms.aimOnlyProgram },
        allowedCurvedBendDirections: [
          ...rules.shotPrograms.allowedCurvedBendDirections,
        ],
        defaultProgram: copyShotProgram(
          rules.shotPrograms.defaultProgram,
        ),
      },
      vision: {
        ...rules.vision,
        hearingDistanceBandUpperBounds: [
          ...rules.vision.hearingDistanceBandUpperBounds,
        ],
        loudEventTypes: [...rules.vision.loudEventTypes],
      },
      collisions: { ...rules.collisions },
      tickResolution: {
        ...rules.tickResolution,
        phases: [...rules.tickResolution.phases],
      },
    },
    map: {
      schemaVersion: map.schemaVersion,
      mapId: map.mapId,
      mapVersion: map.mapVersion,
      mapFingerprint: map.mapFingerprint,
      formatVersion: map.formatVersion,
      width: map.width,
      height: map.height,
      tileRows: [...map.tileRows],
      spawns: [...map.spawns]
        .sort((left, right) => left.teamId - right.teamId)
        .map((spawn) => ({
          teamId: spawn.teamId,
          position: { x: spawn.x, y: spawn.y },
          facing: spawn.facing,
        })),
      objectiveTiles: map.objectiveTiles
        .map(positionFromTuple)
        .sort(comparePosition),
      frontline: map.frontline
        ? {
            positions: [...map.frontline.positions]
              .sort(
                (left, right) =>
                  left.positionIndex - right.positionIndex,
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
            anchorForbiddenTiles:
              map.frontline.anchorForbiddenTiles
                .map(positionFromTuple)
                .sort(comparePosition),
          }
        : null,
    },
    topology: {
      teamCount: topology.teamCount,
      participantCount: topology.participantCount,
      unitSlotCount: topology.unitSlotCount,
      initialLifeCount: topology.initialLifeCount,
      teams: [...topology.teams]
        .sort((left, right) => left.teamId - right.teamId)
        .map((team) => ({
          teamId: team.teamId,
          teamKey: replayTeamKey(team.teamId),
          classId: null,
        })),
      participants: [...topology.participants]
        .sort(compareParticipant)
        .map((participant) => ({
          participantId: participant.participantId,
          participantKey: replayParticipantKey(
            participant.participantId,
          ),
          teamId: participant.teamId,
          teamKey: replayTeamKey(participant.teamId),
          classId: null,
        })),
      unitSlots: [...topology.unitSlots]
        .sort(compareUnitIdentity)
        .map((unit) => ({
          teamId: unit.teamId,
          teamKey: replayTeamKey(unit.teamId),
          unitId: unit.unitId,
          unitKey: frontlineUnitKey(unit.teamId, unit.unitId),
          controllerParticipantId: unit.controllerParticipantId,
          controllerParticipantKey: replayParticipantKey(
            unit.controllerParticipantId,
          ),
        })),
      initialLives: [...topology.initialLives]
        .sort(compareActorIdentity)
        .map((life) => {
          const actor = actorIdentityFromV2(life);
          return {
            teamId: life.teamId,
            unitId: life.unitId,
            lifeId: life.lifeId,
            actorKey: actor.actorKey,
            unitKey: actor.unitKey,
            formId: life.formId,
          };
        }),
    },
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
    regions: [],
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
                compareOrdinal(left.family, right.family),
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
        defaultFormId: unit.defaultFormId,
        formId: v2UnitEffectiveFormId(unit),
        lifecycleStatus: unit.lifecycleStatus,
        respawnAtTick: unit.respawnAtTick,
        unlockAtTick: unit.unlockAtTick,
        rebuildReadyAtTick: unit.rebuildReadyAtTick,
        fabricationAtTick: unit.fabricationAtTick,
        reservedSpawn: unit.reservedSpawn
          ? copyPosition(unit.reservedSpawn)
          : null,
        pendingSpawnReason: unit.pendingSpawnReason,
        hasSpawned: unit.hasSpawned,
        nextLifeId: unit.nextLifeId,
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
    formId: life.formId,
    position: copyPosition(life.position),
    facing: life.facing,
    health: life.health,
    cooldown: life.cooldown,
    energy: life.energy,
    damageDealt: life.damageDealt,
    previousActionResult: life.previousActionResult,
    spawnedAtTick: life.spawnedAtTick,
    pendingFormTransition: copyV2FormTransition(
      life.pendingFormTransition,
    ),
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
        launchHeading: payload.launchHeading,
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
        unlockAtTick: unit.unlockAtTick,
        rebuildReadyAtTick: unit.rebuildReadyAtTick,
        fabricationAtTick: unit.fabricationAtTick,
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
        classId: null,
        formId: enemy.formId,
        position: copyPosition(enemy.position),
        facing: enemy.facing,
        health: enemy.health,
        cooldown: null,
        energy: null,
        previousActionResult: null,
        pendingFormTransition: copyV2FormTransition(
          enemy.pendingFormTransition,
        ),
        carriedScrap: 0,
        // Per-life generations have no way to publish one.
        roleTag: null,
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
        spawnReservation: null,
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
        projectileHeading: event.projectileHeading,
        fromFormId: event.fromFormId,
        toFormId: event.toFormId,
        formTransitionStartedAtTick:
          event.formTransitionStartedAtTick,
        formTransitionCompletesAtTick:
          event.formTransitionCompletesAtTick,
        actionId: event.actionId,
        actionCode: event.actionCode,
        formTargetId: event.formTargetId,
        actionResult: event.actionResult,
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
          compareOrdinal(left.actionId, right.actionId),
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
        allowedProjectileHeadings:
          action.allowedProjectileHeadings === null
            ? null
            : [...action.allowedProjectileHeadings],
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
    classId: null,
    formId: observed.formId,
    position: copyPosition(observed.position),
    facing: observed.facing,
    health: observed.health,
    cooldown: observed.cooldown,
    energy: observed.energy,
    previousActionResult: observed.previousActionResult,
    pendingFormTransition: copyV2FormTransition(
      observed.pendingFormTransition,
    ),
    observedBy,
    // Internal replay-v2 predates the scrap economy.
    carriedScrap: 0,
    // Per-life generations have no way to publish one.
    roleTag: null,
  };
}

function copyV2FormTransition(
  transition: V2.ReplayV2FormTransition | null,
): Model.ReplayFormTransition | null {
  return transition ? { ...transition } : null;
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
    unitId: event.unitId,
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
    fromFormId: event.fromFormId,
    toFormId: event.toFormId,
    formTransitionStartedAtTick:
      event.formTransitionStartedAtTick,
    formTransitionCompletesAtTick:
      event.formTransitionCompletesAtTick,
    actionPayload: actionPayloadFromV2(event.actionPayload),
    actionId: event.actionId,
    actionCode: event.actionCode,
    actionResult: event.actionResult,
    amount: event.amount,
    newHealth: event.newHealth,
    lifecycleStatus: event.lifecycleStatus,
    spawnReason: event.spawnReason,
    respawnAtTick: event.respawnAtTick,
    unlockAtTick: event.unlockAtTick,
    rebuildReadyAtTick: event.rebuildReadyAtTick,
    fabricationAtTick: event.fabricationAtTick,
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
        activeHealth: team.activeHealth,
        damageDealt: team.damageDealt,
        units: [...team.units]
          .sort((left, right) => left.unitId - right.unitId)
          .map((unit) => {
            const activeActor = unit.activeActorId
              ? actorIdentityFromV2(unit.activeActorId)
              : null;
            return {
              unitKey: frontlineUnitKey(unit.teamId, unit.unitId),
              teamId: unit.teamId,
              unitId: unit.unitId,
              defaultFormId: unit.defaultFormId,
              formId: unit.formId,
              lifecycleStatus: unit.lifecycleStatus,
              activeActor,
              activeActorKey: activeActor?.actorKey ?? null,
              health: unit.health,
              damageDealt: unit.damageDealt,
              pendingFormTransition: copyV2FormTransition(
                unit.pendingFormTransition,
              ),
            };
          }),
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

function compareOrdinal(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}
