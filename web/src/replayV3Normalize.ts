import {
  replayGenericIdentity,
  replayParticipantKey,
  replayTeamKey,
} from './replayModel';
import type * as Model from './replayModel';
import type * as V3 from './replayWireV3';

export type ReplayV3Fail = (path: string, message: string) => never;

const own = (value: object, key: string): boolean =>
  Object.prototype.hasOwnProperty.call(value, key);

function object(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    fail(path, 'expected an object');
  }
  return value as Record<string, unknown>;
}

function exact(
  value: unknown,
  path: string,
  keys: readonly string[],
  fail: ReplayV3Fail,
): Record<string, unknown> {
  const item = object(value, path, fail);
  const allowed = new Set(keys);
  for (const key of Object.keys(item)) {
    if (!allowed.has(key)) fail(`${path}.${key}`, 'unknown property');
  }
  for (const key of keys) {
    if (!own(item, key)) fail(`${path}.${key}`, 'missing required property');
  }
  return item;
}

function array(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): unknown[] {
  if (!Array.isArray(value)) fail(path, 'expected an array');
  return value;
}

function string(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): asserts value is string {
  if (typeof value !== 'string') fail(path, 'expected a string');
}

function nonEmpty(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): asserts value is string {
  string(value, path, fail);
  if (value.length === 0) fail(path, 'must not be empty');
}

function semanticId(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): asserts value is string {
  string(value, path, fail);
  if (
    value.length === 0 ||
    value.length > 64 ||
    !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value)
  ) {
    fail(path, 'expected a 1-64 character lowercase-kebab semantic ID');
  }
}

function integer(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): asserts value is number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value)) {
    fail(path, 'expected a safe integer');
  }
}

function boolean(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): asserts value is boolean {
  if (typeof value !== 'boolean') fail(path, 'expected a boolean');
}

function nullable(
  value: unknown,
  path: string,
  validator: (value: unknown, path: string, fail: ReplayV3Fail) => void,
  fail: ReplayV3Fail,
): void {
  if (value !== null) validator(value, path, fail);
}

function canonicalUnsigned(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
  max: bigint,
): asserts value is string {
  if (
    typeof value !== 'string' ||
    !/^(0|[1-9][0-9]*)$/.test(value) ||
    BigInt(value) > max
  ) {
    fail(path, 'expected a canonical non-negative decimal string');
  }
}

function uint64(value: unknown, path: string, fail: ReplayV3Fail): void {
  canonicalUnsigned(value, path, fail, 18_446_744_073_709_551_615n);
}

function int64(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
  nonNegative = false,
): void {
  if (typeof value !== 'string' || !/^(0|-?[1-9][0-9]*)$/.test(value)) {
    fail(path, 'expected a canonical signed 64-bit decimal string');
  }
  const parsed = BigInt(value);
  if (
    parsed < -9_223_372_036_854_775_808n ||
    parsed > 9_223_372_036_854_775_807n ||
    (nonNegative && parsed < 0n)
  ) {
    fail(path, 'expected a canonical signed 64-bit decimal string');
  }
}

function direction(value: unknown, path: string, fail: ReplayV3Fail): void {
  if (!['north', 'east', 'south', 'west'].includes(String(value))) {
    fail(path, 'expected a cardinal direction');
  }
}

/**
 * A movement profile's optional facing coupling. The engine's canonical
 * writer omits the property entirely while the profile preserves facing —
 * the same omit-when-inert discipline the capture-gain schedule uses — so an
 * absent field means 'preserve-facing' and an explicitly inert one is a
 * second, non-canonical encoding of the same contract.
 */
function movementFacingCoupling(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  if (value === 'preserve-facing') {
    fail(path, 'must be omitted instead of emitted inert');
  }
  if (
    ![
      'face-movement-direction',
      'facing-locked',
      'face-movement-heading-projected',
      'combat-strafe',
    ].includes(String(value))
  ) {
    fail(
      path,
      'expected a known non-inert movement/facing coupling',
    );
  }
}

function heading(value: unknown, path: string, fail: ReplayV3Fail): void {
  if (
    ![
      'north',
      'north-east',
      'east',
      'south-east',
      'south',
      'south-west',
      'west',
      'north-west',
    ].includes(String(value))
  ) {
    fail(path, 'expected an eight-way projectile heading');
  }
}

function actorId(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(value, path, ['teamId', 'unitId', 'lifeId'], fail);
  integer(item.teamId, `${path}.teamId`, fail);
  integer(item.unitId, `${path}.unitId`, fail);
  integer(item.lifeId, `${path}.lifeId`, fail);
}

function position(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(value, path, ['x', 'y'], fail);
  integer(item.x, `${path}.x`, fail);
  integer(item.y, `${path}.y`, fail);
}

function contractPosition(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const tuple = array(value, path, fail);
  if (tuple.length !== 2) fail(path, 'expected a two-item position tuple');
  integer(tuple[0], `${path}[0]`, fail);
  integer(tuple[1], `${path}[1]`, fail);
}

function shotProgram(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(
    value,
    path,
    [
      'initialAimOffset',
      'bendDirection',
      'bendAfterTiles',
      'bendEveryTiles',
      'bendCount',
    ],
    fail,
  );
  for (const key of Object.keys(item)) integer(item[key], `${path}.${key}`, fail);
}

function jsonValue(value: unknown, path: string, fail: ReplayV3Fail): void {
  if (
    value === null ||
    typeof value === 'string' ||
    typeof value === 'boolean'
  ) {
    return;
  }
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value)) fail(path, 'expected a safe JSON integer');
    return;
  }
  if (Array.isArray(value)) {
    value.forEach((entry, index) =>
      jsonValue(entry, `${path}[${index}]`, fail),
    );
    return;
  }
  const item = object(value, path, fail);
  for (const [key, entry] of Object.entries(item)) {
    jsonValue(entry, `${path}.${key}`, fail);
  }
}

function validateRankings(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  array(value, path, fail).forEach((entry, index) => {
    const ranking = exact(
      entry,
      `${path}[${index}]`,
      ['channel', 'direction'],
      fail,
    );
    nonEmpty(ranking.channel, `${path}[${index}].channel`, fail);
    if (
      ranking.direction !== 'higher-wins' &&
      ranking.direction !== 'lower-wins'
    ) {
      fail(`${path}[${index}].direction`, 'unknown ranking direction');
    }
  });
}

/// The one redeploy policy that carries a territory-ratchet hold, and
/// therefore the only one whose observations may publish hold clocks.
/**
 * The participant-scoped MIND profile. It is the one thing that decides which
 * turn record a tick carries, and the memo is explicit that it must be read
 * from the header rather than inferred from the payload (§5.1).
 */
export const MIND_CONTRACT_PROFILE_ID = 'generic-mind-match-1';

const RATCHET_REDEPLOY_POLICY =
  'advance-immediately-then-deny-enemy-regression-past-the-high-water-mark-through-configured-hold-ticks';

/// The one control policy that channels a capture, and therefore the only one
/// that carries a stationary multiplier cap, an opposing erosion multiple, and
/// a claim interrupt. All three ride together or not at all.
const CHANNEL_CONTROL_POLICY =
  'stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-opposition-erodes-at-multiple-then-builds';

function validateContract(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const contract = exact(
    value,
    path,
    [
      'schemaVersion',
      'matchContractFingerprint',
      'capabilityVersions',
      'rules',
      'map',
      'format',
      'topology',
      'initialDeployment',
      'lifecycleAssignments',
      'participantRegionAssignments',
      'modeMapBinding',
    ],
    fail,
  );
  integer(contract.schemaVersion, `${path}.schemaVersion`, fail);
  nonEmpty(
    contract.matchContractFingerprint,
    `${path}.matchContractFingerprint`,
    fail,
  );
  const capabilities = exact(
    contract.capabilityVersions,
    `${path}.capabilityVersions`,
    [
      'contractProfileId',
      'runtimeProtocolVersion',
      'runtimeConfigurationVersion',
      'runtimeContractVersion',
      'matchStartSchemaVersion',
      'observationSchemaVersion',
      'decisionSchemaVersion',
      'matchContractSchemaVersion',
    ],
    fail,
  );
  for (const key of [
    'contractProfileId',
    'runtimeProtocolVersion',
    'runtimeConfigurationVersion',
  ]) {
    nonEmpty(capabilities[key], `${path}.capabilityVersions.${key}`, fail);
  }
  for (const key of [
    'runtimeContractVersion',
    'matchStartSchemaVersion',
    'observationSchemaVersion',
    'decisionSchemaVersion',
    'matchContractSchemaVersion',
  ]) {
    integer(capabilities[key], `${path}.capabilityVersions.${key}`, fail);
  }

  const rules = exact(
    contract.rules,
    `${path}.rules`,
    [
      'schemaVersion',
      'rulesetId',
      'rulesFingerprint',
      'limits',
      'seedMechanics',
      'gameMode',
      'lifecycle',
      'forms',
      'movementProfiles',
      'visionProfiles',
      'attackProfiles',
      'actions',
      'fabricationTransitions',
      'sameLifeTransitions',
      'replicationTransitions',
      'teamPerception',
      'collisions',
      'tickResolution',
    ],
    fail,
  );
  integer(rules.schemaVersion, `${path}.rules.schemaVersion`, fail);
  nonEmpty(rules.rulesetId, `${path}.rules.rulesetId`, fail);
  nonEmpty(rules.rulesFingerprint, `${path}.rules.rulesFingerprint`, fail);
  for (const key of [
    'limits',
    'seedMechanics',
    'lifecycle',
    'teamPerception',
    'collisions',
    'tickResolution',
  ]) {
    jsonValue(rules[key], `${path}.rules.${key}`, fail);
  }
  const limits = object(rules.limits, `${path}.rules.limits`, fail);
  integer(limits.maxTicks, `${path}.rules.limits.maxTicks`, fail);
  const modePath = `${path}.rules.gameMode`;
  const mode = object(rules.gameMode, modePath, fail);
  string(mode.kind, `${modePath}.kind`, fail);
  const modeKeys =
    mode.kind === 'deathmatch'
      ? ['kind', 'modeId', 'victory', 'scoreCatalog', 'scoring']
      : mode.kind === 'frontline'
        ? [
            'kind',
            'modeId',
            'victory',
            'scoreCatalog',
            'frontlinePositionCount',
            'capture',
            // Additive trailing optional block with the capture ratchet's
            // discipline: the engine writes it only for a mode that declares
            // a side objective, so its presence is itself a contract fact.
            ...(own(mode, 'secondaryControl') ? ['secondaryControl'] : []),
            // Same discipline for the battlefield economy: written only for
            // a mode that declares one, and mutually exclusive with the side
            // objective, because both claim the side lanes' attention.
            ...(own(mode, 'scrapEconomy') ? ['scrapEconomy'] : []),
          ]
        : mode.kind === 'arc-relay'
          ? [
              'kind',
              'modeId',
              'victory',
              'scoreCatalog',
              'pendingRearmTicks',
              'coreRelocationIntervalTicks',
              'coresPerPulse',
              'fieldedSlotsPerTeam',
              'maxCopiesPerClass',
              'respawnDelayTicks',
              // Grammar 2 (owner ruling 2026-08-05): dodgeable signature
              // physics plus designed-role metadata on each signature.
              ...(own(mode, 'signatureGrammarVersion')
                ? ['signatureGrammarVersion']
                : []),
              // Foundations -03: seed-derived well-birth jitter half-width,
              // written only when non-zero.
              ...(own(mode, 'wellBirthJitterTicks')
                ? ['wellBirthJitterTicks']
                : []),
              // Foundations -03: parity-alternating resolution, written only
              // when true.
              ...(own(mode, 'alternatingResolutionOrder')
                ? ['alternatingResolutionOrder']
                : []),
              // Threefold Pulse prototype: per-origin sockets, written only
              // when true.
              ...(own(mode, 'threefoldSockets') ? ['threefoldSockets'] : []),
              'wells',
              'signatures',
            ]
          : null;
  if (modeKeys === null) {
    fail(`${modePath}.kind`, `unknown game mode ${String(mode.kind)}`);
  }
  exact(mode, modePath, modeKeys, fail);
  nonEmpty(mode.modeId, `${modePath}.modeId`, fail);
  const supportedModeId =
    mode.kind === 'arc-relay' ? 'arc-relay-h0' : mode.kind;
  if (mode.modeId !== supportedModeId) {
    fail(
      `${modePath}.modeId`,
      'must match the supported game-mode kind',
    );
  }
  const scoreCatalog = array(
    mode.scoreCatalog,
    `${modePath}.scoreCatalog`,
    fail,
  );
  scoreCatalog.forEach((entry, index) => {
    const score = exact(
      entry,
      `${modePath}.scoreCatalog[${index}]`,
      ['channel', 'domain'],
      fail,
    );
    nonEmpty(
      score.channel,
      `${modePath}.scoreCatalog[${index}].channel`,
      fail,
    );
    if (score.domain !== 'non-negative' && score.domain !== 'signed') {
      fail(
        `${modePath}.scoreCatalog[${index}].domain`,
        'unknown score value domain',
      );
    }
  });
  const victoryPath = `${modePath}.victory`;
  if (mode.kind === 'deathmatch') {
    const deathmatchChannels = [
      'kills',
      'deaths',
      'damage-dealt',
      'active-health',
    ] as const;
    if (scoreCatalog.length === 0) {
      fail(
        `${modePath}.scoreCatalog`,
        'Deathmatch requires a non-empty score catalog',
      );
    }
    scoreCatalog.forEach((entry, index) => {
      const score = entry as Record<string, unknown>;
      const channelIndex = deathmatchChannels.indexOf(
        score.channel as (typeof deathmatchChannels)[number],
      );
      if (
        channelIndex < 0 ||
        score.domain !== 'non-negative' ||
        (index > 0 &&
          channelIndex <= deathmatchChannels.indexOf(
            (scoreCatalog[index - 1] as Record<string, unknown>)
              .channel as (typeof deathmatchChannels)[number],
          ))
      ) {
        fail(
          `${modePath}.scoreCatalog[${index}]`,
          'Deathmatch score channels must be a unique canonical-order subset of kills, deaths, damage-dealt, and active-health with non-negative domains',
        );
      }
    });
    const victory = exact(
      mode.victory,
      victoryPath,
      [
        'kind',
        'timeoutRanking',
        'killsToWin',
        'terminalTickPrecedence',
      ],
      fail,
    );
    if (victory.kind !== 'deathmatch') {
      fail(`${victoryPath}.kind`, 'must match the deathmatch mode');
    }
    validateRankings(
      victory.timeoutRanking,
      `${victoryPath}.timeoutRanking`,
      fail,
    );
    const timeoutRankings = array(
      victory.timeoutRanking,
      `${victoryPath}.timeoutRanking`,
      fail,
    ).map((ranking) => ranking as Record<string, unknown>);
    if (
      timeoutRankings.length === 0 ||
      timeoutRankings[0]!.channel !== 'kills' ||
      timeoutRankings[0]!.direction !== 'higher-wins'
    ) {
      fail(
        `${victoryPath}.timeoutRanking`,
        'Deathmatch timeout ranking must begin with higher kills',
      );
    }
    const rankedChannels = new Set<string>();
    timeoutRankings.forEach((ranking, index) => {
      const channel = ranking.channel as string;
      if (
        rankedChannels.has(channel) ||
        !scoreCatalog.some(
          (score) =>
            (score as Record<string, unknown>).channel === channel,
        )
      ) {
        fail(
          `${victoryPath}.timeoutRanking[${index}].channel`,
          'must be unique and reference a declared Deathmatch score channel',
        );
      }
      rankedChannels.add(channel);
    });
    nullable(victory.killsToWin, `${victoryPath}.killsToWin`, integer, fail);
    if (
      typeof victory.killsToWin === 'number' &&
      victory.killsToWin <= 0
    ) {
      fail(`${victoryPath}.killsToWin`, 'must be positive when present');
    }
    nonEmpty(
      victory.terminalTickPrecedence,
      `${victoryPath}.terminalTickPrecedence`,
      fail,
    );
    if (
      victory.terminalTickPrecedence !==
      'kill-limit-after-complete-joint-tick-before-max-tick-timeout'
    ) {
      fail(
        `${victoryPath}.terminalTickPrecedence`,
        'does not match the supported Deathmatch completion precedence',
      );
    }
    const scoring = exact(
      mode.scoring,
      `${modePath}.scoring`,
      [
        'deathIncrement',
        'killIncrement',
        'alliedFinalDamage',
        'damageDealtIncrement',
        'activeHealthSnapshot',
        'nonDamageRetirement',
        'earlyKillLimitResolution',
      ],
      fail,
    );
    for (const key of Object.keys(scoring)) {
      nonEmpty(scoring[key], `${modePath}.scoring.${key}`, fail);
    }
    const fixedScoring = {
      deathIncrement:
        'one-raw-death-to-destroyed-actor-team-per-damage-caused-destruction',
      killIncrement:
        'one-raw-kill-to-exact-hostile-health-to-zero-damage-source-team',
      alliedFinalDamage: 'victim-team-death-no-kill',
      damageDealtIncrement:
        'hostile-actual-health-removed-to-exact-source-team',
      activeHealthSnapshot: 'terminal-sum-across-active-team-lives',
      nonDamageRetirement:
        'replication-retirement-adds-neither-death-nor-kill',
      earlyKillLimitResolution:
        'complete-joint-tick-then-highest-raw-kills-win-tied-top-draw',
    } as const;
    for (const [key, expected] of Object.entries(fixedScoring)) {
      if (scoring[key] !== expected) {
        fail(
          `${modePath}.scoring.${key}`,
          `expected ${expected}`,
        );
      }
    }
  } else if (mode.kind === 'frontline') {
    const victory = exact(
      mode.victory,
      victoryPath,
      ['kind', 'timeoutRanking', 'pushesToBreach'],
      fail,
    );
    if (victory.kind !== 'frontline') {
      fail(`${victoryPath}.kind`, 'must match the frontline mode');
    }
    validateRankings(
      victory.timeoutRanking,
      `${victoryPath}.timeoutRanking`,
      fail,
    );
    integer(victory.pushesToBreach, `${victoryPath}.pushesToBreach`, fail);
    integer(
      mode.frontlinePositionCount,
      `${modePath}.frontlinePositionCount`,
      fail,
    );
    if (
      (victory.pushesToBreach as number) <= 0 ||
      (mode.frontlinePositionCount as number) < 3 ||
      (mode.frontlinePositionCount as number) % 2 === 0 ||
      (victory.pushesToBreach as number) * 2 - 1 !==
        mode.frontlinePositionCount
    ) {
      fail(
        `${modePath}.frontlinePositionCount`,
        'must be odd, at least three, and equal pushesToBreach * 2 - 1',
      );
    }
    const capturePath = `${modePath}.capture`;
    const captureValue = object(mode.capture, capturePath, fail);
    const hasGainSchedule = own(captureValue, 'gainSchedule');
    // Additive optional field with the capture-gain schedule's discipline:
    // the engine writes a hold duration only for the high-water-mark
    // redeploy policy, so its presence is itself part of the contract.
    const hasRatchetHold = own(captureValue, 'ratchetHoldTicks');
    // The capture channel's three trailing settings, with the same
    // discipline: the engine writes them only for the channel control
    // policy, so their presence is itself part of the contract.
    const hasStackCap = own(captureValue, 'stationaryGainMultiplierCap');
    const hasErosionMultiplier = own(
      captureValue,
      'opposingErosionMultiplier',
    );
    const hasClaimInterrupt = own(captureValue, 'claimInterrupt');
    const capture = exact(
      captureValue,
      capturePath,
      [
        'threshold',
        'gainPerSoleTeamTick',
        ...(hasGainSchedule ? ['gainSchedule'] : []),
        'decayAmount',
        'decayIntervalTicks',
        'redeployPauseTicks',
        'controlPolicy',
        'timeoutPolicy',
        'territorialProgressFormula',
        'completionPolicy',
        'initialPosition',
        'captureArithmetic',
        'oppositionArithmetic',
        'decayClock',
        'disabledDecay',
        'redeployPolicy',
        ...(hasRatchetHold ? ['ratchetHoldTicks'] : []),
        'redeployTickArithmetic',
        ...(hasStackCap ? ['stationaryGainMultiplierCap'] : []),
        ...(hasErosionMultiplier ? ['opposingErosionMultiplier'] : []),
        ...(hasClaimInterrupt ? ['claimInterrupt'] : []),
      ],
      fail,
    );
    for (const key of [
      'threshold',
      'gainPerSoleTeamTick',
      'decayAmount',
      'decayIntervalTicks',
      'redeployPauseTicks',
    ]) {
      integer(capture[key], `${capturePath}.${key}`, fail);
    }
    if (
      (capture.threshold as number) <= 0 ||
      (capture.gainPerSoleTeamTick as number) <= 0 ||
      (capture.decayAmount as number) < 0 ||
      (capture.decayIntervalTicks as number) < 0 ||
      (capture.redeployPauseTicks as number) < 0 ||
      ((capture.decayAmount as number) === 0) !==
        ((capture.decayIntervalTicks as number) === 0)
    ) {
      fail(
        capturePath,
        'contains invalid threshold, gain, decay, or redeploy tuning',
      );
    }
    if (hasGainSchedule) {
      const schedule = array(
        capture.gainSchedule,
        `${capturePath}.gainSchedule`,
        fail,
      );
      if (schedule.length === 0) {
        fail(
          `${capturePath}.gainSchedule`,
          'must be omitted instead of emitted empty',
        );
      }
      const phaseIds = new Set<string>();
      let priorStartTick = -1;
      schedule.forEach((value, index) => {
        const phasePath = `${capturePath}.gainSchedule[${index}]`;
        const phase = exact(
          value,
          phasePath,
          ['phaseId', 'startsAtTick', 'gainPerSoleTeamTick'],
          fail,
        );
        semanticId(phase.phaseId, `${phasePath}.phaseId`, fail);
        integer(phase.startsAtTick, `${phasePath}.startsAtTick`, fail);
        integer(
          phase.gainPerSoleTeamTick,
          `${phasePath}.gainPerSoleTeamTick`,
          fail,
        );
        if (phaseIds.has(phase.phaseId)) {
          fail(`${phasePath}.phaseId`, 'must be unique within the schedule');
        }
        phaseIds.add(phase.phaseId);
        if (
          (phase.startsAtTick as number) <= priorStartTick ||
          (phase.startsAtTick as number) >= (limits.maxTicks as number)
        ) {
          fail(
            `${phasePath}.startsAtTick`,
            'must be strictly increasing, non-negative, and before maxTicks',
          );
        }
        priorStartTick = phase.startsAtTick as number;
        if ((phase.gainPerSoleTeamTick as number) <= 0) {
          fail(`${phasePath}.gainPerSoleTeamTick`, 'must be positive');
        }
        if (
          index === 0 &&
          ((phase.startsAtTick as number) !== 0 ||
            phase.gainPerSoleTeamTick !== capture.gainPerSoleTeamTick)
        ) {
          fail(
            phasePath,
            'first phase must start at tick zero with the declared base gain',
          );
        }
      });
    }
    const fixedPolicies = {
      timeoutPolicy:
        'signed-position-threshold-plus-claim-zero-draw-no-tiebreakers',
      territorialProgressFormula:
        'per-team-advance-delta-times-index-offset-times-threshold-plus-signed-claim',
      completionPolicy: 'base-breach-before-max-ticks',
      initialPosition: 'centre-objective-index',
      captureArithmetic:
        'checked-int64-add-compare-threshold-completes-one-push-and-discards-overshoot',
      oppositionArithmetic:
        'erode-toward-zero-without-carrying-overshoot-into-own-claim',
      disabledDecay: 'zero-pair-preserves-claim-and-keeps-clock-zero',
      redeployTickArithmetic:
        'checked-int64-capture-tick-plus-one-plus-pause-require-int32',
    } as const;
    for (const [key, expected] of Object.entries(fixedPolicies)) {
      if (capture[key] !== expected) {
        fail(`${capturePath}.${key}`, `expected ${expected}`);
      }
    }
    // The three policies with pre-registered candidate arms. Each value is a
    // distinct ruleset with its own fingerprint; the viewer accepts any of
    // them and pins the rest of the capture contract as before.
    const policyArms = {
      controlPolicy: [
        'binary-positive-weight-per-team-no-stacking-non-sole-applies-configured-decay-opposition-erodes-to-neutral',
        'net-positive-objective-weight-difference-scales-gain-non-positive-applies-configured-decay-opposition-erodes-to-neutral',
        CHANNEL_CONTROL_POLICY,
      ],
      decayClock: [
        'consecutive-empty-or-contested-ticks-reset-by-any-sole-control',
        'empty-and-contested-ticks-preserve-claim-enemy-sole-erosion-only',
      ],
      redeployPolicy: [
        'advance-immediately-reset-claim-keep-world-pause-through-capture-plus-configured-ticks-breach-skips-pause',
        RATCHET_REDEPLOY_POLICY,
      ],
    } as const;
    for (const [key, allowed] of Object.entries(policyArms)) {
      if (!(allowed as readonly string[]).includes(String(capture[key]))) {
        fail(`${capturePath}.${key}`, `expected one of ${allowed.join(', ')}`);
      }
    }
    if (hasRatchetHold) {
      integer(capture.ratchetHoldTicks, `${capturePath}.ratchetHoldTicks`, fail);
      if ((capture.ratchetHoldTicks as number) <= 0) {
        fail(
          `${capturePath}.ratchetHoldTicks`,
          'must be omitted instead of emitted inert',
        );
      }
    }
    if (hasRatchetHold !== (capture.redeployPolicy === RATCHET_REDEPLOY_POLICY)) {
      fail(
        `${capturePath}.ratchetHoldTicks`,
        'is carried by exactly the high-water-mark redeploy policy',
      );
    }
    const channels = capture.controlPolicy === CHANNEL_CONTROL_POLICY;
    if (
      hasStackCap !== channels ||
      hasErosionMultiplier !== channels ||
      hasClaimInterrupt !== channels
    ) {
      fail(
        `${capturePath}.claimInterrupt`,
        'the stationary cap, the erosion multiple, and the interrupt are carried by exactly the channel control policy',
      );
    }
    if (channels) {
      for (const key of [
        'stationaryGainMultiplierCap',
        'opposingErosionMultiplier',
      ]) {
        integer(capture[key], `${capturePath}.${key}`, fail);
        if ((capture[key] as number) <= 0) {
          fail(`${capturePath}.${key}`, 'must be positive');
        }
      }
      const interruptPath = `${capturePath}.claimInterrupt`;
      const interrupt = exact(
        object(capture.claimInterrupt, interruptPath, fail),
        interruptPath,
        ['kind', 'revertPerDamagePoint', 'scope', 'granularity'],
        fail,
      );
      integer(
        interrupt.revertPerDamagePoint,
        `${interruptPath}.revertPerDamagePoint`,
        fail,
      );
      if ((interrupt.revertPerDamagePoint as number) <= 0) {
        fail(`${interruptPath}.revertPerDamagePoint`, 'must be positive');
      }
      const interruptPolicies = {
        kind: 'damage-to-controller-on-objective-reverts-work',
        scope: 'controlling-team-bodies-on-active-objective-region',
        granularity: 'whole-run',
      } as const;
      for (const [key, expected] of Object.entries(interruptPolicies)) {
        if (interrupt[key] !== expected) {
          fail(`${interruptPath}.${key}`, `expected ${expected}`);
        }
      }
    }
    if (own(mode, 'secondaryControl')) {
      const secondaryPath = `${modePath}.secondaryControl`;
      const secondary = exact(
        object(mode.secondaryControl, secondaryPath, fail),
        secondaryPath,
        [
          'regionIds',
          'captureThresholdTicks',
          'ownership',
          'effect',
          'rallyScope',
        ],
        fail,
      );
      const regionIds = array(
        secondary.regionIds,
        `${secondaryPath}.regionIds`,
        fail,
      );
      regionIds.forEach((regionId, index) =>
        nonEmpty(regionId, `${secondaryPath}.regionIds[${index}]`, fail),
      );
      if (
        regionIds.length === 0 ||
        new Set(regionIds.map(String)).size !== regionIds.length
      ) {
        fail(
          `${secondaryPath}.regionIds`,
          'names at least one site region and never repeats one',
        );
      }
      integer(
        secondary.captureThresholdTicks,
        `${secondaryPath}.captureThresholdTicks`,
        fail,
      );
      if ((secondary.captureThresholdTicks as number) <= 0) {
        fail(
          `${secondaryPath}.captureThresholdTicks`,
          'must be a positive latch threshold',
        );
      }
      const secondaryArms = {
        ownership: ['latched-until-recaptured-by-sole-objective-weight'],
        effect: ['muster'],
        rallyScope: ['prime-automatic-return-only'],
      } as const;
      for (const [key, allowed] of Object.entries(secondaryArms)) {
        if (!(allowed as readonly string[]).includes(String(secondary[key]))) {
          fail(
            `${secondaryPath}.${key}`,
            `expected one of ${allowed.join(', ')}`,
          );
        }
      }
    }
    if (own(mode, 'scrapEconomy')) {
      if (own(mode, 'secondaryControl')) {
        fail(
          `${modePath}.scrapEconomy`,
          'a mode declares a side objective or a scrap economy, never both',
        );
      }
      const economyPath = `${modePath}.scrapEconomy`;
      const economy = exact(
        object(mode.scrapEconomy, economyPath, fail),
        economyPath,
        [
          'veinSites',
          'veinFirstSpawnTick',
          'veinSpawnIntervalTicks',
          'veinLastSpawnTick',
          'veinAmount',
          'wreckAmount',
          'assayAmount',
          'carryCapacity',
          'pileLifetimeTicks',
          'maxSimultaneousPiles',
          'bankRegionIds',
          'upgradeScope',
          'maxTotalTiers',
          'purchaseMode',
          'tracks',
        ],
        fail,
      );
      const veinSites = array(
        economy.veinSites,
        `${economyPath}.veinSites`,
        fail,
      );
      veinSites.forEach((site, index) => {
        const sitePath = `${economyPath}.veinSites[${index}]`;
        const value = exact(object(site, sitePath, fail), sitePath, ['x', 'y'], fail);
        integer(value.x, `${sitePath}.x`, fail);
        integer(value.y, `${sitePath}.y`, fail);
      });
      if (veinSites.length === 0) {
        fail(`${economyPath}.veinSites`, 'declares at least one vein site');
      }
      for (const key of [
        'veinFirstSpawnTick',
        'veinSpawnIntervalTicks',
        'veinLastSpawnTick',
        'veinAmount',
        'wreckAmount',
        'assayAmount',
        'carryCapacity',
        'pileLifetimeTicks',
        'maxSimultaneousPiles',
        'maxTotalTiers',
      ]) {
        integer(economy[key], `${economyPath}.${key}`, fail);
      }
      const firstTick = economy.veinFirstSpawnTick as number;
      const interval = economy.veinSpawnIntervalTicks as number;
      const lastTick = economy.veinLastSpawnTick as number;
      if (
        firstTick < 0 ||
        interval <= 0 ||
        lastTick < firstTick ||
        (lastTick - firstTick) % interval !== 0
      ) {
        fail(
          `${economyPath}.veinLastSpawnTick`,
          'the last scheduled vein tick must sit on the declared cadence',
        );
      }
      const bankRegionIds = array(
        economy.bankRegionIds,
        `${economyPath}.bankRegionIds`,
        fail,
      );
      bankRegionIds.forEach((regionId, index) =>
        nonEmpty(regionId, `${economyPath}.bankRegionIds[${index}]`, fail),
      );
      if (
        bankRegionIds.length === 0 ||
        new Set(bankRegionIds.map(String)).size !== bankRegionIds.length
      ) {
        fail(
          `${economyPath}.bankRegionIds`,
          'names one distinct banking region per scoring team',
        );
      }
      // `all-slot-lives` is prime dissolution's forced consequence
      // (DECISIONS #194): with no prime slot there is nothing narrower to
      // scope a purchased tier to.
      if (
        String(economy.upgradeScope) !== 'prime-slot-lives-only' &&
        String(economy.upgradeScope) !== 'all-slot-lives'
      ) {
        fail(
          `${economyPath}.upgradeScope`,
          'expected one of prime-slot-lives-only, all-slot-lives',
        );
      }
      if (
        !['invest-action', 'automatic-greedy-declared-order'].includes(
          String(economy.purchaseMode),
        )
      ) {
        fail(
          `${economyPath}.purchaseMode`,
          'expected one of invest-action, automatic-greedy-declared-order',
        );
      }
      const tracks = array(economy.tracks, `${economyPath}.tracks`, fail);
      const trackIds: string[] = [];
      tracks.forEach((entry, index) => {
        const trackPath = `${economyPath}.tracks[${index}]`;
        const track = exact(
          object(entry, trackPath, fail),
          trackPath,
          ['trackId', 'effect', 'perTierMagnitude', 'maxTier', 'tierCosts'],
          fail,
        );
        nonEmpty(track.trackId, `${trackPath}.trackId`, fail);
        trackIds.push(String(track.trackId));
        if (
          ![
            'mobile-attack-travel-tiles-delta',
            'spawn-max-health-delta',
            'vision-range-delta',
          ].includes(String(track.effect))
        ) {
          fail(`${trackPath}.effect`, 'unknown scrap upgrade effect');
        }
        integer(track.perTierMagnitude, `${trackPath}.perTierMagnitude`, fail);
        integer(track.maxTier, `${trackPath}.maxTier`, fail);
        const tierCosts = array(track.tierCosts, `${trackPath}.tierCosts`, fail);
        tierCosts.forEach((cost, costIndex) =>
          integer(cost, `${trackPath}.tierCosts[${costIndex}]`, fail),
        );
        if (
          (track.maxTier as number) <= 0 ||
          tierCosts.length !== (track.maxTier as number) ||
          tierCosts.some((cost) => (cost as number) <= 0)
        ) {
          fail(
            `${trackPath}.tierCosts`,
            'prices every declared tier, positively',
          );
        }
      });
      if (tracks.length === 0 || new Set(trackIds).size !== trackIds.length) {
        fail(
          `${economyPath}.tracks`,
          'declares at least one track with unique IDs',
        );
      }
    }
    if (
      scoreCatalog.length !== 1 ||
      (scoreCatalog[0] as Record<string, unknown>).channel !==
        'territorial-progress' ||
      (scoreCatalog[0] as Record<string, unknown>).domain !== 'signed'
    ) {
      fail(
        `${modePath}.scoreCatalog`,
        'frontline requires exactly the signed territorial-progress channel',
      );
    }
    const timeoutRanking = array(
      victory.timeoutRanking,
      `${victoryPath}.timeoutRanking`,
      fail,
    );
    if (
      timeoutRanking.length !== 1 ||
      (timeoutRanking[0] as Record<string, unknown>).channel !==
        'territorial-progress' ||
      (timeoutRanking[0] as Record<string, unknown>).direction !==
        'higher-wins'
    ) {
      fail(
        `${victoryPath}.timeoutRanking`,
        'frontline requires territorial-progress higher-wins',
      );
    }
  } else {
    const victory = exact(
      mode.victory,
      victoryPath,
      ['kind', 'timeoutRanking', 'pulsesToDestroyReactor'],
      fail,
    );
    if (victory.kind !== 'arc-relay') {
      fail(`${victoryPath}.kind`, 'must match the Arc Relay mode');
    }
    validateRankings(
      victory.timeoutRanking,
      `${victoryPath}.timeoutRanking`,
      fail,
    );
    for (const key of [
      'pendingRearmTicks',
      'coreRelocationIntervalTicks',
      'coresPerPulse',
      'fieldedSlotsPerTeam',
      'maxCopiesPerClass',
      'respawnDelayTicks',
    ]) {
      integer(mode[key], `${modePath}.${key}`, fail);
      if ((mode[key] as number) <= 0) {
        fail(`${modePath}.${key}`, 'must be positive');
      }
    }
    integer(
      victory.pulsesToDestroyReactor,
      `${victoryPath}.pulsesToDestroyReactor`,
      fail,
    );
    if ((victory.pulsesToDestroyReactor as number) <= 0) {
      fail(`${victoryPath}.pulsesToDestroyReactor`, 'must be positive');
    }
    const wells = array(mode.wells, `${modePath}.wells`, fail);
    wells.forEach((entry, index) => {
      const wellPath = `${modePath}.wells[${index}]`;
      const well = exact(
        entry,
        wellPath,
        ['wellId', 'firstBirthTick', 'cadenceTicks', 'finalBirthTick'],
        fail,
      );
      nonEmpty(well.wellId, `${wellPath}.wellId`, fail);
      for (const key of ['firstBirthTick', 'cadenceTicks', 'finalBirthTick'])
        integer(well[key], `${wellPath}.${key}`, fail);
    });
    const signatures = array(
      mode.signatures,
      `${modePath}.signatures`,
      fail,
    );
    signatures.forEach((entry, index) => {
      const signaturePath = `${modePath}.signatures[${index}]`;
      const signature = object(entry, signaturePath, fail);
      for (const key of ['kind', 'signatureId', 'classId', 'actionId'])
        nonEmpty(signature[key], `${signaturePath}.${key}`, fail);
      integer(
        signature.cooldownTicks,
        `${signaturePath}.cooldownTicks`,
        fail,
      );
      jsonValue(signature, signaturePath, fail);
    });
    if (wells.length !== 3 || signatures.length !== 16) {
      fail(
        modePath,
        'Arc Relay H0 declares exactly three Wells and sixteen signatures',
      );
    }
    const expectedChannels = ['pulses', 'reactor-charge'];
    if (
      scoreCatalog.length !== expectedChannels.length ||
      !scoreCatalog.every(
        (entry, index) =>
          (entry as Record<string, unknown>).channel ===
            expectedChannels[index] &&
          (entry as Record<string, unknown>).domain === 'non-negative',
      )
    ) {
      fail(
        `${modePath}.scoreCatalog`,
        'Arc Relay requires pulses then reactor-charge',
      );
    }
  }
  for (const key of [
    'forms',
    'movementProfiles',
    'visionProfiles',
    'attackProfiles',
    'actions',
    'fabricationTransitions',
    'sameLifeTransitions',
    'replicationTransitions',
  ]) {
    const entries = array(rules[key], `${path}.rules.${key}`, fail);
    entries.forEach((entry, index) =>
      jsonValue(entry, `${path}.rules.${key}[${index}]`, fail),
    );
  }
  array(
    rules.movementProfiles,
    `${path}.rules.movementProfiles`,
    fail,
  ).forEach((entry, index) => {
    const profilePath = `${path}.rules.movementProfiles[${index}]`;
    const profileValue = object(entry, profilePath, fail);
    const hasFacingCoupling = own(profileValue, 'facingCoupling');
    const profile = exact(
      profileValue,
      profilePath,
      ['id', 'movementLayer', ...(hasFacingCoupling ? ['facingCoupling'] : [])],
      fail,
    );
    semanticId(profile.id, `${profilePath}.id`, fail);
    nonEmpty(profile.movementLayer, `${profilePath}.movementLayer`, fail);
    if (hasFacingCoupling) {
      movementFacingCoupling(
        profile.facingCoupling,
        `${profilePath}.facingCoupling`,
        fail,
      );
    }
  });

  // Two more additive optional contract fields with the facing coupling's
  // discipline: the engine omits them while they are inert, so an explicitly
  // inert value is a second, non-canonical encoding of the same contract and
  // must be rejected rather than normalized away.
  array(rules.forms, `${path}.rules.forms`, fail).forEach((entry, index) => {
    const formPath = `${path}.rules.forms[${index}]`;
    const formValue = object(entry, formPath, fail);
    if (!own(formValue, 'projectileGuard')) {
      return;
    }
    if (formValue.projectileGuard !== 'facing-quadrant-contacts-deflected') {
      fail(
        `${formPath}.projectileGuard`,
        'must be omitted instead of emitted inert',
      );
    }
  });
  array(
    rules.attackProfiles,
    `${path}.rules.attackProfiles`,
    fail,
  ).forEach((entry, index) => {
    const profilePath = `${path}.rules.attackProfiles[${index}]`;
    const profileValue = object(entry, profilePath, fail);
    const hasFacingCone = own(profileValue, 'facingAimHalfWidthSectors');
    if (hasFacingCone) {
      integer(
        profileValue.facingAimHalfWidthSectors,
        `${profilePath}.facingAimHalfWidthSectors`,
        fail,
      );
      const halfWidth = profileValue.facingAimHalfWidthSectors as number;
      if (halfWidth < 1 || halfWidth > 3) {
        fail(
          `${profilePath}.facingAimHalfWidthSectors`,
          'must be 1..3 when present',
        );
      }
      if (
        profileValue.omnidirectionalAim !== false ||
        profileValue.aimInterpretation !==
          'absolute-submitted-eight-way-heading-within-facing-cone-facing-unchanged'
      ) {
        fail(
          `${profilePath}.facingAimHalfWidthSectors`,
          'requires non-omnidirectional facing-cone aim interpretation',
        );
      }
      const program = object(
        profileValue.shotProgram,
        `${profilePath}.shotProgram`,
        fail,
      );
      if (program.enabled !== false) {
        fail(
          `${profilePath}.facingAimHalfWidthSectors`,
          'is mutually exclusive with programmed shots',
        );
      }
    } else if (
      profileValue.aimInterpretation ===
      'absolute-submitted-eight-way-heading-within-facing-cone-facing-unchanged'
    ) {
      fail(
        `${profilePath}.aimInterpretation`,
        'requires facingAimHalfWidthSectors',
      );
    }
    if (!own(profileValue, 'volley')) {
      return;
    }
    const volleyPath = `${profilePath}.volley`;
    const volley = exact(
      profileValue.volley,
      volleyPath,
      ['projectileCount', 'spread', 'identityOrder'],
      fail,
    );
    integer(volley.projectileCount, `${volleyPath}.projectileCount`, fail);
    const SYMMETRIC_FAN =
      'symmetric-adjacent-heading-fan-ascending-signed-sector-offset';
    if (
      !['shared-resolved-heading', SYMMETRIC_FAN].includes(
        String(volley.spread),
      )
    ) {
      fail(`${volleyPath}.spread`, 'is not a known volley spread');
    }
    if (volley.identityOrder !== 'contiguous-ascending-in-launch-order') {
      fail(
        `${volleyPath}.identityOrder`,
        'expected contiguous-ascending-in-launch-order',
      );
    }
    if ((volley.projectileCount as number) < 2) {
      fail(
        `${volleyPath}.projectileCount`,
        'must be omitted instead of emitted inert',
      );
    }
    if (
      volley.spread === SYMMETRIC_FAN &&
      (volley.projectileCount as number) % 2 === 0
    ) {
      fail(`${volleyPath}.projectileCount`, 'must be odd for a symmetric fan');
    }
    const program = object(profileValue.shotProgram, `${profilePath}.shotProgram`, fail);
    if (program.enabled === true) {
      fail(
        volleyPath,
        'is mutually exclusive with programmed shots',
      );
    }
  });
  array(rules.actions, `${path}.rules.actions`, fail).forEach((entry, index) => {
    const actionPath = `${path}.rules.actions[${index}]`;
    const actionValue = object(entry, actionPath, fail);
    if (!own(actionValue, 'movementFacingOverride')) {
      return;
    }
    if (actionValue.kind !== 'movement') {
      fail(
        `${actionPath}.movementFacingOverride`,
        'is permitted only on movement actions',
      );
    }
    const override = String(actionValue.movementFacingOverride);
    if (
      ![
        'preserve-facing',
        'face-movement-direction',
        'facing-locked',
        'face-movement-heading-projected',
        'combat-strafe',
      ].includes(override)
    ) {
      fail(
        `${actionPath}.movementFacingOverride`,
        'expected a known movement/facing coupling',
      );
    }
  });
  array(
    rules.sameLifeTransitions,
    `${path}.rules.sameLifeTransitions`,
    fail,
  ).forEach((entry, index) => {
    const routePath = `${path}.rules.sameLifeTransitions[${index}]`;
    const routeValue = object(entry, routePath, fail);
    if (!own(routeValue, 'automaticReturn')) {
      return;
    }
    const triggerPath = `${routePath}.automaticReturn`;
    const trigger = exact(
      routeValue.automaticReturn,
      triggerPath,
      ['counter', 'threshold'],
      fail,
    );
    if (
      ![
        'attacks-issued-since-entering-source-form',
        'projectiles-deflected-since-entering-source-form',
      ].includes(String(trigger.counter))
    ) {
      fail(`${triggerPath}.counter`, 'is not a known automatic-return counter');
    }
    integer(trigger.threshold, `${triggerPath}.threshold`, fail);
    if ((trigger.threshold as number) < 1) {
      fail(
        `${triggerPath}.threshold`,
        'must be omitted instead of emitted inert',
      );
    }
  });

  const map = exact(
    contract.map,
    `${path}.map`,
    [
      'schemaVersion',
      'mapId',
      'mapVersion',
      'mapFingerprint',
      'formatVersion',
      'width',
      'height',
      'tileRows',
      'spawnAnchors',
      'regions',
      'tileTags',
    ],
    fail,
  );
  for (const key of [
    'schemaVersion',
    'mapVersion',
    'formatVersion',
    'width',
    'height',
  ]) {
    integer(map[key], `${path}.map.${key}`, fail);
  }
  for (const key of ['mapId', 'mapFingerprint']) {
    nonEmpty(map[key], `${path}.map.${key}`, fail);
  }
  array(map.tileRows, `${path}.map.tileRows`, fail).forEach((row, index) =>
    string(row, `${path}.map.tileRows[${index}]`, fail),
  );
  array(map.spawnAnchors, `${path}.map.spawnAnchors`, fail).forEach(
    (entry, index) => {
      const spawn = exact(
        entry,
        `${path}.map.spawnAnchors[${index}]`,
        ['spawnId', 'position', 'facing', 'compatibleMovementLayers'],
        fail,
      );
      nonEmpty(spawn.spawnId, `${path}.map.spawnAnchors[${index}].spawnId`, fail);
      contractPosition(
        spawn.position,
        `${path}.map.spawnAnchors[${index}].position`,
        fail,
      );
      direction(spawn.facing, `${path}.map.spawnAnchors[${index}].facing`, fail);
      array(
        spawn.compatibleMovementLayers,
        `${path}.map.spawnAnchors[${index}].compatibleMovementLayers`,
        fail,
      ).forEach((layer, layerIndex) =>
        nonEmpty(
          layer,
          `${path}.map.spawnAnchors[${index}].compatibleMovementLayers[${layerIndex}]`,
          fail,
        ),
      );
    },
  );
  array(map.regions, `${path}.map.regions`, fail).forEach((entry, index) => {
    const regionPath = `${path}.map.regions[${index}]`;
    const region = exact(entry, regionPath, ['regionId', 'kind', 'tiles'], fail);
    nonEmpty(region.regionId, `${regionPath}.regionId`, fail);
    if (
      region.kind !== 'objective' &&
      region.kind !== 'transition-placement'
    ) {
      fail(`${regionPath}.kind`, 'unknown map region kind');
    }
    array(region.tiles, `${regionPath}.tiles`, fail).forEach(
      (tile, tileIndex) =>
        contractPosition(tile, `${regionPath}.tiles[${tileIndex}]`, fail),
    );
  });
  array(map.tileTags, `${path}.map.tileTags`, fail).forEach((entry, index) => {
    const tagPath = `${path}.map.tileTags[${index}]`;
    const tag = exact(entry, tagPath, ['tagId', 'kind', 'tiles'], fail);
    nonEmpty(tag.tagId, `${tagPath}.tagId`, fail);
    if (
      tag.kind !== 'transition-placement-forbidden' &&
      tag.kind !== 'spawn-protected' &&
      tag.kind !== 'signature-placement-forbidden'
    ) {
      fail(`${tagPath}.kind`, 'unknown map tile-tag kind');
    }
    array(tag.tiles, `${tagPath}.tiles`, fail).forEach((tile, tileIndex) =>
      contractPosition(tile, `${tagPath}.tiles[${tileIndex}]`, fail),
    );
  });

  jsonValue(contract.format, `${path}.format`, fail);
  validateTopology(contract.topology, `${path}.topology`, fail);
  validateInitialDeployment(
    contract.initialDeployment,
    `${path}.initialDeployment`,
    fail,
  );
  for (const key of [
    'lifecycleAssignments',
    'participantRegionAssignments',
  ]) {
    array(contract[key], `${path}.${key}`, fail).forEach((entry, index) =>
      jsonValue(entry, `${path}.${key}[${index}]`, fail),
    );
  }
  const bindingPath = `${path}.modeMapBinding`;
  const binding = object(contract.modeMapBinding, bindingPath, fail);
  if (binding.kind === 'deathmatch') {
    exact(binding, bindingPath, ['kind'], fail);
  } else if (binding.kind === 'frontline') {
    const frontline = exact(
      binding,
      bindingPath,
      ['kind', 'orderedObjectiveRegionIds', 'teamAdvances'],
      fail,
    );
    array(
      frontline.orderedObjectiveRegionIds,
      `${bindingPath}.orderedObjectiveRegionIds`,
      fail,
    ).forEach((regionId, index) =>
      nonEmpty(
        regionId,
        `${bindingPath}.orderedObjectiveRegionIds[${index}]`,
        fail,
      ),
    );
    array(frontline.teamAdvances, `${bindingPath}.teamAdvances`, fail).forEach(
      (entry, index) => {
        const advancePath = `${bindingPath}.teamAdvances[${index}]`;
        const advance = exact(
          entry,
          advancePath,
          ['teamId', 'direction', 'objectiveIndexDelta'],
          fail,
        );
        integer(advance.teamId, `${advancePath}.teamId`, fail);
        if (
          advance.direction !== 'toward-lower-index' &&
          advance.direction !== 'toward-higher-index'
        ) {
          fail(`${advancePath}.direction`, 'unknown objective advance direction');
        }
        integer(
          advance.objectiveIndexDelta,
          `${advancePath}.objectiveIndexDelta`,
          fail,
        );
        if (
          advance.objectiveIndexDelta !==
          (advance.direction === 'toward-lower-index' ? -1 : 1)
        ) {
          fail(
            `${advancePath}.objectiveIndexDelta`,
            'must match the objective advance direction',
          );
        }
      },
    );
  } else if (binding.kind === 'arc-relay') {
    const arc = exact(
      binding,
      bindingPath,
      [
        'kind',
        'orderedWellRegionIds',
        'reactorRegionRoleId',
        'homePadRegionRoleId',
      ],
      fail,
    );
    const wellIds = array(
      arc.orderedWellRegionIds,
      `${bindingPath}.orderedWellRegionIds`,
      fail,
    );
    wellIds.forEach((regionId, index) =>
      nonEmpty(
        regionId,
        `${bindingPath}.orderedWellRegionIds[${index}]`,
        fail,
      ),
    );
    nonEmpty(
      arc.reactorRegionRoleId,
      `${bindingPath}.reactorRegionRoleId`,
      fail,
    );
    nonEmpty(
      arc.homePadRegionRoleId,
      `${bindingPath}.homePadRegionRoleId`,
      fail,
    );
    if (wellIds.length !== 3 || new Set(wellIds.map(String)).size !== 3) {
      fail(
        `${bindingPath}.orderedWellRegionIds`,
        'Arc Relay requires three distinct Well regions',
      );
    }
  } else {
    fail(`${bindingPath}.kind`, `unknown mode-map binding ${String(binding.kind)}`);
  }
}

