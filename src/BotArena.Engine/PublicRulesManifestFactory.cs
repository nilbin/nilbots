using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Projects authoritative engine configuration into immutable public contracts.</summary>
public static class PublicRulesManifestFactory
{
    private const string MobileFormId = "mobile";

    public static PublicRulesManifest CreateRules(GameRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        bool shotProgramsEnabled =
            rules.AllowProgrammedShots && rules.ProjectileTicksPerTile > 0;
        ImmutableArray<PublicActionDefinition> actions =
        [
            new("wait", (int)BotAction.Wait, PublicActionKind.Wait,
                PublicActionParameterKind.None, true),
            new("move-forward", (int)BotAction.MoveForward, PublicActionKind.Movement,
                PublicActionParameterKind.None, true),
            new("turn-left", (int)BotAction.TurnLeft, PublicActionKind.Rotation,
                PublicActionParameterKind.None, true),
            new("turn-right", (int)BotAction.TurnRight, PublicActionKind.Rotation,
                PublicActionParameterKind.None, true),
            new("shoot", (int)BotAction.Shoot, PublicActionKind.Attack,
                shotProgramsEnabled
                    ? PublicActionParameterKind.ShotProgram
                    : PublicActionParameterKind.None,
                true),
            new("strafe-left", (int)BotAction.StrafeLeft, PublicActionKind.Movement,
                PublicActionParameterKind.None, rules.AllowStrafe),
            new("strafe-right", (int)BotAction.StrafeRight, PublicActionKind.Movement,
                PublicActionParameterKind.None, rules.AllowStrafe),
        ];
        actions = actions.OrderBy(action => action.Code).ToImmutableArray();

        ImmutableArray<string> allowedActionIds = actions
            .Where(action => action.Enabled)
            .Select(action => action.Id)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        var manifest = new PublicRulesManifest
        {
            SchemaVersion = BotArenaVersions.PublicManifestSchemaVersion,
            RulesetId = rules.RulesVersion,
            RulesFingerprint = "",
            Limits = new PublicMatchLimits(
                rules.MaxTicks,
                rules.FaultLimit,
                TeamCount: 2,
                ParticipantCount: 2,
                UnitSlotCount: 2,
                InitialUnitsPerTeam: 1,
                MaxUnitsPerTeam: 1,
                DestructionEndsMatch: true,
                RespawnsEnabled: false),
            Objective = new PublicObjectiveRules(
                rules.ZoneControl
                    ? rules.ActiveZoneControl
                        ? PublicObjectiveMode.SharedPressure
                        : PublicObjectiveMode.ZoneTicks
                    : PublicObjectiveMode.None,
                rules.ZoneControl,
                rules.ZoneDominationTicks,
                rules.ZoneExclusiveAccrual,
                rules.ActiveZoneControl,
                rules.ControlBySoleOccupancy,
                rules.ControlPressureLimit,
                rules.ControlPressureGain,
                rules.ControlPressureDecayInterval,
                new PublicObjectiveOvertimeRules(
                    rules.ControlOvertimeStartTick,
                    rules.ControlOvertimePressureLimit,
                    rules.ControlOvertimePressureGain,
                    rules.ControlOvertimeStopsDecay),
                rules.ZoneControl
                    ? [PublicScoreMetric.Objective, PublicScoreMetric.Health, PublicScoreMetric.DamageDealt]
                    : [PublicScoreMetric.Health, PublicScoreMetric.DamageDealt]),
            Energy = new PublicEnergyRules(
                rules.MaxEnergy > 0,
                rules.MaxEnergy,
                rules.ShotEnergyCost,
                rules.EnergyRegenTicks,
                RegenerationAmount: 1),
            Forms =
            [
                new PublicFormDefinition(
                    MobileFormId,
                    rules.MaxHealth,
                    PublicMovementLayer.Ground,
                    ObjectiveWeight: 1,
                    allowedActionIds),
            ],
            Actions = actions,
            Projectiles = new PublicProjectileRules(
                rules.ProjectileTicksPerTile > 0
                    ? PublicProjectileMode.Discrete
                    : PublicProjectileMode.InstantRay,
                rules.DamagePerHit,
                rules.ShotRange,
                rules.ShootCooldownTicks,
                rules.ProjectileTicksPerTile,
                rules.ProjectileTilesPerAdvance,
                LaunchTiles: 1,
                AdvancesOnLaunchTick: false,
                DamageAppliedSimultaneously: true),
            ShotPrograms = new PublicShotProgramRules(
                Enabled: shotProgramsEnabled,
                HeadingSectors: 8,
                BendStepOctants: 1,
                MinInitialAimOctants: -rules.ProgrammedShotMaxInitialAimOctants,
                MaxInitialAimOctants: rules.ProgrammedShotMaxInitialAimOctants,
                AimOnlyProgram: new PublicAimOnlyShotProgramRules(
                    BendDirection: 0,
                    BendAfterTiles: 0,
                    BendEveryTiles: 1,
                    BendCount: 0),
                AllowedCurvedBendDirections: [-1, 1],
                MinBendAfterTiles: 1,
                MaxBendAfterTiles: rules.ProgrammedShotMaxBendAfterTiles,
                MinBendEveryTiles: 1,
                MaxBendEveryTiles: rules.ProgrammedShotMaxBendEveryTiles,
                MinBendCount: 1,
                MaxBendCount: rules.ProgrammedShotMaxBendCount,
                LaunchTiles: Math.Max(1, rules.ProgrammedShotLaunchTiles),
                PayloadOptional: true,
                DefaultProgram: new PublicShotProgramValue(
                    ShotProgram.Straight.InitialAimOffset,
                    ShotProgram.Straight.BendDirection,
                    ShotProgram.Straight.BendAfterTiles,
                    ShotProgram.Straight.BendEveryTiles,
                    ShotProgram.Straight.BendCount),
                InvalidPayloadResult: shotProgramsEnabled
                    ? PublicActionRejectionResult.Faulted
                    : null,
                UnsupportedPayloadResult: PublicActionRejectionResult.Blocked,
                DiagonalCornersMustBeClear: true),
            Vision = new PublicVisionRules(
                rules.VisionRange,
                PublicDistanceMetric.Chebyshev,
                rules.VisionCone
                    ? PublicVisionShape.FacingQuadrant
                    : PublicVisionShape.Omnidirectional,
                rules.VisionCone ? 1 : 0,
                PublicLineOfSightModel.CornerStrictSupercover,
                rules.HearingRadius,
                HearingBearingSectors: 8,
                [Hearing.NearMax, Hearing.MediumMax],
                [GameEventType.Shot, GameEventType.Damage, GameEventType.Destroyed]),
            Collisions = new PublicCollisionRules(
                UnitsBlockWalls: true,
                UnitsBlockUnits: true,
                SameDestinationMovesBlockAll: true,
                SwapMovesBlocked: true,
                FollowingVacatedUnitAllowed: true,
                ProjectilesBlockMovement: false,
                MovingOntoProjectileCausesHit: true,
                WallsConsumeProjectiles: true,
                ProjectilesIgnoreOwner: true,
                ProjectilesStopOnFirstNonOwnerUnit: true,
                ProjectilesCollideWithProjectiles: false),
            TickResolution = new PublicTickResolutionRules(
                ObservationsUsePreTickState: true,
                DecisionsResolveAsJointStep: true,
                [
                    PublicTickResolutionPhase.FreezeObservations,
                    PublicTickResolutionPhase.CollectJointDecisions,
                    PublicTickResolutionPhase.ValidateActions,
                    PublicTickResolutionPhase.Rotate,
                    PublicTickResolutionPhase.Move,
                    PublicTickResolutionPhase.AdvanceExistingProjectiles,
                    PublicTickResolutionPhase.LaunchShotsAndApplyDamage,
                    PublicTickResolutionPhase.UpdateCooldownsAndEnergy,
                    PublicTickResolutionPhase.ApplyRuntimeFaults,
                    PublicTickResolutionPhase.UpdateObjective,
                    PublicTickResolutionPhase.ResolveMatchCompletion,
                ]),
        };

        return manifest with
        {
            RulesFingerprint = MatchContractFingerprint.ComputeRules(manifest, rules),
        };
    }

