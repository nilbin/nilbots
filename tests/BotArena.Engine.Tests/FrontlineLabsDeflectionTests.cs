using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// BULWARK AEGIS SHELL after the deflection ruling: the stance no longer
/// blanks frontal fire, it sends it back. These pin the parts of that ruling
/// that are the skill rather than the generic rule — the locked arc, the
/// canonical encoding, the arm identity, and the fact that a naive attacker
/// kills itself on a shell it keeps poking.
/// </summary>
public sealed class FrontlineLabsDeflectionTests
{
    /// <summary>Team 0's shooting station on the stance row.</summary>
    private static readonly Position ShooterTile = new(9, 13);

    /// <summary>Team 1's shell station, three tiles east of the shooter.</summary>
    private static readonly Position ShellTile = new(12, 13);

    /// <summary>
    /// Every heading reverses onto its exact opposite, and the reversal maps
    /// each of a facing's three protected approaches back out along the way it
    /// came. This is the whole geometry of a return: the guard never aims.
    /// </summary>
    [Fact]
    public void EveryGuardedApproachReturnsAlongItsExactReverse()
    {
        foreach (ProjectileHeading heading in Enum
                     .GetValues<ProjectileHeading>())
        {
            var (dx, dy) = heading.Vector();
            var (rdx, rdy) = heading.Reversed().Vector();
            Assert.Equal((-dx, -dy), (rdx, rdy));
            Assert.Equal(heading, heading.Reversed().Reversed());
        }

        foreach ((Direction facing, ProjectileHeading[] guarded) in new[]
                 {
                     (Direction.North,
                         new[]
                         {
                             ProjectileHeading.South,
                             ProjectileHeading.SouthEast,
                             ProjectileHeading.SouthWest,
                         }),
                     (Direction.East,
                         new[]
                         {
                             ProjectileHeading.West,
                             ProjectileHeading.NorthWest,
                             ProjectileHeading.SouthWest,
                         }),
                     (Direction.South,
                         new[]
                         {
                             ProjectileHeading.North,
                             ProjectileHeading.NorthEast,
                             ProjectileHeading.NorthWest,
                         }),
                     (Direction.West,
                         new[]
                         {
                             ProjectileHeading.East,
                             ProjectileHeading.NorthEast,
                             ProjectileHeading.SouthEast,
                         }),
                 })
        {
            foreach (ProjectileHeading heading in guarded)
            {
                Assert.True(Guarded(heading, facing));
                // The return retraces the approach: the reversed heading
                // points back out of the quadrant the bolt arrived through.
                Assert.False(Guarded(heading.Reversed(), facing));
            }

            foreach (ProjectileHeading heading in Enum
                         .GetValues<ProjectileHeading>()
                         .Except(guarded))
            {
                Assert.False(Guarded(heading, facing));
            }
        }
    }

    /// <summary>
    /// The locked arc (owner ruling): the protected quadrant is chosen before
    /// the shield rises. A stance that could rotate would be an invincible
    /// capturer at objective weight 1, so the deflecting form publishes no
    /// rotation at all — while the volley stance, which is not a shield,
    /// keeps it.
    /// </summary>
    [Fact]
    public void TheDeflectingStanceCannotRotate()
    {
        ActorResolvedMatchDefinition shellArm = Arm(
            FrontlineLabsSkillKit.BulwarkAegisShell);
        FrontlineLabsClassDefinition bulwark =
            FrontlineLabsClassDefinition.Bulwark;

        foreach (string formId in new[]
                 {
                     bulwark.PrimeStanceFormId,
                     bulwark.ChildStanceFormId,
                 })
        {
            ActorFormDefinition form = Form(shellArm, formId);
            Assert.DoesNotContain("rotate", form.AllowedActionIds);
            Assert.Contains("mobilize", form.AllowedActionIds);
            Assert.Equal(
                ActorFormProjectileGuardKind.FacingQuadrantContactsDeflected,
                form.ProjectileGuard);
        }

        // The mobile source form still turns freely: the quadrant is chosen
        // there, one windup before the shield exists.
        Assert.Contains(
            "rotate",
            Form(shellArm, bulwark.PrimeFormId).AllowedActionIds);
        Assert.Contains(
            "rotate",
            Form(
                    Arm(FrontlineLabsSkillKit.StrikerVolley),
                    FrontlineLabsClassDefinition.Striker.PrimeStanceFormId)
                .AllowedActionIds);
    }

