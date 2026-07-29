import assert from 'node:assert/strict';
import test from 'node:test';
import type {
  BotDetail,
  BotSummary,
  LabsCatalog,
  LabsPlaylist,
} from '../src/site/api';
import {
  createLabsMatchRequest,
  eligibleLabsOpponents,
  eligibleLabsPlaylist,
  eligibleLabsPlaylists,
  eligibleLabsPlaylistsForRosterBot,
  eligibleOwnedLabsRoster,
} from '../src/site/labs';

const GENERIC_PROFILE = 'generic-actor-match-2';

const playlist: LabsPlaylist = {
  playlistVersionId: '10000000-0000-0000-0000-000000000001',
  key: 'frontline-labs',
  displayName: 'Frontline Labs',
  version: 1,
  gameModeId: 'frontline',
  rulesetId: 'frontline-labs-1',
  matchFormatId: 'head-to-head',
  participantCount: 2,
  scoringTeamCount: 2,
  participantsPerTeam: 1,
  requiredContractProfileId: GENERIC_PROFILE,
};

const catalog: LabsCatalog = {
  enabled: true,
  playlists: [playlist],
};

test('Labs requires an owned bot whose active artifact explicitly supports the playlist', () => {
  assert.equal(
    eligibleLabsPlaylist(
      botDetail(true, [
        botVersion(false, [GENERIC_PROFILE]),
        botVersion(true, ['legacy-duel-0.1']),
      ]),
      catalog,
    ),
    undefined,
  );
  assert.equal(
    eligibleLabsPlaylist(
      botDetail(true, [botVersion(true, null)]),
      catalog,
    ),
    undefined,
  );
  assert.equal(
    eligibleLabsPlaylist(
      botDetail(false, [botVersion(true, [GENERIC_PROFILE])]),
      catalog,
    ),
    undefined,
  );
  assert.deepEqual(
    eligibleLabsPlaylist(
      botDetail(true, [botVersion(true, [GENERIC_PROFILE])]),
      catalog,
    ),
    playlist,
  );
});

test('Labs opponents are distinct active bots with the exact required profile', () => {
  const entrantId = '20000000-0000-0000-0000-000000000001';
  const eligible = botSummary(
    '20000000-0000-0000-0000-000000000002',
    'Eligible',
    [GENERIC_PROFILE],
  );
  const roster = [
    botSummary(entrantId, 'Self', [GENERIC_PROFILE]),
    eligible,
    botSummary(
      '20000000-0000-0000-0000-000000000003',
      'Legacy',
      ['legacy-duel-0.1'],
    ),
    botSummary(
      '20000000-0000-0000-0000-000000000004',
      'Historical',
      null,
    ),
    botSummary(
      '20000000-0000-0000-0000-000000000005',
      'Inactive',
      [GENERIC_PROFILE],
      false,
    ),
  ];

  assert.deepEqual(
    eligibleLabsOpponents(roster, entrantId, GENERIC_PROFILE),
    [eligible],
  );
});

test('Labs exposes every eligible experiment in catalog order', () => {
  const second = {
    ...playlist,
    playlistVersionId: '10000000-0000-0000-0000-000000000002',
    key: 'frontline-nightly',
    displayName: 'Frontline Nightly',
  };
  const unsupported = {
    ...playlist,
    playlistVersionId: '10000000-0000-0000-0000-000000000003',
    key: 'future-mode',
    requiredContractProfileId: 'future-contract-1',
  };

  assert.deepEqual(
    eligibleLabsPlaylists(
      botDetail(true, [botVersion(true, [GENERIC_PROFILE])]),
      { enabled: true, playlists: [playlist, unsupported, second] },
    ),
    [playlist, second],
  );
});

test('the roster eligibility seam uses only the active artifact profile', () => {
  const compatible = botSummary(
    '20000000-0000-0000-0000-000000000006',
    'Compatible',
    [GENERIC_PROFILE],
  );
  const incompatible = botSummary(
    '20000000-0000-0000-0000-000000000007',
    'Legacy',
    ['legacy-duel-0.1'],
  );

  assert.deepEqual(
    eligibleLabsPlaylistsForRosterBot(compatible, catalog),
    [playlist],
  );
  assert.deepEqual(
    eligibleLabsPlaylistsForRosterBot(incompatible, catalog),
    [],
  );
  assert.deepEqual(
    eligibleLabsPlaylistsForRosterBot(
      { ...compatible, activeVersion: null },
      catalog,
    ),
    [],
  );
});

test('the global Labs roster requires authoritative ownership as well as compatibility', () => {
  const owned = botSummary(
    '20000000-0000-0000-0000-000000000008',
    'Owned compatible',
    [GENERIC_PROFILE],
  );
  const publicBot = botSummary(
    '20000000-0000-0000-0000-000000000009',
    'Public compatible',
    [GENERIC_PROFILE],
  );
  const ownedIncompatible = botSummary(
    '20000000-0000-0000-0000-000000000010',
    'Owned legacy',
    ['legacy-duel-0.1'],
  );

  assert.deepEqual(
    eligibleOwnedLabsRoster(
      [owned, publicBot, ownedIncompatible],
      catalog,
      new Set([owned.id, ownedIncompatible.id]),
    ),
    [owned],
  );
});

test('Labs match creation keeps the owned bot in entrant slot zero', () => {
  assert.deepEqual(
    createLabsMatchRequest(
      playlist.playlistVersionId,
      '30000000-0000-0000-0000-000000000001',
      '30000000-0000-0000-0000-000000000002',
    ),
    {
      playlistVersionId: playlist.playlistVersionId,
      entrantBotIds: [
        '30000000-0000-0000-0000-000000000001',
        '30000000-0000-0000-0000-000000000002',
      ],
      seed: null,
    },
  );
});

function botDetail(
  isOwner: boolean,
  versions: BotDetail['versions'],
): BotDetail {
  return {
    id: '20000000-0000-0000-0000-000000000001',
    name: 'Entrant',
    slug: 'entrant',
    accent: '#00ffff',
    lookId: 'default',
    projectileLookId: 'default',
    createdAt: '2026-07-28T00:00:00Z',
    owner: 'Owner',
    isOwner,
    currentStanding: null,
    versions,
  };
}

function botVersion(
  isActive: boolean,
  supportedContractProfiles: string[] | null,
): BotDetail['versions'][number] {
  return {
    id: crypto.randomUUID(),
    versionNumber: isActive ? 2 : 1,
    status: 'Built',
    artifactHash: 'abc',
    isActive,
    createdAt: '2026-07-28T00:00:00Z',
    buildReceipt: null,
    buildLog: null,
    entryType: null,
    sources: null,
    supportedContractProfiles,
  };
}

function botSummary(
  id: string,
  name: string,
  supportedContractProfiles: string[] | null,
  active = true,
): BotSummary {
  return {
    id,
    name,
    slug: name.toLowerCase(),
    accent: '#00ffff',
    lookId: 'default',
    projectileLookId: 'default',
    createdAt: '2026-07-28T00:00:00Z',
    ratings: [],
    owner: `${name} owner`,
    activeVersion: active
      ? {
          id: crypto.randomUUID(),
          versionNumber: 1,
          artifactHash: 'abc',
          supportedContractProfiles,
        }
      : null,
    versionCount: active ? 1 : 0,
    currentStanding: null,
  };
}
