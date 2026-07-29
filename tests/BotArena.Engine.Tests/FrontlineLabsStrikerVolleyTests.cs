using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins STRIKER VOLLEY: the turret-shaped stance route, the one new engine
/// capability it needs (N projectiles from one attack action), and the
/// additive-canonical discipline that lets that capability exist without
/// moving a single existing fingerprint.
/// </summary>
public sealed class FrontlineLabsStrikerVolleyTests
{
    private static ActorResolvedMatchDefinition Arm() =>
        FrontlineLabsSkillArmTestFixture.Arm(
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsSkillKit.StrikerVolley);

    [Fact]
    public void TheStanceIsAReversibleImmobileRouteThatKeepsItsWeight()
    {
        ActorResolvedMatchDefinition arm = Arm();
        FrontlineLabsClassDefinition striker =
            FrontlineLabsClassDefinition.Striker;

        foreach ((string source, string stance, string route) in new[]
                 {
                     (striker.PrimeFormId,
                         striker.PrimeStanceFormId,
                         "volley-striker-prime"),
                     (striker.ChildFormId,
                         striker.ChildStanceFormId,
                         "volley-striker-child"),
                 })
        {
            ActorFormTransitionDefinition entry = Route(arm, route);
            Assert.Equal(source, entry.SourceFormId);
            Assert.Equal(stance, entry.TargetFormId);
            Assert.Equal("transform", entry.ActionId);
            Assert.Equal(2, entry.Windup.DurationTicks);
            Assert.False(entry.IrreversibleForLife);

            ActorFormTransitionDefinition exit = Route(
                arm,
                route.Replace("volley-", "unstance-", StringComparison.Ordinal));
            Assert.Equal(stance, exit.SourceFormId);
            Assert.Equal(source, exit.TargetFormId);
            Assert.Equal("mobilize", exit.ActionId);
            Assert.Equal(1, exit.Windup.DurationTicks);
            Assert.False(exit.IrreversibleForLife);

            ActorFormDefinition form = Form(arm, stance);
            // The stance forfeits mobility and keeps objective weight 1 —
            // the deliberate half of the turret bargain.
            Assert.Equal(1, form.ObjectiveWeight);
            Assert.DoesNotContain("move", form.AllowedActionIds);
            Assert.Contains("rotate", form.AllowedActionIds);
            Assert.Contains("mobilize", form.AllowedActionIds);
            Assert.Equal(
                striker.StanceAttackProfileId,
                form.AttackProfileId);
            Assert.Equal(
                ActorFormProjectileGuardKind.None,
                form.ProjectileGuard);
        }
    }

    [Fact]
    public void TheVolleyGunFiresThreeStraightBoltsOnASlowerCadence()
    {
        ActorAttackProfileDefinition volley = Attack(
            Arm(),
            FrontlineLabsClassDefinition.Striker.StanceAttackProfileId);
        ActorAttackProfileDefinition mobile = Attack(
            Arm(),
            FrontlineLabsClassDefinition.Striker.MobileAttackProfileId);

        Assert.Equal(3, volley.ProjectilesPerAttack);
        Assert.Equal(
            ActorAttackVolleyDefinition.VolleySpreadKind
                .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset,
            volley.Volley!.Spread);
        Assert.Equal(1, volley.Volley.FanHalfWidthSectors);
        // Straight only: no shot programs on the special (slate law).
        Assert.False(volley.ShotProgram.Enabled);
        Assert.Equal(
            ActorAttackProfileDefinition.AimInterpretationKind
                .CurrentFacingStraight,
            volley.AimInterpretation);
        // Zoning, not a wipe.
        Assert.True(
            volley.CooldownTicks > mobile.CooldownTicks,
            "the volley must be meaningfully slower than the mobile gun");
        Assert.Equal(5, volley.CooldownTicks);
        Assert.Equal(1, mobile.ProjectilesPerAttack);
        Assert.Null(mobile.Volley);
        // Kinematics stay shared with the class's own bolt (#153).
        Assert.Equal(mobile.Projectile, volley.Projectile);
    }

