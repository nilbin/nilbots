using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class ActorResolvedMatchDefinitionTests
{
    [Fact]
    public void SameDeathmatchRulesAndMapAcceptHeadToHeadFfaFourAndTwoByTwo()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();

        ActorResolvedMatchDefinition headToHead = Resolve(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            CreateTopology([[10], [20]]),
            ["west", "east"]);
        ActorResolvedMatchDefinition freeForAll = Resolve(
            rules,
            map,
            new FreeForAllMatchFormatDefinition(4),
            CreateTopology([[10], [20], [30], [40]]),
            ["west", "east", "north", "south"]);
        ActorResolvedMatchDefinition twoByTwo = Resolve(
            rules,
            map,
            new TeamsMatchFormatDefinition(2, 2),
            CreateTopology([[10, 11], [20, 21]]),
            ["west", "north", "east", "south"]);

        Assert.Same(rules, headToHead.Rules);
        Assert.Same(rules, freeForAll.Rules);
        Assert.Same(rules, twoByTwo.Rules);
        Assert.Same(map, headToHead.Map);
        Assert.Same(map, freeForAll.Map);
        Assert.Same(map, twoByTwo.Map);
        Assert.Equal(
            ActorResolvedMatchDefinition.CurrentSchemaVersion,
            headToHead.SchemaVersion);
        Assert.Same(
            ActorMatchCapabilityVersions.Current,
            headToHead.CapabilityVersions);
        Assert.Equal([2, 4, 4],
            new[]
            {
                headToHead.Topology.Participants.Length,
                freeForAll.Topology.Participants.Length,
                twoByTwo.Topology.Participants.Length,
            });
    }

    [Fact]
    public void ReversedIdentitySetsProduceTheSameCanonicalSnapshots()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        PublicMatchTopology ordered =
            CreateTopology([[10], [20], [30], [40]]);
        InitialDeploymentDefinition deployment = CreateDeployment(
            ordered,
            map,
            ["west", "east", "north", "south"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateLifecycleAssignments(ordered, deployment, includeChild: false);
        PublicMatchTopology reversed = ordered with
        {
            Teams = ordered.Teams.Reverse().ToImmutableArray(),
            Participants = ordered.Participants.Reverse().ToImmutableArray(),
            UnitSlots = ordered.UnitSlots.Reverse().ToImmutableArray(),
            InitialLives = ordered.InitialLives.Reverse().ToImmutableArray(),
        };

        var resolved = new ActorResolvedMatchDefinition(
            rules,
            map,
            new FreeForAllMatchFormatDefinition(4),
            reversed,
            deployment,
            assignments.Reverse(),
            [],
            new DeathmatchActorModeMapBindingDefinition());

        Assert.Equal([0, 1, 2, 3],
            resolved.Topology.Teams.Select(team => team.TeamId).ToArray());
        Assert.Equal([10, 20, 30, 40],
            resolved.Topology.Participants
                .Select(participant => participant.ParticipantId)
                .ToArray());
        Assert.Equal(
            [(0, 0), (1, 0), (2, 0), (3, 0)],
            resolved.LifecycleAssignments
                .Select(assignment => (assignment.TeamId, assignment.UnitId))
                .ToArray());
    }

    [Fact]
    public void RejectsMissingCoverageUnknownSpawnAndWrongModeBinding()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            map,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateLifecycleAssignments(topology, deployment, includeChild: false);

        ActorResolvedMatchValidationException missing =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    deployment,
                    assignments.Take(1),
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));
        ActorUnitSlotLifecycleAssignmentDefinition unknownSpawn =
            ActiveAssignment(
                teamId: 0,
                unitId: 0,
                lifecycleProfileId: "automatic-mobile",
                allowedFormIds: ["mobile"],
                assignedRespawnSpawnId: "ghost");
        ActorResolvedMatchValidationException crossReference =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    deployment,
                    [unknownSpawn, assignments[1]],
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));
        ActorResolvedMatchValidationException wrongBinding =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    deployment,
                    assignments,
                    [],
                    new FrontlineActorModeMapBindingDefinition(
                        ["objective"],
                        [
                            new(
                                1,
                                FrontlineTeamAdvanceDefinition
                                    .ObjectiveAdvanceDirection.TowardLowerIndex),
                            new(
                                0,
                                FrontlineTeamAdvanceDefinition
                                    .ObjectiveAdvanceDirection.TowardHigherIndex),
                        ])));

        Assert.Contains(missing.Errors, error =>
            error.Contains("has no lifecycle assignment", StringComparison.Ordinal));
        Assert.Equal(
            missing.Errors.Order(StringComparer.Ordinal).ToArray(),
            missing.Errors.ToArray());
        Assert.Contains(crossReference.Errors, error =>
            error.Contains("unknown respawn spawn 'ghost'", StringComparison.Ordinal));
        Assert.Contains(wrongBinding.Errors, error =>
            error.Contains("Deathmatch requires", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsNonRootInitialIdentityAndSharedAutomaticRespawnSpawn()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            map,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateLifecycleAssignments(topology, deployment, includeChild: false);

        PublicMatchTopology nonRootTopology = topology with
        {
            InitialLives =
            [
                new(0, 0, 1, "mobile"),
                topology.InitialLives.Single(life => life.TeamId == 1),
            ],
        };
        InitialDeploymentDefinition nonRootDeployment = CreateDeployment(
            nonRootTopology,
            map,
            ["west", "east"]);
        ActorResolvedMatchValidationException nonRoot =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    nonRootTopology,
                    nonRootDeployment,
                    assignments,
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));

        ActorUnitSlotLifecycleAssignmentDefinition sharedWestZero =
            ActiveAssignment(
                0,
                0,
                "automatic-mobile",
                ["mobile"],
                "west");
        ActorUnitSlotLifecycleAssignmentDefinition sharedWestOne =
            ActiveAssignment(
                1,
                0,
                "automatic-mobile",
                ["mobile"],
                "west");
        ActorResolvedMatchValidationException shared =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    deployment,
                    [sharedWestZero, sharedWestOne],
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));
        ActorResolvedMatchValidationException crossed =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    deployment,
                    [
                        ActiveAssignment(
                            0,
                            0,
                            "automatic-mobile",
                            ["mobile"],
                            "east"),
                        ActiveAssignment(
                            1,
                            0,
                            "automatic-mobile",
                            ["mobile"],
                            "west"),
                    ],
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));
        ActorUnitSlotLifecycleAssignmentDefinition overflowGeneration = new(
            teamId: 0,
            unitId: 0,
            lifecycleProfileId: "automatic-mobile",
            initialGeneration: int.MaxValue,
            allowedFormIds: ["mobile"],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.ActiveAtTickZero,
            unlockTick: null,
            assignedRespawnSpawnId: "west");
        ActorResolvedMatchValidationException generation =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    deployment,
                    [overflowGeneration, assignments[1]],
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));

        Assert.Contains(nonRoot.Errors, error =>
            error.Contains("life ID 0 and lineage generation 0",
                StringComparison.Ordinal));
        Assert.Contains(shared.Errors, error =>
            error.Contains("is shared by stable slots", StringComparison.Ordinal));
        Assert.Equal(
            2,
            crossed.Errors.Count(error =>
                error.Contains(
                    "is initially occupied by",
                    StringComparison.Ordinal)));
        Assert.Contains(generation.Errors, error =>
            error.Contains("life ID 0 and lineage generation 0",
                StringComparison.Ordinal));
    }

    [Fact]
    public void SplitCountsOnlyReadySameControllerSlotsAndProvesMapOffsets()
    {
        ActorRulesDefinition rules = CreateRules(includeSplit: true);
        PublicMatchTopology expandable = CreateExpandableTopology(
            additionalSlotsInitiallyActive: false);
        ActorMapDefinition map = CreateMap();
        InitialDeploymentDefinition deployment = CreateDeployment(
            expandable,
            map,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] readyAssignments =
            CreateLifecycleAssignments(
                expandable,
                deployment,
                includeChild: true);

        var accepted = new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            expandable,
            deployment,
            readyAssignments,
            [],
            new DeathmatchActorModeMapBindingDefinition());

        PublicMatchTopology allActive = CreateExpandableTopology(
            additionalSlotsInitiallyActive: true);
        InitialDeploymentDefinition allActiveDeployment = CreateDeployment(
            allActive,
            map,
            ["west", "north", "east", "south"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] allActiveAssignments =
            CreateLifecycleAssignments(
                allActive,
                allActiveDeployment,
                includeChild: true);
        ActorResolvedMatchValidationException unavailable =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    allActive,
                    allActiveDeployment,
                    allActiveAssignments,
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));

        ActorMapDefinition crampedMap = CreateCrampedMap();
        InitialDeploymentDefinition crampedDeployment = CreateDeployment(
            expandable,
            crampedMap,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] crampedAssignments =
            CreateLifecycleAssignments(
                expandable,
                crampedDeployment,
                includeChild: true);
        ActorResolvedMatchValidationException impossibleMap =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    crampedMap,
                    new HeadToHeadMatchFormatDefinition(),
                    expandable,
                    crampedDeployment,
                    crampedAssignments,
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));
        PublicMatchTopology sourceIncompatible = new()
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(20, 1)],
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 10),
                new(0, 2, 10),
                new(1, 0, 20),
                new(1, 1, 20),
                new(1, 2, 20),
            ],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(1, 0, 0, "mobile"),
            ],
        };
        InitialDeploymentDefinition sourceIncompatibleDeployment =
            CreateDeployment(
                sourceIncompatible,
                map,
                ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] sourceIncompatibleAssignments =
        [
            ActiveAssignment(
                0,
                0,
                "automatic-mobile",
                ["mobile"],
                "west"),
            DormantChildAssignment(0, 1),
            DormantChildAssignment(0, 2),
            ActiveAssignment(
                1,
                0,
                "automatic-mobile",
                ["mobile"],
                "east"),
            DormantChildAssignment(1, 1),
            DormantChildAssignment(1, 2),
        ];
        ActorResolvedMatchValidationException incompatibleSource =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    map,
                    new HeadToHeadMatchFormatDefinition(),
                    sourceIncompatible,
                    sourceIncompatibleDeployment,
                    sourceIncompatibleAssignments,
                    [],
                    new DeathmatchActorModeMapBindingDefinition()));

        Assert.Equal(4, accepted.LifecycleAssignments.Length);
        Assert.Contains(unavailable.Errors, error =>
            error.Contains(
                "insufficient same-controller output slots",
                StringComparison.Ordinal));
        Assert.Contains(impossibleMap.Errors, error =>
            error.Contains("no map floor pose", StringComparison.Ordinal));
        Assert.Equal(
            2,
            incompatibleSource.Errors.Count(error =>
                error.Contains(
                    "reused source slot",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void FabricationRequiresJointRegionOffsetFeasibility()
    {
        ActorRulesDefinition rules = CreateRules(includeFabrication: true);
        PublicMatchTopology topology = CreateExpandableTopology(
            additionalSlotsInitiallyActive: false);
        ActorMapDefinition reachableMap = CreateMap(
            includeFabricationRegions: true,
            fabricationOffsetsReach: true);
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            reachableMap,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateLifecycleAssignments(
                topology,
                deployment,
                includeChild: true);
        ActorParticipantRegionAssignmentDefinition[] regionAssignments =
            CreateFabricationRegionAssignments();

        var accepted = new ActorResolvedMatchDefinition(
            rules,
            reachableMap,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            assignments,
            regionAssignments.Reverse(),
            new DeathmatchActorModeMapBindingDefinition());

        ActorMapDefinition unreachableMap = CreateMap(
            includeFabricationRegions: true,
            fabricationOffsetsReach: false);
        InitialDeploymentDefinition unreachableDeployment = CreateDeployment(
            topology,
            unreachableMap,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] unreachableAssignments =
            CreateLifecycleAssignments(
                topology,
                unreachableDeployment,
                includeChild: true);
        ActorResolvedMatchValidationException impossible =
            Assert.Throws<ActorResolvedMatchValidationException>(() =>
                new ActorResolvedMatchDefinition(
                    rules,
                    unreachableMap,
                    new HeadToHeadMatchFormatDefinition(),
                    topology,
                    unreachableDeployment,
                    unreachableAssignments,
                    regionAssignments,
                    new DeathmatchActorModeMapBindingDefinition()));

        Assert.Equal(
            [(10, "output-pad"), (10, "source-pad"),
             (20, "output-pad"), (20, "source-pad")],
            accepted.ParticipantRegionAssignments
                .Select(assignment =>
                    (assignment.ParticipantId, assignment.RegionRoleId))
                .ToArray());
        Assert.Contains(impossible.Errors, error =>
            error.Contains(
                "rotated candidate offset reaching",
                StringComparison.Ordinal));
    }

    [Fact]
    public void FrontlineBindingPreservesObjectiveOrderAndCanonicalizesTeams()
    {
        FrontlineGameModeDefinition mode = CreateFrontlineMode();
        ActorRulesDefinition rules = CreateRules(mode);
        ActorMapDefinition map = CreateMap(includeFrontlineRegions: true);
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            map,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateLifecycleAssignments(topology, deployment, includeChild: false);
        string[] objectiveOrder =
        [
            "far-west",
            "near-west",
            "centre",
            "near-east",
            "far-east",
        ];

        var resolved = new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            assignments,
            [],
            new FrontlineActorModeMapBindingDefinition(
                objectiveOrder,
                [
                    new(
                        1,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardLowerIndex),
                    new(
                        0,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                ]));

        var binding = Assert.IsType<
            FrontlineActorModeMapBindingDefinition>(resolved.ModeMapBinding);
        Assert.Equal(objectiveOrder, binding.OrderedObjectiveRegionIds.ToArray());
        Assert.Equal([0, 1],
            binding.TeamAdvances.Select(advance => advance.TeamId).ToArray());
        Assert.Throws<ArgumentException>(() =>
            new FrontlineActorModeMapBindingDefinition(
                objectiveOrder,
                [
                    new(
                        0,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                    new(
                        1,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                ]));
    }

    private static ActorResolvedMatchDefinition Resolve(
        ActorRulesDefinition rules,
        ActorMapDefinition map,
        MatchFormatDefinition format,
        PublicMatchTopology topology,
        IReadOnlyList<string> spawnIds)
    {
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            map,
            spawnIds);
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            format,
            topology,
            deployment,
            CreateLifecycleAssignments(
                topology,
                deployment,
                includeChild: false),
            [],
            new DeathmatchActorModeMapBindingDefinition());
    }

    private static ActorRulesDefinition CreateRules(
        GameModeDefinition? gameMode = null,
        bool includeSplit = false,
        bool includeFabrication = false)
    {
        bool isFrontline = gameMode is FrontlineGameModeDefinition;
        var movement = new ActorMovementProfileDefinition(
            "ground",
            ActorMovementLayer.Ground);
        var vision = new ActorVisionProfileDefinition(
            "standard",
            range: 8,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 1,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 0,
            hearingBearingSectors: 0,
            ActorHearingBearingModel.Disabled,
            hearingDistanceBandUpperBounds: [],
            loudEventKinds: []);
        var projectile = new ActorProjectileDefinition(
            ActorProjectileMode.Discrete,
            damagePerHit: 1,
            maxTravelTiles: 8,
            ticksPerAdvance: 1,
            tilesPerAdvance: 1,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);
        var shotProgram = new ActorShotProgramDefinition(
            enabled: false,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            minInitialAimSteps: 0,
            maxInitialAimSteps: 0,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: 1,
            minBendEveryTiles: 1,
            maxBendEveryTiles: 1,
            minBendCount: 1,
            maxBendCount: 1,
            launchTiles: 1,
            payloadOptional: false,
            new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: null,
            ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);
        var attack = new ActorAttackProfileDefinition(
            "bolt",
            omnidirectionalAim: false,
            projectile,
            cooldownTicks: 2,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            shotProgram);

        var actions = new List<ActorActionDefinition>
        {
            new("wait", 0, ActorActionKind.Wait, []),
            new("shoot", 4, ActorActionKind.Attack, []),
        };
        var mobileActions = new List<string> { "wait", "shoot" };
        var fabricationTransitions =
            new List<ActorFabricationTransitionDefinition>();
        var replicationTransitions =
            new List<ActorReplicationTransitionDefinition>();
        if (includeFabrication)
        {
            actions.Add(new(
                "fabricate",
                100,
                ActorActionKind.Fabrication,
                [ActorActionParameterKind.UnitTarget]));
            mobileActions.Add("fabricate");
            fabricationTransitions.Add(
                new BoundedChildFabricationDefinition(
                    "fabricate-child",
                    "fabricate",
                    ["mobile"],
                    "child",
                    "source-pad",
                    "output-pad",
                    requiredSourceTileTags: [],
                    requiredOutputTileTags: [],
                    forbiddenOutputTileTags: [],
                    candidateOffsets: [new(1, 0)],
                    new ActorFabricationDelayDefinition(1),
                    ActorActionRejectionResult.Blocked));
        }
        if (includeSplit)
        {
            actions.Add(new(
                "split",
                103,
                ActorActionKind.Replication,
                []));
            mobileActions.Add("split");
            replicationTransitions.Add(
                new SplitReplicationTransitionDefinition(
                    "split-mobile",
                    "split",
                    ["mobile"],
                    "child",
                    descendantCount: 2,
                    maxSourceGeneration: 0,
                    requireNoPriorSameLifeTransition: true,
                    new ActorReplicationHealthDefinition(
                        ActorReplicationHealthDefinition.DistributionKind
                            .DivideCurrentHealthEquallyFloor,
                        minimumHealthPerDescendant: 1,
                        ActorReplicationHealthDefinition.RemainderKind.Discard),
                    candidateOffsets: [new(0, -1), new(0, 1)],
                    TransitionWindup()));
        }

        var forms = new List<ActorFormDefinition>
        {
            new(
                "mobile",
                maxHealth: 3,
                movement.Id,
                vision.Id,
                attack.Id,
                objectiveWeight: isFrontline ? 1 : 0,
                mobileActions),
        };
        if (includeSplit || includeFabrication)
        {
            forms.Add(new(
                "child",
                maxHealth: 2,
                movement.Id,
                vision.Id,
                attack.Id,
                objectiveWeight: isFrontline ? 1 : 0,
                ["wait", "shoot"]));
        }

        var lifecycleProfiles = new List<ActorLifecycleProfileDefinition>
        {
            new(
                "automatic-mobile",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                delayTicks: 3,
                automaticReturnFormId: "mobile"),
            new(
                "ready-child",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication,
                delayTicks: 2,
                automaticReturnFormId: null),
        };
        if (includeSplit || includeFabrication)
        {
            lifecycleProfiles.Add(new(
                "automatic-child",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                delayTicks: 3,
                automaticReturnFormId: "child"));
        }

        return new ActorRulesDefinition(
            rulesetId:
                includeSplit
                    ? "deathmatch-split-proof"
                    : includeFabrication
                        ? "deathmatch-fabrication-proof"
                        : gameMode is FrontlineGameModeDefinition
                            ? "frontline-proof"
                            : "deathmatch-proof",
            new ActorRulesLimits(
                maxTicks: 100,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            new ActorSeedMechanicsDefinition(
                "actor-proof",
                ActorSeedMechanicsDefinition.SeedDerivationKind
                    .MatchSeedProfileTeamUnitLifeMix64V1,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime),
            gameMode ?? CreateDeathmatchMode(),
            new ActorLifecycleDefinition(lifecycleProfiles),
            forms,
            [movement],
            [vision],
            [attack],
            actions,
            fabricationTransitions,
            sameLifeTransitions: [],
            replicationTransitions,
            new ActorTeamPerceptionDefinition(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            CreateCollisions(),
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                ActorTickResolutionDefinition.CreateSupportedPhases()));
    }

    private static DeathmatchGameModeDefinition CreateDeathmatchMode() =>
        new(
            new DeathmatchVictoryDefinition(
                killsToWin: 10,
                [
                    new(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [new(ScoreChannelDefinition.ChannelKind.Kills)],
            DeathmatchScoringDefinition.RawHostileKillV1);

    private static FrontlineGameModeDefinition CreateFrontlineMode() =>
        new(
            new FrontlineVictoryDefinition(
                pushesToBreach: 3,
                [
                    new(
                        ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [
                new(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress),
            ],
            frontlinePositionCount: 5,
            new FrontlineCaptureDefinition(
                threshold: 3,
                gainPerSoleTeamTick: 1,
                decayAmount: 1,
                decayIntervalTicks: 2,
                redeployPauseTicks: 1));

    private static ActorTransitionWindupDefinition TransitionWindup() =>
        new(
            durationTicks: 1,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            ActorTransitionWindupDefinition.PlacementReferenceKind.QueueTimePose);

    private static ActorCollisionDefinition CreateCollisions() =>
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
            ActorCollisionDefinition.AlliedProjectileContactKind.PassThrough);

    private static ActorMapDefinition CreateMap(
        bool includeFabricationRegions = false,
        bool fabricationOffsetsReach = true,
        bool includeFrontlineRegions = false)
    {
        var regions = new List<ActorMapRegionDefinition>();
        if (includeFabricationRegions)
        {
            regions.AddRange(
            [
                new(
                    "source-west",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new(2, 3)]),
                new(
                    "output-west",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new(fabricationOffsetsReach ? 3 : 4, 3)]),
                new(
                    "source-east",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new(6, 3)]),
                new(
                    "output-east",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new(fabricationOffsetsReach ? 5 : 4, 3)]),
            ]);
        }
        if (includeFrontlineRegions)
        {
            regions.AddRange(
            [
                Objective("far-west", 2),
                Objective("near-west", 3),
                Objective("centre", 4),
                Objective("near-east", 5),
                Objective("far-east", 6),
            ]);
        }

        return new ActorMapDefinition(
            "shared-arena",
            version: 1,
            [
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            ],
            [
                Spawn("west", 1, 3, Direction.East),
                Spawn("east", 7, 3, Direction.West),
                Spawn("north", 4, 1, Direction.South),
                Spawn("south", 4, 5, Direction.North),
            ],
            regions.ToImmutableArray(),
            []);
    }

    private static ActorMapDefinition CreateCrampedMap() =>
        new(
            "cramped-arena",
            version: 1,
            [
                "#####",
                "#.#.#",
                "#####",
            ],
            [
                Spawn("west", 1, 1, Direction.East),
                Spawn("east", 3, 1, Direction.West),
            ],
            [],
            []);

    private static ActorMapRegionDefinition Objective(string id, int x) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.Objective,
            [new(x, 2)]);

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        int x,
        int y,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(id, new Position(x, y), facing),
            [ActorMovementLayer.Ground]);

    private static PublicMatchTopology CreateTopology(
        IReadOnlyList<IReadOnlyList<int>> participantIdsByTeam)
    {
        var teams = new List<PublicScoringTeam>();
        var participants = new List<PublicParticipant>();
        var slots = new List<PublicUnitSlot>();
        var lives = new List<PublicInitialLife>();
        for (int teamId = 0; teamId < participantIdsByTeam.Count; teamId++)
        {
            teams.Add(new(teamId));
            IReadOnlyList<int> teamParticipants =
                participantIdsByTeam[teamId];
            for (int unitId = 0; unitId < teamParticipants.Count; unitId++)
            {
                int participantId = teamParticipants[unitId];
                participants.Add(new(participantId, teamId));
                slots.Add(new(teamId, unitId, participantId));
                lives.Add(new(teamId, unitId, 0, "mobile"));
            }
        }

        return new PublicMatchTopology
        {
            Teams = teams.ToImmutableArray(),
            Participants = participants.ToImmutableArray(),
            UnitSlots = slots.ToImmutableArray(),
            InitialLives = lives.ToImmutableArray(),
        };
    }

    private static PublicMatchTopology CreateExpandableTopology(
        bool additionalSlotsInitiallyActive)
    {
        ImmutableArray<PublicInitialLife> lives =
            additionalSlotsInitiallyActive
                ?
                [
                    new(0, 0, 0, "mobile"),
                    new(0, 1, 0, "child"),
                    new(1, 0, 0, "mobile"),
                    new(1, 1, 0, "child"),
                ]
                :
                [
                    new(0, 0, 0, "mobile"),
                    new(1, 0, 0, "mobile"),
                ];
        return new PublicMatchTopology
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(20, 1)],
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 10),
                new(1, 0, 20),
                new(1, 1, 20),
            ],
            InitialLives = lives,
        };
    }

    private static InitialDeploymentDefinition CreateDeployment(
        PublicMatchTopology topology,
        ActorMapDefinition map,
        IReadOnlyList<string> spawnIds)
    {
        PublicInitialLife[] lives = topology.InitialLives
            .OrderBy(life => life.TeamId)
            .ThenBy(life => life.UnitId)
            .ToArray();
        Assert.Equal(lives.Length, spawnIds.Count);
        Dictionary<string, InitialSpawnDefinition> mapSpawns =
            map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                anchor => anchor.Spawn,
                StringComparer.Ordinal);

        return new InitialDeploymentDefinition(
            spawnIds.Select(spawnId => mapSpawns[spawnId]).ToImmutableArray(),
            lives.Select((life, index) =>
                    new InitialLifeDeployment(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId,
                        life.FormId,
                        spawnIds[index]))
                .ToImmutableArray());
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition[]
        CreateLifecycleAssignments(
            PublicMatchTopology topology,
            InitialDeploymentDefinition deployment,
            bool includeChild)
    {
        Dictionary<(int TeamId, int UnitId), PublicInitialLife> lives =
            topology.InitialLives.ToDictionary(
                life => (life.TeamId, life.UnitId));
        Dictionary<(int TeamId, int UnitId), string> spawns = deployment.Lives
            .ToDictionary(
                life => (life.TeamId, life.UnitId),
                life => life.SpawnId);
        return topology.UnitSlots
            .Select(slot =>
            {
                var key = (slot.TeamId, slot.UnitId);
                if (!lives.TryGetValue(key, out PublicInitialLife? life))
                {
                    return new ActorUnitSlotLifecycleAssignmentDefinition(
                        slot.TeamId,
                        slot.UnitId,
                        "ready-child",
                        initialGeneration: null,
                        allowedFormIds: ["child"],
                        ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.DormantUnlockAtTick,
                        unlockTick: 0,
                        assignedRespawnSpawnId: null);
                }

                string lifecycleProfileId =
                    life.FormId == "child"
                        ? "automatic-child"
                        : "automatic-mobile";
                IEnumerable<string> allowedForms =
                    includeChild && life.FormId == "mobile"
                        ? ["mobile", "child"]
                        : [life.FormId];
                return ActiveAssignment(
                    slot.TeamId,
                    slot.UnitId,
                    lifecycleProfileId,
                    allowedForms,
                    spawns[key]);
            })
            .ToArray();
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition ActiveAssignment(
        int teamId,
        int unitId,
        string lifecycleProfileId,
        IEnumerable<string> allowedFormIds,
        string assignedRespawnSpawnId) =>
        new(
            teamId,
            unitId,
            lifecycleProfileId,
            initialGeneration: 0,
            allowedFormIds,
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.ActiveAtTickZero,
            unlockTick: null,
            assignedRespawnSpawnId);

    private static ActorUnitSlotLifecycleAssignmentDefinition
        DormantChildAssignment(
            int teamId,
            int unitId) =>
        new(
            teamId,
            unitId,
            "ready-child",
            initialGeneration: null,
            allowedFormIds: ["child"],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.DormantUnlockAtTick,
            unlockTick: 0,
            assignedRespawnSpawnId: null);

    private static ActorParticipantRegionAssignmentDefinition[]
        CreateFabricationRegionAssignments() =>
        [
            // Source-region facing is output metadata, not the actor's dynamic
            // queue-time facing used to rotate candidate offsets.
            new(10, "source-pad", "source-west", Direction.North),
            new(10, "output-pad", "output-west", Direction.East),
            new(20, "source-pad", "source-east", Direction.North),
            new(20, "output-pad", "output-east", Direction.West),
        ];
}
