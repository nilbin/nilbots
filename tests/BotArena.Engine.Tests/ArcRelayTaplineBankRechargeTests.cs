using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Tapline-8 replaces recurring global supply beats with one source-local
/// recharge that starts only when that source Well's active Core is banked.
/// These tests drive the mode seam directly so no mind can hide a timing bug.
/// </summary>
public sealed class ArcRelayTaplineBankRechargeTests
{
    private const int RechargeTicks = 46;
    private const string HistoricalRulesFingerprint =
        "7a237424735e352291816077591eb6e4c93ba51dfea46979accd84c16e9a5b2a";
    private const string HistoricalMapFingerprint =
        "e536d04deff7e53296d10f2b93c38741a6a3e465a03e779a84336ba0ae1df519";

    [Fact]
    public void Tapline15KeepsTapline8RechargeRulesAndBroadloomIsCurrent()
    {
        ActorResolvedMatchDefinition historical = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline7);
        ActorResolvedMatchDefinition recharge = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline8);
        ActorResolvedMatchDefinition prior = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline14);
        ActorResolvedMatchDefinition current = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline15);
        var historicalMode = Assert.IsType<ArcRelayGameModeDefinition>(
            historical.Rules.GameMode);
        var currentMode = Assert.IsType<ArcRelayGameModeDefinition>(
            current.Rules.GameMode);

        Assert.Same(ArcRelayLoopProfile.Broadloom3, ArcRelayLoopProfile.Current);
        Assert.Equal("broadloom-3", ArcRelayLoopProfile.Current.Id);
        Assert.Equal("arc-relay-tapline-15", current.Rules.RulesetId);
        Assert.Equal("arc-relay-tapline-03", current.Map.Id);
        Assert.Equal(600, current.Rules.Limits.MaxTicks);
        Assert.Equal(RechargeTicks,
            currentMode.BankTriggeredWellRechargeTicks);
        Assert.Equal((1, 2, 4), (
            currentMode.OwnWellCoreCharge,
            currentMode.CentreWellCoreCharge,
            currentMode.EnemyWellCoreCharge));
        Assert.True(currentMode.ReactiveImpactFacing);
        Assert.Equal(50, currentMode.VeterancyDeathRetentionPercent);
        Assert.Equal(
            ActorCollisionDefinition.AlliedProjectileContactKind
                .BlockWithoutDamage,
            current.Rules.Collisions.AlliedProjectileContact);
        Assert.Equal(0, Assert.IsType<ArcRelayGameModeDefinition>(
            prior.Rules.GameMode).VeterancyDeathRetentionPercent);
        Assert.Equal(
            ActorCollisionDefinition.AlliedProjectileContactKind.PassThrough,
            prior.Rules.Collisions.AlliedProjectileContact);
        Assert.All(currentMode.Wells, well => Assert.Equal(
            well.FirstBirthTick,
            well.FinalBirthTick));

        Assert.Equal(
            HistoricalRulesFingerprint,
            ActorContractFingerprint.ComputeRules(historical.Rules));
        Assert.Equal(
            HistoricalMapFingerprint,
            ActorContractFingerprint.ComputeMap(historical.Map));
        Assert.Equal(
            [(50, 65, 570), (50, 65, 570), (50, 65, 570)],
            historicalMode.Wells.Select(well => (
                well.FirstBirthTick,
                well.CadenceTicks,
                well.FinalBirthTick)));
        Assert.Equal(0, historicalMode.BankTriggeredWellRechargeTicks);
        Assert.True(historical.Map.TileRows.SequenceEqual(recharge.Map.TileRows));
        Assert.NotEqual(
            HistoricalRulesFingerprint,
            ActorContractFingerprint.ComputeRules(current.Rules));

        Assert.Equal(
            RechargeTicks,
            Assert.IsType<ArcRelayGameModeDefinition>(
                recharge.Rules.GameMode).BankTriggeredWellRechargeTicks);
        Assert.False(recharge.Map.TileRows.SequenceEqual(current.Map.TileRows));

        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            current);
        Assert.Contains(
            "\"bankTriggeredWellRechargeTicks\":46",
            canonical,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"reactiveImpactFacing\":true",
            canonical,
            StringComparison.Ordinal);
        var parsedMode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                ActorCanonicalContractReader.Parse(canonical).Rules.GameMode);
        Assert.Equal(RechargeTicks,
            parsedMode.BankTriggeredWellRechargeTicks);
        Assert.True(parsedMode.ReactiveImpactFacing);
    }

    [Fact]
    public void InitialBirthsMatchTapline7ForTheSameSeed()
    {
        const ulong seed = 20_260_816UL;
        Assert.Equal(
            FirstBirths(ArcRelayLoopProfile.Tapline7, seed),
            FirstBirths(ArcRelayLoopProfile.Tapline8, seed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BankingIntoEitherReactorStartsOneExactPublicRecharge(
        int scoringTeamId)
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity carrier = ActorIdentity.FromTeamUnitLife(
            scoringTeamId, 0, 0);
        int pickupTick = PickUpInitialCore(driver, carrier, "centre");
        ArcRelayWellState active = Well(driver, "centre");
        AssertActiveWithoutCountdown(active);

        int bankTick = pickupTick + 1;
        Position reactor = State(driver).Reactors.Single(value =>
            value.TeamId == scoringTeamId).Position;
        GenericActorModeTickResult banked = driver.ApplyJointTick(
            World(Life(carrier, reactor)),
            new GenericActorModeTickInput(bankTick, [], []));
        ArcRelayEvent[] bankFacts = Facts(banked.ModeEvents);
        Assert.Single(bankFacts.OfType<ArcRelayEvent.CoreBanked>());
        ArcRelayEvent.WellChanged rechargeStarted = Assert.Single(
            bankFacts.OfType<ArcRelayEvent.WellChanged>(),
            value => value.WellId == "centre");
        int dueTick = bankTick + RechargeTicks;
        Assert.False(rechargeStarted.PendingCharge);
        Assert.Equal(dueTick, rechargeStarted.RearmCompletesAtTick);
        Assert.Null(rechargeStarted.OutstandingCoreId);

        ArcRelayWellState recharging = Well(driver, "centre");
        Assert.Equal(dueTick, recharging.NextScheduledBirthTick);
        Assert.Equal(dueTick, recharging.RearmCompletesAtTick);
        Assert.False(recharging.PendingCharge);
        Assert.Null(recharging.OutstandingCoreId);

        for (int tick = bankTick + 1; tick < dueTick; tick++)
        {
            GenericActorModeTickResult waiting = driver.PrepareTick(
                tick, World());
            Assert.DoesNotContain(
                Facts(waiting.ModeEvents).OfType<ArcRelayEvent.CoreBorn>(),
                value => value.CoreId.SourceWellId == "centre");
            Assert.Equal(dueTick, Well(driver, "centre")
                .NextScheduledBirthTick);
        }

        GenericActorModeTickResult completed = driver.PrepareTick(
            dueTick, World());
        ArcRelayEvent.CoreBorn born = Assert.Single(
            Facts(completed.ModeEvents).OfType<ArcRelayEvent.CoreBorn>(),
            value => value.CoreId.SourceWellId == "centre");
        Assert.Equal(1, born.CoreId.SourceOrdinal);
        AssertActiveWithoutCountdown(Well(driver, "centre"));

        // Re-preparing the due tick and crossing the old 65-tick beat cannot
        // birth a duplicate while this new Core remains active.
        Assert.Empty(Facts(driver.PrepareTick(dueTick, World()).ModeEvents)
            .OfType<ArcRelayEvent.CoreBorn>());
        for (int tick = dueTick + 1; tick <= dueTick + 70; tick++)
            driver.PrepareTick(tick, World());
        Assert.Single(
            State(driver).VisibleCores,
            value => value.CoreId.SourceWellId == "centre");
        AssertActiveWithoutCountdown(Well(driver, "centre"));
    }

    [Fact]
    public void CarryTossDropAndStealNeverStartTheRecharge()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity carrier = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        ActorIdentity thief = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        int pickupTick = PickUpInitialCore(driver, carrier, "centre");
        Position source = Well(driver, "centre").Position;

        for (int tick = pickupTick + 1; tick <= pickupTick + 70; tick++)
        {
            driver.PrepareTick(tick, World(Life(carrier, source)));
            AssertActiveWithoutCountdown(Well(driver, "centre"));
        }

        int launchTick = pickupTick + 71;
        Position requestedLanding = source.Offset(1, 0);
        Assert.NotEmpty(driver.LaunchArcToss(
            launchTick,
            carrier,
            requestedLanding,
            completesAtTick: launchTick + 2));
        AssertActiveWithoutCountdown(Well(driver, "centre"));

        int landingTick = launchTick + 2;
        driver.LandArcToss(
            landingTick,
            carrier,
            requestedLanding,
            World());
        AssertActiveWithoutCountdown(Well(driver, "centre"));
        Position landed = State(driver).VisibleCores.Single(value =>
            value.CoreId.SourceWellId == "centre").Position;

        driver.PrepareTick(landingTick + 1, World(Life(thief, landed)));
        Assert.Equal(
            thief,
            State(driver).VisibleCores.Single(value =>
                value.CoreId.SourceWellId == "centre").CarrierActorId);
        AssertActiveWithoutCountdown(Well(driver, "centre"));

        Assert.True(driver.TryDrop(landingTick + 3, thief, out _));
        AssertActiveWithoutCountdown(Well(driver, "centre"));
    }

    private static Dictionary<string, int> FirstBirths(
        ArcRelayLoopProfile profile,
        ulong seed)
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create(loopProfile: profile), seed);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int tick = 0; tick <= 80 && result.Count < 3; tick++)
        {
            foreach (ArcRelayEvent.CoreBorn born in Facts(
                         driver.PrepareTick(tick, World()).ModeEvents)
                     .OfType<ArcRelayEvent.CoreBorn>())
            {
                result.Add(born.CoreId.SourceWellId, tick);
            }
        }
        Assert.Equal(3, result.Count);
        return result;
    }

    private static int PickUpInitialCore(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity carrier,
        string wellId)
    {
        Position position = Well(driver, wellId).Position;
        for (int tick = 0; tick <= 80; tick++)
        {
            driver.PrepareTick(tick, World(Life(carrier, position)));
            if (State(driver).VisibleCores.Any(value =>
                    value.CoreId.SourceWellId == wellId
                    && value.CarrierActorId == carrier))
            {
                return tick;
            }
        }
        throw new Xunit.Sdk.XunitException(
            $"The initial {wellId} Core did not open by tick 80.");
    }

    private static void AssertActiveWithoutCountdown(ArcRelayWellState well)
    {
        Assert.NotNull(well.OutstandingCoreId);
        Assert.Null(well.NextScheduledBirthTick);
        Assert.Null(well.RearmCompletesAtTick);
        Assert.False(well.PendingCharge);
    }

    private static ArcRelayActorMatchModeDriver Driver() => new(
        ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline8),
        matchSeed: 20_260_816UL);

    private static ArcRelayWellState Well(
        ArcRelayActorMatchModeDriver driver,
        string wellId) => State(driver).Wells.Single(value =>
            value.WellId == wellId);

    private static GenericActorRuntimeObservation.ModeObservationState.ArcRelay
        State(ArcRelayActorMatchModeDriver driver) =>
        Assert.IsType<GenericActorModeState.ArcRelay>(driver.State).State;

    private static ArcRelayEvent[] Facts(
        IEnumerable<GenericActorModeEvent> events) =>
        events.Select(value => Assert.IsType<
                GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                value.Payload).Fact)
            .ToArray();

    private static GenericActorModeActiveLife Life(
        ActorIdentity actor,
        Position position) =>
        new(actor, "arc-body-relay", position, health: 40);

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
