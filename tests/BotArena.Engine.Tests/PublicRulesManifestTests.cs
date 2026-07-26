namespace BotArena.Engine.Tests;

public class PublicRulesManifestTests
{
    [Fact]
    public void CurrentRules_ProjectCompleteTypedBotContract()
    {
        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(GameRules.Current);

        Assert.Equal(BotArenaVersions.PublicManifestSchemaVersion, manifest.SchemaVersion);
        Assert.Equal(GameRules.Current.RulesVersion, manifest.RulesetId);
        Assert.Matches("^[0-9a-f]{64}$", manifest.RulesFingerprint);

        Assert.Equal(2, manifest.Limits.TeamCount);
        Assert.Equal(2, manifest.Limits.ParticipantCount);
        Assert.Equal(2, manifest.Limits.UnitSlotCount);
        Assert.Equal(GameRules.Current.FaultLimit, manifest.Limits.FaultLimit);
        Assert.Equal(1, manifest.Limits.InitialUnitsPerTeam);
        Assert.Equal(1, manifest.Limits.MaxUnitsPerTeam);
        Assert.True(manifest.Limits.DestructionEndsMatch);
        Assert.False(manifest.Limits.RespawnsEnabled);

        PublicFormDefinition form = Assert.Single(manifest.Forms);
        Assert.Equal("mobile", form.Id);
        Assert.Equal(GameRules.Current.MaxHealth, form.MaxHealth);
        Assert.Equal(PublicMovementLayer.Ground, form.MovementLayer);
        Assert.Equal(1, form.ObjectiveWeight);
        Assert.Equal(
            ["move-forward", "shoot", "turn-left", "turn-right", "wait"],
            form.AllowedActionIds.ToArray());

        Assert.Equal(
            Enum.GetValues<BotAction>().Select(action => (int)action),
            manifest.Actions.Select(action => action.Code));
        Assert.All(
            manifest.Actions.Where(action => action.Id.StartsWith("strafe-", StringComparison.Ordinal)),
            action => Assert.False(action.Enabled));
        Assert.Equal(
            PublicActionParameterKind.ShotProgram,
            manifest.Actions.Single(action => action.Id == "shoot").ParameterKind);

        Assert.Equal(PublicObjectiveMode.SharedPressure, manifest.Objective.Mode);
        Assert.True(manifest.Objective.ControlBySoleOccupancy);
        Assert.Equal(GameRules.Current.ControlPressureLimit, manifest.Objective.ControlPressureLimit);
        Assert.Equal(
            [PublicScoreMetric.Objective, PublicScoreMetric.Health, PublicScoreMetric.DamageDealt],
            manifest.Objective.MaxTickTiebreakers.ToArray());

        Assert.Equal(PublicProjectileMode.Discrete, manifest.Projectiles.Mode);
        Assert.Equal(GameRules.Current.ShotRange, manifest.Projectiles.MaxTravelTiles);
        Assert.Equal(
            GameRules.Current.ProjectileTilesPerAdvance,
            manifest.Projectiles.TilesPerAdvance);
        Assert.False(manifest.Projectiles.AdvancesOnLaunchTick);
        Assert.Equal(1, manifest.Projectiles.LaunchTiles);
        Assert.True(manifest.Projectiles.DamageAppliedSimultaneously);

        Assert.True(manifest.ShotPrograms.Enabled);
        Assert.Equal(8, manifest.ShotPrograms.HeadingSectors);
        Assert.Equal(1, manifest.ShotPrograms.BendStepOctants);
        Assert.Equal(
            -GameRules.Current.ProgrammedShotMaxInitialAimOctants,
            manifest.ShotPrograms.MinInitialAimOctants);
        Assert.Equal(
            new PublicAimOnlyShotProgramRules(
                BendDirection: 0,
                BendAfterTiles: 0,
                BendEveryTiles: 1,
                BendCount: 0),
            manifest.ShotPrograms.AimOnlyProgram);
        Assert.Equal([-1, 1], manifest.ShotPrograms.AllowedCurvedBendDirections.ToArray());
        Assert.Equal(1, manifest.ShotPrograms.MinBendAfterTiles);
        Assert.Equal(1, manifest.ShotPrograms.MinBendEveryTiles);
        Assert.Equal(1, manifest.ShotPrograms.MinBendCount);
        Assert.Equal(
            GameRules.Current.ProgrammedShotMaxBendCount,
            manifest.ShotPrograms.MaxBendCount);
        Assert.True(manifest.ShotPrograms.PayloadOptional);
        Assert.Equal(
            new PublicShotProgramValue(
                ShotProgram.Straight.InitialAimOffset,
                ShotProgram.Straight.BendDirection,
                ShotProgram.Straight.BendAfterTiles,
                ShotProgram.Straight.BendEveryTiles,
                ShotProgram.Straight.BendCount),
            manifest.ShotPrograms.DefaultProgram);
        Assert.Equal(
            PublicActionRejectionResult.Faulted,
            manifest.ShotPrograms.InvalidPayloadResult);
        Assert.Equal(
            PublicActionRejectionResult.Blocked,
            manifest.ShotPrograms.UnsupportedPayloadResult);
        Assert.True(manifest.ShotPrograms.DiagonalCornersMustBeClear);

        Assert.Equal(PublicVisionShape.FacingQuadrant, manifest.Vision.Shape);
        Assert.Equal(PublicDistanceMetric.Chebyshev, manifest.Vision.DistanceMetric);
        Assert.Equal(1, manifest.Vision.OmnidirectionalProximityRange);
        Assert.Equal(
            [Hearing.NearMax, Hearing.MediumMax],
            manifest.Vision.HearingDistanceBandUpperBounds.ToArray());
        Assert.Equal(
            [GameEventType.Shot, GameEventType.Damage, GameEventType.Destroyed],
            manifest.Vision.LoudEventTypes.ToArray());

        Assert.True(manifest.Collisions.SameDestinationMovesBlockAll);
        Assert.True(manifest.Collisions.SwapMovesBlocked);
        Assert.True(manifest.Collisions.FollowingVacatedUnitAllowed);
        Assert.True(manifest.Collisions.MovingOntoProjectileCausesHit);
        Assert.True(manifest.Collisions.ProjectilesIgnoreOwner);
        Assert.False(manifest.Collisions.ProjectilesCollideWithProjectiles);
        Assert.True(manifest.TickResolution.ObservationsUsePreTickState);
        Assert.True(manifest.TickResolution.DecisionsResolveAsJointStep);
        Assert.Equal(
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
            ],
            manifest.TickResolution.Phases.ToArray());
    }

    [Fact]
    public void LegacyRules_KeepDisabledCapabilitiesExplicit()
    {
        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(GameRules.V0_1);

        Assert.Equal(PublicObjectiveMode.None, manifest.Objective.Mode);
        Assert.False(manifest.Energy.Enabled);
        Assert.Equal(PublicProjectileMode.InstantRay, manifest.Projectiles.Mode);
        Assert.False(manifest.ShotPrograms.Enabled);
        Assert.Null(manifest.ShotPrograms.InvalidPayloadResult);
        Assert.Equal(
            PublicActionRejectionResult.Blocked,
            manifest.ShotPrograms.UnsupportedPayloadResult);
        Assert.Equal(
            [PublicScoreMetric.Health, PublicScoreMetric.DamageDealt],
            manifest.Objective.MaxTickTiebreakers.ToArray());
        Assert.Equal(
            PublicActionParameterKind.None,
            manifest.Actions.Single(action => action.Id == "shoot").ParameterKind);
    }

    [Fact]
    public void ShotPrograms_RequireBothTheCapabilityFlagAndDiscreteProjectiles()
    {
        GameRules inconsistent = GameRules.V0_1 with
        {
            AllowProgrammedShots = true,
            ProjectileTicksPerTile = 0,
        };

        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(inconsistent);

        Assert.False(manifest.ShotPrograms.Enabled);
        Assert.Null(manifest.ShotPrograms.InvalidPayloadResult);
        Assert.Equal(
            PublicActionRejectionResult.Blocked,
            manifest.ShotPrograms.UnsupportedPayloadResult);
        Assert.Equal(
            PublicActionParameterKind.None,
            manifest.Actions.Single(action => action.Id == "shoot").ParameterKind);
    }
}