    /// <summary>
    /// The end-to-end ruling, on the exact naive probe whose absorbing
    /// ancestor produced a zero-damage match: every frontal bolt is turned,
    /// every turned bolt belongs to the shell's team, and the attacker that
    /// keeps poking is killed by its own fire.
    /// </summary>
    [Fact]
    public void TheNaiveAttackerIsKilledByItsOwnReturnedBolts()
    {
        GenericActorMatchChronology chronology = RunShellProbe();

        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected[]
            deflections =
            [
                .. chronology.Ticks.SelectMany(frame =>
                    FrontlineLabsSkillArmTestFixture.Deflections(frame)),
            ];
        Assert.NotEmpty(deflections);
        Assert.All(
            deflections,
            item =>
            {
                Assert.Equal(0, item.SourceTeamId);
                Assert.Equal(1, item.TargetActorId.TeamId);
                Assert.Equal(ShellTile, item.Position);
                Assert.Equal(ProjectileHeading.East, item.Heading);
                Assert.Equal(
                    FrontlineLabsClassDefinition.Bulwark.PrimeStanceFormId,
                    item.TargetFormId);
            });

        HashSet<long> returns = deflections
            .Select(item => item.DeflectedProjectileId)
            .ToHashSet();
        GenericActorRuntimeObservation.EventPayload.Damage[] hits =
        [
            .. chronology.Ticks.SelectMany(frame =>
                FrontlineLabsSkillArmTestFixture.Damages(frame)),
        ];
        Assert.NotEmpty(hits);
        // Every hit in this match comes from a returned bolt, and every one of
        // them lands on the shooter's team. The shell never takes damage: its
        // whole face is covered and nothing flanks it.
        Assert.All(
            hits,
            hit =>
            {
                Assert.Contains(hit.ProjectileId, returns);
                Assert.Equal(0, hit.TargetActorId.TeamId);
                Assert.Equal(1, hit.SourceTeamId);
            });
        Assert.Contains(
            chronology.Ticks.SelectMany(frame => frame.Events),
            item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.Destruction
                && ((GenericActorRuntimeObservation.EventPayload.Destruction)
                    item.Payload).ActorId.TeamId == 0);
    }

