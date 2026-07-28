import type {
  AuthProviders,
  BotDetail,
  BotMatchHistory,
  BotStatistics,
  BotSummary,
  CosmeticCatalog,
  LadderStanding,
  LabsCatalog,
  MatchDetail,
  Leaderboard,
  MatchLive,
  MatchSetDetail,
  MatchSummary,
  Me,
  Meta,
  MyBot,
  Store,
  UserNotification,
} from '../api';

export const REVIEW_USER_ID = '00000000-0000-4000-8000-000000000001';
export const REVIEW_WARDEN_ID = '10000000-0000-4000-8000-000000000001';
export const REVIEW_BASTILLE_ID = '10000000-0000-4000-8000-000000000002';
export const REVIEW_PINCER_ID = '10000000-0000-4000-8000-000000000003';
export const REVIEW_RAMPART_ID = '10000000-0000-4000-8000-000000000004';
export const REVIEW_HALYARD_ID = '10000000-0000-4000-8000-000000000005';
export const REVIEW_MURDER_ID = '10000000-0000-4000-8000-000000000006';
export const REVIEW_COMPLETED_MATCH_ID = '30000000-0000-4000-8000-000000000001';
export const REVIEW_LIVE_MATCH_ID = '30000000-0000-4000-8000-000000000002';
export const REVIEW_FAILED_MATCH_ID = '30000000-0000-4000-8000-000000000003';
export const REVIEW_SET_ID = '40000000-0000-4000-8000-000000000001';
export const REVIEW_REPLAY_HASH =
  '199d519237dd4b0665e5da2155d8acc68d876906292c9a690ff201d1da264c16';

export interface ReviewSetGameSpec {
  readonly id: string;
  readonly game: number;
  readonly mapId: string;
  readonly themeId: string;
  readonly pincerSlot: 0 | 1;
  readonly replayHash: string;
  readonly createdAt: string;
}

/**
 * One canonical engine replay drives all six ranked-set review games. Mirrored
 * participant slots make the first mover win each game; map/theme substitutions stay
 * explicit and every resulting payload is re-hashed in the review-only Vite config.
 */
export const reviewSetGameSpecs = [
  {
    id: REVIEW_COMPLETED_MATCH_ID,
    game: 1,
    mapId: 'arena-01',
    themeId: 'ember-forge',
    pincerSlot: 0,
    replayHash: REVIEW_REPLAY_HASH,
    createdAt: '2026-07-27T21:00:00Z',
  },
  {
    id: '30000000-0000-4000-8000-000000000004',
    game: 2,
    mapId: 'arena-01',
    themeId: 'ember-forge',
    pincerSlot: 1,
    replayHash:
      '725138b17ccf5b51f7a98c1a04b7ec0b47365d0d6e6b108a01dbcfc739f3f4ad',
    createdAt: '2026-07-27T21:02:00Z',
  },
  {
    id: '30000000-0000-4000-8000-000000000005',
    game: 3,
    mapId: 'bastion-01',
    themeId: 'control-room',
    pincerSlot: 0,
    replayHash:
      'ac5e14f5fbea1ff645eefc3fdfd0f44196db23f7bb54aea2c316b08e4d417e8c',
    createdAt: '2026-07-27T21:04:00Z',
  },
  {
    id: '30000000-0000-4000-8000-000000000006',
    game: 4,
    mapId: 'bastion-01',
    themeId: 'control-room',
    pincerSlot: 1,
    replayHash:
      '0506ec3974fe341b3b24d291801607f4d135d4189d247893e95c5e0ba190a45a',
    createdAt: '2026-07-27T21:06:00Z',
  },
  {
    id: '30000000-0000-4000-8000-000000000007',
    game: 5,
    mapId: 'vault-01',
    themeId: 'frost-relay',
    pincerSlot: 0,
    replayHash:
      'ef876a675cc067030f36a42b3b9cd5970120b4d8a90d1c01e7825b12a229da42',
    createdAt: '2026-07-27T21:08:00Z',
  },
  {
    id: '30000000-0000-4000-8000-000000000008',
    game: 6,
    mapId: 'vault-01',
    themeId: 'frost-relay',
    pincerSlot: 1,
    replayHash:
      'f2baff4b2e68a3196b7c7e151047880b022ec1bbfcb66b49fa27b898aadfa0b8',
    createdAt: '2026-07-27T21:10:00Z',
  },
] as const satisfies readonly ReviewSetGameSpec[];

const pincerStanding = {
  botId: REVIEW_PINCER_ID,
  rulesVersion: '0.5',
  rating: 1284,
  rankedSets: 11,
  rank: 3,
} satisfies LadderStanding;

export const meFixture = {
  id: REVIEW_USER_ID,
  displayName: 'you',
  email: 'you@example.com',
} satisfies Me;

export const metaFixture = {
  engineVersion: '0.1.0',
  gameRulesVersion: '0.5',
  runtimeProtocolVersion: '0.1',
  sdkVersion: '0.5.0-review',
  buildPipelineVersion: '0.5.0-review',
  cliVersion: '0.5.0-review',
  maps: [
    { id: 'bastion-01', width: 24, height: 18, themeId: 'control-room' },
    { id: 'arena-01', width: 24, height: 18, themeId: 'ember-forge' },
    { id: 'vault-01', width: 24, height: 18, themeId: 'frost-relay' },
  ],
} satisfies Meta;

