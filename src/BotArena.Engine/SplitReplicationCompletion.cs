using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Completion-time result for one previously reserved Split bundle.</summary>
public sealed record SplitReplicationCompletion(
    SplitReplicationReservation Reservation,
    SplitReplicationCompletion.SplitCompletionOutcomeKind Outcome,
    SplitReplicationCompletion.SplitCancellationReason? Reason,
    ImmutableArray<SplitReplicationSpawn> Descendants)
{
    public enum SplitCompletionOutcomeKind
    {
        Completed = 0,
        Cancelled = 1,
    }

    public enum SplitCancellationReason
    {
        SourceUnavailable = 0,
        SourceIdentityChanged = 1,
        SourceStateChanged = 2,
        InsufficientHealth = 3,
    }
}
