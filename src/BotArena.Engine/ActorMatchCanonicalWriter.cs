using System.Text.Json;

namespace BotArena.Engine;

/// <summary>
/// Explicit canonical writers for map, format, topology, and resolved match
/// components in the generation-3 actor contract family.
/// </summary>
internal static class ActorMatchCanonicalWriter
{
    public static string SerializeMap(
        ActorMapDefinition map,
        bool includeProvenance)
    {
        string? fingerprint = includeProvenance
            ? ActorContractFingerprint.ComputeMap(map)
            : null;
        return ActorContractCanonicalJson.Write(
            writer => WriteMap(
                writer,
                map,
                includeProvenance,
                fingerprint));
    }

    public static string SerializeFormat(
        MatchFormatDefinition format,
        bool includeProvenance)
    {
        string? fingerprint = includeProvenance
            ? ActorContractFingerprint.ComputeFormat(format)
            : null;
        return ActorContractCanonicalJson.Write(
            writer => WriteFormat(
                writer,
                format,
                includeProvenance,
                fingerprint));
    }

    public static string SerializeTopology(
        PublicMatchTopology topology,
        bool includeFingerprint)
    {
        ValidateTopology(topology);
        string? fingerprint = includeFingerprint
            ? ActorContractFingerprint.ComputeTopology(topology)
            : null;
        return ActorContractCanonicalJson.Write(
            writer => WriteTopology(
                writer,
                topology,
                includeFingerprint,
                fingerprint));
    }

    public static string SerializeMatch(
        ActorResolvedMatchDefinition match,
        bool includeFingerprint)
    {
        ActorContractFingerprint.ValidateMatch(match);
        string? fingerprint = includeFingerprint
            ? ActorContractFingerprint.ComputeMatch(match)
            : null;
        return ActorContractCanonicalJson.Write(
            writer => WriteMatch(
                writer,
                match,
                includeFingerprint,
                fingerprint));
    }

