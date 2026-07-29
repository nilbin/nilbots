using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The ratchet hold stops being inferred and starts being published
/// (DECISIONS #169). Two facts join the Frontline mode observation beside
/// <c>ControlResumesAtTick</c>: which team's advance is protected, and the
/// tick the protection lifts.
///
/// <para>#156's lesson applies exactly as it did to the automatic return —
/// a new authoritative fact has to be taught to the validator, and the
/// validator must then refuse every history that merely looks consistent. The
/// three forgeries worth naming are a hold INVENTED on a tick that has none
/// (which would let a bot claim ground it never took), a hold whose OWNER is
/// the other team (the exact fact that previously had no derivation at all,
/// so the exact fact a forger would reach for), and a hold whose EXPIRY is
/// stretched (a longer window than the contract sells).</para>
/// </summary>
public sealed class FrontlineRatchetHoldObservabilityTests
{
    /// <summary>Centre objective (index 2) of the classes map.</summary>
    private static readonly Position Objective = new(11, 7);

    [Fact]
    public void APublishedHoldNamesItsOwnerAndTheTickItLifts()
    {
        GenericActorMatchChronology chronology = KeelAdvance();
        (int Tick, GenericActorRuntimeObservation.ModeObservationState.Frontline
            Mode) advance = FirstHold(chronology);

        Assert.Equal(0, advance.Mode.HoldOwnerTeamId);
        // The hold runs the declared duration from the advance tick, and the
        // clock names the tick it LIFTS — the same grammar
        // ControlResumesAtTick uses, so a bot compares both the same way.
        Assert.Equal(
            advance.Tick + FrontlineLabsDefinition.RatchetHoldTicksDefault + 1,
            advance.Mode.HoldEndsAtTick);

        // Every actor sees the same frozen boundary, the loser included: the
        // ownership fact is exactly what a life on the wrong side of a hold
        // could not derive at all before.
        GenericActorMatchTickFrame frame = chronology.Ticks
            .Single(item => item.Tick == advance.Tick + 1);
        Assert.All(
            frame.ActorTurns,
            turn => Assert.Equal(
                advance.Mode,
                turn.Observation.Mode));
        Assert.Contains(
            frame.ActorTurns,
            turn => turn.ActorId.TeamId != advance.Mode.HoldOwnerTeamId);
    }

    /// <summary>
    /// The hold is published only while it BINDS, so its last tick and its
    /// expiry are both observable, and the lapse is a control change like any
    /// other — which is why it arrives as an ordinary mode-changed fact rather
    /// than as something a bot has to notice by watching a number stop.
    /// </summary>
    [Fact]
    public void TheHoldIsPublishedWhileItBindsAndItsLapseIsAModeChange()
    {
        GenericActorMatchChronology chronology = KeelAdvance();
        (int Tick, GenericActorRuntimeObservation.ModeObservationState.Frontline
            Mode) advance = FirstHold(chronology);
        int endsAt = advance.Mode.HoldEndsAtTick!.Value;

        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            var mode = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.Frontline>(
                frame.TickStart.State.Mode);
            bool binds = frame.Tick > advance.Tick && frame.Tick < endsAt;
            Assert.Equal(
                binds ? 0 : (int?)null,
                mode.HoldOwnerTeamId);
            Assert.Equal(
                binds ? endsAt : (int?)null,
                mode.HoldEndsAtTick);
        }

