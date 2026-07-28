using BotArena.Sdk;

/// <summary>
/// A competent apprentice: useful immediately, deliberately unsolved.
/// Nilbots creates one independent instance for every active body life.
/// </summary>
public sealed class BOTNAME : IGenericActorBot
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

        // This priority list is the best first place to experiment. It handles
        // basic mechanics, but deliberately has no body roles, transformations,
        // curved-shot traps, focus fire, or opponent model.
        return ArenaBasics.TryFabricateReady(contract, context)
            ?? ArenaBasics.TryDodge(contract, context)
            ?? ArenaBasics.TryDirectShot(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(contract, context)
            ?? ArenaBasics.Wait(context, "holding position");
    }
}
