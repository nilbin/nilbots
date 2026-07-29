import type {
  ArenaCapabilities,
  BotSummary,
  MatchPlayability,
} from './api';

/**
 * The Arena projection owns admission and ownership. Public bot responses still supply
 * names and appearance, so UI choices are an explicit ID join rather than a second
 * interpretation of build/profile metadata.
 */
export function indexArenaPlayability(
  rows: readonly MatchPlayability[],
): ReadonlyMap<string, MatchPlayability> {
  return new Map(rows.map((row) => [row.botId, row]));
}

export function playableArenaRoster(
  roster: readonly BotSummary[],
  capabilities: ArenaCapabilities,
): BotSummary[] {
  const byId = indexArenaPlayability(capabilities.bots);
  return roster.filter((bot) => byId.get(bot.id)?.playable === true);
}

export function ownedPlayableArenaRoster(
  roster: readonly BotSummary[],
  capabilities: ArenaCapabilities,
): BotSummary[] {
  const byId = indexArenaPlayability(capabilities.bots);
  return roster.filter((bot) => {
    const admission = byId.get(bot.id);
    return admission?.isOwned === true && admission.playable;
  });
}

export function arenaOpponents(
  playableRoster: readonly BotSummary[],
  entrantBotId: string | null | undefined,
): BotSummary[] {
  return playableRoster.filter((bot) => bot.id !== entrantBotId);
}

export function playableArenaBotIds(
  capabilities: ArenaCapabilities | null,
): ReadonlySet<string> {
  return new Set(
    (capabilities?.bots ?? [])
      .filter((bot) => bot.playable)
      .map((bot) => bot.botId),
  );
}

export function ownedPlayableArenaBotIds(
  capabilities: ArenaCapabilities | null,
): ReadonlySet<string> {
  return new Set(
    (capabilities?.bots ?? [])
      .filter((bot) => bot.isOwned && bot.playable)
      .map((bot) => bot.botId),
  );
}

export function ownedArenaBotIds(
  capabilities: ArenaCapabilities | null,
): ReadonlySet<string> {
  return new Set(
    (capabilities?.bots ?? [])
      .filter((bot) => bot.isOwned)
      .map((bot) => bot.botId),
  );
}
