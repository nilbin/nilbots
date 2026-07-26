using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Projects authoritative engine configuration into immutable public contracts.</summary>
public static class PublicRulesManifestFactory
{
    private const string MobileFormId = "mobile";

    public static PublicRulesManifest CreateRules(GameRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        FrontlineRules? frontline = rules.Frontline;
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
                TeamCount: frontline?.TeamCount ?? 2,
                ParticipantCount: frontline is null
                    ? 2
                    : frontline.TeamCount * frontline.ParticipantsPerTeam,
                UnitSlotCount: frontline is null
                    ? 2
                    : frontline.TeamCount * frontline.MaxUnitsPerTeam,
                InitialUnitsPerTeam: frontline?.InitialUnitsPerTeam ?? 1,
                MaxUnitsPerTeam: frontline?.MaxUnitsPerTeam ?? 1,
                DestructionEndsMatch: frontline is null,
                RespawnsEnabled: frontline is not null),
            Objective = new PublicObjectiveRules(
                frontline is not null
                    ? PublicObjectiveMode.Frontline
                    : rules.ZoneControl
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
                frontline is not null
                    ? []
                    : rules.ZoneControl
                        ? [PublicScoreMetric.Objective, PublicScoreMetric.Health, PublicScoreMetric.DamageDealt]
                        : [PublicScoreMetric.Health, PublicScoreMetric.DamageDealt]),
            Frontline = CreateFrontlineDefinition(frontline),
            Energy = new PublicEnergyRules(
                rules.MaxEnergy > 0,
                rules.MaxEnergy,
                rules.ShotEnergyCost,
                rules.EnergyRegenTicks,
                RegenerationAmount: 1),
            Forms = frontline is null
                ? [
                    new PublicFormDefinition(
                        MobileFormId,
                        rules.MaxHealth,
                        PublicMovementLayer.Ground,
                        ObjectiveWeight: 1,
                        allowedActionIds),
                  ]
                : [],
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

        PublicFrontlineMapDefinition? frontline = CreateFrontlineMapDefinition(map);
        ImmutableArray<PublicMapSpawn> spawns = frontline is null
            ? map.Spawns
                .Select((spawn, teamId) => new PublicMapSpawn(
                    teamId,
                    new Position(spawn.X, spawn.Y),
                    spawn.Facing))
                .OrderBy(spawn => spawn.TeamId)
                .ToImmutableArray()
            : frontline.TeamHomes
                .Select(home => new PublicMapSpawn(
                    home.TeamId,
                    home.PrimeSpawnPosition,
                    home.PrimeSpawnFacing))
                .OrderBy(spawn => spawn.TeamId)
                .ToImmutableArray();

        var manifest = new PublicMapManifest
        {
            SchemaVersion = BotArenaVersions.PublicManifestSchemaVersion,
            MapId = map.Id,
            MapVersion = map.Version,
            MapFingerprint = "",
            FormatVersion = map.FormatVersion,
            Width = map.Width,
            Height = map.Height,
            TileRows = map.TileRows.ToImmutableArray(),
            Spawns = spawns,
            ObjectiveTiles = frontline is null
                ? map.EffectiveZone().ToImmutableArray()
                : [],
            Frontline = frontline,
        };

        return manifest with
        {
            MapFingerprint = MatchContractFingerprint.ComputeMap(manifest),
        };
    }

    public static PublicMatchContractManifest CreateMatchContract(GameRules rules, ArenaMap map)
    {
        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(rules, map);
        PublicRulesManifest rulesManifest = CreateRules(rules);
        PublicMapManifest mapManifest = CreateMap(map);
        return CreateMatchContract(rulesManifest, mapManifest, definition.Topology);
    }

    public static PublicMatchContractManifest CreateMatchContract(
        GameRules rules,
        ArenaMap map,
        PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ResolvedMatchDefinition definition =
            MatchDefinitionResolver.Resolve(rules, map, topology);
        return CreateMatchContract(
            CreateRules(rules),
            CreateMap(map),
            definition.Topology);
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

    private static PublicFrontlineDefinition? CreateFrontlineDefinition(
        FrontlineRules? rules)
    {
        if (rules is null)
            return null;
        if (rules.PrimeForm is null
            || rules.ChildForm is null
            || rules.TurretForm is null)
        {
            throw new ArgumentException(
                "Frontline Prime, child, and turret form definitions are required.",
                nameof(rules));
        }
        if (rules.FabricationUnlockTicks.IsDefault)
        {
            throw new ArgumentException(
                "Frontline fabrication unlock ticks must be initialized.",
                nameof(rules));
        }

        return new PublicFrontlineDefinition(
            rules.TeamCount,
            rules.ParticipantsPerTeam,
            rules.FrontlinePositionCount,
            rules.InitialUnitsPerTeam,
            rules.MaxUnitsPerTeam,
            new PublicFrontlineCaptureDefinition(
                rules.CaptureThreshold,
                rules.CaptureGainPerSoleTeamTick,
                rules.CaptureDecayAmount,
                rules.CaptureDecayIntervalTicks,
                rules.RedeployPauseTicks,
                rules.PushesToBreach),
            new PublicFrontlineLifecycleDefinition(
                rules.PrimeRespawnTicks,
                rules.ChildRebuildTicks,
                rules.FabricationUnlockTicks),
            new PublicFrontlineFormsDefinition(
                CreateFrontlineUnitFormDefinition(rules.PrimeForm),
                CreateFrontlineUnitFormDefinition(rules.ChildForm),
                CreateFrontlineUnitFormDefinition(rules.TurretForm)),
            new PublicFrontlineAnchorDefinition(
                rules.AnchorWindupTicks,
                rules.AnchorHealthGain,
                rules.AnchorIrreversibleForLife),
            new PublicFrontlineAlliedCombatDefinition(
                rules.FriendlyFireEnabled,
                rules.AlliedProjectilesBlock));
    }

    private static PublicFrontlineUnitFormDefinition CreateFrontlineUnitFormDefinition(
        UnitFormRules form) =>
        new(
            form.FormId,
            form.MaxHealth,
            form.VisionRange,
            form.ShootCooldownTicks,
            form.OmnidirectionalVision,
            form.OmnidirectionalShooting,
            form.ObjectiveWeight,
            form.CanMove,
            form.CanShoot,
            form.AllowsProgrammedShots);

    private static PublicFrontlineMapDefinition? CreateFrontlineMapDefinition(
        ArenaMap map)
    {
        if (map.FormatVersion == 1)
            return null;
        FrontlineMapProfile profile = map.Frontline
            ?? throw new ArgumentException(
                "A format-v2 map must include a Frontline profile.",
                nameof(map));

        return new PublicFrontlineMapDefinition(
            profile.Positions
                .Select(position => new PublicFrontlinePosition(
                    position.PositionIndex,
                    CanonicalizeTiles(position.Tiles)))
                .ToImmutableArray(),
            profile.TeamHomes
                .Select(home => new PublicFrontlineTeamHome(
                    home.TeamId,
                    new Position(home.PrimeSpawn.X, home.PrimeSpawn.Y),
                    home.PrimeSpawn.Facing,
                    CanonicalizeTiles(home.ProtectedSpawnPad)))
                .OrderBy(home => home.TeamId)
                .ToImmutableArray(),
            CanonicalizeTiles(profile.AnchorForbiddenTiles));
    }

    private static ImmutableArray<Position> CanonicalizeTiles(
        IEnumerable<Position> tiles) =>
        tiles
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToImmutableArray();
}
