using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the enemy-sole decay clock (N2 in
/// <c>docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md</c>): an empty or
/// contested objective stops destroying capture progress, so the only thing
/// that erodes a claim is an enemy body standing on the objective alone. The
/// baseline clock must keep decaying exactly as it always did.
/// </summary>
public sealed class FrontlineEnemySoleDecayTests
{
    private const int LowerTeam = 1;
    private const int HigherTeam = 0;

    [Fact]
    public void BaselineClockDecaysWhileTheObjectiveIsEmpty()
    {
        FrontlineModeKernel kernel = Kernel(enemySoleOnly: false);
        FrontlineControlState claimed = Claim(kernel, progress: 3);

        FrontlineControlState first = kernel
            .ApplyJointTick(claimed, claimed.NextTick, [])
            .State;
        FrontlineControlState second = kernel
            .ApplyJointTick(first, first.NextTick, [])
            .State;

        Assert.Equal(1, first.DecayTicksElapsed);
        Assert.Equal(3, first.CaptureProgress);
        Assert.Equal(0, second.DecayTicksElapsed);
        Assert.Equal(2, second.CaptureProgress);
    }

    [Fact]
    public void EnemySoleClockPreservesTheClaimOnAnEmptyObjective()
    {
        FrontlineModeKernel kernel = Kernel(enemySoleOnly: true);
        FrontlineControlState state = Claim(kernel, progress: 3);

        for (int tick = 0; tick < 10; tick++)
            state = kernel.ApplyJointTick(state, state.NextTick, []).State;

        Assert.Equal(HigherTeam, state.ClaimingTeamId);
        Assert.Equal(3, state.CaptureProgress);
        Assert.Equal(0, state.DecayTicksElapsed);
    }

    [Fact]
    public void EnemySoleClockPreservesTheClaimWhileContested()
    {
        FrontlineModeKernel kernel = Kernel(enemySoleOnly: true);
        FrontlineControlState state = Claim(kernel, progress: 3);

        for (int tick = 0; tick < 10; tick++)
        {
            state = kernel
                .ApplyJointTick(
                    state,
                    state.NextTick,
                    [HigherTeam, LowerTeam])
                .State;
        }

        Assert.Equal(HigherTeam, state.ClaimingTeamId);
        Assert.Equal(3, state.CaptureProgress);
        Assert.Equal(0, state.DecayTicksElapsed);
    }

    [Fact]
    public void OnlySoleEnemyPresenceErodesTheClaim()
    {
        FrontlineModeKernel kernel = Kernel(enemySoleOnly: true);
        FrontlineControlState state = Claim(kernel, progress: 3);

        state = kernel
            .ApplyJointTick(state, state.NextTick, [LowerTeam])
            .State;
        Assert.Equal(HigherTeam, state.ClaimingTeamId);
        Assert.Equal(2, state.CaptureProgress);

        state = kernel
            .ApplyJointTick(state, state.NextTick, [LowerTeam])
            .State;
        state = kernel
            .ApplyJointTick(state, state.NextTick, [LowerTeam])
            .State;

        Assert.Null(state.ClaimingTeamId);
        Assert.Equal(0, state.CaptureProgress);
    }

    [Fact]
    public void AnAdvancedDecayClockIsNotAdmissibleUnderTheEnemySoleClock()
    {
        FrontlineModeKernel kernel = Kernel(enemySoleOnly: true);
        FrontlineControlState invalid = kernel.CreateInitialState() with
        {
            ClaimingTeamId = HigherTeam,
            CaptureProgress = 2,
            DecayTicksElapsed = 1,
        };

        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(invalid, tick: 0, []));
    }

    private static FrontlineControlState Claim(
        FrontlineModeKernel kernel,
        int progress)
    {
        FrontlineControlState state = kernel.CreateInitialState();
        for (int tick = 0; tick < progress; tick++)
        {
            state = kernel
                .ApplyJointTick(state, state.NextTick, [HigherTeam])
                .State;
        }
        Assert.Equal(progress, state.CaptureProgress);
        return state;
    }

    private static FrontlineModeKernel Kernel(bool enemySoleOnly) =>
        new(
            Topology(),
            Mode(enemySoleOnly),
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

    private static FrontlineGameModeDefinition Mode(bool enemySoleOnly) =>
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
                threshold: 15,
                gainPerSoleTeamTick: 1,
                decayAmount: 1,
                decayIntervalTicks: 2,
                redeployPauseTicks: 0,
                gainSchedule: null,
                controlPolicy: FrontlineCaptureDefinition.ControlPolicyKind
                    .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
                decayClock: enemySoleOnly
                    ? FrontlineCaptureDefinition.DecayClockKind
                        .EmptyAndContestedTicksPreserveClaimEnemySoleErosionOnly
                    : FrontlineCaptureDefinition.DecayClockKind
                        .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl));

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
