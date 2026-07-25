namespace BotArena.App.Cosmetics;

public static class CosmeticUnlockEvents
{
    public const string Achievement = "achievement";
    public const string Challenge = "challenge";
    public const string FirstSuccessfulBuild = "first-successful-build";
    public const string FirstUnrankedMatch = "first-unranked-match";
    public const string RankedMatches100 = "ranked-matches-100";
    /// <summary>Reaching the rating once is enough. The ledger grant is permanent, so
    /// falling back down the ladder never costs an account the look it earned.</summary>
    public const string Rating1300 = "rating-1300";
}
