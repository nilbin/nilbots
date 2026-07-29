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
    public void RejectsADeflectionFromOutsideTheGuardArc()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
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
    public void RejectsADeflectionByAFormThatDeclaresNoGuard()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
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
    public void RejectsADeflectionOfOwnTeamFire()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
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
    public void RejectsADeflectionWithNoConsumingTraversal()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
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
    /// The return is the deflection's second fact and is checked as hard as
    /// the first: a deflection that names a bolt nothing launched is refused.
    /// </summary>
    [Fact]
    public void RejectsADeflectionWhoseReturnWasNeverLaunched()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
            original = FirstDeflectedPayload(frame);

        Assert.Contains(
            "guard-deflection launch",
            RebuildWithoutTraversal(
                chronology,
                frame,
                original.DeflectedProjectileId).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// And the converse: a launch nobody deflected is a projectile appearing
    /// from nowhere, which is exactly the forgery the ownership flip would
    /// make profitable.
    /// </summary>
    [Fact]
    public void RejectsAGuardDeflectionLaunchNoEventNames()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
            original = FirstDeflectedPayload(frame);

        Assert.Contains(
            "named by a deflection event",
            Rebuild(
                chronology,
                frame,
                original,
                original with
                {
                    DeflectedProjectileId =
                        original.DeflectedProjectileId + 5_000,
                }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsADeflectionWhoseReturnLeavesTheWrongTile()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
            original = FirstDeflectedPayload(frame);

        Assert.Contains(
            "own tile",
            Rebuild(
                chronology,
                frame,
                original,
                original with
                {
                    Position = original.Position.Offset(0, -1),
                }).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The forged heading stays inside the guard's quadrant, so only the
    /// reversal rule can catch it — which is the point of stating the return
    /// as the exact reverse rather than as "roughly back".
    /// </summary>
    [Fact]
    public void RejectsADeflectionWhoseReturnDoesNotReverseTheApproach()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
            original = FirstDeflectedPayload(frame);

        Assert.Contains(
            "exactly reversed heading",
            Rebuild(
                chronology,
                frame,
                original,
                original with
                {
                    Heading = original.Heading.Turned(1),
                }).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsADeflectionWhoseReturnIsOwnedByTheShooter()
    {
        GenericActorMatchChronology chronology = ShellChronology();
        GenericActorMatchTickFrame frame = FirstDeflection(chronology);
        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
            original = FirstDeflectedPayload(frame);

        Assert.Contains(
            "deflecting life and its team",
            RebuildWithReassignedLaunch(
                chronology,
                frame,
                original.DeflectedProjectileId,
                original.SourceActorId!).Message,
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

    /// <summary>
    /// Rebuilds the chronology with the named projectile's guard-deflection
    /// launch deleted, leaving every other fact exactly as recorded.
    /// </summary>
    private static ArgumentException RebuildWithoutTraversal(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        long projectileId) =>
        RebuildTraversals(
            chronology,
            frame,
            [
                .. frame.Traversals.Where(traversal =>
                    traversal.ProjectileId != projectileId),
            ]);

    /// <summary>
    /// Rebuilds the chronology with the named return's launch reassigned to
    /// another life — the forgery that would let the shooter's own bolt come
    /// back under the shooter's colours.
    /// </summary>
    private static ArgumentException RebuildWithReassignedLaunch(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        long projectileId,
        ActorIdentity owner) =>
        RebuildTraversals(
            chronology,
            frame,
            [
                .. frame.Traversals.Select(traversal =>
                    traversal.ProjectileId == projectileId
                        ? new GenericActorProjectileTraversal(
                            traversal.Tick,
                            traversal.GlobalOrdinal,
                            traversal.Phase,
                            traversal.Trigger,
                            traversal.ProjectileId,
                            traversal.OwnerParticipantId,
                            owner.TeamId,
                            owner,
                            traversal.AttackProfileId,
                            traversal.From,
                            traversal.Path,
                            traversal.LaunchHeading,
                            traversal.FinalHeading,
                            traversal.ShotProgram,
                            traversal.Terminal)
                        : traversal),
            ]);

    private static ArgumentException RebuildTraversals(
        GenericActorMatchChronology chronology,
        GenericActorMatchTickFrame frame,
        GenericActorProjectileTraversal[] traversals)
    {
        var edited = new GenericActorMatchTickFrame(
            frame.TickStart,
            frame.ActorTurns,
            frame.Events,
            traversals,
            frame.PostState);
        return Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [.. chronology.Ticks.Select(item =>
                    item.Tick == frame.Tick ? edited : item)],
                result: null));
    }

    private static GenericActorRuntimeObservation.EventPayload
        .ProjectileDeflected FirstDeflectedPayload(
            GenericActorMatchTickFrame frame) =>
        (GenericActorRuntimeObservation.EventPayload.ProjectileDeflected)
        frame.Events.First(item => item.Kind
            == GenericActorRuntimeObservation.EventKind.ProjectileDeflected)
            .Payload;

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

    private static GenericActorMatchTickFrame FirstDeflection(
        GenericActorMatchChronology chronology) =>
        chronology.Ticks.First(frame =>
            FrontlineLabsSkillArmTestFixture.Deflections(frame).Length > 0);
}