        // The lapse tick publishes exactly one mode change, and its payload is
        // the post-change state: no hold.
        GenericActorMatchTickFrame lapse = chronology.Ticks
            .Single(frame => frame.Tick == endsAt - 1);
        GenericActorRuntimeObservation.ModeObservationState.Frontline lapsed =
            Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.Frontline>(
                Assert.Single(
                    lapse.Events
                        .Where(item => item.Kind
                            == GenericActorRuntimeObservation.EventKind
                                .ModeChanged)
                        .Select(item =>
                            ((GenericActorRuntimeObservation.EventPayload
                                .ModeChanged)item.Payload).State)));
        Assert.Null(lapsed.HoldOwnerTeamId);
        Assert.Null(lapsed.HoldEndsAtTick);
    }

    [Fact]
    public void ARulesetWithoutARatchetNeverPublishesAHold()
    {
        GenericActorMatchChronology chronology = Advance(Anchor());

        Assert.All(
            chronology.Ticks,
            frame =>
            {
                var mode = Assert.IsType<
                    GenericActorRuntimeObservation.ModeObservationState
                        .Frontline>(frame.TickStart.State.Mode);
                Assert.Null(mode.HoldOwnerTeamId);
                Assert.Null(mode.HoldEndsAtTick);
            });
        // ...and the arm really did advance, so the null is a fact about the
        // policy rather than about a match where nothing happened.
        Assert.Contains(
            chronology.Ticks,
            frame => Assert.IsType<
                    GenericActorRuntimeObservation.ModeObservationState
                        .Frontline>(frame.PostState.Mode)
                .ActivePositionIndex != 2);
    }

    [Fact]
    public void TheChronologyRefusesAHoldForgedOntoATickThatHasNone()
    {
        GenericActorMatchChronology chronology = KeelAdvance();
        (int Tick, GenericActorRuntimeObservation.ModeObservationState.Frontline
            Mode) advance = FirstHold(chronology);
        // The tick BEFORE the advance: no hold exists there at all.
        GenericActorMatchTickFrame frame = chronology.Ticks
            .Single(item => item.Tick == advance.Tick - 1);
        var honest = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.Frontline>(
            frame.PostState.Mode);
        Assert.Null(honest.HoldOwnerTeamId);

        Assert.Contains(
            "authoritative objective kernel",
            Rebuild(
                chronology,
                frame,
                WithHold(honest, 0, frame.Tick + 40)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheChronologyRefusesAHoldCreditedToTheWrongTeam()
    {
        GenericActorMatchChronology chronology = KeelAdvance();
        (int Tick, GenericActorRuntimeObservation.ModeObservationState.Frontline
            Mode) advance = FirstHold(chronology);
        GenericActorMatchTickFrame frame = chronology.Ticks
            .Single(item => item.Tick == advance.Tick);

        Assert.Contains(
            "authoritative objective kernel",
            Rebuild(
                chronology,
                frame,
                WithHold(
                    advance.Mode,
                    1,
                    advance.Mode.HoldEndsAtTick)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheChronologyRefusesAHoldWhoseExpiryWasStretched()
    {
        GenericActorMatchChronology chronology = KeelAdvance();
        (int Tick, GenericActorRuntimeObservation.ModeObservationState.Frontline
            Mode) advance = FirstHold(chronology);
        GenericActorMatchTickFrame frame = chronology.Ticks
            .Single(item => item.Tick == advance.Tick);

        Assert.Contains(
            "authoritative objective kernel",
            Rebuild(
                chronology,
                frame,
                WithHold(
                    advance.Mode,
                    advance.Mode.HoldOwnerTeamId,
                    advance.Mode.HoldEndsAtTick + 1)).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A hold handed to one actor's observation but not to the world boundary
    /// is the cheapest lie available — every published fact still exists and
    /// only one bot's copy differs.
    /// </summary>
    [Fact]
    public void TheChronologyRefusesAHoldShownToOneActorOnly()
    {
        GenericActorMatchChronology chronology = KeelAdvance();
        (int Tick, GenericActorRuntimeObservation.ModeObservationState.Frontline
            Mode) advance = FirstHold(chronology);
        GenericActorMatchTickFrame frame = chronology.Ticks
            .Single(item => item.Tick == advance.Tick + 1);
        GenericActorMatchActorTurn turn = frame.ActorTurns[0];

        var edited = new GenericActorMatchTickFrame(
            frame.TickStart,
            [
                new GenericActorMatchActorTurn(
                    frame.Tick,
                    turn.ParticipantId,
                    turn.ActorId,
                    turn.Observation with
                    {
                        Mode = WithHold(
                            advance.Mode,
                            1,
                            advance.Mode.HoldEndsAtTick),
                    },
                    turn.SubmittedDecision,
                    turn.ActionResolution),
                .. frame.ActorTurns.Skip(1),
            ],
            frame.Events,
            frame.Traversals,
            frame.PostState);

        Assert.Contains(
            "frozen public Frontline mode",
            Assert.Throws<ArgumentException>(() =>
                new GenericActorMatchChronology(
                    chronology.Descriptor,
                    chronology.InitialFrame,
                    [
                        .. chronology.Ticks.Select(item =>
                            item.Tick == frame.Tick ? edited : item),
                    ],
                    result: null)).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes one forged mode state everywhere a consistent history would
    /// carry it — the tick's own post state, the next tick's frozen boundary,
    /// and that tick's per-actor observations — and rebuilds. Forging only one
    /// of the three trips the cheaper stability guards instead, so the
    /// refusal under test is the kernel re-derivation and nothing weaker.
    /// </summary>
    private static ArgumentException Rebuild(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        GenericActorRuntimeObservation.ModeObservationState mode)
    {
        var edited = new GenericActorMatchTickFrame(
            frame.TickStart,
            frame.ActorTurns,
            frame.Events,
            frame.Traversals,
            WithMode(frame.PostState, mode));
        GenericActorMatchTickFrame? next = chronology.Ticks
            .FirstOrDefault(item => item.Tick == frame.Tick + 1);
        GenericActorMatchTickFrame? editedNext = next is null
            ? null
            : new GenericActorMatchTickFrame(
                new GenericActorMatchTickStart(
                    next.TickStart.Tick,
                    WithMode(next.TickStart.State, mode),
                    next.TickStart.ActiveActorIds,
                    next.TickStart.LifeStarts,
                    next.TickStart.Events,
                    next.TickStart.Traversals),
                [
                    .. next.ActorTurns.Select(turn =>
                        new GenericActorMatchActorTurn(
                            next.Tick,
                            turn.ParticipantId,
                            turn.ActorId,
                            turn.Observation with { Mode = mode },
                            turn.SubmittedDecision,
                            turn.ActionResolution)),
                ],
                next.Events,
                next.Traversals,
                next.PostState);
        return Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [
                    .. chronology.Ticks.Select(item =>
                        item.Tick == frame.Tick
                            ? edited
                            : editedNext is not null
                                && item.Tick == editedNext.Tick
                                ? editedNext
                                : item),
                ],
                result: null));
    }

    /// <summary>
    /// The same control facts with one forged hold. The observation record's
    /// hold clocks are constructor-set, so a forgery has to be built rather
    /// than patched — which is also why the honest path cannot half-set them.
    /// </summary>
    private static GenericActorRuntimeObservation.ModeObservationState.Frontline
        WithHold(
            GenericActorRuntimeObservation.ModeObservationState.Frontline mode,
            int? holdOwnerTeamId,
            int? holdEndsAtTick) =>
        new(
            mode.ModeId,
            mode.ActivePositionIndex,
            mode.ClaimingTeamId,
            mode.CaptureProgress,
            mode.DecayTicksElapsed,
            mode.ControlResumesAtTick,
            holdOwnerTeamId,
            holdEndsAtTick);

    private static GenericActorWorldSnapshot WithMode(
        GenericActorWorldSnapshot state,
        GenericActorRuntimeObservation.ModeObservationState mode) =>
        new(
            KeelArm(),
            state.NextTick,
            state.NextProjectileId,
            state.Participants,
            state.Slots,
            state.ActiveLives,
            state.PendingReplications,
            state.Projectiles,
            state.Scoreboard,
            mode);

    /// <summary>
    /// The first tick whose post state publishes a hold, with that state.
    /// </summary>
    private static (
        int Tick,
        GenericActorRuntimeObservation.ModeObservationState.Frontline Mode)
        FirstHold(GenericActorMatchChronology chronology)
    {
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            if (frame.PostState.Mode
                    is GenericActorRuntimeObservation.ModeObservationState
                        .Frontline { HoldOwnerTeamId: not null } mode)
            {
                return (frame.Tick, mode);
            }
        }
        throw new InvalidOperationException(
            "The scripted keel match never completed an advance.");
    }

    private static GenericActorMatchChronology KeelAdvance() =>
        Advance(KeelArm());

    /// <summary>
    /// One team walks its prime onto the centre objective and stands there
    /// while the other never contests, so sole control completes a capture and
    /// the ratchet engages. A mirrored pair of real doctrines deadlocks, which
    /// is precisely the history in which no hold is ever observable.
    /// </summary>
    private static GenericActorMatchChronology Advance(
        ActorResolvedMatchDefinition definition) =>
        FrontlineLabsSkillArmTestFixture.Run(
            definition,
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0)
                {
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                return FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        Objective)
                    ?? GenericDeathmatchSessionTestFixture.Wait();
            });

    private static ActorResolvedMatchDefinition KeelArm() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally
                | FrontlineLabsPendulumArm.ContestMajority
                | FrontlineLabsPendulumArm.EnemySoleDecay,
            (FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked);

    private static ActorResolvedMatchDefinition Anchor() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.ContestMajority,
            (FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked);
}