function validateTopology(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const topology = exact(
    value,
    path,
    [
      'schemaVersion',
      'topologyFingerprint',
      'counts',
      'teams',
      'participants',
      'unitSlots',
      'initialLives',
    ],
    fail,
  );
  integer(topology.schemaVersion, `${path}.schemaVersion`, fail);
  nonEmpty(topology.topologyFingerprint, `${path}.topologyFingerprint`, fail);
  const counts = exact(
    topology.counts,
    `${path}.counts`,
    ['teamCount', 'participantCount', 'unitSlotCount', 'initialLifeCount'],
    fail,
  );
  for (const key of Object.keys(counts)) {
    integer(counts[key], `${path}.counts.${key}`, fail);
  }
  array(topology.teams, `${path}.teams`, fail).forEach((entry, index) => {
    const teamValue = object(entry, `${path}.teams[${index}]`, fail);
    const hasClassId = own(teamValue, 'classId');
    const team = exact(
      teamValue,
      `${path}.teams[${index}]`,
      hasClassId ? ['teamId', 'classId'] : ['teamId'],
      fail,
    );
    integer(team.teamId, `${path}.teams[${index}].teamId`, fail);
    if (hasClassId) {
      semanticId(team.classId, `${path}.teams[${index}].classId`, fail);
    }
  });
  array(topology.participants, `${path}.participants`, fail).forEach(
    (entry, index) => {
      const participantValue = object(
        entry,
        `${path}.participants[${index}]`,
        fail,
      );
      const hasClassId = own(participantValue, 'classId');
      const participant = exact(
        participantValue,
        `${path}.participants[${index}]`,
        hasClassId
          ? ['participantId', 'teamId', 'classId']
          : ['participantId', 'teamId'],
        fail,
      );
      integer(
        participant.participantId,
        `${path}.participants[${index}].participantId`,
        fail,
      );
      integer(participant.teamId, `${path}.participants[${index}].teamId`, fail);
      if (hasClassId) {
        semanticId(
          participant.classId,
          `${path}.participants[${index}].classId`,
          fail,
        );
      }
    },
  );
  array(topology.unitSlots, `${path}.unitSlots`, fail).forEach(
    (entry, index) => {
      const slotValue = object(entry, `${path}.unitSlots[${index}]`, fail);
      // The per-slot chassis (§9.2). Additive under the #156 canonical
      // discipline: written only when a ruleset declares compositions, and an
      // explicit null is refused as a second encoding of absence — which is
      // what keeps every existing contract's topology fingerprint exact.
      const hasClassId = own(slotValue, 'classId');
      const slot = exact(
        slotValue,
        `${path}.unitSlots[${index}]`,
        hasClassId
          ? ['teamId', 'unitId', 'controllerParticipantId', 'classId']
          : ['teamId', 'unitId', 'controllerParticipantId'],
        fail,
      );
      for (const key of ['teamId', 'unitId', 'controllerParticipantId']) {
        integer(slot[key], `${path}.unitSlots[${index}].${key}`, fail);
      }
      if (hasClassId) {
        semanticId(slot.classId, `${path}.unitSlots[${index}].classId`, fail);
      }
    },
  );
  array(topology.initialLives, `${path}.initialLives`, fail).forEach(
    (entry, index) => {
      const life = exact(
        entry,
        `${path}.initialLives[${index}]`,
        ['teamId', 'unitId', 'lifeId', 'formId'],
        fail,
      );
      for (const key of ['teamId', 'unitId', 'lifeId']) {
        integer(life[key], `${path}.initialLives[${index}].${key}`, fail);
      }
      nonEmpty(life.formId, `${path}.initialLives[${index}].formId`, fail);
    },
  );
}

function validateInitialDeployment(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const deployment = exact(value, path, ['spawns', 'lives'], fail);
  array(deployment.spawns, `${path}.spawns`, fail).forEach((entry, index) => {
    const spawn = exact(
      entry,
      `${path}.spawns[${index}]`,
      ['spawnId', 'position', 'facing'],
      fail,
    );
    nonEmpty(spawn.spawnId, `${path}.spawns[${index}].spawnId`, fail);
    contractPosition(spawn.position, `${path}.spawns[${index}].position`, fail);
    direction(spawn.facing, `${path}.spawns[${index}].facing`, fail);
  });
  array(deployment.lives, `${path}.lives`, fail).forEach((entry, index) => {
    const life = exact(
      entry,
      `${path}.lives[${index}]`,
      ['teamId', 'unitId', 'lifeId', 'formId', 'spawnId'],
      fail,
    );
    for (const key of ['teamId', 'unitId', 'lifeId']) {
      integer(life[key], `${path}.lives[${index}].${key}`, fail);
    }
    nonEmpty(life.formId, `${path}.lives[${index}].formId`, fail);
    nonEmpty(life.spawnId, `${path}.lives[${index}].spawnId`, fail);
  });
}

function unitSlotState(value: unknown, path: string, fail: ReplayV3Fail): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  switch (base.kind) {
    case 'active': {
      const item = exact(
        base,
        path,
        ['kind', 'actorId', 'generation', 'formId'],
        fail,
      );
      actorId(item.actorId, `${path}.actorId`, fail);
      integer(item.generation, `${path}.generation`, fail);
      nonEmpty(item.formId, `${path}.formId`, fail);
      return;
    }
    case 'availability-pending': {
      const item = exact(base, path, ['kind', 'reason', 'dueTick'], fail);
      nonEmpty(item.reason, `${path}.reason`, fail);
      integer(item.dueTick, `${path}.dueTick`, fail);
      return;
    }
    case 'automatic-return-pending': {
      const item = exact(
        base,
        path,
        ['kind', 'dueTick', 'targetFormId', 'generation'],
        fail,
      );
      integer(item.dueTick, `${path}.dueTick`, fail);
      nonEmpty(item.targetFormId, `${path}.targetFormId`, fail);
      integer(item.generation, `${path}.generation`, fail);
      return;
    }
    case 'ready':
    case 'permanently-dormant':
      exact(base, path, ['kind'], fail);
      return;
    case 'fabrication-pending':
    case 'replication-pending': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'dueTick',
          'sourceActorId',
          'transitionId',
          'operationId',
          'targetFormId',
          'reservedPosition',
        ],
        fail,
      );
      integer(item.dueTick, `${path}.dueTick`, fail);
      actorId(item.sourceActorId, `${path}.sourceActorId`, fail);
      for (const key of ['transitionId', 'operationId', 'targetFormId']) {
        nonEmpty(item[key], `${path}.${key}`, fail);
      }
      position(item.reservedPosition, `${path}.reservedPosition`, fail);
      return;
    }
    default:
      fail(`${path}.kind`, `unknown slot state ${String(base.kind)}`);
  }
}

function participantStatus(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const status = object(value, path, fail);
  const item = exact(
    status,
    path,
    [
      'participantId',
      'teamId',
      'runtimeFaultCount',
      'disqualified',
      'classId',
    ],
    fail,
  );
  integer(item.participantId, `${path}.participantId`, fail);
  integer(item.teamId, `${path}.teamId`, fail);
  int64(item.runtimeFaultCount, `${path}.runtimeFaultCount`, fail, true);
  boolean(item.disqualified, `${path}.disqualified`, fail);
  nullable(item.classId, `${path}.classId`, semanticId, fail);
}

function pendingTransition(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  if (value === null) return;
  const item = exact(
    value,
    path,
    ['transitionId', 'operationId', 'targetFormId', 'startedTick', 'dueTick'],
    fail,
  );
  for (const key of ['transitionId', 'operationId', 'targetFormId']) {
    nonEmpty(item[key], `${path}.${key}`, fail);
  }
  integer(item.startedTick, `${path}.startedTick`, fail);
  integer(item.dueTick, `${path}.dueTick`, fail);
}

function rawArgument(value: unknown, path: string, fail: ReplayV3Fail): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  if (base.kind === 'form-target') {
    const item = exact(base, path, ['kind', 'formId'], fail);
    nullable(item.formId, `${path}.formId`, string, fail);
    return;
  }
  if (base.kind === 'upgrade-track') {
    const item = exact(base, path, ['kind', 'trackId'], fail);
    nullable(item.trackId, `${path}.trackId`, string, fail);
    return;
  }
  const item = exact(base, path, ['kind', 'value'], fail);
  switch (item.kind) {
    case 'shot-program':
      shotProgram(item.value, `${path}.value`, fail);
      return;
    case 'direction':
    case 'projectile-heading':
      integer(item.value, `${path}.value`, fail);
      return;
    case 'unit-target': {
      const target = exact(item.value, `${path}.value`, ['teamId', 'unitId'], fail);
      integer(target.teamId, `${path}.value.teamId`, fail);
      integer(target.unitId, `${path}.value.unitId`, fail);
      return;
    }
    case 'position-target':
      position(item.value, `${path}.value`, fail);
      return;
    default:
      fail(`${path}.kind`, `unknown raw action argument ${String(item.kind)}`);
  }
}

function actionArgument(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  if (base.kind === 'form-target') {
    const item = exact(base, path, ['kind', 'formId'], fail);
    nonEmpty(item.formId, `${path}.formId`, fail);
    return;
  }
  if (base.kind === 'upgrade-track') {
    const item = exact(base, path, ['kind', 'trackId'], fail);
    nonEmpty(item.trackId, `${path}.trackId`, fail);
    return;
  }
  const item = exact(base, path, ['kind', 'value'], fail);
  switch (item.kind) {
    case 'shot-program':
      shotProgram(item.value, `${path}.value`, fail);
      return;
    case 'direction':
      direction(item.value, `${path}.value`, fail);
      return;
    case 'projectile-heading':
      heading(item.value, `${path}.value`, fail);
      return;
    case 'unit-target': {
      const target = exact(item.value, `${path}.value`, ['teamId', 'unitId'], fail);
      integer(target.teamId, `${path}.value.teamId`, fail);
      integer(target.unitId, `${path}.value.unitId`, fail);
      return;
    }
    case 'position-target':
      position(item.value, `${path}.value`, fail);
      return;
    default:
      fail(`${path}.kind`, `unknown action argument ${String(item.kind)}`);
  }
}

function resolvedAction(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(value, path, ['actionId', 'actionCode', 'arguments'], fail);
  nonEmpty(item.actionId, `${path}.actionId`, fail);
  integer(item.actionCode, `${path}.actionCode`, fail);
  array(item.arguments, `${path}.arguments`, fail).forEach((entry, index) =>
    actionArgument(entry, `${path}.arguments[${index}]`, fail),
  );
}

function runtimeFault(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(
    value,
    path,
    [
      'participantId',
      'actorId',
      'stage',
      'faultCode',
      'cumulativeFaultCount',
      'disqualificationTriggered',
    ],
    fail,
  );
  integer(item.participantId, `${path}.participantId`, fail);
  actorId(item.actorId, `${path}.actorId`, fail);
  nonEmpty(item.stage, `${path}.stage`, fail);
  nonEmpty(item.faultCode, `${path}.faultCode`, fail);
  int64(item.cumulativeFaultCount, `${path}.cumulativeFaultCount`, fail, true);
  boolean(
    item.disqualificationTriggered,
    `${path}.disqualificationTriggered`,
    fail,
  );
}

function actionResolution(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    [
      'submittedAction',
      'acceptedAction',
      'validatedAction',
      'outcome',
      'runtimeFault',
    ],
    fail,
  );
  nullable(item.submittedAction, `${path}.submittedAction`, resolvedAction, fail);
  resolvedAction(item.acceptedAction, `${path}.acceptedAction`, fail);
  resolvedAction(item.validatedAction, `${path}.validatedAction`, fail);
  nonEmpty(item.outcome, `${path}.outcome`, fail);
  nullable(item.runtimeFault, `${path}.runtimeFault`, runtimeFault, fail);
}

function lifeState(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(
    value,
    path,
    [
      'actorId',
      'participantId',
      'generation',
      'formId',
      'position',
      'facing',
      'health',
      'cooldown',
      'energy',
      'spawnedAtTick',
      'spawnReason',
      'parentActorId',
      'sourceTransitionId',
      'sourceOperationId',
      'previousActionResolution',
      'pendingSameLifeTransition',
    ],
    fail,
  );
  actorId(item.actorId, `${path}.actorId`, fail);
  for (const key of [
    'participantId',
    'generation',
    'health',
    'cooldown',
    'spawnedAtTick',
  ]) {
    integer(item[key], `${path}.${key}`, fail);
  }
  nullable(item.energy, `${path}.energy`, integer, fail);
  nonEmpty(item.formId, `${path}.formId`, fail);
  position(item.position, `${path}.position`, fail);
  direction(item.facing, `${path}.facing`, fail);
  nonEmpty(item.spawnReason, `${path}.spawnReason`, fail);
  nullable(item.parentActorId, `${path}.parentActorId`, actorId, fail);
  nullable(item.sourceTransitionId, `${path}.sourceTransitionId`, string, fail);
  nullable(item.sourceOperationId, `${path}.sourceOperationId`, string, fail);
  nullable(
    item.previousActionResolution,
    `${path}.previousActionResolution`,
    actionResolution,
    fail,
  );
  pendingTransition(
    item.pendingSameLifeTransition,
    `${path}.pendingSameLifeTransition`,
    fail,
  );
}

function pendingReplication(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    [
      'sourceActorId',
      'participantId',
      'sourceGeneration',
      'sourceFormId',
      'sourcePosition',
      'sourceFacing',
      'transitionId',
      'operationId',
      'queuedTick',
      'dueTick',
      'descendants',
    ],
    fail,
  );
  actorId(item.sourceActorId, `${path}.sourceActorId`, fail);
  for (const key of [
    'participantId',
    'sourceGeneration',
    'queuedTick',
    'dueTick',
  ]) {
    integer(item[key], `${path}.${key}`, fail);
  }
  for (const key of ['sourceFormId', 'transitionId', 'operationId']) {
    nonEmpty(item[key], `${path}.${key}`, fail);
  }
  position(item.sourcePosition, `${path}.sourcePosition`, fail);
  direction(item.sourceFacing, `${path}.sourceFacing`, fail);
  array(item.descendants, `${path}.descendants`, fail).forEach(
    (entry, index) => {
      const descendant = exact(
        entry,
        `${path}.descendants[${index}]`,
        ['teamId', 'unitId', 'formId', 'generation', 'position'],
        fail,
      );
      for (const key of ['teamId', 'unitId', 'generation']) {
        integer(descendant[key], `${path}.descendants[${index}].${key}`, fail);
      }
      nonEmpty(
        descendant.formId,
        `${path}.descendants[${index}].formId`,
        fail,
      );
      position(
        descendant.position,
        `${path}.descendants[${index}].position`,
        fail,
      );
    },
  );
}

function slotState(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(
    value,
    path,
    [
      'teamId',
      'unitId',
      'participantId',
      'nextLifeId',
      'state',
      'pendingParentActorId',
      'splitReservation',
    ],
    fail,
  );
  for (const key of ['teamId', 'unitId', 'participantId', 'nextLifeId']) {
    integer(item[key], `${path}.${key}`, fail);
  }
  unitSlotState(item.state, `${path}.state`, fail);
  nullable(item.pendingParentActorId, `${path}.pendingParentActorId`, actorId, fail);
  nullable(item.splitReservation, `${path}.splitReservation`, pendingReplication, fail);
}

function projectileState(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    [
      'projectileId',
      'ownerParticipantId',
      'ownerTeamId',
      'ownerActorId',
      'attackProfileId',
      'spawnedAtTick',
      'origin',
      'position',
      'launchHeading',
      'heading',
      'shotProgram',
      'committedPath',
      'nextPathIndex',
      'remainingTiles',
      'ticksUntilAdvance',
    ],
    fail,
  );
  int64(item.projectileId, `${path}.projectileId`, fail, true);
  for (const key of [
    'ownerParticipantId',
    'ownerTeamId',
    'spawnedAtTick',
    'nextPathIndex',
    'remainingTiles',
    'ticksUntilAdvance',
  ]) {
    integer(item[key], `${path}.${key}`, fail);
  }
  actorId(item.ownerActorId, `${path}.ownerActorId`, fail);
  nonEmpty(item.attackProfileId, `${path}.attackProfileId`, fail);
  position(item.origin, `${path}.origin`, fail);
  position(item.position, `${path}.position`, fail);
  heading(item.launchHeading, `${path}.launchHeading`, fail);
  heading(item.heading, `${path}.heading`, fail);
  nullable(item.shotProgram, `${path}.shotProgram`, shotProgram, fail);
  array(item.committedPath, `${path}.committedPath`, fail).forEach(
    (entry, index) => position(entry, `${path}.committedPath[${index}]`, fail),
  );
}

function scoreboard(value: unknown, path: string, fail: ReplayV3Fail): void {
  const board = exact(value, path, ['teams'], fail);
  array(board.teams, `${path}.teams`, fail).forEach((entry, index) => {
    const team = exact(
      entry,
      `${path}.teams[${index}]`,
      ['teamId', 'eligible', 'scores'],
      fail,
    );
    integer(team.teamId, `${path}.teams[${index}].teamId`, fail);
    boolean(team.eligible, `${path}.teams[${index}].eligible`, fail);
    array(team.scores, `${path}.teams[${index}].scores`, fail).forEach(
      (scoreEntry, scoreIndex) => {
        const score = exact(
          scoreEntry,
          `${path}.teams[${index}].scores[${scoreIndex}]`,
          ['channel', 'value'],
          fail,
        );
        nonEmpty(
          score.channel,
          `${path}.teams[${index}].scores[${scoreIndex}].channel`,
          fail,
        );
        int64(
          score.value,
          `${path}.teams[${index}].scores[${scoreIndex}].value`,
          fail,
        );
      },
    );
  });
}