export const currentLeaderboardEntries = [
  {
    id: REVIEW_WARDEN_ID,
    slug: 'warden-gen-1',
    rank: 1,
    name: 'Warden gen-1',
    owner: 'ada',
    accent: '#7dd3fc',
    lookId: 'aureate-warden',
    rating: 1341,
    rankedSets: 14,
  },
  {
    id: REVIEW_BASTILLE_ID,
    slug: 'bastille-gen-5',
    rank: 2,
    name: 'Bastille gen-5',
    owner: 'kell',
    accent: '#ef4444',
    lookId: 'bulwark',
    rating: 1309,
    rankedSets: 12,
  },
  {
    id: REVIEW_PINCER_ID,
    slug: 'pincer-gen-10',
    rank: 3,
    name: 'Pincer gen-10',
    owner: 'you',
    accent: '#22d3ee',
    lookId: 'vanguard',
    rating: 1284,
    rankedSets: 11,
  },
  {
    id: REVIEW_RAMPART_ID,
    slug: 'rampart-gen-2',
    rank: 4,
    name: 'Rampart gen-2',
    owner: 'juno',
    accent: '#bef264',
    lookId: 'orbiter',
    rating: 1250,
    rankedSets: 9,
  },
  {
    id: REVIEW_HALYARD_ID,
    slug: 'halyard-gen-3',
    rank: 5,
    name: 'Halyard gen-3',
    owner: 'mox',
    accent: '#fb7185',
    lookId: 'needle',
    rating: 1238,
    rankedSets: 9,
  },
  {
    id: REVIEW_MURDER_ID,
    slug: 'murder-roomba',
    rank: 6,
    name: 'Murder Roomba',
    owner: 'you',
    accent: '#f5a623',
    lookId: 'mantis',
    rating: 1147,
    rankedSets: 6,
  },
] satisfies Leaderboard['entries'];

export const currentLeaderboardFixture = {
  rulesVersion: '0.5',
  activeRulesVersion: '0.5',
  ladders: ['0.4', '0.5'],
  entries: currentLeaderboardEntries,
} satisfies Leaderboard;

export const previousLeaderboardFixture = {
  rulesVersion: '0.4',
  activeRulesVersion: '0.5',
  ladders: ['0.4', '0.5'],
  entries: currentLeaderboardEntries.map((entry) => ({
    ...entry,
    rating: entry.rating - 35,
  })),
} satisfies Leaderboard;

export const botsFixture = [
  {
    id: REVIEW_WARDEN_ID,
    slug: 'warden-gen-1',
    name: 'Warden gen-1',
    owner: 'ada',
    accent: '#7dd3fc',
    lookId: 'aureate-warden',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-06-01T00:00:00Z',
    versionCount: 4,
    ratings: [{ rulesVersion: '0.5', rating: 1341, rankedSets: 14 }],
    activeVersion: {
      id: '20000000-0000-4000-8000-000000000001',
      versionNumber: 4,
      artifactHash: '4f9229f8eb7b7725',
      supportedContractProfiles: [
        'legacy-duel-0.1',
        'generic-actor-match-2',
      ],
    },
    currentStanding: {
      botId: REVIEW_WARDEN_ID,
      rulesVersion: '0.5',
      rating: 1341,
      rankedSets: 14,
      rank: 1,
    },
  },
  {
    id: REVIEW_BASTILLE_ID,
    slug: 'bastille-gen-5',
    name: 'Bastille gen-5',
    owner: 'kell',
    accent: '#ef4444',
    lookId: 'bulwark',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-05-24T00:00:00Z',
    versionCount: 5,
    ratings: [{ rulesVersion: '0.5', rating: 1309, rankedSets: 12 }],
    activeVersion: {
      id: '20000000-0000-4000-8000-000000000002',
      versionNumber: 5,
      artifactHash: '11b2b6bf82cf61e9',
      supportedContractProfiles: null,
    },
    currentStanding: {
      botId: REVIEW_BASTILLE_ID,
      rulesVersion: '0.5',
      rating: 1309,
      rankedSets: 12,
      rank: 2,
    },
  },
  {
    id: REVIEW_PINCER_ID,
    slug: 'pincer-gen-10',
    name: 'Pincer gen-10',
    owner: 'you',
    accent: '#22d3ee',
    lookId: 'vanguard',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-05-11T00:00:00Z',
    versionCount: 10,
    ratings: [{ rulesVersion: '0.5', rating: 1284, rankedSets: 11 }],
    activeVersion: {
      id: '20000000-0000-4000-8000-000000000010',
      versionNumber: 10,
      artifactHash: '9f31c0a4b7de51aa',
      supportedContractProfiles: [
        'legacy-duel-0.1',
        'generic-actor-match-2',
      ],
    },
    currentStanding: pincerStanding,
  },
  {
    id: REVIEW_RAMPART_ID,
    slug: 'rampart-gen-2',
    name: 'Rampart gen-2',
    owner: 'juno',
    accent: '#bef264',
    lookId: 'orbiter',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-06-14T00:00:00Z',
    versionCount: 2,
    ratings: [{ rulesVersion: '0.5', rating: 1250, rankedSets: 9 }],
    activeVersion: {
      id: '20000000-0000-4000-8000-000000000004',
      versionNumber: 2,
      artifactHash: '77dba5d2fe1939ac',
      supportedContractProfiles: null,
    },
    currentStanding: {
      botId: REVIEW_RAMPART_ID,
      rulesVersion: '0.5',
      rating: 1250,
      rankedSets: 9,
      rank: 4,
    },
  },
  {
    id: REVIEW_HALYARD_ID,
    slug: 'halyard-gen-3',
    name: 'Halyard gen-3',
    owner: 'mox',
    accent: '#fb7185',
    lookId: 'needle',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-06-22T00:00:00Z',
    versionCount: 3,
    ratings: [{ rulesVersion: '0.5', rating: 1238, rankedSets: 9 }],
    activeVersion: {
      id: '20000000-0000-4000-8000-000000000005',
      versionNumber: 3,
      artifactHash: '8ef34ad7b1c29054',
      supportedContractProfiles: null,
    },
    currentStanding: {
      botId: REVIEW_HALYARD_ID,
      rulesVersion: '0.5',
      rating: 1238,
      rankedSets: 9,
      rank: 5,
    },
  },
  {
    id: REVIEW_MURDER_ID,
    slug: 'murder-roomba',
    name: 'Murder Roomba',
    owner: 'you',
    accent: '#f5a623',
    lookId: 'mantis',
    projectileLookId: 'pulse-bolt',
    createdAt: '2026-07-08T00:00:00Z',
    versionCount: 3,
    ratings: [{ rulesVersion: '0.5', rating: 1147, rankedSets: 6 }],
    activeVersion: {
      id: '20000000-0000-4000-8000-000000000013',
      versionNumber: 3,
      artifactHash: '31b7fa44c18a998e',
      supportedContractProfiles: null,
    },
    currentStanding: {
      botId: REVIEW_MURDER_ID,
      rulesVersion: '0.5',
      rating: 1147,
      rankedSets: 6,
      rank: 6,
    },
  },
] satisfies BotSummary[];

