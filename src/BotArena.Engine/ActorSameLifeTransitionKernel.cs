using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Pure route, queue, and completion kernel for data-driven same-life form
/// transitions. Callers own actor mutation, phase placement, and evidence.
/// </summary>
public sealed class ActorSameLifeTransitionKernel
{
    private readonly ActorResolvedMatchDefinition _definition;
    private readonly Dictionary<string, ActorFormTransitionDefinition>
        _transitions;
    private readonly Dictionary<string, ActorFormDefinition> _forms;
    private readonly Dictionary<string, ActorAttackProfileDefinition>
        _attacks;
    private readonly Dictionary<(int TeamId, int UnitId), PublicUnitSlot>
        _topologySlots;
    private readonly Dictionary<
        (int TeamId, int UnitId),
        ActorUnitSlotLifecycleAssignmentDefinition> _assignments;

    public ActorSameLifeTransitionKernel(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateSupportedTransitions(definition.Rules.SameLifeTransitions);

        _definition = definition;
        _transitions = definition.Rules.SameLifeTransitions
            .Cast<ActorFormTransitionDefinition>()
            .ToDictionary(
                transition => transition.TransitionId,
                StringComparer.Ordinal);
        _forms = definition.Rules.Forms.ToDictionary(
            form => form.Id,
            StringComparer.Ordinal);
        _attacks = definition.Rules.AttackProfiles.ToDictionary(
            attack => attack.Id,
            StringComparer.Ordinal);
        _topologySlots = definition.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId));
        _assignments = definition.LifecycleAssignments.ToDictionary(
            assignment => (assignment.TeamId, assignment.UnitId));
    }

    /// <summary>
    /// Returns the exact declared routes for one source/action pair and
    /// optional FormTarget value.
    /// </summary>
    public ImmutableArray<ActorFormTransitionDefinition> MatchRoutes(
        string sourceFormId,
        string actionId,
        string? targetFormId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFormId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (targetFormId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(targetFormId);

        return _transitions.Values
            .Where(transition =>
                string.Equals(
                    transition.SourceFormId,
                    sourceFormId,
                    StringComparison.Ordinal)
                && string.Equals(
                    transition.ActionId,
                    actionId,
                    StringComparison.Ordinal)
                && (targetFormId is null
                    || string.Equals(
                        transition.TargetFormId,
                        targetFormId,
                        StringComparison.Ordinal)))
            .OrderBy(
                transition => transition.TargetFormId,
                StringComparer.Ordinal)
            .ThenBy(
                transition => transition.TransitionId,
                StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// Determines whether the current source can queue the declared route.
    /// </summary>
    public bool CanQueue(
        ActorSameLifeTransitionActorSnapshot source,
        string transitionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);
        ValidateSource(source);
        if (!_transitions.TryGetValue(
                transitionId,
                out ActorFormTransitionDefinition? transition))
        {
            throw new ArgumentException(
                "The queue check references an unknown same-life transition.",
                nameof(transitionId));
        }
        return QueueBlockReason(source, transition) is null;
    }

    /// <summary>
    /// Captures queue-time identity and pose and computes the exact configured
    /// due tick. No actor state is mutated.
    /// </summary>
    public ActorSameLifeTransitionQueueOutcome Queue(
        int tick,
        ActorSameLifeTransitionRequest request,
        ActorSameLifeTransitionActorSnapshot source)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ValidateSource(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TransitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        if (!_transitions.TryGetValue(
                request.TransitionId,
                out ActorFormTransitionDefinition? transition))
        {
            throw new ArgumentException(
                "The request references an unknown same-life transition.",
                nameof(request));
        }

        ActorSameLifeTransitionQueueOutcome.QueueBlockReason? reason =
            request.SourceActorId != source.ActorId
                ? ActorSameLifeTransitionQueueOutcome.QueueBlockReason
                    .SourceIdentityChanged
                : QueueBlockReason(source, transition);
        if (reason is not null)
        {
            return new ActorSameLifeTransitionQueueOutcome(
                request,
                Reservation: null,
                reason);
        }

        long offset = CompletionOffset(transition.Windup);
        var reservation = new ActorSameLifeTransitionReservation(
            source.ActorId,
            source.ParticipantId,
            source.Generation,
            source.FormId,
            transition.TargetFormId,
            source.Position,
            source.Facing,
            transition.TransitionId,
            request.OperationId,
            tick,
            checked((int)((long)tick + offset)));
        return new ActorSameLifeTransitionQueueOutcome(
            request,
            reservation,
            Reason: null);
    }

    /// <summary>
    /// Rechecks identity, source pose, and placement at the exact due tick,
    /// then evaluates health and combat continuity from current state.
    /// </summary>
    public ActorSameLifeTransitionCompletion Complete(
        int tick,
        ActorSameLifeTransitionReservation reservation,
        ActorSameLifeTransitionActorSnapshot? source)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(reservation);
        if (!_transitions.TryGetValue(
                reservation.TransitionId,
                out ActorFormTransitionDefinition? transition))
        {
            throw new ArgumentException(
                "The reservation references an unknown same-life transition.",
                nameof(reservation));
        }
        ValidateReservation(reservation, transition);
        if (tick != reservation.DueTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "A same-life transition completes only at its exact due tick.");
        }
        if (source is null)
        {
            return Cancelled(
                reservation,
                ActorSameLifeTransitionCompletion.CancellationReason
                    .SourceUnavailable);
        }
        ValidateSource(source);
        if (source.ActorId != reservation.SourceActorId
            || source.ParticipantId != reservation.ParticipantId)
        {
            return Cancelled(
                reservation,
                ActorSameLifeTransitionCompletion.CancellationReason
                    .SourceIdentityChanged);
        }
        if (source.Generation != reservation.SourceGeneration
            || !string.Equals(
                source.FormId,
                reservation.SourceFormId,
                StringComparison.Ordinal)
            || source.Position != reservation.SourcePosition
            || source.Facing != reservation.SourceFacing
            || source.PendingSameLifeTransition != reservation
            || source.Health <= 0)
        {
            return Cancelled(
                reservation,
                ActorSameLifeTransitionCompletion.CancellationReason
                    .SourceStateChanged);
        }
        if (!PlacementIsLegal(source.Position, transition.Placement))
        {
            return Cancelled(
                reservation,
                ActorSameLifeTransitionCompletion.CancellationReason
                    .PlacementInvalid);
        }

        ActorFormDefinition sourceForm = _forms[transition.SourceFormId];
        ActorFormDefinition targetForm = _forms[transition.TargetFormId];
        var completed =
            new ActorSameLifeTransitionCompletion.CompletedState(
                targetForm.Id,
                source.Position,
                source.Facing,
                TransitionHealth(
                    transition.Health,
                    source.Health,
                    sourceForm.MaxHealth,
                    targetForm.MaxHealth),
                source.Cooldown,
                TransitionEnergy(targetForm, source.Energy));
        return new ActorSameLifeTransitionCompletion(
            reservation,
            ActorSameLifeTransitionCompletion.CompletionOutcomeKind.Completed,
            Reason: null,
            completed);
    }

    private ActorSameLifeTransitionQueueOutcome.QueueBlockReason?
        QueueBlockReason(
            ActorSameLifeTransitionActorSnapshot source,
            ActorFormTransitionDefinition transition)
    {
        if (source.PendingSameLifeTransition is not null)
        {
            return ActorSameLifeTransitionQueueOutcome.QueueBlockReason
                .AlreadyPending;
        }
        if (source.Health <= 0
            || source.Generation < 0
            || source.ParticipantId < 0
            || !string.Equals(
                source.FormId,
                transition.SourceFormId,
                StringComparison.Ordinal))
        {
            return ActorSameLifeTransitionQueueOutcome.QueueBlockReason
                .SourceStateIneligible;
        }
        if (source.IrreversibleReturnFormIds.Contains(
                transition.TargetFormId,
                StringComparer.Ordinal))
        {
            return ActorSameLifeTransitionQueueOutcome.QueueBlockReason
                .IrreversibleReturn;
        }
        return PlacementIsLegal(source.Position, transition.Placement)
            ? null
            : ActorSameLifeTransitionQueueOutcome.QueueBlockReason
                .PlacementInvalid;
    }

    private void ValidateSource(
        ActorSameLifeTransitionActorSnapshot source)
    {
        bool topologyOwned = source.ActorId is not null
            && _topologySlots.TryGetValue(
                (source.ActorId.TeamId, source.ActorId.UnitId),
                out PublicUnitSlot? slot)
            && slot.ControllerParticipantId == source.ParticipantId;
        bool formAllowed = source.ActorId is not null
            && _assignments.TryGetValue(
                (source.ActorId.TeamId, source.ActorId.UnitId),
                out ActorUnitSlotLifecycleAssignmentDefinition? assignment)
            && assignment.AllowedFormIds.Contains(
                source.FormId,
                StringComparer.Ordinal);
        bool healthValid = _forms.TryGetValue(
                source.FormId,
                out ActorFormDefinition? form)
            && source.Health <= form.MaxHealth;
        bool energyValid = form is not null
            && form.AttackProfileId is string attackProfileId
                ? _attacks[attackProfileId].MaxEnergy == 0
                    ? source.Energy is null
                    : source.Energy is >= 0
                        && source.Energy
                            <= _attacks[attackProfileId].MaxEnergy
                : source.Energy is null;
        bool historyValid =
            !source.IrreversibleReturnFormIds.IsDefault
            && source.IrreversibleReturnFormIds.All(value =>
                !string.IsNullOrWhiteSpace(value)
                && _forms.ContainsKey(value))
            && source.IrreversibleReturnFormIds
                .Distinct(StringComparer.Ordinal)
                .Count() == source.IrreversibleReturnFormIds.Length
            && (source.HasPriorSameLifeTransition
                || source.IrreversibleReturnFormIds.IsEmpty);
        if (source.ActorId is null
            || source.ParticipantId < 0
            || source.Generation < 0
            || source.Health < 0
            || source.Cooldown < 0
            || string.IsNullOrWhiteSpace(source.FormId)
            || !Enum.IsDefined(source.Facing)
            || !topologyOwned
            || !formAllowed
            || !healthValid
            || !energyValid
            || !historyValid
            || _definition.Map.IsWall(source.Position))
        {
            throw new ArgumentException(
                "A same-life source snapshot must identify one valid topology-owned actor state.",
                nameof(source));
        }
    }

    private bool PlacementIsLegal(
        Position position,
        ActorSameLifePlacementDefinition placement)
    {
        if (_definition.Map.IsWall(position))
            return false;
        HashSet<ActorMapTileTagDefinition.TileTagKind> actual =
            _definition.Map.TileTags
                .Where(tag => tag.Tiles.Contains(position))
                .Select(tag => tag.Kind)
                .ToHashSet();
        return placement.RequiredTileTags.All(actual.Contains)
            && !placement.ForbiddenTileTags.Any(actual.Contains);
    }

    private static long CompletionOffset(
        ActorTransitionWindupDefinition windup) =>
        windup.Completion switch
        {
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .TickStartAfterDuration =>
                windup.DurationTicks,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate =>
                windup.DurationTicks - 1L,
            _ => throw new NotSupportedException(
                "The same-life transition uses an unsupported completion clock."),
        };

    private static int TransitionHealth(
        ActorSameLifeHealthDefinition definition,
        int currentHealth,
        int sourceMaximum,
        int targetMaximum) =>
        definition.Policy switch
        {
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveCurrentCappedToTargetMaximum =>
                Math.Min(currentHealth, targetMaximum),
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .AddFlatCappedToTargetMaximum =>
                checked((int)Math.Min(
                    checked((long)currentHealth
                        + definition.FlatHealthGain),
                    targetMaximum)),
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .SetToTargetMaximum =>
                targetMaximum,
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveRatioFloorMinimumOne =>
                checked((int)Math.Clamp(
                    checked((long)currentHealth * targetMaximum)
                        / sourceMaximum,
                    1L,
                    targetMaximum)),
            _ => throw new NotSupportedException(
                "The same-life transition uses an unsupported health policy."),
        };

    private int? TransitionEnergy(
        ActorFormDefinition target,
        int? currentEnergy)
    {
        if (target.AttackProfileId is not string attackProfileId)
            return null;
        ActorAttackProfileDefinition attack = _attacks[attackProfileId];
        return attack.MaxEnergy == 0
            ? null
            : Math.Min(currentEnergy ?? 0, attack.MaxEnergy);
    }

    private static ActorSameLifeTransitionCompletion Cancelled(
        ActorSameLifeTransitionReservation reservation,
        ActorSameLifeTransitionCompletion.CancellationReason reason) =>
        new(
            reservation,
            ActorSameLifeTransitionCompletion.CompletionOutcomeKind.Cancelled,
            reason,
            State: null);

    private static void ValidateReservation(
        ActorSameLifeTransitionReservation reservation,
        ActorFormTransitionDefinition transition)
    {
        long expectedDue =
            (long)reservation.StartedTick
            + CompletionOffset(transition.Windup);
        if (reservation.SourceActorId is null
            || reservation.ParticipantId < 0
            || reservation.SourceGeneration < 0
            || !string.Equals(
                reservation.SourceFormId,
                transition.SourceFormId,
                StringComparison.Ordinal)
            || !string.Equals(
                reservation.TargetFormId,
                transition.TargetFormId,
                StringComparison.Ordinal)
            || !Enum.IsDefined(reservation.SourceFacing)
            || string.IsNullOrWhiteSpace(reservation.OperationId)
            || reservation.StartedTick < 0
            || expectedDue != reservation.DueTick)
        {
            throw new ArgumentException(
                "The same-life transition reservation is inconsistent with its declared route.",
                nameof(reservation));
        }
    }

    private static void ValidateSupportedTransitions(
        IEnumerable<ActorSameLifeTransitionDefinition> transitions)
    {
        foreach (ActorSameLifeTransitionDefinition transition in transitions)
        {
            if (transition is not ActorFormTransitionDefinition form
                || form.Kind
                    != ActorSameLifeTransitionDefinition
                        .SameLifeTransitionKind.FormTransition
                || form.MemoryContinuity
                    != ActorSameLifeTransitionDefinition
                        .MemoryContinuityKind.PreservePrivateMemory
                || form.Windup.PendingAction
                    != ActorTransitionWindupDefinition.PendingActionKind
                        .WaitOnly
                || form.Windup.SourceForm
                    != ActorTransitionWindupDefinition.SourceFormKind
                        .RetainSourceForm
                || form.Windup.Targetability
                    != ActorTransitionWindupDefinition.TargetabilityKind
                        .TargetableAndOccupiesTile
                || form.Windup.LethalDamage
                    != ActorTransitionWindupDefinition.LethalDamageKind
                        .CancelTransition
                || form.Windup.PlacementReference
                    != ActorTransitionWindupDefinition.PlacementReferenceKind
                        .QueueTimePose
                || form.Placement.PositionContinuity
                    != ActorSameLifePlacementDefinition.PositionContinuityKind
                        .SameOccupiedGroundTile
                || form.Placement.LegalityEvaluation
                    != ActorSameLifePlacementDefinition
                        .LegalityEvaluationKind.QueueAndCompletionTileTags
                || form.Placement.FailedCompletion
                    != ActorSameLifePlacementDefinition.FailedCompletionKind
                        .CancelAndRemainInSourceForm
                || form.CombatState.CooldownContinuity
                    != ActorSameLifeCombatStateDefinition
                        .CooldownContinuityKind.PreserveRemainingTicks
                || form.CombatState.EnergyContinuity
                    != ActorSameLifeCombatStateDefinition
                        .EnergyContinuityKind
                        .PreserveCurrentCappedToTargetMaximumMissingSourcePoolBecomesZero)
            {
                throw new NotSupportedException(
                    $"Same-life transition '{transition.TransitionId}' uses semantics unsupported by the generic execution kernel.");
            }
        }
    }
}
