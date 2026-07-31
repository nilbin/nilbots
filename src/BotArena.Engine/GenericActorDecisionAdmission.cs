using System.Collections.Immutable;
using System.Text;

namespace BotArena.Engine;

/// <summary>
/// The contract-driven half of runtime admission: catalog resolution, argument
/// canonicalization, legality-mask validation, and the per-form synthetic
/// <c>Wait</c>. It is deliberately shared by the per-life
/// <see cref="GenericActorRuntimeCoordinator"/> and the participant-scoped
/// <see cref="GenericMindRuntimeCoordinator"/>.
/// <para>
/// Sharing it is not tidiness. The mind profile's whole claim is that it
/// changes the DRIVER and not the GAME
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §7.2), and a second
/// copy of these rules would be exactly the place that claim could quietly
/// stop being true. One body's action is admitted by identical code on both
/// profiles or the null pin is measuring two implementations rather than two
/// drivers.
/// </para>
/// </summary>
internal sealed class GenericActorDecisionAdmission
{
    private const int MaximumDebugUtf8Bytes = 4096;

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private readonly ActorResolvedMatchDefinition _contract;
    private readonly Dictionary<(int TeamId, int UnitId), PublicUnitSlot>
        _slots;
    private readonly Dictionary<string, ActorActionDefinition> _actionsById;
    private readonly Dictionary<int, ActorActionDefinition> _actionsByCode;
    private readonly Dictionary<string, ActorFormDefinition> _formsById;
    private readonly Dictionary<string, GenericActorRuntimeDecision>
        _waitsByForm;

