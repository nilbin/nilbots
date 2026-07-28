namespace BotArena.Engine.Tests;

public sealed class ActorRulesDefinitionTests
{
    [Fact]
    public void CompleteDeathmatchSplitFabricationAndAnchorCatalogIsCanonical()
    {
        RulesFixture fixture = ValidFixture();
        RulesFixture reversed = fixture with
        {
            Forms = fixture.Forms.Reverse().ToArray(),
            MovementProfiles = fixture.MovementProfiles.Reverse().ToArray(),
            VisionProfiles = fixture.VisionProfiles.Reverse().ToArray(),
            AttackProfiles = fixture.AttackProfiles.Reverse().ToArray(),
            Actions = fixture.Actions.Reverse().ToArray(),
            FabricationTransitions =
                fixture.FabricationTransitions.Reverse().ToArray(),
            SameLifeTransitions =
                fixture.SameLifeTransitions.Reverse().ToArray(),
            ReplicationTransitions =
                fixture.ReplicationTransitions.Reverse().ToArray(),
        };

        ActorRulesDefinition left = fixture.Build();
        ActorRulesDefinition right = reversed.Build();

        Assert.Equal(ActorRulesDefinition.CurrentSchemaVersion, left.SchemaVersion);
        Assert.Equal(
            ["child-mobile", "prime-mobile", "turret"],
            left.Forms.Select(form => form.Id).ToArray());
        Assert.Equal(
            [
                "wait",
                "move",
                "rotate",
                "shoot",
                "fabricate",
                "anchor",
                "shoot-direction",
                "split",
            ],
            left.Actions.Select(action => action.Id).ToArray());
        Assert.Equal(
            left.Forms.Select(form => form.Id).ToArray(),
            right.Forms.Select(form => form.Id).ToArray());
        Assert.Equal(
            left.Actions.Select(action => (action.Code, action.Id)).ToArray(),
            right.Actions.Select(action => (action.Code, action.Id)).ToArray());
        Assert.Equal(
            left.FabricationTransitions
                .Select(transition => transition.TransitionId)
                .ToArray(),
            right.FabricationTransitions
                .Select(transition => transition.TransitionId)
                .ToArray());
        Assert.Equal(
            left.SameLifeTransitions
                .Select(transition => transition.TransitionId)
                .ToArray(),
            right.SameLifeTransitions
                .Select(transition => transition.TransitionId)
                .ToArray());
        Assert.Equal(
            left.ReplicationTransitions
                .Select(transition => transition.TransitionId)
                .ToArray(),
            right.ReplicationTransitions
                .Select(transition => transition.TransitionId)
                .ToArray());
    }

    [Fact]
    public void SnapshotsCatalogInputs()
    {
        RulesFixture fixture = ValidFixture();
        ActorRulesDefinition rules = fixture.Build();

        fixture.Forms[0] = fixture.Forms[1];
        fixture.Actions[0] = fixture.Actions[1];
        fixture.FabricationTransitions[0] = null!;

        Assert.Equal(
            ["child-mobile", "prime-mobile", "turret"],
            rules.Forms.Select(form => form.Id).ToArray());
        Assert.Equal("wait", rules.Actions[0].Id);
        Assert.Equal(
            "fabricate-child",
            rules.FabricationTransitions.Single().TransitionId);
    }

    [Fact]
    public void RejectsNonCanonicalIdsDuplicatesAndEmptyCatalogs()
    {
        RulesFixture fixture = ValidFixture();
        AssertError(
            fixture with { RulesetId = "Bad Rules" },
            "lowercase-kebab");
        AssertError(
            fixture with
            {
                SeedMechanics = Seed("Bad Seed"),
            },
            "Seed profile ID");
        AssertError(
            fixture with
            {
                Forms =
                [
                    Form(
                        "Bad Form",
                        3,
                        "mobile-vision",
                        "mobile-bolt",
                        ["wait"]),
                    .. fixture.Forms,
                ],
            },
            "Form ID");
        AssertError(
            fixture with
            {
                FabricationTransitions =
                [
                    Fabrication(sourceRegionRoleId: "Bad Role"),
                ],
            },
            "source-region role ID");
        AssertError(
            fixture with
            {
                Forms = [fixture.Forms[0], fixture.Forms[0], .. fixture.Forms[1..]],
            },
            "declared more than once");
        AssertError(
            fixture with
            {
                Actions =
                [
                    .. fixture.Actions,
                    new ActorActionDefinition(
                        "second-wait",
                        code: 0,
                        ActorActionKind.Wait,
                        []),
                ],
            },
            "Action code 0");

        SplitReplicationTransitionDefinition sharedIdSplit = Split(
            transitionId: "anchor-child");
        AssertError(
            fixture with
            {
                ReplicationTransitions = [sharedIdSplit],
            },
            "Transition ID 'anchor-child' is shared");
        AssertError(
            fixture with { Forms = [] },
            "form catalog");
        AssertError(
            fixture with { AttackProfiles = [] },
            "attack profile catalog");
        AssertError(
            fixture with { Forms = [null!, .. fixture.Forms] },
            "cannot contain null");
    }

