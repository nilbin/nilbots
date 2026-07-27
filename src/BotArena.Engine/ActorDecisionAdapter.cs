namespace BotArena.Engine;

/// <summary>
/// Temporary Prime-only bridge from the extensible actor action contract to
/// the Package 3 action enum. Package 5 replaces this with entity-action
/// validation owned directly by the Frontline session.
/// </summary>
public static class ActorDecisionAdapter
{
    public static BotDecision ToPrimeDecision(
        ActorDecision decision,
        PublicMatchContractManifest contract)
    {
        ActorDecision canonical = Normalize(decision, contract);
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
        PublicMatchContractManifest contract)
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
        if (!Enum.IsDefined(typeof(BotAction), action.Code))
        {
            throw new NotSupportedException(
                $"Prime-only Frontline cannot execute action code {action.Code}.");
        }

        ActorActionPayload? payload = decision.Payload;
        if (payload?.Direction is not null
            || payload?.UnitTarget is not null
            || payload?.FormTargetId is not null)
        {
            throw new ArgumentException(
                "Prime-only Frontline supports only the shot-program payload.",
                nameof(decision));
        }

        var botAction = (BotAction)action.Code;
        if (payload?.ShotProgram is not null
            && (!action.ParameterKinds.Contains(
                    PublicActionParameterKind.ShotProgram)
                || botAction != BotAction.Shoot))
        {
            throw new ArgumentException(
                "A shot program may only accompany an action whose active contract declares that parameter.",
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

        ActorActionPayload? canonicalPayload = payload is not null
            && (payload.ShotProgram is not null
                || payload.Direction is not null
                || payload.UnitTarget is not null
                || payload.FormTargetId is not null)
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
