using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Bounded Split mechanics. The variant guarantees atomic reservation,
/// source-slot reuse followed by lowest-ID compatible dormant slots, source
/// retirement rather than destruction, fresh isolated descendant runtimes,
/// and cancellation when the targetable source dies before completion.
/// </summary>
public sealed record SplitReplicationTransitionDefinition
    : ActorReplicationTransitionDefinition
{
    public SplitReplicationTransitionDefinition(
        string transitionId,
        string actionId,
        IReadOnlyCollection<string> sourceFormIds,
        string outputFormId,
        int descendantCount,
        int maxSourceGeneration,
        bool requireNoPriorSameLifeTransition,
        ActorReplicationHealthDefinition health,
        IReadOnlyList<ActorRelativePositionOffset> candidateOffsets,
        ActorTransitionWindupDefinition windup)
        : base(transitionId, actionId, sourceFormIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFormId);
        if (descendantCount < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(descendantCount),
                "Replication must produce at least two descendant lives.");
        }
        if (maxSourceGeneration < 0)
            throw new ArgumentOutOfRangeException(nameof(maxSourceGeneration));
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(candidateOffsets);
        ArgumentNullException.ThrowIfNull(windup);
        if (windup.Completion
            != ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration)
        {
            throw new ArgumentException(
                "Split must complete at tick start after its declared windup.",
                nameof(windup));
        }

        ActorRelativePositionOffset[] offsets = [.. candidateOffsets];
        if (offsets.Length < descendantCount)
        {
            throw new ArgumentException(
                "Split needs at least one ordered candidate offset per descendant.",
                nameof(candidateOffsets));
        }
        if (offsets.Distinct().Count() != offsets.Length)
        {
            throw new ArgumentException(
                "Split candidate offsets must be unique.",
                nameof(candidateOffsets));
        }
        OutputFormId = outputFormId;
        DescendantCount = descendantCount;
        MaxSourceGeneration = maxSourceGeneration;
        RequireNoPriorSameLifeTransition =
            requireNoPriorSameLifeTransition;
        Health = health;
        try
        {
            MinimumSourceHealth = checked(
                descendantCount * health.MinimumHealthPerDescendant);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(descendantCount),
                descendantCount,
                "Split's minimum source-health threshold exceeds the supported integer range.");
        }
        CandidateOffsets = offsets.ToImmutableArray();
        Windup = windup;
    }

    public override ReplicationTransitionKind Kind =>
        ReplicationTransitionKind.Split;

    public string OutputFormId { get; }
    public int DescendantCount { get; }
    public int MaxSourceGeneration { get; }
    public bool RequireNoPriorSameLifeTransition { get; }
    public ActorReplicationHealthDefinition Health { get; }
    public int MinimumSourceHealth { get; }

    /// <summary>
    /// Semantic preference order in source-facing-relative coordinates.
    /// Unlike set-like catalogs, this collection is intentionally not sorted.
    /// </summary>
    public ImmutableArray<ActorRelativePositionOffset> CandidateOffsets
    {
        get;
    }

    public ActorTransitionWindupDefinition Windup { get; }

    public int DescendantGenerationIncrement => 1;
    public bool ReservationIsAtomic => true;
    public bool ConflictingBundlesAllBlock => true;
    public bool InsufficientHealthBlocks => true;
    public bool ReuseSourceSlotFirst => true;
    public bool AdditionalSlotsUseLowestCompatibleDormantId => true;
    public bool SourceRemainsTargetableUntilCompletion => true;
    public bool LethalSourceDamageCancels => true;
    public bool SourceRetirementCountsAsDestruction => false;
    public bool DescendantsUseFreshIsolatedRuntimes => true;
    public bool DescendantsInheritPrivateMemory => false;

    public ActorReplicationCandidateSnapshotKind CandidateSnapshot =>
        ActorReplicationCandidateSnapshotKind
            .PostMovementPreLifecycleReservationSnapshot;

    public ActorReplicationPositionSelectionKind PositionSelection =>
        ActorReplicationPositionSelectionKind.FirstEligibleDeclaredOffsets;

    public SlotSelectionKind SlotSelection =>
        SlotSelectionKind
            .SourceThenLowestCompatibleReadyDormantSameParticipant;

    public DescendantAssignmentKind DescendantAssignment =>
        DescendantAssignmentKind.ZipSlotOrderToPositionOrder;

    public ActorReplicationClaimScopeKind ClaimScope =>
        ActorReplicationClaimScopeKind.SelectedSlotsAndTilesOnly;

    public ActorReplicationConflictResolutionKind ConflictResolution =>
        ActorReplicationConflictResolutionKind
            .BlockIntersectingClaimComponents;

    public HealthEvaluationKind HealthEvaluation =>
        HealthEvaluationKind
            .CompletionTimeCurrentHealthCancelIfBelowMinimum;

    public ActorReplicationOffsetArithmeticKind OffsetArithmetic =>
        ActorReplicationOffsetArithmeticKind.CheckedInt64ThenMapBounds;

    public enum ActorReplicationCandidateSnapshotKind
    {
        PostMovementPreLifecycleReservationSnapshot = 0,
    }

    public enum ActorReplicationPositionSelectionKind
    {
        FirstEligibleDeclaredOffsets = 0,
    }

    public enum SlotSelectionKind
    {
        /// <summary>
        /// Compatibility includes the source scoring team, source controller
        /// participant, output form, and dormant state.
        /// </summary>
        /// <summary>
        /// "Ready" excludes a locked initial slot, a pending automatic
        /// return, an active life, a queued lifecycle action, and permanent
        /// post-destruction dormancy. A slot is eligible after its declared
        /// initial unlock or explicit-fabrication rebuild clock completes.
        /// </summary>
        SourceThenLowestCompatibleReadyDormantSameParticipant = 0,
    }

    public enum DescendantAssignmentKind
    {
        ZipSlotOrderToPositionOrder = 0,
    }

    public enum ActorReplicationClaimScopeKind
    {
        SelectedSlotsAndTilesOnly = 0,
    }

    public enum ActorReplicationConflictResolutionKind
    {
        /// <summary>
        /// Every bundle in a connected component of intersecting selected
        /// slot/tile claims blocks; no collection-order winner or fallback
        /// retry is permitted.
        /// </summary>
        BlockIntersectingClaimComponents = 0,
    }

    public enum HealthEvaluationKind
    {
        CompletionTimeCurrentHealthCancelIfBelowMinimum = 0,
    }

    public enum ActorReplicationOffsetArithmeticKind
    {
        CheckedInt64ThenMapBounds = 0,
    }
}