function modeState(value: unknown, path: string, fail: ReplayV3Fail): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  if (base.kind === 'deathmatch') {
    const item = exact(base, path, ['kind', 'modeId'], fail);
    nonEmpty(item.modeId, `${path}.modeId`, fail);
    return;
  }
  if (base.kind === 'frontline') {
    const item = exact(
      base,
      path,
      [
        'kind',
        'modeId',
        'activePositionIndex',
        'claimingTeamId',
        'captureProgress',
        'decayTicksElapsed',
        'controlResumesAtTick',
        // Trailing additive pair (DECISIONS #169). Nullable and always
        // present, the discipline claimingTeamId already follows: null is a
        // fact about this tick, not an omitted field.
        'holdOwnerTeamId',
        'holdEndsAtTick',
        // The side objective's two facts, on the same discipline: null
        // owner means neutral (or no side objective declared at all), and a
        // signed claim whose sign names the claiming team.
        'secondaryOwnerTeamId',
        'secondaryClaimProgress',
        // The economy's two collections are TRAILING and optional: they
        // appear only on a ruleset that declares an economy, so a document
        // written before the capability existed reads identically.
        ...(own(base, 'scrapTeams') ? ['scrapTeams'] : []),
        ...(own(base, 'scrapPiles') ? ['scrapPiles'] : []),
      ],
      fail,
    );
    nonEmpty(item.modeId, `${path}.modeId`, fail);
    for (const key of [
      'activePositionIndex',
      'captureProgress',
      'decayTicksElapsed',
      'controlResumesAtTick',
    ]) {
      integer(item[key], `${path}.${key}`, fail);
    }
    nullable(item.claimingTeamId, `${path}.claimingTeamId`, integer, fail);
    nullable(item.holdOwnerTeamId, `${path}.holdOwnerTeamId`, integer, fail);
    nullable(item.holdEndsAtTick, `${path}.holdEndsAtTick`, integer, fail);
    if ((item.holdOwnerTeamId === null) !== (item.holdEndsAtTick === null)) {
      fail(
        `${path}.holdOwnerTeamId`,
        'territory-ratchet hold owner and expiry must be published together',
      );
    }
    nullable(
      item.secondaryOwnerTeamId,
      `${path}.secondaryOwnerTeamId`,
      integer,
      fail,
    );
    integer(item.secondaryClaimProgress, `${path}.secondaryClaimProgress`, fail);
    if (own(item, 'scrapTeams')) {
      const teams = array(item.scrapTeams, `${path}.scrapTeams`, fail);
      if (teams.length === 0) {
        fail(`${path}.scrapTeams`, 'must be omitted when empty');
      }
      teams.forEach((entry, index) => {
        const teamPath = `${path}.scrapTeams[${index}]`;
        const team = exact(
          object(entry, teamPath, fail),
          teamPath,
          ['teamId', 'bank', 'tierLevels'],
          fail,
        );
        integer(team.teamId, `${teamPath}.teamId`, fail);
        integer(team.bank, `${teamPath}.bank`, fail);
        const tiers = array(team.tierLevels, `${teamPath}.tierLevels`, fail);
        tiers.forEach((tier, tierIndex) =>
          integer(tier, `${teamPath}.tierLevels[${tierIndex}]`, fail),
        );
        if ((team.bank as number) < 0 || tiers.some((tier) => (tier as number) < 0)) {
          fail(`${teamPath}.bank`, 'a bank and its tiers are never negative');
        }
      });
    }
    if (own(item, 'scrapPiles')) {
      const piles = array(item.scrapPiles, `${path}.scrapPiles`, fail);
      if (piles.length === 0) {
        fail(`${path}.scrapPiles`, 'must be omitted when empty');
      }
      let previousKey: string | null = null;
      piles.forEach((entry, index) => {
        const pilePath = `${path}.scrapPiles[${index}]`;
        const pile = exact(
          object(entry, pilePath, fail),
          pilePath,
          ['position', 'amount', 'expiresAtTick'],
          fail,
        );
        position(pile.position, `${pilePath}.position`, fail);
        integer(pile.amount, `${pilePath}.amount`, fail);
        integer(pile.expiresAtTick, `${pilePath}.expiresAtTick`, fail);
        if ((pile.amount as number) <= 0) {
          fail(`${pilePath}.amount`, 'a published pile carries something');
        }
        const point = pile.position as { x: number; y: number };
        const key = `${String(point?.y).padStart(6, '0')}:${String(point?.x).padStart(6, '0')}`;
        if (previousKey !== null && previousKey >= key) {
          fail(
            `${pilePath}.position`,
            'scrap piles must be strictly ordered by (y, x)',
          );
        }
        previousKey = key;
      });
    }
    return;
  }
  if (base.kind === 'arc-relay') {
    const item = exact(
      base,
      path,
      [
        'kind',
        'modeId',
        'wells',
        'reactors',
        'visibleCores',
        'visibleSignatures',
        'latestPulseTeamId',
        'latestPulseTick',
      ],
      fail,
    );
    nonEmpty(item.modeId, `${path}.modeId`, fail);
    const coreId = (value: unknown, corePath: string) => {
      const core = exact(
        value,
        corePath,
        ['sourceWellId', 'sourceOrdinal'],
        fail,
      );
      nonEmpty(core.sourceWellId, `${corePath}.sourceWellId`, fail);
      integer(core.sourceOrdinal, `${corePath}.sourceOrdinal`, fail);
    };
    array(item.wells, `${path}.wells`, fail).forEach((entry, index) => {
      const wellPath = `${path}.wells[${index}]`;
      const well = exact(
        entry,
        wellPath,
        [
          'wellId',
          'position',
          'nextScheduledBirthTick',
          'outstandingCoreId',
          'pendingCharge',
          'rearmCompletesAtTick',
        ],
        fail,
      );
      nonEmpty(well.wellId, `${wellPath}.wellId`, fail);
      position(well.position, `${wellPath}.position`, fail);
      nullable(
        well.nextScheduledBirthTick,
        `${wellPath}.nextScheduledBirthTick`,
        integer,
        fail,
      );
      if (well.outstandingCoreId !== null)
        coreId(well.outstandingCoreId, `${wellPath}.outstandingCoreId`);
      boolean(well.pendingCharge, `${wellPath}.pendingCharge`, fail);
      nullable(
        well.rearmCompletesAtTick,
        `${wellPath}.rearmCompletesAtTick`,
        integer,
        fail,
      );
    });
    array(item.reactors, `${path}.reactors`, fail).forEach((entry, index) => {
      const reactorPath = `${path}.reactors[${index}]`;
      const hasSockets =
        typeof entry === 'object'
        && entry !== null
        && own(entry, 'filledSocketWellIds');
      const reactor = exact(
        entry,
        reactorPath,
        [
          'teamId',
          'position',
          'chargePips',
          'integritySegments',
          // Threefold sockets, written only under threefold rulesets.
          ...(hasSockets ? ['filledSocketWellIds'] : []),
        ],
        fail,
      );
      integer(reactor.teamId, `${reactorPath}.teamId`, fail);
      position(reactor.position, `${reactorPath}.position`, fail);
      integer(reactor.chargePips, `${reactorPath}.chargePips`, fail);
      integer(
        reactor.integritySegments,
        `${reactorPath}.integritySegments`,
        fail,
      );
      if (hasSockets) {
        array(
          reactor.filledSocketWellIds,
          `${reactorPath}.filledSocketWellIds`,
          fail,
        ).forEach((wellId, wellIndex) =>
          nonEmpty(
            wellId,
            `${reactorPath}.filledSocketWellIds[${wellIndex}]`,
            fail,
          ),
        );
      }
    });
    array(item.visibleCores, `${path}.visibleCores`, fail).forEach(
      (entry, index) => {
        const corePath = `${path}.visibleCores[${index}]`;
        const hasChargeValue =
          typeof entry === 'object'
          && entry !== null
          && own(entry, 'chargeValue');
        const core = exact(
          entry,
          corePath,
          [
            'coreId',
            'position',
            'disposition',
            'carrierActorId',
            'nextRelocationTick',
            'flightTarget',
            'flightCompletesAtTick',
            // Charge-value rulesets only.
            ...(hasChargeValue ? ['chargeValue'] : []),
          ],
          fail,
        );
        coreId(core.coreId, `${corePath}.coreId`);
        position(core.position, `${corePath}.position`, fail);
        if (!['loose', 'carried', 'in-flight'].includes(String(core.disposition)))
          fail(`${corePath}.disposition`, 'unknown Core disposition');
        if (core.carrierActorId !== null)
          actorId(core.carrierActorId, `${corePath}.carrierActorId`, fail);
        integer(core.nextRelocationTick, `${corePath}.nextRelocationTick`, fail);
        if (core.flightTarget !== null)
          position(core.flightTarget, `${corePath}.flightTarget`, fail);
        nullable(
          core.flightCompletesAtTick,
          `${corePath}.flightCompletesAtTick`,
          integer,
          fail,
        );
        if (hasChargeValue)
          integer(core.chargeValue, `${corePath}.chargeValue`, fail);
      },
    );
    array(
      item.visibleSignatures,
      `${path}.visibleSignatures`,
      fail,
    ).forEach((entry, index) => {
      const signaturePath = `${path}.visibleSignatures[${index}]`;
      const signature = exact(
        entry,
        signaturePath,
        [
          'operationId',
          'signatureId',
          'signatureKind',
          'ownerActorId',
          'ownerTeamId',
          'phase',
          'startedTick',
          'completesAtTick',
          'endsAtTick',
          'positions',
          'targetActorId',
          'remainingCapacity',
          'suppressed',
        ],
        fail,
      );
      for (const key of ['operationId', 'signatureId', 'signatureKind'])
        nonEmpty(signature[key], `${signaturePath}.${key}`, fail);
      actorId(signature.ownerActorId, `${signaturePath}.ownerActorId`, fail);
      integer(signature.ownerTeamId, `${signaturePath}.ownerTeamId`, fail);
      if (!['tell', 'active', 'channel', 'in-flight'].includes(String(signature.phase)))
        fail(`${signaturePath}.phase`, 'unknown signature phase');
      integer(signature.startedTick, `${signaturePath}.startedTick`, fail);
      nullable(
        signature.completesAtTick,
        `${signaturePath}.completesAtTick`,
        integer,
        fail,
      );
      nullable(
        signature.endsAtTick,
        `${signaturePath}.endsAtTick`,
        integer,
        fail,
      );
      array(signature.positions, `${signaturePath}.positions`, fail).forEach(
        (point, pointIndex) =>
          position(point, `${signaturePath}.positions[${pointIndex}]`, fail),
      );
      if (signature.targetActorId !== null)
        actorId(
          signature.targetActorId,
          `${signaturePath}.targetActorId`,
          fail,
        );
      integer(
        signature.remainingCapacity,
        `${signaturePath}.remainingCapacity`,
        fail,
      );
      boolean(signature.suppressed, `${signaturePath}.suppressed`, fail);
    });
    nullable(item.latestPulseTeamId, `${path}.latestPulseTeamId`, integer, fail);
    nullable(item.latestPulseTick, `${path}.latestPulseTick`, integer, fail);
    return;
  }
  fail(`${path}.kind`, `unknown replay-v3 mode ${String(base.kind)}`);
}

function worldState(value: unknown, path: string, fail: ReplayV3Fail): void {
  const world = exact(
    value,
    path,
    [
      'matchContractFingerprint',
      'nextTick',
      'nextProjectileId',
      'participants',
      'slots',
      'activeLives',
      'pendingReplications',
      'projectiles',
      'scoreboard',
      'mode',
    ],
    fail,
  );
  nonEmpty(
    world.matchContractFingerprint,
    `${path}.matchContractFingerprint`,
    fail,
  );
  integer(world.nextTick, `${path}.nextTick`, fail);
  int64(world.nextProjectileId, `${path}.nextProjectileId`, fail, true);
  array(world.participants, `${path}.participants`, fail).forEach(
    (entry, index) => participantStatus(entry, `${path}.participants[${index}]`, fail),
  );
  array(world.slots, `${path}.slots`, fail).forEach((entry, index) =>
    slotState(entry, `${path}.slots[${index}]`, fail),
  );
  array(world.activeLives, `${path}.activeLives`, fail).forEach((entry, index) =>
    lifeState(entry, `${path}.activeLives[${index}]`, fail),
  );
  array(world.pendingReplications, `${path}.pendingReplications`, fail).forEach(
    (entry, index) =>
      pendingReplication(entry, `${path}.pendingReplications[${index}]`, fail),
  );
  array(world.projectiles, `${path}.projectiles`, fail).forEach((entry, index) =>
    projectileState(entry, `${path}.projectiles[${index}]`, fail),
  );
  scoreboard(world.scoreboard, `${path}.scoreboard`, fail);
  modeState(world.mode, `${path}.mode`, fail);
}

function lifeStart(value: unknown, path: string, fail: ReplayV3Fail): void {
  const container = object(value, path, fail);
  // Trailing additive key, the routeCooldowns discipline: present in every
  // document written since the team stream landed, absent in one written
  // before it, and never optional within a single document's own bytes.
  const hasTeamRandomSeed = own(container, 'teamRandomSeed');
  const start = exact(
    container,
    path,
    [
      'schemaVersion',
      'runtimeContractVersion',
      'actorId',
      'participantId',
      'actorRandomSeed',
      'origin',
      'matchContractFingerprint',
      ...(hasTeamRandomSeed ? ['teamRandomSeed'] : []),
    ],
    fail,
  );
  integer(start.schemaVersion, `${path}.schemaVersion`, fail);
  integer(start.runtimeContractVersion, `${path}.runtimeContractVersion`, fail);
  actorId(start.actorId, `${path}.actorId`, fail);
  integer(start.participantId, `${path}.participantId`, fail);
  uint64(start.actorRandomSeed, `${path}.actorRandomSeed`, fail);
  const origin = exact(
    start.origin,
    `${path}.origin`,
    [
      'reason',
      'generation',
      'parentActorId',
      'sourceTransitionId',
      'sourceOperationId',
    ],
    fail,
  );
  nonEmpty(origin.reason, `${path}.origin.reason`, fail);
  integer(origin.generation, `${path}.origin.generation`, fail);
  nullable(origin.parentActorId, `${path}.origin.parentActorId`, actorId, fail);
  nullable(
    origin.sourceTransitionId,
    `${path}.origin.sourceTransitionId`,
    string,
    fail,
  );
  nullable(
    origin.sourceOperationId,
    `${path}.origin.sourceOperationId`,
    string,
    fail,
  );
  nonEmpty(
    start.matchContractFingerprint,
    `${path}.matchContractFingerprint`,
    fail,
  );
  if (hasTeamRandomSeed) {
    // Bounds only, exactly as actorRandomSeed is treated here: the seed
    // ALGORITHM lives in the engine, so the C# validator is the layer that
    // re-derives this value from the header seed and refuses a forged or
    // team-swapped one. The viewer refuses only what it can decide alone.
    uint64(start.teamRandomSeed, `${path}.teamRandomSeed`, fail);
  }
}

/**
 * Canonical form for observed route cooldowns: the key exists only while at
 * least one clock is live, entries are ordered by transition ID, and a
 * published clock must still bind (a lapsed one is an impossible history).
 */
function validateRouteCooldowns(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const entries = array(value, path, fail);
  if (entries.length === 0) {
    fail(path, 'must be omitted when empty');
  }
  let previousTransitionId: string | null = null;
  entries.forEach((entry, index) => {
    const cooldownPath = `${path}[${index}]`;
    const cooldown = exact(
      entry,
      cooldownPath,
      ['transitionId', 'readyAtTick'],
      fail,
    );
    nonEmpty(cooldown.transitionId, `${cooldownPath}.transitionId`, fail);
    integer(cooldown.readyAtTick, `${cooldownPath}.readyAtTick`, fail);
    if (
      typeof cooldown.transitionId === 'string' &&
      previousTransitionId !== null &&
      previousTransitionId >= cooldown.transitionId
    ) {
      fail(
        `${cooldownPath}.transitionId`,
        'route cooldowns must be strictly ordered by transition id',
      );
    }
    if (typeof cooldown.transitionId === 'string') {
      previousTransitionId = cooldown.transitionId;
    }
  });
}

function observedSelf(value: unknown, path: string, fail: ReplayV3Fail): void {
  const self = object(value, path, fail);
  const hasRouteCooldowns = own(self, 'routeCooldowns');
  // Trailing additive key on the same discipline: the load is written only
  // while the body is actually carrying, so a document from a contract with
  // no declared economy never carries the key.
  const hasCarriedScrap = own(self, 'carriedScrap');
  const hasRoleTag = own(self, 'roleTag');
  const item = exact(
    self,
    path,
    [
      'actorId',
      'generation',
      'formId',
      'position',
      'facing',
      'health',
      'cooldown',
      'energy',
      'previousActionResolution',
      'pendingSameLifeTransition',
      'classId',
      ...(hasRouteCooldowns ? ['routeCooldowns'] : []),
      ...(hasCarriedScrap ? ['carriedScrap'] : []),
      ...(hasRoleTag ? ['roleTag'] : []),
    ],
    fail,
  );
  if (hasRoleTag) roleTag(item.roleTag, `${path}.roleTag`, fail);
  actorId(item.actorId, `${path}.actorId`, fail);
  integer(item.generation, `${path}.generation`, fail);
  nonEmpty(item.formId, `${path}.formId`, fail);
  position(item.position, `${path}.position`, fail);
  direction(item.facing, `${path}.facing`, fail);
  integer(item.health, `${path}.health`, fail);
  integer(item.cooldown, `${path}.cooldown`, fail);
  nullable(item.energy, `${path}.energy`, integer, fail);
  nullable(
    item.previousActionResolution,
    `${path}.previousActionResolution`,
    actionResolution,
    fail,
  );
  pendingTransition(
    item.pendingSameLifeTransition,
    `${path}.pendingSameLifeTransition`,
    fail,
  );
  nullable(item.classId, `${path}.classId`, semanticId, fail);
  if (hasRouteCooldowns) {
    validateRouteCooldowns(item.routeCooldowns, `${path}.routeCooldowns`, fail);
  }
  if (hasCarriedScrap) {
    integer(item.carriedScrap, `${path}.carriedScrap`, fail);
    if ((item.carriedScrap as number) <= 0) {
      fail(`${path}.carriedScrap`, 'must be omitted when nothing is carried');
    }
  }
}

function eventPayload(value: unknown, path: string, fail: ReplayV3Fail): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  const actorAction = (keys: string[]) => {
    const item = exact(base, path, ['kind', 'actorId', 'action', ...keys], fail);
    actorId(item.actorId, `${path}.actorId`, fail);
    resolvedAction(item.action, `${path}.action`, fail);
    return item;
  };
  switch (base.kind) {
    case 'rotation': {
      const item = actorAction(['position', 'fromFacing', 'toFacing']);
      position(item.position, `${path}.position`, fail);
      direction(item.fromFacing, `${path}.fromFacing`, fail);
      direction(item.toFacing, `${path}.toFacing`, fail);
      return;
    }
    case 'movement': {
      const item = actorAction(['from', 'to', 'facing']);
      position(item.from, `${path}.from`, fail);
      position(item.to, `${path}.to`, fail);
      direction(item.facing, `${path}.facing`, fail);
      return;
    }
    case 'movement-blocked': {
      const item = actorAction(['from', 'attemptedTo', 'facing']);
      position(item.from, `${path}.from`, fail);
      position(item.attemptedTo, `${path}.attemptedTo`, fail);
      direction(item.facing, `${path}.facing`, fail);
      return;
    }
    case 'attack': {
      const item = actorAction(['projectileId', 'origin', 'heading']);
      int64(item.projectileId, `${path}.projectileId`, fail, true);
      position(item.origin, `${path}.origin`, fail);
      heading(item.heading, `${path}.heading`, fail);
      return;
    }
    case 'projectile-deflected': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'sourceTeamId',
          'sourceActorId',
          'targetActorId',
          'projectileId',
          'deflectedProjectileId',
          'targetFormId',
          'targetFacing',
          'heading',
          'position',
        ],
        fail,
      );
      integer(item.sourceTeamId, `${path}.sourceTeamId`, fail);
      nullable(item.sourceActorId, `${path}.sourceActorId`, actorId, fail);
      actorId(item.targetActorId, `${path}.targetActorId`, fail);
      int64(item.projectileId, `${path}.projectileId`, fail, true);
      int64(
        item.deflectedProjectileId,
        `${path}.deflectedProjectileId`,
        fail,
        true,
      );
      nonEmpty(item.targetFormId, `${path}.targetFormId`, fail);
      direction(item.targetFacing, `${path}.targetFacing`, fail);
      heading(item.heading, `${path}.heading`, fail);
      position(item.position, `${path}.position`, fail);
      return;
    }
    case 'damage': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'sourceTeamId',
          'sourceActorId',
          'targetActorId',
          'projectileId',
          'amount',
          'newHealth',
          'position',
        ],
        fail,
      );
      integer(item.sourceTeamId, `${path}.sourceTeamId`, fail);
      nullable(item.sourceActorId, `${path}.sourceActorId`, actorId, fail);
      actorId(item.targetActorId, `${path}.targetActorId`, fail);
      int64(item.projectileId, `${path}.projectileId`, fail, true);
      integer(item.amount, `${path}.amount`, fail);
      integer(item.newHealth, `${path}.newHealth`, fail);
      position(item.position, `${path}.position`, fail);
      return;
    }
    case 'destruction': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'actorId',
          'sourceTeamId',
          'sourceActorId',
          'projectileId',
          'generation',
          'formId',
          'position',
        ],
        fail,
      );
      actorId(item.actorId, `${path}.actorId`, fail);
      nullable(item.sourceTeamId, `${path}.sourceTeamId`, integer, fail);
      nullable(item.sourceActorId, `${path}.sourceActorId`, actorId, fail);
      if (item.projectileId !== null) {
        int64(item.projectileId, `${path}.projectileId`, fail, true);
      }
      integer(item.generation, `${path}.generation`, fail);
      nonEmpty(item.formId, `${path}.formId`, fail);
      position(item.position, `${path}.position`, fail);
      return;
    }
    case 'life-spawned': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'actorId',
          'participantId',
          'parentActorId',
          'generation',
          'formId',
          'health',
          'position',
          'reason',
          'sourceTransitionId',
          'sourceOperationId',
        ],
        fail,
      );
      actorId(item.actorId, `${path}.actorId`, fail);
      integer(item.participantId, `${path}.participantId`, fail);
      nullable(item.parentActorId, `${path}.parentActorId`, actorId, fail);
      integer(item.generation, `${path}.generation`, fail);
      nonEmpty(item.formId, `${path}.formId`, fail);
      integer(item.health, `${path}.health`, fail);
      position(item.position, `${path}.position`, fail);
      nonEmpty(item.reason, `${path}.reason`, fail);
      nullable(item.sourceTransitionId, `${path}.sourceTransitionId`, string, fail);
      nullable(item.sourceOperationId, `${path}.sourceOperationId`, string, fail);
      return;
    }
    case 'life-retired': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'actorId',
          'generation',
          'formId',
          'position',
          'reason',
          'sourceTransitionId',
          'sourceOperationId',
        ],
        fail,
      );
      actorId(item.actorId, `${path}.actorId`, fail);
      integer(item.generation, `${path}.generation`, fail);
      nonEmpty(item.formId, `${path}.formId`, fail);
      position(item.position, `${path}.position`, fail);
      nonEmpty(item.reason, `${path}.reason`, fail);
      nullable(item.sourceTransitionId, `${path}.sourceTransitionId`, string, fail);
      nullable(item.sourceOperationId, `${path}.sourceOperationId`, string, fail);
      return;
    }
    case 'runtime-fault': {
      const item = exact(base, path, ['kind', 'fault'], fail);
      runtimeFault(item.fault, `${path}.fault`, fail);
      return;
    }
    case 'mind-runtime-fault': {
      // The participant-scoped fault with no body to attribute it to. It
      // exists ONLY for that case, so a payload carrying an actor identity is
      // the per-body event wearing the wrong kind.
      const item = exact(base, path, ['kind', 'fault'], fail);
      mindRuntimeFault(item.fault, `${path}.fault`, fail);
      if ((item.fault as { actorId: unknown }).actorId !== null) {
        fail(`${path}.fault.actorId`, 'must be null on a mind-scoped fault');
      }
      return;
    }
    case 'participant': {
      const item = exact(base, path, ['kind', 'participantId', 'teamId'], fail);
      integer(item.participantId, `${path}.participantId`, fail);
      integer(item.teamId, `${path}.teamId`, fail);
      return;
    }
    case 'lifecycle': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'transitionId',
          'operationId',
          'sourceActorId',
          'targetTeamId',
          'targetUnitId',
          'dueTick',
          'cancellationReason',
        ],
        fail,
      );
      nonEmpty(item.transitionId, `${path}.transitionId`, fail);
      nonEmpty(item.operationId, `${path}.operationId`, fail);
      actorId(item.sourceActorId, `${path}.sourceActorId`, fail);
      nullable(item.targetTeamId, `${path}.targetTeamId`, integer, fail);
      nullable(item.targetUnitId, `${path}.targetUnitId`, integer, fail);
      nullable(item.dueTick, `${path}.dueTick`, integer, fail);
      nullable(item.cancellationReason, `${path}.cancellationReason`, string, fail);
      return;
    }
    case 'form-transition': {
      // The cause is additive and omitted while inert: absent means the
      // author requested it, so an explicit 'requested' is refused as a
      // second encoding of the same history.
      const hasReason = own(base, 'reason');
      const item = exact(
        base,
        path,
        [
          'kind',
          'actorId',
          'transitionId',
          'operationId',
          'fromFormId',
          'toFormId',
          'startedTick',
          'dueTick',
          ...(hasReason ? ['reason'] : []),
        ],
        fail,
      );
      actorId(item.actorId, `${path}.actorId`, fail);
      for (const key of [
        'transitionId',
        'operationId',
        'fromFormId',
        'toFormId',
      ]) {
        nonEmpty(item[key], `${path}.${key}`, fail);
      }
      integer(item.startedTick, `${path}.startedTick`, fail);
      integer(item.dueTick, `${path}.dueTick`, fail);
      if (hasReason && item.reason !== 'automatic-threshold-return') {
        fail(`${path}.reason`, 'must be omitted instead of emitted inert');
      }
      return;
    }
    case 'score-changed': {
      const item = exact(
        base,
        path,
        ['kind', 'teamId', 'channel', 'newValue'],
        fail,
      );
      integer(item.teamId, `${path}.teamId`, fail);
      nonEmpty(item.channel, `${path}.channel`, fail);
      int64(item.newValue, `${path}.newValue`, fail);
      return;
    }
    case 'mode-changed': {
      const item = exact(base, path, ['kind', 'state'], fail);
      modeState(item.state, `${path}.state`, fail);
      return;
    }
    case 'lifecycle-clock-cancelled': {
      const item = exact(
        base,
        path,
        [
          'kind',
          'targetTeamId',
          'targetUnitId',
          'cancelledState',
          'cancellationReason',
        ],
        fail,
      );
      integer(item.targetTeamId, `${path}.targetTeamId`, fail);
      integer(item.targetUnitId, `${path}.targetUnitId`, fail);
      unitSlotState(item.cancelledState, `${path}.cancelledState`, fail);
      nonEmpty(item.cancellationReason, `${path}.cancellationReason`, fail);
      return;
    }
    case 'arc-relay': {
      const item = exact(base, path, ['kind', 'fact'], fail);
      const fact = object(item.fact, `${path}.fact`, fail);
      nonEmpty(fact.kind, `${path}.fact.kind`, fail);
      jsonValue(fact, `${path}.fact`, fail);
      return;
    }
    default:
      fail(`${path}.kind`, `unknown event payload ${String(base.kind)}`);
  }
}

function validateEventKindAndPayload(
  kind: string,
  payload: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const expectedPayloadKind = (() => {
    switch (kind) {
      case 'rotation':
      case 'movement':
      case 'movement-blocked':
      case 'attack':
      case 'projectile-deflected':
      case 'damage':
      case 'destruction':
      case 'life-spawned':
      case 'life-retired':
      case 'runtime-fault':
      case 'mind-runtime-fault':
      case 'score-changed':
      case 'mode-changed':
      case 'lifecycle-clock-cancelled':
      case 'arc-relay':
        return kind;
      case 'participant-disqualified':
        return 'participant';
      case 'lifecycle-queued':
      case 'lifecycle-cancelled':
      case 'lifecycle-completed':
        return 'lifecycle';
      case 'form-transition-started':
      case 'form-transition-completed':
      case 'form-transition-cancelled':
        return 'form-transition';
      default:
        fail(`${path}.kind`, `unknown event kind ${kind}`);
    }
  })();
  if ((payload as Record<string, unknown>).kind !== expectedPayloadKind) {
    fail(
      `${path}.kind`,
      `must use payload kind ${expectedPayloadKind}`,
    );
  }
}

function observedEvent(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(
    value,
    path,
    ['eventHandle', 'sourceTick', 'sourceOrdinal', 'kind', 'payload', 'observedBy'],
    fail,
  );
  nonEmpty(item.eventHandle, `${path}.eventHandle`, fail);
  integer(item.sourceTick, `${path}.sourceTick`, fail);
  integer(item.sourceOrdinal, `${path}.sourceOrdinal`, fail);
  nonEmpty(item.kind, `${path}.kind`, fail);
  eventPayload(item.payload, `${path}.payload`, fail);
  validateEventKindAndPayload(item.kind, item.payload, path, fail);
  array(item.observedBy, `${path}.observedBy`, fail).forEach((entry, index) =>
    actorId(entry, `${path}.observedBy[${index}]`, fail),
  );
}

function actionConstraint(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  if (base.kind === 'shot-program') {
    const item = exact(base, path, ['kind', 'allowed'], fail);
    boolean(item.allowed, `${path}.allowed`, fail);
    return;
  }
  if (base.kind === 'form-target') {
    const item = exact(base, path, ['kind', 'allowedFormIds'], fail);
    array(item.allowedFormIds, `${path}.allowedFormIds`, fail).forEach(
      (entry, index) =>
        nonEmpty(entry, `${path}.allowedFormIds[${index}]`, fail),
    );
    return;
  }
  if (base.kind === 'upgrade-track') {
    const item = exact(base, path, ['kind', 'allowedTrackIds'], fail);
    array(item.allowedTrackIds, `${path}.allowedTrackIds`, fail).forEach(
      (entry, index) =>
        nonEmpty(entry, `${path}.allowedTrackIds[${index}]`, fail),
    );
    return;
  }
  const item = exact(base, path, ['kind', 'allowedValues'], fail);
  const values = array(item.allowedValues, `${path}.allowedValues`, fail);
  if (base.kind === 'direction') {
    values.forEach((entry, index) =>
      direction(entry, `${path}.allowedValues[${index}]`, fail),
    );
    return;
  }
  if (base.kind === 'projectile-heading') {
    values.forEach((entry, index) =>
      heading(entry, `${path}.allowedValues[${index}]`, fail),
    );
    return;
  }
  if (base.kind === 'unit-target') {
    values.forEach((entry, index) => {
      const target = exact(
        entry,
        `${path}.allowedValues[${index}]`,
        ['teamId', 'unitId'],
        fail,
      );
      integer(target.teamId, `${path}.allowedValues[${index}].teamId`, fail);
      integer(target.unitId, `${path}.allowedValues[${index}].unitId`, fail);
    });
    return;
  }
  if (base.kind === 'position-target') {
    values.forEach((entry, index) =>
      position(entry, `${path}.allowedValues[${index}]`, fail),
    );
    return;
  }
  fail(`${path}.kind`, `unknown action constraint ${String(base.kind)}`);
}

/**
 * The TEAM-SHARED half of an observation: the collections a per-life document
 * repeats once per body and a mind document carries exactly once. Extracted so
 * both turn kinds are validated by the same code — which is the point of the
 * memo's "every nested record type is the existing SDK type, unchanged".
 */
function sharedObservationCollections(
  item: Record<string, unknown>,
  path: string,
  fail: ReplayV3Fail,
): void {
  array(item.teamUnits, `${path}.teamUnits`, fail).forEach((entry, index) => {
    const unit = exact(
      entry,
      `${path}.teamUnits[${index}]`,
      ['teamId', 'unitId', 'state'],
      fail,
    );
    integer(unit.teamId, `${path}.teamUnits[${index}].teamId`, fail);
    integer(unit.unitId, `${path}.teamUnits[${index}].unitId`, fail);
    unitSlotState(unit.state, `${path}.teamUnits[${index}].state`, fail);
  });
  array(item.participants, `${path}.participants`, fail).forEach(
    (entry, index) =>
      participantStatus(entry, `${path}.participants[${index}]`, fail),
  );
  array(item.allies, `${path}.allies`, fail).forEach((entry, index) =>
    observedSelf(entry, `${path}.allies[${index}]`, fail),
  );
  array(item.enemies, `${path}.enemies`, fail).forEach((entry, index) => {
    const enemyValue = object(
      entry,
      `${path}.enemies[${index}]`,
      fail,
    );
    const hasEnemyCarriedScrap = own(enemyValue, 'carriedScrap');
    // Trailing additive key on the same discipline (§12): a published label
    // exists only when a mind set one, so every per-life document is
    // byte-identical to what it was.
    const hasEnemyRoleTag = own(enemyValue, 'roleTag');
    const enemy = exact(
      enemyValue,
      `${path}.enemies[${index}]`,
      [
        'actorId',
        'formId',
        'position',
        'facing',
        'health',
        'pendingSameLifeTransition',
        'observedBy',
        'classId',
        ...(hasEnemyCarriedScrap ? ['carriedScrap'] : []),
        ...(hasEnemyRoleTag ? ['roleTag'] : []),
      ],
      fail,
    );
    actorId(enemy.actorId, `${path}.enemies[${index}].actorId`, fail);
    nonEmpty(enemy.formId, `${path}.enemies[${index}].formId`, fail);
    position(enemy.position, `${path}.enemies[${index}].position`, fail);
    direction(enemy.facing, `${path}.enemies[${index}].facing`, fail);
    integer(enemy.health, `${path}.enemies[${index}].health`, fail);
    pendingTransition(
      enemy.pendingSameLifeTransition,
      `${path}.enemies[${index}].pendingSameLifeTransition`,
      fail,
    );
    array(enemy.observedBy, `${path}.enemies[${index}].observedBy`, fail).forEach(
      (observer, observerIndex) =>
        actorId(
          observer,
          `${path}.enemies[${index}].observedBy[${observerIndex}]`,
          fail,
        ),
    );
    nullable(
      enemy.classId,
      `${path}.enemies[${index}].classId`,
      semanticId,
      fail,
    );
    if (hasEnemyCarriedScrap) {
      integer(
        enemy.carriedScrap,
        `${path}.enemies[${index}].carriedScrap`,
        fail,
      );
      if ((enemy.carriedScrap as number) <= 0) {
        fail(
          `${path}.enemies[${index}].carriedScrap`,
          'must be omitted when nothing is carried',
        );
      }
    }
    if (hasEnemyRoleTag) {
      roleTag(enemy.roleTag, `${path}.enemies[${index}].roleTag`, fail);
    }
  });
  array(item.visibleTiles, `${path}.visibleTiles`, fail).forEach(
    (entry, index) => {
      const tileValue = object(
        entry,
        `${path}.visibleTiles[${index}]`,
        fail,
      );
      const tile = exact(
        tileValue,
        `${path}.visibleTiles[${index}]`,
        // spawnReservation is nullable and always present, the discipline
        // every other nullable observation fact follows: null is a fact
        // about this tile, not an omitted field.
        ['position', 'isWall', 'observedBy', 'spawnReservation'],
        fail,
      );
      position(tile.position, `${path}.visibleTiles[${index}].position`, fail);
      boolean(tile.isWall, `${path}.visibleTiles[${index}].isWall`, fail);
      array(
        tile.observedBy,
        `${path}.visibleTiles[${index}].observedBy`,
        fail,
      ).forEach((observer, observerIndex) =>
        actorId(
          observer,
          `${path}.visibleTiles[${index}].observedBy[${observerIndex}]`,
          fail,
        ),
      );
      if (tile.spawnReservation !== null) {
        const reservationPath =
          `${path}.visibleTiles[${index}].spawnReservation`;
        const reservation = exact(
          tile.spawnReservation,
          reservationPath,
          ['teamId', 'unitId', 'kind', 'dueTick'],
          fail,
        );
        string(reservation.kind, `${reservationPath}.kind`, fail);
        const automatic = reservation.kind === 'automatic-return';
        if (
          !automatic &&
          reservation.kind !== 'fabrication' &&
          reservation.kind !== 'replication'
        ) {
          fail(
            `${reservationPath}.kind`,
            `unknown spawn reservation ${String(reservation.kind)}`,
          );
        }
        // A permanent slot claim has no clock; a lifecycle output has one.
        if (automatic !== (reservation.dueTick === null)) {
          fail(
            reservationPath,
            automatic
              ? 'automatic-return must have a null dueTick'
              : 'dynamic spawn reservations require dueTick',
          );
        }
        nullable(
          reservation.dueTick,
          `${reservationPath}.dueTick`,
          integer,
          fail,
        );
        integer(reservation.teamId, `${reservationPath}.teamId`, fail);
        integer(reservation.unitId, `${reservationPath}.unitId`, fail);
      }
    },
  );
  if (item.visibleProjectiles !== null) {
    array(item.visibleProjectiles, `${path}.visibleProjectiles`, fail).forEach(
      (entry, index) => {
        const projectile = exact(
          entry,
          `${path}.visibleProjectiles[${index}]`,
          [
            'projectileId',
            'ownerTeamId',
            'ownerActorId',
            'position',
            'heading',
            'tilesPerAdvance',
            'ticksUntilAdvance',
            'remainingTiles',
            'observedBy',
            // Trailing additive pair (DECISIONS #169): the timing cadence and
            // the cost of one contact, published per projectile because a
            // volley bolt and a mobile bolt need not agree on either.
            'ticksPerAdvance',
            'damagePerHit',
          ],
          fail,
        );
        int64(
          projectile.projectileId,
          `${path}.visibleProjectiles[${index}].projectileId`,
          fail,
          true,
        );
        integer(
          projectile.ownerTeamId,
          `${path}.visibleProjectiles[${index}].ownerTeamId`,
          fail,
        );
        nullable(
          projectile.ownerActorId,
          `${path}.visibleProjectiles[${index}].ownerActorId`,
          actorId,
          fail,
        );
        position(
          projectile.position,
          `${path}.visibleProjectiles[${index}].position`,
          fail,
        );
        heading(
          projectile.heading,
          `${path}.visibleProjectiles[${index}].heading`,
          fail,
        );
        for (const key of [
          'tilesPerAdvance',
          'ticksUntilAdvance',
          'remainingTiles',
          'ticksPerAdvance',
          'damagePerHit',
        ]) {
          integer(
            projectile[key],
            `${path}.visibleProjectiles[${index}].${key}`,
            fail,
          );
        }
        if (
          (projectile.tilesPerAdvance as number) <= 0 ||
          (projectile.ticksUntilAdvance as number) <= 0 ||
          (projectile.remainingTiles as number) < 0 ||
          (projectile.ticksPerAdvance as number) <= 0 ||
          (projectile.ticksUntilAdvance as number) >
            (projectile.ticksPerAdvance as number) ||
          (projectile.damagePerHit as number) <= 0
        ) {
          fail(
            `${path}.visibleProjectiles[${index}]`,
            'projectile timing, speed, range, and damage are outside their canonical domains',
          );
        }
        array(
          projectile.observedBy,
          `${path}.visibleProjectiles[${index}].observedBy`,
          fail,
        ).forEach((observer, observerIndex) =>
          actorId(
            observer,
            `${path}.visibleProjectiles[${index}].observedBy[${observerIndex}]`,
            fail,
          ),
        );
      },
    );
  }
  array(item.visibleEvents, `${path}.visibleEvents`, fail).forEach(
    (entry, index) =>
      observedEvent(entry, `${path}.visibleEvents[${index}]`, fail),
  );
  if (item.heardSounds !== null) {
    array(item.heardSounds, `${path}.heardSounds`, fail).forEach(
      (entry, index) => {
        const sound = exact(
          entry,
          `${path}.heardSounds[${index}]`,
          [
            'eventHandle',
            'sourceTick',
            'sourceOrdinal',
            'observerActorId',
            'kind',
            'bearing',
            'distance',
          ],
          fail,
        );
        nonEmpty(sound.eventHandle, `${path}.heardSounds[${index}].eventHandle`, fail);
        for (const key of ['sourceTick', 'sourceOrdinal', 'bearing', 'distance']) {
          integer(sound[key], `${path}.heardSounds[${index}].${key}`, fail);
        }
        actorId(
          sound.observerActorId,
          `${path}.heardSounds[${index}].observerActorId`,
          fail,
        );
        nonEmpty(sound.kind, `${path}.heardSounds[${index}].kind`, fail);
      },
    );
  }
  scoreboard(item.scoreboard, `${path}.scoreboard`, fail);
  modeState(item.mode, `${path}.mode`, fail);
}

