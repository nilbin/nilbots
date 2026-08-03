namespace BotArena.Cli.Tests;

public sealed class StandingStrategyMachineTests
{
    [Fact]
    public void MinimumTenureAndHysteresisGateATransition()
    {
        StandingStrategyMachine machine = Machine(
            Phase("assault", 2,
                Transition("occupy", 2, Condition("always"))),
            Phase("occupy"));

        Assert.False(machine.Advance(Snapshot(0), Evaluate));
        Assert.False(machine.Advance(Snapshot(1), Evaluate));
        Assert.False(machine.Advance(Snapshot(2), Evaluate));
        Assert.True(machine.Advance(Snapshot(3), Evaluate));
        Assert.Equal("occupy", machine.PhaseId);
        Assert.Equal(3, machine.EnteredTick);
    }

    [Fact]
    public void CasualtyCollapseMustPersistBeforeRegrouping()
    {
        StandingStrategyMachine machine = Machine(
            Phase("occupy", 0,
                Transition("regroup", 3,
                    Condition("live-friendlies", "at-most", 4))),
            Phase("regroup"));

        Assert.False(machine.Advance(Snapshot(30, live: 4), Evaluate));
        Assert.False(machine.Advance(Snapshot(31, live: 5), Evaluate));
        Assert.False(machine.Advance(Snapshot(32, live: 4), Evaluate));
        Assert.False(machine.Advance(Snapshot(33, live: 4), Evaluate));
        Assert.True(machine.Advance(Snapshot(34, live: 4), Evaluate));
        Assert.Equal("regroup", machine.PhaseId);
    }

    [Fact]
    public void RegroupRequiresAStableFiveBodyRallyBeforeBreach()
    {
        StandingStrategyMachine machine = Machine(
            Phase("regroup", 0,
                Transition("breach", 4,
                    Condition("friendlies-in-zone", "at-least", 5,
                        "forward-rally"))),
            Phase("breach"));

        Assert.False(machine.Advance(Snapshot(40, rally: 5), Evaluate));
        Assert.False(machine.Advance(Snapshot(41, rally: 5), Evaluate));
        Assert.False(machine.Advance(Snapshot(42, rally: 4), Evaluate));
        Assert.False(machine.Advance(Snapshot(43, rally: 5), Evaluate));
        Assert.False(machine.Advance(Snapshot(44, rally: 5), Evaluate));
        Assert.False(machine.Advance(Snapshot(45, rally: 5), Evaluate));
        Assert.True(machine.Advance(Snapshot(46, rally: 5), Evaluate));
        Assert.Equal("breach", machine.PhaseId);
    }

    [Fact]
    public void TargetEntryConditionCanVetoAnOtherwiseReadyTransition()
    {
        StandingConditionGroup entry = Group(
            Condition("live-friendlies", "at-least", 5));
        StandingStrategyMachine machine = Machine(
            Phase("regroup", 0,
                Transition("breach", 1, Condition("always"))),
            Phase("breach", entry: [entry]));

        Assert.False(machine.Advance(Snapshot(50, live: 4), Evaluate));
        Assert.True(machine.Advance(Snapshot(51, live: 5), Evaluate));
        Assert.Equal("breach", machine.PhaseId);
    }

    [Fact]
    public void FogMemoryAndConfirmedDeathsRemainDifferentFacts()
    {
        StandingSnapshot onlyLastSeen = Snapshot(
            70, enemyUnavailable: 0, rememberedInHome: 2);
        StandingSnapshot confirmedDeaths = Snapshot(
            71, enemyUnavailable: 2, rememberedInHome: 0);

        Assert.False(Evaluate(
            Condition("known-enemies-unavailable", "at-least", 1),
            onlyLastSeen));
        Assert.True(Evaluate(
            Condition("remembered-enemies-in-zone", "at-least", 1,
                "enemy-home"), onlyLastSeen));
        Assert.True(Evaluate(
            Condition("known-enemies-unavailable", "at-least", 1),
            confirmedDeaths));
    }

