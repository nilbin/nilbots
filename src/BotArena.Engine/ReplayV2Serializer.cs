using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.Engine;

/// <summary>
/// Explicit canonical codec for the internal replay-v2 contract. Replay-v1
/// serialization remains exclusively owned by <see cref="ReplaySerializer"/>.
/// </summary>
internal static class ReplayV2Serializer
{
    public static string ToCanonicalJson(ReplayV2 replay) =>
        Encoding.UTF8.GetString(ToCanonicalUtf8(replay));

    public static string ComputeHash(ReplayV2 replay) =>
        Convert.ToHexStringLower(SHA256.HashData(ToCanonicalUtf8(replay)));

    public static string ToJson(ReplayV2 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        Validate(replay);
        string hash = ComputeHash(replay);
        return Encoding.UTF8.GetString(Write(writer =>
        {
            writer.WriteStartObject();
            WriteReplayProperties(writer, replay);
            writer.WriteString("replayHash", hash);
            writer.WriteBoolean("partial", false);
            writer.WriteEndObject();
        }));
    }

    public static string ToPartialJson(
        ReplayV2Header header,
        ImmutableArray<ReplayV2Tick> ticks)
    {
        ArgumentNullException.ThrowIfNull(header);
        ValidatePrefix(header, ticks);
        return Encoding.UTF8.GetString(Write(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("header");
            WriteHeader(writer, header);
            writer.WritePropertyName("ticks");
            writer.WriteStartArray();
            foreach (ReplayV2Tick tick in ticks.OrderBy(value => value.Tick))
                WriteTick(writer, tick);
            writer.WriteEndArray();
            writer.WriteNull("result");
            writer.WriteNull("replayHash");
            writer.WriteBoolean("partial", true);
            writer.WriteEndObject();
        }));
    }

    public static bool VerifyHash(string json) =>
        VerifyHash(json, out _);

    public static bool VerifyHash(string json, out string? failure)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            if (ReplayV2VersionProbe.Probe(json)
                != ReplayV2DocumentFormat.EntityV2)
            {
                failure = "Document is not replay v2.";
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (!HasExactTopLevelProperties(root))
            {
                failure =
                    "Replay-v2 document must contain exactly header, ticks, result, replayHash, and partial.";
                return false;
            }
            JsonElement partial = root.GetProperty("partial");
            if (partial.ValueKind != JsonValueKind.False)
            {
                failure = "Partial replay-v2 documents are intentionally unhashed.";
                return false;
            }
            JsonElement header = root.GetProperty("header");
            if (!HasCanonicalSeed(header))
            {
                failure = "Replay-v2 seed must be a canonical unsigned decimal string.";
                return false;
            }
            if (!HasCanonicalWireStrings(root))
            {
                failure =
                    "Replay-v2 Int64 values, actor seeds, and event/projectile IDs must use canonical decimal-safe strings.";
                return false;
            }

            JsonElement replayHashElement = root.GetProperty("replayHash");
            if (replayHashElement.ValueKind != JsonValueKind.String
                || replayHashElement.GetString() is not { } replayHash
                || !IsLowercaseSha256(replayHash))
            {
                failure = "Replay-v2 replayHash must be lowercase SHA-256 hex.";
                return false;
            }

            byte[] payload = Write(writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("header");
                header.WriteTo(writer);
                writer.WritePropertyName("ticks");
                root.GetProperty("ticks").WriteTo(writer);
                writer.WritePropertyName("result");
                root.GetProperty("result").WriteTo(writer);
                writer.WriteEndObject();
            });
            string actual =
                Convert.ToHexStringLower(SHA256.HashData(payload));
            bool verified = string.Equals(
                replayHash,
                actual,
                StringComparison.Ordinal);
            failure = verified ? null : "Replay-v2 hash mismatch.";
            return verified;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or JsonException
            or NotSupportedException)
        {
            failure = exception.Message;
            return false;
        }
    }

    private static byte[] ToCanonicalUtf8(ReplayV2 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        Validate(replay);
        return Write(writer =>
        {
            writer.WriteStartObject();
            WriteReplayProperties(writer, replay);
            writer.WriteEndObject();
        });
    }

    private static void WriteReplayProperties(
        Utf8JsonWriter writer,
        ReplayV2 replay)
    {
        writer.WritePropertyName("header");
        WriteHeader(writer, replay.Header);
        writer.WritePropertyName("ticks");
        writer.WriteStartArray();
        foreach (ReplayV2Tick tick in replay.Ticks.OrderBy(value => value.Tick))
            WriteTick(writer, tick);
        writer.WriteEndArray();
        writer.WritePropertyName("result");
        WriteResult(writer, replay.Result);
    }

    private static void WriteHeader(
        Utf8JsonWriter writer,
        ReplayV2Header header)
    {
        writer.WriteStartObject();
        writer.WriteNumber("replayVersion", header.ReplayVersion);
        writer.WriteString("engineVersion", header.EngineVersion);
        writer.WriteString("gameRulesVersion", header.GameRulesVersion);
        writer.WritePropertyName("actorRuntime");
        writer.WriteStartObject();
        writer.WriteString("family", header.ActorRuntime.Family);
        writer.WriteNumber("version", header.ActorRuntime.Version);
        writer.WriteNumber(
            "matchStartSchemaVersion",
            header.ActorRuntime.MatchStartSchemaVersion);
        writer.WriteNumber(
            "observationSchemaVersion",
            header.ActorRuntime.ObservationSchemaVersion);
        writer.WriteNumber(
            "decisionSchemaVersion",
            header.ActorRuntime.DecisionSchemaVersion);
        writer.WriteEndObject();
        writer.WriteString("seed", header.Seed);
        writer.WritePropertyName("contract");
        using (JsonDocument contract = JsonDocument.Parse(
                   RulesManifestSerializer.ToCanonicalJson(header.Contract)))
        {
            contract.RootElement.WriteTo(writer);
        }
        writer.WritePropertyName("presentation");
        WritePresentation(writer, header.Presentation);
        writer.WritePropertyName("participants");
        writer.WriteStartArray();
        foreach (ReplayV2ParticipantController participant in
                 header.Participants
                     .OrderBy(value => value.ParticipantId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("participantId", participant.ParticipantId);
            writer.WriteNumber("teamId", participant.TeamId);
            writer.WriteString("name", participant.Name);
            writer.WriteString("runtimeKind", participant.RuntimeKind);
            writer.WriteString("artifactHash", participant.ArtifactHash);
            writer.WriteString("accent", participant.Accent);
            WriteNullableString(writer, "lookId", participant.LookId);
            WriteNullableString(
                writer,
                "projectileLookId",
                participant.ProjectileLookId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WritePresentation(
        Utf8JsonWriter writer,
        ReplayV2Presentation? presentation)
    {
        if (presentation is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteNullableString(writer, "themeId", presentation.ThemeId);
        writer.WritePropertyName("map");
        if (presentation.Map is not { } map)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("boundaryWall", map.BoundaryWall);
            writer.WriteString("interiorWall", map.InteriorWall);
            writer.WritePropertyName("wallGroups");
            writer.WriteStartArray();
            foreach (ReplayV2WallGroup group in map.WallGroups
                         .OrderBy(value => value.Family, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("family", group.Family);
                writer.WritePropertyName("tiles");
                WritePositions(
                    writer,
                    group.Tiles
                        .OrderBy(value => value.Y)
                        .ThenBy(value => value.X));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteTick(Utf8JsonWriter writer, ReplayV2Tick tick)
    {
        writer.WriteStartObject();
        writer.WriteNumber("tick", tick.Tick);
        writer.WritePropertyName("tickStart");
        WriteTickStart(writer, tick.TickStart);
        writer.WritePropertyName("actors");
        writer.WriteStartArray();
        foreach (ReplayV2ActorTurn actor in tick.Actors
                     .OrderBy(value => value.ActorId))
        {
            WriteActorTurn(writer, actor);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("resolution");
        WriteAuthoritativeResolution(writer, tick.Resolution);
        writer.WritePropertyName("postState");
        WriteWorldState(writer, tick.PostState);
        writer.WriteEndObject();
    }

    private static void WriteTickStart(
        Utf8JsonWriter writer,
        ReplayV2TickStart tickStart)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("state");
        WriteWorldState(writer, tickStart.State);
        writer.WritePropertyName("activeActors");
        writer.WriteStartArray();
        foreach (ReplayV2ActorId actorId in tickStart.ActiveActors.Order())
            WriteActorId(writer, actorId);
        writer.WriteEndArray();
        writer.WritePropertyName("lifecycleEvents");
        writer.WriteStartArray();
        foreach (ReplayV2Event value in tickStart.LifecycleEvents)
            WriteEvent(writer, value);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteActorTurn(
        Utf8JsonWriter writer,
        ReplayV2ActorTurn turn)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, turn.ActorId);
        writer.WritePropertyName("lifeStart");
        WriteLifeStart(writer, turn.LifeStart);
        writer.WritePropertyName("observation");
        WriteObservation(writer, turn.Observation);
        writer.WritePropertyName("aliases");
        WriteObservationAliases(writer, turn.Aliases);
        writer.WritePropertyName("runtimeReply");
        WriteActorDecision(writer, turn.RuntimeReply);
        writer.WritePropertyName("acceptedDecision");
        WriteActorDecision(writer, turn.AcceptedDecision);
        writer.WritePropertyName("actionResolution");
        WriteActionResolution(writer, turn.ActionResolution);
        writer.WriteEndObject();
    }

    private static void WriteLifeStart(
        Utf8JsonWriter writer,
        ReplayV2LifeStart? lifeStart)
    {
        if (lifeStart is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", lifeStart.SchemaVersion);
        writer.WriteNumber(
            "runtimeContractVersion",
            lifeStart.RuntimeContractVersion);
        writer.WritePropertyName("actorId");
        WriteActorId(writer, lifeStart.ActorId);
        writer.WriteNumber("participantId", lifeStart.ParticipantId);
        writer.WriteString("actorRandomSeed", lifeStart.ActorRandomSeed);
        writer.WriteString(
            "spawnReason",
            SpawnReasonId(lifeStart.SpawnReason));
        writer.WriteString(
            "matchContractFingerprint",
            lifeStart.MatchContractFingerprint);
        writer.WriteEndObject();
    }

    private static void WriteObservationAliases(
        Utf8JsonWriter writer,
        ReplayV2ObservationAliases aliases)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("enemyLives");
        writer.WriteStartArray();
        foreach (ReplayV2EnemyLifeAlias alias in aliases.EnemyLives
                     .OrderBy(value =>
                         ReplayV2AliasHandles.ParseOrdinal(
                             value.LifeHandle,
                             ReplayV2AliasHandles.EnemyLifePrefix)))
        {
            writer.WriteStartObject();
            writer.WriteString("lifeHandle", alias.LifeHandle);
            writer.WritePropertyName("actorId");
            WriteActorId(writer, alias.ActorId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("projectiles");
        writer.WriteStartArray();
        foreach (ReplayV2ProjectileAlias alias in aliases.Projectiles
                     .OrderBy(value =>
                         ReplayV2AliasHandles.ParseOrdinal(
                             value.ProjectileHandle,
                             ReplayV2AliasHandles.ProjectilePrefix)))
        {
            writer.WriteStartObject();
            writer.WriteString(
                "projectileHandle",
                alias.ProjectileHandle);
            writer.WriteString("projectileId", alias.ProjectileId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("events");
        writer.WriteStartArray();
        foreach (ReplayV2EventAlias alias in aliases.Events
                     .OrderBy(value =>
                         ReplayV2AliasHandles.ParseOrdinal(
                             value.EventHandle,
                             ReplayV2AliasHandles.EventPrefix)))
        {
            writer.WriteStartObject();
            writer.WriteString("eventHandle", alias.EventHandle);
            writer.WriteString("eventId", alias.EventId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteObservation(
        Utf8JsonWriter writer,
        ReplayV2ActorObservation observation)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", observation.SchemaVersion);
        writer.WriteNumber("tick", observation.Tick);
        writer.WriteString(
            "matchContractFingerprint",
            observation.MatchContractFingerprint);
        writer.WriteString(
            "teamPerception",
            TeamPerceptionId(observation.TeamPerception));
        writer.WritePropertyName("self");
        WriteObservedSelf(writer, observation.Self);

        writer.WritePropertyName("teamUnits");
        writer.WriteStartArray();
        foreach (ReplayV2ObservedUnitSlot unit in observation.TeamUnits
                     .OrderBy(value => value.TeamId)
                     .ThenBy(value => value.UnitId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", unit.TeamId);
            writer.WriteNumber("unitId", unit.UnitId);
            writer.WriteString("formId", unit.FormId);
            writer.WriteString(
                "lifecycleStatus",
                LifecycleId(unit.LifecycleStatus));
            writer.WritePropertyName("activeActorId");
            WriteNullableActorId(writer, unit.ActiveActorId);
            WriteNullableNumber(writer, "respawnAtTick", unit.RespawnAtTick);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("allies");
        writer.WriteStartArray();
        foreach (ReplayV2ObservedAlly ally in observation.Allies
                     .OrderBy(value => value.ActorId))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("actorId");
            WriteActorId(writer, ally.ActorId);
            writer.WriteString("formId", ally.FormId);
            writer.WritePropertyName("position");
            WritePosition(writer, ally.Position);
            writer.WriteString("facing", DirectionId(ally.Facing));
            writer.WriteNumber("health", ally.Health);
            writer.WriteNumber("cooldown", ally.Cooldown);
            WriteNullableNumber(writer, "energy", ally.Energy);
            writer.WriteString(
                "previousActionResult",
                ActionResultId(ally.PreviousActionResult));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("enemies");
        writer.WriteStartArray();
        foreach (ReplayV2ObservedEnemy enemy in observation.Enemies
                     .OrderBy(value => value.Actor.TeamId)
                     .ThenBy(value => value.Actor.UnitId)
                     .ThenBy(value =>
                         ReplayV2AliasHandles.ParseOrdinal(
                             value.Actor.LifeHandle,
                             ReplayV2AliasHandles.EnemyLifePrefix)))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("actor");
            WriteEnemyActorRef(writer, enemy.Actor);
            writer.WriteString("formId", enemy.FormId);
            writer.WritePropertyName("position");
            WritePosition(writer, enemy.Position);
            writer.WriteString("facing", DirectionId(enemy.Facing));
            writer.WriteNumber("health", enemy.Health);
            writer.WritePropertyName("observedBy");
            WriteActorIds(writer, enemy.ObservedBy);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("visibleTiles");
        writer.WriteStartArray();
        foreach (ReplayV2ObservedMapTile tile in observation.VisibleTiles
                     .OrderBy(value => value.Position.Y)
                     .ThenBy(value => value.Position.X))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("position");
            WritePosition(writer, tile.Position);
            writer.WriteBoolean("isWall", tile.IsWall);
            writer.WritePropertyName("observedBy");
            WriteActorIds(writer, tile.ObservedBy);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("visibleProjectiles");
        if (observation.VisibleProjectiles is not { } projectiles)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (ReplayV2ObservedProjectile projectile in projectiles
                         .OrderBy(value =>
                             ReplayV2AliasHandles.ParseOrdinal(
                                 value.ProjectileHandle,
                                 ReplayV2AliasHandles.ProjectilePrefix))
                         .ThenBy(value => value.OwnerTeamId)
                         .ThenBy(value => value.AlliedOwnerActorId))
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "projectileHandle",
                    projectile.ProjectileHandle);
                writer.WriteNumber("ownerTeamId", projectile.OwnerTeamId);
                writer.WritePropertyName("alliedOwnerActorId");
                WriteNullableActorId(
                    writer,
                    projectile.AlliedOwnerActorId);
                writer.WritePropertyName("visibleEnemyOwner");
                WriteNullableEnemyActorRef(
                    writer,
                    projectile.VisibleEnemyOwner);
                writer.WritePropertyName("position");
                WritePosition(writer, projectile.Position);
                writer.WriteString(
                    "heading",
                    ProjectileHeadingId(projectile.Heading));
                writer.WriteNumber(
                    "tilesPerAdvance",
                    projectile.TilesPerAdvance);
                writer.WriteNumber(
                    "ticksUntilAdvance",
                    projectile.TicksUntilAdvance);
                writer.WriteNumber(
                    "remainingTiles",
                    projectile.RemainingTiles);
                writer.WritePropertyName("observedBy");
                WriteActorIds(writer, projectile.ObservedBy);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WritePropertyName("visibleEvents");
        writer.WriteStartArray();
        foreach (ReplayV2ObservedEvent value in observation.VisibleEvents
                     .OrderBy(item => item.SourceTick)
                     .ThenBy(item =>
                         ReplayV2AliasHandles.ParseOrdinal(
                             item.EventHandle,
                             ReplayV2AliasHandles.EventPrefix)))
        {
            WriteObservedEvent(writer, value);
        }
        writer.WriteEndArray();

        writer.WritePropertyName("heardSounds");
        if (observation.HeardSounds is not { } sounds)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (ReplayV2ObservedSound sound in sounds
                         .OrderBy(value => value.SourceTick)
                         .ThenBy(value =>
                             ReplayV2AliasHandles.ParseOrdinal(
                                 value.EventHandle,
                                 ReplayV2AliasHandles.EventPrefix))
                         .ThenBy(value => value.ObserverActorId))
            {
                writer.WriteStartObject();
                writer.WriteString("eventHandle", sound.EventHandle);
                writer.WriteNumber("sourceTick", sound.SourceTick);
                writer.WritePropertyName("observerActorId");
                WriteActorId(writer, sound.ObserverActorId);
                writer.WriteString(
                    "type",
                    ObservedEventTypeId(sound.Type));
                writer.WriteNumber("bearing", sound.Bearing);
                writer.WriteNumber("distance", sound.Distance);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WritePropertyName("frontlineObjective");
        if (observation.FrontlineObjective is not { } objective)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteNumber(
                "activePositionIndex",
                objective.ActivePositionIndex);
            WriteNullableNumber(
                writer,
                "claimingTeamId",
                objective.ClaimingTeamId);
            writer.WriteNumber(
                "captureProgress",
                objective.CaptureProgress);
            writer.WriteNumber(
                "decayTicksElapsed",
                objective.DecayTicksElapsed);
            writer.WriteNumber(
                "controlResumesAtTick",
                objective.ControlResumesAtTick);
            writer.WriteEndObject();
        }

        writer.WritePropertyName("actions");
        writer.WriteStartArray();
        foreach (ReplayV2ObservedActionAvailability action in
                 observation.Actions
                     .OrderBy(value => value.ActionCode)
                     .ThenBy(value => value.ActionId, StringComparer.Ordinal))
        {
            WriteObservedAction(writer, action);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteObservedSelf(
        Utf8JsonWriter writer,
        ReplayV2ObservedSelf self)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, self.ActorId);
        writer.WriteString("formId", self.FormId);
        writer.WritePropertyName("position");
        WritePosition(writer, self.Position);
        writer.WriteString("facing", DirectionId(self.Facing));
        writer.WriteNumber("health", self.Health);
        writer.WriteNumber("cooldown", self.Cooldown);
        WriteNullableNumber(writer, "energy", self.Energy);
        writer.WriteString(
            "previousActionResult",
            ActionResultId(self.PreviousActionResult));
        writer.WriteEndObject();
    }

    private static void WriteObservedEvent(
        Utf8JsonWriter writer,
        ReplayV2ObservedEvent value)
    {
        writer.WriteStartObject();
        writer.WriteString("eventHandle", value.EventHandle);
        writer.WriteNumber("sourceTick", value.SourceTick);
        writer.WriteString("type", ObservedEventTypeId(value.Type));
        WriteNullableNumber(writer, "teamId", value.TeamId);
        writer.WritePropertyName("alliedActorId");
        WriteNullableActorId(writer, value.AlliedActorId);
        writer.WritePropertyName("enemyActor");
        WriteNullableEnemyActorRef(writer, value.EnemyActor);
        WriteNullableString(
            writer,
            "projectileHandle",
            value.ProjectileHandle);
        writer.WritePropertyName("position");
        WriteNullablePosition(writer, value.Position);
        WriteNullableString(
            writer,
            "facing",
            value.Facing is { } facing ? DirectionId(facing) : null);
        WriteNullableNumber(writer, "amount", value.Amount);
        WriteNullableNumber(writer, "newHealth", value.NewHealth);
        writer.WritePropertyName("observedBy");
        WriteActorIds(writer, value.ObservedBy);
        writer.WriteEndObject();
    }

    private static void WriteObservedAction(
        Utf8JsonWriter writer,
        ReplayV2ObservedActionAvailability action)
    {
        writer.WriteStartObject();
        writer.WriteString("actionId", action.ActionId);
        writer.WriteNumber("actionCode", action.ActionCode);
        writer.WritePropertyName("parameterKinds");
        writer.WriteStartArray();
        foreach (PublicActionParameterKind parameterKind in
                 action.ParameterKinds.OrderBy(value => (int)value))
        {
            writer.WriteStringValue(ActionParameterKindId(parameterKind));
        }
        writer.WriteEndArray();
        writer.WriteBoolean("enabled", action.Enabled);
        writer.WriteBoolean("available", action.Available);
        WriteNullableBoolean(
            writer,
            "shotProgramAvailable",
            action.ShotProgramAvailable);

        writer.WritePropertyName("allowedDirections");
        if (action.AllowedDirections is not { } directions)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (Direction direction in directions.OrderBy(value => (int)value))
                writer.WriteStringValue(DirectionId(direction));
            writer.WriteEndArray();
        }

        writer.WritePropertyName("allowedUnitTargets");
        if (action.AllowedUnitTargets is not { } targets)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (ReplayV2ObservedUnitTarget target in targets
                         .OrderBy(value => value.TeamId)
                         .ThenBy(value => value.UnitId))
            {
                writer.WriteStartObject();
                writer.WriteNumber("teamId", target.TeamId);
                writer.WriteNumber("unitId", target.UnitId);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WritePropertyName("allowedFormTargets");
        if (action.AllowedFormTargets is not { } forms)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartArray();
            foreach (string form in forms.Order(StringComparer.Ordinal))
                writer.WriteStringValue(form);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteActorDecision(
        Utf8JsonWriter writer,
        ReplayV2ActorDecision decision)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "actionId", decision.ActionId);
        WriteNullableNumber(writer, "actionCode", decision.ActionCode);
        writer.WritePropertyName("payload");
        WriteActionPayload(writer, decision.Payload);
        WriteNullableString(
            writer,
            "debugMessage",
            decision.DebugMessage);
        writer.WriteBoolean("faulted", decision.Faulted);
        WriteNullableString(
            writer,
            "faultMessage",
            decision.FaultMessage);
        writer.WriteEndObject();
    }

    private static void WriteActionPayload(
        Utf8JsonWriter writer,
        ReplayV2ActionPayload? payload)
    {
        if (payload is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("shotProgram");
        WriteNullableShotProgram(writer, payload.ShotProgram);
        WriteNullableString(
            writer,
            "direction",
            payload.Direction is { } direction
                ? DirectionId(direction)
                : null);
        writer.WritePropertyName("unitTarget");
        if (payload.UnitTarget is not { } target)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", target.TeamId);
            writer.WriteNumber("unitId", target.UnitId);
            writer.WriteEndObject();
        }
        WriteNullableString(
            writer,
            "formTargetId",
            payload.FormTargetId);
        writer.WriteEndObject();
    }

    private static void WriteActionResolution(
        Utf8JsonWriter writer,
        ReplayV2ActionResolution resolution)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, resolution.ActorId);
        writer.WriteString("chosenActionId", resolution.ChosenActionId);
        writer.WriteNumber("chosenActionCode", resolution.ChosenActionCode);
        writer.WritePropertyName("chosenPayload");
        WriteActionPayload(writer, resolution.ChosenPayload);
        writer.WriteString(
            "validatedActionId",
            resolution.ValidatedActionId);
        writer.WriteNumber(
            "validatedActionCode",
            resolution.ValidatedActionCode);
        writer.WritePropertyName("validatedPayload");
        WriteActionPayload(writer, resolution.ValidatedPayload);
        writer.WriteString("result", ActionResultId(resolution.Result));
        writer.WriteEndObject();
    }

    private static void WriteAuthoritativeResolution(
        Utf8JsonWriter writer,
        ReplayV2AuthoritativeResolution resolution)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("events");
        writer.WriteStartArray();
        foreach (ReplayV2Event value in resolution.Events)
            WriteEvent(writer, value);
        writer.WriteEndArray();
        writer.WritePropertyName("projectileTraversals");
        writer.WriteStartArray();
        foreach (ReplayV2ProjectileTraversal traversal in
                 resolution.ProjectileTraversals)
        {
            WriteProjectileTraversal(writer, traversal);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteEvent(
        Utf8JsonWriter writer,
        ReplayV2Event value)
    {
        writer.WriteStartObject();
        writer.WriteString("eventId", value.EventId);
        writer.WriteNumber("tick", value.Tick);
        writer.WriteString("type", FrontlineEventTypeId(value.Type));
        WriteNullableNumber(writer, "teamId", value.TeamId);
        writer.WritePropertyName("sourceActorId");
        WriteNullableActorId(writer, value.SourceActorId);
        writer.WritePropertyName("targetActorId");
        WriteNullableActorId(writer, value.TargetActorId);
        WriteNullableString(writer, "projectileId", value.ProjectileId);
        writer.WritePropertyName("from");
        WriteNullablePosition(writer, value.From);
        writer.WritePropertyName("to");
        WriteNullablePosition(writer, value.To);
        WriteNullableString(
            writer,
            "fromFacing",
            value.FromFacing is { } fromFacing
                ? DirectionId(fromFacing)
                : null);
        WriteNullableString(
            writer,
            "toFacing",
            value.ToFacing is { } toFacing
                ? DirectionId(toFacing)
                : null);
        WriteNullableString(
            writer,
            "projectileHeading",
            value.ProjectileHeading is { } heading
                ? ProjectileHeadingId(heading)
                : null);
        WriteNullableString(writer, "actionId", value.ActionId);
        WriteNullableNumber(writer, "actionCode", value.ActionCode);
        writer.WritePropertyName("actionPayload");
        WriteActionPayload(writer, value.ActionPayload);
        WriteNullableString(
            writer,
            "actionResult",
            value.ActionResult is { } result
                ? ActionResultId(result)
                : null);
        WriteNullableNumber(writer, "amount", value.Amount);
        WriteNullableNumber(writer, "newHealth", value.NewHealth);
        WriteNullableString(
            writer,
            "lifecycleStatus",
            value.LifecycleStatus is { } lifecycle
                ? LifecycleId(lifecycle)
                : null);
        WriteNullableNumber(writer, "respawnAtTick", value.RespawnAtTick);
        WriteNullableNumber(
            writer,
            "fromPositionIndex",
            value.FromPositionIndex);
        WriteNullableNumber(
            writer,
            "toPositionIndex",
            value.ToPositionIndex);
        WriteNullableNumber(
            writer,
            "claimingTeamId",
            value.ClaimingTeamId);
        WriteNullableNumber(
            writer,
            "captureProgress",
            value.CaptureProgress);
        WriteNullableNumber(
            writer,
            "controlResumesAtTick",
            value.ControlResumesAtTick);
        writer.WriteEndObject();
    }

    private static void WriteProjectileTraversal(
        Utf8JsonWriter writer,
        ReplayV2ProjectileTraversal traversal)
    {
        writer.WriteStartObject();
        writer.WriteString("projectileId", traversal.ProjectileId);
        writer.WritePropertyName("ownerActorId");
        WriteActorId(writer, traversal.OwnerActorId);
        writer.WriteString(
            "launchDirection",
            DirectionId(traversal.LaunchDirection));
        writer.WritePropertyName("from");
        WritePosition(writer, traversal.From);
        writer.WritePropertyName("path");
        WritePositions(writer, traversal.Path);
        WriteNullableString(
            writer,
            "heading",
            traversal.Heading is { } heading
                ? ProjectileHeadingId(heading)
                : null);
        writer.WritePropertyName("shotProgram");
        WriteNullableShotProgram(writer, traversal.ShotProgram);
        writer.WritePropertyName("programmedPath");
        WriteNullablePositions(writer, traversal.ProgrammedPath);
        writer.WriteEndObject();
    }

    private static void WriteWorldState(
        Utf8JsonWriter writer,
        ReplayV2WorldState state)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("teams");
        writer.WriteStartArray();
        foreach (ReplayV2TeamState team in state.Teams
                     .OrderBy(value => value.TeamId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", team.TeamId);
            writer.WriteString("damageDealt", team.DamageDealt);
            writer.WritePropertyName("units");
            writer.WriteStartArray();
            foreach (ReplayV2UnitState unit in team.Units
                         .OrderBy(value => value.UnitId))
            {
                WriteUnitState(writer, unit);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("projectiles");
        writer.WriteStartArray();
        foreach (ReplayV2ProjectileState projectile in state.Projectiles
                     .OrderBy(
                         value => ParseWireId(value.ProjectileId)))
        {
            WriteProjectileState(writer, projectile);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("objective");
        WriteControl(writer, state.Control);
        writer.WriteEndObject();
    }

    private static void WriteUnitState(
        Utf8JsonWriter writer,
        ReplayV2UnitState unit)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", unit.TeamId);
        writer.WriteNumber("unitId", unit.UnitId);
        writer.WriteString("formId", unit.FormId);
        writer.WriteString(
            "lifecycleStatus",
            LifecycleId(unit.LifecycleStatus));
        WriteNullableNumber(writer, "respawnAtTick", unit.RespawnAtTick);
        writer.WriteString("damageDealt", unit.DamageDealt);
        writer.WritePropertyName("activeLife");
        if (unit.ActiveLife is not { } life)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WritePropertyName("actorId");
            WriteActorId(writer, life.ActorId);
            writer.WritePropertyName("position");
            WritePosition(writer, life.Position);
            writer.WriteString("facing", DirectionId(life.Facing));
            writer.WriteNumber("health", life.Health);
            writer.WriteNumber("cooldown", life.Cooldown);
            WriteNullableNumber(writer, "energy", life.Energy);
            writer.WriteString("damageDealt", life.DamageDealt);
            writer.WriteString(
                "previousActionResult",
                ActionResultId(life.PreviousActionResult));
            writer.WriteNumber("spawnedAtTick", life.SpawnedAtTick);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteProjectileState(
        Utf8JsonWriter writer,
        ReplayV2ProjectileState projectile)
    {
        writer.WriteStartObject();
        writer.WriteString("projectileId", projectile.ProjectileId);
        writer.WritePropertyName("ownerActorId");
        WriteActorId(writer, projectile.OwnerActorId);
        writer.WritePropertyName("position");
        WritePosition(writer, projectile.Position);
        writer.WriteString(
            "launchDirection",
            DirectionId(projectile.LaunchDirection));
        WriteNullableString(
            writer,
            "heading",
            projectile.Heading is { } heading
                ? ProjectileHeadingId(heading)
                : null);
        writer.WritePropertyName("shotProgram");
        WriteNullableShotProgram(writer, projectile.ShotProgram);
        writer.WritePropertyName("programmedPath");
        WriteNullablePositions(writer, projectile.ProgrammedPath);
        writer.WriteNumber(
            "nextProgrammedPathIndex",
            projectile.NextProgrammedPathIndex);
        writer.WriteNumber("tilesTraveled", projectile.TilesTraveled);
        writer.WriteNumber("phase", projectile.Phase);
        writer.WriteEndObject();
    }

    private static void WriteResult(
        Utf8JsonWriter writer,
        ReplayV2Result result)
    {
        writer.WriteStartObject();
        WriteNullableNumber(writer, "winnerTeamId", result.WinnerTeamId);
        writer.WriteString("reason", MatchEndReasonId(result.Reason));
        writer.WriteNumber("endTick", result.EndTick);
        writer.WriteString("territorialScore", result.TerritorialScore);
        writer.WritePropertyName("objective");
        WriteControl(writer, result.Control);
        writer.WritePropertyName("teams");
        writer.WriteStartArray();
        foreach (ReplayV2TeamResult team in result.Teams
                     .OrderBy(value => value.TeamId))
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", team.TeamId);
            writer.WriteString("outcome", TeamOutcomeId(team.Outcome));
            writer.WriteNumber("finalHealth", team.FinalHealth);
            writer.WriteString("damageDealt", team.DamageDealt);
            writer.WriteString(
                "finalLifecycleStatus",
                LifecycleId(team.FinalLifecycleStatus));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteControl(
        Utf8JsonWriter writer,
        ReplayV2ControlState control)
    {
        writer.WriteStartObject();
        writer.WriteNumber("nextTick", control.NextTick);
        writer.WriteNumber(
            "activePositionIndex",
            control.ActivePositionIndex);
        WriteNullableNumber(
            writer,
            "claimingTeamId",
            control.ClaimingTeamId);
        writer.WriteNumber("captureProgress", control.CaptureProgress);
        writer.WriteNumber(
            "decayTicksElapsed",
            control.DecayTicksElapsed);
        writer.WriteNumber(
            "controlResumesAtTick",
            control.ControlResumesAtTick);
        WriteNullableNumber(writer, "winnerTeamId", control.WinnerTeamId);
        writer.WriteEndObject();
    }

    private static void WriteActorId(
        Utf8JsonWriter writer,
        ReplayV2ActorId actorId)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", actorId.TeamId);
        writer.WriteNumber("unitId", actorId.UnitId);
        writer.WriteNumber("lifeId", actorId.LifeId);
        writer.WriteEndObject();
    }

    private static void WriteNullableActorId(
        Utf8JsonWriter writer,
        ReplayV2ActorId? actorId)
    {
        if (actorId is { } value)
            WriteActorId(writer, value);
        else
            writer.WriteNullValue();
    }

    private static void WriteEnemyActorRef(
        Utf8JsonWriter writer,
        ReplayV2ObservedEnemyActorRef actor)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", actor.TeamId);
        writer.WriteNumber("unitId", actor.UnitId);
        writer.WriteString("lifeHandle", actor.LifeHandle);
        writer.WriteEndObject();
    }

    private static void WriteNullableEnemyActorRef(
        Utf8JsonWriter writer,
        ReplayV2ObservedEnemyActorRef? actor)
    {
        if (actor is not null)
            WriteEnemyActorRef(writer, actor);
        else
            writer.WriteNullValue();
    }

    private static void WriteActorIds(
        Utf8JsonWriter writer,
        IEnumerable<ReplayV2ActorId> actorIds)
    {
        writer.WriteStartArray();
        foreach (ReplayV2ActorId actorId in actorIds.Order())
            WriteActorId(writer, actorId);
        writer.WriteEndArray();
    }

    private static void WritePosition(
        Utf8JsonWriter writer,
        Position position)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", position.X);
        writer.WriteNumber("y", position.Y);
        writer.WriteEndObject();
    }

    private static void WriteNullablePosition(
        Utf8JsonWriter writer,
        Position? position)
    {
        if (position is { } value)
            WritePosition(writer, value);
        else
            writer.WriteNullValue();
    }

    private static void WritePositions(
        Utf8JsonWriter writer,
        IEnumerable<Position> positions)
    {
        writer.WriteStartArray();
        foreach (Position position in positions)
            WritePosition(writer, position);
        writer.WriteEndArray();
    }

    private static void WriteNullablePositions(
        Utf8JsonWriter writer,
        ImmutableArray<Position>? positions)
    {
        if (positions is { } values)
            WritePositions(writer, values);
        else
            writer.WriteNullValue();
    }

    private static void WriteNullableShotProgram(
        Utf8JsonWriter writer,
        ShotProgram? program)
    {
        if (program is not { } value)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("initialAimOffset", value.InitialAimOffset);
        writer.WriteNumber("bendDirection", value.BendDirection);
        writer.WriteNumber("bendAfterTiles", value.BendAfterTiles);
        writer.WriteNumber("bendEveryTiles", value.BendEveryTiles);
        writer.WriteNumber("bendCount", value.BendCount);
        writer.WriteEndObject();
    }

    private static void WriteNullableString(
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value is null)
            writer.WriteNull(propertyName);
        else
            writer.WriteString(propertyName, value);
    }

    private static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        int? value)
    {
        if (value is int number)
            writer.WriteNumber(propertyName, number);
        else
            writer.WriteNull(propertyName);
    }

    private static void WriteNullableBoolean(
        Utf8JsonWriter writer,
        string propertyName,
        bool? value)
    {
        if (value is bool boolean)
            writer.WriteBoolean(propertyName, boolean);
        else
            writer.WriteNull(propertyName);
    }

    private static byte[] Write(Action<Utf8JsonWriter> write)
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
        return stream.ToArray();
    }

    private static void Validate(ReplayV2 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ValidateHeader(replay.Header);
        ValidateTicks(replay.Header, replay.Ticks);
        if (replay.Ticks.IsEmpty)
        {
            throw new ArgumentException(
                "A finalized replay-v2 document must contain at least one tick.",
                nameof(replay));
        }
        ValidateResult(replay);
    }

    private static void ValidatePrefix(
        ReplayV2Header header,
        ImmutableArray<ReplayV2Tick> ticks)
    {
        ValidateHeader(header);
        ValidateTicks(header, ticks);
    }

    private static void ValidateHeader(ReplayV2Header header)
    {
        ArgumentNullException.ThrowIfNull(header);
        if (header.ReplayVersion
            != BotArenaVersions.EntityReplayFormatVersion)
        {
            throw new ArgumentException(
                $"Replay-v2 header version must be {BotArenaVersions.EntityReplayFormatVersion}.",
                nameof(header));
        }
        ArgumentNullException.ThrowIfNull(header.ActorRuntime);
        if (!string.Equals(
                header.ActorRuntime.Family,
                "nilbots-actor",
                StringComparison.Ordinal)
            || header.ActorRuntime.Version
                != BotArenaVersions.ActorRuntimeContractVersion
            || header.ActorRuntime.MatchStartSchemaVersion
                != BotArenaVersions.ActorMatchStartSchemaVersion
            || header.ActorRuntime.ObservationSchemaVersion
                != BotArenaVersions.ActorObservationSchemaVersion
            || header.ActorRuntime.DecisionSchemaVersion
                != BotArenaVersions.ActorDecisionSchemaVersion)
        {
            throw new ArgumentException(
                "Replay-v2 actorRuntime must exactly identify the supported actor contract and schemas.",
                nameof(header));
        }
        if (!IsCanonicalSeed(header.Seed))
        {
            throw new ArgumentException(
                "Replay-v2 seed must be a canonical unsigned decimal string.",
                nameof(header));
        }
        ArgumentNullException.ThrowIfNull(header.Contract);
        if (!string.Equals(
                header.GameRulesVersion,
                header.Contract.Rules.RulesetId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay gameRulesVersion must match the embedded ruleset ID.",
                nameof(header));
        }
        string expectedFingerprint =
            MatchContractFingerprint.ComputeMatch(header.Contract);
        if (!string.Equals(
                header.Contract.MatchContractFingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Embedded match-contract fingerprint is invalid.",
                nameof(header));
        }

        RequireInitialized(header.Participants, "header.participants");
        RequireInitialized(
            header.Contract.Rules.Actions,
            "header.contract.rules.actions");
        EnsureUnique(
            header.Participants,
            value => value.ParticipantId,
            "participant IDs");
        EnsureUnique(
            header.Contract.Rules.Actions,
            value => value.Id,
            "contract action IDs");
        EnsureUnique(
            header.Contract.Rules.Actions,
            value => value.Code,
            "contract action codes");
        foreach (PublicActionDefinition action in
                 header.Contract.Rules.Actions)
        {
            RequireInitialized(
                action.ParameterKinds,
                $"contract action '{action.Id}' parameter kinds");
            if (!action.ParameterKinds
                    .All(kind => Enum.IsDefined(typeof(
                        PublicActionParameterKind), kind))
                || action.ParameterKinds.Distinct().Count()
                    != action.ParameterKinds.Length
                || !action.ParameterKinds.SequenceEqual(
                    action.ParameterKinds.OrderBy(kind => (int)kind)))
            {
                throw new ArgumentException(
                    $"Contract action '{action.Id}' parameter kinds must be known, unique, and canonical.",
                    nameof(header));
            }
        }

        PublicParticipant[] expectedParticipants =
            header.Contract.Topology.Participants
                .OrderBy(value => value.ParticipantId)
                .ToArray();
        ReplayV2ParticipantController[] actualParticipants =
            header.Participants
                .OrderBy(value => value.ParticipantId)
                .ToArray();
        if (expectedParticipants.Length != actualParticipants.Length
            || expectedParticipants
                .Zip(actualParticipants)
                .Any(pair =>
                    pair.First.ParticipantId != pair.Second.ParticipantId
                    || pair.First.TeamId != pair.Second.TeamId))
        {
            throw new ArgumentException(
                "Replay participant metadata must exactly match contract topology.",
                nameof(header));
        }
    }

    private static void ValidateTicks(
        ReplayV2Header header,
        ImmutableArray<ReplayV2Tick> ticks)
    {
        RequireInitialized(ticks, "ticks");
        EnsureUnique(ticks, value => value.Tick, "tick IDs");
        int[] tickIds = ticks
            .Select(value => value.Tick)
            .Order()
            .ToArray();
        for (int index = 0; index < tickIds.Length; index++)
        {
            if (tickIds[index] != index)
            {
                throw new ArgumentException(
                    "Replay-v2 ticks must be contiguous from zero.",
                    nameof(ticks));
            }
        }

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        var aliasHistories =
            new Dictionary<ReplayV2AliasAudience, ReplayV2AliasHistory>();
        ImmutableArray<ReplayV2Event> priorResolutionEvents = [];
        var seenActorLives = new HashSet<ReplayV2ActorId>();
        ReplayV2WorldState? priorPostState = null;
        int[] expectedTeamIds = header.Contract.Topology.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        foreach (ReplayV2Tick tick in ticks.OrderBy(value => value.Tick))
        {
            RequireInitialized(
                tick.TickStart.ActiveActors,
                $"ticks[{tick.Tick}].tickStart.activeActors");
            RequireInitialized(
                tick.TickStart.LifecycleEvents,
                $"ticks[{tick.Tick}].tickStart.lifecycleEvents");
            RequireInitialized(
                tick.TickStart.State.Teams,
                $"ticks[{tick.Tick}].tickStart.state.teams");
            RequireInitialized(
                tick.TickStart.State.Projectiles,
                $"ticks[{tick.Tick}].tickStart.state.projectiles");
            RequireInitialized(tick.Actors, $"ticks[{tick.Tick}].actors");
            RequireInitialized(
                tick.Resolution.Events,
                $"ticks[{tick.Tick}].resolution.events");
            RequireInitialized(
                tick.Resolution.ProjectileTraversals,
                $"ticks[{tick.Tick}].resolution.projectileTraversals");
            RequireInitialized(
                tick.PostState.Teams,
                $"ticks[{tick.Tick}].postState.teams");
            RequireInitialized(
                tick.PostState.Projectiles,
                $"ticks[{tick.Tick}].postState.projectiles");
            if (priorPostState is not null
                && tick.TickStart.LifecycleEvents.IsEmpty
                && !WorldStatesEqual(
                    priorPostState,
                    tick.TickStart.State))
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} tick-start state must exactly equal the prior tick's post-state.",
                    nameof(ticks));
            }

            EnsureUnique(
                tick.TickStart.ActiveActors,
                value => value,
                $"tick {tick.Tick} active actor IDs");
            EnsureUnique(
                tick.Actors,
                value => value.ActorId,
                $"tick {tick.Tick} actor turn IDs");
            ReplayV2ActorId[] active =
                tick.TickStart.ActiveActors.Order().ToArray();
            ReplayV2ActorId[] turns =
                tick.Actors.Select(value => value.ActorId).Order().ToArray();
            if (!active.SequenceEqual(turns))
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} active actors must exactly match actor turns.",
                    nameof(ticks));
            }
            ReplayV2ActorId[] stateActors = tick.TickStart.State.Teams
                .SelectMany(team => team.Units)
                .Where(unit => unit.ActiveLife is not null)
                .Select(unit => unit.ActiveLife!.ActorId)
                .Order()
                .ToArray();
            if (!active.SequenceEqual(stateActors))
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} tick-start state must exactly match active actors.",
                    nameof(ticks));
            }
            if (tick.TickStart.State.Control.NextTick != tick.Tick)
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} tick-start objective must identify the prepared tick.",
                    nameof(ticks));
            }
            if (tick.PostState.Control.NextTick != checked(tick.Tick + 1))
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} post-state objective must identify the next tick.",
                    nameof(ticks));
            }

            foreach (ReplayV2Event value in tick.TickStart.LifecycleEvents)
                ValidateEvent(
                    value,
                    tick.Tick,
                    eventIds,
                    header.Contract);
            foreach (ReplayV2Event value in tick.Resolution.Events)
                ValidateEvent(
                    value,
                    tick.Tick,
                    eventIds,
                    header.Contract);
            ReplayV2Event[] observableEvents =
                priorResolutionEvents
                    .Concat(tick.TickStart.LifecycleEvents)
                    .ToArray();
            Dictionary<string, ReplayV2Event> observableEventsById =
                observableEvents.ToDictionary(
                    value => value.EventId,
                    StringComparer.Ordinal);
            HashSet<ReplayV2ActorId> observableActorIds =
                tick.TickStart.State.Teams
                    .SelectMany(team => team.Units)
                    .Where(unit => unit.ActiveLife is not null)
                    .Select(unit => unit.ActiveLife!.ActorId)
                    .Concat(observableEvents
                        .SelectMany(value => new[]
                        {
                            value.SourceActorId,
                            value.TargetActorId,
                        })
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value))
                    .ToHashSet();
            HashSet<string> observableProjectileIds =
                tick.TickStart.State.Projectiles
                    .Select(value => value.ProjectileId)
                    .Concat(observableEvents
                        .Where(value => value.ProjectileId is not null)
                        .Select(value => value.ProjectileId!))
                    .ToHashSet(StringComparer.Ordinal);
            foreach (ReplayV2ProjectileTraversal traversal in
                     tick.Resolution.ProjectileTraversals)
            {
                RequireWireId(
                    traversal.ProjectileId,
                    $"tick {tick.Tick} traversal projectile ID");
                RequireInitialized(
                    traversal.Path,
                    $"tick {tick.Tick} traversal path");
                if (traversal.ProgrammedPath is { } programmedPath)
                {
                    RequireInitialized(
                        programmedPath,
                        $"tick {tick.Tick} traversal programmed path");
                }
            }

            foreach (ReplayV2ActorTurn turn in tick.Actors)
            {
                ValidateActorTurn(
                    header,
                    tick,
                    turn,
                    seenActorLives.Add(turn.ActorId),
                    aliasHistories,
                    observableActorIds,
                    observableProjectileIds,
                    observableEventsById);
            }
            ValidateWorldState(
                tick.TickStart.State,
                tick.Tick,
                "tick-start",
                expectedTeamIds);
            ValidateWorldState(
                tick.PostState,
                tick.Tick,
                "post-state",
                expectedTeamIds);
            priorResolutionEvents = tick.Resolution.Events;
            priorPostState = tick.PostState;
        }
    }

    private static void ValidateResult(ReplayV2 replay)
    {
        RequireInitialized(replay.Result.Teams, "result.teams");
        RequireWireInt64(
            replay.Result.TerritorialScore,
            "result territorialScore",
            nonNegative: false);
        EnsureUnique(
            replay.Result.Teams,
            value => value.TeamId,
            "result team IDs");
        foreach (ReplayV2TeamResult team in replay.Result.Teams)
        {
            RequireWireInt64(
                team.DamageDealt,
                $"result team {team.TeamId} damageDealt",
                nonNegative: true);
        }

        ReplayV2Tick finalTick =
            replay.Ticks.OrderBy(tick => tick.Tick).Last();
        // FrontlineMatchResult.EndTick is the zero-based tick that executed,
        // while the post-state control snapshot points at the following tick.
        if (replay.Result.EndTick != finalTick.Tick
            || (long)replay.Result.EndTick + 1
                != finalTick.PostState.Control.NextTick)
        {
            throw new ArgumentException(
                "Replay result endTick must match the final executed tick.",
                nameof(replay));
        }
        if (replay.Result.Control != finalTick.PostState.Control)
        {
            throw new ArgumentException(
                "Replay result objective must equal the final post-state objective.",
                nameof(replay));
        }

        int[] topologyTeamIds = replay.Header.Contract.Topology.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        int[] postStateTeamIds = finalTick.PostState.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        int[] resultTeamIds = replay.Result.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        if (!topologyTeamIds.SequenceEqual(postStateTeamIds)
            || !topologyTeamIds.SequenceEqual(resultTeamIds))
        {
            throw new ArgumentException(
                "Replay result, final post-state, and topology team IDs must match.",
                nameof(replay));
        }
        if (replay.Result.WinnerTeamId is int winnerTeamId
            && !topologyTeamIds.Contains(winnerTeamId))
        {
            throw new ArgumentException(
                "Replay result winnerTeamId must reference a topology team.",
                nameof(replay));
        }
        if (replay.Result.Reason == FrontlineMatchEndReason.BaseBreach
            && replay.Result.WinnerTeamId
                != replay.Result.Control.WinnerTeamId)
        {
            throw new ArgumentException(
                "A base-breach winner must match the final objective winner.",
                nameof(replay));
        }
    }

    private static void ValidateActorTurn(
        ReplayV2Header header,
        ReplayV2Tick tick,
        ReplayV2ActorTurn turn,
        bool isFirstTurnForLife,
        IDictionary<ReplayV2AliasAudience, ReplayV2AliasHistory>
            aliasHistories,
        IReadOnlySet<ReplayV2ActorId> observableActorIds,
        IReadOnlySet<string> observableProjectileIds,
        IReadOnlyDictionary<string, ReplayV2Event> observableEvents)
    {
        ReplayV2ActorObservation observation = turn.Observation;
        if (observation.Tick != tick.Tick
            || observation.Self.ActorId != turn.ActorId
            || turn.ActionResolution.ActorId != turn.ActorId)
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} actor turn identity/chronology is inconsistent.",
                nameof(turn));
        }
        if (!string.Equals(
                observation.MatchContractFingerprint,
                header.Contract.MatchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} observation contract fingerprint is inconsistent.",
                nameof(turn));
        }
        PublicFrontlineDefinition frontline =
            header.Contract.Rules.Frontline
            ?? throw new ArgumentException(
                "Replay-v2 actor observations require a Frontline contract.",
                nameof(header));
        if (observation.SchemaVersion
                != header.ActorRuntime.ObservationSchemaVersion
            || observation.TeamPerception != frontline.TeamPerception)
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} observation schema/team perception must match the actor runtime and Frontline contract.",
                nameof(turn));
        }
        ValidateLifeStart(
            header,
            tick,
            turn,
            isFirstTurnForLife);

        ReplayV2TeamState? authoritativeTeam =
            tick.TickStart.State.Teams.FirstOrDefault(
                team => team.TeamId == turn.ActorId.TeamId);
        ReplayV2UnitState? authoritativeUnit = authoritativeTeam?.Units
            .FirstOrDefault(unit => unit.UnitId == turn.ActorId.UnitId);
        ReplayV2LifeState? authoritativeLife = authoritativeUnit?.ActiveLife;
        ReplayV2ObservedSelf self = observation.Self;
        if (authoritativeUnit is null
            || authoritativeLife is null
            || authoritativeLife.ActorId != turn.ActorId
            || self.ActorId != authoritativeLife.ActorId
            || !string.Equals(
                self.FormId,
                authoritativeUnit.FormId,
                StringComparison.Ordinal)
            || self.Position != authoritativeLife.Position
            || self.Facing != authoritativeLife.Facing
            || self.Health != authoritativeLife.Health
            || self.Cooldown != authoritativeLife.Cooldown
            || self.Energy != authoritativeLife.Energy
            || self.PreviousActionResult
                != authoritativeLife.PreviousActionResult)
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} observation self must equal authoritative tick-start state.",
                nameof(turn));
        }
        RequireInitialized(observation.TeamUnits, "observation.teamUnits");
        RequireInitialized(observation.Allies, "observation.allies");

        ReplayV2ObservedUnitSlot[] observedUnits =
            observation.TeamUnits
                .OrderBy(unit => unit.UnitId)
                .ToArray();
        ReplayV2UnitState[] expectedUnits =
            authoritativeTeam!.Units
                .OrderBy(unit => unit.UnitId)
                .ToArray();
        if (observedUnits.Length != expectedUnits.Length
            || observedUnits.Zip(expectedUnits).Any(pair =>
                pair.First.TeamId != pair.Second.TeamId
                || pair.First.UnitId != pair.Second.UnitId
                || !string.Equals(
                    pair.First.FormId,
                    pair.Second.FormId,
                    StringComparison.Ordinal)
                || pair.First.LifecycleStatus
                    != pair.Second.LifecycleStatus
                || pair.First.ActiveActorId
                    != pair.Second.ActiveLife?.ActorId
                || pair.First.RespawnAtTick
                    != pair.Second.RespawnAtTick))
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} observation teamUnits must equal authoritative tick-start state.",
                nameof(turn));
        }

        Dictionary<ReplayV2ActorId, ReplayV2LifeState> expectedAllies =
            authoritativeTeam.Units
                .Where(unit =>
                    unit.ActiveLife is not null
                    && unit.ActiveLife.ActorId != turn.ActorId)
                .ToDictionary(
                    unit => unit.ActiveLife!.ActorId,
                    unit => unit.ActiveLife!);
        if (observation.Allies.Length != expectedAllies.Count
            || observation.Allies.Any(ally =>
                !expectedAllies.TryGetValue(
                    ally.ActorId,
                    out ReplayV2LifeState? life)
                || tick.TickStart.State.Teams
                    .SelectMany(team => team.Units)
                    .Single(unit =>
                        unit.ActiveLife?.ActorId == ally.ActorId)
                    .FormId != ally.FormId
                || life.Position != ally.Position
                || life.Facing != ally.Facing
                || life.Health != ally.Health
                || life.Cooldown != ally.Cooldown
                || life.Energy != ally.Energy
                || life.PreviousActionResult
                    != ally.PreviousActionResult))
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} observation allies must equal authoritative tick-start state.",
                nameof(turn));
        }

        ValidateActionResolution(
            header.Contract,
            turn.ActionResolution,
            tick.Tick);
        ValidateActorDecisions(
            header.Contract,
            turn,
            tick.Tick);

        RequireInitialized(observation.TeamUnits, "observation.teamUnits");
        RequireInitialized(observation.Allies, "observation.allies");
        RequireInitialized(observation.Enemies, "observation.enemies");
        RequireInitialized(
            observation.VisibleTiles,
            "observation.visibleTiles");
        RequireInitialized(
            observation.VisibleEvents,
            "observation.visibleEvents");
        RequireInitialized(observation.Actions, "observation.actions");
        if (observation.VisibleProjectiles is { } projectiles)
        {
            RequireInitialized(
                projectiles,
                "observation.visibleProjectiles");
        }
        if (observation.HeardSounds is { } sounds)
            RequireInitialized(sounds, "observation.heardSounds");
        ValidateObservationAliases(
            turn,
            aliasHistories,
            tick.TickStart.State,
            observableActorIds,
            observableProjectileIds,
            observableEvents,
            header.Contract.Topology.Teams
                .Select(team => team.TeamId)
                .ToHashSet());

        foreach (ReplayV2ObservedEnemy enemy in observation.Enemies)
            RequireInitialized(enemy.ObservedBy, "enemy.observedBy");
        foreach (ReplayV2ObservedMapTile tile in observation.VisibleTiles)
            RequireInitialized(tile.ObservedBy, "tile.observedBy");
        foreach (ReplayV2ObservedEvent value in observation.VisibleEvents)
        {
            RequireInitialized(value.ObservedBy, "event.observedBy");
        }
        foreach (ReplayV2ObservedActionAvailability action in
                 observation.Actions)
        {
            RequireInitialized(
                action.ParameterKinds,
                "action.parameterKinds");
            if (!action.ParameterKinds
                    .All(kind => Enum.IsDefined(typeof(
                        PublicActionParameterKind), kind))
                || action.ParameterKinds.Distinct().Count()
                    != action.ParameterKinds.Length
                || !action.ParameterKinds.SequenceEqual(
                    action.ParameterKinds.OrderBy(kind => (int)kind)))
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} action '{action.ActionId}' parameter kinds must be known, unique, and canonical.",
                    nameof(turn));
            }
            PublicActionDefinition knownAction = ResolveAction(
                header.Contract,
                action.ActionId,
                action.ActionCode,
                $"tick {tick.Tick} observation");
            if (action.Enabled != knownAction.Enabled
                || !action.ParameterKinds.SequenceEqual(
                    knownAction.ParameterKinds))
            {
                throw new ArgumentException(
                    $"Tick {tick.Tick} action '{action.ActionId}' must match its contract definition.",
                    nameof(turn));
            }
            if (action.AllowedDirections is { } directions)
                RequireInitialized(directions, "action.allowedDirections");
            if (action.AllowedUnitTargets is { } targets)
                RequireInitialized(targets, "action.allowedUnitTargets");
            if (action.AllowedFormTargets is { } forms)
                RequireInitialized(forms, "action.allowedFormTargets");
        }
        EnsureUnique(
            observation.Actions,
            action => action.ActionId,
            $"tick {tick.Tick} observation action IDs");
        EnsureUnique(
            observation.Actions,
            action => action.ActionCode,
            $"tick {tick.Tick} observation action codes");
        (string Id, int Code)[] observedActionCatalog =
            observation.Actions
                .Select(action => (action.ActionId, action.ActionCode))
                .OrderBy(action => action.ActionCode)
                .ThenBy(action => action.ActionId, StringComparer.Ordinal)
                .ToArray();
        (string Id, int Code)[] contractActionCatalog =
            header.Contract.Rules.Actions
                .Select(action => (action.Id, action.Code))
                .OrderBy(action => action.Code)
                .ThenBy(action => action.Id, StringComparer.Ordinal)
                .ToArray();
        if (!observedActionCatalog.SequenceEqual(contractActionCatalog))
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} observation actions must contain the complete contract catalog.",
                nameof(turn));
        }
    }

    private static void ValidateLifeStart(
        ReplayV2Header header,
        ReplayV2Tick tick,
        ReplayV2ActorTurn turn,
        bool isFirstTurnForLife)
    {
        if (isFirstTurnForLife != (turn.LifeStart is not null))
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} lifeStart must appear exactly on an actor life's first turn.",
                nameof(turn));
        }
        if (turn.LifeStart is not { } start)
            return;

        PublicUnitSlot? unit = header.Contract.Topology.UnitSlots
            .FirstOrDefault(value =>
                value.TeamId == turn.ActorId.TeamId
                && value.UnitId == turn.ActorId.UnitId);
        bool isInitialLife = header.Contract.Topology.InitialLives.Any(
            value =>
                value.TeamId == turn.ActorId.TeamId
                && value.UnitId == turn.ActorId.UnitId
                && value.LifeId == turn.ActorId.LifeId);
        if (unit is null
            || start.SchemaVersion
                != header.ActorRuntime.MatchStartSchemaVersion
            || start.RuntimeContractVersion
                != header.ActorRuntime.Version
            || start.ActorId != turn.ActorId
            || start.ParticipantId != unit.ControllerParticipantId
            || !IsCanonicalSeed(start.ActorRandomSeed)
            || !Enum.IsDefined(start.SpawnReason)
            || !string.Equals(
                start.MatchContractFingerprint,
                header.Contract.MatchContractFingerprint,
                StringComparison.Ordinal)
            || (isInitialLife
                && start.SpawnReason != ActorSpawnReason.Initial)
            || (!isInitialLife
                && turn.ActorId.UnitId == 0
                && start.SpawnReason != ActorSpawnReason.Respawn))
        {
            throw new ArgumentException(
                $"Tick {tick.Tick} lifeStart must match its actor, controller, contract, schemas, seed shape, and spawn chronology.",
                nameof(turn));
        }
    }

    private static void ValidateObservationAliases(
        ReplayV2ActorTurn turn,
        IDictionary<ReplayV2AliasAudience, ReplayV2AliasHistory>
            aliasHistories,
        ReplayV2WorldState authoritativeState,
        IReadOnlySet<ReplayV2ActorId> observableActorIds,
        IReadOnlySet<string> observableProjectileIds,
        IReadOnlyDictionary<string, ReplayV2Event> observableEvents,
        IReadOnlySet<int> topologyTeamIds)
    {
        ReplayV2ObservationAliases aliases = turn.Aliases
            ?? throw new ArgumentException(
                "Replay actor-turn aliases cannot be null.",
                nameof(turn));
        RequireInitialized(
            aliases.EnemyLives,
            "actorTurn.aliases.enemyLives");
        RequireInitialized(
            aliases.Projectiles,
            "actorTurn.aliases.projectiles");
        RequireInitialized(
            aliases.Events,
            "actorTurn.aliases.events");

        ReplayV2AliasAudience audience =
            turn.Observation.TeamPerception switch
            {
                TeamPerceptionMode.ImmediateUnion =>
                    ReplayV2AliasAudience.ForTeam(turn.ActorId.TeamId),
                TeamPerceptionMode.Individual =>
                    ReplayV2AliasAudience.ForActor(turn.ActorId),
                _ => throw new ArgumentException(
                    "Replay observation has an unknown alias audience.",
                    nameof(turn)),
            };
        if (!aliasHistories.TryGetValue(
                audience,
                out ReplayV2AliasHistory? history))
        {
            history = new ReplayV2AliasHistory();
            aliasHistories.Add(audience, history);
        }

        HashSet<ReplayV2ActorId> allowedEnemyActors =
            observableActorIds
                .Where(actor => actor.TeamId != turn.ActorId.TeamId)
                .ToHashSet();
        Dictionary<string, ReplayV2ActorId> enemyMappings =
            ValidateAliasMappings(
                aliases.EnemyLives,
                "enemy-life",
                ReplayV2AliasHandles.EnemyLifePrefix,
                value => value.LifeHandle,
                value => value.ActorId,
                allowedEnemyActors,
                history.EnemyLives);
        Dictionary<string, string> projectileMappings =
            ValidateAliasMappings(
                aliases.Projectiles,
                "projectile",
                ReplayV2AliasHandles.ProjectilePrefix,
                value => value.ProjectileHandle,
                value => value.ProjectileId,
                observableProjectileIds,
                history.Projectiles);
        Dictionary<string, string> eventMappings =
            ValidateAliasMappings(
                aliases.Events,
                "event",
                ReplayV2AliasHandles.EventPrefix,
                value => value.EventHandle,
                value => value.EventId,
                observableEvents.Keys.ToHashSet(StringComparer.Ordinal),
                history.Events);

        var referencedEnemyHandles =
            new HashSet<string>(StringComparer.Ordinal);
        var visibleEnemyHandles =
            new HashSet<string>(StringComparer.Ordinal);
        var referencedProjectileHandles =
            new HashSet<string>(StringComparer.Ordinal);
        var referencedEventHandles =
            new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, ReplayV2ProjectileState>
            authoritativeProjectiles = authoritativeState.Projectiles
                .ToDictionary(
                    value => value.ProjectileId,
                    StringComparer.Ordinal);
        Dictionary<ReplayV2ActorId, (ReplayV2UnitState Unit,
            ReplayV2LifeState Life)> activeEnemies =
            authoritativeState.Teams
                .Where(team => team.TeamId != turn.ActorId.TeamId)
                .SelectMany(team => team.Units)
                .Where(unit => unit.ActiveLife is not null)
                .ToDictionary(
                    unit => unit.ActiveLife!.ActorId,
                    unit => (unit, unit.ActiveLife!));

        ReplayV2ActorId ResolveEnemy(
            ReplayV2ObservedEnemyActorRef reference)
        {
            ReplayV2AliasHandles.ParseOrdinal(
                reference.LifeHandle,
                ReplayV2AliasHandles.EnemyLifePrefix);
            if (!enemyMappings.TryGetValue(
                    reference.LifeHandle,
                    out ReplayV2ActorId actorId)
                || actorId.TeamId != reference.TeamId
                || actorId.UnitId != reference.UnitId
                || actorId.TeamId == turn.ActorId.TeamId)
            {
                throw new ArgumentException(
                    "Observed enemy-life handle does not resolve exactly through its replay alias mapping.",
                    nameof(turn));
            }
            referencedEnemyHandles.Add(reference.LifeHandle);
            return actorId;
        }

        string ResolveProjectile(string handle)
        {
            ReplayV2AliasHandles.ParseOrdinal(
                handle,
                ReplayV2AliasHandles.ProjectilePrefix);
            if (!projectileMappings.TryGetValue(
                    handle,
                    out string? projectileId))
            {
                throw new ArgumentException(
                    "Observed projectile handle does not resolve through its replay alias mapping.",
                    nameof(turn));
            }
            referencedProjectileHandles.Add(handle);
            return projectileId;
        }

        ReplayV2Event ResolveEvent(
            string handle,
            int sourceTick,
            ObservedMatchEventType type)
        {
            ReplayV2AliasHandles.ParseOrdinal(
                handle,
                ReplayV2AliasHandles.EventPrefix);
            if (!eventMappings.TryGetValue(handle, out string? eventId)
                || !observableEvents.TryGetValue(
                    eventId,
                    out ReplayV2Event? authoritative)
                || authoritative.Tick != sourceTick
                || ToObservedType(authoritative.Type) != type)
            {
                throw new ArgumentException(
                    "Observed event handle does not resolve exactly through its replay alias mapping.",
                    nameof(turn));
            }
            referencedEventHandles.Add(handle);
            return authoritative;
        }

        foreach (ReplayV2ObservedEnemy enemy in
                 turn.Observation.Enemies)
        {
            ReplayV2ActorId actorId = ResolveEnemy(enemy.Actor);
            if (!activeEnemies.TryGetValue(
                    actorId,
                    out (ReplayV2UnitState Unit,
                        ReplayV2LifeState Life) authoritative)
                || !string.Equals(
                    enemy.FormId,
                    authoritative.Unit.FormId,
                    StringComparison.Ordinal)
                || enemy.Position != authoritative.Life.Position
                || enemy.Facing != authoritative.Life.Facing
                || enemy.Health != authoritative.Life.Health)
            {
                throw new ArgumentException(
                    "Observed enemy must equal a currently active authoritative enemy life.",
                    nameof(turn));
            }
            visibleEnemyHandles.Add(enemy.Actor.LifeHandle);
        }
        if (visibleEnemyHandles.Count != turn.Observation.Enemies.Length)
        {
            throw new ArgumentException(
                "Observed enemy handles must be unique.",
                nameof(turn));
        }

        if (turn.Observation.VisibleProjectiles is { } projectiles)
        {
            foreach (ReplayV2ObservedProjectile projectile in projectiles)
            {
                string projectileId =
                    ResolveProjectile(projectile.ProjectileHandle);
                if (!topologyTeamIds.Contains(projectile.OwnerTeamId)
                    || !authoritativeProjectiles.TryGetValue(
                        projectileId,
                        out ReplayV2ProjectileState? authoritative)
                    || authoritative.OwnerActorId.TeamId
                        != projectile.OwnerTeamId
                    || authoritative.Position != projectile.Position
                    || (authoritative.Heading
                            ?? authoritative.LaunchDirection
                                .ToProjectileHeading())
                        != projectile.Heading)
                {
                    throw new ArgumentException(
                        "Observed projectile alias does not match the authoritative tick-start projectile.",
                        nameof(turn));
                }

                bool allied =
                    projectile.OwnerTeamId == turn.ActorId.TeamId;
                if (allied)
                {
                    if (projectile.AlliedOwnerActorId
                            != authoritative.OwnerActorId
                        || projectile.VisibleEnemyOwner is not null)
                    {
                        throw new ArgumentException(
                            "An allied projectile owner must use only its exact allied-owner field.",
                            nameof(turn));
                    }
                }
                else
                {
                    if (projectile.AlliedOwnerActorId is not null)
                    {
                        throw new ArgumentException(
                            "An enemy projectile cannot expose an exact allied-owner field.",
                            nameof(turn));
                    }
                    if (projectile.VisibleEnemyOwner is { } enemyOwner)
                    {
                        ReplayV2ActorId enemyActor =
                            ResolveEnemy(enemyOwner);
                        if (enemyActor != authoritative.OwnerActorId
                            || !visibleEnemyHandles.Contains(
                                enemyOwner.LifeHandle))
                        {
                            throw new ArgumentException(
                                "An enemy projectile owner may be named only through a currently visible enemy reference.",
                                nameof(turn));
                        }
                    }
                }
            }
        }

        foreach (ReplayV2ObservedEvent value in
                 turn.Observation.VisibleEvents)
        {
            ReplayV2Event authoritative = ResolveEvent(
                value.EventHandle,
                value.SourceTick,
                value.Type);
            ReplayV2ActorId? expectedActor =
                ObservedEventActor(authoritative);
            if (value.AlliedActorId is not null
                && value.EnemyActor is not null)
            {
                throw new ArgumentException(
                    "An observed event cannot carry both allied and enemy actor references.",
                    nameof(turn));
            }
            if (expectedActor is { } expected)
            {
                if (expected.TeamId == turn.ActorId.TeamId)
                {
                    if (value.AlliedActorId != expected
                        || value.EnemyActor is not null)
                    {
                        throw new ArgumentException(
                            "Observed allied event actor does not match its authoritative event.",
                            nameof(turn));
                    }
                }
                else if (value.EnemyActor is not { } enemy
                         || ResolveEnemy(enemy) != expected
                         || value.AlliedActorId is not null)
                {
                    throw new ArgumentException(
                        "Observed enemy event actor does not match its authoritative alias.",
                        nameof(turn));
                }
            }
            else if (value.AlliedActorId is not null
                     || value.EnemyActor is not null)
            {
                throw new ArgumentException(
                    "An actorless authoritative event cannot gain an observed actor.",
                    nameof(turn));
            }

            if (value.ProjectileHandle is { } projectileHandle)
            {
                string projectileId =
                    ResolveProjectile(projectileHandle);
                if (!string.Equals(
                        authoritative.ProjectileId,
                        projectileId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Observed event projectile handle does not match its authoritative event.",
                        nameof(turn));
                }
            }
            else if (authoritative.ProjectileId is not null)
            {
                throw new ArgumentException(
                    "An observed projectile event must retain its opaque projectile handle.",
                    nameof(turn));
            }
        }

        if (turn.Observation.HeardSounds is { } sounds)
        {
            foreach (ReplayV2ObservedSound sound in sounds)
            {
                ResolveEvent(
                    sound.EventHandle,
                    sound.SourceTick,
                    sound.Type);
            }
        }

        EnsureExactAliasReferences(
            enemyMappings.Keys,
            referencedEnemyHandles,
            "enemy-life");
        EnsureExactAliasReferences(
            projectileMappings.Keys,
            referencedProjectileHandles,
            "projectile");
        EnsureExactAliasReferences(
            eventMappings.Keys,
            referencedEventHandles,
            "event");
    }

    private static Dictionary<string, TAuthoritative>
        ValidateAliasMappings<TAlias, TAuthoritative>(
            ImmutableArray<TAlias> mappings,
            string field,
            string prefix,
            Func<TAlias, string> handle,
            Func<TAlias, TAuthoritative> authoritativeId,
            IReadOnlySet<TAuthoritative> allowedAuthoritativeIds,
            ReplayV2AliasNamespace<TAuthoritative> history)
        where TAuthoritative : notnull
    {
        EnsureUnique(
            mappings,
            handle,
            $"{field} alias handles");
        EnsureUnique(
            mappings,
            authoritativeId,
            $"{field} alias authoritative IDs");

        var current = new Dictionary<string, TAuthoritative>(
            StringComparer.Ordinal);
        foreach (TAlias mapping in mappings.OrderBy(value =>
                     ReplayV2AliasHandles.ParseOrdinal(
                         handle(value),
                         prefix)))
        {
            string localHandle = handle(mapping);
            TAuthoritative authoritative = authoritativeId(mapping);
            ReplayV2AliasHandles.ParseOrdinal(localHandle, prefix);
            if (!allowedAuthoritativeIds.Contains(authoritative))
            {
                throw new ArgumentException(
                    $"Replay {field} alias does not resolve to an observable authoritative ID.");
            }
            history.Accept(localHandle, authoritative, prefix);
            current.Add(localHandle, authoritative);
        }
        return current;
    }

    private static void EnsureExactAliasReferences(
        IEnumerable<string> mapped,
        IReadOnlySet<string> referenced,
        string field)
    {
        if (!mapped.ToHashSet(StringComparer.Ordinal).SetEquals(referenced))
        {
            throw new ArgumentException(
                $"Replay {field} alias mappings must exactly match the handles referenced by the observation.");
        }
    }

    private static bool WorldStatesEqual(
        ReplayV2WorldState left,
        ReplayV2WorldState right) =>
        Write(writer => WriteWorldState(writer, left))
            .AsSpan()
            .SequenceEqual(Write(writer => WriteWorldState(writer, right)));

    private static ReplayV2ActorId? ObservedEventActor(
        ReplayV2Event value) =>
        value.Type is
            FrontlineMatchEventType.Damage or
            FrontlineMatchEventType.Destroyed
            ? value.TargetActorId
            : value.SourceActorId;

    private static ObservedMatchEventType ToObservedType(
        FrontlineMatchEventType type) =>
        type switch
        {
            FrontlineMatchEventType.Respawned =>
                ObservedMatchEventType.Respawned,
            FrontlineMatchEventType.Turn =>
                ObservedMatchEventType.Turn,
            FrontlineMatchEventType.Move =>
                ObservedMatchEventType.Move,
            FrontlineMatchEventType.MoveBlocked =>
                ObservedMatchEventType.MoveBlocked,
            FrontlineMatchEventType.Shot =>
                ObservedMatchEventType.Shot,
            FrontlineMatchEventType.Damage =>
                ObservedMatchEventType.Damage,
            FrontlineMatchEventType.Destroyed =>
                ObservedMatchEventType.Destroyed,
            FrontlineMatchEventType.FrontlineProgressChanged =>
                ObservedMatchEventType.FrontlineProgressChanged,
            FrontlineMatchEventType.FrontlinePositionAdvanced =>
                ObservedMatchEventType.FrontlinePositionAdvanced,
            FrontlineMatchEventType.BaseBreached =>
                ObservedMatchEventType.BaseBreached,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static void ValidateActionResolution(
        PublicMatchContractManifest contract,
        ReplayV2ActionResolution resolution,
        int tick)
    {
        PublicActionDefinition chosen = ResolveAction(
            contract,
            resolution.ChosenActionId,
            resolution.ChosenActionCode,
            $"tick {tick} chosen resolution");
        PublicActionDefinition validated = ResolveAction(
            contract,
            resolution.ValidatedActionId,
            resolution.ValidatedActionCode,
            $"tick {tick} validated resolution");
        ValidateActionPayload(
            resolution.ChosenPayload,
            chosen,
            $"tick {tick} chosen resolution",
            contract);
        ValidateActionPayload(
            resolution.ValidatedPayload,
            validated,
            $"tick {tick} validated resolution",
            contract);
    }

    private static void ValidateActorDecisions(
        PublicMatchContractManifest contract,
        ReplayV2ActorTurn turn,
        int tick)
    {
        ArgumentNullException.ThrowIfNull(turn.RuntimeReply);
        ArgumentNullException.ThrowIfNull(turn.AcceptedDecision);
        RejectEmptyPayload(
            turn.RuntimeReply.Payload,
            $"tick {tick} runtime reply");

        ReplayV2ActorDecision accepted = turn.AcceptedDecision;
        if (accepted.ActionId is null
            || accepted.ActionCode is null
            || accepted.Faulted
            || accepted.FaultMessage is not null)
        {
            throw new ArgumentException(
                $"Tick {tick} accepted decision must be canonical and non-faulted.",
                nameof(turn));
        }
        PublicActionDefinition action = ResolveAction(
            contract,
            accepted.ActionId,
            accepted.ActionCode.Value,
            $"tick {tick} accepted decision");
        ValidateActionPayload(
            accepted.Payload,
            action,
            $"tick {tick} accepted decision",
            contract);
        if (!string.Equals(
                accepted.ActionId,
                turn.ActionResolution.ChosenActionId,
                StringComparison.Ordinal)
            || accepted.ActionCode
                != turn.ActionResolution.ChosenActionCode
            || accepted.Payload != turn.ActionResolution.ChosenPayload)
        {
            throw new ArgumentException(
                $"Tick {tick} accepted decision must exactly match the chosen resolution selector and payload.",
                nameof(turn));
        }
    }

    private static void ValidateActionPayload(
        ReplayV2ActionPayload? payload,
        PublicActionDefinition action,
        string context,
        PublicMatchContractManifest contract)
    {
        if (payload is null)
            return;
        RejectEmptyPayload(payload, context);
        if ((payload.ShotProgram is not null
                && !action.ParameterKinds.Contains(
                    PublicActionParameterKind.ShotProgram))
            || (payload.Direction is not null
                && !action.ParameterKinds.Contains(
                    PublicActionParameterKind.Direction))
            || (payload.UnitTarget is not null
                && !action.ParameterKinds.Contains(
                    PublicActionParameterKind.UnitTarget))
            || (payload.FormTargetId is not null
                && !action.ParameterKinds.Contains(
                    PublicActionParameterKind.FormTarget)))
        {
            throw new ArgumentException(
                $"{context} payload is inconsistent with action '{action.Id}'.");
        }
        if (payload.Direction is { } direction
            && !Enum.IsDefined(direction))
        {
            throw new ArgumentException(
                $"{context} payload has an unknown direction.");
        }
        if (payload.UnitTarget is { } target
            && !contract.Topology.UnitSlots.Any(unit =>
                unit.TeamId == target.TeamId
                && unit.UnitId == target.UnitId))
        {
            throw new ArgumentException(
                $"{context} payload unit target must exist in match topology.");
        }
        if (payload.FormTargetId is not null
            && !contract.Rules.Forms.Any(form => string.Equals(
                form.Id,
                payload.FormTargetId,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"{context} payload form target must exist in the contract.");
        }
        if (payload.ShotProgram is { } program
            && !IsValidShotProgram(
                program,
                contract.Rules.ShotPrograms))
        {
            throw new ArgumentException(
                $"{context} payload shot program is outside the contract.");
        }
    }

    private static void RejectEmptyPayload(
        ReplayV2ActionPayload? payload,
        string context)
    {
        if (payload is not null
            && payload.ShotProgram is null
            && payload.Direction is null
            && payload.UnitTarget is null
            && payload.FormTargetId is null)
        {
            throw new ArgumentException(
                $"{context} empty action payload must canonicalize to null.");
        }
    }

    private static bool IsValidShotProgram(
        ShotProgram program,
        PublicShotProgramRules rules)
    {
        if (program.InitialAimOffset < rules.MinInitialAimOctants
            || program.InitialAimOffset > rules.MaxInitialAimOctants)
        {
            return false;
        }
        if (program.BendCount == 0)
        {
            return program.BendDirection
                    == rules.AimOnlyProgram.BendDirection
                && program.BendAfterTiles
                    == rules.AimOnlyProgram.BendAfterTiles
                && program.BendEveryTiles
                    == rules.AimOnlyProgram.BendEveryTiles;
        }
        return rules.AllowedCurvedBendDirections.Contains(
                program.BendDirection)
            && program.BendAfterTiles >= rules.MinBendAfterTiles
            && program.BendAfterTiles <= rules.MaxBendAfterTiles
            && program.BendEveryTiles >= rules.MinBendEveryTiles
            && program.BendEveryTiles <= rules.MaxBendEveryTiles
            && program.BendCount >= rules.MinBendCount
            && program.BendCount <= rules.MaxBendCount;
    }

    private static PublicActionDefinition ResolveAction(
        PublicMatchContractManifest contract,
        string actionId,
        int actionCode,
        string context)
    {
        PublicActionDefinition? byId = contract.Rules.Actions
            .FirstOrDefault(action => string.Equals(
                action.Id,
                actionId,
                StringComparison.Ordinal));
        PublicActionDefinition? byCode = contract.Rules.Actions
            .FirstOrDefault(action => action.Code == actionCode);
        if (byId is null
            || byCode is null
            || !ReferenceEquals(byId, byCode))
        {
            throw new ArgumentException(
                $"{context} action ID/code must identify the same contract action.");
        }
        return byId;
    }

    private static void ValidateWorldState(
        ReplayV2WorldState state,
        int tick,
        string phase,
        IReadOnlyList<int> expectedTeamIds)
    {
        EnsureUnique(
            state.Teams,
            value => value.TeamId,
            $"tick {tick} {phase} team IDs");
        if (!state.Teams
                .Select(team => team.TeamId)
                .Order()
                .SequenceEqual(expectedTeamIds))
        {
            throw new ArgumentException(
                $"Tick {tick} {phase} team IDs must match contract topology.");
        }
        if ((state.Control.ClaimingTeamId is int claimingTeamId
                && !expectedTeamIds.Contains(claimingTeamId))
            || (state.Control.WinnerTeamId is int winnerTeamId
                && !expectedTeamIds.Contains(winnerTeamId)))
        {
            throw new ArgumentException(
                $"Tick {tick} {phase} objective team references must match contract topology.");
        }
        EnsureUnique(
            state.Projectiles,
            value => value.ProjectileId,
            $"tick {tick} {phase} projectile IDs");
        foreach (ReplayV2TeamState team in state.Teams)
        {
            RequireWireInt64(
                team.DamageDealt,
                $"tick {tick} {phase} team {team.TeamId} damageDealt",
                nonNegative: true);
            RequireInitialized(
                team.Units,
                $"tick {tick} {phase} team {team.TeamId} units");
            EnsureUnique(
                team.Units,
                value => value.UnitId,
                $"tick {tick} {phase} team {team.TeamId} unit IDs");
            if (team.Units.Any(unit => unit.TeamId != team.TeamId))
            {
                throw new ArgumentException(
                    $"Tick {tick} {phase} unit team identity is inconsistent.");
            }
            foreach (ReplayV2UnitState unit in team.Units)
            {
                RequireWireInt64(
                    unit.DamageDealt,
                    $"tick {tick} {phase} unit {unit.TeamId}:{unit.UnitId} damageDealt",
                    nonNegative: true);
                if (unit.ActiveLife is { } life)
                {
                    RequireWireInt64(
                        life.DamageDealt,
                        $"tick {tick} {phase} life {life.ActorId} damageDealt",
                        nonNegative: true);
                }
            }
        }
        foreach (ReplayV2ProjectileState projectile in
                 state.Projectiles)
        {
            RequireWireId(
                projectile.ProjectileId,
                $"tick {tick} {phase} projectile ID");
            if (projectile.ProgrammedPath is { } programmedPath)
            {
                RequireInitialized(
                    programmedPath,
                    $"tick {tick} {phase} projectile programmed path");
            }
        }
    }

    private static void ValidateEvent(
        ReplayV2Event value,
        int expectedTick,
        ISet<string> eventIds,
        PublicMatchContractManifest contract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EventId);
        if (!eventIds.Add(value.EventId))
        {
            throw new ArgumentException(
                $"Replay event ID '{value.EventId}' is duplicated.");
        }
        if (value.Tick != expectedTick)
        {
            throw new ArgumentException(
                $"Replay event '{value.EventId}' belongs to the wrong tick.");
        }
        if (value.ProjectileId is { } projectileId)
            RequireWireId(projectileId, $"event {value.EventId} projectile ID");
        if ((value.ActionId is null) != (value.ActionCode is null))
        {
            throw new ArgumentException(
                $"Replay event '{value.EventId}' action selector is incomplete.");
        }
        if (value.ActionId is { } actionId
            && value.ActionCode is int actionCode)
        {
            PublicActionDefinition action = ResolveAction(
                contract,
                actionId,
                actionCode,
                $"event {value.EventId}");
            ValidateActionPayload(
                value.ActionPayload,
                action,
                $"event {value.EventId}",
                contract);
        }
        else if (value.ActionPayload is not null)
        {
            throw new ArgumentException(
                $"Replay event '{value.EventId}' cannot carry a payload without an action.");
        }
    }

    private static void RequireInitialized<T>(
        ImmutableArray<T> values,
        string field)
    {
        if (values.IsDefault)
        {
            throw new ArgumentException(
                $"Replay-v2 collection '{field}' must be initialized.");
        }
    }

    private static void EnsureUnique<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> key,
        string field)
        where TKey : notnull
    {
        var keys = new HashSet<TKey>();
        if (values.Any(value => !keys.Add(key(value))))
            throw new ArgumentException($"Replay-v2 {field} must be unique.");
    }

    private static void RequireWireId(string value, string field)
    {
        RequireWireInt64(value, field, nonNegative: true);
    }

    private static void RequireWireInt64(
        string value,
        string field,
        bool nonNegative)
    {
        if (!IsCanonicalInt64(value, nonNegative))
        {
            throw new ArgumentException(
                $"{field} must be a canonical " +
                $"{(nonNegative ? "non-negative " : string.Empty)}" +
                "Int64 decimal string.");
        }
    }

    private static bool IsCanonicalInt64(
        string? value,
        bool nonNegative) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long parsed)
        && (!nonNegative || parsed >= 0)
        && string.Equals(
            parsed.ToString(CultureInfo.InvariantCulture),
            value,
            StringComparison.Ordinal);

    private static long ParseWireId(string value) =>
        long.Parse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture);

    private static bool HasExactTopLevelProperties(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        string[] expected =
            ["header", "ticks", "result", "replayHash", "partial"];
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!expected.Contains(property.Name, StringComparer.Ordinal)
                || !found.Add(property.Name))
            {
                return false;
            }
        }
        return expected.All(found.Contains);
    }

    private static bool HasCanonicalSeed(JsonElement header)
    {
        if (header.ValueKind != JsonValueKind.Object
            || !header.TryGetProperty("seed", out JsonElement seed)
            || seed.ValueKind != JsonValueKind.String
            || seed.GetString() is not { } value)
        {
            return false;
        }
        return IsCanonicalSeed(value);
    }

    private static bool IsCanonicalSeed(string value) =>
        ulong.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out ulong parsed)
        && string.Equals(
            parsed.ToString(CultureInfo.InvariantCulture),
            value,
            StringComparison.Ordinal);

    private static bool HasCanonicalWireStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name == "eventId"
                    && (property.Value.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(
                            property.Value.GetString())))
                {
                    return false;
                }
                if (property.Name == "projectileId")
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        if (!IsCanonicalInt64(
                                property.Value.GetString(),
                                nonNegative: true))
                        {
                            return false;
                        }
                    }
                    else if (property.Value.ValueKind != JsonValueKind.Null)
                    {
                        return false;
                    }
                }
                if (property.Name == "damageDealt"
                    && (property.Value.ValueKind != JsonValueKind.String
                        || !IsCanonicalInt64(
                            property.Value.GetString(),
                            nonNegative: true)))
                {
                    return false;
                }
                if (property.Name == "actorRandomSeed"
                    && (property.Value.ValueKind != JsonValueKind.String
                        || property.Value.GetString() is not { } actorSeed
                        || !IsCanonicalSeed(actorSeed)))
                {
                    return false;
                }
                if (property.Name == "territorialScore"
                    && (property.Value.ValueKind != JsonValueKind.String
                        || !IsCanonicalInt64(
                            property.Value.GetString(),
                            nonNegative: false)))
                {
                    return false;
                }
                if (!HasCanonicalWireStrings(property.Value))
                    return false;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!HasCanonicalWireStrings(item))
                    return false;
            }
        }
        return true;
    }

    private static bool IsLowercaseSha256(string value) =>
        value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static string TeamPerceptionId(TeamPerceptionMode value) =>
        value switch
        {
            TeamPerceptionMode.Individual => "individual",
            TeamPerceptionMode.ImmediateUnion => "immediate-union",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string DirectionId(Direction value) => value switch
    {
        Direction.North => "north",
        Direction.East => "east",
        Direction.South => "south",
        Direction.West => "west",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ProjectileHeadingId(ProjectileHeading value) =>
        value switch
        {
            ProjectileHeading.North => "north",
            ProjectileHeading.NorthEast => "north-east",
            ProjectileHeading.East => "east",
            ProjectileHeading.SouthEast => "south-east",
            ProjectileHeading.South => "south",
            ProjectileHeading.SouthWest => "south-west",
            ProjectileHeading.West => "west",
            ProjectileHeading.NorthWest => "north-west",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string ActionResultId(ActionResult value) => value switch
    {
        ActionResult.None => "none",
        ActionResult.Success => "success",
        ActionResult.Blocked => "blocked",
        ActionResult.OnCooldown => "on-cooldown",
        ActionResult.Faulted => "faulted",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string LifecycleId(FrontlineLifecycleStatus value) =>
        value switch
        {
            FrontlineLifecycleStatus.Active => "active",
            FrontlineLifecycleStatus.Respawning => "respawning",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string SpawnReasonId(ActorSpawnReason value) =>
        value switch
        {
            ActorSpawnReason.Initial => "initial",
            ActorSpawnReason.Respawn => "respawn",
            ActorSpawnReason.Rebuild => "rebuild",
            ActorSpawnReason.Fabrication => "fabrication",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string ActionParameterKindId(
        PublicActionParameterKind value) => value switch
        {
            PublicActionParameterKind.ShotProgram => "shot-program",
            PublicActionParameterKind.Direction => "direction",
            PublicActionParameterKind.UnitTarget => "unit-target",
            PublicActionParameterKind.FormTarget => "form-target",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string ObservedEventTypeId(
        ObservedMatchEventType value) => value switch
        {
            ObservedMatchEventType.Turn => "turn",
            ObservedMatchEventType.Move => "move",
            ObservedMatchEventType.MoveBlocked => "move-blocked",
            ObservedMatchEventType.Shot => "shot",
            ObservedMatchEventType.Damage => "damage",
            ObservedMatchEventType.Destroyed => "destroyed",
            ObservedMatchEventType.Fault => "fault",
            ObservedMatchEventType.Disqualified => "disqualified",
            ObservedMatchEventType.Respawned => "respawned",
            ObservedMatchEventType.FrontlineProgressChanged =>
                "frontline-progress-changed",
            ObservedMatchEventType.FrontlinePositionAdvanced =>
                "frontline-position-advanced",
            ObservedMatchEventType.BaseBreached => "base-breached",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string FrontlineEventTypeId(
        FrontlineMatchEventType value) => value switch
        {
            FrontlineMatchEventType.Respawned => "respawned",
            FrontlineMatchEventType.Turn => "turn",
            FrontlineMatchEventType.Move => "move",
            FrontlineMatchEventType.MoveBlocked => "move-blocked",
            FrontlineMatchEventType.Shot => "shot",
            FrontlineMatchEventType.Damage => "damage",
            FrontlineMatchEventType.Destroyed => "destroyed",
            FrontlineMatchEventType.FrontlineProgressChanged =>
                "frontline-progress-changed",
            FrontlineMatchEventType.FrontlinePositionAdvanced =>
                "frontline-position-advanced",
            FrontlineMatchEventType.BaseBreached => "base-breached",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string MatchEndReasonId(
        FrontlineMatchEndReason value) => value switch
        {
            FrontlineMatchEndReason.BaseBreach => "base-breach",
            FrontlineMatchEndReason.MaxTicks => "max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string TeamOutcomeId(FrontlineTeamOutcome value) =>
        value switch
        {
            FrontlineTeamOutcome.Win => "win",
            FrontlineTeamOutcome.Loss => "loss",
            FrontlineTeamOutcome.Draw => "draw",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private readonly record struct ReplayV2AliasAudience(
        TeamPerceptionMode Mode,
        int TeamId,
        ReplayV2ActorId? ActorId)
    {
        public static ReplayV2AliasAudience ForTeam(int teamId) =>
            new(TeamPerceptionMode.ImmediateUnion, teamId, ActorId: null);

        public static ReplayV2AliasAudience ForActor(
            ReplayV2ActorId actorId) =>
            new(
                TeamPerceptionMode.Individual,
                actorId.TeamId,
                actorId);
    }

    private sealed class ReplayV2AliasHistory
    {
        public ReplayV2AliasNamespace<ReplayV2ActorId> EnemyLives
        {
            get;
        } = new();

        public ReplayV2AliasNamespace<string> Projectiles { get; } =
            new();

        public ReplayV2AliasNamespace<string> Events { get; } =
            new();
    }

    private sealed class ReplayV2AliasNamespace<T>
        where T : notnull
    {
        private readonly Dictionary<string, T> _byHandle =
            new(StringComparer.Ordinal);
        private readonly Dictionary<T, string> _byAuthoritativeId = [];
        private int _nextOrdinal;

        public void Accept(
            string handle,
            T authoritativeId,
            string prefix)
        {
            int ordinal =
                ReplayV2AliasHandles.ParseOrdinal(handle, prefix);
            if (_byHandle.TryGetValue(handle, out T? priorByHandle))
            {
                if (!EqualityComparer<T>.Default.Equals(
                        priorByHandle,
                        authoritativeId))
                {
                    throw new ArgumentException(
                        $"Replay alias handle '{handle}' was reused for another authoritative ID.");
                }
                return;
            }
            if (_byAuthoritativeId.TryGetValue(
                    authoritativeId,
                    out string? priorHandle))
            {
                throw new ArgumentException(
                    $"Replay authoritative ID was reassigned from alias '{priorHandle}' to '{handle}'.");
            }
            if (ordinal != _nextOrdinal)
            {
                throw new ArgumentException(
                    $"Replay aliases in the '{prefix}' audience must be allocated densely in first-discovery order.");
            }

            _byHandle.Add(handle, authoritativeId);
            _byAuthoritativeId.Add(authoritativeId, handle);
            _nextOrdinal = checked(_nextOrdinal + 1);
        }
    }
}