function observation(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    [
      'schemaVersion',
      'tick',
      'matchContractFingerprint',
      'self',
      'teamUnits',
      'participants',
      'allies',
      'enemies',
      'visibleTiles',
      'visibleProjectiles',
      'visibleEvents',
      'heardSounds',
      'scoreboard',
      'mode',
      'actionLegalities',
    ],
    fail,
  );
  integer(item.schemaVersion, `${path}.schemaVersion`, fail);
  integer(item.tick, `${path}.tick`, fail);
  nonEmpty(
    item.matchContractFingerprint,
    `${path}.matchContractFingerprint`,
    fail,
  );
  observedSelf(item.self, `${path}.self`, fail);
  sharedObservationCollections(item, path, fail);
  array(item.actionLegalities, `${path}.actionLegalities`, fail).forEach(
    (entry, index) =>
      actionLegality(entry, `${path}.actionLegalities[${index}]`, fail),
  );
}

function actionLegality(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const legality = exact(
    value,
    path,
    ['actionId', 'actionCode', 'allowedByForm', 'available', 'constraints'],
    fail,
  );
  nonEmpty(legality.actionId, `${path}.actionId`, fail);
  integer(legality.actionCode, `${path}.actionCode`, fail);
  boolean(legality.allowedByForm, `${path}.allowedByForm`, fail);
  boolean(legality.available, `${path}.available`, fail);
  array(legality.constraints, `${path}.constraints`, fail).forEach(
    (constraint, constraintIndex) =>
      actionConstraint(
        constraint,
        `${path}.constraints[${constraintIndex}]`,
        fail,
      ),
  );
}

function submittedDecision(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    ['actionId', 'actionCode', 'arguments', 'debugMessage'],
    fail,
  );
  nullable(item.actionId, `${path}.actionId`, string, fail);
  integer(item.actionCode, `${path}.actionCode`, fail);
  if (item.arguments !== null) {
    array(item.arguments, `${path}.arguments`, fail).forEach((entry, index) => {
      if (entry !== null) rawArgument(entry, `${path}.arguments[${index}]`, fail);
    });
  }
  nullable(item.debugMessage, `${path}.debugMessage`, string, fail);
}

function actorTurn(value: unknown, path: string, fail: ReplayV3Fail): void {
  const turn = exact(
    value,
    path,
    [
      'tick',
      'participantId',
      'actorId',
      'observation',
      'submittedDecision',
      'actionResolution',
    ],
    fail,
  );
  integer(turn.tick, `${path}.tick`, fail);
  integer(turn.participantId, `${path}.participantId`, fail);
  actorId(turn.actorId, `${path}.actorId`, fail);
  observation(turn.observation, `${path}.observation`, fail);
  nullable(
    turn.submittedDecision,
    `${path}.submittedDecision`,
    submittedDecision,
    fail,
  );
  actionResolution(turn.actionResolution, `${path}.actionResolution`, fail);
}

/**
 * A role tag (docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md §12.1): a canonical
 * lowercase-kebab semantic ID capped at 24 UTF-8 bytes rather than the 64-byte
 * semantic-ID cap, because it is a display label sent per body per tick. The
 * EMPTY string is legal on a command and means "clear the tag"; an absent field
 * means "leave it unchanged", and the two must stay distinct.
 */
const ROLE_TAG_MAX_UTF8_BYTES = 24;

function roleTag(value: unknown, path: string, fail: ReplayV3Fail): void {
  if (typeof value !== 'string') {
    fail(path, 'expected a role tag string');
    return;
  }
  if (value.length === 0) return;
  if (new TextEncoder().encode(value).length > ROLE_TAG_MAX_UTF8_BYTES) {
    fail(path, `must not exceed ${ROLE_TAG_MAX_UTF8_BYTES} UTF-8 bytes`);
  }
  semanticId(value, path, fail);
}

function mindIntent(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(value, path, ['tagId', 'value'], fail);
  semanticId(item.tagId, `${path}.tagId`, fail);
  int64(item.value, `${path}.value`, fail);
}

function mindAlliedIntent(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(value, path, ['participantId', 'tagId', 'value'], fail);
  integer(item.participantId, `${path}.participantId`, fail);
  semanticId(item.tagId, `${path}.tagId`, fail);
  int64(item.value, `${path}.value`, fail);
}

function mindRuntimeFault(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    [
      'participantId',
      'teamId',
      'actorId',
      'stage',
      'faultCode',
      'cumulativeFaultCount',
      'disqualificationTriggered',
    ],
    fail,
  );
  integer(item.participantId, `${path}.participantId`, fail);
  integer(item.teamId, `${path}.teamId`, fail);
  // Null is the WHOLE point of the shape: a mind that trapped on a tick it
  // owned no body has nothing to name (§4.7).
  nullable(item.actorId, `${path}.actorId`, actorId, fail);
  nonEmpty(item.stage, `${path}.stage`, fail);
  semanticId(item.faultCode, `${path}.faultCode`, fail);
  int64(item.cumulativeFaultCount, `${path}.cumulativeFaultCount`, fail, true);
  boolean(
    item.disqualificationTriggered,
    `${path}.disqualificationTriggered`,
    fail,
  );
}

function mindCommand(value: unknown, path: string, fail: ReplayV3Fail): void {
  const commandValue = object(value, path, fail);
  const hasRoleTag = own(commandValue, 'roleTag');
  const item = exact(
    commandValue,
    path,
    [
      'unitId',
      'lifeId',
      'actionId',
      'actionCode',
      'arguments',
      'outcome',
      ...(hasRoleTag ? ['roleTag'] : []),
      'debugMessage',
    ],
    fail,
  );
  integer(item.unitId, `${path}.unitId`, fail);
  integer(item.lifeId, `${path}.lifeId`, fail);
  nonEmpty(item.actionId, `${path}.actionId`, fail);
  integer(item.actionCode, `${path}.actionCode`, fail);
  if (item.arguments !== null) {
    array(item.arguments, `${path}.arguments`, fail).forEach((entry, index) => {
      if (entry !== null) {
        rawArgument(entry, `${path}.arguments[${index}]`, fail);
      }
    });
  }
  if (item.outcome !== 'accepted' && item.outcome !== 'rejected') {
    fail(`${path}.outcome`, 'expected accepted or rejected');
  }
  if (hasRoleTag) roleTag(item.roleTag, `${path}.roleTag`, fail);
  nullable(item.debugMessage, `${path}.debugMessage`, string, fail);
}

function mindBodyResolution(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    ['unitId', 'lifeId', 'submittedDecision', 'actionResolution'],
    fail,
  );
  integer(item.unitId, `${path}.unitId`, fail);
  integer(item.lifeId, `${path}.lifeId`, fail);
  nullable(
    item.submittedDecision,
    `${path}.submittedDecision`,
    submittedDecision,
    fail,
  );
  actionResolution(item.actionResolution, `${path}.actionResolution`, fail);
}

function mindSlot(value: unknown, path: string, fail: ReplayV3Fail): void {
  const slotValue = object(value, path, fail);
  const hasClassId = own(slotValue, 'classId');
  const hasCandidates = own(slotValue, 'candidateClassIds');
  const hasSelected = own(slotValue, 'selectedClassId');
  const item = exact(
    slotValue,
    path,
    [
      'teamId',
      'unitId',
      'state',
      ...(hasClassId ? ['classId'] : []),
      ...(hasCandidates ? ['candidateClassIds'] : []),
      ...(hasSelected ? ['selectedClassId'] : []),
    ],
    fail,
  );
  integer(item.teamId, `${path}.teamId`, fail);
  integer(item.unitId, `${path}.unitId`, fail);
  unitSlotState(item.state, `${path}.state`, fail);
  if (hasClassId) semanticId(item.classId, `${path}.classId`, fail);
  // The chassis-at-activation block is RESERVED and v1 never writes it, so a
  // document that carries one was not written by a shipped engine (§10.1).
  if (hasCandidates || hasSelected) {
    fail(path, 'reserved chassis selection is never written by v1');
  }
}

function mindBody(value: unknown, path: string, fail: ReplayV3Fail): void {
  const bodyValue = object(value, path, fail);
  const hasRouteCooldowns = own(bodyValue, 'routeCooldowns');
  const hasCarriedScrap = own(bodyValue, 'carriedScrap');
  const hasRoleTag = own(bodyValue, 'roleTag');
  const item = exact(
    bodyValue,
    path,
    [
      'actorId',
      'generation',
      'formId',
      'position',
      'facing',
      'health',
      'cooldown',
      'energy',
      'previousActionResolution',
      'pendingSameLifeTransition',
      'classId',
      'previousPosition',
      'movedLastTick',
      'lifeStartedTick',
      'origin',
      'bodyRandomSeed',
      ...(hasRouteCooldowns ? ['routeCooldowns'] : []),
      ...(hasCarriedScrap ? ['carriedScrap'] : []),
      ...(hasRoleTag ? ['roleTag'] : []),
      'actionLegalities',
    ],
    fail,
  );
  actorId(item.actorId, `${path}.actorId`, fail);
  integer(item.generation, `${path}.generation`, fail);
  nonEmpty(item.formId, `${path}.formId`, fail);
  position(item.position, `${path}.position`, fail);
  direction(item.facing, `${path}.facing`, fail);
  integer(item.health, `${path}.health`, fail);
  integer(item.cooldown, `${path}.cooldown`, fail);
  nullable(item.energy, `${path}.energy`, integer, fail);
  nullable(
    item.previousActionResolution,
    `${path}.previousActionResolution`,
    actionResolution,
    fail,
  );
  pendingTransition(
    item.pendingSameLifeTransition,
    `${path}.pendingSameLifeTransition`,
    fail,
  );
  nullable(item.classId, `${path}.classId`, semanticId, fail);
  // Null is a fact — "this life's first tick" — not an omitted field.
  nullable(item.previousPosition, `${path}.previousPosition`, position, fail);
  boolean(item.movedLastTick, `${path}.movedLastTick`, fail);
  integer(item.lifeStartedTick, `${path}.lifeStartedTick`, fail);
  const origin = exact(
    item.origin,
    `${path}.origin`,
    [
      'reason',
      'generation',
      'parentActorId',
      'sourceTransitionId',
      'sourceOperationId',
    ],
    fail,
  );
  nonEmpty(origin.reason, `${path}.origin.reason`, fail);
  integer(origin.generation, `${path}.origin.generation`, fail);
  nullable(origin.parentActorId, `${path}.origin.parentActorId`, actorId, fail);
  for (const key of ['sourceTransitionId', 'sourceOperationId']) {
    nullable(origin[key], `${path}.origin.${key}`, string, fail);
  }
  // A uint64 over a decimal string, never widened to a float.
  uint64(item.bodyRandomSeed, `${path}.bodyRandomSeed`, fail);
  if (hasRouteCooldowns) {
    validateRouteCooldowns(item.routeCooldowns, `${path}.routeCooldowns`, fail);
  }
  if (hasCarriedScrap) {
    integer(item.carriedScrap, `${path}.carriedScrap`, fail);
    if ((item.carriedScrap as number) <= 0) {
      fail(`${path}.carriedScrap`, 'must be omitted when nothing is carried');
    }
  }
  if (hasRoleTag) roleTag(item.roleTag, `${path}.roleTag`, fail);
  array(item.actionLegalities, `${path}.actionLegalities`, fail).forEach(
    (entry, index) =>
      actionLegality(entry, `${path}.actionLegalities[${index}]`, fail),
  );
}

function mindObservation(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const item = exact(
    value,
    path,
    [
      'schemaVersion',
      'tick',
      'matchContractFingerprint',
      'participantId',
      'teamId',
      'bodies',
      'slots',
      'teamUnits',
      'participants',
      'allies',
      'enemies',
      'visibleTiles',
      'visibleProjectiles',
      'visibleEvents',
      'heardSounds',
      'scoreboard',
      'mode',
      'alliedIntents',
    ],
    fail,
  );
  integer(item.schemaVersion, `${path}.schemaVersion`, fail);
  integer(item.tick, `${path}.tick`, fail);
  nonEmpty(
    item.matchContractFingerprint,
    `${path}.matchContractFingerprint`,
    fail,
  );
  integer(item.participantId, `${path}.participantId`, fail);
  integer(item.teamId, `${path}.teamId`, fail);
  array(item.bodies, `${path}.bodies`, fail).forEach((entry, index) =>
    mindBody(entry, `${path}.bodies[${index}]`, fail),
  );
  array(item.slots, `${path}.slots`, fail).forEach((entry, index) =>
    mindSlot(entry, `${path}.slots[${index}]`, fail),
  );
  sharedObservationCollections(item, path, fail);
  // RESERVED (§11.3): the engine writes the empty collection so the field is
  // negotiated, and a non-empty one could not have come from a v1 host.
  const intents = array(item.alliedIntents, `${path}.alliedIntents`, fail);
  if (intents.length > 0) {
    fail(`${path}.alliedIntents`, 'allied intents are reserved and always empty');
  }
  intents.forEach((entry, index) =>
    mindAlliedIntent(entry, `${path}.alliedIntents[${index}]`, fail),
  );
}

function mindTurn(value: unknown, path: string, fail: ReplayV3Fail): void {
  const turnValue = object(value, path, fail);
  // The mind's own diagnostic text is omitted when inert, like every other
  // additive key in this format, so its presence is optional and its absence
  // is a real answer rather than a gap.
  const hasDebugMessage = own(turnValue, 'debugMessage');
  const turn = exact(
    turnValue,
    path,
    [
      'tick',
      'participantId',
      'teamId',
      'fuelBudget',
      'liveBodyCount',
      'observation',
      'commands',
      'resolutions',
      'intents',
      'runtimeFault',
      ...(hasDebugMessage ? ['debugMessage'] : []),
    ],
    fail,
  );
  integer(turn.tick, `${path}.tick`, fail);
  integer(turn.participantId, `${path}.participantId`, fail);
  integer(turn.teamId, `${path}.teamId`, fail);
  int64(turn.fuelBudget, `${path}.fuelBudget`, fail, true);
  integer(turn.liveBodyCount, `${path}.liveBodyCount`, fail);
  // The budget is a pure function of authoritative tick-start state, so the
  // mirror can decide it alone: 250M + 200M per live body (§4.2).
  const expectedFuel =
    BigInt(250_000_000) + BigInt(200_000_000) * BigInt(turn.liveBodyCount as number);
  if (BigInt(turn.fuelBudget as string) !== expectedFuel) {
    fail(`${path}.fuelBudget`, 'must be exactly 250M + 200M per live body');
  }
  mindObservation(turn.observation, `${path}.observation`, fail);
  array(turn.commands, `${path}.commands`, fail).forEach((entry, index) =>
    mindCommand(entry, `${path}.commands[${index}]`, fail),
  );
  const resolutions = array(turn.resolutions, `${path}.resolutions`, fail);
  resolutions.forEach((entry, index) =>
    mindBodyResolution(entry, `${path}.resolutions[${index}]`, fail),
  );
  if (resolutions.length !== (turn.liveBodyCount as number)) {
    fail(`${path}.resolutions`, 'must cover exactly the live body count');
  }
  array(turn.intents, `${path}.intents`, fail).forEach((entry, index) =>
    mindIntent(entry, `${path}.intents[${index}]`, fail),
  );
  nullable(turn.runtimeFault, `${path}.runtimeFault`, mindRuntimeFault, fail);
  if (hasDebugMessage) {
    string(turn.debugMessage, `${path}.debugMessage`, fail);
    // A faulted turn's reply never parsed, so it cannot have carried text.
    // This one the mirror can decide alone, which is where the division of
    // labour puts it.
    if (turn.runtimeFault !== null) {
      fail(`${path}.debugMessage`, 'a faulted mind turn carries no diagnostic');
    }
  }
}

function eventAudience(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  if (base.kind === 'public') {
    exact(base, path, ['kind'], fail);
    return;
  }
  if (base.kind === 'spatial') {
    const item = exact(base, path, ['kind', 'primaryPosition'], fail);
    position(item.primaryPosition, `${path}.primaryPosition`, fail);
    return;
  }
  if (base.kind === 'team-private') {
    const item = exact(base, path, ['kind', 'teamId'], fail);
    integer(item.teamId, `${path}.teamId`, fail);
    return;
  }
  fail(`${path}.kind`, `unknown event audience ${String(base.kind)}`);
}

function authoritativeEvent(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const event = exact(
    value,
    path,
    [
      'eventHandle',
      'tick',
      'globalOrdinal',
      'sourceOrdinal',
      'kind',
      'payload',
      'audience',
    ],
    fail,
  );
  nonEmpty(event.eventHandle, `${path}.eventHandle`, fail);
  integer(event.tick, `${path}.tick`, fail);
  int64(event.globalOrdinal, `${path}.globalOrdinal`, fail, true);
  integer(event.sourceOrdinal, `${path}.sourceOrdinal`, fail);
  nonEmpty(event.kind, `${path}.kind`, fail);
  eventPayload(event.payload, `${path}.payload`, fail);
  validateEventKindAndPayload(event.kind, event.payload, path, fail);
  eventAudience(event.audience, `${path}.audience`, fail);
}

function traversalTerminal(
  value: unknown,
  path: string,
  fail: ReplayV3Fail,
): void {
  const base = object(value, path, fail);
  string(base.kind, `${path}.kind`, fail);
  if (
    base.kind === 'retained' ||
    base.kind === 'wall-or-path-exhausted' ||
    base.kind === 'range-exhausted'
  ) {
    exact(base, path, ['kind'], fail);
    return;
  }
  if (base.kind === 'actor-contact' || base.kind === 'movement-contact') {
    const item = exact(base, path, ['kind', 'targetActorId', 'appliedDamage'], fail);
    actorId(item.targetActorId, `${path}.targetActorId`, fail);
    boolean(item.appliedDamage, `${path}.appliedDamage`, fail);
    return;
  }
  if (base.kind === 'lifecycle-placement-purge') {
    const item = exact(base, path, ['kind', 'position'], fail);
    position(item.position, `${path}.position`, fail);
    return;
  }
  if (base.kind === 'participant-disqualification') {
    const item = exact(base, path, ['kind', 'participantId'], fail);
    integer(item.participantId, `${path}.participantId`, fail);
    return;
  }
  fail(`${path}.kind`, `unknown traversal terminal ${String(base.kind)}`);
}

function traversal(value: unknown, path: string, fail: ReplayV3Fail): void {
  const item = exact(
    value,
    path,
    [
      'tick',
      'globalOrdinal',
      'phase',
      'trigger',
      'projectileId',
      'ownerParticipantId',
      'ownerTeamId',
      'ownerActorId',
      'attackProfileId',
      'from',
      'path',
      'launchHeading',
      'finalHeading',
      'shotProgram',
      'terminal',
    ],
    fail,
  );
  integer(item.tick, `${path}.tick`, fail);
  int64(item.globalOrdinal, `${path}.globalOrdinal`, fail, true);
  nonEmpty(item.phase, `${path}.phase`, fail);
  nonEmpty(item.trigger, `${path}.trigger`, fail);
  int64(item.projectileId, `${path}.projectileId`, fail, true);
  integer(item.ownerParticipantId, `${path}.ownerParticipantId`, fail);
  integer(item.ownerTeamId, `${path}.ownerTeamId`, fail);
  actorId(item.ownerActorId, `${path}.ownerActorId`, fail);
  nonEmpty(item.attackProfileId, `${path}.attackProfileId`, fail);
  position(item.from, `${path}.from`, fail);
  array(item.path, `${path}.path`, fail).forEach((entry, index) =>
    position(entry, `${path}.path[${index}]`, fail),
  );
  heading(item.launchHeading, `${path}.launchHeading`, fail);
  heading(item.finalHeading, `${path}.finalHeading`, fail);
  nullable(item.shotProgram, `${path}.shotProgram`, shotProgram, fail);
  traversalTerminal(item.terminal, `${path}.terminal`, fail);
}

function validateResult(value: unknown, path: string, fail: ReplayV3Fail): void {
  const result = exact(
    value,
    path,
    [
      'completionReason',
      'endTick',
      'standings',
      'eligibleTeamIds',
      'units',
      'mode',
    ],
    fail,
  );
  nonEmpty(result.completionReason, `${path}.completionReason`, fail);
  nullable(result.endTick, `${path}.endTick`, integer, fail);
  const standings = exact(
    result.standings,
    `${path}.standings`,
    ['winnerTeamId', 'teams'],
    fail,
  );
  nullable(standings.winnerTeamId, `${path}.standings.winnerTeamId`, integer, fail);
  array(standings.teams, `${path}.standings.teams`, fail).forEach(
    (entry, index) => {
      const team = exact(
        entry,
        `${path}.standings.teams[${index}]`,
        ['teamId', 'rank', 'outcome', 'scores'],
        fail,
      );
      integer(team.teamId, `${path}.standings.teams[${index}].teamId`, fail);
      integer(team.rank, `${path}.standings.teams[${index}].rank`, fail);
      if (!['win', 'loss', 'draw'].includes(String(team.outcome))) {
        fail(
          `${path}.standings.teams[${index}].outcome`,
          'expected win, loss, or draw',
        );
      }
      const board = { teams: [{ teamId: team.teamId, eligible: true, scores: team.scores }] };
      scoreboard(board, `${path}.standings.teams[${index}].scoreboard`, fail);
    },
  );
  array(result.eligibleTeamIds, `${path}.eligibleTeamIds`, fail).forEach(
    (teamId, index) =>
      integer(teamId, `${path}.eligibleTeamIds[${index}]`, fail),
  );
  array(result.units, `${path}.units`, fail).forEach((entry, index) => {
    const unit = exact(
      entry,
      `${path}.units[${index}]`,
      ['slot', 'activeLife'],
      fail,
    );
    slotState(unit.slot, `${path}.units[${index}].slot`, fail);
    nullable(unit.activeLife, `${path}.units[${index}].activeLife`, lifeState, fail);
  });
  const mode = object(result.mode, `${path}.mode`, fail);
  if (mode.kind === 'deathmatch') {
    const deathmatch = exact(
      mode,
      `${path}.mode`,
      ['kind', 'reason', 'scores'],
      fail,
    );
    if (
      deathmatch.reason !== 'fault-eligibility' &&
      deathmatch.reason !== 'kill-limit' &&
      deathmatch.reason !== 'max-ticks'
    ) {
      fail(`${path}.mode.reason`, 'unknown deathmatch end reason');
    }
    array(deathmatch.scores, `${path}.mode.scores`, fail).forEach(
      (entry, index) => {
        const score = exact(
          entry,
          `${path}.mode.scores[${index}]`,
          ['teamId', 'kills', 'deaths', 'damageDealt'],
          fail,
        );
        integer(score.teamId, `${path}.mode.scores[${index}].teamId`, fail);
        for (const key of ['kills', 'deaths', 'damageDealt']) {
          int64(
            score[key],
            `${path}.mode.scores[${index}].${key}`,
            fail,
            true,
          );
        }
      },
    );
    return;
  }
  if (mode.kind === 'frontline') {
    const frontline = exact(
      mode,
      `${path}.mode`,
      ['kind', 'reason', 'control', 'scores'],
      fail,
    );
    if (
      frontline.reason !== 'fault-eligibility' &&
      frontline.reason !== 'base-breach' &&
      frontline.reason !== 'max-ticks'
    ) {
      fail(`${path}.mode.reason`, 'unknown frontline end reason');
    }
    modeState(frontline.control, `${path}.mode.control`, fail);
    const control = object(
      frontline.control,
      `${path}.mode.control`,
      fail,
    );
    if (control.kind !== 'frontline') {
      fail(
        `${path}.mode.control.kind`,
        'frontline result requires frontline control',
      );
    }
    array(frontline.scores, `${path}.mode.scores`, fail).forEach(
      (entry, index) => {
        const score = exact(
          entry,
          `${path}.mode.scores[${index}]`,
          ['teamId', 'territorialProgress'],
          fail,
        );
        integer(score.teamId, `${path}.mode.scores[${index}].teamId`, fail);
        int64(
          score.territorialProgress,
          `${path}.mode.scores[${index}].territorialProgress`,
          fail,
        );
      },
    );
    return;
  }
  if (mode.kind === 'arc-relay') {
    const arc = exact(
      mode,
      `${path}.mode`,
      ['kind', 'reason', 'state'],
      fail,
    );
    if (
      arc.reason !== 'fault-eligibility' &&
      arc.reason !== 'reactor-destroyed' &&
      arc.reason !== 'max-ticks'
    ) {
      fail(`${path}.mode.reason`, 'unknown Arc Relay end reason');
    }
    modeState(arc.state, `${path}.mode.state`, fail);
    const state = object(arc.state, `${path}.mode.state`, fail);
    if (state.kind !== 'arc-relay') {
      fail(
        `${path}.mode.state.kind`,
        'Arc Relay result requires Arc Relay state',
      );
    }
    return;
  }
  fail(`${path}.mode.kind`, `unknown mode result ${String(mode.kind)}`);
}

/**
 * Validates the closed replay-v3 envelope and all replay-owned DTOs before
 * relationship checks. The embedded contract's extensible policy objects are
 * JSON-validated but retained verbatim.
 */
export function validateReplayV3(
  input: unknown,
  fail: ReplayV3Fail,
): asserts input is V3.ReplayV3Document {
  const root = exact(
    input,
    'replay',
    ['header', 'initialFrame', 'ticks', 'result', 'replayHash', 'partial'],
    fail,
  );
  const header = exact(
    root.header,
    'replay.header',
    [
      'replayVersion',
      'engineVersion',
      'gameRulesVersion',
      'runtime',
      'seed',
      'contract',
      'presentation',
      'provenance',
    ],
    fail,
  );
  if (header.replayVersion !== 3) {
    fail('replay.header.replayVersion', 'expected replay version 3');
  }
  nonEmpty(header.engineVersion, 'replay.header.engineVersion', fail);
  nonEmpty(header.gameRulesVersion, 'replay.header.gameRulesVersion', fail);
  const runtime = exact(
    header.runtime,
    'replay.header.runtime',
    [
      'contractProfileId',
      'protocolVersion',
      'configurationVersion',
      'runtimeContractVersion',
      'matchStartSchemaVersion',
      'observationSchemaVersion',
      'decisionSchemaVersion',
      'matchContractSchemaVersion',
    ],
    fail,
  );
  for (const key of ['contractProfileId', 'protocolVersion', 'configurationVersion']) {
    nonEmpty(runtime[key], `replay.header.runtime.${key}`, fail);
  }
  for (const key of [
    'runtimeContractVersion',
    'matchStartSchemaVersion',
    'observationSchemaVersion',
    'decisionSchemaVersion',
    'matchContractSchemaVersion',
  ]) {
    integer(runtime[key], `replay.header.runtime.${key}`, fail);
  }
  uint64(header.seed, 'replay.header.seed', fail);
  validateContract(header.contract, 'replay.header.contract', fail);
  if (header.presentation !== null) {
    const presentation = exact(
      header.presentation,
      'replay.header.presentation',
      ['themeId', 'map', 'forms'],
      fail,
    );
    nullable(presentation.themeId, 'replay.header.presentation.themeId', string, fail);
    if (presentation.map !== null) {
      const map = exact(
        presentation.map,
        'replay.header.presentation.map',
        ['boundaryWall', 'interiorWall', 'wallGroups'],
        fail,
      );
      nonEmpty(map.boundaryWall, 'replay.header.presentation.map.boundaryWall', fail);
      nonEmpty(map.interiorWall, 'replay.header.presentation.map.interiorWall', fail);
      array(map.wallGroups, 'replay.header.presentation.map.wallGroups', fail).forEach(
        (entry, index) => {
          const group = exact(
            entry,
            `replay.header.presentation.map.wallGroups[${index}]`,
            ['family', 'tiles'],
            fail,
          );
          nonEmpty(
            group.family,
            `replay.header.presentation.map.wallGroups[${index}].family`,
            fail,
          );
          array(
            group.tiles,
            `replay.header.presentation.map.wallGroups[${index}].tiles`,
            fail,
          ).forEach((tile, tileIndex) =>
            position(
              tile,
              `replay.header.presentation.map.wallGroups[${index}].tiles[${tileIndex}]`,
              fail,
            ),
          );
        },
      );
    }
    array(presentation.forms, 'replay.header.presentation.forms', fail).forEach(
      (entry, index) => {
        const form = exact(
          entry,
          `replay.header.presentation.forms[${index}]`,
          ['formId', 'lookId', 'projectileLookId'],
          fail,
        );
        nonEmpty(form.formId, `replay.header.presentation.forms[${index}].formId`, fail);
        nullable(
          form.lookId,
          `replay.header.presentation.forms[${index}].lookId`,
          string,
          fail,
        );
        nullable(
          form.projectileLookId,
          `replay.header.presentation.forms[${index}].projectileLookId`,
          string,
          fail,
        );
      },
    );
  }
  if (header.provenance !== null) {
    const provenance = exact(
      header.provenance,
      'replay.header.provenance',
      ['participants'],
      fail,
    );
    array(provenance.participants, 'replay.header.provenance.participants', fail).forEach(
      (entry, index) => {
        const participant = exact(
          entry,
          `replay.header.provenance.participants[${index}]`,
          [
            'participantId',
            'teamId',
            'name',
            'runtimeKind',
            'artifactHash',
            'accent',
            'lookId',
            'projectileLookId',
          ],
          fail,
        );
        integer(
          participant.participantId,
          `replay.header.provenance.participants[${index}].participantId`,
          fail,
        );
        integer(
          participant.teamId,
          `replay.header.provenance.participants[${index}].teamId`,
          fail,
        );
        for (const key of ['name', 'runtimeKind', 'accent']) {
          string(
            participant[key],
            `replay.header.provenance.participants[${index}].${key}`,
            fail,
          );
        }
        for (const key of ['artifactHash', 'lookId', 'projectileLookId']) {
          nullable(
            participant[key],
            `replay.header.provenance.participants[${index}].${key}`,
            string,
            fail,
          );
        }
      },
    );
  }

  const initial = exact(
    root.initialFrame,
    'replay.initialFrame',
    ['state', 'lifeStarts', 'events'],
    fail,
  );
  worldState(initial.state, 'replay.initialFrame.state', fail);
  array(initial.lifeStarts, 'replay.initialFrame.lifeStarts', fail).forEach(
    (entry, index) => lifeStart(entry, `replay.initialFrame.lifeStarts[${index}]`, fail),
  );
  array(initial.events, 'replay.initialFrame.events', fail).forEach(
    (entry, index) =>
      authoritativeEvent(entry, `replay.initialFrame.events[${index}]`, fail),
  );

  // THE TURN-KIND DISCRIMINATOR (§5.1). The header's contract profile decides
  // which turn record a tick carries, and a document carries exactly one —
  // never both, and never the other profile's.
  const mindProfile = runtime.contractProfileId === MIND_CONTRACT_PROFILE_ID;
  array(root.ticks, 'replay.ticks', fail).forEach((entry, index) => {
    const tick = exact(
      entry,
      `replay.ticks[${index}]`,
      [
        'tick',
        'tickStart',
        mindProfile ? 'mindTurns' : 'actorTurns',
        'events',
        'traversals',
        'postState',
      ],
      fail,
    );
    integer(tick.tick, `replay.ticks[${index}].tick`, fail);
    const start = exact(
      tick.tickStart,
      `replay.ticks[${index}].tickStart`,
      ['tick', 'state', 'activeActorIds', 'lifeStarts', 'events', 'traversals'],
      fail,
    );
    integer(start.tick, `replay.ticks[${index}].tickStart.tick`, fail);
    worldState(start.state, `replay.ticks[${index}].tickStart.state`, fail);
    array(start.activeActorIds, `replay.ticks[${index}].tickStart.activeActorIds`, fail).forEach(
      (actor, actorIndex) =>
        actorId(
          actor,
          `replay.ticks[${index}].tickStart.activeActorIds[${actorIndex}]`,
          fail,
        ),
    );
    array(start.lifeStarts, `replay.ticks[${index}].tickStart.lifeStarts`, fail).forEach(
      (life, lifeIndex) =>
        lifeStart(
          life,
          `replay.ticks[${index}].tickStart.lifeStarts[${lifeIndex}]`,
          fail,
        ),
    );
    array(start.events, `replay.ticks[${index}].tickStart.events`, fail).forEach(
      (event, eventIndex) =>
        authoritativeEvent(
          event,
          `replay.ticks[${index}].tickStart.events[${eventIndex}]`,
          fail,
        ),
    );
    array(start.traversals, `replay.ticks[${index}].tickStart.traversals`, fail).forEach(
      (item, traversalIndex) =>
        traversal(
          item,
          `replay.ticks[${index}].tickStart.traversals[${traversalIndex}]`,
          fail,
        ),
    );
    if (mindProfile) {
      array(tick.mindTurns, `replay.ticks[${index}].mindTurns`, fail).forEach(
        (turn, turnIndex) =>
          mindTurn(
            turn,
            `replay.ticks[${index}].mindTurns[${turnIndex}]`,
            fail,
          ),
      );
    } else {
      array(tick.actorTurns, `replay.ticks[${index}].actorTurns`, fail).forEach(
        (turn, turnIndex) =>
          actorTurn(
            turn,
            `replay.ticks[${index}].actorTurns[${turnIndex}]`,
            fail,
          ),
      );
    }
    array(tick.events, `replay.ticks[${index}].events`, fail).forEach(
      (event, eventIndex) =>
        authoritativeEvent(event, `replay.ticks[${index}].events[${eventIndex}]`, fail),
    );
    array(tick.traversals, `replay.ticks[${index}].traversals`, fail).forEach(
      (item, traversalIndex) =>
        traversal(
          item,
          `replay.ticks[${index}].traversals[${traversalIndex}]`,
          fail,
        ),
    );
    worldState(tick.postState, `replay.ticks[${index}].postState`, fail);
  });

  nullable(root.result, 'replay.result', validateResult, fail);
  if (root.replayHash !== null) {
    if (typeof root.replayHash !== 'string' || !/^[0-9a-f]{64}$/.test(root.replayHash)) {
      fail('replay.replayHash', 'expected lowercase SHA-256 hex or null');
    }
  }
  boolean(root.partial, 'replay.partial', fail);

  validateV3Relationships(input as V3.ReplayV3Document, fail);
}

