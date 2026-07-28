import type {
  BotDetail,
  BotSummary,
  CreateLabsMatchRequest,
  LabsCatalog,
  LabsPlaylist,
} from './api';
import { explicitlySupportsContractProfile } from './botContractProfiles';

/**
 * All two-entrant experiments this owned bot can launch.
 *
 * Capability support belongs to the active artifact, not the bot generally. Historical
 * artifacts have null profile metadata and are therefore not inferred to support the
 * generic actor contract.
 */
export function eligibleLabsPlaylists(
  bot: BotDetail,
  catalog: LabsCatalog,
): LabsPlaylist[] {
  if (!bot.isOwner || !catalog.enabled) return [];

  const activeVersion = bot.versions.find((version) => version.isActive);
  if (!activeVersion) return [];

  return playlistsForContractProfiles(
    activeVersion.supportedContractProfiles,
    catalog,
  );
}

/**
 * Experiments an active roster entry can run.
 *
 * Ownership is deliberately not inferred from the public roster. Callers must join this
 * result with an authenticated ownership projection (Arena does that for the global Play
 * composer).
 */
export function eligibleLabsPlaylistsForRosterBot(
  bot: BotSummary,
  catalog: LabsCatalog,
): LabsPlaylist[] {
  if (!bot.activeVersion) return [];
  return playlistsForContractProfiles(
    bot.activeVersion.supportedContractProfiles,
    catalog,
  );
}

/** Owned roster entries with at least one compatible hosted experiment. */
export function eligibleOwnedLabsRoster(
  roster: readonly BotSummary[],
  catalog: LabsCatalog,
  ownedBotIds: ReadonlySet<string>,
): BotSummary[] {
  return roster.filter(
    (bot) =>
      ownedBotIds.has(bot.id) &&
      eligibleLabsPlaylistsForRosterBot(bot, catalog).length > 0,
  );
}

function playlistsForContractProfiles(
  supportedContractProfiles: readonly string[] | null,
  catalog: LabsCatalog,
): LabsPlaylist[] {
  if (!catalog.enabled) return [];

  return catalog.playlists.filter(
    (playlist) =>
      playlist.participantCount === 2 &&
      playlist.scoringTeamCount === 2 &&
      playlist.participantsPerTeam === 1 &&
      explicitlySupportsContractProfile(
        supportedContractProfiles,
        playlist.requiredContractProfileId,
      ),
  );
}

/** First eligible playlist retained for callers that only need an availability check. */
export function eligibleLabsPlaylist(
  bot: BotDetail,
  catalog: LabsCatalog,
): LabsPlaylist | undefined {
  return eligibleLabsPlaylists(bot, catalog)[0];
}

export function eligibleLabsOpponents(
  roster: readonly BotSummary[],
  entrantBotId: string,
  requiredProfile: string,
) {
  return roster.filter(
    (candidate) =>
      candidate.id !== entrantBotId &&
      candidate.activeVersion !== null &&
      explicitlySupportsContractProfile(
        candidate.activeVersion.supportedContractProfiles,
        requiredProfile,
      ),
  );
}

export function createLabsMatchRequest(
  playlistVersionId: string,
  entrantBotId: string,
  opponentBotId: string,
): CreateLabsMatchRequest {
  return {
    playlistVersionId,
    entrantBotIds: [entrantBotId, opponentBotId],
    seed: null,
  };
}
