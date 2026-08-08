using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Exact runtime input, raw reply, and authoritative resolution for one actor
/// in one joint tick.
/// </summary>
public sealed record GenericActorMatchActorTurn
{
    public GenericActorMatchActorTurn(
        int tick,
        int participantId,
        ActorIdentity actorId,
        GenericActorRuntimeObservation observation,
        GenericActorRuntimeDecision? submittedDecision,
        GenericActorRuntimeActionResolution actionResolution)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        if (participantId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participantId));
        }
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(actionResolution);
        if (observation.Tick != tick)
        {
            throw new ArgumentException(
                "Actor-turn observation tick does not match the turn.",
                nameof(observation));
        }
        if (observation.Self.ActorId != actorId)
        {
            throw new ArgumentException(
                "Actor-turn observation identity does not match the turn.",
                nameof(observation));
        }
        if (!Enum.IsDefined(actionResolution.Outcome))
        {
            throw new ArgumentException(
                "Actor-turn action outcome is unknown.",
                nameof(actionResolution));
        }
        ValidateRuntimeFault(
            participantId,
            actorId,
            actionResolution);

        Tick = tick;
        ParticipantId = participantId;
        ActorId = actorId;
        Observation = observation;
        SubmittedDecision = submittedDecision;
        ActionResolution = actionResolution;
    }

    public int Tick { get; }
    public int ParticipantId { get; }
    public ActorIdentity ActorId { get; }

    /// <summary>
    /// The exact immutable observation instance delivered to the runtime.
    /// </summary>
    public GenericActorRuntimeObservation Observation { get; }

    /// <summary>
    /// Raw runtime evidence. Null represents a reply that could not be
    /// produced, rather than a synthesized action.
    /// </summary>
    public GenericActorRuntimeDecision? SubmittedDecision { get; }

    public GenericActorRuntimeActionResolution ActionResolution { get; }

    internal void ValidateAgainst(
        GenericActorMatchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ActorResolvedMatchDefinition definition = descriptor.Definition;
        PublicUnitSlot? slot = definition.Topology.UnitSlots.SingleOrDefault(
            candidate =>
                candidate.TeamId == ActorId.TeamId
                && candidate.UnitId == ActorId.UnitId);
        if (slot is null
            || slot.ControllerParticipantId != ParticipantId)
        {
            throw InvalidEvidence(
                "actor and participant do not match topology ownership");
        }
        if (Observation.SchemaVersion
            != definition.CapabilityVersions.ObservationSchemaVersion)
        {
            throw InvalidEvidence(
                "observation schema version does not match the descriptor");
        }

        ValidateCatalogSelector(
            definition,
            ActionResolution.AcceptedAction,
            "accepted");
        ValidateCatalogSelector(
            definition,
            ActionResolution.ValidatedAction,
            "validated");
        ValidateResolvedActionShape(
            definition,
            ActionResolution.AcceptedAction,
            "accepted");
        ValidateResolvedActionShape(
            definition,
            ActionResolution.ValidatedAction,
            "validated");

        GenericActorRuntimeActionResolution.ResolvedAction? projected =
            ProjectSubmittedAction(definition, SubmittedDecision);
        if (!ResolvedActionsSemanticallyEqual(
                projected,
                ActionResolution.SubmittedAction))
        {
            throw InvalidEvidence(
                "raw submitted decision and projected submitted action disagree");
        }
        if (ActionResolution.RuntimeFault is null)
        {
            if (projected is null
                || !ResolvedActionsSemanticallyEqual(
                    projected,
                    ActionResolution.AcceptedAction))
            {
                throw InvalidEvidence(
                    "a successfully admitted raw decision must exactly match the accepted action");
            }
        }
        else
        {
            GenericActorRuntimeActionResolution.ResolvedAction fallback =
                SyntheticWait(
                    definition,
                    Observation.Self.FormId);
            if (!ResolvedActionsSemanticallyEqual(
                    fallback,
                    ActionResolution.AcceptedAction))
            {
                throw InvalidEvidence(
                    "a runtime fault must use the form's canonical Wait action");
            }
        }

        // The generic actor resolver preserves the admitted selector and
        // arguments when gameplay marks it blocked or rejected. Keeping this
        // invariant here prevents a replay from substituting an unrelated,
        // merely catalog-valid action.
        if (definition.Rules.GameMode
                is DeathmatchGameModeDefinition
                    or FrontlineGameModeDefinition
            && !ResolvedActionsSemanticallyEqual(
                ActionResolution.AcceptedAction,
                ActionResolution.ValidatedAction))
        {
            throw InvalidEvidence(
                "Deathmatch validated action must exactly match its accepted action");
        }
    }

    private static void ValidateRuntimeFault(
        int participantId,
        ActorIdentity actorId,
        GenericActorRuntimeActionResolution resolution)
    {
        GenericActorRuntimeFault? fault = resolution.RuntimeFault;
        bool faulted =
            resolution.Outcome
            == GenericActorRuntimeActionResolution.ActionOutcome.Faulted;
        if (faulted != (fault is not null))
        {
            throw new ArgumentException(
                "A faulted outcome must carry one runtime fault, and no other outcome may carry one.",
                nameof(resolution));
        }
        if (fault is null)
            return;
        if (fault.ParticipantId != participantId
            || fault.ActorId != actorId
            || !Enum.IsDefined(fault.Stage)
            || string.IsNullOrWhiteSpace(fault.FaultCode)
            || fault.CumulativeFaultCount <= 0)
        {
            throw new ArgumentException(
                "Runtime fault evidence does not match its actor turn.",
                nameof(resolution));
        }
    }

    private static void ValidateCatalogSelector(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeActionResolution.ResolvedAction action,
        string evidenceName)
    {
        if (action is null
            || string.IsNullOrWhiteSpace(action.ActionId)
            || action.ActionCode < 0
            || action.Arguments.IsDefault
            || action.Arguments.Any(argument => argument is null))
        {
            throw new ArgumentException(
                $"Actor-turn {evidenceName} action is malformed.",
                nameof(action));
        }
        ActorActionDefinition? byId = definition.Rules.Actions.SingleOrDefault(
            candidate => string.Equals(
                candidate.Id,
                action.ActionId,
                StringComparison.Ordinal));
        ActorActionDefinition? byCode =
            definition.Rules.Actions.SingleOrDefault(candidate =>
                candidate.Code == action.ActionCode);
        if (byId is null
            || byCode is null
            || !ReferenceEquals(byId, byCode))
        {
            throw new ArgumentException(
                $"Actor-turn {evidenceName} action selector is not in the resolved catalog.",
                nameof(action));
        }
    }

    private static void ValidateResolvedActionShape(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeActionResolution.ResolvedAction action,
        string evidenceName)
    {
        var decision = new GenericActorRuntimeDecision(
            action.ActionId,
            action.ActionCode,
            action.Arguments,
            DebugMessage: null);
        GenericActorRuntimeActionResolution.ResolvedAction? projected =
            ProjectSubmittedAction(definition, decision);
        if (!ResolvedActionsSemanticallyEqual(projected, action))
        {
            throw new ArgumentException(
                $"Actor-turn {evidenceName} action arguments do not match its catalog shape.",
                nameof(action));
        }
    }

    private static GenericActorRuntimeActionResolution.ResolvedAction
        SyntheticWait(
            ActorResolvedMatchDefinition definition,
            string formId)
    {
        ActorFormDefinition form = definition.Rules.Forms.Single(value =>
            string.Equals(value.Id, formId, StringComparison.Ordinal));
        ActorActionDefinition wait = definition.Rules.Actions
            .Where(action =>
                action.Kind == ActorActionKind.Wait
                && form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal))
            .OrderBy(action => action.Code)
            .ThenBy(action => action.Id, StringComparer.Ordinal)
            .First();
        return new GenericActorRuntimeActionResolution.ResolvedAction(
            wait.Id,
            wait.Code,
            []);
    }

    private static GenericActorRuntimeActionResolution.ResolvedAction?
        ProjectSubmittedAction(
            ActorResolvedMatchDefinition definition,
            GenericActorRuntimeDecision? decision)
    {
        if (decision is null
            || string.IsNullOrWhiteSpace(decision.ActionId)
            || decision.ActionCode < 0
            || decision.Arguments.IsDefault)
        {
            return null;
        }

        ActorActionDefinition? byId = definition.Rules.Actions.SingleOrDefault(
            candidate => string.Equals(
                candidate.Id,
                decision.ActionId,
                StringComparison.Ordinal));
        ActorActionDefinition? byCode =
            definition.Rules.Actions.SingleOrDefault(candidate =>
                candidate.Code == decision.ActionCode);
        if (byId is null
            || byCode is null
            || !ReferenceEquals(byId, byCode))
        {
            return null;
        }

        var arguments = new Dictionary<
            ActorActionParameterKind,
            GenericActorRuntimeActionArgument>();
        foreach (GenericActorRuntimeActionArgument? argument in
                 decision.Arguments)
        {
            if (argument is null
                || !Enum.IsDefined(argument.Kind)
                || !arguments.TryAdd(argument.Kind, argument)
                || !byId.ParameterKinds.Contains(argument.Kind)
                || !IsStructurallyRepresentable(definition, argument))
            {
                return null;
            }
        }
        foreach (ActorActionParameterKind kind in byId.ParameterKinds)
        {
            if (!arguments.ContainsKey(kind)
                && !IsOptionalArgument(definition, byId, kind))
            {
                return null;
            }
        }

        return new GenericActorRuntimeActionResolution.ResolvedAction(
            byId.Id,
            byId.Code,
            arguments.Values
                .OrderBy(argument => argument.Kind)
                .ToImmutableArray());
    }

    private static bool IsOptionalArgument(
        ActorResolvedMatchDefinition definition,
        ActorActionDefinition action,
        ActorActionParameterKind kind)
    {
        // The declared LOCK is optional wherever it exists, exactly as
        // GenericActorDecisionAdmission admits it: a declarer with nobody
        // worth naming still fires down the lane. Evidence validation has to
        // agree with admission or a legal nameless declare aborts the match
        // on the way into the chronology.
        if (kind == ActorActionParameterKind.UnitTarget
            && (action.Kind == ActorActionKind.Attack
                || action.ParameterKinds.Contains(
                    ActorActionParameterKind.ProjectileHeading)))
        {
            return true;
        }

        if (kind != ActorActionParameterKind.ShotProgram
            || action.Kind != ActorActionKind.Attack)
        {
            return false;
        }

        Dictionary<string, ActorAttackProfileDefinition> attacks =
            definition.Rules.AttackProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        return definition.Rules.Forms.Any(form =>
            form.AllowedActionIds.Contains(
                action.Id,
                StringComparer.Ordinal)
            && form.AttackProfileId is string attackProfileId
            && attacks[attackProfileId].ShotProgram.PayloadOptional);
    }

    private static bool IsStructurallyRepresentable(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeActionArgument argument) =>
        argument switch
        {
            GenericActorRuntimeActionArgument.ShotProgramArgument => true,
            GenericActorRuntimeActionArgument.DirectionArgument value =>
                Enum.IsDefined(value.Value),
            GenericActorRuntimeActionArgument.UnitTargetArgument value =>
                value.Value.TeamId >= 0
                && value.Value.UnitId >= 0
                && definition.Topology.UnitSlots.Any(slot =>
                    slot.TeamId == value.Value.TeamId
                    && slot.UnitId == value.Value.UnitId),
            GenericActorRuntimeActionArgument.FormTargetArgument value =>
                !string.IsNullOrWhiteSpace(value.FormId)
                && definition.Rules.Forms.Any(form => string.Equals(
                    form.Id,
                    value.FormId,
                    StringComparison.Ordinal)),
            GenericActorRuntimeActionArgument.ProjectileHeadingArgument
                value =>
                Enum.IsDefined(value.Value),
            // The declared tracks live on the mode rather than on the rules
            // catalog, so structural representability is exactly a non-blank
            // ID; whether it is legal THIS tick is the legality mask's job.
            GenericActorRuntimeActionArgument.UpgradeTrackArgument value =>
                !string.IsNullOrWhiteSpace(value.TrackId),
            GenericActorRuntimeActionArgument.PositionTargetArgument value =>
                value.Value.X >= 0
                && value.Value.Y >= 0
                && value.Value.X < definition.Map.Width
                && value.Value.Y < definition.Map.Height,
            _ => false,
        };

    private static bool ResolvedActionsSemanticallyEqual(
        GenericActorRuntimeActionResolution.ResolvedAction? left,
        GenericActorRuntimeActionResolution.ResolvedAction? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (!string.Equals(
                left.ActionId,
                right.ActionId,
                StringComparison.Ordinal)
            || left.ActionCode != right.ActionCode
            || left.Arguments.IsDefault
            || right.Arguments.IsDefault
            || left.Arguments.Any(argument => argument is null)
            || right.Arguments.Any(argument => argument is null)
            || left.Arguments.Length != right.Arguments.Length)
        {
            return false;
        }

        GenericActorRuntimeActionArgument[] leftArguments = left.Arguments
            .OrderBy(argument => argument.Kind)
            .ToArray();
        GenericActorRuntimeActionArgument[] rightArguments = right.Arguments
            .OrderBy(argument => argument.Kind)
            .ToArray();
        return leftArguments
            .Zip(rightArguments)
            .All(pair => ArgumentsSemanticallyEqual(
                pair.First,
                pair.Second));
    }

    internal static bool ActionResolutionsSemanticallyEqual(
        GenericActorRuntimeActionResolution? left,
        GenericActorRuntimeActionResolution? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        return ResolvedActionsSemanticallyEqual(
                left.SubmittedAction,
                right.SubmittedAction)
            && ResolvedActionsSemanticallyEqual(
                left.AcceptedAction,
                right.AcceptedAction)
            && ResolvedActionsSemanticallyEqual(
                left.ValidatedAction,
                right.ValidatedAction)
            && left.Outcome == right.Outcome
            && left.RuntimeFault == right.RuntimeFault;
    }

    private static bool ArgumentsSemanticallyEqual(
        GenericActorRuntimeActionArgument left,
        GenericActorRuntimeActionArgument right) =>
        (left, right) switch
        {
            (
                GenericActorRuntimeActionArgument.ShotProgramArgument a,
                GenericActorRuntimeActionArgument.ShotProgramArgument b) =>
                a.Value == b.Value,
            (
                GenericActorRuntimeActionArgument.DirectionArgument a,
                GenericActorRuntimeActionArgument.DirectionArgument b) =>
                a.Value == b.Value,
            (
                GenericActorRuntimeActionArgument.UnitTargetArgument a,
                GenericActorRuntimeActionArgument.UnitTargetArgument b) =>
                a.Value == b.Value,
            (
                GenericActorRuntimeActionArgument.FormTargetArgument a,
                GenericActorRuntimeActionArgument.FormTargetArgument b) =>
                string.Equals(
                    a.FormId,
                    b.FormId,
                    StringComparison.Ordinal),
            (
                GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument a,
                GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument b) =>
                a.Value == b.Value,
            (
                GenericActorRuntimeActionArgument.UpgradeTrackArgument a,
                GenericActorRuntimeActionArgument.UpgradeTrackArgument b) =>
                string.Equals(
                    a.TrackId,
                    b.TrackId,
                    StringComparison.Ordinal),
            (
                GenericActorRuntimeActionArgument.PositionTargetArgument a,
                GenericActorRuntimeActionArgument.PositionTargetArgument b) =>
                a.Value == b.Value,
            _ => false,
        };

    private ArgumentException InvalidEvidence(string reason) =>
        new(
            $"Actor-turn evidence for '{ActorId}' is invalid: {reason}.",
            nameof(ActionResolution));
}