    public GenericActorDecisionAdmission(
        ActorResolvedMatchDefinition contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        _contract = contract;
        _slots = contract.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId));
        _actionsById = contract.Rules.Actions.ToDictionary(
            action => action.Id,
            StringComparer.Ordinal);
        _actionsByCode = contract.Rules.Actions.ToDictionary(
            action => action.Code);
        _formsById = contract.Rules.Forms.ToDictionary(
            form => form.Id,
            StringComparer.Ordinal);
        _waitsByForm = BuildWaitCatalog(
            contract.Rules.Forms,
            contract.Rules.Actions);
    }

    public IReadOnlyDictionary<(int TeamId, int UnitId), PublicUnitSlot>
        Slots => _slots;

    public IReadOnlyDictionary<string, ActorActionDefinition> ActionsById =>
        _actionsById;

    public IReadOnlyDictionary<string, ActorFormDefinition> FormsById =>
        _formsById;

    /// <summary>
    /// The pre-filled reply every live body starts the tick holding. Under the
    /// mind this is what makes the default mechanical: the host fills every
    /// live body with <c>Wait</c> and the mind overwrites what it wants moved,
    /// so there is no key set to get right (§2.4).
    /// </summary>
    public GenericActorRuntimeDecision SyntheticWait(string formId) =>
        _waitsByForm[formId];

    /// <summary>
    /// Projects a raw runtime reply into the public typed action model only
    /// when its selectors, argument shape, and catalog-static values are
    /// representable. Dynamic per-tick illegality and debug-message faults do
    /// not make an otherwise well-formed submitted action unrepresentable.
    /// </summary>
    public bool TryProjectSubmittedAction(
        GenericActorRuntimeDecision? decision,
        out GenericActorRuntimeActionResolution.ResolvedAction?
            submittedAction)
    {
        submittedAction = null;
        if (!TryResolveCatalogAction(
                decision,
                out ActorActionDefinition? action)
            || !TryCanonicalizeArguments(
                formId: null,
                action!,
                decision!.Arguments,
                out ImmutableArray<GenericActorRuntimeActionArgument>
                    arguments,
                out _))
        {
            return false;
        }

        submittedAction =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                action!.Id,
                action.Code,
                arguments);
        return true;
    }

    public bool TryAdmitDecision(
        string formId,
        ImmutableArray<GenericActorRuntimeActionLegality> actionLegalities,
        GenericActorRuntimeDecision? decision,
        out GenericActorRuntimeDecision? admitted,
        out string? faultCode)
    {
        admitted = null;
        if (decision is null
            || string.IsNullOrWhiteSpace(decision.ActionId)
            || decision.ActionCode < 0
            || decision.Arguments.IsDefault)
        {
            faultCode = GenericActorRuntimeFaultCodes.MalformedDecision;
            return false;
        }
        if (!IsValidDebugMessage(decision.DebugMessage))
        {
            faultCode = GenericActorRuntimeFaultCodes.InvalidDebugMessage;
            return false;
        }

        if (!TryResolveCatalogAction(
                decision,
                out ActorActionDefinition? action,
                out faultCode))
        {
            return false;
        }
        GenericActorRuntimeActionLegality legality =
            actionLegalities.Single(value =>
                string.Equals(
                    value.ActionId,
                    action!.Id,
                    StringComparison.Ordinal));
        if (!TryCanonicalizeArguments(
                legality.AllowedByForm
                    ? formId
                    : null,
                action!,
                decision.Arguments,
                out ImmutableArray<GenericActorRuntimeActionArgument>
                    arguments,
                out faultCode))
        {
            return false;
        }

        if (legality.AllowedByForm)
        {
            foreach (GenericActorRuntimeActionArgument argument in arguments)
            {
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    constraint = legality.Constraints.Single(
                        value => value.Kind == argument.Kind);
                if (!IsArgumentInDomain(
                        formId,
                        action!,
                        argument,
                        constraint))
                {
                    faultCode =
                        GenericActorRuntimeFaultCodes.ArgumentOutOfDomain;
                    return false;
                }
            }
        }

        admitted = new GenericActorRuntimeDecision(
            action!.Id,
            action.Code,
            arguments,
            decision.DebugMessage);
        faultCode = null;
        return true;
    }

    public void ValidateActionLegalities(
        ActorIdentity actorId,
        ActorFormDefinition form,
        ImmutableArray<GenericActorRuntimeActionLegality> actionLegalities)
    {
        if (actionLegalities.IsDefault)
        {
            throw InvalidObservation(
                actorId,
                "action legality collection is uninitialized");
        }

        var legalitiesById =
            new Dictionary<string, GenericActorRuntimeActionLegality>(
                StringComparer.Ordinal);
        var seenCodes = new HashSet<int>();
        foreach (GenericActorRuntimeActionLegality? legality in
                 actionLegalities)
        {
            if (legality is null
                || string.IsNullOrWhiteSpace(legality.ActionId))
            {
                throw InvalidObservation(
                    actorId,
                    "action legality contains a null entry or blank ID");
            }
            if (!legalitiesById.TryAdd(legality.ActionId, legality)
                || !seenCodes.Add(legality.ActionCode))
            {
                throw InvalidObservation(
                    actorId,
                    "action legality selectors are duplicated");
            }
            if (!_actionsById.TryGetValue(
                    legality.ActionId,
                    out ActorActionDefinition? action)
                || action.Code != legality.ActionCode)
            {
                throw InvalidObservation(
                    actorId,
                    $"action legality '{legality.ActionId}' does not match the rules catalog");
            }

            bool expectedAllowed = form.AllowedActionIds.Contains(
                action.Id,
                StringComparer.Ordinal);
            if (legality.AllowedByForm != expectedAllowed
                || (legality.Available && !legality.AllowedByForm))
            {
                throw InvalidObservation(
                    actorId,
                    $"action legality '{action.Id}' contradicts form '{form.Id}'");
            }
            ValidateConstraints(actorId, action, legality);
        }

        if (legalitiesById.Count != _actionsById.Count
            || _actionsById.Keys.Any(
                actionId => !legalitiesById.ContainsKey(actionId)))
        {
            throw InvalidObservation(
                actorId,
                "action legality must describe every catalog action exactly once");
        }
    }

    public static ArgumentException InvalidObservation(
        ActorIdentity actorId,
        string reason) =>
        new(
            $"Observation for actor '{actorId}' is invalid: {reason}.",
            "observations");

    private void ValidateConstraints(
        ActorIdentity actorId,
        ActorActionDefinition action,
        GenericActorRuntimeActionLegality legality)
    {
        if (legality.Constraints.IsDefault)
        {
            throw InvalidObservation(
                actorId,
                $"action legality '{action.Id}' has an uninitialized constraint collection");
        }

        var constraints = new Dictionary<
            ActorActionParameterKind,
            GenericActorRuntimeActionLegality.ArgumentConstraint>();
        foreach (GenericActorRuntimeActionLegality.ArgumentConstraint?
                 constraint in legality.Constraints)
        {
            if (constraint is null
                || !Enum.IsDefined(constraint.Kind)
                || !constraints.TryAdd(constraint.Kind, constraint))
            {
                throw InvalidObservation(
                    actorId,
                    $"action legality '{action.Id}' has a null, unknown, or duplicate constraint");
            }
            ValidateConstraintValues(actorId, action.Id, constraint);
        }

        if (constraints.Count != action.ParameterKinds.Length
            || action.ParameterKinds.Any(
                kind => !constraints.ContainsKey(kind)))
        {
            throw InvalidObservation(
                actorId,
                $"action legality '{action.Id}' constraints do not match its catalog parameters");
        }
    }

    private void ValidateConstraintValues(
        ActorIdentity actorId,
        string actionId,
        GenericActorRuntimeActionLegality.ArgumentConstraint constraint)
    {
        bool invalid = constraint switch
        {
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ShotProgramConstraint => false,
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .DirectionConstraint value =>
                value.AllowedValues.IsDefault
                || value.AllowedValues.Any(direction =>
                    !Enum.IsDefined(direction))
                || HasDuplicates(value.AllowedValues),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .UnitTargetConstraint value =>
                value.AllowedValues.IsDefault
                || value.AllowedValues.Any(target =>
                    !_slots.ContainsKey((target.TeamId, target.UnitId)))
                || HasDuplicates(value.AllowedValues),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .FormTargetConstraint value =>
                value.AllowedFormIds.IsDefault
                || value.AllowedFormIds.Any(formId =>
                    string.IsNullOrWhiteSpace(formId)
                    || !_formsById.ContainsKey(formId))
                || HasDuplicates(
                    value.AllowedFormIds,
                    StringComparer.Ordinal),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint value =>
                value.AllowedValues.IsDefault
                || value.AllowedValues.Any(heading =>
                    !Enum.IsDefined(heading))
                || HasDuplicates(value.AllowedValues),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .UpgradeTrackConstraint value =>
                value.AllowedTrackIds.IsDefault
                || value.AllowedTrackIds.Any(string.IsNullOrWhiteSpace)
                || HasDuplicates(
                    value.AllowedTrackIds,
                    StringComparer.Ordinal),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .PositionTargetConstraint value =>
                value.AllowedValues.IsDefault
                || value.AllowedValues.Any(position =>
                    position.X < 0
                    || position.Y < 0
                    || position.X >= _contract.Map.Width
                    || position.Y >= _contract.Map.Height)
                || HasDuplicates(value.AllowedValues),
            _ => true,
        };
        if (invalid)
        {
            throw InvalidObservation(
                actorId,
                $"action legality '{actionId}' has malformed constraint values");
        }
    }

    private bool TryResolveCatalogAction(
        GenericActorRuntimeDecision? decision,
        out ActorActionDefinition? action) =>
        TryResolveCatalogAction(decision, out action, out _);

    private bool TryResolveCatalogAction(
        GenericActorRuntimeDecision? decision,
        out ActorActionDefinition? action,
        out string? faultCode)
    {
        action = null;
        if (decision is null
            || string.IsNullOrWhiteSpace(decision.ActionId)
            || decision.ActionCode < 0
            || decision.Arguments.IsDefault)
        {
            faultCode = GenericActorRuntimeFaultCodes.MalformedDecision;
            return false;
        }

        bool hasId = _actionsById.TryGetValue(
            decision.ActionId,
            out ActorActionDefinition? byId);
        bool hasCode = _actionsByCode.TryGetValue(
            decision.ActionCode,
            out ActorActionDefinition? byCode);
        if (!hasId && !hasCode)
        {
            faultCode = GenericActorRuntimeFaultCodes.UnknownAction;
            return false;
        }
        if (!hasId
            || !hasCode
            || !ReferenceEquals(byId, byCode))
        {
            faultCode = GenericActorRuntimeFaultCodes.ActionSelectorMismatch;
            return false;
        }

        action = byId;
        faultCode = null;
        return true;
    }

    private bool TryCanonicalizeArguments(
        string? formId,
        ActorActionDefinition action,
        ImmutableArray<GenericActorRuntimeActionArgument> submittedArguments,
        out ImmutableArray<GenericActorRuntimeActionArgument> arguments,
        out string? faultCode)
    {
        arguments = default;
        if (submittedArguments.IsDefault)
        {
            faultCode = GenericActorRuntimeFaultCodes.MalformedDecision;
            return false;
        }

        var byKind = new Dictionary<
            ActorActionParameterKind,
            GenericActorRuntimeActionArgument>();
        foreach (GenericActorRuntimeActionArgument? argument in
                 submittedArguments)
        {
            if (argument is null
                || !Enum.IsDefined(argument.Kind))
            {
                faultCode = GenericActorRuntimeFaultCodes.MalformedArgument;
                return false;
            }
            if (!byKind.TryAdd(argument.Kind, argument))
            {
                faultCode = GenericActorRuntimeFaultCodes.DuplicateArgument;
                return false;
            }
            if (!action.ParameterKinds.Contains(argument.Kind))
            {
                faultCode = GenericActorRuntimeFaultCodes.UnexpectedArgument;
                return false;
            }
            if (!IsStructurallyRepresentableArgument(argument))
            {
                faultCode =
                    GenericActorRuntimeFaultCodes.ArgumentOutOfDomain;
                return false;
            }
        }

        foreach (ActorActionParameterKind kind in action.ParameterKinds)
        {
            if (!byKind.ContainsKey(kind)
                && !IsOptionalArgument(formId, action, kind))
            {
                faultCode = GenericActorRuntimeFaultCodes.MissingArgument;
                return false;
            }
        }

        arguments = byKind.Values
            .OrderBy(argument => argument.Kind)
            .ToImmutableArray();
        faultCode = null;
        return true;
    }

    private bool IsOptionalArgument(
        string? formId,
        ActorActionDefinition action,
        ActorActionParameterKind kind)
    {
        if (kind != ActorActionParameterKind.ShotProgram
            || action.Kind != ActorActionKind.Attack)
        {
            return false;
        }

        IEnumerable<ActorFormDefinition> candidateForms = formId is null
            ? _formsById.Values.Where(form =>
                form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal))
            : [_formsById[formId]];
        return candidateForms.Any(form =>
            form.AttackProfileId is string attackProfileId
            && _contract.Rules.AttackProfiles
                .Single(profile =>
                    string.Equals(
                        profile.Id,
                        attackProfileId,
                        StringComparison.Ordinal))
                .ShotProgram.PayloadOptional);
    }

    private bool IsStructurallyRepresentableArgument(
        GenericActorRuntimeActionArgument argument) =>
        argument switch
        {
            GenericActorRuntimeActionArgument.ShotProgramArgument => true,
            GenericActorRuntimeActionArgument.DirectionArgument value =>
                Enum.IsDefined(value.Value),
            GenericActorRuntimeActionArgument.UnitTargetArgument value =>
                value.Value.TeamId >= 0
                && value.Value.UnitId >= 0
                && _slots.ContainsKey(
                    (value.Value.TeamId, value.Value.UnitId)),
            GenericActorRuntimeActionArgument.FormTargetArgument value =>
                !string.IsNullOrWhiteSpace(value.FormId)
                && _formsById.ContainsKey(value.FormId),
            GenericActorRuntimeActionArgument.ProjectileHeadingArgument
                value =>
                Enum.IsDefined(value.Value),
            GenericActorRuntimeActionArgument.UpgradeTrackArgument value =>
                !string.IsNullOrWhiteSpace(value.TrackId),
            GenericActorRuntimeActionArgument.PositionTargetArgument value =>
                value.Value.X >= 0
                && value.Value.Y >= 0
                && value.Value.X < _contract.Map.Width
                && value.Value.Y < _contract.Map.Height,
            _ => false,
        };

    private bool IsArgumentInDomain(
        string formId,
        ActorActionDefinition action,
        GenericActorRuntimeActionArgument argument,
        GenericActorRuntimeActionLegality.ArgumentConstraint constraint) =>
        (argument, constraint) switch
        {
            (
                GenericActorRuntimeActionArgument.ShotProgramArgument value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ShotProgramConstraint allowed) =>
                allowed.Allowed
                && IsValidShotProgram(formId, action, value.Value),
            (
                GenericActorRuntimeActionArgument.DirectionArgument value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .DirectionConstraint allowed) =>
                Enum.IsDefined(value.Value)
                && allowed.AllowedValues.Contains(value.Value),
            (
                GenericActorRuntimeActionArgument.UnitTargetArgument value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .UnitTargetConstraint allowed) =>
                value.Value.TeamId >= 0
                && value.Value.UnitId >= 0
                && allowed.AllowedValues.Contains(value.Value),
            (
                GenericActorRuntimeActionArgument.FormTargetArgument value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .FormTargetConstraint allowed) =>
                !string.IsNullOrWhiteSpace(value.FormId)
                && allowed.AllowedFormIds.Contains(
                    value.FormId,
                    StringComparer.Ordinal),
            (
                GenericActorRuntimeActionArgument.ProjectileHeadingArgument
                    value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint allowed) =>
                Enum.IsDefined(value.Value)
                && allowed.AllowedValues.Contains(value.Value),
            (
                GenericActorRuntimeActionArgument.UpgradeTrackArgument value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .UpgradeTrackConstraint allowed) =>
                !string.IsNullOrWhiteSpace(value.TrackId)
                && allowed.AllowedTrackIds.Contains(
                    value.TrackId,
                    StringComparer.Ordinal),
            (
                GenericActorRuntimeActionArgument.PositionTargetArgument value,
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .PositionTargetConstraint allowed) =>
                allowed.AllowedValues.Contains(value.Value),
            _ => false,
        };

    private bool IsValidShotProgram(
        string formId,
        ActorActionDefinition action,
        ShotProgram value)
    {
        if (action.Kind != ActorActionKind.Attack
            || !_formsById.TryGetValue(
                formId,
                out ActorFormDefinition? form)
            || form.AttackProfileId is not string attackProfileId)
        {
            return false;
        }

        ActorAttackProfileDefinition profile =
            _contract.Rules.AttackProfiles.Single(candidate =>
                string.Equals(
                    candidate.Id,
                    attackProfileId,
                    StringComparison.Ordinal));
        ActorShotProgramDefinition program = profile.ShotProgram;
        if (!program.Enabled
            || value.InitialAimOffset < program.MinInitialAimSteps
            || value.InitialAimOffset > program.MaxInitialAimSteps)
        {
            return false;
        }

        if (value.BendCount == 0)
        {
            return value.BendDirection
                    == program.AimOnlyProgram.BendDirection
                && value.BendAfterTiles
                    == program.AimOnlyProgram.BendAfterTiles
                && value.BendEveryTiles
                    == program.AimOnlyProgram.BendEveryTiles;
        }

        return program.AllowedCurvedBendDirections.Contains(
                   value.BendDirection)
            && value.BendAfterTiles >= program.MinBendAfterTiles
            && value.BendAfterTiles <= program.MaxBendAfterTiles
            && value.BendEveryTiles >= program.MinBendEveryTiles
            && value.BendEveryTiles <= program.MaxBendEveryTiles
            && value.BendCount >= program.MinBendCount
            && value.BendCount <= program.MaxBendCount;
    }

    private static bool IsValidDebugMessage(string? value)
    {
        if (value is null)
            return true;
        try
        {
            return StrictUtf8.GetByteCount(value) <= MaximumDebugUtf8Bytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static Dictionary<string, GenericActorRuntimeDecision>
        BuildWaitCatalog(
            IEnumerable<ActorFormDefinition> forms,
            IEnumerable<ActorActionDefinition> actions)
    {
        ActorActionDefinition[] waits = actions
            .Where(action => action.Kind == ActorActionKind.Wait)
            .OrderBy(action => action.Code)
            .ThenBy(action => action.Id, StringComparer.Ordinal)
            .ToArray();
        var result = new Dictionary<string, GenericActorRuntimeDecision>(
            StringComparer.Ordinal);
        foreach (ActorFormDefinition form in forms)
        {
            ActorActionDefinition wait = waits.First(action =>
                form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal));
            result.Add(
                form.Id,
                new GenericActorRuntimeDecision(
                    wait.Id,
                    wait.Code,
                    [],
                    DebugMessage: null));
        }
        return result;
    }

    private static bool HasDuplicates<T>(
        IEnumerable<T> values,
        IEqualityComparer<T>? comparer = null)
    {
        var set = new HashSet<T>(comparer);
        return values.Any(value => !set.Add(value));
    }
}
