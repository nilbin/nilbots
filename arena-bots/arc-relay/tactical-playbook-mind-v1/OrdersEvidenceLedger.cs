using BotArena.Sdk;

/// <summary>
/// Deterministic, team-shared evidence reconstructed from the observation
/// stream. Sheets may query this finite ledger but can never mutate it.
/// </summary>
internal sealed class OrdersEvidenceLedger
{
    private readonly Dictionary<int, HashSet<Position>> _visibleAtDecision = [];
    private readonly Dictionary<int, HashSet<Position>> _visibleAfterTick = [];
    private readonly List<EnemySample> _enemySamples = [];
    private readonly List<EnemyEntry> _entries = [];
    private readonly List<EnemyDestruction> _destructions = [];
    private readonly List<FriendlyCorePickup> _friendlyCorePickups = [];
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);

    public void Reset()
    {
        _visibleAtDecision.Clear();
        _visibleAfterTick.Clear();
        _enemySamples.Clear();
        _entries.Clear();
        _destructions.Clear();
        _friendlyCorePickups.Clear();
        _processedEvents.Clear();
    }

    public void Observe(int teamId, MindContext mind)
        => ObserveSnapshot(
            teamId,
            mind.Tick,
            mind.VisibleTiles.Select(tile => tile.Position),
            mind.Enemies,
            mind.VisibleEvents);

    internal void ObserveSnapshot(
        int teamId,
        int tick,
        IEnumerable<Position> visibleTiles,
        IEnumerable<GenericActorContext.ObservedEnemyState> enemies,
        IEnumerable<GenericActorContext.ObservedEvent> visibleEvents)
    {
        HashSet<Position> visible = visibleTiles.ToHashSet();
        _visibleAtDecision[tick] = visible;

        int completedTick = tick - 1;
        if (completedTick >= 0)
        {
            _visibleAfterTick[completedTick] = visible;
            foreach (GenericActorContext.ObservedEnemyState enemy in enemies)
            {
                _enemySamples.Add(new EnemySample(
                    completedTick, enemy.ActorId, enemy.Position));
            }
        }

        foreach (GenericActorContext.ObservedEvent observed in visibleEvents)
        {
            // A same-tick event is not historical evidence yet. Do not mark it
            // consumed before the next decision can evaluate it as completed.
            if (observed.SourceTick >= tick
                || !_processedEvents.Add(observed.EventHandle))
                continue;
            switch (observed.Payload)
            {
                case GenericActorContext.EventPayload.Movement movement
                    when movement.ActorId.TeamId != teamId
                        && _visibleAtDecision.TryGetValue(
                            observed.SourceTick, out HashSet<Position>? before)
                        && _visibleAtDecision.TryGetValue(
                            observed.SourceTick + 1, out HashSet<Position>? after)
                        && before.Contains(movement.From)
                        && after.Contains(movement.To):
                    _entries.Add(new EnemyEntry(
                        observed.SourceTick,
                        movement.ActorId,
                        movement.From,
                        movement.To));
                    break;
                case GenericActorContext.EventPayload.Destruction destruction
                    when destruction.ActorId.TeamId != teamId:
                    _destructions.Add(new EnemyDestruction(
                        observed.SourceTick,
                        destruction.ActorId,
                        destruction.Position));
                    break;
                case GenericActorContext.EventPayload.ArcRelay mode
                    when mode.Fact is GenericActorContext.ArcRelayEvent
                        .CorePickedUp pickup
                        && pickup.CarrierActorId.TeamId == teamId:
                    _friendlyCorePickups.Add(new FriendlyCorePickup(
                        observed.SourceTick,
                        pickup.CoreId,
                        pickup.CarrierActorId));
                    break;
            }
        }
    }

    public EvidenceRead Read(
        OrdersPackage package,
        GenericActorMapContract map,
        OrdersPackage.Condition condition,
        int decisionTick,
        int orderTicks)
    {
        if (condition.Fact == "friendly-core-pickups")
        {
            (int start, int end) = Interval(
                condition, decisionTick, orderTicks);
            int count = _friendlyCorePickups.Count(value =>
                value.Tick >= start && value.Tick < end);
            string interval = end <= start
                ? $"{start}..empty"
                : $"{start}..{end - 1}";
            return new EvidenceRead(
                count,
                $"{condition.Id}={count}-friendly-core-pickups@{interval}");
        }
        return Read(
            condition,
            decisionTick,
            orderTicks,
            position => package.Contains(condition.Zone!, position),
            package.ZoneTiles(condition.Zone!, map));
    }

    internal EvidenceRead Read(
        OrdersPackage.Condition condition,
        int decisionTick,
        int orderTicks,
        Func<Position, bool> inZone,
        IReadOnlyCollection<Position> zoneTiles)
    {
        (int start, int end) = Interval(condition, decisionTick, orderTicks);
        string interval = end <= start
            ? $"{start}..empty"
            : $"{start}..{end - 1}";
        if (condition.Fact == "friendly-core-pickups")
        {
            int count = _friendlyCorePickups.Count(value =>
                value.Tick >= start && value.Tick < end);
            return new EvidenceRead(
                count,
                $"{condition.Id}={count}-friendly-core-pickups@{interval}");
        }
        if (condition.Fact == "observed-enemy-entries-in-zone")
        {
            EnemyEntry[] values = _entries.Where(value =>
                    value.Tick >= start && value.Tick < end
                    && !inZone(value.From)
                    && inZone(value.To))
                .ToArray();
            int lives = values.Select(value => value.Actor).Distinct().Count();
            return new EvidenceRead(
                values.Length,
                $"{condition.Id}={values.Length}-entries/{lives}-lives@{interval}");
        }
        if (condition.Fact == "distinct-enemy-lives-observed-in-zone")
        {
            int lives = _enemySamples.Where(value =>
                    value.Tick >= start && value.Tick < end
                    && inZone(value.Position))
                .Select(value => value.Actor).Distinct().Count();
            return new EvidenceRead(
                lives,
                $"{condition.Id}={lives}-lives@{interval}");
        }
        if (condition.Fact == "confirmed-enemy-destructions-in-zone")
        {
            EnemyDestruction[] values = _destructions.Where(value =>
                    value.Tick >= start && value.Tick < end
                    && inZone(value.Position))
                .ToArray();
            int lives = values.Select(value => value.Actor).Distinct().Count();
            return new EvidenceRead(
                values.Length,
                $"{condition.Id}={values.Length}-kills/{lives}-lives@{interval}");
        }

        int? firstGap = null;
        if (end <= start)
            firstGap = start;
        for (int tick = start; tick < end && firstGap is null; tick++)
        {
            if (!_visibleAfterTick.TryGetValue(
                    tick, out HashSet<Position>? tickVisible)
                || zoneTiles.Any(tile => !tickVisible.Contains(tile)))
            {
                firstGap = tick;
            }
        }
        return firstGap is int gap
            ? new EvidenceRead(
                0, $"{condition.Id}=coverage-gap-{gap}@{interval}")
            : new EvidenceRead(
                1, $"{condition.Id}=coverage-full@{interval}");
    }

    private static (int Start, int End) Interval(
        OrdersPackage.Condition condition,
        int decisionTick,
        int orderTicks)
    {
        int start = condition.WindowTicks is int window
            ? Math.Max(0, decisionTick - window)
            : condition.Since == "node-entry"
                ? Math.Max(0, decisionTick - orderTicks)
                : 0;
        return (start, decisionTick);
    }

    internal readonly record struct EvidenceRead(int Actual, string Trace);
    private sealed record EnemySample(
        int Tick, ActorIdentity Actor, Position Position);
    private sealed record EnemyEntry(
        int Tick, ActorIdentity Actor, Position From, Position To);
    private sealed record EnemyDestruction(
        int Tick, ActorIdentity Actor, Position Position);
    private sealed record FriendlyCorePickup(
        int Tick,
        GenericActorContext.ArcRelayCoreId Core,
        ActorIdentity Carrier);
}