    [Fact]
    public void AProgrammedShotProfileCannotAlsoCarryAVolley()
    {
        ActorAttackProfileDefinition bend = Attack(
            Arm(),
            FrontlineLabsClassDefinition.Striker.MobileAttackProfileId);

        Assert.Throws<ArgumentException>(() =>
            new ActorAttackProfileDefinition(
                "hybrid",
                omnidirectionalAim: false,
                bend.Projectile,
                cooldownTicks: 1,
                maxEnergy: 0,
                attackEnergyCost: 0,
                energyRegenerationIntervalTicks: 0,
                energyRegenerationAmount: 0,
                bend.ShotProgram,
                new ActorAttackVolleyDefinition(
                    3,
                    ActorAttackVolleyDefinition.VolleySpreadKind
                        .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset)));
    }

    [Fact]
    public void OneVolleyLaunchesThreeBoltsWithDeterministicIdsAndHeadings()
    {
        ActorAttackProfileDefinition volley = Attack(
            Arm(),
            FrontlineLabsClassDefinition.Striker.StanceAttackProfileId);

        foreach (ProjectileHeading facing in Enum
                     .GetValues<ProjectileHeading>())
        {
            ImmutableArray<ProjectileHeading> headings =
                [.. GenericActorMatchSession.VolleyHeadings(volley, facing)];

            Assert.Equal(3, headings.Length);
            Assert.Equal(facing.Turned(-1), headings[0]);
            Assert.Equal(facing, headings[1]);
            Assert.Equal(facing.Turned(1), headings[2]);
        }

        // A profile without a volley is unchanged: exactly one bolt down the
        // resolved heading.
        Assert.Equal(
            [ProjectileHeading.East],
            GenericActorMatchSession.VolleyHeadings(
                Attack(
                    Arm(),
                    FrontlineLabsClassDefinition.Striker
                        .MobileAttackProfileId),
                ProjectileHeading.East));
    }

