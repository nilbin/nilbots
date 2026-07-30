using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The capture channel's arithmetic, pinned against the memo's derived
/// numbers (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §P2.4/§P3.2–P3.5,
/// DECISIONS #187): stillness gates gain and not denial, the interrupt reverts
/// run-work by the damage amount, the multiplier stops at two, erosion runs at
/// four, and a full flip costs 10 ticks against a fresh capture's 8.
/// </summary>
public sealed class FrontlineCaptureChannelKernelTests
{
    private const int Attacker = 0;
    private const int Defender = 1;
    private const int Threshold = 8;

    /// <summary>
    /// The headline number: a solo channeler whose screens keep every bolt
    /// off it gains 1 per tick and takes the point on the eighth. It has to
    /// fit inside the 18-tick Prime automatic return with room for the
    /// approach, which is where 8 came from.
    /// </summary>
    [Fact]
    public void AScreenedStationaryChannelerCompletesInEightTicks()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState state = kernel.CreateInitialState();

        for (int tick = 0; tick < Threshold - 1; tick++)
        {
            state = Step(kernel, state, tick, stationary: 1).State;
            Assert.Equal(Attacker, state.ClaimingTeamId);
            Assert.Equal(tick + 1, state.CaptureProgress);
        }

        FrontlineControlStepResult completed = Step(
            kernel,
            state,
            Threshold - 1,
            stationary: 1);
        Assert.IsType<FrontlinePositionAdvanced>(completed.Transition);
        Assert.Null(completed.State.ClaimingTeamId);
        // A completed push ends the run, so the next controller starts from
        // the reset number rather than inheriting a stale floor.
        Assert.Null(completed.State.ChannelRun);
    }

    /// <summary>
    /// The gate itself. A body that changed tile is still ON the objective —
    /// it still denies — but it adds nothing to the claim that tick.
    /// </summary>
    [Fact]
    public void AChannelerThatChangedTileContributesNothingThatTick()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState held = Step(
            kernel,
            kernel.CreateInitialState(),
            tick: 0,
            stationary: 1).State;

        // Same body, same tile occupancy, but it stepped: denial weight 1,
        // claim weight 0, so nobody controls and the claim stands still.
        FrontlineControlStepResult moved = kernel.ApplyJointTick(
            held,
            1,
            new FrontlineObjectivePresence(
                Weights((Attacker, 1)),
                ImmutableSortedDictionary<int, int>.Empty,
                ImmutableSortedDictionary<int, long>.Empty),
            ImmutableDictionary<int, int>.Empty);

        Assert.Equal(1, held.CaptureProgress);
        Assert.Equal(Attacker, moved.State.ClaimingTeamId);
        Assert.Equal(1, moved.State.CaptureProgress);
        Assert.Null(moved.State.ChannelRun);
    }

    /// <summary>
    /// Denial does NOT require stillness. One kiting defender cancels one
    /// stationary channeler outright — claim 1 against denial 1 is not
    /// strictly greater — and the memo's headline case falls out of the same
    /// rule: three stationary attackers against two kiting defenders control
    /// at multiplier ONE, not three, so their +1 a tick loses to two guns
    /// putting ~1.2 a tick back onto the point. Two hold three.
    /// </summary>
    [Fact]
    public void KitingDefendersDenyWithoutHoldingTheirTile()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlStepResult cancelled = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            0,
            new FrontlineObjectivePresence(
                Weights((Attacker, 1), (Defender, 1)),
                Weights((Attacker, 1)),
                ImmutableSortedDictionary<int, long>.Empty),
            ImmutableDictionary<int, int>.Empty);
        Assert.Null(cancelled.State.ClaimingTeamId);
        Assert.Equal(0, cancelled.State.CaptureProgress);

        // 3 stationary against 2 kiting: surplus one, so gain one.
        FrontlineControlState pushed = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            0,
            new FrontlineObjectivePresence(
                Weights((Attacker, 3), (Defender, 2)),
                Weights((Attacker, 3)),
                ImmutableSortedDictionary<int, long>.Empty),
            ImmutableDictionary<int, int>.Empty).State;
        Assert.Equal(Attacker, pushed.ClaimingTeamId);
        Assert.Equal(1, pushed.CaptureProgress);

        // And the two defenders' guns take it straight back off.
        FrontlineControlState held = kernel.ApplyJointTick(
            pushed,
            1,
            new FrontlineObjectivePresence(
                Weights((Attacker, 3), (Defender, 2)),
                Weights((Attacker, 3)),
                new Dictionary<int, long> { [Attacker] = 2 }),
            ImmutableDictionary<int, int>.Empty).State;
        Assert.Null(held.ClaimingTeamId);
        Assert.Equal(0, held.CaptureProgress);
    }

    /// <summary>
    /// Damage to a controlling body ON the objective reverts the run's work
    /// point for point; damage to a screen OFF it reverts nothing, which is
    /// the single scoping choice the escort pattern rests on. The kernel sees
    /// the scoping as a number that is or is not there.
    /// </summary>
    [Fact]
    public void OnObjectiveDamageRevertsRunWorkAndOffObjectiveDamageDoesNot()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState state = kernel.CreateInitialState();
        for (int tick = 0; tick < 4; tick++)
            state = Step(kernel, state, tick, stationary: 1).State;
        Assert.Equal(4, state.CaptureProgress);

        // Gain lands first, then the revert: +1 then -2 is a net -1.
        FrontlineControlState hit =
            Step(kernel, state, 4, stationary: 1, damage: 2).State;
        Assert.Equal(3, hit.CaptureProgress);

        // The identical tick with the bolts absorbed off the objective is a
        // clean +1: the screen paid nothing.
        FrontlineControlState screened =
            Step(kernel, state, 4, stationary: 1).State;
        Assert.Equal(5, screened.CaptureProgress);
    }

    /// <summary>
    /// The floor. A run can be undone completely and never further — a full
    /// revert restores the position exactly as the controller found it, which
    /// is what makes it impossible for being shot to complete a capture for
    /// the team doing the shooting.
    /// </summary>
    [Fact]
    public void TheRevertNeverGoesBelowTheRunStart()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState state = kernel.CreateInitialState();
        for (int tick = 0; tick < 3; tick++)
            state = Step(kernel, state, tick, stationary: 1).State;
        Assert.Equal(3, state.CaptureProgress);

        // A tick nobody controls ends the run, so the next controller finds
        // the number at 3 and that becomes its floor.
        state = kernel.ApplyJointTick(
            state,
            3,
            new FrontlineObjectivePresence(
                Weights((Attacker, 1), (Defender, 1)),
                Weights((Attacker, 1)),
                ImmutableSortedDictionary<int, long>.Empty),
            ImmutableDictionary<int, int>.Empty).State;
        Assert.Null(state.ChannelRun);

        state = Step(kernel, state, 4, stationary: 1).State;
        Assert.Equal(4, state.CaptureProgress);

        // Enough damage to erase far more than this run has done.
        FrontlineControlState hammered =
            Step(kernel, state, 5, stationary: 1, damage: 99).State;
        Assert.Equal(3, hammered.CaptureProgress);
        Assert.Equal(Attacker, hammered.ClaimingTeamId);
    }

    /// <summary>
    /// A hit on the tick a capture would have completed denies it. The gain
    /// lands first and the revert second, deliberately: poke delays,
    /// sustained control denies.
    /// </summary>
    [Fact]
    public void AHitOnTheCompletingTickDeniesTheCapture()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState state = kernel.CreateInitialState();
        for (int tick = 0; tick < Threshold - 1; tick++)
            state = Step(kernel, state, tick, stationary: 1).State;
        Assert.Equal(Threshold - 1, state.CaptureProgress);

        FrontlineControlStepResult denied = Step(
            kernel,
            state,
            Threshold - 1,
            stationary: 1,
            damage: 1);
        Assert.Null(denied.Transition);
        Assert.Equal(Threshold - 1, denied.State.CaptureProgress);
    }

    /// <summary>
    /// Stacking pays, and stops paying. Two stationary channelers take a
    /// point in four ticks; a third buys nothing at all.
    /// </summary>
    [Fact]
    public void TwoStackedChannelersGainTwoAndAThirdBuysNothing()
    {
        FrontlineModeKernel kernel = Channel();
        Assert.Equal(
            2,
            Step(
                kernel,
                kernel.CreateInitialState(),
                0,
                stationary: 2).State.CaptureProgress);
        Assert.Equal(
            2,
            Step(
                kernel,
                kernel.CreateInitialState(),
                0,
                stationary: 3).State.CaptureProgress);
        Assert.Equal(
            2,
            Step(
                kernel,
                kernel.CreateInitialState(),
                0,
                stationary: 5).State.CaptureProgress);

        // Four ticks at the cap, not eight at gain one.
        FrontlineControlState state = kernel.CreateInitialState();
        for (int tick = 0; tick < 3; tick++)
            state = Step(kernel, state, tick, stationary: 2).State;
        Assert.Equal(6, state.CaptureProgress);
        Assert.IsType<FrontlinePositionAdvanced>(
            Step(kernel, state, 3, stationary: 2).Transition);
    }

    /// <summary>
    /// Recapture, priced. A maximal standing enemy claim (7 at threshold 8)
    /// erodes at 4 per stationary sole tick, clears on the second without
    /// starting a claim of its own, and the flip completes on tick 10 — 1.25×
    /// a fresh capture, the top of the owner's stated band.
    /// </summary>
    [Fact]
    public void AFullFlipCostsTenTicksAgainstAFreshCapturesEight()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState state = kernel.CreateInitialState() with
        {
            ClaimingTeamId = Defender,
            CaptureProgress = Threshold - 1,
        };

        FrontlineControlState first =
            Step(kernel, state, 0, stationary: 1).State;
        Assert.Equal(Defender, first.ClaimingTeamId);
        Assert.Equal(3, first.CaptureProgress);

        // Overshoot is discarded and the controller starts no claim on the
        // crossing tick: the documented invariant, preserved.
        FrontlineControlState cleared =
            Step(kernel, first, 1, stationary: 1).State;
        Assert.Null(cleared.ClaimingTeamId);
        Assert.Equal(0, cleared.CaptureProgress);

        state = cleared;
        for (int tick = 2; tick < 9; tick++)
        {
            state = Step(kernel, state, tick, stationary: 1).State;
            Assert.Equal(Attacker, state.ClaimingTeamId);
            Assert.Equal(tick - 1, state.CaptureProgress);
        }

        Assert.IsType<FrontlinePositionAdvanced>(
            Step(kernel, state, 9, stationary: 1).Transition);
    }

    /// <summary>
    /// Eroding is a channel too, and an interrupted eroder pays for it — but
    /// the enemy claim can only climb back to where the run found it, never
    /// past. Reverting WORK rather than the raw claim is what guarantees that.
    /// </summary>
    [Fact]
    public void AnInterruptedEroderLosesItsWorkAndNeverRestoresMore()
    {
        FrontlineModeKernel kernel = Channel();
        FrontlineControlState state = kernel.CreateInitialState() with
        {
            ClaimingTeamId = Defender,
            CaptureProgress = 6,
        };

        FrontlineControlState eroded =
            Step(kernel, state, 0, stationary: 1).State;
        Assert.Equal(Defender, eroded.ClaimingTeamId);
        Assert.Equal(2, eroded.CaptureProgress);

        // +4 erosion, then a fan bolt for 2 reverted: net +2 of work.
        FrontlineControlState interrupted =
            Step(kernel, eroded, 1, stationary: 1, damage: 2).State;
        Assert.Equal(Defender, interrupted.ClaimingTeamId);
        Assert.Equal(2, interrupted.CaptureProgress);

        // Everything the run has ever done, reverted at once: the claim is
        // back at 6 and stops there no matter how much more lands.
        FrontlineControlState hammered =
            Step(kernel, interrupted, 2, stationary: 1, damage: 50).State;
        Assert.Equal(Defender, hammered.ClaimingTeamId);
        Assert.Equal(6, hammered.CaptureProgress);
    }

    /// <summary>
    /// The compatibility story, stated exactly. With every body stationary,
    /// the channel policy at an unreachable cap and an erosion multiple of
    /// one is byte-for-byte contest-majority: same claimant, same progress,
    /// same clocks, same transitions, tick for tick, over a script that
    /// builds, erodes, contests, and captures.
    /// </summary>
    [Fact]
    public void EveryoneStationaryWithoutTheCapOrTheMultipleIsTodaysArithmetic()
    {
        FrontlineModeKernel channel = Channel(
            stationaryGainMultiplierCap: 64,
            opposingErosionMultiplier: 1);
        FrontlineModeKernel today = Kernel(
            FrontlineCaptureDefinition.ControlPolicyKind
                .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral);
        FrontlineControlState channelState = channel.CreateInitialState();
        FrontlineControlState todayState = today.CreateInitialState();

        (int Attackers, int Defenders)[] script =
        [
            (1, 0), (1, 0), (2, 0), (0, 1), (0, 3), (0, 3),
            (2, 2), (3, 1), (1, 0), (0, 0), (3, 0), (3, 0),
            (1, 0), (2, 1), (0, 2), (0, 2), (4, 0), (1, 1),
        ];
        for (int tick = 0; tick < script.Length; tick++)
        {
            (int attackers, int defenders) = script[tick];
            FrontlineControlStepResult channelStep = channel.ApplyJointTick(
                channelState,
                tick,
                new FrontlineObjectivePresence(
                    Weights((Attacker, attackers), (Defender, defenders)),
                    Weights((Attacker, attackers), (Defender, defenders)),
                    ImmutableSortedDictionary<int, long>.Empty),
                ImmutableDictionary<int, int>.Empty);
            FrontlineControlStepResult todayStep = today.ApplyJointTick(
                todayState,
                tick,
                Weights((Attacker, attackers), (Defender, defenders)));

            Assert.Equal(
                todayStep.State with { ChannelRun = null },
                channelStep.State with { ChannelRun = null });
            Assert.Equal(todayStep.Transition, channelStep.Transition);
            channelState = channelStep.State;
            todayState = todayStep.State;
        }
    }

    /// <summary>
    /// And the two places it deliberately departs, so the boundary is pinned
    /// rather than glossed: the shipped cap of two (a 3v0 wipe gains 2, not
    /// 3) and the shipped erosion multiple of four.
    /// </summary>
    [Fact]
    public void TheCapAndTheMultipleAreTheOnlyDeparturesFromTodaysArithmetic()
    {
        FrontlineModeKernel channel = Channel();
        FrontlineModeKernel today = Kernel(
            FrontlineCaptureDefinition.ControlPolicyKind
                .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral);

        Assert.Equal(
            3,
            today.ApplyJointTick(
                today.CreateInitialState(),
                0,
                Weights((Attacker, 3))).State.CaptureProgress);
        Assert.Equal(
            2,
            Step(channel, channel.CreateInitialState(), 0, stationary: 3)
                .State.CaptureProgress);

        FrontlineControlState standing = channel.CreateInitialState() with
        {
            ClaimingTeamId = Defender,
            CaptureProgress = 6,
        };
        Assert.Equal(
            5,
            today.ApplyJointTick(
                standing with { ChannelRun = null },
                0,
                Weights((Attacker, 1))).State.CaptureProgress);
        Assert.Equal(
            2,
            Step(channel, standing, 0, stationary: 1)
                .State.CaptureProgress);
    }

    /// <summary>
    /// A channel reading and a channel policy travel together. Handing a
    /// plain presence to a channel — or a channel reading to a policy that
    /// would ignore it — is a caller error, not a silent downgrade.
    /// </summary>
    [Fact]
    public void AReadingAndAPolicyMustAgree()
    {
        FrontlineModeKernel channel = Channel();
        Assert.Throws<ArgumentException>(() =>
            channel.ApplyJointTick(
                channel.CreateInitialState(),
                0,
                Weights((Attacker, 1))));

        FrontlineModeKernel today = Kernel(
            FrontlineCaptureDefinition.ControlPolicyKind
                .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral);
        Assert.Throws<ArgumentException>(() =>
            today.ApplyJointTick(
                today.CreateInitialState(),
                0,
                new FrontlineObjectivePresence(
                    Weights((Attacker, 1)),
                    Weights((Attacker, 1)),
                    ImmutableSortedDictionary<int, long>.Empty),
                ImmutableDictionary<int, int>.Empty));
    }

    /// <summary>
    /// Claim weight is a subset of a team's own denial weight by
    /// construction, so a reading that claims more stillness than presence is
    /// refused rather than resolved.
    /// </summary>
    [Fact]
    public void StillnessCannotExceedPresence()
    {
        FrontlineModeKernel channel = Channel();
        Assert.Throws<ArgumentException>(() =>
            channel.ApplyJointTick(
                channel.CreateInitialState(),
                0,
                new FrontlineObjectivePresence(
                    Weights((Attacker, 1)),
                    Weights((Attacker, 2)),
                    ImmutableSortedDictionary<int, long>.Empty),
                ImmutableDictionary<int, int>.Empty));
    }

    private static FrontlineControlStepResult Step(
        FrontlineModeKernel kernel,
        FrontlineControlState state,
        int tick,
        int stationary,
        long damage = 0) =>
        kernel.ApplyJointTick(
            state,
            tick,
            new FrontlineObjectivePresence(
                Weights((Attacker, stationary)),
                Weights((Attacker, stationary)),
                damage == 0
                    ? ImmutableSortedDictionary<int, long>.Empty
                    : new Dictionary<int, long> { [Attacker] = damage }),
            ImmutableDictionary<int, int>.Empty);

    private static ImmutableSortedDictionary<int, int> Weights(
        params (int TeamId, int Weight)[] entries) =>
        entries
            .Where(entry => entry.Weight > 0)
            .ToImmutableSortedDictionary(
                entry => entry.TeamId,
                entry => entry.Weight);

    private static FrontlineModeKernel Channel(
        int stationaryGainMultiplierCap =
            FrontlineLabsDefinition.ChannelStationaryGainMultiplierCap,
        int opposingErosionMultiplier =
            FrontlineLabsDefinition.ChannelOpposingErosionMultiplier) =>
        Kernel(
            FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds,
            stationaryGainMultiplierCap,
            opposingErosionMultiplier);

    private static FrontlineModeKernel Kernel(
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy,
        int stationaryGainMultiplierCap = 0,
        int opposingErosionMultiplier = 0) =>
        new(
            new PublicMatchTopology
            {
                Teams =
                [
                    new PublicScoringTeam(Attacker),
                    new PublicScoringTeam(Defender),
                ],
                Participants = ImmutableArray<PublicParticipant>.Empty,
                UnitSlots = ImmutableArray<PublicUnitSlot>.Empty,
                InitialLives = ImmutableArray<PublicInitialLife>.Empty,
            },
            new FrontlineGameModeDefinition(
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
                    threshold: Threshold,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 1,
                    decayIntervalTicks: 2,
                    redeployPauseTicks: 5,
                    gainSchedule: null,
                    controlPolicy,
                    FrontlineCaptureDefinition.DecayClockKind
                        .EmptyAndContestedTicksPreserveClaimEnemySoleErosionOnly,
                    FrontlineCaptureDefinition.RedeployPolicyKind
                        .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause,
                    ratchetHoldTicks: 0,
                    stationaryGainMultiplierCap,
                    opposingErosionMultiplier,
                    stationaryGainMultiplierCap > 0
                        ? FrontlineClaimInterruptDefinition.DamageRevertsWork
                        : null)),
            new FrontlineActorModeMapBindingDefinition(
                ["front-0", "front-1", "front-2", "front-3", "front-4"],
                [
                    new FrontlineTeamAdvanceDefinition(
                        Attacker,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                    new FrontlineTeamAdvanceDefinition(
                        Defender,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardLowerIndex),
                ]));
}
