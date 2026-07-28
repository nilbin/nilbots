using BotArena.Sdk;

namespace BotArena.Cli;

/// <summary>
/// Public-contract-only deterministic controller for the T2 straight-evade
/// probe. It fires the first available ordinary straight attack, then waits.
/// Probe geometry guarantees that the tested actor observes the projectile
/// before its threatened advance.
/// </summary>
internal sealed class FrontlineLabsQualificationOneShotController
    : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private bool _fired;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _fired = false;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");
        if (!_fired)
        {
            HashSet<string> attackIds = contract.Rules.Actions
                .Where(action =>
                    action.Kind
                        == GenericActorRulesContract.ActionKind.Attack)
                .Select(action => action.Id)
                .ToHashSet(StringComparer.Ordinal);
            GenericActorActionLegality? attack = context.ActionLegalities
                .Where(action =>
                    action.Available
                    && attackIds.Contains(action.ActionId))
                .OrderBy(action => action.ActionId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (attack is not null)
            {
                _fired = true;
                return GenericActorDecision.WithoutArguments(
                    attack.ActionId,
                    attack.ActionCode,
                    "qualification controller: one straight shot");
            }
        }

        HashSet<string> waitIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Wait)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality wait = context.ActionLegalities
            .Single(action =>
                action.Available
                && waitIds.Contains(action.ActionId));
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            "qualification controller: passive after shot");
    }
}
