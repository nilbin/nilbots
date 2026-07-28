import type {
  BotDetail,
  BotSummary,
  CreateLabsMatchRequest,
  LabsCatalog,
  LabsPlaylist,
} from './api';
import { explicitlySupportsContractProfile } from './botContractProfiles';

/**
 * Select the first experiment this deliberately small two-entrant panel can launch.
 *
 * Capability support belongs to the active artifact, not the bot generally. Historical
 * artifacts have null profile metadata and are therefore not inferred to support the
 * generic actor contract.
 */
export function eligibleLabsPlaylist(
  bot: BotDetail,
  catalog: LabsCatalog,
): LabsPlaylist | undefined {
  if (!bot.isOwner || !catalog.enabled) return undefined;

  const activeVersion = bot.versions.find((version) => version.isActive);
  if (!activeVersion) return undefined;

  return catalog.playlists.find(
    (playlist) =>
      playlist.participantCount === 2 &&
      playlist.scoringTeamCount === 2 &&
      playlist.participantsPerTeam === 1 &&
      explicitlySupportsContractProfile(
        activeVersion.supportedContractProfiles,
        playlist.requiredContractProfileId,
      ),
  );
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
