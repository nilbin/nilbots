import assert from 'node:assert/strict';
import test from 'node:test';
import type {
  ArenaCapabilities,
  BotSummary,
} from '../src/site/api';
import {
  arenaOpponents,
  indexArenaPlayability,
  ownedArenaBotIds,
  ownedPlayableArenaRoster,
  playableArenaRoster,
} from '../src/site/arenaCapabilities';

const roster = [
  bot('owned-playable', ['generic-only']),
  bot('owned-blocked', ['legacy-duel-0.1']),
  bot('public-playable', ['generic-only']),
  bot('missing-row', ['legacy-duel-0.1']),
] satisfies BotSummary[];

const capabilities = {
  format: {
    rulesVersion: '0.5',
    requiredContractProfileId: 'legacy-duel-0.1',
    unranked: { gamesPerMatch: 1, defaultMapId: 'arena-01' },
    ranked: {
      gamesPerSet: 6,
      mapSeedPairs: 3,
      mirroredSlots: true,
      mapPool: ['arena-01', 'basic-01', 'bastion-01'],
      matchmakingPoolSize: 5,
    },
  },
  unrankedAllowance: allowance(),
  rankedAllowance: {
    ...allowance(),
    inProgress: 0,
    concurrencyLimit: 2,
  },
  bots: [
    admission('owned-playable', true, true),
    admission('owned-blocked', true, false),
    admission('public-playable', false, true),
  ],
} satisfies ArenaCapabilities;

test('server admission overrides contradictory public profile metadata', () => {
  assert.deepEqual(
    playableArenaRoster(roster, capabilities).map((bot) => bot.id),
    ['owned-playable', 'public-playable'],
  );
});

test('global choices require both server ownership and playability', () => {
  assert.deepEqual(
    ownedPlayableArenaRoster(roster, capabilities).map((bot) => bot.id),
    ['owned-playable'],
  );
});

test('server ownership remains visible when an owned bot is not playable', () => {
  assert.deepEqual(
    [...ownedArenaBotIds(capabilities)],
    ['owned-playable', 'owned-blocked'],
  );
});

test('a missing Arena capability row fails closed', () => {
  const byId = indexArenaPlayability(capabilities.bots);
  assert.equal(byId.get('missing-row'), undefined);
  assert.equal(
    playableArenaRoster(roster, capabilities).some(
      (bot) => bot.id === 'missing-row',
    ),
    false,
  );
});

test('opponent choices never contain the selected entrant', () => {
  const playable = playableArenaRoster(roster, capabilities);
  assert.deepEqual(
    arenaOpponents(playable, 'owned-playable').map((bot) => bot.id),
    ['public-playable'],
  );
});

function bot(
  id: string,
  supportedContractProfiles: string[],
): BotSummary {
  return {
    id,
    name: id,
    slug: id,
    accent: '#22d3ee',
    lookId: 'vanguard',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-07-28T00:00:00Z',
    ratings: [],
    owner: 'owner',
    activeVersion: {
      id: `${id}-version`,
      versionNumber: 1,
      artifactHash: 'artifact',
      supportedContractProfiles,
    },
    versionCount: 1,
    currentStanding: null,
  };
}

function admission(
  botId: string,
  isOwned: boolean,
  playable: boolean,
): ArenaCapabilities['bots'][number] {
  return {
    botId,
    isOwned,
    playable,
    refusalCode: playable
      ? null
      : 'matches.active_version_required',
    refusalDetail: playable ? null : 'No active generation.',
  };
}

function allowance(): ArenaCapabilities['unrankedAllowance'] {
  return {
    used: 1,
    limit: 10,
    remaining: 9,
    rollingWindowHours: 24,
    nextDailySlotAt: null,
    canStart: true,
    refusalCode: null,
    retryAt: null,
  };
}