export const myBotsFixture = [
  {
    id: REVIEW_PINCER_ID,
    slug: 'pincer-gen-10',
    name: 'Pincer gen-10',
    accent: '#22d3ee',
    lookId: 'vanguard',
    projectileLookId: 'pulse-bolt',
    latestVersion: {
      versionNumber: 10,
      status: 'Built',
      isActive: true,
    },
  },
  {
    id: REVIEW_MURDER_ID,
    slug: 'murder-roomba',
    name: 'Murder Roomba',
    accent: '#f5a623',
    lookId: 'mantis',
    projectileLookId: 'pulse-bolt',
    latestVersion: {
      versionNumber: 3,
      status: 'Built',
      isActive: true,
    },
  },
] satisfies MyBot[];

export const emptyMyBotsFixture = [] satisfies MyBot[];

export const labsCatalogFixture = {
  enabled: true,
  playlists: [
    {
      playlistVersionId: '60000000-0000-4000-8000-000000000001',
      key: 'frontline-labs',
      displayName: 'Frontline Labs',
      version: 1,
      gameModeId: 'frontline',
      rulesetId: 'frontline-labs-1',
      matchFormatId: 'head-to-head',
      participantCount: 2,
      scoringTeamCount: 2,
      participantsPerTeam: 1,
      requiredContractProfileId: 'generic-actor-match-2',
    },
  ],
} satisfies LabsCatalog;

export const botDetailFixture = {
  id: REVIEW_PINCER_ID,
  slug: 'pincer-gen-10',
  name: 'Pincer gen-10',
  owner: 'you',
  accent: '#22d3ee',
  lookId: 'vanguard',
  projectileLookId: 'pulse-bolt',
  createdAt: '2026-05-11T00:00:00Z',
  isOwner: true,
  currentStanding: pincerStanding,
  versions: [
    {
      id: '20000000-0000-4000-8000-000000000010',
      versionNumber: 10,
      status: 'Built',
      artifactHash: '9f31c0a4b7de51aa',
      isActive: true,
      createdAt: '2026-07-17T10:00:00Z',
      buildReceipt: null,
      buildLog: null,
      entryType: null,
      sources: null,
      supportedContractProfiles: [
        'legacy-duel-0.1',
        'generic-actor-match-2',
      ],
    },
    {
      id: '20000000-0000-4000-8000-000000000009',
      versionNumber: 9,
      status: 'Built',
      artifactHash: '3ab77c1904ee62bb',
      isActive: false,
      createdAt: '2026-07-02T10:00:00Z',
      buildReceipt: null,
      buildLog: null,
      entryType: null,
      sources: null,
      supportedContractProfiles: null,
    },
    {
      id: '20000000-0000-4000-8000-000000000008',
      versionNumber: 8,
      status: 'Failed',
      artifactHash: null,
      isActive: false,
      createdAt: '2026-06-24T10:00:00Z',
      buildReceipt: null,
      buildLog: null,
      entryType: null,
      sources: null,
      supportedContractProfiles: null,
    },
  ],
} satisfies BotDetail;

function detailFromSummary(bot: BotSummary): BotDetail {
  const activeVersion = bot.activeVersion;
  return {
    id: bot.id,
    slug: bot.slug,
    name: bot.name,
    owner: bot.owner,
    accent: bot.accent,
    lookId: bot.lookId,
    projectileLookId: bot.projectileLookId,
    createdAt: bot.createdAt,
    isOwner: bot.owner === meFixture.displayName,
    currentStanding: bot.currentStanding ?? null,
    versions:
      activeVersion === null
        ? []
        : [
            {
              id: activeVersion.id,
              versionNumber: activeVersion.versionNumber,
              status: 'Built',
              artifactHash: activeVersion.artifactHash,
              isActive: true,
              createdAt: bot.createdAt,
              buildReceipt: null,
              buildLog: null,
              entryType: null,
              sources: null,
              supportedContractProfiles:
                activeVersion.supportedContractProfiles,
            },
          ],
  };
}

/** Every public bot link in the review UI resolves by both slug and id. */
export const botDetailsFixture: readonly BotDetail[] = botsFixture.map((bot) =>
  bot.id === REVIEW_PINCER_ID ? botDetailFixture : detailFromSummary(bot),
);

export const botStatisticsFixture = {
  overall: { played: 214, wins: 118, losses: 96, draws: 0 },
  ranked: { played: 66, wins: 38, losses: 28, draws: 0 },
  unranked: { played: 148, wins: 80, losses: 68, draws: 0 },
  combat: { games: 214, damageDealt: 512, faults: 0 },
} satisfies BotStatistics;

export const botStatisticsFixtures: Readonly<Record<string, BotStatistics>> =
  Object.fromEntries(
    botsFixture.map((bot, index) => {
      if (bot.id === REVIEW_PINCER_ID) {
        return [bot.id, botStatisticsFixture];
      }
      const played = 174 - index * 11;
      const draws = bot.id === REVIEW_BASTILLE_ID ? 4 : 0;
      const wins = Math.max(24, Math.round(played * (0.58 - index * 0.035)));
      const losses = played - wins - draws;
      const rankedPlayed = bot.currentStanding?.rankedSets
        ? bot.currentStanding.rankedSets * 6
        : 0;
      const rankedWins = Math.min(wins, Math.round(rankedPlayed * 0.54));
      const rankedDraws = bot.id === REVIEW_BASTILLE_ID ? draws : 0;
      const rankedLosses = rankedPlayed - rankedWins - rankedDraws;
      const statistics = {
        overall: { played, wins, losses, draws },
        ranked: {
          played: rankedPlayed,
          wins: rankedWins,
          losses: rankedLosses,
          draws: rankedDraws,
        },
        unranked: {
          played: played - rankedPlayed,
          wins: wins - rankedWins,
          losses: losses - rankedLosses,
          draws: draws - rankedDraws,
        },
        combat: {
          games: played,
          damageDealt: played * 2 + wins,
          faults: index === botsFixture.length - 1 ? 2 : 0,
        },
      } satisfies BotStatistics;
      return [bot.id, statistics];
    }),
  );

