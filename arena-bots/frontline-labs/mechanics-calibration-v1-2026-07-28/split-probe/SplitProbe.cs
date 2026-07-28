using BotArena.Sdk;

/// <summary>
/// A deterministic mechanics probe that waits for a ready allied slot and
/// submits the contract-declared Split action exactly once.
/// </summary>
public sealed class SplitProbe : IGenericActorBot
{
    private GenericActorRulesContract.SplitReplicationTransition? _split;
    private bool _isInitialLife;
    private bool _submitted;

    public void StartLife(GenericActorMatchStart start)
    {
        _split = start.Contract.Rules.ReplicationTransitions
            .OfType<GenericActorRulesContract.SplitReplicationTransition>()
            .OrderBy(transition => transition.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();
        _isInitialLife =
            start.Origin.Reason == GenericActorMatchStart.SpawnReason.Initial;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (context.Self.PendingSameLifeTransition is not null)
            return Wait(context, "split windup");

        if (_submitted
            && context.Self.PreviousActionResolution is { } prior
            && _split is not null
            && string.Equals(
                prior.AcceptedAction.ActionId,
                _split.ActionId,
                StringComparison.Ordinal)
            && prior.Outcome
                != GenericActorActionResolution.ActionOutcome.Success)
        {
            _submitted = false;
        }

        bool hasReadySlot = context.TeamUnits.Any(slot =>
            slot.TeamId == context.Self.ActorId.TeamId
            && slot.State is GenericActorContext.UnitSlotState.Ready);
        bool sourceFormAllowed = _split?.SourceFormIds.Contains(
            context.Self.FormId,
            StringComparer.Ordinal) == true;
        GenericActorActionLegality? legality = _split is null
            ? null
            : context.Action(_split.ActionId);

        if (_isInitialLife
            && !_submitted
            && hasReadySlot
            && sourceFormAllowed
            && legality is { Available: true })
        {
            _submitted = true;
            return GenericActorDecision.WithoutArguments(
                legality.ActionId,
                legality.ActionCode,
                "calibration: split with a ready allied slot");
        }

        return Wait(
            context,
            _isInitialLife
                ? "calibration: waiting for legal split"
                : "calibration: replication descendant idle");
    }

    private static GenericActorDecision Wait(
        GenericActorContext context,
        string debug)
    {
        GenericActorActionLegality wait = context.ActionLegalities
            .Where(action => action.Available)
            .Join(
                context.ActionLegalities,
                action => action.ActionId,
                action => action.ActionId,
                (action, _) => action)
            .FirstOrDefault(action =>
                string.Equals(action.ActionId, "wait", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The contract exposes no available wait action.");
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            debug);
    }
}