    private static void WriteMap(
        Utf8JsonWriter writer,
        ActorMapDefinition map,
        bool includeProvenance,
        string? fingerprint)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "schemaVersion",
            ActorContractManifestSerializer.MapManifestSchemaVersion);
        if (includeProvenance)
        {
            writer.WriteString("mapId", map.Id);
            writer.WriteNumber("mapVersion", map.Version);
            writer.WriteString("mapFingerprint", fingerprint);
        }
        writer.WriteNumber("formatVersion", map.FormatVersion);
        writer.WriteNumber("width", map.Width);
        writer.WriteNumber("height", map.Height);
        writer.WritePropertyName("tileRows");
        writer.WriteStartArray();
        foreach (string row in map.TileRows)
            writer.WriteStringValue(row);
        writer.WriteEndArray();

        writer.WritePropertyName("spawnAnchors");
        writer.WriteStartArray();
        foreach (ActorMapSpawnAnchorDefinition anchor in map.SpawnAnchors)
        {
            writer.WriteStartObject();
            writer.WriteString("spawnId", anchor.Spawn.SpawnId);
            writer.WritePropertyName("position");
            ActorContractCanonicalJson.WritePosition(
                writer,
                anchor.Spawn.Position);
            writer.WriteString("facing", Id(anchor.Spawn.Facing));
            writer.WritePropertyName("compatibleMovementLayers");
            writer.WriteStartArray();
            foreach (ActorMovementLayer layer in
                     anchor.CompatibleMovementLayers)
            {
                writer.WriteStringValue(Id(layer));
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("regions");
        writer.WriteStartArray();
        foreach (ActorMapRegionDefinition region in map.Regions)
        {
            writer.WriteStartObject();
            writer.WriteString("regionId", region.RegionId);
            writer.WriteString("kind", Id(region.Kind));
            WritePositions(writer, region.Tiles);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("tileTags");
        writer.WriteStartArray();
        foreach (ActorMapTileTagDefinition tag in map.TileTags)
        {
            writer.WriteStartObject();
            writer.WriteString("tagId", tag.TagId);
            writer.WriteString("kind", Id(tag.Kind));
            WritePositions(writer, tag.Tiles);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteFormat(
        Utf8JsonWriter writer,
        MatchFormatDefinition format,
        bool includeProvenance,
        string? fingerprint)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "schemaVersion",
            ActorContractManifestSerializer.FormatManifestSchemaVersion);
        if (includeProvenance)
        {
            writer.WriteString("formatId", format.FormatId);
            writer.WriteString("formatFingerprint", fingerprint);
        }
        writer.WriteString("kind", Id(format.Kind));
        writer.WriteNumber(
            "scoringTeamCount",
            format.ScoringTeamCount);
        writer.WriteNumber(
            "participantsPerTeam",
            format.ParticipantsPerTeam);
        writer.WriteNumber(
            "participantCount",
            format.ParticipantCount);
        writer.WriteEndObject();
    }

    private static void WriteTopology(
        Utf8JsonWriter writer,
        PublicMatchTopology topology,
        bool includeFingerprint,
        string? fingerprint)
    {
        PublicScoringTeam[] teams = topology.Teams
            .OrderBy(team => team.TeamId)
            .ToArray();
        PublicParticipant[] participants = topology.Participants
            .OrderBy(participant => participant.ParticipantId)
            .ToArray();
        PublicUnitSlot[] unitSlots = topology.UnitSlots
            .OrderBy(slot => slot.TeamId)
            .ThenBy(slot => slot.UnitId)
            .ToArray();
        PublicInitialLife[] initialLives = topology.InitialLives
            .OrderBy(life => life.TeamId)
            .ThenBy(life => life.UnitId)
            .ThenBy(life => life.LifeId)
            .ToArray();

        writer.WriteStartObject();
        writer.WriteNumber(
            "schemaVersion",
            ActorContractManifestSerializer.TopologyManifestSchemaVersion);
        if (includeFingerprint)
            writer.WriteString("topologyFingerprint", fingerprint);
        writer.WritePropertyName("counts");
        writer.WriteStartObject();
        writer.WriteNumber("teamCount", teams.Length);
        writer.WriteNumber("participantCount", participants.Length);
        writer.WriteNumber("unitSlotCount", unitSlots.Length);
        writer.WriteNumber("initialLifeCount", initialLives.Length);
        writer.WriteEndObject();

        writer.WritePropertyName("teams");
        writer.WriteStartArray();
        foreach (PublicScoringTeam team in teams)
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", team.TeamId);
            // Emitted only when a ruleset declares classes, the #156
            // additive-canonical pattern: a class-free topology writes exactly
            // the bytes it wrote before, so every existing fingerprint holds.
            // The SDK reader and the web normalizer both refuse an explicit
            // null, so the absence has one encoding.
            if (team.ClassId is not null)
                writer.WriteString("classId", team.ClassId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("participants");
        writer.WriteStartArray();
        foreach (PublicParticipant participant in participants)
        {
            writer.WriteStartObject();
            writer.WriteNumber(
                "participantId",
                participant.ParticipantId);
            writer.WriteNumber("teamId", participant.TeamId);
            // Same additive discipline as the scoring team above.
            if (participant.ClassId is not null)
                writer.WriteString("classId", participant.ClassId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("unitSlots");
        writer.WriteStartArray();
        foreach (PublicUnitSlot slot in unitSlots)
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", slot.TeamId);
            writer.WriteNumber("unitId", slot.UnitId);
            writer.WriteNumber(
                "controllerParticipantId",
                slot.ControllerParticipantId);
            // Per-slot chassis, emitted only when a ruleset declares
            // compositions — the same #156 additive-canonical pattern the
            // scoring team and participant above already follow. A
            // composition-free topology writes exactly the bytes it wrote
            // before, so every existing fingerprint holds.
            if (slot.ClassId is not null)
                writer.WriteString("classId", slot.ClassId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("initialLives");
        writer.WriteStartArray();
        foreach (PublicInitialLife life in initialLives)
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

    private static void WriteMatch(
        Utf8JsonWriter writer,
        ActorResolvedMatchDefinition match,
        bool includeFingerprint,
        string? fingerprint)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", match.SchemaVersion);
        if (includeFingerprint)
        {
            writer.WriteString(
                "matchContractFingerprint",
                fingerprint);
        }
        writer.WritePropertyName("capabilityVersions");
        WriteCapabilities(writer, match.CapabilityVersions);
        writer.WritePropertyName("rules");
        ActorRulesCanonicalWriter.Write(
            writer,
            match.Rules,
            includeProvenance: true);
        writer.WritePropertyName("map");
        WriteMap(
            writer,
            match.Map,
            includeProvenance: true,
            ActorContractFingerprint.ComputeMap(match.Map));
        writer.WritePropertyName("format");
        WriteFormat(
            writer,
            match.Format,
            includeProvenance: true,
            ActorContractFingerprint.ComputeFormat(match.Format));
        writer.WritePropertyName("topology");
        WriteTopology(
            writer,
            match.Topology,
            includeFingerprint: true,
            ActorContractFingerprint.ComputeTopology(match.Topology));
        writer.WritePropertyName("initialDeployment");
        WriteInitialDeployment(writer, match.InitialDeployment);
        WriteLifecycleAssignments(writer, match.LifecycleAssignments);
        WriteParticipantRegionAssignments(
            writer,
            match.ParticipantRegionAssignments);
        writer.WritePropertyName("modeMapBinding");
        WriteModeMapBinding(writer, match.ModeMapBinding);
        writer.WriteEndObject();
    }

    private static void WriteCapabilities(
        Utf8JsonWriter writer,
        ActorMatchCapabilityVersions capabilities)
    {
        writer.WriteStartObject();
        writer.WriteString(
            "contractProfileId",
            capabilities.ContractProfileId);
        writer.WriteString(
            "runtimeProtocolVersion",
            capabilities.RuntimeProtocolVersion);
        writer.WriteString(
            "runtimeConfigurationVersion",
            capabilities.RuntimeConfigurationVersion);
        writer.WriteNumber(
            "runtimeContractVersion",
            capabilities.RuntimeContractVersion);
        writer.WriteNumber(
            "matchStartSchemaVersion",
            capabilities.MatchStartSchemaVersion);
        writer.WriteNumber(
            "observationSchemaVersion",
            capabilities.ObservationSchemaVersion);
        writer.WriteNumber(
            "decisionSchemaVersion",
            capabilities.DecisionSchemaVersion);
        writer.WriteNumber(
            "matchContractSchemaVersion",
            capabilities.MatchContractSchemaVersion);
        writer.WriteEndObject();
    }

    private static void WriteInitialDeployment(
        Utf8JsonWriter writer,
        InitialDeploymentDefinition deployment)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("spawns");
        writer.WriteStartArray();
        foreach (InitialSpawnDefinition spawn in deployment.Spawns)
        {
            writer.WriteStartObject();
            writer.WriteString("spawnId", spawn.SpawnId);
            writer.WritePropertyName("position");
            ActorContractCanonicalJson.WritePosition(
                writer,
                spawn.Position);
            writer.WriteString("facing", Id(spawn.Facing));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("lives");
        writer.WriteStartArray();
        foreach (InitialLifeDeployment life in deployment.Lives)
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", life.TeamId);
            writer.WriteNumber("unitId", life.UnitId);
            writer.WriteNumber("lifeId", life.LifeId);
            writer.WriteString("formId", life.FormId);
            writer.WriteString("spawnId", life.SpawnId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteLifecycleAssignments(
        Utf8JsonWriter writer,
        IEnumerable<ActorUnitSlotLifecycleAssignmentDefinition> assignments)
    {
        writer.WritePropertyName("lifecycleAssignments");
        writer.WriteStartArray();
        foreach (ActorUnitSlotLifecycleAssignmentDefinition assignment in
                 assignments)
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", assignment.TeamId);
            writer.WriteNumber("unitId", assignment.UnitId);
            writer.WriteString(
                "lifecycleProfileId",
                assignment.LifecycleProfileId);
            ActorContractCanonicalJson.WriteNullableNumber(
                writer,
                "initialGeneration",
                assignment.InitialGeneration);
            writer.WritePropertyName("allowedFormIds");
            writer.WriteStartArray();
            foreach (string formId in assignment.AllowedFormIds)
                writer.WriteStringValue(formId);
            writer.WriteEndArray();
            writer.WriteString(
                "initialAvailability",
                Id(assignment.InitialAvailability));
            ActorContractCanonicalJson.WriteNullableNumber(
                writer,
                "unlockTick",
                assignment.UnlockTick);
            ActorContractCanonicalJson.WriteNullableString(
                writer,
                "assignedRespawnSpawnId",
                assignment.AssignedRespawnSpawnId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteParticipantRegionAssignments(
        Utf8JsonWriter writer,
        IEnumerable<ActorParticipantRegionAssignmentDefinition> assignments)
    {
        writer.WritePropertyName("participantRegionAssignments");
        writer.WriteStartArray();
        foreach (ActorParticipantRegionAssignmentDefinition assignment in
                 assignments)
        {
            writer.WriteStartObject();
            writer.WriteNumber(
                "participantId",
                assignment.ParticipantId);
            writer.WriteString(
                "regionRoleId",
                assignment.RegionRoleId);
            writer.WriteString("mapRegionId", assignment.MapRegionId);
            writer.WriteString("facing", Id(assignment.Facing));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteModeMapBinding(
        Utf8JsonWriter writer,
        ActorModeMapBindingDefinition binding)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Id(binding.Kind));
        switch (binding)
        {
            case DeathmatchActorModeMapBindingDefinition:
                break;
            case FrontlineActorModeMapBindingDefinition frontline:
                writer.WritePropertyName("orderedObjectiveRegionIds");
                writer.WriteStartArray();
                foreach (string regionId in
                         frontline.OrderedObjectiveRegionIds)
                {
                    writer.WriteStringValue(regionId);
                }
                writer.WriteEndArray();
                writer.WritePropertyName("teamAdvances");
                writer.WriteStartArray();
                foreach (FrontlineTeamAdvanceDefinition advance in
                         frontline.TeamAdvances)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("teamId", advance.TeamId);
                    writer.WriteString(
                        "direction",
                        Id(advance.Direction));
                    writer.WriteNumber(
                        "objectiveIndexDelta",
                        advance.ObjectiveIndexDelta);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(binding),
                    binding,
                    "Unsupported generation-3 mode-map binding.");
        }
        writer.WriteEndObject();
    }

    private static void WritePositions(
        Utf8JsonWriter writer,
        IEnumerable<Position> positions)
    {
        writer.WritePropertyName("tiles");
        writer.WriteStartArray();
        foreach (Position position in positions)
            ActorContractCanonicalJson.WritePosition(writer, position);
        writer.WriteEndArray();
    }

    private static void ValidateTopology(PublicMatchTopology topology)
    {
        if (topology.Teams.IsDefaultOrEmpty
            || topology.Participants.IsDefaultOrEmpty
            || topology.UnitSlots.IsDefaultOrEmpty
            || topology.InitialLives.IsDefaultOrEmpty)
        {
            throw InvalidTopology(
                "Teams, participants, unit slots, and initial lives must be initialized and non-empty.");
        }
        if (topology.Teams.Any(team => team is null)
            || topology.Participants.Any(participant => participant is null)
            || topology.UnitSlots.Any(slot => slot is null)
            || topology.InitialLives.Any(life => life is null))
        {
            throw InvalidTopology(
                "Topology collections cannot contain null entries.");
        }

        var teamIds = new HashSet<int>();
        foreach (PublicScoringTeam team in topology.Teams)
        {
            if (team.TeamId < 0
                || !teamIds.Add(team.TeamId)
                || team.ClassId is not null
                    && !IsCanonicalSemanticId(team.ClassId))
            {
                throw InvalidTopology(
                    "Scoring-team IDs must be unique and non-negative, and optional class IDs must be lowercase-kebab IDs.");
            }
        }

        var participants = new Dictionary<int, PublicParticipant>();
        foreach (PublicParticipant participant in topology.Participants)
        {
            if (participant.ParticipantId < 0
                || !teamIds.Contains(participant.TeamId)
                || participant.ClassId is not null
                    && !IsCanonicalSemanticId(participant.ClassId)
                || !participants.TryAdd(
                    participant.ParticipantId,
                    participant))
            {
                throw InvalidTopology(
                    "Participants must have unique non-negative IDs, reference a declared team, and use lowercase-kebab optional class IDs.");
            }
        }
        foreach (PublicScoringTeam team in topology.Teams)
        {
            if (participants.Values.Any(participant =>
                    participant.TeamId == team.TeamId
                    && !string.Equals(
                        participant.ClassId,
                        team.ClassId,
                        StringComparison.Ordinal)))
            {
                throw InvalidTopology(
                    "Every participant class ID must exactly match its scoring team's class ID.");
            }
        }
        if (teamIds.Any(teamId =>
                !participants.Values.Any(
                    participant => participant.TeamId == teamId)))
        {
            throw InvalidTopology(
                "Every scoring team must have at least one participant.");
        }

        var slots = new HashSet<(int TeamId, int UnitId)>();
        var controllersWithSlots = new HashSet<int>();
        foreach (PublicUnitSlot slot in topology.UnitSlots)
        {
            if (slot.UnitId < 0
                || !teamIds.Contains(slot.TeamId)
                || !slots.Add((slot.TeamId, slot.UnitId)))
            {
                throw InvalidTopology(
                    "Unit slots must be unique within a declared team and use non-negative IDs.");
            }
            if (!participants.TryGetValue(
                    slot.ControllerParticipantId,
                    out PublicParticipant? controller)
                || controller.TeamId != slot.TeamId)
            {
                throw InvalidTopology(
                    "Every unit slot must be controlled by a participant on the same team.");
            }
            controllersWithSlots.Add(slot.ControllerParticipantId);
        }
        if (participants.Keys.Any(
                participantId =>
                    !controllersWithSlots.Contains(participantId)))
        {
            throw InvalidTopology(
                "Every participant must control at least one unit slot.");
        }

        var occupiedSlots = new HashSet<(int TeamId, int UnitId)>();
        var actorIds = new HashSet<(int TeamId, int UnitId, int LifeId)>();
        foreach (PublicInitialLife life in topology.InitialLives)
        {
            var slot = (life.TeamId, life.UnitId);
            if (life.LifeId < 0
                || string.IsNullOrWhiteSpace(life.FormId)
                || !slots.Contains(slot)
                || !occupiedSlots.Add(slot)
                || !actorIds.Add((life.TeamId, life.UnitId, life.LifeId)))
            {
                throw InvalidTopology(
                    "Each initial life must uniquely occupy a declared slot and have a non-negative life ID and form.");
            }
        }
        if (teamIds.Any(teamId =>
                !topology.InitialLives.Any(life => life.TeamId == teamId)))
        {
            throw InvalidTopology(
                "Every scoring team must have at least one initial life.");
        }
    }

    private static bool IsCanonicalSemanticId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64)
            return false;

        bool needsSegmentStart = true;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (needsSegmentStart)
                    return false;
                needsSegmentStart = true;
                continue;
            }
            if (character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9'))
            {
                return false;
            }
            needsSegmentStart = false;
        }
        return !needsSegmentStart;
    }

    private static ArgumentException InvalidTopology(string message) =>
        new(message, "topology");

    private static string Id(Enum value) =>
        ActorContractCanonicalIds.Id(value);
}