const baseBotMatchHistoryFixture = {
  wins: 118,
  losses: 96,
  draws: 0,
  matches: [
    {
      id: REVIEW_FAILED_MATCH_ID,
      mapId: 'arena-01',
      status: 'Failed',
      broadcasting: false,
      matchSetId: null,
      setGame: null,
      createdAt: '2026-07-27T18:00:00Z',
      outcome: null,
      opponent: {
        botId: REVIEW_BASTILLE_ID,
        nameSnapshot: 'Bastille gen-5',
        ownerDisplayNameSnapshot: 'kell',
        accentSnapshot: '#ef4444',
        lookIdSnapshot: 'bulwark',
      },
    },
  ],
} satisfies BotMatchHistory;

const baseMatchesFixture = [
  {
    id: REVIEW_COMPLETED_MATCH_ID,
    mapId: 'arena-01',
    status: 'Completed',
    broadcasting: false,
    matchSetId: REVIEW_SET_ID,
    setGame: 1,
    winnerSlot: 0,
    endReason: 'Elimination',
    endTick: 96,
    createdAt: '2026-07-27T21:00:00Z',
    completedAt: '2026-07-27T21:02:00Z',
    participants: [
      {
        slot: 0,
        nameSnapshot: 'Pincer gen-10',
        ownerDisplayNameSnapshot: 'you',
        accentSnapshot: '#22d3ee',
        lookIdSnapshot: 'vanguard',
        projectileLookIdSnapshot: 'pulse-bolt',
        outcome: 'Win',
        finalHealth: 1,
      },
      {
        slot: 1,
        nameSnapshot: 'Bastille gen-5',
        ownerDisplayNameSnapshot: 'kell',
        accentSnapshot: '#ef4444',
        lookIdSnapshot: 'bulwark',
        projectileLookIdSnapshot: 'pulse-bolt',
        outcome: 'Loss',
        finalHealth: 0,
      },
    ],
  },
  {
    id: REVIEW_LIVE_MATCH_ID,
    mapId: 'arena-01',
    status: 'Completed',
    broadcasting: true,
    matchSetId: null,
    setGame: null,
    winnerSlot: null,
    endReason: null,
    endTick: null,
    createdAt: '2026-07-27T21:20:00Z',
    completedAt: null,
    participants: [
      {
        slot: 0,
        nameSnapshot: 'Warden gen-1',
        ownerDisplayNameSnapshot: 'ada',
        accentSnapshot: '#7dd3fc',
        lookIdSnapshot: 'aureate-warden',
        projectileLookIdSnapshot: 'pulse-bolt',
        outcome: null,
        finalHealth: null,
      },
      {
        slot: 1,
        nameSnapshot: 'Rampart gen-2',
        ownerDisplayNameSnapshot: 'juno',
        accentSnapshot: '#bef264',
        lookIdSnapshot: 'orbiter',
        projectileLookIdSnapshot: 'pulse-bolt',
        outcome: null,
        finalHealth: null,
      },
    ],
  },
  {
    id: REVIEW_FAILED_MATCH_ID,
    mapId: 'arena-01',
    status: 'Failed',
    broadcasting: false,
    matchSetId: null,
    setGame: null,
    winnerSlot: null,
    endReason: null,
    endTick: null,
    createdAt: '2026-07-27T18:00:00Z',
    completedAt: '2026-07-27T18:00:02Z',
    participants: [
      {
        slot: 0,
        nameSnapshot: 'Pincer gen-10',
        ownerDisplayNameSnapshot: 'you',
        accentSnapshot: '#22d3ee',
        lookIdSnapshot: 'vanguard',
        projectileLookIdSnapshot: 'pulse-bolt',
        outcome: null,
        finalHealth: null,
      },
      {
        slot: 1,
        nameSnapshot: 'Bastille gen-5',
        ownerDisplayNameSnapshot: 'kell',
        accentSnapshot: '#ef4444',
        lookIdSnapshot: 'bulwark',
        projectileLookIdSnapshot: 'pulse-bolt',
        outcome: null,
        finalHealth: null,
      },
    ],
  },
] satisfies MatchSummary[];

export const liveMatchFixture = {
  status: 'Completed',
  matchSetId: null,
  setGame: null,
  presentationTicksPerSecond: 5,
  presentationTick: 24,
  totalTicks: null,
  broadcastComplete: false,
  countdownMs: 0,
} satisfies MatchLive;

export const completedMatchLiveFixture = {
  status: 'Completed',
  matchSetId: REVIEW_SET_ID,
  setGame: 1,
  presentationTicksPerSecond: 5,
  presentationTick: 96,
  totalTicks: 97,
  broadcastComplete: true,
  countdownMs: 0,
} satisfies MatchLive;

export const completedMatchDetailFixture = {
  id: REVIEW_COMPLETED_MATCH_ID,
  mapId: 'arena-01',
  gameRulesVersion: '0.5',
  seed: 3004873239773946906,
  status: 'Completed',
  broadcasting: false,
  matchSetId: REVIEW_SET_ID,
  setGame: 1,
  winnerSlot: 0,
  endReason: 'Elimination',
  endTick: 96,
  replayHash: REVIEW_REPLAY_HASH,
  replayFormatVersion: 1,
  error: null,
  createdAt: '2026-07-27T21:00:00Z',
  completedAt: '2026-07-27T21:02:00Z',
  participants: [
    {
      slot: 0,
      teamId: 0,
      botId: REVIEW_PINCER_ID,
      nameSnapshot: 'Pincer gen-10',
      ownerDisplayNameSnapshot: 'you',
      accentSnapshot: '#22d3ee',
      lookIdSnapshot: 'vanguard',
      projectileLookIdSnapshot: 'pulse-bolt',
      artifactHashSnapshot: '9f31c0a4b7de51aa',
      outcome: 'Win',
      finalHealth: 1,
      damageDealt: 3,
      faults: 0,
    },
    {
      slot: 1,
      teamId: 1,
      botId: REVIEW_BASTILLE_ID,
      nameSnapshot: 'Bastille gen-5',
      ownerDisplayNameSnapshot: 'kell',
      accentSnapshot: '#ef4444',
      lookIdSnapshot: 'bulwark',
      projectileLookIdSnapshot: 'pulse-bolt',
      artifactHashSnapshot: '11b2b6bf82cf61e9',
      outcome: 'Loss',
      finalHealth: 0,
      damageDealt: 2,
      faults: 0,
    },
  ],
  teamResults: duelTeamResults(0),
} satisfies MatchDetail;

