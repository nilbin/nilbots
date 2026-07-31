using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// The approved Arc Relay H0 rules, Threefold map, and selectable eight-slot
/// sheets. Alternative numbers require a new identity and are not exposed as
/// mutable options here.
/// </summary>
public static class ArcRelayH0Definition
{
    public const string RulesetId = "arc-relay-h0-01";
    public const string MapId = "arc-relay-threefold-01";
    public const string TopologyProfileId = "arc-relay-eight-slot-sheets-01";
    public const string SeedProfileId = "arc-relay-h0-seed-01";
    public const string LifecycleProfilePrefix = "arc-return-";
    public const string FormPrefix = "arc-body-";
    public const string ReactorRoleId = "own-reactor";
    public const string HomePadRoleId = "own-home-pad";
    public const string WaitActionId = "wait";
    public const string MoveActionId = "move-eight-way";
    public const string RotateActionId = "rotate";
    public const string ShootActionId = "shoot-direction";

    private static readonly ImmutableArray<LaunchClass> Classes =
        CreateClasses();

    public static ActorResolvedMatchDefinition Create(
        IReadOnlyList<string>? teamZeroClasses = null,
        IReadOnlyList<string>? teamOneClasses = null,
        ActorMatchCapabilityVersions? capabilityVersions = null)
    {
        teamZeroClasses ??=
        [
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Hush,
        ];
        teamOneClasses ??=
        [
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Switchback,
            ArcRelayLaunchClassIds.Longshot,
            ArcRelayLaunchClassIds.Mason,
            ArcRelayLaunchClassIds.Sunder,
            ArcRelayLaunchClassIds.Repulsor,
            ArcRelayLaunchClassIds.Veil,
            ArcRelayLaunchClassIds.Nest,
        ];
        ValidateSheet(teamZeroClasses, nameof(teamZeroClasses));
        ValidateSheet(teamOneClasses, nameof(teamOneClasses));

        ActorMapDefinition map = CreateMap();
        PublicMatchTopology topology = CreateTopology(
            teamZeroClasses,
            teamOneClasses);
        return new ActorResolvedMatchDefinition(
            CreateRules(),
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            CreateInitialDeployment(map, topology),
            CreateLifecycleAssignments(topology),
            CreateRegionAssignments(),
            new ArcRelayActorModeMapBindingDefinition(
                ["well-centre", "well-north", "well-south"],
                ReactorRoleId,
                HomePadRoleId),
            capabilityVersions ?? ActorMatchCapabilityVersions.Mind);
    }

    public static ActorMapDefinition CreateMap()
    {
        ImmutableArray<string> rows =
        [
            "###############################",
            "#....#...................#....#",
            "#.............................#",
            "#....#....###.....###....#....#",
            "#....#....###.....###....#....#",
            "#....#....###.....###....#....#",
            "#.............................#",
            "#....#...................#....#",
            "#....#...................#....#",
            "#.............................#",
            "#....#....###.....###....#....#",
            "#....#....###.....###....#....#",
            "#....#....###.....###....#....#",
            "#.............................#",
            "#....#...................#....#",
            "#....#...................#....#",
            "#.............................#",
            "#....#....###.....###....#....#",
            "#....#....###.....###....#....#",
            "#....#....###.....###....#....#",
            "#.............................#",
            "#....#...................#....#",
            "###############################",
        ];
        var anchors = ImmutableArray.CreateBuilder<ActorMapSpawnAnchorDefinition>();
        Position[] west =
        [
            new(1, 8), new(2, 8), new(3, 9), new(1, 10),
            new(1, 12), new(3, 13), new(1, 14), new(2, 14),
        ];
        for (int unitId = 0; unitId < west.Length; unitId++)
        {
            anchors.Add(Anchor(0, unitId, west[unitId], Direction.East));
            anchors.Add(Anchor(
                1,
                unitId,
                new Position(30 - west[unitId].X, west[unitId].Y),
                Direction.West));
        }

        ImmutableArray<Position> westHome = Rectangle(1, 3, 8, 14);
        ImmutableArray<Position> eastHome = westHome
            .Select(value => new Position(30 - value.X, value.Y))
            .OrderBy(value => value.Y).ThenBy(value => value.X)
            .ToImmutableArray();
        Position[] forbidden =
        [
            new(15, 4), new(15, 11), new(15, 18),
            new(2, 11), new(28, 11),
        ];
        return new ActorMapDefinition(
            MapId,
            version: 1,
            rows,
            anchors.ToImmutable(),
            [
                Region("well-north", new Position(15, 4)),
                Region("well-centre", new Position(15, 11)),
                Region("well-south", new Position(15, 18)),
                Region("reactor-west", new Position(2, 11)),
                Region("reactor-east", new Position(28, 11)),
                new ActorMapRegionDefinition(
                    "home-west",
                    ActorMapRegionDefinition.RegionKind.Objective,
                    westHome),
                new ActorMapRegionDefinition(
                    "home-east",
                    ActorMapRegionDefinition.RegionKind.Objective,
                    eastHome),
            ],
            [
                new ActorMapTileTagDefinition(
                    "home-pads",
                    ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
                    westHome.Concat(eastHome).ToImmutableArray()),
                new ActorMapTileTagDefinition(
                    "arc-required-tiles",
                    ActorMapTileTagDefinition.TileTagKind
                        .SignaturePlacementForbidden,
                    forbidden.ToImmutableArray()),
            ]);
    }

