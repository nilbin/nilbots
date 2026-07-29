using BotArena.Sdk;

/// <summary>
/// What the bodies this life has actually watched actually did.
///
/// Revision 1 assumed a dodge rate. Against an opponent that refuses to close
/// and steps off every bolt, that assumption prices a straight shot at over
/// one full point of expected value while it lands nothing, and the duelist
/// keeps buying it — the exact shape of a stalled exchange in the measured
/// striker mirror: fire, watch it step aside, fire again, for hundreds of
/// ticks with the objective contested and nobody scoring.
///
/// So the rate is measured instead. Each tick this ledger compares where the
/// visible enemies were with where they are, counting only bodies that could
/// see this one — a target that cannot see the gun is not choosing to hold
/// still, and its stillness says nothing about how it dodges. The estimate
/// starts at revision 1's assumption and moves as evidence arrives.
///
/// The ledger is life-scoped, like every other private memory here: a fresh
/// life starts from the prior and learns its own opponent again.
/// </summary>
internal sealed class DodgeLedger
{
    /// <summary>
    /// Share of watched ticks revision 1 implicitly assumed a target holds its
    /// tile, derived from its weight of 2.5 against four unit-weight steps.
    /// </summary>
    private const double AssumedStillRate = 2.5 / 6.5;

    /// <summary>Evidence weight of that assumption, in observations.</summary>
    private const double PriorWeight = 6.0;

    /// <summary>Bounds on how far measurement may move the assumption.</summary>
    private const double MinFactor = 0.10;
    private const double MaxFactor = 3.0;

    private readonly Dictionary<ActorIdentity, Position> _lastSeen = [];
    private double _still;
    private double _moved;

    /// <summary>
    /// Folds this tick's observation into the estimate. Call once per tick,
    /// before the shot solver prices anything.
    /// </summary>
    public void Observe(Doctrine doctrine, GenericActorContext context)
    {
        Position self = context.Self.Position;
        var current = new Dictionary<ActorIdentity, Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            current[enemy.ActorId] = enemy.Position;
            if (!_lastSeen.TryGetValue(enemy.ActorId, out Position previous))
                continue;

            // Only a body that could see this one is choosing whether to move
            // out of its way; anyone else is just walking its own errand.
            GenericActorRulesContract.VisionProfile? vision =
                doctrine.VisionFor(enemy.FormId);
            if (vision is not null
                && !doctrine.CanSee(vision, previous, enemy.Facing, self))
            {
                continue;
            }
            if (enemy.Position == previous)
                _still += 1.0;
            else
                _moved += 1.0;
        }
        _lastSeen.Clear();
        foreach (KeyValuePair<ActorIdentity, Position> entry in current)
            _lastSeen[entry.Key] = entry.Value;
    }

    /// <summary>
    /// Multiplier on the assumed stay-put weight of a watching target. One
    /// while nothing has been observed; well below one against a body that has
    /// stepped off every bolt so far, which is what finally lets a bend — or
    /// the tile itself — outbid a straight shot that has stopped working.
    /// </summary>
    /// <param name="priorScale">
    /// How much stickier the movement profile makes a sidestep before any
    /// evidence arrives — face-movement spends the target's own aim and sight
    /// quadrant on a dodge, and facing-locked does not offer a sidestep at
    /// all. It shifts the prior rather than the answer, so measurement still
    /// overrules it once the bodies on this map have been watched.
    /// </param>
    public double WatchedInertiaFactor(double priorScale)
    {
        double prior = Math.Clamp(AssumedStillRate * priorScale, 0.02, 0.90);
        double still = _still + PriorWeight * prior;
        double total = _still + _moved + PriorWeight;
        double rate = still / total;
        return Math.Clamp(rate / AssumedStillRate, MinFactor, MaxFactor);
    }
}