    [Fact]
    public void RejectsDanglingReferencesAndUnusedCatalogEntries()
    {
        RulesFixture fixture = ValidFixture();
        ActorLifecycleDefinition badLifecycle = new(
        [
            new ActorLifecycleProfileDefinition(
                "prime-respawn",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                delayTicks: 2,
                automaticReturnFormId: "missing-form"),
        ]);
        AssertError(
            fixture with { Lifecycle = badLifecycle },
            "unknown return form");

        ActorFormDefinition badMovement = Form(
            "prime-mobile",
            4,
            "mobile-vision",
            "mobile-bolt",
            ["wait", "move", "rotate", "shoot", "fabricate", "split"],
            movementProfileId: "missing-movement");
        AssertError(
            ReplaceForm(fixture, badMovement),
            "unknown ID 'missing-movement'");

        ActorFormDefinition badAction = Form(
            "prime-mobile",
            4,
            "mobile-vision",
            "mobile-bolt",
            [
                "wait",
                "move",
                "rotate",
                "shoot",
                "fabricate",
                "split",
                "missing-action",
            ]);
        AssertError(
            ReplaceForm(fixture, badAction),
            "unknown action 'missing-action'");

        AssertError(
            fixture with
            {
                MovementProfiles =
                [
                    .. fixture.MovementProfiles,
                    new ActorMovementProfileDefinition(
                        "unused-ground",
                        ActorMovementLayer.Ground),
                ],
            },
            "not used by any form");
        AssertError(
            fixture with
            {
                Actions =
                [
                    .. fixture.Actions,
                    new ActorActionDefinition(
                        "unused-move",
                        code: 20,
                        ActorActionKind.Movement,
                        [ActorActionParameterKind.Direction]),
                ],
            },
            "Action 'unused-move' is not used");
    }

    [Fact]
    public void RequiresAttackAndWaitCapabilitiesToMatchEachForm()
    {
        RulesFixture fixture = ValidFixture();
        ActorFormDefinition attackWithoutProfile = Form(
            "prime-mobile",
            4,
            "mobile-vision",
            attackProfileId: null,
            ["wait", "move", "rotate", "shoot", "fabricate", "split"]);
        AssertError(
            ReplaceForm(fixture, attackWithoutProfile),
            "without an attack profile");

        ActorFormDefinition profileWithoutAttack = Form(
            "prime-mobile",
            4,
            "mobile-vision",
            "mobile-bolt",
            ["wait", "move", "rotate", "fabricate", "split"]);
        AssertError(
            ReplaceForm(fixture, profileWithoutAttack),
            "permits no Attack action");

        ActorFormDefinition turretWithoutWait = Form(
            "turret",
            5,
            "turret-vision",
            "turret-bolt",
            ["shoot-direction"]);
        AssertError(
            ReplaceForm(fixture, turretWithoutWait),
            "must permit at least one Wait-kind action");

        ActorActionDefinition parameterizedWait = new(
            "wait",
            code: 0,
            ActorActionKind.Wait,
            [ActorActionParameterKind.Direction]);
        AssertError(
            ReplaceAction(fixture, parameterizedWait),
            "must be parameterless");
    }

    [Fact]
    public void RejectsUnadmittedAirProfiles()
    {
        RulesFixture fixture = ValidFixture();
        var air = new ActorMovementProfileDefinition(
            "air",
            ActorMovementLayer.Air);
        ActorFormDefinition turret = Form(
            "turret",
            5,
            "turret-vision",
            "turret-bolt",
            ["wait", "shoot-direction"],
            movementProfileId: "air");

        AssertError(
            ReplaceForm(
                fixture with
                {
                    MovementProfiles = [.. fixture.MovementProfiles, air],
                },
                turret),
            "admits only implemented Ground semantics");
    }

