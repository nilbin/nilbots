namespace BotArena.App.ArcRelay;

/// <summary>Keeps an older validation match from admitting a newer mind revision.</summary>
public static class ArcRelayPreflightSettlement
{
    public static bool ApplyIfCurrent(
        ArcRelayEntrant entrant,
        Guid matchId,
        int revision,
        int faults,
        DateTime completedAt)
    {
        if (entrant.PreflightMatchId != matchId || entrant.PreflightRevision != revision)
            return false;

        bool passed = faults == 0;
        entrant.PreflightStatus = passed
            ? ArcRelayPreflightStatus.Passed
            : ArcRelayPreflightStatus.Failed;
        entrant.PreflightFailure = passed
            ? null
            : $"Hosted validation recorded {faults} runtime fault(s).";
        entrant.UpdatedAt = completedAt;
        return true;
    }
}
