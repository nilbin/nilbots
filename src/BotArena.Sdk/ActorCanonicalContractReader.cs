using System.Collections.Immutable;
using System.Globalization;
using System.Text;
#if BOTARENA_ACTOR_CONTRACTS
using JsonElement = BotArena.ActorContracts.ActorCanonicalJson.Node;
using JsonProperty = BotArena.ActorContracts.ActorCanonicalJson.Property;
using JsonValueKind = BotArena.ActorContracts.ActorCanonicalJson.Kind;
using MapContract = BotArena.ActorContracts.GenericActorMapContract;
using MatchContract = BotArena.ActorContracts.GenericActorResolvedMatchContract;
using RulesContract = BotArena.ActorContracts.GenericActorRulesContract;
using ContractDirection = BotArena.ActorContracts.Direction;
#else
using JsonElement = BotArena.Sdk.ActorCanonicalJson.Node;
using JsonProperty = BotArena.Sdk.ActorCanonicalJson.Property;
using JsonValueKind = BotArena.Sdk.ActorCanonicalJson.Kind;
using MapContract = BotArena.Sdk.GenericActorMapContract;
using MatchContract = BotArena.Sdk.GenericActorResolvedMatchContract;
using RulesContract = BotArena.Sdk.GenericActorRulesContract;
using ContractDirection = BotArena.Sdk.Direction;
#endif

#if BOTARENA_ACTOR_CONTRACTS
namespace BotArena.ActorContracts;
#else
namespace BotArena.Sdk;
#endif

