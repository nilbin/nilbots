using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Creates one fresh child in an explicitly targeted dormant slot owned by
/// the source participant. Placement and conflict policies are deterministic
/// and never award collection-order priority.
/// </summary>
public sealed record BoundedChildFabricationDefinition
    : ActorFabricationTransitionDefinition
{
    public BoundedChildFabricationDefinition(
        string transitionId,
        string actionId,
        IReadOnlyCollection<string> sourceFormIds,
        string outputFormId,
        string sourceRegionRoleId,
        string outputRegionRoleId,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind>
            requiredSourceTileTags,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind>
            requiredOutputTileTags,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind>
            forbiddenOutputTileTags,
        IReadOnlyList<ActorRelativePositionOffset> candidateOffsets,
        ActorFabricationDelayDefinition delay,
        ActorActionRejectionResult unavailablePlacementResult)
        : base(transitionId, actionId, sourceFormIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFormId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRegionRoleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRegionRoleId);
        ArgumentNullException.ThrowIfNull(requiredSourceTileTags);
        ArgumentNullException.ThrowIfNull(requiredOutputTileTags);
        ArgumentNullException.ThrowIfNull(forbiddenOutputTileTags);
        ArgumentNullException.ThrowIfNull(candidateOffsets);
        ArgumentNullException.ThrowIfNull(delay);
        if (!Enum.IsDefined(unavailablePlacementResult))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unavailablePlacementResult));
        }

        RequiredSourceTileTags = CanonicalTags(
            requiredSourceTileTags,
            nameof(requiredSourceTileTags));
        RequiredOutputTileTags = CanonicalTags(
            requiredOutputTileTags,
            nameof(requiredOutputTileTags));
        ForbiddenOutputTileTags = CanonicalTags(
            forbiddenOutputTileTags,
            nameof(forbiddenOutputTileTags));
        if (RequiredOutputTileTags
            .Intersect(ForbiddenOutputTileTags)
            .Any())
        {
            throw new ArgumentException(
                "Required and forbidden output tile-tag kinds cannot overlap.",
                nameof(forbiddenOutputTileTags));
        }

        ActorRelativePositionOffset[] offsets = [.. candidateOffsets];
        if (offsets.Length == 0)
        {
            throw new ArgumentException(
                "Fabrication needs at least one candidate offset.",
                nameof(candidateOffsets));
        }
        if (offsets.Distinct().Count() != offsets.Length)
        {
            throw new ArgumentException(
                "Fabrication candidate offsets must be unique.",
                nameof(candidateOffsets));
        }
        if (offsets.Any(offset => offset.Forward == 0 && offset.Right == 0))
        {
            throw new ArgumentException(
                "Fabrication cannot select the source's occupied tile because " +
                "the source life survives.",
                nameof(candidateOffsets));
        }

        OutputFormId = outputFormId;
        SourceRegionRoleId = sourceRegionRoleId;
        OutputRegionRoleId = outputRegionRoleId;
        CandidateOffsets = offsets.ToImmutableArray();
        Delay = delay;
        UnavailablePlacementResult = unavailablePlacementResult;
    }

    public override FabricationTransitionKind Kind =>
        FabricationTransitionKind.BoundedChild;

    public string OutputFormId { get; }
    public int OutputCount => 1;
    public string SourceRegionRoleId { get; }
    public string OutputRegionRoleId { get; }
    public ImmutableArray<ActorMapTileTagDefinition.TileTagKind>
        RequiredSourceTileTags { get; }
    public ImmutableArray<ActorMapTileTagDefinition.TileTagKind>
        RequiredOutputTileTags { get; }
    public ImmutableArray<ActorMapTileTagDefinition.TileTagKind>
        ForbiddenOutputTileTags { get; }
    public ImmutableArray<ActorRelativePositionOffset> CandidateOffsets
    {
        get;
    }
    public ActorFabricationDelayDefinition Delay { get; }
    public ActorActionRejectionResult UnavailablePlacementResult { get; }

    public TargetSlotKind TargetSlot =>
        TargetSlotKind
            .ExplicitReadyDormantSameParticipant;
    public ActorFabricationCandidateSnapshotKind CandidateSnapshot =>
        ActorFabricationCandidateSnapshotKind
            .PostMovementPreLifecycleQueueSnapshot;
    public ActorFabricationPositionSelectionKind PositionSelection =>
        ActorFabricationPositionSelectionKind.FirstEligibleDeclaredOffset;
    public ActorFabricationClaimScopeKind ClaimScope =>
        ActorFabricationClaimScopeKind.SelectedTargetSlotAndTile;
    public ActorFabricationConflictResolutionKind ConflictResolution =>
        ActorFabricationConflictResolutionKind
            .BlockIntersectingLifecycleClaimComponents;
    public SourceDispositionKind SourceDisposition =>
        SourceDispositionKind.SourceLifeSurvives;
    public ChildInitialStateKind ChildInitialState =>
        ChildInitialStateKind
            .FreshRuntimeEmptyMemoryTargetFormDefaultsCanActOnCreationTick;
    public OutputFacingKind OutputFacing =>
        OutputFacingKind.ParticipantOutputRegionAssignmentFacing;
    public CandidateReferenceKind CandidateReference =>
        CandidateReferenceKind.QueueTimeSourcePose;
    public LineageKind Lineage =>
        LineageKind.ParentIsFabricatingLifeSourceGenerationPlusOne;
    public OutputHealthKind OutputHealth =>
        OutputHealthKind.TargetFormMaximum;
    public SpawnReasonKind SpawnReason =>
        SpawnReasonKind.FabricationOrRebuildFromTargetSlotHistory;
    public ActorFabricationOffsetArithmeticKind OffsetArithmetic =>
        ActorFabricationOffsetArithmeticKind.CheckedInt64ThenMapBounds;
    public OutstandingBundleKind OutstandingBundles =>
        OutstandingBundleKind
            .SourceMayQueueMultipleBundlesLimitedByDistinctReservedTargetsAndTiles;

    public enum TargetSlotKind
    {
        /// <summary>
        /// The submitted target must be a non-source slot on the same scoring
        /// team and controller participant, made ready either by its initial
        /// unlock or by completion of its rebuild delay.
        /// </summary>
        ExplicitReadyDormantSameParticipant = 0,
    }

    public enum ActorFabricationCandidateSnapshotKind
    {
        PostMovementPreLifecycleQueueSnapshot = 0,
    }

    public enum ActorFabricationPositionSelectionKind
    {
        FirstEligibleDeclaredOffset = 0,
    }

    public enum ActorFabricationClaimScopeKind
    {
        SelectedTargetSlotAndTile = 0,
    }

    public enum ActorFabricationConflictResolutionKind
    {
        BlockIntersectingLifecycleClaimComponents = 0,
    }

    public enum SourceDispositionKind
    {
        SourceLifeSurvives = 0,
    }

    public enum ChildInitialStateKind
    {
        FreshRuntimeEmptyMemoryTargetFormDefaultsCanActOnCreationTick = 0,
    }

    public enum OutputFacingKind
    {
        ParticipantOutputRegionAssignmentFacing = 0,
    }

    public enum CandidateReferenceKind
    {
        QueueTimeSourcePose = 0,
    }

    public enum LineageKind
    {
        ParentIsFabricatingLifeSourceGenerationPlusOne = 0,
    }

    public enum OutputHealthKind
    {
        TargetFormMaximum = 0,
    }

    public enum SpawnReasonKind
    {
        FabricationOrRebuildFromTargetSlotHistory = 0,
    }

    public enum ActorFabricationOffsetArithmeticKind
    {
        CheckedInt64ThenMapBounds = 0,
    }

    public enum OutstandingBundleKind
    {
        /// <summary>
        /// A surviving source may queue at most one bundle per action tick and
        /// may own several outstanding bundles. Bounded stable-slot capacity
        /// plus exclusive target-slot/tile reservations is the hard limit.
        /// </summary>
        SourceMayQueueMultipleBundlesLimitedByDistinctReservedTargetsAndTiles = 0,
    }

    private static ImmutableArray<
        ActorMapTileTagDefinition.TileTagKind> CanonicalTags(
            IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> tags,
            string parameterName)
    {
        ActorMapTileTagDefinition.TileTagKind[] snapshot = [.. tags];
        if (snapshot.Any(tag => !Enum.IsDefined(tag)))
            throw new ArgumentOutOfRangeException(parameterName);
        if (snapshot.Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Tile-tag kinds must be unique.",
                parameterName);
        }

        return snapshot
            .Order()
            .ToImmutableArray();
    }
}
