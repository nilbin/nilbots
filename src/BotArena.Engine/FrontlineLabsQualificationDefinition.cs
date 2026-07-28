using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Versioned, local-only resolved contracts used to qualify gameplay
/// capabilities before artifacts may contribute balance evidence. A probe is
/// an ordinary generic match contract; bots receive no hidden scenario API.
/// </summary>
public static class FrontlineLabsQualificationDefinition
{
    public const string SuiteId = "frontline-qualification-1";
    public const string FoundationSuiteId = "frontline-qualification-2";
    public const int FoundationSuiteVersion = 2;
    public const string FoundationProfileId =
        "frontline-h2h-one-bend-auto-foundation-1";
    public const string FundamentalsSuiteId = "frontline-qualification-3";
    public const int FundamentalsSuiteVersion = 1;
    public const string FundamentalsProfileId =
        "frontline-duel-depth-union-t2-v1";
    public const string TacticalSuiteId = "frontline-qualification-4";
    public const int TacticalSuiteVersion = 1;
    public const string TacticalProfileId =
        "frontline-duel-depth-union-t3-v1";
    public const string EntryProbeId = "entry-initiative";
    public const string ContractAutoDeterminismProbeId =
        "contract-auto-determinism";
    public const string ContractMatrixProbeId = "contract-matrix";
    public const string AutomaticLifeCycleProbeId =
        "automatic-life-cycle";
    public const string ObjectivePathProbeId = "objective-path";
    public const string DirectFireProbeId = "direct-fire";
    public const string StraightEvadeProbeId = "straight-evade";
    public const string ManualFabricationProbeId = "manual-fabrication";
    public const string WallTerminatedBendProbeId =
        "wall-terminated-bend";
    public const string StrictCornerProbeId = "strict-corner";
    public const string CadenceParityProbeId = "cadence-parity";
    public const string CooldownWindowProbeId = "cooldown-window";
    public const string LocalFormSafetyProbeId = "local-form-safety";

    private const string Team0SpawnId =
        "qualification-team-0-prime";
    private const string Team1SpawnId =
        "qualification-team-1-prime";

