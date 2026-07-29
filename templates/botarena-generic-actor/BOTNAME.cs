using BotArena.Sdk;

/// <summary>
/// A competent apprentice: useful immediately, deliberately unsolved.
/// Nilbots creates one independent instance for every active body life.
/// </summary>
public sealed class BOTNAME : IGenericActorBot
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
        //
        // It is also deliberately blind to what a push is WORTH. Contracts
        // differ in what completes a capture, in whether a completed advance
        // locks, in whether a second body on the objective adds pressure, and
        // in what erodes a claim — none of which changes the observation
        // schema. ArenaBasics.Capture(contract) and
        // ArenaBasics.ObjectivePresence(contract, context) answer those from
        // the contract; branch on them here rather than assuming an arm.
        // Likewise ArenaBasics.ExpectedArrivalTiles says where dying puts you,
        // which is not always home.
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