export const liveMatchDetailFixture = {
  id: REVIEW_LIVE_MATCH_ID,
  mapId: 'arena-01',
  gameRulesVersion: '0.5',
  seed: 3004873239773946906,
  status: 'Completed',
  broadcasting: true,
  matchSetId: null,
  setGame: null,
  winnerSlot: null,
  endReason: null,
  endTick: null,
  replayHash: null,
  replayFormatVersion: null,
  error: null,
  createdAt: '2026-07-27T21:20:00Z',
  completedAt: null,
  participants: [
    {
      slot: 0,
      teamId: 0,
      botId: REVIEW_WARDEN_ID,
      nameSnapshot: 'Warden gen-1',
      ownerDisplayNameSnapshot: 'ada',
      accentSnapshot: '#7dd3fc',
      lookIdSnapshot: 'aureate-warden',
      projectileLookIdSnapshot: 'pulse-bolt',
      artifactHashSnapshot: '4f9229f8eb7b7725',
      outcome: null,
      finalHealth: null,
      damageDealt: null,
      faults: null,
    },
    {
      slot: 1,
      teamId: 1,
      botId: REVIEW_RAMPART_ID,
      nameSnapshot: 'Rampart gen-2',
      ownerDisplayNameSnapshot: 'juno',
      accentSnapshot: '#bef264',
      lookIdSnapshot: 'orbiter',
      projectileLookIdSnapshot: 'pulse-bolt',
      artifactHashSnapshot: '77dba5d2fe1939ac',
      outcome: null,
      finalHealth: null,
      damageDealt: null,
      faults: null,
    },
  ],
  teamResults: [],
} satisfies MatchDetail;

export const failedMatchLiveFixture = {
  status: 'Failed',
  matchSetId: null,
  setGame: null,
  presentationTicksPerSecond: 5,
  presentationTick: 0,
  totalTicks: null,
  broadcastComplete: true,
  countdownMs: 0,
} satisfies MatchLive;

export const failedMatchDetailFixture = {
  id: REVIEW_FAILED_MATCH_ID,
  mapId: 'arena-01',
  gameRulesVersion: '0.5',
  seed: 42,
  status: 'Failed',
  broadcasting: false,
  matchSetId: null,
  setGame: null,
  winnerSlot: null,
  endReason: null,
  endTick: null,
  replayHash: null,
  replayFormatVersion: null,
  error: 'Bot execution stopped before a replay could be produced.',
  createdAt: '2026-07-27T18:00:00Z',
  completedAt: '2026-07-27T18:00:02Z',
  participants: [
    {
      slot: 0,
      teamId: 0,
      botId: REVIEW_PINCER_ID,
      nameSnapshot: 'Pincer gen-10',
      ownerDisplayNameSnapshot: 'you',
      accentSnapshot: '#22d3ee',
      lookIdSnapshot: 'vanguard',
      projectileLookIdSnapshot: 'pulse-bolt',
      artifactHashSnapshot: '9f31c0a4b7de51aa',
      outcome: null,
      finalHealth: null,
      damageDealt: null,
      faults: null,
    },
    {
      slot: 1,
      teamId: 1,
      botId: REVIEW_BASTILLE_ID,
      nameSnapshot: 'Bastille gen-5',
      ownerDisplayNameSnapshot: 'kell',
      accentSnapshot: '#ef4444',
      lookIdSnapshot: 'bulwark',
      projectileLookIdSnapshot: 'pulse-bolt',
      artifactHashSnapshot: '11b2b6bf82cf61e9',
      outcome: null,
      finalHealth: null,
      damageDealt: null,
      faults: null,
    },
  ],
  teamResults: [],
} satisfies MatchDetail;

function duelTeamResults(
  winnerTeamId: number,
): MatchDetail['teamResults'] {
  return [0, 1].map((teamId) => ({
    teamId,
    placement: teamId === winnerTeamId ? 1 : 2,
    outcome: teamId === winnerTeamId ? 'Win' : 'Loss',
    scores: [],
  }));
}

const setParticipantSnapshots = {
  pincer: {
    botId: REVIEW_PINCER_ID,
    nameSnapshot: 'Pincer gen-10',
    ownerDisplayNameSnapshot: 'you',
    accentSnapshot: '#22d3ee',
    lookIdSnapshot: 'vanguard',
    projectileLookIdSnapshot: 'pulse-bolt',
  },
  bastille: {
    botId: REVIEW_BASTILLE_ID,
    nameSnapshot: 'Bastille gen-5',
    ownerDisplayNameSnapshot: 'kell',
    accentSnapshot: '#ef4444',
    lookIdSnapshot: 'bulwark',
    projectileLookIdSnapshot: 'pulse-bolt',
  },
} as const;