function actorValue(actor: V3.ReplayV3ActorId): string {
  return `${actor.teamId}:${actor.unitId}:${actor.lifeId}`;
}

function unitValue(unit: { teamId: number; unitId: number }): string {
  return `${unit.teamId}:${unit.unitId}`;
}

function sorted(values: Iterable<string>): string[] {
  return [...values].sort((left, right) => left.localeCompare(right));
}

function sameSet(left: Iterable<string>, right: Iterable<string>): boolean {
  return JSON.stringify(sorted(left)) === JSON.stringify(sorted(right));
}

function jsonEqual(left: unknown, right: unknown): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function observedModeMatchesWorld(
  observed: V3.ReplayV3ModeState,
  world: V3.ReplayV3ModeState,
): boolean {
  if (observed.kind !== 'arc-relay' || world.kind !== 'arc-relay') {
    return jsonEqual(observed, world);
  }
  if (
    observed.modeId !== world.modeId ||
    !jsonEqual(observed.wells, world.wells) ||
    !jsonEqual(observed.reactors, world.reactors) ||
    observed.latestPulseTeamId !== world.latestPulseTeamId ||
    observed.latestPulseTick !== world.latestPulseTick
  ) {
    return false;
  }
  const cores = new Map(
    world.visibleCores.map((core) => [JSON.stringify(core.coreId), core]),
  );
  const signatures = new Map(
    world.visibleSignatures.map((signature) => [
      signature.operationId,
      signature,
    ]),
  );
  return (
    observed.visibleCores.every((core) =>
      jsonEqual(cores.get(JSON.stringify(core.coreId)), core),
    ) &&
    observed.visibleSignatures.every((signature) =>
      jsonEqual(signatures.get(signature.operationId), signature),
    )
  );
}

function scoreboardsStableAcrossTickStart(
  before: V3.ReplayV3Scoreboard,
  after: V3.ReplayV3Scoreboard,
): boolean {
  if (before.teams.length !== after.teams.length) return false;
  return before.teams.every((beforeTeam, teamIndex) => {
    const afterTeam = after.teams[teamIndex];
    if (
      !afterTeam ||
      beforeTeam.teamId !== afterTeam.teamId ||
      beforeTeam.eligible !== afterTeam.eligible ||
      beforeTeam.scores.length !== afterTeam.scores.length
    ) {
      return false;
    }
    return beforeTeam.scores.every((beforeScore, scoreIndex) => {
      const afterScore = afterTeam.scores[scoreIndex];
      return (
        afterScore !== undefined &&
        beforeScore.channel === afterScore.channel &&
        (beforeScore.channel === 'active-health' ||
          beforeScore.value === afterScore.value)
      );
    });
  });
}

/**
 * Tick start applies exact-due lifecycle work before observations are frozen.
 * Its authoritative state can therefore contain declared unlocks, returns,
 * fabrication/Split completions, same-life form work, and placement purges.
 */
export function validateReplayV3TickStartBoundary(
  before: V3.ReplayV3WorldState,
  tickStart: V3.ReplayV3TickStart,
  path: string,
  fail: ReplayV3Fail,
): void {
  const after = tickStart.state;
  if (jsonEqual(before, after)) return;

  const arcModeChange =
    before.mode.kind === 'arc-relay' &&
    after.mode.kind === 'arc-relay' &&
    tickStart.events.some((event) =>
      event.payload.kind === 'arc-relay' ||
      event.payload.kind === 'mode-changed',
    );

  if (
    before.matchContractFingerprint !== after.matchContractFingerprint ||
    before.nextTick !== tickStart.tick ||
    after.nextTick !== tickStart.tick ||
    before.nextProjectileId !== after.nextProjectileId ||
    !jsonEqual(before.participants, after.participants) ||
    (!jsonEqual(before.mode, after.mode) && !arcModeChange) ||
    !scoreboardsStableAcrossTickStart(before.scoreboard, after.scoreboard)
  ) {
    fail(
      path,
      'tick-start lifecycle cannot change participants, mode, projectile issuance, eligibility, or non-derived scores without exact Arc mode evidence',
    );
  }

  const beforeLives = new Map(
    before.activeLives.map((life) => [actorValue(life.actorId), life]),
  );
  const afterLives = new Map(
    after.activeLives.map((life) => [actorValue(life.actorId), life]),
  );
  const starts = new Map(
    tickStart.lifeStarts.map((start) => [actorValue(start.actorId), start]),
  );
  const spawnEvents = tickStart.events.filter(
    (event) =>
      event.kind === 'life-spawned' &&
      event.payload.kind === 'life-spawned',
  );
  const spawnEventsByActor = new Map(
    spawnEvents.map((event) => [
      actorValue(
        (event.payload as Extract<
          V3.ReplayV3EventPayload,
          { kind: 'life-spawned' }
        >).actorId,
      ),
      event,
    ]),
  );
  const addedActors = [...afterLives.keys()].filter(
    (actor) => !beforeLives.has(actor),
  );
  if (
    !sameSet(addedActors, starts.keys()) ||
    !sameSet(addedActors, spawnEventsByActor.keys()) ||
    starts.size !== tickStart.lifeStarts.length ||
    spawnEventsByActor.size !== spawnEvents.length
  ) {
    fail(
      `${path}.activeLives`,
      'every tick-start life addition must have exactly one life start and LifeSpawned event',
    );
  }
  for (const actor of addedActors) {
    const life = afterLives.get(actor)!;
    const start = starts.get(actor)!;
    const event = spawnEventsByActor.get(actor)!;
    const spawned = event.payload as Extract<
      V3.ReplayV3EventPayload,
      { kind: 'life-spawned' }
    >;
    if (
      life.participantId !== start.participantId ||
      life.generation !== start.origin.generation ||
      spawned.participantId !== start.participantId ||
      !jsonEqual(spawned.parentActorId, start.origin.parentActorId) ||
      spawned.generation !== start.origin.generation ||
      spawned.formId !== life.formId ||
      spawned.health !== life.health ||
      !jsonEqual(spawned.position, life.position) ||
      spawned.reason !== start.origin.reason ||
      spawned.sourceTransitionId !== start.origin.sourceTransitionId ||
      spawned.sourceOperationId !== start.origin.sourceOperationId
    ) {
      fail(
        `${path}.activeLives`,
        `life ${actor} does not match its tick-start life start and spawn event`,
      );
    }
  }

  const removalEvents = tickStart.events.filter(
    (event) =>
      (event.kind === 'destruction' &&
        event.payload.kind === 'destruction') ||
      (event.kind === 'life-retired' &&
        event.payload.kind === 'life-retired'),
  );
  const removedActors = [...beforeLives.keys()].filter(
    (actor) => !afterLives.has(actor),
  );
  const evidencedRemovals = removalEvents.map((event) =>
    actorValue(
      (
        event.payload as Extract<
          V3.ReplayV3EventPayload,
          { kind: 'destruction' | 'life-retired' }
        >
      ).actorId,
    ),
  );
  if (
    !sameSet(removedActors, evidencedRemovals) ||
    new Set(evidencedRemovals).size !== evidencedRemovals.length
  ) {
    fail(
      `${path}.activeLives`,
      'every tick-start life removal must have exactly one Destruction or LifeRetired event',
    );
  }

  for (const [actor, beforeLife] of beforeLives) {
    const afterLife = afterLives.get(actor);
    if (!afterLife || jsonEqual(beforeLife, afterLife)) continue;
    const formEvents = tickStart.events.filter(
      (event) =>
        event.payload.kind === 'form-transition' &&
        actorValue(event.payload.actorId) === actor,
    );
    const hasArcLifeEvidence = tickStart.events.some((event) =>
      event.payload.kind === 'arc-relay' &&
      ((event.payload.fact.kind === 'body-relocated' &&
        actorValue(event.payload.fact.targetActorId) === actor) ||
        ((event.payload.fact.kind === 'signature-damage' ||
          event.payload.fact.kind === 'signature-repair') &&
          actorValue(event.payload.fact.targetActorId) === actor)),
    );
    if (formEvents.length !== 1 && !hasArcLifeEvidence) {
      fail(
        `${path}.activeLives`,
        `surviving life ${actor} changed without exactly one form-transition event`,
      );
    }
  }

  const afterSlots = new Map(after.slots.map((slot) => [unitValue(slot), slot]));
  for (const beforeSlot of before.slots) {
    const key = unitValue(beforeSlot);
    const afterSlot = afterSlots.get(key);
    if (!afterSlot) {
      fail(`${path}.slots`, `stable unit slot ${key} disappeared at tick start`);
    }
    if (jsonEqual(beforeSlot, afterSlot)) continue;

    const sameStableFields =
      afterSlot.teamId === beforeSlot.teamId &&
      afterSlot.unitId === beforeSlot.unitId &&
      afterSlot.participantId === beforeSlot.participantId;
    if (
      sameStableFields &&
      beforeSlot.state.kind === 'availability-pending' &&
      beforeSlot.state.dueTick === tickStart.tick &&
      afterSlot.nextLifeId === beforeSlot.nextLifeId &&
      afterSlot.state.kind === 'ready' &&
      afterSlot.pendingParentActorId === null &&
      afterSlot.splitReservation === null
    ) {
      continue;
    }

    if (
      sameStableFields &&
      beforeSlot.state.kind === 'availability-pending' &&
      beforeSlot.state.dueTick === tickStart.tick &&
      afterSlot.nextLifeId === beforeSlot.nextLifeId + 1 &&
      afterSlot.state.kind === 'active' &&
      afterSlot.pendingParentActorId === null &&
      afterSlot.splitReservation === null
    ) {
      // Declared automatic activation: a dormant slot's first life spawns
      // at its exact unlock tick (the auto-companions and class arms)
      // instead of merely becoming ready for explicit fabrication.
      const actor = actorValue(afterSlot.state.actorId);
      const start = starts.get(actor);
      if (
        afterSlot.state.actorId.teamId === beforeSlot.teamId &&
        afterSlot.state.actorId.unitId === beforeSlot.unitId &&
        afterSlot.state.actorId.lifeId === beforeSlot.nextLifeId &&
        (start?.origin.reason === 'automatic-activation' ||
          // THE ROOT FACTORY (DECISIONS #194): a slot whose profile declares
          // a bootstrap consumes its own due availability clock into a live
          // body at its home spawn, because for a participant holding nothing
          // an idle Ready slot is a slot nothing can ever fill.
          start?.origin.reason === 'root-factory-seed')
      ) {
        continue;
      }
    }

    if (
      sameStableFields &&
      (beforeSlot.state.kind === 'automatic-return-pending' ||
        beforeSlot.state.kind === 'fabrication-pending' ||
        beforeSlot.state.kind === 'replication-pending') &&
      beforeSlot.state.dueTick === tickStart.tick &&
      afterSlot.nextLifeId === beforeSlot.nextLifeId + 1 &&
      afterSlot.state.kind === 'active'
    ) {
      const actor = actorValue(afterSlot.state.actorId);
      const start = starts.get(actor);
      const expectedReason =
        beforeSlot.state.kind === 'automatic-return-pending'
          ? 'automatic-return'
          : beforeSlot.state.kind === 'fabrication-pending'
            ? 'fabrication'
            : 'replication';
      if (
        afterSlot.state.actorId.teamId === beforeSlot.teamId &&
        afterSlot.state.actorId.unitId === beforeSlot.unitId &&
        afterSlot.state.actorId.lifeId === beforeSlot.nextLifeId &&
        start?.origin.reason === expectedReason
      ) {
        continue;
      }
    }

    const beforeActor =
      beforeSlot.state.kind === 'active'
        ? actorValue(beforeSlot.state.actorId)
        : null;
    const afterActor =
      afterSlot.state.kind === 'active'
        ? actorValue(afterSlot.state.actorId)
        : null;
    const hasSameLifeEvidence =
      beforeActor !== null &&
      beforeActor === afterActor &&
      tickStart.events.some(
        (event) =>
          event.payload.kind === 'form-transition' &&
          actorValue(event.payload.actorId) === beforeActor,
      );
    const hasReplicationEvidence =
      tickStart.events.some(
        (event) =>
          (event.payload.kind === 'lifecycle' &&
            ((event.payload.targetTeamId === beforeSlot.teamId &&
              event.payload.targetUnitId === beforeSlot.unitId) ||
              actorValue(event.payload.sourceActorId) === beforeActor)) ||
          (event.payload.kind === 'life-retired' &&
            actorValue(event.payload.actorId) === beforeActor),
      ) &&
      (afterActor === null || starts.has(afterActor));
    if (!sameStableFields || (!hasSameLifeEvidence && !hasReplicationEvidence)) {
      fail(
        `${path}.slots`,
        `slot ${key} changed without exact-due lifecycle evidence`,
      );
    }
  }

  if (after.slots.length !== before.slots.length) {
    fail(`${path}.slots`, 'tick start cannot add or remove stable unit slots');
  }

  const beforeProjectiles = new Map(
    before.projectiles.map((projectile) => [
      projectile.projectileId,
      projectile,
    ]),
  );
  const afterProjectiles = new Map(
    after.projectiles.map((projectile) => [
      projectile.projectileId,
      projectile,
    ]),
  );
  for (const [projectileId, projectile] of afterProjectiles) {
    if (!jsonEqual(projectile, beforeProjectiles.get(projectileId))) {
      fail(
        `${path}.projectiles`,
        `tick start cannot create or mutate projectile ${projectileId}`,
      );
    }
  }
  const removedProjectiles = [...beforeProjectiles.keys()].filter(
    (projectileId) => !afterProjectiles.has(projectileId),
  );
  const purgedProjectiles = tickStart.traversals
    .filter(
      (traversal) =>
        traversal.terminal.kind === 'lifecycle-placement-purge',
    )
    .map((traversal) => traversal.projectileId);
  if (
    !sameSet(removedProjectiles, purgedProjectiles) ||
    new Set(purgedProjectiles).size !== purgedProjectiles.length
  ) {
    fail(
      `${path}.projectiles`,
      'every tick-start projectile removal must have exactly one lifecycle-placement purge traversal',
    );
  }

  if (
    !jsonEqual(before.pendingReplications, after.pendingReplications) &&
    !tickStart.events.some(
      (event) =>
        event.kind === 'lifecycle-completed' ||
        event.kind === 'lifecycle-cancelled' ||
        event.kind === 'life-retired',
    )
  ) {
    fail(
      `${path}.pendingReplications`,
      'pending replication state changed without resolution evidence',
    );
  }
}

function ensureUnique<T>(
  entries: readonly T[],
  key: (entry: T) => string | number,
  path: string,
  fail: ReplayV3Fail,
): void {
  const seen = new Set<string | number>();
  for (const entry of entries) {
    const value = key(entry);
    if (seen.has(value)) fail(path, `duplicate identity ${String(value)}`);
    seen.add(value);
  }
}

/**
 * THE MIND SPECIALIZATION. One mind turn becomes one per-body turn for every
 * own live body, which is what lets the whole viewer — fog, per-unit facts,
 * the bot panel, both renderers — stay exactly as it is on a mind replay
 * (docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md §5.3).
 *
 * It is deliberately the same projection the Guest's migration adapter
 * performs host-side: `self` from the matching body, `allies` from the OTHER
 * own bodies plus any allied mind's bodies, and every team-shared collection
 * passed through untouched. Because the mind observation reuses the per-life
 * shapes for every nested type, this moves references rather than rebuilding
 * values.
 */
export function specializeMindTurn(
  turn: V3.ReplayV3MindTurn,
): V3.ReplayV3ActorTurn[] {
  const bodies = new Map(
    turn.observation.bodies.map((body) => [
      `${body.actorId.unitId}:${body.actorId.lifeId}`,
      body,
    ]),
  );
  return turn.resolutions.flatMap((resolution) => {
    const body = bodies.get(`${resolution.unitId}:${resolution.lifeId}`);
    if (!body) return [];
    const allies: V3.ReplayV3ObservedAlly[] = [
      ...turn.observation.bodies
        .filter((other) => other !== body)
        .map(mindBodyAsAlly),
      ...turn.observation.allies,
    ];
    return [
      {
        tick: turn.tick,
        participantId: turn.participantId,
        actorId: body.actorId,
        observation: {
          schemaVersion: turn.observation.schemaVersion,
          tick: turn.observation.tick,
          matchContractFingerprint:
            turn.observation.matchContractFingerprint,
          self: mindBodyAsAlly(body),
          teamUnits: turn.observation.teamUnits,
          participants: turn.observation.participants,
          allies,
          enemies: turn.observation.enemies,
          visibleTiles: turn.observation.visibleTiles,
          visibleProjectiles: turn.observation.visibleProjectiles,
          visibleEvents: turn.observation.visibleEvents,
          heardSounds: turn.observation.heardSounds,
          scoreboard: turn.observation.scoreboard,
          mode: turn.observation.mode,
          actionLegalities: body.actionLegalities,
        },
        submittedDecision: resolution.submittedDecision,
        actionResolution: resolution.actionResolution,
      },
    ];
  });
}

function mindBodyAsAlly(body: V3.ReplayV3MindBody): V3.ReplayV3ObservedAlly {
  return {
    actorId: body.actorId,
    generation: body.generation,
    formId: body.formId,
    position: body.position,
    facing: body.facing,
    health: body.health,
    cooldown: body.cooldown,
    energy: body.energy,
    previousActionResolution: body.previousActionResolution,
    pendingSameLifeTransition: body.pendingSameLifeTransition,
    classId: body.classId,
    ...(body.routeCooldowns ? { routeCooldowns: body.routeCooldowns } : {}),
    ...(body.carriedScrap ? { carriedScrap: body.carriedScrap } : {}),
    ...(body.roleTag ? { roleTag: body.roleTag } : {}),
  };
}

/**
 * The mind-era relational rules the mirror can decide alone (§5.3). The
 * division of labour is unchanged: the mirror bounds-checks and re-derives
 * what one document contains; the C# validator re-derives against the engine.
 *
 * Refused here: a turn whose participant is not the one the pre-state says
 * owns those bodies; a resolution set that is not exactly the participant's
 * own live bodies; a command claimed accepted on a body that is not an own
 * live body; two commands for one body on a healthy turn; a slot table that is
 * not the participant's own slots; a published role tag no accepted command
 * ever set; and a body random seed that is not the one the document itself
 * declared at that life's start.
 */
function validateMindTurnRelationships(
  mindTurns: readonly V3.ReplayV3MindTurn[],
  tick: V3.ReplayV3Tick,
  path: string,
  roleTags: Map<string, string>,
  seedsByActor: ReadonlyMap<string, string>,
  fail: ReplayV3Fail,
): void {
  ensureUnique(
    mindTurns,
    (turn) => String(turn.participantId),
    `${path}.mindTurns`,
    fail,
  );
  mindTurns.forEach((turn, turnIndex) => {
    const turnPath = `${path}.mindTurns[${turnIndex}]`;
    if (turn.tick !== tick.tick || turn.observation.tick !== tick.tick) {
      fail(turnPath, 'mind turn and its observation must state their tick');
    }
    if (
      turn.observation.participantId !== turn.participantId ||
      turn.observation.teamId !== turn.teamId
    ) {
      fail(turnPath, 'mind observation must identify its own participant');
    }

    const ownLives = tick.tickStart.state.activeLives.filter(
      (life) => life.participantId === turn.participantId,
    );
    const ownKeys = ownLives.map((life) => actorValue(life.actorId));
    if (
      !sameSet(
        turn.resolutions.map(
          (resolution) =>
            `${turn.teamId}:${resolution.unitId}:${resolution.lifeId}`,
        ),
        ownKeys,
      ) ||
      turn.resolutions.length !== ownLives.length
    ) {
      fail(
        `${turnPath}.resolutions`,
        'must cover exactly the participant own live bodies',
      );
    }
    if (
      !sameSet(
        turn.observation.bodies.map((body) => actorValue(body.actorId)),
        ownKeys,
      )
    ) {
      fail(
        `${turnPath}.observation.bodies`,
        'must be exactly the participant own live bodies',
      );
    }

    const ownSlots = tick.tickStart.state.slots
      .filter((slot) => slot.participantId === turn.participantId)
      .map(unitValue);
    if (
      !sameSet(turn.observation.slots.map(unitValue), ownSlots) ||
      turn.observation.slots.some((slot) => slot.teamId !== turn.teamId)
    ) {
      fail(
        `${turnPath}.observation.slots`,
        'must be exactly the participant own slots',
      );
    }

    const faulted = turn.runtimeFault !== null;
    const liveKeys = new Set(
      ownLives.map((life) => `${life.actorId.unitId}:${life.actorId.lifeId}`),
    );
    const commanded = new Set<string>();
    turn.commands.forEach((command, commandIndex) => {
      const key = `${command.unitId}:${command.lifeId}`;
      // A duplicate is legitimate only on the faulted turn the duplicate
      // itself caused, where nothing was routed.
      if (commanded.has(key) && !faulted) {
        fail(
          `${turnPath}.commands[${commandIndex}]`,
          'cannot command the same body twice',
        );
      }
      commanded.add(key);
      const accepted = command.outcome === 'accepted';
      if (faulted && accepted) {
        fail(
          `${turnPath}.commands[${commandIndex}]`,
          'a faulted turn cannot record an accepted command',
        );
      }
      if (!faulted && accepted !== liveKeys.has(key)) {
        fail(
          `${turnPath}.commands[${commandIndex}]`,
          accepted
            ? 'accepted a command on a body that is not an own live body'
            : 'rejected a command on one of its own live bodies',
        );
      }
    });

    // Every published tag, on own bodies and on visible enemies alike, must be
    // the last tag its own mind actually set.
    for (const body of turn.observation.bodies) {
      requirePublishedRoleTag(
        body.roleTag,
        actorValue(body.actorId),
        roleTags,
        `${turnPath}.observation.bodies`,
        fail,
      );
      const declared = seedsByActor.get(actorValue(body.actorId));
      if (declared === undefined || body.bodyRandomSeed !== declared) {
        fail(
          `${turnPath}.observation.bodies`,
          'body random seed must be the seed declared at that life start',
        );
      }
    }
    for (const enemy of turn.observation.enemies) {
      requirePublishedRoleTag(
        enemy.roleTag,
        actorValue(enemy.actorId),
        roleTags,
        `${turnPath}.observation.enemies`,
        fail,
      );
    }
    for (const ally of turn.observation.allies) {
      requirePublishedRoleTag(
        ally.roleTag,
        actorValue(ally.actorId),
        roleTags,
        `${turnPath}.observation.allies`,
        fail,
      );
    }
  });

  // Tags set this tick are what the NEXT tick publishes: the observation the
  // mind just answered was frozen before any of them were written.
  for (const turn of mindTurns) {
    if (turn.runtimeFault !== null) continue;
    for (const command of turn.commands) {
      if (command.outcome !== 'accepted' || command.roleTag === undefined) {
        continue;
      }
      const key = `${turn.teamId}:${command.unitId}:${command.lifeId}`;
      if (command.roleTag.length === 0) roleTags.delete(key);
      else roleTags.set(key, command.roleTag);
    }
  }
  const live = new Set(
    tick.postState.activeLives.map((life) => actorValue(life.actorId)),
  );
  for (const key of [...roleTags.keys()]) {
    if (!live.has(key)) roleTags.delete(key);
  }
}

function requirePublishedRoleTag(
  published: string | undefined,
  actorKey: string,
  roleTags: ReadonlyMap<string, string>,
  path: string,
  fail: ReplayV3Fail,
): void {
  const expected = roleTags.get(actorKey);
  if ((published ?? null) !== (expected ?? null)) {
    fail(path, 'publishes a role tag its mind never set on that body');
  }
}

