using BotArena.Sdk;

/// <summary>
/// A public-geometry evasion baseline. It treats each visible projectile's
/// current heading as manifested danger, preserves objective control when a
/// safe adjacent tile permits it, and fires only obvious straight shots.
/// </summary>
public sealed class GeometricDodger : IGenericActorBot
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

        // Evasion precedes tempo. The policy deliberately does not infer a
        // projectile's private future bend or learn an opponent model.
        return ArenaBasics.TryDodge(contract, context)
            ?? ArenaBasics.TryDirectShot(contract, context)
            ?? ArenaBasics.TryFabricateReady(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(contract, context)
            ?? ArenaBasics.Wait(context, "holding position");
    }
}
