using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The Core-owned clock is the mechanical guardrail behind relay play. These
/// tests deliberately drive the mode seam directly so no cooperative mind or
/// action-mask accident can make an illegal chain merely unlikely.
/// </summary>
public sealed class ArcRelayCoreInvariantTests
{
    private static readonly ActorIdentity Carrier =
        ActorIdentity.FromTeamUnitLife(0, 0, 0);
    private static readonly ActorIdentity Receiver =
        ActorIdentity.FromTeamUnitLife(0, 1, 0);
    private static readonly ActorIdentity NextReceiver =
        ActorIdentity.FromTeamUnitLife(0, 2, 0);
    private static readonly Position CentreWell = new(15, 11);

    [Fact]
    public void BirthSettlesPickupBeforeObservationAndStartsRecovery()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create());
        GenericActorModeTickResult tickStart = driver.PrepareTick(
            25,
            World(Life(Carrier, CentreWell)));

        ArcRelayEvent[] facts = Facts(tickStart.ModeEvents);
        Assert.Collection(
            facts,
            value => Assert.IsType<ArcRelayEvent.CoreBorn>(value),
            value => Assert.IsType<ArcRelayEvent.WellChanged>(value),
            value => Assert.IsType<ArcRelayEvent.CorePickedUp>(value));
        ArcRelayCoreState core = State(driver).VisibleCores.Single();
        Assert.Equal(Carrier, core.CarrierActorId);
        Assert.Equal(CentreWell, core.Position);
        Assert.Equal(27, core.NextRelocationTick);
    }

    [Fact]
    public void PassingCannotEraseTravelRecoveryOrChainOnConsecutiveTicks()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create());
        Position receiverPosition = CentreWell.Offset(-1, 0);
        Position nextPosition = CentreWell.Offset(-2, 0);
        GenericActorModeWorldView world = World(
            Life(Carrier, CentreWell),
            Life(Receiver, receiverPosition),
            Life(NextReceiver, nextPosition));
        driver.PrepareTick(25, world);

        Assert.False(driver.TryHandoff(
            26,
            Carrier,
            Receiver,
            CentreWell,
            receiverPosition,
            out _));
        Assert.True(driver.TryHandoff(
            27,
            Carrier,
            Receiver,
            CentreWell,
            receiverPosition,
            out GenericActorModeEvent? first));
        var firstFact = Assert.IsType<ArcRelayEvent.CoreHandedOff>(
            Assert.IsType<GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                first!.Payload).Fact);
        Assert.Equal(29, firstFact.NextRelocationTick);

        Assert.False(driver.TryHandoff(
            28,
            Receiver,
            NextReceiver,
            receiverPosition,
            nextPosition,
            out _));
        Assert.True(driver.TryHandoff(
            29,
            Receiver,
            NextReceiver,
            receiverPosition,
            nextPosition,
            out GenericActorModeEvent? second));
        var secondFact = Assert.IsType<ArcRelayEvent.CoreHandedOff>(
            Assert.IsType<GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                second!.Payload).Fact);
        Assert.Equal(31, secondFact.NextRelocationTick);
        Assert.Equal(NextReceiver, State(driver).VisibleCores.Single()
            .CarrierActorId);
    }

    [Fact]
    public void CarrierRelocationAvailabilityFollowsTheObjectOwnedClock()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create());
        Position receiverPosition = CentreWell.Offset(-1, 0);
        driver.PrepareTick(
            25,
            World(
                Life(Carrier, CentreWell),
                Life(Receiver, receiverPosition)));

        Assert.False(driver.CanCarrierRelocate(Carrier, 26));
        Assert.True(driver.CanCarrierRelocate(Carrier, 27));
        Assert.True(driver.TryHandoff(
            27,
            Carrier,
            Receiver,
            CentreWell,
            receiverPosition,
            out _));
        Assert.False(driver.CanCarrierRelocate(Receiver, 28));
        Assert.True(driver.CanCarrierRelocate(Receiver, 29));
    }

    [Fact]
    public void DestructionDropPrecedesBankAndCannotShortenRecovery()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create());
        driver.PrepareTick(25, World(Life(Carrier, CentreWell)));
        Position reactor = State(driver).Reactors.Single(value =>
            value.TeamId == Carrier.TeamId).Position;
        driver.ResolveForcedMovement(
            27,
            [Carrier],
            World(Life(Carrier, reactor)));

        GenericActorModeTickResult result = driver.ApplyJointTick(
            World(),
            new GenericActorModeTickInput(
                27,
                damageContacts: [],
                [new FrontlineScrapDestruction(Carrier, reactor)]));

        ArcRelayEvent[] facts = Facts(result.ModeEvents);
        ArcRelayEvent.CoreDropped drop = Assert.Single(
            facts.OfType<ArcRelayEvent.CoreDropped>());
        Assert.Equal(ArcRelayEvent.CoreDropKind.Destruction, drop.Kind);
        Assert.Equal(29, drop.NextRelocationTick);
        Assert.Empty(facts.OfType<ArcRelayEvent.CoreBanked>());
        ArcRelayCoreState core = State(driver).VisibleCores.Single();
        Assert.Null(core.CarrierActorId);
        Assert.Equal(reactor, core.Position);
        Assert.Equal(29, core.NextRelocationTick);
        Assert.Equal(0, State(driver).Reactors.Single(value =>
            value.TeamId == Carrier.TeamId).ChargePips);
    }

    [Fact]
    public void InFlightCoreCannotBePickedBackUpAtItsDepartureTile()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create());
        driver.PrepareTick(25, World(Life(Carrier, CentreWell)));

        Assert.NotEmpty(driver.LaunchArcToss(
            27,
            Carrier,
            CentreWell.Offset(5, 0),
            completesAtTick: 30));
        driver.PrepareTick(28, World(Life(Carrier, CentreWell)));

        ArcRelayCoreState core = State(driver).VisibleCores.Single();
        Assert.Equal(ArcRelayCoreState.CoreDisposition.InFlight,
            core.Disposition);
        Assert.Null(core.CarrierActorId);
        Assert.Equal(CentreWell.Offset(5, 0), core.FlightTarget);
        Assert.Equal(30, core.FlightCompletesAtTick);
    }

    [Fact]
    public void PendingChargeRemainsPublicThroughoutItsRearmRing()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create());
        driver.PrepareTick(25, World(Life(Carrier, CentreWell)));
        driver.PrepareTick(100, World(Life(Carrier, CentreWell)));
        Position reactor = State(driver).Reactors.Single(value =>
            value.TeamId == Carrier.TeamId).Position;
        driver.ResolveForcedMovement(
            102,
            [Carrier],
            World(Life(Carrier, reactor)));
        driver.ApplyJointTick(
            World(Life(Carrier, reactor)),
            new GenericActorModeTickInput(102, [], []));

        ArcRelayWellState rearming = State(driver).Wells.Single(value =>
            value.WellId == "centre");
        Assert.True(rearming.PendingCharge);
        Assert.Null(rearming.OutstandingCoreId);
        Assert.Equal(113, rearming.RearmCompletesAtTick);

        driver.PrepareTick(113, World());
        ArcRelayWellState rearmed = State(driver).Wells.Single(value =>
            value.WellId == "centre");
        Assert.False(rearmed.PendingCharge);
        Assert.NotNull(rearmed.OutstandingCoreId);
        Assert.Null(rearmed.RearmCompletesAtTick);
    }

    private static ArcRelayEvent[] Facts(
        IEnumerable<GenericActorModeEvent> events) =>
        events.Select(value => Assert.IsType<
                GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                value.Payload).Fact)
            .ToArray();

    private static GenericActorRuntimeObservation.ModeObservationState.ArcRelay
        State(ArcRelayActorMatchModeDriver driver) =>
        Assert.IsType<GenericActorModeState.ArcRelay>(driver.State).State;

    private static GenericActorModeActiveLife Life(
        ActorIdentity actor,
        Position position) =>
        new(actor, "arc-body-kestrel", position, health: 3);

    private static GenericActorModeWorldView World(
        params GenericActorModeActiveLife[] lives) =>
        new(
            new Dictionary<int, long>
            {
                [0] = lives.Where(value => value.ActorId.TeamId == 0)
                    .Sum(value => value.Health),
                [1] = lives.Where(value => value.ActorId.TeamId == 1)
                    .Sum(value => value.Health),
            },
            [0, 1],
            lives.ToImmutableArray());
}
