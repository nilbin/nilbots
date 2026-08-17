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
    private readonly List<FriendlyCoreEvent> _friendlyCoreEvents = [];
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);

    public void Reset()
    {
        _visibleAtDecision.Clear();
        _visibleAfterTick.Clear();
        _enemySamples.Clear();
        _entries.Clear();
        _destructions.Clear();
        _friendlyCorePickups.Clear();
        _friendlyCoreEvents.Clear();
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
            // Tick-start mode events (including Core pickup/catch) are already
            // authoritative when they enter this decision's observation and
            // may be visible only once. Record them now; Interval deliberately
            // excludes the current decision tick, so they become eligible on
            // the next decision. Movement is different: entry evidence needs
            // the following visibility sample, so defer it without consuming
            // its handle until that sample exists.
            if (observed.SourceTick > tick
                || observed.SourceTick == tick
                    && observed.Payload is GenericActorContext.EventPayload.Movement
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
                    _friendlyCoreEvents.Add(new FriendlyCoreEvent(
                        observed.SourceTick, pickup.CoreId,
                        pickup.CarrierActorId, "acquisition"));
                    break;
                case GenericActorContext.EventPayload.ArcRelay mode
                    when mode.Fact is GenericActorContext.ArcRelayEvent
                        .CoreHandedOff handoff
                        && handoff.TargetActorId.TeamId == teamId:
                    _friendlyCoreEvents.Add(new FriendlyCoreEvent(
                        observed.SourceTick, handoff.CoreId,
                        handoff.TargetActorId, "acquisition"));
                    break;
                case GenericActorContext.EventPayload.ArcRelay mode
                    when mode.Fact is GenericActorContext.ArcRelayEvent
                        .CoreBanked banked
                        && banked.TeamId == teamId:
                    _friendlyCoreEvents.Add(new FriendlyCoreEvent(
                        observed.SourceTick, banked.CoreId,
                        banked.CarrierActorId, "bank"));
                    break;
                case GenericActorContext.EventPayload.ArcRelay mode
                    when mode.Fact is GenericActorContext.ArcRelayEvent
                        .CoreDropped dropped
                        && dropped.SourceActorId.TeamId == teamId:
                    string? outcome = dropped.Kind switch
                    {
                        "voluntary" => "drop",
                        "destruction" or "arc-toss-landing" => "loss",
                        // Signature departure is the first half of Arc Toss,
                        // not a tactical loss. The later catch/pickup or loose
                        // landing supplies the authoritative outcome.
                        _ => null,
                    };
                    if (outcome is not null)
                    {
                        _friendlyCoreEvents.Add(new FriendlyCoreEvent(
                            observed.SourceTick, dropped.CoreId,
                            dropped.SourceActorId, outcome));
                    }
                    break;
            }
        }
    }

    public EvidenceRead Read(
        OrdersPackage package,
        GenericActorMapContract map,
        OrdersPackage.Condition condition,
        int decisionTick,
        int orderTicks,
        Func<GenericActorContext.ArcRelayCoreId, int>? bankValue = null,
        Func<GenericActorContext.ArcRelayCoreId, bool>? sourceMatches = null)
        => ReadCore(
            condition,
            decisionTick,
            orderTicks,
            unitId => package.RolesByUnit.TryGetValue(unitId, out var role)
                ? role.Id
                : null,
            bankValue,
            sourceMatches,
            fallback: () => Read(
                condition,
                decisionTick,
                orderTicks,
                position => package.Contains(condition.Zone!, position),
                package.ZoneTiles(condition.Zone!, map)));

    internal EvidenceRead ReadCore(
        OrdersPackage.Condition condition,
        int decisionTick,
        int orderTicks,
        Func<int, string?> roleForUnit,
        Func<GenericActorContext.ArcRelayCoreId, int>? bankValue = null,
        Func<GenericActorContext.ArcRelayCoreId, bool>? sourceMatches = null,
        Func<EvidenceRead>? fallback = null)
    {
        if (condition.Fact == "friendly-core-pickups")
        {
            (int start, int end) = Interval(
                condition, decisionTick, orderTicks);
            int count = _friendlyCorePickups.Count(value =>
                value.Tick >= start && value.Tick < end
                && Matches(roleForUnit, condition, value.Core, value.Carrier,
                    bankValue, sourceMatches));
            string interval = end <= start
                ? $"{start}..empty"
                : $"{start}..{end - 1}";
            return new EvidenceRead(
                count,
                $"{condition.Id}={count}-friendly-core-pickups@{interval}");
        }
        if (condition.Fact is "friendly-core-acquisitions"
                or "friendly-core-banks" or "friendly-core-drops"
                or "friendly-core-losses" or "friendly-core-resolutions")
        {
            (int start, int end) = Interval(condition, decisionTick, orderTicks);
            string[] kinds = condition.Fact switch
            {
                "friendly-core-banks" => ["bank"],
                "friendly-core-drops" => ["drop"],
                "friendly-core-losses" => ["loss"],
                "friendly-core-resolutions" => condition.Outcomes ?? [],
                _ => ["acquisition"],
            };
            FriendlyCoreEvent[] values = _friendlyCoreEvents.Where(value =>
                    kinds.Contains(value.Kind, StringComparer.Ordinal)
                    && value.Tick >= start && value.Tick < end
                    && Matches(roleForUnit, condition, value.Core, value.Actor,
                        bankValue, sourceMatches))
                .ToArray();
            if (condition.CorrelateAcquisition)
            {
                HashSet<GenericActorContext.ArcRelayCoreId> acquired =
                    _friendlyCoreEvents.Where(value =>
                            value.Kind == "acquisition"
                            && value.Tick >= start && value.Tick < end
                            && Matches(roleForUnit, condition, value.Core, value.Actor,
                                bankValue, sourceMatches))
                        .Select(value => value.Core).ToHashSet();
                values = values.Where(value => acquired.Contains(value.Core)).ToArray();
            }
            string interval = end <= start
                ? $"{start}..empty" : $"{start}..{end - 1}";
            return new EvidenceRead(values.Length,
                $"{condition.Id}={values.Length}-{string.Join("-or-", kinds)}-events@{interval}");
        }
        return fallback?.Invoke()
            ?? throw new InvalidOperationException(
                $"No evidence reader exists for '{condition.Fact}'.");
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

    private static bool Matches(
        Func<int, string?> roleForUnit,
        OrdersPackage.Condition condition,
        GenericActorContext.ArcRelayCoreId core,
        ActorIdentity actor,
        Func<GenericActorContext.ArcRelayCoreId, int>? bankValue,
        Func<GenericActorContext.ArcRelayCoreId, bool>? sourceMatches)
    {
        if (condition.Roles is { Length: > 0 }
            && (roleForUnit(actor.UnitId) is not string roleId
                || !condition.Roles.Contains(roleId, StringComparer.Ordinal)))
            return false;
        if (sourceMatches is not null && !sourceMatches(core)) return false;
        return condition.CoreBankValue is null || bankValue is not null
            && condition.CoreBankValue.Matches(bankValue(core));
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
    private sealed record FriendlyCoreEvent(
        int Tick,
        GenericActorContext.ArcRelayCoreId Core,
        ActorIdentity Actor,
        string Kind);
}
