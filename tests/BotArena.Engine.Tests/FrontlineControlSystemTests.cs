using BotArena.Engine;

namespace BotArena.Engine.Tests;

public class FrontlineControlSystemTests
{
    private static readonly FrontlineTeamPresence Team0 =
        new(Team0Present: true, Team1Present: false);
    private static readonly FrontlineTeamPresence Team1 =
        new(Team0Present: false, Team1Present: true);
    private static readonly FrontlineTeamPresence Empty =
        new(Team0Present: false, Team1Present: false);
    private static readonly FrontlineTeamPresence Contested =
        new(Team0Present: true, Team1Present: true);

    [Fact]
    public void UninterruptedTeam0Control_CapturesAt14And34ThenBreachesAt54()
    {
        var rules = new FrontlineRules();
        FrontlineControlState state = FrontlineControlSystem.CreateInitial(rules);
        var transitions = new List<FrontlineControlTransition>();

        for (int tick = 0; tick <= 54; tick++)
        {
            FrontlineControlStepResult result =
                FrontlineControlSystem.Step(rules, state, tick, Team0);
            state = result.State;
            if (result.Transition is not null)
                transitions.Add(result.Transition);

            if (tick is >= 15 and <= 19)
            {
                Assert.Equal(3, state.ActivePositionIndex);
                Assert.Null(state.ClaimingTeamId);
                Assert.Equal(0, state.CaptureProgress);
            }
            if (tick == 20)
            {
                Assert.Equal(0, state.ClaimingTeamId);
                Assert.Equal(1, state.CaptureProgress);
            }
        }

        Assert.Equal(
            [
                new FrontlinePositionAdvanced(14, 0, 2, 3),
                new FrontlinePositionAdvanced(34, 0, 3, 4),
                new FrontlineBaseBreached(54, 0, 4),
            ],
            transitions);
        Assert.Equal(0, state.WinnerTeamId);
        Assert.Equal(55, state.NextTick);
    }

    [Fact]
    public void Team1Direction_IsTheExactMirror()
    {
        var rules = new FrontlineRules();
        FrontlineControlState state = FrontlineControlSystem.CreateInitial(rules);
        var transitions = new List<FrontlineControlTransition>();

        for (int tick = 0; tick <= 54; tick++)
        {
            FrontlineControlStepResult result =
                FrontlineControlSystem.Step(rules, state, tick, Team1);
            state = result.State;
            if (result.Transition is not null)
                transitions.Add(result.Transition);
        }

        Assert.Equal(
            [
                new FrontlinePositionAdvanced(14, 1, 2, 1),
                new FrontlinePositionAdvanced(34, 1, 1, 0),
                new FrontlineBaseBreached(54, 1, 0),
            ],
            transitions);
        Assert.Equal(1, state.WinnerTeamId);
    }

    [Fact]
    public void EmptyAndContestedSteps_ShareExactTwoStepDecayCadence()
    {
        var rules = new FrontlineRules();
        FrontlineControlState state = FrontlineControlSystem.CreateInitial(rules);

        state = Step(rules, state, Team0);
        state = Step(rules, state, Team0);
        state = Step(rules, state, Team0);
        Assert.Equal(3, state.CaptureProgress);

        state = Step(rules, state, Empty);
        Assert.Equal(3, state.CaptureProgress);
        Assert.Equal(1, state.DecayTicksElapsed);

        state = Step(rules, state, Contested);
        Assert.Equal(2, state.CaptureProgress);
        Assert.Equal(0, state.DecayTicksElapsed);

        state = Step(rules, state, Empty);
        Assert.Equal(1, state.DecayTicksElapsed);
        state = Step(rules, state, Team0);
        Assert.Equal(3, state.CaptureProgress);
        Assert.Equal(0, state.DecayTicksElapsed);

        state = Step(rules, state, Contested);
        state = Step(rules, state, Empty);
        Assert.Equal(2, state.CaptureProgress);
    }

    [Fact]
    public void OpposingControl_MustEraseTheClaimBeforeStartingItsOwn()
    {
        var rules = new FrontlineRules();
        FrontlineControlState state = FrontlineControlSystem.CreateInitial(rules);

        state = Step(rules, state, Team0);
        state = Step(rules, state, Team0);
        Assert.Equal(2, state.CaptureProgress);

        state = Step(rules, state, Team1);
        Assert.Equal(0, state.ClaimingTeamId);
        Assert.Equal(1, state.CaptureProgress);

        state = Step(rules, state, Team1);
        Assert.Null(state.ClaimingTeamId);
        Assert.Equal(0, state.CaptureProgress);

        state = Step(rules, state, Team1);
        Assert.Equal(1, state.ClaimingTeamId);
        Assert.Equal(1, state.CaptureProgress);
    }

