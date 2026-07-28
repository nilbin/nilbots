using System.Globalization;
using System.Text.Json;

namespace BotArena.Engine;

/// <summary>Explicit canonical writer for actor rules schema 3.</summary>
internal static class ActorRulesCanonicalWriter
{
    public static void Write(
        Utf8JsonWriter writer,
        ActorRulesDefinition rules,
        bool includeProvenance)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", rules.SchemaVersion);
        if (includeProvenance)
        {
            writer.WriteString("rulesetId", rules.RulesetId);
            writer.WriteString(
                "rulesFingerprint",
                ActorContractFingerprint.ComputeRules(rules));
        }

        WriteLimits(writer, rules.Limits);
        WriteSeedMechanics(writer, rules.SeedMechanics);
        WriteGameMode(writer, rules.GameMode);
        WriteLifecycle(writer, rules.Lifecycle);
        WriteForms(writer, rules.Forms);
        WriteMovementProfiles(writer, rules.MovementProfiles);
        WriteVisionProfiles(writer, rules.VisionProfiles);
        WriteAttackProfiles(writer, rules.AttackProfiles);
        WriteActions(writer, rules.Actions);
        ActorTransitionCanonicalWriter.WriteFabricationTransitions(
            writer,
            rules.FabricationTransitions);
        ActorTransitionCanonicalWriter.WriteSameLifeTransitions(
            writer,
            rules.SameLifeTransitions);
        ActorTransitionCanonicalWriter.WriteReplicationTransitions(
            writer,
            rules.ReplicationTransitions);
        WriteTeamPerception(writer, rules.TeamPerception);
        WriteCollisions(writer, rules.Collisions);
        WriteTickResolution(writer, rules.TickResolution);
        writer.WriteEndObject();
    }

    private static void WriteLimits(
        Utf8JsonWriter writer,
        ActorRulesLimits limits)
    {
        writer.WritePropertyName("limits");
        writer.WriteStartObject();
        writer.WriteNumber("maxTicks", limits.MaxTicks);
        writer.WritePropertyName("runtimeFaults");
        writer.WriteStartObject();
        ActorRuntimeFaultDefinition faults = limits.RuntimeFaults;
        writer.WriteNumber(
            "faultsAllowedBeforeDisqualification",
            faults.FaultsAllowedBeforeDisqualification);
        writer.WriteString(
            "disqualificationFaultCount",
            faults.DisqualificationFaultCount.ToString(
                CultureInfo.InvariantCulture));
        writer.WriteString(
            "accumulationScope",
            Id(faults.AccumulationScope));
        writer.WriteString(
            "faultCounterArithmetic",
            Id(faults.FaultCounterArithmetic));
        writer.WriteString(
            "faultingDecision",
            Id(faults.FaultingDecision));
        writer.WriteString(
            "runtimeStageRecovery",
            Id(faults.RuntimeStageRecovery));
        writer.WriteString(
            "replayFaultRepresentation",
            Id(faults.ReplayFaultRepresentation));
        writer.WriteString(
            "faultBatchEventOrder",
            Id(faults.FaultBatchEventOrder));
        writer.WriteString(
            "applicationTiming",
            Id(faults.ApplicationTiming));
        writer.WriteString("threshold", Id(faults.Threshold));
        writer.WriteString(
            "participantDisposition",
            Id(faults.ParticipantDisposition));
        writer.WriteString(
            "pendingWorkDisposition",
            Id(faults.PendingWorkDisposition));
        writer.WriteString(
            "cancellationEventOrder",
            Id(faults.CancellationEventOrder));
        writer.WriteString(
            "ownedProjectileDisposition",
            Id(faults.OwnedProjectileDisposition));
        writer.WriteString(
            "scoreDisposition",
            Id(faults.ScoreDisposition));
        writer.WriteString(
            "scoringTeamEligibility",
            Id(faults.ScoringTeamEligibility));
        writer.WriteString(
            "matchCompletion",
            Id(faults.MatchCompletion));
        writer.WriteString("finalRanking", Id(faults.FinalRanking));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteSeedMechanics(
        Utf8JsonWriter writer,
        ActorSeedMechanicsDefinition seed)
    {
        writer.WritePropertyName("seedMechanics");
        writer.WriteStartObject();
        writer.WriteString("seedProfileId", seed.SeedProfileId);
        writer.WriteString("seedDerivation", Id(seed.SeedDerivation));
        writer.WriteString(
            "lifeIdentityAssignment",
            Id(seed.LifeIdentityAssignment));
        writer.WriteString("runtimeLifetime", Id(seed.RuntimeLifetime));
        writer.WriteString("privateMemory", Id(seed.PrivateMemory));
        writer.WriteEndObject();
    }

    private static void WriteGameMode(
        Utf8JsonWriter writer,
        GameModeDefinition mode)
    {
        writer.WritePropertyName("gameMode");
        writer.WriteStartObject();
        writer.WriteString("kind", Id(mode.Kind));
        writer.WriteString("modeId", mode.ModeId);
        writer.WritePropertyName("victory");
        WriteVictory(writer, mode.Victory);

        writer.WritePropertyName("scoreCatalog");
        writer.WriteStartArray();
        foreach (ScoreChannelDefinition channel in mode.ScoreCatalog)
        {
            writer.WriteStartObject();
            writer.WriteString("channel", Id(channel.Channel));
            writer.WriteString("domain", Id(channel.Domain));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        switch (mode)
        {
            case DeathmatchGameModeDefinition deathmatch:
                writer.WritePropertyName("scoring");
                WriteDeathmatchScoring(writer, deathmatch.Scoring);
                break;
            case FrontlineGameModeDefinition frontline:
                writer.WriteNumber(
                    "frontlinePositionCount",
                    frontline.FrontlinePositionCount);
                writer.WritePropertyName("capture");
                WriteFrontlineCapture(writer, frontline.Capture);
                break;
            default:
                throw Unsupported(mode);
        }

        writer.WriteEndObject();
    }

    private static void WriteVictory(
        Utf8JsonWriter writer,
        VictoryDefinition victory)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Id(victory.Kind));
        writer.WritePropertyName("timeoutRanking");
        writer.WriteStartArray();
        foreach (ScoreRankingDefinition ranking in victory.TimeoutRanking)
        {
            writer.WriteStartObject();
            writer.WriteString("channel", Id(ranking.Channel));
            writer.WriteString("direction", Id(ranking.Direction));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        switch (victory)
        {
            case DeathmatchVictoryDefinition deathmatch:
                ActorContractCanonicalJson.WriteNullableNumber(
                    writer,
                    "killsToWin",
                    deathmatch.KillsToWin);
                writer.WriteString(
                    "terminalTickPrecedence",
                    Id(deathmatch.TerminalTickPrecedence));
                break;
            case FrontlineVictoryDefinition frontline:
                writer.WriteNumber(
                    "pushesToBreach",
                    frontline.PushesToBreach);
                break;
            default:
                throw Unsupported(victory);
        }

        writer.WriteEndObject();
    }

    private static void WriteDeathmatchScoring(
        Utf8JsonWriter writer,
        DeathmatchScoringDefinition scoring)
    {
        writer.WriteStartObject();
        writer.WriteString(
            "deathIncrement",
            Id(scoring.DeathIncrement));
        writer.WriteString(
            "killIncrement",
            Id(scoring.KillIncrement));
        writer.WriteString(
            "alliedFinalDamage",
            Id(scoring.AlliedFinalDamage));
        writer.WriteString(
            "damageDealtIncrement",
            Id(scoring.DamageDealtIncrement));
        writer.WriteString(
            "activeHealthSnapshot",
            Id(scoring.ActiveHealthSnapshot));
        writer.WriteString(
            "nonDamageRetirement",
            Id(scoring.NonDamageRetirement));
        writer.WriteString(
            "earlyKillLimitResolution",
            Id(scoring.EarlyKillLimitResolution));
        writer.WriteEndObject();
    }

    private static void WriteFrontlineCapture(
        Utf8JsonWriter writer,
        FrontlineCaptureDefinition capture)
    {
        writer.WriteStartObject();
        writer.WriteNumber("threshold", capture.Threshold);
        writer.WriteNumber(
            "gainPerSoleTeamTick",
            capture.GainPerSoleTeamTick);
        writer.WriteNumber("decayAmount", capture.DecayAmount);
        writer.WriteNumber(
            "decayIntervalTicks",
            capture.DecayIntervalTicks);
        writer.WriteNumber(
            "redeployPauseTicks",
            capture.RedeployPauseTicks);
        writer.WriteString("controlPolicy", Id(capture.ControlPolicy));
        writer.WriteString("timeoutPolicy", Id(capture.TimeoutPolicy));
        writer.WriteString(
            "territorialProgressFormula",
            Id(capture.TerritorialProgressFormula));
        writer.WriteString(
            "completionPolicy",
            Id(capture.CompletionPolicy));
        writer.WriteString(
            "initialPosition",
            Id(capture.InitialPosition));
        writer.WriteString(
            "captureArithmetic",
            Id(capture.CaptureArithmetic));
        writer.WriteString(
            "oppositionArithmetic",
            Id(capture.OppositionArithmetic));
        writer.WriteString("decayClock", Id(capture.DecayClock));
        writer.WriteString(
            "disabledDecay",
            Id(capture.DisabledDecay));
        writer.WriteString(
            "redeployPolicy",
            Id(capture.RedeployPolicy));
        writer.WriteString(
            "redeployTickArithmetic",
            Id(capture.RedeployTickArithmetic));
        writer.WriteEndObject();
    }

    private static void WriteLifecycle(
        Utf8JsonWriter writer,
        ActorLifecycleDefinition lifecycle)
    {
        writer.WritePropertyName("lifecycle");
        writer.WriteStartObject();
        writer.WritePropertyName("profiles");
        writer.WriteStartArray();
        foreach (ActorLifecycleProfileDefinition profile in lifecycle.Profiles)
        {
            writer.WriteStartObject();
            writer.WriteString("profileId", profile.ProfileId);
            writer.WriteString(
                "destructionPolicy",
                Id(profile.DestructionPolicy));
            writer.WriteNumber("delayTicks", profile.DelayTicks);
            ActorContractCanonicalJson.WriteNullableString(
                writer,
                "automaticReturnFormId",
                profile.AutomaticReturnFormId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString(
            "destructionClock",
            Id(lifecycle.DestructionClock));
        writer.WriteString(
            "newLifeSemantics",
            Id(lifecycle.NewLifeSemantics));
        writer.WriteString(
            "newLifeCombatState",
            Id(lifecycle.NewLifeCombatState));
        writer.WriteString(
            "newLifeResourceClock",
            Id(lifecycle.NewLifeResourceClock));
        writer.WriteString(
            "generationSemantics",
            Id(lifecycle.GenerationSemantics));
        writer.WriteString(
            "automaticReturnPlacement",
            Id(lifecycle.AutomaticReturnPlacement));
        writer.WriteString(
            "tickStartLifecycleOrder",
            Id(lifecycle.TickStartLifecycleOrder));
        writer.WriteString(
            "outputTileProjectile",
            Id(lifecycle.OutputTileProjectile));
        writer.WriteEndObject();
    }

    private static void WriteForms(
        Utf8JsonWriter writer,
        IEnumerable<ActorFormDefinition> forms)
    {
        writer.WritePropertyName("forms");
        writer.WriteStartArray();
        foreach (ActorFormDefinition form in forms)
        {
            writer.WriteStartObject();
            writer.WriteString("id", form.Id);
            writer.WriteNumber("maxHealth", form.MaxHealth);
            writer.WriteString(
                "movementProfileId",
                form.MovementProfileId);
            writer.WriteString("visionProfileId", form.VisionProfileId);
            ActorContractCanonicalJson.WriteNullableString(
                writer,
                "attackProfileId",
                form.AttackProfileId);
            writer.WriteNumber("objectiveWeight", form.ObjectiveWeight);
            writer.WritePropertyName("allowedActionIds");
            writer.WriteStartArray();
            foreach (string actionId in form.AllowedActionIds)
                writer.WriteStringValue(actionId);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteMovementProfiles(
        Utf8JsonWriter writer,
        IEnumerable<ActorMovementProfileDefinition> profiles)
    {
        writer.WritePropertyName("movementProfiles");
        writer.WriteStartArray();
        foreach (ActorMovementProfileDefinition profile in profiles)
        {
            writer.WriteStartObject();
            writer.WriteString("id", profile.Id);
            writer.WriteString(
                "movementLayer",
                Id(profile.MovementLayer));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteVisionProfiles(
        Utf8JsonWriter writer,
        IEnumerable<ActorVisionProfileDefinition> profiles)
    {
        writer.WritePropertyName("visionProfiles");
        writer.WriteStartArray();
        foreach (ActorVisionProfileDefinition profile in profiles)
        {
            writer.WriteStartObject();
            writer.WriteString("id", profile.Id);
            writer.WriteNumber("range", profile.Range);
            writer.WriteString(
                "distanceMetric",
                Id(profile.DistanceMetric));
            writer.WriteString("shape", Id(profile.Shape));
            writer.WriteNumber(
                "omnidirectionalProximityRange",
                profile.OmnidirectionalProximityRange);
            writer.WriteString(
                "lineOfSight",
                Id(profile.LineOfSight));
            writer.WriteNumber("hearingRadius", profile.HearingRadius);
            writer.WriteNumber(
                "hearingBearingSectors",
                profile.HearingBearingSectors);
            writer.WriteString(
                "hearingBearingModel",
                Id(profile.HearingBearingModel));
            writer.WriteString(
                "hearingDistanceBandModel",
                Id(profile.HearingDistanceBandModel));
            writer.WritePropertyName("hearingDistanceBandUpperBounds");
            writer.WriteStartArray();
            foreach (int upperBound in
                     profile.HearingDistanceBandUpperBounds)
            {
                writer.WriteNumberValue(upperBound);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("loudEventKinds");
            writer.WriteStartArray();
            foreach (ActorAudibleEventKind eventKind in
                     profile.LoudEventKinds)
            {
                writer.WriteStringValue(Id(eventKind));
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteAttackProfiles(
        Utf8JsonWriter writer,
        IEnumerable<ActorAttackProfileDefinition> profiles)
    {
        writer.WritePropertyName("attackProfiles");
        writer.WriteStartArray();
        foreach (ActorAttackProfileDefinition profile in profiles)
        {
            writer.WriteStartObject();
            writer.WriteString("id", profile.Id);
            writer.WriteBoolean(
                "omnidirectionalAim",
                profile.OmnidirectionalAim);
            writer.WriteString(
                "aimInterpretation",
                Id(profile.AimInterpretation));
            writer.WritePropertyName("projectile");
            WriteProjectile(writer, profile.Projectile);
            writer.WriteNumber("cooldownTicks", profile.CooldownTicks);
            writer.WriteNumber("maxEnergy", profile.MaxEnergy);
            writer.WriteNumber(
                "attackEnergyCost",
                profile.AttackEnergyCost);
            writer.WriteNumber(
                "energyRegenerationIntervalTicks",
                profile.EnergyRegenerationIntervalTicks);
            writer.WriteNumber(
                "energyRegenerationAmount",
                profile.EnergyRegenerationAmount);
            writer.WriteString(
                "energyRegenerationClock",
                Id(profile.EnergyRegenerationClock));
            writer.WriteString(
                "energyUpdateOrder",
                Id(profile.EnergyUpdateOrder));
            writer.WriteString(
                "energyArithmetic",
                Id(profile.EnergyArithmetic));
            writer.WriteString(
                "attackAvailability",
                Id(profile.AttackAvailability));
            writer.WriteString(
                "cooldownUpdate",
                Id(profile.CooldownUpdate));
            writer.WritePropertyName("shotProgram");
            WriteShotProgram(writer, profile.ShotProgram);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteProjectile(
        Utf8JsonWriter writer,
        ActorProjectileDefinition projectile)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", Id(projectile.Mode));
        writer.WriteNumber("damagePerHit", projectile.DamagePerHit);
        writer.WriteNumber(
            "maxTravelTiles",
            projectile.MaxTravelTiles);
        writer.WriteNumber(
            "ticksPerAdvance",
            projectile.TicksPerAdvance);
        writer.WriteNumber(
            "tilesPerAdvance",
            projectile.TilesPerAdvance);
        writer.WriteNumber("launchTiles", projectile.LaunchTiles);
        writer.WriteBoolean(
            "advancesOnLaunchTick",
            projectile.AdvancesOnLaunchTick);
        writer.WriteBoolean(
            "damageAppliedSimultaneously",
            projectile.DamageAppliedSimultaneously);
        writer.WriteBoolean(
            "diagonalCornersMustBeClear",
            projectile.DiagonalCornersMustBeClear);
        writer.WriteEndObject();
    }

    private static void WriteShotProgram(
        Utf8JsonWriter writer,
        ActorShotProgramDefinition program)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", program.Enabled);
        writer.WriteNumber("headingSectors", program.HeadingSectors);
        writer.WriteString("headingModel", Id(program.HeadingModel));
        writer.WriteNumber(
            "bendStepSectors",
            program.BendStepSectors);
        writer.WriteNumber(
            "minInitialAimSteps",
            program.MinInitialAimSteps);
        writer.WriteNumber(
            "maxInitialAimSteps",
            program.MaxInitialAimSteps);
        writer.WritePropertyName("aimOnlyProgram");
        writer.WriteStartObject();
        writer.WriteNumber(
            "bendDirection",
            program.AimOnlyProgram.BendDirection);
        writer.WriteNumber(
            "bendAfterTiles",
            program.AimOnlyProgram.BendAfterTiles);
        writer.WriteNumber(
            "bendEveryTiles",
            program.AimOnlyProgram.BendEveryTiles);
        writer.WriteNumber(
            "bendCount",
            program.AimOnlyProgram.BendCount);
        writer.WriteEndObject();
        writer.WritePropertyName("allowedCurvedBendDirections");
        writer.WriteStartArray();
        foreach (int direction in program.AllowedCurvedBendDirections)
            writer.WriteNumberValue(direction);
        writer.WriteEndArray();
        writer.WriteNumber(
            "minBendAfterTiles",
            program.MinBendAfterTiles);
        writer.WriteNumber(
            "maxBendAfterTiles",
            program.MaxBendAfterTiles);
        writer.WriteNumber(
            "minBendEveryTiles",
            program.MinBendEveryTiles);
        writer.WriteNumber(
            "maxBendEveryTiles",
            program.MaxBendEveryTiles);
        writer.WriteNumber("minBendCount", program.MinBendCount);
        writer.WriteNumber("maxBendCount", program.MaxBendCount);
        writer.WriteNumber("launchTiles", program.LaunchTiles);
        writer.WriteBoolean("payloadOptional", program.PayloadOptional);
        writer.WritePropertyName("defaultProgram");
        WriteShotProgramValue(writer, program.DefaultProgram);
        if (program.InvalidPayloadResult is { } invalid)
        {
            writer.WriteString(
                "invalidPayloadResult",
                Id(invalid));
        }
        else
        {
            writer.WriteNull("invalidPayloadResult");
        }
        writer.WriteString(
            "unsupportedPayloadResult",
            Id(program.UnsupportedPayloadResult));
        writer.WriteBoolean(
            "diagonalCornersMustBeClear",
            program.DiagonalCornersMustBeClear);
        writer.WriteEndObject();
    }

    private static void WriteShotProgramValue(
        Utf8JsonWriter writer,
        ActorShotProgramValue program)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "initialAimOffset",
            program.InitialAimOffset);
        writer.WriteNumber("bendDirection", program.BendDirection);
        writer.WriteNumber("bendAfterTiles", program.BendAfterTiles);
        writer.WriteNumber("bendEveryTiles", program.BendEveryTiles);
        writer.WriteNumber("bendCount", program.BendCount);
        writer.WriteEndObject();
    }

    private static void WriteActions(
        Utf8JsonWriter writer,
        IEnumerable<ActorActionDefinition> actions)
    {
        writer.WritePropertyName("actions");
        writer.WriteStartArray();
        foreach (ActorActionDefinition action in actions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", action.Id);
            writer.WriteNumber("code", action.Code);
            writer.WriteString("kind", Id(action.Kind));
            writer.WritePropertyName("parameterKinds");
            writer.WriteStartArray();
            foreach (ActorActionParameterKind parameter in
                     action.ParameterKinds)
            {
                writer.WriteStringValue(Id(parameter));
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteTeamPerception(
        Utf8JsonWriter writer,
        ActorTeamPerceptionDefinition perception)
    {
        writer.WritePropertyName("teamPerception");
        writer.WriteStartObject();
        writer.WriteString("kind", Id(perception.Kind));
        writer.WriteString("snapshot", Id(perception.Snapshot));
        writer.WriteString(
            "sameTickDecisionSharing",
            Id(perception.SameTickDecisionSharing));
        writer.WriteString(
            "observationProvenance",
            Id(perception.ObservationProvenance));
        writer.WriteEndObject();
    }

    private static void WriteCollisions(
        Utf8JsonWriter writer,
        ActorCollisionDefinition collisions)
    {
        writer.WritePropertyName("collisions");
        writer.WriteStartObject();
        writer.WriteBoolean(
            "actorsBlockWalls",
            collisions.ActorsBlockWalls);
        writer.WriteBoolean(
            "actorsBlockActors",
            collisions.ActorsBlockActors);
        writer.WriteBoolean(
            "sameDestinationMovesBlockAll",
            collisions.SameDestinationMovesBlockAll);
        writer.WriteBoolean(
            "swapMovesBlocked",
            collisions.SwapMovesBlocked);
        writer.WriteBoolean(
            "followingVacatedActorAllowed",
            collisions.FollowingVacatedActorAllowed);
        writer.WriteBoolean(
            "projectilesBlockMovement",
            collisions.ProjectilesBlockMovement);
        writer.WriteBoolean(
            "movingOntoProjectileCausesHit",
            collisions.MovingOntoProjectileCausesHit);
        writer.WriteBoolean(
            "wallsConsumeProjectiles",
            collisions.WallsConsumeProjectiles);
        writer.WriteBoolean(
            "projectilesIgnoreFiringLife",
            collisions.ProjectilesIgnoreFiringLife);
        writer.WriteBoolean(
            "projectilesStopOnFirstEnemyActor",
            collisions.ProjectilesStopOnFirstEnemyActor);
        writer.WriteBoolean(
            "projectilesCollideWithProjectiles",
            collisions.ProjectilesCollideWithProjectiles);
        writer.WriteString(
            "alliedProjectileContact",
            Id(collisions.AlliedProjectileContact));
        writer.WriteString(
            "movementResolution",
            Id(collisions.MovementResolution));
        writer.WriteString(
            "projectileTraversalResolution",
            Id(collisions.ProjectileTraversalResolution));
        writer.WriteString(
            "actorProjectileContactTiming",
            Id(collisions.ActorProjectileContactTiming));
        writer.WriteString(
            "movementDestinationProjectileResult",
            Id(collisions.MovementDestinationProjectileResult));
        writer.WriteString(
            "alliedMovementDestinationOverride",
            Id(collisions.AlliedMovementDestinationOverride));
        writer.WriteEndObject();
    }

    private static void WriteTickResolution(
        Utf8JsonWriter writer,
        ActorTickResolutionDefinition tick)
    {
        writer.WritePropertyName("tickResolution");
        writer.WriteStartObject();
        writer.WriteBoolean(
            "observationsUsePreTickState",
            tick.ObservationsUsePreTickState);
        writer.WriteBoolean(
            "decisionsResolveAsJointStep",
            tick.DecisionsResolveAsJointStep);
        writer.WriteString(
            "movementActionResolution",
            Id(tick.MovementActionResolution));
        writer.WriteString(
            "rotationActionResolution",
            Id(tick.RotationActionResolution));
        writer.WriteString(
            "actionAdmission",
            Id(tick.ActionAdmission));
        writer.WriteString(
            "actionFaultCounting",
            Id(tick.ActionFaultCounting));
        writer.WriteString(
            "matchCompletionPrecedence",
            Id(tick.MatchCompletionPrecedence));
        writer.WritePropertyName("damageResolution");
        WriteDamageResolution(writer, tick.DamageResolution);
        writer.WritePropertyName("phases");
        writer.WriteStartArray();
        foreach (ActorTickResolutionPhase phase in tick.Phases)
            writer.WriteStringValue(Id(phase));
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteDamageResolution(
        Utf8JsonWriter writer,
        ActorDamageResolutionDefinition damage)
    {
        writer.WriteStartObject();
        writer.WriteString("contactBatch", Id(damage.ContactBatch));
        writer.WriteString(
            "perTargetApplicationOrder",
            Id(damage.PerTargetApplicationOrder));
        writer.WriteString(
            "projectileIdentityAssignment",
            Id(damage.ProjectileIdentityAssignment));
        writer.WriteString(
            "contactOrdinalAssignment",
            Id(damage.ContactOrdinalAssignment));
        writer.WriteString(
            "healthApplication",
            Id(damage.HealthApplication));
        writer.WriteString(
            "destructionAttribution",
            Id(damage.DestructionAttribution));
        writer.WriteString("eventOrder", Id(damage.EventOrder));
        writer.WriteEndObject();
    }

    private static string Id(Enum value) =>
        ActorContractCanonicalIds.Id(value);

    private static ArgumentOutOfRangeException Unsupported(object value) =>
        new(
            nameof(value),
            value,
            "The generation-3 writer does not support this contract variant.");
}
