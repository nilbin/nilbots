namespace BotArena.Cli.Tests;

public sealed class TacticalMembershipPrimitivesTests
{
    [Fact]
    public void HoldVacancyDoesNotStealAnotherStableRole()
    {
        Dictionary<int, string> assigned = Allocate(
            deathPolicy: "hold-vacancy",
            preemption: "higher-priority",
            phaseBoundary: false);

        Assert.Equal("line", assigned[1]);
    }

    [Fact]
    public void PromoteBestFillsADeadPriorityRoleDeterministically()
    {
        Dictionary<int, string> assigned = Allocate(
            deathPolicy: "promote-best",
            preemption: "higher-priority",
            phaseBoundary: false);

        Assert.Equal("runner", assigned[1]);
    }

    [Fact]
    public void PhaseBoundaryPreemptionCannotHappenMidPhase()
    {
        Assert.Equal("line", Allocate(
            "promote-best", "phase-boundary", phaseBoundary: false)[1]);
        Assert.Equal("runner", Allocate(
            "promote-best", "phase-boundary", phaseBoundary: true)[1]);
    }

    [Fact]
    public void ReplaceRespawnDoesNotDisplaceThePromotedLiveBody()
    {
        TacticalMembershipPrimitives.Candidate[] candidates =
        [
            new(0, "relay", 5, Respawned: true),
            new(1, "relay", 3, Respawned: false),
        ];
        TacticalMembershipPrimitives.RoleRule[] rules =
        [
            Rule("runner", 1, "promote-best", "replace", "line",
                "stable-slot", "higher-priority", "declared-role"),
            Rule("line", 1, "rebalance", "rejoin", "",
                "stable-slot", "higher-priority", "lowest-count"),
        ];
        var prior = new Dictionary<int, string>
        {
            [0] = "runner",
            [1] = "runner",
        };

        Dictionary<int, string> assigned = TacticalMembershipPrimitives
            .Allocate(candidates, rules, prior, phaseBoundary: false);

        Assert.Equal("line", assigned[0]);
        Assert.Equal("runner", assigned[1]);
    }

    [Fact]
    public void BestFitRebalancesByCandidateRankHealthAndUnitId()
    {
        TacticalMembershipPrimitives.Candidate[] candidates =
        [
            new(0, "relay", 2, Respawned: false),
            new(1, "relay", 5, Respawned: false),
        ];
        TacticalMembershipPrimitives.RoleRule[] rules =
        [
            Rule("runner", 1, "rebalance", "resume", "line",
                "best-fit", "higher-priority", "declared-role"),
            Rule("line", 1, "rebalance", "resume", "",
                "best-fit", "higher-priority", "lowest-count"),
        ];

        Dictionary<int, string> assigned = TacticalMembershipPrimitives
            .Allocate(candidates, rules, new Dictionary<int, string>(),
                phaseBoundary: false);

        Assert.Equal("line", assigned[0]);
        Assert.Equal("runner", assigned[1]);
    }

    [Theory]
    [InlineData("resume", false)]
    [InlineData("rejoin", true)]
    [InlineData("rally", true)]
    [InlineData("replace", true)]
    public void RespawnPolicyControlsCohortJoining(
        string policy,
        bool expected) => Assert.Equal(
            expected,
            TacticalMembershipPrimitives.JoinsCohort(policy));

    private static Dictionary<int, string> Allocate(
        string deathPolicy,
        string preemption,
        bool phaseBoundary)
    {
        TacticalMembershipPrimitives.Candidate[] candidates =
        [new(1, "relay", 4, Respawned: false)];
        TacticalMembershipPrimitives.RoleRule[] rules =
        [
            Rule("runner", 1, deathPolicy, "replace", "line",
                "stable-slot", preemption, "declared-role"),
            Rule("line", 1, "promote-best", "resume", "",
                "stable-slot", "never", "lowest-count"),
        ];
        var prior = new Dictionary<int, string>
        {
            [0] = "runner",
            [1] = "line",
        };
        return TacticalMembershipPrimitives.Allocate(
            candidates, rules, prior, phaseBoundary);
    }

    private static TacticalMembershipPrimitives.RoleRule Rule(
        string id,
        int preferred,
        string death,
        string respawn,
        string overflow,
        string persistence,
        string preemption,
        string groupOverflow) => new(
            id,
            ["relay"],
            Minimum: preferred,
            Preferred: preferred,
            Maximum: preferred,
            DeathPolicy: death,
            RespawnPolicy: respawn,
            OverflowRoleId: overflow,
            Persistence: persistence,
            Preemption: preemption,
            GroupOverflow: groupOverflow);
}