const baseMatchSetFixture = {
  id: REVIEW_SET_ID,
  status: 'Completed',
  rulesVersion: '0.5',
  botA: {
    id: REVIEW_PINCER_ID,
    name: 'Pincer gen-10',
    accent: '#22d3ee',
    lookId: 'vanguard',
  },
  botB: {
    id: REVIEW_BASTILLE_ID,
    name: 'Bastille gen-5',
    accent: '#ef4444',
    lookId: 'bulwark',
  },
  createdAt: '2026-07-27T20:40:00Z',
  revealed: true,
  scoreA: 4,
  scoreB: 2,
  ratingChangeA: 17,
  ratingChangeB: -17,
  winnerBotId: REVIEW_PINCER_ID,
  games: [
    {
      id: REVIEW_COMPLETED_MATCH_ID,
      game: 1,
      mapId: 'arena-01',
      status: 'Completed',
      broadcasting: false,
      winnerBotId: REVIEW_PINCER_ID,
      draw: false,
      participants: [
        { slot: 0, ...setParticipantSnapshots.pincer },
        { slot: 1, ...setParticipantSnapshots.bastille },
      ],
    },
    {
      id: '30000000-0000-4000-8000-000000000004',
      game: 2,
      mapId: 'arena-01',
      status: 'Completed',
      broadcasting: false,
      winnerBotId: REVIEW_PINCER_ID,
      draw: false,
      participants: [
        { slot: 0, ...setParticipantSnapshots.bastille },
        { slot: 1, ...setParticipantSnapshots.pincer },
      ],
    },
    {
      id: '30000000-0000-4000-8000-000000000005',
      game: 3,
      mapId: 'bastion-01',
      status: 'Completed',
      broadcasting: false,
      winnerBotId: REVIEW_PINCER_ID,
      draw: false,
      participants: [
        { slot: 0, ...setParticipantSnapshots.pincer },
        { slot: 1, ...setParticipantSnapshots.bastille },
      ],
    },
    {
      id: '30000000-0000-4000-8000-000000000006',
      game: 4,
      mapId: 'bastion-01',
      status: 'Completed',
      broadcasting: false,
      winnerBotId: REVIEW_BASTILLE_ID,
      draw: false,
      participants: [
        { slot: 0, ...setParticipantSnapshots.bastille },
        { slot: 1, ...setParticipantSnapshots.pincer },
      ],
    },
    {
      id: '30000000-0000-4000-8000-000000000007',
      game: 5,
      mapId: 'vault-01',
      status: 'Completed',
      broadcasting: false,
      winnerBotId: REVIEW_BASTILLE_ID,
      draw: false,
      participants: [
        { slot: 0, ...setParticipantSnapshots.pincer },
        { slot: 1, ...setParticipantSnapshots.bastille },
      ],
    },
    {
      id: '30000000-0000-4000-8000-000000000008',
      game: 6,
      mapId: 'vault-01',
      status: 'Completed',
      broadcasting: false,
      winnerBotId: REVIEW_PINCER_ID,
      draw: false,
      participants: [
        { slot: 0, ...setParticipantSnapshots.bastille },
        { slot: 1, ...setParticipantSnapshots.pincer },
      ],
    },
  ],
} satisfies MatchSetDetail;

type SetParticipantSnapshot =
  (typeof setParticipantSnapshots)[keyof typeof setParticipantSnapshots];

function setGameSnapshots(spec: ReviewSetGameSpec) {
  return spec.pincerSlot === 0
    ? [setParticipantSnapshots.pincer, setParticipantSnapshots.bastille]
    : [setParticipantSnapshots.bastille, setParticipantSnapshots.pincer];
}

function setMatchDetail(spec: ReviewSetGameSpec): MatchDetail {
  const snapshots = setGameSnapshots(spec);
  return {
    id: spec.id,
    mapId: spec.mapId,
    gameRulesVersion: '0.5',
    seed: 3004873239773946906,
    status: 'Completed',
    broadcasting: false,
    matchSetId: REVIEW_SET_ID,
    setGame: spec.game,
    winnerSlot: 0,
    endReason: 'Elimination',
    endTick: 96,
    replayHash: spec.replayHash,
    replayFormatVersion: 1,
    error: null,
    createdAt: spec.createdAt,
    completedAt: new Date(Date.parse(spec.createdAt) + 120_000).toISOString(),
    participants: snapshots.map((snapshot, slot) =>
      setDetailParticipant(slot, snapshot),
    ),
    teamResults: duelTeamResults(0),
  };
}

function setDetailParticipant(
  slot: number,
  snapshot: SetParticipantSnapshot,
): MatchDetail['participants'][number] {
  return {
    slot,
    teamId: slot,
    ...snapshot,
    artifactHashSnapshot:
      snapshot.botId === REVIEW_PINCER_ID
        ? '9f31c0a4b7de51aa'
        : '11b2b6bf82cf61e9',
    outcome: slot === 0 ? 'Win' : 'Loss',
    finalHealth: slot === 0 ? 1 : 0,
    damageDealt: slot === 0 ? 3 : 2,
    faults: 0,
  };
}

function setMatchLive(spec: ReviewSetGameSpec): MatchLive {
  return {
    status: 'Completed',
    matchSetId: REVIEW_SET_ID,
    setGame: spec.game,
    presentationTicksPerSecond: 5,
    presentationTick: 96,
    totalTicks: 97,
    broadcastComplete: true,
    countdownMs: 0,
  };
}

export const completedSetMatchDetailFixtures: readonly MatchDetail[] =
  reviewSetGameSpecs.map((spec) =>
    spec.id === REVIEW_COMPLETED_MATCH_ID
      ? completedMatchDetailFixture
      : setMatchDetail(spec),
  );

export const completedSetMatchLiveFixtures: readonly MatchLive[] =
  reviewSetGameSpecs.map((spec) =>
    spec.id === REVIEW_COMPLETED_MATCH_ID
      ? completedMatchLiveFixture
      : setMatchLive(spec),
  );

export const matchSetFixture = {
  ...baseMatchSetFixture,
  scoreA: 3,
  scoreB: 3,
  ratingChangeA: 2,
  ratingChangeB: -2,
  winnerBotId: null,
  games: reviewSetGameSpecs.map((spec) => {
    const snapshots = setGameSnapshots(spec);
    return {
      id: spec.id,
      game: spec.game,
      mapId: spec.mapId,
      status: 'Completed',
      broadcasting: false,
      winnerBotId: snapshots[0].botId,
      draw: false,
      participants: snapshots.map((snapshot, slot) => ({
        slot,
        ...snapshot,
      })),
    };
  }),
} satisfies MatchSetDetail;

function matchSummaryFromDetail(detail: MatchDetail): MatchSummary {
  return {
    id: detail.id,
    mapId: detail.mapId,
    status: detail.status,
    broadcasting: detail.broadcasting,
    matchSetId: detail.matchSetId,
    setGame: detail.setGame,
    winnerSlot: detail.winnerSlot,
    endReason: detail.endReason,
    endTick: detail.endTick,
    createdAt: detail.createdAt,
    completedAt: detail.completedAt,
    participants: detail.participants.map((participant) => ({
      slot: participant.slot,
      nameSnapshot: participant.nameSnapshot,
      ownerDisplayNameSnapshot: participant.ownerDisplayNameSnapshot,
      accentSnapshot: participant.accentSnapshot,
      lookIdSnapshot: participant.lookIdSnapshot,
      projectileLookIdSnapshot: participant.projectileLookIdSnapshot,
      outcome: participant.outcome,
      finalHealth: participant.finalHealth,
    })),
  };
}

export const setMatchSummaryFixtures: readonly MatchSummary[] =
  completedSetMatchDetailFixtures.map(matchSummaryFromDetail);

