namespace BotArena.App.Competition;

public static class ArcRelayLadderPolicy
{
    public const int MaximumOptedInPerAccount = 3;
    public const int MaximumMatchesPerEntrantPerDay = 6;
    public const int RecentOpponentAvoidanceHours = 24;
    public const int MaximumPairingsPerPass = 4;
    public const int MaximumQueuedOrRunningMatches = 16;
    public const string LegacyLaunchSeasonKey = "arc-relay-launch";
    public const string SeasonKey = "arc-relay-season-1";
    public const string SeasonName = "Season 1";
    public const int SeasonNumber = 1;
    public const int SeasonDurationDays = 56;
    public const string RatingSeedPolicyId = "fresh-1200-v1";
    public const string LadderName = "Arc Relay ranked";
}