    [Fact]
    public void SecuredCoreAndProgressStallCanTriggerAConversionBranch()
    {
        StandingConditionGroup conversion = new()
        {
            All =
            [
                Condition("secured-cores", "at-least", 1),
                Condition("ticks-without-objective-progress", "at-least", 45),
            ],
        };
        StandingSnapshot ready = Snapshot(90, secured: 1, stalled: 45);
        StandingSnapshot noCore = Snapshot(90, secured: 0, stalled: 90);

        Assert.True(StandingStrategyMachine.Matches(
            conversion, ready, Evaluate));
        Assert.False(StandingStrategyMachine.Matches(
            conversion, noCore, Evaluate));
    }

    private static StandingStrategyMachine Machine(
        params StandingPhasePlan[] phases) => new(new StandingStrategyPlan
        {
            InitialPhase = phases[0].Id,
            Parameters = [],
            Memory = new StandingMemoryPolicy(),
            Phases = phases,
        });

    private static StandingPhasePlan Phase(
        string id,
        int minimumTicks = 0,
        StandingTransitionPlan? transition = null,
        StandingConditionGroup[]? entry = null) => new()
        {
            Id = id,
            MinimumTicks = minimumTicks,
            Entry = entry ?? [],
            Assignments = [Assignment()],
            Transitions = transition is null ? [] : [transition],
        };

    private static StandingTransitionPlan Transition(
        string to,
        int stableTicks,
        StandingCondition condition) => new()
        {
            Priority = 10,
            To = to,
            StableTicks = stableTicks,
            When = [Group(condition)],
        };

    private static StandingAssignmentPlan Assignment() => new()
    {
        Id = "body",
        Resilience = "replaceable",
        Behavior = "advance",
        Position = new StandingPositionIntent
        {
            Kind = "zone",
            Target = "test",
        },
    };

    private static StandingConditionGroup Group(
        params StandingCondition[] conditions) => new() { All = conditions };

    private static StandingCondition Condition(
        string fact,
        string op = "at-least",
        int value = 1,
        string zone = "") => new()
        {
            Fact = fact,
            Operator = op,
            Value = value,
            Zone = zone,
        };

    private static StandingSnapshot Snapshot(
        int tick,
        int live = 8,
        int enemyUnavailable = 0,
        int secured = 0,
        int stalled = 0,
        int rally = 0,
        int rememberedInHome = 0) => new(
            tick,
            live,
            enemyUnavailable,
            secured,
            0,
            0,
            stalled,
            new Dictionary<string, int>(),
            new Dictionary<string, int> { ["forward-rally"] = rally },
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>
            {
                ["enemy-home"] = rememberedInHome,
            });

    private static bool Evaluate(
        StandingCondition condition,
        StandingSnapshot snapshot) => Compare(condition, condition.Fact switch
        {
            "always" => 1,
            "live-friendlies" => snapshot.LiveFriendlies,
            "known-enemies-unavailable" => snapshot.KnownEnemiesUnavailable,
            "secured-cores" => snapshot.SecuredCores,
            "ticks-without-objective-progress" =>
                snapshot.TicksWithoutObjectiveProgress,
            "friendlies-in-zone" => snapshot.FriendliesByZone
                .GetValueOrDefault(condition.Zone),
            "remembered-enemies-in-zone" => snapshot.RememberedEnemiesByZone
                .GetValueOrDefault(condition.Zone),
            _ => 0,
        });

    private static bool Compare(StandingCondition condition, int actual) =>
        condition.Operator switch
        {
            "at-least" => actual >= condition.Value,
            "at-most" => actual <= condition.Value,
            "equals" => actual == condition.Value,
            "less-than" => actual < condition.Value,
            "greater-than" => actual > condition.Value,
            _ => false,
        };
}
