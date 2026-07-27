using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace BotArena.Engine;

/// <summary>
/// Canonical JSON for public match manifests. Every property and collection order is
/// written explicitly; fingerprints never depend on reflection or declaration order.
/// </summary>
public static class RulesManifestSerializer
{
    public static string ToCanonicalJson(PublicRulesManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Write(writer => WriteRules(writer, manifest, includeProvenance: true));
    }

    public static string ToCanonicalJson(PublicMapManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Write(writer => WriteMap(writer, manifest, includeProvenance: true));
    }

    public static string ToCanonicalJson(PublicMatchContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("matchContractFingerprint", manifest.MatchContractFingerprint);
            writer.WritePropertyName("rules");
            WriteRules(writer, manifest.Rules, includeProvenance: true);
            writer.WritePropertyName("map");
            WriteMap(writer, manifest.Map, includeProvenance: true);
            writer.WritePropertyName("topology");
            WriteTopology(writer, manifest.Topology);
            writer.WriteEndObject();
        });
    }

    internal static string SerializeRulesFingerprintPayload(
        PublicRulesManifest manifest,
        GameRules rules) =>
        Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WritePropertyName("publicGameplay");
            WriteRules(writer, manifest, includeProvenance: false);
            writer.WritePropertyName("seedMechanics");
            WriteSeedMechanics(writer, rules);
            writer.WriteEndObject();
        });

    internal static string SerializeMapFingerprintPayload(PublicMapManifest manifest) =>
        Write(writer => WriteMap(writer, manifest, includeProvenance: false));

    internal static string SerializeMatchFingerprintPayload(
        PublicMatchContractManifest manifest) =>
        Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteNumber("rulesSchemaVersion", manifest.Rules.SchemaVersion);
            writer.WriteNumber("mapSchemaVersion", manifest.Map.SchemaVersion);
            writer.WriteString("rulesetId", manifest.Rules.RulesetId);
            writer.WriteString("rulesFingerprint", manifest.Rules.RulesFingerprint);
            writer.WriteString("mapId", manifest.Map.MapId);
            writer.WriteNumber("mapVersion", manifest.Map.MapVersion);
            writer.WriteString("mapFingerprint", manifest.Map.MapFingerprint);
            writer.WritePropertyName("topology");
            WriteTopology(writer, manifest.Topology);
            writer.WriteEndObject();
        });

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false,
        }))
        {
            write(writer);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRules(
        Utf8JsonWriter writer,
        PublicRulesManifest manifest,
        bool includeProvenance)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
        if (includeProvenance)
        {
            writer.WriteString("rulesetId", manifest.RulesetId);
            writer.WriteString("rulesFingerprint", manifest.RulesFingerprint);
        }

        writer.WritePropertyName("limits");
        writer.WriteStartObject();
        writer.WriteNumber("maxTicks", manifest.Limits.MaxTicks);
        writer.WriteNumber("faultLimit", manifest.Limits.FaultLimit);
        writer.WriteNumber("teamCount", manifest.Limits.TeamCount);
        writer.WriteNumber("participantCount", manifest.Limits.ParticipantCount);
        writer.WriteNumber("unitSlotCount", manifest.Limits.UnitSlotCount);
        writer.WriteNumber("initialUnitsPerTeam", manifest.Limits.InitialUnitsPerTeam);
        writer.WriteNumber("maxUnitsPerTeam", manifest.Limits.MaxUnitsPerTeam);
        writer.WriteBoolean("destructionEndsMatch", manifest.Limits.DestructionEndsMatch);
        writer.WriteBoolean("respawnsEnabled", manifest.Limits.RespawnsEnabled);
        writer.WriteEndObject();

        writer.WritePropertyName("objective");
        writer.WriteStartObject();
        writer.WriteString("mode", ObjectiveModeId(manifest.Objective.Mode));
        writer.WriteBoolean("zoneControlEnabled", manifest.Objective.ZoneControlEnabled);
        writer.WriteNumber("zoneDominationTicks", manifest.Objective.ZoneDominationTicks);
        writer.WriteBoolean("zoneExclusiveAccrual", manifest.Objective.ZoneExclusiveAccrual);
        writer.WriteBoolean("sharedPressureEnabled", manifest.Objective.SharedPressureEnabled);
        writer.WriteBoolean("controlBySoleOccupancy", manifest.Objective.ControlBySoleOccupancy);
        writer.WriteNumber("controlPressureLimit", manifest.Objective.ControlPressureLimit);
        writer.WriteNumber("controlPressureGain", manifest.Objective.ControlPressureGain);
        writer.WriteNumber(
            "controlPressureDecayInterval",
            manifest.Objective.ControlPressureDecayInterval);
        writer.WritePropertyName("overtime");
        writer.WriteStartObject();
        writer.WriteNumber("startTick", manifest.Objective.Overtime.StartTick);
        writer.WriteNumber("pressureLimit", manifest.Objective.Overtime.PressureLimit);
        writer.WriteNumber("pressureGain", manifest.Objective.Overtime.PressureGain);
        writer.WriteBoolean("stopsDecay", manifest.Objective.Overtime.StopsDecay);
        writer.WriteEndObject();
        writer.WritePropertyName("maxTickTiebreakers");
        writer.WriteStartArray();
        foreach (PublicScoreMetric metric in manifest.Objective.MaxTickTiebreakers)
            writer.WriteStringValue(ScoreMetricId(metric));
        writer.WriteEndArray();
        writer.WriteEndObject();

        if (manifest.Frontline is { } frontline)
        {
            writer.WritePropertyName("frontlineDefinition");
            WriteFrontlineDefinition(writer, frontline, manifest.Forms);
        }

        writer.WritePropertyName("energy");
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", manifest.Energy.Enabled);
        writer.WriteNumber("maxEnergy", manifest.Energy.MaxEnergy);
        writer.WriteNumber("shotEnergyCost", manifest.Energy.ShotEnergyCost);
        writer.WriteNumber(
            "regenerationIntervalTicks",
            manifest.Energy.RegenerationIntervalTicks);
        writer.WriteNumber("regenerationAmount", manifest.Energy.RegenerationAmount);
        writer.WriteEndObject();

        writer.WritePropertyName("forms");
        writer.WriteStartArray();
        foreach (PublicFormDefinition form in manifest.Forms.OrderBy(form => form.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", form.Id);
            writer.WriteNumber("maxHealth", form.MaxHealth);
            writer.WriteNumber("visionRange", form.VisionRange);
            writer.WriteNumber("shootCooldownTicks", form.ShootCooldownTicks);
            writer.WriteBoolean(
                "omnidirectionalVision",
                form.OmnidirectionalVision);
            writer.WriteBoolean(
                "omnidirectionalShooting",
                form.OmnidirectionalShooting);
            writer.WriteString("movementLayer", MovementLayerId(form.MovementLayer));
            writer.WriteNumber("objectiveWeight", form.ObjectiveWeight);
            writer.WriteBoolean("canMove", form.CanMove);
            writer.WriteBoolean("canShoot", form.CanShoot);
            writer.WriteBoolean(
                "allowsProgrammedShots",
                form.AllowsProgrammedShots);
            writer.WritePropertyName("allowedActionIds");
            writer.WriteStartArray();
            foreach (string actionId in form.AllowedActionIds.Order(StringComparer.Ordinal))
                writer.WriteStringValue(actionId);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("actions");
        writer.WriteStartArray();
        foreach (PublicActionDefinition action in manifest.Actions.OrderBy(action => action.Code))
        {
            writer.WriteStartObject();
            writer.WriteString("id", action.Id);
            writer.WriteNumber("code", action.Code);
            writer.WriteString("kind", ActionKindId(action.Kind));
            writer.WritePropertyName("parameterKinds");
            WriteActionParameterKinds(writer, action.ParameterKinds);
            writer.WriteBoolean("enabled", action.Enabled);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("projectiles");
        writer.WriteStartObject();
        writer.WriteString("mode", ProjectileModeId(manifest.Projectiles.Mode));
        writer.WriteNumber("damagePerHit", manifest.Projectiles.DamagePerHit);
        writer.WriteNumber("maxTravelTiles", manifest.Projectiles.MaxTravelTiles);
        writer.WriteNumber("shootCooldownTicks", manifest.Projectiles.ShootCooldownTicks);
        writer.WriteNumber("ticksPerAdvance", manifest.Projectiles.TicksPerAdvance);
        writer.WriteNumber("tilesPerAdvance", manifest.Projectiles.TilesPerAdvance);
        writer.WriteNumber("launchTiles", manifest.Projectiles.LaunchTiles);
        writer.WriteBoolean("advancesOnLaunchTick", manifest.Projectiles.AdvancesOnLaunchTick);
        writer.WriteBoolean(
            "damageAppliedSimultaneously",
            manifest.Projectiles.DamageAppliedSimultaneously);
        writer.WriteEndObject();

        writer.WritePropertyName("shotPrograms");
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", manifest.ShotPrograms.Enabled);
        writer.WriteNumber("headingSectors", manifest.ShotPrograms.HeadingSectors);
        writer.WriteNumber("bendStepOctants", manifest.ShotPrograms.BendStepOctants);
        writer.WriteNumber(
            "minInitialAimOctants",
            manifest.ShotPrograms.MinInitialAimOctants);
        writer.WriteNumber(
            "maxInitialAimOctants",
            manifest.ShotPrograms.MaxInitialAimOctants);
        writer.WritePropertyName("aimOnlyProgram");
        writer.WriteStartObject();
        writer.WriteNumber(
            "bendDirection",
            manifest.ShotPrograms.AimOnlyProgram.BendDirection);
        writer.WriteNumber(
            "bendAfterTiles",
            manifest.ShotPrograms.AimOnlyProgram.BendAfterTiles);
        writer.WriteNumber(
            "bendEveryTiles",
            manifest.ShotPrograms.AimOnlyProgram.BendEveryTiles);
        writer.WriteNumber(
            "bendCount",
            manifest.ShotPrograms.AimOnlyProgram.BendCount);
        writer.WriteEndObject();
        writer.WritePropertyName("allowedCurvedBendDirections");
        writer.WriteStartArray();
        foreach (int direction in manifest.ShotPrograms.AllowedCurvedBendDirections.Order())
            writer.WriteNumberValue(direction);
        writer.WriteEndArray();
        writer.WriteNumber("minBendAfterTiles", manifest.ShotPrograms.MinBendAfterTiles);
        writer.WriteNumber("maxBendAfterTiles", manifest.ShotPrograms.MaxBendAfterTiles);
        writer.WriteNumber("minBendEveryTiles", manifest.ShotPrograms.MinBendEveryTiles);
        writer.WriteNumber("maxBendEveryTiles", manifest.ShotPrograms.MaxBendEveryTiles);
        writer.WriteNumber("minBendCount", manifest.ShotPrograms.MinBendCount);
        writer.WriteNumber("maxBendCount", manifest.ShotPrograms.MaxBendCount);
        writer.WriteNumber("launchTiles", manifest.ShotPrograms.LaunchTiles);
        writer.WriteBoolean("payloadOptional", manifest.ShotPrograms.PayloadOptional);
        writer.WritePropertyName("defaultProgram");
        WriteShotProgram(writer, manifest.ShotPrograms.DefaultProgram);
        if (manifest.ShotPrograms.InvalidPayloadResult is { } invalidPayloadResult)
        {
            writer.WriteString(
                "invalidPayloadResult",
                ActionRejectionResultId(invalidPayloadResult));
        }
        else
        {
            writer.WriteNull("invalidPayloadResult");
        }
        writer.WriteString(
            "unsupportedPayloadResult",
            ActionRejectionResultId(manifest.ShotPrograms.UnsupportedPayloadResult));
        writer.WriteBoolean(
            "diagonalCornersMustBeClear",
            manifest.ShotPrograms.DiagonalCornersMustBeClear);
        writer.WriteEndObject();

        writer.WritePropertyName("vision");
        writer.WriteStartObject();
        writer.WriteNumber("range", manifest.Vision.Range);
        writer.WriteString("distanceMetric", DistanceMetricId(manifest.Vision.DistanceMetric));
        writer.WriteString("shape", VisionShapeId(manifest.Vision.Shape));
        writer.WriteNumber(
            "omnidirectionalProximityRange",
            manifest.Vision.OmnidirectionalProximityRange);
        writer.WriteString("lineOfSight", LineOfSightModelId(manifest.Vision.LineOfSight));
        writer.WriteNumber("hearingRadius", manifest.Vision.HearingRadius);
        writer.WriteNumber("hearingBearingSectors", manifest.Vision.HearingBearingSectors);
        writer.WritePropertyName("hearingDistanceBandUpperBounds");
        writer.WriteStartArray();
        foreach (int upperBound in manifest.Vision.HearingDistanceBandUpperBounds.Order())
            writer.WriteNumberValue(upperBound);
        writer.WriteEndArray();
        writer.WritePropertyName("loudEventTypes");
        writer.WriteStartArray();
        foreach (GameEventType eventType in manifest.Vision.LoudEventTypes.OrderBy(type => (int)type))
            writer.WriteStringValue(GameEventTypeId(eventType));
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("collisions");
        writer.WriteStartObject();
        writer.WriteBoolean("unitsBlockWalls", manifest.Collisions.UnitsBlockWalls);
        writer.WriteBoolean("unitsBlockUnits", manifest.Collisions.UnitsBlockUnits);
        writer.WriteBoolean(
            "sameDestinationMovesBlockAll",
            manifest.Collisions.SameDestinationMovesBlockAll);
        writer.WriteBoolean("swapMovesBlocked", manifest.Collisions.SwapMovesBlocked);
        writer.WriteBoolean(
            "followingVacatedUnitAllowed",
            manifest.Collisions.FollowingVacatedUnitAllowed);
        writer.WriteBoolean(
            "projectilesBlockMovement",
            manifest.Collisions.ProjectilesBlockMovement);
        writer.WriteBoolean(
            "movingOntoProjectileCausesHit",
            manifest.Collisions.MovingOntoProjectileCausesHit);
        writer.WriteBoolean(
            "wallsConsumeProjectiles",
            manifest.Collisions.WallsConsumeProjectiles);
        writer.WriteBoolean("projectilesIgnoreOwner", manifest.Collisions.ProjectilesIgnoreOwner);
        writer.WriteBoolean(
            "projectilesStopOnFirstNonOwnerUnit",
            manifest.Collisions.ProjectilesStopOnFirstNonOwnerUnit);
        writer.WriteBoolean(
            "projectilesCollideWithProjectiles",
            manifest.Collisions.ProjectilesCollideWithProjectiles);
        writer.WriteEndObject();

        writer.WritePropertyName("tickResolution");
        writer.WriteStartObject();
        writer.WriteBoolean(
            "observationsUsePreTickState",
            manifest.TickResolution.ObservationsUsePreTickState);
        writer.WriteBoolean(
            "decisionsResolveAsJointStep",
            manifest.TickResolution.DecisionsResolveAsJointStep);
        writer.WritePropertyName("phases");
        writer.WriteStartArray();
        foreach (PublicTickResolutionPhase phase in manifest.TickResolution.Phases)
            writer.WriteStringValue(TickResolutionPhaseId(phase));
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteSeedMechanics(Utf8JsonWriter writer, GameRules rules)
    {
        writer.WriteStartObject();
        writer.WriteString("seedDerivation", "splitmix64-v1");
        writer.WriteString("seedNamespace", rules.SeedProfile ?? rules.RulesVersion);
        writer.WriteBoolean("spawnVariationEnabled", rules.SeedSpawnVariation);
        if (rules.SeedSpawnVariation)
        {
            writer.WriteString(
                "spawnSelection",
                rules.ExhaustiveSpawns ? "exhaustive-v1" : "sampled-v1");
            writer.WriteBoolean("laneSafety", rules.SpawnLaneSafety);
            writer.WriteBoolean("zoneFairness", rules.ZoneControl && rules.ZoneSpawnFairness);
            writer.WriteNumber("zoneDistanceTolerance", SpawnVariation.ZoneDistanceTolerance);
            if (!rules.ExhaustiveSpawns)
                writer.WriteNumber("spawnAttempts", rules.SpawnAttempts);
        }
        writer.WriteEndObject();
    }

    private static void WriteMap(
        Utf8JsonWriter writer,
        PublicMapManifest manifest,
        bool includeProvenance)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
        if (includeProvenance)
        {
            writer.WriteString("mapId", manifest.MapId);
            writer.WriteNumber("mapVersion", manifest.MapVersion);
            writer.WriteString("mapFingerprint", manifest.MapFingerprint);
        }
        writer.WriteNumber("formatVersion", manifest.FormatVersion);
        writer.WriteNumber("width", manifest.Width);
        writer.WriteNumber("height", manifest.Height);
        writer.WritePropertyName("tileRows");
        writer.WriteStartArray();
        foreach (string row in manifest.TileRows)
            writer.WriteStringValue(row);
        writer.WriteEndArray();
        writer.WritePropertyName("spawns");
        writer.WriteStartArray();
        foreach (PublicMapSpawn spawn in manifest.Spawns.OrderBy(spawn => spawn.TeamId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", spawn.TeamId);
            writer.WriteNumber("x", spawn.Position.X);
            writer.WriteNumber("y", spawn.Position.Y);
            writer.WriteString("facing", DirectionId(spawn.Facing));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("objectiveTiles");
        writer.WriteStartArray();
        foreach (Position tile in manifest.ObjectiveTiles)
            WritePosition(writer, tile);
        writer.WriteEndArray();
        if (manifest.Frontline is { } frontline)
        {
            writer.WritePropertyName("frontline");
            WriteFrontlineMapDefinition(writer, frontline);
        }
        writer.WriteEndObject();
    }

    private static void WriteFrontlineDefinition(
        Utf8JsonWriter writer,
        PublicFrontlineDefinition frontline,
        ImmutableArray<PublicFormDefinition> forms)
    {
        if (forms.IsDefault
            || string.IsNullOrWhiteSpace(
                frontline.Deployment.PrimeDefaultFormId)
            || string.IsNullOrWhiteSpace(
                frontline.Deployment.ChildDefaultFormId)
            || string.Equals(
                frontline.Deployment.PrimeDefaultFormId,
                frontline.Deployment.ChildDefaultFormId,
                StringComparison.Ordinal)
            || !forms.Any(form => string.Equals(
                form.Id,
                frontline.Deployment.PrimeDefaultFormId,
                StringComparison.Ordinal))
            || !forms.Any(form => string.Equals(
                form.Id,
                frontline.Deployment.ChildDefaultFormId,
                StringComparison.Ordinal))
            || !string.Equals(
                frontline.Fabrication.FabricatorFormId,
                frontline.Deployment.PrimeDefaultFormId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Frontline deployment default and fabricator form IDs must reference distinct matching catalog forms.",
                nameof(frontline));
        }

        writer.WriteStartObject();
        writer.WriteNumber("teamCount", frontline.TeamCount);
        writer.WriteNumber("participantsPerTeam", frontline.ParticipantsPerTeam);
        writer.WriteNumber("frontlinePositionCount", frontline.FrontlinePositionCount);
        writer.WriteNumber("initialUnitsPerTeam", frontline.InitialUnitsPerTeam);
        writer.WriteNumber("maxUnitsPerTeam", frontline.MaxUnitsPerTeam);
        writer.WriteString(
            "teamPerception",
            TeamPerceptionModeId(frontline.TeamPerception));

        writer.WritePropertyName("capture");
        writer.WriteStartObject();
        writer.WriteNumber("threshold", frontline.Capture.Threshold);
        writer.WriteNumber(
            "gainPerSoleTeamTick",
            frontline.Capture.GainPerSoleTeamTick);
        writer.WriteNumber("decayAmount", frontline.Capture.DecayAmount);
        writer.WriteNumber(
            "decayIntervalTicks",
            frontline.Capture.DecayIntervalTicks);
        writer.WriteNumber(
            "redeployPauseTicks",
            frontline.Capture.RedeployPauseTicks);
        writer.WriteNumber("pushesToBreach", frontline.Capture.PushesToBreach);
        writer.WriteString(
            "presence",
            FrontlineCapturePresencePolicyId(frontline.Capture.Presence));
        writer.WriteString(
            "nonSolePresence",
            FrontlineNonSolePresencePolicyId(
                frontline.Capture.NonSolePresence));
        writer.WriteString(
            "counterCapture",
            FrontlineCounterCapturePolicyId(
                frontline.Capture.CounterCapture));
        writer.WriteEndObject();

        writer.WritePropertyName("victory");
        writer.WriteStartObject();
        writer.WriteString(
            "initialPosition",
            FrontlineInitialPositionPolicyId(
                frontline.Victory.InitialPosition));
        writer.WritePropertyName("teamAdvances");
        WriteFrontlineTeamAdvances(
            writer,
            frontline.Victory.TeamAdvances);
        writer.WriteString(
            "completionPrecedence",
            FrontlineCompletionPrecedenceId(
                frontline.Victory.CompletionPrecedence));
        writer.WriteString(
            "timeoutResolution",
            FrontlineTimeoutResolutionId(
                frontline.Victory.TimeoutResolution));
        writer.WriteEndObject();

        writer.WritePropertyName("lifecycle");
        writer.WriteStartObject();
        writer.WriteNumber(
            "primeRespawnTicks",
            frontline.Lifecycle.PrimeRespawnTicks);
        writer.WriteNumber(
            "childRebuildTicks",
            frontline.Lifecycle.ChildRebuildTicks);
        writer.WritePropertyName("fabricationUnlockTicks");
        writer.WriteStartArray();
        foreach (int tick in frontline.Lifecycle.FabricationUnlockTicks)
            writer.WriteNumberValue(tick);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("deployment");
        writer.WriteStartObject();
        writer.WriteString(
            "primeDefaultFormId",
            frontline.Deployment.PrimeDefaultFormId);
        writer.WriteString(
            "childDefaultFormId",
            frontline.Deployment.ChildDefaultFormId);
        writer.WriteString(
            "destructionTransitionClock",
            FrontlineDestructionTransitionClockId(
                frontline.Deployment.DestructionTransitionClock));
        writer.WriteString(
            "primeReturn",
            FrontlinePrimeReturnPolicyId(
                frontline.Deployment.PrimeReturn));
        writer.WriteString(
            "childReturn",
            FrontlineChildReturnPolicyId(
                frontline.Deployment.ChildReturn));
        writer.WriteString(
            "newLife",
            FrontlineNewLifePolicyId(frontline.Deployment.NewLife));
        writer.WriteString(
            "primeSpawnReservation",
            FrontlinePrimeSpawnReservationPolicyId(
                frontline.Deployment.PrimeSpawnReservation));
        writer.WriteString(
            "protectedPad",
            FrontlineProtectedPadPolicyId(
                frontline.Deployment.ProtectedPad));
        writer.WriteEndObject();

        writer.WritePropertyName("fabrication");
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", frontline.Fabrication.Enabled);
        writer.WriteString("actionId", frontline.Fabrication.ActionId);
        writer.WriteNumber(
            "fabricatorUnitId",
            frontline.Fabrication.FabricatorUnitId);
        writer.WriteString(
            "fabricatorFormId",
            frontline.Fabrication.FabricatorFormId);
        writer.WriteString(
            "targetPolicy",
            FrontlineFabricationTargetPolicyId(
                frontline.Fabrication.TargetPolicy));
        writer.WriteString(
            "activationRegion",
            FrontlineFabricationActivationRegionId(
                frontline.Fabrication.ActivationRegion));
        writer.WriteBoolean("consumesTick", frontline.Fabrication.ConsumesTick);
        writer.WriteNumber(
            "spawnDelayTicks",
            frontline.Fabrication.SpawnDelayTicks);
        writer.WriteString(
            "capacityEvaluation",
            FrontlineFabricationCapacityEvaluationId(
                frontline.Fabrication.CapacityEvaluation));
        writer.WriteString(
            "spawnRegion",
            FrontlineFabricationSpawnRegionId(
                frontline.Fabrication.SpawnRegion));
        writer.WriteString(
            "spawnSelection",
            FrontlineFabricationSpawnSelectionId(
                frontline.Fabrication.SpawnSelection));
        writer.WriteString(
            "spawnFacing",
            FrontlineFabricationSpawnFacingId(
                frontline.Fabrication.SpawnFacing));
        writer.WriteString(
            "unavailableSpawnResult",
            ActionRejectionResultId(
                frontline.Fabrication.UnavailableSpawnResult));
        writer.WriteBoolean(
            "requiresExplicitRefabricationAfterRebuild",
            frontline.Fabrication
                .RequiresExplicitRefabricationAfterRebuild);
        writer.WriteEndObject();

        writer.WritePropertyName("anchor");
        writer.WriteStartObject();
        writer.WriteNumber("windupTicks", frontline.Anchor.WindupTicks);
        writer.WriteNumber("healthGain", frontline.Anchor.HealthGain);
        writer.WriteBoolean(
            "irreversibleForLife",
            frontline.Anchor.IrreversibleForLife);
        writer.WriteEndObject();

        writer.WritePropertyName("alliedCombat");
        writer.WriteStartObject();
        writer.WriteBoolean(
            "friendlyFireEnabled",
            frontline.AlliedCombat.FriendlyFireEnabled);
        writer.WriteBoolean(
            "alliedProjectilesBlock",
            frontline.AlliedCombat.AlliedProjectilesBlock);
        writer.WriteString(
            "projectileAttribution",
            FrontlineProjectileAttributionPolicyId(
                frontline.AlliedCombat.ProjectileAttribution));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteFrontlineTeamAdvances(
        Utf8JsonWriter writer,
        ImmutableArray<PublicFrontlineTeamAdvance> advances)
    {
        if (advances.IsDefault)
        {
            throw new ArgumentException(
                "Frontline team advances must be initialized.",
                nameof(advances));
        }

        PublicFrontlineTeamAdvance[] canonical = advances
            .OrderBy(value => value.TeamId)
            .ToArray();
        if (canonical.Length != 2
            || canonical[0].TeamId != 0
            || canonical[1].TeamId != 1
            || !canonical
                .Select(value => value.PositionIndexDelta)
                .Order()
                .SequenceEqual([-1, 1]))
        {
            throw new ArgumentException(
                "Frontline team advances must define teams 0 and 1 with unique -1 and +1 position deltas.",
                nameof(advances));
        }

        writer.WriteStartArray();
        int? previousTeamId = null;
        foreach (PublicFrontlineTeamAdvance advance in canonical)
        {
            if (previousTeamId == advance.TeamId)
            {
                throw new ArgumentException(
                    "Frontline team advances require unique team IDs.",
                    nameof(advances));
            }

            writer.WriteStartObject();
            writer.WriteNumber("teamId", advance.TeamId);
            writer.WriteNumber(
                "positionIndexDelta",
                advance.PositionIndexDelta);
            writer.WriteEndObject();
            previousTeamId = advance.TeamId;
        }
        writer.WriteEndArray();
    }

    private static void WriteFrontlineMapDefinition(
        Utf8JsonWriter writer,
        PublicFrontlineMapDefinition frontline)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("positions");
        writer.WriteStartArray();
        foreach (PublicFrontlinePosition position in frontline.Positions)
        {
            writer.WriteStartObject();
            writer.WriteNumber("positionIndex", position.PositionIndex);
            writer.WritePropertyName("tiles");
            WriteCanonicalTileSet(writer, position.Tiles);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("teamHomes");
        writer.WriteStartArray();
        foreach (PublicFrontlineTeamHome home in frontline.TeamHomes
                     .OrderBy(home => home.TeamId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", home.TeamId);
            writer.WritePropertyName("primeSpawn");
            writer.WriteStartObject();
            writer.WriteNumber("x", home.PrimeSpawnPosition.X);
            writer.WriteNumber("y", home.PrimeSpawnPosition.Y);
            writer.WriteString("facing", DirectionId(home.PrimeSpawnFacing));
            writer.WriteEndObject();
            writer.WritePropertyName("protectedSpawnPad");
            WriteCanonicalTileSet(writer, home.ProtectedSpawnPad);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("anchorForbiddenTiles");
        WriteCanonicalTileSet(writer, frontline.AnchorForbiddenTiles);
        writer.WriteEndObject();
    }

    private static void WriteCanonicalTileSet(
        Utf8JsonWriter writer,
        IEnumerable<Position> tiles)
    {
        writer.WriteStartArray();
        foreach (Position tile in tiles
                     .OrderBy(tile => tile.Y)
                     .ThenBy(tile => tile.X))
        {
            WritePosition(writer, tile);
        }
        writer.WriteEndArray();
    }

    private static void WritePosition(Utf8JsonWriter writer, Position tile)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(tile.X);
        writer.WriteNumberValue(tile.Y);
        writer.WriteEndArray();
    }

    private static void WriteTopology(
        Utf8JsonWriter writer,
        PublicMatchTopology topology)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamCount", topology.Teams.Length);
        writer.WriteNumber("participantCount", topology.Participants.Length);
        writer.WriteNumber("unitSlotCount", topology.UnitSlots.Length);
        writer.WriteNumber("initialLifeCount", topology.InitialLives.Length);

        writer.WritePropertyName("teams");
        writer.WriteStartArray();
        foreach (PublicScoringTeam team in topology.Teams.OrderBy(team => team.TeamId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", team.TeamId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("participants");
        writer.WriteStartArray();
        foreach (PublicParticipant participant in topology.Participants
                     .OrderBy(participant => participant.ParticipantId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("participantId", participant.ParticipantId);
            writer.WriteNumber("teamId", participant.TeamId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("unitSlots");
        writer.WriteStartArray();
        foreach (PublicUnitSlot unit in topology.UnitSlots
                     .OrderBy(unit => unit.TeamId)
                     .ThenBy(unit => unit.UnitId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", unit.TeamId);
            writer.WriteNumber("unitId", unit.UnitId);
            writer.WriteNumber(
                "controllerParticipantId",
                unit.ControllerParticipantId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("initialLives");
        writer.WriteStartArray();
        foreach (PublicInitialLife life in topology.InitialLives
                     .OrderBy(life => life.TeamId)
                     .ThenBy(life => life.UnitId)
                     .ThenBy(life => life.LifeId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", life.TeamId);
            writer.WriteNumber("unitId", life.UnitId);
            writer.WriteNumber("lifeId", life.LifeId);
            writer.WriteString("formId", life.FormId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteShotProgram(
        Utf8JsonWriter writer,
        PublicShotProgramValue program)
    {
        writer.WriteStartObject();
        writer.WriteNumber("initialAimOffset", program.InitialAimOffset);
        writer.WriteNumber("bendDirection", program.BendDirection);
        writer.WriteNumber("bendAfterTiles", program.BendAfterTiles);
        writer.WriteNumber("bendEveryTiles", program.BendEveryTiles);
        writer.WriteNumber("bendCount", program.BendCount);
        writer.WriteEndObject();
    }

    private static string ObjectiveModeId(PublicObjectiveMode mode) => mode switch
    {
        PublicObjectiveMode.None => "none",
        PublicObjectiveMode.ZoneTicks => "zone-ticks",
        PublicObjectiveMode.SharedPressure => "shared-pressure",
        PublicObjectiveMode.Frontline => "frontline",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static string ScoreMetricId(PublicScoreMetric metric) => metric switch
    {
        PublicScoreMetric.Objective => "objective",
        PublicScoreMetric.Health => "health",
        PublicScoreMetric.DamageDealt => "damage-dealt",
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    private static string MovementLayerId(PublicMovementLayer layer) => layer switch
    {
        PublicMovementLayer.Ground => "ground",
        _ => throw new ArgumentOutOfRangeException(nameof(layer)),
    };

    private static string ActionKindId(PublicActionKind kind) => kind switch
    {
        PublicActionKind.Wait => "wait",
        PublicActionKind.Movement => "movement",
        PublicActionKind.Rotation => "rotation",
        PublicActionKind.Attack => "attack",
        PublicActionKind.Fabrication => "fabrication",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void WriteActionParameterKinds(
        Utf8JsonWriter writer,
        ImmutableArray<PublicActionParameterKind> kinds)
    {
        if (kinds.IsDefault)
        {
            throw new ArgumentException(
                "Action parameter kinds must be initialized.",
                nameof(kinds));
        }

        writer.WriteStartArray();
        PublicActionParameterKind? previous = null;
        foreach (PublicActionParameterKind kind in kinds)
        {
            string kindId = ActionParameterKindId(kind);
            if (previous is { } previousKind
                && (int)kind <= (int)previousKind)
            {
                throw new ArgumentException(
                    "Action parameter kinds must be unique and in canonical enum order.",
                    nameof(kinds));
            }

            writer.WriteStringValue(kindId);
            previous = kind;
        }
        writer.WriteEndArray();
    }

    private static string ActionParameterKindId(PublicActionParameterKind kind) => kind switch
    {
        PublicActionParameterKind.ShotProgram => "shot-program",
        PublicActionParameterKind.Direction => "direction",
        PublicActionParameterKind.UnitTarget => "unit-target",
        PublicActionParameterKind.FormTarget => "form-target",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ActionRejectionResultId(PublicActionRejectionResult result) =>
        result switch
        {
            PublicActionRejectionResult.Blocked => "blocked",
            PublicActionRejectionResult.Faulted => "faulted",
            PublicActionRejectionResult.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

    private static string ProjectileModeId(PublicProjectileMode mode) => mode switch
    {
        PublicProjectileMode.InstantRay => "instant-ray",
        PublicProjectileMode.Discrete => "discrete",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static string DistanceMetricId(PublicDistanceMetric metric) => metric switch
    {
        PublicDistanceMetric.Chebyshev => "chebyshev",
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    private static string VisionShapeId(PublicVisionShape shape) => shape switch
    {
        PublicVisionShape.Omnidirectional => "omnidirectional",
        PublicVisionShape.FacingQuadrant => "facing-quadrant",
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static string LineOfSightModelId(PublicLineOfSightModel model) => model switch
    {
        PublicLineOfSightModel.CornerStrictSupercover => "corner-strict-supercover",
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };

    private static string DirectionId(Direction direction) => direction switch
    {
        Direction.North => "north",
        Direction.East => "east",
        Direction.South => "south",
        Direction.West => "west",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    private static string GameEventTypeId(GameEventType eventType) => eventType switch
    {
        GameEventType.Turn => "turn",
        GameEventType.Move => "move",
        GameEventType.MoveBlocked => "move-blocked",
        GameEventType.Shot => "shot",
        GameEventType.Damage => "damage",
        GameEventType.Destroyed => "destroyed",
        GameEventType.Fault => "fault",
        GameEventType.Disqualified => "disqualified",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
    };

    private static string TickResolutionPhaseId(PublicTickResolutionPhase phase) => phase switch
    {
        PublicTickResolutionPhase.FreezeObservations => "freeze-observations",
        PublicTickResolutionPhase.CollectJointDecisions => "collect-joint-decisions",
        PublicTickResolutionPhase.ValidateActions => "validate-actions",
        PublicTickResolutionPhase.Rotate => "rotate",
        PublicTickResolutionPhase.Move => "move",
        PublicTickResolutionPhase.AdvanceExistingProjectiles => "advance-existing-projectiles",
        PublicTickResolutionPhase.LaunchShotsAndApplyDamage => "launch-shots-and-apply-damage",
        PublicTickResolutionPhase.UpdateCooldownsAndEnergy => "update-cooldowns-and-energy",
        PublicTickResolutionPhase.ApplyRuntimeFaults => "apply-runtime-faults",
        PublicTickResolutionPhase.UpdateObjective => "update-objective",
        PublicTickResolutionPhase.ResolveMatchCompletion => "resolve-match-completion",
        PublicTickResolutionPhase.ApplyTickStartLifecycle =>
            "apply-tick-start-lifecycle",
        PublicTickResolutionPhase.QueueDestroyedLives =>
            "queue-destroyed-lives",
        PublicTickResolutionPhase.QueueFabrications =>
            "queue-fabrications",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static string TeamPerceptionModeId(TeamPerceptionMode mode) => mode switch
    {
        TeamPerceptionMode.Individual => "individual",
        TeamPerceptionMode.ImmediateUnion => "immediate-union",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static string FrontlineCapturePresencePolicyId(
        PublicFrontlineCapturePresencePolicy policy) =>
        policy switch
        {
            PublicFrontlineCapturePresencePolicy
                    .BinaryPositiveWeightPerTeamNoStacking =>
                "binary-positive-weight-per-team-no-stacking",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineNonSolePresencePolicyId(
        PublicFrontlineNonSolePresencePolicy policy) =>
        policy switch
        {
            PublicFrontlineNonSolePresencePolicy.DecayExistingClaim =>
                "decay-existing-claim",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineCounterCapturePolicyId(
        PublicFrontlineCounterCapturePolicy policy) =>
        policy switch
        {
            PublicFrontlineCounterCapturePolicy.ErodeToNeutralBeforeClaim =>
                "erode-to-neutral-before-claim",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineInitialPositionPolicyId(
        PublicFrontlineInitialPositionPolicy policy) =>
        policy switch
        {
            PublicFrontlineInitialPositionPolicy.CentrePositionIndex =>
                "centre-position-index",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineCompletionPrecedenceId(
        PublicFrontlineCompletionPrecedence precedence) =>
        precedence switch
        {
            PublicFrontlineCompletionPrecedence.BaseBreachBeforeMaxTicks =>
                "base-breach-before-max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(precedence)),
        };

    private static string FrontlineTimeoutResolutionId(
        PublicFrontlineTimeoutResolution resolution) =>
        resolution switch
        {
            PublicFrontlineTimeoutResolution
                    .SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers =>
                "signed-position-threshold-plus-claim-zero-draw-no-tiebreakers",
            _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
        };

    private static string FrontlineDestructionTransitionClockId(
        PublicFrontlineDestructionTransitionClock clock) =>
        clock switch
        {
            PublicFrontlineDestructionTransitionClock
                    .TickStartAtDestroyedTickPlusOnePlusDelay =>
                "tick-start-at-destroyed-tick-plus-one-plus-delay",
            _ => throw new ArgumentOutOfRangeException(nameof(clock)),
        };

    private static string FrontlinePrimeReturnPolicyId(
        PublicFrontlinePrimeReturnPolicy policy) =>
        policy switch
        {
            PublicFrontlinePrimeReturnPolicy.AutomaticAtAuthoredPrimeSpawn =>
                "automatic-at-authored-prime-spawn",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineChildReturnPolicyId(
        PublicFrontlineChildReturnPolicy policy) =>
        policy switch
        {
            PublicFrontlineChildReturnPolicy.ReadyThenExplicitFabrication =>
                "ready-then-explicit-fabrication",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineNewLifePolicyId(
        PublicFrontlineNewLifePolicy policy) =>
        policy switch
        {
            PublicFrontlineNewLifePolicy
                    .FreshRuntimeFormDefaultsHomeFacingCanActOnCreationTick =>
                "fresh-runtime-form-defaults-home-facing-can-act-on-creation-tick",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlinePrimeSpawnReservationPolicyId(
        PublicFrontlinePrimeSpawnReservationPolicy policy) =>
        policy switch
        {
            PublicFrontlinePrimeSpawnReservationPolicy
                    .PermanentAgainstOwnChildren =>
                "permanent-against-own-children",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineProtectedPadPolicyId(
        PublicFrontlineProtectedPadPolicy policy) =>
        policy switch
        {
            PublicFrontlineProtectedPadPolicy
                    .EnemyGroundEntryBlockedNoDamageImmunityNoProjectileBlocking =>
                "enemy-ground-entry-blocked-no-damage-immunity-no-projectile-blocking",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineProjectileAttributionPolicyId(
        PublicFrontlineProjectileAttributionPolicy policy) =>
        policy switch
        {
            PublicFrontlineProjectileAttributionPolicy
                    .ExactFiringLifePersistsCreditsStableUnitByActualHealthRemoved =>
                "exact-firing-life-persists-credits-stable-unit-by-actual-health-removed",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineFabricationTargetPolicyId(
        PublicFrontlineFabricationTargetPolicy policy) =>
        policy switch
        {
            PublicFrontlineFabricationTargetPolicy.OwnReadyChildSlot =>
                "own-ready-child-slot",
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    private static string FrontlineFabricationActivationRegionId(
        PublicFrontlineFabricationActivationRegion region) =>
        region switch
        {
            PublicFrontlineFabricationActivationRegion.OwnProtectedSpawnPad =>
                "own-protected-spawn-pad",
            _ => throw new ArgumentOutOfRangeException(nameof(region)),
        };

    private static string FrontlineFabricationSpawnRegionId(
        PublicFrontlineFabricationSpawnRegion region) =>
        region switch
        {
            PublicFrontlineFabricationSpawnRegion
                    .OwnProtectedSpawnPadExcludingPrimeSpawn =>
                "own-protected-spawn-pad-excluding-prime-spawn",
            _ => throw new ArgumentOutOfRangeException(nameof(region)),
        };

    private static string FrontlineFabricationCapacityEvaluationId(
        PublicFrontlineFabricationCapacityEvaluation evaluation) =>
        evaluation switch
        {
            PublicFrontlineFabricationCapacityEvaluation
                    .PostMovementDuringQueueFabrications =>
                "post-movement-during-queue-fabrications",
            _ => throw new ArgumentOutOfRangeException(nameof(evaluation)),
        };

    private static string FrontlineFabricationSpawnSelectionId(
        PublicFrontlineFabricationSpawnSelection selection) =>
        selection switch
        {
            PublicFrontlineFabricationSpawnSelection
                    .FirstUnoccupiedUnreservedCanonicalYThenX =>
                "first-unoccupied-unreserved-canonical-y-x",
            _ => throw new ArgumentOutOfRangeException(nameof(selection)),
        };

    private static string FrontlineFabricationSpawnFacingId(
        PublicFrontlineFabricationSpawnFacing facing) =>
        facing switch
        {
            PublicFrontlineFabricationSpawnFacing.OwnPrimeSpawnFacing =>
                "own-prime-spawn-facing",
            _ => throw new ArgumentOutOfRangeException(nameof(facing)),
        };
}