    /// <summary>
    /// Launch order is identity order across the whole match: a return's
    /// identity is minted at contact, after the consumed bolt's whole attack
    /// reserved its block, and no identity is ever reused.
    /// </summary>
    [Fact]
    public void ReturnedIdentitiesFollowTheSessionLaunchOrder()
    {
        GenericActorMatchChronology chronology = RunShellProbe();

        var issued = new HashSet<long>();
        long previous = -1;
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            ImmutableArray<
                    GenericActorRuntimeObservation.EventPayload
                        .ProjectileDeflected>
                deflections =
                    FrontlineLabsSkillArmTestFixture.Deflections(frame);
            foreach (GenericActorRuntimeObservation.EventPayload
                     .ProjectileDeflected item in deflections)
            {
                Assert.True(issued.Add(item.DeflectedProjectileId));
                Assert.True(item.DeflectedProjectileId > previous);
                Assert.True(
                    item.DeflectedProjectileId > item.ProjectileId);
                previous = item.DeflectedProjectileId;
            }
        }
    }

    /// <summary>
    /// The deflection arm is its own ruleset with its own fingerprints, and it
    /// round-trips through the canonical reader unchanged. The absorbing arm's
    /// token is gone: an arm that returns fire cannot share an identity with
    /// one that swallowed it.
    /// </summary>
    [Fact]
    public void TheDeflectionArmRoundTripsUnderItsOwnIdentity()
    {
        ActorResolvedMatchDefinition arm = Arm(
            FrontlineLabsSkillKit.BulwarkAegisShell);
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-bulwark-break",
            arm.Rules.RulesetId);
        Assert.True(arm.Rules.RulesetId.Length <= 64);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(arm.Rules));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(arm));

        string canonical = ActorContractManifestSerializer.ToCanonicalJson(arm);
        Assert.Contains(
            "\"projectileGuard\":\"facing-quadrant-contacts-deflected\"",
            canonical,
            StringComparison.Ordinal);
        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(canonical);
        Assert.Equal(arm.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(arm),
            validation.MatchContractFingerprint);
    }

    /// <summary>
    /// Team 0's prime walks to <see cref="ShooterTile"/> and opens fire once
    /// the enemy prime is standing in its shell; team 1's prime walks to
    /// <see cref="ShellTile"/>, turns west, and shells. Everything else waits.
    /// </summary>
    /// <summary>
    /// A fan cast point-blank into a raised shell: the centre bolt contacts
    /// the guard during its own launch traversal, so the return's identity is
    /// minted while the fan is still launching. The fan's identities are
    /// reserved as one block before any bolt flies precisely so that mint
    /// cannot gap them — the contract promises
    /// contiguous-ascending-in-launch-order, and the phase-2 factorial's
    /// cross-class cells are where that promise first met a shell.
    /// </summary>
    [Fact]
    public void AMidFanDeflectionCannotGapTheVolleysIdentities()
    {
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;
        var guardTile = new Position(
            11,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        var casterTile = new Position(
            12,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        GenericActorMatchChronology chronology =
            FrontlineLabsSkillArmTestFixture.Run(
                FrontlineLabsSkillArmTestFixture.Arm(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker,
                    FrontlineLabsSkillKit.StrikerVolley
                        | FrontlineLabsSkillKit.BulwarkAegisShell),
                (_, observation) =>
                {
                    if (observation.Self.ActorId.UnitId != 0)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    if (observation.Self.ActorId.TeamId == 1)
                    {
                        if (observation.Self.FormId == stance)
                        {
                            return FrontlineLabsSkillArmTestFixture.Allows(
                                observation,
                                "shoot-straight")
                                ? FrontlineLabsSkillArmTestFixture
                                    .ShootStraight()
                                : GenericDeathmatchSessionTestFixture.Wait();
                        }
                        GenericActorRuntimeDecision? walk =
                            FrontlineLabsSkillArmTestFixture.WalkTo(
                                observation,
                                casterTile);
                        if (walk is not null)
                            return walk;
                        if (observation.Self.Facing != Direction.West)
                        {
                            return GenericDeathmatchSessionTestFixture.Rotate(
                                Direction.West);
                        }
                        return observation.Enemies.Any(enemy =>
                                string.Equals(
                                    enemy.FormId,
                                    shell,
                                    StringComparison.Ordinal))
                            && FrontlineLabsSkillArmTestFixture.Allows(
                                observation,
                                "transform")
                            ? GenericDeathmatchSessionTestFixture.Transform(
                                stance)
                            : GenericDeathmatchSessionTestFixture.Wait();
                    }
                    GenericActorRuntimeDecision? approach =
                        FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            guardTile);
                    if (approach is not null)
                        return approach;
                    if (observation.Self.Facing != Direction.East)
                    {
                        return GenericDeathmatchSessionTestFixture.Rotate(
                            Direction.East);
                    }
                    if (observation.Self.FormId == shell)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(shell)
                        : GenericDeathmatchSessionTestFixture.Wait();
                });

        bool exercised = false;
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            ImmutableArray<
                    GenericActorRuntimeObservation.EventPayload
                        .ProjectileDeflected>
                deflections =
                    FrontlineLabsSkillArmTestFixture.Deflections(frame);
            foreach (IGrouping<
                         ActorIdentity,
                         GenericActorRuntimeObservation.EventPayload.Attack>
                     fan in FrontlineLabsSkillArmTestFixture.Attacks(frame)
                         .GroupBy(attack => attack.ActorId)
                         .Where(group => group.Count() == 3))
            {
                long[] identities =
                    [.. fan.Select(attack => attack.ProjectileId)];
                for (int index = 1; index < identities.Length; index++)
                {
                    Assert.Equal(
                        identities[index - 1] + 1,
                        identities[index]);
                }
                foreach (GenericActorRuntimeObservation.EventPayload
                             .ProjectileDeflected item
                         in deflections.Where(item =>
                             identities.Contains(item.ProjectileId)))
                {
                    exercised = true;
                    // Minted mid-fan at contact, yet landing after the
                    // whole reserved block.
                    Assert.True(
                        item.DeflectedProjectileId > identities.Max());
                }
            }
        }
        Assert.True(
            exercised,
            "no fan bolt was deflected on its own launch tick — the probe "
            + "no longer reproduces the mid-fan mint");
    }

    private static GenericActorMatchChronology RunShellProbe()
    {
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;
        return FrontlineLabsSkillArmTestFixture.Run(
            Arm(FrontlineLabsSkillKit.BulwarkAegisShell),
            (_, observation) =>
            {
                if (observation.Self.ActorId.UnitId != 0)
                    return GenericDeathmatchSessionTestFixture.Wait();
                if (observation.Self.ActorId.TeamId == 0)
                {
                    GenericActorRuntimeDecision? walk =
                        FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            ShooterTile);
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
                        ShellTile);
                if (approach is not null)
                    return approach;
                if (observation.Self.Facing != Direction.West)
                {
                    return GenericDeathmatchSessionTestFixture.Rotate(
                        Direction.West);
                }
                if (observation.Self.FormId == shell)
                    return GenericDeathmatchSessionTestFixture.Wait();
                return FrontlineLabsSkillArmTestFixture.Allows(
                    observation,
                    "transform")
                    ? GenericDeathmatchSessionTestFixture.Transform(shell)
                    : GenericDeathmatchSessionTestFixture.Wait();
            });
    }

    private static ActorResolvedMatchDefinition Arm(
        FrontlineLabsSkillKit skills) =>
        FrontlineLabsSkillArmTestFixture.Arm(
            skills == FrontlineLabsSkillKit.StrikerVolley
                ? FrontlineLabsClassDefinition.Striker
                : FrontlineLabsClassDefinition.Bulwark,
            skills == FrontlineLabsSkillKit.StrikerVolley
                ? FrontlineLabsClassDefinition.Striker
                : FrontlineLabsClassDefinition.Bulwark,
            skills);

    private static bool Guarded(
        ProjectileHeading heading,
        Direction facing)
    {
        var (dx, dy) = heading.Vector();
        return Visibility.InQuadrant(-dx, -dy, facing);
    }

    private static ActorFormDefinition Form(
        ActorResolvedMatchDefinition definition,
        string formId) =>
        definition.Rules.Forms.Single(form => form.Id == formId);
}
