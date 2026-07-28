using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class FrontlineModeKernelTests
{
    private const int LowerTeam = 7;
    private const int HigherTeam = 42;

    [Fact]
    public void UsesTopologyTeamIdsRatherThanHardCodedSides()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 5,
            gain: 3,
            decayAmount: 1,
            decayInterval: 2,
            pause: 2);
        FrontlineControlState initial = kernel.CreateInitialState();

        FrontlineControlStepResult first =
            kernel.ApplyJointTick(initial, tick: 0, [HigherTeam]);
        FrontlineControlStepResult capture =
            kernel.ApplyJointTick(first.State, tick: 1, [HigherTeam]);

        Assert.Equal(HigherTeam, first.State.ClaimingTeamId);
        Assert.Equal(3, first.State.CaptureProgress);
        var advanced =
            Assert.IsType<FrontlinePositionAdvanced>(capture.Transition);
        Assert.Equal(HigherTeam, advanced.TeamId);
        Assert.Equal(2, advanced.FromPositionIndex);
        Assert.Equal(3, advanced.ToPositionIndex);
        Assert.Equal(4, capture.State.ControlResumesAtTick);

        FrontlineControlStepResult paused = kernel.ApplyJointTick(
            capture.State,
            tick: 2,
            [LowerTeam]);
        Assert.Equal(3, paused.State.ActivePositionIndex);
        Assert.Null(paused.State.ClaimingTeamId);

        FrontlineScoreState scores =
            kernel.CreateScoreState(paused.State);
        Assert.Equal(
            -5,
            scores.Teams.Single(team => team.TeamId == LowerTeam)
                .TerritorialProgress);
        Assert.Equal(
            5,
            scores.Teams.Single(team => team.TeamId == HigherTeam)
                .TerritorialProgress);
    }

    [Fact]
    public void OppositionErasesClaimWithoutCarryingOvershoot()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 10,
            gain: 4,
            decayAmount: 0,
            decayInterval: 0,
            pause: 0);
        FrontlineControlState state = kernel.CreateInitialState() with
        {
            ClaimingTeamId = HigherTeam,
            CaptureProgress = 3,
        };

        FrontlineControlStepResult erased =
            kernel.ApplyJointTick(state, tick: 0, [LowerTeam]);
        FrontlineControlStepResult claimed = kernel.ApplyJointTick(
            erased.State,
            tick: 1,
            [LowerTeam]);

        Assert.Null(erased.State.ClaimingTeamId);
        Assert.Equal(0, erased.State.CaptureProgress);
        Assert.Equal(LowerTeam, claimed.State.ClaimingTeamId);
        Assert.Equal(4, claimed.State.CaptureProgress);
    }

    [Fact]
    public void EmptyAndContestedTicksUseConfiguredDecayClock()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 10,
            gain: 6,
            decayAmount: 2,
            decayInterval: 2,
            pause: 0);
        FrontlineControlState state = kernel.CreateInitialState();

        state = kernel.ApplyJointTick(
            state,
            tick: 0,
            [HigherTeam]).State;
        state = kernel.ApplyJointTick(
            state,
            tick: 1,
            [LowerTeam, HigherTeam]).State;
        Assert.Equal(6, state.CaptureProgress);
        Assert.Equal(1, state.DecayTicksElapsed);

        state = kernel.ApplyJointTick(
            state,
            tick: 2,
            []).State;
        Assert.Equal(4, state.CaptureProgress);
        Assert.Equal(0, state.DecayTicksElapsed);

        FrontlineControlStepResult captured = kernel.ApplyJointTick(
            state,
            tick: 3,
            [HigherTeam]);
        Assert.IsType<FrontlinePositionAdvanced>(captured.Transition);
    }

    [Fact]
    public void DisabledDecayPreservesAClaimAndZeroClock()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 10,
            gain: 3,
            decayAmount: 0,
            decayInterval: 0,
            pause: 0);
        FrontlineControlState state = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            tick: 0,
            [HigherTeam]).State;

        for (int tick = 1; tick <= 4; tick++)
        {
            state = kernel.ApplyJointTick(
                state,
                tick,
                tick % 2 == 0
                    ? [LowerTeam, HigherTeam]
                    : []).State;
        }

        Assert.Equal(HigherTeam, state.ClaimingTeamId);
        Assert.Equal(3, state.CaptureProgress);
        Assert.Equal(0, state.DecayTicksElapsed);
    }

    [Fact]
    public void CaptureGainPhaseBeginsOnItsDeclaredTick()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 20,
            gain: 1,
            decayAmount: 0,
            decayInterval: 0,
            pause: 0,
            gainSchedule:
            [
                new("opening", startsAtTick: 0, gainPerSoleTeamTick: 1),
                new("late", startsAtTick: 300, gainPerSoleTeamTick: 2),
            ]);
        FrontlineControlState before = kernel.CreateInitialState(299);

        FrontlineControlStepResult opening =
            kernel.ApplyJointTick(before, tick: 299, [HigherTeam]);
        FrontlineControlStepResult late =
            kernel.ApplyJointTick(opening.State, tick: 300, [HigherTeam]);

        Assert.Equal(1, opening.State.CaptureProgress);
        Assert.Equal(3, late.State.CaptureProgress);
    }

    [Fact]
    public void EdgeCaptureBreachesAndProducesCanonicalStandings()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 5,
            gain: 3,
            decayAmount: 1,
            decayInterval: 1,
            pause: 0);
        FrontlineControlState state = kernel.CreateInitialState() with
        {
            ActivePositionIndex = 4,
            ClaimingTeamId = HigherTeam,
            CaptureProgress = 2,
        };

        FrontlineControlStepResult result = kernel.ApplyJointTick(
            state,
            tick: 0,
            [HigherTeam]);
        var breached =
            Assert.IsType<FrontlineBaseBreached>(result.Transition);
        TeamStandings standings = kernel.ResolveBreachStandings(
            result.State,
            [LowerTeam, HigherTeam]);

        Assert.Equal(HigherTeam, breached.TeamId);
        Assert.Equal(HigherTeam, result.State.WinnerTeamId);
        Assert.Equal(HigherTeam, standings.WinnerTeamId);
        Assert.Equal(
            TeamStandingOutcome.Win,
            standings.Standings.Single(team =>
                team.TeamId == HigherTeam).Outcome);
        Assert.Throws<InvalidOperationException>(() =>
            kernel.ApplyJointTick(
                result.State,
                tick: 1,
                [HigherTeam]));
    }

    [Fact]
    public void TimeoutRankingSupportsTiesAndEligibility()
    {
        FrontlineModeKernel kernel = Kernel(
            threshold: 10,
            gain: 2,
            decayAmount: 0,
            decayInterval: 0,
            pause: 0);
        FrontlineControlState neutral = kernel.CreateInitialState();
        TeamStandings tied = kernel.ResolveTimeoutStandings(
            neutral,
            [LowerTeam, HigherTeam]);

        Assert.Null(tied.WinnerTeamId);
        Assert.All(tied.Standings, team =>
        {
            Assert.Equal(1, team.Rank);
            Assert.Equal(TeamStandingOutcome.Draw, team.Outcome);
        });

        FrontlineControlState claimed = kernel.ApplyJointTick(
            neutral,
            tick: 0,
            [HigherTeam]).State;
        TeamStandings scored = kernel.ResolveTimeoutStandings(
            claimed,
            [LowerTeam, HigherTeam]);
        Assert.Equal(HigherTeam, scored.WinnerTeamId);

        TeamStandings eligibility = kernel.ResolveTimeoutStandings(
            claimed,
            [LowerTeam]);
        Assert.Equal(LowerTeam, eligibility.WinnerTeamId);
        Assert.Equal(
            2,
            eligibility.Standings.Single(team =>
                team.TeamId == HigherTeam).Rank);
    }

    [Fact]
    public void RejectsMismatchedBindingAndNonSetPresence()
    {
        PublicMatchTopology topology = Topology();
        FrontlineGameModeDefinition mode = Mode();
        var mismatched = new FrontlineActorModeMapBindingDefinition(
            ObjectiveIds(),
            [
                new FrontlineTeamAdvanceDefinition(
                    LowerTeam,
                    FrontlineTeamAdvanceDefinition
                        .ObjectiveAdvanceDirection.TowardLowerIndex),
                new FrontlineTeamAdvanceDefinition(
                    99,
                    FrontlineTeamAdvanceDefinition
                        .ObjectiveAdvanceDirection.TowardHigherIndex),
            ]);

        Assert.Throws<ArgumentException>(() =>
            new FrontlineModeKernel(topology, mode, mismatched));

        FrontlineModeKernel kernel = Kernel();
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                tick: 0,
                [HigherTeam, HigherTeam]));
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                tick: 0,
                [99]));
    }

    private static FrontlineModeKernel Kernel(
        int threshold = 5,
        int gain = 2,
        int decayAmount = 1,
        int decayInterval = 2,
        int pause = 0,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            gainSchedule = null) =>
        new(
            Topology(),
            Mode(
                threshold,
                gain,
                decayAmount,
                decayInterval,
                pause,
                gainSchedule),
            new FrontlineActorModeMapBindingDefinition(
                ObjectiveIds(),
                [
                    new FrontlineTeamAdvanceDefinition(
                        LowerTeam,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardLowerIndex),
                    new FrontlineTeamAdvanceDefinition(
                        HigherTeam,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                ]));

    private static PublicMatchTopology Topology() =>
        new()
        {
            Teams =
            [
                new PublicScoringTeam(HigherTeam),
                new PublicScoringTeam(LowerTeam),
            ],
            Participants = ImmutableArray<PublicParticipant>.Empty,
            UnitSlots = ImmutableArray<PublicUnitSlot>.Empty,
            InitialLives = ImmutableArray<PublicInitialLife>.Empty,
        };

    private static FrontlineGameModeDefinition Mode(
        int threshold = 5,
        int gain = 2,
        int decayAmount = 1,
        int decayInterval = 2,
        int pause = 0,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            gainSchedule = null) =>
        new(
            new FrontlineVictoryDefinition(
                pushesToBreach: 3,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind
                        .TerritorialProgress),
            ],
            frontlinePositionCount: 5,
            new FrontlineCaptureDefinition(
                threshold,
                gain,
                decayAmount,
                decayInterval,
                pause,
                gainSchedule));

    private static string[] ObjectiveIds() =>
        ["front-0", "front-1", "front-2", "front-3", "front-4"];
}
