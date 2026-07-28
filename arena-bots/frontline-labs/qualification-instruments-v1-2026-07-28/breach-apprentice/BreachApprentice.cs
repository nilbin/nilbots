using BotArena.Sdk;

/// <summary>
/// A positional apprentice: exact local geometry plus objective initiative.
/// Nilbots creates one independent instance for every active body life.
/// </summary>
public sealed class BreachApprentice : IGenericActorBot
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

        // Curves are used only for a previewed current intercept. Ordinary
        // objective tempo comes before an obvious straight exchange.
        GenericActorDecision? fabrication =
            ArenaBasics.TryFabricateReady(contract, context);
        if (fabrication is not null)
            return fabrication;

        // Cross a suppression lane while the currently visible projectile
        // still leaves one safe advance toward the active objective. Waiting
        // for the generic two-advance dodge window would concede initiative.
        GenericActorDecision? initiative =
            ArenaBasics.TryInitiativeAdvance(contract, context);
        if (initiative is not null)
            return initiative;

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
        return ArenaBasics.TryCurvedShot(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(
                contract,
                context,
                temporaryAvoid)
            ?? ArenaBasics.TryDirectShot(contract, context)
            ?? ArenaBasics.Wait(context, "holding position");
    }
}
