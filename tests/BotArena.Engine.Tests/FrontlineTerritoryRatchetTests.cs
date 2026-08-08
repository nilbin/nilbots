using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the territory ratchet (S1 in
/// <c>docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md</c>): a completed advance
/// holds its position against the reinforcement wave it triggered, and the
/// enemy must pay the threshold again once the hold expires. The baseline
/// policy must keep retreating exactly as it always did.
/// </summary>
public sealed class FrontlineTerritoryRatchetTests
{
    private const int LowerTeam = 1;
    private const int HigherTeam = 0;

    [Fact]
    public void BaselineRedeployPolicyLetsTheFrontlineRetreatImmediately()
    {
        FrontlineModeKernel kernel = Kernel(ratchetHoldTicks: 0);
        FrontlineControlState advanced = Advance(kernel, HigherTeam);

        Assert.Equal(3, advanced.ActivePositionIndex);
        Assert.Null(advanced.RatchetHold);

        FrontlineControlState pushedBack = Advance(
            kernel,
            LowerTeam,
            advanced);

        Assert.Equal(2, pushedBack.ActivePositionIndex);
    }

    [Fact]
    public void AHeldAdvanceDeniesTheEnemyCaptureAndKeepsThePosition()
    {
        FrontlineModeKernel kernel = Kernel(ratchetHoldTicks: 40);
        FrontlineControlState advanced = Advance(kernel, HigherTeam);

        Assert.Equal(3, advanced.ActivePositionIndex);
        FrontlineRatchetHold hold =
            Assert.IsType<FrontlineRatchetHold>(advanced.RatchetHold);
        Assert.Equal(HigherTeam, hold.TeamId);
        Assert.Equal(3, hold.PositionIndex);
        Assert.Equal(advanced.NextTick - 1 + 40, hold.HoldsThroughTick);

        FrontlineControlState denied = Advance(
            kernel,
            LowerTeam,
            advanced);

        // The capture is spent, not converted: the objective holds, the claim
        // is gone, and nothing redeployed, so control never pauses.
        Assert.Equal(3, denied.ActivePositionIndex);
        Assert.Null(denied.ClaimingTeamId);
        Assert.Equal(0, denied.CaptureProgress);
        Assert.Equal(0, denied.DecayTicksElapsed);
        Assert.Equal(advanced.ControlResumesAtTick, denied.ControlResumesAtTick);
        Assert.Equal(hold, denied.RatchetHold);
    }

    [Fact]
    public void TheEnemyConvertsAgainOnceTheHoldExpires()
    {
        FrontlineModeKernel kernel = Kernel(ratchetHoldTicks: 6);
        FrontlineControlState state = Advance(kernel, HigherTeam);
        FrontlineRatchetHold hold =
            Assert.IsType<FrontlineRatchetHold>(state.RatchetHold);

        // Wait out the hold on an empty objective, then push.
        while (state.NextTick <= hold.HoldsThroughTick)
            state = kernel.ApplyJointTick(state, state.NextTick, []).State;

        FrontlineControlState pushedBack = Advance(kernel, LowerTeam, state);

        Assert.Equal(2, pushedBack.ActivePositionIndex);
        FrontlineRatchetHold enemyHold =
            Assert.IsType<FrontlineRatchetHold>(pushedBack.RatchetHold);
        Assert.Equal(LowerTeam, enemyHold.TeamId);
        Assert.Equal(2, enemyHold.PositionIndex);
    }

    [Fact]
    public void TheHoldingTeamKeepsAdvancingAndRearmsTheMarkForward()
    {
        FrontlineModeKernel kernel = Kernel(ratchetHoldTicks: 40);
        FrontlineControlState first = Advance(kernel, HigherTeam);
        FrontlineControlState second = Advance(kernel, HigherTeam, first);

        Assert.Equal(4, second.ActivePositionIndex);
        FrontlineRatchetHold hold =
            Assert.IsType<FrontlineRatchetHold>(second.RatchetHold);
        Assert.Equal(HigherTeam, hold.TeamId);
        Assert.Equal(4, hold.PositionIndex);
    }

    [Fact]
    public void ABreachIsTerminalAndIsNeverDeniedByAHold()
    {
        FrontlineModeKernel kernel = Kernel(ratchetHoldTicks: 40);
        FrontlineControlState state = Advance(kernel, HigherTeam);
        state = Advance(kernel, HigherTeam, state);

        Assert.Equal(4, state.ActivePositionIndex);

        FrontlineControlState breached = Advance(kernel, HigherTeam, state);

        Assert.Equal(HigherTeam, breached.WinnerTeamId);
    }

    [Fact]
    public void AHoldDurationBelongsToExactlyTheRatchetPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrontlineCaptureDefinition(
                threshold: 5,
                gainPerSoleTeamTick: 1,
                decayAmount: 1,
                decayIntervalTicks: 2,
                redeployPauseTicks: 0,
                ratchetHoldTicks: 12));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrontlineCaptureDefinition(
                threshold: 5,
                gainPerSoleTeamTick: 1,
                decayAmount: 1,
                decayIntervalTicks: 2,
                redeployPauseTicks: 0,
                redeployPolicy: FrontlineCaptureDefinition.RedeployPolicyKind
                    .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks,
                ratchetHoldTicks: 0));
    }

    [Fact]
    public void AHoldFromAnotherPolicyIsNotAnAdmissibleState()
    {
        FrontlineModeKernel baseline = Kernel(ratchetHoldTicks: 0);
        FrontlineControlState invalid = baseline.CreateInitialState() with
        {
            RatchetHold = new FrontlineRatchetHold(HigherTeam, 3, 40),
        };

        Assert.Throws<ArgumentException>(() =>
            baseline.ApplyJointTick(invalid, tick: 0, []));
    }

    /// <summary>
    /// Runs <paramref name="teamId"/> alone on the objective until it either
    /// converts a capture or is denied one, and returns the resulting state.
    /// </summary>
    private static FrontlineControlState Advance(
        FrontlineModeKernel kernel,
        int teamId,
        FrontlineControlState? from = null)
    {
        FrontlineControlState state = from ?? kernel.CreateInitialState();
        int startIndex = state.ActivePositionIndex;
        for (int step = 0; step < 64; step++)
        {
            FrontlineControlStepResult result = kernel.ApplyJointTick(
                state,
                state.NextTick,
                [teamId]);
            state = result.State;
            if (result.Transition is not null
                || state.ActivePositionIndex != startIndex
                || state.WinnerTeamId is not null)
            {
                return state;
            }
            if (state.ClaimingTeamId is null && state.CaptureProgress == 0
                && step > 0)
            {
                // A denied capture resets the claim without moving anything.
                return state;
            }
        }
        throw new InvalidOperationException(
            "The team never resolved a capture.");
    }

    private static FrontlineModeKernel Kernel(int ratchetHoldTicks) =>
        new(
            Topology(),
            Mode(ratchetHoldTicks),
            new FrontlineActorModeMapBindingDefinition(
                [
                    "front-0",
                    "front-1",
                    "front-2",
                    "front-3",
                    "front-4",
                ],
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

    private static FrontlineGameModeDefinition Mode(int ratchetHoldTicks) =>
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
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress),
            ],
            frontlinePositionCount: 5,
            new FrontlineCaptureDefinition(
                threshold: 3,
                gainPerSoleTeamTick: 1,
                decayAmount: 1,
                decayIntervalTicks: 2,
                redeployPauseTicks: 0,
                gainSchedule: null,
                controlPolicy: FrontlineCaptureDefinition.ControlPolicyKind
                    .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
                decayClock: FrontlineCaptureDefinition.DecayClockKind
                    .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl,
                redeployPolicy: ratchetHoldTicks == 0
                    ? FrontlineCaptureDefinition.RedeployPolicyKind
                        .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause
                    : FrontlineCaptureDefinition.RedeployPolicyKind
                        .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks,
                ratchetHoldTicks));

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
}