    /// <summary>
    /// Starts the two Primes at the known six-tile central suppression
    /// approach and ends immediately before the first companion unlock. The
    /// rules retain the exact one-bend projectile semantics under a distinct
    /// qualification identity.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateEntryProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeLimits(
            source.Rules,
            $"{SuiteId}-{EntryProbeId}",
            maxTicks: 120);
        ActorMapDefinition map = new(
            $"{SuiteId}-{EntryProbeId}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(
                    Team0SpawnId,
                    new Position(8, 7),
                    Direction.East),
                Spawn(
                    Team1SpawnId,
                    new Position(14, 7),
                    Direction.West),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        InitialDeploymentDefinition deployment = new(
            [
                new InitialSpawnDefinition(
                    Team0SpawnId,
                    new Position(8, 7),
                    Direction.East),
                new InitialSpawnDefinition(
                    Team1SpawnId,
                    new Position(14, 7),
                    Direction.West),
            ],
            [
                .. source.InitialDeployment.Lives.Select(life =>
                    new InitialLifeDeployment(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId,
                        life.FormId,
                        life.TeamId == 0
                            ? Team0SpawnId
                            : Team1SpawnId)),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] lifecycleAssignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                new ActorUnitSlotLifecycleAssignmentDefinition(
                    assignment.TeamId,
                    assignment.UnitId,
                    assignment.LifecycleProfileId,
                    assignment.InitialGeneration,
                    assignment.AllowedFormIds,
                    assignment.InitialAvailability,
                    assignment.UnlockTick,
                    assignment.UnitId == 0
                        ? assignment.TeamId == 0
                            ? Team0SpawnId
                            : Team1SpawnId
                        : assignment.AssignedRespawnSpawnId)),
        ];
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives = source.Topology.InitialLives
                .Where(life => life.UnitId == 0)
                .ToImmutableArray(),
        };

        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            lifecycleAssignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// First immutable component of the v2 foundation profile. The ordinary
    /// automatic-companion contract is shortened to one unlock and given an
    /// unreachable capture threshold so every conforming artifact observes
    /// the same full topology window. No tier is awarded by this component.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateContractAutoDeterminismProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();
        var frontline = (FrontlineGameModeDefinition)
            source.Rules.GameMode;
        FrontlineCaptureDefinition capture = frontline.Capture;
        var probeMode = new FrontlineGameModeDefinition(
            frontline.FrontlineVictory,
            frontline.ScoreCatalog,
            frontline.FrontlinePositionCount,
            new FrontlineCaptureDefinition(
                threshold: 1000,
                capture.GainPerSoleTeamTick,
                capture.DecayAmount,
                capture.DecayIntervalTicks,
                capture.RedeployPauseTicks,
                capture.GainSchedule,
                capture.ControlPolicy));
        var rules = new ActorRulesDefinition(
            $"{FoundationSuiteId}-{ContractAutoDeterminismProbeId}",
            new ActorRulesLimits(
                maxTicks: 130,
                source.Rules.Limits.RuntimeFaults),
            source.Rules.SeedMechanics,
            probeMode,
            source.Rules.Lifecycle,
            source.Rules.Forms,
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

        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId != 2)
                .ToImmutableArray(),
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] lifecycleAssignments =
        [
            .. source.LifecycleAssignments.Where(assignment =>
                assignment.UnitId != 2),
        ];

        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            lifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// T1 contract/count/identity probe for the union profile. It uses
    /// non-default participant IDs and exposes all three stable unit slots,
    /// with automatic children accelerated only for this local contract.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateContractMatrixProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{FundamentalsSuiteId}-{ContractMatrixProbeId}",
            maxTicks: 24,
            captureThreshold: 1000,
            removePopulationActions: false);
        IReadOnlyDictionary<int, int> participantIds =
            new Dictionary<int, int>
            {
                [0] = 7,
                [1] = 19,
            };
        var topology = source.Topology with
        {
            Participants = source.Topology.Participants
                .Select(participant => new PublicParticipant(
                    participantIds[participant.ParticipantId],
                    participant.TeamId))
                .ToImmutableArray(),
            UnitSlots = source.Topology.UnitSlots
                .Select(slot => new PublicUnitSlot(
                    slot.TeamId,
                    slot.UnitId,
                    participantIds[slot.ControllerParticipantId]))
                .ToImmutableArray(),
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments.Select(assignment =>
            {
                if (assignment.UnitId == 0)
                    return assignment;
                int unlockTick = assignment.UnitId == 1 ? 4 : 8;
                return new ActorUnitSlotLifecycleAssignmentDefinition(
                    assignment.TeamId,
                    assignment.UnitId,
                    assignment.LifecycleProfileId,
                    assignment.InitialGeneration,
                    assignment.AllowedFormIds,
                    assignment.InitialAvailability,
                    unlockTick,
                    assignment.AssignedRespawnSpawnId);
            }),
        ];
        ActorParticipantRegionAssignmentDefinition[] regions =
        [
            .. source.ParticipantRegionAssignments.Select(assignment =>
                new ActorParticipantRegionAssignmentDefinition(
                    participantIds[assignment.ParticipantId],
                    assignment.RegionRoleId,
                    assignment.MapRegionId,
                    assignment.Facing)),
        ];
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            assignments,
            regions,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// T2 objective-path micro-scenario. The tested team starts at the
    /// two-step central approach while the passive controller is placed in a
    /// distant map corner. The active objective and every route fact remain
    /// ordinary contract data.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateObjectivePathProbe(
        int testedTeamId)
    {
        if (testedTeamId is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(testedTeamId));
        Position team0 = testedTeamId == 0
            ? new Position(8, 7)
            : new Position(2, 1);
        Position team1 = testedTeamId == 1
            ? new Position(14, 7)
            : new Position(20, 1);
        return CreatePrimeOnlyProbe(
            ObjectivePathProbeId,
            maxTicks: 24,
            team0,
            testedTeamId == 0 ? Direction.East : Direction.South,
            team1,
            testedTeamId == 1 ? Direction.West : Direction.South);
    }

    /// <summary>
    /// T2 clear-lane attack micro-scenario. Both assignments see the same
    /// open range-six line and an inert target.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateDirectFireProbe() =>
        CreatePrimeOnlyProbe(
            DirectFireProbeId,
            maxTicks: 20,
            new Position(8, 7),
            Direction.East,
            new Position(14, 7),
            Direction.West);

    /// <summary>
    /// T2 straight-projectile evasion micro-scenario. A framework controller
    /// fires once down the open central line and then waits.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateStraightEvadeProbe() =>
        CreatePrimeOnlyProbe(
            StraightEvadeProbeId,
            maxTicks: 12,
            new Position(8, 7),
            Direction.East,
            new Position(14, 7),
            Direction.West);

    /// <summary>
    /// T2 explicit lifecycle half of the union profile. One child slot is
    /// Ready at tick zero while each Prime is still on its declared local
    /// fabrication source. The passive opponent cannot interfere.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateManualFabricationProbe()
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{FundamentalsSuiteId}-{ManualFabricationProbeId}",
            maxTicks: 20,
            captureThreshold: 1000,
            removePopulationActions: false);
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId != 2)
                .ToImmutableArray(),
        };
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId != 2)
                .Select(assignment =>
                {
                    if (assignment.UnitId != 1)
                        return assignment;
                    return new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        initialGeneration: null,
                        assignment.AllowedFormIds,
                        ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.DormantUnlockAtTick,
                        unlockTick: 0,
                        assignedRespawnSpawnId: null);
                }),
        ];
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    /// <summary>
    /// T3 positive geometry probe. The tested Prime sees an off-axis target
    /// that cannot be reached by a legal straight or aim-only shot. A
    /// one-bend program after the first tile hits; the opposite bend enters
    /// the central lower pocket and terminates against its declared wall.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateWallTerminatedBendProbe(int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                WallTerminatedBendProbeId,
                maxTicks: 12,
                new Position(8, 7),
                Direction.East,
                "prime-mobile",
                new Position(12, 6),
                Direction.South,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                WallTerminatedBendProbeId,
                maxTicks: 12,
                new Position(10, 6),
                Direction.South,
                "prime-mobile",
                new Position(14, 7),
                Direction.West,
                "prime-mobile");
    }

    /// <summary>
    /// T3 negative strict-corner probe. The tested Prime holds the central
    /// objective and sees an off-axis opponent through a clear supercover
    /// line. A lax one-bend preview appears to hit after three tiles, but the
    /// authoritative strict diagonal is blocked by the centre-column wall.
    /// The preceding positive bend probe prevents "never curve" from gaming
    /// this selective-safety case.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateStrictCornerProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                StrictCornerProbeId,
                maxTicks: 8,
                new Position(10, 7),
                Direction.North,
                "prime-mobile",
                new Position(9, 3),
                Direction.South,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                StrictCornerProbeId,
                maxTicks: 8,
                new Position(13, 3),
                Direction.South,
                "prime-mobile",
                new Position(12, 7),
                Direction.North,
                "prime-mobile");
    }

    /// <summary>
    /// T3 cadence pair. The geometry is identical while the declared mobile
    /// projectile range is either three (ends one tile short) or four
    /// (reaches the objective occupant). The artifact must read the resolved
    /// remaining-range semantics rather than assume even speed-two steps.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateCadenceParityProbe(
        int testedTeamId,
        int projectileRange)
    {
        ValidateTestedTeam(testedTeamId);
        if (projectileRange is not (3 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectileRange));
        }
        ActorResolvedMatchDefinition probe = testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                $"{CadenceParityProbeId}-range-{projectileRange}",
                maxTicks: 8,
                new Position(11, 7),
                Direction.East,
                "prime-mobile",
                new Position(15, 7),
                Direction.West,
                "prime-mobile",
                mapIdentityId: CadenceParityProbeId)
            : CreateTacticalSingleLifeProbe(
                $"{CadenceParityProbeId}-range-{projectileRange}",
                maxTicks: 8,
                new Position(7, 7),
                Direction.East,
                "prime-mobile",
                new Position(11, 7),
                Direction.West,
                "prime-mobile",
                mapIdentityId: CadenceParityProbeId);
        return WithMobileProjectileRange(probe, projectileRange);
    }

    /// <summary>
    /// T3 tempo probe. The tested Prime begins two steps from the central
    /// objective while the controller commits its one allowed opening shot.
    /// The declared cooldown creates a bounded window for useful movement or
    /// damage.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateCooldownWindowProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                CooldownWindowProbeId,
                maxTicks: 10,
                new Position(8, 7),
                Direction.East,
                "prime-mobile",
                new Position(14, 7),
                Direction.North,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                CooldownWindowProbeId,
                maxTicks: 10,
                new Position(8, 7),
                Direction.North,
                "prime-mobile",
                new Position(14, 7),
                Direction.West,
                "prime-mobile");
    }

    /// <summary>
    /// T3 local commitment probe. The tested child already supplies positive
    /// weight on the active objective. Transforming into the available
    /// weight-zero turret is locally dominated by continuing to control it
    /// against a passive distant opponent.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateLocalFormSafetyProbe(
        int testedTeamId)
    {
        ValidateTestedTeam(testedTeamId);
        return testedTeamId == 0
            ? CreateTacticalSingleLifeProbe(
                LocalFormSafetyProbeId,
                maxTicks: 8,
                new Position(11, 8),
                Direction.North,
                "child-mobile",
                new Position(20, 1),
                Direction.South,
                "prime-mobile")
            : CreateTacticalSingleLifeProbe(
                LocalFormSafetyProbeId,
                maxTicks: 8,
                new Position(2, 1),
                Direction.South,
                "prime-mobile",
                new Position(11, 8),
                Direction.North,
                "child-mobile");
    }

    private static ActorResolvedMatchDefinition CreatePrimeOnlyProbe(
        string probeId,
        int maxTicks,
        Position team0Position,
        Direction team0Facing,
        Position team1Position,
        Direction team1Facing)
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{FundamentalsSuiteId}-{probeId}",
            maxTicks,
            captureThreshold: 1000,
            removePopulationActions: true);
        string team0SpawnId =
            $"{FundamentalsSuiteId}-{probeId}-team-0";
        string team1SpawnId =
            $"{FundamentalsSuiteId}-{probeId}-team-1";
        var map = new ActorMapDefinition(
            $"{FundamentalsSuiteId}-{probeId}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(team0SpawnId, team0Position, team0Facing),
                Spawn(team1SpawnId, team1Position, team1Facing),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    team0SpawnId,
                    team0Position,
                    team0Facing),
                new InitialSpawnDefinition(
                    team1SpawnId,
                    team1Position,
                    team1Facing),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    "prime-mobile",
                    team0SpawnId),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    "prime-mobile",
                    team1SpawnId),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                    new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        assignment.InitialGeneration,
                        assignment.AllowedFormIds,
                        assignment.InitialAvailability,
                        assignment.UnlockTick,
                        assignment.TeamId == 0
                            ? team0SpawnId
                            : team1SpawnId)),
        ];
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives = source.Topology.InitialLives
                .Where(life => life.UnitId == 0)
                .ToImmutableArray(),
        };
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition
        CreateTacticalSingleLifeProbe(
            string probeId,
            int maxTicks,
            Position team0Position,
            Direction team0Facing,
            string team0FormId,
            Position team1Position,
            Direction team1Facing,
            string team1FormId,
            string? mapIdentityId = null)
    {
        ActorResolvedMatchDefinition source =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorRulesDefinition rules = WithProbeMode(
            source.Rules,
            $"{TacticalSuiteId}-{probeId}",
            maxTicks,
            captureThreshold: 1000,
            removePopulationActions: true);
        string mapIdentity = mapIdentityId ?? probeId;
        string team0SpawnId =
            $"{TacticalSuiteId}-{mapIdentity}-team-0";
        string team1SpawnId =
            $"{TacticalSuiteId}-{mapIdentity}-team-1";
        var map = new ActorMapDefinition(
            $"{TacticalSuiteId}-{mapIdentity}-map",
            version: 1,
            source.Map.TileRows,
            [
                Spawn(team0SpawnId, team0Position, team0Facing),
                Spawn(team1SpawnId, team1Position, team1Facing),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    team0SpawnId,
                    team0Position,
                    team0Facing),
                new InitialSpawnDefinition(
                    team1SpawnId,
                    team1Position,
                    team1Facing),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    team0FormId,
                    team0SpawnId),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    team1FormId,
                    team1SpawnId),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            .. source.LifecycleAssignments
                .Where(assignment => assignment.UnitId == 0)
                .Select(assignment =>
                {
                    string initialForm = assignment.TeamId == 0
                        ? team0FormId
                        : team1FormId;
                    string[] allowedForms = initialForm switch
                    {
                        "child-mobile" =>
                        [
                            "prime-mobile",
                            "child-mobile",
                            "turret",
                        ],
                        "turret" =>
                        [
                            "prime-mobile",
                            "turret",
                        ],
                        _ => ["prime-mobile"],
                    };
                    return new ActorUnitSlotLifecycleAssignmentDefinition(
                        assignment.TeamId,
                        assignment.UnitId,
                        assignment.LifecycleProfileId,
                        assignment.InitialGeneration,
                        allowedForms,
                        assignment.InitialAvailability,
                        assignment.UnlockTick,
                        assignment.TeamId == 0
                            ? team0SpawnId
                            : team1SpawnId);
                }),
        ];
        var topology = source.Topology with
        {
            UnitSlots = source.Topology.UnitSlots
                .Where(slot => slot.UnitId == 0)
                .ToImmutableArray(),
            InitialLives =
            [
                new PublicInitialLife(0, 0, 0, team0FormId),
                new PublicInitialLife(1, 0, 0, team1FormId),
            ],
        };
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            [],
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition WithMobileProjectileRange(
        ActorResolvedMatchDefinition source,
        int maxTravelTiles)
    {
        ActorAttackProfileDefinition[] attacks =
        [
            .. source.Rules.AttackProfiles.Select(profile =>
            {
                if (profile.Id != "mobile-bolt")
                    return profile;
                ActorProjectileDefinition projectile = profile.Projectile;
                return new ActorAttackProfileDefinition(
                    profile.Id,
                    profile.OmnidirectionalAim,
                    new ActorProjectileDefinition(
                        projectile.Mode,
                        projectile.DamagePerHit,
                        maxTravelTiles,
                        projectile.TicksPerAdvance,
                        projectile.TilesPerAdvance,
                        projectile.LaunchTiles,
                        projectile.AdvancesOnLaunchTick,
                        projectile.DamageAppliedSimultaneously,
                        projectile.DiagonalCornersMustBeClear),
                    profile.CooldownTicks,
                    profile.MaxEnergy,
                    profile.AttackEnergyCost,
                    profile.EnergyRegenerationIntervalTicks,
                    profile.EnergyRegenerationAmount,
                    profile.ShotProgram);
            }),
        ];
        ActorRulesDefinition rules = source.Rules;
        var rangedRules = new ActorRulesDefinition(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles,
            rules.VisionProfiles,
            attacks,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            rangedRules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static void ValidateTestedTeam(int testedTeamId)
    {
        if (testedTeamId is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(testedTeamId));
    }

    private static ActorRulesDefinition WithProbeMode(
        ActorRulesDefinition source,
        string rulesetId,
        int maxTicks,
        int captureThreshold,
        bool removePopulationActions)
    {
        var frontline = (FrontlineGameModeDefinition)source.GameMode;
        FrontlineCaptureDefinition capture = frontline.Capture;
        var mode = new FrontlineGameModeDefinition(
            frontline.FrontlineVictory,
            frontline.ScoreCatalog,
            frontline.FrontlinePositionCount,
            new FrontlineCaptureDefinition(
                captureThreshold,
                capture.GainPerSoleTeamTick,
                capture.DecayAmount,
                capture.DecayIntervalTicks,
                capture.RedeployPauseTicks,
                capture.GainSchedule,
                capture.ControlPolicy));
        HashSet<string> removedActionIds = removePopulationActions
            ? ["fabricate", "split"]
            : [];
        return new ActorRulesDefinition(
            rulesetId,
            new ActorRulesLimits(maxTicks, source.Limits.RuntimeFaults),
            source.SeedMechanics,
            mode,
            source.Lifecycle,
            source.Forms.Select(form =>
                removedActionIds.Count == 0
                    ? form
                    : new ActorFormDefinition(
                        form.Id,
                        form.MaxHealth,
                        form.MovementProfileId,
                        form.VisionProfileId,
                        form.AttackProfileId,
                        form.ObjectiveWeight,
                        form.AllowedActionIds.Where(actionId =>
                            !removedActionIds.Contains(actionId)))),
            source.MovementProfiles,
            source.VisionProfiles,
            source.AttackProfiles,
            source.Actions.Where(action =>
                !removedActionIds.Contains(action.Id)),
            removePopulationActions
                ? []
                : source.FabricationTransitions,
            source.SameLifeTransitions,
            removePopulationActions
                ? []
                : source.ReplicationTransitions,
            source.TeamPerception,
            source.Collisions,
            source.TickResolution);
    }

    private static ActorRulesDefinition WithProbeLimits(
        ActorRulesDefinition source,
        string rulesetId,
        int maxTicks) =>
        new(
            rulesetId,
            new ActorRulesLimits(
                maxTicks,
                source.Limits.RuntimeFaults),
            source.SeedMechanics,
            source.GameMode,
            source.Lifecycle,
            source.Forms.Select(form =>
                form.Id == "prime-mobile"
                    ? new ActorFormDefinition(
                        form.Id,
                        form.MaxHealth,
                        form.MovementProfileId,
                        form.VisionProfileId,
                        form.AttackProfileId,
                        form.ObjectiveWeight,
                        form.AllowedActionIds.Where(actionId =>
                            actionId is not ("fabricate" or "split")))
                    : form),
            source.MovementProfiles,
            source.VisionProfiles,
            source.AttackProfiles,
            source.Actions.Where(action =>
                action.Id is not ("fabricate" or "split")),
            [],
            source.SameLifeTransitions,
            [],
            source.TeamPerception,
            source.Collisions,
            source.TickResolution);

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        Position position,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(id, position, facing),
            ImmutableArray.Create(ActorMovementLayer.Ground));
}
