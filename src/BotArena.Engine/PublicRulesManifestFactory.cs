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
        var actionBuilder = new List<PublicActionDefinition>
        {
            new(PublicActionIds.Wait, (int)BotAction.Wait, PublicActionKind.Wait,
                [], true),
            new(PublicActionIds.MoveForward, (int)BotAction.MoveForward, PublicActionKind.Movement,
                [], true),
            new(PublicActionIds.TurnLeft, (int)BotAction.TurnLeft, PublicActionKind.Rotation,
                [], true),
            new(PublicActionIds.TurnRight, (int)BotAction.TurnRight, PublicActionKind.Rotation,
                [], true),
            new(PublicActionIds.Shoot, (int)BotAction.Shoot, PublicActionKind.Attack,
                shotProgramsEnabled
                    ? [PublicActionParameterKind.ShotProgram]
                    : [],
                true),
            new(PublicActionIds.StrafeLeft, (int)BotAction.StrafeLeft, PublicActionKind.Movement,
                [], rules.AllowStrafe),
            new(PublicActionIds.StrafeRight, (int)BotAction.StrafeRight, PublicActionKind.Movement,
                [], rules.AllowStrafe),
        };
        bool fabricationEnabled = frontline is not null
            && frontline.MaxUnitsPerTeam > frontline.InitialUnitsPerTeam;
        if (fabricationEnabled)
        {
            actionBuilder.Add(new PublicActionDefinition(
                PublicActionIds.Fabricate,
                PublicActionCodes.Fabricate,
                PublicActionKind.Fabrication,
                [PublicActionParameterKind.UnitTarget],
                Enabled: true));
        }
        if (frontline is not null)
        {
            actionBuilder.Add(new PublicActionDefinition(
                PublicActionIds.Transform,
                PublicActionCodes.Transform,
                PublicActionKind.Transformation,
                [PublicActionParameterKind.FormTarget],
                Enabled: true));
            actionBuilder.Add(new PublicActionDefinition(
                PublicActionIds.ShootDirection,
                PublicActionCodes.ShootDirection,
                PublicActionKind.Attack,
                [PublicActionParameterKind.ProjectileHeading],
                Enabled: true));
        }
        ImmutableArray<PublicActionDefinition> actions = actionBuilder
            .OrderBy(action => action.Code)
            .ToImmutableArray();

        ImmutableArray<PublicFormDefinition> forms =
            CreateFormDefinitions(rules, frontline, actions, shotProgramsEnabled);

        var manifest = new PublicRulesManifest
        {
            SchemaVersion = BotArenaVersions.PublicRulesManifestSchemaVersion,
            RulesetId = rules.RulesVersion,
            RulesFingerprint = "",
            Limits = new PublicMatchLimits(
                rules.MaxTicks,
                FaultLimit: frontline is null ? rules.FaultLimit : 0,
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
                    ? [PublicScoreMetric.Objective]
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
            Forms = forms,
            Actions = actions,
            Projectiles = new PublicProjectileRules(
                rules.ProjectileTicksPerTile > 0
                    ? PublicProjectileMode.Discrete
                    : PublicProjectileMode.InstantRay,
                rules.DamagePerHit,
                rules.ShotRange,
                frontline?.PrimeForm?.ShootCooldownTicks
                    ?? rules.ShootCooldownTicks,
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
                    ? frontline is null
                        ? PublicActionRejectionResult.Faulted
                        : PublicActionRejectionResult.Rejected
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
                CreateTickResolutionPhases(frontline is not null)),
        };

        return manifest with
        {
            RulesFingerprint = MatchContractFingerprint.ComputeRules(manifest, rules),
        };
    }

    private static ImmutableArray<PublicTickResolutionPhase>
        CreateTickResolutionPhases(bool frontline) =>
        frontline
            ?
            [
                PublicTickResolutionPhase.ApplyTickStartLifecycle,
                PublicTickResolutionPhase.FreezeObservations,
                PublicTickResolutionPhase.CollectJointDecisions,
                PublicTickResolutionPhase.ValidateActions,
                PublicTickResolutionPhase.Rotate,
                PublicTickResolutionPhase.Move,
                PublicTickResolutionPhase.QueueFabrications,
                PublicTickResolutionPhase.StartFormTransitions,
                PublicTickResolutionPhase.AdvanceExistingProjectiles,
                PublicTickResolutionPhase.LaunchShotsAndApplyDamage,
                PublicTickResolutionPhase.QueueDestroyedLives,
                PublicTickResolutionPhase.UpdateCooldownsAndEnergy,
                PublicTickResolutionPhase.UpdateObjective,
                PublicTickResolutionPhase.CompleteFormTransitions,
                PublicTickResolutionPhase.ResolveMatchCompletion,
            ]
            :
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
            ];

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
            SchemaVersion = BotArenaVersions.PublicMapManifestSchemaVersion,
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
            SchemaVersion = BotArenaVersions.PublicMatchContractSchemaVersion,
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
            TeamPerceptionMode.ImmediateUnion,
            new PublicFrontlineCaptureDefinition(
                rules.CaptureThreshold,
                rules.CaptureGainPerSoleTeamTick,
                rules.CaptureDecayAmount,
                rules.CaptureDecayIntervalTicks,
                rules.RedeployPauseTicks,
                rules.PushesToBreach,
                PublicFrontlineCapturePresencePolicy
                    .BinaryPositiveWeightPerTeamNoStacking,
                PublicFrontlineNonSolePresencePolicy.DecayExistingClaim,
                PublicFrontlineCounterCapturePolicy
                    .ErodeToNeutralBeforeClaim),
            new PublicFrontlineVictoryDefinition(
                PublicFrontlineInitialPositionPolicy.CentrePositionIndex,
                [
                    new PublicFrontlineTeamAdvance(
                        TeamId: 0,
                        PositionIndexDelta: 1),
                    new PublicFrontlineTeamAdvance(
                        TeamId: 1,
                        PositionIndexDelta: -1),
                ],
                PublicFrontlineCompletionPrecedence
                    .BaseBreachBeforeMaxTicks,
                PublicFrontlineTimeoutResolution
                    .SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers),
            new PublicFrontlineLifecycleDefinition(
                rules.PrimeRespawnTicks,
                rules.ChildRebuildTicks,
                rules.FabricationUnlockTicks),
            new PublicFrontlineDeploymentDefinition(
                rules.PrimeForm.FormId,
                rules.ChildForm.FormId,
                PublicFrontlineDestructionTransitionClock
                    .TickStartAtDestroyedTickPlusOnePlusDelay,
                PublicFrontlinePrimeReturnPolicy
                    .AutomaticAtAuthoredPrimeSpawn,
                PublicFrontlineChildReturnPolicy
                    .ReadyThenExplicitFabrication,
                PublicFrontlineNewLifePolicy
                    .FreshRuntimeFormDefaultsHomeFacingCanActOnCreationTick,
                PublicFrontlinePrimeSpawnReservationPolicy
                    .PermanentAgainstOwnChildren,
                PublicFrontlineProtectedPadPolicy
                    .EnemyGroundEntryBlockedNoDamageImmunityNoProjectileBlocking),
            new PublicFrontlineFabricationDefinition(
                Enabled:
                    rules.MaxUnitsPerTeam > rules.InitialUnitsPerTeam,
                ActionId: PublicActionIds.Fabricate,
                FabricatorUnitId: 0,
                FabricatorFormId: rules.PrimeForm.FormId,
                TargetPolicy:
                    PublicFrontlineFabricationTargetPolicy.OwnReadyChildSlot,
                ActivationRegion:
                    PublicFrontlineFabricationActivationRegion
                        .OwnProtectedSpawnPad,
                ConsumesTick: true,
                SpawnDelayTicks: 1,
                CapacityEvaluation:
                    PublicFrontlineFabricationCapacityEvaluation
                        .PostMovementDuringQueueFabrications,
                SpawnRegion:
                    PublicFrontlineFabricationSpawnRegion
                        .OwnProtectedSpawnPadExcludingPrimeSpawn,
                SpawnSelection:
                    PublicFrontlineFabricationSpawnSelection
                        .FirstUnoccupiedUnreservedCanonicalYThenX,
                SpawnFacing:
                    PublicFrontlineFabricationSpawnFacing.OwnPrimeSpawnFacing,
                UnavailableSpawnResult:
                    PublicActionRejectionResult.Blocked,
                RequiresExplicitRefabricationAfterRebuild: true),
            new PublicFrontlineAnchorDefinition(
                rules.AnchorWindupTicks,
                rules.AnchorHealthGain,
                rules.AnchorIrreversibleForLife)
            {
                ActionId = PublicActionIds.Transform,
                SourceFormId = rules.ChildForm.FormId,
                TargetFormId = rules.TurretForm.FormId,
                ConsumesTick = true,
                Completion = PublicFrontlineAnchorCompletionPolicy
                    .EndOfStartedTickPlusWindupMinusOneAfterObjective,
                PendingActions =
                    PublicFrontlineAnchorPendingActionPolicy.WaitOnly,
                SurvivingDamage =
                    PublicFrontlineAnchorSurvivingDamagePolicy.DoesNotCancel,
                Death = PublicFrontlineAnchorDeathPolicy
                    .CancelsWithExplicitEvent,
                ForbiddenTiles = PublicFrontlineAnchorForbiddenTilePolicy
                    .AllMapAnchorForbiddenTilesIllegal,
                PendingForm = PublicFrontlineAnchorPendingFormPolicy
                    .SourceFormUntilCompletion,
                Health = PublicFrontlineAnchorHealthPolicy
                    .MinimumTargetMaximumAndCurrentPlusGain,
                StateContinuity = PublicFrontlineAnchorStateContinuityPolicy
                    .SameLifeRuntimeMemoryPositionFacingCooldownEnergyAndDamage,
                Terminal = PublicFrontlineAnchorTerminalPolicy
                    .PreserveFuturePendingWithoutSyntheticCancellation,
            },
            new PublicFrontlineAlliedCombatDefinition(
                rules.FriendlyFireEnabled,
                rules.AlliedProjectilesBlock,
                PublicFrontlineProjectileAttributionPolicy
                    .ExactFiringLifePersistsCreditsStableUnitByActualHealthRemoved))
        {
            TurretFire = new PublicFrontlineTurretFireDefinition(
                PublicActionIds.ShootDirection,
                rules.TurretForm.FormId,
                Enum.GetValues<ProjectileHeading>().ToImmutableArray(),
                PublicFrontlineTurretFireAimPolicy
                    .AbsoluteEightWayLaunchHeading,
                PublicFrontlineTurretFireProjectilePolicy
                    .OneStraightNonProgrammedProjectile,
                PublicFrontlineTurretFireFacingPolicy.BodyFacingUnchanged,
                PublicFrontlineTurretFireRangePolicy.GlobalProjectileRange,
                PublicFrontlineTurretFireResourcePolicy
                    .StandardEnergyCooldownAndDamage,
                PublicFrontlineTurretFireTraversalPolicy
                    .StandardTraversalStrictDiagonalCorners),
        };
    }

    private static ImmutableArray<PublicFormDefinition> CreateFormDefinitions(
        GameRules rules,
        FrontlineRules? frontline,
        ImmutableArray<PublicActionDefinition> actions,
        bool shotProgramsEnabled)
    {
        if (frontline is null)
        {
            return
            [
                CreateFormDefinition(
                    MobileFormId,
                    rules.MaxHealth,
                    rules.VisionRange,
                    rules.ShootCooldownTicks,
                    omnidirectionalVision: !rules.VisionCone,
                    omnidirectionalShooting: false,
                    objectiveWeight: 1,
                    canMove: true,
                    canRotate: true,
                    canShoot: true,
                    allowsProgrammedShots: shotProgramsEnabled,
                    allowsFabrication: false,
                    allowsTransformation: false,
                    allowsDirectionalShooting: false,
                    actions),
            ];
        }

        if (frontline.PrimeForm is null
            || frontline.ChildForm is null
            || frontline.TurretForm is null)
        {
            throw new ArgumentException(
                "Frontline Prime, child, and turret form definitions are required.",
                nameof(frontline));
        }

        UnitFormRules[] sourceForms =
        [
            frontline.PrimeForm,
            frontline.ChildForm,
            frontline.TurretForm,
        ];
        return sourceForms
            .Select(form => CreateFormDefinition(
                form.FormId,
                form.MaxHealth,
                form.VisionRange,
                form.ShootCooldownTicks,
                form.OmnidirectionalVision,
                form.OmnidirectionalShooting,
                form.ObjectiveWeight,
                form.CanMove,
                form.CanRotate,
                form.CanShoot,
                form.AllowsProgrammedShots,
                allowsFabrication: string.Equals(
                        form.FormId,
                        frontline.PrimeForm.FormId,
                        StringComparison.Ordinal)
                    && frontline.MaxUnitsPerTeam
                        > frontline.InitialUnitsPerTeam,
                allowsTransformation: string.Equals(
                    form.FormId,
                    frontline.ChildForm.FormId,
                    StringComparison.Ordinal),
                allowsDirectionalShooting: string.Equals(
                    form.FormId,
                    frontline.TurretForm.FormId,
                    StringComparison.Ordinal),
                actions))
            .OrderBy(form => form.Id, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static PublicFormDefinition CreateFormDefinition(
        string id,
        int maxHealth,
        int visionRange,
        int shootCooldownTicks,
        bool omnidirectionalVision,
        bool omnidirectionalShooting,
        int objectiveWeight,
        bool canMove,
        bool canRotate,
        bool canShoot,
        bool allowsProgrammedShots,
        bool allowsFabrication,
        bool allowsTransformation,
        bool allowsDirectionalShooting,
        ImmutableArray<PublicActionDefinition> actions) =>
        new(
            id,
            maxHealth,
            visionRange,
            shootCooldownTicks,
            omnidirectionalVision,
            omnidirectionalShooting,
            PublicMovementLayer.Ground,
            objectiveWeight,
            canMove,
            canShoot,
            allowsProgrammedShots,
            actions
                .Where(action =>
                    action.Enabled
                    && (action.Kind != PublicActionKind.Movement || canMove)
                    && (action.Kind != PublicActionKind.Rotation || canRotate)
                    && (!string.Equals(
                            action.Id,
                            PublicActionIds.Shoot,
                            StringComparison.Ordinal)
                        || (canShoot && !allowsDirectionalShooting))
                    && (!string.Equals(
                            action.Id,
                            PublicActionIds.ShootDirection,
                            StringComparison.Ordinal)
                        || (canShoot && allowsDirectionalShooting))
                    && (action.Kind != PublicActionKind.Fabrication
                        || allowsFabrication)
                    && (action.Kind != PublicActionKind.Transformation
                        || allowsTransformation))
                .Select(action => action.Id)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray());

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
