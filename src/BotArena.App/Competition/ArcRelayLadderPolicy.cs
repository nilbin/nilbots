namespace BotArena.App.Competition;

public static class ArcRelayLadderPolicy
{
    public const int MaximumOptedInPerAccount = 3;
    public const int MaximumMatchesPerEntrantPerDay = 6;
    public const int RecentOpponentAvoidanceHours = 24;
    public const int MaximumPairingsPerPass = 4;
    public const int MaximumQueuedOrRunningMatches = 16;
    public const string SeasonKey = "arc-relay-launch";
    public const string SeasonName = "Arc Relay Launch";
    public const string LadderName = "Arc Relay ranked";
}
