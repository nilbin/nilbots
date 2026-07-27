using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Anchor/turret smoke doctrine. The Prime fabricates the complete roster;
/// children leave protected pads, Anchor on legal defensive tiles, and fire
/// with the turret's absolute-heading action.
/// </summary>
public sealed class FrontlineBastionBot : IActorBot
{
    private ActorMatchStart? _start;

    public void StartLife(ActorMatchStart start) => _start = start;

    public ActorDecision Tick(ActorContext context)
    {
        ActorMatchStart start = _start
            ?? throw new InvalidOperationException("Life was not started.");
        if (FrontlineReferenceBotLogic.IsFabricator(start, context))
        {
            ActorDecision? fabrication =
                FrontlineReferenceBotLogic.TryFabricate(context);
            if (fabrication is not null)
                return fabrication;
            if (FrontlineReferenceBotLogic.HasInactiveChild(start, context))
            {
                return FrontlineReferenceBotLogic.MoveToHomePad(
                    start,
                    context);
            }
            return FrontlineReferenceBotLogic.TryAttack(start, context)
                ?? FrontlineReferenceBotLogic.MoveToDefensiveLine(
                    start,
                    context);
        }

        if (context.Self.PendingFormTransition is not null)
            return Actions.Wait();
        ActorDecision? anchor =
            FrontlineReferenceBotLogic.TryAnchor(start, context);
        if (anchor is not null)
            return anchor;
        if (string.Equals(
                context.Self.FormId,
                start.Contract.Rules.Frontline?.TurretFire.FormId,
                StringComparison.Ordinal))
        {
            return FrontlineReferenceBotLogic.TryTurretShot(start, context)
                ?? Actions.Wait();
        }
        return FrontlineReferenceBotLogic.TryAttack(start, context)
            ?? FrontlineReferenceBotLogic.MoveToAnchorSite(start, context);
    }
}