    [Fact]
    public void PresenceDeduplicatesAlliedBodies_AndAnyEnemyContests()
    {
        FrontlineTeamPresence one =
            FrontlineTeamPresence.FromOccupyingTeamIds([0]);
        FrontlineTeamPresence three =
            FrontlineTeamPresence.FromOccupyingTeamIds([0, 0, 0]);
        FrontlineTeamPresence contested =
            FrontlineTeamPresence.FromOccupyingTeamIds([0, 0, 1]);

        Assert.Equal(one, three);
        Assert.Equal(0, three.SoleTeamId);
        Assert.Null(contested.SoleTeamId);

        var rules = new FrontlineRules();
        FrontlineControlState initial = FrontlineControlSystem.CreateInitial(rules);
        Assert.Equal(
            FrontlineControlSystem.Step(rules, initial, 0, one).State,
            FrontlineControlSystem.Step(rules, initial, 0, three).State);
    }

    [Fact]
    public void CapturedPositions_CanBePushedBack()
    {
        var rules = new FrontlineRules
        {
            CaptureThreshold = 1,
            RedeployPauseTicks = 0,
        };
        FrontlineControlState state = FrontlineControlSystem.CreateInitial(rules);
        FrontlineControlStepResult forward =
            FrontlineControlSystem.Step(rules, state, 0, Team0);
        FrontlineControlStepResult backward =
            FrontlineControlSystem.Step(rules, forward.State, 1, Team1);

        Assert.Equal(new FrontlinePositionAdvanced(0, 0, 2, 3), forward.Transition);
        Assert.Equal(new FrontlinePositionAdvanced(1, 1, 3, 2), backward.Transition);
        Assert.Equal(2, backward.State.ActivePositionIndex);
    }

    [Fact]
    public void NonSequentialAndPostBreachSteps_AreRejected()
    {
        var fastRules = new FrontlineRules
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
            CaptureThreshold = 1,
            RedeployPauseTicks = 0,
        };
        FrontlineControlState state =
            FrontlineControlSystem.CreateInitial(fastRules);

        Assert.Throws<ArgumentException>(() =>
            FrontlineControlSystem.Step(fastRules, state, 1, Team0));

        state = FrontlineControlSystem.Step(fastRules, state, 0, Team0).State;
        Assert.Equal(2, state.ActivePositionIndex);
        state = FrontlineControlSystem.Step(fastRules, state, 1, Team0).State;
        Assert.Equal(0, state.WinnerTeamId);

        Assert.Throws<InvalidOperationException>(() =>
            FrontlineControlSystem.Step(fastRules, state, 2, Team0));
    }

    [Fact]
    public void MalformedTerminalStates_AreRejectedBeforePostBreachHandling()
    {
        var rules = new FrontlineRules();
        FrontlineControlState initial = FrontlineControlSystem.CreateInitial(rules);
        FrontlineControlState wrongEdge = initial with
        {
            ActivePositionIndex = 0,
            WinnerTeamId = 0,
        };
        FrontlineControlState retainedProgress = initial with
        {
            ActivePositionIndex = 4,
            ClaimingTeamId = 0,
            CaptureProgress = 1,
            WinnerTeamId = 0,
        };
        FrontlineControlState neutralWithDecayCadence = initial with
        {
            DecayTicksElapsed = 1,
        };
        FrontlineControlState winnerDuringRedeploy = initial with
        {
            ActivePositionIndex = 4,
            ControlResumesAtTick = 1,
            WinnerTeamId = 0,
        };

        Assert.Throws<ArgumentException>(() =>
            FrontlineControlSystem.Step(rules, wrongEdge, 0, Empty));
        Assert.Throws<ArgumentException>(() =>
            FrontlineControlSystem.Step(rules, retainedProgress, 0, Empty));
        Assert.Throws<ArgumentException>(() =>
            FrontlineControlSystem.Step(
                rules,
                neutralWithDecayCadence,
                0,
                Empty));
        Assert.Throws<ArgumentException>(() =>
            FrontlineControlSystem.Step(rules, winnerDuringRedeploy, 0, Empty));
    }

    [Fact]
    public void CaptureOnTheFinalAllowedTick_StillEmitsImmediateBreach()
    {
        var rules = new FrontlineRules
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
            CaptureThreshold = 1,
            RedeployPauseTicks = 0,
        };
        FrontlineControlState state =
            FrontlineControlSystem.CreateInitial(rules, firstTick: 498);
        state = FrontlineControlSystem.Step(
            rules,
            state,
            tick: 498,
            presence: Team0).State;

        FrontlineControlStepResult finalTick =
            FrontlineControlSystem.Step(
                rules,
                state,
                tick: 499,
                presence: Team0);

        Assert.Equal(new FrontlineBaseBreached(499, 0, 2), finalTick.Transition);
        Assert.Equal(0, finalTick.State.WinnerTeamId);
    }

    private static FrontlineControlState Step(
        FrontlineRules rules,
        FrontlineControlState state,
        FrontlineTeamPresence presence) =>
        FrontlineControlSystem.Step(
            rules,
            state,
            state.NextTick,
            presence).State;
}
