using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Objective-first smoke doctrine. It deliberately declines fabrication and
/// Anchor so evaluation plumbing has a simple mobile-pressure baseline.
/// </summary>
public sealed class FrontlineRusherBot : IActorBot
{
    private ActorMatchStart? _start;

    public void StartLife(ActorMatchStart start) => _start = start;

    public ActorDecision Tick(ActorContext context)
    {
        ActorMatchStart start = _start
            ?? throw new InvalidOperationException("Life was not started.");
        return FrontlineReferenceBotLogic.TryAttack(start, context)
            ?? FrontlineReferenceBotLogic.MoveToActiveObjective(start, context);
    }
}
