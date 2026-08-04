using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalTaskMachineTests
{
    [Fact]
    public void ClassPreferenceSelectsOneBodyAndCompletionReleasesIt()
    {
        TacticalTaskMachine machine = Machine(Task(
            "convert",
            Assignment("courier", "convert-order", ["kestrel", "relay"])));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts,
            Candidate(0, "relay", x: 1),
            Candidate(1, "kestrel", x: 5));

        Assert.Null(machine.DirectiveFor(0));
        Assert.Equal("convert-order", machine.DirectiveFor(1)!.OrderId);

        facts.Add("complete");
        Step(machine, 1, facts,
            Candidate(0, "relay", x: 1),
            Candidate(1, "kestrel", x: 5));

        Assert.Null(machine.DirectiveFor(1));
        Assert.Equal(
            TacticalTaskPhase.Dormant,
            machine.State("convert").Phase);
    }

    [Fact]
    public void DistanceBreaksEqualClassTiesBeforeUnitId()
    {
        TacticalTaskMachine machine = Machine(Task(
            "nearest",
            Assignment("scout", "convert-order", ["kestrel"])));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts,
            Candidate(0, "kestrel", x: 8),
            Candidate(1, "kestrel", x: 2));

        Assert.Null(machine.DirectiveFor(0));
        Assert.NotNull(machine.DirectiveFor(1));
    }

    [Fact]
    public void DisjointTasksRunConcurrently()
    {
        TacticalTaskMachine machine = Machine(
            Task("courier", Assignment(
                "courier", "convert-order", ["kestrel"]), priority: 10),
            Task("screen", Assignment(
                "screen", "deny-order", ["repulsor"]), priority: 20));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts,
            Candidate(0, "kestrel", x: 1),
            Candidate(1, "repulsor", x: 2));

        Assert.Equal("courier", machine.DirectiveFor(0)!.TaskId);
        Assert.Equal("screen", machine.DirectiveFor(1)!.TaskId);
    }

    [Fact]
    public void HigherPriorityTaskPreemptsOnlyAnExplicitlyPreemptibleLease()
    {
        TacticalTaskMachine machine = Machine(
            Task("urgent", Assignment(
                    "actor", "urgent-order", ["kestrel"]),
                priority: 10,
                triggerFact: "urgent"),
            Task("routine", Assignment(
                    "actor", "routine-order", ["kestrel"]),
                priority: 20,
                preemption: "higher-priority"));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);
        TacticalTaskCandidate actor = Candidate(0, "kestrel", x: 1);

        Step(machine, 0, facts, actor);
        Assert.Equal("routine", machine.DirectiveFor(0)!.TaskId);

        facts.Add("urgent");
        Step(machine, 1, facts, actor);
        Assert.Equal("urgent", machine.DirectiveFor(0)!.TaskId);
        Assert.Contains(machine.Transitions, value =>
            value.TaskId == "routine"
            && value.Reason == "preempted-by-higher-priority");
    }

    [Fact]
    public void NeverPreemptionKeepsTheExistingLease()
    {
        TacticalTaskMachine machine = Machine(
            Task("urgent", Assignment(
                    "actor", "urgent-order", ["kestrel"]),
                priority: 10,
                triggerFact: "urgent"),
            Task("routine", Assignment(
                    "actor", "routine-order", ["kestrel"]),
                priority: 20,
                preemption: "never"));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);
        TacticalTaskCandidate actor = Candidate(0, "kestrel", x: 1);

        Step(machine, 0, facts, actor);
        facts.Add("urgent");
        Step(machine, 1, facts, actor);

        Assert.Equal("routine", machine.DirectiveFor(0)!.TaskId);
        Assert.Equal(
            "participants-unavailable",
            machine.State("urgent").LastReason);
    }

    [Fact]
    public void ReplacePolicyPromotesTheNextEligibleLife()
    {
        TacticalTaskMachine machine = Machine(Task(
            "replace",
            Assignment("actor", "convert-order", ["kestrel"]),
            participantLoss: "replace"));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts,
            Candidate(0, "kestrel", x: 1),
            Candidate(1, "kestrel", x: 2));
        Assert.NotNull(machine.DirectiveFor(0));

        Step(machine, 1, facts, Candidate(1, "kestrel", x: 2));
        Assert.NotNull(machine.DirectiveFor(1));
        Assert.Equal(
            TacticalTaskPhase.Active,
            machine.State("replace").Phase);
    }

    [Fact]
    public void AbortPolicyReleasesSurvivorsAfterParticipantDeath()
    {
        TacticalTaskMachine machine = Machine(Task(
            "abort",
            Assignment(
                "pair", "convert-order", ["kestrel"], minimum: 2),
            participantLoss: "abort"));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts,
            Candidate(0, "kestrel", x: 1),
            Candidate(1, "kestrel", x: 2));
        Step(machine, 1, facts, Candidate(1, "kestrel", x: 2));

        Assert.Null(machine.DirectiveFor(1));
        Assert.Equal(
            TacticalTaskPhase.Dormant,
            machine.State("abort").Phase);
        Assert.Equal(
            "participant-lost-primary-order",
            machine.State("abort").LastReason);
    }

    [Fact]
    public void CarrierRequirementCannotClaimANonCarrier()
    {
        TacticalPlaybookPackage.TaskAssignment assignment = Assignment(
            "carrier", "convert-order", ["relay"]) with
        {
            Carrier = "require",
        };
        TacticalTaskMachine machine = Machine(Task("carry", assignment));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts, Candidate(0, "relay", x: 1));
        Assert.Null(machine.DirectiveFor(0));

        Step(machine, 1, facts, Candidate(0, "relay", x: 1, carrier: true));
        Assert.NotNull(machine.DirectiveFor(0));
    }

    [Fact]
    public void ActiveLeaseSurvivesCarrierAndPrimaryLocalStateChanges()
    {
        TacticalTaskMachine machine = Machine(Task(
            "carry",
            Assignment("courier", "convert-order", ["kestrel"]),
            participantLoss: "abort"));
        var facts = new HashSet<string>(["trigger"], StringComparer.Ordinal);

        Step(machine, 0, facts, Candidate(0, "kestrel", x: 1));
        Step(machine, 1, facts, Candidate(
            0,
            "kestrel",
            x: 2,
            carrier: true,
            localState: "recovering"));

        Assert.Equal("convert-order", machine.DirectiveFor(0)!.OrderId);
        Assert.Equal(TacticalTaskPhase.Active, machine.State("carry").Phase);
        Assert.Equal("active", machine.State("carry").LastReason);
    }

    private static TacticalTaskMachine Machine(
        params TacticalPlaybookPackage.TacticalTask[] tasks)
    {
        TacticalPlaybookPackage.Order[] orders =
        [
            Order("convert-order"),
            Order("deny-order"),
            Order("urgent-order"),
            Order("routine-order"),
        ];
        var playbook = new TacticalPlaybookPackage.Playbook
        {
            Schema = "arc-relay-tactical-playbook-v1",
            PlaybookId = "task-machine-test",
            AuditStatus = new(true, false),
            Composition = [],
            Layout = new("", ""),
            Perspective = "team-relative",
            Memory = new(1, 1, 1, 1, 1),
            Arbitration = new("first-legal", []),
            Roles = [],
            Groups = [],
            Formations = [],
            Engagements = [],
            SupportPolicies = [],
            CustodyPolicies = [],
            Orders = orders,
            Coordination = new("occupy", [], tasks),
        };
        return new TacticalTaskMachine(playbook);
    }

    private static TacticalPlaybookPackage.TacticalTask Task(
        string id,
        TacticalPlaybookPackage.TaskAssignment assignment,
        int priority = 10,
        string triggerFact = "trigger",
        string preemption = "higher-priority",
        string participantLoss = "continue") => new(
        id,
        priority,
        "while-true",
        preemption,
        participantLoss,
        1,
        0,
        20,
        0,
        ["occupy"],
        [assignment],
        [Group(triggerFact)],
        [Group("complete")],
        [],
        new("primary-order", [], [], 0));

    private static TacticalPlaybookPackage.TaskAssignment Assignment(
        string id,
        string orderId,
        string[] classes,
        int minimum = 1) => new(
        id,
        orderId,
        ["line"],
        classes,
        minimum,
        minimum,
        minimum,
        "forbid",
        new("anchor", "target"));

    private static TacticalPlaybookPackage.Order Order(string id) => new(
        id,
        "line-group",
        10,
        new("all", null, null, null),
        new("hold", "", 0, "continuous", 1, "hold", 0, "free"),
        "formation",
        "engagement",
        "",
        "custody",
        "ready",
        new("hold", "continue", "hold", ""));

    private static TacticalTaskCandidate Candidate(
        int unitId,
        string classId,
        int x,
        bool carrier = false,
        int lifeId = 0,
        string localState = "ready") => new(
        unitId,
        new ActorIdentity(0, unitId, lifeId),
        "line",
        "line-group",
        localState,
        classId,
        carrier,
        new Position(x, 0));

    private static TacticalPlaybookPackage.ConditionGroup Group(string fact) =>
        new([new(fact, "equals", 1, "", "", 0)], []);

    private static void Step(
        TacticalTaskMachine machine,
        int tick,
        IReadOnlySet<string> facts,
        params TacticalTaskCandidate[] candidates) => machine.Update(
        tick,
        "occupy",
        candidates,
        group => group.All.All(condition => facts.Contains(condition.Fact)),
        (_, candidate) => candidate.Position.X);
}
