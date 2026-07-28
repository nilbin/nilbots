namespace BotArena.Engine;

/// <summary>
/// Fully disclosed pending-life behavior shared by queued form changes,
/// fabrication, and replication. A duration is tunable without leaving
/// movement, targetability, cancellation, or completion timing implicit.
/// </summary>
public sealed record ActorTransitionWindupDefinition
{
    public ActorTransitionWindupDefinition(
        int durationTicks,
        PendingActionKind pendingAction,
        SourceFormKind sourceForm,
        TargetabilityKind targetability,
        LethalDamageKind lethalDamage,
        ActorTransitionCompletionKind completion,
        PlacementReferenceKind placementReference)
    {
        if (durationTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationTicks));
        if (!Enum.IsDefined(pendingAction))
            throw new ArgumentOutOfRangeException(nameof(pendingAction));
        if (!Enum.IsDefined(sourceForm))
            throw new ArgumentOutOfRangeException(nameof(sourceForm));
        if (!Enum.IsDefined(targetability))
            throw new ArgumentOutOfRangeException(nameof(targetability));
        if (!Enum.IsDefined(lethalDamage))
            throw new ArgumentOutOfRangeException(nameof(lethalDamage));
        if (!Enum.IsDefined(completion))
            throw new ArgumentOutOfRangeException(nameof(completion));
        if (!Enum.IsDefined(placementReference))
        {
            throw new ArgumentOutOfRangeException(
                nameof(placementReference));
        }

        DurationTicks = durationTicks;
        PendingAction = pendingAction;
        SourceForm = sourceForm;
        Targetability = targetability;
        LethalDamage = lethalDamage;
        Completion = completion;
        PlacementReference = placementReference;
    }

    public int DurationTicks { get; }
    public PendingActionKind PendingAction { get; }
    public SourceFormKind SourceForm { get; }
    public TargetabilityKind Targetability { get; }
    public LethalDamageKind LethalDamage { get; }
    public ActorTransitionCompletionKind Completion { get; }
    public PlacementReferenceKind PlacementReference { get; }

    public enum PendingActionKind
    {
        WaitOnly = 0,
    }

    public enum SourceFormKind
    {
        RetainSourceForm = 0,
    }

    public enum TargetabilityKind
    {
        TargetableAndOccupiesTile = 0,
    }

    public enum LethalDamageKind
    {
        CancelTransition = 0,
    }

    public enum ActorTransitionCompletionKind
    {
        TickStartAfterDuration = 0,
        EndOfStartedTickPlusDurationMinusOneAfterModeUpdate = 1,
    }

    public enum PlacementReferenceKind
    {
        QueueTimePose = 0,
    }
}
