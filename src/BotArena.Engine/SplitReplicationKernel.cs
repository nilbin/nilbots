using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Pure queue/completion kernel for the first bounded Split semantic. The
/// caller owns lifecycle state mutation; this type owns candidate selection,
/// atomic claims, all-block conflicts, and completion-time health division.
/// </summary>
public sealed class SplitReplicationKernel
{
    private readonly ActorResolvedMatchDefinition _definition;
    private readonly Dictionary<string, SplitReplicationTransitionDefinition>
        _transitions;
    private readonly Dictionary<(int TeamId, int UnitId), PublicUnitSlot>
        _topologySlots;
    private readonly Dictionary<
        (int TeamId, int UnitId),
        ActorUnitSlotLifecycleAssignmentDefinition> _assignments;
    private readonly Dictionary<string, ActorFormDefinition> _forms;
    private readonly HashSet<Position> _automaticReturnTiles;

    public SplitReplicationKernel(ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _definition = definition;
        _transitions = definition.Rules.ReplicationTransitions
            .OfType<SplitReplicationTransitionDefinition>()
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
    /// Selects candidates from one immutable post-movement snapshot. Bundles
    /// that intersect another bundle's selected slot or tile all block; they
    /// do not retry later offsets after discovering the conflict.
    /// </summary>
    public SplitReplicationBatchResult ReserveBatch(
        int tick,
        IReadOnlyCollection<SplitReplicationRequest> requests,
        IReadOnlyCollection<SplitReplicationActorSnapshot> activeActors,
        IReadOnlyCollection<SplitReplicationSlotSnapshot> slots,
        IReadOnlyCollection<Position> existingLifecycleTileClaims)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(activeActors);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(existingLifecycleTileClaims);

        SplitReplicationRequest[] requestSnapshot = [.. requests];
        ValidateRequests(requestSnapshot);
        SplitReplicationRequest[] orderedRequests = requestSnapshot
            .OrderBy(request => request.SourceActorId)
            .ThenBy(request => request.TransitionId, StringComparer.Ordinal)
            .ThenBy(request => request.OperationId, StringComparer.Ordinal)
            .ToArray();

        Dictionary<ActorIdentity, SplitReplicationActorSnapshot> actorsById =
            SnapshotActors(activeActors);
        Dictionary<
            (int TeamId, int UnitId),
            SplitReplicationSlotSnapshot> slotsById =
            SnapshotSlots(slots, actorsById);
        HashSet<Position> occupiedPositions = actorsById.Values
            .Select(actor => actor.Position)
            .ToHashSet();
        if (occupiedPositions.Count != actorsById.Count)
        {
            throw new ArgumentException(
                "Active Split actor positions must be unique.",
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

        Candidate[] candidates = orderedRequests
            .Select(request => BuildCandidate(
                tick,
                request,
                actorsById,
                slotsById,
                occupiedPositions,
                preclaimedTiles))
            .ToArray();

        var conflicted = new HashSet<int>();
        for (int left = 0; left < candidates.Length; left++)
        {
            if (candidates[left].Reservation is null)
                continue;
            for (int right = left + 1; right < candidates.Length; right++)
            {
                if (candidates[right].Reservation is null)
                    continue;
                if (ClaimsIntersect(
                        candidates[left].Reservation!,
                        candidates[right].Reservation!))
                {
                    conflicted.Add(left);
                    conflicted.Add(right);
                }
            }
        }

        return new SplitReplicationBatchResult(
            candidates.Select((candidate, index) =>
            {
                if (conflicted.Contains(index))
                {
                    return Blocked(
                        candidate.Request,
                        SplitReplicationReservationOutcome
                            .SplitReservationBlockReason
                            .ConflictingReservation);
                }
                return candidate.Outcome;
            }).ToImmutableArray());
    }

    /// <summary>
    /// Rechecks the surviving source at the due tick and distributes its
    /// current health. The queue-time value is never used to create health.
    /// </summary>
    public SplitReplicationCompletion Complete(
        int tick,
        SplitReplicationReservation reservation,
        SplitReplicationActorSnapshot? source)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(reservation);
        if (!_transitions.TryGetValue(
                reservation.TransitionId,
                out SplitReplicationTransitionDefinition? transition))
        {
            throw new ArgumentException(
                "Reservation references an unknown Split transition.",
                nameof(reservation));
        }
        ValidateReservation(reservation, transition);
        if (tick != reservation.DueTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "A Split reservation completes only at its exact due tick.");
        }

        SplitReplicationCompletion? cancelled =
            ValidateCompletionSource(reservation, transition, source);
        if (cancelled is not null)
            return cancelled;

        int dividedHealth = source!.Health / transition.DescendantCount;
        if (dividedHealth < transition.Health.MinimumHealthPerDescendant)
        {
            return Cancelled(
                reservation,
                SplitReplicationCompletion.SplitCancellationReason
                    .InsufficientHealth);
        }

        int maximumHealth = _forms[transition.OutputFormId].MaxHealth;
        int descendantHealth = Math.Min(dividedHealth, maximumHealth);
        ImmutableArray<SplitReplicationSpawn> descendants =
            reservation.Descendants
                .Select(descendant => new SplitReplicationSpawn(
                    descendant.TeamId,
                    descendant.UnitId,
                    descendant.FormId,
                    descendant.Generation,
                    descendantHealth,
                    descendant.Position))
                .ToImmutableArray();
        return new SplitReplicationCompletion(
            reservation,
            SplitReplicationCompletion.SplitCompletionOutcomeKind.Completed,
            Reason: null,
            descendants);
    }

