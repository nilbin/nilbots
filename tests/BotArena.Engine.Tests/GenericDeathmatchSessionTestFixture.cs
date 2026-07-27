using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

internal static class GenericDeathmatchSessionTestFixture
{
    public sealed record Options
    {
        public int MaxTicks { get; init; } = 8;
        public int? KillsToWin { get; init; }
        public int MaxHealth { get; init; } = 3;
        public int DamagePerHit { get; init; } = 1;
        public int RespawnDelayTicks { get; init; }
        public int FaultsAllowedBeforeDisqualification { get; init; }
        public int CooldownTicks { get; init; }
        public int MaxEnergy { get; init; }
        public int AttackEnergyCost { get; init; }
        public int EnergyRegenerationIntervalTicks { get; init; }
        public int EnergyRegenerationAmount { get; init; }
        public int VisionRange { get; init; } = 8;
        public bool IncludeSplit { get; init; }
        public int SplitDurationTicks { get; init; } = 1;
    }

    public static ActorResolvedMatchDefinition Definition(
        string formatName,
        Options? options = null)
    {
        options ??= new Options();
        if (options.IncludeSplit && formatName != "head-to-head")
        {
            throw new ArgumentException(
                "The focused Split fixture is head-to-head.",
                nameof(formatName));
        }

        ActorResolvedMatchDefinition baseline =
            GenericActorContractTestFixture.Deathmatch(formatName);
        ActorAttackProfileDefinition baselineAttack =
            baseline.Rules.AttackProfiles.Single();
        var projectile = new ActorProjectileDefinition(
            ActorProjectileMode.Discrete,
            options.DamagePerHit,
            maxTravelTiles: 8,
            ticksPerAdvance: 1,
            tilesPerAdvance: 8,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);
        var attack = new ActorAttackProfileDefinition(
            baselineAttack.Id,
            omnidirectionalAim: false,
            projectile,
            options.CooldownTicks,
            options.MaxEnergy,
            options.AttackEnergyCost,
            options.EnergyRegenerationIntervalTicks,
            options.EnergyRegenerationAmount,
            baselineAttack.ShotProgram);
        var vision = new ActorVisionProfileDefinition(
            "mobile-vision",
            options.VisionRange,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.Omnidirectional,
            options.VisionRange,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 0,
            hearingBearingSectors: 0,
            ActorHearingBearingModel.Disabled,
            hearingDistanceBandUpperBounds: [],
            loudEventKinds: []);

        var actionList = new List<ActorActionDefinition>
        {
            new("wait", 0, ActorActionKind.Wait, []),
            new(
                "move",
                1,
                ActorActionKind.Movement,
                [ActorActionParameterKind.Direction]),
            new(
                "rotate",
                2,
                ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
            new(
                "shoot",
                4,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ShotProgram]),
        };
        if (options.IncludeSplit)
        {
            actionList.Add(
                new ActorActionDefinition(
                    "split",
                    103,
                    ActorActionKind.Replication,
                    []));
        }
        ActorActionDefinition[] actions = [.. actionList];
        string[] mobileActions = options.IncludeSplit
            ? ["wait", "move", "rotate", "shoot", "split"]
            : ["wait", "move", "rotate", "shoot"];
        ActorFormDefinition[] forms = options.IncludeSplit
            ?
            [
                new(
                    "mobile",
                    options.MaxHealth,
                    "ground",
                    vision.Id,
                    attack.Id,
                    objectiveWeight: 0,
                    mobileActions),
                new(
                    "child",
                    maxHealth: 2,
                    "ground",
                    vision.Id,
                    attack.Id,
                    objectiveWeight: 0,
                    ["wait", "move", "rotate", "shoot"]),
            ]
            :
            [
                new ActorFormDefinition(
                    "mobile",
                    options.MaxHealth,
                    "ground",
                    vision.Id,
                    attack.Id,
                    objectiveWeight: 0,
                    mobileActions),
            ];

        var lifecycleProfiles = new List<ActorLifecycleProfileDefinition>
        {
            new(
                "prime-respawn",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                options.RespawnDelayTicks,
                "mobile"),
        };
        if (options.IncludeSplit)
        {
            lifecycleProfiles.Add(
                new ActorLifecycleProfileDefinition(
                    "child-ready",
                    ActorLifecycleProfileDefinition.DestructionPolicyKind
                        .ReadyForExplicitFabrication,
                    delayTicks: 0,
                    automaticReturnFormId: null));
        }

        ImmutableArray<ScoreChannelDefinition> scoreCatalog =
        [
            new(ScoreChannelDefinition.ChannelKind.Kills),
            new(ScoreChannelDefinition.ChannelKind.Deaths),
            new(ScoreChannelDefinition.ChannelKind.DamageDealt),
            new(ScoreChannelDefinition.ChannelKind.ActiveHealth),
        ];
        var mode = new DeathmatchGameModeDefinition(
            new DeathmatchVictoryDefinition(
                options.KillsToWin,
                [
                    new(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                    new(
                        ScoreChannelDefinition.ChannelKind.DamageDealt,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                    new(
                        ScoreChannelDefinition.ChannelKind.ActiveHealth,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                    new(
                        ScoreChannelDefinition.ChannelKind.Deaths,
                        ScoreRankingDefinition.SortDirection.LowerWins),
                ]),
            scoreCatalog,
            DeathmatchScoringDefinition.RawHostileKillV1);

        ActorReplicationTransitionDefinition[] replicationTransitions =
            options.IncludeSplit
                ?
                [
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
                            ActorReplicationHealthDefinition.RemainderKind
                                .Discard),
                        candidateOffsets: [new(0, -1), new(0, 1)],
                        SplitWindup(options.SplitDurationTicks)),
                ]
                : [];
        var rules = new ActorRulesDefinition(
            "generic-deathmatch-session-fixture",
            new ActorRulesLimits(
                options.MaxTicks,
                new ActorRuntimeFaultDefinition(
                    options.FaultsAllowedBeforeDisqualification)),
            baseline.Rules.SeedMechanics,
            mode,
            new ActorLifecycleDefinition(lifecycleProfiles),
            forms,
            baseline.Rules.MovementProfiles,
            [vision],
            [attack],
            actions,
            fabricationTransitions: [],
            sameLifeTransitions: [],
            replicationTransitions,
            new ActorTeamPerceptionDefinition(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            baseline.Rules.Collisions,
            baseline.Rules.TickResolution);

        PublicMatchTopology topology = options.IncludeSplit
            ? SplitTopology()
            : baseline.Topology;
        IReadOnlyCollection<ActorUnitSlotLifecycleAssignmentDefinition>
            assignments = options.IncludeSplit
                ? SplitAssignments()
                : baseline.LifecycleAssignments;
        return new ActorResolvedMatchDefinition(
            rules,
            baseline.Map,
            baseline.Format,
            topology,
            baseline.InitialDeployment,
            assignments,
            baseline.ParticipantRegionAssignments,
            baseline.ModeMapBinding,
            baseline.CapabilityVersions);
    }

    public static ActorResolvedMatchDefinition DefinitionWithSplitMover(
        Options? options = null)
    {
        options ??= new Options
        {
            IncludeSplit = true,
            SplitDurationTicks = 2,
        };
        if (!options.IncludeSplit)
        {
            throw new ArgumentException(
                "The claimed-tile fixture requires Split.",
                nameof(options));
        }
        ActorResolvedMatchDefinition source =
            Definition("head-to-head", options);
        var moverSpawn = new InitialSpawnDefinition(
            "mover",
            new Position(2, 2),
            Direction.West);
        var map = new ActorMapDefinition(
            "split-claimed-tile-arena",
            version: 1,
            source.Map.TileRows,
            [
                .. source.Map.SpawnAnchors,
                new ActorMapSpawnAnchorDefinition(
                    moverSpawn,
                    [ActorMovementLayer.Ground]),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
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
                new(1, 1, 0, "child"),
            ],
        };
        InitialSpawnDefinition west = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "west").Spawn;
        InitialSpawnDefinition east = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "east").Spawn;
        var deployment = new InitialDeploymentDefinition(
            [west, east, moverSpawn],
            [
                new(0, 0, 0, "mobile", "west"),
                new(1, 0, 0, "mobile", "east"),
                new(1, 1, 0, "child", "mover"),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 0),
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 1),
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 1 && value.UnitId == 0),
            new(
                1,
                unitId: 1,
                "child-ready",
                initialGeneration: 0,
                allowedFormIds: ["child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: null),
        ];
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static ActorResolvedMatchDefinition
        DefinitionWithSplitProjectileOccupants(Options? options = null)
    {
        options ??= new Options
        {
            IncludeSplit = true,
        };
        if (!options.IncludeSplit)
        {
            throw new ArgumentException(
                "The projectile-occupancy fixture requires Split.",
                nameof(options));
        }

        ActorResolvedMatchDefinition source =
            Definition("head-to-head", options);
        var northThreat = new InitialSpawnDefinition(
            "north-threat",
            new Position(1, 1),
            Direction.South);
        var southThreat = new InitialSpawnDefinition(
            "south-threat",
            new Position(1, 5),
            Direction.North);
        var map = new ActorMapDefinition(
            "split-projectile-occupancy-arena",
            version: 1,
            source.Map.TileRows,
            [
                .. source.Map.SpawnAnchors,
                new ActorMapSpawnAnchorDefinition(
                    northThreat,
                    [ActorMovementLayer.Ground]),
                new ActorMapSpawnAnchorDefinition(
                    southThreat,
                    [ActorMovementLayer.Ground]),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 10),
                new(1, 0, 20),
                new(1, 1, 20),
                new(1, 2, 20),
            ],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(1, 0, 0, "mobile"),
                new(1, 1, 0, "child"),
                new(1, 2, 0, "child"),
            ],
        };
        InitialSpawnDefinition west = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "west").Spawn;
        InitialSpawnDefinition east = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "east").Spawn;
        var deployment = new InitialDeploymentDefinition(
            [west, east, northThreat, southThreat],
            [
                new(0, 0, 0, "mobile", "west"),
                new(1, 0, 0, "mobile", "east"),
                new(1, 1, 0, "child", "north-threat"),
                new(1, 2, 0, "child", "south-threat"),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition ChildAssignment(
            int unitId) =>
            new(
                1,
                unitId,
                "child-ready",
                initialGeneration: 0,
                allowedFormIds: ["child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: null);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 0),
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 1),
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 1 && value.UnitId == 0),
            ChildAssignment(1),
            ChildAssignment(2),
        ];
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static ActorResolvedMatchDefinition
        DefinitionWithDisqualificationWork(Options? options = null)
    {
        options ??= new Options
        {
            IncludeSplit = true,
            FaultsAllowedBeforeDisqualification = 0,
        };
        if (!options.IncludeSplit)
        {
            throw new ArgumentException(
                "The disqualification-work fixture requires Split.",
                nameof(options));
        }

        ActorResolvedMatchDefinition source =
            Definition("head-to-head", options);
        var faultActorSpawn = new InitialSpawnDefinition(
            "fault-actor",
            new Position(3, 1),
            Direction.South);
        var eastFaultActorSpawn = new InitialSpawnDefinition(
            "east-fault-actor",
            new Position(5, 5),
            Direction.North);
        var map = new ActorMapDefinition(
            "disqualification-work-arena",
            version: 1,
            source.Map.TileRows,
            [
                .. source.Map.SpawnAnchors,
                new ActorMapSpawnAnchorDefinition(
                    faultActorSpawn,
                    [ActorMovementLayer.Ground]),
                new ActorMapSpawnAnchorDefinition(
                    eastFaultActorSpawn,
                    [ActorMovementLayer.Ground]),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 10),
                new(0, 2, 10),
                new(0, 3, 10),
                new(1, 0, 20),
                new(1, 1, 20),
                new(1, 2, 20),
                new(1, 3, 20),
            ],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(0, 3, 0, "mobile"),
                new(1, 0, 0, "mobile"),
                new(1, 3, 0, "mobile"),
            ],
        };
        InitialSpawnDefinition west = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "west").Spawn;
        InitialSpawnDefinition east = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "east").Spawn;
        var deployment = new InitialDeploymentDefinition(
            [west, faultActorSpawn, east, eastFaultActorSpawn],
            [
                new(0, 0, 0, "mobile", "west"),
                new(0, 3, 0, "mobile", "fault-actor"),
                new(1, 0, 0, "mobile", "east"),
                new(1, 3, 0, "mobile", "east-fault-actor"),
            ]);
        var delayedClock =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                0,
                unitId: 2,
                "child-ready",
                initialGeneration: null,
                allowedFormIds: ["child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 4,
                assignedRespawnSpawnId: null);
        var activeFaultActor =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                0,
                unitId: 3,
                "prime-respawn",
                initialGeneration: 0,
                allowedFormIds: ["mobile", "child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: "fault-actor");
        var eastDelayedClock =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                1,
                unitId: 2,
                "child-ready",
                initialGeneration: null,
                allowedFormIds: ["child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 5,
                assignedRespawnSpawnId: null);
        var eastActiveFaultActor =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                1,
                unitId: 3,
                "prime-respawn",
                initialGeneration: 0,
                allowedFormIds: ["mobile", "child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: "east-fault-actor");
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 0),
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 1),
            delayedClock,
            activeFaultActor,
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 1 && value.UnitId == 0),
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 1 && value.UnitId == 1),
            eastDelayedClock,
            eastActiveFaultActor,
        ];
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static ActorResolvedMatchDefinition
        DefinitionWithDisqualifiedAutomaticReturn(Options? options = null)
    {
        options ??= new Options
        {
            MaxTicks = 6,
            MaxHealth = 1,
            DamagePerHit = 1,
            RespawnDelayTicks = 2,
            FaultsAllowedBeforeDisqualification = 0,
        };
        ActorResolvedMatchDefinition source =
            Definition("head-to-head", options);
        var companionSpawn = new InitialSpawnDefinition(
            "companion",
            new Position(3, 1),
            Direction.South);
        var map = new ActorMapDefinition(
            "disqualified-automatic-return-arena",
            version: 1,
            source.Map.TileRows,
            [
                .. source.Map.SpawnAnchors,
                new ActorMapSpawnAnchorDefinition(
                    companionSpawn,
                    [ActorMovementLayer.Ground]),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 10),
                new(1, 0, 20),
            ],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(0, 1, 0, "mobile"),
                new(1, 0, 0, "mobile"),
            ],
        };
        InitialSpawnDefinition west = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "west").Spawn;
        InitialSpawnDefinition east = map.SpawnAnchors
            .Single(anchor => anchor.Spawn.SpawnId == "east").Spawn;
        var deployment = new InitialDeploymentDefinition(
            [west, companionSpawn, east],
            [
                new(0, 0, 0, "mobile", "west"),
                new(0, 1, 0, "mobile", "companion"),
                new(1, 0, 0, "mobile", "east"),
            ]);
        var companionAssignment =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                0,
                unitId: 1,
                "prime-respawn",
                initialGeneration: 0,
                allowedFormIds: ["mobile"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: "companion");
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 0 && value.UnitId == 0),
            companionAssignment,
            source.LifecycleAssignments.Single(value =>
                value.TeamId == 1 && value.UnitId == 0),
        ];
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static ActorResolvedMatchDefinition
        DefinitionWithVisibilityBoundary(Options? options = null)
    {
        options ??= new Options
        {
            MaxTicks = 3,
            VisionRange = 1,
        };
        ActorResolvedMatchDefinition source =
            Definition("head-to-head", options);
        var observerSpawn = new InitialSpawnDefinition(
            "observer",
            new Position(3, 3),
            Direction.East);
        var moverSpawn = new InitialSpawnDefinition(
            "visibility-mover",
            new Position(5, 3),
            Direction.West);
        var map = new ActorMapDefinition(
            "movement-visibility-boundary-arena",
            version: 1,
            source.Map.TileRows,
            [
                .. source.Map.SpawnAnchors,
                new ActorMapSpawnAnchorDefinition(
                    observerSpawn,
                    [ActorMovementLayer.Ground]),
                new ActorMapSpawnAnchorDefinition(
                    moverSpawn,
                    [ActorMovementLayer.Ground]),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [observerSpawn, moverSpawn],
            [
                new(0, 0, 0, "mobile", "observer"),
                new(1, 0, 0, "mobile", "visibility-mover"),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition Assignment(
            int teamId,
            string spawnId) =>
            new(
                teamId,
                unitId: 0,
                "prime-respawn",
                initialGeneration: 0,
                allowedFormIds: ["mobile"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: spawnId);
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            source.Topology,
            deployment,
            [Assignment(0, "observer"), Assignment(1, "visibility-mover")],
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static ActorResolvedMatchDefinition DefinitionWithNoAttackForm(
        Options? options = null)
    {
        ActorResolvedMatchDefinition source =
            Definition("head-to-head", options);
        ActorFormDefinition mobile = source.Rules.Forms.Single();
        var noAttackMobile = new ActorFormDefinition(
            mobile.Id,
            mobile.MaxHealth,
            mobile.MovementProfileId,
            mobile.VisionProfileId,
            attackProfileId: null,
            mobile.ObjectiveWeight,
            ["wait", "move", "rotate"]);
        var rules = new ActorRulesDefinition(
            "generic-deathmatch-no-attack-fixture",
            source.Rules.Limits,
            source.Rules.SeedMechanics,
            source.Rules.GameMode,
            source.Rules.Lifecycle,
            [
                noAttackMobile,
                new ActorFormDefinition(
                    "armed",
                    mobile.MaxHealth,
                    mobile.MovementProfileId,
                    mobile.VisionProfileId,
                    mobile.AttackProfileId,
                    mobile.ObjectiveWeight,
                    mobile.AllowedActionIds),
            ],
            source.Rules.MovementProfiles,
            source.Rules.VisionProfiles,
            source.Rules.AttackProfiles,
            source.Rules.Actions,
            source.Rules.FabricationTransitions,
            source.Rules.SameLifeTransitions,
            source.Rules.ReplicationTransitions,
            source.Rules.TeamPerception,
            source.Rules.Collisions,
            source.Rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static Dictionary<int, RecordingFactory> Factories(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision>? decide = null) =>
        definition.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId,
            participant => new RecordingFactory(
                decide ?? ((_, _) => Wait())));

    public static ImmutableArray<GenericActorParticipantConfiguration>
        Configurations(
            ActorResolvedMatchDefinition definition,
            IReadOnlyDictionary<int, RecordingFactory> factories,
            bool reverse = false)
    {
        IEnumerable<PublicParticipant> participants =
            definition.Topology.Participants;
        if (reverse)
        {
            participants = participants
                .OrderByDescending(participant => participant.ParticipantId);
        }
        return participants.Select(participant =>
            new GenericActorParticipantConfiguration
            {
                ParticipantId = participant.ParticipantId,
                TeamId = participant.TeamId,
                Name = $"participant-{participant.ParticipantId}",
                RuntimeFactory = factories[participant.ParticipantId],
                ArtifactHash =
                    $"fixture-participant-{participant.ParticipantId}",
            }).ToImmutableArray();
    }

    public static GenericActorRuntimeDecision Wait() =>
        new("wait", 0, [], null);

    public static GenericActorRuntimeDecision Move(Direction direction) =>
        new(
            "move",
            1,
            [
                new GenericActorRuntimeActionArgument.DirectionArgument(
                    direction),
            ],
            null);

    public static GenericActorRuntimeDecision Rotate(Direction direction) =>
        new(
            "rotate",
            2,
            [
                new GenericActorRuntimeActionArgument.DirectionArgument(
                    direction),
            ],
            null);

    public static GenericActorRuntimeDecision Shoot() =>
        Shoot(ShotProgram.Straight);

    public static GenericActorRuntimeDecision Shoot(ShotProgram program) =>
        new(
            "shoot",
            4,
            [
                new GenericActorRuntimeActionArgument.ShotProgramArgument(
                    program),
            ],
            null);

    public static GenericActorRuntimeDecision Split() =>
        new("split", 103, [], null);

    public static GenericActorRuntimeDecision Unknown() =>
        new("unknown-action", 999, [], null);

    private static ActorTransitionWindupDefinition SplitWindup(
        int durationTicks) =>
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

    private static PublicMatchTopology SplitTopology() =>
        new()
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

    private static ActorUnitSlotLifecycleAssignmentDefinition[]
        SplitAssignments() =>
        [
            new(
                0,
                unitId: 0,
                "prime-respawn",
                initialGeneration: 0,
                allowedFormIds: ["mobile", "child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: "west"),
            new(
                0,
                unitId: 1,
                "child-ready",
                initialGeneration: null,
                allowedFormIds: ["child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 0,
                assignedRespawnSpawnId: null),
            new(
                1,
                unitId: 0,
                "prime-respawn",
                initialGeneration: 0,
                allowedFormIds: ["mobile", "child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: "east"),
            new(
                1,
                unitId: 1,
                "child-ready",
                initialGeneration: null,
                allowedFormIds: ["child"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 0,
                assignedRespawnSpawnId: null),
        ];

    public sealed class RecordingFactory : IGenericActorRuntimeFactory
    {
        private readonly Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision> _decide;
        private readonly bool _throwOnDispose;

        public RecordingFactory(
            Func<
                GenericActorRuntimeStart,
                GenericActorRuntimeObservation,
                GenericActorRuntimeDecision> decide,
            bool throwOnDispose = false)
        {
            _decide = decide;
            _throwOnDispose = throwOnDispose;
        }

        public List<GenericActorRuntimeStart> Starts { get; } = [];
        public int CreateCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public int DisposedRuntimeCount { get; private set; }

        public IGenericActorRuntime CreateRuntime()
        {
            CreateCount++;
            return new Runtime(this);
        }

        private sealed class Runtime : IGenericActorRuntime
        {
            private readonly RecordingFactory _owner;
            private GenericActorRuntimeStart? _start;
            private bool _disposed;

            public Runtime(RecordingFactory owner)
            {
                _owner = owner;
            }

            public void StartLife(GenericActorRuntimeStart start)
            {
                _start = start;
                _owner.Starts.Add(start);
            }

            public GenericActorRuntimeDecision ExecuteTick(
                GenericActorRuntimeObservation observation)
            {
                _owner.ExecuteCount++;
                return _owner._decide(
                    _start
                    ?? throw new InvalidOperationException(
                        "Runtime was not started."),
                    observation);
            }

            public void Dispose()
            {
                if (_disposed)
                    throw new InvalidOperationException("Double-disposed runtime.");
                _disposed = true;
                _owner.DisposedRuntimeCount++;
                if (_owner._throwOnDispose)
                {
                    throw new InvalidOperationException(
                        "Configured runtime disposal failure.");
                }
            }
        }
    }
}
