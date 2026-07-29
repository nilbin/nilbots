using BotArena.Sdk;

/// <summary>
/// The attrition banker's books, kept privately by one life. It tracks what we
/// owe (bodies lost since the last queue), what the other side is fielding, and
/// where the last exchange actually happened - the three numbers that decide
/// whether the bank lends another child and where it drops it. Memory is
/// life-scoped by contract, so every value is rebuilt from observations rather
/// than assumed to survive a death.
/// </summary>
internal sealed class Ledger
{
    private const int MemoryTicks = 40;
    private const int ExchangeMemoryTicks = 60;

    private readonly Dictionary<ActorIdentity, (Position Tile, int Tick)>
        _enemySeen = [];
    private HashSet<int>? _activeUnitsLastTick;
    private int _healthLastTick = -1;
    private Position? _lastExchange;
    private int _lastExchangeTick = int.MinValue;

    /// <summary>Bodies destroyed on our side that no queue has replaced yet.</summary>
    public int LossesOwed { get; private set; }

    /// <summary>Active own slots that are not the economy anchor.</summary>
    public int FieldBodies { get; private set; }

    /// <summary>Active own slots in total, including this life.</summary>
    public int OwnBodies { get; private set; }

    /// <summary>Best current estimate of the opposing body count.</summary>
    public int EnemyBodies { get; private set; } = 1;

    /// <summary>Tiles where an enemy body is currently believed to stand.</summary>
    public List<Position> KnownEnemyTiles { get; } = [];

    /// <summary>Folds this tick's observation into the books.</summary>
    public void Observe(MatchLens lens, GenericActorContext context)
    {
        var active = new HashSet<int>();
        FieldBodies = 0;
        OwnBodies = 0;
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != lens.TeamId
                || slot.State.Kind
                    != GenericActorContext.UnitSlotStateKind.Active)
            {
                continue;
            }
            active.Add(slot.UnitId);
            OwnBodies++;
            if (!lens.IsAlliedBankUnit(slot.UnitId))
                FieldBodies++;
        }

        if (_activeUnitsLastTick is HashSet<int> previous)
        {
            foreach (int unitId in previous)
            {
                if (!active.Contains(unitId))
                    LossesOwed++;
            }
        }
        _activeUnitsLastTick = active;

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            _enemySeen[enemy.ActorId] = (enemy.Position, context.Tick);

        foreach (GenericActorContext.ObservedEvent visible in context.VisibleEvents)
        {
            switch (visible.Payload)
            {
                case GenericActorContext.EventPayload.Destruction destruction:
                    _enemySeen.Remove(destruction.ActorId);
                    Record(destruction.Position, visible.SourceTick);
                    break;
                case GenericActorContext.EventPayload.Damage damage:
                    Record(damage.Position, visible.SourceTick);
                    break;
                case GenericActorContext.EventPayload.Attack attack:
                    if (attack.ActorId.TeamId != lens.TeamId)
                        Record(attack.Origin, visible.SourceTick);
                    break;
                default:
                    break;
            }
        }

        if (_healthLastTick >= 0 && context.Self.Health < _healthLastTick)
            Record(context.Self.Position, context.Tick);
        _healthLastTick = context.Self.Health;

        foreach (ActorIdentity stale in _enemySeen
                     .Where(entry => context.Tick - entry.Value.Tick > MemoryTicks)
                     .Select(entry => entry.Key)
                     .ToList())
        {
            _enemySeen.Remove(stale);
        }

        KnownEnemyTiles.Clear();
        foreach (KeyValuePair<ActorIdentity, (Position Tile, int Tick)> entry
                 in _enemySeen.OrderBy(entry => entry.Key))
        {
            KnownEnemyTiles.Add(entry.Value.Tile);
        }
        EnemyBodies = Math.Max(1, _enemySeen.Count);
    }

    /// <summary>
    /// Where the next companion belongs: the last exchange while it is still
    /// fresh, otherwise the objective we are contesting.
    /// </summary>
    public Position ExchangeAnchor(MatchLens lens, GenericActorContext context)
    {
        if (_lastExchange is Position exchange
            && context.Tick - _lastExchangeTick <= ExchangeMemoryTicks)
        {
            return exchange;
        }
        Position[] objective = lens.ActiveObjective(context);
        return objective.Length == 0
            ? lens.HomeAnchor
            : MatchLens.Centroid(objective);
    }

    /// <summary>
    /// The banker's lending rule, restated after the wave-1 loss review.
    ///
    /// The first revision denominated solvency in enemies it could currently
    /// see and refused to lend while that count was matched. Against ranged
    /// pressure the count was almost always wrong: a facing-quadrant sensor
    /// was blind on a third to a half of all our ticks, the estimate floored at
    /// one, and the ledger sat on a Ready slot for forty to a hundred and sixty
    /// slot-ticks per match while the other side fielded more bodies than we
    /// did in a third to a half of every match. A banked slot is not a reserve;
    /// it is an unpaid debt earning nothing.
    ///
    /// So solvency is now denominated in the opposing capacity the *contract*
    /// declares - unit slots whose unlock tick has passed - and the target is
    /// one body clear of it. The rebuild clock, not the slot, is the float.
    /// </summary>
    public bool WantsReplacement(MatchLens lens, GenericActorContext context)
    {
        SolvencyTarget = lens.EnemySlotCapacity(context.Tick) + 1;
        return LossesOwed > 0
            || OwnBodies < SolvencyTarget
            || FieldBodies < EnemyBodies;
    }

    /// <summary>Bodies the ledger currently wants on the field.</summary>
    public int SolvencyTarget { get; private set; } = 2;

    /// <summary>Clears the debt a queued replacement has just settled.</summary>
    public void RecordQueued() => LossesOwed = Math.Max(0, LossesOwed - 1);

    private void Record(Position tile, int tick)
    {
        if (tick < _lastExchangeTick)
            return;
        _lastExchange = tile;
        _lastExchangeTick = tick;
    }
}
