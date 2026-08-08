using BotArena.Sdk;

/// <summary>
/// A competent apprentice: useful immediately, deliberately unsolved.
/// Nilbots creates one independent instance for every active body life.
/// </summary>
public sealed class HouseApprentice : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private Position? _recentDodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _recentDodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");

        // This priority list is the best first place to experiment. It handles
        // basic mechanics, but deliberately has no body roles, transformations,
        // curved-shot traps, focus fire, or opponent model.
        GenericActorDecision? fabrication =
            ArenaBasics.TryFabricateReady(contract, context);
        if (fabrication is not null)
            return fabrication;

        GenericActorDecision? dodge =
            ArenaBasics.TryDodge(contract, context);
        if (dodge is not null)
        {
            _recentDodgeOrigin = context.Self.Position;
            _avoidDodgeOriginThroughTick = context.Tick + 1;
            return dodge;
        }

        Position[] temporaryAvoid =
            _recentDodgeOrigin is Position dodgeOrigin
            && context.Tick <= _avoidDodgeOriginThroughTick
                ? [dodgeOrigin]
                : [];
        return ArenaBasics.TryDirectShot(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(
                contract,
                context,
                temporaryAvoid)
            ?? ArenaBasics.Wait(context, "holding position");
    }
}
