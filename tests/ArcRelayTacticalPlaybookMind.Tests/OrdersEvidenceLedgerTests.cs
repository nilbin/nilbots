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

    [Fact]
    public void TickStartCorePickupIsRetainedWhenObservationDoesNotRepeatIt()
    {
        var ledger = new OrdersEvidenceLedger();
        var core = new GenericActorContext.ArcRelayCoreId("south", 0);
        ActorIdentity relay = new(0, 1, 0);
        GenericActorContext.ObservedEvent pickup = ArcEvent(
            "tick-start-pickup",
            0,
            new GenericActorContext.ArcRelayEvent.CorePickedUp(
                core, relay, new Position(1, 0), 2));

        ledger.ObserveSnapshot(0, 0, ZoneTiles, [], [pickup]);
        Assert.Equal(0, ledger.ReadCore(
            Condition("friendly-core-acquisitions", since: "node-entry",
                roles: ["relay"]),
            decisionTick: 0,
            orderTicks: 0,
            _ => "relay").Actual);

        // Real tick-start events are not repeated in the next observation.
        // The prior observation must nevertheless become completed evidence.
        ledger.ObserveSnapshot(0, 1, ZoneTiles, [], []);
        Assert.Equal(1, ledger.ReadCore(
            Condition("friendly-core-acquisitions", since: "node-entry",
                roles: ["relay"]),
            decisionTick: 1,
            orderTicks: 1,
            _ => "relay").Actual);
    }

    [Fact]
    public void ScopedCustodyEvidenceFollowsOneCoreAcrossRoleTransferAndBank()
    {
        var ledger = new OrdersEvidenceLedger();
        var wanted = new GenericActorContext.ArcRelayCoreId("enemy", 1);
        var unrelated = new GenericActorContext.ArcRelayCoreId("enemy", 2);
        ActorIdentity kestrel = new(0, 2, 0);
        ActorIdentity relay = new(0, 3, 0);

        ledger.ObserveSnapshot(0, 0, ZoneTiles, [], []);
        ledger.ObserveSnapshot(0, 1, ZoneTiles, [],
        [
            ArcEvent("pickup-wanted", 0,
                new GenericActorContext.ArcRelayEvent.CorePickedUp(
                    wanted, kestrel, new Position(1, 0), 2)),
        ]);
        ledger.ObserveSnapshot(0, 2, ZoneTiles, [],
        [
            ArcEvent("handoff-wanted", 1,
                new GenericActorContext.ArcRelayEvent.CoreHandedOff(
                    wanted, kestrel, relay, new Position(1, 0), 3)),
            ArcEvent("bank-unrelated", 1,
                new GenericActorContext.ArcRelayEvent.CoreBanked(
                    unrelated, relay, 0, new Position(0, 0), 4)
                    { ChargeAwarded = 4 }),
        ]);
        ledger.ObserveSnapshot(0, 3, ZoneTiles, [],
        [
            ArcEvent("bank-wanted", 2,
                new GenericActorContext.ArcRelayEvent.CoreBanked(
                    wanted, relay, 0, new Position(0, 0), 8)
                    { ChargeAwarded = 4 }),
        ]);

        string? RoleForUnit(int unitId) => unitId switch
        {
            2 => "runner",
            3 => "relay",
            _ => null,
        };
        int Value(GenericActorContext.ArcRelayCoreId _) => 4;
        bool EnemySource(GenericActorContext.ArcRelayCoreId core) =>
            core.SourceWellId == "enemy";

        OrdersEvidenceLedger.EvidenceRead acquisitions = ledger.ReadCore(
            Condition("friendly-core-acquisitions", since: "node-entry",
                roles: ["runner", "relay"]),
            decisionTick: 3,
            orderTicks: 3,
            RoleForUnit,
            Value,
            EnemySource);
        OrdersEvidenceLedger.EvidenceRead banks = ledger.ReadCore(
            Condition("friendly-core-banks", since: "node-entry",
                roles: ["runner", "relay"], correlateAcquisition: true,
                coreBankValue: new("equals", 4)),
            decisionTick: 3,
            orderTicks: 3,
            RoleForUnit,
            Value,
            EnemySource);

        Assert.Equal(2, acquisitions.Actual);
        Assert.Equal(1, banks.Actual);
        Assert.Contains("1-bank-events@0..2", banks.Trace);
    }

    [Fact]
    public void ScopedDropEvidenceRejectsOtherRolesAndOtherSources()
    {
        var ledger = new OrdersEvidenceLedger();
        var own = new GenericActorContext.ArcRelayCoreId("own", 1);
        var enemy = new GenericActorContext.ArcRelayCoreId("enemy", 1);
        ActorIdentity runner = new(0, 2, 0);
        ActorIdentity outsider = new(0, 7, 0);

        ledger.ObserveSnapshot(0, 0, ZoneTiles, [], []);
        ledger.ObserveSnapshot(0, 1, ZoneTiles, [],
        [
            ArcEvent("drop-own", 0,
                new GenericActorContext.ArcRelayEvent.CoreDropped(
                    own, runner, new Position(1, 0), 2, "voluntary")),
            ArcEvent("drop-outsider", 0,
                new GenericActorContext.ArcRelayEvent.CoreDropped(
                    enemy, outsider, new Position(1, 0), 2, "voluntary")),
        ]);

        OrdersEvidenceLedger.EvidenceRead drops = ledger.ReadCore(
            Condition("friendly-core-drops", since: "node-entry",
                roles: ["runner"]),
            decisionTick: 1,
            orderTicks: 1,
            unitId => unitId == 2 ? "runner" : "outsider",
            _ => 1,
            core => core.SourceWellId == "own");

        Assert.Equal(1, drops.Actual);
    }

    [Fact]
    public void ArcTossDepartureIsNotALossButAnUncaughtLandingIs()
    {
        var ledger = new OrdersEvidenceLedger();
        var core = new GenericActorContext.ArcRelayCoreId("enemy", 4);
        ActorIdentity runner = new(0, 2, 0);

        ledger.ObserveSnapshot(0, 0, ZoneTiles, [], []);
        ledger.ObserveSnapshot(0, 1, ZoneTiles, [],
        [
            ArcEvent("pickup", 0,
                new GenericActorContext.ArcRelayEvent.CorePickedUp(
                    core, runner, new Position(1, 0), 1)),
            ArcEvent("toss-departure", 0,
                new GenericActorContext.ArcRelayEvent.CoreDropped(
                    core, runner, new Position(1, 0), 1,
                    "signature-departure")),
        ]);

        OrdersEvidenceLedger.EvidenceRead beforeLanding = ledger.ReadCore(
            Condition("friendly-core-losses", since: "node-entry",
                roles: ["runner"], correlateAcquisition: true),
            decisionTick: 1,
            orderTicks: 1,
            unitId => unitId == 2 ? "runner" : null);
        Assert.Equal(0, beforeLanding.Actual);

        ledger.ObserveSnapshot(0, 2, ZoneTiles, [],
        [
            ArcEvent("uncaught-landing", 1,
                new GenericActorContext.ArcRelayEvent.CoreDropped(
                    core, runner, new Position(2, 0), 2,
                    "arc-toss-landing")),
        ]);
        OrdersEvidenceLedger.EvidenceRead afterLanding = ledger.ReadCore(
            Condition("friendly-core-losses", since: "node-entry",
                roles: ["runner"], correlateAcquisition: true),
            decisionTick: 2,
            orderTicks: 2,
            unitId => unitId == 2 ? "runner" : null);
        Assert.Equal(1, afterLanding.Actual);
        Assert.Contains("1-loss-events@0..1", afterLanding.Trace);
        OrdersEvidenceLedger.EvidenceRead resolved = ledger.ReadCore(
            Condition("friendly-core-resolutions", since: "node-entry",
                roles: ["runner"], correlateAcquisition: true,
                outcomes: ["bank", "loss"]),
            decisionTick: 2,
            orderTicks: 2,
            unitId => unitId == 2 ? "runner" : null);
        Assert.Equal(1, resolved.Actual);
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
        string? since = null,
        string[]? roles = null,
        bool correlateAcquisition = false,
        OrdersPackage.CoreBankValueFilter? coreBankValue = null,
        string[]? outcomes = null) => new(
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
            coreBankValue,
            roles,
            correlateAcquisition,
            outcomes);

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