function validateV3Relationships(
  document: V3.ReplayV3Document,
  fail: ReplayV3Fail,
): void {
  const { header, initialFrame } = document;
  const { contract } = header;
  const { topology } = contract;
  const fingerprint = contract.matchContractFingerprint;

  const runtimePairs: [string, string | number, string | number][] = [
    [
      'contractProfileId',
      header.runtime.contractProfileId,
      contract.capabilityVersions.contractProfileId,
    ],
    [
      'protocolVersion',
      header.runtime.protocolVersion,
      contract.capabilityVersions.runtimeProtocolVersion,
    ],
    [
      'configurationVersion',
      header.runtime.configurationVersion,
      contract.capabilityVersions.runtimeConfigurationVersion,
    ],
    [
      'runtimeContractVersion',
      header.runtime.runtimeContractVersion,
      contract.capabilityVersions.runtimeContractVersion,
    ],
    [
      'matchStartSchemaVersion',
      header.runtime.matchStartSchemaVersion,
      contract.capabilityVersions.matchStartSchemaVersion,
    ],
    [
      'observationSchemaVersion',
      header.runtime.observationSchemaVersion,
      contract.capabilityVersions.observationSchemaVersion,
    ],
    [
      'decisionSchemaVersion',
      header.runtime.decisionSchemaVersion,
      contract.capabilityVersions.decisionSchemaVersion,
    ],
    [
      'matchContractSchemaVersion',
      header.runtime.matchContractSchemaVersion,
      contract.capabilityVersions.matchContractSchemaVersion,
    ],
  ];
  for (const [name, runtime, capability] of runtimePairs) {
    if (runtime !== capability) {
      fail(
        `replay.header.runtime.${name}`,
        'must match contract capability versions',
      );
    }
  }
  const capabilities = contract.capabilityVersions;
  // Two exact tuples, side by side rather than one widened one: the mind
  // profile mints fresh runtime/MatchStart/observation/decision schema numbers
  // in its own namespace precisely so they never collide with the actor line's
  // 2s, and it CARRIES match-contract schema 2 because the game is unchanged —
  // only who is driving it changes (§1.2). Admitting it by relaxing the
  // per-life tuple would lose exactly that distinction.
  const perLifeProfile =
    capabilities.contractProfileId === 'generic-actor-match-2' &&
    capabilities.runtimeProtocolVersion === '1.0' &&
    capabilities.runtimeConfigurationVersion === '1.0' &&
    capabilities.runtimeContractVersion === 2 &&
    capabilities.matchStartSchemaVersion === 2 &&
    capabilities.observationSchemaVersion === 2 &&
    capabilities.decisionSchemaVersion === 2;
  const mindProfileTuple =
    capabilities.contractProfileId === MIND_CONTRACT_PROFILE_ID &&
    capabilities.runtimeProtocolVersion === '1.0' &&
    capabilities.runtimeConfigurationVersion === '2.0' &&
    capabilities.runtimeContractVersion === 1 &&
    capabilities.matchStartSchemaVersion === 1 &&
    capabilities.observationSchemaVersion === 1 &&
    capabilities.decisionSchemaVersion === 1;
  if (
    (!perLifeProfile && !mindProfileTuple) ||
    capabilities.matchContractSchemaVersion !== 2 ||
    contract.schemaVersion !== 2
  ) {
    fail(
      'replay.header.contract.capabilityVersions',
      'must select the exact supported generic actor contract profile',
    );
  }
  if (contract.rules.rulesetId !== header.gameRulesVersion) {
    fail(
      'replay.header.gameRulesVersion',
      'must match contract.rules.rulesetId',
    );
  }
  if (contract.rules.gameMode.kind !== contract.modeMapBinding.kind) {
    fail(
      'replay.header.contract.modeMapBinding.kind',
      'must match rules.gameMode.kind',
    );
  }
  if (
    contract.rules.gameMode.kind === 'frontline' &&
    contract.modeMapBinding.kind === 'frontline'
  ) {
    const mode = contract.rules.gameMode;
    const binding = contract.modeMapBinding;
    const bindingPath = 'replay.header.contract.modeMapBinding';
    if (
      binding.orderedObjectiveRegionIds.length !==
      mode.frontlinePositionCount
    ) {
      fail(
        `${bindingPath}.orderedObjectiveRegionIds`,
        'must contain exactly frontlinePositionCount regions',
      );
    }
    ensureUnique(
      binding.orderedObjectiveRegionIds,
      (regionId) => regionId,
      `${bindingPath}.orderedObjectiveRegionIds`,
      fail,
    );
    ensureUnique(
      contract.map.regions,
      (region) => region.regionId,
      'replay.header.contract.map.regions',
      fail,
    );
    const claimedObjectiveTiles = new Set<string>();
    binding.orderedObjectiveRegionIds.forEach((regionId, index) => {
      const region = contract.map.regions.find(
        (candidate) => candidate.regionId === regionId,
      );
      if (!region || region.kind !== 'objective') {
        fail(
          `${bindingPath}.orderedObjectiveRegionIds[${index}]`,
          'must reference an objective map region',
        );
      }
      if (region.tiles.length === 0) {
        fail(
          `${bindingPath}.orderedObjectiveRegionIds[${index}]`,
          'must reference a non-empty objective map region',
        );
      }
      region.tiles.forEach((tile, tileIndex) => {
        const key = `${tile[0]},${tile[1]}`;
        if (claimedObjectiveTiles.has(key)) {
          fail(
            `${bindingPath}.orderedObjectiveRegionIds[${index}]`,
            `overlaps another frontline objective at tile ${key}`,
          );
        }
        claimedObjectiveTiles.add(key);
        if (
          tile[0] < 0 ||
          tile[0] >= contract.map.width ||
          tile[1] < 0 ||
          tile[1] >= contract.map.height ||
          contract.map.tileRows[tile[1]]?.[tile[0]] !== '.'
        ) {
          fail(
            `${bindingPath}.orderedObjectiveRegionIds[${index}].tiles[${tileIndex}]`,
            'must identify an in-bounds floor tile',
          );
        }
      });
    });
  }
  if (
    contract.map.tileRows.length !== contract.map.height ||
    contract.map.tileRows.some((row) => row.length !== contract.map.width)
  ) {
    fail(
      'replay.header.contract.map.tileRows',
      'must match the declared width and height',
    );
  }

  ensureUnique(topology.teams, (team) => team.teamId, 'replay.header.contract.topology.teams', fail);
  ensureUnique(
    topology.participants,
    (participant) => participant.participantId,
    'replay.header.contract.topology.participants',
    fail,
  );
  ensureUnique(
    topology.unitSlots,
    unitValue,
    'replay.header.contract.topology.unitSlots',
    fail,
  );
  ensureUnique(
    topology.initialLives,
    actorValue,
    'replay.header.contract.topology.initialLives',
    fail,
  );
  const counts = topology.counts;
  for (const [path, actual, expected] of [
    ['teamCount', topology.teams.length, counts.teamCount],
    ['participantCount', topology.participants.length, counts.participantCount],
    ['unitSlotCount', topology.unitSlots.length, counts.unitSlotCount],
    ['initialLifeCount', topology.initialLives.length, counts.initialLifeCount],
  ] as const) {
    if (actual !== expected) {
      fail(
        `replay.header.contract.topology.counts.${path}`,
        `declares ${expected} but contains ${actual}`,
      );
    }
  }

  const teams = new Set(topology.teams.map((team) => team.teamId));
  const teamDefinitions = new Map(
    topology.teams.map((team) => [team.teamId, team]),
  );
  if (contract.modeMapBinding.kind === 'frontline') {
    const advances = contract.modeMapBinding.teamAdvances;
    ensureUnique(
      advances,
      (advance) => advance.teamId,
      'replay.header.contract.modeMapBinding.teamAdvances',
      fail,
    );
    advances.forEach((advance, index) => {
      if (index > 0 && advance.teamId <= advances[index - 1]!.teamId) {
        fail(
          `replay.header.contract.modeMapBinding.teamAdvances[${index}].teamId`,
          'must be in canonical ascending team order',
        );
      }
    });
    if (
      advances.length !== 2 ||
      !sameSet(
        advances.map((advance) => String(advance.teamId)),
        topology.teams.map((team) => String(team.teamId)),
      ) ||
      advances.reduce(
        (total, advance) => total + advance.objectiveIndexDelta,
        0,
      ) !== 0
    ) {
      fail(
        'replay.header.contract.modeMapBinding.teamAdvances',
        'must cover exactly two topology teams advancing in opposite directions',
      );
    }
  }
  const participants = new Map(
    topology.participants.map((participant) => [
      participant.participantId,
      participant,
    ]),
  );
  const slots = new Map(
    topology.unitSlots.map((slot) => [unitValue(slot), slot]),
  );
  const classForActor = (actor: V3.ReplayV3ActorId): string | null => {
    const slot = slots.get(unitValue(actor));
    if (!slot) return null;
    // A BODY's published chassis is its SLOT's where the slot declares one,
    // and its participant's otherwise. Under a mixed COMPOSITION the
    // participant's ID is a composition token rather than a chassis, so a
    // body must never be checked against it (DECISIONS #191 §9.2, #194).
    return (
      slot.classId ??
      participants.get(slot.controllerParticipantId)?.classId ??
      null
    );
  };
  const attackProfiles = new Map(
    contract.rules.attackProfiles.map((profile) => [profile.id, profile]),
  );
  const forms = new Set(contract.rules.forms.map((form) => form.id));
  for (const [index, participant] of topology.participants.entries()) {
    const team = teamDefinitions.get(participant.teamId);
    if (!team) {
      fail(
        `replay.header.contract.topology.participants[${index}].teamId`,
        `unknown team ${participant.teamId}`,
      );
    }
    if (participant.classId !== team.classId) {
      fail(
        `replay.header.contract.topology.participants[${index}].classId`,
        'must exactly match the scoring team classId, including omission',
      );
    }
  }
  for (const [index, slot] of topology.unitSlots.entries()) {
    const controller = participants.get(slot.controllerParticipantId);
    if (!teams.has(slot.teamId)) {
      fail(
        `replay.header.contract.topology.unitSlots[${index}].teamId`,
        `unknown team ${slot.teamId}`,
      );
    }
    if (!controller || controller.teamId !== slot.teamId) {
      fail(
        `replay.header.contract.topology.unitSlots[${index}].controllerParticipantId`,
        'must reference a participant on the same team',
      );
    }
  }
  for (const [index, life] of topology.initialLives.entries()) {
    if (!slots.has(unitValue(life))) {
      fail(
        `replay.header.contract.topology.initialLives[${index}]`,
        'must reference a topology unit slot',
      );
    }
    if (!forms.has(life.formId)) {
      fail(
        `replay.header.contract.topology.initialLives[${index}].formId`,
        `unknown form ${life.formId}`,
      );
    }
  }

  ensureUnique(
    contract.initialDeployment.spawns,
    (spawn) => spawn.spawnId,
    'replay.header.contract.initialDeployment.spawns',
    fail,
  );
  ensureUnique(
    contract.initialDeployment.lives,
    actorValue,
    'replay.header.contract.initialDeployment.lives',
    fail,
  );
  const declaredSpawns = new Map(
    contract.map.spawnAnchors.map((spawn) => [
      spawn.spawnId,
      spawn.position,
    ]),
  );
  const permanentReservations = new Map<
    string,
    V3.ReplayV3SpawnReservation
  >();
  const lifecycle = object(
    contract.rules.lifecycle,
    'replay.header.contract.rules.lifecycle',
    fail,
  );
  const automaticProfileIds = new Set(
    array(
      lifecycle.profiles,
      'replay.header.contract.rules.lifecycle.profiles',
      fail,
    )
      .map((value, index) =>
        object(
          value,
          `replay.header.contract.rules.lifecycle.profiles[${index}]`,
          fail,
        ),
      )
      .filter(
        (profile) => profile.destructionPolicy === 'automatic-respawn',
      )
      .map((profile) => {
        semanticId(
          profile.profileId,
          'replay.header.contract.rules.lifecycle.profiles[].profileId',
          fail,
        );
        return profile.profileId as string;
      }),
  );
  contract.lifecycleAssignments.forEach((assignment, index) => {
    if (!automaticProfileIds.has(assignment.lifecycleProfileId)) return;
    if (assignment.assignedRespawnSpawnId === null) return;
    const position = declaredSpawns.get(
      assignment.assignedRespawnSpawnId,
    );
    if (!position) {
      fail(
        `replay.header.contract.lifecycleAssignments[${index}].assignedRespawnSpawnId`,
        'must reference a declared map spawn',
      );
    }
    const key = `${position[0]},${position[1]}`;
    if (permanentReservations.has(key)) {
      fail(
        `replay.header.contract.lifecycleAssignments[${index}].assignedRespawnSpawnId`,
        'automatic-return spawn reservations must be position-unique',
      );
    }
    permanentReservations.set(key, {
      teamId: assignment.teamId,
      unitId: assignment.unitId,
      kind: 'automatic-return',
      dueTick: null,
    });
  });
  const spawnReservationAt = (
    position: V3.ReplayV3Position,
    world: V3.ReplayV3WorldState,
  ): V3.ReplayV3SpawnReservation | null => {
    for (const replication of world.pendingReplications) {
      const descendant = replication.descendants.find(
        (candidate) =>
          candidate.position.x === position.x &&
          candidate.position.y === position.y,
      );
      if (descendant) {
        return {
          teamId: descendant.teamId,
          unitId: descendant.unitId,
          kind: 'replication',
          dueTick: replication.dueTick,
        };
      }
    }
    for (const slot of world.slots) {
      if (
        slot.state.kind === 'fabrication-pending' &&
        slot.state.reservedPosition.x === position.x &&
        slot.state.reservedPosition.y === position.y
      ) {
        return {
          teamId: slot.teamId,
          unitId: slot.unitId,
          kind: 'fabrication',
          dueTick: slot.state.dueTick,
        };
      }
    }
    return permanentReservations.get(`${position.x},${position.y}`) ?? null;
  };
  if (
    !sameSet(
      contract.initialDeployment.lives.map(actorValue),
      topology.initialLives.map(actorValue),
    )
  ) {
    fail(
      'replay.header.contract.initialDeployment.lives',
      'must cover exactly the topology initial lives',
    );
  }
  const spawnIds = new Set(
    contract.initialDeployment.spawns.map((spawn) => spawn.spawnId),
  );
  for (const [index, life] of contract.initialDeployment.lives.entries()) {
    const topologyLife = topology.initialLives.find(
      (candidate) => actorValue(candidate) === actorValue(life),
    );
    if (
      !topologyLife ||
      topologyLife.formId !== life.formId ||
      !spawnIds.has(life.spawnId)
    ) {
      fail(
        `replay.header.contract.initialDeployment.lives[${index}]`,
        'must match a topology life, form, and declared spawn',
      );
    }
  }

  if (header.provenance) {
    ensureUnique(
      header.provenance.participants,
      (participant) => participant.participantId,
      'replay.header.provenance.participants',
      fail,
    );
    if (
      !sameSet(
        header.provenance.participants.map((participant) =>
          String(participant.participantId),
        ),
        topology.participants.map((participant) =>
          String(participant.participantId),
        ),
      )
    ) {
      fail(
        'replay.header.provenance.participants',
        'must cover exactly the topology participants',
      );
    }
    header.provenance.participants.forEach((participant, index) => {
      if (
        participants.get(participant.participantId)?.teamId !==
        participant.teamId
      ) {
        fail(
          `replay.header.provenance.participants[${index}].teamId`,
          'must match topology participant team',
        );
      }
    });
  }

  const scoreChannels = contract.rules.gameMode.scoreCatalog.map(
    (score) => score.channel,
  );
  ensureUnique(
    contract.rules.gameMode.scoreCatalog,
    (score) => score.channel,
    'replay.header.contract.rules.gameMode.scoreCatalog',
    fail,
  );

  const validateWorld = (
    world: V3.ReplayV3WorldState,
    path: string,
    expectedNextTick: number,
  ) => {
    if (world.matchContractFingerprint !== fingerprint) {
      fail(
        `${path}.matchContractFingerprint`,
        'must match header contract fingerprint',
      );
    }
    if (world.nextTick !== expectedNextTick) {
      fail(`${path}.nextTick`, `expected ${expectedNextTick}`);
    }
    ensureUnique(world.participants, (status) => status.participantId, `${path}.participants`, fail);
    ensureUnique(world.slots, unitValue, `${path}.slots`, fail);
    ensureUnique(world.activeLives, (life) => actorValue(life.actorId), `${path}.activeLives`, fail);
    ensureUnique(world.projectiles, (projectile) => projectile.projectileId, `${path}.projectiles`, fail);
    if (
      !sameSet(
        world.participants.map((status) => String(status.participantId)),
        topology.participants.map((participant) => String(participant.participantId)),
      )
    ) {
      fail(`${path}.participants`, 'must cover exactly topology participants');
    }
    if (
      !sameSet(world.slots.map(unitValue), topology.unitSlots.map(unitValue))
    ) {
      fail(`${path}.slots`, 'must cover exactly topology unit slots');
    }
    for (const [index, status] of world.participants.entries()) {
      const participant = participants.get(status.participantId);
      if (
        participant?.teamId !== status.teamId ||
        (participant.classId ?? null) !== status.classId
      ) {
        fail(
          `${path}.participants[${index}]`,
          'must match topology participant team and classId',
        );
      }
    }
    const activeByActor = new Map(
      world.activeLives.map((life) => [actorValue(life.actorId), life]),
    );
    for (const [index, slot] of world.slots.entries()) {
      const topologySlot = slots.get(unitValue(slot));
      if (
        !topologySlot ||
        topologySlot.controllerParticipantId !== slot.participantId
      ) {
        fail(
          `${path}.slots[${index}].participantId`,
          'must match topology controller',
        );
      }
      if (slot.state.kind === 'active') {
        const active = activeByActor.get(actorValue(slot.state.actorId));
        if (
          !active ||
          unitValue(active.actorId) !== unitValue(slot) ||
          active.participantId !== slot.participantId ||
          active.generation !== slot.state.generation ||
          active.formId !== slot.state.formId
        ) {
          fail(
            `${path}.slots[${index}].state`,
            'active slot must match exactly one active life',
          );
        }
      }
    }
    for (const [index, life] of world.activeLives.entries()) {
      const slot = world.slots.find(
        (candidate) => unitValue(candidate) === unitValue(life.actorId),
      );
      if (
        !slot ||
        slot.state.kind !== 'active' ||
        actorValue(slot.state.actorId) !== actorValue(life.actorId)
      ) {
        fail(
          `${path}.activeLives[${index}]`,
          'must be the active life of its stable unit slot',
        );
      }
    }
    ensureUnique(world.scoreboard.teams, (team) => team.teamId, `${path}.scoreboard.teams`, fail);
    if (
      !sameSet(
        world.scoreboard.teams.map((team) => String(team.teamId)),
        topology.teams.map((team) => String(team.teamId)),
      )
    ) {
      fail(`${path}.scoreboard.teams`, 'must cover exactly topology teams');
    }
    world.scoreboard.teams.forEach((team, index) => {
      ensureUnique(team.scores, (score) => score.channel, `${path}.scoreboard.teams[${index}].scores`, fail);
      if (!sameSet(team.scores.map((score) => score.channel), scoreChannels)) {
        fail(
          `${path}.scoreboard.teams[${index}].scores`,
          'must cover exactly the mode score catalog',
        );
      }
    });
    if (
      world.mode.kind !== contract.rules.gameMode.kind ||
      world.mode.modeId !== contract.rules.gameMode.modeId
    ) {
      fail(`${path}.mode`, 'must match the resolved contract game mode');
    }
    if (
      world.mode.kind === 'frontline' &&
      contract.rules.gameMode.kind === 'frontline' &&
      contract.modeMapBinding.kind === 'frontline'
    ) {
      const control = world.mode;
      const mode = contract.rules.gameMode;
      const capture = mode.capture;
      const secondaryThresholdTicks =
        mode.secondaryControl?.captureThresholdTicks ?? null;
      const claimantKnown =
        control.claimingTeamId === null ||
        teams.has(control.claimingTeamId);
      const neutral = control.claimingTeamId === null;
      const invalidControl =
        control.activePositionIndex < 0 ||
        control.activePositionIndex >= mode.frontlinePositionCount ||
        !claimantKnown ||
        control.captureProgress < 0 ||
        control.captureProgress >= capture.threshold ||
        neutral !== (control.captureProgress === 0) ||
        control.decayTicksElapsed < 0 ||
        (neutral && control.decayTicksElapsed !== 0) ||
        (capture.decayIntervalTicks === 0
          ? control.decayTicksElapsed !== 0
          : control.decayTicksElapsed >= capture.decayIntervalTicks) ||
        control.controlResumesAtTick < 0 ||
        control.controlResumesAtTick - world.nextTick >
          capture.redeployPauseTicks ||
        (world.nextTick < control.controlResumesAtTick &&
          (!neutral ||
            control.captureProgress !== 0 ||
            control.decayTicksElapsed !== 0)) ||
        // The hold clocks travel as a pair, only the high-water-mark redeploy
        // policy may carry them at all, an owner must be a real scoring team,
        // and a PUBLISHED hold is by definition still live — so its expiry is
        // strictly ahead of this tick and inside the declared duration.
        (control.holdOwnerTeamId === null) !==
          (control.holdEndsAtTick === null) ||
        (control.holdOwnerTeamId !== null &&
          capture.redeployPolicy !== RATCHET_REDEPLOY_POLICY) ||
        (control.holdOwnerTeamId !== null &&
          !teams.has(control.holdOwnerTeamId)) ||
        // A hold is created on the advance tick T with expiry T+hold+1, and
        // the earliest boundary that can publish it has nextTick T+1, so the
        // widest honest gap is exactly the declared duration.
        (control.holdEndsAtTick !== null &&
          (control.holdEndsAtTick <= world.nextTick ||
            control.holdEndsAtTick - world.nextTick >
              (capture.ratchetHoldTicks ?? 0))) ||
        // Only a mode that declares a side objective may publish one. Its
        // owner and its claimant are real scoring teams, they are never the
        // same team, and a standing claim is strictly below the declared
        // threshold because reaching it latches ownership that very tick.
        ((control.secondaryOwnerTeamId !== null ||
          control.secondaryClaimProgress !== 0) &&
          secondaryThresholdTicks === null) ||
        (control.secondaryOwnerTeamId !== null &&
          !teams.has(control.secondaryOwnerTeamId)) ||
        Math.abs(control.secondaryClaimProgress) >=
          Math.max(secondaryThresholdTicks ?? 0, 1) ||
        (secondaryClaimant(control.secondaryClaimProgress) !== null &&
          (!teams.has(
            secondaryClaimant(control.secondaryClaimProgress) as number,
          ) ||
            secondaryClaimant(control.secondaryClaimProgress) ===
              control.secondaryOwnerTeamId));
      if (invalidControl) {
        fail(`${path}.mode`, 'violates frontline control invariants');
      }

      const centre = Math.floor(mode.frontlinePositionCount / 2);
      const advances = new Map(
        contract.modeMapBinding.teamAdvances.map((advance) => [
          advance.teamId,
          advance.objectiveIndexDelta,
        ]),
      );
      world.scoreboard.teams.forEach((team, teamIndex) => {
        const territorial = team.scores.find(
          (score) => score.channel === 'territorial-progress',
        );
        const delta = advances.get(team.teamId);
        if (!territorial || delta === undefined) {
          fail(
            `${path}.scoreboard.teams[${teamIndex}].scores`,
            'must expose frontline territorial progress for the team',
          );
        }
        const claim =
          control.claimingTeamId === null
            ? 0n
            : control.claimingTeamId === team.teamId
              ? BigInt(control.captureProgress)
              : -BigInt(control.captureProgress);
        const expected =
          BigInt(delta) *
            BigInt(control.activePositionIndex - centre) *
            BigInt(capture.threshold) +
          claim;
        if (territorial.value !== expected.toString()) {
          fail(
            `${path}.scoreboard.teams[${teamIndex}].scores`,
            'must match the frontline territorial-progress formula',
          );
        }
      });
    }
  };

  validateWorld(initialFrame.state, 'replay.initialFrame.state', 0);
  if (
    !sameSet(
      initialFrame.lifeStarts.map((start) => actorValue(start.actorId)),
      topology.initialLives.map(actorValue),
    )
  ) {
    fail(
      'replay.initialFrame.lifeStarts',
      'must cover exactly the topology initial lives',
    );
  }

  const validateStart = (
    start: V3.ReplayV3LifeStart,
    path: string,
  ) => {
    if (
      start.matchContractFingerprint !== fingerprint ||
      start.schemaVersion !== header.runtime.matchStartSchemaVersion ||
      start.runtimeContractVersion !== header.runtime.runtimeContractVersion
    ) {
      fail(path, 'life start versions/fingerprint must match the replay header');
    }
    const slot = slots.get(unitValue(start.actorId));
    if (!slot || slot.controllerParticipantId !== start.participantId) {
      fail(`${path}.participantId`, 'must match the topology unit controller');
    }
  };
  initialFrame.lifeStarts.forEach((start, index) =>
    {
      const path = `replay.initialFrame.lifeStarts[${index}]`;
      validateStart(start, path);
      const life = initialFrame.state.activeLives.find(
        (candidate) =>
          actorValue(candidate.actorId) === actorValue(start.actorId),
      );
      if (
        !life ||
        life.participantId !== start.participantId ||
        life.generation !== start.origin.generation
      ) {
        fail(path, 'must match an initial authoritative active life');
      }
    },
  );

  const ordinalOwners = new Map<string, string>();
  const recordOrdinals = (
    values: readonly ({ globalOrdinal: string })[],
    path: string,
  ) => {
    values.forEach((value, index) => {
      const owner = `${path}[${index}]`;
      const prior = ordinalOwners.get(value.globalOrdinal);
      if (prior) {
        fail(`${owner}.globalOrdinal`, `duplicates ${prior}.globalOrdinal`);
      }
      ordinalOwners.set(value.globalOrdinal, owner);
    });
  };
  recordOrdinals(initialFrame.events, 'replay.initialFrame.events');

  if (document.ticks.length > contract.rules.limits.maxTicks) {
    fail(
      'replay.ticks',
      'cannot extend beyond the configured maximum tick boundary',
    );
  }

  // The mind-era derived facts, accumulated across ticks so the mirror can
  // decide them alone: the seed each life was declared with, and the last tag
  // each mind actually set. Empty on a per-life document.
  const seedsByActor = new Map<string, string>(
    initialFrame.lifeStarts.map((start) => [
      actorValue(start.actorId),
      start.actorRandomSeed,
    ]),
  );
  const roleTags = new Map<string, string>();

  let previousWorld = initialFrame.state;
  document.ticks.forEach((tick, tickIndex) => {
    const path = `replay.ticks[${tickIndex}]`;
    if (tick.tick !== tickIndex || tick.tickStart.tick !== tick.tick) {
      fail(`${path}.tick`, `ticks must be contiguous from zero`);
    }
    validateReplayV3TickStartBoundary(
      previousWorld,
      tick.tickStart,
      `${path}.tickStart.state`,
      fail,
    );
    validateWorld(tick.tickStart.state, `${path}.tickStart.state`, tick.tick);
    validateWorld(tick.postState, `${path}.postState`, tick.tick + 1);
    if (
      !sameSet(
        tick.tickStart.activeActorIds.map(actorValue),
        tick.tickStart.state.activeLives.map((life) => actorValue(life.actorId)),
      )
    ) {
      fail(
        `${path}.tickStart.activeActorIds`,
        'must cover exactly the tick-start active lives',
      );
    }
    // Under the mind, the per-body turns are DERIVED from one turn per
    // participant. Deriving them here rather than branching every check below
    // is the same trade the memo makes for the viewer: the union was always
    // the interesting invariant, and the per-life specialization of it was
    // only ever a projection.
    for (const start of tick.tickStart.lifeStarts) {
      seedsByActor.set(actorValue(start.actorId), start.actorRandomSeed);
    }
    const mindTurns = tick.mindTurns;
    const turns = mindTurns
      ? mindTurns.flatMap(specializeMindTurn)
      : (tick.actorTurns ?? []);
    if (mindTurns) {
      validateMindTurnRelationships(
        mindTurns,
        tick,
        path,
        roleTags,
        seedsByActor,
        fail,
      );
    }
    ensureUnique(turns, (turn) => actorValue(turn.actorId), `${path}.actorTurns`, fail);
    if (
      !sameSet(
        turns.map((turn) => actorValue(turn.actorId)),
        tick.tickStart.activeActorIds.map(actorValue),
      )
    ) {
      fail(`${path}.actorTurns`, 'must cover exactly the active actors');
    }
    tick.tickStart.lifeStarts.forEach((start, index) => {
      const startPath = `${path}.tickStart.lifeStarts[${index}]`;
      seedsByActor.set(actorValue(start.actorId), start.actorRandomSeed);
      validateStart(start, startPath);
      const life = tick.tickStart.state.activeLives.find(
        (candidate) =>
          actorValue(candidate.actorId) === actorValue(start.actorId),
      );
      if (
        !life ||
        life.participantId !== start.participantId ||
        life.generation !== start.origin.generation
      ) {
        fail(startPath, 'must match a tick-start authoritative active life');
      }
    });
    turns.forEach((turn, turnIndex) => {
      const turnPath = `${path}.actorTurns[${turnIndex}]`;
      const actor = tick.tickStart.state.activeLives.find(
        (life) => actorValue(life.actorId) === actorValue(turn.actorId),
      );
      if (
        !actor ||
        actor.participantId !== turn.participantId ||
        turn.tick !== tick.tick ||
        actorValue(turn.observation.self.actorId) !== actorValue(turn.actorId) ||
        turn.observation.tick !== tick.tick ||
        turn.observation.schemaVersion !== header.runtime.observationSchemaVersion ||
        turn.observation.matchContractFingerprint !== fingerprint
      ) {
        fail(turnPath, 'turn identity, tick, or observation contract is inconsistent');
      }
      const observedSelf = turn.observation.self;
      if (
        actor.generation !== observedSelf.generation ||
        actor.formId !== observedSelf.formId ||
        JSON.stringify(actor.position) !==
          JSON.stringify(observedSelf.position) ||
        actor.facing !== observedSelf.facing ||
        actor.health !== observedSelf.health ||
        actor.cooldown !== observedSelf.cooldown ||
        actor.energy !== observedSelf.energy ||
        observedSelf.classId !== classForActor(turn.actorId) ||
        JSON.stringify(actor.previousActionResolution) !==
          JSON.stringify(observedSelf.previousActionResolution) ||
        JSON.stringify(actor.pendingSameLifeTransition) !==
          JSON.stringify(observedSelf.pendingSameLifeTransition)
      ) {
        fail(
          `${turnPath}.observation.self`,
          'must equal the exact frozen tick-start life',
        );
      }
      if (
        JSON.stringify(turn.observation.participants) !==
          JSON.stringify(tick.tickStart.state.participants) ||
        JSON.stringify(turn.observation.scoreboard) !==
          JSON.stringify(tick.tickStart.state.scoreboard) ||
        !observedModeMatchesWorld(
          turn.observation.mode,
          tick.tickStart.state.mode,
        )
      ) {
        fail(
          `${turnPath}.observation`,
          'participants, scoreboard, and mode must use the frozen tick-start state',
        );
      }
      const expectedTeamUnits = tick.tickStart.state.slots
        .filter((slot) => slot.teamId === turn.actorId.teamId)
        .map(unitValue);
      if (
        !sameSet(
          turn.observation.teamUnits.map(unitValue),
          expectedTeamUnits,
        )
      ) {
        fail(
          `${turnPath}.observation.teamUnits`,
          'must cover exactly the observer team unit slots',
        );
      }
      turn.observation.teamUnits.forEach((observedUnit, unitIndex) => {
        const authoritative = tick.tickStart.state.slots.find(
          (slot) => unitValue(slot) === unitValue(observedUnit),
        );
        if (
          !authoritative ||
          JSON.stringify(observedUnit.state) !==
            JSON.stringify(authoritative.state)
        ) {
          fail(
            `${turnPath}.observation.teamUnits[${unitIndex}].state`,
            'must equal the frozen tick-start slot state',
          );
        }
      });
      [...turn.observation.allies, ...turn.observation.enemies].forEach(
        (observedActor, observedIndex) => {
          if (
            observedActor.classId !==
            classForActor(observedActor.actorId)
          ) {
            fail(
              `${turnPath}.observation.visibleActors[${observedIndex}].classId`,
              'must match the observed actor controller classId',
            );
          }
        },
      );
      turn.observation.visibleTiles.forEach((tile, tileIndex) => {
        const expected = spawnReservationAt(
          tile.position,
          tick.tickStart.state,
        );
        if (
          JSON.stringify(tile.spawnReservation ?? null) !==
          JSON.stringify(expected)
        ) {
          fail(
            `${turnPath}.observation.visibleTiles[${tileIndex}].spawnReservation`,
            'must match the authoritative tick-start spawn claim',
          );
        }
      });
      const authoritativeProjectiles = new Map(
        tick.tickStart.state.projectiles.map((projectile) => [
          projectile.projectileId,
          projectile,
        ]),
      );
      turn.observation.visibleProjectiles?.forEach(
        (observedProjectile, projectileIndex) => {
          const authoritative = authoritativeProjectiles.get(
            observedProjectile.projectileId,
          );
          const profile = authoritative
            ? attackProfiles.get(authoritative.attackProfileId)
            : undefined;
          const expectedOwnerActorId =
            authoritative &&
            (authoritative.ownerTeamId === turn.actorId.teamId ||
              turn.observation.enemies.some(
                (enemy) =>
                  actorValue(enemy.actorId) ===
                  actorValue(authoritative.ownerActorId),
              ))
              ? authoritative.ownerActorId
              : null;
          if (
            !authoritative ||
            !profile ||
            observedProjectile.ownerTeamId !==
              authoritative.ownerTeamId ||
            JSON.stringify(observedProjectile.ownerActorId) !==
              JSON.stringify(expectedOwnerActorId) ||
            JSON.stringify(observedProjectile.position) !==
              JSON.stringify(authoritative.position) ||
            observedProjectile.heading !== authoritative.heading ||
            observedProjectile.tilesPerAdvance !==
              profile.projectile.tilesPerAdvance ||
            observedProjectile.ticksUntilAdvance !==
              authoritative.ticksUntilAdvance ||
            observedProjectile.remainingTiles !==
              authoritative.remainingTiles ||
            observedProjectile.ticksPerAdvance !==
              profile.projectile.ticksPerAdvance ||
            observedProjectile.damagePerHit !==
              profile.projectile.damagePerHit
          ) {
            fail(
              `${turnPath}.observation.visibleProjectiles[${projectileIndex}]`,
              'must match the authoritative projectile and attack profile',
            );
          }
        },
      );
      const actionsById = new Map(
        contract.rules.actions.map((action) => [action.id, action]),
      );
      ensureUnique(
        turn.observation.actionLegalities,
        (legality) => legality.actionId,
        `${turnPath}.observation.actionLegalities`,
        fail,
      );
      if (
        !sameSet(
          turn.observation.actionLegalities.map(
            (legality) => legality.actionId,
          ),
          contract.rules.actions.map((action) => action.id),
        )
      ) {
        fail(
          `${turnPath}.observation.actionLegalities`,
          'must cover exactly the contract action catalog',
        );
      }
      turn.observation.actionLegalities.forEach((legality, legalityIndex) => {
        if (actionsById.get(legality.actionId)?.code !== legality.actionCode) {
          fail(
            `${turnPath}.observation.actionLegalities[${legalityIndex}].actionCode`,
            'must match the contract action code',
          );
        }
      });
      for (const [name, action] of [
        ['acceptedAction', turn.actionResolution.acceptedAction],
        ['validatedAction', turn.actionResolution.validatedAction],
      ] as const) {
        if (actionsById.get(action.actionId)?.code !== action.actionCode) {
          fail(
            `${turnPath}.actionResolution.${name}`,
            'must reference a contract action id/code pair',
          );
        }
      }
    });
    for (const [collectionName, values] of [
      ['tickStart.events', tick.tickStart.events],
      ['events', tick.events],
    ] as const) {
      values.forEach((event, index) => {
        if (
          event.tick !== tick.tick ||
          (index > 0 &&
            event.sourceOrdinal <= (values[index - 1]?.sourceOrdinal ?? -1))
        ) {
          fail(
            `${path}.${collectionName}[${index}]`,
            'event tick must match and source ordinals must be increasing',
          );
        }
      });
      recordOrdinals(values, `${path}.${collectionName}`);
    }
    for (const [collectionName, values] of [
      ['tickStart.traversals', tick.tickStart.traversals],
      ['traversals', tick.traversals],
    ] as const) {
      values.forEach((item, index) => {
        if (item.tick !== tick.tick) {
          fail(
            `${path}.${collectionName}[${index}].tick`,
            'must match the owning tick',
          );
        }
      });
      recordOrdinals(values, `${path}.${collectionName}`);
    }
    previousWorld = tick.postState;
  });

  if (document.partial !== (document.result === null)) {
    fail('replay.partial', 'must be true exactly when result is null');
  }
  if (document.partial && document.replayHash !== null) {
    fail('replay.replayHash', 'partial replay must not carry a hash');
  }
  if (!document.partial && document.replayHash === null) {
    fail('replay.replayHash', 'complete replay must carry a hash');
  }
  if (document.result) {
    const result = document.result;
    const finalWorld = previousWorld;
    ensureUnique(result.standings.teams, (team) => team.teamId, 'replay.result.standings.teams', fail);
    if (
      !sameSet(
        result.standings.teams.map((team) => String(team.teamId)),
        topology.teams.map((team) => String(team.teamId)),
      )
    ) {
      fail('replay.result.standings.teams', 'must cover exactly topology teams');
    }
    if (
      result.standings.winnerTeamId !== null &&
      !teams.has(result.standings.winnerTeamId)
    ) {
      fail('replay.result.standings.winnerTeamId', 'unknown winning team');
    }
    if (
      !sameSet(
        result.eligibleTeamIds.map(String),
        finalWorld.scoreboard.teams
          .filter((team) => team.eligible)
          .map((team) => String(team.teamId)),
      )
    ) {
      fail(
        'replay.result.eligibleTeamIds',
        'must match the final scoreboard eligibility',
      );
    }
    ensureUnique(
      result.eligibleTeamIds,
      (teamId) => teamId,
      'replay.result.eligibleTeamIds',
      fail,
    );
    result.eligibleTeamIds.forEach((teamId, index) => {
      if (
        index > 0 &&
        teamId <= result.eligibleTeamIds[index - 1]!
      ) {
        fail(
          `replay.result.eligibleTeamIds[${index}]`,
          'must be in canonical ascending team order',
        );
      }
    });
    ensureUnique(result.units, (unit) => unitValue(unit.slot), 'replay.result.units', fail);
    if (
      !sameSet(
        result.units.map((unit) => unitValue(unit.slot)),
        finalWorld.slots.map(unitValue),
      )
    ) {
      fail('replay.result.units', 'must cover exactly the final unit slots');
    }
    result.units.forEach((unit, index) => {
      const finalSlot = finalWorld.slots.find(
        (slot) => unitValue(slot) === unitValue(unit.slot),
      );
      const finalLife = finalWorld.activeLives.find(
        (life) => unitValue(life.actorId) === unitValue(unit.slot),
      ) ?? null;
      if (
        JSON.stringify(unit.slot) !== JSON.stringify(finalSlot) ||
        JSON.stringify(unit.activeLife) !== JSON.stringify(finalLife)
      ) {
        fail(
          `replay.result.units[${index}]`,
          'must match the final authoritative slot and active life',
        );
      }
    });
    result.standings.teams.forEach((standing, index) => {
      const finalScore = finalWorld.scoreboard.teams.find(
        (team) => team.teamId === standing.teamId,
      );
      if (
        !finalScore ||
        JSON.stringify(standing.scores) !== JSON.stringify(finalScore.scores)
      ) {
        fail(
          `replay.result.standings.teams[${index}].scores`,
          'must match the final scoreboard',
        );
      }
    });
    const noTicksExecuted = document.ticks.length === 0;
    if ((result.endTick === null) !== noTicksExecuted) {
      fail(
        'replay.result.endTick',
        'must be null exactly when no joint tick executed',
      );
    }
    if (
      result.endTick !== null &&
      result.endTick !== document.ticks.at(-1)?.tick
    ) {
      fail('replay.result.endTick', 'must identify the last replay tick');
    }
    if (
      result.mode.kind !== finalWorld.mode.kind ||
      result.mode.reason !== result.completionReason
    ) {
      fail('replay.result.mode', 'must match completion reason and final mode');
    }
    if (result.mode.kind !== 'arc-relay') {
      const modeScores = result.mode.scores;
      ensureUnique(
        modeScores.map((score) => score.teamId),
        (teamId) => teamId,
        'replay.result.mode.scores',
        fail,
      );
      modeScores.forEach((score, index) => {
        if (
          index > 0 &&
          score.teamId <= modeScores[index - 1]!.teamId
        ) {
          fail(
            `replay.result.mode.scores[${index}].teamId`,
            'must be in canonical ascending team order',
          );
        }
      });
      if (
        !sameSet(
          modeScores.map((score) => String(score.teamId)),
          topology.teams.map((team) => String(team.teamId)),
        )
      ) {
        fail('replay.result.mode.scores', 'must cover exactly topology teams');
      }
    }
    if (
      result.mode.kind === 'deathmatch' &&
      finalWorld.mode.kind === 'deathmatch' &&
      contract.rules.gameMode.kind === 'deathmatch'
    ) {
      const terminalScores = new Map(
        result.mode.scores.map((score) => [score.teamId, score]),
      );
      const finalScores = new Map(
        finalWorld.scoreboard.teams.map((team) => [team.teamId, team]),
      );
      result.mode.scores.forEach((score, index) => {
        const finalTeam = finalScores.get(score.teamId);
        for (const [field, channel] of [
          ['kills', 'kills'],
          ['deaths', 'deaths'],
          ['damageDealt', 'damage-dealt'],
        ] as const) {
          const finalValue = finalTeam?.scores.find(
            (value) => value.channel === channel,
          );
          if (finalValue && score[field] !== finalValue.value) {
            fail(
              `replay.result.mode.scores[${index}].${field}`,
              `must match the final ${channel} scoreboard value`,
            );
          }
        }
      });

      const mode = contract.rules.gameMode;
      mode.victory.timeoutRanking.forEach((ranking, index) => {
        if (
          !mode.scoreCatalog.some(
            (score) => score.channel === ranking.channel,
          )
        ) {
          fail(
            `replay.header.contract.rules.gameMode.victory.timeoutRanking[${index}].channel`,
            'must reference a declared score channel',
          );
        }
      });
      const scoreValue = (teamId: number, channel: string): bigint => {
        const value = finalScores
          .get(teamId)
          ?.scores.find((score) => score.channel === channel);
        if (!value) {
          fail(
            'replay.result.standings',
            `cannot rank missing score channel ${channel}`,
          );
        }
        return BigInt(value.value);
      };
      const kills = (teamId: number): bigint =>
        BigInt(terminalScores.get(teamId)!.kills);
      const compareTimeout = (left: number, right: number): number => {
        for (const ranking of mode.victory.timeoutRanking) {
          const leftValue = scoreValue(left, ranking.channel);
          const rightValue = scoreValue(right, ranking.channel);
          if (leftValue === rightValue) continue;
          const comparison = leftValue < rightValue ? -1 : 1;
          return ranking.direction === 'higher-wins'
            ? -comparison
            : comparison;
        }
        return 0;
      };
      const compareKills = (left: number, right: number): number => {
        const leftKills = kills(left);
        const rightKills = kills(right);
        return leftKills === rightKills
          ? 0
          : leftKills > rightKills
            ? -1
            : 1;
      };

      const eligible = new Set(result.eligibleTeamIds);
      const killLimitReached =
        mode.victory.killsToWin !== null &&
        [...eligible].some(
          (teamId) =>
            kills(teamId) >= BigInt(mode.victory.killsToWin!),
        );
      let authoritativeComparison:
        | typeof compareTimeout
        | typeof compareKills;
      if (result.mode.reason === 'fault-eligibility') {
        if (eligible.size > 1) {
          fail(
            'replay.result.mode.reason',
            'fault eligibility requires at most one eligible team',
          );
        }
        authoritativeComparison = compareTimeout;
      } else if (result.mode.reason === 'kill-limit') {
        if (
          eligible.size <= 1 ||
          mode.victory.killsToWin === null ||
          !killLimitReached
        ) {
          fail(
            'replay.result.mode.reason',
            'kill-limit requires multiple eligible teams and a configured reached kill threshold',
          );
        }
        authoritativeComparison = compareKills;
      } else {
        if (
          eligible.size <= 1 ||
          result.endTick !== contract.rules.limits.maxTicks - 1 ||
          killLimitReached
        ) {
          fail(
            'replay.result.mode.reason',
            'max-ticks requires multiple eligible teams at the configured boundary with no reached kill limit',
          );
        }
        authoritativeComparison = compareTimeout;
      }

      const rankedEligible = [...eligible].sort(
        (left, right) =>
          authoritativeComparison(left, right) || left - right,
      );
      const expectedRanks = new Map<number, number>();
      rankedEligible.forEach((teamId, index) => {
        const previous = rankedEligible[index - 1];
        expectedRanks.set(
          teamId,
          previous !== undefined &&
            authoritativeComparison(previous, teamId) === 0
            ? expectedRanks.get(previous)!
            : index + 1,
        );
      });
      const ineligibleRank = rankedEligible.length + 1;
      topology.teams.forEach((team) => {
        if (!eligible.has(team.teamId)) {
          expectedRanks.set(team.teamId, ineligibleRank);
        }
      });
      const topCount = [...expectedRanks.values()].filter(
        (rank) => rank === 1,
      ).length;
      const expectedStandings = topology.teams
        .map((team) => {
          const rank = expectedRanks.get(team.teamId)!;
          return {
            teamId: team.teamId,
            rank,
            outcome:
              rank === 1
                ? topCount === 1
                  ? 'win'
                  : 'draw'
                : 'loss',
          };
        })
        .sort(
          (left, right) =>
            left.rank - right.rank || left.teamId - right.teamId,
        );
      result.standings.teams.forEach((standing, index) => {
        const expected = expectedStandings[index];
        if (
          !expected ||
          standing.teamId !== expected.teamId ||
          standing.rank !== expected.rank ||
          standing.outcome !== expected.outcome
        ) {
          fail(
            `replay.result.standings.teams[${index}]`,
            'does not follow deathmatch eligibility and victory ranking',
          );
        }
      });
      const expectedWinner =
        topCount === 1
          ? [...expectedRanks].find(([, rank]) => rank === 1)?.[0] ?? null
          : null;
      if (result.standings.winnerTeamId !== expectedWinner) {
        fail(
          'replay.result.standings.winnerTeamId',
          'must match the resolved deathmatch standings',
        );
      }
    }
    if (
      result.mode.kind === 'frontline' &&
      finalWorld.mode.kind === 'frontline' &&
      contract.rules.gameMode.kind === 'frontline' &&
      contract.modeMapBinding.kind === 'frontline'
    ) {
      if (
        JSON.stringify(result.mode.control) !==
        JSON.stringify(finalWorld.mode)
      ) {
        fail(
          'replay.result.mode.control',
          'must exactly match final authoritative frontline control',
        );
      }
      result.mode.scores.forEach((score, index) => {
        const finalTeam = finalWorld.scoreboard.teams.find(
          (team) => team.teamId === score.teamId,
        );
        const finalTerritorial = finalTeam?.scores.find(
          (value) => value.channel === 'territorial-progress',
        );
        if (
          !finalTerritorial ||
          score.territorialProgress !== finalTerritorial.value
        ) {
          fail(
            `replay.result.mode.scores[${index}].territorialProgress`,
            'must match the final territorial-progress scoreboard value',
          );
        }
      });

      const eligible = new Set(result.eligibleTeamIds);
      const mode = contract.rules.gameMode;
      const binding = contract.modeMapBinding;
      let breachWinner: number | null = null;
      if (result.mode.reason === 'fault-eligibility') {
        if (
          eligible.size > 1 ||
          result.endTick === null ||
          result.endTick >= contract.rules.limits.maxTicks
        ) {
          fail(
            'replay.result.mode.reason',
            'fault eligibility requires at most one eligible team before the configured maximum tick boundary',
          );
        }
      } else if (result.mode.reason === 'max-ticks') {
        if (
          eligible.size <= 1 ||
          finalWorld.nextTick !== contract.rules.limits.maxTicks
        ) {
          fail(
            'replay.result.mode.reason',
            'max-ticks requires multiple eligible teams at the configured maximum tick boundary',
          );
        }
      } else {
        if (
          eligible.size <= 1 ||
          result.endTick === null ||
          result.endTick >= contract.rules.limits.maxTicks
        ) {
          fail(
            'replay.result.mode.reason',
            'base breach requires multiple eligible teams before the configured maximum tick boundary',
          );
        }
        const requiredDelta =
          finalWorld.mode.activePositionIndex === 0
            ? -1
            : finalWorld.mode.activePositionIndex ===
                mode.frontlinePositionCount - 1
              ? 1
              : 0;
        breachWinner =
          binding.teamAdvances.find(
            (advance) => advance.objectiveIndexDelta === requiredDelta,
          )?.teamId ?? null;
        if (
          requiredDelta === 0 ||
          breachWinner === null ||
          !eligible.has(breachWinner) ||
          finalWorld.mode.claimingTeamId !== null ||
          finalWorld.mode.captureProgress !== 0 ||
          finalWorld.mode.decayTicksElapsed !== 0 ||
          finalWorld.mode.controlResumesAtTick > finalWorld.nextTick
        ) {
          fail(
            'replay.result.mode.control',
            'does not describe a valid terminal base breach',
          );
        }
      }

      const territorialScores = new Map(
        result.mode.scores.map((score) => [
          score.teamId,
          BigInt(score.territorialProgress),
        ]),
      );
      const rankedEligible = [...eligible].sort((left, right) => {
        if (breachWinner !== null) {
          return left === breachWinner
            ? -1
            : right === breachWinner
              ? 1
              : left - right;
        }
        const scoreComparison =
          (territorialScores.get(right) ?? 0n) -
          (territorialScores.get(left) ?? 0n);
        return scoreComparison < 0n
          ? -1
          : scoreComparison > 0n
            ? 1
            : left - right;
      });
      const expectedRanks = new Map<number, number>();
      rankedEligible.forEach((teamId, index) => {
        const previous = rankedEligible[index - 1];
        const tiedWithPrevious =
          previous !== undefined &&
          breachWinner === null &&
          territorialScores.get(previous) === territorialScores.get(teamId);
        expectedRanks.set(
          teamId,
          tiedWithPrevious
            ? expectedRanks.get(previous)!
            : index + 1,
        );
      });
      const ineligibleRank = rankedEligible.length + 1;
      topology.teams.forEach((team) => {
        if (!eligible.has(team.teamId)) {
          expectedRanks.set(team.teamId, ineligibleRank);
        }
      });
      const topCount = [...expectedRanks.values()].filter(
        (rank) => rank === 1,
      ).length;
      const expectedStandings = topology.teams
        .map((team) => {
          const rank = expectedRanks.get(team.teamId)!;
          return {
            teamId: team.teamId,
            rank,
            outcome:
              rank === 1
                ? topCount === 1
                  ? 'win'
                  : 'draw'
                : 'loss',
          };
        })
        .sort(
          (left, right) =>
            left.rank - right.rank || left.teamId - right.teamId,
        );
      result.standings.teams.forEach((standing, index) => {
        const expected = expectedStandings[index];
        if (
          !expected ||
          standing.teamId !== expected.teamId ||
          standing.rank !== expected.rank ||
          standing.outcome !== expected.outcome
        ) {
          fail(
            `replay.result.standings.teams[${index}]`,
            'does not follow frontline eligibility and victory ranking',
          );
        }
      });
      const expectedWinner =
        topCount === 1
          ? [...expectedRanks].find(([, rank]) => rank === 1)?.[0] ?? null
          : null;
      if (result.standings.winnerTeamId !== expectedWinner) {
        fail(
          'replay.result.standings.winnerTeamId',
          'must match the resolved frontline standings',
        );
      }
    }
    if (
      result.mode.kind === 'arc-relay' &&
      finalWorld.mode.kind === 'arc-relay' &&
      contract.rules.gameMode.kind === 'arc-relay'
    ) {
      if (JSON.stringify(result.mode.state) !== JSON.stringify(finalWorld.mode)) {
        fail(
          'replay.result.mode.state',
          'must exactly match final authoritative Arc Relay state',
        );
      }
      if (
        result.mode.reason === 'max-ticks' &&
        finalWorld.nextTick !== contract.rules.limits.maxTicks
      ) {
        fail(
          'replay.result.mode.reason',
          'max-ticks requires the configured maximum tick boundary',
        );
      }
      if (
        result.mode.reason === 'reactor-destroyed' &&
        !finalWorld.mode.reactors.some((reactor) => reactor.integritySegments === 0)
      ) {
        fail(
          'replay.result.mode.reason',
          'reactor-destroyed requires a reactor with no integrity segments',
        );
      }
      if (
        result.mode.reason === 'fault-eligibility' &&
        result.eligibleTeamIds.length > 1
      ) {
        fail(
          'replay.result.mode.reason',
          'fault eligibility requires at most one eligible team',
        );
      }
    }
  }
}

