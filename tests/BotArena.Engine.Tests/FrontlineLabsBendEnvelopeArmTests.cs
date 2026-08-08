namespace BotArena.Engine.Tests;

/// <summary>
/// The universal curve grammar (owner ruling,
/// <c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>): bends become shared
/// grammar on every class's MOBILE gun, and the striker keeps the deepest
/// envelope so depth stays its identity rather than its monopoly. The classes
/// that gain the grammar get the shallower half the skill-shot forensics
/// justified — option richness, not raw power, because the solver's
/// covering-number cap is envelope-invariant.
///
/// Specials never curve, and that is verified rather than assumed: a volley
/// profile refuses programmed shots structurally, and turret guns stay
/// straight in both arms.
///
/// Striker-only versus universal is a phase-2 factor, so both levels stay
/// expressible and separately identified, and the striker-only baseline adds
/// no ruleset token at all.
/// </summary>
public sealed class FrontlineLabsBendEnvelopeArmTests
{
    [Fact]
    public void TheBaselineLeavesEveryExistingArmIdentityAndContractUntouched()
    {
        foreach ((FrontlineLabsClassDefinition zero,
                 FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            ActorResolvedMatchDefinition baseline = Arm(
                zero,
                one,
                FrontlineLabsBendEnvelopeArm.StrikerOnly);
            ActorResolvedMatchDefinition implicitBaseline =
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    FrontlineLabsPendulumArm.ContestMajority,
                    (zero, one));

            Assert.Equal(
                implicitBaseline.Rules.RulesetId,
                baseline.Rules.RulesetId);
            Assert.DoesNotContain(
                "bend",
                baseline.Rules.RulesetId,
                StringComparison.Ordinal);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(implicitBaseline),
                ActorContractFingerprint.ComputeMatch(baseline));
        }
    }

    [Fact]
    public void TheUniversalArmIsItsOwnIdentityAndFingerprint()
    {
        foreach ((FrontlineLabsClassDefinition zero,
                 FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            ActorResolvedMatchDefinition baseline = Arm(
                zero,
                one,
                FrontlineLabsBendEnvelopeArm.StrikerOnly);
            ActorResolvedMatchDefinition universal = Arm(
                zero,
                one,
                FrontlineLabsBendEnvelopeArm.Universal);

            Assert.Equal(
                $"{baseline.Rules.RulesetId}-bend",
                universal.Rules.RulesetId);
            Assert.True(universal.Rules.RulesetId.Length <= 64);
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(baseline),
                ActorContractFingerprint.ComputeMatch(universal));
            // Only the guns move: the topology is untouched by a grammar
            // change, which is what keeps the factor separable.
            Assert.Equal(
                ActorContractFingerprint.ComputeTopology(baseline.Topology),
                ActorContractFingerprint.ComputeTopology(universal.Topology));
        }
    }

    /// <summary>
    /// Depth per class, and only for classes that gain the grammar here: the
    /// striker's contract bytes for its own gun are the ones it was measured
    /// on, in both arms.
    /// </summary>
    [Theory]
    [InlineData("striker", 4)]
    [InlineData("bulwark", 2)]
    [InlineData("fabricator", 2)]
    public void EachClassBendsToItsOwnDepthUnderTheUniversalArm(
        string classId,
        int expectedMaxBendAfterTiles)
    {
        FrontlineLabsClassDefinition entry =
            FrontlineLabsClassDefinition.Parse(classId);
        ActorAttackProfileDefinition gun = MobileGun(
            Arm(entry, entry, FrontlineLabsBendEnvelopeArm.Universal),
            entry);

        Assert.True(gun.ShotProgram.Enabled);
        Assert.Equal(1, gun.ShotProgram.MinBendAfterTiles);
        Assert.Equal(
            expectedMaxBendAfterTiles,
            gun.ShotProgram.MaxBendAfterTiles);
        // One bend, no initial aim offset: the envelope is deeper or
        // shallower, never a different grammar.
        Assert.Equal(1, gun.ShotProgram.MinBendCount);
        Assert.Equal(1, gun.ShotProgram.MaxBendCount);
        Assert.Equal(1, gun.ShotProgram.MaxBendEveryTiles);
        Assert.Equal(0, gun.ShotProgram.MinInitialAimSteps);
        Assert.Equal(0, gun.ShotProgram.MaxInitialAimSteps);
        Assert.Equal(
            [-1, 1],
            gun.ShotProgram.AllowedCurvedBendDirections.ToArray());
        // A straight shot stays one parameterless-equivalent decision.
        Assert.True(gun.ShotProgram.PayloadOptional);
    }

    [Theory]
    [InlineData("bulwark")]
    [InlineData("fabricator")]
    public void ClassesWithoutTheGrammarStillFireStraightOnTheBaseline(
        string classId)
    {
        FrontlineLabsClassDefinition entry =
            FrontlineLabsClassDefinition.Parse(classId);
        ActorAttackProfileDefinition gun = MobileGun(
            Arm(entry, entry, FrontlineLabsBendEnvelopeArm.StrikerOnly),
            entry);

        Assert.False(gun.ShotProgram.Enabled);
        Assert.Equal(1, gun.ShotProgram.MaxBendAfterTiles);
        Assert.Equal(
            ActorAttackProfileDefinition.AimInterpretationKind
                .CurrentFacingStraight,
            gun.AimInterpretation);
    }

    /// <summary>
    /// The action a form allows is the observable half: gaining the grammar
    /// moves a class's mobile gun from the parameterless action to the
    /// program-bearing one, which is what a contract-driven bot reads.
    /// </summary>
    [Theory]
    [InlineData("bulwark")]
    [InlineData("fabricator")]
    public void GainingTheGrammarMovesTheMobileFormOntoTheProgrammedAction(
        string classId)
    {
        FrontlineLabsClassDefinition entry =
            FrontlineLabsClassDefinition.Parse(classId);
        ActorResolvedMatchDefinition baseline = Arm(
            entry,
            entry,
            FrontlineLabsBendEnvelopeArm.StrikerOnly);
        ActorResolvedMatchDefinition universal = Arm(
            entry,
            entry,
            FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Contains(
            "shoot-straight",
            Form(baseline, entry.PrimeFormId).AllowedActionIds);
        Assert.Contains(
            "shoot",
            Form(universal, entry.PrimeFormId).AllowedActionIds);
        Assert.DoesNotContain(
            "shoot-straight",
            Form(universal, entry.PrimeFormId).AllowedActionIds);
        Assert.Equal(
            ActorActionParameterKind.ShotProgram,
            Assert.Single(
                universal.Rules.Actions
                    .Single(action => action.Id == "shoot")
                    .ParameterKinds));
    }

    /// <summary>
    /// The slate's law, verified rather than assumed. The volley's exclusion
    /// is structural — an attack profile refuses to be built with both a fan
    /// and an enabled program — so the check is that the arm actually resolves
    /// with the fan straight, in the arm where every OTHER gun bends.
    /// </summary>
    [Fact]
    public void TheVolleyFanRefusesProgramsEvenWhenEveryMobileGunBends()
    {
        FrontlineLabsClassDefinition striker =
            FrontlineLabsClassDefinition.Striker;
        ActorResolvedMatchDefinition arm =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ContestMajority,
                (striker, striker),
                skills: FrontlineLabsSkillKit.StrikerVolley,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);
        ActorAttackProfileDefinition fan = arm.Rules.AttackProfiles
            .Single(profile => profile.Id == striker.StanceAttackProfileId);

        Assert.NotNull(fan.Volley);
        Assert.False(fan.ShotProgram.Enabled);
        Assert.Equal(1, fan.ShotProgram.MaxBendAfterTiles);
        // And the stance still fires through the parameterless action.
        Assert.Contains(
            "shoot-straight",
            Form(arm, striker.PrimeStanceFormId).AllowedActionIds);
        Assert.DoesNotContain(
            "shoot",
            Form(arm, striker.PrimeStanceFormId).AllowedActionIds);
        // Meanwhile the same class's mobile gun does bend.
        Assert.True(MobileGun(arm, striker).ShotProgram.Enabled);
    }

    [Fact]
    public void TheStructuralExclusionIsWhatMakesTheFanStraight() =>
        Assert.Contains(
            "programmed shots",
            Assert.Throws<ArgumentException>(() =>
                new ActorAttackProfileDefinition(
                    "curved-fan",
                    omnidirectionalAim: false,
                    new ActorProjectileDefinition(
                        ActorProjectileMode.Discrete,
                        damagePerHit: 1,
                        maxTravelTiles: 8,
                        ticksPerAdvance: 1,
                        tilesPerAdvance: 2,
                        launchTiles: 1,
                        advancesOnLaunchTick: false,
                        damageAppliedSimultaneously: true,
                        diagonalCornersMustBeClear: true),
                    cooldownTicks: 5,
                    maxEnergy: 0,
                    attackEnergyCost: 0,
                    energyRegenerationIntervalTicks: 0,
                    energyRegenerationAmount: 0,
                    ProgrammedShotProgram(),
                    new ActorAttackVolleyDefinition(
                        3,
                        ActorAttackVolleyDefinition.VolleySpreadKind
                            .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset)))
                .Message,
            StringComparison.Ordinal);

    [Fact]
    public void TurretGunsStayStraightInBothArms()
    {
        FrontlineLabsClassDefinition bulwark =
            FrontlineLabsClassDefinition.Bulwark;
        foreach (FrontlineLabsBendEnvelopeArm envelope in
                 new[]
                 {
                     FrontlineLabsBendEnvelopeArm.StrikerOnly,
                     FrontlineLabsBendEnvelopeArm.Universal,
                 })
        {
            ActorResolvedMatchDefinition arm = Arm(bulwark, bulwark, envelope);
            ActorFormDefinition turret = Form(arm, bulwark.PrimeTurretFormId);
            ActorAttackProfileDefinition gun = arm.Rules.AttackProfiles
                .Single(profile => profile.Id == turret.AttackProfileId);

            Assert.False(gun.ShotProgram.Enabled);
            Assert.Equal(1, gun.ShotProgram.MaxBendAfterTiles);
            Assert.Equal(
                ActorAttackProfileDefinition.AimInterpretationKind
                    .AbsoluteSubmittedEightWayHeadingFacingUnchanged,
                gun.AimInterpretation);
            Assert.DoesNotContain("shoot", turret.AllowedActionIds);
        }
    }

    /// <summary>
    /// The grammar is handed to class chassis, so it has no meaning without
    /// one — the same guard skills carry, and the reason the baseline can stay
    /// tokenless.
    /// </summary>
    [Fact]
    public void TheUniversalArmRequiresAClassPair() =>
        Assert.Contains(
            "without a class pair",
            Assert.Throws<ArgumentException>(() =>
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    FrontlineLabsPendulumArm.ContestMajority,
                    classes: null,
                    bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal))
                .Message,
            StringComparison.Ordinal);

    /// <summary>
    /// The universal arm composes with the whole adoption kit, which is the
    /// cell the probes and the phase-2 factorial actually run.
    /// </summary>
    [Fact]
    public void TheWholeAdoptionKitResolvesInsideTheCanonicalIdBudget()
    {
        ActorResolvedMatchDefinition kit =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                skills: FrontlineLabsSkillKit.StrikerVolley
                    | FrontlineLabsSkillKit.BulwarkAegisShell,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-contest-cast-break-bend",
            kit.Rules.RulesetId);
        Assert.True(kit.Rules.RulesetId.Length <= 64);
    }

    private static ActorResolvedMatchDefinition Arm(
        FrontlineLabsClassDefinition zero,
        FrontlineLabsClassDefinition one,
        FrontlineLabsBendEnvelopeArm envelope) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.ContestMajority,
            (zero, one),
            bendEnvelope: envelope);

    private static ActorFormDefinition Form(
        ActorResolvedMatchDefinition arm,
        string formId) =>
        arm.Rules.Forms.Single(form => form.Id == formId);

    private static ActorAttackProfileDefinition MobileGun(
        ActorResolvedMatchDefinition arm,
        FrontlineLabsClassDefinition entry) =>
        arm.Rules.AttackProfiles
            .Single(profile => profile.Id == entry.MobileAttackProfileId);

    private static ActorShotProgramDefinition ProgrammedShotProgram() =>
        new(
            enabled: true,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            minInitialAimSteps: 0,
            maxInitialAimSteps: 0,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: 2,
            minBendEveryTiles: 1,
            maxBendEveryTiles: 1,
            minBendCount: 1,
            maxBendCount: 1,
            launchTiles: 1,
            payloadOptional: true,
            defaultProgram: new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: ActorActionRejectionResult.Rejected,
            unsupportedPayloadResult: ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);
}
