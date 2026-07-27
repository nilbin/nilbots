namespace BotArena.Engine;

/// <summary>
/// Canonicalizes extensible entity actions against the public action catalog.
/// The historical Prime action adapter remains available for compatibility.
/// </summary>
public static class ActorDecisionAdapter
{
    public static BotDecision ToPrimeDecision(
        ActorDecision decision,
        PublicMatchContractManifest contract)
    {
        ActorDecision canonical = Normalize(decision, contract);
        if (!Enum.IsDefined(typeof(BotAction), canonical.ActionCode!.Value))
        {
            throw new NotSupportedException(
                $"Action code {canonical.ActionCode} is not a historical BotAction.");
        }
        return new BotDecision
        {
            Action = (BotAction)canonical.ActionCode!.Value,
            ShotProgram = canonical.Payload?.ShotProgram,
            DebugMessage = canonical.DebugMessage,
        };
    }

    /// <summary>
    /// Validates a raw runtime reply and returns its single canonical replay
    /// representation. Accepted ID-only and code-only replies are completed
    /// with both selectors, and an empty payload envelope normalizes to null.
    /// </summary>
    public static ActorDecision Normalize(
        ActorDecision decision,
        PublicMatchContractManifest contract,
        ActorIdentity? actorId = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(contract);

        if (decision.Faulted)
        {
            throw new ArgumentException(
                "Frontline runtime faults are rejected at the host boundary.",
                nameof(decision));
        }

        PublicActionDefinition action = ResolveAction(decision, contract);
        if (!action.Enabled)
        {
            throw new ArgumentException(
                $"Action '{action.Id}' is disabled by this match contract.",
                nameof(decision));
        }
        ActorActionPayload? payload = decision.Payload;
        if (payload?.ShotProgram is not null
            && !action.ParameterKinds.Contains(
                PublicActionParameterKind.ShotProgram))
        {
            throw new ArgumentException(
                $"Action '{action.Id}' does not accept a shot program.",
                nameof(decision));
        }
        if (payload?.Direction is not null
            && !action.ParameterKinds.Contains(
                PublicActionParameterKind.Direction))
        {
            throw new ArgumentException(
                $"Action '{action.Id}' does not accept a direction.",
                nameof(decision));
        }
        if (payload?.UnitTarget is not null
            && !action.ParameterKinds.Contains(
                PublicActionParameterKind.UnitTarget))
        {
            throw new ArgumentException(
                $"Action '{action.Id}' does not accept a unit target.",
                nameof(decision));
        }
        if (payload?.FormTargetId is not null
            && !action.ParameterKinds.Contains(
                PublicActionParameterKind.FormTarget))
        {
            throw new ArgumentException(
                $"Action '{action.Id}' does not accept a form target.",
                nameof(decision));
        }
        if (payload?.LaunchHeading is not null
            && !action.ParameterKinds.Contains(
                PublicActionParameterKind.ProjectileHeading))
        {
            throw new ArgumentException(
                $"Action '{action.Id}' does not accept a launch heading.",
                nameof(decision));
        }
        if (payload?.LaunchHeading is ProjectileHeading launchHeading
            && !Enum.IsDefined(launchHeading))
        {
            throw new ArgumentException(
                "The launch heading is outside the eight-way projectile heading catalog.",
                nameof(decision));
        }
        if (payload?.ShotProgram is ShotProgram program
            && contract.Rules.ShotPrograms.Enabled
            && !IsValidShotProgram(program, contract.Rules.ShotPrograms))
        {
            throw new ArgumentException(
                "The shot program is outside the active match contract.",
                nameof(decision));
        }
        if (action.Kind == PublicActionKind.Fabrication
            && payload?.UnitTarget is null)
        {
            throw new ArgumentException(
                "Fabrication requires an explicit unit target.",
                nameof(decision));
        }
        if (action.Kind == PublicActionKind.Transformation
            && payload?.FormTargetId is null)
        {
            throw new ArgumentException(
                "Transformation requires an explicit form target.",
                nameof(decision));
        }
        if (action.Kind == PublicActionKind.Transformation
            && (contract.Rules.Frontline is not { } transformFrontline
                || !string.Equals(
                    payload!.FormTargetId,
                    transformFrontline.Anchor.TargetFormId,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Transformation target must match the active Anchor contract.",
                nameof(decision));
        }
        if (string.Equals(
                action.Id,
                PublicActionIds.ShootDirection,
                StringComparison.Ordinal)
            && payload?.LaunchHeading is null)
        {
            throw new ArgumentException(
                "Directional shooting requires an explicit launch heading.",
                nameof(decision));
        }
        if (action.Kind == PublicActionKind.Fabrication
            && payload?.UnitTarget is ObservedUnitTarget target
            && (contract.Rules.Frontline is not { } frontline
                || !contract.Topology.UnitSlots.Any(unit =>
                    unit.TeamId == target.TeamId
                    && unit.UnitId == target.UnitId)
                || target.UnitId
                    == frontline.Fabrication.FabricatorUnitId
                || (actorId is not null
                    && target.TeamId != actorId.TeamId)))
        {
            throw new ArgumentException(
                "Fabrication target must be a child slot owned by the acting team.",
                nameof(decision));
        }

        ActorActionPayload? canonicalPayload = HasAnyParameter(payload)
            ? payload
            : null;
        return decision with
        {
            ActionId = action.Id,
            ActionCode = action.Code,
            Payload = canonicalPayload,
            Faulted = false,
            FaultMessage = null,
        };
    }

    private static bool HasAnyParameter(ActorActionPayload? payload) =>
        payload is not null
        && (payload.ShotProgram is not null
            || payload.Direction is not null
            || payload.UnitTarget is not null
            || payload.FormTargetId is not null
            || payload.LaunchHeading is not null);

    private static bool IsValidShotProgram(
        ShotProgram program,
        PublicShotProgramRules rules)
    {
        if (program.InitialAimOffset < rules.MinInitialAimOctants
            || program.InitialAimOffset > rules.MaxInitialAimOctants)
        {
            return false;
        }

        if (program.BendCount == 0)
        {
            return program.BendDirection
                    == rules.AimOnlyProgram.BendDirection
                && program.BendAfterTiles
                    == rules.AimOnlyProgram.BendAfterTiles
                && program.BendEveryTiles
                    == rules.AimOnlyProgram.BendEveryTiles;
        }

        return rules.AllowedCurvedBendDirections.Contains(
                program.BendDirection)
            && program.BendAfterTiles >= rules.MinBendAfterTiles
            && program.BendAfterTiles <= rules.MaxBendAfterTiles
            && program.BendEveryTiles >= rules.MinBendEveryTiles
            && program.BendEveryTiles <= rules.MaxBendEveryTiles
            && program.BendCount >= rules.MinBendCount
            && program.BendCount <= rules.MaxBendCount;
    }

    private static PublicActionDefinition ResolveAction(
        ActorDecision decision,
        PublicMatchContractManifest contract)
    {
        PublicActionDefinition? byId = decision.ActionId is { } actionId
            ? contract.Rules.Actions.FirstOrDefault(
                action => string.Equals(
                    action.Id,
                    actionId,
                    StringComparison.Ordinal))
            : null;
        PublicActionDefinition? byCode = decision.ActionCode is int actionCode
            ? contract.Rules.Actions.FirstOrDefault(
                action => action.Code == actionCode)
            : null;

        if (byId is null && byCode is null)
        {
            throw new ArgumentException(
                "A decision must identify a known action by ID or code.",
                nameof(decision));
        }
        if (byId is not null
            && byCode is not null
            && (byId.Id != byCode.Id || byId.Code != byCode.Code))
        {
            throw new ArgumentException(
                "Decision action ID and code refer to different actions.",
                nameof(decision));
        }
        if (decision.ActionId is not null && byId is null)
        {
            throw new ArgumentException(
                $"Unknown action ID '{decision.ActionId}'.",
                nameof(decision));
        }
        if (decision.ActionCode is not null && byCode is null)
        {
            throw new ArgumentException(
                $"Unknown action code {decision.ActionCode}.",
                nameof(decision));
        }
        return byId ?? byCode!;
    }
}
