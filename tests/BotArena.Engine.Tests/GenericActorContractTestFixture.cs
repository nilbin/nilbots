using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

internal static class GenericActorContractTestFixture
{
    public static ActorResolvedMatchDefinition Deathmatch(string formatName)
    {
        MatchFormatDefinition format;
        IReadOnlyList<IReadOnlyList<int>> participants;
        IReadOnlyList<string> spawnIds;
        switch (formatName)
        {
            case "head-to-head":
                format = new HeadToHeadMatchFormatDefinition();
                participants = [[10], [20]];
                spawnIds = ["west", "east"];
                break;
            case "free-for-all":
                format = new FreeForAllMatchFormatDefinition(4);
                participants = [[10], [20], [30], [40]];
                spawnIds = ["west", "east", "north", "south"];
                break;
            case "teams":
                format = new TeamsMatchFormatDefinition(2, 2);
                participants = [[10, 11], [20, 21]];
                spawnIds = ["west", "north", "east", "south"];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(formatName));
        }

        ActorRulesDefinition rules = Rules(frontline: false);
        ActorMapDefinition map = DeathmatchMap();
        PublicMatchTopology topology = Topology(participants);
        InitialDeploymentDefinition deployment =
            Deployment(topology, map, spawnIds);
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            format,
            topology,
            deployment,
            Assignments(topology, deployment),
            [],
            new DeathmatchActorModeMapBindingDefinition());
    }

    public static ActorResolvedMatchDefinition Frontline()
    {
        ActorRulesDefinition rules = Rules(frontline: true);
        ActorMapDefinition map = FrontlineMap();
        PublicMatchTopology topology = Topology([[10], [20]]);
        InitialDeploymentDefinition deployment =
            Deployment(topology, map, ["west", "east"]);
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            Assignments(topology, deployment),
            [],
            new FrontlineActorModeMapBindingDefinition(
                [
                    "far-west",
                    "near-west",
                    "centre",
                    "near-east",
                    "far-east",
                ],
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
    }

    public static ActorResolvedMatchDefinition WithTransitions(
        string replicationTransitionId = "split-mobile",
        IReadOnlyList<ActorRelativePositionOffset>?
            fabricationCandidateOffsets = null,
        int fabricationDelayTicks = 1,
        ActorActionRejectionResult fabricationUnavailableResult =
            ActorActionRejectionResult.Blocked,
        int mobileMaxHealth = 6,
        int childMaxHealth = 2,
        bool includeMovement = false)
    {
        ActorRulesDefinition rules =
            TransitionRules(
                replicationTransitionId,
                fabricationCandidateOffsets,
                fabricationDelayTicks,
                fabricationUnavailableResult,
                mobileMaxHealth,
                childMaxHealth,
                includeMovement);
        ActorMapDefinition map = TransitionMap();
        var topology = new PublicMatchTopology
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
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(1, 0, 0, "mobile"),
            ],
        };
        InitialDeploymentDefinition deployment =
            Deployment(topology, map, ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            ActiveTransitionAssignment(0, "west"),
            DormantTransitionAssignment(0),
            ActiveTransitionAssignment(1, "east"),
            DormantTransitionAssignment(1),
        ];
        ActorParticipantRegionAssignmentDefinition[] regions =
        [
            new(10, "source-pad", "source-west", Direction.North),
            new(10, "output-pad", "output-west", Direction.East),
            new(20, "source-pad", "source-east", Direction.North),
            new(20, "output-pad", "output-east", Direction.West),
        ];
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            assignments,
            regions,
            new DeathmatchActorModeMapBindingDefinition());
    }

    private static ActorRulesDefinition Rules(bool frontline)
    {
        var movement = new ActorMovementProfileDefinition(
            "ground",
            ActorMovementLayer.Ground);
        var vision = new ActorVisionProfileDefinition(
            "mobile-vision",
            range: 6,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 1,
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
            enabled: true,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            minInitialAimSteps: -1,
            maxInitialAimSteps: 1,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: 4,
            minBendEveryTiles: 1,
            maxBendEveryTiles: 3,
            minBendCount: 1,
            maxBendCount: 3,
            launchTiles: 1,
            payloadOptional: true,
            defaultProgram: new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: ActorActionRejectionResult.Rejected,
            unsupportedPayloadResult: ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);
        var attack = new ActorAttackProfileDefinition(
            "mobile-bolt",
            omnidirectionalAim: false,
            projectile,
            cooldownTicks: 3,
            maxEnergy: 10,
            attackEnergyCost: 5,
            energyRegenerationIntervalTicks: 2,
            energyRegenerationAmount: 1,
            shotProgram);
        var lifecycle = new ActorLifecycleDefinition(
            [
                new ActorLifecycleProfileDefinition(
                    "prime-respawn",
                    ActorLifecycleProfileDefinition.DestructionPolicyKind
                        .AutomaticRespawn,
                    delayTicks: 3,
                    automaticReturnFormId: "mobile"),
            ]);
        GameModeDefinition mode = frontline
            ? new FrontlineGameModeDefinition(
                new FrontlineVictoryDefinition(
                    pushesToBreach: 3,
                    [
                        new(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [
                    new(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 5,
                new FrontlineCaptureDefinition(
                    threshold: 3,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 1,
                    decayIntervalTicks: 2,
                    redeployPauseTicks: 1))
            : new DeathmatchGameModeDefinition(
                new DeathmatchVictoryDefinition(
                    killsToWin: 10,
                    [
                        new(
                            ScoreChannelDefinition.ChannelKind.Kills,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [new(ScoreChannelDefinition.ChannelKind.Kills)],
                DeathmatchScoringDefinition.RawHostileKillV1);

        return new ActorRulesDefinition(
            frontline
                ? "frontline-sdk-contract"
                : "deathmatch-sdk-contract",
            new ActorRulesLimits(
                maxTicks: 100,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            new ActorSeedMechanicsDefinition(
                "sdk-contract",
                ActorSeedMechanicsDefinition.SeedDerivationKind
                    .MatchSeedProfileTeamUnitLifeMix64V1,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime),
            mode,
            lifecycle,
            [
                new ActorFormDefinition(
                    "mobile",
                    maxHealth: 3,
                    movement.Id,
                    vision.Id,
                    attack.Id,
                    objectiveWeight: frontline ? 1 : 0,
                    ["wait", "shoot"]),
            ],
            [movement],
            [vision],
            [attack],
            [
                new ActorActionDefinition(
                    "wait",
                    0,
                    ActorActionKind.Wait,
                    []),
                new ActorActionDefinition(
                    "shoot",
                    4,
                    ActorActionKind.Attack,
                    [ActorActionParameterKind.ShotProgram]),
            ],
            [],
            [],
            [],
            new ActorTeamPerceptionDefinition(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            new ActorCollisionDefinition(
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
                    .PassThrough),
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                ActorTickResolutionDefinition.CreateSupportedPhases()));
    }

    private static ActorRulesDefinition TransitionRules(
        string replicationTransitionId,
        IReadOnlyList<ActorRelativePositionOffset>?
            fabricationCandidateOffsets,
        int fabricationDelayTicks,
        ActorActionRejectionResult fabricationUnavailableResult,
        int mobileMaxHealth,
        int childMaxHealth,
        bool includeMovement)
    {
        ActorRulesDefinition baseline = Rules(frontline: false);
        var windup = new ActorTransitionWindupDefinition(
            durationTicks: 1,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);
        return new ActorRulesDefinition(
            "transition-sdk-contract",
            baseline.Limits,
            baseline.SeedMechanics,
            baseline.GameMode,
            new ActorLifecycleDefinition(
                [
                    baseline.Lifecycle.Profiles.Single(),
                    new ActorLifecycleProfileDefinition(
                        "child-ready",
                        ActorLifecycleProfileDefinition
                            .DestructionPolicyKind
                            .ReadyForExplicitFabrication,
                        delayTicks: 2,
                        automaticReturnFormId: null),
                ]),
            [
                new ActorFormDefinition(
                    "mobile",
                    maxHealth: mobileMaxHealth,
                    "ground",
                    "mobile-vision",
                    "mobile-bolt",
                    objectiveWeight: 0,
                    includeMovement
                        ? ["wait", "move", "shoot", "fabricate", "split"]
                        : ["wait", "shoot", "fabricate", "split"]),
                new ActorFormDefinition(
                    "child",
                    maxHealth: childMaxHealth,
                    "ground",
                    "mobile-vision",
                    "mobile-bolt",
                    objectiveWeight: 0,
                    includeMovement
                        ? ["wait", "move", "shoot", "anchor"]
                        : ["wait", "shoot", "anchor"]),
                new ActorFormDefinition(
                    "turret",
                    maxHealth: 5,
                    "ground",
                    "mobile-vision",
                    "mobile-bolt",
                    objectiveWeight: 0,
                    ["wait", "shoot"]),
            ],
            baseline.MovementProfiles,
            baseline.VisionProfiles,
            baseline.AttackProfiles,
            [
                .. baseline.Actions,
                .. includeMovement
                    ?
                    [
                        new ActorActionDefinition(
                            "move",
                            1,
                            ActorActionKind.Movement,
                            [ActorActionParameterKind.Direction]),
                    ]
                    : Array.Empty<ActorActionDefinition>(),
                new ActorActionDefinition(
                    "fabricate",
                    100,
                    ActorActionKind.Fabrication,
                    [ActorActionParameterKind.UnitTarget]),
                new ActorActionDefinition(
                    "anchor",
                    101,
                    ActorActionKind.SameLifeTransition,
                    []),
                new ActorActionDefinition(
                    "split",
                    103,
                    ActorActionKind.Replication,
                    []),
            ],
            [
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
                    candidateOffsets:
                        fabricationCandidateOffsets ?? [new(1, 0)],
                    new ActorFabricationDelayDefinition(
                        fabricationDelayTicks),
                    fabricationUnavailableResult),
            ],
            [
                new ActorFormTransitionDefinition(
                    "anchor-child",
                    "anchor",
                    "child",
                    "turret",
                    windup,
                    ActorSameLifeTransitionDefinition.MemoryContinuityKind
                        .PreservePrivateMemory,
                    new ActorSameLifeHealthDefinition(
                        ActorSameLifeHealthDefinition.HealthPolicyKind
                            .AddFlatCappedToTargetMaximum,
                        flatHealthGain: 2),
                    ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
                    new ActorSameLifePlacementDefinition(
                        ActorSameLifePlacementDefinition
                            .PositionContinuityKind.SameOccupiedGroundTile,
                        ActorSameLifePlacementDefinition
                            .LegalityEvaluationKind
                            .QueueAndCompletionTileTags,
                        requiredTileTags: [],
                        forbiddenTileTags: [],
                        ActorSameLifePlacementDefinition
                            .FailedCompletionKind
                            .CancelAndRemainInSourceForm),
                    irreversibleForLife: true),
            ],
            [
                new SplitReplicationTransitionDefinition(
                    replicationTransitionId,
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
                    windup),
            ],
            baseline.TeamPerception,
            baseline.Collisions,
            baseline.TickResolution);
    }

    private static ActorMapDefinition DeathmatchMap() =>
        new(
            "sdk-shared-arena",
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
            [],
            []);

    private static ActorMapDefinition FrontlineMap() =>
        new(
            "sdk-frontline-arena",
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
            ],
            [
                Objective("far-west", 2),
                Objective("near-west", 3),
                Objective("centre", 4),
                Objective("near-east", 5),
                Objective("far-east", 6),
            ],
            []);

    private static ActorMapDefinition TransitionMap() =>
        new(
            "sdk-transition-arena",
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
            ],
            [
                new ActorMapRegionDefinition(
                    "source-west",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new Position(2, 3)]),
                new ActorMapRegionDefinition(
                    "output-west",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new Position(3, 3)]),
                new ActorMapRegionDefinition(
                    "source-east",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new Position(6, 3)]),
                new ActorMapRegionDefinition(
                    "output-east",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new Position(5, 3)]),
            ],
            []);

    private static ActorMapRegionDefinition Objective(string id, int x) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.Objective,
            [new Position(x, 2)]);

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        int x,
        int y,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(id, new Position(x, y), facing),
            [ActorMovementLayer.Ground]);

    private static PublicMatchTopology Topology(
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

    private static InitialDeploymentDefinition Deployment(
        PublicMatchTopology topology,
        ActorMapDefinition map,
        IReadOnlyList<string> spawnIds)
    {
        Dictionary<string, InitialSpawnDefinition> mapSpawns =
            map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                anchor => anchor.Spawn,
                StringComparer.Ordinal);
        PublicInitialLife[] lives = topology.InitialLives
            .OrderBy(life => life.TeamId)
            .ThenBy(life => life.UnitId)
            .ToArray();
        return new InitialDeploymentDefinition(
            spawnIds
                .Select(spawnId => mapSpawns[spawnId])
                .ToImmutableArray(),
            lives.Select(
                (life, index) =>
                    new InitialLifeDeployment(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId,
                        life.FormId,
                        spawnIds[index]))
                .ToImmutableArray());
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition[] Assignments(
        PublicMatchTopology topology,
        InitialDeploymentDefinition deployment)
    {
        Dictionary<(int TeamId, int UnitId), string> spawnIds =
            deployment.Lives.ToDictionary(
                life => (life.TeamId, life.UnitId),
                life => life.SpawnId);
        return topology.UnitSlots
            .Select(
                slot =>
                    new ActorUnitSlotLifecycleAssignmentDefinition(
                        slot.TeamId,
                        slot.UnitId,
                        "prime-respawn",
                        initialGeneration: 0,
                        allowedFormIds: ["mobile"],
                        ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.ActiveAtTickZero,
                        unlockTick: null,
                        assignedRespawnSpawnId:
                            spawnIds[(slot.TeamId, slot.UnitId)]))
            .ToArray();
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition
        ActiveTransitionAssignment(int teamId, string spawnId) =>
        new(
            teamId,
            unitId: 0,
            "prime-respawn",
            initialGeneration: 0,
            allowedFormIds: ["mobile", "child", "turret"],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.ActiveAtTickZero,
            unlockTick: null,
            assignedRespawnSpawnId: spawnId);

    private static ActorUnitSlotLifecycleAssignmentDefinition
        DormantTransitionAssignment(int teamId) =>
        new(
            teamId,
            unitId: 1,
            "child-ready",
            initialGeneration: null,
            allowedFormIds: ["child", "turret"],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.DormantUnlockAtTick,
            unlockTick: 0,
            assignedRespawnSpawnId: null);
}
