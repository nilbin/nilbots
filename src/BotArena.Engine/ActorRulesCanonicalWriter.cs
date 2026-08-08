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
                // Trailing inert-default omission, exactly like the capture
                // ratchet's hold and a form's projectile guard: a mode with
                // no side objective writes no bytes for one, so every
                // contract authored before the secondary-control capability
                // existed — the immutable hosted frontline-labs-1 included —
                // keeps its exact rules, match, and aggregate fingerprints.
                if (frontline.SecondaryControl is { } secondaryControl)
                {
                    writer.WritePropertyName("secondaryControl");
                    WriteFrontlineSecondaryControl(writer, secondaryControl);
                }
                // The battlefield economy follows the same trailing
                // inert-default omission: a mode that declares none writes no
                // bytes for one, so every contract authored before this
                // capability existed keeps its exact rules, match, and
                // aggregate fingerprints.
                if (frontline.ScrapEconomy is { } scrapEconomy)
                {
                    writer.WritePropertyName("scrapEconomy");
                    WriteFrontlineScrapEconomy(writer, scrapEconomy);
                }
                break;
            case ArcRelayGameModeDefinition arcRelay:
                WriteArcRelay(writer, arcRelay);
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
            case ArcRelayVictoryDefinition arcRelay:
                writer.WriteNumber(
                    "pulsesToDestroyReactor",
                    arcRelay.PulsesToDestroyReactor);
                break;
            default:
                throw Unsupported(victory);
        }

        writer.WriteEndObject();
    }

    private static void WriteArcRelay(
        Utf8JsonWriter writer,
        ArcRelayGameModeDefinition mode)
    {
        writer.WriteNumber("pendingRearmTicks", mode.PendingRearmTicks);
        writer.WriteNumber(
            "coreRelocationIntervalTicks",
            mode.CoreRelocationIntervalTicks);
        writer.WriteNumber("coresPerPulse", mode.CoresPerPulse);
        writer.WriteNumber("fieldedSlotsPerTeam", mode.FieldedSlotsPerTeam);
        writer.WriteNumber("maxCopiesPerClass", mode.MaxCopiesPerClass);
        writer.WriteNumber("respawnDelayTicks", mode.RespawnDelayTicks);
        // Written only when not 1 so historical grammar-1 rules bytes are
        // untouched by this field's existence.
        if (mode.SignatureGrammarVersion != 1)
        {
            writer.WriteNumber(
                "signatureGrammarVersion",
                mode.SignatureGrammarVersion);
        }
        // Written only when non-zero so historical rules bytes are untouched.
        if (mode.WellBirthJitterTicks != 0)
        {
            writer.WriteNumber(
                "wellBirthJitterTicks",
                mode.WellBirthJitterTicks);
        }
        // Written only when true so historical rules bytes are untouched.
        if (mode.AlternatingResolutionOrder)
        {
            writer.WriteBoolean(
                "alternatingResolutionOrder",
                mode.AlternatingResolutionOrder);
        }
        // Written only when true so historical rules bytes are untouched.
        if (mode.ThreefoldSockets)
        {
            writer.WriteBoolean("threefoldSockets", mode.ThreefoldSockets);
        }
        // Written only when not 1 so historical rules bytes are untouched.
        if (mode.CoreBaseValue != 1)
            writer.WriteNumber("coreBaseValue", mode.CoreBaseValue);
        // Written only when ripening is active.
        if (mode.RipenIntervalTicks != 0)
        {
            writer.WriteNumber("ripenIntervalTicks", mode.RipenIntervalTicks);
            writer.WriteNumber("ripenMaxValue", mode.RipenMaxValue);
            writer.WriteNumber("ripenResumeTicks", mode.RipenResumeTicks);
        }
        if (mode.RearArcDamageMultiplier != 1)
        {
            writer.WriteNumber(
                "rearArcDamageMultiplier", mode.RearArcDamageMultiplier);
        }
        // Written only when veterancy is active.
        if (mode.VeterancyXpPerLevel != 0)
        {
            writer.WriteNumber(
                "veterancyXpPerLevel", mode.VeterancyXpPerLevel);
            writer.WriteNumber("veterancyMaxLevel", mode.VeterancyMaxLevel);
        }
        if (mode.SeedPhasedResolutionOrder)
        {
            writer.WriteBoolean(
                "seedPhasedResolutionOrder",
                mode.SeedPhasedResolutionOrder);
        }
        if (mode.SeedPhasedWellLead)
        {
            writer.WriteBoolean(
                "seedPhasedWellLead",
                mode.SeedPhasedWellLead);
        }
        if (mode.HealZoneTicksPerHp != 0)
        {
            writer.WriteNumber(
                "healZoneTicksPerHp", mode.HealZoneTicksPerHp);
        }
        writer.WritePropertyName("wells");
        writer.WriteStartArray();
        foreach (ArcRelayWellScheduleDefinition well in mode.Wells)
        {
            writer.WriteStartObject();
            writer.WriteString("wellId", well.WellId);
            writer.WriteNumber("firstBirthTick", well.FirstBirthTick);
            writer.WriteNumber("cadenceTicks", well.CadenceTicks);
            writer.WriteNumber("finalBirthTick", well.FinalBirthTick);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("signatures");
        writer.WriteStartArray();
        foreach (ArcRelaySignatureDefinition signature in mode.Signatures)
            WriteArcRelaySignature(
                writer,
                signature,
                includeMetadata: mode.SignatureGrammarVersion >= 2);
        writer.WriteEndArray();
    }

    private static void WriteArcRelaySignature(
        Utf8JsonWriter writer,
        ArcRelaySignatureDefinition signature,
        bool includeMetadata)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Id(signature.Kind));
        writer.WriteString("signatureId", signature.SignatureId);
        writer.WriteString("classId", signature.ClassId);
        writer.WriteString("actionId", signature.ActionId);
        writer.WriteNumber("cooldownTicks", signature.CooldownTicks);
        switch (signature)
        {
            case ArcRelaySignatureDefinition.VectorDash value:
                writer.WriteNumber("tellTicks", value.TellTicks);
                writer.WriteNumber("maxTiles", value.MaxTiles);
                break;
            case ArcRelaySignatureDefinition.PrismWall value:
                writer.WriteNumber("segmentCount", value.SegmentCount);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                writer.WriteNumber("contactCapacity", value.ContactCapacity);
                break;
            case ArcRelaySignatureDefinition.TractorHook value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("maxPullTiles", value.MaxPullTiles);
                break;
            case ArcRelaySignatureDefinition.RepairBeam value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("ticksPerRepair", value.TicksPerRepair);
                writer.WriteNumber("hullPerRepair", value.HullPerRepair);
                writer.WriteNumber(
                    "maxHullPerActivation",
                    value.MaxHullPerActivation);
                break;
            case ArcRelaySignatureDefinition.SurveyFlare value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber(
                    "travelTilesPerTick",
                    value.TravelTilesPerTick);
                writer.WriteNumber("revealRadius", value.RevealRadius);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                break;
            case ArcRelaySignatureDefinition.FallingStar value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("tellTicks", value.TellTicks);
                writer.WriteNumber("damage", value.Damage);
                break;
            case ArcRelaySignatureDefinition.TripNode value:
                writer.WriteNumber("hull", value.Hull);
                writer.WriteNumber("triggerDamage", value.TriggerDamage);
                writer.WriteNumber("revealRange", value.RevealRange);
                break;
            case ArcRelaySignatureDefinition.NullField value:
                writer.WriteNumber("radius", value.Radius);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                break;
            case ArcRelaySignatureDefinition.ArcToss value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("tellTicks", value.TellTicks);
                writer.WriteNumber(
                    "travelTilesPerTick",
                    value.TravelTilesPerTick);
                break;
            case ArcRelaySignatureDefinition.Exchange value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("tellTicks", value.TellTicks);
                break;
            case ArcRelaySignatureDefinition.RailLine value:
                // Rail's telegraph has always been on the wire as `tellTicks`
                // and keeps that name; the ruling renamed the CONCEPT, not
                // the contract, and every existing ruleset keeps its
                // fingerprint.
                writer.WriteNumber("tellTicks", value.WindupTicks);
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("damage", value.Damage);
                writer.WriteNumber(
                    "cancelCooldownTicks",
                    value.CancelCooldownTicks);
                break;
            case ArcRelaySignatureDefinition.HardlightBlock value:
                writer.WriteNumber("hull", value.Hull);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                break;
            case ArcRelaySignatureDefinition.TargetPaint value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                writer.WriteNumber(
                    "enhancedHitCount",
                    value.EnhancedHitCount);
                writer.WriteNumber("bonusDamage", value.BonusDamage);
                break;
            case ArcRelaySignatureDefinition.KineticBurst value:
                writer.WriteNumber("tellTicks", value.TellTicks);
                writer.WriteNumber("pushTiles", value.PushTiles);
                break;
            case ArcRelaySignatureDefinition.SmokeCanister value:
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("radius", value.Radius);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                break;
            case ArcRelaySignatureDefinition.SentinelSeed value:
                writer.WriteNumber("hull", value.Hull);
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("damage", value.Damage);
                writer.WriteNumber(
                    "fireCooldownTicks",
                    value.FireCooldownTicks);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                break;
            case ArcRelaySignatureDefinition.SentinelSeed2 value:
                // The bolt-class windup is presence-driven, exactly like the
                // grammar-2 bolt fields beside it: a ruleset that authors no
                // windup writes no key and keeps its fingerprint.
                if (value.WindupTicks > 0)
                    writer.WriteNumber("windupTicks", value.WindupTicks);
                writer.WriteNumber("hull", value.Hull);
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("damage", value.Damage);
                writer.WriteNumber(
                    "fireCooldownTicks",
                    value.FireCooldownTicks);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                writer.WriteNumber(
                    "boltTilesPerAdvance",
                    value.BoltTilesPerAdvance);
                break;
            case ArcRelaySignatureDefinition.TractorHook2 value:
                if (value.WindupTicks > 0)
                    writer.WriteNumber("windupTicks", value.WindupTicks);
                writer.WriteNumber("range", value.Range);
                writer.WriteNumber("maxPullTiles", value.MaxPullTiles);
                writer.WriteNumber(
                    "boltTilesPerAdvance",
                    value.BoltTilesPerAdvance);
                break;
            case ArcRelaySignatureDefinition.NullField2 value:
                writer.WriteNumber("radius", value.Radius);
                writer.WriteNumber("durationTicks", value.DurationTicks);
                writer.WriteNumber("tellTicks", value.TellTicks);
                break;
            default:
                throw Unsupported(signature);
        }
        if (includeMetadata)
        {
            ArcRelaySignatureDefinition.SignatureMetadata metadata =
                ArcRelaySignatureDefinition.MetadataFor(signature.Kind);
            writer.WriteString("category", metadata.Category);
            writer.WriteString("argumentKind", metadata.ArgumentKind);
            writer.WriteNumber("engagementRange", metadata.EngagementRange);
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

    private static void WriteFrontlineSecondaryControl(
        Utf8JsonWriter writer,
        FrontlineSecondaryControlDefinition secondaryControl)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("regionIds");
        writer.WriteStartArray();
        // Declared order, not sorted: the site regions are a sequence the
        // contract hands to bots, exactly like the objective chain.
        foreach (string regionId in secondaryControl.RegionIds)
            writer.WriteStringValue(regionId);
        writer.WriteEndArray();
        writer.WriteNumber(
            "captureThresholdTicks",
            secondaryControl.CaptureThresholdTicks);
        writer.WriteString("ownership", Id(secondaryControl.Ownership));
        writer.WriteString("effect", Id(secondaryControl.Effect));
        writer.WriteString("rallyScope", Id(secondaryControl.RallyScope));
        writer.WriteEndObject();
    }

    private static void WriteFrontlineScrapEconomy(
        Utf8JsonWriter writer,
        FrontlineScrapEconomyDefinition economy)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("veinSites");
        writer.WriteStartArray();
        // Declared order, not sorted: a bot reads the sites positionally out
        // of the contract, exactly like the objective chain.
        foreach (Position site in economy.VeinSites)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", site.X);
            writer.WriteNumber("y", site.Y);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteNumber(
            "veinFirstSpawnTick",
            economy.VeinFirstSpawnTick);
        writer.WriteNumber(
            "veinSpawnIntervalTicks",
            economy.VeinSpawnIntervalTicks);
        writer.WriteNumber("veinLastSpawnTick", economy.VeinLastSpawnTick);
        writer.WriteNumber("veinAmount", economy.VeinAmount);
        writer.WriteNumber("wreckAmount", economy.WreckAmount);
        writer.WriteNumber("assayAmount", economy.AssayAmount);
        writer.WriteNumber("carryCapacity", economy.CarryCapacity);
        writer.WriteNumber("pileLifetimeTicks", economy.PileLifetimeTicks);
        writer.WriteNumber(
            "maxSimultaneousPiles",
            economy.MaxSimultaneousPiles);
        writer.WritePropertyName("bankRegionIds");
        writer.WriteStartArray();
        // Declared order is team order: index is the scoring team ID.
        foreach (string regionId in economy.BankRegionIds)
            writer.WriteStringValue(regionId);
        writer.WriteEndArray();
        writer.WriteString("upgradeScope", Id(economy.UpgradeScope));
        writer.WriteNumber("maxTotalTiers", economy.MaxTotalTiers);
        writer.WriteString("purchaseMode", Id(economy.PurchaseMode));
        writer.WritePropertyName("tracks");
        writer.WriteStartArray();
        foreach (FrontlineScrapTrackDefinition track in economy.Tracks)
        {
            writer.WriteStartObject();
            writer.WriteString("trackId", track.TrackId);
            writer.WriteString("effect", Id(track.Effect));
            writer.WriteNumber("perTierMagnitude", track.PerTierMagnitude);
            writer.WriteNumber("maxTier", track.MaxTier);
            writer.WritePropertyName("tierCosts");
            writer.WriteStartArray();
            foreach (int cost in track.TierCosts)
                writer.WriteNumberValue(cost);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
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
        if (!capture.GainSchedule.IsDefaultOrEmpty)
        {
            writer.WritePropertyName("gainSchedule");
            writer.WriteStartArray();
            foreach (
                FrontlineCaptureGainPhaseDefinition phase
                in capture.GainSchedule)
            {
                writer.WriteStartObject();
                writer.WriteString("phaseId", phase.PhaseId);
                writer.WriteNumber("startsAtTick", phase.StartsAtTick);
                writer.WriteNumber(
                    "gainPerSoleTeamTick",
                    phase.GainPerSoleTeamTick);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
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
        // Inert-default omission, exactly like the optional capture-gain
        // schedule and the movement profile's facing coupling: a hold
        // duration exists only for the high-water-mark redeploy policy, so
        // every contract authored before the territory ratchet existed —
        // including the immutable hosted frontline-labs-1 — writes no bytes
        // for it and keeps its exact fingerprint.
        if (capture.RatchetHoldTicks != 0)
        {
            writer.WriteNumber(
                "ratchetHoldTicks",
                capture.RatchetHoldTicks);
        }
        writer.WriteString(
            "redeployTickArithmetic",
            Id(capture.RedeployTickArithmetic));
        // The capture channel's three settings, trailing and inert-omitted
        // together, exactly like the ratchet's hold and the mode's side
        // objective: a ruleset that does not channel writes no bytes for any
        // of them, so every historical contract — the immutable hosted
        // frontline-labs-1 included — keeps its exact rules, match, and
        // aggregate fingerprints.
        if (capture.StationaryGainMultiplierCap != 0)
        {
            writer.WriteNumber(
                "stationaryGainMultiplierCap",
                capture.StationaryGainMultiplierCap);
        }
        if (capture.OpposingErosionMultiplier != 0)
        {
            writer.WriteNumber(
                "opposingErosionMultiplier",
                capture.OpposingErosionMultiplier);
        }
        if (capture.ClaimInterrupt is { } claimInterrupt)
        {
            writer.WritePropertyName("claimInterrupt");
            writer.WriteStartObject();
            writer.WriteString("kind", Id(claimInterrupt.Kind));
            writer.WriteNumber(
                "revertPerDamagePoint",
                claimInterrupt.RevertPerDamagePoint);
            writer.WriteString("scope", Id(claimInterrupt.Scope));
            writer.WriteString(
                "granularity",
                Id(claimInterrupt.Granularity));
            writer.WriteEndObject();
        }
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
            // Additive trailing field under the #156 canonical discipline,
            // exactly like the form's projectile guard: emitted only when the
            // profile declares a root-factory bootstrap, so every profile
            // shipped before prime dissolution writes the bytes it always
            // wrote and an absent property means "no bootstrap — total body
            // loss is permanent for this slot".
            if (profile.RootFactorySeedFormId is not null)
            {
                writer.WriteString(
                    "rootFactorySeedFormId",
                    profile.RootFactorySeedFormId);
            }
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
            // Inert-default omission, exactly like the movement profile's
            // facing coupling and the capture ratchet's hold: a form without a
            // defensive guard writes no bytes, so every contract authored
            // before guards existed — the immutable hosted frontline-labs-1
            // included — keeps its exact fingerprint.
            if (form.ProjectileGuard != ActorFormProjectileGuardKind.None)
            {
                writer.WriteString(
                    "projectileGuard",
                    Id(form.ProjectileGuard));
            }
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
            // Inert-default omission, exactly like the optional capture-gain
            // schedule above: PreserveFacing writes no bytes, so every
            // contract authored before facing coupling existed — including
            // the immutable hosted frontline-labs-1 — keeps its exact
            // fingerprint.
            if (profile.FacingCoupling
                != ActorMovementFacingCoupling.PreserveFacing)
            {
                writer.WriteString(
                    "facingCoupling",
                    Id(profile.FacingCoupling));
            }
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
            if (profile.FacingAimHalfWidthSectors > 0)
            {
                writer.WriteNumber(
                    "facingAimHalfWidthSectors",
                    profile.FacingAimHalfWidthSectors);
            }
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
            // Inert-default omission: a one-bolt attack carries no volley
            // object at all, so every pre-volley contract keeps its exact
            // fingerprint (DECISIONS #156's additive pattern).
            if (profile.Volley is { } volley)
            {
                writer.WritePropertyName("volley");
                WriteVolley(writer, volley);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteVolley(
        Utf8JsonWriter writer,
        ActorAttackVolleyDefinition volley)
    {
        writer.WriteStartObject();
        writer.WriteNumber("projectileCount", volley.ProjectileCount);
        writer.WriteString("spread", Id(volley.Spread));
        writer.WriteString("identityOrder", Id(volley.IdentityOrder));
        writer.WriteEndObject();
    }

    private static void WriteProjectile(
        Utf8JsonWriter writer,
        ActorProjectileDefinition projectile)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", Id(projectile.Mode));
        // Declared strikes (DECISIONS #212) emit only when authored, exactly
        // like the projectile guard: historical rulesets keep byte-identical
        // fingerprints.
        if (projectile.StrikeWindupTicks > 0)
        {
            writer.WriteNumber(
                "strikeWindupTicks",
                projectile.StrikeWindupTicks);
        }
        if (projectile.StrikeSweep)
            writer.WriteBoolean("strikeSweep", true);
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
            if (action.MovementFacingOverride is { } facingOverride)
            {
                writer.WriteString(
                    "movementFacingOverride",
                    Id(facingOverride));
            }
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
        // Trailing additive optional field (#156): the historical clock
        // writes nothing, so every pre-existing contract keeps its exact
        // bytes, and pre-0.10.7 readers reject the property by design —
        // the accepted frozen-artifact consequence.
        if (tick.CooldownClock != ActorTickResolutionDefinition
                .CooldownClockKind.AdvancesOnlyWithAnArmedForm)
        {
            writer.WriteString("cooldownClock", Id(tick.CooldownClock));
        }
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
