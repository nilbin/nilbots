using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The automatic return is a new authoritative cause, and #156's lesson is
/// that a validator taught to accept one must still refuse every history that
/// merely looks consistent. Each test replays a real cast or shatter, edits
/// exactly one fact in place — every ordinal, handle, and audience preserved —
/// and requires the chronology to reject it.
///
/// Three forgeries are worth naming: a SUPPRESSED return (the budget waited
/// out, which is the whole mechanism defeated), a FORGED early return (the
/// windup claimed before it was earned, which would let a stance dodge on
/// demand), and a WRONG COUNT (the same start relabelled onto a tick whose
/// counter never moved).
/// </summary>
public sealed class FrontlineLabsAutomaticReturnChronologyTests
{
    [Fact]
    public void RejectsASuppressedThresholdReturn()
    {
        GenericActorMatchChronology chronology = VolleyCast();
        GenericActorMatchTickFrame fan = FirstFan(chronology);

        // Relabel the cast's return as a request. Every other fact stands, so
        // the only claim under test is that the fan did not spend the budget —
        // which is the artillery doctrine the rule exists to forbid.
        GenericActorRuntimeObservation.EventPayload.FormTransition start =
            AutomaticStart(fan);
        Assert.Contains(
            "not waitable",
            RebuildRelabelled(chronology, fan, start).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The striker enters its stance and leaves without ever firing, so the
    /// cast counter stands at zero. Relabelling that legitimate early exit as
    /// the threshold return is the forgery that would let a stance dodge on
    /// demand while claiming the rule made it do so.
    /// </summary>
    [Fact]
    public void RejectsAnAutomaticReturnForgedBeforeItsCount()
    {
        GenericActorMatchChronology chronology = VolleyEarlyExit();
        (GenericActorMatchTickFrame frame,
            GenericActorRuntimeObservation.EventPayload.FormTransition exit) =
            FirstStart(
                chronology,
                FrontlineLabsClassDefinition.Striker.PrimeStanceFormId);

        Assert.Contains(
            "first reaches the threshold",
            Rebuild(
                chronology,
                frame,
                exit,
                exit with
                {
                    Reason = GenericActorRuntimeObservation
                        .FormTransitionReason.AutomaticThresholdReturn,
                }).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The shell holds after its first deflection and leaves by hand, one bolt
    /// into a three-bolt budget. Same forgery, a counter that moved but not
    /// far enough — the arithmetic is checked, not merely the counter's name.
    /// </summary>
    [Fact]
    public void RejectsAnAutomaticReturnForgedOnAnIncompleteCount()
    {
        GenericActorMatchChronology chronology = ShellEarlyExit();
        (GenericActorMatchTickFrame frame,
            GenericActorRuntimeObservation.EventPayload.FormTransition exit) =
            FirstStart(
                chronology,
                FrontlineLabsClassDefinition.Bulwark.PrimeStanceFormId);
        int deflected = chronology.Ticks
            .Where(item => item.Tick <= frame.Tick)
            .Sum(item =>
                FrontlineLabsSkillArmTestFixture.Deflections(item).Length);
        Assert.InRange(
            deflected,
            1,
            FrontlineLabsDefinition.ShieldBreakBudget - 1);

        Assert.Contains(
            "first reaches the threshold",
            Rebuild(
                chronology,
                frame,
                exit,
                exit with
                {
                    Reason = GenericActorRuntimeObservation
                        .FormTransitionReason.AutomaticThresholdReturn,
                }).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The stance ENTRY declares no automatic return — only the way out does —
    /// so a cause asserted onto it has no route to point at.
    /// </summary>
    [Fact]
    public void RejectsAnAutomaticCauseOnARouteThatDeclaresNoTrigger()
    {
        GenericActorMatchChronology chronology = VolleyEarlyExit();
        (GenericActorMatchTickFrame frame,
            GenericActorRuntimeObservation.EventPayload.FormTransition entry) =
            FirstStart(
                chronology,
                FrontlineLabsClassDefinition.Striker.PrimeFormId);

        Assert.Contains(
            "exact route its source form declares",
            Rebuild(
                chronology,
                frame,
                entry,
                entry with
                {
                    Reason = GenericActorRuntimeObservation
                        .FormTransitionReason.AutomaticThresholdReturn,
                }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsACompletionWhoseCauseDisagreesWithItsStart()
    {
        GenericActorMatchChronology chronology = VolleyCast();
        GenericActorMatchTickFrame fan = FirstFan(chronology);
        GenericActorRuntimeObservation.EventPayload.FormTransition completion =
            Transitions(
                fan,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted)
                .First(FrontlineLabsSkillArmTestFixture.IsAutomatic);

        Assert.Contains(
            "same cause as the start it consumes",
            Rebuild(
                chronology,
                fan,
                completion,
                completion with
                {
                    Reason = GenericActorRuntimeObservation
                        .FormTransitionReason.Requested,
                }).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A manual exit relabelled automatic on a contract that never declares a
    /// trigger has no route to point at — the cheapest check, and the one that
    /// keeps every non-skill arm honest for free.
    /// </summary>
    [Fact]
    public void RejectsAnAutomaticCauseOnAContractThatDeclaresNoTrigger()
    {
        GenericActorMatchChronology chronology = AnchorWithoutSkills();
        (GenericActorMatchTickFrame frame,
            GenericActorRuntimeObservation.EventPayload.FormTransition start) =
            chronology.Ticks
                .SelectMany(item => Transitions(
                        item,
                        GenericActorRuntimeObservation.EventKind
                            .FormTransitionStarted)
                    .Select(payload => (Frame: item, Payload: payload)))
                .First();

        Assert.Contains(
            "route that declares the trigger",
            Rebuild(
                chronology,
                frame,
                start,
                start with
                {
                    Reason = GenericActorRuntimeObservation
                        .FormTransitionReason.AutomaticThresholdReturn,
                }).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Replaces one payload in one frame and rebuilds, keeping every ordinal,
    /// handle, audience, and traversal exactly as recorded.
    /// </summary>
    private static ArgumentException Rebuild(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        GenericActorRuntimeObservation.EventPayload original,
        GenericActorRuntimeObservation.EventPayload replacement) =>
        RebuildEvents(
            chronology,
            frame,
            [
                .. frame.Events.Select(item =>
                    ReferenceEquals(item.UnredactedPayload, original)
                        ? Replace(item, replacement)
                        : item),
            ]);

    /// <summary>
    /// Relabels one real automatic return — start and completion together — as
    /// an ordinary request, which is the cheapest lie available: every event
    /// still exists and only the cause moves.
    /// </summary>
    private static ArgumentException RebuildRelabelled(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        GenericActorRuntimeObservation.EventPayload.FormTransition start) =>
        RebuildEvents(
            chronology,
            frame,
            [
                .. frame.Events.Select(item =>
                    item.UnredactedPayload is GenericActorRuntimeObservation
                            .EventPayload.FormTransition payload
                        && payload.ActorId == start.ActorId
                        && payload.StartedTick == start.StartedTick
                        ? Replace(
                            item,
                            payload with
                            {
                                Reason = GenericActorRuntimeObservation
                                    .FormTransitionReason.Requested,
                            })
                        : item),
            ]);

    private static GenericActorAuthoritativeEvent Replace(
        GenericActorAuthoritativeEvent item,
        GenericActorRuntimeObservation.EventPayload payload) =>
        new(
            item.EventHandle,
            item.Tick,
            item.GlobalOrdinal,
            item.SourceOrdinal,
            item.Kind,
            payload,
            item.EventAudience);

    private static ArgumentException RebuildEvents(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        GenericActorAuthoritativeEvent[] events)
    {
        var edited = new GenericActorMatchTickFrame(
            frame.TickStart,
            frame.ActorTurns,
            events,
            frame.Traversals,
            frame.PostState);
        return Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [
                    .. chronology.Ticks.Select(item =>
                        item.Tick == frame.Tick ? edited : item),
                ],
                result: null));
    }

    /// <summary>
    /// The first recorded start leaving <paramref name="sourceFormId"/>, with
    /// the frame it belongs to — resolution events only, because those are the
    /// ones a rebuild can edit in place.
    /// </summary>
    private static (
        GenericActorMatchTickFrame Frame,
        GenericActorRuntimeObservation.EventPayload.FormTransition Payload)
        FirstStart(
            GenericActorMatchChronology chronology,
            string sourceFormId) =>
        chronology.Ticks
            .SelectMany(frame => frame.Events
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionStarted)
                .Select(item => (
                    Frame: frame,
                    Payload: (GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload)))
            .First(entry => string.Equals(
                entry.Payload.FromFormId,
                sourceFormId,
                StringComparison.Ordinal));

    private static GenericActorRuntimeObservation.EventPayload.FormTransition
        AutomaticStart(GenericActorMatchTickFrame frame) =>
        Transitions(
            frame,
            GenericActorRuntimeObservation.EventKind.FormTransitionStarted)
            .First(FrontlineLabsSkillArmTestFixture.IsAutomatic);

    private static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.FormTransition>
        Transitions(
            GenericActorMatchTickFrame frame,
            GenericActorRuntimeObservation.EventKind kind) =>
        FrontlineLabsSkillArmTestFixture.Transitions(frame, kind);

    /// <summary>
    /// The striker enters its stance and leaves at once, never firing, so the
    /// cast counter never moves. This is the legal early exit — and therefore
    /// the exact history a forged automatic cause would have to hide in.
    /// </summary>
    private static GenericActorMatchChronology VolleyEarlyExit()
    {
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        var target = new Position(
            3,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        return FrontlineLabsSkillArmTestFixture.Run(
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.StrikerVolley),
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0)
                {
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                if (observation.Self.FormId == stance)
                    return FrontlineLabsSkillArmTestFixture.Mobilize();
                return FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        target)
                    ?? (FrontlineLabsSkillArmTestFixture.Allows(
                            observation,
                            "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(stance)
                        : GenericDeathmatchSessionTestFixture.Wait());
            });
    }

    /// <summary>
    /// The shell drops its guard the moment it sees a deflection, one bolt
    /// into a three-bolt budget — the counter has moved, and the exit is still
    /// the author's.
    /// </summary>
    private static GenericActorMatchChronology ShellEarlyExit()
    {
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;
        var shooterTile = new Position(9, 13);
        var shellTile = new Position(12, 13);
        return FrontlineLabsSkillArmTestFixture.Run(
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsSkillKit.BulwarkAegisShell),
            (_, observation) =>
            {
                if (observation.Self.ActorId.UnitId != 0)
                    return GenericDeathmatchSessionTestFixture.Wait();
                if (observation.Self.ActorId.TeamId == 0)
                {
                    GenericActorRuntimeDecision? walk =
                        FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            shooterTile);
                    if (walk is not null)
                        return walk;
                    if (observation.Self.Facing != Direction.East)
                    {
                        return GenericDeathmatchSessionTestFixture.Rotate(
                            Direction.East);
                    }
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "shoot-straight")
                        ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                GenericActorRuntimeDecision? approach =
                    FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        shellTile);
                if (approach is not null)
                    return approach;
                if (observation.Self.FormId != shell)
                {
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(shell)
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                // A deflection is public, so the guard can read its own.
                return observation.VisibleEvents.Any(item =>
                    item.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .ProjectileDeflected)
                    ? FrontlineLabsSkillArmTestFixture.Mobilize()
                    : GenericDeathmatchSessionTestFixture.Wait();
            });
    }

    private static GenericActorMatchChronology VolleyCast()
    {
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        var target = new Position(
            3,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        return FrontlineLabsSkillArmTestFixture.Run(
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.StrikerVolley),
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0)
                {
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                if (observation.Self.FormId == stance)
                {
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "shoot-straight")
                        ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                return FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        target)
                    ?? (FrontlineLabsSkillArmTestFixture.Allows(
                            observation,
                            "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(stance)
                        : GenericDeathmatchSessionTestFixture.Wait());
            });
    }

    private static GenericActorMatchChronology ShieldBreak()
    {
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;
        var shooterTile = new Position(9, 13);
        var shellTile = new Position(12, 13);
        return FrontlineLabsSkillArmTestFixture.Run(
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsSkillKit.BulwarkAegisShell),
            (_, observation) =>
            {
                if (observation.Self.ActorId.UnitId != 0)
                    return GenericDeathmatchSessionTestFixture.Wait();
                if (observation.Self.ActorId.TeamId == 0)
                {
                    GenericActorRuntimeDecision? walk =
                        FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            shooterTile);
                    if (walk is not null)
                        return walk;
                    if (observation.Self.Facing != Direction.East)
                    {
                        return GenericDeathmatchSessionTestFixture.Rotate(
                            Direction.East);
                    }
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "shoot-straight")
                        ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                GenericActorRuntimeDecision? approach =
                    FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        shellTile);
                if (approach is not null)
                    return approach;
                if (observation.Self.FormId == shell)
                    return GenericDeathmatchSessionTestFixture.Wait();
                return FrontlineLabsSkillArmTestFixture.Allows(
                    observation,
                    "transform")
                    ? GenericDeathmatchSessionTestFixture.Transform(shell)
                    : GenericDeathmatchSessionTestFixture.Wait();
            });
    }

    /// <summary>
    /// A bulwark mirror with no skills at all: Anchor is a same-life route
    /// that declares no automatic return, so it is the natural place to check
    /// that the cause cannot simply be asserted.
    /// </summary>
    private static GenericActorMatchChronology AnchorWithoutSkills()
    {
        string turret = FrontlineLabsClassDefinition.Bulwark
            .PrimeTurretFormId;
        var target = new Position(
            3,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        return FrontlineLabsSkillArmTestFixture.Run(
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ContestMajority,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Bulwark)),
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0
                    || observation.Self.FormId == turret)
                {
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                return FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        target)
                    ?? (FrontlineLabsSkillArmTestFixture.Allows(
                            observation,
                            "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(turret)
                        : GenericDeathmatchSessionTestFixture.Wait());
            });
    }

    private static GenericActorMatchTickFrame FirstFan(
        GenericActorMatchChronology chronology) =>
        chronology.Ticks.First(frame =>
            FrontlineLabsSkillArmTestFixture.Attacks(frame).Length == 3);
}