function compareUnit(
  left: { teamId: number; unitId: number },
  right: { teamId: number; unitId: number },
): number {
  return left.teamId - right.teamId || left.unitId - right.unitId;
}

function compareActor(
  left: V3.ReplayV3ActorId,
  right: V3.ReplayV3ActorId,
): number {
  return compareUnit(left, right) || left.lifeId - right.lifeId;
}

function copyPosition(value: V3.ReplayV3Position): Model.ReplayPosition {
  return { x: value.x, y: value.y };
}

function positionFromTuple(
  value: V3.ReplayV3ContractPosition,
): Model.ReplayPosition {
  return { x: value[0], y: value[1] };
}

function genericUnitKey(
  teamId: number,
  unitId: number,
): Model.ReplayStableUnitKey {
  return replayGenericIdentity(teamId, unitId, 0).unitKey;
}

function identity(
  value: V3.ReplayV3ActorId,
): Model.ReplayGenericActorIdentity {
  return replayGenericIdentity(value.teamId, value.unitId, value.lifeId);
}

function frontlineMapFromV3(
  contract: V3.ReplayV3ResolvedContract,
): Model.ReplayFrontlineMap | null {
  if (
    contract.rules.gameMode.kind !== 'frontline' ||
    contract.modeMapBinding.kind !== 'frontline'
  ) {
    return null;
  }
  const regions = new Map(
    contract.map.regions.map((region) => [region.regionId, region]),
  );
  return {
    positions: contract.modeMapBinding.orderedObjectiveRegionIds.map(
      (regionId, positionIndex) => ({
        positionIndex,
        tiles: regions.get(regionId)!.tiles.map(positionFromTuple),
      }),
    ),
    // Generation-3 maps express deployment and transition placement through
    // named spawns/regions rather than the old Frontline-specific home bag.
    teamHomes: [],
    anchorForbiddenTiles: [],
  };
}

export function normalizeReplayV3(
  document: V3.ReplayV3Document,
): Model.ReplayModel {
  const { contract } = document.header;
  const provenance = new Map(
    document.header.provenance?.participants.map((participant) => [
      participant.participantId,
      participant,
    ]) ?? [],
  );
  const participants = [...contract.topology.participants]
    .sort((left, right) => left.participantId - right.participantId)
    .map<Model.ReplayParticipantController>((participant) => {
      const source = provenance.get(participant.participantId);
      return {
        participantKey: replayParticipantKey(participant.participantId),
        participantId: participant.participantId,
        teamKey: replayTeamKey(participant.teamId),
        teamId: participant.teamId,
        classId: participant.classId ?? null,
        name: source?.name ?? `participant ${participant.participantId}`,
        runtimeKind: source?.runtimeKind ?? 'unknown',
        artifactHash: source?.artifactHash ?? null,
        accent: source?.accent ?? '#94a3b8',
        lookId: source?.lookId ?? null,
        projectileLookId: source?.projectileLookId ?? null,
      };
    });
  const initialLifeByUnit = new Map(
    contract.topology.initialLives.map((life) => [unitValue(life), life]),
  );
  const units = [...contract.topology.unitSlots]
    .sort(compareUnit)
    .map<Model.ReplayStableUnit>((slot) => {
      const initialLife = initialLifeByUnit.get(unitValue(slot));
      const initialActor = initialLife ? identity(initialLife) : null;
      return {
        unitKey: genericUnitKey(slot.teamId, slot.unitId),
        teamKey: replayTeamKey(slot.teamId),
        teamId: slot.teamId,
        unitId: slot.unitId,
        controllerParticipantKey: replayParticipantKey(
          slot.controllerParticipantId,
        ),
        controllerParticipantId: slot.controllerParticipantId,
        initialActorKey: initialActor?.actorKey ?? null,
        initialLifeId: initialLife?.lifeId ?? null,
        initialFormId: initialLife?.formId ?? null,
        classId: slot.classId ?? null,
      };
    });
  const teams = [...contract.topology.teams]
    .sort((left, right) => left.teamId - right.teamId)
    .map<Model.ReplayTeam>((team) => ({
      teamKey: replayTeamKey(team.teamId),
      teamId: team.teamId,
      classId: team.classId ?? null,
      participantKeys: participants
        .filter((participant) => participant.teamId === team.teamId)
        .map((participant) => participant.participantKey),
      unitKeys: units
        .filter((unit) => unit.teamId === team.teamId)
        .map((unit) => unit.unitKey),
    }));
  const formPresentation = new Map(
    document.header.presentation?.forms.map((form) => [form.formId, form]) ??
      [],
  );
  const movementProfiles = new Map(
    contract.rules.movementProfiles.map((profile) => [profile.id, profile]),
  );
  const visionProfiles = new Map(
    contract.rules.visionProfiles.map((profile) => [profile.id, profile]),
  );
  const attackProfiles = new Map(
    contract.rules.attackProfiles.map((profile) => [profile.id, profile]),
  );
  const actionKinds = new Map(
    contract.rules.actions.map((action) => [action.id, action.kind]),
  );
  const forms = [...contract.rules.forms]
    .sort((left, right) => left.id.localeCompare(right.id))
    .map<Model.ReplayForm>((form) => {
      const movement = form.movementProfileId
        ? movementProfiles.get(form.movementProfileId)
        : undefined;
      const vision = visionProfiles.get(form.visionProfileId);
      const attack = form.attackProfileId
        ? attackProfiles.get(form.attackProfileId)
        : undefined;
      const presentation = formPresentation.get(form.id);
      const canMove =
        movement !== undefined &&
        form.allowedActionIds.some(
          (actionId) => actionKinds.get(actionId) === 'movement',
        );
      const canShoot =
        attack !== undefined &&
        form.allowedActionIds.some(
          (actionId) => actionKinds.get(actionId) === 'attack',
        );
      return {
        formId: form.id,
        maxHealth: form.maxHealth,
        visionRange: vision?.range ?? 0,
        shootCooldownTicks: attack?.cooldownTicks ?? null,
        omnidirectionalVision: vision?.shape === 'omnidirectional',
        omnidirectionalShooting: attack?.omnidirectionalAim ?? false,
        movementLayer: movement?.movementLayer ?? 'none',
        objectiveWeight: form.objectiveWeight,
        canMove,
        canShoot,
        allowsProgrammedShots:
          canShoot && (attack?.shotProgram.enabled ?? false),
        allowedActionIds: [...form.allowedActionIds],
        lookId: presentation?.lookId ?? null,
        projectileLookId: presentation?.projectileLookId ?? null,
        completeness: 'exact',
      };
    });
  const ticks = document.ticks.map((tick) => tickFromV3(tick, document));

  return {
    sourceVersion: 3,
    versions: {
      engineVersion: document.header.engineVersion,
      gameRulesVersion: document.header.gameRulesVersion,
      runtimeProtocolVersion: document.header.runtime.protocolVersion,
      runtimeConfigurationVersion:
        document.header.runtime.configurationVersion,
      actorRuntime: {
        family: document.header.runtime.contractProfileId,
        protocolVersion: document.header.runtime.protocolVersion,
        configurationVersion: document.header.runtime.configurationVersion,
        version: document.header.runtime.runtimeContractVersion,
        matchStartSchemaVersion:
          document.header.runtime.matchStartSchemaVersion,
        observationSchemaVersion:
          document.header.runtime.observationSchemaVersion,
        decisionSchemaVersion: document.header.runtime.decisionSchemaVersion,
      },
    },
    seed: document.header.seed,
    seedExact: true,
    seedEncoding: 'decimal-string',
    partial: document.partial,
    replayHash: document.replayHash,
    matchContractFingerprint: contract.matchContractFingerprint,
    contract: contractFromV3(contract),
    map: mapFromV3(document.header),
    forms,
    participants,
    teams,
    units,
    initialWorld: worldFromV3(document.initialFrame.state, document),
    initialLifeStarts: document.initialFrame.lifeStarts.map(lifeStartFromV3),
    initialEvents: document.initialFrame.events.map(eventFromV3),
    ticks,
    result: document.result ? resultFromV3(document.result, document) : null,
  };
}

