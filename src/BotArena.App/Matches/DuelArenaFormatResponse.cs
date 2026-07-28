namespace BotArena.App.Matches;

public sealed record DuelArenaFormatResponse(
    string RulesVersion,
    string RequiredContractProfileId,
    ArenaUnrankedFormatResponse Unranked,
    ArenaRankedFormatResponse Ranked);
