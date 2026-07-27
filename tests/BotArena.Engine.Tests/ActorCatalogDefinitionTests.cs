namespace BotArena.Engine.Tests;

public class ActorCatalogDefinitionTests
{
    [Fact]
    public void CanonicalizesTrueSetsAndSnapshotsMutableInputs()
    {
        var parameters = new[]
        {
            ActorActionParameterKind.FormTarget,
            ActorActionParameterKind.Direction,
        };
        var allowedActionIds = new[] { "split", "move", "wait" };
        var hearingBands = new[] { 2, 5 };
        var loudKinds = new[]
        {
            ActorAudibleEventKind.Destruction,
            ActorAudibleEventKind.Attack,
            ActorAudibleEventKind.Damage,
        };
        var bendDirections = new[] { 1, -1 };

        var action = new ActorActionDefinition(
            "transition",
            code: 100,
            ActorActionKind.SameLifeTransition,
            parameters);
        var form = new ActorFormDefinition(
            "mobile",
            maxHealth: 3,
            movementProfileId: "ground",
            visionProfileId: "standard",
            attackProfileId: "bolt",
            objectiveWeight: 1,
            allowedActionIds);
        ActorVisionProfileDefinition vision = Vision(
            hearingBands,
            loudKinds);
        ActorShotProgramDefinition shotProgram = ShotProgram(bendDirections);

        parameters[0] = ActorActionParameterKind.ShotProgram;
        allowedActionIds[0] = "changed";
        hearingBands[0] = 4;
        loudKinds[0] = ActorAudibleEventKind.Attack;
        bendDirections[0] = -1;

        Assert.Equal(
            new[]
            {
                ActorActionParameterKind.Direction,
                ActorActionParameterKind.FormTarget,
            },
            action.ParameterKinds.ToArray());
        Assert.Equal(
            new[] { "move", "split", "wait" },
            form.AllowedActionIds.ToArray());
        Assert.Equal(
            new[] { 2, 5 },
            vision.HearingDistanceBandUpperBounds.ToArray());
        Assert.Equal(
            new[]
            {
                ActorAudibleEventKind.Attack,
                ActorAudibleEventKind.Damage,
                ActorAudibleEventKind.Destruction,
            },
            vision.LoudEventKinds.ToArray());
        Assert.Equal(
            new[] { -1, 1 },
            shotProgram.AllowedCurvedBendDirections.ToArray());

        Assert.Equal(
            action.ParameterKinds.ToArray(),
            new ActorActionDefinition(
                "transition",
                100,
                ActorActionKind.SameLifeTransition,
                [
                    ActorActionParameterKind.Direction,
                    ActorActionParameterKind.FormTarget,
                ]).ParameterKinds.ToArray());
        Assert.Equal(
            form.AllowedActionIds.ToArray(),
            new ActorFormDefinition(
                "mobile",
                3,
                "ground",
                "standard",
                "bolt",
                1,
                ["wait", "split", "move"]).AllowedActionIds.ToArray());
    }

