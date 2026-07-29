namespace BotArena.App.Matches;

public sealed record ArenaCapabilitiesResponse(
    DuelArenaFormatResponse Format,
    ArenaAllowanceResponse UnrankedAllowance,
    RankedArenaAllowanceResponse RankedAllowance,
    IReadOnlyList<MatchPlayabilityResponse> Bots);