const liveMatchSummary = baseMatchesFixture.find(
  (match) => match.id === REVIEW_LIVE_MATCH_ID,
);
const failedMatchSummary = baseMatchesFixture.find(
  (match) => match.id === REVIEW_FAILED_MATCH_ID,
);
if (!liveMatchSummary || !failedMatchSummary) {
  throw new Error('The review match feed templates are incomplete.');
}

/** Feed order mirrors the API: newest first, and every set game is discoverable. */
export const matchesFixture = [
  liveMatchSummary,
  ...[...setMatchSummaryFixtures].reverse(),
  failedMatchSummary,
] satisfies MatchSummary[];

function historyRowForSetGame(
  spec: ReviewSetGameSpec,
  botId: string,
): BotMatchHistory['matches'][number] {
  const isPincer = botId === REVIEW_PINCER_ID;
  const botSlot = isPincer ? spec.pincerSlot : spec.pincerSlot === 0 ? 1 : 0;
  const opponent = isPincer
    ? setParticipantSnapshots.bastille
    : setParticipantSnapshots.pincer;
  return {
    id: spec.id,
    mapId: spec.mapId,
    status: 'Completed',
    broadcasting: false,
    matchSetId: REVIEW_SET_ID,
    setGame: spec.game,
    createdAt: spec.createdAt,
    outcome: botSlot === 0 ? 'Win' : 'Loss',
    opponent: {
      botId: opponent.botId,
      nameSnapshot: opponent.nameSnapshot,
      ownerDisplayNameSnapshot: opponent.ownerDisplayNameSnapshot,
      accentSnapshot: opponent.accentSnapshot,
      lookIdSnapshot: opponent.lookIdSnapshot,
    },
  };
}

function liveHistoryRow(
  opponentId: typeof REVIEW_WARDEN_ID | typeof REVIEW_RAMPART_ID,
): BotMatchHistory['matches'][number] {
  const opponent =
    opponentId === REVIEW_WARDEN_ID
      ? currentLeaderboardEntries.find(
          (entry) => entry.id === REVIEW_WARDEN_ID,
        )
      : currentLeaderboardEntries.find(
          (entry) => entry.id === REVIEW_RAMPART_ID,
        );
  if (!opponent) throw new Error('The live review opponent is missing.');
  return {
    id: REVIEW_LIVE_MATCH_ID,
    mapId: 'arena-01',
    status: 'Completed',
    broadcasting: true,
    matchSetId: null,
    setGame: null,
    createdAt: '2026-07-27T21:20:00Z',
    outcome: null,
    opponent: {
      botId: opponent.id,
      nameSnapshot: opponent.name,
      ownerDisplayNameSnapshot: opponent.owner,
      accentSnapshot: opponent.accent,
      lookIdSnapshot: opponent.lookId,
    },
  };
}

function botHistory(botId: string): BotMatchHistory {
  const statistics = botStatisticsFixtures[botId];
  if (!statistics) throw new Error(`Missing review statistics for ${botId}.`);

  let matches: BotMatchHistory['matches'] = [];
  if (botId === REVIEW_PINCER_ID || botId === REVIEW_BASTILLE_ID) {
    matches = [
      ...[...reviewSetGameSpecs]
        .reverse()
        .map((spec) => historyRowForSetGame(spec, botId)),
      ...(botId === REVIEW_PINCER_ID
        ? baseBotMatchHistoryFixture.matches
        : []),
    ];
  } else if (botId === REVIEW_WARDEN_ID) {
    matches = [liveHistoryRow(REVIEW_RAMPART_ID)];
  } else if (botId === REVIEW_RAMPART_ID) {
    matches = [liveHistoryRow(REVIEW_WARDEN_ID)];
  }

  return {
    wins: statistics.overall.wins,
    losses: statistics.overall.losses,
    draws: statistics.overall.draws,
    matches,
  };
}

export const botMatchHistoryFixtures: Readonly<
  Record<string, BotMatchHistory>
> = Object.fromEntries(
  botsFixture.map((bot) => [bot.id, botHistory(bot.id)]),
);

export const botMatchHistoryFixture =
  botMatchHistoryFixtures[REVIEW_PINCER_ID] ??
  (() => {
    throw new Error('The Pincer review history is missing.');
  })();

export const authProvidersFixture = {
  google: true,
} satisfies AuthProviders;

