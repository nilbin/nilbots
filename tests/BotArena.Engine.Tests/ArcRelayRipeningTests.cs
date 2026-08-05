using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Ripening Cores prototype (owner direction 2026-08-05, depth memo #1):
/// the charge-value primitive with its registered control arm, minted
/// BESIDE -03 so every prior ruleset's bytes never move.
/// </summary>
public sealed class ArcRelayRipeningTests
{
    private static readonly ActorIdentity Runner =
        ActorIdentity.FromTeamUnitLife(0, 0, 0);
    private static readonly Position CentreWell = new(15, 11);

    [Fact]
    public void BothMintsAreNewFingerprintsAndPriorRulesAreUnchanged()
    {
        string three = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat3));
        string control = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ChargeValueControl));
        string ripening = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.RipeningCores));
        Assert.NotEqual(three, control);
        Assert.NotEqual(three, ripening);
        Assert.NotEqual(control, ripening);
        Assert.Equal(
            three,
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat3)));

        string controlJson = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ChargeValueControl));
        Assert.Contains("\"coreBaseValue\":2", controlJson);
        Assert.Contains("\"coresPerPulse\":6", controlJson);
        Assert.DoesNotContain("ripenIntervalTicks", controlJson);
        string ripeningJson = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.RipeningCores));
        Assert.Contains("\"ripenIntervalTicks\":45", ripeningJson);
        Assert.Contains("\"ripenMaxValue\":4", ripeningJson);
        Assert.Contains("\"ripenResumeTicks\":20", ripeningJson);
        Assert.DoesNotContain(
            "coreBaseValue",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat3)));
    }

    [Fact]
    public void LooseCoresRipenOnTheIntervalAndCapAtTheMaximum()
    {
        var driver = Driver(ArcRelayLoopProfile.RipeningCores);
        // Walk far past three ripen intervals with the Core untouched.
        for (int tick = 19; tick <= 200; tick++)
            driver.PrepareTick(tick, World());
        ArcRelayCoreState core = State(driver).VisibleCores.Single(
            value => value.CoreId.SourceWellId == "centre");
        Assert.Equal(4, core.ChargeValue);
    }

    [Fact]
    public void PickupFreezesTheValue()
    {
        var driver = Driver(ArcRelayLoopProfile.RipeningCores);
        // Let the centre Core ripen one step (45 loose ticks past birth),
        // then stand a runner on it.
        for (int tick = 19; tick <= 90; tick++)
            driver.PrepareTick(tick, World());
        for (int tick = 91; tick <= 200; tick++)
            driver.PrepareTick(tick, World(Life(Runner, CentreWell)));
        ArcRelayCoreState core = State(driver).VisibleCores.Single(
            value => value.CarrierActorId == Runner);
        Assert.Equal(3, core.ChargeValue);
    }

    [Fact]
    public void BankAddsTheValueAndThePulseCarriesTheRemainder()
    {
        var driver = Driver(ArcRelayLoopProfile.RipeningCores);
        for (int tick = 19; tick <= 90; tick++)
            driver.PrepareTick(tick, World());
        for (int tick = 91; tick <= 120; tick++)
            driver.PrepareTick(tick, World(Life(Runner, CentreWell)));
        Position reactor = State(driver).Reactors
            .Single(value => value.TeamId == 0).Position;
        driver.ApplyJointTick(
            World(Life(Runner, reactor)),
            new GenericActorModeTickInput(130, [], []));
        Assert.Equal(
            3,
            State(driver).Reactors.Single(value => value.TeamId == 0)
                .ChargePips);
    }

    [Fact]
    public void BirthAndRipeningEmitTheirChargeFacts()
    {
        var driver = Driver(ArcRelayLoopProfile.RipeningCores);
        var facts = new List<ArcRelayEvent>();
        for (int tick = 19; tick <= 90; tick++)
        {
            facts.AddRange(driver.PrepareTick(tick, World()).ModeEvents
                .Select(value => value.Payload)
                .OfType<GenericActorRuntimeObservation.EventPayload.ArcRelay>()
                .Select(value => value.Fact));
        }

        ArcRelayEvent.CoreBorn born = facts.OfType<ArcRelayEvent.CoreBorn>()
            .Single(value => value.CoreId.SourceWellId == "centre");
        Assert.Equal(2, born.ChargeValue);
        // Exactly one ripen step fits before tick 90 (45 loose ticks past
        // birth), and its fact carries the post-step value.
        ArcRelayEvent.CoreRipened ripen = Assert.Single(
            facts.OfType<ArcRelayEvent.CoreRipened>(),
            value => value.CoreId.SourceWellId == "centre");
        Assert.Equal(3, ripen.Value);
        Assert.Equal(CentreWell, ripen.Position);
    }

    private static ArcRelayActorMatchModeDriver Driver(
        ArcRelayLoopProfile profile) =>
        new(
            ArcRelayH0Definition.Create(loopProfile: profile),
            matchSeed: 0);

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
