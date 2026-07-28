using BotArena.Sdk;

namespace BotArena.Cli;

/// <summary>
/// Public-contract-only deterministic control used where a qualification
/// probe needs inert opposition. It discovers the Wait action by kind so
/// action IDs and codes remain ordinary contract inputs.
/// </summary>
internal sealed class FrontlineLabsQualificationWaitController
    : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");
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
            "qualification controller: passive");
    }
}