export const cosmeticCatalogFixture = {
  version: 3,
  items: [
    {
      key: 'bot-look:vanguard',
      kind: 'bot-look',
      id: 'vanguard',
      label: 'Vanguard',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:bulwark',
      kind: 'bot-look',
      id: 'bulwark',
      label: 'Bulwark',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:needle',
      kind: 'bot-look',
      id: 'needle',
      label: 'Needle',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:orbiter',
      kind: 'bot-look',
      id: 'orbiter',
      label: 'Orbiter',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:mantis',
      kind: 'bot-look',
      id: 'mantis',
      label: 'Mantis',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'achievement',
        sourceId: 'rating-1300',
        hint: 'Reach 1300 rating on an official ladder.',
      },
      owned: false,
      progress: { current: 1284, target: 1300, unit: 'rating' },
    },
    {
      key: 'bot-look:lancer',
      kind: 'bot-look',
      id: 'lancer',
      label: 'Lancer',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'achievement',
        sourceId: 'first-successful-build',
        hint: 'Successfully build your first bot version.',
      },
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:aureate-warden',
      kind: 'bot-look',
      id: 'aureate-warden',
      label: 'Aureate Warden',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'achievement',
        sourceId: 'ranked-matches-100',
        hint: 'Complete 100 ranked matches.',
      },
      owned: false,
      progress: { current: 66, target: 100, unit: 'ranked-matches' },
    },
    {
      key: 'bot-look:rift-runner',
      kind: 'bot-look',
      id: 'rift-runner',
      label: 'Rift Runner',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:mossback',
      kind: 'bot-look',
      id: 'mossback',
      label: 'Mossback',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:helio-kite',
      kind: 'bot-look',
      id: 'helio-kite',
      label: 'Helio Kite',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'helio-kite',
        hint: 'Available in the store.',
      },
      owned: true,
      progress: null,
    },
    {
      key: 'bot-look:scrap-jackal',
      kind: 'bot-look',
      id: 'scrap-jackal',
      label: 'Scrap Jackal',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'scrap-jackal',
        hint: 'Available in the store.',
      },
      owned: false,
      progress: null,
    },
    {
      key: 'bot-look:glass-manta',
      kind: 'bot-look',
      id: 'glass-manta',
      label: 'Glass Manta',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'glass-manta',
        hint: 'Available in the store.',
      },
      owned: false,
      progress: null,
    },
    {
      key: 'projectile-look:pulse-bolt',
      kind: 'projectile-look',
      id: 'pulse-bolt',
      label: 'Pulse Bolt',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:ion-orb',
      kind: 'projectile-look',
      id: 'ion-orb',
      label: 'Ion Orb',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:razor-shard',
      kind: 'projectile-look',
      id: 'razor-shard',
      label: 'Razor Shard',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:talon',
      kind: 'projectile-look',
      id: 'talon',
      label: 'Talon',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'achievement',
        sourceId: 'rating-1300',
        hint: 'Reach 1300 rating on an official ladder.',
      },
      owned: false,
      progress: { current: 1284, target: 1300, unit: 'rating' },
    },
    {
      key: 'projectile-look:arc-spark',
      kind: 'projectile-look',
      id: 'arc-spark',
      label: 'Arc Spark',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'challenge',
        sourceId: 'first-unranked-match',
        hint: 'Complete your first unranked challenge match.',
      },
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:regent-lance',
      kind: 'projectile-look',
      id: 'regent-lance',
      label: 'Regent Lance',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'achievement',
        sourceId: 'ranked-matches-100',
        hint: 'Complete 100 ranked matches.',
      },
      owned: false,
      progress: { current: 66, target: 100, unit: 'ranked-matches' },
    },
    {
      key: 'projectile-look:phase-needle',
      kind: 'projectile-look',
      id: 'phase-needle',
      label: 'Phase Needle',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:cinder-disc',
      kind: 'projectile-look',
      id: 'cinder-disc',
      label: 'Cinder Disc',
      availability: 'starter',
      unlock: null,
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:helix-dart',
      kind: 'projectile-look',
      id: 'helix-dart',
      label: 'Helix Dart',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'helio-kite',
        hint: 'Available in the store.',
      },
      owned: true,
      progress: null,
    },
    {
      key: 'projectile-look:gravity-knot',
      kind: 'projectile-look',
      id: 'gravity-knot',
      label: 'Gravity Knot',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'scrap-jackal',
        hint: 'Available in the store.',
      },
      owned: false,
      progress: null,
    },
    {
      key: 'projectile-look:prism-fan',
      kind: 'projectile-look',
      id: 'prism-fan',
      label: 'Prism Fan',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'glass-manta',
        hint: 'Available in the store.',
      },
      owned: false,
      progress: null,
    },
    {
      key: 'capacity:extra-daily-builds',
      kind: 'capacity',
      id: 'extra-daily-builds',
      label: 'Extra daily builds',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'extra-daily-builds',
        hint: 'Available in the store.',
      },
      owned: true,
      progress: null,
    },
    {
      key: 'capacity:extra-daily-ranked-sets',
      kind: 'capacity',
      id: 'extra-daily-ranked-sets',
      label: 'Extra daily ranked sets',
      availability: 'entitlement',
      unlock: {
        sourceKind: 'purchase',
        sourceId: 'extra-daily-ranked-sets',
        hint: 'Available in the store.',
      },
      owned: false,
      progress: null,
    },
  ],
} satisfies CosmeticCatalog;

export const storeFixture = {
  open: false,
  categories: [
    {
      id: 'appearance',
      label: 'Appearance',
      packs: [
        {
          id: 'helio-kite',
          label: 'Helio Kite',
          description: 'A sunlit racer and the darts it throws.',
          items: [
            {
              key: 'bot-look:helio-kite',
              kind: 'bot-look',
              id: 'helio-kite',
              label: 'Helio Kite',
            },
            {
              key: 'projectile-look:helix-dart',
              kind: 'projectile-look',
              id: 'helix-dart',
              label: 'Helix Dart',
            },
          ],
          owned: true,
          repeatable: false,
        },
        {
          id: 'scrap-jackal',
          label: 'Scrap Jackal',
          description: 'Salvaged plating, and a shot that drags what it hits.',
          items: [
            {
              key: 'bot-look:scrap-jackal',
              kind: 'bot-look',
              id: 'scrap-jackal',
              label: 'Scrap Jackal',
            },
            {
              key: 'projectile-look:gravity-knot',
              kind: 'projectile-look',
              id: 'gravity-knot',
              label: 'Gravity Knot',
            },
          ],
          owned: false,
          repeatable: false,
        },
        {
          id: 'glass-manta',
          label: 'Glass Manta',
          description: 'Refracting hull, refracting fire.',
          items: [
            {
              key: 'bot-look:glass-manta',
              kind: 'bot-look',
              id: 'glass-manta',
              label: 'Glass Manta',
            },
            {
              key: 'projectile-look:prism-fan',
              kind: 'projectile-look',
              id: 'prism-fan',
              label: 'Prism Fan',
            },
          ],
          owned: false,
          repeatable: false,
        },
      ],
    },
    {
      id: 'capacity',
      label: 'Your account',
      packs: [
        {
          id: 'extra-daily-builds',
          label: 'Extra daily builds',
          description:
            'Raises your daily build allowance by 30, so a long tuning session does not run out of turns.',
          items: [
            {
              key: 'capacity:extra-daily-builds',
              kind: 'capacity',
              id: 'extra-daily-builds',
              label: 'Extra daily builds',
            },
          ],
          owned: true,
          repeatable: true,
        },
        {
          id: 'extra-daily-ranked-sets',
          label: 'Extra daily ranked sets',
          description:
            'Adds 5 ranked sets to your daily allowance. Each set is six games.',
          items: [
            {
              key: 'capacity:extra-daily-ranked-sets',
              kind: 'capacity',
              id: 'extra-daily-ranked-sets',
              label: 'Extra daily ranked sets',
            },
          ],
          owned: false,
          repeatable: true,
        },
      ],
    },
  ],
} satisfies Store;

export const notificationsFixture = [] satisfies UserNotification[];
