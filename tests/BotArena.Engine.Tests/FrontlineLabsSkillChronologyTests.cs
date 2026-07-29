namespace BotArena.Engine.Tests;

/// <summary>
/// The causality validator must reject self-consistent-but-impossible skill
/// histories, exactly as #156 had to teach it that a Movement event can be the
/// evidence for a facing change. Each test replays a real match on a skill
/// arm, edits one authoritative fact in place — keeping every ordinal and
/// handle intact so the edit is the only thing under test — and requires the
/// chronology to refuse it.
/// </summary>
public sealed class FrontlineLabsSkillChronologyTests
{
    [Fact]
    public void RejectsAVolleyWhoseHeadingsLeaveTheDeclaredFan()
    {
        GenericActorMatchChronology chronology = VolleyChronology();
        GenericActorMatchTickFrame fan = FirstFan(chronology);
        var original = (GenericActorRuntimeObservation.EventPayload.Attack)
            fan.Events.First(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Attack).Payload;

        Assert.Contains(
            "spread",
            Rebuild(
                chronology,
                fan,
                original,
                original with { Heading = original.Heading.Turned(4) }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAVolleyWhoseProjectileIdentitiesAreNotContiguous()
    {
        GenericActorMatchChronology chronology = VolleyChronology();
        GenericActorMatchTickFrame fan = FirstFan(chronology);
        var original = (GenericActorRuntimeObservation.EventPayload.Attack)
            fan.Events.Last(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Attack).Payload;

        Assert.Contains(
            "contiguous",
            Rebuild(
                chronology,
                fan,
                original,
                original with
                {
                    ProjectileId = original.ProjectileId + 100,
                }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnAbsorptionFromOutsideTheGuardArc()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstAbsorption(chronology);
        var original = (GenericActorRuntimeObservation.EventPayload
            .ProjectileDeflected)frame.Events.First(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .ProjectileDeflected).Payload;

        Assert.Contains(
            "facing quadrant",
            Rebuild(
                chronology,
                frame,
                original,
                original with { TargetFacing = Direction.North }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnAbsorptionByAFormThatDeclaresNoGuard()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstAbsorption(chronology);
        var original = (GenericActorRuntimeObservation.EventPayload
            .ProjectileDeflected)frame.Events.First(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .ProjectileDeflected).Payload;

        Assert.Contains(
            "declared guard",
            Rebuild(
                chronology,
                frame,
                original,
                original with
                {
                    TargetFormId =
                        FrontlineLabsClassDefinition.Bulwark.PrimeFormId,
                }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnAbsorptionOfOwnTeamFire()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstAbsorption(chronology);
        var original = (GenericActorRuntimeObservation.EventPayload
            .ProjectileDeflected)frame.Events.First(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .ProjectileDeflected).Payload;

        Assert.Contains(
            "hostile fire",
            Rebuild(
                chronology,
                frame,
                original,
                original with
                {
                    SourceTeamId = original.TargetActorId.TeamId,
                    SourceActorId = null,
                }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnAbsorptionWithNoConsumingTraversal()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstAbsorption(chronology);
        var original = (GenericActorRuntimeObservation.EventPayload
            .ProjectileDeflected)frame.Events.First(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .ProjectileDeflected).Payload;

        Assert.Contains(
            "contact traversal",
            Rebuild(
                chronology,
                frame,
                original,
                original with
                {
                    ProjectileId = original.ProjectileId + 1_000,
                }).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Replaces one payload in one frame and rebuilds the chronology, keeping
    /// every ordinal, handle, audience, and traversal exactly as recorded.
    /// </summary>
    private static ArgumentException Rebuild(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        GenericActorRuntimeObservation.EventPayload original,
        GenericActorRuntimeObservation.EventPayload replacement)
    {
        GenericActorAuthoritativeEvent[] events = [.. frame.Events.Select(
            item => ReferenceEquals(item.UnredactedPayload, original)
                ? new GenericActorAuthoritativeEvent(
                    item.EventHandle,
                    item.Tick,
                    item.GlobalOrdinal,
                    item.SourceOrdinal,
                    item.Kind,
                    replacement,
                    item.EventAudience)
                : item)];
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
                [.. chronology.Ticks.Select(item =>
                    item.Tick == frame.Tick ? edited : item)],
                result: null));
    }

    private static GenericActorMatchChronology VolleyChronology()
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

    private static GenericActorMatchChronology ShellChronology()
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
                    return observation.Enemies.Any(enemy =>
                            string.Equals(
                                enemy.FormId,
                                shell,
                                StringComparison.Ordinal))
                        && FrontlineLabsSkillArmTestFixture.Allows(
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

    private static GenericActorMatchTickFrame FirstFan(
        GenericActorMatchChronology chronology) =>
        chronology.Ticks.First(frame =>
            FrontlineLabsSkillArmTestFixture.Attacks(frame).Length == 3);

    private static GenericActorMatchTickFrame FirstAbsorption(
        GenericActorMatchChronology chronology) =>
        chronology.Ticks.First(frame =>
            FrontlineLabsSkillArmTestFixture.Deflections(frame).Length > 0);
}
