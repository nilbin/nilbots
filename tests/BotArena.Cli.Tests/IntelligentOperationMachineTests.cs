using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class IntelligentOperationMachineTests
{
    [Fact]
    public void Case1_EssentialLossDuringPreparationAbortsWithoutSubstitution()
    {
        IntelligentOperationMachine machine = Machine(Plan(
            prepare: Task("hooks", ParticipantResilience.Essential, 2, 0, 1)));
        Facts facts = new("trigger");
        Step(machine, 0, Actors(0, 1, 2), facts);

        Step(machine, 1, Actors(1, 2), facts);

        Assert.Equal(OperationPhase.Recover, machine.State("play").Phase);
        Assert.Equal(RecoveryKind.Abort, machine.State("play").RecoveryKind);
        Assert.Equal(
            "prepare-participant-minimum",
            machine.Transitions[^1].Reason);
    }

    [Fact]
    public void Case2_SuccessWinsSameTickTieWithCommittedActorLoss()
    {
        IntelligentOperationMachine machine = Machine(Plan());
        Facts facts = new("trigger", "commit");
        Step(machine, 0, Actors(0, 1), facts);
        Step(machine, 1, Actors(0, 1), facts);

        facts.True("success");
        Step(machine, 2, Actors(1), facts);

        Assert.Equal(OperationPhase.Recover, machine.State("play").Phase);
        Assert.Equal(RecoveryKind.Success, machine.State("play").RecoveryKind);
        Assert.Equal("mission-success", machine.Transitions[^1].Reason);
    }

    [Fact]
    public void Case3_UnknownTargetDoesNotBecomeOmniscienceAndExpiryAborts()
    {
        IntelligentOperationMachine machine = Machine(Plan());
        Facts facts = new("trigger", "commit");
        Step(machine, 0, Actors(0, 1), facts);
        Step(machine, 1, Actors(0, 1), facts);

        facts.Unknown("target-invalid");
        Step(machine, 2, Actors(0, 1), facts);
        Assert.Equal(OperationPhase.Commit, machine.State("play").Phase);

        facts.True("target-invalid");
        Step(machine, 3, Actors(0, 1), facts);
        Assert.Equal(OperationPhase.Recover, machine.State("play").Phase);
        Assert.Equal("mission-abort", machine.Transitions[^1].Reason);

        var coreId = new GenericActorContext.ArcRelayCoreId("north", 0);
        var carrier = new ActorIdentity(1, 3, 0);
        var target = new CausalCarrierTarget(
            coreId, carrier, new Position(12, 4), 10);
        var unrelated = new GenericActorContext.ArcRelayCoreState(
            new GenericActorContext.ArcRelayCoreId("south", 0),
            new Position(4, 4),
            GenericActorContext.ArcRelayCoreDisposition.Loose,
            null,
            0,
            null,
            null);
        Assert.Equal(
            OperationTruth.Unknown,
            target.Success([unrelated], ownTeamId: 0));
        Assert.Equal(
            OperationTruth.False,
            target.Invalid(
                [], [], 0, tick: 12, freshnessTicks: 2,
                insideMissionArea: _ => true));
        Assert.Equal(
            OperationTruth.True,
            target.Invalid(
                [], [], 0, tick: 13, freshnessTicks: 2,
                insideMissionArea: _ => true));
        var exactLoose = unrelated with { CoreId = coreId };
        Assert.Equal(
            OperationTruth.True,
            target.Success([unrelated, exactLoose], ownTeamId: 0));
    }

    [Fact]
    public void Case4_FalseAlarmChoosesClearRouteOnceAndRequiresEdgeRearm()
    {
        IntelligentOperationMachine machine = Machine(Plan(
            branches:
            [
                Branch("alternate", "threat"),
                Branch("primary", "clear"),
            ]));
        Facts facts = new("trigger", "clear");
        Step(machine, 0, Actors(0, 1), facts);
        Step(machine, 1, Actors(0, 1), facts);
        Assert.Equal("primary", machine.State("play").BranchId);

        facts.True("success");
        Step(machine, 2, Actors(0, 1), facts);
        Step(machine, 3, Actors(0, 1), facts);
        Step(machine, 40, Actors(0, 1), facts);

        Assert.Equal(OperationPhase.Dormant, machine.State("play").Phase);
        Assert.Equal(1, machine.Transitions.Count(value =>
            value.To == OperationPhase.Prepare));
    }

    [Fact]
    public void Case5_EssentialScoutLossBeforeBranchUsesRecovery()
    {
        IntelligentOperationMachine machine = Machine(Plan(
            prepare: Task("carrier-and-scout",
                ParticipantResilience.Essential, 2, 0, 1)));
        Facts facts = new("trigger");
        Step(machine, 0, Actors(0, 1, 2), facts);

        Step(machine, 1, Actors(0, 2), facts);

        Assert.Equal(OperationPhase.Recover, machine.State("play").Phase);
        Assert.DoesNotContain(machine.Transitions, value =>
            value.To == OperationPhase.Commit);
    }

    [Fact]
    public void Case6_FirstTrueOrderedBranchLocksWhenBothAreTrue()
    {
        IntelligentOperationMachine machine = Machine(Plan(
            branches:
            [
                Branch("alternate", "threat"),
                Branch("primary", "clear"),
            ]));
        Facts facts = new("trigger", "threat", "clear");
        Step(machine, 0, Actors(0, 1), facts);
        Step(machine, 1, Actors(0, 1), facts);

        Assert.Equal(OperationPhase.Commit, machine.State("play").Phase);
        Assert.Equal("alternate", machine.State("play").BranchId);
        facts.False("threat");
        Step(machine, 2, Actors(0, 1), facts);
        Assert.Equal("alternate", machine.State("play").BranchId);
    }

    [Fact]
    public void Case7_PrepareCanReplaceButCommitCannotRecruitARespawn()
    {
        OperationTask pool = Task(
            "pool", ParticipantResilience.Replaceable, 2, 0, 1, 2);
        IntelligentOperationMachine machine = Machine(Plan(
            prepare: pool,
            branches: [Branch("go", "commit", pool)]));
        Facts facts = new("trigger");
        Step(machine, 0, Actors(0, 1, 2), facts);

        Step(machine, 1, Actors(1, 2), facts);
        Assert.Equal(
            [1, 2],
            machine.State("play").Assignments
                .Select(value => value.UnitId).Order().ToArray());

        facts.True("commit");
        Step(machine, 2, Actors(1, 2), facts);
        Assert.Equal(OperationPhase.Commit, machine.State("play").Phase);
        Step(machine, 3, Actors(0, 2), facts);
        Assert.Equal(OperationPhase.Recover, machine.State("play").Phase);
        Assert.Equal("commit-participant-minimum", machine.Transitions[^1].Reason);
    }

    [Fact]
    public void Case8_PreparationIsPreemptibleButCommitmentIsLocked()
    {
        OperationTask shared = Task(
            "receiver", ParticipantResilience.Replaceable, 1, 0, 1);
        IntelligentOperationPlan emergency = Plan(
            id: "emergency", priority: 1, trigger: "emergency",
            prepare: Task("receiver", ParticipantResilience.Essential, 1, 0));
        IntelligentOperationPlan rotation = Plan(
            id: "rotation", priority: 20, trigger: "rotation",
            prepare: shared,
            branches: [Branch("rotate", "rotate-commit", shared)]);
        Facts facts = new("rotation");
        var preparing = new IntelligentOperationMachine([emergency, rotation]);
        Step(preparing, 0, Actors(0, 1), facts);
        facts.True("emergency");
        Step(preparing, 1, Actors(0, 1), facts);
        Assert.Equal(
            [0], preparing.State("emergency").Assignments
                .Select(value => value.UnitId).ToArray());
        Assert.Equal(
            [1], preparing.State("rotation").Assignments
                .Select(value => value.UnitId).ToArray());

        facts = new Facts("rotation");
        var committed = new IntelligentOperationMachine([emergency, rotation]);
        Step(committed, 0, Actors(0, 1), facts);
        facts.True("rotate-commit");
        Step(committed, 1, Actors(0, 1), facts);
        facts.True("emergency");
        Step(committed, 2, Actors(0, 1), facts);
        Assert.Equal(OperationPhase.Commit, committed.State("rotation").Phase);
        Assert.Equal(OperationPhase.Dormant, committed.State("emergency").Phase);
        Assert.Empty(committed.State("emergency").Assignments);
    }

    [Fact]
    public void Case9_OptionalStrikeGroupContinuesAfterOnePartnerDies()
    {
        OperationTask strike = Task(
            "strike", ParticipantResilience.Optional, 0, 0, 1);
        IntelligentOperationMachine machine = Machine(Plan(
            branches: [Branch("go", "commit", strike)]));
        Facts facts = new("trigger", "commit");
        Step(machine, 0, Actors(0, 1), facts);
        Step(machine, 1, Actors(0, 1), facts);

        Step(machine, 2, Actors(1), facts);
        Assert.Equal(OperationPhase.Commit, machine.State("play").Phase);

        facts.True("success");
        Step(machine, 3, Actors(1), facts);
        Assert.Equal(RecoveryKind.Success, machine.State("play").RecoveryKind);
    }

    [Fact]
    public void Case10_RecoveryDeadlineAlwaysReleasesSurvivorsToBaseline()
    {
        IntelligentOperationPlan plan = Plan() with
        {
            Recovery = new OperationRecovery(
                3,
                [Condition("safe")],
                [Task("extract", ParticipantResilience.Optional, 0, 0, 1)],
                [Task("extract", ParticipantResilience.Optional, 0, 0, 1)])
        };
        IntelligentOperationMachine machine = Machine(plan);
        Facts facts = new("trigger", "commit");
        Step(machine, 0, Actors(0, 1), facts);
        Step(machine, 1, Actors(0, 1), facts);
        facts.True("target-invalid");
        Step(machine, 2, Actors(0, 1), facts);
        Step(machine, 3, Actors(0, 1), facts);
        Step(machine, 4, Actors(0, 1), facts);
        Step(machine, 5, Actors(0, 1), facts);

        Assert.Equal(OperationPhase.Dormant, machine.State("play").Phase);
        Assert.Empty(machine.State("play").Assignments);
        Assert.Equal(
            "recovery-deadline-baseline-release",
            machine.Transitions[^1].Reason);
    }

    private static IntelligentOperationMachine Machine(
        params IntelligentOperationPlan[] plans) => new(plans);

    private static IntelligentOperationPlan Plan(
        string id = "play",
        int priority = 10,
        string trigger = "trigger",
        OperationTask? prepare = null,
        OperationBranch[]? branches = null) => new(
        priority,
        id,
        10,
        5,
        new OperationConditionGroup([Condition(trigger)], []),
        [],
        [prepare ?? Task("pair", ParticipantResilience.Essential, 2, 0, 1)],
        branches ?? [Branch("go", "commit")],
        new OperationRecovery(3, [], [], []));

    private static OperationBranch Branch(
        string id,
        string fact,
        OperationTask? task = null) => new(
        id,
        new OperationConditionGroup([Condition(fact)], []),
        [task ?? Task("pair", ParticipantResilience.Essential, 2, 0, 1)],
        [Condition("success")],
        [Condition("target-invalid")],
        10);

    private static OperationTask Task(
        string id,
        ParticipantResilience resilience,
        int minimum,
        params int[] units) => new(
        id,
        resilience,
        minimum,
        units,
        [],
        [],
        false,
        false,
        PositionIntent.BaseAssignment,
        "",
        "opportunistic",
        "normal");

    private static OperationCondition Condition(string fact) => new(
        fact, "equals", 1, "", "", 0, []);

    private static OperationActor[] Actors(params int[] ids) => ids
        .Select(id => new OperationActor(
            id, $"life-{id}", "test", "reserve", false, new Position(id, 0)))
        .ToArray();

    private static void Step(
        IntelligentOperationMachine machine,
        int tick,
        OperationActor[] actors,
        Facts facts) => machine.Update(
        tick,
        actors,
        (_, _, condition) => facts[condition.Fact],
        static (_, _, _) => true);

    private sealed class Facts(params string[] trueFacts)
    {
        private readonly Dictionary<string, OperationTruth> _values =
            trueFacts.ToDictionary(
                value => value,
                _ => OperationTruth.True,
                StringComparer.Ordinal);

        internal OperationTruth this[string fact] =>
            _values.GetValueOrDefault(fact, OperationTruth.False);
        internal void True(string fact) => _values[fact] = OperationTruth.True;
        internal void False(string fact) => _values[fact] = OperationTruth.False;
        internal void Unknown(string fact) =>
            _values[fact] = OperationTruth.Unknown;
    }
}