    /// <summary>
    /// Team 0's prime walks to the stance row, enters the stance, optionally
    /// turns, and then fires. Everything else waits, so the recorded fan
    /// belongs to exactly one shooter.
    /// </summary>
    private static GenericActorMatchChronology RunVolleyProbe(
        Direction? turnTo = null)
    {
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        var target = new Position(3, FrontlineLabsSkillArmTestFixture
            .StanceRowY);
        return FrontlineLabsSkillArmTestFixture.Run(
            Arm(),
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0)
                {
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                if (observation.Self.FormId == stance)
                {
                    if (turnTo is { } facing
                        && observation.Self.Facing != facing)
                    {
                        return GenericDeathmatchSessionTestFixture.Rotate(
                            facing);
                    }
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

    [Fact]
    public void AFiredVolleyPublishesThreeOrdinaryBoltsInLaunchOrder()
    {
        GenericActorMatchChronology chronology = RunVolleyProbe();

        GenericActorMatchTickFrame firstFan = chronology.Ticks.First(frame =>
            FrontlineLabsSkillArmTestFixture.Attacks(frame).Length > 0);
        GenericActorRuntimeObservation.EventPayload.Attack[] bolts =
            [.. FrontlineLabsSkillArmTestFixture.Attacks(firstFan)];

        Assert.Equal(3, bolts.Length);
        Assert.Single(bolts.Select(bolt => bolt.ActorId).Distinct());
        Assert.Equal(bolts[0].ProjectileId + 1, bolts[1].ProjectileId);
        Assert.Equal(bolts[1].ProjectileId + 1, bolts[2].ProjectileId);
        Assert.Equal(bolts[1].Heading.Turned(-1), bolts[0].Heading);
        Assert.Equal(bolts[1].Heading.Turned(1), bolts[2].Heading);
        // Every bolt is an ordinary projectile with an ordinary launch
        // traversal — the volley adds width, not a new entity kind.
        foreach (GenericActorRuntimeObservation.EventPayload.Attack bolt
                 in bolts)
        {
            Assert.Contains(
                firstFan.Traversals,
                traversal => traversal.ProjectileId == bolt.ProjectileId
                    && traversal.Trigger
                        == GenericActorProjectileTraversal
                            .TraversalTrigger.AttackLaunch);
        }
    }

    [Fact]
    public void AFanBoltTruncatedByTheMapStillTerminatesCleanly()
    {
        // Standing on the stance row against the map's south wall and facing
        // west, the south-west lane leaves the walkable region within a tile.
        // Truncation must be a terminal disposition, never a missing bolt.
        GenericActorMatchChronology chronology =
            RunVolleyProbe(Direction.West);

        GenericActorMatchTickFrame frame = chronology.Ticks.First(item =>
            FrontlineLabsSkillArmTestFixture.Attacks(item).Length > 0);
        long[] boltIds =
            [.. FrontlineLabsSkillArmTestFixture.Attacks(frame)
                .Select(attack => attack.ProjectileId)];

        Assert.Equal(3, boltIds.Length);
        Assert.Contains(
            frame.Traversals,
            traversal => boltIds.Contains(traversal.ProjectileId)
                && traversal.Terminal is GenericActorProjectileTraversal
                    .TerminalDisposition.WallOrPathExhausted);
        foreach (long boltId in boltIds)
        {
            Assert.Contains(
                frame.Traversals,
                traversal => traversal.ProjectileId == boltId);
        }
    }

    [Fact]
    public void LethalDamageDuringTheEntryWindupCancelsTheStance()
    {
        // The stance uses the shared CancelTransition lethal-damage policy, so
        // a windup that outlives its body must not complete.
        ActorFormTransitionDefinition entry = Route(
            Arm(),
            "volley-striker-prime");

        Assert.Equal(
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            entry.Windup.LethalDamage);
        Assert.Equal(
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            entry.Windup.PendingAction);
        Assert.Equal(
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            entry.Windup.SourceForm);
    }

    [Fact]
    public void TheVolleyFieldIsAdditiveAndHasExactlyOneCanonicalEncoding()
    {
        string withVolley = ActorContractManifestSerializer.ToCanonicalJson(
            Arm());
        string without = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker));

        Assert.DoesNotContain("volley", without, StringComparison.Ordinal);
        Assert.Contains(
            "\"volley\":{\"projectileCount\":3,",
            withVolley,
            StringComparison.Ordinal);

        // An explicitly inert volley is a second encoding of the same
        // contract, and both mirrors must refuse it.
        string inert = without.Replace(
            "\"diagonalCornersMustBeClear\":true}}",
            "\"diagonalCornersMustBeClear\":true},\"volley\":{\"projectileCount\":1,\"spread\":\"shared-resolved-heading\",\"identityOrder\":\"contiguous-ascending-in-launch-order\"}}",
            StringComparison.Ordinal);
        Assert.NotEqual(without, inert);
        Assert.Contains(
            "volley",
            Assert.Throws<FormatException>(() =>
                    GenericActorCanonicalContractValidator.Validate(inert))
                .Message,
            StringComparison.Ordinal);
    }

    [Theory]
    // A symmetric fan with no centre bolt.
    [InlineData("\"projectileCount\":3", "\"projectileCount\":4")]
    // An unregistered spread.
    [InlineData(
        "\"spread\":\"symmetric-adjacent-heading-fan-ascending-signed-sector-offset\"",
        "\"spread\":\"shotgun\"")]
    // An unregistered identity order.
    [InlineData(
        "\"identityOrder\":\"contiguous-ascending-in-launch-order\"",
        "\"identityOrder\":\"whatever\"")]
    public void TheMirrorRejectsAnUnregisteredVolleyShape(
        string original,
        string replacement)
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            Arm());
        string mutated = canonical.Replace(
            original,
            replacement,
            StringComparison.Ordinal);

        Assert.NotEqual(canonical, mutated);
        // The mirror refuses it; whether it lands as an unregistered semantic
        // tag or an invalid shape is the reader's existing convention.
        Assert.NotNull(
            Record.Exception(() =>
                GenericActorCanonicalContractValidator.Validate(mutated)));
    }

    [Fact]
    public void TheArmRoundTripsAndKeepsADistinctIdentity()
    {
        ActorResolvedMatchDefinition arm = Arm();
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker);

        Assert.Equal(
            "frontline-labs-1-striker-vs-striker-cast",
            arm.Rules.RulesetId);
        Assert.True(arm.Rules.RulesetId.Length <= 64);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(arm.Rules));
        // Only the rules move: map, format, and topology stay pinned.
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(baseline.Map),
            ActorContractFingerprint.ComputeMap(arm.Map));
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

    private static ActorAttackProfileDefinition Attack(
        ActorResolvedMatchDefinition definition,
        string profileId) =>
        definition.Rules.AttackProfiles.Single(
            profile => profile.Id == profileId);
}