    private void ValidateReservation(
        SplitReplicationReservation reservation,
        SplitReplicationTransitionDefinition transition)
    {
        SplitReplicationReservedDescendant? first =
            reservation.Descendants.IsDefaultOrEmpty
                ? null
                : reservation.Descendants[0];
        long expectedDueTick =
            (long)reservation.QueuedTick
            + transition.Windup.DurationTicks;
        bool invalid = reservation.SourceActorId is null
            || reservation.ParticipantId < 0
            || reservation.SourceGeneration < 0
            || string.IsNullOrWhiteSpace(reservation.SourceFormId)
            || !transition.SourceFormIds.Contains(
                reservation.SourceFormId,
                StringComparer.Ordinal)
            || reservation.SourceGeneration > transition.MaxSourceGeneration
            || reservation.SourceGeneration == int.MaxValue
            || !Enum.IsDefined(reservation.SourceFacing)
            || _definition.Map.IsWall(reservation.SourcePosition)
            || string.IsNullOrWhiteSpace(reservation.OperationId)
            || reservation.QueuedTick < 0
            || expectedDueTick > int.MaxValue
            || reservation.DueTick != expectedDueTick
            || reservation.SourceActorId.TeamId
                != first?.TeamId
            || reservation.SourceActorId.UnitId
                != first?.UnitId
            || !_topologySlots.TryGetValue(
                (reservation.SourceActorId.TeamId,
                 reservation.SourceActorId.UnitId),
                out PublicUnitSlot? sourceSlot)
            || sourceSlot.ControllerParticipantId != reservation.ParticipantId
            || reservation.Descendants.IsDefault
            || reservation.Descendants.Length != transition.DescendantCount
            || reservation.Descendants.Any(descendant =>
                descendant is null
                || descendant.TeamId != reservation.SourceActorId.TeamId
                || descendant.FormId != transition.OutputFormId
                || descendant.Generation
                    != (long)reservation.SourceGeneration + 1
                || !_topologySlots.TryGetValue(
                    (descendant.TeamId, descendant.UnitId),
                    out PublicUnitSlot? slot)
                || slot.ControllerParticipantId != reservation.ParticipantId
                || !_assignments[(descendant.TeamId, descendant.UnitId)]
                    .AllowedFormIds.Contains(
                        descendant.FormId,
                        StringComparer.Ordinal)
                || _definition.Map.IsWall(descendant.Position))
            || reservation.Descendants
                .Select(descendant =>
                    (descendant.TeamId, descendant.UnitId))
                .Distinct()
                .Count() != reservation.Descendants.Length
            || reservation.Descendants
                .Select(descendant => descendant.Position)
                .Distinct()
                .Count() != reservation.Descendants.Length
            || !HasCanonicalReservedSlotOrder(reservation)
            || !HasCanonicalReservedPositionOrder(
                reservation,
                transition);
        if (invalid)
        {
            throw new ArgumentException(
                "Split reservation does not match the resolved transition and topology.",
                nameof(reservation));
        }
    }

    private static bool HasCanonicalReservedSlotOrder(
        SplitReplicationReservation reservation)
    {
        int[] additionalUnitIds = reservation.Descendants
            .Skip(1)
            .Select(descendant => descendant.UnitId)
            .ToArray();
        return additionalUnitIds.SequenceEqual(
            additionalUnitIds.Order());
    }

