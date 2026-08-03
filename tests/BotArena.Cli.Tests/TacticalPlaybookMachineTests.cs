namespace BotArena.Cli.Tests;

public sealed class TacticalPlaybookMachineTests
{
    [Fact]
    public void RespectTransitionCannotAccumulateBeforeThePhaseMinimum()
    {
        TacticalPlaybookMachine machine = Machine(
            minimumTicks: 8,
            transition: Transition("respect", stableTicks: 2));

        Assert.False(machine.AdvanceGlobal(6, _ => true));
        Assert.False(machine.AdvanceGlobal(7, _ => true));
        Assert.False(machine.AdvanceGlobal(8, _ => true));
        Assert.True(machine.AdvanceGlobal(9, _ => true));
        Assert.Equal("next", machine.PhaseId);
    }

    [Fact]
    public void InterruptTransitionMayStabilizeBeforeThePhaseMinimum()
    {
        TacticalPlaybookMachine machine = Machine(
            minimumTicks: 8,
            transition: Transition("interrupt", stableTicks: 2));

        Assert.False(machine.AdvanceGlobal(1, _ => true));
        Assert.True(machine.AdvanceGlobal(2, _ => true));
        Assert.Equal("next", machine.PhaseId);
    }

    private static TacticalPlaybookMachine Machine(
        int minimumTicks,
        TacticalPlaybookPackage.Transition transition)
    {
        var localState = new TacticalPlaybookPackage.LocalState(
            "ready", 0, []);
        var group = new TacticalPlaybookPackage.Group(
            "group", [], 0, 0, 0,
            new TacticalPlaybookPackage.Membership("", "", "", ""),
            new TacticalPlaybookPackage.StateMachine(
                "ready", [localState]));
        var playbook = new TacticalPlaybookPackage.Playbook
        {
            Schema = "arc-relay-tactical-playbook-v1",
            PlaybookId = "machine-test",
            AuditStatus = new TacticalPlaybookPackage.AuditStatus(true, false),
            Composition = [],
            Layout = new TacticalPlaybookPackage.LayoutReference("", ""),
            Perspective = "team-causal",
            Memory = new TacticalPlaybookPackage.MemoryPolicy(0, 0, 0, 0, 0),
            Arbitration = new TacticalPlaybookPackage.ArbitrationPolicy("", []),
            Roles = [],
            Groups = [group],
            Formations = [],
            Engagements = [],
            SupportPolicies = [],
            CustodyPolicies = [],
            Orders = [],
            Coordination = new TacticalPlaybookPackage.Coordination(
                "start",
                [
                    new TacticalPlaybookPackage.Phase(
                        "start", minimumTicks, [], [transition]),
                    new TacticalPlaybookPackage.Phase(
                        "next", 0, [], []),
                ]),
        };
        return new TacticalPlaybookMachine(playbook);
    }

    private static TacticalPlaybookPackage.Transition Transition(
        string minimumPolicy,
        int stableTicks) => new(
        Priority: 1,
        To: "next",
        Cause: "reaction",
        MinimumPolicy: minimumPolicy,
        StableTicks: stableTicks,
        When:
        [
            new TacticalPlaybookPackage.ConditionGroup(
                [new TacticalPlaybookPackage.Condition(
                    "live-friendlies", "at-least", 1, "", "", 0)],
                []),
        ]);
}
