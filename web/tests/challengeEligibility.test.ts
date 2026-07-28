import assert from 'node:assert/strict';
import test from 'node:test';
import type {
  BotDetail,
  BotSummary,
  LabsCatalog,
} from '../src/site/api';
import {
  detailBotSupportsLegacyDuel,
  explicitlySupportsContractProfile,
  LEGACY_DUEL_CONTRACT_PROFILE,
  rosterBotSupportsLegacyDuel,
  supportsLegacyDuel,
} from '../src/site/botContractProfiles';
import { eligibleLabsPlaylist } from '../src/site/labs';

const GENERIC_PROFILE = 'generic-actor-match-2';

test('legacy Duel treats omitted or null historical metadata as compatible', () => {
  assert.equal(supportsLegacyDuel(undefined), true);
  assert.equal(supportsLegacyDuel(null), true);
  assert.equal(supportsLegacyDuel([GENERIC_PROFILE]), false);
  assert.equal(
    supportsLegacyDuel([
      GENERIC_PROFILE,
      LEGACY_DUEL_CONTRACT_PROFILE,
    ]),
    true,
  );
  assert.equal(
    explicitlySupportsContractProfile(undefined, GENERIC_PROFILE),
    false,
  );
});

test('legacy Duel eligibility follows only the active bot version', () => {
  const bot = botDetail([
    botVersion(false, [LEGACY_DUEL_CONTRACT_PROFILE]),
    botVersion(true, [GENERIC_PROFILE]),
  ]);

  assert.equal(detailBotSupportsLegacyDuel(bot), false);
  assert.equal(
    rosterBotSupportsLegacyDuel(botSummary([GENERIC_PROFILE])),
    false,
  );
  assert.equal(rosterBotSupportsLegacyDuel(botSummary(null)), true);
});

test('an owned generic-only bot can enter Labs without appearing Duel-compatible', () => {
  const bot = botDetail([botVersion(true, [GENERIC_PROFILE])]);
  const catalog: LabsCatalog = {
    enabled: true,
    playlists: [
      {
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
      },
    ],
  };

  assert.equal(detailBotSupportsLegacyDuel(bot), false);
  assert.equal(
    eligibleLabsPlaylist(bot, catalog)?.key,
    'frontline-labs',
  );
});

function botDetail(versions: BotDetail['versions']): BotDetail {
  return {
    id: '20000000-0000-0000-0000-000000000001',
    name: 'Entrant',
    slug: 'entrant',
    accent: '#00ffff',
    lookId: 'default',
    projectileLookId: 'default',
    createdAt: '2026-07-28T00:00:00Z',
    owner: 'Owner',
    isOwner: true,
    currentStanding: null,
    versions,
  };
}

function botVersion(
  isActive: boolean,
  supportedContractProfiles: string[] | null,
): BotDetail['versions'][number] {
  return {
    id: isActive
      ? '30000000-0000-0000-0000-000000000002'
      : '30000000-0000-0000-0000-000000000001',
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
  supportedContractProfiles: string[] | null,
): BotSummary {
  return {
    id: '40000000-0000-0000-0000-000000000001',
    name: 'Opponent',
    slug: 'opponent',
    accent: '#00ffff',
    lookId: 'default',
    projectileLookId: 'default',
    createdAt: '2026-07-28T00:00:00Z',
    ratings: [],
    owner: 'Opponent owner',
    activeVersion: {
      id: '50000000-0000-0000-0000-000000000001',
      versionNumber: 1,
      artifactHash: 'abc',
      supportedContractProfiles,
    },
    versionCount: 1,
    currentStanding: null,
  };
}