    public static PublicMapManifest CreateMap(ArenaMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var manifest = new PublicMapManifest
        {
            SchemaVersion = BotArenaVersions.PublicManifestSchemaVersion,
            MapId = map.Id,
            MapVersion = map.Version,
            MapFingerprint = "",
            FormatVersion = 1,
            Width = map.Width,
            Height = map.Height,
            TileRows = map.TileRows.ToImmutableArray(),
            Spawns = map.Spawns
                .Select((spawn, teamId) => new PublicMapSpawn(
                    teamId,
                    new Position(spawn.X, spawn.Y),
                    spawn.Facing))
                .OrderBy(spawn => spawn.TeamId)
                .ToImmutableArray(),
            ObjectiveTiles = map.EffectiveZone().ToImmutableArray(),
        };

        return manifest with
        {
            MapFingerprint = MatchContractFingerprint.ComputeMap(manifest),
        };
    }

    public static PublicMatchContractManifest CreateMatchContract(GameRules rules, ArenaMap map)
    {
        PublicRulesManifest rulesManifest = CreateRules(rules);
        PublicMapManifest mapManifest = CreateMap(map);
        PublicMatchTopology topology = CreateCurrentDuelTopology(rulesManifest);
        return CreateMatchContract(rulesManifest, mapManifest, topology);
    }

    public static PublicMatchContractManifest CreateMatchContract(
        GameRules rules,
        ArenaMap map,
        PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        return CreateMatchContract(CreateRules(rules), CreateMap(map), topology);
    }

    private static PublicMatchContractManifest CreateMatchContract(
        PublicRulesManifest rules,
        PublicMapManifest map,
        PublicMatchTopology topology)
    {
        var manifest = new PublicMatchContractManifest
        {
            SchemaVersion = BotArenaVersions.PublicManifestSchemaVersion,
            MatchContractFingerprint = "",
            Rules = rules,
            Map = map,
            Topology = topology,
        };
        return manifest with
        {
            MatchContractFingerprint = MatchContractFingerprint.ComputeMatch(manifest),
        };
    }

    private static PublicMatchTopology CreateCurrentDuelTopology(PublicRulesManifest rules)
    {
        ImmutableArray<PublicScoringTeam> teams = Enumerable
            .Range(0, rules.Limits.TeamCount)
            .Select(teamId => new PublicScoringTeam(teamId))
            .ToImmutableArray();

        return new PublicMatchTopology
        {
            Teams = teams,
            Participants = teams
                .Select(team => new PublicParticipant(team.TeamId, team.TeamId))
                .ToImmutableArray(),
            UnitSlots = teams
                .Select(team => new PublicUnitSlot(
                    team.TeamId,
                    UnitId: 0,
                    ControllerParticipantId: team.TeamId))
                .ToImmutableArray(),
            InitialLives = teams
                .Select(team => new PublicInitialLife(
                    team.TeamId,
                    UnitId: 0,
                    LifeId: 0,
                    MobileFormId))
                .ToImmutableArray(),
        };
    }
}
