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
        if (objective.Length == 0)
            return lens.HomeAnchor;
        int x = 0;
        int y = 0;
        foreach (Position tile in objective)
        {
            x += tile.X;
            y += tile.Y;
        }
        return new Position(x / objective.Length, y / objective.Length);
    }

    /// <summary>
    /// The banker's lending rule. It replaces losses and covers a body deficit,
    /// but refuses to lend eagerly when we already field more bodies than the
    /// other side and the objective is ours - that unspent slot is a fast
    /// rebuild banked for the exchange that is still coming.
    /// </summary>
    public bool WantsReplacement(MatchLens lens, GenericActorContext context)
    {
        bool holdingObjective =
            context.Mode
                is GenericActorContext.ModeObservationState.Frontline mode
            && mode.ClaimingTeamId == lens.TeamId;
        if (FieldBodies > EnemyBodies && holdingObjective && LossesOwed == 0)
            return false;
        return FieldBodies == 0
            || FieldBodies < EnemyBodies
            || LossesOwed > 0;
    }

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