    private bool HasCanonicalReservedPositionOrder(
        SplitReplicationReservation reservation,
        SplitReplicationTransitionDefinition transition)
    {
        var candidateIndexes = new Dictionary<Position, int>();
        for (int index = 0; index < transition.CandidateOffsets.Length; index++)
        {
            if (!TryApplyOffset(
                    reservation.SourcePosition,
                    reservation.SourceFacing,
                    transition.CandidateOffsets[index],
                    out Position position)
                || _definition.Map.IsWall(position)
                || _automaticReturnTiles.Contains(position))
            {
                continue;
            }

            candidateIndexes.Add(position, index);
        }

        int previousIndex = -1;
        foreach (SplitReplicationReservedDescendant descendant
                 in reservation.Descendants)
        {
            if (!candidateIndexes.TryGetValue(
                    descendant.Position,
                    out int candidateIndex)
                || candidateIndex <= previousIndex)
            {
                return false;
            }
            previousIndex = candidateIndex;
        }
        return true;
    }

    private Candidate BuildCandidate(
        int tick,
        SplitReplicationRequest request,
        IReadOnlyDictionary<
            ActorIdentity,
            SplitReplicationActorSnapshot> actorsById,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            SplitReplicationSlotSnapshot> slotsById,
        IReadOnlySet<Position> occupiedPositions,
        IReadOnlySet<Position> preclaimedTiles)
    {
        if (!_transitions.TryGetValue(
                request.TransitionId,
                out SplitReplicationTransitionDefinition? transition)
            || !actorsById.TryGetValue(
                request.SourceActorId,
                out SplitReplicationActorSnapshot? source)
            || !_topologySlots.TryGetValue(
                (request.SourceActorId.TeamId, request.SourceActorId.UnitId),
                out PublicUnitSlot? sourceSlot)
            || sourceSlot.ControllerParticipantId != source.ParticipantId)
        {
            return new Candidate(
                request,
                Blocked(
                    request,
                    SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .SourceUnavailable));
        }

        if (!IsEligibleSource(source, transition))
        {
                SplitReplicationReservationOutcome.SplitReservationBlockReason
                    reason =
                source.Health < transition.MinimumSourceHealth
                    ? SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .InsufficientHealth
                    : SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .SourceNotEligible;
            return new Candidate(request, Blocked(request, reason));
        }

        SplitReplicationSlotSnapshot dynamicSourceSlot =
            slotsById[(source.ActorId.TeamId, source.ActorId.UnitId)];
        if (dynamicSourceSlot.State
                != SplitReplicationSlotSnapshot.SplitSlotState.Active
            || dynamicSourceSlot.ActiveActorId != source.ActorId)
        {
            return new Candidate(
                request,
                Blocked(
                    request,
                    SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .SourceUnavailable));
        }

        List<PublicUnitSlot> outputSlots = SelectOutputSlots(
            source,
            transition,
            sourceSlot,
            slotsById);
        if (outputSlots.Count != transition.DescendantCount)
        {
            return new Candidate(
                request,
                Blocked(
                    request,
                    SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .InsufficientSlots));
        }

        List<Position> outputPositions = SelectOutputPositions(
            source,
            transition,
            occupiedPositions,
            preclaimedTiles);
        if (outputPositions.Count != transition.DescendantCount)
        {
            return new Candidate(
                request,
                Blocked(
                    request,
                    SplitReplicationReservationOutcome
                        .SplitReservationBlockReason
                        .InsufficientPositions));
        }

        int generation = checked(source.Generation + 1);
        ImmutableArray<SplitReplicationReservedDescendant> descendants =
            outputSlots.Zip(
                outputPositions,
                (slot, position) =>
                    new SplitReplicationReservedDescendant(
                        slot.TeamId,
                        slot.UnitId,
                        transition.OutputFormId,
                        generation,
                        position))
                .ToImmutableArray();
        int dueTick = checked(tick + transition.Windup.DurationTicks);
        var reservation = new SplitReplicationReservation(
            source.ActorId,
            source.ParticipantId,
            source.Generation,
            source.FormId,
            source.Position,
            source.Facing,
            transition.TransitionId,
            request.OperationId,
            tick,
            dueTick,
            descendants);
        return new Candidate(
            request,
            new SplitReplicationReservationOutcome(
                request,
                SplitReplicationReservationOutcome
                    .SplitReservationOutcomeKind.Reserved,
                Reason: null,
                reservation));
    }

