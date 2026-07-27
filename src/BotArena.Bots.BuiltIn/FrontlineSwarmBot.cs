using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Mobile replication smoke doctrine. Its Prime waits for every child slot,
/// fabricates each one, then all bodies pressure the live objective.
/// </summary>
public sealed class FrontlineSwarmBot : IActorBot
{
    private ActorMatchStart? _start;

    public void StartLife(ActorMatchStart start) => _start = start;

    public ActorDecision Tick(ActorContext context)
    {
        ActorMatchStart start = _start
            ?? throw new InvalidOperationException("Life was not started.");
        ActorDecision? fabrication =
            FrontlineReferenceBotLogic.TryFabricate(context);
        if (fabrication is not null)
            return fabrication;
        if (FrontlineReferenceBotLogic.HasInactiveChild(start, context))
        {
            return FrontlineReferenceBotLogic.MoveToHomePad(start, context);
        }
        return FrontlineReferenceBotLogic.TryAttack(start, context)
            ?? FrontlineReferenceBotLogic.MoveToActiveObjective(start, context);
    }
}
