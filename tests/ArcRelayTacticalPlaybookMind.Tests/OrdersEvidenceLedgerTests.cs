using BotArena.Sdk;

public sealed class OrdersEvidenceLedgerTests
{
    private static readonly ActorIdentity Observer = new(0, 0, 0);
    private static readonly ActorIdentity EnemyLife0 = new(1, 0, 0);
    private static readonly ActorIdentity EnemyLife1 = new(1, 0, 1);
    private static readonly Position[] ZoneTiles =
        [new(1, 0), new(1, 1)];

    [Fact]
    public void LedgerCountsOnlyObservedCompletedEvidenceAndExplainsCoverage()
    {
        var ledger = new OrdersEvidenceLedger();
        ledger.ObserveSnapshot(
            teamId: 0,
            tick: 0,
            visibleTiles: [new Position(0, 0), .. ZoneTiles],
            enemies: [],
            visibleEvents: []);
        ledger.ObserveSnapshot(
            teamId: 0,
            tick: 1,
            visibleTiles: [new Position(0, 0), .. ZoneTiles],
            enemies: [Enemy(EnemyLife0, new Position(1, 0))],
            visibleEvents:
            [
                Movement("move-0", 0, EnemyLife0,
                    new Position(0, 0), new Position(1, 0)),
            ]);

        OrdersEvidenceLedger.EvidenceRead entries = ledger.Read(
            Condition("observed-enemy-entries-in-zone", windowTicks: 10),
            decisionTick: 1,
            orderTicks: 0,
            InZone,
            ZoneTiles);
        OrdersEvidenceLedger.EvidenceRead lives = ledger.Read(
            Condition("distinct-enemy-lives-observed-in-zone", since: "match-start"),
            decisionTick: 1,
            orderTicks: 0,
            InZone,
            ZoneTiles);
        OrdersEvidenceLedger.EvidenceRead covered = ledger.Read(
            Condition("zone-fully-observed-throughout-interval", windowTicks: 1),
            decisionTick: 1,
            orderTicks: 0,
            InZone,
            ZoneTiles);

        Assert.Equal(1, entries.Actual);
        Assert.Contains("1-entries/1-lives@0..0", entries.Trace);
        Assert.Equal(1, lives.Actual);
        Assert.Equal(1, covered.Actual);
        Assert.Contains("coverage-full@0..0", covered.Trace);

        ledger.ObserveSnapshot(
            teamId: 0,
            tick: 2,
            visibleTiles: [new Position(1, 0)],
            enemies: [Enemy(EnemyLife1, new Position(1, 0))],
            visibleEvents:
            [
                Destruction("kill-1", 1, EnemyLife0, new Position(1, 0)),
            ]);

        OrdersEvidenceLedger.EvidenceRead kills = ledger.Read(
            Condition(
                "confirmed-enemy-destructions-in-zone", since: "node-entry"),
            decisionTick: 2,
            orderTicks: 1,
            InZone,
            ZoneTiles);
        OrdersEvidenceLedger.EvidenceRead gap = ledger.Read(
            Condition("zone-fully-observed-throughout-interval", windowTicks: 2),
            decisionTick: 2,
            orderTicks: 0,
            InZone,
            ZoneTiles);

        Assert.Equal(1, kills.Actual);
        Assert.Contains("1-kills/1-lives@1..1", kills.Trace);
        Assert.Equal(0, gap.Actual);
        Assert.Contains("coverage-gap-1@0..1", gap.Trace);
    }

    [Fact]
    public void HiddenOriginDoesNotBecomeAnEntryAfterTheEnemyAppears()
    {
        var ledger = new OrdersEvidenceLedger();
        ledger.ObserveSnapshot(0, 0, ZoneTiles, [], []);
        ledger.ObserveSnapshot(
            0,
            1,
            ZoneTiles,
            [Enemy(EnemyLife0, new Position(1, 0))],
            [Movement("hidden-origin", 0, EnemyLife0,
                new Position(0, 0), new Position(1, 0))]);

        OrdersEvidenceLedger.EvidenceRead entries = ledger.Read(
            Condition("observed-enemy-entries-in-zone", windowTicks: 10),
            1,
            0,
            InZone,
            ZoneTiles);

        Assert.Equal(0, entries.Actual);
        Assert.Contains("0-entries", entries.Trace);
    }