    [Fact]
    public void DefinesCurrentCapabilitiesWithoutRoutingThem()
    {
        var limits = new ActorRulesLimits(
            maxTicks: 900,
            new ActorRuntimeFaultDefinition(
                faultsAllowedBeforeDisqualification: 0));
        var movement = new ActorMovementProfileDefinition(
            "ground",
            ActorMovementLayer.Ground);
        ActorVisionProfileDefinition vision = Vision(
            [2, 5],
            [
                ActorAudibleEventKind.Attack,
                ActorAudibleEventKind.Damage,
                ActorAudibleEventKind.Destruction,
            ]);
        ActorProjectileDefinition projectile = Projectile();
        ActorShotProgramDefinition shotProgram = ShotProgram([-1, 1]);
        var attack = new ActorAttackProfileDefinition(
            "bolt",
            omnidirectionalAim: false,
            projectile,
            cooldownTicks: 3,
            maxEnergy: 10,
            attackEnergyCost: 5,
            energyRegenerationIntervalTicks: 2,
            energyRegenerationAmount: 1,
            shotProgram);
        var split = new ActorActionDefinition(
            "split",
            code: 103,
            ActorActionKind.Replication,
            []);
        var fabricate = new ActorActionDefinition(
            "fabricate",
            code: 100,
            ActorActionKind.Fabrication,
            [ActorActionParameterKind.UnitTarget]);
        var form = new ActorFormDefinition(
            "mobile",
            maxHealth: 3,
            movement.Id,
            vision.Id,
            attack.Id,
            objectiveWeight: 1,
            ["split", "wait"]);

        Assert.Equal(900, limits.MaxTicks);
        Assert.Equal(
            ActorRuntimeFaultDefinition.AccumulationScopeKind
                .ParticipantAcrossAllSlotsLivesAndRuntimeStages,
            limits.RuntimeFaults.AccumulationScope);
        Assert.Equal(1, limits.RuntimeFaults.DisqualificationFaultCount);
        Assert.Equal(
            2_147_483_648L,
            new ActorRuntimeFaultDefinition(int.MaxValue)
                .DisqualificationFaultCount);
        Assert.Equal(
            ActorRuntimeFaultDefinition.FaultCounterArithmeticKind
                .SignedInt64SaturatingAtAllowedPlusOne,
            limits.RuntimeFaults.FaultCounterArithmetic);
        Assert.Equal(
            ActorRuntimeFaultDefinition.RuntimeStageRecoveryKind
                .CreateOrStartFailureDiscardsInstanceSyntheticWaitRetryFreshOnceNextActiveTick,
            limits.RuntimeFaults.RuntimeStageRecovery);
        Assert.Equal(
            ActorRuntimeFaultDefinition.FaultBatchEventOrderKind
                .ParticipantThenActorIdentityThenCreateStartTickValidationStage,
            limits.RuntimeFaults.FaultBatchEventOrder);
        Assert.Equal(
            ActorRuntimeFaultDefinition.PendingWorkDispositionKind
                .CancelAllOwnedClocksBundlesAndTransitionsReleaseEveryClaim,
            limits.RuntimeFaults.PendingWorkDisposition);
        Assert.Equal(
            ActorRuntimeFaultDefinition.OwnedProjectileDispositionKind
                .RemoveAfterJointDamageByProjectileIdWithoutContactOrScore,
            limits.RuntimeFaults.OwnedProjectileDisposition);
        Assert.Equal(ActorMovementLayer.Ground, movement.MovementLayer);
        Assert.Equal(ActorVisionShape.FacingQuadrant, vision.Shape);
        Assert.Equal(
            ActorVisionProfileDefinition.HearingDistanceBandModelKind
                .ChebyshevInclusiveOrderedUpperBoundsThenFinalRadiusBand,
            vision.HearingDistanceBandModel);
        Assert.Equal(2, projectile.TilesPerAdvance);
        Assert.True(shotProgram.Enabled);
        Assert.Equal(5, attack.AttackEnergyCost);
        Assert.Equal(
            ActorAttackProfileDefinition.AimInterpretationKind
                .CurrentFacingPlusRelativeEightWayShotProgram,
            attack.AimInterpretation);
        Assert.Equal(
            ActorAttackProfileDefinition.EnergyArithmeticKind
                .CheckedInt64ThenClampToMaximum,
            attack.EnergyArithmetic);
        Assert.Equal(ActorActionKind.Replication, split.Kind);
        Assert.Equal(ActorActionKind.Fabrication, fabricate.Kind);
        Assert.Equal("bolt", form.AttackProfileId);
    }

