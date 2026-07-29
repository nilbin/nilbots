using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins BULWARK AEGIS SHELL: the reversible stance whose price is the form
/// (immobile, gunless, objective weight 1 — tenure is not clock-bounded), the
/// typed form-level guard that turns frontal fire, the observed event that
/// makes the exchange renderable, and the fingerprint discipline around both.
/// What the turned bolt then does is FrontlineLabsDeflectionTests.
/// </summary>
public sealed class FrontlineLabsAegisShellTests
{
    /// <summary>Team 0's shooting station on the stance row.</summary>
    private static readonly Position ShooterTile = new(9, 13);

    /// <summary>Team 1's shell station, three tiles east of the shooter.</summary>
    private static readonly Position ShellTile = new(12, 13);

    private static ActorResolvedMatchDefinition Arm() =>
        FrontlineLabsSkillArmTestFixture.Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsSkillKit.BulwarkAegisShell);

    [Fact]
    public void TheShellIsAReversibleStanceThatPaysInTheForm()
    {
        ActorResolvedMatchDefinition arm = Arm();
        FrontlineLabsClassDefinition bulwark =
            FrontlineLabsClassDefinition.Bulwark;

        foreach ((string source, string shell, string route, int maxHealth)
                 in new[]
                 {
                     (bulwark.PrimeFormId,
                         bulwark.PrimeStanceFormId,
                         "shell-bulwark-prime",
                         bulwark.PrimeMaxHealth),
                     (bulwark.ChildFormId,
                         bulwark.ChildStanceFormId,
                         "shell-bulwark-child",
                         bulwark.ChildMaxHealth),
                 })
        {
            ActorFormTransitionDefinition entry = Route(arm, route);
            Assert.Equal(source, entry.SourceFormId);
            Assert.Equal(shell, entry.TargetFormId);
            Assert.Equal(1, entry.Windup.DurationTicks);
            Assert.False(entry.IrreversibleForLife);
            // No health gain in either direction: a reversible stance must not
            // become a repair loop the way a healing Anchor would.
            Assert.Equal(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveCurrentCappedToTargetMaximum,
                entry.Health.Policy);

            ActorFormTransitionDefinition exit = Route(
                arm,
                route.Replace("shell-", "unstance-", StringComparison.Ordinal));
            Assert.Equal(shell, exit.SourceFormId);
            Assert.Equal(source, exit.TargetFormId);
            Assert.Equal("mobilize", exit.ActionId);
            Assert.Equal(1, exit.Windup.DurationTicks);
            Assert.False(exit.IrreversibleForLife);

            ActorFormDefinition form = Form(arm, shell);
            Assert.Equal(maxHealth, form.MaxHealth);
            // The price is the form: immobile, no gun, weight kept.
            Assert.Null(form.AttackProfileId);
            Assert.DoesNotContain("move", form.AllowedActionIds);
            Assert.DoesNotContain("shoot-straight", form.AllowedActionIds);
            Assert.Equal(1, form.ObjectiveWeight);
            Assert.Equal(
                ActorFormProjectileGuardKind
                    .FacingQuadrantContactsDeflected,
                form.ProjectileGuard);
            // Tenure is priced, never clock-enforced: no rule caps it.
            Assert.Contains("wait", form.AllowedActionIds);
        }

        // The turret is untouched by the skill: the class keeps both routes.
        Assert.Equal(
            ActorFormProjectileGuardKind.None,
            Form(arm, bulwark.PrimeTurretFormId).ProjectileGuard);
    }

    [Fact]
    public void TheGuardArcIsTheFacingQuadrantOnTheApproachVector()
    {
        // Frontal, including both diagonal edges of the quadrant.
        foreach (ProjectileHeading heading in new[]
                 {
                     ProjectileHeading.East,
                     ProjectileHeading.NorthEast,
                     ProjectileHeading.SouthEast,
                 })
        {
            Assert.True(Frontal(heading, Direction.West));
        }
        // Flank and rear.
        foreach (ProjectileHeading heading in new[]
                 {
                     ProjectileHeading.North,
                     ProjectileHeading.South,
                     ProjectileHeading.West,
                     ProjectileHeading.NorthWest,
                     ProjectileHeading.SouthWest,
                 })
        {
            Assert.False(Frontal(heading, Direction.West));
        }
        // Point-blank is NOT an exemption here: the guard reads an approach
        // direction, not a tile, so the vision cone's proximity ring must not
        // leak in and make every contact frontal.
        Assert.False(Frontal(ProjectileHeading.West, Direction.West));
    }

    [Fact]
    public void AFrontalContactIsTurnedAndPublishedWithoutDamage()
    {
        GenericActorMatchChronology chronology = RunShellProbe(
            Direction.West);

        GenericActorMatchTickFrame frame = chronology.Ticks.First(item =>
            FrontlineLabsSkillArmTestFixture.Deflections(item).Length > 0);
        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
            turned = FrontlineLabsSkillArmTestFixture
                .Deflections(frame)
                .Single();

        Assert.Equal(0, turned.SourceTeamId);
        Assert.Equal(1, turned.TargetActorId.TeamId);
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark.PrimeStanceFormId,
            turned.TargetFormId);
        Assert.Equal(Direction.West, turned.TargetFacing);
        Assert.Equal(ProjectileHeading.East, turned.Heading);
        Assert.Equal(ShellTile, turned.Position);
        // The consumed bolt never damages: it dies on the arc.
        Assert.DoesNotContain(
            FrontlineLabsSkillArmTestFixture.Damages(frame),
            damage => damage.ProjectileId == turned.ProjectileId);
        Assert.Contains(
            frame.Traversals,
            traversal => traversal.ProjectileId == turned.ProjectileId
                && traversal.Terminal is GenericActorProjectileTraversal
                    .TerminalDisposition.ActorContact
                    {
                        AppliedDamage: false,
                    });
        // The shell keeps full health through the whole exchange.
        Assert.All(
            chronology.Ticks,
            tick => Assert.DoesNotContain(
                FrontlineLabsSkillArmTestFixture.Damages(tick),
                damage => damage.TargetActorId.TeamId == 1));
    }

    [Fact]
    public void AFlankContactDamagesTheSameShellNormally()
    {
        // Same probe, same tiles, same bolts — only the shell's facing moves,
        // so the arc is the whole difference.
        GenericActorMatchChronology chronology = RunShellProbe(
            Direction.North);

        Assert.DoesNotContain(
            chronology.Ticks,
            frame => FrontlineLabsSkillArmTestFixture
                .Deflections(frame)
                .Length > 0);
        GenericActorRuntimeObservation.EventPayload.Damage first = chronology
            .Ticks
            .SelectMany(frame =>
                FrontlineLabsSkillArmTestFixture.Damages(frame))
            .First(damage => damage.TargetActorId.TeamId == 1);
        Assert.Equal(1, first.Amount);
        Assert.Equal(ShellTile, first.Position);
    }

    [Fact]
    public void LethalDamageDuringTheShellWindupCancelsIt()
    {
        ActorFormTransitionDefinition entry = Route(
            Arm(),
            "shell-bulwark-prime");

        Assert.Equal(
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            entry.Windup.LethalDamage);
        // The windup is public and punishable: the source form is retained and
        // the life may only Wait while it runs.
        Assert.Equal(
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            entry.Windup.SourceForm);
        Assert.Equal(
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            entry.Windup.PendingAction);
        Assert.Equal(
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            entry.Windup.Targetability);
    }

    [Fact]
    public void TheGuardFieldIsAdditiveAndHasExactlyOneCanonicalEncoding()
    {
        string guarded = ActorContractManifestSerializer.ToCanonicalJson(
            Arm());
        string plain = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark));

        Assert.DoesNotContain(
            "projectileGuard",
            plain,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"projectileGuard\":\"facing-quadrant-contacts-deflected\"",
            guarded,
            StringComparison.Ordinal);

        // An explicit "none" is a second encoding of the unguarded contract.
        string inert = plain.Replace(
            "\"objectiveWeight\":0,",
            "\"objectiveWeight\":0,\"projectileGuard\":\"none\",",
            StringComparison.Ordinal);
        Assert.NotEqual(plain, inert);
        Assert.Contains(
            "projectileGuard",
            Assert.Throws<FormatException>(() =>
                    GenericActorCanonicalContractValidator.Validate(inert))
                .Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheArmRoundTripsAndKeepsADistinctIdentity()
    {
        ActorResolvedMatchDefinition arm = Arm();
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-bulwark-parry",
            arm.Rules.RulesetId);
        Assert.True(arm.Rules.RulesetId.Length <= 64);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(arm.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeTopology(baseline.Topology),
            ActorContractFingerprint.ComputeTopology(arm.Topology));

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(
                ActorContractManifestSerializer.ToCanonicalJson(arm));
        Assert.Equal(arm.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(arm),
            validation.MatchContractFingerprint);
    }

    /// <summary>
    /// Team 0's prime walks to <see cref="ShooterTile"/> and opens fire once
    /// the enemy prime is standing in its shell; team 1's prime walks to
    /// <see cref="ShellTile"/>, turns to <paramref name="shellFacing"/>, and
    /// shells. Everything else waits.
    /// </summary>
    private static GenericActorMatchChronology RunShellProbe(
        Direction shellFacing)
    {
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;
        return FrontlineLabsSkillArmTestFixture.Run(
            Arm(),
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
                    bool shellUp = observation.Enemies.Any(enemy =>
                        string.Equals(
                            enemy.FormId,
                            shell,
                            StringComparison.Ordinal));
                    return shellUp
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
                if (observation.Self.Facing != shellFacing)
                {
                    return GenericDeathmatchSessionTestFixture.Rotate(
                        shellFacing);
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

    private static bool Frontal(
        ProjectileHeading heading,
        Direction facing)
    {
        var (dx, dy) = heading.Vector();
        return Visibility.InQuadrant(-dx, -dy, facing);
    }

    private static ActorFormTransitionDefinition Route(
        ActorResolvedMatchDefinition definition,
        string transitionId) =>
        definition.Rules.SameLifeTransitions
            .OfType<ActorFormTransitionDefinition>()
            .Single(transition => transition.TransitionId == transitionId);

    private static ActorFormDefinition Form(
        ActorResolvedMatchDefinition definition,
        string formId) =>
        definition.Rules.Forms.Single(form => form.Id == formId);
}
