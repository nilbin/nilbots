using BotArena.Sdk;

/// <summary>
/// A territorial tempo bot. It advances to the active objective, fires along
/// clear straight lines, and only leaves valuable control for serious danger.
/// </summary>
public sealed class TerritoryHolder : IGenericActorBot
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

        return ArenaBasics.TryFabricateReady(contract, context)
            ?? ArenaBasics.TryTerritorialRiskResponse(contract, context)
            ?? ArenaBasics.TryDirectShot(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(contract, context)
            ?? ArenaBasics.Wait(
                context,
                "holding active ground for the next capture tick");
    }
}
