using BotArena.Sdk;

/// <summary>
/// A tactical apprentice: exact local projectile geometry, still no doctrine.
/// Nilbots creates one independent instance for every active body life.
/// </summary>
public sealed class ArcApprentice : IGenericActorBot
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