    [Fact]
    public void RejectsModeInertObjectiveWeightsAndUnsafeAbsoluteTicks()
    {
        RulesFixture fixture = ValidFixture();
        ActorFormDefinition weightedDeathmatchForm = Form(
            "prime-mobile",
            4,
            "mobile-vision",
            "mobile-bolt",
            ["wait", "move", "rotate", "shoot", "fabricate", "split"],
            objectiveWeight: 1);

        AssertError(
            ReplaceForm(fixture, weightedDeathmatchForm),
            "must use objective weight zero");
        AssertError(
            fixture with
            {
                Limits = new ActorRulesLimits(
                    int.MaxValue,
                    new ActorRuntimeFaultDefinition(0)),
            },
            "beyond the supported 32-bit range");
    }

    [Fact]
    public void RejectsGenericActionShapesTheSchemaThreeKernelCannotInterpret()
    {
        RulesFixture fixture = ValidFixture();
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "move",
                    code: 1,
                    ActorActionKind.Movement,
                    [])),
            "Movement action 'move' must declare exactly Direction");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "rotate",
                    code: 2,
                    ActorActionKind.Rotation,
                    [ActorActionParameterKind.ShotProgram])),
            "Rotation action 'rotate' must declare exactly Direction");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "shoot",
                    code: 3,
                    ActorActionKind.Attack,
                    [ActorActionParameterKind.Direction])),
            "facing-relative programmed profile with exactly ShotProgram");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                "shoot-direction",
                code: 102,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ShotProgram])),
            "omnidirectional profile with disabled shot programs");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "shoot",
                    code: 3,
                    ActorActionKind.Attack,
                    [ActorActionParameterKind.UnitTarget])),
            "has no supported schema-3 payload shape");

        RulesFixture disabledProgram = fixture with
        {
            AttackProfiles = fixture.AttackProfiles
                .Select(profile =>
                    profile.Id == "mobile-bolt"
                        ? Attack(
                            "mobile-bolt",
                            omnidirectional: false,
                            shotProgramsEnabled: false)
                        : profile)
                .ToArray(),
        };
        AssertError(
            disabledProgram,
            "facing-relative non-programmed profile with no parameters");
    }

    [Fact]
    public void ValidatesFabricationAndSplitActionContracts()
    {
        RulesFixture fixture = ValidFixture();
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "fabricate",
                    code: 100,
                    ActorActionKind.Replication,
                    [ActorActionParameterKind.UnitTarget])),
            "requires action 'fabricate'/100");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "fabricate",
                    code: 100,
                    ActorActionKind.Fabrication,
                    [])),
            "exactly UnitTarget");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "fabricate",
                    code: 104,
                    ActorActionKind.Fabrication,
                    [ActorActionParameterKind.UnitTarget])),
            "requires action 'fabricate'/100");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "split",
                    code: 104,
                    ActorActionKind.Replication,
                    [])),
            "parameterless action 'split'/103");
        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "split",
                    code: 103,
                    ActorActionKind.Replication,
                    [ActorActionParameterKind.FormTarget])),
            "parameterless action 'split'/103");

        ActorFormDefinition weakPrime = Form(
            "prime-mobile",
            maxHealth: 1,
            "mobile-vision",
            "mobile-bolt",
            ["wait", "move", "rotate", "shoot", "fabricate", "split"]);
        AssertError(
            ReplaceForm(fixture, weakPrime),
            "needs source health 2");
        AssertError(
            fixture with
            {
                ReplicationTransitions =
                [
                    Split(minimumHealthPerDescendant: 3),
                ],
            },
            "below the required descendant minimum 3");

        ActorFormDefinition noSplitPrime = Form(
            "prime-mobile",
            maxHealth: 4,
            "mobile-vision",
            "mobile-bolt",
            ["wait", "move", "rotate", "shoot", "fabricate"]);
        AssertError(
            ReplaceForm(fixture, noSplitPrime),
            "is not allowed by source form");
    }

    [Fact]
    public void SameLifeTargetPayloadIsRequiredOnlyForAmbiguousRoutes()
    {
        RulesFixture fixture = ValidFixture();
        ActorFormTransitionDefinition secondTarget = Anchor(
            transitionId: "unanchor-child",
            targetFormId: "prime-mobile");
        AssertError(
            fixture with
            {
                SameLifeTransitions =
                    [.. fixture.SameLifeTransitions, secondTarget],
            },
            "resolves 2 targets");

        ActorActionDefinition targetedAnchor = new(
            "anchor",
            code: 101,
            ActorActionKind.SameLifeTransition,
            [ActorActionParameterKind.FormTarget]);
        ActorRulesDefinition targeted = (fixture with
        {
            Actions = fixture.Actions
                .Select(action =>
                    action.Id == targetedAnchor.Id
                        ? targetedAnchor
                        : action)
                .ToArray(),
            SameLifeTransitions =
                [.. fixture.SameLifeTransitions, secondTarget],
        }).Build();

        Assert.Equal(
            ActorActionParameterKind.FormTarget,
            targeted.Actions
                .Single(action => action.Id == "anchor")
                .ParameterKinds
                .Single());

        AssertError(
            ReplaceAction(
                fixture,
                new ActorActionDefinition(
                    "anchor",
                    code: 101,
                    ActorActionKind.Attack,
                    [])),
            "requires a SameLifeTransition action");
    }

    private static void AssertError(
        RulesFixture fixture,
        string expectedFragment)
    {
        ActorRulesValidationException error =
            Assert.Throws<ActorRulesValidationException>(fixture.Build);
        Assert.Contains(
            error.Errors,
            message => message.Contains(
                expectedFragment,
                StringComparison.Ordinal));
    }

    private static RulesFixture ReplaceForm(
        RulesFixture fixture,
        ActorFormDefinition replacement) =>
        fixture with
        {
            Forms = fixture.Forms
                .Select(form =>
                    form.Id == replacement.Id ? replacement : form)
                .ToArray(),
        };

    private static RulesFixture ReplaceAction(
        RulesFixture fixture,
        ActorActionDefinition replacement) =>
        fixture with
        {
            Actions = fixture.Actions
                .Select(action =>
                    action.Id == replacement.Id ? replacement : action)
                .ToArray(),
        };

    private static RulesFixture ValidFixture()
    {
        ActorActionDefinition[] actions =
        [
            new("split", 103, ActorActionKind.Replication, []),
            new(
                "fabricate",
                100,
                ActorActionKind.Fabrication,
                [ActorActionParameterKind.UnitTarget]),
            new("anchor", 101, ActorActionKind.SameLifeTransition, []),
            new(
                "shoot",
                3,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ShotProgram]),
            new(
                "shoot-direction",
                102,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ProjectileHeading]),
            new(
                "rotate",
                2,
                ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
            new(
                "move",
                1,
                ActorActionKind.Movement,
                [ActorActionParameterKind.Direction]),
            new("wait", 0, ActorActionKind.Wait, []),
        ];
        ActorFormDefinition[] forms =
        [
            Form(
                "turret",
                5,
                "turret-vision",
                "turret-bolt",
                ["shoot-direction", "wait"]),
            Form(
                "prime-mobile",
                4,
                "mobile-vision",
                "mobile-bolt",
                ["split", "fabricate", "shoot", "rotate", "move", "wait"]),
            Form(
                "child-mobile",
                2,
                "mobile-vision",
                "mobile-bolt",
                ["anchor", "shoot", "rotate", "move", "wait"]),
        ];

        return new RulesFixture
        {
            RulesetId = "deathmatch-split-proof",
            Limits = new ActorRulesLimits(
                maxTicks: 900,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            SeedMechanics = Seed("deathmatch-split-proof"),
            GameMode = Deathmatch(),
            Lifecycle = Lifecycle(),
            Forms = forms,
            MovementProfiles =
            [
                new("ground", ActorMovementLayer.Ground),
            ],
            VisionProfiles =
            [
                Vision("turret-vision", omnidirectional: true),
                Vision("mobile-vision", omnidirectional: false),
            ],
            AttackProfiles =
            [
                Attack("turret-bolt", omnidirectional: true),
                Attack("mobile-bolt", omnidirectional: false),
            ],
            Actions = actions,
            FabricationTransitions = [Fabrication()],
            SameLifeTransitions = [Anchor()],
            ReplicationTransitions = [Split()],
            TeamPerception = new(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            Collisions = Collisions(),
            TickResolution = new(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                ActorTickResolutionDefinition.CreateSupportedPhases()),
        };
    }

    private static GameModeDefinition Deathmatch()
    {
        var victory = new DeathmatchVictoryDefinition(
            killsToWin: 10,
            [
                new ScoreRankingDefinition(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                new ScoreRankingDefinition(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ]);
        return new DeathmatchGameModeDefinition(
            victory,
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.DamageDealt),
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.Deaths),
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.Kills),
            ],
            DeathmatchScoringDefinition.RawHostileKillV1);
    }

    private static ActorSeedMechanicsDefinition Seed(string profileId) =>
        new(
            profileId,
            ActorSeedMechanicsDefinition.SeedDerivationKind
                .MatchSeedProfileTeamUnitLifeMix64V1,
            ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                .PerStableUnitMonotonicStartingAtZero,
            ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                .FreshRuntimePerLife,
            ActorSeedMechanicsDefinition.PrivateMemoryKind
                .IsolatedPerRuntime);

    private static ActorLifecycleDefinition Lifecycle() =>
        new(
        [
            new ActorLifecycleProfileDefinition(
                "prime-respawn",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                delayTicks: 2,
                automaticReturnFormId: "prime-mobile"),
            new ActorLifecycleProfileDefinition(
                "child-ready",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication,
                delayTicks: 2,
                automaticReturnFormId: null),
        ]);

    private static ActorFormDefinition Form(
        string id,
        int maxHealth,
        string visionProfileId,
        string? attackProfileId,
        IEnumerable<string> allowedActionIds,
        string movementProfileId = "ground",
        int objectiveWeight = 0) =>
        new(
            id,
            maxHealth,
            movementProfileId,
            visionProfileId,
            attackProfileId,
            objectiveWeight,
            allowedActionIds);

    private static ActorVisionProfileDefinition Vision(
        string id,
        bool omnidirectional) =>
        new(
            id,
            range: omnidirectional ? 8 : 6,
            ActorVisionDistanceMetric.Chebyshev,
            omnidirectional
                ? ActorVisionShape.Omnidirectional
                : ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: omnidirectional ? 0 : 1,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 8,
            hearingBearingSectors: 8,
            ActorHearingBearingModel
                .EightOctantsStrictTwoToOneCardinalV1,
            hearingDistanceBandUpperBounds: [2, 5],
            loudEventKinds:
            [
                ActorAudibleEventKind.Destruction,
                ActorAudibleEventKind.Damage,
                ActorAudibleEventKind.Attack,
            ]);

    private static ActorAttackProfileDefinition Attack(
        string id,
        bool omnidirectional,
        bool? shotProgramsEnabled = null)
    {
        bool programsEnabled =
            shotProgramsEnabled ?? !omnidirectional;
        var projectile = new ActorProjectileDefinition(
            ActorProjectileMode.Discrete,
            damagePerHit: 1,
            maxTravelTiles: 8,
            ticksPerAdvance: 1,
            tilesPerAdvance: 2,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);
        var shotProgram = new ActorShotProgramDefinition(
            enabled: programsEnabled,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            minInitialAimSteps: programsEnabled ? -1 : 0,
            maxInitialAimSteps: programsEnabled ? 1 : 0,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: programsEnabled ? 4 : 1,
            minBendEveryTiles: 1,
            maxBendEveryTiles: programsEnabled ? 3 : 1,
            minBendCount: 1,
            maxBendCount: programsEnabled ? 3 : 1,
            launchTiles: 1,
            payloadOptional: programsEnabled,
            defaultProgram: new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: programsEnabled
                ? ActorActionRejectionResult.Rejected
                : null,
            unsupportedPayloadResult: ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);
        return new ActorAttackProfileDefinition(
            id,
            omnidirectional,
            projectile,
            cooldownTicks: 3,
            maxEnergy: 10,
            attackEnergyCost: 5,
            energyRegenerationIntervalTicks: 2,
            energyRegenerationAmount: 1,
            shotProgram);
    }

    private static BoundedChildFabricationDefinition Fabrication(
        string sourceRegionRoleId = "own-fabrication-pad") =>
        new(
            transitionId: "fabricate-child",
            actionId: "fabricate",
            sourceFormIds: ["prime-mobile"],
            outputFormId: "child-mobile",
            sourceRegionRoleId,
            outputRegionRoleId: "own-fabrication-pad",
            requiredSourceTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            ],
            requiredOutputTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            ],
            forbiddenOutputTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind
                    .TransitionPlacementForbidden,
            ],
            candidateOffsets: [new(0, -1), new(0, 1)],
            new ActorFabricationDelayDefinition(durationTicks: 1),
            ActorActionRejectionResult.Blocked);

    private static ActorFormTransitionDefinition Anchor(
        string transitionId = "anchor-child",
        string targetFormId = "turret") =>
        new(
            transitionId,
            actionId: "anchor",
            sourceFormId: "child-mobile",
            targetFormId,
            Windup(durationTicks: 1),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            new ActorSameLifeHealthDefinition(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .AddFlatCappedToTargetMaximum,
                flatHealthGain: 2),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition.PositionContinuityKind
                    .SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition.LegalityEvaluationKind
                    .QueueAndCompletionTileTags,
                requiredTileTags: [],
                forbiddenTileTags:
                [
                    ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden,
                ],
                ActorSameLifePlacementDefinition.FailedCompletionKind
                    .CancelAndRemainInSourceForm),
            irreversibleForLife: true);

    private static SplitReplicationTransitionDefinition Split(
        string transitionId = "split-prime",
        int minimumHealthPerDescendant = 1) =>
        new(
            transitionId,
            actionId: "split",
            sourceFormIds: ["prime-mobile"],
            outputFormId: "child-mobile",
            descendantCount: 2,
            maxSourceGeneration: 0,
            requireNoPriorSameLifeTransition: true,
            new ActorReplicationHealthDefinition(
                ActorReplicationHealthDefinition.DistributionKind
                    .DivideCurrentHealthEquallyFloor,
                minimumHealthPerDescendant,
                ActorReplicationHealthDefinition.RemainderKind.Discard),
            candidateOffsets:
            [
                new(0, -1),
                new(0, 1),
                new(1, 0),
            ],
            Windup(durationTicks: 1));

    private static ActorTransitionWindupDefinition Windup(int durationTicks) =>
        new(
            durationTicks,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);

    private static ActorCollisionDefinition Collisions() =>
        new(
            actorsBlockWalls: true,
            actorsBlockActors: true,
            sameDestinationMovesBlockAll: true,
            swapMovesBlocked: true,
            followingVacatedActorAllowed: false,
            projectilesBlockMovement: true,
            movingOntoProjectileCausesHit: true,
            wallsConsumeProjectiles: true,
            projectilesIgnoreFiringLife: true,
            projectilesStopOnFirstEnemyActor: true,
            projectilesCollideWithProjectiles: false,
            ActorCollisionDefinition.AlliedProjectileContactKind
                .BlockWithoutDamage);

    private sealed record RulesFixture
    {
        public required string RulesetId { get; init; }
        public required ActorRulesLimits Limits { get; init; }
        public required ActorSeedMechanicsDefinition SeedMechanics { get; init; }
        public required GameModeDefinition GameMode { get; init; }
        public required ActorLifecycleDefinition Lifecycle { get; init; }
        public required ActorFormDefinition[] Forms { get; init; }
        public required ActorMovementProfileDefinition[] MovementProfiles
        {
            get;
            init;
        }
        public required ActorVisionProfileDefinition[] VisionProfiles
        {
            get;
            init;
        }
        public required ActorAttackProfileDefinition[] AttackProfiles
        {
            get;
            init;
        }
        public required ActorActionDefinition[] Actions { get; init; }
        public required ActorFabricationTransitionDefinition[]
            FabricationTransitions { get; init; }
        public required ActorSameLifeTransitionDefinition[]
            SameLifeTransitions { get; init; }
        public required ActorReplicationTransitionDefinition[]
            ReplicationTransitions { get; init; }
        public required ActorTeamPerceptionDefinition TeamPerception
        {
            get;
            init;
        }
        public required ActorCollisionDefinition Collisions { get; init; }
        public required ActorTickResolutionDefinition TickResolution
        {
            get;
            init;
        }

        public ActorRulesDefinition Build() =>
            new(
                RulesetId,
                Limits,
                SeedMechanics,
                GameMode,
                Lifecycle,
                Forms,
                MovementProfiles,
                VisionProfiles,
                AttackProfiles,
                Actions,
                FabricationTransitions,
                SameLifeTransitions,
                ReplicationTransitions,
                TeamPerception,
                Collisions,
                TickResolution);
    }
}
