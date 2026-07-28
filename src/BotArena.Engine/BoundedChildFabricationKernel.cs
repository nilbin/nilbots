using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Pure queue kernel for source-preserving bounded-child fabrication. It
/// captures lineage, pose, output facing, target slot, and output tile before
/// a family-neutral lifecycle arbiter decides the joint batch.
/// </summary>
public sealed class BoundedChildFabricationKernel
{
    private readonly ActorResolvedMatchDefinition _definition;
    private readonly Dictionary<string, BoundedChildFabricationDefinition>
        _transitions;
    private readonly Dictionary<(int TeamId, int UnitId), PublicUnitSlot>
        _topologySlots;
    private readonly Dictionary<
        (int TeamId, int UnitId),
        ActorUnitSlotLifecycleAssignmentDefinition> _assignments;
    private readonly Dictionary<string, ActorFormDefinition> _forms;
    private readonly Dictionary<
        (int ParticipantId, string RegionRoleId),
        ActorParticipantRegionAssignmentDefinition> _regionAssignments;
    private readonly Dictionary<string, ActorMapRegionDefinition> _regions;
    private readonly HashSet<Position> _automaticReturnTiles;

    public BoundedChildFabricationKernel(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _definition = definition;
        _transitions = definition.Rules.FabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .ToDictionary(
                transition => transition.TransitionId,
                StringComparer.Ordinal);
        _topologySlots = definition.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId));
        _assignments = definition.LifecycleAssignments.ToDictionary(
            assignment => (assignment.TeamId, assignment.UnitId));
        _forms = definition.Rules.Forms.ToDictionary(
            form => form.Id,
            StringComparer.Ordinal);
        _regionAssignments = definition.ParticipantRegionAssignments
            .ToDictionary(
                assignment =>
                    (assignment.ParticipantId, assignment.RegionRoleId));
        _regions = definition.Map.Regions.ToDictionary(
            region => region.RegionId,
            StringComparer.Ordinal);

        Dictionary<string, Position> spawnPositions =
            definition.Map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                anchor => anchor.Spawn.Position,
                StringComparer.Ordinal);
        Dictionary<string, ActorLifecycleProfileDefinition> profiles =
            definition.Rules.Lifecycle.Profiles.ToDictionary(
                profile => profile.ProfileId,
                StringComparer.Ordinal);
        _automaticReturnTiles = definition.LifecycleAssignments
            .Where(assignment =>
                profiles[assignment.LifecycleProfileId].DestructionPolicy
                    == ActorLifecycleProfileDefinition.DestructionPolicyKind
                        .AutomaticRespawn)
            .Select(assignment => assignment.AssignedRespawnSpawnId!)
            .Select(spawnId => spawnPositions[spawnId])
            .ToHashSet();
    }

    /// <summary>
    /// Selects and arbitrates one fabrication-only batch. Generic sessions
    /// use the internal provisional seam to arbitrate jointly with Split.
    /// </summary>
    public BoundedChildFabricationBatchResult ReserveBatch(
        int tick,
        IReadOnlyCollection<BoundedChildFabricationRequest> requests,
        IReadOnlyCollection<BoundedChildFabricationActorSnapshot>
            activeActors,
        IReadOnlyCollection<BoundedChildFabricationSlotSnapshot> slots,
        IReadOnlyCollection<Position> existingLifecycleTileClaims)
    {
        ImmutableArray<BoundedChildFabricationReservationOutcome>
            provisional = BuildProvisionalBatch(
                tick,
                requests,
                activeActors,
                slots,
                existingLifecycleTileClaims);
        ImmutableHashSet<string> blocked =
            ActorLifecycleReservationArbiter.BlockedOperationIds(
                provisional
                    .Where(outcome => outcome.Reservation is not null)
                    .Select(outcome =>
                        LifecycleClaim(outcome.Reservation!)));
        return new BoundedChildFabricationBatchResult(
            FinalizeBatch(provisional, blocked));
    }

    internal ImmutableArray<BoundedChildFabricationReservationOutcome>
        BuildProvisionalBatch(
            int tick,
            IReadOnlyCollection<BoundedChildFabricationRequest> requests,
            IReadOnlyCollection<BoundedChildFabricationActorSnapshot>
                activeActors,
            IReadOnlyCollection<BoundedChildFabricationSlotSnapshot> slots,
            IReadOnlyCollection<Position> existingLifecycleTileClaims)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(activeActors);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(existingLifecycleTileClaims);

        BoundedChildFabricationRequest[] requestSnapshot = [.. requests];
        ValidateRequests(requestSnapshot);
        BoundedChildFabricationRequest[] orderedRequests = requestSnapshot
            .OrderBy(request => request.SourceActorId)
            .ThenBy(request => request.TransitionId, StringComparer.Ordinal)
            .ThenBy(request => request.TargetTeamId)
            .ThenBy(request => request.TargetUnitId)
            .ThenBy(request => request.OperationId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<
            ActorIdentity,
            BoundedChildFabricationActorSnapshot> actorsById =
            SnapshotActors(activeActors);
        Dictionary<
            (int TeamId, int UnitId),
            BoundedChildFabricationSlotSnapshot> slotsById =
            SnapshotSlots(slots, actorsById);
        HashSet<Position> occupiedPositions = actorsById.Values
            .Select(actor => actor.Position)
            .ToHashSet();
        if (occupiedPositions.Count != actorsById.Count)
        {
            throw new ArgumentException(
                "Active fabrication actor positions must be unique.",
                nameof(activeActors));
        }
        HashSet<Position> preclaimedTiles =
            existingLifecycleTileClaims.ToHashSet();
        if (preclaimedTiles.Count != existingLifecycleTileClaims.Count)
        {
            throw new ArgumentException(
                "Existing lifecycle tile claims must be unique.",
                nameof(existingLifecycleTileClaims));
        }

        return orderedRequests
            .Select(request => BuildCandidate(
                tick,
                request,
                actorsById,
                slotsById,
                occupiedPositions,
                preclaimedTiles))
            .ToImmutableArray();
    }

    internal static ImmutableArray<
        BoundedChildFabricationReservationOutcome> FinalizeBatch(
        IEnumerable<BoundedChildFabricationReservationOutcome> provisional,
        IReadOnlySet<string> blockedOperationIds)
    {
        ArgumentNullException.ThrowIfNull(provisional);
        ArgumentNullException.ThrowIfNull(blockedOperationIds);
        return provisional
            .Select(outcome =>
                outcome.Reservation is not null
                && blockedOperationIds.Contains(
                    outcome.Reservation.OperationId)
                    ? Blocked(
                        outcome.Request,
                        BoundedChildFabricationReservationOutcome
                            .FabricationReservationBlockReason
                            .ConflictingReservation)
                    : outcome)
            .ToImmutableArray();
    }

    internal static ActorLifecycleReservationClaim LifecycleClaim(
        BoundedChildFabricationProvisionalReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return new ActorLifecycleReservationClaim(
            reservation.OperationId,
            ActorLifecycleReservationFamily.Fabrication,
            [
                new ActorLifecycleSlotClaim(
                    reservation.TargetTeamId,
                    reservation.TargetUnitId),
            ],
            [reservation.ReservedPosition]);
    }

    internal void ValidateReservationEvidence(
        BoundedChildFabricationProvisionalReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!_transitions.TryGetValue(
                reservation.TransitionId,
                out BoundedChildFabricationDefinition? transition))
        {
            throw new ArgumentException(
                "Reservation references an unknown fabrication transition.",
                nameof(reservation));
        }

        long expectedDueTick =
            (long)reservation.QueuedTick + transition.Delay.DurationTicks;
        bool invalid = reservation.SourceActorId is null
            || reservation.ParticipantId < 0
            || reservation.SourceGeneration < 0
            || reservation.SourceGeneration == int.MaxValue
            || string.IsNullOrWhiteSpace(reservation.SourceFormId)
            || !transition.SourceFormIds.Contains(
                reservation.SourceFormId,
                StringComparer.Ordinal)
            || !Enum.IsDefined(reservation.SourceFacing)
            || !Enum.IsDefined(reservation.OutputFacing)
            || string.IsNullOrWhiteSpace(reservation.OperationId)
            || reservation.QueuedTick < 0
            || expectedDueTick > int.MaxValue
            || reservation.DueTick != expectedDueTick
            || reservation.TargetTeamId
                != reservation.SourceActorId.TeamId
            || reservation.TargetGeneration
                != (long)reservation.SourceGeneration + 1
            || !string.Equals(
                reservation.TargetFormId,
                transition.OutputFormId,
                StringComparison.Ordinal)
            || !_topologySlots.TryGetValue(
                (reservation.SourceActorId.TeamId,
                    reservation.SourceActorId.UnitId),
                out PublicUnitSlot? sourceSlot)
            || sourceSlot.ControllerParticipantId
                != reservation.ParticipantId
            || !_topologySlots.TryGetValue(
                (reservation.TargetTeamId, reservation.TargetUnitId),
                out PublicUnitSlot? targetSlot)
            || targetSlot.ControllerParticipantId
                != reservation.ParticipantId
            || (targetSlot.TeamId, targetSlot.UnitId)
                == (sourceSlot.TeamId, sourceSlot.UnitId)
            || !_assignments[(targetSlot.TeamId, targetSlot.UnitId)]
                .AllowedFormIds.Contains(
                    transition.OutputFormId,
                    StringComparer.Ordinal)
            || !IsEligibleSourcePosition(
                reservation.ParticipantId,
                transition,
                reservation.SourcePosition)
            || !IsEligibleOutputPosition(
                reservation.ParticipantId,
                transition,
                reservation.ReservedPosition,
                out Direction outputFacing)
            || outputFacing != reservation.OutputFacing
            || _automaticReturnTiles.Contains(
                reservation.ReservedPosition)
            || !IsDeclaredOffset(
                reservation.SourcePosition,
                reservation.SourceFacing,
                transition,
                reservation.ReservedPosition);
        if (invalid)
        {
            throw new ArgumentException(
                "Fabrication reservation does not match the resolved transition, topology, and map bindings.",
                nameof(reservation));
        }
    }

    private BoundedChildFabricationReservationOutcome BuildCandidate(
        int tick,
        BoundedChildFabricationRequest request,
        IReadOnlyDictionary<
            ActorIdentity,
            BoundedChildFabricationActorSnapshot> actorsById,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            BoundedChildFabricationSlotSnapshot> slotsById,
        IReadOnlySet<Position> occupiedPositions,
        IReadOnlySet<Position> preclaimedTiles)
    {
        if (!_transitions.TryGetValue(
                request.TransitionId,
                out BoundedChildFabricationDefinition? transition)
            || !actorsById.TryGetValue(
                request.SourceActorId,
                out BoundedChildFabricationActorSnapshot? source)
            || !_topologySlots.TryGetValue(
                (request.SourceActorId.TeamId,
                    request.SourceActorId.UnitId),
                out PublicUnitSlot? sourceSlot)
            || sourceSlot.ControllerParticipantId != source.ParticipantId)
        {
            return Blocked(
                request,
                BoundedChildFabricationReservationOutcome
                    .FabricationReservationBlockReason.SourceUnavailable);
        }

        BoundedChildFabricationSlotSnapshot dynamicSource =
            slotsById[(source.ActorId.TeamId, source.ActorId.UnitId)];
        if (dynamicSource.State
                != BoundedChildFabricationSlotSnapshot
                    .FabricationSlotState.Active
            || dynamicSource.ActiveActorId != source.ActorId)
        {
            return Blocked(
                request,
                BoundedChildFabricationReservationOutcome
                    .FabricationReservationBlockReason.SourceUnavailable);
        }
        if (!IsSourceEligibleForRequest(source, transition))
        {
            return Blocked(
                request,
                BoundedChildFabricationReservationOutcome
                    .FabricationReservationBlockReason.SourceNotEligible);
        }

        if (!_topologySlots.TryGetValue(
                (request.TargetTeamId, request.TargetUnitId),
                out PublicUnitSlot? targetSlot)
            || request.TargetTeamId != source.ActorId.TeamId
            || targetSlot.ControllerParticipantId != source.ParticipantId
            || (targetSlot.TeamId, targetSlot.UnitId)
                == (sourceSlot.TeamId, sourceSlot.UnitId)
            || !_assignments[(targetSlot.TeamId, targetSlot.UnitId)]
                .AllowedFormIds.Contains(
                    transition.OutputFormId,
                    StringComparer.Ordinal)
            || slotsById[(targetSlot.TeamId, targetSlot.UnitId)].State
                != BoundedChildFabricationSlotSnapshot
                    .FabricationSlotState.Ready)
        {
            return Blocked(
                request,
                BoundedChildFabricationReservationOutcome
                    .FabricationReservationBlockReason.TargetUnavailable);
        }

        Position? selectedPosition = null;
        Direction outputFacing = default;
        foreach (ActorRelativePositionOffset offset
                 in transition.CandidateOffsets)
        {
            if (!TryApplyOffset(
                    source.Position,
                    source.Facing,
                    offset,
                    out Position position)
                || _definition.Map.IsWall(position)
                || _automaticReturnTiles.Contains(position)
                || occupiedPositions.Contains(position)
                || preclaimedTiles.Contains(position)
                || !IsEligibleOutputPosition(
                    source.ParticipantId,
                    transition,
                    position,
                    out outputFacing))
            {
                continue;
            }
            selectedPosition = position;
            break;
        }
        if (selectedPosition is null)
            return UnavailablePlacement(request, transition);

        int generation = checked(source.Generation + 1);
        int dueTick = checked(tick + transition.Delay.DurationTicks);
        var reservation =
            new BoundedChildFabricationProvisionalReservation(
                source.ActorId,
                source.ParticipantId,
                source.Generation,
                source.FormId,
                source.Position,
                source.Facing,
                transition.TransitionId,
                request.OperationId,
                targetSlot.TeamId,
                targetSlot.UnitId,
                transition.OutputFormId,
                generation,
                tick,
                dueTick,
                selectedPosition.Value,
                outputFacing);
        return new BoundedChildFabricationReservationOutcome(
            request,
            BoundedChildFabricationReservationOutcome
                .FabricationReservationOutcomeKind.Reserved,
            Reason: null,
            reservation);
    }

    /// <summary>
    /// Reports source-local eligibility that cannot be changed by another
    /// actor's simultaneous decision. Placement and reservation conflicts
    /// remain queue-time outcomes so this check does not leak hidden
    /// occupancy or rule out a tile another actor may vacate this tick.
    /// </summary>
    internal bool IsSourceEligibleForRequest(
        BoundedChildFabricationActorSnapshot source,
        BoundedChildFabricationDefinition transition)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transition);
        return _transitions.ContainsKey(transition.TransitionId)
            && transition.SourceFormIds.Contains(
                source.FormId,
                StringComparer.Ordinal)
            && source.Generation < int.MaxValue
            && IsEligibleSourcePosition(
                source.ParticipantId,
                transition,
                source.Position);
    }

    private bool IsEligibleSourcePosition(
        int participantId,
        BoundedChildFabricationDefinition transition,
        Position position) =>
        TryRegion(
            participantId,
            transition.SourceRegionRoleId,
            out _,
            out ActorMapRegionDefinition? region)
        && region.Tiles.Contains(position)
        && TileSatisfiesTags(
            position,
            transition.RequiredSourceTileTags,
            []);

    private bool IsEligibleOutputPosition(
        int participantId,
        BoundedChildFabricationDefinition transition,
        Position position,
        out Direction facing)
    {
        if (!TryRegion(
                participantId,
                transition.OutputRegionRoleId,
                out ActorParticipantRegionAssignmentDefinition? assignment,
                out ActorMapRegionDefinition? region)
            || !region.Tiles.Contains(position)
            || !TileSatisfiesTags(
                position,
                transition.RequiredOutputTileTags,
                transition.ForbiddenOutputTileTags))
        {
            facing = default;
            return false;
        }
        facing = assignment.Facing;
        return true;
    }

    private bool TryRegion(
        int participantId,
        string roleId,
        out ActorParticipantRegionAssignmentDefinition assignment,
        out ActorMapRegionDefinition region)
    {
        if (_regionAssignments.TryGetValue(
                (participantId, roleId),
                out ActorParticipantRegionAssignmentDefinition? found)
            && _regions.TryGetValue(
                found.MapRegionId,
                out ActorMapRegionDefinition? foundRegion)
            && foundRegion.Kind
                == ActorMapRegionDefinition.RegionKind.TransitionPlacement)
        {
            assignment = found;
            region = foundRegion;
            return true;
        }
        assignment = null!;
        region = null!;
        return false;
    }

    private bool TileSatisfiesTags(
        Position position,
        IReadOnlyCollection<
            ActorMapTileTagDefinition.TileTagKind> required,
        IReadOnlyCollection<
            ActorMapTileTagDefinition.TileTagKind> forbidden)
    {
        HashSet<ActorMapTileTagDefinition.TileTagKind> actual =
            _definition.Map.TileTags
                .Where(tag => tag.Tiles.Contains(position))
                .Select(tag => tag.Kind)
                .ToHashSet();
        return required.All(actual.Contains)
            && !forbidden.Any(actual.Contains);
    }

    private static bool IsDeclaredOffset(
        Position source,
        Direction facing,
        BoundedChildFabricationDefinition transition,
        Position output) =>
        transition.CandidateOffsets.Any(offset =>
            TryApplyOffset(source, facing, offset, out Position candidate)
            && candidate == output);

    private Dictionary<
        ActorIdentity,
        BoundedChildFabricationActorSnapshot> SnapshotActors(
        IReadOnlyCollection<BoundedChildFabricationActorSnapshot> actors)
    {
        if (actors.Any(actor =>
                actor is null || actor.ActorId is null))
        {
            throw new ArgumentException(
                "Active fabrication actors cannot contain null entries or identities.",
                nameof(actors));
        }
        var snapshot = new Dictionary<
            ActorIdentity,
            BoundedChildFabricationActorSnapshot>();
        foreach (BoundedChildFabricationActorSnapshot actor in actors)
        {
            bool topologyOwned = _topologySlots.TryGetValue(
                    (actor.ActorId.TeamId, actor.ActorId.UnitId),
                    out PublicUnitSlot? topologySlot)
                && topologySlot.ControllerParticipantId
                    == actor.ParticipantId;
            bool formAllowed = _assignments.TryGetValue(
                    (actor.ActorId.TeamId, actor.ActorId.UnitId),
                    out ActorUnitSlotLifecycleAssignmentDefinition? assignment)
                && assignment.AllowedFormIds.Contains(
                    actor.FormId,
                    StringComparer.Ordinal);
            if (actor.ParticipantId < 0
                || actor.Generation < 0
                || string.IsNullOrWhiteSpace(actor.FormId)
                || !Enum.IsDefined(actor.Facing)
                || !topologyOwned
                || !formAllowed
                || !_forms.ContainsKey(actor.FormId)
                || _definition.Map.IsWall(actor.Position)
                || !snapshot.TryAdd(actor.ActorId, actor))
            {
                throw new ArgumentException(
                    "Active fabrication actors must have unique valid identities and state.",
                    nameof(actors));
            }
        }
        return snapshot;
    }

    private Dictionary<
        (int TeamId, int UnitId),
        BoundedChildFabricationSlotSnapshot> SnapshotSlots(
        IReadOnlyCollection<BoundedChildFabricationSlotSnapshot> slots,
        IReadOnlyDictionary<
            ActorIdentity,
            BoundedChildFabricationActorSnapshot> actors)
    {
        if (slots.Any(slot => slot is null))
        {
            throw new ArgumentException(
                "Fabrication slot snapshots cannot contain null entries.",
                nameof(slots));
        }
        var snapshot = new Dictionary<
            (int TeamId, int UnitId),
            BoundedChildFabricationSlotSnapshot>();
        foreach (BoundedChildFabricationSlotSnapshot slot in slots)
        {
            if (!Enum.IsDefined(slot.State)
                || !_topologySlots.ContainsKey((slot.TeamId, slot.UnitId))
                || !snapshot.TryAdd((slot.TeamId, slot.UnitId), slot)
                || slot.State
                    == BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Active
                    != (slot.ActiveActorId is not null)
                || slot.ActiveActorId is ActorIdentity actorId
                    && (actorId.TeamId != slot.TeamId
                        || actorId.UnitId != slot.UnitId
                        || !actors.ContainsKey(actorId)))
            {
                throw new ArgumentException(
                    "Fabrication slot snapshots must exactly identify valid topology state.",
                    nameof(slots));
            }
        }
        if (snapshot.Count != _topologySlots.Count
            || _topologySlots.Keys.Any(key => !snapshot.ContainsKey(key))
            || actors.Keys.Any(actorId =>
                snapshot[(actorId.TeamId, actorId.UnitId)].ActiveActorId
                    != actorId))
        {
            throw new ArgumentException(
                "Fabrication slot snapshots must exactly cover topology and active actors.",
                nameof(slots));
        }
        return snapshot;
    }

    private static void ValidateRequests(
        IReadOnlyCollection<BoundedChildFabricationRequest> requests)
    {
        if (requests.Any(request => request is null)
            || requests.Any(request =>
                request.SourceActorId is null
                || string.IsNullOrWhiteSpace(request.TransitionId)
                || string.IsNullOrWhiteSpace(request.OperationId)
                || request.TargetTeamId < 0
                || request.TargetUnitId < 0)
            || requests.Select(request => request.SourceActorId)
                .Distinct().Count() != requests.Count
            || requests.Select(request => request.OperationId)
                .Distinct(StringComparer.Ordinal).Count() != requests.Count)
        {
            throw new ArgumentException(
                "Fabrication requests require unique sources, unique non-blank operation IDs, valid targets, and non-blank transition IDs.",
                nameof(requests));
        }
    }

    private static bool TryApplyOffset(
        Position source,
        Direction facing,
        ActorRelativePositionOffset offset,
        out Position result)
    {
        long x = source.X;
        long y = source.Y;
        switch (facing)
        {
            case Direction.North:
                x += offset.Right;
                y -= offset.Forward;
                break;
            case Direction.East:
                x += offset.Forward;
                y += offset.Right;
                break;
            case Direction.South:
                x -= offset.Right;
                y += offset.Forward;
                break;
            case Direction.West:
                x -= offset.Forward;
                y -= offset.Right;
                break;
            default:
                result = default;
                return false;
        }
        if (x is < int.MinValue or > int.MaxValue
            || y is < int.MinValue or > int.MaxValue)
        {
            result = default;
            return false;
        }
        result = new Position((int)x, (int)y);
        return true;
    }

    private static BoundedChildFabricationReservationOutcome
        UnavailablePlacement(
            BoundedChildFabricationRequest request,
            BoundedChildFabricationDefinition transition)
    {
        BoundedChildFabricationReservationOutcome
            .FabricationReservationOutcomeKind outcome =
            transition.UnavailablePlacementResult switch
            {
                ActorActionRejectionResult.Blocked =>
                    BoundedChildFabricationReservationOutcome
                        .FabricationReservationOutcomeKind.Blocked,
                ActorActionRejectionResult.Rejected =>
                    BoundedChildFabricationReservationOutcome
                        .FabricationReservationOutcomeKind.Rejected,
                ActorActionRejectionResult.Faulted =>
                    BoundedChildFabricationReservationOutcome
                        .FabricationReservationOutcomeKind.Faulted,
                _ => throw new InvalidOperationException(
                    "Unknown fabrication placement result."),
            };
        return new BoundedChildFabricationReservationOutcome(
            request,
            outcome,
            BoundedChildFabricationReservationOutcome
                .FabricationReservationBlockReason.InsufficientPositions,
            Reservation: null);
    }

    private static BoundedChildFabricationReservationOutcome Blocked(
        BoundedChildFabricationRequest request,
        BoundedChildFabricationReservationOutcome
            .FabricationReservationBlockReason reason) =>
        new(
            request,
            BoundedChildFabricationReservationOutcome
                .FabricationReservationOutcomeKind.Blocked,
            reason,
            Reservation: null);
}
