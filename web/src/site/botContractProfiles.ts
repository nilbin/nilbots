import type { BotDetail, BotSummary } from './api';

export const LEGACY_DUEL_CONTRACT_PROFILE = 'legacy-duel-0.1';

export function explicitlySupportsContractProfile(
  profiles: readonly string[] | null | undefined,
  requiredProfile: string,
) {
  return profiles?.includes(requiredProfile) ?? false;
}

/**
 * Profile metadata predates hosted capability admission. Historical APIs may omit it and
 * historical rows return null; both mean legacy-Duel-only. Once a compiler records an
 * explicit list, that list is authoritative.
 */
export function supportsLegacyDuel(
  profiles: readonly string[] | null | undefined,
) {
  if (profiles == null) return true;
  return explicitlySupportsContractProfile(
    profiles,
    LEGACY_DUEL_CONTRACT_PROFILE,
  );
}

export function detailBotSupportsLegacyDuel(bot: BotDetail) {
  const activeVersion = bot.versions.find((version) => version.isActive);
  return (
    activeVersion !== undefined &&
    supportsLegacyDuel(activeVersion.supportedContractProfiles)
  );
}

export function rosterBotSupportsLegacyDuel(bot: BotSummary) {
  return (
    bot.activeVersion !== null &&
    supportsLegacyDuel(bot.activeVersion.supportedContractProfiles)
  );
}