    [Fact]
    public void ResetRemovesTeamEvidenceBetweenMatches()
    {
        var ledger = new OrdersEvidenceLedger();
        ledger.ObserveSnapshot(
            0,
            0,
            [new Position(0, 0), .. ZoneTiles],
            [],
            []);
        ledger.ObserveSnapshot(
            0,
            1,
            [new Position(0, 0), .. ZoneTiles],
            [Enemy(EnemyLife0, new Position(1, 0))],
            [Movement("move-before-reset", 0, EnemyLife0,
                new Position(0, 0), new Position(1, 0))]);
        ledger.Reset();

        OrdersEvidenceLedger.EvidenceRead entries = ledger.Read(
            Condition("observed-enemy-entries-in-zone", since: "match-start"),
            1,
            0,
            InZone,
            ZoneTiles);

        Assert.Equal(0, entries.Actual);
    }

    [Fact]
    public void FriendlyPickupHistoryCountsOnlyCompletedPickupEvents()
    {
        var ledger = new OrdersEvidenceLedger();
        var core = new GenericActorContext.ArcRelayCoreId("centre", 1);
        ActorIdentity friendly = new(0, 2, 0);
        ledger.ObserveSnapshot(0, 0, ZoneTiles, [], []);
        ledger.ObserveSnapshot(
            0,
            1,
            ZoneTiles,
            [],
            [ArcEvent(
                "pickup",
                0,
                new GenericActorContext.ArcRelayEvent.CorePickedUp(
                    core, friendly, new Position(1, 0), 2))]);

        OrdersEvidenceLedger.EvidenceRead read = ledger.Read(
            Condition("friendly-core-pickups", windowTicks: 10),
            1,
            0,
            _ => false,
            []);

        Assert.Equal(1, read.Actual);
        Assert.Contains("1-friendly-core-pickups@0..0", read.Trace);
    }

    [Theory]
    [InlineData(Direction.North, 1, -1, Direction.North)]
    [InlineData(Direction.East, 1, -1, Direction.East)]
    [InlineData(Direction.South, 1, -1, Direction.East)]
    [InlineData(Direction.West, 1, -1, Direction.North)]
    [InlineData(Direction.West, 0, 0, Direction.West)]
    public void DestinationFacingPreservesAValidQuadrantAndTurnsMinimally(
        Direction current,
        int dx,
        int dy,
        Direction expected)
    {
        Direction actual = ArenaBasics.FacingTowardAnchor(
            current,
            new Position(5, 5),
            new Position(5 + dx, 5 + dy),
            mirrored: false);

        Assert.Equal(expected, actual);
    }

    private static bool InZone(Position position) => position.X == 1;

    private static OrdersPackage.Condition Condition(
        string fact,
        int? windowTicks = null,
        string? since = null) => new(
            fact,
            fact,
            "at-least",
            1,
            null,
            "gate",
            null,
            null,
            windowTicks,
            since,
            null,
            null);

    private static GenericActorContext.ObservedEnemyState Enemy(
        ActorIdentity actor,
        Position position) => new(
            actor,
            "body",
            position,
            Direction.West,
            10,
            null,
            [Observer]);

    private static GenericActorContext.ObservedEvent Movement(
        string handle,
        int tick,
        ActorIdentity actor,
        Position from,
        Position to) => new(
            handle,
            tick,
            0,
            GenericActorContext.EventKind.Movement,
            new GenericActorContext.EventPayload.Movement(
                actor,
                new GenericActorActionResolution.ResolvedAction("move", 1, []),
                from,
                to,
                Direction.East),
            [Observer]);

    private static GenericActorContext.ObservedEvent Destruction(
        string handle,
        int tick,
        ActorIdentity actor,
        Position position) => new(
            handle,
            tick,
            0,
            GenericActorContext.EventKind.Destruction,
            new GenericActorContext.EventPayload.Destruction(
                actor,
                null,
                null,
                null,
                0,
                "body",
                position),
            [Observer]);

    private static GenericActorContext.ObservedEvent ArcEvent(
        string handle,
        int tick,
        GenericActorContext.ArcRelayEvent fact) => new(
            handle,
            tick,
            0,
            GenericActorContext.EventKind.ArcRelay,
            new GenericActorContext.EventPayload.ArcRelay(fact),
            [Observer]);
}