    public static ActorRulesDefinition CreateRules()
    {
        ArcRelayGameModeDefinition mode = CreateMode();
        ActorMovementProfileDefinition[] movement =
        [
            new("swift", ActorMovementLayer.Ground,
                ActorMovementFacingCoupling.FaceMovementDirection),
            new("standard", ActorMovementLayer.Ground,
                ActorMovementFacingCoupling.PreserveFacing),
            new("deliberate", ActorMovementLayer.Ground,
                ActorMovementFacingCoupling.FacingLocked),
        ];
        ActorVisionProfileDefinition vision = new(
            "arc-facing-vision",
            range: 7,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 1,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 0,
            hearingBearingSectors: 0,
            ActorHearingBearingModel.Disabled,
            hearingDistanceBandUpperBounds: [],
            loudEventKinds: []);
        ActorAttackProfileDefinition[] attacks =
        [
            Attack("short-fast", range: 4, nextFireInterval: 2),
            Attack("medium-steady", range: 6, nextFireInterval: 3),
            Attack("long-slow", range: 9, nextFireInterval: 5),
        ];
        List<ActorActionDefinition> actions =
        [
            new(WaitActionId, 0, ActorActionKind.Wait, []),
            new(MoveActionId, 1, ActorActionKind.Movement,
                [ActorActionParameterKind.ProjectileHeading]),
            new(RotateActionId, 2, ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
            new(ShootActionId, 4, ActorActionKind.Attack,
                [ActorActionParameterKind.ProjectileHeading]),
            new(ArcRelayActionIds.DropCore, ArcRelayActionIds.DropCoreCode,
                ActorActionKind.Objective, []),
            new(ArcRelayActionIds.HandoffCore,
                ArcRelayActionIds.HandoffCoreCode,
                ActorActionKind.Objective,
                [ActorActionParameterKind.UnitTarget]),
        ];
        actions.AddRange(mode.Signatures.Select((signature, index) =>
            new ActorActionDefinition(
                signature.ActionId,
                ArcRelayActionIds.FirstSignatureCode + index,
                ActorActionKind.Signature,
                SignatureParameters(signature))));

        return new ActorRulesDefinition(
            RulesetId,
            new ActorRulesLimits(
                maxTicks: 600,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            new ActorSeedMechanicsDefinition(
                SeedProfileId,
                ActorSeedMechanicsDefinition.SeedDerivationKind
                    .MatchSeedProfileTeamUnitLifeMix64V1,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime),
            mode,
            new ActorLifecycleDefinition(Classes.Select(entry =>
                new ActorLifecycleProfileDefinition(
                    LifecycleProfileId(entry.ClassId),
                    ActorLifecycleProfileDefinition.DestructionPolicyKind
                        .AutomaticRespawn,
                    delayTicks: 20,
                    automaticReturnFormId: FormId(entry.ClassId)))),
            Classes.Select(entry => new ActorFormDefinition(
                FormId(entry.ClassId),
                entry.Hull,
                entry.Handling,
                vision.Id,
                entry.Gun,
                objectiveWeight: 0,
                [
                    WaitActionId,
                    MoveActionId,
                    RotateActionId,
                    ShootActionId,
                    ArcRelayActionIds.DropCore,
                    ArcRelayActionIds.HandoffCore,
                    entry.Signature.ActionId,
                ])),
            movement,
            [vision],
            attacks,
            actions,
            fabricationTransitions: [],
            sameLifeTransitions: [],
            replicationTransitions: [],
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
                ActorTickResolutionDefinition.CreateSupportedPhases(),
                ActorTickResolutionDefinition.CooldownClockKind
                    .AdvancesWithTime));
    }

    private static ArcRelayGameModeDefinition CreateMode() =>
        new(
            ArcRelayGameModeDefinition.Id,
            new ArcRelayVictoryDefinition(
                pulsesToDestroyReactor: 3,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind.Pulses,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind.ReactorCharge,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.Pulses),
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.ReactorCharge),
            ],
            [
                new ArcRelayWellScheduleDefinition(
                    "centre", 25, 75, 475),
                new ArcRelayWellScheduleDefinition(
                    "north", 50, 75, 500),
                new ArcRelayWellScheduleDefinition(
                    "south", 75, 75, 525),
            ],
            pendingRearmTicks: 10,
            coreRelocationIntervalTicks: 2,
            coresPerPulse: 3,
            fieldedSlotsPerTeam: 8,
            maxCopiesPerClass: 2,
            respawnDelayTicks: 20,
            Classes.Select(entry => entry.Signature));

    private static ImmutableArray<LaunchClass> CreateClasses() =>
    [
        new(ArcRelayLaunchClassIds.Kestrel, 3, "swift", "short-fast",
            new ArcRelaySignatureDefinition.VectorDash(
                "vector-dash", ArcRelayLaunchClassIds.Kestrel,
                "vector-dash", 1, 4, 12)),
        new(ArcRelayLaunchClassIds.Palisade, 5, "deliberate", "short-fast",
            new ArcRelaySignatureDefinition.PrismWall(
                "prism-wall", ArcRelayLaunchClassIds.Palisade,
                "prism-wall", 3, 8, 3, 16)),
        new(ArcRelayLaunchClassIds.Towline, 4, "standard", "medium-steady",
            new ArcRelaySignatureDefinition.TractorHook(
                "tractor-hook", ArcRelayLaunchClassIds.Towline,
                "tractor-hook", 6, 3, 12)),
        new(ArcRelayLaunchClassIds.Patchbay, 4, "standard", "short-fast",
            new ArcRelaySignatureDefinition.RepairBeam(
                "repair-beam", ArcRelayLaunchClassIds.Patchbay,
                "repair-beam", 4, 2, 1, 2, 10)),
        new(ArcRelayLaunchClassIds.Lantern, 3, "swift", "short-fast",
            new ArcRelaySignatureDefinition.SurveyFlare(
                "survey-flare", ArcRelayLaunchClassIds.Lantern,
                "survey-flare", 8, 2, 4, 8, 16)),
        new(ArcRelayLaunchClassIds.Mortar, 3, "deliberate", "medium-steady",
            new ArcRelaySignatureDefinition.FallingStar(
                "falling-star", ArcRelayLaunchClassIds.Mortar,
                "falling-star", 8, 2, 1, 12)),
        new(ArcRelayLaunchClassIds.Minesmith, 4, "standard", "short-fast",
            new ArcRelaySignatureDefinition.TripNode(
                "trip-node", ArcRelayLaunchClassIds.Minesmith,
                "trip-node", 1, 2, 2, 12)),
        new(ArcRelayLaunchClassIds.Hush, 4, "standard", "medium-steady",
            new ArcRelaySignatureDefinition.NullField(
                "null-field", ArcRelayLaunchClassIds.Hush,
                "null-field", 3, 5, 18)),
        new(ArcRelayLaunchClassIds.Relay, 4, "swift", "short-fast",
            new ArcRelaySignatureDefinition.ArcToss(
                "arc-toss", ArcRelayLaunchClassIds.Relay,
                "arc-toss", 5, 1, 2, 12)),
        new(ArcRelayLaunchClassIds.Switchback, 3, "standard", "medium-steady",
            new ArcRelaySignatureDefinition.Exchange(
                "exchange", ArcRelayLaunchClassIds.Switchback,
                "exchange", 6, 1, 16)),
        new(ArcRelayLaunchClassIds.Longshot, 3, "deliberate", "long-slow",
            new ArcRelaySignatureDefinition.RailLine(
                "rail-line", ArcRelayLaunchClassIds.Longshot,
                "rail-line", 2, 12, 2, 8, 18)),
        new(ArcRelayLaunchClassIds.Mason, 5, "deliberate", "short-fast",
            new ArcRelaySignatureDefinition.HardlightBlock(
                "hardlight-block", ArcRelayLaunchClassIds.Mason,
                "hardlight-block", 3, 12, 14)),
        new(ArcRelayLaunchClassIds.Sunder, 4, "standard", "medium-steady",
            new ArcRelaySignatureDefinition.TargetPaint(
                "target-paint", ArcRelayLaunchClassIds.Sunder,
                "target-paint", 7, 8, 3, 1, 16)),
        new(ArcRelayLaunchClassIds.Repulsor, 5, "standard", "short-fast",
            new ArcRelaySignatureDefinition.KineticBurst(
                "kinetic-burst", ArcRelayLaunchClassIds.Repulsor,
                "kinetic-burst", 1, 1, 12)),
        new(ArcRelayLaunchClassIds.Veil, 3, "swift", "short-fast",
            new ArcRelaySignatureDefinition.SmokeCanister(
                "smoke-canister", ArcRelayLaunchClassIds.Veil,
                "smoke-canister", 6, 2, 10, 18)),
        new(ArcRelayLaunchClassIds.Nest, 4, "deliberate", "medium-steady",
            new ArcRelaySignatureDefinition.SentinelSeed(
                "sentinel-seed", ArcRelayLaunchClassIds.Nest,
                "sentinel-seed", 2, 4, 1, 3, 30, 18)),
    ];

    private static ImmutableArray<ActorActionParameterKind>
        SignatureParameters(ArcRelaySignatureDefinition signature) =>
        signature switch
        {
            ArcRelaySignatureDefinition.VectorDash or
            ArcRelaySignatureDefinition.TractorHook or
            ArcRelaySignatureDefinition.RailLine =>
                [ActorActionParameterKind.ProjectileHeading],
            ArcRelaySignatureDefinition.RepairBeam or
            ArcRelaySignatureDefinition.Exchange or
            ArcRelaySignatureDefinition.TargetPaint =>
                [ActorActionParameterKind.UnitTarget],
            ArcRelaySignatureDefinition.SurveyFlare or
            ArcRelaySignatureDefinition.FallingStar or
            ArcRelaySignatureDefinition.TripNode or
            ArcRelaySignatureDefinition.ArcToss or
            ArcRelaySignatureDefinition.HardlightBlock or
            ArcRelaySignatureDefinition.SmokeCanister or
            ArcRelaySignatureDefinition.SentinelSeed =>
                [ActorActionParameterKind.PositionTarget],
            ArcRelaySignatureDefinition.PrismWall =>
                [ActorActionParameterKind.Direction],
            ArcRelaySignatureDefinition.NullField or
            ArcRelaySignatureDefinition.KineticBurst => [],
            _ => throw new ArgumentOutOfRangeException(nameof(signature)),
        };

    private static ActorAttackProfileDefinition Attack(
        string id,
        int range,
        int nextFireInterval) =>
        new(
            id,
            omnidirectionalAim: true,
            new ActorProjectileDefinition(
                ActorProjectileMode.Discrete,
                damagePerHit: 1,
                maxTravelTiles: range,
                ticksPerAdvance: 1,
                tilesPerAdvance: 2,
                launchTiles: 1,
                advancesOnLaunchTick: false,
                damageAppliedSimultaneously: true,
                diagonalCornersMustBeClear: true),
            cooldownTicks: nextFireInterval - 1,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            DisabledShotProgram());

    private static ActorShotProgramDefinition DisabledShotProgram() =>
        new(
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

    private static PublicMatchTopology CreateTopology(
        IReadOnlyList<string> teamZeroClasses,
        IReadOnlyList<string> teamOneClasses)
    {
        var slots = ImmutableArray.CreateBuilder<PublicUnitSlot>();
        var lives = ImmutableArray.CreateBuilder<PublicInitialLife>();
        foreach ((int teamId, IReadOnlyList<string> classes) in new[]
                 {
                     (0, teamZeroClasses),
                     (1, teamOneClasses),
                 })
        {
            for (int unitId = 0; unitId < classes.Count; unitId++)
            {
                slots.Add(new PublicUnitSlot(
                    teamId, unitId, teamId, classes[unitId]));
                lives.Add(new PublicInitialLife(
                    teamId, unitId, 0, FormId(classes[unitId])));
            }
        }
        return new PublicMatchTopology
        {
            Teams = [new PublicScoringTeam(0), new PublicScoringTeam(1)],
            Participants =
            [
                new PublicParticipant(0, 0),
                new PublicParticipant(1, 1),
            ],
            UnitSlots = slots.ToImmutable(),
            InitialLives = lives.ToImmutable(),
        };
    }

    private static InitialDeploymentDefinition CreateInitialDeployment(
        ActorMapDefinition map,
        PublicMatchTopology topology)
    {
        Dictionary<string, InitialSpawnDefinition> spawns = map.SpawnAnchors
            .ToDictionary(value => value.Spawn.SpawnId,
                value => value.Spawn, StringComparer.Ordinal);
        ImmutableArray<InitialLifeDeployment> lives = topology.InitialLives
            .Select(life => new InitialLifeDeployment(
                life.TeamId,
                life.UnitId,
                life.LifeId,
                life.FormId,
                SpawnId(life.TeamId, life.UnitId)))
            .ToImmutableArray();
        return new InitialDeploymentDefinition(
            lives.Select(value => spawns[value.SpawnId]).ToImmutableArray(),
            lives);
    }

    private static IEnumerable<ActorUnitSlotLifecycleAssignmentDefinition>
        CreateLifecycleAssignments(PublicMatchTopology topology) =>
        topology.UnitSlots.Select(slot =>
            new ActorUnitSlotLifecycleAssignmentDefinition(
                slot.TeamId,
                slot.UnitId,
                LifecycleProfileId(slot.ClassId!),
                initialGeneration: 0,
                [FormId(slot.ClassId!)],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: SpawnId(slot.TeamId, slot.UnitId)));

    private static ActorParticipantRegionAssignmentDefinition[]
        CreateRegionAssignments() =>
    [
        new(0, ReactorRoleId, "reactor-west", Direction.East),
        new(0, HomePadRoleId, "home-west", Direction.East),
        new(1, ReactorRoleId, "reactor-east", Direction.West),
        new(1, HomePadRoleId, "home-east", Direction.West),
    ];

    private static ActorMapSpawnAnchorDefinition Anchor(
        int teamId,
        int unitId,
        Position position,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(SpawnId(teamId, unitId), position, facing),
            [ActorMovementLayer.Ground]);

    private static ActorMapRegionDefinition Region(
        string id,
        Position position) =>
        new(id, ActorMapRegionDefinition.RegionKind.Objective, [position]);

    private static ImmutableArray<Position> Rectangle(
        int minX,
        int maxX,
        int minY,
        int maxY) =>
        Enumerable.Range(minY, maxY - minY + 1)
            .SelectMany(y => Enumerable.Range(minX, maxX - minX + 1)
                .Select(x => new Position(x, y)))
            .ToImmutableArray();

    private static string SpawnId(int teamId, int unitId) =>
        $"team-{teamId}-unit-{unitId}";
    private static string FormId(string classId) => FormPrefix + classId;
    private static string LifecycleProfileId(string classId) =>
        LifecycleProfilePrefix + classId;

    private static void ValidateSheet(
        IReadOnlyList<string> classes,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(classes);
        HashSet<string> launch = ArcRelayLaunchClassIds.All
            .ToHashSet(StringComparer.Ordinal);
        if (classes.Count != 8
            || classes.Any(value => !launch.Contains(value))
            || classes.GroupBy(value => value, StringComparer.Ordinal)
                .Any(group => group.Count() > 2))
        {
            throw new ArgumentException(
                "An Arc Relay sheet must name eight launch classes with at most two copies of each.",
                parameterName);
        }
    }

    private sealed record LaunchClass(
        string ClassId,
        int Hull,
        string Handling,
        string Gun,
        ArcRelaySignatureDefinition Signature);
}