function contractFromV3(
  contract: V3.ReplayV3ResolvedContract,
): Model.ReplayGenericMatchContract {
  const topology = contract.topology;
  const firstAttack = contract.rules.attackProfiles[0];
  const firstVision = contract.rules.visionProfiles[0];
  const actionKinds = new Map(
    contract.rules.actions.map((action) => [action.id, action.kind]),
  );
  const shot = firstAttack?.shotProgram;
  const shotRecord = (shot ?? {}) as Record<string, unknown>;
  const aimOnly = (shotRecord.aimOnlyProgram ?? {}) as Record<string, unknown>;
  const defaultProgram = shot?.defaultProgram ?? {
    initialAimOffset: 0,
    bendDirection: 0,
    bendAfterTiles: 0,
    bendEveryTiles: 1,
    bendCount: 0,
  };
  const collisions = contract.rules.collisions;
  const bool = (key: string, fallback: boolean) =>
    typeof collisions[key] === 'boolean'
      ? (collisions[key] as boolean)
      : fallback;
  const spawnsById = new Map(
    contract.initialDeployment.spawns.map((spawn) => [spawn.spawnId, spawn]),
  );
  const frontlineMap = frontlineMapFromV3(contract);
  const mode: Model.ReplayGenericModeDefinition = (() => {
    if (contract.rules.gameMode.kind === 'deathmatch') {
      return {
        kind: 'deathmatch',
        modeId: contract.rules.gameMode.modeId,
      };
    }
    if (contract.rules.gameMode.kind === 'arc-relay') {
      if (contract.modeMapBinding.kind !== 'arc-relay') {
        throw new Error('validated replay-v3 lost its Arc Relay map binding');
      }
      return {
        kind: 'arc-relay',
        modeId: contract.rules.gameMode.modeId,
        pendingRearmTicks: contract.rules.gameMode.pendingRearmTicks,
        coreRelocationIntervalTicks:
          contract.rules.gameMode.coreRelocationIntervalTicks,
        coresPerPulse: contract.rules.gameMode.coresPerPulse,
        pulsesToDestroyReactor:
          contract.rules.gameMode.victory.pulsesToDestroyReactor,
        orderedWellRegionIds: [
          ...contract.modeMapBinding.orderedWellRegionIds,
        ],
      };
    }
    if (contract.modeMapBinding.kind !== 'frontline') {
      throw new Error('validated replay-v3 lost its frontline map binding');
    }
    return {
      kind: 'frontline',
      modeId: contract.rules.gameMode.modeId,
      frontlinePositionCount:
        contract.rules.gameMode.frontlinePositionCount,
      pushesToBreach:
        contract.rules.gameMode.victory.pushesToBreach,
      capture: {
        threshold: contract.rules.gameMode.capture.threshold,
        gainPerSoleTeamTick:
          contract.rules.gameMode.capture.gainPerSoleTeamTick,
        ...(contract.rules.gameMode.capture.gainSchedule
          ? {
              gainSchedule:
                contract.rules.gameMode.capture.gainSchedule.map(
                  (phase) => ({ ...phase }),
                ),
            }
          : {}),
        decayAmount: contract.rules.gameMode.capture.decayAmount,
        decayIntervalTicks:
          contract.rules.gameMode.capture.decayIntervalTicks,
        redeployPauseTicks:
          contract.rules.gameMode.capture.redeployPauseTicks,
      },
      orderedObjectiveRegionIds: [
        ...contract.modeMapBinding.orderedObjectiveRegionIds,
      ],
      teamAdvances: contract.modeMapBinding.teamAdvances.map(
        (advance) => ({
          teamId: advance.teamId,
          positionIndexDelta: advance.objectiveIndexDelta as -1 | 1,
        }),
      ),
    };
  })();
  return {
    kind: 'v3-generic',
    completeness: 'exact',
    schemaVersion: contract.schemaVersion,
    matchContractFingerprint: contract.matchContractFingerprint,
    modeKind: contract.rules.gameMode.kind,
    modeId: contract.rules.gameMode.modeId,
    mode,
    rawContract: contract,
    rules: {
      schemaVersion: contract.rules.schemaVersion,
      rulesetId: contract.rules.rulesetId,
      rulesFingerprint: contract.rules.rulesFingerprint,
      limits: {
        maxTicks: Number(contract.rules.limits.maxTicks),
        faultLimit: Number(
          ((contract.rules.limits.runtimeFaults ?? {}) as Record<string, unknown>)
            .faultsAllowedBeforeDisqualification ?? 0,
        ),
        teamCount: topology.counts.teamCount,
        participantCount: topology.counts.participantCount,
        unitSlotCount: topology.counts.unitSlotCount,
        initialUnitsPerTeam: Math.min(
          ...topology.teams.map(
            (team) =>
              topology.initialLives.filter((life) => life.teamId === team.teamId)
                .length,
          ),
        ),
        maxUnitsPerTeam: Math.max(
          ...topology.teams.map(
            (team) =>
              topology.unitSlots.filter((slot) => slot.teamId === team.teamId)
                .length,
          ),
        ),
        destructionEndsMatch: false,
        respawnsEnabled: true,
      },
      objective: {
        mode: contract.rules.gameMode.kind === 'frontline' ? 'frontline' : 'none',
        zoneControlEnabled: contract.rules.gameMode.kind === 'frontline',
        zoneDominationTicks: 0,
        zoneExclusiveAccrual: false,
        sharedPressureEnabled: false,
        controlBySoleOccupancy: false,
        controlPressureLimit: 0,
        controlPressureGain: 0,
        controlPressureDecayInterval: 0,
        overtime: {
          startTick: 0,
          pressureLimit: 0,
          pressureGain: 0,
          stopsDecay: false,
        },
        maxTickTiebreakers: [],
      },
      frontlineDefinition: null,
      energy: {
        enabled: (firstAttack?.maxEnergy ?? 0) > 0,
        maxEnergy: firstAttack?.maxEnergy ?? 0,
        shotEnergyCost: firstAttack?.attackEnergyCost ?? 0,
        regenerationIntervalTicks:
          firstAttack?.energyRegenerationIntervalTicks ?? 0,
        regenerationAmount: firstAttack?.energyRegenerationAmount ?? 0,
      },
      forms: contract.rules.forms.map((form) => {
        const movement = contract.rules.movementProfiles.find(
          (profile) => profile.id === form.movementProfileId,
        );
        const vision = contract.rules.visionProfiles.find(
          (profile) => profile.id === form.visionProfileId,
        );
        const attack = contract.rules.attackProfiles.find(
          (profile) => profile.id === form.attackProfileId,
        );
        const canMove =
          movement !== undefined &&
          form.allowedActionIds.some(
            (actionId) => actionKinds.get(actionId) === 'movement',
          );
        const canShoot =
          attack !== undefined &&
          form.allowedActionIds.some(
            (actionId) => actionKinds.get(actionId) === 'attack',
          );
        return {
          id: form.id,
          maxHealth: form.maxHealth,
          visionRange: vision?.range ?? 0,
          shootCooldownTicks: attack?.cooldownTicks ?? 0,
          omnidirectionalVision: vision?.shape === 'omnidirectional',
          omnidirectionalShooting: attack?.omnidirectionalAim ?? false,
          movementLayer: movement?.movementLayer ?? 'none',
          objectiveWeight: form.objectiveWeight,
          canMove,
          canShoot,
          allowsProgrammedShots:
            canShoot && (attack?.shotProgram.enabled ?? false),
          allowedActionIds: [...form.allowedActionIds],
        };
      }),
      actions: contract.rules.actions.map((action) => ({
        id: action.id,
        code: action.code,
        kind: action.kind,
        parameterKinds: [...action.parameterKinds] as Model.ReplayActionParameterKind[],
        enabled: true,
      })),
      projectiles: {
        mode:
          firstAttack?.projectile.mode === 'instant-ray'
            ? 'instant-ray'
            : 'discrete',
        damagePerHit: firstAttack?.projectile.damagePerHit ?? 0,
        maxTravelTiles: firstAttack?.projectile.maxTravelTiles ?? 0,
        shootCooldownTicks: firstAttack?.cooldownTicks ?? 0,
        ticksPerAdvance: firstAttack?.projectile.ticksPerAdvance ?? 0,
        tilesPerAdvance: firstAttack?.projectile.tilesPerAdvance ?? 0,
        launchTiles: firstAttack?.projectile.launchTiles ?? 0,
        advancesOnLaunchTick:
          firstAttack?.projectile.advancesOnLaunchTick ?? false,
        damageAppliedSimultaneously:
          firstAttack?.projectile.damageAppliedSimultaneously ?? false,
      },
      shotPrograms: {
        enabled: shot?.enabled ?? false,
        headingSectors: shot?.headingSectors ?? 8,
        bendStepOctants: Number(shotRecord.bendStepSectors ?? 1),
        minInitialAimOctants: shot?.minInitialAimSteps ?? 0,
        maxInitialAimOctants: shot?.maxInitialAimSteps ?? 0,
        aimOnlyProgram: {
          bendDirection: Number(aimOnly.bendDirection ?? 0),
          bendAfterTiles: Number(aimOnly.bendAfterTiles ?? 0),
          bendEveryTiles: Number(aimOnly.bendEveryTiles ?? 1),
          bendCount: Number(aimOnly.bendCount ?? 0),
        },
        allowedCurvedBendDirections: [
          ...((shotRecord.allowedCurvedBendDirections as number[] | undefined) ??
            []),
        ],
        minBendAfterTiles: Number(shotRecord.minBendAfterTiles ?? 0),
        maxBendAfterTiles: Number(shotRecord.maxBendAfterTiles ?? 0),
        minBendEveryTiles: Number(shotRecord.minBendEveryTiles ?? 1),
        maxBendEveryTiles: Number(shotRecord.maxBendEveryTiles ?? 1),
        minBendCount: Number(shotRecord.minBendCount ?? 0),
        maxBendCount: Number(shotRecord.maxBendCount ?? 0),
        launchTiles: shot?.launchTiles ?? 0,
        payloadOptional: shot?.payloadOptional ?? false,
        defaultProgram: { ...defaultProgram },
        invalidPayloadResult:
          shotRecord.invalidPayloadResult === 'blocked' ||
          shotRecord.invalidPayloadResult === 'faulted' ||
          shotRecord.invalidPayloadResult === 'rejected'
            ? shotRecord.invalidPayloadResult
            : null,
        unsupportedPayloadResult:
          shotRecord.unsupportedPayloadResult === 'faulted' ||
          shotRecord.unsupportedPayloadResult === 'rejected'
            ? shotRecord.unsupportedPayloadResult
            : 'blocked',
        diagonalCornersMustBeClear:
          shot?.diagonalCornersMustBeClear ?? false,
      },
      vision: {
        range: firstVision?.range ?? 0,
        distanceMetric: 'chebyshev',
        shape:
          firstVision?.shape === 'facing-quadrant'
            ? 'facing-quadrant'
            : 'omnidirectional',
        omnidirectionalProximityRange:
          firstVision?.omnidirectionalProximityRange ?? 0,
        lineOfSight: 'corner-strict-supercover',
        hearingRadius: firstVision?.hearingRadius ?? 0,
        hearingBearingSectors: firstVision?.hearingBearingSectors ?? 0,
        hearingDistanceBandUpperBounds: [
          ...(firstVision?.hearingDistanceBandUpperBounds ?? []),
        ],
        loudEventTypes: [...(firstVision?.loudEventKinds ?? [])],
      },
      collisions: {
        unitsBlockWalls: bool('actorsBlockWalls', true),
        unitsBlockUnits: bool('actorsBlockActors', true),
        sameDestinationMovesBlockAll: bool('sameDestinationMovesBlockAll', true),
        swapMovesBlocked: bool('swapMovesBlocked', true),
        followingVacatedUnitAllowed: bool('followingVacatedActorAllowed', false),
        projectilesBlockMovement: bool('projectilesBlockMovement', true),
        movingOntoProjectileCausesHit: bool(
          'movingOntoProjectileCausesHit',
          true,
        ),
        wallsConsumeProjectiles: bool('wallsConsumeProjectiles', true),
        projectilesIgnoreOwner: bool('projectilesIgnoreFiringLife', true),
        projectilesStopOnFirstNonOwnerUnit: bool(
          'projectilesStopOnFirstEnemyActor',
          true,
        ),
        projectilesCollideWithProjectiles: bool(
          'projectilesCollideWithProjectiles',
          false,
        ),
      },
      tickResolution: {
        observationsUsePreTickState:
          contract.rules.tickResolution.observationsUsePreTickState === true,
        decisionsResolveAsJointStep:
          contract.rules.tickResolution.decisionsResolveAsJointStep === true,
        phases: [...contract.rules.tickResolution.phases],
      },
    },
    map: {
      schemaVersion: contract.map.schemaVersion,
      mapId: contract.map.mapId,
      mapVersion: contract.map.mapVersion,
      mapFingerprint: contract.map.mapFingerprint,
      formatVersion: contract.map.formatVersion,
      width: contract.map.width,
      height: contract.map.height,
      tileRows: [...contract.map.tileRows],
      spawns: contract.initialDeployment.lives
        .map((life) => {
          const spawn = spawnsById.get(life.spawnId);
          return spawn
            ? {
                teamId: life.teamId,
                position: positionFromTuple(spawn.position),
                facing: spawn.facing,
              }
            : null;
        })
        .filter((spawn): spawn is NonNullable<typeof spawn> => spawn !== null),
      objectiveTiles:
        frontlineMap?.positions.flatMap((position) =>
          position.tiles.map((tile) => ({ ...tile })),
        ) ?? [],
      frontline: frontlineMap,
    },
    topology: {
      teamCount: topology.counts.teamCount,
      participantCount: topology.counts.participantCount,
      unitSlotCount: topology.counts.unitSlotCount,
      initialLifeCount: topology.counts.initialLifeCount,
      teams: topology.teams.map((team) => ({
        teamId: team.teamId,
        teamKey: replayTeamKey(team.teamId),
        classId: team.classId ?? null,
      })),
      participants: topology.participants.map((participant) => ({
        participantId: participant.participantId,
        participantKey: replayParticipantKey(participant.participantId),
        teamId: participant.teamId,
        teamKey: replayTeamKey(participant.teamId),
        classId: participant.classId ?? null,
      })),
      unitSlots: topology.unitSlots.map((slot) => ({
        teamId: slot.teamId,
        teamKey: replayTeamKey(slot.teamId),
        unitId: slot.unitId,
        unitKey: genericUnitKey(slot.teamId, slot.unitId),
        controllerParticipantId: slot.controllerParticipantId,
        controllerParticipantKey: replayParticipantKey(
          slot.controllerParticipantId,
        ),
      })),
      initialLives: topology.initialLives.map((life) => {
        const actor = identity(life);
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

function mapFromV3(header: V3.ReplayV3Header): Model.ReplayMap {
  const { contract } = header;
  const map = contract.map;
  const frontline = frontlineMapFromV3(contract);
  return {
    mapId: map.mapId,
    mapVersion: map.mapVersion,
    formatVersion: map.formatVersion,
    width: map.width,
    height: map.height,
    tileRows: [...map.tileRows],
    objectiveTiles:
      frontline?.positions.flatMap((position) =>
        position.tiles.map((tile) => ({ ...tile })),
      ) ?? [],
    frontline,
    presentation: header.presentation
      ? {
          themeId: header.presentation.themeId,
          boundaryWall: header.presentation.map?.boundaryWall ?? null,
          interiorWall: header.presentation.map?.interiorWall ?? null,
          wallGroups:
            header.presentation.map?.wallGroups.map((group) => ({
              family: group.family,
              tiles: group.tiles.map(copyPosition),
            })) ?? null,
        }
      : null,
  };
}

function scoreValue(
  scores: readonly V3.ReplayV3ScoreValue[],
  channel: string,
): string | null {
  return scores.find((score) => score.channel === channel)?.value ?? null;
}

function scoreboardFromV3(
  scoreboard: V3.ReplayV3Scoreboard,
): Model.ReplayScoreboard {
  return {
    teams: scoreboard.teams.map((team) => ({
      teamKey: replayTeamKey(team.teamId),
      teamId: team.teamId,
      eligible: team.eligible,
      scores: team.scores.map((score) => ({ ...score })),
    })),
  };
}

/**
 * The team a signed side-objective claim belongs to: positive counts for team
 * 0 and negative for team 1, the direction the public team-advance ordering
 * uses. Zero is no claim at all.
 */
function secondaryClaimant(claimProgress: number): number | null {
  if (claimProgress === 0) {
    return null;
  }
  return claimProgress > 0 ? 0 : 1;
}

function modeFromV3(mode: V3.ReplayV3ModeState): Model.ReplayModeState {
  if (mode.kind === 'deathmatch')
    return { kind: 'deathmatch', modeId: mode.modeId };
  if (mode.kind === 'frontline')
    return {
        kind: 'frontline',
        modeId: mode.modeId,
        activePositionIndex: mode.activePositionIndex,
        claimingTeamId: mode.claimingTeamId,
        captureProgress: mode.captureProgress,
        decayTicksElapsed: mode.decayTicksElapsed,
        controlResumesAtTick: mode.controlResumesAtTick,
        holdOwnerTeamId: mode.holdOwnerTeamId,
        holdEndsAtTick: mode.holdEndsAtTick,
        secondaryOwnerTeamId: mode.secondaryOwnerTeamId,
        secondaryClaimProgress: mode.secondaryClaimProgress,
        // Spread rather than assign: the economy's collections are absent
        // on every ruleset without one, and an explicit `undefined` key is a
        // different shape from an omitted one.
        ...(mode.scrapTeams === undefined
          ? {}
          : {
              scrapTeams: mode.scrapTeams.map((team) => ({
                teamId: team.teamId,
                bank: team.bank,
                tierLevels: [...team.tierLevels],
              })),
            }),
        ...(mode.scrapPiles === undefined
          ? {}
          : {
              scrapPiles: mode.scrapPiles.map((pile) => ({
                position: { x: pile.position.x, y: pile.position.y },
                amount: pile.amount,
                expiresAtTick: pile.expiresAtTick,
              })),
            }),
      };
  return {
    kind: 'arc-relay',
    modeId: mode.modeId,
    wells: mode.wells.map((well) => ({
      ...well,
      position: copyPosition(well.position),
      outstandingCoreId: well.outstandingCoreId
        ? { ...well.outstandingCoreId }
        : null,
    })),
    reactors: mode.reactors.map((reactor) => ({
      teamId: reactor.teamId,
      position: copyPosition(reactor.position),
      chargePips: reactor.chargePips,
      integritySegments: reactor.integritySegments,
      filledSocketWellIds: [...(reactor.filledSocketWellIds ?? [])],
    })),
    visibleCores: mode.visibleCores.map((core) => ({
      coreId: { ...core.coreId },
      position: copyPosition(core.position),
      disposition: core.disposition,
      carrierActor: core.carrierActorId
        ? identity(core.carrierActorId)
        : null,
      nextRelocationTick: core.nextRelocationTick,
      flightTarget: core.flightTarget
        ? copyPosition(core.flightTarget)
        : null,
      flightCompletesAtTick: core.flightCompletesAtTick,
    })),
    visibleSignatures: mode.visibleSignatures.map((signature) => ({
      operationId: signature.operationId,
      signatureId: signature.signatureId,
      signatureKind: signature.signatureKind,
      ownerActor: identity(signature.ownerActorId),
      ownerTeamId: signature.ownerTeamId,
      phase: signature.phase,
      startedTick: signature.startedTick,
      completesAtTick: signature.completesAtTick,
      endsAtTick: signature.endsAtTick,
      positions: signature.positions.map(copyPosition),
      targetActor: signature.targetActorId
        ? identity(signature.targetActorId)
        : null,
      remainingCapacity: signature.remainingCapacity,
      suppressed: signature.suppressed,
    })),
    latestPulseTeamId: mode.latestPulseTeamId,
    latestPulseTick: mode.latestPulseTick,
  };
}

function objectiveFromV3(
  mode: V3.ReplayV3ModeState,
): Model.ReplayObjectiveState {
  if (mode.kind === 'frontline') {
    return {
      kind: 'frontline',
      nextTick: 0,
      activePositionIndex: mode.activePositionIndex,
      claimingTeamId: mode.claimingTeamId,
      captureProgress: mode.captureProgress,
      decayTicksElapsed: mode.decayTicksElapsed,
      controlResumesAtTick: mode.controlResumesAtTick,
      holdOwnerTeamId: mode.holdOwnerTeamId,
      holdEndsAtTick: mode.holdEndsAtTick,
      winnerTeamId: null,
      completeness: 'exact',
    };
  }
  return {
    kind: 'legacy',
    mode: 'none',
    controlPressure: null,
    zoneTicks: [],
    completeness: 'legacy-derived',
  };
}

function slotLifecycle(
  state: V3.ReplayV3UnitSlotState,
): Model.ReplayUnitLifecycleStatus {
  switch (state.kind) {
    case 'active':
      return 'active';
    case 'availability-pending':
      return 'locked';
    case 'automatic-return-pending':
      return 'respawning';
    case 'ready':
      return 'ready';
    case 'fabrication-pending':
      return 'fabrication-queued';
    case 'replication-pending':
      return 'replication-pending';
    case 'permanently-dormant':
      return 'locked';
  }
}

function defaultFormForSlot(
  slot: { teamId: number; unitId: number; state: V3.ReplayV3UnitSlotState },
  document: V3.ReplayV3Document,
): string {
  return (
    document.header.contract.topology.initialLives.find(
      (life) => unitValue(life) === unitValue(slot),
    )?.formId ??
    document.header.contract.lifecycleAssignments.find(
      (assignment) => unitValue(assignment) === unitValue(slot),
    )?.allowedFormIds[0] ??
    (slot.state.kind === 'active' ? slot.state.formId : null) ??
    document.header.contract.rules.forms[0]?.id ??
    ''
  );
}

function transitionFromV3(
  transition: V3.ReplayV3PendingSameLifeTransition | null,
  fromFormId: string,
): Model.ReplayFormTransition | null {
  return transition
    ? {
        fromFormId,
        toFormId: transition.targetFormId,
        startedAtTick: transition.startedTick,
        completesAtTick: transition.dueTick,
      }
    : null;
}

function worldFromV3(
  world: V3.ReplayV3WorldState,
  document: V3.ReplayV3Document,
): Model.ReplayWorldSnapshot {
  const actors = [...world.activeLives]
    .sort((left, right) => compareActor(left.actorId, right.actorId))
    .map<Model.ReplayActorState>((life) => {
      const actor = identity(life.actorId);
      return {
        identity: actor,
        actorKey: actor.actorKey,
        unitKey: actor.unitKey,
        formId: life.formId,
        position: copyPosition(life.position),
        facing: life.facing,
        health: life.health,
        cooldown: life.cooldown,
        energy: life.energy,
        damageDealt: null,
        previousActionResult:
          (life.previousActionResolution?.outcome as Model.ReplayActionResult) ??
          'none',
        spawnedAtTick: life.spawnedAtTick,
        participantId: life.participantId,
        generation: life.generation,
        spawnReason: life.spawnReason,
        parentActor: life.parentActorId ? identity(life.parentActorId) : null,
        sourceTransitionId: life.sourceTransitionId,
        sourceOperationId: life.sourceOperationId,
        pendingFormTransition: transitionFromV3(
          life.pendingSameLifeTransition,
          life.formId,
        ),
        status: 'active',
      };
    });
  const units = [...world.slots]
    .sort(compareUnit)
    .map<Model.ReplayUnitState>((slot) => {
      const activeActorId =
        slot.state.kind === 'active' ? slot.state.actorId : null;
      const active =
        activeActorId
          ? world.activeLives.find(
              (life) =>
                actorValue(life.actorId) === actorValue(activeActorId),
            )
          : undefined;
      const defaultFormId = defaultFormForSlot(slot, document);
      const due =
        'dueTick' in slot.state && typeof slot.state.dueTick === 'number'
          ? slot.state.dueTick
          : null;
      return {
        unitKey: genericUnitKey(slot.teamId, slot.unitId),
        teamKey: replayTeamKey(slot.teamId),
        teamId: slot.teamId,
        unitId: slot.unitId,
        defaultFormId,
        formId:
          active?.formId ??
          ('targetFormId' in slot.state ? slot.state.targetFormId : defaultFormId),
        lifecycleStatus: slotLifecycle(slot.state),
        respawnAtTick:
          slot.state.kind === 'automatic-return-pending' ? due : null,
        unlockAtTick:
          slot.state.kind === 'availability-pending' ? due : null,
        rebuildReadyAtTick: null,
        fabricationAtTick:
          slot.state.kind === 'fabrication-pending' ||
          slot.state.kind === 'replication-pending'
            ? due
            : null,
        reservedSpawn:
          slot.state.kind === 'fabrication-pending' ||
          slot.state.kind === 'replication-pending'
            ? copyPosition(slot.state.reservedPosition)
            : null,
        pendingSpawnReason:
          slot.state.kind === 'automatic-return-pending'
            ? 'respawn'
            : slot.state.kind === 'fabrication-pending'
              ? 'fabrication'
              : slot.state.kind === 'replication-pending'
                ? 'replication'
                : null,
        hasSpawned: slot.nextLifeId > 0,
        nextLifeId: slot.nextLifeId,
        damageDealt: null,
        activeActorKey:
          slot.state.kind === 'active'
            ? identity(slot.state.actorId).actorKey
            : null,
      };
    });
  const teams = [...document.header.contract.topology.teams]
    .sort((left, right) => left.teamId - right.teamId)
    .map<Model.ReplayTeamState>((team) => {
      const score = world.scoreboard.teams.find(
        (entry) => entry.teamId === team.teamId,
      );
      return {
        teamKey: replayTeamKey(team.teamId),
        teamId: team.teamId,
        damageDealt: score ? scoreValue(score.scores, 'damage-dealt') : null,
        unitKeys: units
          .filter((unit) => unit.teamId === team.teamId)
          .map((unit) => unit.unitKey),
      };
    });
  const objective = objectiveFromV3(world.mode);
  if (objective.kind === 'frontline') objective.nextTick = world.nextTick;
  return {
    completeness: 'exact',
    participants: world.participants.map((participant) => ({
      participantKey: replayParticipantKey(participant.participantId),
      participantId: participant.participantId,
      teamKey: replayTeamKey(participant.teamId),
      teamId: participant.teamId,
      classId: participant.classId ?? null,
      runtimeFaultCount: participant.runtimeFaultCount,
      disqualified: participant.disqualified,
    })),
    teams,
    units,
    actors,
    projectiles: [...world.projectiles]
      .sort((left, right) =>
        BigInt(left.projectileId) < BigInt(right.projectileId) ? -1 : 1,
      )
      .map((projectile) => {
        const owner = identity(projectile.ownerActorId);
        return {
          projectileId: projectile.projectileId,
          ownerActor: owner,
          ownerActorKey: owner.actorKey,
          position: copyPosition(projectile.position),
          launchDirection: projectile.launchHeading,
          heading: projectile.heading,
          shotProgram: projectile.shotProgram
            ? { ...projectile.shotProgram }
            : null,
          programmedPath: projectile.committedPath.map(copyPosition),
          ticksUntilAdvance: projectile.ticksUntilAdvance,
          remainingTiles: projectile.remainingTiles,
          tilesPerAdvance:
            document.header.contract.rules.attackProfiles.find(
              (profile) => profile.id === projectile.attackProfileId,
            )?.projectile.tilesPerAdvance ?? null,
          nextProgrammedPathIndex: projectile.nextPathIndex,
          tilesTraveled: projectile.nextPathIndex,
          phase: null,
          ownerParticipantId: projectile.ownerParticipantId,
          attackProfileId: projectile.attackProfileId,
          spawnedAtTick: projectile.spawnedAtTick,
          origin: copyPosition(projectile.origin),
          committedPath: projectile.committedPath.map(copyPosition),
        };
      }),
    scoreboard: scoreboardFromV3(world.scoreboard),
    mode: modeFromV3(world.mode),
    objective,
  };
}

function tickFromV3(
  tick: V3.ReplayV3Tick,
  document: V3.ReplayV3Document,
): Model.ReplayTick {
  const starts = new Map(
    tick.tickStart.lifeStarts.map((start) => [actorValue(start.actorId), start]),
  );
  return {
    tick: tick.tick,
    before: worldFromV3(tick.tickStart.state, document),
    activeActorKeys: tick.tickStart.activeActorIds.map(
      (actor) => identity(actor).actorKey,
    ),
    lifecycleEvents: tick.tickStart.events.map(eventFromV3),
    // A mind tick's per-body turns are derived from its one turn per
    // participant, so everything downstream — fog, per-unit facts, the bot
    // panel, both renderers — is untouched by the profile (§5.3).
    actorTurns: (
      tick.actorTurns ?? (tick.mindTurns ?? []).flatMap(specializeMindTurn)
    ).map((turn) =>
      actorTurnFromV3(turn, starts.get(actorValue(turn.actorId)) ?? null),
    ),
    events: tick.events.map(eventFromV3),
    projectileTraversals: [
      ...tick.tickStart.traversals,
      ...tick.traversals,
    ].map(traversalFromV3),
    after: worldFromV3(tick.postState, document),
  };
}

function payloadFromArguments(
  argumentsValue: readonly V3.ReplayV3ActionArgument[],
): Model.ReplayActionPayload {
  const payload: Model.ReplayActionPayload = {
    shotProgram: null,
    direction: null,
    launchHeading: null,
    unitKey: null,
    formTargetId: null,
    positionTarget: null,
  };
  for (const argument of argumentsValue) {
    switch (argument.kind) {
      case 'shot-program':
        payload.shotProgram = { ...argument.value };
        break;
      case 'direction':
        payload.direction = argument.value;
        break;
      case 'projectile-heading':
        payload.launchHeading = argument.value;
        break;
      case 'unit-target':
        payload.unitKey = genericUnitKey(
          argument.value.teamId,
          argument.value.unitId,
        );
        break;
      case 'form-target':
        payload.formTargetId = argument.formId;
        break;
      case 'position-target':
        payload.positionTarget = copyPosition(argument.value);
        break;
    }
  }
  return payload;
}

function rawPayload(
  argumentsValue: readonly (V3.ReplayV3RawActionArgument | null)[] | null,
): Model.ReplayActionPayload | null {
  if (argumentsValue === null) return null;
  const payload: Model.ReplayActionPayload = {
    shotProgram: null,
    direction: null,
    launchHeading: null,
    unitKey: null,
    formTargetId: null,
    positionTarget: null,
  };
  for (const argument of argumentsValue) {
    if (argument === null) continue;
    switch (argument.kind) {
      case 'shot-program':
        payload.shotProgram = { ...argument.value };
        break;
      case 'unit-target':
        payload.unitKey = genericUnitKey(
          argument.value.teamId,
          argument.value.unitId,
        );
        break;
      case 'form-target':
        payload.formTargetId = argument.formId;
        break;
      case 'position-target':
        payload.positionTarget = copyPosition(argument.value);
        break;
      // Raw numeric enum arguments deliberately remain only on the retained
      // wire document until they have been accepted into a named value.
      case 'direction':
      case 'projectile-heading':
        break;
    }
  }
  return payload;
}

function decisionFromResolved(
  action: V3.ReplayV3ResolvedAction,
): Model.ReplayActorDecision {
  return {
    actionId: action.actionId,
    actionCode: action.actionCode,
    payload: payloadFromArguments(action.arguments),
    debugMessage: null,
    faulted: false,
    faultMessage: null,
  };
}

function resolutionFromV3(
  resolution: V3.ReplayV3ActionResolution,
): Model.ReplayActionResolution {
  const accepted = resolution.acceptedAction;
  const validated = resolution.validatedAction;
  return {
    chosenActionId: accepted.actionId,
    chosenActionCode: accepted.actionCode,
    chosenPayload: payloadFromArguments(accepted.arguments),
    validatedActionId: validated.actionId,
    validatedActionCode: validated.actionCode,
    validatedPayload: payloadFromArguments(validated.arguments),
    result: resolution.outcome as Model.ReplayActionResult,
    submittedActionId: resolution.submittedAction?.actionId ?? null,
    runtimeFault: resolution.runtimeFault
      ? {
          participantId: resolution.runtimeFault.participantId,
          actor: identity(resolution.runtimeFault.actorId),
          stage: resolution.runtimeFault.stage,
          faultCode: resolution.runtimeFault.faultCode,
          cumulativeFaultCount: resolution.runtimeFault.cumulativeFaultCount,
          disqualificationTriggered:
            resolution.runtimeFault.disqualificationTriggered,
        }
      : null,
  };
}

function lifeStartFromV3(
  start: V3.ReplayV3LifeStart,
): Model.ReplayActorLifeStart {
  return {
    completeness: 'exact',
    schemaVersion: start.schemaVersion,
    runtimeContractVersion: start.runtimeContractVersion,
    actor: identity(start.actorId),
    participantId: start.participantId,
    actorRandomSeed: start.actorRandomSeed,
    teamRandomSeed: start.teamRandomSeed ?? null,
    spawnReason: start.origin.reason,
    generation: start.origin.generation,
    parentActor: start.origin.parentActorId
      ? identity(start.origin.parentActorId)
      : null,
    sourceTransitionId: start.origin.sourceTransitionId,
    sourceOperationId: start.origin.sourceOperationId,
    matchContractFingerprint: start.matchContractFingerprint,
  };
}

function observedActor(
  actor: V3.ReplayV3ObservedSelf | V3.ReplayV3ObservedAlly,
  observedBy: Model.ReplayActorLifeKey[],
): Model.ReplayObservedActor {
  return {
    actor: { kind: 'exact', identity: identity(actor.actorId) },
    classId: actor.classId ?? null,
    formId: actor.formId,
    position: copyPosition(actor.position),
    facing: actor.facing,
    health: actor.health,
    cooldown: actor.cooldown,
    energy: actor.energy,
    previousActionResult:
      (actor.previousActionResolution?.outcome as Model.ReplayActionResult) ??
      null,
    pendingFormTransition: transitionFromV3(
      actor.pendingSameLifeTransition,
      actor.formId,
    ),
    observedBy,
    // The wire omits the key while a body carries nothing, and omits it for
    // the whole match on a ruleset with no declared economy. Both mean the
    // same thing to a viewer, so normalization settles them into one number.
    carriedScrap: actor.carriedScrap ?? 0,
    // Absent means unlabelled, which is what an unlabelled body should look
    // like. Never the string "none".
    roleTag: actor.roleTag ?? null,
  };
}

function observedUnitFromV3(
  unit: V3.ReplayV3Observation['teamUnits'][number],
): Model.ReplayObservedUnit {
  const active =
    unit.state.kind === 'active' ? identity(unit.state.actorId) : null;
  const due =
    'dueTick' in unit.state && typeof unit.state.dueTick === 'number'
      ? unit.state.dueTick
      : null;
  return {
    unitKey: genericUnitKey(unit.teamId, unit.unitId),
    teamId: unit.teamId,
    unitId: unit.unitId,
    formId:
      unit.state.kind === 'active'
        ? unit.state.formId
        : 'targetFormId' in unit.state
          ? unit.state.targetFormId
          : '',
    lifecycleStatus: slotLifecycle(unit.state),
    activeActor: active,
    respawnAtTick:
      unit.state.kind === 'automatic-return-pending' ? due : null,
    unlockAtTick: unit.state.kind === 'availability-pending' ? due : null,
    rebuildReadyAtTick: null,
    fabricationAtTick:
      unit.state.kind === 'fabrication-pending' ||
      unit.state.kind === 'replication-pending'
        ? due
        : null,
  };
}

function observationFromV3(
  observation: V3.ReplayV3Observation,
): Model.ReplayActorObservation {
  const self = identity(observation.self.actorId);
  return {
    completeness: 'exact',
    schemaVersion: observation.schemaVersion,
    tick: observation.tick,
    matchContractFingerprint: observation.matchContractFingerprint,
    teamPerception: null,
    participants: observation.participants.map((participant) => ({
      participantKey: replayParticipantKey(participant.participantId),
      participantId: participant.participantId,
      teamKey: replayTeamKey(participant.teamId),
      teamId: participant.teamId,
      classId: participant.classId ?? null,
      runtimeFaultCount: participant.runtimeFaultCount,
      disqualified: participant.disqualified,
    })),
    self: observedActor(observation.self, [self.actorKey]),
    teamUnits: observation.teamUnits.map(observedUnitFromV3),
    allies: observation.allies.map((ally) =>
      observedActor(ally, [self.actorKey]),
    ),
    enemies: observation.enemies.map((enemy) => ({
      actor: { kind: 'exact', identity: identity(enemy.actorId) },
      classId: enemy.classId ?? null,
      formId: enemy.formId,
      position: copyPosition(enemy.position),
      facing: enemy.facing,
      health: enemy.health,
      cooldown: null,
      energy: null,
      previousActionResult: null,
      pendingFormTransition: transitionFromV3(
        enemy.pendingSameLifeTransition,
        enemy.formId,
      ),
      observedBy: enemy.observedBy.map((actor) => identity(actor).actorKey),
      carriedScrap: enemy.carriedScrap ?? 0,
      // Public on visible enemies by design (§12.2): half the drama of a
      // set-piece is seeing both sides' assignments and knowing one is wrong.
      roleTag: enemy.roleTag ?? null,
    })),
    visibleTiles: observation.visibleTiles.map((tile) => ({
      position: copyPosition(tile.position),
      isWall: tile.isWall,
      observedBy: tile.observedBy.map((actor) => identity(actor).actorKey),
      spawnReservation: tile.spawnReservation
        ? {
            teamId: tile.spawnReservation.teamId,
            unitId: tile.spawnReservation.unitId,
            unitKey: genericUnitKey(
              tile.spawnReservation.teamId,
              tile.spawnReservation.unitId,
            ),
            kind: tile.spawnReservation.kind,
            dueTick: tile.spawnReservation.dueTick ?? null,
          }
        : null,
    })),
    visibleProjectiles:
      observation.visibleProjectiles?.map((projectile) => {
        const owner = projectile.ownerActorId
          ? identity(projectile.ownerActorId)
          : null;
        return {
          projectileHandle: projectile.projectileId,
          projectileId: projectile.projectileId,
          ownerTeamId: projectile.ownerTeamId,
          ownerActor: owner,
          alliedOwnerActor:
            owner?.teamId === observation.self.actorId.teamId ? owner : null,
          visibleEnemyOwner: null,
          position: copyPosition(projectile.position),
          heading: projectile.heading,
          tilesPerAdvance: projectile.tilesPerAdvance,
          ticksUntilAdvance: projectile.ticksUntilAdvance,
          remainingTiles: projectile.remainingTiles,
          observedBy: projectile.observedBy.map(
            (actor) => identity(actor).actorKey,
          ),
          ticksPerAdvance: projectile.ticksPerAdvance,
          damagePerHit: projectile.damagePerHit,
        };
      }) ?? null,
    visibleEvents: observation.visibleEvents.map(observedEventFromV3),
    heardSounds:
      observation.heardSounds?.map((sound) => ({
        eventHandle: sound.eventHandle,
        sourceTick: sound.sourceTick,
        observerActor: identity(sound.observerActorId),
        type: sound.kind,
        bearing: sound.bearing,
        distance: sound.distance,
      })) ?? null,
    scoreboard: scoreboardFromV3(observation.scoreboard),
    mode: modeFromV3(observation.mode),
    frontlineObjective:
      observation.mode.kind === 'frontline'
        ? {
            activePositionIndex: observation.mode.activePositionIndex,
            claimingTeamId: observation.mode.claimingTeamId,
            captureProgress: observation.mode.captureProgress,
            decayTicksElapsed: observation.mode.decayTicksElapsed,
            controlResumesAtTick: observation.mode.controlResumesAtTick,
            holdOwnerTeamId: observation.mode.holdOwnerTeamId,
            holdEndsAtTick: observation.mode.holdEndsAtTick,
          }
        : null,
    actions: observation.actionLegalities.map((legality) => {
      const shot = legality.constraints.find(
        (constraint) => constraint.kind === 'shot-program',
      );
      const directions = legality.constraints.find(
        (constraint) => constraint.kind === 'direction',
      );
      const headings = legality.constraints.find(
        (constraint) => constraint.kind === 'projectile-heading',
      );
      const units = legality.constraints.find(
        (constraint) => constraint.kind === 'unit-target',
      );
      const forms = legality.constraints.find(
        (constraint) => constraint.kind === 'form-target',
      );
      const positions = legality.constraints.find(
        (constraint) => constraint.kind === 'position-target',
      );
      const tracks = legality.constraints.find(
        (constraint) => constraint.kind === 'upgrade-track',
      );
      return {
        actionId: legality.actionId,
        actionCode: legality.actionCode,
        parameterKinds: legality.constraints.map(
          (constraint) => constraint.kind,
        ),
        enabled: legality.allowedByForm,
        available: legality.available,
        shotProgramAvailable:
          shot?.kind === 'shot-program' ? shot.allowed : null,
        allowedDirections:
          directions?.kind === 'direction'
            ? [...directions.allowedValues]
            : null,
        allowedProjectileHeadings:
          headings?.kind === 'projectile-heading'
            ? [...headings.allowedValues]
            : null,
        allowedUnitKeys:
          units?.kind === 'unit-target'
            ? units.allowedValues.map((unit) =>
                genericUnitKey(unit.teamId, unit.unitId),
              )
            : null,
        allowedFormTargets:
          forms?.kind === 'form-target' ? [...forms.allowedFormIds] : null,
        allowedPositions:
          positions?.kind === 'position-target'
            ? positions.allowedValues.map(copyPosition)
            : null,
        allowedUpgradeTracks:
          tracks?.kind === 'upgrade-track'
            ? [...tracks.allowedTrackIds]
            : null,
      };
    }),
  };
}

function actorTurnFromV3(
  turn: V3.ReplayV3ActorTurn,
  start: V3.ReplayV3LifeStart | null,
): Model.ReplayActorTurn {
  const actor = identity(turn.actorId);
  const submitted = turn.submittedDecision;
  return {
    actor,
    actorKey: actor.actorKey,
    lifeStart: start ? lifeStartFromV3(start) : null,
    observation: observationFromV3(turn.observation),
    aliases: {
      completeness: 'exact',
      enemyLives: [],
      projectiles:
        turn.observation.visibleProjectiles?.map((projectile) => ({
          projectileHandle: projectile.projectileId,
          projectileId: projectile.projectileId,
        })) ?? [],
      events: turn.observation.visibleEvents.map((event) => ({
        eventHandle: event.eventHandle,
        eventId: event.eventHandle,
      })),
    },
    runtimeReply: {
      actionId: submitted?.actionId ?? null,
      actionCode: submitted?.actionCode ?? null,
      payload: submitted ? rawPayload(submitted.arguments) : null,
      debugMessage: submitted?.debugMessage ?? null,
      faulted:
        turn.actionResolution.runtimeFault !== null ||
        turn.actionResolution.outcome === 'faulted',
      faultMessage: turn.actionResolution.runtimeFault?.faultCode ?? null,
    },
    acceptedDecision: decisionFromResolved(
      turn.actionResolution.acceptedAction,
    ),
    actionResolution: resolutionFromV3(turn.actionResolution),
  };
}

function payloadRecord(
  payload: V3.ReplayV3EventPayload,
): Record<string, unknown> {
  return payload as Record<string, unknown>;
}

function actorField(
  payload: Record<string, unknown>,
  key: string,
): Model.ReplayActorIdentity | null {
  const value = payload[key] as V3.ReplayV3ActorId | null | undefined;
  return value ? identity(value) : null;
}

function positionField(
  payload: Record<string, unknown>,
  key: string,
): Model.ReplayPosition | null {
  const value = payload[key] as V3.ReplayV3Position | null | undefined;
  return value ? copyPosition(value) : null;
}

function stringField(
  payload: Record<string, unknown>,
  key: string,
): string | null {
  return typeof payload[key] === 'string' ? (payload[key] as string) : null;
}

function numberField(
  payload: Record<string, unknown>,
  key: string,
): number | null {
  return typeof payload[key] === 'number' ? (payload[key] as number) : null;
}

function actionFromPayload(
  payload: Record<string, unknown>,
): V3.ReplayV3ResolvedAction | null {
  return (payload.action as V3.ReplayV3ResolvedAction | undefined) ?? null;
}

function arcRelayFactFromV3(
  fact: V3.ReplayV3ArcRelayFact,
): Model.ReplayArcRelayFact {
  switch (fact.kind) {
    case 'core-born':
      return {
        kind: fact.kind,
        coreId: { ...fact.coreId },
        position: copyPosition(fact.position),
      };
    case 'core-picked-up':
      return {
        kind: fact.kind,
        coreId: { ...fact.coreId },
        carrierActor: identity(fact.carrierActorId),
        position: copyPosition(fact.position),
        nextRelocationTick: fact.nextRelocationTick,
      };
    case 'core-relocated':
      return {
        kind: fact.kind,
        coreId: { ...fact.coreId },
        carrierActor: fact.carrierActorId
          ? identity(fact.carrierActorId)
          : null,
        from: copyPosition(fact.from),
        to: copyPosition(fact.to),
        nextRelocationTick: fact.nextRelocationTick,
        relocationKind: fact.relocationKind,
      };
    case 'core-handed-off':
      return {
        kind: fact.kind,
        coreId: { ...fact.coreId },
        sourceActor: identity(fact.sourceActorId),
        targetActor: identity(fact.targetActorId),
        position: copyPosition(fact.position),
        nextRelocationTick: fact.nextRelocationTick,
      };
    case 'core-dropped':
      return {
        kind: fact.kind,
        coreId: { ...fact.coreId },
        sourceActor: identity(fact.sourceActorId),
        position: copyPosition(fact.position),
        nextRelocationTick: fact.nextRelocationTick,
        dropKind: fact.dropKind,
      };
    case 'core-banked':
      return {
        kind: fact.kind,
        coreId: { ...fact.coreId },
        carrierActor: identity(fact.carrierActorId),
        teamId: fact.teamId,
        position: copyPosition(fact.position),
        chargePips: fact.chargePips,
      };
    case 'well-changed':
      return {
        kind: fact.kind,
        wellId: fact.wellId,
        pendingCharge: fact.pendingCharge,
        rearmCompletesAtTick: fact.rearmCompletesAtTick,
        outstandingCoreId: fact.outstandingCoreId
          ? { ...fact.outstandingCoreId }
          : null,
      };
    case 'pulse':
      return {
        kind: fact.kind,
        teamId: fact.teamId,
        pulseOrdinal: fact.pulseOrdinal,
        opposingReactorIntegrity: fact.opposingReactorIntegrity,
      };
    case 'signature-changed':
      return {
        kind: fact.kind,
        operationId: fact.operationId,
        signatureId: fact.signatureId,
        ownerActor: identity(fact.ownerActorId),
        phase: fact.phase,
        reason: fact.reason,
      };
    case 'body-relocated':
      return {
        kind: fact.kind,
        operationId: fact.operationId,
        signatureId: fact.signatureId,
        ownerActor: identity(fact.ownerActorId),
        targetActor: identity(fact.targetActorId),
        from: copyPosition(fact.from),
        to: copyPosition(fact.to),
      };
    case 'signature-damage':
    case 'signature-repair':
      return {
        kind: fact.kind,
        operationId: fact.operationId,
        signatureId: fact.signatureId,
        ownerActor: identity(fact.ownerActorId),
        targetActor: identity(fact.targetActorId),
        amount: fact.amount,
        newHealth: fact.newHealth,
        position: copyPosition(fact.position),
      };
  }
}

function observedEventFromV3(
  event: V3.ReplayV3ObservedEvent,
): Model.ReplayObservedEvent {
  const payload = payloadRecord(event.payload);
  const actor =
    actorField(payload, 'actorId') ??
    actorField(payload, 'targetActorId') ??
    actorField(payload, 'sourceActorId');
  const action = actionFromPayload(payload);
  const transition =
    event.payload.kind === 'form-transition'
      ? {
          fromFormId: stringField(payload, 'fromFormId'),
          toFormId: stringField(payload, 'toFormId'),
          startedAtTick: numberField(payload, 'startedTick'),
          dueTick: numberField(payload, 'dueTick'),
        }
      : null;
  return {
    eventHandle: event.eventHandle,
    sourceTick: event.sourceTick,
    type: event.kind,
    teamId:
      numberField(payload, 'teamId') ??
      numberField(payload, 'sourceTeamId') ??
      actor?.teamId ??
      null,
    alliedActor: actor,
    enemyActor: null,
    projectileHandle: stringField(payload, 'projectileId'),
    position:
      positionField(payload, 'position') ??
      positionField(payload, 'origin') ??
      positionField(payload, 'to'),
    facing:
      (stringField(payload, 'facing') as Model.ReplayDirection | null) ??
      (stringField(payload, 'toFacing') as Model.ReplayDirection | null),
    projectileHeading: stringField(
      payload,
      'heading',
    ) as Model.ReplayProjectileHeading | null,
    fromFormId: transition?.fromFormId ?? null,
    toFormId: transition?.toFormId ?? null,
    formTransitionStartedAtTick: transition?.startedAtTick ?? null,
    formTransitionCompletesAtTick: transition?.dueTick ?? null,
    actionId: action?.actionId ?? null,
    actionCode: action?.actionCode ?? null,
    formTargetId:
      action?.arguments.find((argument) => argument.kind === 'form-target')
        ?.kind === 'form-target'
        ? (
            action.arguments.find(
              (argument) => argument.kind === 'form-target',
            ) as Extract<V3.ReplayV3ActionArgument, { kind: 'form-target' }>
          ).formId
        : null,
    actionResult: null,
    amount: numberField(payload, 'amount'),
    newHealth: numberField(payload, 'newHealth'),
    observedBy: event.observedBy.map((observer) => identity(observer).actorKey),
    sourceOrdinal: event.sourceOrdinal,
    payloadKind: event.payload.kind,
  };
}

function eventFromV3(
  event: V3.ReplayV3AuthoritativeEvent,
): Model.ReplayCausalEvent {
  const payload = payloadRecord(event.payload);
  const primaryActor =
    actorField(payload, 'actorId') ?? actorField(payload, 'sourceActorId');
  const targetActor = actorField(payload, 'targetActorId');
  const action = actionFromPayload(payload);
  const actionPayload = action ? payloadFromArguments(action.arguments) : null;
  const position =
    positionField(payload, 'position') ?? positionField(payload, 'origin');
  const from =
    positionField(payload, 'from') ??
    (event.payload.kind === 'rotation' ? position : null);
  const to =
    positionField(payload, 'to') ??
    positionField(payload, 'attemptedTo') ??
    position;
  const teamId =
    numberField(payload, 'teamId') ??
    numberField(payload, 'sourceTeamId') ??
    numberField(payload, 'targetTeamId') ??
    primaryActor?.teamId ??
    targetActor?.teamId ??
    null;
  const unitId =
    numberField(payload, 'targetUnitId') ??
    primaryActor?.unitId ??
    targetActor?.unitId ??
    null;
  return {
    eventId: event.eventHandle,
    tick: event.tick,
    ordinal: event.sourceOrdinal,
    type: event.kind,
    teamId,
    unitId,
    sourceActor: primaryActor,
    targetActor,
    projectileId: stringField(payload, 'projectileId'),
    from,
    to,
    fromFacing: stringField(
      payload,
      'fromFacing',
    ) as Model.ReplayDirection | null,
    toFacing:
      (stringField(payload, 'toFacing') as Model.ReplayDirection | null) ??
      (stringField(payload, 'facing') as Model.ReplayDirection | null) ??
      // `projectile-deflected` carries the guard's own facing, and it is the
      // load-bearing field of the event: the shell turns contacts arriving in
      // that quadrant and nothing else. Surfaced beside `targetFormId` below,
      // which the same event uses the same way.
      (stringField(payload, 'targetFacing') as Model.ReplayDirection | null),
    projectileHeading: stringField(
      payload,
      'heading',
    ) as Model.ReplayProjectileHeading | null,
    fromFormId: stringField(payload, 'fromFormId'),
    toFormId:
      stringField(payload, 'toFormId') ??
      stringField(payload, 'targetFormId'),
    formTransitionStartedAtTick: numberField(payload, 'startedTick'),
    formTransitionCompletesAtTick: numberField(payload, 'dueTick'),
    actionPayload,
    actionId: action?.actionId ?? null,
    actionCode: action?.actionCode ?? null,
    actionResult: null,
    amount: numberField(payload, 'amount'),
    newHealth: numberField(payload, 'newHealth'),
    lifecycleStatus:
      event.payload.kind === 'life-spawned'
        ? 'active'
        : event.payload.kind === 'life-retired' ||
            event.payload.kind === 'destruction'
          ? 'destroyed'
          : null,
    spawnReason: stringField(payload, 'reason'),
    respawnAtTick: null,
    unlockAtTick: null,
    rebuildReadyAtTick: null,
    fabricationAtTick: numberField(payload, 'dueTick'),
    fromPositionIndex: numberField(payload, 'fromPositionIndex'),
    toPositionIndex: numberField(payload, 'toPositionIndex'),
    claimingTeamId: numberField(payload, 'claimingTeamId'),
    captureProgress: numberField(payload, 'captureProgress'),
    controlResumesAtTick: numberField(payload, 'controlResumesAtTick'),
    completeness: 'exact',
    globalOrdinal: event.globalOrdinal,
    payloadKind: event.payload.kind,
    arcRelayFact:
      event.payload.kind === 'arc-relay'
        ? arcRelayFactFromV3(event.payload.fact)
        : undefined,
    audience:
      event.audience.kind === 'spatial'
        ? {
            kind: 'spatial',
            primaryPosition: copyPosition(event.audience.primaryPosition),
          }
        : event.audience.kind === 'team-private'
          ? { kind: 'team-private', teamId: event.audience.teamId }
          : { kind: 'public' },
  };
}

function traversalFromV3(
  traversal: V3.ReplayV3ProjectileTraversal,
): Model.ReplayProjectileTraversal {
  const owner = identity(traversal.ownerActorId);
  return {
    projectileId: traversal.projectileId,
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    launchDirection: traversal.launchHeading,
    from: copyPosition(traversal.from),
    path: traversal.path.map(copyPosition),
    heading: traversal.finalHeading,
    shotProgram: traversal.shotProgram
      ? { ...traversal.shotProgram }
      : null,
    programmedPath: null,
    globalOrdinal: traversal.globalOrdinal,
    phase: traversal.phase,
    trigger: traversal.trigger,
    ownerParticipantId: traversal.ownerParticipantId,
    ownerTeamId: traversal.ownerTeamId,
    attackProfileId: traversal.attackProfileId,
    finalHeading: traversal.finalHeading,
    terminal: { ...traversal.terminal },
  };
}

function resultFromV3(
  result: V3.ReplayV3Result,
  document: V3.ReplayV3Document,
): Model.ReplayTerminalResult {
  const finalWorld =
    document.ticks.at(-1)?.postState ?? document.initialFrame.state;
  const unitFactsByKey = new Map(
    result.units.map((unit) => [unitValue(unit.slot), unit]),
  );
  const teams = result.standings.teams.map<Model.ReplayTeamResult>(
    (standing) => {
      const teamUnits = document.header.contract.topology.unitSlots
        .filter((slot) => slot.teamId === standing.teamId)
        .sort(compareUnit)
        .map<Model.ReplayUnitResult>((topologySlot) => {
          const fact = unitFactsByKey.get(unitValue(topologySlot));
          if (!fact) {
            throw new Error(
              `validated replay-v3 result lost unit ${unitValue(topologySlot)}`,
            );
          }
          const active = fact.activeLife;
          const activeActor = active ? identity(active.actorId) : null;
          const defaultFormId = defaultFormForSlot(fact.slot, document);
          return {
            unitKey: genericUnitKey(fact.slot.teamId, fact.slot.unitId),
            teamId: fact.slot.teamId,
            unitId: fact.slot.unitId,
            defaultFormId,
            formId:
              active?.formId ??
              ('targetFormId' in fact.slot.state
                ? fact.slot.state.targetFormId
                : defaultFormId),
            lifecycleStatus: slotLifecycle(fact.slot.state),
            activeActor,
            activeActorKey: activeActor?.actorKey ?? null,
            health: active?.health ?? 0,
            damageDealt: null,
            pendingFormTransition: active
              ? transitionFromV3(
                  active.pendingSameLifeTransition,
                  active.formId,
                )
              : null,
            participantId: fact.slot.participantId,
            generation: active?.generation ?? null,
            nextLifeId: fact.slot.nextLifeId,
          };
        });
      return {
        teamKey: replayTeamKey(standing.teamId),
        teamId: standing.teamId,
        outcome: standing.outcome,
        activeHealth: Number(scoreValue(standing.scores, 'active-health') ?? 0),
        damageDealt: scoreValue(standing.scores, 'damage-dealt') ?? '0',
        units: teamUnits,
        faults: null,
        zoneTicks: null,
        rank: standing.rank,
        scores: standing.scores.map((score) => ({ ...score })),
      };
    },
  );
  const objective = objectiveFromV3(finalWorld.mode);
  if (objective.kind === 'frontline') {
    objective.nextTick = finalWorld.nextTick;
    objective.winnerTeamId =
      result.mode.kind === 'frontline' &&
      result.mode.reason === 'base-breach'
        ? result.standings.winnerTeamId
        : null;
  }
  return {
    winnerTeamId: result.standings.winnerTeamId,
    reason: result.completionReason,
    endTick:
      result.endTick ??
      document.ticks.at(-1)?.tick ??
      Math.max(0, finalWorld.nextTick - 1),
    reportedEndTick: result.endTick,
    territorialScore: null,
    objective,
    teams,
    eligibleTeamIds: [...result.eligibleTeamIds],
    mode:
      result.mode.kind === 'deathmatch'
        ? {
            kind: 'deathmatch',
            reason: result.mode.reason,
            scores: result.mode.scores.map((score) => ({
              teamKey: replayTeamKey(score.teamId),
              ...score,
            })),
          }
        : result.mode.kind === 'frontline'
          ? {
              kind: 'frontline',
              reason: result.mode.reason,
              control: {
                ...result.mode.control,
                holdOwnerTeamId: result.mode.control.holdOwnerTeamId,
                holdEndsAtTick: result.mode.control.holdEndsAtTick,
              },
              scores: result.mode.scores.map((score) => ({
                teamKey: replayTeamKey(score.teamId),
                ...score,
              })),
            }
          : {
              kind: 'arc-relay',
              reason: result.mode.reason,
              state: modeFromV3(result.mode.state) as Model.ReplayArcRelayModeState,
            },
  };
}