    private List<PublicUnitSlot> SelectOutputSlots(
        SplitReplicationActorSnapshot source,
        SplitReplicationTransitionDefinition transition,
        PublicUnitSlot sourceSlot,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            SplitReplicationSlotSnapshot> dynamicSlots)
    {
        if (!_assignments[
                (sourceSlot.TeamId, sourceSlot.UnitId)]
            .AllowedFormIds.Contains(
                transition.OutputFormId,
                StringComparer.Ordinal))
        {
            return [];
        }

        var selected = new List<PublicUnitSlot> { sourceSlot };
        selected.AddRange(_definition.Topology.UnitSlots
            .Where(slot =>
                slot.TeamId == sourceSlot.TeamId
                && slot.ControllerParticipantId == source.ParticipantId
                && slot.UnitId != sourceSlot.UnitId
                && dynamicSlots[(slot.TeamId, slot.UnitId)].State
                    == SplitReplicationSlotSnapshot.SplitSlotState.Ready
                && _assignments[(slot.TeamId, slot.UnitId)]
                    .AllowedFormIds.Contains(
                        transition.OutputFormId,
                        StringComparer.Ordinal))
            .OrderBy(slot => slot.UnitId)
            .Take(transition.DescendantCount - 1));
        return selected;
    }

    private List<Position> SelectOutputPositions(
        SplitReplicationActorSnapshot source,
        SplitReplicationTransitionDefinition transition,
        IReadOnlySet<Position> occupiedPositions,
        IReadOnlySet<Position> preclaimedTiles)
    {
        var selected = new List<Position>(transition.DescendantCount);
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
                || preclaimedTiles.Contains(position)
                || occupiedPositions.Contains(position)
                    && position != source.Position)
            {
                continue;
            }

