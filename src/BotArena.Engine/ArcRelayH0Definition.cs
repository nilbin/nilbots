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
    public const string StrafeActionId = "strafe-eight-way";
    public const string RotateActionId = "rotate";
    public const string ShootActionId = "shoot-direction";

    private static readonly ImmutableArray<LaunchClass> Classes =
        CreateClasses();

    public static ActorResolvedMatchDefinition Create(
        IReadOnlyList<string>? teamZeroClasses = null,
        IReadOnlyList<string>? teamOneClasses = null,
        ActorMatchCapabilityVersions? capabilityVersions = null,
        ArcRelayLoopProfile? loopProfile = null)
    {
        loopProfile ??= ArcRelayLoopProfile.H0;
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

        ActorMapDefinition map = CreateMap(loopProfile);
        PublicMatchTopology topology = CreateTopology(
            teamZeroClasses,
            teamOneClasses);
        return new ActorResolvedMatchDefinition(
            CreateRules(loopProfile),
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

    public static ActorMapDefinition CreateMap() =>
        CreateMap(ArcRelayLoopProfile.H0);

    public static ActorMapDefinition CreateMap(ArcRelayLoopProfile loopProfile)
    {
        ArgumentNullException.ThrowIfNull(loopProfile);
        ImmutableArray<string> rows = loopProfile.Geometry
            == ArcRelayMapGeometry.DepthLarger
            ? CreateLargerRows()
            :
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
        rows = ApplyGeometry(rows, loopProfile.Geometry);
        bool larger = loopProfile.Geometry == ArcRelayMapGeometry.DepthLarger;
        // The deep warren wraps the interior in two shadow-corridor rows per
        // side, so every interior landmark sits two rows lower.
        int drop = loopProfile.Geometry == ArcRelayMapGeometry.AmbushWarrenDeep
            ? 2
            : 0;
        int maximumX = rows[0].Length - 1;
        int centreX = 15;
        int centreY = (larger ? 14 : 11) + drop;
        int northY = 4 + drop;
        int southY = (larger ? 24 : 18) + drop;
        var anchors = ImmutableArray.CreateBuilder<ActorMapSpawnAnchorDefinition>();
        Position[] west = larger
            ?
            [
                new(1, 11), new(2, 11), new(3, 12), new(1, 13),
                new(1, 15), new(3, 16), new(1, 17), new(2, 17),
            ]
            :
            [
                new(1, 8 + drop), new(2, 8 + drop), new(3, 9 + drop),
                new(1, 10 + drop),
                new(1, 12 + drop), new(3, 13 + drop), new(1, 14 + drop),
                new(2, 14 + drop),
            ];
        // Eastern anchors historically X-flip the western tiles, which is
        // fair only on left-right symmetric maps. The warren maps are
        // 180-degree chiral, so profiles opting into rotational assignment
        // spawn team 1 unit N at the full rotation of team 0 unit N's tile
        // instead; the western tile column is Y-symmetric, so the eastern
        // tile set is identical either way and only unit indexing changes.
        int maximumY = rows.Length - 1;
        for (int unitId = 0; unitId < west.Length; unitId++)
        {
            anchors.Add(Anchor(0, unitId, west[unitId], Direction.East));
            anchors.Add(Anchor(
                1,
                unitId,
                loopProfile.RotationalSpawnAssignment
                    ? new Position(
                        maximumX - west[unitId].X,
                        maximumY - west[unitId].Y)
                    : new Position(maximumX - west[unitId].X, west[unitId].Y),
                Direction.West));
        }

        ImmutableArray<Position> westHome = Rectangle(
            1, 3, (larger ? 11 : 8) + drop, (larger ? 17 : 14) + drop);
        ImmutableArray<Position> eastHome = westHome
            .Select(value => new Position(maximumX - value.X, value.Y))
            .OrderBy(value => value.Y).ThenBy(value => value.X)
            .ToImmutableArray();
        Position[] forbidden =
        [
            new(centreX, northY), new(centreX, centreY),
            new(centreX, southY), new(larger ? 1 : 2, centreY),
            new(maximumX - (larger ? 1 : 2), centreY),
        ];
        return new ActorMapDefinition(
            loopProfile.MapId,
            version: 1,
            rows,
            anchors.ToImmutable(),
            [
                .. loopProfile.HealZoneTicksPerHp > 0
                    ? new[]
                    {
                        Region(
                            "heal-north", new Position(centreX, 8 + drop)),
                        Region(
                            "heal-south", new Position(centreX, 14 + drop)),
                    }
                    : System.Array.Empty<ActorMapRegionDefinition>(),
                Region("well-north", new Position(centreX, northY)),
                Region("well-centre", new Position(centreX, centreY)),
                Region("well-south", new Position(centreX, southY)),
                Region("reactor-west", new Position(larger ? 1 : 2, centreY)),
                Region("reactor-east", new Position(
                    maximumX - (larger ? 1 : 2), centreY)),
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

    public static ActorRulesDefinition CreateRules() =>
        CreateRules(ArcRelayLoopProfile.H0);

    public static ActorRulesDefinition CreateRules(
        ArcRelayLoopProfile loopProfile)
    {
        ArgumentNullException.ThrowIfNull(loopProfile);
        ArcRelayGameModeDefinition mode = CreateMode(loopProfile);
        ActorMovementProfileDefinition[] movement =
        [
            new("swift", ActorMovementLayer.Ground,
                loopProfile.DirectionalCombat
                    ? ActorMovementFacingCoupling.FaceMovementHeadingProjected
                    : ActorMovementFacingCoupling.FaceMovementDirection),
            new("standard", ActorMovementLayer.Ground,
                loopProfile.DirectionalCombat
                    ? ActorMovementFacingCoupling.CombatStrafe
                    : ActorMovementFacingCoupling.PreserveFacing),
            new("deliberate", ActorMovementLayer.Ground,
                ActorMovementFacingCoupling.FacingLocked),
        ];
        ActorVisionProfileDefinition vision = new(
            "arc-facing-vision",
            range: 7,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: loopProfile.OmniProximityRange,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 0,
            hearingBearingSectors: 0,
            ActorHearingBearingModel.Disabled,
            hearingDistanceBandUpperBounds: [],
            loudEventKinds: []);
        ActorAttackProfileDefinition[] attacks =
        [
            // Positional combat (DECISIONS #212): the class guns take the
            // profile's strike windup; a short gun stays a quicker draw than
            // a long one through the existing range/cadence triangle.
            // Signature bolts keep their historical behavior — they are
            // utility casts, not the combat model under test.
            Attack("short-fast", range: 4, nextFireInterval: 2,
                loopProfile.DirectionalCombat,
                loopProfile.StrikeWindupTicks),
            Attack("medium-steady", range: 6, nextFireInterval: 3,
                loopProfile.DirectionalCombat,
                loopProfile.StrikeWindupTicks),
            Attack("long-slow", range: 9, nextFireInterval: 5,
                loopProfile.DirectionalCombat,
                loopProfile.StrikeWindupTicks),
            // Grammar-2 signature bolts are contract-declared projectiles:
            // the world snapshot, validators, and viewers all know them the
            // same way they know the guns. No body selects them as its gun.
            .. loopProfile.SignatureGrammarVersion >= 2
                ? new[]
                {
                    Attack("sentinel-bolt", range: 4, nextFireInterval: 2,
                        directionalCombat: false),
                    Attack("hook-bolt", range: 6, nextFireInterval: 2,
                        directionalCombat: false),
                }
                : System.Array.Empty<ActorAttackProfileDefinition>(),
        ];
        List<ActorActionDefinition> actions =
        [
            new(WaitActionId, 0, ActorActionKind.Wait, []),
            new(MoveActionId, 1, ActorActionKind.Movement,
                [ActorActionParameterKind.ProjectileHeading]),
            new(RotateActionId, 2, ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
            // A windup gun names WHO it is shooting at (DECISIONS #222,
            // owner correction): the strike locks the target the mind
            // declared, so the identity has to reach the engine. The
            // parameter is optional — a declare with no named target is the
            // deliberate suppressive whiff down the lane — and it exists only
            // on a loop profile that actually has a windup, so an instant gun
            // keeps its historical single-parameter shape and fingerprint.
            new(ShootActionId, 4, ActorActionKind.Attack,
                loopProfile.StrikeWindupTicks > 0
                    ?
                    [
                        ActorActionParameterKind.ProjectileHeading,
                        ActorActionParameterKind.UnitTarget,
                    ]
                    : [ActorActionParameterKind.ProjectileHeading]),
            new(ArcRelayActionIds.DropCore, ArcRelayActionIds.DropCoreCode,
                ActorActionKind.Objective, []),
            new(ArcRelayActionIds.HandoffCore,
                ArcRelayActionIds.HandoffCoreCode,
                ActorActionKind.Objective,
                [ActorActionParameterKind.UnitTarget]),
        ];
        if (loopProfile.VeterancyXpPerLevel > 0)
        {
            actions.Add(new ActorActionDefinition(
                ArcRelayActionIds.Invest,
                ArcRelayActionIds.InvestCode,
                ActorActionKind.ModeInvestment,
                [ActorActionParameterKind.UpgradeTrack]));
        }
        if (loopProfile.DirectionalCombat)
        {
            actions.Add(new ActorActionDefinition(
                StrafeActionId,
                3,
                ActorActionKind.Movement,
                [ActorActionParameterKind.ProjectileHeading])
            {
                MovementFacingOverride =
                    ActorMovementFacingCoupling.PreserveFacing,
            });
        }
        actions.AddRange(mode.Signatures.Select((signature, index) =>
            new ActorActionDefinition(
                signature.ActionId,
                ArcRelayActionIds.FirstSignatureCode + index,
                ActorActionKind.Signature,
                SignatureParameters(signature, loopProfile))));

        return new ActorRulesDefinition(
            loopProfile.RulesetId,
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
                    delayTicks: loopProfile.RespawnDelayTicks,
                    automaticReturnFormId: FormId(entry.ClassId)))),
            Classes.Select(entry => new ActorFormDefinition(
                FormId(entry.ClassId),
                entry.Hull,
                entry.Handling,
                vision.Id,
                entry.Gun,
                objectiveWeight: 0,
                AllowedActions(entry, loopProfile))),
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

    private static ArcRelayGameModeDefinition CreateMode(
        ArcRelayLoopProfile loopProfile) =>
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
                    "centre",
                    loopProfile.FirstGlobalBeatTicks,
                    loopProfile.WellCadenceTicks,
                    loopProfile.FirstGlobalBeatTicks
                        + (loopProfile.ScheduledBirthRounds - 1)
                            * loopProfile.WellCadenceTicks),
                new ArcRelayWellScheduleDefinition(
                    "north",
                    2 * loopProfile.FirstGlobalBeatTicks,
                    loopProfile.WellCadenceTicks,
                    2 * loopProfile.FirstGlobalBeatTicks
                        + (loopProfile.ScheduledBirthRounds - 1)
                            * loopProfile.WellCadenceTicks),
                new ArcRelayWellScheduleDefinition(
                    "south",
                    3 * loopProfile.FirstGlobalBeatTicks,
                    loopProfile.WellCadenceTicks,
                    3 * loopProfile.FirstGlobalBeatTicks
                        + (loopProfile.ScheduledBirthRounds - 1)
                            * loopProfile.WellCadenceTicks),
            ],
            pendingRearmTicks: 10,
            coreRelocationIntervalTicks: 2,
            coresPerPulse: loopProfile.CoresPerPulse,
            fieldedSlotsPerTeam: 8,
            maxCopiesPerClass: 2,
            respawnDelayTicks: loopProfile.RespawnDelayTicks,
            ClassesFor(loopProfile)
                .Select(entry => entry.Signature),
            signatureGrammarVersion: loopProfile.SignatureGrammarVersion,
            wellBirthJitterTicks: loopProfile.WellBirthJitterTicks,
            alternatingResolutionOrder:
                loopProfile.AlternatingResolutionOrder,
            threefoldSockets: loopProfile.ThreefoldSockets,
            coreBaseValue: loopProfile.CoreBaseValue,
            ripenIntervalTicks: loopProfile.RipenIntervalTicks,
            ripenMaxValue: loopProfile.RipenMaxValue,
            ripenResumeTicks: loopProfile.RipenResumeTicks,
            rearArcDamageMultiplier: loopProfile.RearArcDamageMultiplier,
            veterancyXpPerLevel: loopProfile.VeterancyXpPerLevel,
            veterancyMaxLevel: loopProfile.VeterancyMaxLevel,
            healZoneTicksPerHp: loopProfile.HealZoneTicksPerHp,
            seedPhasedResolutionOrder:
                loopProfile.SeedPhasedResolutionOrder,
            seedPhasedWellLead: loopProfile.SeedPhasedWellLead);

    /// <summary>
    /// Grammar 2 swaps exactly three envelopes for their dodgeable forms
    /// (owner ruling 2026-08-05): the sentinel fires real bolts and fires
    /// faster (2-tick cadence as compensation), the hook becomes a grapple
    /// bolt, and null-field gains a one-tick tell. Hulls, handling, guns,
    /// and every other signature are identical across grammars.
    /// </summary>
    private static ImmutableArray<LaunchClass> ClassesFor(
        ArcRelayLoopProfile loopProfile)
    {
        // Every bolt-class signature carries its OWN windup (owner ruling
        // 2026-08-08), so retuning rail against hook against sentinel is a
        // ruleset edit and never an engine edit. The default is the loop's
        // strike windup — a beam that damages should announce itself exactly
        // as long as a gun that damages — and zero authors an instant cast,
        // which is what every utility signature keeps.
        int windup = loopProfile.StrikeWindupTicks;
        return loopProfile.SignatureGrammarVersion == 1
            ? Classes
            : [.. Classes.Select(entry => entry.Signature switch
                {
                    ArcRelaySignatureDefinition.SentinelSeed seed =>
                        entry with
                        {
                            Signature = new ArcRelaySignatureDefinition
                                .SentinelSeed2(
                                    seed.SignatureId, seed.ClassId,
                                    seed.ActionId, seed.Hull, seed.Range,
                                    seed.Damage,
                                    fireCooldownTicks: 2,
                                    seed.DurationTicks,
                                    boltTilesPerAdvance: 2,
                                    seed.CooldownTicks,
                                    windupTicks: windup),
                        },
                    ArcRelaySignatureDefinition.TractorHook hook =>
                        entry with
                        {
                            Signature = new ArcRelaySignatureDefinition
                                .TractorHook2(
                                    hook.SignatureId, hook.ClassId,
                                    hook.ActionId, hook.Range,
                                    hook.MaxPullTiles,
                                    boltTilesPerAdvance: 2,
                                    hook.CooldownTicks,
                                    windupTicks: windup),
                        },
                    // A ruleset with no strike windup at all keeps rail's
                    // authored one: "default to the strike windup" is a
                    // default, not an erasure, and a beam always telegraphs.
                    ArcRelaySignatureDefinition.RailLine rail
                        when windup > 0 && windup != rail.WindupTicks =>
                        entry with
                        {
                            Signature = new ArcRelaySignatureDefinition
                                .RailLine(
                                    rail.SignatureId, rail.ClassId,
                                    rail.ActionId,
                                    windupTicks: windup,
                                    rail.Range, rail.Damage,
                                    rail.CancelCooldownTicks,
                                    rail.CooldownTicks),
                        },
                    ArcRelaySignatureDefinition.NullField field =>
                        entry with
                        {
                            Signature = new ArcRelaySignatureDefinition
                                .NullField2(
                                    field.SignatureId, field.ClassId,
                                    field.ActionId, field.Radius,
                                    field.DurationTicks,
                                    tellTicks: 1,
                                    field.CooldownTicks),
                        },
                    _ => entry,
                })];
    }

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

    /// <summary>
    /// One signature's declared argument shape.
    /// </summary>
    /// <remarks>
    /// The bolt-class LINE attacks — rail and the grammar-2 hook — name WHO
    /// they are shooting at on a ruleset that carries the strike lock (owner
    /// ruling 2026-08-09), exactly as a windup gun does since DECISIONS #222.
    /// The parameter is optional, so a declare that names nobody stays the
    /// theatrical whiff down the declared heading; and it is presence-driven
    /// on <see cref="ArcRelayLoopProfile.StrikeWindupTicks"/>, so every
    /// ruleset without the strike lock keeps its historical single-parameter
    /// shape and its fingerprint. vector-dash is movement utility and never
    /// locks anything.
    /// </remarks>
    private static ImmutableArray<ActorActionParameterKind>
        SignatureParameters(
            ArcRelaySignatureDefinition signature,
            ArcRelayLoopProfile loopProfile) =>
        signature switch
        {
            ArcRelaySignatureDefinition.TractorHook2 or
            ArcRelaySignatureDefinition.RailLine
                when loopProfile.StrikeWindupTicks > 0 =>
                [
                    ActorActionParameterKind.ProjectileHeading,
                    ActorActionParameterKind.UnitTarget,
                ],
            ArcRelaySignatureDefinition.VectorDash or
            ArcRelaySignatureDefinition.TractorHook or
            ArcRelaySignatureDefinition.TractorHook2 or
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
            ArcRelaySignatureDefinition.SentinelSeed or
            ArcRelaySignatureDefinition.SentinelSeed2 =>
                [ActorActionParameterKind.PositionTarget],
            ArcRelaySignatureDefinition.PrismWall =>
                [ActorActionParameterKind.Direction],
            ArcRelaySignatureDefinition.NullField or
            ArcRelaySignatureDefinition.NullField2 or
            ArcRelaySignatureDefinition.KineticBurst => [],
            _ => throw new ArgumentOutOfRangeException(nameof(signature)),
        };

    private static ActorAttackProfileDefinition Attack(
        string id,
        int range,
        int nextFireInterval,
        bool directionalCombat,
        int strikeWindupTicks = 0) =>
        new(
            id,
            omnidirectionalAim: !directionalCombat,
            strikeWindupTicks > 0
                ? new ActorProjectileDefinition(
                    ActorProjectileMode.InstantRay,
                    damagePerHit: 1,
                    maxTravelTiles: range,
                    ticksPerAdvance: 0,
                    tilesPerAdvance: 1,
                    launchTiles: 1,
                    advancesOnLaunchTick: false,
                    damageAppliedSimultaneously: true,
                    diagonalCornersMustBeClear: true,
                    strikeWindupTicks: strikeWindupTicks)
                : new ActorProjectileDefinition(
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
            DisabledShotProgram(),
            facingAimHalfWidthSectors: directionalCombat ? 1 : 0);

    private static IEnumerable<string> AllowedActions(
        LaunchClass entry,
        ArcRelayLoopProfile loopProfile)
    {
        yield return WaitActionId;
        yield return MoveActionId;
        if (loopProfile.DirectionalCombat
            && string.Equals(entry.Handling, "swift", StringComparison.Ordinal))
        {
            yield return StrafeActionId;
        }
        yield return RotateActionId;
        yield return ShootActionId;
        yield return ArcRelayActionIds.DropCore;
        yield return ArcRelayActionIds.HandoffCore;
        if (loopProfile.VeterancyXpPerLevel > 0)
            yield return ArcRelayActionIds.Invest;
        yield return entry.Signature.ActionId;
    }

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

    private static ImmutableArray<string> ApplyGeometry(
        ImmutableArray<string> rows,
        ArcRelayMapGeometry geometry)
    {
        if (geometry is ArcRelayMapGeometry.H0
            or ArcRelayMapGeometry.DepthLarger)
            return rows;

        if (geometry == ArcRelayMapGeometry.AmbushWarrenDeep)
        {
            // The deep warren (owner map ruling 2026-08-06: "there's no way
            // to sneak by on top/mid/bottom") wraps the serpentine interior
            // in shadow lanes: a fully open perimeter corridor top and
            // bottom behind a screen wall with three gates. The gate columns
            // are an x-symmetric set and the two screens are identical, so
            // the wrap is exactly 180-degree chiral and the interior needs
            // no re-derivation - every interior feature just sits two rows
            // lower on the canvas.
            ImmutableArray<string> interior = ApplyGeometry(
                rows, ArcRelayMapGeometry.AmbushWarrenSerpentine);
            int width = interior[0].Length;
            string corridor = $"#{new string('.', width - 2)}#";
            char[] screenChars = new string('#', width).ToCharArray();
            foreach (int gate in new[] { 3, 15, 27 })
                screenChars[gate] = '.';
            string screen = new string(screenChars);
            return
            [
                interior[0],
                corridor,
                screen,
                .. interior.Skip(1).Take(interior.Length - 2),
                screen,
                corridor,
                interior[^1],
            ];
        }

        string[] changed = rows.ToArray();
        if (geometry == ArcRelayMapGeometry.HomeGatesWide)
        {
            foreach (int y in new[] { 1, 5, 8, 14, 17, 21 })
            {
                changed[y] = Open(changed[y], 5, 25);
            }
        }
        else if (geometry == ArcRelayMapGeometry.HomeGatesThree)
        {
            foreach (int y in new[]
                { 1, 3, 5, 7, 8, 10, 12, 14, 15, 17, 19, 21 })
            {
                changed[y] = Open(changed[y], 5, 25);
            }
        }
        else if (geometry == ArcRelayMapGeometry.HomeConcourse)
        {
            foreach (int y in new[] { 8, 10, 11, 12, 14 })
            {
                changed[y] = Open(changed[y], 5, 25);
            }
        }
        else if (geometry == ArcRelayMapGeometry.CoverTrim)
        {
            foreach (int y in new[] { 3, 4, 5, 10, 11, 12, 17, 18, 19 })
            {
                changed[y] = Open(changed[y], 10, 20);
            }
        }
        else if (geometry is ArcRelayMapGeometry.DepthCounterflow
                 or ArcRelayMapGeometry.AmbushWarren
                 or ArcRelayMapGeometry.AmbushWarrenDense
                 or ArcRelayMapGeometry.AmbushWarrenSerpentine)
        {
            foreach (int y in new[] { 1, 5, 8, 14, 17, 21 })
                changed[y] = Open(changed[y], 5, 25);

            // Swap equal cover mass into a chiral counterflow. The second half
            // of each pair is the exact 180-degree partner of the first: west
            // gets the faster north entry, east the faster south entry, while
            // neither participant owns a globally better side.
            foreach ((int x, int y) in new[]
            {
                (10, 5), (11, 5), (12, 5),
                (20, 17), (19, 17), (18, 17),
            })
            {
                changed[y] = Set(changed[y], x, '.');
            }
            foreach ((int x, int y) in new[]
            {
                (6, 3), (7, 3), (8, 3),
                (24, 19), (23, 19), (22, 19),
            })
            {
                changed[y] = Set(changed[y], x, '#');
            }
            if (geometry is ArcRelayMapGeometry.AmbushWarren
                or ArcRelayMapGeometry.AmbushWarrenDense
                or ArcRelayMapGeometry.AmbushWarrenSerpentine)
            {
                // Counterflow plus predation terrain, every wall in an exact
                // 180-degree chiral pair: sightline breakers across the open
                // strips so the facing-quadrant vision and team-union
                // projectile visibility actually bite, and two dead-end
                // alcove pairs (pockets at (8,4)/(22,18) and (8,18)/(22,4))
                // whose occupants are invisible except through the opening.
                // Verified against every layout route, sheet path, anchor,
                // spawn pad, gate hole, well ring, and reactor ring; all 507
                // open tiles remain mutually reachable.
                foreach ((int x, int y) in new[]
                {
                    (9, 2), (10, 2), (11, 2), (21, 20), (20, 20), (19, 20),
                    (16, 1), (17, 1), (14, 21), (13, 21),
                    (12, 6), (18, 16),
                    (10, 9), (13, 9), (20, 13), (17, 13),
                    (7, 4), (7, 5), (8, 5), (23, 18), (23, 17), (22, 17),
                    (7, 17), (7, 18), (8, 17), (8, 19),
                    (23, 5), (23, 4), (22, 5), (22, 3),
                })
                {
                    changed[y] = Set(changed[y], x, '#');
                }
            }
            if (geometry is ArcRelayMapGeometry.AmbushWarrenDense
                or ArcRelayMapGeometry.AmbushWarrenSerpentine)
            {
                // Owner review: the warren still played too open. Every
                // horizontal corridor now breaks (border lanes, second
                // strips, gate rows, centre approaches), north-south transit
                // weaves through the x12/x18 stubs, and the centre pillars
                // grow hooks. Chiral pairs throughout; verified against all
                // protected tiles with 475 open tiles mutually reachable.
                foreach ((int x, int y) in new[]
                {
                    (7, 1), (8, 1), (13, 1), (14, 1),
                    (23, 21), (22, 21), (17, 21), (16, 21),
                    (20, 2), (21, 2), (10, 20), (9, 20),
                    (18, 6), (24, 6), (12, 16), (6, 16),
                    (13, 8), (14, 8), (20, 8), (21, 8),
                    (17, 14), (16, 14), (10, 14), (9, 14),
                    (7, 9), (23, 13),
                    (12, 7), (12, 8), (18, 15), (18, 14),
                    (13, 12), (17, 10),
                })
                {
                    changed[y] = Set(changed[y], x, '#');
                }
            }
            if (geometry == ArcRelayMapGeometry.AmbushWarrenSerpentine)
            {
                // Owner review: well-to-base returns still ran straight.
                // Single-tile return chokes (passage only at (7,11) and
                // (23,11)), lane serpentine staggers, a south split-lane
                // divider, and centre orbit blockers. Verified: zero
                // protected clashes, 461 open tiles mutually reachable,
                // exact chirality.
                foreach ((int x, int y) in new[]
                {
                    (7, 10), (7, 12), (23, 12), (23, 10),
                    (9, 5), (14, 7), (21, 17), (16, 15),
                    (10, 15), (11, 15), (20, 7), (19, 7),
                    (14, 13), (16, 9),
                })
                {
                    changed[y] = Set(changed[y], x, '#');
                }
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(geometry));
        }
        return [.. changed];
    }

    private static string Open(string row, params int[] columns)
    {
        char[] changed = row.ToCharArray();
        foreach (int x in columns)
            changed[x] = '.';
        return new string(changed);
    }

    private static string Set(string row, int column, char value)
    {
        char[] changed = row.ToCharArray();
        changed[column] = value;
        return new string(changed);
    }

    private static ImmutableArray<string> CreateLargerRows()
    {
        const int width = 31;
        const int height = 29;
        char[][] rows = Enumerable.Range(0, height)
            .Select(y => Enumerable.Range(0, width)
                .Select(x => x == 0 || x == width - 1
                    || y == 0 || y == height - 1 ? '#' : '.')
                .ToArray())
            .ToArray();
        foreach (int y in new[]
            { 3, 4, 5, 8, 12, 13, 14, 15, 20, 23, 24, 25 })
        {
            rows[y][5] = '#';
            rows[y][25] = '#';
        }
        foreach (int y in new[]
            { 3, 4, 5, 6, 12, 13, 14, 15, 22, 23, 24, 25 })
        {
            foreach (int x in new[] { 10, 11, 12, 18, 19, 20 })
                rows[y][x] = '#';
        }
        return [.. rows.Select(row => new string(row))];
    }

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
