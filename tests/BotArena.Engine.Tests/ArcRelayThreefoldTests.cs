using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Threefold Pulse prototype (owner brief 2026-08-05): a Pulse requires one
/// banked Core from each Well origin; duplicate-origin Cores stay physical
/// and contestable; sockets reset on the Pulse. Minted BESIDE -03 so every
/// prior ruleset's bytes never move.
/// </summary>
public sealed class ArcRelayThreefoldTests
{
    private static readonly ActorIdentity CentreRunner =
        ActorIdentity.FromTeamUnitLife(0, 0, 0);
    private static readonly ActorIdentity NorthRunner =
        ActorIdentity.FromTeamUnitLife(0, 1, 0);
    private static readonly ActorIdentity SouthRunner =
        ActorIdentity.FromTeamUnitLife(0, 2, 0);
    private static readonly Position CentreWell = new(15, 11);
    private static readonly Position NorthWell = new(15, 4);
    private static readonly Position SouthWell = new(15, 18);

    [Fact]
    public void ThreefoldMintsANewFingerprintAndLeavesPriorRulesAlone()
    {
        string[] prior =
        [
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat2)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat3)),
        ];
        string threefold = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ThreefoldPulse));
        Assert.DoesNotContain(threefold, prior);
        // Re-deriving every prior ruleset beside the mint reproduces the
        // exact bytes.
        Assert.Equal(
            prior[2],
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat3)));
        string json = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ThreefoldPulse));
        Assert.Contains("\"threefoldSockets\":true", json);
        Assert.DoesNotContain(
            "threefoldSockets",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat3)));
    }

    [Fact]
    public void BankFillsTheOriginSocketAndPublishesIt()
    {
        var driver = Driver();
        BirthAndCarry(driver, CentreRunner, CentreWell, 19, 31);
        BankAt(driver, CentreRunner, 40);

        ArcRelayReactorState reactor = Reactor(driver, teamId: 0);
        Assert.Equal(1, reactor.ChargePips);
        Assert.Equal(["centre"], reactor.FilledSocketWellIds.ToArray());
    }

    [Fact]
    public void DuplicateOriginCoreIsNotConsumedAndStaysCarried()
    {
        var driver = Driver();
        BirthAndCarry(driver, CentreRunner, CentreWell, 19, 31);
        BankAt(driver, CentreRunner, 40);
        // The centre Well rearms after its banked Core's pending charge and
        // births again; the second centre Core reaches the reactor while the
        // centre socket is already filled.
        BirthAndCarry(driver, NorthRunner, CentreWell, 41, 120);
        BankAt(driver, NorthRunner, 130);

        ArcRelayReactorState reactor = Reactor(driver, teamId: 0);
        Assert.Equal(1, reactor.ChargePips);
        Assert.Equal(["centre"], reactor.FilledSocketWellIds.ToArray());
        // The duplicate stays physical, carried, and contestable.
        ArcRelayCoreState duplicate = State(driver).VisibleCores.Single(
            core => core.CarrierActorId == NorthRunner);
        Assert.Equal("centre", duplicate.CoreId.SourceWellId);
        Assert.Equal(NorthRunner, duplicate.CarrierActorId);
    }

    [Fact]
    public void ThirdDistinctOriginPulsesAndResetsTheSockets()
    {
        var driver = Driver();
        BirthAndCarry(driver, CentreRunner, CentreWell, 19, 31);
        BankAt(driver, CentreRunner, 40);
        BirthAndCarry(driver, NorthRunner, NorthWell, 44, 56);
        BankAt(driver, NorthRunner, 60);
        BirthAndCarry(driver, SouthRunner, SouthWell, 69, 81);

        int opposingBefore = Reactor(driver, teamId: 1).IntegritySegments;
        BankAt(driver, SouthRunner, 90);

        ArcRelayReactorState reactor = Reactor(driver, teamId: 0);
        Assert.Equal(0, reactor.ChargePips);
        Assert.Empty(reactor.FilledSocketWellIds);
        Assert.Equal(
            opposingBefore - 1,
            Reactor(driver, teamId: 1).IntegritySegments);
        Assert.Equal(0, State(driver).LatestPulseTeamId);
    }

    private static ArcRelayActorMatchModeDriver Driver() =>
        new(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.ThreefoldPulse),
            matchSeed: 0);

    /// <summary>
    /// Walks PrepareTick across the jittered birth window with the runner
    /// standing on the Well, so the birth's tick-start pickup lands on it.
    /// </summary>
    private static void BirthAndCarry(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity runner,
        Position well,
        int firstTick,
        int lastTick)
    {
        for (int tick = firstTick; tick <= lastTick; tick++)
            driver.PrepareTick(tick, World(Life(runner, well)));
        Assert.Contains(
            State(driver).VisibleCores,
            core => core.CarrierActorId == runner);
    }

    private static void BankAt(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity runner,
        int tick)
    {
        Position reactor = Reactor(driver, runner.TeamId).Position;
        driver.ApplyJointTick(
            World(Life(runner, reactor)),
            new GenericActorModeTickInput(tick, [], []));
    }

    private static ArcRelayReactorState Reactor(
        ArcRelayActorMatchModeDriver driver,
        int teamId) =>
        State(driver).Reactors.Single(value => value.TeamId == teamId);

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