            selected.Add(position);
            if (selected.Count == transition.DescendantCount)
                break;
        }
        return selected;
    }

    private static bool IsEligibleSource(
        SplitReplicationActorSnapshot source,
        SplitReplicationTransitionDefinition transition) =>
        transition.SourceFormIds.Contains(
            source.FormId,
            StringComparer.Ordinal)
        && source.Generation <= transition.MaxSourceGeneration
        && source.Generation < int.MaxValue
        && (!transition.RequireNoPriorSameLifeTransition
            || !source.HasPriorSameLifeTransition)
        && !source.HasPendingSameLifeTransition
        && source.Health >= transition.MinimumSourceHealth;

    private static SplitReplicationCompletion? ValidateCompletionSource(
        SplitReplicationReservation reservation,
        SplitReplicationTransitionDefinition transition,
        SplitReplicationActorSnapshot? source)
    {
        if (source is null)
        {
            return Cancelled(
                reservation,
                SplitReplicationCompletion.SplitCancellationReason
                    .SourceUnavailable);
        }
        if (source.ActorId != reservation.SourceActorId
            || source.ParticipantId != reservation.ParticipantId)
        {
            return Cancelled(
                reservation,
                SplitReplicationCompletion.SplitCancellationReason
                    .SourceIdentityChanged);
        }
        if (!transition.SourceFormIds.Contains(
                source.FormId,
                StringComparer.Ordinal)
            || source.FormId != reservation.SourceFormId
            || source.Generation != reservation.SourceGeneration
            || source.Position != reservation.SourcePosition
            || source.Facing != reservation.SourceFacing
            || transition.RequireNoPriorSameLifeTransition
                && source.HasPriorSameLifeTransition
            || source.HasPendingSameLifeTransition
            || reservation.Descendants.IsDefaultOrEmpty
            || (long)source.Generation + 1
                != reservation.Descendants[0].Generation)
        {
            return Cancelled(
                reservation,
                SplitReplicationCompletion.SplitCancellationReason
                    .SourceStateChanged);
        }
        return null;
    }

    private static bool ClaimsIntersect(
        SplitReplicationReservation left,
        SplitReplicationReservation right)
    {
        var leftSlots = left.Descendants
            .Select(descendant => (descendant.TeamId, descendant.UnitId))
            .ToHashSet();
        var leftTiles = left.Descendants
            .Select(descendant => descendant.Position)
            .ToHashSet();
        return right.Descendants.Any(descendant =>
            leftSlots.Contains((descendant.TeamId, descendant.UnitId))
            || leftTiles.Contains(descendant.Position));
    }

    private Dictionary<
        ActorIdentity,
        SplitReplicationActorSnapshot> SnapshotActors(
        IReadOnlyCollection<SplitReplicationActorSnapshot> actors)
    {
        if (actors.Any(actor =>
                actor is null
                || actor.ActorId is null))
        {
            throw new ArgumentException(
                "Active Split actors cannot contain null entries or identities.",
                nameof(actors));
        }
        var snapshot = new Dictionary<
            ActorIdentity,
            SplitReplicationActorSnapshot>();
        foreach (SplitReplicationActorSnapshot actor in actors)
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
            bool healthValid = _forms.TryGetValue(
                    actor.FormId,
                    out ActorFormDefinition? form)
                && actor.Health <= form.MaxHealth;
            if (actor.ParticipantId < 0
                || actor.Generation < 0
                || actor.Health <= 0
                || string.IsNullOrWhiteSpace(actor.FormId)
                || !Enum.IsDefined(actor.Facing)
                || !topologyOwned
                || !formAllowed
                || !healthValid
                || _definition.Map.IsWall(actor.Position)
                || !snapshot.TryAdd(actor.ActorId, actor))
            {
                throw new ArgumentException(
                    "Active Split actors must have unique valid identities and state.",
                    nameof(actors));
            }
        }
        return snapshot;
    }

    private Dictionary<
        (int TeamId, int UnitId),
        SplitReplicationSlotSnapshot> SnapshotSlots(
        IReadOnlyCollection<SplitReplicationSlotSnapshot> slots,
        IReadOnlyDictionary<ActorIdentity, SplitReplicationActorSnapshot>
            actors)
    {
        if (slots.Any(slot => slot is null))
        {
            throw new ArgumentException(
                "Split slot snapshots cannot contain null entries.",
                nameof(slots));
        }
        var snapshot = new Dictionary<
            (int TeamId, int UnitId),
            SplitReplicationSlotSnapshot>();
        foreach (SplitReplicationSlotSnapshot slot in slots)
        {
            if (!Enum.IsDefined(slot.State)
                || !_topologySlots.ContainsKey((slot.TeamId, slot.UnitId))
                || !snapshot.TryAdd((slot.TeamId, slot.UnitId), slot)
                || slot.State
                    == SplitReplicationSlotSnapshot.SplitSlotState.Active
                    != (slot.ActiveActorId is not null)
                || slot.ActiveActorId is ActorIdentity actorId
                    && (actorId.TeamId != slot.TeamId
                        || actorId.UnitId != slot.UnitId
                        || !actors.ContainsKey(actorId)))
            {
                throw new ArgumentException(
                    "Split slot snapshots must exactly identify valid topology state.",
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
                "Split slot snapshots must exactly cover topology and active actors.",
                nameof(slots));
        }
        return snapshot;
    }

    private static void ValidateRequests(
        IReadOnlyCollection<SplitReplicationRequest> requests)
    {
        if (requests.Any(request => request is null))
        {
            throw new ArgumentException(
                "Split requests cannot contain null entries.",
                nameof(requests));
        }
        if (requests.Any(request =>
                request.SourceActorId is null
                || string.IsNullOrWhiteSpace(request.TransitionId)
                || string.IsNullOrWhiteSpace(request.OperationId))
            || requests
                .Select(request => request.SourceActorId)
                .Distinct()
                .Count() != requests.Count
            || requests
                .Select(request => request.OperationId)
                .Distinct(StringComparer.Ordinal)
                .Count() != requests.Count)
        {
            throw new ArgumentException(
                "Split requests require unique sources, unique non-blank operation IDs, and non-blank transition IDs.",
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

    private static SplitReplicationReservationOutcome Blocked(
        SplitReplicationRequest request,
        SplitReplicationReservationOutcome.SplitReservationBlockReason
            reason) =>
        new(
            request,
            SplitReplicationReservationOutcome
                .SplitReservationOutcomeKind.Blocked,
            reason,
            Reservation: null);

    private static SplitReplicationCompletion Cancelled(
        SplitReplicationReservation reservation,
        SplitReplicationCompletion.SplitCancellationReason reason) =>
        new(
            reservation,
            SplitReplicationCompletion.SplitCompletionOutcomeKind.Cancelled,
            reason,
            []);

    private sealed record Candidate(
        SplitReplicationRequest Request,
        SplitReplicationReservationOutcome Outcome)
    {
        public SplitReplicationReservation? Reservation =>
            Outcome.Reservation;
    }
}