    [Fact]
    public void RejectsUnknownEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorMovementProfileDefinition(
                "unknown",
                (ActorMovementLayer)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "unknown",
                8,
                (ActorVisionDistanceMetric)99,
                ActorVisionShape.Omnidirectional,
                0,
                ActorLineOfSightModel.CornerStrictSupercover,
                0,
                0,
                ActorHearingBearingModel.Disabled,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "unknown",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                (ActorVisionShape)99,
                0,
                ActorLineOfSightModel.CornerStrictSupercover,
                0,
                0,
                ActorHearingBearingModel.Disabled,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "unknown",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.Omnidirectional,
                0,
                (ActorLineOfSightModel)99,
                0,
                0,
                ActorHearingBearingModel.Disabled,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "unknown",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.Omnidirectional,
                0,
                ActorLineOfSightModel.CornerStrictSupercover,
                0,
                0,
                (ActorHearingBearingModel)99,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vision(
                [2, 5],
                [(ActorAudibleEventKind)99]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorProjectileDefinition(
                (ActorProjectileMode)99,
                1,
                8,
                1,
                1,
                1,
                false,
                true,
                true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorActionDefinition(
                "unknown",
                1,
                (ActorActionKind)99,
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorActionDefinition(
                "unknown",
                1,
                ActorActionKind.Attack,
                [(ActorActionParameterKind)99]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(
                unsupportedPayloadResult:
                    (ActorActionRejectionResult)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(
                invalidPayloadResult:
                    (ActorActionRejectionResult)99));
    }

    [Fact]
    public void RejectsInvalidLimitsIdsActionsAndForms()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorRulesLimits(
                maxTicks: 0,
                new ActorRuntimeFaultDefinition(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorRuntimeFaultDefinition(
                faultsAllowedBeforeDisqualification: -1));
        Assert.Throws<ArgumentException>(() =>
            new ActorMovementProfileDefinition(
                " ",
                ActorMovementLayer.Ground));
        Assert.Throws<ArgumentException>(() =>
            new ActorActionDefinition(
                "",
                0,
                ActorActionKind.Wait,
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorActionDefinition(
                "wait",
                -1,
                ActorActionKind.Wait,
                []));
        Assert.Throws<ArgumentException>(() =>
            new ActorActionDefinition(
                "shoot",
                3,
                ActorActionKind.Attack,
                [
                    ActorActionParameterKind.Direction,
                    ActorActionParameterKind.Direction,
                ]));
        Assert.Throws<ArgumentException>(() =>
            new ActorFormDefinition(
                "mobile",
                3,
                "ground",
                "standard",
                "bolt",
                1,
                ["wait", "wait"]));
        Assert.Throws<ArgumentException>(() =>
            new ActorFormDefinition(
                "mobile",
                3,
                "ground",
                "standard",
                "bolt",
                1,
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorFormDefinition(
                "mobile",
                0,
                "ground",
                "standard",
                null,
                0,
                ["wait"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorFormDefinition(
                "mobile",
                1,
                "ground",
                "standard",
                null,
                -1,
                ["wait"]));
        Assert.Throws<ArgumentException>(() =>
            new ActorFormDefinition(
                "mobile",
                1,
                " ",
                "standard",
                null,
                0,
                ["wait"]));
        Assert.Throws<ArgumentException>(() =>
            new ActorFormDefinition(
                "mobile",
                1,
                "ground",
                "standard",
                " ",
                0,
                ["wait"]));
    }

    [Fact]
    public void RejectsContradictoryVisionAndHearingValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "bad",
                -1,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.Omnidirectional,
                0,
                ActorLineOfSightModel.CornerStrictSupercover,
                0,
                0,
                ActorHearingBearingModel.Disabled,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "bad",
                3,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.FacingQuadrant,
                4,
                ActorLineOfSightModel.CornerStrictSupercover,
                0,
                0,
                ActorHearingBearingModel.Disabled,
                [],
                []));
        Assert.Throws<ArgumentException>(() =>
            new ActorVisionProfileDefinition(
                "bad",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.FacingQuadrant,
                1,
                ActorLineOfSightModel.CornerStrictSupercover,
                0,
                8,
                ActorHearingBearingModel
                    .EightOctantsStrictTwoToOneCardinalV1,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "bad",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.FacingQuadrant,
                1,
                ActorLineOfSightModel.CornerStrictSupercover,
                8,
                0,
                ActorHearingBearingModel.Disabled,
                [],
                [ActorAudibleEventKind.Attack]));
        Assert.Throws<ArgumentException>(() =>
            new ActorVisionProfileDefinition(
                "bad",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.FacingQuadrant,
                1,
                ActorLineOfSightModel.CornerStrictSupercover,
                8,
                8,
                ActorHearingBearingModel
                    .EightOctantsStrictTwoToOneCardinalV1,
                [],
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorVisionProfileDefinition(
                "bad",
                8,
                ActorVisionDistanceMetric.Chebyshev,
                ActorVisionShape.FacingQuadrant,
                1,
                ActorLineOfSightModel.CornerStrictSupercover,
                8,
                4,
                ActorHearingBearingModel
                    .EightOctantsStrictTwoToOneCardinalV1,
                [2, 5],
                [ActorAudibleEventKind.Attack]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vision(
                [5, 2],
                [ActorAudibleEventKind.Attack]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vision(
                [2, 8],
                [ActorAudibleEventKind.Attack]));
        Assert.Throws<ArgumentException>(() =>
            Vision(
                [2, 5],
                [
                    ActorAudibleEventKind.Attack,
                    ActorAudibleEventKind.Attack,
                ]));
    }

    [Fact]
    public void RejectsInvalidProjectileShotAndEnergyValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProjectile(damagePerHit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProjectile(maxTravelTiles: 0));
        Assert.Throws<ArgumentException>(() =>
            CreateProjectile(
                mode: ActorProjectileMode.Discrete,
                ticksPerAdvance: 0));
        Assert.Throws<ArgumentException>(() =>
            CreateProjectile(
                mode: ActorProjectileMode.InstantRay,
                ticksPerAdvance: 1));
        Assert.Throws<ArgumentException>(() =>
            CreateProjectile(
                mode: ActorProjectileMode.InstantRay,
                ticksPerAdvance: 0,
                tilesPerAdvance: 2));
        Assert.Throws<ArgumentException>(() =>
            CreateProjectile(advancesOnLaunchTick: true));
        Assert.Throws<ArgumentException>(() =>
            CreateProjectile(damageAppliedSimultaneously: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProjectile(maxTravelTiles: 4, launchTiles: 5));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(headingSectors: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(headingSectors: 7));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(
                headingModel: (ActorShotHeadingModel)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(headingSectors: 8, bendStepSectors: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(headingSectors: 8, bendStepSectors: 2));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(minInitialAimSteps: 2, maxInitialAimSteps: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(minInitialAimSteps: -5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(minBendAfterTiles: 3, maxBendAfterTiles: 2));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(allowedCurvedBendDirections: []));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(allowedCurvedBendDirections: [-1, -1]));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(allowedCurvedBendDirections: [-1, 0, 1]));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(allowedCurvedBendDirections: [-1, 2]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShotProgram(
                aimOnlyProgram: new ActorAimOnlyShotProgramDefinition(
                    1,
                    0,
                    1,
                    0)));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(
                defaultProgram: new ActorShotProgramValue(
                    0,
                    1,
                    5,
                    1,
                    1)));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(
                enabled: true,
                invalidPayloadResult: null));
        Assert.Throws<ArgumentException>(() =>
            CreateShotProgram(
                enabled: false,
                invalidPayloadResult: ActorActionRejectionResult.Rejected));

        ActorProjectileDefinition projectile = Projectile();
        ActorShotProgramDefinition shotProgram = ShotProgram([-1, 1]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAttack(
                projectile,
                shotProgram,
                cooldownTicks: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAttack(
                projectile,
                shotProgram,
                maxEnergy: 3,
                attackEnergyCost: 4));
        Assert.Throws<ArgumentException>(() =>
            CreateAttack(
                projectile,
                shotProgram,
                energyRegenerationIntervalTicks: 0,
                energyRegenerationAmount: 1));
        Assert.Throws<ArgumentException>(() =>
            CreateAttack(
                projectile,
                CreateShotProgram(launchTiles: 2)));
        Assert.Throws<ArgumentException>(() =>
            CreateAttack(
                projectile,
                CreateShotProgram(diagonalCornersMustBeClear: false)));
    }

    private static ActorVisionProfileDefinition Vision(
        IEnumerable<int> hearingDistanceBandUpperBounds,
        IEnumerable<ActorAudibleEventKind> loudEventKinds) =>
        new(
            "standard",
            range: 8,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 1,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 8,
            hearingBearingSectors: 8,
            ActorHearingBearingModel
                .EightOctantsStrictTwoToOneCardinalV1,
            hearingDistanceBandUpperBounds,
            loudEventKinds);

    private static ActorProjectileDefinition Projectile() =>
        CreateProjectile();

    private static ActorProjectileDefinition CreateProjectile(
        ActorProjectileMode mode = ActorProjectileMode.Discrete,
        int damagePerHit = 1,
        int maxTravelTiles = 8,
        int ticksPerAdvance = 1,
        int tilesPerAdvance = 2,
        int launchTiles = 1,
        bool advancesOnLaunchTick = false,
        bool damageAppliedSimultaneously = true,
        bool diagonalCornersMustBeClear = true) =>
        new(
            mode,
            damagePerHit,
            maxTravelTiles,
            ticksPerAdvance,
            tilesPerAdvance,
            launchTiles,
            advancesOnLaunchTick,
            damageAppliedSimultaneously,
            diagonalCornersMustBeClear);

    private static ActorShotProgramDefinition ShotProgram(
        IEnumerable<int> allowedCurvedBendDirections) =>
        CreateShotProgram(
            allowedCurvedBendDirections: allowedCurvedBendDirections);

    private static ActorShotProgramDefinition CreateShotProgram(
        bool enabled = true,
        int headingSectors = 8,
        ActorShotHeadingModel headingModel =
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
        int bendStepSectors = 1,
        int minInitialAimSteps = -1,
        int maxInitialAimSteps = 1,
        ActorAimOnlyShotProgramDefinition? aimOnlyProgram = null,
        IEnumerable<int>? allowedCurvedBendDirections = null,
        int minBendAfterTiles = 1,
        int maxBendAfterTiles = 4,
        int minBendEveryTiles = 1,
        int maxBendEveryTiles = 3,
        int minBendCount = 1,
        int maxBendCount = 3,
        int launchTiles = 1,
        bool payloadOptional = true,
        ActorShotProgramValue? defaultProgram = null,
        ActorActionRejectionResult? invalidPayloadResult =
            ActorActionRejectionResult.Rejected,
        ActorActionRejectionResult unsupportedPayloadResult =
            ActorActionRejectionResult.Blocked,
        bool diagonalCornersMustBeClear = true) =>
        new(
            enabled,
            headingSectors,
            headingModel,
            bendStepSectors,
            minInitialAimSteps,
            maxInitialAimSteps,
            aimOnlyProgram ?? new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections ?? [-1, 1],
            minBendAfterTiles,
            maxBendAfterTiles,
            minBendEveryTiles,
            maxBendEveryTiles,
            minBendCount,
            maxBendCount,
            launchTiles,
            payloadOptional,
            defaultProgram ?? new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult,
            unsupportedPayloadResult,
            diagonalCornersMustBeClear);

    private static ActorAttackProfileDefinition CreateAttack(
        ActorProjectileDefinition projectile,
        ActorShotProgramDefinition shotProgram,
        int cooldownTicks = 3,
        int maxEnergy = 10,
        int attackEnergyCost = 5,
        int energyRegenerationIntervalTicks = 2,
        int energyRegenerationAmount = 1) =>
        new(
            "bolt",
            omnidirectionalAim: false,
            projectile,
            cooldownTicks,
            maxEnergy,
            attackEnergyCost,
            energyRegenerationIntervalTicks,
            energyRegenerationAmount,
            shotProgram);
}
