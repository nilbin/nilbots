namespace BotArena.App.ArcRelay;

/// <summary>One mutation boundary for post-match cohort ineligibility.</summary>
public static class ArcRelayEntrantSuspension
{
    public static void Apply(
        ArcRelayEntrant entrant,
        Guid matchId,
        IEnumerable<string> reasons,
        DateTime suspendedAt)
    {
        string[] distinct = reasons
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (distinct.Length == 0)
            throw new ArgumentException("A suspension needs at least one felt-degeneracy reason.", nameof(reasons));

        entrant.LadderOptedIn = false;
        entrant.LadderOptedInAt = null;
        entrant.SuspensionReason = string.Join(", ", distinct);
        entrant.SuspensionMatchId = matchId;
        entrant.SuspendedAt = suspendedAt;
        entrant.UpdatedAt = suspendedAt;
    }
}
