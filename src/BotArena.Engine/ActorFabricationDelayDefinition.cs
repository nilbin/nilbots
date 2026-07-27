namespace BotArena.Engine;

/// <summary>
/// Source-preserving fabrication delay. Once a bundle is queued, its target
/// slot and tile stay reserved while the fabricator remains an ordinary
/// active life; later source destruction does not cancel the child.
/// </summary>
public sealed record ActorFabricationDelayDefinition
{
    public ActorFabricationDelayDefinition(int durationTicks)
    {
        if (durationTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationTicks));

        DurationTicks = durationTicks;
    }

    public int DurationTicks { get; }
    public SourceBehaviorKind SourceBehavior =>
        SourceBehaviorKind.UnchangedActiveLifeCanAct;
    public SourceDeathKind SourceDeath =>
        SourceDeathKind.DoesNotCancelQueuedFabrication;
    public ReservationKind Reservation =>
        ReservationKind.TargetSlotAndTileBlockUntilCompletion;
    public ActorFabricationCompletionKind Completion =>
        ActorFabricationCompletionKind.TickStartAfterDuration;
    public TickArithmeticKind TickArithmetic =>
        TickArithmeticKind.CheckedAddition;

    public enum SourceBehaviorKind
    {
        UnchangedActiveLifeCanAct = 0,
    }

    public enum SourceDeathKind
    {
        DoesNotCancelQueuedFabrication = 0,
    }

    public enum ReservationKind
    {
        TargetSlotAndTileBlockUntilCompletion = 0,
    }

    public enum ActorFabricationCompletionKind
    {
        TickStartAfterDuration = 0,
    }

    public enum TickArithmeticKind
    {
        CheckedAddition = 0,
    }
}