/// <summary>
/// Strict syntax, profile, and fingerprint reader for the bounded canonical
/// JSON contract authored and semantically validated by the Engine host. It
/// never rewrites the contract, so fingerprints bind to exactly the same text
/// exposed through <c>CanonicalJson</c>.
/// </summary>
#if BOTARENA_ACTOR_CONTRACTS
internal static class ActorCanonicalContractReader
#else
public static class ActorCanonicalContractReader
#endif
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static MatchContract Parse(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        EnsureBoundedUtf8(canonicalJson);
        EnsureCompact(canonicalJson);

        JsonElement root = ActorCanonicalJson.Parse(
            canonicalJson,
            GenericActorContractVersions.MaxCanonicalContractDepth,
            GenericActorContractVersions
                .MaxCanonicalContractCollectionCount);
        ExactObject(
            root,
            "schemaVersion",
            "matchContractFingerprint",
            "capabilityVersions",
            "rules",
            "map",
            "format",
            "topology",
            "initialDeployment",
            "lifecycleAssignments",
            "participantRegionAssignments",
            "modeMapBinding");

        int schemaVersion = Int(root, "schemaVersion");
        RequireVersion(
            schemaVersion,
            GenericActorContractVersions.MatchContractSchemaVersion,
            "match contract");
        string fingerprint = Fingerprint(
            root,
            "matchContractFingerprint");
        MatchContract.CapabilityVersionSet capabilities =
            ReadCapabilities(Property(root, "capabilityVersions"));
        RulesContract rules = ReadRules(Property(root, "rules"));
        MapContract map = ReadMap(Property(root, "map"));
        MatchContract.MatchFormat format =
            ReadFormat(Property(root, "format"));
        MatchContract.MatchTopology topology =
            ReadTopology(Property(root, "topology"));
        MatchContract.Deployment deployment =
            ReadDeployment(Property(root, "initialDeployment"));
        ImmutableArray<MatchContract.LifecycleAssignment>
            lifecycleAssignments = Array(
                Property(root, "lifecycleAssignments"),
                ReadLifecycleAssignment);
        ImmutableArray<MatchContract.ParticipantRegionAssignment>
            regionAssignments = Array(
                Property(root, "participantRegionAssignments"),
                ReadRegionAssignment);
        MatchContract.ModeMapBindingDefinition modeMapBinding =
            ReadModeMapBinding(Property(root, "modeMapBinding"));

        VerifyFingerprint(
            Property(root, "rules"),
            rules.RulesFingerprint,
            "rules",
            "rulesetId",
            "rulesFingerprint");
        VerifyFingerprint(
            Property(root, "map"),
            map.MapFingerprint,
            "map",
            "mapId",
            "mapVersion",
            "mapFingerprint");
        VerifyFingerprint(
            Property(root, "format"),
            format.FormatFingerprint,
            "format",
            "formatId",
            "formatFingerprint");
        VerifyFingerprint(
            Property(root, "topology"),
            topology.TopologyFingerprint,
            "topology",
            "topologyFingerprint");
        VerifyFingerprint(
            root,
            fingerprint,
            "match contract",
            "matchContractFingerprint");

        ValidateStructuralConsistency(
            schemaVersion,
            capabilities,
            rules,
            map,
            format,
            topology,
            deployment,
            modeMapBinding);

        return new MatchContract(
            canonicalJson,
            schemaVersion,
            fingerprint,
            capabilities,
            rules,
            map,
            format,
            topology,
            deployment,
            lifecycleAssignments,
            regionAssignments,
            modeMapBinding);
    }

    public static MatchContract ParseUtf8(ReadOnlySpan<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length
            > GenericActorContractVersions.MaxCanonicalContractBytes)
        {
            throw new FormatException(
                "The canonical actor contract exceeds the profile limit.");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(canonicalUtf8);
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException(
                "The canonical actor contract is not valid UTF-8.",
                exception);
        }
        return Parse(text);
    }

    private static MatchContract.CapabilityVersionSet ReadCapabilities(
        JsonElement element)
    {
        ExactObject(
            element,
            "contractProfileId",
            "runtimeProtocolVersion",
            "runtimeConfigurationVersion",
            "runtimeContractVersion",
            "matchStartSchemaVersion",
            "observationSchemaVersion",
            "decisionSchemaVersion",
            "matchContractSchemaVersion");

        var result = new MatchContract.CapabilityVersionSet(
            Id(element, "contractProfileId"),
            Text(element, "runtimeProtocolVersion"),
            Text(element, "runtimeConfigurationVersion"),
            Int(element, "runtimeContractVersion"),
            Int(element, "matchStartSchemaVersion"),
            Int(element, "observationSchemaVersion"),
            Int(element, "decisionSchemaVersion"),
            Int(element, "matchContractSchemaVersion"));

        if (result.ContractProfileId
                != GenericActorContractVersions.ContractProfileId
            || result.RuntimeProtocolVersion
                != GenericActorContractVersions.RuntimeProtocolVersion
            || result.RuntimeConfigurationVersion
                != GenericActorContractVersions.RuntimeConfigurationVersion
            || result.RuntimeContractVersion
                != GenericActorContractVersions.RuntimeContractVersion
            || result.MatchStartSchemaVersion
                != GenericActorContractVersions.MatchStartSchemaVersion
            || result.ObservationSchemaVersion
                != GenericActorContractVersions.ObservationSchemaVersion
            || result.DecisionSchemaVersion
                != GenericActorContractVersions.DecisionSchemaVersion
            || result.MatchContractSchemaVersion
                != GenericActorContractVersions.MatchContractSchemaVersion)
        {
            throw Unsupported(
                "capabilityVersions",
                "The capability tuple is not the negotiated generic-v2 profile.");
        }
        return result;
    }

    private static MapContract ReadMap(JsonElement element)
    {
        ExactObject(
            element,
            "schemaVersion",
            "mapId",
            "mapVersion",
            "mapFingerprint",
            "formatVersion",
            "width",
            "height",
            "tileRows",
            "spawnAnchors",
            "regions",
            "tileTags");
        int schemaVersion = Int(element, "schemaVersion");
        RequireVersion(schemaVersion, 1, "map manifest");
        int formatVersion = Int(element, "formatVersion");
        RequireVersion(formatVersion, 3, "map format");

        return new MapContract(
            schemaVersion,
            Id(element, "mapId"),
            Int(element, "mapVersion"),
            Fingerprint(element, "mapFingerprint"),
            formatVersion,
            Int(element, "width"),
            Int(element, "height"),
            Array(Property(element, "tileRows"), Text),
            Array(Property(element, "spawnAnchors"), ReadSpawnAnchor),
            Array(Property(element, "regions"), ReadRegion),
            Array(Property(element, "tileTags"), ReadTileTag));
    }

    private static MapContract.SpawnAnchor ReadSpawnAnchor(JsonElement element)
    {
        ExactObject(
            element,
            "spawnId",
            "position",
            "facing",
            "compatibleMovementLayers");
        return new MapContract.SpawnAnchor(
            Id(element, "spawnId"),
            Position(Property(element, "position")),
            Direction(element, "facing"),
            Array(
                Property(element, "compatibleMovementLayers"),
                MovementLayer));
    }

    private static MapContract.Region ReadRegion(JsonElement element)
    {
        ExactObject(element, "regionId", "kind", "tiles");
        return new MapContract.Region(
            Id(element, "regionId"),
            RegionKind(element, "kind"),
            Array(Property(element, "tiles"), Position));
    }

    private static MapContract.TileTag ReadTileTag(JsonElement element)
    {
        ExactObject(element, "tagId", "kind", "tiles");
        return new MapContract.TileTag(
            Id(element, "tagId"),
            TileTagKind(element, "kind"),
            Array(Property(element, "tiles"), Position));
    }

    private static MatchContract.MatchFormat ReadFormat(JsonElement element)
    {
        ExactObject(
            element,
            "schemaVersion",
            "formatId",
            "formatFingerprint",
            "kind",
            "scoringTeamCount",
            "participantsPerTeam",
            "participantCount");
        int schemaVersion = Int(element, "schemaVersion");
        RequireVersion(schemaVersion, 1, "format manifest");
        return new MatchContract.MatchFormat(
            schemaVersion,
            Id(element, "formatId"),
            Fingerprint(element, "formatFingerprint"),
            MatchFormatKind(element, "kind"),
            Int(element, "scoringTeamCount"),
            Int(element, "participantsPerTeam"),
            Int(element, "participantCount"));
    }

    private static MatchContract.MatchTopology ReadTopology(
        JsonElement element)
    {
        ExactObject(
            element,
            "schemaVersion",
            "topologyFingerprint",
            "counts",
            "teams",
            "participants",
            "unitSlots",
            "initialLives");
        int schemaVersion = Int(element, "schemaVersion");
        RequireVersion(schemaVersion, 1, "topology manifest");
        JsonElement counts = Property(element, "counts");
        ExactObject(
            counts,
            "teamCount",
            "participantCount",
            "unitSlotCount",
            "initialLifeCount");

        return new MatchContract.MatchTopology(
            schemaVersion,
            Fingerprint(element, "topologyFingerprint"),
            new MatchContract.TopologyCounts(
                Int(counts, "teamCount"),
                Int(counts, "participantCount"),
                Int(counts, "unitSlotCount"),
                Int(counts, "initialLifeCount")),
            Array(Property(element, "teams"), ReadTeam),
            Array(Property(element, "participants"), ReadParticipant),
            Array(Property(element, "unitSlots"), ReadUnitSlot),
            Array(Property(element, "initialLives"), ReadInitialLife));
    }

    private static PublicScoringTeam ReadTeam(JsonElement element)
    {
        ExactObject(element, "teamId");
        return new PublicScoringTeam(Int(element, "teamId"));
    }

    private static PublicParticipant ReadParticipant(JsonElement element)
    {
        ExactObject(element, "participantId", "teamId");
        return new PublicParticipant(
            Int(element, "participantId"),
            Int(element, "teamId"));
    }

    private static PublicUnitSlot ReadUnitSlot(JsonElement element)
    {
        ExactObject(
            element,
            "teamId",
            "unitId",
            "controllerParticipantId");
        return new PublicUnitSlot(
            Int(element, "teamId"),
            Int(element, "unitId"),
            Int(element, "controllerParticipantId"));
    }

    private static PublicInitialLife ReadInitialLife(JsonElement element)
    {
        ExactObject(element, "teamId", "unitId", "lifeId", "formId");
        return new PublicInitialLife(
            Int(element, "teamId"),
            Int(element, "unitId"),
            Int(element, "lifeId"),
            Id(element, "formId"));
    }

    private static MatchContract.Deployment ReadDeployment(
        JsonElement element)
    {
        ExactObject(element, "spawns", "lives");
        return new MatchContract.Deployment(
            Array(Property(element, "spawns"), ReadInitialSpawn),
            Array(Property(element, "lives"), ReadInitialDeploymentLife));
    }

    private static MatchContract.InitialSpawn ReadInitialSpawn(
        JsonElement element)
    {
        ExactObject(element, "spawnId", "position", "facing");
        return new MatchContract.InitialSpawn(
            Id(element, "spawnId"),
            Position(Property(element, "position")),
            Direction(element, "facing"));
    }

    private static MatchContract.InitialLifeDeployment
        ReadInitialDeploymentLife(JsonElement element)
    {
        ExactObject(
            element,
            "teamId",
            "unitId",
            "lifeId",
            "formId",
            "spawnId");
        return new MatchContract.InitialLifeDeployment(
            Int(element, "teamId"),
            Int(element, "unitId"),
            Int(element, "lifeId"),
            Id(element, "formId"),
            Id(element, "spawnId"));
    }

    private static MatchContract.LifecycleAssignment ReadLifecycleAssignment(
        JsonElement element)
    {
        ExactObject(
            element,
            "teamId",
            "unitId",
            "lifecycleProfileId",
            "initialGeneration",
            "allowedFormIds",
            "initialAvailability",
            "unlockTick",
            "assignedRespawnSpawnId");
        return new MatchContract.LifecycleAssignment(
            Int(element, "teamId"),
            Int(element, "unitId"),
            Id(element, "lifecycleProfileId"),
            NullableInt(element, "initialGeneration"),
            Array(Property(element, "allowedFormIds"), Id),
            InitialAvailability(element, "initialAvailability"),
            NullableInt(element, "unlockTick"),
            NullableId(element, "assignedRespawnSpawnId"));
    }

    private static MatchContract.ParticipantRegionAssignment
        ReadRegionAssignment(JsonElement element)
    {
        ExactObject(
            element,
            "participantId",
            "regionRoleId",
            "mapRegionId",
            "facing");
        return new MatchContract.ParticipantRegionAssignment(
            Int(element, "participantId"),
            Id(element, "regionRoleId"),
            Id(element, "mapRegionId"),
            Direction(element, "facing"));
    }

    private static MatchContract.ModeMapBindingDefinition ReadModeMapBinding(
        JsonElement element)
    {
        string kind = PeekString(element, "kind");
        switch (kind)
        {
            case "deathmatch":
                ExactObject(element, "kind");
                return new MatchContract.DeathmatchModeMapBinding();
            case "frontline":
                ExactObject(
                    element,
                    "kind",
                    "orderedObjectiveRegionIds",
                    "teamAdvances");
                return new MatchContract.FrontlineModeMapBinding(
                    Array(
                        Property(element, "orderedObjectiveRegionIds"),
                        Id),
                    Array(
                        Property(element, "teamAdvances"),
                        ReadTeamAdvance));
            default:
                throw Unsupported("modeMapBinding.kind", kind);
        }
    }

    private static MatchContract.FrontlineTeamAdvance ReadTeamAdvance(
        JsonElement element)
    {
        ExactObject(
            element,
            "teamId",
            "direction",
            "objectiveIndexDelta");
        return new MatchContract.FrontlineTeamAdvance(
            Int(element, "teamId"),
            ObjectiveAdvanceDirection(element, "direction"),
            Int(element, "objectiveIndexDelta"));
    }

    private static RulesContract ReadRules(JsonElement element)
    {
        ExactObject(
            element,
            "schemaVersion",
            "rulesetId",
            "rulesFingerprint",
            "limits",
            "seedMechanics",
            "gameMode",
            "lifecycle",
            "forms",
            "movementProfiles",
            "visionProfiles",
            "attackProfiles",
            "actions",
            "fabricationTransitions",
            "sameLifeTransitions",
            "replicationTransitions",
            "teamPerception",
            "collisions",
            "tickResolution");
        int schemaVersion = Int(element, "schemaVersion");
        RequireVersion(schemaVersion, 3, "rules");
        return new RulesContract(
            schemaVersion,
            Id(element, "rulesetId"),
            Fingerprint(element, "rulesFingerprint"),
            ReadLimits(Property(element, "limits")),
            ReadSeedMechanics(Property(element, "seedMechanics")),
            ReadGameMode(Property(element, "gameMode")),
            ReadLifecycle(Property(element, "lifecycle")),
            Array(Property(element, "forms"), ReadForm),
            Array(
                Property(element, "movementProfiles"),
                ReadMovementProfile),
            Array(Property(element, "visionProfiles"), ReadVisionProfile),
            Array(Property(element, "attackProfiles"), ReadAttackProfile),
            Array(Property(element, "actions"), ReadAction),
            Array(
                Property(element, "fabricationTransitions"),
                ReadFabricationTransition),
            Array(
                Property(element, "sameLifeTransitions"),
                ReadSameLifeTransition),
            Array(
                Property(element, "replicationTransitions"),
                ReadReplicationTransition),
            ReadTeamPerception(Property(element, "teamPerception")),
            ReadCollisions(Property(element, "collisions")),
            ReadTickResolution(Property(element, "tickResolution")));
    }

    private static RulesContract.RulesLimits ReadLimits(JsonElement element)
    {
        ExactObject(element, "maxTicks", "runtimeFaults");
        return new RulesContract.RulesLimits(
            Int(element, "maxTicks"),
            ReadRuntimeFaults(Property(element, "runtimeFaults")));
    }

    private static RulesContract.RuntimeFaults ReadRuntimeFaults(
        JsonElement element)
    {
        ExactObject(
            element,
            "faultsAllowedBeforeDisqualification",
            "disqualificationFaultCount",
            "accumulationScope",
            "faultCounterArithmetic",
            "faultingDecision",
            "runtimeStageRecovery",
            "replayFaultRepresentation",
            "faultBatchEventOrder",
            "applicationTiming",
            "threshold",
            "participantDisposition",
            "pendingWorkDisposition",
            "cancellationEventOrder",
            "ownedProjectileDisposition",
            "scoreDisposition",
            "scoringTeamEligibility",
            "matchCompletion",
            "finalRanking");
        return new RulesContract.RuntimeFaults(
            Int(element, "faultsAllowedBeforeDisqualification"),
            DecimalInt64String(element, "disqualificationFaultCount"),
            Semantic(element, "accumulationScope"),
            Semantic(element, "faultCounterArithmetic"),
            Semantic(element, "faultingDecision"),
            Semantic(element, "runtimeStageRecovery"),
            Semantic(element, "replayFaultRepresentation"),
            Semantic(element, "faultBatchEventOrder"),
            Semantic(element, "applicationTiming"),
            Semantic(element, "threshold"),
            Semantic(element, "participantDisposition"),
            Semantic(element, "pendingWorkDisposition"),
            Semantic(element, "cancellationEventOrder"),
            Semantic(element, "ownedProjectileDisposition"),
            Semantic(element, "scoreDisposition"),
            Semantic(element, "scoringTeamEligibility"),
            Semantic(element, "matchCompletion"),
            Semantic(element, "finalRanking"));
    }

    private static RulesContract.SeedMechanicsDefinition ReadSeedMechanics(
        JsonElement element)
    {
        ExactObject(
            element,
            "seedProfileId",
            "seedDerivation",
            "lifeIdentityAssignment",
            "runtimeLifetime",
            "privateMemory");
        return new RulesContract.SeedMechanicsDefinition(
            Id(element, "seedProfileId"),
            Semantic(element, "seedDerivation"),
            Semantic(element, "lifeIdentityAssignment"),
            Semantic(element, "runtimeLifetime"),
            Semantic(element, "privateMemory"));
    }

    private static RulesContract.GameModeDefinition ReadGameMode(
        JsonElement element)
    {
        string kind = PeekString(element, "kind");
        return kind switch
        {
            "deathmatch" => ReadDeathmatchMode(element),
            "frontline" => ReadFrontlineMode(element),
            _ => throw Unsupported("gameMode.kind", kind),
        };
    }

    private static RulesContract.DeathmatchGameMode ReadDeathmatchMode(
        JsonElement element)
    {
        ExactObject(
            element,
            "kind",
            "modeId",
            "victory",
            "scoreCatalog",
            "scoring");
        RulesContract.Victory victory =
            ReadVictory(Property(element, "victory"));
        if (victory is not RulesContract.DeathmatchVictory deathmatchVictory)
        {
            throw new FormatException(
                "Deathmatch gameMode requires deathmatch victory.");
        }
        return new RulesContract.DeathmatchGameMode(
            Id(element, "modeId"),
            deathmatchVictory,
            Array(Property(element, "scoreCatalog"), ReadScoreChannel),
            ReadDeathmatchScoring(Property(element, "scoring")));
    }

    private static RulesContract.FrontlineGameMode ReadFrontlineMode(
        JsonElement element)
    {
        ExactObject(
            element,
            "kind",
            "modeId",
            "victory",
            "scoreCatalog",
            "frontlinePositionCount",
            "capture");
        RulesContract.Victory victory =
            ReadVictory(Property(element, "victory"));
        if (victory is not RulesContract.FrontlineVictory frontlineVictory)
        {
            throw new FormatException(
                "Frontline gameMode requires frontline victory.");
        }
        return new RulesContract.FrontlineGameMode(
            Id(element, "modeId"),
            frontlineVictory,
            Array(Property(element, "scoreCatalog"), ReadScoreChannel),
            Int(element, "frontlinePositionCount"),
            ReadFrontlineCapture(Property(element, "capture")));
    }

    private static RulesContract.Victory ReadVictory(JsonElement element)
    {
        string kind = PeekString(element, "kind");
        switch (kind)
        {
            case "deathmatch":
                ExactObject(
                    element,
                    "kind",
                    "timeoutRanking",
                    "killsToWin",
                    "terminalTickPrecedence");
                return new RulesContract.DeathmatchVictory(
                    Array(
                        Property(element, "timeoutRanking"),
                        ReadScoreRanking),
                    NullableInt(element, "killsToWin"),
                    Semantic(element, "terminalTickPrecedence"));
            case "frontline":
                ExactObject(
                    element,
                    "kind",
                    "timeoutRanking",
                    "pushesToBreach");
                return new RulesContract.FrontlineVictory(
                    Array(
                        Property(element, "timeoutRanking"),
                        ReadScoreRanking),
                    Int(element, "pushesToBreach"));
            default:
                throw Unsupported("victory.kind", kind);
        }
    }

    private static RulesContract.ScoreChannel ReadScoreChannel(
        JsonElement element)
    {
        ExactObject(element, "channel", "domain");
        return new RulesContract.ScoreChannel(
            ScoreChannel(element, "channel"),
            EnumId(
                element,
                "domain",
                "non-negative",
                "signed"));
    }

    private static RulesContract.ScoreRanking ReadScoreRanking(
        JsonElement element)
    {
        ExactObject(element, "channel", "direction");
        return new RulesContract.ScoreRanking(
            ScoreChannel(element, "channel"),
            EnumId(
                element,
                "direction",
                "higher-wins",
                "lower-wins"));
    }

    private static RulesContract.DeathmatchScoring ReadDeathmatchScoring(
        JsonElement element)
    {
        ExactObject(
            element,
            "deathIncrement",
            "killIncrement",
            "alliedFinalDamage",
            "damageDealtIncrement",
            "activeHealthSnapshot",
            "nonDamageRetirement",
            "earlyKillLimitResolution");
        return new RulesContract.DeathmatchScoring(
            Semantic(element, "deathIncrement"),
            Semantic(element, "killIncrement"),
            Semantic(element, "alliedFinalDamage"),
            Semantic(element, "damageDealtIncrement"),
            Semantic(element, "activeHealthSnapshot"),
            Semantic(element, "nonDamageRetirement"),
            Semantic(element, "earlyKillLimitResolution"));
    }

    private static RulesContract.FrontlineCapture ReadFrontlineCapture(
        JsonElement element)
    {
        bool hasGainSchedule =
            element.TryGetProperty("gainSchedule", out JsonElement schedule);
        // Additive optional field, exactly like the capture-gain schedule and
        // the movement profile's facing coupling: the canonical writer emits
        // a hold duration only for the high-water-mark redeploy policy, so an
        // absent field means "no ratchet" and an explicitly inert zero is a
        // second, non-canonical encoding of the same contract.
        bool hasRatchetHold = element.TryGetProperty(
            "ratchetHoldTicks",
            out JsonElement ratchetHoldTicks);
        ExactObject(
            element,
            [
                "threshold",
                "gainPerSoleTeamTick",
                .. hasGainSchedule ? new[] { "gainSchedule" } : [],
                "decayAmount",
                "decayIntervalTicks",
                "redeployPauseTicks",
                "controlPolicy",
                "timeoutPolicy",
                "territorialProgressFormula",
                "completionPolicy",
                "initialPosition",
                "captureArithmetic",
                "oppositionArithmetic",
                "decayClock",
                "disabledDecay",
                "redeployPolicy",
                .. hasRatchetHold ? new[] { "ratchetHoldTicks" } : [],
                "redeployTickArithmetic",
            ]);
        int hold = hasRatchetHold ? Int(element, "ratchetHoldTicks") : 0;
        bool ratchetPolicy = string.Equals(
            Semantic(element, "redeployPolicy"),
            RatchetRedeployPolicyId,
            StringComparison.Ordinal);
        if (hasRatchetHold != ratchetPolicy || hasRatchetHold && hold <= 0)
        {
            throw new FormatException(
                "A canonical Frontline capture carries a positive ratchetHoldTicks exactly when its redeploy policy holds a high-water mark, and omits it otherwise.");
        }
        return new RulesContract.FrontlineCapture(
            Int(element, "threshold"),
            Int(element, "gainPerSoleTeamTick"),
            Int(element, "decayAmount"),
            Int(element, "decayIntervalTicks"),
            Int(element, "redeployPauseTicks"),
            Semantic(element, "controlPolicy"),
            Semantic(element, "timeoutPolicy"),
            Semantic(element, "territorialProgressFormula"),
            Semantic(element, "completionPolicy"),
            Semantic(element, "initialPosition"),
            Semantic(element, "captureArithmetic"),
            Semantic(element, "oppositionArithmetic"),
            Semantic(element, "decayClock"),
            Semantic(element, "disabledDecay"),
            Semantic(element, "redeployPolicy"),
            Semantic(element, "redeployTickArithmetic"))
        {
            GainSchedule = hasGainSchedule
                ? Array(schedule, ReadFrontlineCaptureGainPhase)
                : [],
            RatchetHoldTicks = hold,
        };
    }

    /// <summary>
    /// The one redeploy policy that owns a hold duration. Named here so the
    /// mirror can reject both halves of the inert encoding.
    /// </summary>
    private const string RatchetRedeployPolicyId =
        "advance-immediately-then-deny-enemy-regression-past-the-high-water-mark-through-configured-hold-ticks";

    private static RulesContract.FrontlineCaptureGainPhase
        ReadFrontlineCaptureGainPhase(JsonElement element)
    {
        ExactObject(
            element,
            "phaseId",
            "startsAtTick",
            "gainPerSoleTeamTick");
        return new RulesContract.FrontlineCaptureGainPhase(
            Id(element, "phaseId"),
            Int(element, "startsAtTick"),
            Int(element, "gainPerSoleTeamTick"));
    }

    private static RulesContract.LifecycleDefinition ReadLifecycle(
        JsonElement element)
    {
        ExactObject(
            element,
            "profiles",
            "destructionClock",
            "newLifeSemantics",
            "newLifeCombatState",
            "newLifeResourceClock",
            "generationSemantics",
            "automaticReturnPlacement",
            "tickStartLifecycleOrder",
            "outputTileProjectile");
        return new RulesContract.LifecycleDefinition(
            Array(Property(element, "profiles"), ReadLifecycleProfile),
            Semantic(element, "destructionClock"),
            Semantic(element, "newLifeSemantics"),
            Semantic(element, "newLifeCombatState"),
            Semantic(element, "newLifeResourceClock"),
            Semantic(element, "generationSemantics"),
            Semantic(element, "automaticReturnPlacement"),
            Semantic(element, "tickStartLifecycleOrder"),
            Semantic(element, "outputTileProjectile"));
    }

    private static RulesContract.LifecycleProfile ReadLifecycleProfile(
        JsonElement element)
    {
        ExactObject(
            element,
            "profileId",
            "destructionPolicy",
            "delayTicks",
            "automaticReturnFormId");
        return new RulesContract.LifecycleProfile(
            Id(element, "profileId"),
            EnumId(
                element,
                "destructionPolicy",
                "automatic-respawn",
                "ready-for-explicit-fabrication",
                "permanently-dormant"),
            Int(element, "delayTicks"),
            NullableId(element, "automaticReturnFormId"));
    }

    private static RulesContract.Form ReadForm(JsonElement element)
    {
        // Additive optional field, exactly like the movement profile's facing
        // coupling: the canonical writer omits it while the form declares no
        // projectile guard, so an absent property means None and an
        // explicitly-inert "none" is a second, non-canonical encoding.
        bool hasProjectileGuard = element.TryGetProperty(
            "projectileGuard",
            out JsonElement projectileGuard);
        ExactObject(
            element,
            [
                "id",
                "maxHealth",
                "movementProfileId",
                "visionProfileId",
                "attackProfileId",
                "objectiveWeight",
                .. hasProjectileGuard ? new[] { "projectileGuard" } : [],
                "allowedActionIds",
            ]);
        return new RulesContract.Form(
            Id(element, "id"),
            Int(element, "maxHealth"),
            Id(element, "movementProfileId"),
            Id(element, "visionProfileId"),
            NullableId(element, "attackProfileId"),
            Int(element, "objectiveWeight"),
            Array(Property(element, "allowedActionIds"), Id))
        {
            ProjectileGuard = hasProjectileGuard
                ? FormProjectileGuard(projectileGuard)
                : RulesContract.FormProjectileGuard.None,
        };
    }

    private static RulesContract.FormProjectileGuard FormProjectileGuard(
        JsonElement element)
    {
        string value = element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw new FormatException(
                "A canonical form projectile guard must be a string.");
        return value switch
        {
            "facing-quadrant-contacts-deflected" =>
                RulesContract.FormProjectileGuard
                    .FacingQuadrantContactsDeflected,
            _ => throw new FormatException(
                "A canonical form omits projectileGuard when it declares no "
                + "guard; an explicitly inert value is a second encoding of "
                + $"the same contract (read '{value}')."),
        };
    }

    private static RulesContract.MovementProfile ReadMovementProfile(
        JsonElement element)
    {
        // Additive optional field, exactly like the capture-gain schedule:
        // the canonical writer omits it while the profile preserves facing,
        // so an absent property means PreserveFacing rather than an error.
        bool hasFacingCoupling = element.TryGetProperty(
            "facingCoupling",
            out JsonElement facingCoupling);
        ExactObject(
            element,
            hasFacingCoupling
                ? ["id", "movementLayer", "facingCoupling"]
                : ["id", "movementLayer"]);
        return new RulesContract.MovementProfile(
            Id(element, "id"),
            MovementLayer(element, "movementLayer"))
        {
            FacingCoupling = hasFacingCoupling
                ? MovementFacingCoupling(facingCoupling)
                : RulesContract.MovementFacingCoupling.PreserveFacing,
        };
    }

    private static RulesContract.VisionProfile ReadVisionProfile(
        JsonElement element)
    {
        ExactObject(
            element,
            "id",
            "range",
            "distanceMetric",
            "shape",
            "omnidirectionalProximityRange",
            "lineOfSight",
            "hearingRadius",
            "hearingBearingSectors",
            "hearingBearingModel",
            "hearingDistanceBandModel",
            "hearingDistanceBandUpperBounds",
            "loudEventKinds");
        return new RulesContract.VisionProfile(
            Id(element, "id"),
            Int(element, "range"),
            EnumId(element, "distanceMetric", "chebyshev"),
            EnumId(
                element,
                "shape",
                "omnidirectional",
                "facing-quadrant"),
            Int(element, "omnidirectionalProximityRange"),
            EnumId(
                element,
                "lineOfSight",
                "corner-strict-supercover"),
            Int(element, "hearingRadius"),
            Int(element, "hearingBearingSectors"),
            EnumId(
                element,
                "hearingBearingModel",
                "disabled",
                "eight-octants-strict-two-to-one-cardinal-v1"),
            Semantic(element, "hearingDistanceBandModel"),
            Array(
                Property(element, "hearingDistanceBandUpperBounds"),
                Int),
            Array(
                Property(element, "loudEventKinds"),
                item => EnumValue(
                    item,
                    "attack",
                    "damage",
                    "destruction")));
    }

    private static RulesContract.AttackProfile ReadAttackProfile(
        JsonElement element)
    {
        // Additive optional field: a one-bolt attack carries no volley object,
        // so its absence means exactly one projectile and an emitted volley
        // with a count of one is a second encoding of the same contract.
        bool hasVolley = element.TryGetProperty(
            "volley",
            out JsonElement volley);
        ExactObject(
            element,
            [
                "id",
                "omnidirectionalAim",
                "aimInterpretation",
                "projectile",
                "cooldownTicks",
                "maxEnergy",
                "attackEnergyCost",
                "energyRegenerationIntervalTicks",
                "energyRegenerationAmount",
                "energyRegenerationClock",
                "energyUpdateOrder",
                "energyArithmetic",
                "attackAvailability",
                "cooldownUpdate",
                "shotProgram",
                .. hasVolley ? new[] { "volley" } : [],
            ]);
        RulesContract.ShotProgramDefinition shotProgram =
            ReadShotProgram(Property(element, "shotProgram"));
        RulesContract.AttackVolley? launch =
            hasVolley ? ReadVolley(volley) : null;
        if (launch is not null && shotProgram.Enabled)
        {
            throw new FormatException(
                "A canonical volley profile fires straight: programmed shots "
                + "and multi-projectile volleys are mutually exclusive.");
        }
        return BaseAttackProfile(element, shotProgram) with
        {
            Volley = launch,
        };
    }

    private static RulesContract.AttackVolley ReadVolley(JsonElement element)
    {
        ExactObject(element, "projectileCount", "spread", "identityOrder");
        int count = Int(element, "projectileCount");
        string spread = EnumId(
            element,
            "spread",
            "shared-resolved-heading",
            "symmetric-adjacent-heading-fan-ascending-signed-sector-offset");
        if (count < 2)
        {
            throw new FormatException(
                "A canonical attack volley launches at least two projectiles; "
                + "a single-bolt attack omits the volley entirely.");
        }
        if (spread
                == "symmetric-adjacent-heading-fan-ascending-signed-sector-offset"
            && count % 2 == 0)
        {
            throw new FormatException(
                "A canonical symmetric heading fan carries an odd projectile count.");
        }
        return new RulesContract.AttackVolley(
            count,
            spread,
            EnumId(
                element,
                "identityOrder",
                "contiguous-ascending-in-launch-order"));
    }

    private static RulesContract.AttackProfile BaseAttackProfile(
        JsonElement element,
        RulesContract.ShotProgramDefinition shotProgram)
    {
        return new RulesContract.AttackProfile(
            Id(element, "id"),
            Bool(element, "omnidirectionalAim"),
            EnumId(
                element,
                "aimInterpretation",
                "current-facing-straight",
                "current-facing-plus-relative-eight-way-shot-program",
                "absolute-submitted-eight-way-heading-facing-unchanged"),
            ReadProjectile(Property(element, "projectile")),
            Int(element, "cooldownTicks"),
            Int(element, "maxEnergy"),
            Int(element, "attackEnergyCost"),
            Int(element, "energyRegenerationIntervalTicks"),
            Int(element, "energyRegenerationAmount"),
            Semantic(element, "energyRegenerationClock"),
            Semantic(element, "energyUpdateOrder"),
            Semantic(element, "energyArithmetic"),
            Semantic(element, "attackAvailability"),
            Semantic(element, "cooldownUpdate"),
            shotProgram);
    }

    private static RulesContract.Projectile ReadProjectile(
        JsonElement element)
    {
        ExactObject(
            element,
            "mode",
            "damagePerHit",
            "maxTravelTiles",
            "ticksPerAdvance",
            "tilesPerAdvance",
            "launchTiles",
            "advancesOnLaunchTick",
            "damageAppliedSimultaneously",
            "diagonalCornersMustBeClear");
        return new RulesContract.Projectile(
            ProjectileMode(element, "mode"),
            Int(element, "damagePerHit"),
            Int(element, "maxTravelTiles"),
            Int(element, "ticksPerAdvance"),
            Int(element, "tilesPerAdvance"),
            Int(element, "launchTiles"),
            Bool(element, "advancesOnLaunchTick"),
            Bool(element, "damageAppliedSimultaneously"),
            Bool(element, "diagonalCornersMustBeClear"));
    }

    private static RulesContract.ShotProgramDefinition ReadShotProgram(
        JsonElement element)
    {
        ExactObject(
            element,
            "enabled",
            "headingSectors",
            "headingModel",
            "bendStepSectors",
            "minInitialAimSteps",
            "maxInitialAimSteps",
            "aimOnlyProgram",
            "allowedCurvedBendDirections",
            "minBendAfterTiles",
            "maxBendAfterTiles",
            "minBendEveryTiles",
            "maxBendEveryTiles",
            "minBendCount",
            "maxBendCount",
            "launchTiles",
            "payloadOptional",
            "defaultProgram",
            "invalidPayloadResult",
            "unsupportedPayloadResult",
            "diagonalCornersMustBeClear");
        return new RulesContract.ShotProgramDefinition(
            Bool(element, "enabled"),
            Int(element, "headingSectors"),
            EnumId(
                element,
                "headingModel",
                "eight-way-clockwise-modulo-v1"),
            Int(element, "bendStepSectors"),
            Int(element, "minInitialAimSteps"),
            Int(element, "maxInitialAimSteps"),
            ReadAimOnlyShotProgramValue(
                Property(element, "aimOnlyProgram")),
            Array(
                Property(element, "allowedCurvedBendDirections"),
                Int),
            Int(element, "minBendAfterTiles"),
            Int(element, "maxBendAfterTiles"),
            Int(element, "minBendEveryTiles"),
            Int(element, "maxBendEveryTiles"),
            Int(element, "minBendCount"),
            Int(element, "maxBendCount"),
            Int(element, "launchTiles"),
            Bool(element, "payloadOptional"),
            ReadShotProgramValue(Property(element, "defaultProgram")),
            NullableEnumId(
                element,
                "invalidPayloadResult",
                "blocked",
                "faulted",
                "rejected"),
            EnumId(
                element,
                "unsupportedPayloadResult",
                "blocked",
                "faulted",
                "rejected"),
            Bool(element, "diagonalCornersMustBeClear"));
    }

    private static RulesContract.ShotProgramValue ReadShotProgramValue(
        JsonElement element)
    {
        ExactObject(
            element,
            "initialAimOffset",
            "bendDirection",
            "bendAfterTiles",
            "bendEveryTiles",
            "bendCount");
        return new RulesContract.ShotProgramValue(
            Int(element, "initialAimOffset"),
            Int(element, "bendDirection"),
            Int(element, "bendAfterTiles"),
            Int(element, "bendEveryTiles"),
            Int(element, "bendCount"));
    }

    private static RulesContract.AimOnlyShotProgramValue
        ReadAimOnlyShotProgramValue(JsonElement element)
    {
        ExactObject(
            element,
            "bendDirection",
            "bendAfterTiles",
            "bendEveryTiles",
            "bendCount");
        return new RulesContract.AimOnlyShotProgramValue(
            Int(element, "bendDirection"),
            Int(element, "bendAfterTiles"),
            Int(element, "bendEveryTiles"),
            Int(element, "bendCount"));
    }

    private static RulesContract.ActionDefinition ReadAction(
        JsonElement element)
    {
        ExactObject(element, "id", "code", "kind", "parameterKinds");
        return new RulesContract.ActionDefinition(
            Id(element, "id"),
            Int(element, "code"),
            ActionKind(element, "kind"),
            Array(
                Property(element, "parameterKinds"),
                ActionParameterKind));
    }

    private static RulesContract.FabricationTransition
        ReadFabricationTransition(JsonElement element)
    {
        string kind = PeekString(element, "kind");
        if (kind != "bounded-child")
            throw Unsupported("fabricationTransitions[].kind", kind);

        ExactObject(
            element,
            "kind",
            "transitionId",
            "actionId",
            "sourceFormIds",
            "outputFormId",
            "outputCount",
            "sourceRegionRoleId",
            "outputRegionRoleId",
            "requiredSourceTileTags",
            "requiredOutputTileTags",
            "forbiddenOutputTileTags",
            "candidateOffsets",
            "delay",
            "unavailablePlacementResult",
            "targetSlot",
            "candidateSnapshot",
            "positionSelection",
            "claimScope",
            "conflictResolution",
            "sourceDisposition",
            "childInitialState",
            "outputFacing",
            "candidateReference",
            "lineage",
            "outputHealth",
            "spawnReason",
            "offsetArithmetic",
            "outstandingBundles");
        return new RulesContract.BoundedChildFabricationTransition(
            Id(element, "transitionId"),
            Id(element, "actionId"),
            Array(Property(element, "sourceFormIds"), Id),
            Id(element, "outputFormId"),
            Int(element, "outputCount"),
            Id(element, "sourceRegionRoleId"),
            Id(element, "outputRegionRoleId"),
            Array(
                Property(element, "requiredSourceTileTags"),
                TileTagKind),
            Array(
                Property(element, "requiredOutputTileTags"),
                TileTagKind),
            Array(
                Property(element, "forbiddenOutputTileTags"),
                TileTagKind),
            Array(
                Property(element, "candidateOffsets"),
                ReadOffset),
            ReadFabricationDelay(Property(element, "delay")),
            EnumId(
                element,
                "unavailablePlacementResult",
                "blocked",
                "faulted",
                "rejected"),
            Semantic(element, "targetSlot"),
            Semantic(element, "candidateSnapshot"),
            Semantic(element, "positionSelection"),
            Semantic(element, "claimScope"),
            Semantic(element, "conflictResolution"),
            Semantic(element, "sourceDisposition"),
            Semantic(element, "childInitialState"),
            Semantic(element, "outputFacing"),
            Semantic(element, "candidateReference"),
            Semantic(element, "lineage"),
            Semantic(element, "outputHealth"),
            Semantic(element, "spawnReason"),
            Semantic(element, "offsetArithmetic"),
            Semantic(element, "outstandingBundles"));
    }

    private static RulesContract.FabricationDelay ReadFabricationDelay(
        JsonElement element)
    {
        ExactObject(
            element,
            "durationTicks",
            "sourceBehavior",
            "sourceDeath",
            "sourceRetirement",
            "reservation",
            "completion",
            "tickArithmetic");
        return new RulesContract.FabricationDelay(
            Int(element, "durationTicks"),
            Semantic(element, "sourceBehavior"),
            Semantic(element, "sourceDeath"),
            Semantic(element, "sourceRetirement"),
            Semantic(element, "reservation"),
            Semantic(element, "completion"),
            Semantic(element, "tickArithmetic"));
    }

    private static RulesContract.SameLifeTransition ReadSameLifeTransition(
        JsonElement element)
    {
        string kind = PeekString(element, "kind");
        if (kind != "form-transition")
            throw Unsupported("sameLifeTransitions[].kind", kind);

        ExactObject(
            element,
            "kind",
            "transitionId",
            "actionId",
            "sourceFormId",
            "targetFormId",
            "windup",
            "memoryContinuity",
            "health",
            "combatState",
            "placement",
            "irreversibleForLife");
        return new RulesContract.FormTransition(
            Id(element, "transitionId"),
            Id(element, "actionId"),
            Id(element, "sourceFormId"),
            Id(element, "targetFormId"),
            ReadWindup(Property(element, "windup")),
            Semantic(element, "memoryContinuity"),
            ReadSameLifeHealth(Property(element, "health")),
            ReadSameLifeCombatState(Property(element, "combatState")),
            ReadSameLifePlacement(Property(element, "placement")),
            Bool(element, "irreversibleForLife"));
    }

    private static RulesContract.TransitionWindup ReadWindup(
        JsonElement element)
    {
        ExactObject(
            element,
            "durationTicks",
            "pendingAction",
            "sourceForm",
            "targetability",
            "lethalDamage",
            "completion",
            "placementReference");
        return new RulesContract.TransitionWindup(
            Int(element, "durationTicks"),
            Semantic(element, "pendingAction"),
            Semantic(element, "sourceForm"),
            Semantic(element, "targetability"),
            Semantic(element, "lethalDamage"),
            Semantic(element, "completion"),
            Semantic(element, "placementReference"));
    }

    private static RulesContract.SameLifeHealth ReadSameLifeHealth(
        JsonElement element)
    {
        ExactObject(
            element,
            "policy",
            "flatHealthGain",
            "evaluation",
            "arithmetic",
            "preserveRatioFormula");
        return new RulesContract.SameLifeHealth(
            EnumId(
                element,
                "policy",
                "preserve-current-capped-to-target-maximum",
                "add-flat-capped-to-target-maximum",
                "set-to-target-maximum",
                "preserve-ratio-floor-minimum-one"),
            Int(element, "flatHealthGain"),
            Semantic(element, "evaluation"),
            Semantic(element, "arithmetic"),
            Semantic(element, "preserveRatioFormula"));
    }

    private static RulesContract.SameLifeCombatState ReadSameLifeCombatState(
        JsonElement element)
    {
        ExactObject(element, "cooldownContinuity", "energyContinuity");
        return new RulesContract.SameLifeCombatState(
            Semantic(element, "cooldownContinuity"),
            Semantic(element, "energyContinuity"));
    }

    private static RulesContract.SameLifePlacement ReadSameLifePlacement(
        JsonElement element)
    {
        ExactObject(
            element,
            "positionContinuity",
            "legalityEvaluation",
            "requiredTileTags",
            "forbiddenTileTags",
            "failedCompletion");
        return new RulesContract.SameLifePlacement(
            Semantic(element, "positionContinuity"),
            Semantic(element, "legalityEvaluation"),
            Array(Property(element, "requiredTileTags"), TileTagKind),
            Array(Property(element, "forbiddenTileTags"), TileTagKind),
            Semantic(element, "failedCompletion"));
    }

    private static RulesContract.ReplicationTransition
        ReadReplicationTransition(JsonElement element)
    {
        string kind = PeekString(element, "kind");
        if (kind != "split")
            throw Unsupported("replicationTransitions[].kind", kind);

        ExactObject(
            element,
            "kind",
            "transitionId",
            "actionId",
            "sourceFormIds",
            "outputFormId",
            "descendantCount",
            "maxSourceGeneration",
            "requireNoPriorSameLifeTransition",
            "health",
            "minimumSourceHealth",
            "candidateOffsets",
            "windup",
            "descendantGenerationIncrement",
            "reservationIsAtomic",
            "conflictingBundlesAllBlock",
            "insufficientHealthBlocks",
            "reuseSourceSlotFirst",
            "additionalSlotsUseLowestCompatibleDormantId",
            "sourceRemainsTargetableUntilCompletion",
            "lethalSourceDamageCancels",
            "sourceRetirementCountsAsDestruction",
            "descendantsUseFreshIsolatedRuntimes",
            "descendantsInheritPrivateMemory",
            "candidateSnapshot",
            "positionSelection",
            "slotSelection",
            "descendantAssignment",
            "claimScope",
            "conflictResolution",
            "healthEvaluation",
            "offsetArithmetic");
        return new RulesContract.SplitReplicationTransition(
            Id(element, "transitionId"),
            Id(element, "actionId"),
            Array(Property(element, "sourceFormIds"), Id),
            Id(element, "outputFormId"),
            Int(element, "descendantCount"),
            Int(element, "maxSourceGeneration"),
            Bool(element, "requireNoPriorSameLifeTransition"),
            ReadReplicationHealth(Property(element, "health")),
            Int(element, "minimumSourceHealth"),
            Array(
                Property(element, "candidateOffsets"),
                ReadOffset),
            ReadWindup(Property(element, "windup")),
            Int(element, "descendantGenerationIncrement"),
            Bool(element, "reservationIsAtomic"),
            Bool(element, "conflictingBundlesAllBlock"),
            Bool(element, "insufficientHealthBlocks"),
            Bool(element, "reuseSourceSlotFirst"),
            Bool(
                element,
                "additionalSlotsUseLowestCompatibleDormantId"),
            Bool(
                element,
                "sourceRemainsTargetableUntilCompletion"),
            Bool(element, "lethalSourceDamageCancels"),
            Bool(
                element,
                "sourceRetirementCountsAsDestruction"),
            Bool(
                element,
                "descendantsUseFreshIsolatedRuntimes"),
            Bool(element, "descendantsInheritPrivateMemory"),
            Semantic(element, "candidateSnapshot"),
            Semantic(element, "positionSelection"),
            Semantic(element, "slotSelection"),
            Semantic(element, "descendantAssignment"),
            Semantic(element, "claimScope"),
            Semantic(element, "conflictResolution"),
            Semantic(element, "healthEvaluation"),
            Semantic(element, "offsetArithmetic"));
    }

    private static RulesContract.ReplicationHealth ReadReplicationHealth(
        JsonElement element)
    {
        ExactObject(
            element,
            "distribution",
            "minimumHealthPerDescendant",
            "remainder",
            "maximumHealth");
        return new RulesContract.ReplicationHealth(
            Semantic(element, "distribution"),
            Int(element, "minimumHealthPerDescendant"),
            Semantic(element, "remainder"),
            Semantic(element, "maximumHealth"));
    }

    private static RulesContract.RelativePositionOffset ReadOffset(
        JsonElement element)
    {
        ExactObject(element, "forward", "right");
        return new RulesContract.RelativePositionOffset(
            Int(element, "forward"),
            Int(element, "right"));
    }

    private static RulesContract.TeamPerceptionDefinition ReadTeamPerception(
        JsonElement element)
    {
        ExactObject(
            element,
            "kind",
            "snapshot",
            "sameTickDecisionSharing",
            "observationProvenance");
        return new RulesContract.TeamPerceptionDefinition(
            TeamPerceptionKind(element, "kind"),
            EnumId(element, "snapshot", "frozen-pre-tick-state"),
            EnumId(element, "sameTickDecisionSharing", "none"),
            Semantic(element, "observationProvenance"));
    }

    private static RulesContract.CollisionRules ReadCollisions(
        JsonElement element)
    {
        ExactObject(
            element,
            "actorsBlockWalls",
            "actorsBlockActors",
            "sameDestinationMovesBlockAll",
            "swapMovesBlocked",
            "followingVacatedActorAllowed",
            "projectilesBlockMovement",
            "movingOntoProjectileCausesHit",
            "wallsConsumeProjectiles",
            "projectilesIgnoreFiringLife",
            "projectilesStopOnFirstEnemyActor",
            "projectilesCollideWithProjectiles",
            "alliedProjectileContact",
            "movementResolution",
            "projectileTraversalResolution",
            "actorProjectileContactTiming",
            "movementDestinationProjectileResult",
            "alliedMovementDestinationOverride");
        return new RulesContract.CollisionRules(
            Bool(element, "actorsBlockWalls"),
            Bool(element, "actorsBlockActors"),
            Bool(element, "sameDestinationMovesBlockAll"),
            Bool(element, "swapMovesBlocked"),
            Bool(element, "followingVacatedActorAllowed"),
            Bool(element, "projectilesBlockMovement"),
            Bool(element, "movingOntoProjectileCausesHit"),
            Bool(element, "wallsConsumeProjectiles"),
            Bool(element, "projectilesIgnoreFiringLife"),
            Bool(element, "projectilesStopOnFirstEnemyActor"),
            Bool(element, "projectilesCollideWithProjectiles"),
            EnumId(
                element,
                "alliedProjectileContact",
                "pass-through",
                "block-without-damage",
                "damage-and-block"),
            Semantic(element, "movementResolution"),
            Semantic(element, "projectileTraversalResolution"),
            Semantic(element, "actorProjectileContactTiming"),
            Semantic(element, "movementDestinationProjectileResult"),
            Semantic(element, "alliedMovementDestinationOverride"));
    }

    private static RulesContract.TickResolutionDefinition ReadTickResolution(
        JsonElement element)
    {
        ExactObject(
            element,
            "observationsUsePreTickState",
            "decisionsResolveAsJointStep",
            "movementActionResolution",
            "rotationActionResolution",
            "actionAdmission",
            "actionFaultCounting",
            "matchCompletionPrecedence",
            "damageResolution",
            "phases");
        return new RulesContract.TickResolutionDefinition(
            Bool(element, "observationsUsePreTickState"),
            Bool(element, "decisionsResolveAsJointStep"),
            Semantic(element, "movementActionResolution"),
            Semantic(element, "rotationActionResolution"),
            Semantic(element, "actionAdmission"),
            Semantic(element, "actionFaultCounting"),
            Semantic(element, "matchCompletionPrecedence"),
            ReadDamageResolution(Property(element, "damageResolution")),
            Array(
                Property(element, "phases"),
                item => EnumValue(
                    item,
                    "resolve-tick-start-lifecycle",
                    "freeze-observations",
                    "collect-joint-decisions",
                    "validate-actions",
                    "rotate",
                    "move",
                    "reserve-lifecycle-actions",
                    "advance-existing-projectiles",
                    "launch-attacks-and-apply-damage",
                    "apply-runtime-faults",
                    "resolve-post-damage-lifecycle",
                    "resolve-fault-eligibility-completion",
                    "update-cooldowns-and-resources",
                    "update-mode",
                    "complete-due-same-life-transitions",
                    "resolve-match-completion")));
    }

    private static RulesContract.DamageResolution ReadDamageResolution(
        JsonElement element)
    {
        ExactObject(
            element,
            "contactBatch",
            "perTargetApplicationOrder",
            "projectileIdentityAssignment",
            "contactOrdinalAssignment",
            "healthApplication",
            "destructionAttribution",
            "eventOrder");
        return new RulesContract.DamageResolution(
            Semantic(element, "contactBatch"),
            Semantic(element, "perTargetApplicationOrder"),
            Semantic(element, "projectileIdentityAssignment"),
            Semantic(element, "contactOrdinalAssignment"),
            Semantic(element, "healthApplication"),
            Semantic(element, "destructionAttribution"),
            Semantic(element, "eventOrder"));
    }

    /// <summary>
    /// Checks contradictions needed to expose a coherent typed view. Full
    /// cross-catalog and match-feasibility validation belongs to the trusted
    /// Engine authoring boundary and is deliberately not duplicated in the
    /// NativeAOT guest SDK.
    /// </summary>
    private static void ValidateStructuralConsistency(
        int schemaVersion,
        MatchContract.CapabilityVersionSet capabilities,
        RulesContract rules,
        MapContract map,
        MatchContract.MatchFormat format,
        MatchContract.MatchTopology topology,
        MatchContract.Deployment deployment,
        MatchContract.ModeMapBindingDefinition binding)
    {
        if (capabilities.MatchContractSchemaVersion != schemaVersion)
        {
            throw new FormatException(
                "Capability match schema does not match the contract envelope.");
        }
        if (map.Width <= 0
            || map.Height <= 0
            || map.TileRows.Length != map.Height
            || map.TileRows.Any(row => row.Length != map.Width))
        {
            throw new FormatException(
                "Map dimensions and canonical tile rows are inconsistent.");
        }
        if (topology.Counts.TeamCount != topology.Teams.Length
            || topology.Counts.ParticipantCount
                != topology.Participants.Length
            || topology.Counts.UnitSlotCount != topology.UnitSlots.Length
            || topology.Counts.InitialLifeCount
                != topology.InitialLives.Length)
        {
            throw new FormatException(
                "Topology counts do not match their canonical collections.");
        }
        if (format.ScoringTeamCount != topology.Teams.Length
            || format.ParticipantCount != topology.Participants.Length
            || format.ParticipantsPerTeam <= 0
            || (long)format.ScoringTeamCount * format.ParticipantsPerTeam
                != format.ParticipantCount)
        {
            throw new FormatException(
                "Match format counts do not match the topology.");
        }
        ValidateFormatProfile(format);
        if (deployment.Spawns.Length != deployment.Lives.Length
            || deployment.Lives.Length != topology.InitialLives.Length)
        {
            throw new FormatException(
                "Initial deployment collection counts are inconsistent.");
        }

        ValidateScoreProfile(rules.GameMode);
        ValidateTickPhaseProfile(rules.TickResolution);

        switch (rules.GameMode, binding)
        {
            case (RulesContract.DeathmatchGameMode deathmatch,
                MatchContract.DeathmatchModeMapBinding):
                if (deathmatch.ModeId != "deathmatch")
                {
                    throw new FormatException(
                        "Deathmatch mode ID is inconsistent.");
                }
                break;

            case (RulesContract.FrontlineGameMode frontline,
                MatchContract.FrontlineModeMapBinding frontlineBinding):
                if (frontline.ModeId != "frontline"
                    || frontlineBinding.OrderedObjectiveRegionIds.Length
                        != frontline.FrontlinePositionCount
                    || frontlineBinding.TeamAdvances.Length
                        != topology.Teams.Length
                    || frontlineBinding.TeamAdvances
                        .Select(advance => advance.TeamId)
                        .Distinct().Count()
                        != frontlineBinding.TeamAdvances.Length
                    || frontlineBinding.TeamAdvances.Any(
                        advance =>
                            advance.Direction
                            switch
                            {
                                MatchContract.ObjectiveAdvanceDirection
                                    .TowardLowerIndex =>
                                    advance.ObjectiveIndexDelta != -1,
                                MatchContract.ObjectiveAdvanceDirection
                                    .TowardHigherIndex =>
                                    advance.ObjectiveIndexDelta != 1,
                                _ => true,
                            }))
                {
                    throw new FormatException(
                        "Frontline mode-map binding is inconsistent.");
                }
                ValidateFrontlineCapture(frontline.Capture);
                break;

            default:
                throw new FormatException(
                    "Game mode and mode-map binding variants disagree.");
        }
    }

    private static void ValidateFrontlineCapture(
        RulesContract.FrontlineCapture capture)
    {
        if (capture.Threshold <= 0
            || capture.GainPerSoleTeamTick <= 0
            || capture.DecayAmount < 0
            || capture.DecayIntervalTicks < 0
            || capture.RedeployPauseTicks < 0)
        {
            throw new FormatException(
                "Frontline capture values are outside the supported domain.");
        }
        if (capture.GainSchedule.IsDefaultOrEmpty)
            return;
        if (capture.GainSchedule[0].StartsAtTick != 0
            || capture.GainSchedule[0].GainPerSoleTeamTick
                != capture.GainPerSoleTeamTick
            || capture.GainSchedule.Any(phase =>
                phase.StartsAtTick < 0
                || phase.GainPerSoleTeamTick <= 0)
            || capture.GainSchedule
                .Select(phase => phase.PhaseId)
                .Distinct(StringComparer.Ordinal)
                .Count()
                != capture.GainSchedule.Length)
        {
            throw new FormatException(
                "Frontline capture gain schedule is inconsistent.");
        }
        for (int index = 1; index < capture.GainSchedule.Length; index++)
        {
            if (capture.GainSchedule[index - 1].StartsAtTick
                >= capture.GainSchedule[index].StartsAtTick)
            {
                throw new FormatException(
                    "Frontline capture gain schedule must be strictly ordered.");
            }
        }
    }

    private static void ValidateFormatProfile(
        MatchContract.MatchFormat format)
    {
        string expectedId = format.Kind switch
        {
            MatchContract.MatchFormatKind.HeadToHead
                when format.ScoringTeamCount == 2
                     && format.ParticipantsPerTeam == 1
                     && format.ParticipantCount == 2 =>
                "head-to-head",
            MatchContract.MatchFormatKind.FreeForAll
                when format.ScoringTeamCount >= 3
                     && format.ParticipantsPerTeam == 1
                     && format.ParticipantCount
                        == format.ScoringTeamCount =>
                $"ffa-{format.ParticipantCount}",
            MatchContract.MatchFormatKind.Teams
                when format.ScoringTeamCount >= 2
                     && format.ParticipantsPerTeam >= 2 =>
                $"teams-{format.ScoringTeamCount}x{format.ParticipantsPerTeam}",
            _ => "",
        };
        if (!string.Equals(
                expectedId,
                format.FormatId,
                StringComparison.Ordinal))
        {
            throw new FormatException(
                "Match format kind, ID, and cardinalities disagree.");
        }
    }

    private static void ValidateScoreProfile(
        RulesContract.GameModeDefinition mode)
    {
        if (mode.ScoreCatalog.IsDefaultOrEmpty
            || mode.ScoreCatalog
                .Select(channel => channel.Channel)
                .Distinct(StringComparer.Ordinal).Count()
                != mode.ScoreCatalog.Length)
        {
            throw new FormatException(
                "Game-mode score channels must be non-empty and unique.");
        }
        foreach (RulesContract.ScoreChannel channel in mode.ScoreCatalog)
        {
            string expectedDomain = channel.Channel
                == "territorial-progress"
                ? "signed"
                : "non-negative";
            if (channel.Domain != expectedDomain)
            {
                throw new FormatException(
                    $"Score channel '{channel.Channel}' has the wrong value domain.");
            }
        }
    }

    private static void ValidateTickPhaseProfile(
        RulesContract.TickResolutionDefinition tick)
    {
        string[] supportedPhases =
        [
            "resolve-tick-start-lifecycle",
            "freeze-observations",
            "collect-joint-decisions",
            "validate-actions",
            "rotate",
            "move",
            "reserve-lifecycle-actions",
            "advance-existing-projectiles",
            "launch-attacks-and-apply-damage",
            "apply-runtime-faults",
            "resolve-post-damage-lifecycle",
            "resolve-fault-eligibility-completion",
            "update-cooldowns-and-resources",
            "update-mode",
            "complete-due-same-life-transitions",
            "resolve-match-completion",
        ];
        if (!tick.ObservationsUsePreTickState
            || !tick.DecisionsResolveAsJointStep
            || !tick.Phases.SequenceEqual(
                supportedPhases,
                StringComparer.Ordinal))
        {
            throw new FormatException(
                "Tick resolution must use the complete generic-v2 phase order.");
        }
    }

    private static void ExactObject(
        JsonElement element,
        params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("Expected a canonical JSON object.");

        JsonElement.ObjectEnumerator enumerator = element.EnumerateObject();
        foreach (string expected in propertyNames)
        {
            if (!enumerator.MoveNext())
            {
                throw new FormatException(
                    $"Canonical object is missing property '{expected}'.");
            }
            if (!string.Equals(
                    enumerator.Current.Name,
                    expected,
                    StringComparison.Ordinal))
            {
                throw new FormatException(
                    $"Expected canonical property '{expected}', found '{enumerator.Current.Name}'.");
            }
        }
        if (enumerator.MoveNext())
        {
            throw new FormatException(
                $"Unknown canonical property '{enumerator.Current.Name}'.");
        }
    }

    private static JsonElement Property(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new FormatException(
                $"Missing canonical property '{propertyName}'.");
        }
        return value;
    }

    private static string PeekString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("Expected a tagged canonical object.");

        string? result = null;
        int count = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName))
            {
                count++;
                result = Text(property.Value);
            }
        }
        if (count != 1)
        {
            throw new FormatException(
                $"Tagged object must contain exactly one '{propertyName}' property.");
        }
        return result!;
    }

    private static ImmutableArray<T> Array<T>(
        JsonElement element,
        Func<JsonElement, T> read)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new FormatException("Expected a canonical JSON array.");
        int length = element.GetArrayLength();
        if (length
            > GenericActorContractVersions
                .MaxCanonicalContractCollectionCount)
        {
            throw new FormatException(
                "Canonical collection exceeds the actor protocol limit.");
        }

        var builder = ImmutableArray.CreateBuilder<T>(length);
        foreach (JsonElement item in element.EnumerateArray())
            builder.Add(read(item));
        return builder.MoveToImmutable();
    }

    private static Position Position(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array
            || element.GetArrayLength() != 2)
        {
            throw new FormatException(
                "Canonical position must be exactly [x,y].");
        }
        JsonElement.ArrayEnumerator values = element.EnumerateArray();
        values.MoveNext();
        int x = Int(values.Current);
        values.MoveNext();
        int y = Int(values.Current);
        return new Position(x, y);
    }

    private static int Int(JsonElement element, string propertyName) =>
        Int(Property(element, propertyName));

    private static int Int(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out int value)
            || !string.Equals(
                element.GetRawText(),
                value.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            throw new FormatException(
                "Expected a canonical JSON Int32 number.");
        }
        return value;
    }

    private static int? NullableInt(
        JsonElement element,
        string propertyName)
    {
        JsonElement value = Property(element, propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : Int(value);
    }

    private static long DecimalInt64String(
        JsonElement element,
        string propertyName)
    {
        string text = Text(element, propertyName);
        if (!long.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out long value)
            || !string.Equals(
                text,
                value.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Property '{propertyName}' is not a canonical Int64 string.");
        }
        return value;
    }

    private static bool Bool(JsonElement element, string propertyName)
    {
        JsonElement value = Property(element, propertyName);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FormatException(
                $"Property '{propertyName}' must be boolean."),
        };
    }

    private static string Text(JsonElement element, string propertyName) =>
        Text(Property(element, propertyName));

    private static string Text(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new FormatException("Expected a canonical JSON string.");
        string value = element.GetString()!;
        if (value.Length == 0)
            throw new FormatException("Canonical string cannot be empty.");
        if (Utf8ByteCount(value)
            > GenericActorContractVersions.MaxCanonicalContractBytes)
        {
            throw new FormatException(
                "Canonical string exceeds the actor protocol limit.");
        }
        return value;
    }

    private static string? NullableId(
        JsonElement element,
        string propertyName)
    {
        JsonElement value = Property(element, propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : Id(value);
    }

    private static string Id(JsonElement element, string propertyName) =>
        Id(Property(element, propertyName));

    private static string Id(JsonElement element)
    {
        string value = Text(element);
        if (Utf8ByteCount(value)
                > GenericActorContractVersions.MaxSemanticIdBytes
            || !value.All(
                character =>
                    character is >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-')
            || value[0] == '-'
            || value[^1] == '-'
            || value.Contains("--", StringComparison.Ordinal))
        {
            throw new FormatException(
                "Canonical identifier must be a 1-64 byte lowercase-kebab semantic ID.");
        }
        return value;
    }

    private static string Semantic(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName));

    private static string Semantic(JsonElement element)
    {
        string value = Text(element);
        if (!value.All(
                character =>
                    character is >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-')
            || value[0] == '-'
            || value[^1] == '-'
            || value.Contains("--", StringComparison.Ordinal))
        {
            throw new FormatException(
                "Canonical semantic policy must be lowercase kebab-case.");
        }
        return value;
    }

    private static string Fingerprint(
        JsonElement element,
        string propertyName)
    {
        string value = Text(element, propertyName);
        if (value.Length != 64
            || !value.All(
                character =>
                    character is >= '0' and <= '9'
                    or >= 'a' and <= 'f'))
        {
            throw new FormatException(
                $"Property '{propertyName}' is not a lowercase SHA-256 fingerprint.");
        }
        return value;
    }

    private static void VerifyFingerprint(
        JsonElement element,
        string supplied,
        string component,
        params string[] excludedPropertyNames)
    {
        string payload =
            element.CanonicalObjectExcluding(excludedPropertyNames);
        string computed = Convert.ToHexStringLower(
            ActorSha256.HashData(StrictUtf8.GetBytes(payload)));
        if (!string.Equals(supplied, computed, StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Canonical {component} fingerprint does not match its payload.");
        }
    }

    private static string ScoreChannel(
        JsonElement element,
        string propertyName) =>
        EnumId(
            element,
            propertyName,
            "kills",
            "deaths",
            "damage-dealt",
            "active-health",
            "territorial-progress");

    private static string EnumId(
        JsonElement element,
        string propertyName,
        params string[] supported) =>
        EnumValue(Property(element, propertyName), supported);

    private static string EnumValue(
        JsonElement element,
        params string[] supported)
    {
        string value = Semantic(element);
        if (!supported.Contains(value, StringComparer.Ordinal))
            throw Unsupported("semantic tag", value);
        return value;
    }

    private static string? NullableEnumId(
        JsonElement element,
        string propertyName,
        params string[] supported)
    {
        JsonElement value = Property(element, propertyName);
        return value.ValueKind == JsonValueKind.Null
            ? null
            : EnumValue(value, supported);
    }

    private static Direction Direction(
        JsonElement element,
        string propertyName) =>
        Direction(Property(element, propertyName));

    private static Direction Direction(JsonElement element) =>
        Semantic(element) switch
        {
            "north" => ContractDirection.North,
            "east" => ContractDirection.East,
            "south" => ContractDirection.South,
            "west" => ContractDirection.West,
            string value => throw Unsupported("direction", value),
        };

    private static MapContract.MovementLayer MovementLayer(
        JsonElement element,
        string propertyName) =>
        MovementLayer(Property(element, propertyName));

    private static MapContract.MovementLayer MovementLayer(
        JsonElement element) =>
        Semantic(element) switch
        {
            "ground" => MapContract.MovementLayer.Ground,
            "air" => MapContract.MovementLayer.Air,
            string value => throw Unsupported("movement layer", value),
        };

    private static RulesContract.MovementFacingCoupling
        MovementFacingCoupling(JsonElement element) =>
        Semantic(element) switch
        {
            // Omitted, never written inert: canonical bytes have exactly one
            // encoding of a facing-preserving profile.
            "face-movement-direction" =>
                RulesContract.MovementFacingCoupling.FaceMovementDirection,
            "facing-locked" =>
                RulesContract.MovementFacingCoupling.FacingLocked,
            string value =>
                throw Unsupported("movement facing coupling", value),
        };

    private static MapContract.RegionKind RegionKind(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "objective" => MapContract.RegionKind.Objective,
            "transition-placement" =>
                MapContract.RegionKind.TransitionPlacement,
            string value => throw Unsupported("map region kind", value),
        };

    private static MapContract.TileTagKind TileTagKind(
        JsonElement element,
        string propertyName) =>
        TileTagKind(Property(element, propertyName));

    private static MapContract.TileTagKind TileTagKind(JsonElement element) =>
        Semantic(element) switch
        {
            "transition-placement-forbidden" =>
                MapContract.TileTagKind.TransitionPlacementForbidden,
            "spawn-protected" =>
                MapContract.TileTagKind.SpawnProtected,
            string value => throw Unsupported("map tile-tag kind", value),
        };

    private static MatchContract.MatchFormatKind MatchFormatKind(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "head-to-head" => MatchContract.MatchFormatKind.HeadToHead,
            "free-for-all" => MatchContract.MatchFormatKind.FreeForAll,
            "teams" => MatchContract.MatchFormatKind.Teams,
            string value => throw Unsupported("match format kind", value),
        };

    private static MatchContract.InitialAvailability InitialAvailability(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "active-at-tick-zero" =>
                MatchContract.InitialAvailability.ActiveAtTickZero,
            "dormant-unlock-at-tick" =>
                MatchContract.InitialAvailability.DormantUnlockAtTick,
            "dormant-automatic-activation-at-tick" =>
                MatchContract.InitialAvailability
                    .DormantAutomaticActivationAtTick,
            string value => throw Unsupported(
                "initial availability",
                value),
        };

    private static MatchContract.ObjectiveAdvanceDirection
        ObjectiveAdvanceDirection(
            JsonElement element,
            string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "toward-lower-index" =>
                MatchContract.ObjectiveAdvanceDirection.TowardLowerIndex,
            "toward-higher-index" =>
                MatchContract.ObjectiveAdvanceDirection.TowardHigherIndex,
            string value => throw Unsupported(
                "objective advance direction",
                value),
        };

    private static RulesContract.ProjectileMode ProjectileMode(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "instant-ray" => RulesContract.ProjectileMode.InstantRay,
            "discrete" => RulesContract.ProjectileMode.Discrete,
            string value => throw Unsupported("projectile mode", value),
        };

    private static RulesContract.TeamPerceptionKind TeamPerceptionKind(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "individual" =>
                RulesContract.TeamPerceptionKind.Individual,
            "immediate-union" =>
                RulesContract.TeamPerceptionKind.ImmediateUnion,
            string value => throw Unsupported(
                "team perception kind",
                value),
        };

    private static RulesContract.ActionKind ActionKind(
        JsonElement element,
        string propertyName) =>
        Semantic(Property(element, propertyName)) switch
        {
            "wait" => RulesContract.ActionKind.Wait,
            "movement" => RulesContract.ActionKind.Movement,
            "rotation" => RulesContract.ActionKind.Rotation,
            "attack" => RulesContract.ActionKind.Attack,
            "fabrication" => RulesContract.ActionKind.Fabrication,
            "same-life-transition" =>
                RulesContract.ActionKind.SameLifeTransition,
            "replication" => RulesContract.ActionKind.Replication,
            string value => throw Unsupported("action kind", value),
        };

    private static RulesContract.ActionParameterKind ActionParameterKind(
        JsonElement element) =>
        Semantic(element) switch
        {
            "shot-program" =>
                RulesContract.ActionParameterKind.ShotProgram,
            "direction" => RulesContract.ActionParameterKind.Direction,
            "unit-target" =>
                RulesContract.ActionParameterKind.UnitTarget,
            "form-target" =>
                RulesContract.ActionParameterKind.FormTarget,
            "projectile-heading" =>
                RulesContract.ActionParameterKind.ProjectileHeading,
            string value => throw Unsupported(
                "action parameter kind",
                value),
        };

    private static void RequireVersion(
        int actual,
        int expected,
        string component)
    {
        if (actual != expected)
        {
            throw Unsupported(
                component,
                $"schema {actual}; expected schema {expected}");
        }
    }

    private static NotSupportedException Unsupported(
        string path,
        string value) =>
        new($"Unsupported canonical {path}: '{value}'.");

    private static void EnsureBoundedUtf8(string value)
    {
        if (Utf8ByteCount(value)
            > GenericActorContractVersions.MaxCanonicalContractBytes)
        {
            throw new FormatException(
                "The canonical actor contract exceeds the profile limit.");
        }
    }

    private static int Utf8ByteCount(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new FormatException(
                "The canonical actor contract contains invalid Unicode.",
                exception);
        }
    }

    private static void EnsureCompact(string value)
    {
        bool inString = false;
        bool escaped = false;
        foreach (char character in value)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                throw new FormatException(
                    "Canonical actor JSON cannot contain insignificant whitespace.");
            }
        }
    }
}
