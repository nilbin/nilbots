using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BotArena.ActorContracts;

namespace BotArena.Engine;

/// <summary>
/// Explicit canonical codec for generation-3 generic actor replays. Historical
/// replay serializers remain byte-for-byte isolated from this implementation.
/// </summary>
internal static class ReplayV3Serializer
{
    private static readonly string[] EnvelopeProperties =
    [
        "header",
        "initialFrame",
        "ticks",
        "result",
        "replayHash",
        "partial",
    ];

    /// <summary>
    /// The one redeploy policy that carries a territory-ratchet hold, and
    /// therefore the only one whose observations may publish hold clocks.
    /// </summary>
    private const string RatchetRedeployPolicy =
        "advance-immediately-then-deny-enemy-regression-past-the-high-water-mark-through-configured-hold-ticks";

    private static readonly JsonSerializerOptions ReadOptions =
        CreateReadOptions();

    private static readonly JsonDocumentOptions DocumentOptions =
        new()
        {
            AllowDuplicateProperties = false,
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        };

    private static readonly string[] ContractRootProperties =
    [
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
        "modeMapBinding",
    ];

    private static readonly string[] CapabilityProperties =
    [
        "contractProfileId",
        "runtimeProtocolVersion",
        "runtimeConfigurationVersion",
        "runtimeContractVersion",
        "matchStartSchemaVersion",
        "observationSchemaVersion",
        "decisionSchemaVersion",
        "matchContractSchemaVersion",
    ];

    public static string ToCanonicalJson(ReplayV3 replay) =>
        Encoding.UTF8.GetString(ToCanonicalUtf8(replay));

    public static string ComputeHash(ReplayV3 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ValidateEnvelope(replay);
        if (replay.Partial)
        {
            throw new ArgumentException(
                "Partial replay-v3 documents are intentionally unhashed.",
                nameof(replay));
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(ToCanonicalUtf8(replay)));
    }

    /// <summary>
    /// Creates the completed canonical envelope and its payload hash from one
    /// payload serialization. The payload is the first four envelope fields;
    /// the hash and partial marker are an exact ASCII suffix, so writing the
    /// entire 50–100 MiB graph a second time is unnecessary.
    /// </summary>
    internal static (byte[] CanonicalUtf8, string ReplayHash)
        CreateCanonicalDocument(ReplayV3 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        bool perfDiagnostics = string.Equals(
            Environment.GetEnvironmentVariable("BOTARENA_PERF_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal);
        long validationStart = perfDiagnostics ? Stopwatch.GetTimestamp() : 0;
        long validationAllocationStart = perfDiagnostics
            ? GC.GetTotalAllocatedBytes(precise: false)
            : 0;
        ValidateEnvelope(replay);
        if (perfDiagnostics)
        {
            Console.Error.WriteLine(
                $"PERF replay.validate: wall={Stopwatch.GetElapsedTime(validationStart).TotalMilliseconds:F1}ms "
                + $"allocated={(GC.GetTotalAllocatedBytes(precise: false) - validationAllocationStart) / 1_048_576.0:F1}MiB");
        }
        if (replay.Partial)
        {
            throw new ArgumentException(
                "Partial replay-v3 documents are intentionally unhashed.",
                nameof(replay));
        }

        int estimatedBytes = Math.Max(
            1024,
            checked(replay.Ticks.Length * 192_000));
        using var stream = new MemoryStream(estimatedBytes);
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Indented = false,
                       SkipValidation = false,
                   }))
        {
            writer.WriteStartObject();
            WritePayloadProperties(writer, replay);
            writer.WriteEndObject();
        }

        int payloadLength = checked((int)stream.Length);
        ReadOnlySpan<byte> payload = stream.GetBuffer()
            .AsSpan(0, payloadLength);
        string replayHash = Convert.ToHexStringLower(
            SHA256.HashData(payload));
        byte[] suffix = Encoding.UTF8.GetBytes(
            $",\"replayHash\":\"{replayHash}\",\"partial\":false}}");
        byte[] envelope = GC.AllocateUninitializedArray<byte>(
            checked(payloadLength - 1 + suffix.Length));
        payload[..^1].CopyTo(envelope);
        suffix.CopyTo(envelope, payloadLength - 1);
        return (envelope, replayHash);
    }

    public static string ToJson(ReplayV3 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ValidateEnvelope(replay);

        string? hash = replay.Partial ? null : ComputeHash(replay);
        if (replay.ReplayHash is not null
            && !string.Equals(
                replay.ReplayHash,
                hash,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supplied replay-v3 hash does not match its canonical payload.",
                nameof(replay));
        }

        return WriteEnvelope(replay, hash);
    }

    private static string WriteEnvelope(ReplayV3 replay, string? hash) =>
        Encoding.UTF8.GetString(Write(writer =>
        {
            writer.WriteStartObject();
            WritePayloadProperties(writer, replay);
            WriteNullableString(writer, "replayHash", hash);
            writer.WriteBoolean("partial", replay.Partial);
            writer.WriteEndObject();
        }));

    public static bool VerifyHash(string json) =>
        VerifyHash(json, out _);

    public static ReplayV3 ReadCanonicalComplete(string json)
    {
        if (!VerifyHash(json, out string? failure))
        {
            throw new InvalidDataException(
                $"Replay-v3 document is not a verified canonical complete replay: {failure}");
        }

        return JsonSerializer.Deserialize<ReplayV3>(json, ReadOptions)
            ?? throw new InvalidDataException(
                "Replay-v3 document deserialized to null.");
    }

    public static bool VerifyHash(
        string json,
        out string? failure)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            using JsonDocument document = JsonDocument.Parse(
                json,
                DocumentOptions);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.EnumerateObject()
                    .Select(property => property.Name)
                    .SequenceEqual(EnvelopeProperties))
            {
                failure =
                    "Replay-v3 document must contain exactly header, initialFrame, ticks, result, replayHash, and partial in canonical order.";
                return false;
            }

            JsonElement header = root.GetProperty("header");
            if (header.ValueKind != JsonValueKind.Object
                || !header.TryGetProperty(
                    "replayVersion",
                    out JsonElement replayVersion)
                || replayVersion.ValueKind != JsonValueKind.Number
                || !replayVersion.TryGetInt32(out int version)
                || version
                    != BotArenaVersions.GenericActorReplayFormatVersion)
            {
                failure = "Document is not replay v3.";
                return false;
            }

            JsonElement partial = root.GetProperty("partial");
            if (partial.ValueKind != JsonValueKind.False)
            {
                failure =
                    "Partial replay-v3 documents are intentionally unhashed.";
                return false;
            }
            if (root.GetProperty("result").ValueKind
                == JsonValueKind.Null)
            {
                failure =
                    "A complete replay-v3 document must contain a result.";
                return false;
            }
            if (!HasCanonicalWireIntegers(root, out failure))
                return false;

            JsonElement replayHashElement =
                root.GetProperty("replayHash");
            if (replayHashElement.ValueKind != JsonValueKind.String
                || replayHashElement.GetString() is not { } replayHash
                || !IsLowercaseSha256(replayHash))
            {
                failure =
                    "Replay-v3 replayHash must be lowercase SHA-256 hex.";
                return false;
            }

            ReplayV3 decoded = JsonSerializer.Deserialize<ReplayV3>(
                    json,
                    ReadOptions)
                ?? throw new JsonException(
                    "Replay-v3 document deserialized to null.");
            string canonicalEnvelope = ToJson(decoded);
            if (!string.Equals(
                    json,
                    canonicalEnvelope,
                    StringComparison.Ordinal))
            {
                failure =
                    "Replay-v3 document is not the exact canonical wire representation.";
                return false;
            }

            byte[] payload = Write(writer =>
            {
                writer.WriteStartObject();
                foreach (string propertyName in
                         EnvelopeProperties.Take(4))
                {
                    writer.WritePropertyName(propertyName);
                    root.GetProperty(propertyName).WriteTo(writer);
                }
                writer.WriteEndObject();
            });
            string actual = Convert.ToHexStringLower(
                SHA256.HashData(payload));
            bool verified = string.Equals(
                replayHash,
                actual,
                StringComparison.Ordinal);
            failure = verified
                ? null
                : "Replay-v3 hash mismatch.";
            return verified;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or FormatException
            or InvalidDataException
            or InvalidOperationException
            or KeyNotFoundException
            or JsonException
            or NullReferenceException
            or NotSupportedException
            or OverflowException)
        {
            failure = exception.Message;
            return false;
        }
    }

    private static byte[] ToCanonicalUtf8(ReplayV3 replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ValidateEnvelope(replay);
        return Write(writer =>
        {
            writer.WriteStartObject();
            WritePayloadProperties(writer, replay);
            writer.WriteEndObject();
        });
    }

    private static void WritePayloadProperties(
        Utf8JsonWriter writer,
        ReplayV3 replay)
    {
        writer.WritePropertyName("header");
        WriteHeader(writer, replay.Header);
        writer.WritePropertyName("initialFrame");
        WriteInitialFrame(writer, replay.InitialFrame);
        bool mindProfile = IsMindProfile(replay.Header);
        WriteArray(
            writer,
            "ticks",
            replay.Ticks,
            (value, tick) => WriteTick(value, tick, mindProfile));
        writer.WritePropertyName("result");
        if (replay.Result is null)
            writer.WriteNullValue();
        else
            WriteResult(writer, replay.Result);
    }

    internal static void WriteHeader(
        Utf8JsonWriter writer,
        ReplayV3.ReplayHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        writer.WriteStartObject();
        writer.WriteNumber("replayVersion", header.ReplayVersion);
        writer.WriteString("engineVersion", header.EngineVersion);
        writer.WriteString(
            "gameRulesVersion",
            header.GameRulesVersion);
        writer.WritePropertyName("runtime");
        WriteRuntime(writer, header.Runtime);
        WriteUInt64String(writer, "seed", header.Seed);
        writer.WritePropertyName("contract");
        WriteContract(writer, header.Contract);
        writer.WritePropertyName("presentation");
        WritePresentation(writer, header.Presentation);
        writer.WritePropertyName("provenance");
        WriteProvenance(writer, header.Provenance);
        writer.WriteEndObject();
    }

    private static void WriteRuntime(
        Utf8JsonWriter writer,
        ReplayV3.RuntimeVersions runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        writer.WriteStartObject();
        writer.WriteString(
            "contractProfileId",
            runtime.ContractProfileId);
        writer.WriteString(
            "protocolVersion",
            runtime.ProtocolVersion);
        writer.WriteString(
            "configurationVersion",
            runtime.ConfigurationVersion);
        writer.WriteNumber(
            "runtimeContractVersion",
            runtime.RuntimeContractVersion);
        writer.WriteNumber(
            "matchStartSchemaVersion",
            runtime.MatchStartSchemaVersion);
        writer.WriteNumber(
            "observationSchemaVersion",
            runtime.ObservationSchemaVersion);
        writer.WriteNumber(
            "decisionSchemaVersion",
            runtime.DecisionSchemaVersion);
        writer.WriteNumber(
            "matchContractSchemaVersion",
            runtime.MatchContractSchemaVersion);
        writer.WriteEndObject();
    }

    private static void WriteContract(
        Utf8JsonWriter writer,
        ReplayV3.ResolvedContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        using JsonDocument document =
            JsonDocument.Parse(contract.CanonicalJson, DocumentOptions);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !string.Equals(
                root.GetRawText(),
                contract.CanonicalJson,
                StringComparison.Ordinal)
            || !root.TryGetProperty(
                "schemaVersion",
                out JsonElement schemaVersion)
            || schemaVersion.GetInt32() != contract.SchemaVersion
            || !root.TryGetProperty(
                "matchContractFingerprint",
                out JsonElement fingerprint)
            || !string.Equals(
                fingerprint.GetString(),
                contract.MatchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay-v3 contract metadata must match its exact canonical JSON.",
                nameof(contract));
        }
        ActorContractProfileAdmission.ValidateCanonicalMatch(
            contract.CanonicalJson);
        root.WriteTo(writer);
    }

    private static void WritePresentation(
        Utf8JsonWriter writer,
        ReplayV3.PresentationMetadata? presentation)
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
            WriteArray(
                writer,
                "wallGroups",
                map.WallGroups,
                static (json, group) =>
                {
                    json.WriteStartObject();
                    json.WriteString("family", group.Family);
                    WriteArray(
                        json,
                        "tiles",
                        group.Tiles,
                        WritePosition);
                    json.WriteEndObject();
                });
            writer.WriteEndObject();
        }
        WriteArray(
            writer,
            "forms",
            presentation.Forms,
            static (json, form) =>
            {
                json.WriteStartObject();
                json.WriteString("formId", form.FormId);
                WriteNullableString(json, "lookId", form.LookId);
                WriteNullableString(
                    json,
                    "projectileLookId",
                    form.ProjectileLookId);
                json.WriteEndObject();
            });
        writer.WriteEndObject();
    }

    private static void WriteProvenance(
        Utf8JsonWriter writer,
        ReplayV3.ProvenanceMetadata? provenance)
    {
        if (provenance is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteArray(
            writer,
            "participants",
            provenance.Participants,
            static (json, participant) =>
            {
                json.WriteStartObject();
                json.WriteNumber(
                    "participantId",
                    participant.ParticipantId);
                json.WriteNumber("teamId", participant.TeamId);
                json.WriteString("name", participant.Name);
                json.WriteString(
                    "runtimeKind",
                    participant.RuntimeKind);
                WriteNullableString(
                    json,
                    "artifactHash",
                    participant.ArtifactHash);
                // Added after replay-v3 shipped. Absence, rather than null,
                // preserves every historical canonical byte and golden hash.
                if (participant.MindDataHash is not null)
                {
                    json.WriteString(
                        "mindDataHash",
                        participant.MindDataHash);
                }
                json.WriteString("accent", participant.Accent);
                WriteNullableString(
                    json,
                    "lookId",
                    participant.LookId);
                WriteNullableString(
                    json,
                    "projectileLookId",
                    participant.ProjectileLookId);
                json.WriteEndObject();
            });
        writer.WriteEndObject();
    }

    private static void WriteInitialFrame(
        Utf8JsonWriter writer,
        ReplayV3.ReplayInitialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        writer.WriteStartObject();
        writer.WritePropertyName("state");
        WriteWorldState(writer, frame.State);
        WriteArray(
            writer,
            "lifeStarts",
            frame.LifeStarts,
            WriteLifeStart);
        WriteArray(writer, "events", frame.Events, WriteEvent);
        writer.WriteEndObject();
    }

    /// <summary>
    /// A tick carries EXACTLY ONE of <c>actorTurns</c> / <c>mindTurns</c>,
    /// decided by the header's contract profile — never by inspecting the
    /// payload (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1). A
    /// document that carried both would round-trip to a different canonical
    /// text and is therefore refused before its hash is ever compared.
    /// </summary>
    private static void WriteTick(
        Utf8JsonWriter writer,
        ReplayV3.TickFrame tick,
        bool mindProfile)
    {
        writer.WriteStartObject();
        writer.WriteNumber("tick", tick.Tick);
        writer.WritePropertyName("tickStart");
        WriteTickStart(writer, tick.TickStart);
        if (mindProfile)
        {
            WriteArray(
                writer,
                "mindTurns",
                tick.MindTurns,
                WriteMindTurn);
        }
        else
        {
            WriteArray(
                writer,
                "actorTurns",
                tick.ActorTurns,
                WriteActorTurn);
        }
        WriteArray(writer, "events", tick.Events, WriteEvent);
        WriteArray(
            writer,
            "traversals",
            tick.Traversals,
            WriteTraversal);
        writer.WritePropertyName("postState");
        WriteWorldState(writer, tick.PostState);
        writer.WriteEndObject();
    }

    private static void WriteTickStart(
        Utf8JsonWriter writer,
        ReplayV3.TickStart tickStart)
    {
        writer.WriteStartObject();
        writer.WriteNumber("tick", tickStart.Tick);
        writer.WritePropertyName("state");
        WriteWorldState(writer, tickStart.State);
        WriteArray(
            writer,
            "activeActorIds",
            tickStart.ActiveActorIds,
            WriteActorId);
        WriteArray(
            writer,
            "lifeStarts",
            tickStart.LifeStarts,
            WriteLifeStart);
        WriteArray(
            writer,
            "events",
            tickStart.Events,
            WriteEvent);
        WriteArray(
            writer,
            "traversals",
            tickStart.Traversals,
            WriteTraversal);
        writer.WriteEndObject();
    }

    private static void WriteActorTurn(
        Utf8JsonWriter writer,
        ReplayV3.ActorTurn turn)
    {
        writer.WriteStartObject();
        writer.WriteNumber("tick", turn.Tick);
        writer.WriteNumber("participantId", turn.ParticipantId);
        writer.WritePropertyName("actorId");
        WriteActorId(writer, turn.ActorId);
        writer.WritePropertyName("observation");
        WriteObservation(writer, turn.Observation);
        writer.WritePropertyName("submittedDecision");
        if (turn.SubmittedDecision is null)
            writer.WriteNullValue();
        else
            WriteSubmittedDecision(writer, turn.SubmittedDecision);
        writer.WritePropertyName("actionResolution");
        WriteActionResolution(writer, turn.ActionResolution);
        writer.WriteEndObject();
    }

    private static bool IsMindProfile(ReplayV3.ReplayHeader header) =>
        header.Runtime is not null
        && string.Equals(
            header.Runtime.ContractProfileId,
            BotArenaVersions.GenericMindContractProfileId,
            StringComparison.Ordinal);

    private static void WriteMindTurn(
        Utf8JsonWriter writer,
        ReplayV3.MindTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        writer.WriteStartObject();
        writer.WriteNumber("tick", turn.Tick);
        writer.WriteNumber("participantId", turn.ParticipantId);
        writer.WriteNumber("teamId", turn.TeamId);
        writer.WriteString("fuelBudget", turn.FuelBudget);
        writer.WriteNumber("liveBodyCount", turn.LiveBodyCount);
        writer.WritePropertyName("observation");
        WriteMindObservation(writer, turn.Observation);
        WriteArray(writer, "commands", turn.Commands, WriteMindCommand);
        WriteArray(
            writer,
            "resolutions",
            turn.Resolutions,
            WriteMindBodyResolution);
        WriteArray(writer, "intents", turn.Intents, WriteMindIntent);
        writer.WritePropertyName("runtimeFault");
        if (turn.RuntimeFault is null)
            writer.WriteNullValue();
        else
            WriteMindRuntimeFault(writer, turn.RuntimeFault);
        // The MIND's diagnostics home, OMITTED when inert (#156) rather than
        // written as an explicit null: a mind that said nothing this tick
        // should cost no bytes, and most ticks say nothing. A mind reasons once
        // per tick over the whole army, so the sentence is not any one body's —
        // and on a tick with no live bodies there is no command to carry it.
        if (turn.DebugMessage is not null)
            writer.WriteString("debugMessage", turn.DebugMessage);
        writer.WriteEndObject();
    }

    private static void WriteMindCommand(
        Utf8JsonWriter writer,
        ReplayV3.MindCommand command)
    {
        writer.WriteStartObject();
        writer.WriteNumber("unitId", command.UnitId);
        writer.WriteNumber("lifeId", command.LifeId);
        writer.WriteString("actionId", command.ActionId);
        writer.WriteNumber("actionCode", command.ActionCode);
        WriteNullableRawActionArguments(writer, command.Arguments);
        writer.WriteString("outcome", command.Outcome);
        // Omit-when-inert (#156): an absent tag means "leave it unchanged" and
        // the empty string means "clear it", so the two must stay distinct on
        // the wire.
        WriteRoleTag(writer, command.RoleTag);
        WriteNullableString(
            writer,
            "debugMessage",
            command.DebugMessage);
        writer.WriteEndObject();
    }

    private static void WriteMindBodyResolution(
        Utf8JsonWriter writer,
        ReplayV3.MindBodyResolution resolution)
    {
        writer.WriteStartObject();
        writer.WriteNumber("unitId", resolution.UnitId);
        writer.WriteNumber("lifeId", resolution.LifeId);
        writer.WritePropertyName("submittedDecision");
        if (resolution.SubmittedDecision is null)
            writer.WriteNullValue();
        else
            WriteSubmittedDecision(writer, resolution.SubmittedDecision);
        writer.WritePropertyName("actionResolution");
        WriteActionResolution(writer, resolution.ActionResolution);
        writer.WriteEndObject();
    }

    private static void WriteMindIntent(
        Utf8JsonWriter writer,
        ReplayV3.MindIntent intent)
    {
        writer.WriteStartObject();
        writer.WriteString("tagId", intent.TagId);
        writer.WriteString("value", intent.Value);
        writer.WriteEndObject();
    }

    private static void WriteMindAlliedIntent(
        Utf8JsonWriter writer,
        ReplayV3.MindAlliedIntent intent)
    {
        writer.WriteStartObject();
        writer.WriteNumber("participantId", intent.ParticipantId);
        writer.WriteString("tagId", intent.TagId);
        writer.WriteString("value", intent.Value);
        writer.WriteEndObject();
    }

    private static void WriteMindRuntimeFault(
        Utf8JsonWriter writer,
        ReplayV3.MindRuntimeFault value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("participantId", value.ParticipantId);
        writer.WriteNumber("teamId", value.TeamId);
        writer.WritePropertyName("actorId");
        WriteNullableActorId(writer, value.ActorId);
        writer.WriteString("stage", value.Stage);
        writer.WriteString("faultCode", value.FaultCode);
        WriteInt64String(
            writer,
            "cumulativeFaultCount",
            value.CumulativeFaultCount,
            nonNegative: true);
        writer.WriteBoolean(
            "disqualificationTriggered",
            value.DisqualificationTriggered);
        writer.WriteEndObject();
    }

    private static void WriteMindObservation(
        Utf8JsonWriter writer,
        ReplayV3.MindObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", observation.SchemaVersion);
        writer.WriteNumber("tick", observation.Tick);
        writer.WriteString(
            "matchContractFingerprint",
            observation.MatchContractFingerprint);
        writer.WriteNumber("participantId", observation.ParticipantId);
        writer.WriteNumber("teamId", observation.TeamId);
        WriteArray(writer, "bodies", observation.Bodies, WriteMindBody);
        WriteArray(writer, "slots", observation.Slots, WriteMindSlot);
        WriteArray(
            writer,
            "teamUnits",
            observation.TeamUnits,
            WriteObservedUnitSlot);
        WriteArray(
            writer,
            "participants",
            observation.Participants,
            WriteParticipantStatus);
        WriteArray(
            writer,
            "allies",
            observation.Allies,
            WriteObservedAlly);
        WriteArray(
            writer,
            "enemies",
            observation.Enemies,
            WriteObservedEnemy);
        WriteArray(
            writer,
            "visibleTiles",
            observation.VisibleTiles,
            WriteObservedTile);
        WriteNullableArray(
            writer,
            "visibleProjectiles",
            observation.VisibleProjectiles,
            WriteObservedProjectile);
        WriteArray(
            writer,
            "visibleEvents",
            observation.VisibleEvents,
            WriteObservedEvent);
        WriteNullableArray(
            writer,
            "heardSounds",
            observation.HeardSounds,
            WriteObservedSound);
        writer.WritePropertyName("scoreboard");
        WriteScoreboard(writer, observation.Scoreboard);
        writer.WritePropertyName("mode");
        WriteModeState(writer, observation.Mode);
        WriteArray(
            writer,
            "alliedIntents",
            observation.AlliedIntents,
            WriteMindAlliedIntent);
        writer.WriteEndObject();
    }

    private static void WriteMindBody(
        Utf8JsonWriter writer,
        ReplayV3.MindBody body)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, body.ActorId);
        writer.WriteNumber("generation", body.Generation);
        writer.WriteString("formId", body.FormId);
        writer.WritePropertyName("position");
        WritePosition(writer, body.Position);
        writer.WriteString("facing", body.Facing);
        writer.WriteNumber("health", body.Health);
        writer.WriteNumber("cooldown", body.Cooldown);
        WriteNullableNumber(writer, "energy", body.Energy);
        writer.WritePropertyName("previousActionResolution");
        WriteNullableActionResolution(
            writer,
            body.PreviousActionResolution);
        writer.WritePropertyName("pendingSameLifeTransition");
        WritePendingTransition(
            writer,
            body.PendingSameLifeTransition);
        WriteNullableString(writer, "classId", body.ClassId);
        writer.WritePropertyName("previousPosition");
        if (body.PreviousPosition is null)
            writer.WriteNullValue();
        else
            WritePosition(writer, body.PreviousPosition);
        writer.WriteBoolean("movedLastTick", body.MovedLastTick);
        writer.WriteNumber("lifeStartedTick", body.LifeStartedTick);
        writer.WritePropertyName("origin");
        WriteLifeOrigin(writer, body.Origin);
        WriteUInt64String(
            writer,
            "bodyRandomSeed",
            body.BodyRandomSeed);
        WriteRouteCooldowns(writer, body.RouteCooldowns);
        WriteCarriedScrap(writer, body.CarriedScrap);
        WriteRoleTag(writer, body.RoleTag);
        WriteArray(
            writer,
            "actionLegalities",
            body.ActionLegalities,
            WriteActionLegality);
        writer.WriteEndObject();
    }

    private static void WriteMindSlot(
        Utf8JsonWriter writer,
        ReplayV3.MindSlot slot)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", slot.TeamId);
        writer.WriteNumber("unitId", slot.UnitId);
        writer.WritePropertyName("state");
        WriteUnitSlotState(writer, slot.State);
        // Reserved composition facts, emitted only when a ruleset declares
        // them (§9.2/§10.2): absence has ONE encoding and a classless contract
        // never carries the keys.
        if (slot.ClassId is not null)
            writer.WriteString("classId", slot.ClassId);
        if (!slot.CandidateClassIds.IsDefaultOrEmpty)
        {
            WriteStringArray(
                writer,
                "candidateClassIds",
                slot.CandidateClassIds);
        }
        if (slot.SelectedClassId is not null)
            writer.WriteString("selectedClassId", slot.SelectedClassId);
        writer.WriteEndObject();
    }

    private static void WriteLifeStart(
        Utf8JsonWriter writer,
        ReplayV3.LifeStart start)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", start.SchemaVersion);
        writer.WriteNumber(
            "runtimeContractVersion",
            start.RuntimeContractVersion);
        writer.WritePropertyName("actorId");
        WriteActorId(writer, start.ActorId);
        writer.WriteNumber("participantId", start.ParticipantId);
        WriteUInt64String(
            writer,
            "actorRandomSeed",
            start.ActorRandomSeed);
        writer.WritePropertyName("origin");
        WriteLifeOrigin(writer, start.Origin);
        writer.WriteString(
            "matchContractFingerprint",
            start.MatchContractFingerprint);
        // Trailing additive key: written whenever the recording engine knows
        // the team seed, omitted only for a history that predates it.
        if (start.TeamRandomSeed is { } teamRandomSeed)
        {
            WriteUInt64String(writer, "teamRandomSeed", teamRandomSeed);
        }
        writer.WriteEndObject();
    }

    private static void WriteLifeOrigin(
        Utf8JsonWriter writer,
        ReplayV3.LifeOrigin origin)
    {
        writer.WriteStartObject();
        writer.WriteString("reason", origin.Reason);
        writer.WriteNumber("generation", origin.Generation);
        writer.WritePropertyName("parentActorId");
        WriteNullableActorId(writer, origin.ParentActorId);
        WriteNullableString(
            writer,
            "sourceTransitionId",
            origin.SourceTransitionId);
        WriteNullableString(
            writer,
            "sourceOperationId",
            origin.SourceOperationId);
        writer.WriteEndObject();
    }

    private static void WriteObservation(
        Utf8JsonWriter writer,
        ReplayV3.Observation observation)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "schemaVersion",
            observation.SchemaVersion);
        writer.WriteNumber("tick", observation.Tick);
        writer.WriteString(
            "matchContractFingerprint",
            observation.MatchContractFingerprint);
        writer.WritePropertyName("self");
        WriteObservedSelf(writer, observation.Self);
        WriteArray(
            writer,
            "teamUnits",
            observation.TeamUnits,
            WriteObservedUnitSlot);
        WriteArray(
            writer,
            "participants",
            observation.Participants,
            WriteParticipantStatus);
        WriteArray(
            writer,
            "allies",
            observation.Allies,
            WriteObservedAlly);
        WriteArray(
            writer,
            "enemies",
            observation.Enemies,
            WriteObservedEnemy);
        WriteArray(
            writer,
            "visibleTiles",
            observation.VisibleTiles,
            WriteObservedTile);
        WriteNullableArray(
            writer,
            "visibleProjectiles",
            observation.VisibleProjectiles,
            WriteObservedProjectile);
        WriteArray(
            writer,
            "visibleEvents",
            observation.VisibleEvents,
            WriteObservedEvent);
        WriteNullableArray(
            writer,
            "heardSounds",
            observation.HeardSounds,
            WriteObservedSound);
        writer.WritePropertyName("scoreboard");
        WriteScoreboard(writer, observation.Scoreboard);
        writer.WritePropertyName("mode");
        WriteModeState(writer, observation.Mode);
        WriteArray(
            writer,
            "actionLegalities",
            observation.ActionLegalities,
            WriteActionLegality);
        writer.WriteEndObject();
    }

    private static void WriteObservedSelf(
        Utf8JsonWriter writer,
        ReplayV3.ObservedSelf value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, value.ActorId);
        writer.WriteNumber("generation", value.Generation);
        writer.WriteString("formId", value.FormId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("facing", value.Facing);
        writer.WriteNumber("health", value.Health);
        writer.WriteNumber("cooldown", value.Cooldown);
        WriteNullableNumber(writer, "energy", value.Energy);
        writer.WritePropertyName("previousActionResolution");
        WriteNullableActionResolution(
            writer,
            value.PreviousActionResolution);
        writer.WritePropertyName("pendingSameLifeTransition");
        WritePendingTransition(
            writer,
            value.PendingSameLifeTransition);
        WriteNullableString(writer, "classId", value.ClassId);
        WriteRouteCooldowns(writer, value.RouteCooldowns);
        WriteCarriedScrap(writer, value.CarriedScrap);
        writer.WriteEndObject();
    }

    private static void WriteObservedAlly(
        Utf8JsonWriter writer,
        ReplayV3.ObservedAlly value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, value.ActorId);
        writer.WriteNumber("generation", value.Generation);
        writer.WriteString("formId", value.FormId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("facing", value.Facing);
        writer.WriteNumber("health", value.Health);
        writer.WriteNumber("cooldown", value.Cooldown);
        WriteNullableNumber(writer, "energy", value.Energy);
        writer.WritePropertyName("previousActionResolution");
        WriteNullableActionResolution(
            writer,
            value.PreviousActionResolution);
        writer.WritePropertyName("pendingSameLifeTransition");
        WritePendingTransition(
            writer,
            value.PendingSameLifeTransition);
        WriteNullableString(writer, "classId", value.ClassId);
        WriteRouteCooldowns(writer, value.RouteCooldowns);
        WriteCarriedScrap(writer, value.CarriedScrap);
        WriteRoleTag(writer, value.RoleTag);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Canonical form for a published role tag (§12): the key exists only when
    /// a mind has labelled the body, so every per-life replay and every
    /// document written before tags existed serializes byte-exactly as before.
    /// </summary>
    private static void WriteRoleTag(
        Utf8JsonWriter writer,
        string? value)
    {
        if (value is null)
            return;
        writer.WriteString("roleTag", value);
    }

    private static void WriteObservedEnemy(
        Utf8JsonWriter writer,
        ReplayV3.ObservedEnemy value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, value.ActorId);
        writer.WriteString("formId", value.FormId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("facing", value.Facing);
        writer.WriteNumber("health", value.Health);
        writer.WritePropertyName("pendingSameLifeTransition");
        WritePendingTransition(
            writer,
            value.PendingSameLifeTransition);
        WriteArray(
            writer,
            "observedBy",
            value.ObservedBy,
            WriteActorId);
        WriteNullableString(writer, "classId", value.ClassId);
        WriteCarriedScrap(writer, value.CarriedScrap);
        WriteRoleTag(writer, value.RoleTag);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Canonical form for a body's carried scrap: the key exists only while
    /// the body is actually carrying, so every replay from a contract that
    /// declares no economy serializes byte-exactly as before.
    /// </summary>
    private static void WriteCarriedScrap(
        Utf8JsonWriter writer,
        int value)
    {
        if (value == 0)
            return;
        writer.WriteNumber("carriedScrap", value);
    }

    /// <summary>
    /// Canonical form for observed route cooldowns (#182): the key exists
    /// only while at least one clock is live, so every pre-#181 replay and
    /// every contract declaring no route cooldown serializes byte-exactly
    /// as before.
    /// </summary>
    private static void WriteRouteCooldowns(
        Utf8JsonWriter writer,
        ImmutableArray<ReplayV3.RouteCooldown> value)
    {
        if (value.IsDefaultOrEmpty)
            return;
        writer.WritePropertyName("routeCooldowns");
        writer.WriteStartArray();
        foreach (ReplayV3.RouteCooldown cooldown in value)
        {
            writer.WriteStartObject();
            writer.WriteString("transitionId", cooldown.TransitionId);
            writer.WriteNumber("readyAtTick", cooldown.ReadyAtTick);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteScrapTeams(
        Utf8JsonWriter writer,
        ImmutableArray<ReplayV3.ScrapTeam> value)
    {
        if (value.IsDefaultOrEmpty)
            return;
        writer.WritePropertyName("scrapTeams");
        writer.WriteStartArray();
        foreach (ReplayV3.ScrapTeam team in value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", team.TeamId);
            writer.WriteNumber("bank", team.Bank);
            writer.WritePropertyName("tierLevels");
            writer.WriteStartArray();
            foreach (int tier in team.TierLevels)
                writer.WriteNumberValue(tier);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteScrapPiles(
        Utf8JsonWriter writer,
        ImmutableArray<ReplayV3.ScrapPile> value)
    {
        if (value.IsDefaultOrEmpty)
            return;
        writer.WritePropertyName("scrapPiles");
        writer.WriteStartArray();
        foreach (ReplayV3.ScrapPile pile in value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("position");
            WritePosition(writer, pile.Position);
            writer.WriteNumber("amount", pile.Amount);
            writer.WriteNumber("expiresAtTick", pile.ExpiresAtTick);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WritePendingTransition(
        Utf8JsonWriter writer,
        ReplayV3.PendingSameLifeTransition? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("transitionId", value.TransitionId);
        writer.WriteString("operationId", value.OperationId);
        writer.WriteString("targetFormId", value.TargetFormId);
        writer.WriteNumber("startedTick", value.StartedTick);
        writer.WriteNumber("dueTick", value.DueTick);
        writer.WriteEndObject();
    }

    private static void WriteObservedUnitSlot(
        Utf8JsonWriter writer,
        ReplayV3.ObservedUnitSlot value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", value.TeamId);
        writer.WriteNumber("unitId", value.UnitId);
        writer.WritePropertyName("state");
        WriteUnitSlotState(writer, value.State);
        writer.WriteEndObject();
    }

    internal static void WriteUnitSlotState(
        Utf8JsonWriter writer,
        ReplayV3.UnitSlotState value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.UnitSlotState.Active active:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, active.ActorId);
                writer.WriteNumber("generation", active.Generation);
                writer.WriteString("formId", active.FormId);
                break;
            case ReplayV3.UnitSlotState.AvailabilityPending pending:
                writer.WriteString("reason", pending.Reason);
                writer.WriteNumber("dueTick", pending.DueTick);
                break;
            case ReplayV3.UnitSlotState.AutomaticReturnPending pending:
                writer.WriteNumber("dueTick", pending.DueTick);
                writer.WriteString(
                    "targetFormId",
                    pending.TargetFormId);
                writer.WriteNumber("generation", pending.Generation);
                break;
            case ReplayV3.UnitSlotState.Ready:
            case ReplayV3.UnitSlotState.PermanentlyDormant:
                break;
            case ReplayV3.UnitSlotState.FabricationPending pending:
                WriteLifecyclePending(writer, pending);
                break;
            case ReplayV3.UnitSlotState.ReplicationPending pending:
                WriteLifecyclePending(writer, pending);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 slot state '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteLifecyclePending(
        Utf8JsonWriter writer,
        ReplayV3.UnitSlotState.FabricationPending value)
    {
        writer.WriteNumber("dueTick", value.DueTick);
        writer.WritePropertyName("sourceActorId");
        WriteActorId(writer, value.SourceActorId);
        writer.WriteString("transitionId", value.TransitionId);
        writer.WriteString("operationId", value.OperationId);
        writer.WriteString("targetFormId", value.TargetFormId);
        writer.WritePropertyName("reservedPosition");
        WritePosition(writer, value.ReservedPosition);
    }

    private static void WriteLifecyclePending(
        Utf8JsonWriter writer,
        ReplayV3.UnitSlotState.ReplicationPending value)
    {
        writer.WriteNumber("dueTick", value.DueTick);
        writer.WritePropertyName("sourceActorId");
        WriteActorId(writer, value.SourceActorId);
        writer.WriteString("transitionId", value.TransitionId);
        writer.WriteString("operationId", value.OperationId);
        writer.WriteString("targetFormId", value.TargetFormId);
        writer.WritePropertyName("reservedPosition");
        WritePosition(writer, value.ReservedPosition);
    }

    private static void WriteParticipantStatus(
        Utf8JsonWriter writer,
        ReplayV3.ParticipantStatus value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("participantId", value.ParticipantId);
        writer.WriteNumber("teamId", value.TeamId);
        WriteInt64String(
            writer,
            "runtimeFaultCount",
            value.RuntimeFaultCount,
            nonNegative: true);
        writer.WriteBoolean("disqualified", value.Disqualified);
        WriteNullableString(writer, "classId", value.ClassId);
        writer.WriteEndObject();
    }

    private static void WriteObservedTile(
        Utf8JsonWriter writer,
        ReplayV3.ObservedTile value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteBoolean("isWall", value.IsWall);
        WriteArray(
            writer,
            "observedBy",
            value.ObservedBy,
            WriteActorId);
        writer.WritePropertyName("spawnReservation");
        if (value.SpawnReservation is { } reservation)
            WriteSpawnReservation(writer, reservation);
        else
            writer.WriteNullValue();
        writer.WriteEndObject();
    }

    private static void WriteSpawnReservation(
        Utf8JsonWriter writer,
        ReplayV3.SpawnReservation value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", value.TeamId);
        writer.WriteNumber("unitId", value.UnitId);
        writer.WriteString("kind", value.Kind);
        WriteNullableNumber(writer, "dueTick", value.DueTick);
        writer.WriteEndObject();
    }

    private static void WriteObservedProjectile(
        Utf8JsonWriter writer,
        ReplayV3.ObservedProjectile value)
    {
        writer.WriteStartObject();
        WriteInt64String(
            writer,
            "projectileId",
            value.ProjectileId,
            nonNegative: true);
        writer.WriteNumber("ownerTeamId", value.OwnerTeamId);
        writer.WritePropertyName("ownerActorId");
        WriteNullableActorId(writer, value.OwnerActorId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("heading", value.Heading);
        writer.WriteNumber(
            "tilesPerAdvance",
            value.TilesPerAdvance);
        writer.WriteNumber(
            "ticksUntilAdvance",
            value.TicksUntilAdvance);
        writer.WriteNumber("remainingTiles", value.RemainingTiles);
        WriteArray(
            writer,
            "observedBy",
            value.ObservedBy,
            WriteActorId);
        writer.WriteNumber("ticksPerAdvance", value.TicksPerAdvance);
        writer.WriteNumber("damagePerHit", value.DamagePerHit);
        writer.WriteEndObject();
    }

    private static void WriteObservedEvent(
        Utf8JsonWriter writer,
        ReplayV3.ObservedEvent value)
    {
        writer.WriteStartObject();
        writer.WriteString("eventHandle", value.EventHandle);
        writer.WriteNumber("sourceTick", value.SourceTick);
        writer.WriteNumber("sourceOrdinal", value.SourceOrdinal);
        writer.WriteString("kind", value.Kind);
        writer.WritePropertyName("payload");
        WriteEventPayload(writer, value.Payload);
        WriteArray(
            writer,
            "observedBy",
            value.ObservedBy,
            WriteActorId);
        writer.WriteEndObject();
    }

    private static void WriteObservedSound(
        Utf8JsonWriter writer,
        ReplayV3.ObservedSound value)
    {
        writer.WriteStartObject();
        writer.WriteString("eventHandle", value.EventHandle);
        writer.WriteNumber("sourceTick", value.SourceTick);
        writer.WriteNumber("sourceOrdinal", value.SourceOrdinal);
        writer.WritePropertyName("observerActorId");
        WriteActorId(writer, value.ObserverActorId);
        writer.WriteString("kind", value.Kind);
        writer.WriteNumber("bearing", value.Bearing);
        writer.WriteNumber("distance", value.Distance);
        writer.WriteEndObject();
    }

    private static void WriteSubmittedDecision(
        Utf8JsonWriter writer,
        ReplayV3.SubmittedDecision value)
    {
        writer.WriteStartObject();
        WriteNullableString(writer, "actionId", value.ActionId);
        writer.WriteNumber("actionCode", value.ActionCode);
        WriteNullableRawActionArguments(
            writer,
            value.Arguments);
        WriteNullableString(
            writer,
            "debugMessage",
            value.DebugMessage);
        writer.WriteEndObject();
    }

    private static void WriteNullableRawActionArguments(
        Utf8JsonWriter writer,
        ImmutableArray<ReplayV3.RawActionArgument?>? arguments)
    {
        writer.WritePropertyName("arguments");
        if (arguments is null)
        {
            writer.WriteNullValue();
            return;
        }
        if (arguments.Value.IsDefault)
        {
            throw new ArgumentException(
                "Replay-v3 raw decision arguments are uninitialized.");
        }

        writer.WriteStartArray();
        foreach (ReplayV3.RawActionArgument? argument in
                 arguments.Value)
        {
            if (argument is null)
                writer.WriteNullValue();
            else
                WriteRawActionArgument(writer, argument);
        }
        writer.WriteEndArray();
    }

    private static void WriteRawActionArgument(
        Utf8JsonWriter writer,
        ReplayV3.RawActionArgument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.RawActionArgument.ShotProgram argument:
                writer.WritePropertyName("value");
                WriteShotProgram(writer, argument.Value);
                break;
            case ReplayV3.RawActionArgument.Direction argument:
                writer.WriteNumber("value", argument.Value);
                break;
            case ReplayV3.RawActionArgument.UnitTarget argument:
                writer.WritePropertyName("value");
                WriteUnitTarget(
                    writer,
                    argument.TeamId,
                    argument.UnitId);
                break;
            case ReplayV3.RawActionArgument.FormTarget argument:
                WriteNullableString(
                    writer,
                    "formId",
                    argument.FormId);
                break;
            case ReplayV3.RawActionArgument.ProjectileHeading argument:
                writer.WriteNumber("value", argument.Value);
                break;
            case ReplayV3.RawActionArgument.UpgradeTrack argument:
                WriteNullableString(
                    writer,
                    "trackId",
                    argument.TrackId);
                break;
            case ReplayV3.RawActionArgument.PositionTarget argument:
                writer.WritePropertyName("value");
                WritePosition(writer, argument.Value);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 raw action argument '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteNullableActionResolution(
        Utf8JsonWriter writer,
        ReplayV3.ActionResolution? value)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            WriteActionResolution(writer, value);
    }

    internal static void WriteActionResolution(
        Utf8JsonWriter writer,
        ReplayV3.ActionResolution value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("submittedAction");
        if (value.SubmittedAction is null)
            writer.WriteNullValue();
        else
            WriteResolvedAction(writer, value.SubmittedAction);
        writer.WritePropertyName("acceptedAction");
        WriteResolvedAction(writer, value.AcceptedAction);
        writer.WritePropertyName("validatedAction");
        WriteResolvedAction(writer, value.ValidatedAction);
        writer.WriteString("outcome", value.Outcome);
        writer.WritePropertyName("runtimeFault");
        if (value.RuntimeFault is null)
            writer.WriteNullValue();
        else
            WriteRuntimeFault(writer, value.RuntimeFault);
        writer.WriteEndObject();
    }

    internal static void WriteResolvedAction(
        Utf8JsonWriter writer,
        ReplayV3.ResolvedAction value)
    {
        writer.WriteStartObject();
        writer.WriteString("actionId", value.ActionId);
        writer.WriteNumber("actionCode", value.ActionCode);
        WriteArray(
            writer,
            "arguments",
            value.Arguments,
            WriteActionArgument);
        writer.WriteEndObject();
    }

    internal static void WriteActionArgument(
        Utf8JsonWriter writer,
        ReplayV3.ActionArgument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.ActionArgument.ShotProgram argument:
                writer.WritePropertyName("value");
                WriteShotProgram(writer, argument.Value);
                break;
            case ReplayV3.ActionArgument.Direction argument:
                writer.WriteString("value", argument.Value);
                break;
            case ReplayV3.ActionArgument.UnitTarget argument:
                writer.WritePropertyName("value");
                WriteUnitTarget(
                    writer,
                    argument.TeamId,
                    argument.UnitId);
                break;
            case ReplayV3.ActionArgument.FormTarget argument:
                writer.WriteString("formId", argument.FormId);
                break;
            case ReplayV3.ActionArgument.ProjectileHeading argument:
                writer.WriteString("value", argument.Value);
                break;
            case ReplayV3.ActionArgument.UpgradeTrack argument:
                writer.WriteString("trackId", argument.TrackId);
                break;
            case ReplayV3.ActionArgument.PositionTarget argument:
                writer.WritePropertyName("value");
                WritePosition(writer, argument.Value);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 action argument '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteRuntimeFault(
        Utf8JsonWriter writer,
        ReplayV3.RuntimeFault value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("participantId", value.ParticipantId);
        writer.WritePropertyName("actorId");
        WriteActorId(writer, value.ActorId);
        writer.WriteString("stage", value.Stage);
        writer.WriteString("faultCode", value.FaultCode);
        WriteInt64String(
            writer,
            "cumulativeFaultCount",
            value.CumulativeFaultCount,
            nonNegative: true);
        writer.WriteBoolean(
            "disqualificationTriggered",
            value.DisqualificationTriggered);
        writer.WriteEndObject();
    }

    private static void WriteActionLegality(
        Utf8JsonWriter writer,
        ReplayV3.ActionLegality value)
    {
        writer.WriteStartObject();
        writer.WriteString("actionId", value.ActionId);
        writer.WriteNumber("actionCode", value.ActionCode);
        writer.WriteBoolean(
            "allowedByForm",
            value.AllowedByForm);
        writer.WriteBoolean("available", value.Available);
        WriteArray(
            writer,
            "constraints",
            value.Constraints,
            WriteActionConstraint);
        writer.WriteEndObject();
    }

    private static void WriteActionConstraint(
        Utf8JsonWriter writer,
        ReplayV3.ActionConstraint value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.ActionConstraint.ShotProgram constraint:
                writer.WriteBoolean("allowed", constraint.Allowed);
                break;
            case ReplayV3.ActionConstraint.Direction constraint:
                WriteStringArray(
                    writer,
                    "allowedValues",
                    constraint.AllowedValues);
                break;
            case ReplayV3.ActionConstraint.UnitTarget constraint:
                WriteArray(
                    writer,
                    "allowedValues",
                    constraint.AllowedValues,
                    static (json, target) =>
                        WriteUnitTarget(
                            json,
                            target.TeamId,
                            target.UnitId));
                break;
            case ReplayV3.ActionConstraint.FormTarget constraint:
                WriteStringArray(
                    writer,
                    "allowedFormIds",
                    constraint.AllowedFormIds);
                break;
            case ReplayV3.ActionConstraint.ProjectileHeading constraint:
                WriteStringArray(
                    writer,
                    "allowedValues",
                    constraint.AllowedValues);
                break;
            case ReplayV3.ActionConstraint.UpgradeTrack constraint:
                WriteStringArray(
                    writer,
                    "allowedTrackIds",
                    constraint.AllowedTrackIds);
                break;
            case ReplayV3.ActionConstraint.PositionTarget constraint:
                WriteArray(
                    writer,
                    "allowedValues",
                    constraint.AllowedValues,
                    WritePosition);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 action constraint '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    internal static void WriteEvent(
        Utf8JsonWriter writer,
        ReplayV3.AuthoritativeEvent value)
    {
        writer.WriteStartObject();
        writer.WriteString("eventHandle", value.EventHandle);
        writer.WriteNumber("tick", value.Tick);
        WriteInt64String(
            writer,
            "globalOrdinal",
            value.GlobalOrdinal,
            nonNegative: true);
        writer.WriteNumber("sourceOrdinal", value.SourceOrdinal);
        writer.WriteString("kind", value.Kind);
        writer.WritePropertyName("payload");
        WriteEventPayload(writer, value.Payload);
        writer.WritePropertyName("audience");
        WriteEventAudience(writer, value.Audience);
        writer.WriteEndObject();
    }

    private static void WriteEventPayload(
        Utf8JsonWriter writer,
        ReplayV3.EventPayload value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.EventPayload.Rotation payload:
                WritePayloadActorAction(writer, payload.ActorId, payload.Action);
                writer.WritePropertyName("position");
                WritePosition(writer, payload.Position);
                writer.WriteString("fromFacing", payload.FromFacing);
                writer.WriteString("toFacing", payload.ToFacing);
                break;
            case ReplayV3.EventPayload.Movement payload:
                WritePayloadActorAction(writer, payload.ActorId, payload.Action);
                writer.WritePropertyName("from");
                WritePosition(writer, payload.From);
                writer.WritePropertyName("to");
                WritePosition(writer, payload.To);
                writer.WriteString("facing", payload.Facing);
                break;
            case ReplayV3.EventPayload.MovementBlocked payload:
                WritePayloadActorAction(writer, payload.ActorId, payload.Action);
                writer.WritePropertyName("from");
                WritePosition(writer, payload.From);
                writer.WritePropertyName("attemptedTo");
                WritePosition(writer, payload.AttemptedTo);
                writer.WriteString("facing", payload.Facing);
                break;
            case ReplayV3.EventPayload.Attack payload:
                WritePayloadActorAction(writer, payload.ActorId, payload.Action);
                WriteInt64String(
                    writer,
                    "projectileId",
                    payload.ProjectileId,
                    nonNegative: true);
                writer.WritePropertyName("origin");
                WritePosition(writer, payload.Origin);
                writer.WriteString("heading", payload.Heading);
                break;
            case ReplayV3.EventPayload.Damage payload:
                writer.WriteNumber("sourceTeamId", payload.SourceTeamId);
                writer.WritePropertyName("sourceActorId");
                WriteNullableActorId(writer, payload.SourceActorId);
                writer.WritePropertyName("targetActorId");
                WriteActorId(writer, payload.TargetActorId);
                WriteInt64String(
                    writer,
                    "projectileId",
                    payload.ProjectileId,
                    nonNegative: true);
                writer.WriteNumber("amount", payload.Amount);
                writer.WriteNumber("newHealth", payload.NewHealth);
                writer.WritePropertyName("position");
                WritePosition(writer, payload.Position);
                break;
            case ReplayV3.EventPayload.ProjectileDeflected payload:
                writer.WriteNumber("sourceTeamId", payload.SourceTeamId);
                writer.WritePropertyName("sourceActorId");
                WriteNullableActorId(writer, payload.SourceActorId);
                writer.WritePropertyName("targetActorId");
                WriteActorId(writer, payload.TargetActorId);
                WriteInt64String(
                    writer,
                    "projectileId",
                    payload.ProjectileId,
                    nonNegative: true);
                WriteInt64String(
                    writer,
                    "deflectedProjectileId",
                    payload.DeflectedProjectileId,
                    nonNegative: true);
                writer.WriteString("targetFormId", payload.TargetFormId);
                writer.WriteString("targetFacing", payload.TargetFacing);
                writer.WriteString("heading", payload.Heading);
                writer.WritePropertyName("position");
                WritePosition(writer, payload.Position);
                break;
            case ReplayV3.EventPayload.Destruction payload:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, payload.ActorId);
                WriteNullableNumber(
                    writer,
                    "sourceTeamId",
                    payload.SourceTeamId);
                writer.WritePropertyName("sourceActorId");
                WriteNullableActorId(writer, payload.SourceActorId);
                WriteNullableInt64String(
                    writer,
                    "projectileId",
                    payload.ProjectileId,
                    nonNegative: true);
                writer.WriteNumber("generation", payload.Generation);
                writer.WriteString("formId", payload.FormId);
                writer.WritePropertyName("position");
                WritePosition(writer, payload.Position);
                break;
            case ReplayV3.EventPayload.LifeSpawned payload:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, payload.ActorId);
                writer.WriteNumber(
                    "participantId",
                    payload.ParticipantId);
                writer.WritePropertyName("parentActorId");
                WriteNullableActorId(writer, payload.ParentActorId);
                writer.WriteNumber("generation", payload.Generation);
                writer.WriteString("formId", payload.FormId);
                writer.WriteNumber("health", payload.Health);
                writer.WritePropertyName("position");
                WritePosition(writer, payload.Position);
                writer.WriteString("reason", payload.Reason);
                WriteNullableString(
                    writer,
                    "sourceTransitionId",
                    payload.SourceTransitionId);
                WriteNullableString(
                    writer,
                    "sourceOperationId",
                    payload.SourceOperationId);
                break;
            case ReplayV3.EventPayload.LifeRetired payload:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, payload.ActorId);
                writer.WriteNumber("generation", payload.Generation);
                writer.WriteString("formId", payload.FormId);
                writer.WritePropertyName("position");
                WritePosition(writer, payload.Position);
                writer.WriteString("reason", payload.Reason);
                WriteNullableString(
                    writer,
                    "sourceTransitionId",
                    payload.SourceTransitionId);
                WriteNullableString(
                    writer,
                    "sourceOperationId",
                    payload.SourceOperationId);
                break;
            case ReplayV3.EventPayload.RuntimeFaultValue payload:
                writer.WritePropertyName("fault");
                WriteRuntimeFault(writer, payload.Fault);
                break;
            case ReplayV3.EventPayload.MindRuntimeFaultValue payload:
                writer.WritePropertyName("fault");
                WriteMindRuntimeFault(writer, payload.Fault);
                break;
            case ReplayV3.EventPayload.Participant payload:
                writer.WriteNumber(
                    "participantId",
                    payload.ParticipantId);
                writer.WriteNumber("teamId", payload.TeamId);
                break;
            case ReplayV3.EventPayload.Lifecycle payload:
                writer.WriteString(
                    "transitionId",
                    payload.TransitionId);
                writer.WriteString("operationId", payload.OperationId);
                writer.WritePropertyName("sourceActorId");
                WriteActorId(writer, payload.SourceActorId);
                WriteNullableNumber(
                    writer,
                    "targetTeamId",
                    payload.TargetTeamId);
                WriteNullableNumber(
                    writer,
                    "targetUnitId",
                    payload.TargetUnitId);
                WriteNullableNumber(
                    writer,
                    "dueTick",
                    payload.DueTick);
                WriteNullableString(
                    writer,
                    "cancellationReason",
                    payload.CancellationReason);
                break;
            case ReplayV3.EventPayload.FormTransition payload:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, payload.ActorId);
                writer.WriteString(
                    "transitionId",
                    payload.TransitionId);
                writer.WriteString("operationId", payload.OperationId);
                writer.WriteString("fromFormId", payload.FromFormId);
                writer.WriteString("toFormId", payload.ToFormId);
                writer.WriteNumber(
                    "startedTick",
                    payload.StartedTick);
                writer.WriteNumber("dueTick", payload.DueTick);
                // Inert-default omission: a requested transition writes no
                // reason at all, so replays authored before automatic returns
                // existed stay byte-identical (DECISIONS #156).
                if (payload.Reason is string transitionReason)
                    writer.WriteString("reason", transitionReason);
                break;
            case ReplayV3.EventPayload.ScoreChanged payload:
                writer.WriteNumber("teamId", payload.TeamId);
                writer.WriteString("channel", payload.Channel);
                WriteInt64String(
                    writer,
                    "newValue",
                    payload.NewValue,
                    nonNegative: false);
                break;
            case ReplayV3.EventPayload.ModeChanged payload:
                writer.WritePropertyName("state");
                WriteModeState(writer, payload.State);
                break;
            case ReplayV3.EventPayload.LifecycleClockCancelled payload:
                writer.WriteNumber(
                    "targetTeamId",
                    payload.TargetTeamId);
                writer.WriteNumber(
                    "targetUnitId",
                    payload.TargetUnitId);
                writer.WritePropertyName("cancelledState");
                WriteUnitSlotState(writer, payload.CancelledState);
                writer.WriteString(
                    "cancellationReason",
                    payload.CancellationReason);
                break;
            case ReplayV3.EventPayload.ArcRelay payload:
                writer.WritePropertyName("fact");
                WriteArcRelayFact(writer, payload.Fact);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 event payload '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteArcRelayFact(
        Utf8JsonWriter writer,
        ReplayV3.ArcRelayFact value)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.ArcRelayFact.CoreBorn fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                if (fact.ChargeValue != 1)
                    writer.WriteNumber("chargeValue", fact.ChargeValue);
                break;
            case ReplayV3.ArcRelayFact.CoreRipened fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                writer.WriteNumber("value", fact.Value);
                break;
            case ReplayV3.ArcRelayFact.LeveledUp fact:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, fact.ActorId);
                writer.WriteNumber("level", fact.Level);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                break;
            case ReplayV3.ArcRelayFact.ZoneHealed fact:
                writer.WritePropertyName("actorId");
                WriteActorId(writer, fact.ActorId);
                writer.WriteNumber("amount", fact.Amount);
                writer.WriteNumber("newHealth", fact.NewHealth);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                break;
            case ReplayV3.ArcRelayFact.CorePickedUp fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("carrierActorId");
                WriteActorId(writer, fact.CarrierActorId);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                writer.WriteNumber("nextRelocationTick", fact.NextRelocationTick);
                break;
            case ReplayV3.ArcRelayFact.CoreRelocated fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("carrierActorId");
                if (fact.CarrierActorId is null) writer.WriteNullValue();
                else WriteActorId(writer, fact.CarrierActorId);
                writer.WritePropertyName("from");
                WritePosition(writer, fact.From);
                writer.WritePropertyName("to");
                WritePosition(writer, fact.To);
                writer.WriteNumber("nextRelocationTick", fact.NextRelocationTick);
                writer.WriteString("relocationKind", fact.RelocationKind);
                break;
            case ReplayV3.ArcRelayFact.CoreHandedOff fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("sourceActorId");
                WriteActorId(writer, fact.SourceActorId);
                writer.WritePropertyName("targetActorId");
                WriteActorId(writer, fact.TargetActorId);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                writer.WriteNumber("nextRelocationTick", fact.NextRelocationTick);
                break;
            case ReplayV3.ArcRelayFact.CoreDropped fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("sourceActorId");
                WriteActorId(writer, fact.SourceActorId);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                writer.WriteNumber("nextRelocationTick", fact.NextRelocationTick);
                writer.WriteString("dropKind", fact.DropKind);
                break;
            case ReplayV3.ArcRelayFact.CoreBanked fact:
                WriteArcCoreId(writer, "coreId", fact.CoreId);
                writer.WritePropertyName("carrierActorId");
                WriteActorId(writer, fact.CarrierActorId);
                writer.WriteNumber("teamId", fact.TeamId);
                writer.WritePropertyName("position");
                WritePosition(writer, fact.Position);
                writer.WriteNumber("chargePips", fact.ChargePips);
                break;
            case ReplayV3.ArcRelayFact.WellChanged fact:
                writer.WriteString("wellId", fact.WellId);
                writer.WriteBoolean("pendingCharge", fact.PendingCharge);
                WriteNullableNumber(
                    writer,
                    "rearmCompletesAtTick",
                    fact.RearmCompletesAtTick);
                writer.WritePropertyName("outstandingCoreId");
                if (fact.OutstandingCoreId is null) writer.WriteNullValue();
                else WriteArcCoreId(writer, fact.OutstandingCoreId);
                break;
            case ReplayV3.ArcRelayFact.Pulse fact:
                writer.WriteNumber("teamId", fact.TeamId);
                writer.WriteNumber("pulseOrdinal", fact.PulseOrdinal);
                writer.WriteNumber(
                    "opposingReactorIntegrity",
                    fact.OpposingReactorIntegrity);
                break;
            case ReplayV3.ArcRelayFact.SignatureChanged fact:
                writer.WriteString("operationId", fact.OperationId);
                writer.WriteString("signatureId", fact.SignatureId);
                writer.WritePropertyName("ownerActorId");
                WriteActorId(writer, fact.OwnerActorId);
                WriteNullableString(writer, "phase", fact.Phase);
                writer.WriteString("reason", fact.Reason);
                break;
            case ReplayV3.ArcRelayFact.BodyRelocated fact:
                writer.WriteString("operationId", fact.OperationId);
                writer.WriteString("signatureId", fact.SignatureId);
                writer.WritePropertyName("ownerActorId");
                WriteActorId(writer, fact.OwnerActorId);
                writer.WritePropertyName("targetActorId");
                WriteActorId(writer, fact.TargetActorId);
                writer.WritePropertyName("from");
                WritePosition(writer, fact.From);
                writer.WritePropertyName("to");
                WritePosition(writer, fact.To);
                break;
            case ReplayV3.ArcRelayFact.SignatureDamage fact:
                WriteArcSignatureHealthFact(writer, fact.OperationId,
                    fact.SignatureId, fact.OwnerActorId, fact.TargetActorId,
                    fact.Amount, fact.NewHealth, fact.Position);
                break;
            case ReplayV3.ArcRelayFact.SignatureRepair fact:
                WriteArcSignatureHealthFact(writer, fact.OperationId,
                    fact.SignatureId, fact.OwnerActorId, fact.TargetActorId,
                    fact.Amount, fact.NewHealth, fact.Position);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported Arc Relay fact '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteArcSignatureHealthFact(
        Utf8JsonWriter writer,
        string operationId,
        string signatureId,
        ReplayV3.ActorId owner,
        ReplayV3.ActorId target,
        int amount,
        int newHealth,
        ReplayV3.PositionValue position)
    {
        writer.WriteString("operationId", operationId);
        writer.WriteString("signatureId", signatureId);
        writer.WritePropertyName("ownerActorId");
        WriteActorId(writer, owner);
        writer.WritePropertyName("targetActorId");
        WriteActorId(writer, target);
        writer.WriteNumber("amount", amount);
        writer.WriteNumber("newHealth", newHealth);
        writer.WritePropertyName("position");
        WritePosition(writer, position);
    }

    private static void WriteArcCoreId(
        Utf8JsonWriter writer,
        string propertyName,
        ReplayV3.ArcCoreId value)
    {
        writer.WritePropertyName(propertyName);
        WriteArcCoreId(writer, value);
    }

    private static void WriteArcCoreId(
        Utf8JsonWriter writer,
        ReplayV3.ArcCoreId value)
    {
        writer.WriteStartObject();
        writer.WriteString("sourceWellId", value.SourceWellId);
        writer.WriteNumber("sourceOrdinal", value.SourceOrdinal);
        writer.WriteEndObject();
    }

    private static void WritePayloadActorAction(
        Utf8JsonWriter writer,
        ReplayV3.ActorId actorId,
        ReplayV3.ResolvedAction action)
    {
        writer.WritePropertyName("actorId");
        WriteActorId(writer, actorId);
        writer.WritePropertyName("action");
        WriteResolvedAction(writer, action);
    }

    private static void WriteEventAudience(
        Utf8JsonWriter writer,
        ReplayV3.EventAudience value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.EventAudience.Public:
                break;
            case ReplayV3.EventAudience.Spatial audience:
                writer.WritePropertyName("primaryPosition");
                WritePosition(writer, audience.PrimaryPosition);
                break;
            case ReplayV3.EventAudience.TeamPrivate audience:
                writer.WriteNumber("teamId", audience.TeamId);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 event audience '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    internal static void WriteTraversal(
        Utf8JsonWriter writer,
        ReplayV3.ProjectileTraversal value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("tick", value.Tick);
        WriteInt64String(
            writer,
            "globalOrdinal",
            value.GlobalOrdinal,
            nonNegative: true);
        writer.WriteString("phase", value.Phase);
        writer.WriteString("trigger", value.Trigger);
        WriteInt64String(
            writer,
            "projectileId",
            value.ProjectileId,
            nonNegative: true);
        writer.WriteNumber(
            "ownerParticipantId",
            value.OwnerParticipantId);
        writer.WriteNumber("ownerTeamId", value.OwnerTeamId);
        writer.WritePropertyName("ownerActorId");
        WriteActorId(writer, value.OwnerActorId);
        writer.WriteString(
            "attackProfileId",
            value.AttackProfileId);
        writer.WritePropertyName("from");
        WritePosition(writer, value.From);
        WriteArray(writer, "path", value.Path, WritePosition);
        writer.WriteString("launchHeading", value.LaunchHeading);
        writer.WriteString("finalHeading", value.FinalHeading);
        writer.WritePropertyName("shotProgram");
        WriteNullableShotProgram(writer, value.ShotProgram);
        writer.WritePropertyName("terminal");
        WriteTraversalTerminal(writer, value.Terminal);
        writer.WriteEndObject();
    }

    private static void WriteTraversalTerminal(
        Utf8JsonWriter writer,
        ReplayV3.TraversalTerminal value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.TraversalTerminal.Retained:
            case ReplayV3.TraversalTerminal.WallOrPathExhausted:
            case ReplayV3.TraversalTerminal.RangeExhausted:
                break;
            case ReplayV3.TraversalTerminal.ActorContact terminal:
                writer.WritePropertyName("targetActorId");
                WriteActorId(writer, terminal.TargetActorId);
                writer.WriteBoolean(
                    "appliedDamage",
                    terminal.AppliedDamage);
                break;
            case ReplayV3.TraversalTerminal.MovementContact terminal:
                writer.WritePropertyName("targetActorId");
                WriteActorId(writer, terminal.TargetActorId);
                writer.WriteBoolean(
                    "appliedDamage",
                    terminal.AppliedDamage);
                break;
            case ReplayV3.TraversalTerminal
                .LifecyclePlacementPurge terminal:
                writer.WritePropertyName("position");
                WritePosition(writer, terminal.Position);
                break;
            case ReplayV3.TraversalTerminal
                .ParticipantDisqualification terminal:
                writer.WriteNumber(
                    "participantId",
                    terminal.ParticipantId);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 traversal terminal '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    internal static void WriteWorldState(
        Utf8JsonWriter writer,
        ReplayV3.WorldState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        writer.WriteStartObject();
        writer.WriteString(
            "matchContractFingerprint",
            state.MatchContractFingerprint);
        writer.WriteNumber("nextTick", state.NextTick);
        WriteInt64String(
            writer,
            "nextProjectileId",
            state.NextProjectileId,
            nonNegative: true);
        WriteArray(
            writer,
            "participants",
            state.Participants,
            WriteParticipantStatus);
        WriteArray(writer, "slots", state.Slots, WriteSlotState);
        WriteArray(
            writer,
            "activeLives",
            state.ActiveLives,
            WriteLifeState);
        WriteArray(
            writer,
            "pendingReplications",
            state.PendingReplications,
            WritePendingReplication);
        WriteArray(
            writer,
            "projectiles",
            state.Projectiles,
            WriteProjectileState);
        writer.WritePropertyName("scoreboard");
        WriteScoreboard(writer, state.Scoreboard);
        writer.WritePropertyName("mode");
        WriteModeState(writer, state.Mode);
        writer.WriteEndObject();
    }

    private static void WriteSlotState(
        Utf8JsonWriter writer,
        ReplayV3.SlotState value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", value.TeamId);
        writer.WriteNumber("unitId", value.UnitId);
        writer.WriteNumber("participantId", value.ParticipantId);
        writer.WriteNumber("nextLifeId", value.NextLifeId);
        writer.WritePropertyName("state");
        WriteUnitSlotState(writer, value.State);
        writer.WritePropertyName("pendingParentActorId");
        WriteNullableActorId(writer, value.PendingParentActorId);
        writer.WritePropertyName("splitReservation");
        if (value.SplitReservation is null)
            writer.WriteNullValue();
        else
            WritePendingReplication(writer, value.SplitReservation);
        writer.WriteEndObject();
    }

    private static void WriteLifeState(
        Utf8JsonWriter writer,
        ReplayV3.LifeState value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("actorId");
        WriteActorId(writer, value.ActorId);
        writer.WriteNumber("participantId", value.ParticipantId);
        writer.WriteNumber("generation", value.Generation);
        writer.WriteString("formId", value.FormId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("facing", value.Facing);
        writer.WriteNumber("health", value.Health);
        writer.WriteNumber("cooldown", value.Cooldown);
        WriteNullableNumber(writer, "energy", value.Energy);
        writer.WriteNumber("spawnedAtTick", value.SpawnedAtTick);
        writer.WriteString("spawnReason", value.SpawnReason);
        writer.WritePropertyName("parentActorId");
        WriteNullableActorId(writer, value.ParentActorId);
        WriteNullableString(
            writer,
            "sourceTransitionId",
            value.SourceTransitionId);
        WriteNullableString(
            writer,
            "sourceOperationId",
            value.SourceOperationId);
        writer.WritePropertyName("previousActionResolution");
        WriteNullableActionResolution(
            writer,
            value.PreviousActionResolution);
        writer.WritePropertyName("pendingSameLifeTransition");
        WritePendingTransition(
            writer,
            value.PendingSameLifeTransition);
        writer.WriteEndObject();
    }

    private static void WritePendingReplication(
        Utf8JsonWriter writer,
        ReplayV3.PendingReplication value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("sourceActorId");
        WriteActorId(writer, value.SourceActorId);
        writer.WriteNumber("participantId", value.ParticipantId);
        writer.WriteNumber(
            "sourceGeneration",
            value.SourceGeneration);
        writer.WriteString("sourceFormId", value.SourceFormId);
        writer.WritePropertyName("sourcePosition");
        WritePosition(writer, value.SourcePosition);
        writer.WriteString("sourceFacing", value.SourceFacing);
        writer.WriteString("transitionId", value.TransitionId);
        writer.WriteString("operationId", value.OperationId);
        writer.WriteNumber("queuedTick", value.QueuedTick);
        writer.WriteNumber("dueTick", value.DueTick);
        WriteArray(
            writer,
            "descendants",
            value.Descendants,
            static (json, descendant) =>
            {
                json.WriteStartObject();
                json.WriteNumber("teamId", descendant.TeamId);
                json.WriteNumber("unitId", descendant.UnitId);
                json.WriteString("formId", descendant.FormId);
                json.WriteNumber(
                    "generation",
                    descendant.Generation);
                json.WritePropertyName("position");
                WritePosition(json, descendant.Position);
                json.WriteEndObject();
            });
        writer.WriteEndObject();
    }

    private static void WriteProjectileState(
        Utf8JsonWriter writer,
        ReplayV3.ProjectileState value)
    {
        writer.WriteStartObject();
        WriteInt64String(
            writer,
            "projectileId",
            value.ProjectileId,
            nonNegative: true);
        writer.WriteNumber(
            "ownerParticipantId",
            value.OwnerParticipantId);
        writer.WriteNumber("ownerTeamId", value.OwnerTeamId);
        writer.WritePropertyName("ownerActorId");
        WriteActorId(writer, value.OwnerActorId);
        writer.WriteString(
            "attackProfileId",
            value.AttackProfileId);
        writer.WriteNumber("spawnedAtTick", value.SpawnedAtTick);
        writer.WritePropertyName("origin");
        WritePosition(writer, value.Origin);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("launchHeading", value.LaunchHeading);
        writer.WriteString("heading", value.Heading);
        writer.WritePropertyName("shotProgram");
        WriteNullableShotProgram(writer, value.ShotProgram);
        WriteArray(
            writer,
            "committedPath",
            value.CommittedPath,
            WritePosition);
        writer.WriteNumber("nextPathIndex", value.NextPathIndex);
        writer.WriteNumber("remainingTiles", value.RemainingTiles);
        writer.WriteNumber(
            "ticksUntilAdvance",
            value.TicksUntilAdvance);
        writer.WriteEndObject();
    }

    internal static void WriteScoreboard(
        Utf8JsonWriter writer,
        ReplayV3.Scoreboard value)
    {
        writer.WriteStartObject();
        WriteArray(
            writer,
            "teams",
            value.Teams,
            static (json, team) =>
            {
                json.WriteStartObject();
                json.WriteNumber("teamId", team.TeamId);
                json.WriteBoolean("eligible", team.Eligible);
                WriteArray(
                    json,
                    "scores",
                    team.Scores,
                    WriteScoreValue);
                json.WriteEndObject();
            });
        writer.WriteEndObject();
    }

    private static void WriteScoreValue(
        Utf8JsonWriter writer,
        ReplayV3.ScoreValue value)
    {
        writer.WriteStartObject();
        writer.WriteString("channel", value.Channel);
        WriteInt64String(
            writer,
            "value",
            value.Value,
            nonNegative: false);
        writer.WriteEndObject();
    }

    internal static void WriteModeState(
        Utf8JsonWriter writer,
        ReplayV3.ModeState value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        writer.WriteString("modeId", value.ModeId);
        switch (value)
        {
            case ReplayV3.ModeState.Deathmatch:
                break;
            case ReplayV3.ModeState.Frontline frontline:
                writer.WriteNumber(
                    "activePositionIndex",
                    frontline.ActivePositionIndex);
                WriteNullableNumber(
                    writer,
                    "claimingTeamId",
                    frontline.ClaimingTeamId);
                writer.WriteNumber(
                    "captureProgress",
                    frontline.CaptureProgress);
                writer.WriteNumber(
                    "decayTicksElapsed",
                    frontline.DecayTicksElapsed);
                writer.WriteNumber(
                    "controlResumesAtTick",
                    frontline.ControlResumesAtTick);
                // Trailing additive pair, both nullable and always present:
                // the same discipline claimingTeamId already follows, where
                // null is a fact about this tick rather than an omitted
                // field. Null means no live territory-ratchet hold, which
                // includes every ruleset whose redeploy policy has none.
                WriteNullableNumber(
                    writer,
                    "holdOwnerTeamId",
                    frontline.HoldOwnerTeamId);
                WriteNullableNumber(
                    writer,
                    "holdEndsAtTick",
                    frontline.HoldEndsAtTick);
                // The side objective's two facts, on the same discipline:
                // an owner that is null this tick (including on every
                // ruleset that declares no side objective) and a signed
                // running claim whose sign names the claiming team.
                WriteNullableNumber(
                    writer,
                    "secondaryOwnerTeamId",
                    frontline.SecondaryOwnerTeamId);
                writer.WriteNumber(
                    "secondaryClaimProgress",
                    frontline.SecondaryClaimProgress);
                // The economy's two collections, on the same discipline: the
                // keys exist only on a ruleset that declares an economy, so
                // every replay produced before the capability existed
                // serializes byte-exactly as before.
                WriteScrapTeams(writer, frontline.ScrapTeams);
                WriteScrapPiles(writer, frontline.ScrapPiles);
                break;
            case ReplayV3.ModeState.ArcRelay arcRelay:
                WriteArray(writer, "wells", arcRelay.Wells, WriteArcWell);
                WriteArray(
                    writer,
                    "reactors",
                    arcRelay.Reactors,
                    WriteArcReactor);
                WriteArray(
                    writer,
                    "visibleCores",
                    arcRelay.VisibleCores,
                    WriteArcCore);
                WriteArray(
                    writer,
                    "visibleSignatures",
                    arcRelay.VisibleSignatures,
                    WriteArcSignature);
                WriteNullableNumber(
                    writer,
                    "latestPulseTeamId",
                    arcRelay.LatestPulseTeamId);
                WriteNullableNumber(
                    writer,
                    "latestPulseTick",
                    arcRelay.LatestPulseTick);
                // Declared strikes serialize only where they exist
                // (DECISIONS #212): every replay from a ruleset without
                // strike windups stays byte-exact.
                if (!arcRelay.PendingStrikes.IsEmpty)
                {
                    WriteArray(
                        writer,
                        "pendingStrikes",
                        arcRelay.PendingStrikes,
                        WriteArcPendingStrike);
                }
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 mode state '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    private static void WriteArcPendingStrike(
        Utf8JsonWriter writer,
        ReplayV3.ArcPendingStrike value)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("shooter");
        WriteActorId(writer, value.Shooter);
        writer.WriteNumber("resolveAtTick", value.ResolveAtTick);
        WriteArray(writer, "tiles", value.Tiles, WritePosition);
        writer.WriteEndObject();
    }

    private static void WriteArcWell(
        Utf8JsonWriter writer,
        ReplayV3.ArcWell value)
    {
        writer.WriteStartObject();
        writer.WriteString("wellId", value.WellId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        WriteNullableNumber(
            writer,
            "nextScheduledBirthTick",
            value.NextScheduledBirthTick);
        writer.WritePropertyName("outstandingCoreId");
        if (value.OutstandingCoreId is null) writer.WriteNullValue();
        else WriteArcCoreId(writer, value.OutstandingCoreId);
        writer.WriteBoolean("pendingCharge", value.PendingCharge);
        WriteNullableNumber(
            writer,
            "rearmCompletesAtTick",
            value.RearmCompletesAtTick);
        writer.WriteEndObject();
    }

    private static void WriteArcReactor(
        Utf8JsonWriter writer,
        ReplayV3.ArcReactor value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", value.TeamId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteNumber("chargePips", value.ChargePips);
        writer.WriteNumber("integritySegments", value.IntegritySegments);
        // Written only for threefold rulesets so historical replay bytes
        // are untouched.
        if (!value.FilledSocketWellIds.IsEmpty)
        {
            writer.WritePropertyName("filledSocketWellIds");
            writer.WriteStartArray();
            foreach (string wellId in value.FilledSocketWellIds)
                writer.WriteStringValue(wellId);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteArcCore(
        Utf8JsonWriter writer,
        ReplayV3.ArcCore value)
    {
        writer.WriteStartObject();
        WriteArcCoreId(writer, "coreId", value.CoreId);
        writer.WritePropertyName("position");
        WritePosition(writer, value.Position);
        writer.WriteString("disposition", value.Disposition);
        writer.WritePropertyName("carrierActorId");
        if (value.CarrierActorId is null) writer.WriteNullValue();
        else WriteActorId(writer, value.CarrierActorId);
        writer.WriteNumber("nextRelocationTick", value.NextRelocationTick);
        writer.WritePropertyName("flightTarget");
        if (value.FlightTarget is null) writer.WriteNullValue();
        else WritePosition(writer, value.FlightTarget);
        WriteNullableNumber(
            writer,
            "flightCompletesAtTick",
            value.FlightCompletesAtTick);
        // Written only under charge-value rulesets so historical replay
        // bytes are untouched.
        if (value.ChargeValue != 1)
            writer.WriteNumber("chargeValue", value.ChargeValue);
        writer.WriteEndObject();
    }

    private static void WriteArcSignature(
        Utf8JsonWriter writer,
        ReplayV3.ArcSignature value)
    {
        writer.WriteStartObject();
        writer.WriteString("operationId", value.OperationId);
        writer.WriteString("signatureId", value.SignatureId);
        writer.WriteString("signatureKind", value.SignatureKind);
        writer.WritePropertyName("ownerActorId");
        WriteActorId(writer, value.OwnerActorId);
        writer.WriteNumber("ownerTeamId", value.OwnerTeamId);
        writer.WriteString("phase", value.Phase);
        writer.WriteNumber("startedTick", value.StartedTick);
        WriteNullableNumber(writer, "completesAtTick", value.CompletesAtTick);
        WriteNullableNumber(writer, "endsAtTick", value.EndsAtTick);
        WriteArray(writer, "positions", value.Positions, WritePosition);
        writer.WritePropertyName("targetActorId");
        if (value.TargetActorId is null) writer.WriteNullValue();
        else WriteActorId(writer, value.TargetActorId);
        writer.WriteNumber("remainingCapacity", value.RemainingCapacity);
        writer.WriteBoolean("suppressed", value.Suppressed);
        writer.WriteEndObject();
    }

    internal static void WriteResult(
        Utf8JsonWriter writer,
        ReplayV3.MatchResult result)
    {
        writer.WriteStartObject();
        writer.WriteString(
            "completionReason",
            result.CompletionReason);
        WriteNullableNumber(writer, "endTick", result.EndTick);
        writer.WritePropertyName("standings");
        WriteStandings(writer, result.Standings);
        WriteNumberArray(
            writer,
            "eligibleTeamIds",
            result.EligibleTeamIds);
        WriteArray(
            writer,
            "units",
            result.Units,
            static (json, unit) =>
            {
                json.WriteStartObject();
                json.WritePropertyName("slot");
                WriteSlotState(json, unit.Slot);
                json.WritePropertyName("activeLife");
                if (unit.ActiveLife is null)
                    json.WriteNullValue();
                else
                    WriteLifeState(json, unit.ActiveLife);
                json.WriteEndObject();
            });
        writer.WritePropertyName("mode");
        WriteModeResult(writer, result.Mode);
        writer.WriteEndObject();
    }

    private static void WriteStandings(
        Utf8JsonWriter writer,
        ReplayV3.Standings value)
    {
        writer.WriteStartObject();
        WriteNullableNumber(
            writer,
            "winnerTeamId",
            value.WinnerTeamId);
        WriteArray(
            writer,
            "teams",
            value.Teams,
            static (json, team) =>
            {
                json.WriteStartObject();
                json.WriteNumber("teamId", team.TeamId);
                json.WriteNumber("rank", team.Rank);
                json.WriteString("outcome", team.Outcome);
                WriteArray(
                    json,
                    "scores",
                    team.Scores,
                    WriteScoreValue);
                json.WriteEndObject();
            });
        writer.WriteEndObject();
    }

    private static void WriteModeResult(
        Utf8JsonWriter writer,
        ReplayV3.ModeResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        switch (value)
        {
            case ReplayV3.ModeResult.Deathmatch deathmatch:
                writer.WriteString("reason", deathmatch.Reason);
                WriteArray(
                    writer,
                    "scores",
                    deathmatch.Scores,
                    static (json, score) =>
                    {
                        json.WriteStartObject();
                        json.WriteNumber("teamId", score.TeamId);
                        WriteInt64String(
                            json,
                            "kills",
                            score.Kills,
                            nonNegative: true);
                        WriteInt64String(
                            json,
                            "deaths",
                            score.Deaths,
                            nonNegative: true);
                        WriteInt64String(
                            json,
                            "damageDealt",
                            score.DamageDealt,
                            nonNegative: true);
                        json.WriteEndObject();
                    });
                break;
            case ReplayV3.ModeResult.Frontline frontline:
                writer.WriteString("reason", frontline.Reason);
                writer.WritePropertyName("control");
                WriteModeState(writer, frontline.Control);
                WriteArray(
                    writer,
                    "scores",
                    frontline.Scores,
                    static (json, score) =>
                    {
                        json.WriteStartObject();
                        json.WriteNumber("teamId", score.TeamId);
                        WriteInt64String(
                            json,
                            "territorialProgress",
                            score.TerritorialProgress,
                            nonNegative: false);
                        json.WriteEndObject();
                    });
                break;
            case ReplayV3.ModeResult.ArcRelay arcRelay:
                writer.WriteString("reason", arcRelay.Reason);
                writer.WritePropertyName("state");
                WriteModeState(writer, arcRelay.State);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported replay-v3 mode result '{value.GetType().Name}'.");
        }
        writer.WriteEndObject();
    }

    internal static void WriteActorId(
        Utf8JsonWriter writer,
        ReplayV3.ActorId value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", value.TeamId);
        writer.WriteNumber("unitId", value.UnitId);
        writer.WriteNumber("lifeId", value.LifeId);
        writer.WriteEndObject();
    }

    private static void WriteNullableActorId(
        Utf8JsonWriter writer,
        ReplayV3.ActorId? value)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            WriteActorId(writer, value);
    }

    internal static void WritePosition(
        Utf8JsonWriter writer,
        ReplayV3.PositionValue value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteEndObject();
    }

    private static void WriteShotProgram(
        Utf8JsonWriter writer,
        ReplayV3.ShotProgramValue value)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "initialAimOffset",
            value.InitialAimOffset);
        writer.WriteNumber(
            "bendDirection",
            value.BendDirection);
        writer.WriteNumber(
            "bendAfterTiles",
            value.BendAfterTiles);
        writer.WriteNumber(
            "bendEveryTiles",
            value.BendEveryTiles);
        writer.WriteNumber("bendCount", value.BendCount);
        writer.WriteEndObject();
    }

    internal static void WriteNullableShotProgram(
        Utf8JsonWriter writer,
        ReplayV3.ShotProgramValue? value)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            WriteShotProgram(writer, value);
    }

    private static void WriteUnitTarget(
        Utf8JsonWriter writer,
        int teamId,
        int unitId)
    {
        writer.WriteStartObject();
        writer.WriteNumber("teamId", teamId);
        writer.WriteNumber("unitId", unitId);
        writer.WriteEndObject();
    }

    private static void WriteArray<T>(
        Utf8JsonWriter writer,
        string propertyName,
        ImmutableArray<T> values,
        Action<Utf8JsonWriter, T> writeItem)
    {
        if (values.IsDefault)
        {
            throw new ArgumentException(
                $"Replay-v3 array '{propertyName}' is uninitialized.");
        }
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (T value in values)
        {
            if (value is null)
            {
                throw new ArgumentException(
                    $"Replay-v3 array '{propertyName}' contains null.");
            }
            writeItem(writer, value);
        }
        writer.WriteEndArray();
    }

    private static void WriteNullableArray<T>(
        Utf8JsonWriter writer,
        string propertyName,
        ImmutableArray<T>? values,
        Action<Utf8JsonWriter, T> writeItem)
    {
        writer.WritePropertyName(propertyName);
        if (values is null)
        {
            writer.WriteNullValue();
            return;
        }
        if (values.Value.IsDefault)
        {
            throw new ArgumentException(
                $"Replay-v3 array '{propertyName}' is uninitialized.");
        }

        writer.WriteStartArray();
        foreach (T value in values.Value)
        {
            if (value is null)
            {
                throw new ArgumentException(
                    $"Replay-v3 array '{propertyName}' contains null.");
            }
            writeItem(writer, value);
        }
        writer.WriteEndArray();
    }

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string propertyName,
        ImmutableArray<string> values) =>
        WriteArray(
            writer,
            propertyName,
            values,
            static (json, value) =>
                json.WriteStringValue(value));

    private static void WriteNumberArray(
        Utf8JsonWriter writer,
        string propertyName,
        ImmutableArray<int> values) =>
        WriteArray(
            writer,
            propertyName,
            values,
            static (json, value) =>
                json.WriteNumberValue(value));

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
        if (value is null)
            writer.WriteNull(propertyName);
        else
            writer.WriteNumber(propertyName, value.Value);
    }

    private static void WriteUInt64String(
        Utf8JsonWriter writer,
        string propertyName,
        string value)
    {
        if (!IsCanonicalUInt64(value))
        {
            throw new ArgumentException(
                $"Replay-v3 '{propertyName}' must be a canonical UInt64 decimal string.");
        }
        writer.WriteString(propertyName, value);
    }

    private static void WriteInt64String(
        Utf8JsonWriter writer,
        string propertyName,
        string value,
        bool nonNegative)
    {
        if (!IsCanonicalInt64(value, nonNegative))
        {
            throw new ArgumentException(
                $"Replay-v3 '{propertyName}' must be a canonical Int64 decimal string.");
        }
        writer.WriteString(propertyName, value);
    }

    private static void WriteNullableInt64String(
        Utf8JsonWriter writer,
        string propertyName,
        string? value,
        bool nonNegative)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
            return;
        }
        WriteInt64String(writer, propertyName, value, nonNegative);
    }

    private static void ValidateEnvelope(ReplayV3 replay)
    {
        ArgumentNullException.ThrowIfNull(replay.Header);
        ArgumentNullException.ThrowIfNull(replay.InitialFrame);
        if (replay.Header.ReplayVersion
            != BotArenaVersions.GenericActorReplayFormatVersion)
        {
            throw new ArgumentException(
                "Replay-v3 header must carry replay version 3.",
                nameof(replay));
        }
        ValidatePresentation(replay.Header.Presentation);
        if (replay.Ticks.IsDefault)
        {
            throw new ArgumentException(
                "Replay-v3 ticks must be initialized.",
                nameof(replay));
        }
        if (replay.Partial != (replay.Result is null))
        {
            throw new ArgumentException(
                "Replay-v3 partial must be true exactly when result is null.",
                nameof(replay));
        }
        if (replay.Partial && replay.ReplayHash is not null)
        {
            throw new ArgumentException(
                "A partial replay-v3 document cannot carry a hash.",
                nameof(replay));
        }
        for (int index = 0; index < replay.Ticks.Length; index++)
        {
            if (replay.Ticks[index] is null
                || replay.Ticks[index].Tick != index)
            {
                throw new ArgumentException(
                    "Replay-v3 ticks must be non-null and contiguous from zero.",
                    nameof(replay));
            }
        }

        CanonicalContractIndex contract =
            ValidateContractAndHeader(replay.Header);
        ValidateCanonicalReplay(replay, contract);
        ValidateClosedVocabulary(replay);
    }

    private static CanonicalContractIndex ValidateContractAndHeader(
        ReplayV3.ReplayHeader header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header.EngineVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            header.GameRulesVersion);
        ArgumentNullException.ThrowIfNull(header.Runtime);
        ArgumentNullException.ThrowIfNull(header.Contract);
        if (!IsCanonicalUInt64(header.Seed))
        {
            throw new ArgumentException(
                "Replay-v3 header seed must be a canonical unsigned decimal string.");
        }

        ReplayV3.ResolvedContract contract = header.Contract;
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contract.CanonicalJson);
        using JsonDocument document = JsonDocument.Parse(
            contract.CanonicalJson,
            DocumentOptions);
        JsonElement root = document.RootElement;
        RequireExactObject(
            root,
            "embedded match contract",
            ContractRootProperties);
        GenericActorCanonicalContractValidation strictContract =
            GenericActorCanonicalContractValidator.Validate(
                contract.CanonicalJson);

        int schemaVersion = RequiredInt32(root, "schemaVersion");
        string fingerprint = RequiredString(
            root,
            "matchContractFingerprint");
        if (schemaVersion
                != BotArenaVersions.GenericActorMatchContractSchemaVersion
            || contract.SchemaVersion != schemaVersion)
        {
            throw new ArgumentException(
                "Replay-v3 embedded match-contract schema version is not the supported generation.");
        }
        if (strictContract.SchemaVersion != schemaVersion)
        {
            throw new ArgumentException(
                "Replay-v3 strict contract schema validation disagrees with the embedded metadata.");
        }
        if (!IsLowercaseSha256(fingerprint)
            || !string.Equals(
                contract.MatchContractFingerprint,
                fingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                strictContract.MatchContractFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay-v3 embedded match-contract fingerprint metadata is invalid.");
        }

        JsonElement capabilities = RequiredObject(
            root,
            "capabilityVersions");
        RequireExactObject(
            capabilities,
            "embedded capability versions",
            CapabilityProperties);
        ValidateCapabilityVersions(header.Runtime, capabilities);

        JsonElement rules = RequiredObject(root, "rules");
        if (!string.Equals(
                RequiredString(rules, "rulesetId"),
                header.GameRulesVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                strictContract.RulesetId,
                header.GameRulesVersion,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay-v3 gameRulesVersion must equal the embedded ruleset id.");
        }
        VerifyEmbeddedFingerprint(
            rules,
            RequiredString(rules, "rulesFingerprint"),
            "rules",
            "rulesetId",
            "rulesFingerprint");
        JsonElement map = RequiredObject(root, "map");
        VerifyEmbeddedFingerprint(
            map,
            RequiredString(map, "mapFingerprint"),
            "map",
            "mapId",
            "mapVersion",
            "mapFingerprint");
        JsonElement format = RequiredObject(root, "format");
        VerifyEmbeddedFingerprint(
            format,
            RequiredString(format, "formatFingerprint"),
            "format",
            "formatId",
            "formatFingerprint");
        JsonElement topology = RequiredObject(root, "topology");
        VerifyEmbeddedFingerprint(
            topology,
            RequiredString(
                topology,
                "topologyFingerprint"),
            "topology",
            "topologyFingerprint");
        VerifyEmbeddedFingerprint(
            root,
            fingerprint,
            "match contract",
            "matchContractFingerprint");

        ImmutableArray<ContractAction> actions =
            ReadContractActions(rules);
        ImmutableArray<ContractAttackProfile> attackProfiles =
            ReadContractAttackProfiles(rules);
        ImmutableArray<ContractReplication> replications =
            ReadContractReplications(rules);
        ImmutableArray<string> scoreChannels =
            ReadScoreChannels(rules);
        (
            ImmutableArray<int> teamIds,
            ImmutableArray<ContractParticipant> participants,
            ImmutableArray<ContractUnitSlot> unitSlots) =
            ReadContractTopology(topology);
        ContractMode mode = ReadContractMode(
            rules,
            RequiredObject(root, "modeMapBinding"),
            teamIds);
        ImmutableArray<ContractPermanentReservation>
            permanentReservations =
                ReadPermanentSpawnReservations(root);

        return new CanonicalContractIndex(
            fingerprint,
            RequiredString(
                RequiredObject(rules, "seedMechanics"),
                "seedProfileId"),
            teamIds,
            participants,
            unitSlots,
            scoreChannels,
            actions,
            attackProfiles,
            replications,
            permanentReservations,
            mode);
    }

    private static void VerifyEmbeddedFingerprint(
        JsonElement value,
        string supplied,
        string context,
        params string[] excludedProperties)
    {
        if (!IsLowercaseSha256(supplied))
        {
            throw new ArgumentException(
                $"Replay-v3 embedded {context} fingerprint is invalid.");
        }
        HashSet<string> excluded = excludedProperties.ToHashSet(
            StringComparer.Ordinal);
        byte[] payload = Write(writer =>
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (excluded.Contains(property.Name))
                    continue;
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        });
        string computed = Convert.ToHexStringLower(
            SHA256.HashData(payload));
        if (!string.Equals(
                supplied,
                computed,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 embedded {context} fingerprint does not match its payload.");
        }
    }

    private static void ValidateCapabilityVersions(
        ReplayV3.RuntimeVersions runtime,
        JsonElement capabilities)
    {
        if (!string.Equals(
                RequiredString(
                    capabilities,
                    "contractProfileId"),
                runtime.ContractProfileId,
                StringComparison.Ordinal)
            || !string.Equals(
                RequiredString(
                    capabilities,
                    "runtimeProtocolVersion"),
                runtime.ProtocolVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                RequiredString(
                    capabilities,
                    "runtimeConfigurationVersion"),
                runtime.ConfigurationVersion,
                StringComparison.Ordinal)
            || RequiredInt32(
                capabilities,
                "runtimeContractVersion")
                != runtime.RuntimeContractVersion
            || RequiredInt32(
                capabilities,
                "matchStartSchemaVersion")
                != runtime.MatchStartSchemaVersion
            || RequiredInt32(
                capabilities,
                "observationSchemaVersion")
                != runtime.ObservationSchemaVersion
            || RequiredInt32(
                capabilities,
                "decisionSchemaVersion")
                != runtime.DecisionSchemaVersion
            || RequiredInt32(
                capabilities,
                "matchContractSchemaVersion")
                != runtime.MatchContractSchemaVersion)
        {
            throw new ArgumentException(
                "Replay-v3 runtime versions must exactly equal the embedded contract capability versions.");
        }
        if (runtime.MatchContractSchemaVersion
                != BotArenaVersions.GenericActorMatchContractSchemaVersion
            || runtime.RuntimeContractVersion <= 0
            || runtime.MatchStartSchemaVersion <= 0
            || runtime.ObservationSchemaVersion <= 0
            || runtime.DecisionSchemaVersion <= 0
            || string.IsNullOrWhiteSpace(runtime.ContractProfileId)
            || string.IsNullOrWhiteSpace(runtime.ProtocolVersion)
            || string.IsNullOrWhiteSpace(
                runtime.ConfigurationVersion))
        {
            throw new ArgumentException(
                "Replay-v3 runtime capability metadata is invalid.");
        }
    }

    private static ImmutableArray<ContractAction> ReadContractActions(
        JsonElement rules)
    {
        HashSet<string> optionalAttackActions =
            ReadOptionalAttackActionIds(rules);
        JsonElement values = RequiredArray(rules, "actions");
        var result = ImmutableArray.CreateBuilder<ContractAction>(
            values.GetArrayLength());
        foreach (JsonElement value in values.EnumerateArray())
        {
            bool hasMovementFacingOverride = value.TryGetProperty(
                "movementFacingOverride",
                out JsonElement movementFacingOverride);
            RequireExactObject(
                value,
                "embedded action",
                hasMovementFacingOverride
                    ? ["id", "code", "kind", "parameterKinds",
                        "movementFacingOverride"]
                    : ["id", "code", "kind", "parameterKinds"]);
            string id = RequiredString(value, "id");
            int code = RequiredInt32(value, "code");
            string kind = RequiredString(value, "kind");
            if (hasMovementFacingOverride)
            {
                string facing = movementFacingOverride.ValueKind
                    == JsonValueKind.String
                    ? movementFacingOverride.GetString()!
                    : throw new ArgumentException(
                        "Embedded movement-facing override must be a string.");
                if (!string.Equals(kind, "movement", StringComparison.Ordinal)
                    || facing is not ("preserve-facing"
                        or "face-movement-direction"
                        or "facing-locked"
                        or "face-movement-heading-projected"
                        or "combat-strafe"))
                {
                    throw new ArgumentException(
                        "Embedded movement-facing override is invalid.");
                }
            }
            JsonElement parameterValues = RequiredArray(
                value,
                "parameterKinds");
            ImmutableArray<string> parameters = parameterValues
                .EnumerateArray()
                .Select(item =>
                {
                    if (item.ValueKind != JsonValueKind.String
                        || item.GetString() is not { } parameter
                        || string.IsNullOrWhiteSpace(parameter))
                    {
                        throw new ArgumentException(
                            "Embedded action parameter kinds must be nonblank strings.");
                    }
                    return parameter;
                })
                .ToImmutableArray();
            result.Add(
                new ContractAction(
                    id,
                    code,
                    parameters,
                    string.Equals(
                        kind,
                        "attack",
                        StringComparison.Ordinal)
                    && optionalAttackActions.Contains(id)));
        }

        ImmutableArray<ContractAction> actions = result.ToImmutable();
        RequireCanonicalOrder(
            actions,
            static (left, right) =>
                left.Code.CompareTo(right.Code),
            "embedded actions by action code");
        return actions;
    }

    private static ImmutableArray<ContractAttackProfile>
        ReadContractAttackProfiles(JsonElement rules)
    {
        ImmutableArray<ContractAttackProfile> profiles =
            RequiredArray(rules, "attackProfiles")
                .EnumerateArray()
                .Select(value =>
                {
                    JsonElement projectile =
                        RequiredObject(value, "projectile");
                    return new ContractAttackProfile(
                        RequiredString(value, "id"),
                        RequiredInt32(
                            projectile,
                            "tilesPerAdvance"),
                        RequiredInt32(
                            projectile,
                            "ticksPerAdvance"),
                        RequiredInt32(
                            projectile,
                            "damagePerHit"));
                })
                .ToImmutableArray();
        RequireCanonicalOrder(
            profiles,
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.Id, right.Id),
            "embedded attack profiles");
        return profiles;
    }

    private static ImmutableArray<ContractPermanentReservation>
        ReadPermanentSpawnReservations(JsonElement root)
    {
        JsonElement rules = RequiredObject(root, "rules");
        JsonElement lifecycle = RequiredObject(rules, "lifecycle");
        HashSet<string> automaticProfileIds =
            RequiredArray(lifecycle, "profiles")
                .EnumerateArray()
                .Where(profile => string.Equals(
                    RequiredString(profile, "destructionPolicy"),
                    "automatic-respawn",
                    StringComparison.Ordinal))
                .Select(profile =>
                    RequiredString(profile, "profileId"))
                .ToHashSet(StringComparer.Ordinal);
        JsonElement map = RequiredObject(
            root,
            "map");
        Dictionary<string, ReplayV3.PositionValue> positions =
            RequiredArray(map, "spawnAnchors")
                .EnumerateArray()
                .ToDictionary(
                    value => RequiredString(value, "spawnId"),
                    value => ContractPosition(
                        value.GetProperty("position")),
                    StringComparer.Ordinal);
        var reservations =
            ImmutableArray.CreateBuilder<ContractPermanentReservation>();
        foreach (JsonElement value in RequiredArray(
                     root,
                     "lifecycleAssignments").EnumerateArray())
        {
            if (!automaticProfileIds.Contains(
                    RequiredString(value, "lifecycleProfileId")))
            {
                continue;
            }
            JsonElement assigned =
                value.GetProperty("assignedRespawnSpawnId");
            if (assigned.ValueKind == JsonValueKind.Null)
                continue;
            if (assigned.ValueKind != JsonValueKind.String
                || assigned.GetString() is not { } spawnId
                || !positions.TryGetValue(
                    spawnId,
                    out ReplayV3.PositionValue? position))
            {
                throw new ArgumentException(
                    "Replay-v3 lifecycle reservation references an unknown spawn.");
            }
            reservations.Add(
                new ContractPermanentReservation(
                    RequiredInt32(value, "teamId"),
                    RequiredInt32(value, "unitId"),
                    position));
        }
        return reservations
            .OrderBy(value => value.TeamId)
            .ThenBy(value => value.UnitId)
            .ToImmutableArray();
    }

    private static ReplayV3.PositionValue ContractPosition(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array
            || value.GetArrayLength() != 2
            || !value[0].TryGetInt32(out int x)
            || !value[1].TryGetInt32(out int y))
        {
            throw new ArgumentException(
                "Replay-v3 embedded contract position is invalid.");
        }
        return new ReplayV3.PositionValue(x, y);
    }

    private static HashSet<string> ReadOptionalAttackActionIds(
        JsonElement rules)
    {
        var optionalProfiles = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (JsonElement profile in RequiredArray(
                     rules,
                     "attackProfiles").EnumerateArray())
        {
            JsonElement shotProgram = RequiredObject(
                profile,
                "shotProgram");
            JsonElement optional = shotProgram.GetProperty(
                "payloadOptional");
            if (optional.ValueKind is not (
                    JsonValueKind.True or JsonValueKind.False))
            {
                throw new ArgumentException(
                    "Embedded attack-profile payloadOptional must be boolean.");
            }
            if (optional.GetBoolean())
            {
                optionalProfiles.Add(RequiredString(profile, "id"));
            }
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement form in RequiredArray(
                     rules,
                     "forms").EnumerateArray())
        {
            JsonElement attackProfile = form.GetProperty(
                "attackProfileId");
            if (attackProfile.ValueKind != JsonValueKind.String
                || attackProfile.GetString() is not { } profileId
                || !optionalProfiles.Contains(profileId))
            {
                continue;
            }
            foreach (JsonElement actionId in RequiredArray(
                         form,
                         "allowedActionIds").EnumerateArray())
            {
                if (actionId.ValueKind != JsonValueKind.String
                    || actionId.GetString() is not { } id)
                {
                    throw new ArgumentException(
                        "Embedded form action ids must be strings.");
                }
                result.Add(id);
            }
        }
        return result;
    }

    private static ImmutableArray<string> ReadScoreChannels(
        JsonElement rules)
    {
        JsonElement gameMode = RequiredObject(rules, "gameMode");
        JsonElement catalog = RequiredArray(gameMode, "scoreCatalog");
        var result = ImmutableArray.CreateBuilder<string>(
            catalog.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in catalog.EnumerateArray())
        {
            RequireExactObject(
                value,
                "embedded score channel",
                "channel",
                "domain");
            string channel = RequiredString(value, "channel");
            _ = RequiredString(value, "domain");
            if (!seen.Add(channel))
            {
                throw new ArgumentException(
                    "Embedded score channels must be unique.");
            }
            result.Add(channel);
        }
        return result.ToImmutable();
    }

    private static ContractMode ReadContractMode(
        JsonElement rules,
        JsonElement modeMapBinding,
        ImmutableArray<int> teamIds)
    {
        JsonElement gameMode = RequiredObject(rules, "gameMode");
        string kind = RequiredString(gameMode, "kind");
        string modeId = RequiredString(gameMode, "modeId");
        int maxTicks = RequiredInt32(
            RequiredObject(rules, "limits"),
            "maxTicks");
        if (string.Equals(kind, "frontline", StringComparison.Ordinal))
        {
            RequireExactObject(
                modeMapBinding,
                "embedded Frontline mode-map binding",
                "kind",
                "orderedObjectiveRegionIds",
                "teamAdvances");
            if (!string.Equals(
                    RequiredString(modeMapBinding, "kind"),
                    "frontline",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Embedded Frontline mode-map binding kind is invalid.");
            }

            JsonElement capture = RequiredObject(gameMode, "capture");
            int positionCount = RequiredInt32(
                gameMode,
                "frontlinePositionCount");
            int threshold = RequiredInt32(capture, "threshold");
            int decayAmount = RequiredInt32(capture, "decayAmount");
            int decayIntervalTicks = RequiredInt32(
                capture,
                "decayIntervalTicks");
            int redeployPauseTicks = RequiredInt32(
                capture,
                "redeployPauseTicks");
            // The hold duration is inert-omitted, and it is carried by
            // exactly the high-water-mark redeploy policy (DECISIONS #160).
            // The observed hold clocks are validated against both, so the
            // index has to carry both.
            bool ratchet = string.Equals(
                RequiredString(capture, "redeployPolicy"),
                RatchetRedeployPolicy,
                StringComparison.Ordinal);
            int ratchetHoldTicks =
                capture.TryGetProperty(
                    "ratchetHoldTicks",
                    out JsonElement holdTicks)
                    ? holdTicks.ValueKind == JsonValueKind.Number
                        && holdTicks.TryGetInt32(out int holdValue)
                        ? holdValue
                        : throw new ArgumentException(
                            "Embedded Frontline ratchetHoldTicks must be an Int32.")
                    : 0;
            if (ratchet != ratchetHoldTicks > 0)
            {
                throw new ArgumentException(
                    "Embedded Frontline hold duration is carried by exactly the high-water-mark redeploy policy.");
            }
            // The side objective is inert-omitted too, so its presence is
            // itself a contract fact: a published owner or claim on a mode
            // that declares no secondary control is a forged observation.
            bool secondaryControl = gameMode.TryGetProperty(
                "secondaryControl",
                out JsonElement secondary);
            int secondaryThresholdTicks = secondaryControl
                ? RequiredInt32(secondary, "captureThresholdTicks")
                : 0;
            if (secondaryControl && secondaryThresholdTicks <= 0)
            {
                throw new ArgumentException(
                    "Embedded Frontline secondary-control threshold must be positive.");
            }
            ImmutableArray<ContractTeamAdvance> teamAdvances =
                RequiredArray(modeMapBinding, "teamAdvances")
                    .EnumerateArray()
                    .Select(value =>
                    {
                        RequireExactObject(
                            value,
                            "embedded Frontline team advance",
                            "teamId",
                            "direction",
                            "objectiveIndexDelta");
                        string direction = RequiredString(
                            value,
                            "direction");
                        int expectedDelta = direction switch
                        {
                            "toward-lower-index" => -1,
                            "toward-higher-index" => 1,
                            _ => throw new ArgumentException(
                                "Embedded Frontline team advance direction is invalid."),
                        };
                        int delta = RequiredInt32(
                            value,
                            "objectiveIndexDelta");
                        if (delta != expectedDelta)
                        {
                            throw new ArgumentException(
                                "Embedded Frontline team advance direction and delta disagree.");
                        }
                        return new ContractTeamAdvance(
                            RequiredInt32(value, "teamId"),
                            delta);
                    })
                    .ToImmutableArray();
            RequireCanonicalOrder(
                teamAdvances,
                static (left, right) =>
                    left.TeamId.CompareTo(right.TeamId),
                "embedded Frontline team advances");
            if (!teamAdvances.Select(value => value.TeamId)
                    .SequenceEqual(teamIds)
                || teamAdvances.Length != 2
                || teamAdvances.Sum(value =>
                    value.ObjectiveIndexDelta) != 0
                || positionCount < 3
                || positionCount % 2 == 0
                || threshold <= 0
                || redeployPauseTicks < 0
                || !((decayAmount == 0 && decayIntervalTicks == 0)
                     || (decayAmount > 0
                         && decayIntervalTicks > 0)))
            {
                throw new ArgumentException(
                    "Embedded Frontline mode configuration is invalid.");
            }
            if (RequiredArray(
                    modeMapBinding,
                    "orderedObjectiveRegionIds").GetArrayLength()
                != positionCount)
            {
                throw new ArgumentException(
                    "Embedded Frontline position and objective-region counts disagree.");
            }

            return new ContractMode(
                kind,
                modeId,
                maxTicks,
                KillsToWin: null,
                [],
                new ContractFrontline(
                    positionCount,
                    threshold,
                    decayAmount,
                    decayIntervalTicks,
                    redeployPauseTicks,
                    teamAdvances,
                    ratchet,
                    ratchetHoldTicks,
                    secondaryControl,
                    secondaryThresholdTicks),
                ArcRelay: null);
        }
        if (string.Equals(kind, "arc-relay", StringComparison.Ordinal))
        {
            RequireExactObject(
                modeMapBinding,
                "embedded Arc Relay mode-map binding",
                "kind",
                "orderedWellRegionIds",
                "reactorRegionRoleId",
                "homePadRegionRoleId");
            if (!string.Equals(
                    RequiredString(modeMapBinding, "kind"),
                    "arc-relay",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Embedded Arc Relay mode-map binding kind is invalid.");
            }
            JsonElement arcVictory = RequiredObject(gameMode, "victory");
            int pulsesToDestroy = RequiredInt32(
                arcVictory,
                "pulsesToDestroyReactor");
            int coresPerPulse = RequiredInt32(gameMode, "coresPerPulse");
            int relocationTicks = RequiredInt32(
                gameMode,
                "coreRelocationIntervalTicks");
            JsonElement tripNode = RequiredArray(gameMode, "signatures")
                .EnumerateArray()
                .Single(value => string.Equals(
                    RequiredString(value, "kind"),
                    "trip-node",
                    StringComparison.Ordinal));
            int tripNodeRevealRange = RequiredInt32(
                tripNode,
                "revealRange");
            JsonElement wells = RequiredArray(gameMode, "wells");
            JsonElement wellRegions = RequiredArray(
                modeMapBinding,
                "orderedWellRegionIds");
            if (pulsesToDestroy <= 0
                || coresPerPulse <= 0
                || relocationTicks <= 0
                || tripNodeRevealRange < 0
                || wells.GetArrayLength() == 0
                || wells.GetArrayLength() != wellRegions.GetArrayLength())
            {
                throw new ArgumentException(
                    "Embedded Arc Relay mode configuration is invalid.");
            }
            ImmutableArray<ContractRanking> arcRankings = RequiredArray(
                    arcVictory,
                    "timeoutRanking")
                .EnumerateArray()
                .Select(value => new ContractRanking(
                    RequiredString(value, "channel"),
                    RequiredString(value, "direction")))
                .ToImmutableArray();
            return new ContractMode(
                kind,
                modeId,
                maxTicks,
                KillsToWin: null,
                arcRankings,
                Frontline: null,
                new ContractArcRelay(
                    pulsesToDestroy,
                    coresPerPulse,
                    relocationTicks,
                    tripNodeRevealRange,
                    wells.EnumerateArray()
                        .Select(value => RequiredString(value, "wellId"))
                        .ToImmutableArray()));
        }
        if (!string.Equals(kind, "deathmatch", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 embedded game mode '{kind}' is unsupported.");
        }

        RequireExactObject(
            modeMapBinding,
            "embedded Deathmatch mode-map binding",
            "kind");
        if (!string.Equals(
                RequiredString(modeMapBinding, "kind"),
                "deathmatch",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Embedded Deathmatch mode-map binding kind is invalid.");
        }
        JsonElement victory = RequiredObject(gameMode, "victory");
        JsonElement killsToWinValue = victory.GetProperty("killsToWin");
        int? killsToWin = killsToWinValue.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Number
                when killsToWinValue.TryGetInt32(out int value) => value,
            _ => throw new ArgumentException(
                "Embedded Deathmatch killsToWin must be an integer or null."),
        };
        ImmutableArray<ContractRanking> rankings = RequiredArray(
                victory,
                "timeoutRanking")
            .EnumerateArray()
            .Select(value =>
            {
                RequireExactObject(
                    value,
                    "embedded timeout ranking",
                    "channel",
                    "direction");
                string direction = RequiredString(
                    value,
                    "direction");
                if (direction is not ("higher-wins" or "lower-wins"))
                {
                    throw new ArgumentException(
                        "Embedded timeout ranking direction is invalid.");
                }
                return new ContractRanking(
                    RequiredString(value, "channel"),
                    direction);
            })
            .ToImmutableArray();
        return new ContractMode(
            kind,
            modeId,
            maxTicks,
            killsToWin,
            rankings,
            Frontline: null,
            ArcRelay: null);
    }

    private static ImmutableArray<ContractReplication>
        ReadContractReplications(JsonElement rules)
    {
        JsonElement values = RequiredArray(
            rules,
            "replicationTransitions");
        var result = ImmutableArray.CreateBuilder<ContractReplication>(
            values.GetArrayLength());
        foreach (JsonElement value in values.EnumerateArray())
        {
            string kind = RequiredString(value, "kind");
            if (!string.Equals(
                    kind,
                    "split",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Replay-v3 embedded replication kind '{kind}' is unsupported.");
            }
            ImmutableArray<string> sourceFormIds = RequiredArray(
                    value,
                    "sourceFormIds")
                .EnumerateArray()
                .Select(item =>
                {
                    if (item.ValueKind != JsonValueKind.String
                        || item.GetString() is not { } formId)
                    {
                        throw new ArgumentException(
                            "Embedded Split source forms must be strings.");
                    }
                    return formId;
                })
                .ToImmutableArray();
            JsonElement offsetValues = RequiredArray(
                value,
                "candidateOffsets");
            ImmutableArray<ContractOffset> offsets = offsetValues
                .EnumerateArray()
                .Select(item =>
                {
                    RequireExactObject(
                        item,
                        "embedded Split offset",
                        "forward",
                        "right");
                    return new ContractOffset(
                        RequiredInt32(item, "forward"),
                        RequiredInt32(item, "right"));
                })
                .ToImmutableArray();
            JsonElement windup = RequiredObject(value, "windup");
            result.Add(
                new ContractReplication(
                    RequiredString(value, "transitionId"),
                    sourceFormIds,
                    RequiredString(value, "outputFormId"),
                    RequiredInt32(value, "descendantCount"),
                    RequiredInt32(value, "maxSourceGeneration"),
                    RequiredInt32(windup, "durationTicks"),
                    offsets));
        }
        ImmutableArray<ContractReplication> transitions =
            result.ToImmutable();
        RequireCanonicalOrder(
            transitions,
            static (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.TransitionId,
                    right.TransitionId),
            "embedded replication transitions");
        return transitions;
    }

    private static (
        ImmutableArray<int> TeamIds,
        ImmutableArray<ContractParticipant> Participants,
        ImmutableArray<ContractUnitSlot> UnitSlots)
        ReadContractTopology(JsonElement topology)
    {
        ImmutableArray<ContractTeam> teams = RequiredArray(
                topology,
                "teams")
            .EnumerateArray()
            .Select(value =>
            {
                bool hasClassId = value.TryGetProperty(
                    "classId",
                    out _);
                RequireExactObject(
                    value,
                    "embedded topology team",
                    hasClassId
                        ? ["teamId", "classId"]
                        : ["teamId"]);
                return new ContractTeam(
                    RequiredInt32(value, "teamId"),
                    hasClassId
                        ? RequiredString(value, "classId")
                        : null);
            })
            .ToImmutableArray();
        ImmutableArray<int> teamIds =
            teams.Select(team => team.TeamId).ToImmutableArray();
        RequireCanonicalOrder(
            teams,
            static (left, right) =>
                left.TeamId.CompareTo(right.TeamId),
            "embedded topology teams");

        ImmutableArray<ContractParticipant> participants =
            RequiredArray(topology, "participants")
                .EnumerateArray()
                .Select(value =>
                {
                    bool hasClassId = value.TryGetProperty(
                        "classId",
                        out _);
                    RequireExactObject(
                        value,
                        "embedded topology participant",
                        hasClassId
                            ? ["participantId", "teamId", "classId"]
                            : ["participantId", "teamId"]);
                    return new ContractParticipant(
                        RequiredInt32(value, "participantId"),
                        RequiredInt32(value, "teamId"),
                        hasClassId
                            ? RequiredString(value, "classId")
                            : null);
                })
                .ToImmutableArray();
        RequireCanonicalOrder(
            participants,
            static (left, right) =>
                left.ParticipantId.CompareTo(right.ParticipantId),
            "embedded topology participants");
        Dictionary<int, string?> teamClasses =
            teams.ToDictionary(team => team.TeamId, team => team.ClassId);
        if (participants.Any(participant =>
                !teamClasses.TryGetValue(
                    participant.TeamId,
                    out string? classId)
                || !string.Equals(
                    participant.ClassId,
                    classId,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Replay-v3 participant class IDs must exactly match their scoring teams.");
        }

        ImmutableArray<ContractUnitSlot> unitSlots =
            RequiredArray(topology, "unitSlots")
                .EnumerateArray()
                .Select(value =>
                {
                    // Per-slot chassis, the same additive-canonical shape the
                    // team and participant above already carry: present only
                    // where a ruleset declares COMPOSITIONS, so a
                    // composition-free document keeps its exact bytes.
                    bool hasSlotClassId = value.TryGetProperty(
                        "classId",
                        out _);
                    RequireExactObject(
                        value,
                        "embedded topology unit slot",
                        hasSlotClassId
                            ?
                            [
                                "teamId",
                                "unitId",
                                "controllerParticipantId",
                                "classId",
                            ]
                            :
                            [
                                "teamId",
                                "unitId",
                                "controllerParticipantId",
                            ]);
                    return new ContractUnitSlot(
                        RequiredInt32(value, "teamId"),
                        RequiredInt32(value, "unitId"),
                        RequiredInt32(
                            value,
                            "controllerParticipantId"),
                        hasSlotClassId
                            ? RequiredString(value, "classId")
                            : null);
                })
                .ToImmutableArray();
        RequireCanonicalOrder(
            unitSlots,
            static (left, right) =>
                CompareUnitKey(
                    left.TeamId,
                    left.UnitId,
                    right.TeamId,
                    right.UnitId),
            "embedded topology unit slots");
        return (teamIds, participants, unitSlots);
    }

    private static void ValidateCanonicalReplay(
        ReplayV3 replay,
        CanonicalContractIndex contract)
    {
        if (replay.Ticks.Length > contract.Mode.MaxTicks)
        {
            throw new ArgumentException(
                "Replay-v3 ticks cannot extend beyond the embedded maximum tick boundary.");
        }
        ValidateProvenance(replay.Header.Provenance, contract);
        ValidateInitialFrame(
            replay.InitialFrame,
            replay.Header,
            contract);

        var factOrdinals = new List<long>();
        var eventSourceOrdinals = new Dictionary<int, List<int>>();
        AppendFactPhase(
            replay.InitialFrame.Events,
            [],
            factOrdinals,
            eventSourceOrdinals,
            "initial frame");

        bool mindProfile = IsMindProfile(replay.Header);
        // Every life's seed, accumulated as the document declares it, so a
        // mind body publishing a seed the engine never derived is refused
        // without the validator having to re-run the derivation itself.
        var seedsByActor = new Dictionary<ReplayV3.ActorId, string>();
        // The last tag each mind actually set, re-derived from the accepted
        // commands, so a doctored document cannot narrate a strategy that
        // never happened (§5.3).
        var roleTags = new Dictionary<ReplayV3.ActorId, string>();
        foreach (ReplayV3.LifeStart start in replay.InitialFrame.LifeStarts)
            seedsByActor[start.ActorId] = start.ActorRandomSeed;

        ReplayV3.WorldState previousState = replay.InitialFrame.State;
        for (int index = 0; index < replay.Ticks.Length; index++)
        {
            ReplayV3.TickFrame tick = replay.Ticks[index];
            ArgumentNullException.ThrowIfNull(tick.TickStart);
            ArgumentNullException.ThrowIfNull(tick.PostState);
            if (tick.TickStart.Tick != tick.Tick
                || tick.TickStart.State.NextTick != tick.Tick
                || tick.PostState.NextTick != checked(tick.Tick + 1))
            {
                throw new ArgumentException(
                    "Replay-v3 tick boundaries must identify their exact pre- and post-tick states.");
            }

            ValidateWorldState(
                tick.TickStart.State,
                contract,
                $"tick {tick.Tick} start state");
            ValidateWorldState(
                tick.PostState,
                contract,
                $"tick {tick.Tick} post state");
            ValidateActorIds(
                tick.TickStart.ActiveActorIds,
                $"tick {tick.Tick} active actor ids");
            if (!tick.TickStart.ActiveActorIds.SequenceEqual(
                    tick.TickStart.State.ActiveLives.Select(
                        life => life.ActorId)))
            {
                throw new ArgumentException(
                    $"Replay-v3 tick {tick.Tick} active actor ids must exactly equal its active lives.");
            }

            ValidateLifeStarts(
                tick.TickStart.LifeStarts,
                replay.Header,
                contract,
                $"tick {tick.Tick} life starts");
            ValidateEvents(
                tick.TickStart.Events,
                tick.Tick,
                $"tick {tick.Tick} start events");
            ValidateTraversals(
                tick.TickStart.Traversals,
                tick.Tick,
                $"tick {tick.Tick} start traversals");
            AppendFactPhase(
                tick.TickStart.Events,
                tick.TickStart.Traversals,
                factOrdinals,
                eventSourceOrdinals,
                $"tick {tick.Tick} start");
            ValidateArcRelayChronologyPhase(
                previousState,
                tick.TickStart.State,
                tick.TickStart.Events,
                tick.Tick,
                "tick start");

            foreach (ReplayV3.LifeStart start in tick.TickStart.LifeStarts)
                seedsByActor[start.ActorId] = start.ActorRandomSeed;

            // The profile decides the turn kind, and a document may never
            // carry both (§5.1).
            if (mindProfile != tick.ActorTurns.IsDefault
                || mindProfile == tick.MindTurns.IsDefault)
            {
                throw new ArgumentException(
                    $"Replay-v3 tick {tick.Tick} must carry exactly the turn kind its contract profile selects.");
            }
            if (!mindProfile)
            {
                RequireCanonicalOrder(
                    tick.ActorTurns,
                    static (left, right) =>
                        CompareActorId(left.ActorId, right.ActorId),
                    $"tick {tick.Tick} actor turns");
                if (!tick.ActorTurns.Select(turn => turn.ActorId)
                    .SequenceEqual(tick.TickStart.ActiveActorIds))
                {
                    throw new ArgumentException(
                        $"Replay-v3 tick {tick.Tick} turns must exactly cover active actors.");
                }
            }
            Dictionary<ReplayV3.ActorId, ReplayV3.LifeState> lives =
                tick.TickStart.State.ActiveLives.ToDictionary(
                    life => life.ActorId);
            if (mindProfile)
            {
                ValidateMindTurns(
                    tick,
                    replay.Header,
                    contract,
                    lives,
                    seedsByActor,
                    roleTags);
            }
            foreach (ReplayV3.ActorTurn turn in
                     tick.ActorTurns.IsDefault ? [] : tick.ActorTurns)
            {
                ArgumentNullException.ThrowIfNull(turn);
                ArgumentNullException.ThrowIfNull(turn.Observation);
                ArgumentNullException.ThrowIfNull(
                    turn.ActionResolution);
                if (turn.Tick != tick.Tick
                    || turn.Observation.Tick != tick.Tick
                    || turn.Observation.Self.ActorId != turn.ActorId
                    || !lives.TryGetValue(
                        turn.ActorId,
                        out ReplayV3.LifeState? life)
                    || turn.ParticipantId != life.ParticipantId
                    || turn.Observation.Self.Generation
                        != life.Generation
                    || !string.Equals(
                        turn.Observation.Self.FormId,
                        life.FormId,
                        StringComparison.Ordinal)
                    || turn.Observation.Self.Position
                        != life.Position
                    || !string.Equals(
                        turn.Observation.Self.Facing,
                        life.Facing,
                        StringComparison.Ordinal)
                    || turn.Observation.Self.Health != life.Health
                    || turn.Observation.Self.Cooldown != life.Cooldown
                    || turn.Observation.Self.Energy != life.Energy)
                {
                    throw new ArgumentException(
                        $"Replay-v3 tick {tick.Tick} actor turn does not match its authoritative pre-state.");
                }
                ValidateObservation(
                    turn.Observation,
                    replay.Header,
                    contract,
                    $"tick {tick.Tick} actor {turn.ActorId}");
                ValidateObservationAgainstState(
                    turn.Observation,
                    tick.TickStart.State,
                    contract,
                    $"tick {tick.Tick} actor {turn.ActorId}");
                ValidateActionResolution(
                    turn.ActionResolution,
                    contract,
                    $"tick {tick.Tick} actor {turn.ActorId} resolution");
            }

            ValidateEvents(
                tick.Events,
                tick.Tick,
                $"tick {tick.Tick} resolution events");
            ValidateTraversals(
                tick.Traversals,
                tick.Tick,
                $"tick {tick.Tick} resolution traversals");
            AppendFactPhase(
                tick.Events,
                tick.Traversals,
                factOrdinals,
                eventSourceOrdinals,
                $"tick {tick.Tick} resolution");
            ValidateArcRelayChronologyPhase(
                tick.TickStart.State,
                tick.PostState,
                tick.Events,
                tick.Tick,
                "resolution");
            previousState = tick.PostState;
        }

        for (int index = 0; index < factOrdinals.Count; index++)
        {
            if (factOrdinals[index] != index)
            {
                throw new ArgumentException(
                    "Replay-v3 authoritative fact ordinals must be globally contiguous from zero in phase order.");
            }
        }
        foreach ((int tick, List<int> ordinals) in eventSourceOrdinals)
        {
            for (int index = 0; index < ordinals.Count; index++)
            {
                if (ordinals[index] != index)
                {
                    throw new ArgumentException(
                        $"Replay-v3 event source ordinals at tick {tick} must be contiguous from zero.");
                }
            }
        }

        ValidateResult(replay.Result, replay.Ticks, previousState, contract);
    }

    /// <summary>
    /// Replays the Core-owned part of one Arc Relay phase from its closed fact
    /// ledger. This is intentionally a replay validation, not merely a state
    /// shape check: a forged handoff, pickup, drop, bank, Well transition, or
    /// signature transition must fail even after the payload hash is rebuilt.
    /// </summary>
    private static void ValidateArcRelayChronologyPhase(
        ReplayV3.WorldState beforeWorld,
        ReplayV3.WorldState afterWorld,
        ImmutableArray<ReplayV3.AuthoritativeEvent> events,
        int tick,
        string phase)
    {
        if (beforeWorld.Mode is not ReplayV3.ModeState.ArcRelay before
            || afterWorld.Mode is not ReplayV3.ModeState.ArcRelay after)
        {
            return;
        }
        ReplayV3.ArcRelayFact[] facts = events
            .Select(value => value.Payload)
            .OfType<ReplayV3.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .ToArray();
        string context = $"Replay-v3 tick {tick} {phase} Arc Relay";

        var cores = before.VisibleCores.ToDictionary(value => value.CoreId);
        var opaqueFlights = new HashSet<ReplayV3.ArcCoreId>();
        foreach (ReplayV3.ArcRelayFact fact in facts)
        {
            switch (fact)
            {
                case ReplayV3.ArcRelayFact.CoreBorn value:
                    if (!cores.TryAdd(
                            value.CoreId,
                            new ReplayV3.ArcCore(
                                value.CoreId,
                                value.Position,
                                "loose",
                                null,
                                0,
                                null,
                                null)
                            {
                                ChargeValue = value.ChargeValue,
                            }))
                    {
                        throw new ArgumentException(
                            $"{context} births an already-live Core.");
                    }
                    break;
                case ReplayV3.ArcRelayFact.CoreRipened value:
                    ReplayV3.ArcCore ripened = RequireCore(
                        cores,
                        value.CoreId,
                        context);
                    if (ripened.CarrierActorId is not null
                        || !string.Equals(
                            ripened.Disposition,
                            "loose",
                            StringComparison.Ordinal)
                        || ripened.Position != value.Position
                        || value.Value != ripened.ChargeValue + 1)
                    {
                        throw new ArgumentException(
                            $"{context} ripens a Core outside the loose +1 progression.");
                    }
                    cores[value.CoreId] = ripened with
                    {
                        ChargeValue = value.Value,
                    };
                    break;
                case ReplayV3.ArcRelayFact.CorePickedUp value:
                    ReplayV3.ArcCore pickedUp = RequireCore(
                        cores,
                        value.CoreId,
                        context);
                    cores[value.CoreId] = pickedUp with
                    {
                        Position = value.Position,
                        Disposition = "carried",
                        CarrierActorId = value.CarrierActorId,
                        NextRelocationTick = value.NextRelocationTick,
                        FlightTarget = null,
                        FlightCompletesAtTick = null,
                    };
                    opaqueFlights.Remove(value.CoreId);
                    break;
                case ReplayV3.ArcRelayFact.CoreRelocated value:
                    RequireCore(cores, value.CoreId, context);
                    ReplayV3.ArcCore prior = cores[value.CoreId];
                    if (prior.Position != value.From)
                    {
                        throw new ArgumentException(
                            $"{context} relocates a Core from a forged position.");
                    }
                    cores[value.CoreId] = prior with
                    {
                        Position = value.To,
                        Disposition =
                            value.CarrierActorId is null ? "loose" : "carried",
                        CarrierActorId = value.CarrierActorId,
                        NextRelocationTick = value.NextRelocationTick,
                        FlightTarget = null,
                        FlightCompletesAtTick = null,
                    };
                    opaqueFlights.Remove(value.CoreId);
                    break;
                case ReplayV3.ArcRelayFact.CoreHandedOff value:
                    ReplayV3.ArcCore handed = RequireCore(
                        cores,
                        value.CoreId,
                        context);
                    if (handed.CarrierActorId != value.SourceActorId)
                    {
                        throw new ArgumentException(
                            $"{context} hands off a Core from a non-carrier.");
                    }
                    cores[value.CoreId] = handed with
                    {
                        Position = value.Position,
                        Disposition = "carried",
                        CarrierActorId = value.TargetActorId,
                        NextRelocationTick = value.NextRelocationTick,
                        FlightTarget = null,
                        FlightCompletesAtTick = null,
                    };
                    break;
                case ReplayV3.ArcRelayFact.CoreDropped value:
                    ReplayV3.ArcCore dropped = RequireCore(
                        cores,
                        value.CoreId,
                        context);
                    if (!string.Equals(
                            value.DropKind,
                            "arc-toss-landing",
                            StringComparison.Ordinal)
                        && dropped.CarrierActorId != value.SourceActorId)
                    {
                        throw new ArgumentException(
                            $"{context} drops a Core from a non-carrier.");
                    }
                    if (string.Equals(
                            value.DropKind,
                            "signature-departure",
                            StringComparison.Ordinal)
                        && facts.OfType<ReplayV3.ArcRelayFact
                                .SignatureChanged>()
                            .Any(changed => string.Equals(
                                    changed.SignatureId,
                                    "arc-toss",
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    changed.Reason,
                                    "launched",
                                    StringComparison.Ordinal)
                                && changed.OwnerActorId
                                    == value.SourceActorId))
                    {
                        // The public drop fact deliberately does not duplicate
                        // the Arc Toss target/arrival clock. The paired
                        // signature fact carries those; final state must still
                        // prove the Core entered flight.
                        opaqueFlights.Add(value.CoreId);
                    }
                    else
                    {
                        cores[value.CoreId] = dropped with
                        {
                            Position = value.Position,
                            Disposition = "loose",
                            CarrierActorId = null,
                            NextRelocationTick = value.NextRelocationTick,
                            FlightTarget = null,
                            FlightCompletesAtTick = null,
                        };
                    }
                    break;
                case ReplayV3.ArcRelayFact.CoreBanked value:
                    ReplayV3.ArcCore banked = RequireCore(
                        cores,
                        value.CoreId,
                        context);
                    if (banked.CarrierActorId != value.CarrierActorId
                        || banked.Position != value.Position
                        || !cores.Remove(value.CoreId))
                    {
                        throw new ArgumentException(
                            $"{context} banks a Core from a forged carrier or position.");
                    }
                    opaqueFlights.Remove(value.CoreId);
                    break;
            }
        }

        Dictionary<ReplayV3.ArcCoreId, ReplayV3.ArcCore> finalCores =
            after.VisibleCores.ToDictionary(value => value.CoreId);
        if (!cores.Keys.ToHashSet().SetEquals(finalCores.Keys))
        {
            throw new ArgumentException(
                $"{context} Core births and banks do not produce the final live-Core set.");
        }
        foreach ((ReplayV3.ArcCoreId id, ReplayV3.ArcCore expected) in cores)
        {
            ReplayV3.ArcCore actual = finalCores[id];
            if (opaqueFlights.Contains(id))
            {
                if (!string.Equals(
                        actual.Disposition,
                        "in-flight",
                        StringComparison.Ordinal)
                    || actual.CarrierActorId is not null
                    || actual.FlightTarget is null
                    || actual.FlightCompletesAtTick is null
                    || !facts.OfType<ReplayV3.ArcRelayFact.SignatureChanged>()
                        .Any(value => string.Equals(
                                value.SignatureId,
                                "arc-toss",
                                StringComparison.Ordinal)
                            && string.Equals(
                                value.Reason,
                                "launched",
                                StringComparison.Ordinal)))
                {
                    throw new ArgumentException(
                        $"{context} Arc Toss departure lacks matching in-flight state and launch fact.");
                }
            }
            else if (actual != expected)
            {
                throw new ArgumentException(
                    $"{context} Core facts do not produce the final Core state.");
            }
        }

        Dictionary<string, ReplayV3.ArcRelayFact.WellChanged> wellFacts = facts
            .OfType<ReplayV3.ArcRelayFact.WellChanged>()
            .GroupBy(value => value.WellId, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.Last(),
                StringComparer.Ordinal);
        foreach ((ReplayV3.ArcWell oldWell, ReplayV3.ArcWell newWell) in
                 before.Wells.Zip(after.Wells))
        {
            bool ledgerChanged = oldWell.PendingCharge != newWell.PendingCharge
                || oldWell.RearmCompletesAtTick
                    != newWell.RearmCompletesAtTick
                || oldWell.OutstandingCoreId != newWell.OutstandingCoreId;
            if (ledgerChanged
                && (!wellFacts.TryGetValue(
                        newWell.WellId,
                        out ReplayV3.ArcRelayFact.WellChanged? changed)
                    || changed.PendingCharge != newWell.PendingCharge
                    || changed.RearmCompletesAtTick
                        != newWell.RearmCompletesAtTick
                    || changed.OutstandingCoreId
                        != newWell.OutstandingCoreId))
            {
                throw new ArgumentException(
                    $"{context} Well ledger changed without an exact WellChanged fact.");
            }
        }

        bool reactorsChanged = !before.Reactors.SequenceEqual(after.Reactors);
        if (reactorsChanged
            && !facts.Any(value => value is
                ReplayV3.ArcRelayFact.CoreBanked
                or ReplayV3.ArcRelayFact.Pulse))
        {
            throw new ArgumentException(
                $"{context} reactor state changed without a bank or Pulse fact.");
        }
        if (before.LatestPulseTeamId != after.LatestPulseTeamId
            || before.LatestPulseTick != after.LatestPulseTick)
        {
            ReplayV3.ArcRelayFact.Pulse? pulse = facts
                .OfType<ReplayV3.ArcRelayFact.Pulse>()
                .LastOrDefault();
            if (pulse is null
                || after.LatestPulseTeamId != pulse.TeamId
                || after.LatestPulseTick != tick)
            {
                throw new ArgumentException(
                    $"{context} latest Pulse state lacks its exact Pulse fact.");
            }
        }

        var beforeSignatures = before.VisibleSignatures.ToDictionary(
            value => value.OperationId,
            StringComparer.Ordinal);
        var afterSignatures = after.VisibleSignatures.ToDictionary(
            value => value.OperationId,
            StringComparer.Ordinal);
        HashSet<string> changedOperations = beforeSignatures.Keys
            .Concat(afterSignatures.Keys)
            .Where(id => !beforeSignatures.TryGetValue(id, out var oldValue)
                || !afterSignatures.TryGetValue(id, out var newValue)
                || oldValue != newValue)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> evidencedOperations = facts
            .OfType<ReplayV3.ArcRelayFact.SignatureChanged>()
            .Select(value => value.OperationId)
            .ToHashSet(StringComparer.Ordinal);
        bool nullFieldChanged = facts
            .OfType<ReplayV3.ArcRelayFact.SignatureChanged>()
            .Any(value => string.Equals(
                value.SignatureId,
                "null-field",
                StringComparison.Ordinal));
        changedOperations.RemoveWhere(id =>
            nullFieldChanged
            && beforeSignatures.TryGetValue(id, out var oldValue)
            && afterSignatures.TryGetValue(id, out var newValue)
            && oldValue with { Suppressed = newValue.Suppressed } == newValue);
        if (!changedOperations.IsSubsetOf(evidencedOperations))
        {
            throw new ArgumentException(
                $"{context} signature state changed without SignatureChanged evidence.");
        }
    }

    private static ReplayV3.ArcCore RequireCore(
        IReadOnlyDictionary<ReplayV3.ArcCoreId, ReplayV3.ArcCore> cores,
        ReplayV3.ArcCoreId coreId,
        string context)
    {
        if (!cores.TryGetValue(coreId, out ReplayV3.ArcCore? core))
        {
            throw new ArgumentException(
                $"{context} references a Core that is not live.");
        }
        return core;
    }

    /// <summary>
    /// The mind-era document rules
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.3). It is
    /// strictly LESS work than the per-life pass and a STRONGER check: one
    /// union is re-derived per participant per tick instead of N
    /// specializations of it, and the facts the per-life format only implied —
    /// which bodies a mind owned, what budget it was handed, which label each
    /// body was actually given — become explicit and checkable.
    /// <para>
    /// The refusals added here, each forgeable and each forged in the tests:
    /// a turn for a non-ticking or unowned participant; a decision claimed
    /// accepted on a body that is not an own live body; two commands for one
    /// body on a healthy turn; a fuel budget off the live-body formula; a
    /// resolution set that is not exactly the participant's own live bodies;
    /// an observation that disagrees with the re-derived pre-state; a role tag
    /// over the 24-byte cap, off the canonical charset, or on a body its mind
    /// never tagged; and a body random seed that is not the one the document
    /// itself declared at that life's start.
    /// </para>
    /// </summary>
    private static void ValidateMindTurns(
        ReplayV3.TickFrame tick,
        ReplayV3.ReplayHeader header,
        CanonicalContractIndex contract,
        IReadOnlyDictionary<ReplayV3.ActorId, ReplayV3.LifeState> lives,
        IReadOnlyDictionary<ReplayV3.ActorId, string> seedsByActor,
        Dictionary<ReplayV3.ActorId, string> roleTags)
    {
        RequireCanonicalOrder(
            tick.MindTurns,
            static (left, right) =>
                left.ParticipantId.CompareTo(right.ParticipantId),
            $"tick {tick.Tick} mind turns");
        if (tick.MindTurns.Select(turn => turn.ParticipantId)
                .Distinct()
                .Count() != tick.MindTurns.Length)
        {
            throw new ArgumentException(
                $"Replay-v3 tick {tick.Tick} mind turns must be participant-unique.");
        }

        var covered = new List<ReplayV3.ActorId>();
        foreach (ReplayV3.MindTurn turn in tick.MindTurns)
        {
            ArgumentNullException.ThrowIfNull(turn);
            ArgumentNullException.ThrowIfNull(turn.Observation);
            string context =
                $"tick {tick.Tick} mind {turn.ParticipantId}";
            ContractParticipant? participant =
                contract.Participants.FirstOrDefault(value =>
                    value.ParticipantId == turn.ParticipantId);
            if (participant is null || participant.TeamId != turn.TeamId)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} names a participant the contract does not place on that team.");
            }
            if (turn.Tick != tick.Tick
                || turn.Observation.Tick != tick.Tick
                || turn.Observation.ParticipantId != turn.ParticipantId
                || turn.Observation.TeamId != turn.TeamId)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} does not identify its own tick and participant.");
            }
            if (turn.LiveBodyCount < 0
                || !IsCanonicalInt64(turn.FuelBudget, nonNegative: true)
                || long.Parse(
                        turn.FuelBudget,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture)
                    != checked(
                        GenericMindTickBudget.BaseTickFuel
                        + (GenericMindTickBudget.PerBodyTickFuel
                            * turn.LiveBodyCount)))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} fuel budget must be exactly the live-body formula.");
            }

            ReplayV3.ActorId[] expectedBodies =
            [
                .. tick.TickStart.State.ActiveLives
                    .Where(life =>
                        life.ParticipantId == turn.ParticipantId)
                    .Select(life => life.ActorId),
            ];
            RequireInitialized(turn.Resolutions, $"{context} resolutions");
            RequireInitialized(turn.Commands, $"{context} commands");
            RequireInitialized(turn.Intents, $"{context} intents");
            if (turn.Resolutions.Length != expectedBodies.Length
                || turn.LiveBodyCount != expectedBodies.Length
                || turn.Observation.Bodies.Length != expectedBodies.Length)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} resolutions must cover exactly its own live bodies.");
            }
            for (int index = 0; index < expectedBodies.Length; index++)
            {
                ReplayV3.MindBodyResolution resolution =
                    turn.Resolutions[index];
                ArgumentNullException.ThrowIfNull(resolution);
                ReplayV3.ActorId expected = expectedBodies[index];
                if (resolution.UnitId != expected.UnitId
                    || resolution.LifeId != expected.LifeId)
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} resolutions must be its own live bodies in canonical order.");
                }
                ValidateActionResolution(
                    resolution.ActionResolution,
                    contract,
                    $"{context} body {expected.UnitId}:{expected.LifeId} resolution");
            }
            covered.AddRange(expectedBodies);

            ValidateMindRuntimeFault(turn, context);
            ValidateMindCommands(turn, expectedBodies, context);
            ValidateMindObservation(
                turn,
                tick,
                header,
                contract,
                lives,
                seedsByActor,
                roleTags,
                context);
        }

        if (!covered.Order(ActorIdOrder.Instance)
            .SequenceEqual(tick.TickStart.ActiveActorIds))
        {
            throw new ArgumentException(
                $"Replay-v3 tick {tick.Tick} mind turns must resolve exactly the active actor set exactly once.");
        }

        // Tags set this tick are what the NEXT tick publishes: the observation
        // the mind just answered was frozen before any of them were written.
        foreach (ReplayV3.MindTurn turn in tick.MindTurns)
        {
            if (turn.RuntimeFault is not null)
                continue;
            foreach (ReplayV3.MindCommand command in turn.Commands)
            {
                if (!string.Equals(
                        command.Outcome,
                        "accepted",
                        StringComparison.Ordinal)
                    || command.RoleTag is null)
                {
                    continue;
                }
                var actorId = new ReplayV3.ActorId(
                    turn.TeamId,
                    command.UnitId,
                    command.LifeId);
                if (command.RoleTag.Length == 0)
                    roleTags.Remove(actorId);
                else
                    roleTags[actorId] = command.RoleTag;
            }
        }
        HashSet<ReplayV3.ActorId> live = tick.PostState.ActiveLives
            .Select(life => life.ActorId)
            .ToHashSet();
        foreach (ReplayV3.ActorId dead in
                 roleTags.Keys.Where(actor => !live.Contains(actor))
                     .ToArray())
        {
            roleTags.Remove(dead);
        }
    }

    private static void ValidateMindRuntimeFault(
        ReplayV3.MindTurn turn,
        string context)
    {
        ReplayV3.MindRuntimeFault? fault = turn.RuntimeFault;
        if (fault is null)
            return;
        if (fault.ParticipantId != turn.ParticipantId
            || fault.TeamId != turn.TeamId
            || (fault.ActorId is not null
                && fault.ActorId.TeamId != turn.TeamId)
            || !IsFaultStage(fault.Stage)
            || string.IsNullOrWhiteSpace(fault.FaultCode)
            || !IsCanonicalInt64(
                fault.CumulativeFaultCount,
                nonNegative: true)
            || long.Parse(
                    fault.CumulativeFaultCount,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture) <= 0)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} runtime fault evidence does not match its turn.");
        }
    }

    private static void ValidateMindCommands(
        ReplayV3.MindTurn turn,
        IReadOnlyCollection<ReplayV3.ActorId> ownLiveBodies,
        string context)
    {
        bool faulted = turn.RuntimeFault is not null;
        var keys = new HashSet<(int UnitId, int LifeId)>();
        var live = ownLiveBodies
            .Select(body => (body.UnitId, body.LifeId))
            .ToHashSet();
        foreach (ReplayV3.MindCommand command in turn.Commands)
        {
            ArgumentNullException.ThrowIfNull(command);
            bool accepted = command.Outcome switch
            {
                "accepted" => true,
                "rejected" => false,
                _ => throw new ArgumentException(
                    $"Replay-v3 {context} contains an invalid mind command outcome."),
            };
            // A duplicate is only ever legitimate on the faulted turn the
            // duplicate itself caused, where nothing was routed and the raw
            // submission is preserved verbatim as evidence.
            if (!keys.Add((command.UnitId, command.LifeId)) && !faulted)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} cannot command the same body twice.");
            }
            if (command.UnitId < 0
                || command.LifeId < 0
                || string.IsNullOrWhiteSpace(command.ActionId)
                || command.ActionCode < 0
                || command.Arguments.IsDefault)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains a malformed mind command.");
            }
            if (command.RoleTag is not null
                && !GenericMindRoleTag.IsValid(command.RoleTag))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains a role tag outside the canonical charset or the 24-byte cap.");
            }
            if (faulted && accepted)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} cannot record an accepted command on a faulted turn.");
            }
            if (!faulted
                && accepted != live.Contains(
                    (command.UnitId, command.LifeId)))
            {
                throw new ArgumentException(
                    accepted
                        ? $"Replay-v3 {context} accepted a command on a body that is not an own live body."
                        : $"Replay-v3 {context} rejected a command on one of its own live bodies.");
            }
        }
    }

    private static void ValidateMindObservation(
        ReplayV3.MindTurn turn,
        ReplayV3.TickFrame tick,
        ReplayV3.ReplayHeader header,
        CanonicalContractIndex contract,
        IReadOnlyDictionary<ReplayV3.ActorId, ReplayV3.LifeState> lives,
        IReadOnlyDictionary<ReplayV3.ActorId, string> seedsByActor,
        IReadOnlyDictionary<ReplayV3.ActorId, string> roleTags,
        string context)
    {
        ReplayV3.MindObservation observation = turn.Observation;
        if (observation.SchemaVersion
                != header.Runtime.ObservationSchemaVersion
            || !string.Equals(
                observation.MatchContractFingerprint,
                contract.Fingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} observation does not reference the document's exact contract generation.");
        }
        if (!observation.AlliedIntents.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} allied intents are reserved and must be empty.");
        }
        if (turn.Intents.Length
            > GenericMindContractReservations.MaxDeclaredIntentsPerTick)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} declared more intents than the reserved bound.");
        }
        foreach (ReplayV3.MindIntent intent in turn.Intents)
        {
            if (intent is null
                || string.IsNullOrEmpty(intent.TagId)
                || Encoding.UTF8.GetByteCount(intent.TagId)
                    > GenericMindContractReservations.MaxIntentTagUtf8Bytes
                || !IsCanonicalInt64(intent.Value, nonNegative: false))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains a malformed declared intent.");
            }
        }

        // BODIES: the participant's own live lives, in canonical order, each
        // publishing the exact authoritative pre-tick state.
        RequireCanonicalOrder(
            observation.Bodies,
            static (left, right) =>
                CompareActorId(left.ActorId, right.ActorId),
            $"{context} bodies");
        foreach (ReplayV3.MindBody body in observation.Bodies)
        {
            ArgumentNullException.ThrowIfNull(body);
            if (!lives.TryGetValue(
                    body.ActorId,
                    out ReplayV3.LifeState? life)
                || life.ParticipantId != turn.ParticipantId
                || body.Generation != life.Generation
                || !string.Equals(
                    body.FormId,
                    life.FormId,
                    StringComparison.Ordinal)
                || body.Position != life.Position
                || !string.Equals(
                    body.Facing,
                    life.Facing,
                    StringComparison.Ordinal)
                || body.Health != life.Health
                || body.Cooldown != life.Cooldown
                || body.Energy != life.Energy
                || body.LifeStartedTick != life.SpawnedAtTick)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} body does not match its authoritative pre-state.");
            }
            if (!seedsByActor.TryGetValue(
                    body.ActorId,
                    out string? seed)
                || !string.Equals(
                    body.BodyRandomSeed,
                    seed,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} body random seed is not the seed the document declared at that life's start.");
            }
            RequirePublishedRoleTag(
                body.RoleTag,
                body.ActorId,
                roleTags,
                context);
            ValidateActionLegalities(
                body.ActionLegalities,
                contract,
                $"{context} body {body.ActorId.UnitId}");
        }

        // SLOTS: exactly the participant's own slots, in canonical order,
        // published every tick rather than only at start (§13.2).
        int[] ownSlots =
        [
            .. contract.UnitSlots
                .Where(slot => slot.ParticipantId == turn.ParticipantId)
                .Select(slot => slot.UnitId)
                .Order(),
        ];
        if (!observation.Slots
            .Select(slot => slot.UnitId)
            .SequenceEqual(ownSlots)
            || observation.Slots.Any(slot => slot.TeamId != turn.TeamId))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} slot table must be exactly its own slots in canonical order.");
        }
        foreach (ReplayV3.MindSlot slot in observation.Slots)
        {
            ValidateUnitSlotState(
                slot.State,
                $"{context} slot {slot.UnitId}");
            ReplayV3.SlotState authoritative =
                tick.TickStart.State.Slots.Single(value =>
                    value.TeamId == slot.TeamId
                    && value.UnitId == slot.UnitId);
            if (slot.State != authoritative.State)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} slot {slot.UnitId} does not match its authoritative pre-state.");
            }
            if (!slot.CandidateClassIds.IsDefaultOrEmpty
                || slot.SelectedClassId is not null)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} slot {slot.UnitId} carries a reserved chassis selection that v1 never writes.");
            }
        }

        // ALLIES are allied MINDS' bodies: on this team, never this mind's own.
        foreach (ReplayV3.ObservedAlly ally in observation.Allies)
        {
            if (ally.ActorId.TeamId != turn.TeamId
                || lives.TryGetValue(
                        ally.ActorId,
                        out ReplayV3.LifeState? allyLife)
                    && allyLife.ParticipantId == turn.ParticipantId)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} allies must be allied minds' bodies, never its own.");
            }
            RequirePublishedRoleTag(
                ally.RoleTag,
                ally.ActorId,
                roleTags,
                context);
        }
        foreach (ReplayV3.ObservedEnemy enemy in observation.Enemies)
        {
            RequirePublishedRoleTag(
                enemy.RoleTag,
                enemy.ActorId,
                roleTags,
                context);
        }

        ValidateMindObservationAgainstState(
            observation,
            tick.TickStart.State,
            contract,
            context);
    }

    private static void RequirePublishedRoleTag(
        string? published,
        ReplayV3.ActorId actorId,
        IReadOnlyDictionary<ReplayV3.ActorId, string> roleTags,
        string context)
    {
        if (published is not null && !GenericMindRoleTag.IsValid(published))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} publishes a role tag outside the canonical charset or the 24-byte cap.");
        }
        roleTags.TryGetValue(actorId, out string? expected);
        if (!string.Equals(published, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} publishes a role tag its mind never set on that body.");
        }
    }

    /// <summary>
    /// The union half, re-derived against the authoritative pre-state. It is
    /// the per-life check with the per-life specialization removed: the union
    /// was always the interesting invariant, and now there is one of it per
    /// team per tick instead of N byte-identical copies (§5.3).
    /// </summary>
    private static void ValidateMindObservationAgainstState(
        ReplayV3.MindObservation observation,
        ReplayV3.WorldState state,
        CanonicalContractIndex contract,
        string context)
    {
        if (!ModeObservationMatches(
                observation.Mode,
                state.Mode,
                observation.TeamId,
                observation.VisibleTiles.Select(value => value.Position),
                state.ActiveLives
                    .Where(value => value.ActorId.TeamId == observation.TeamId)
                    .Select(value => value.Position),
                contract))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} observed mode must exactly match the authoritative pre-state.");
        }
        // Shape only, exactly as the per-life pass does. The scoreboard a mind
        // observes is the MODE's projection of the score catalog, which is a
        // different object from the world snapshot's ledger even when the two
        // agree; asserting identity here would be asserting an implementation
        // detail rather than a fact about the match.
        ValidateScoreboard(
            observation.Scoreboard,
            contract,
            $"{context} scoreboard");
        if (!observation.Participants.SequenceEqual(state.Participants))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} observed participant statuses must exactly match the authoritative pre-state.");
        }
        foreach (ReplayV3.ObservedTile tile in observation.VisibleTiles)
        {
            ReplayV3.SpawnReservation? expected = SpawnReservationAt(
                tile.Position,
                state,
                contract);
            if (tile.SpawnReservation != expected)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} visible spawn reservation does not match the authoritative pre-state.");
            }
        }
        if (observation.VisibleProjectiles is not { } projectiles)
            return;
        HashSet<ReplayV3.ActorId> visibleEnemies = observation.Enemies
            .Select(enemy => enemy.ActorId)
            .ToHashSet();
        foreach (ReplayV3.ObservedProjectile observed in projectiles)
        {
            ReplayV3.ProjectileState? authoritative =
                state.Projectiles.FirstOrDefault(value =>
                    string.Equals(
                        value.ProjectileId,
                        observed.ProjectileId,
                        StringComparison.Ordinal));
            ContractAttackProfile? profile = authoritative is null
                ? null
                : contract.AttackProfiles.FirstOrDefault(value =>
                    string.Equals(
                        value.Id,
                        authoritative.AttackProfileId,
                        StringComparison.Ordinal));
            ReplayV3.ActorId? expectedOwnerActorId =
                authoritative is not null
                    && (authoritative.OwnerTeamId == observation.TeamId
                        || visibleEnemies.Contains(
                            authoritative.OwnerActorId))
                    ? authoritative.OwnerActorId
                    : null;
            if (authoritative is null
                || profile is null
                || observed.OwnerTeamId != authoritative.OwnerTeamId
                || observed.OwnerActorId != expectedOwnerActorId
                || observed.Position != authoritative.Position
                || !string.Equals(
                    observed.Heading,
                    authoritative.Heading,
                    StringComparison.Ordinal)
                || observed.TilesPerAdvance != profile.TilesPerAdvance
                || observed.TicksPerAdvance != profile.TicksPerAdvance
                || observed.TicksUntilAdvance
                    != authoritative.TicksUntilAdvance
                || observed.RemainingTiles != authoritative.RemainingTiles
                || observed.DamagePerHit != profile.DamagePerHit)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} visible projectile does not match the authoritative pre-state and attack profile.");
            }
        }
    }

    private sealed class ActorIdOrder : IComparer<ReplayV3.ActorId>
    {
        public static ActorIdOrder Instance { get; } = new();

        public int Compare(ReplayV3.ActorId? x, ReplayV3.ActorId? y) =>
            x is null || y is null ? 0 : CompareActorId(x, y);
    }

    private static void ValidateProvenance(
        ReplayV3.ProvenanceMetadata? provenance,
        CanonicalContractIndex contract)
    {
        if (provenance is null)
            return;
        RequireCanonicalOrder(
            provenance.Participants,
            static (left, right) =>
                left.ParticipantId.CompareTo(right.ParticipantId),
            "provenance participants");
        if (!provenance.Participants
                .Select(value => (value.ParticipantId, value.TeamId))
                .SequenceEqual(contract.Participants.Select(
                    value => (value.ParticipantId, value.TeamId))))
        {
            throw new ArgumentException(
                "Replay-v3 provenance participants must exactly match match topology.");
        }
        foreach (ReplayV3.ParticipantProvenance participant in
                 provenance.Participants)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(participant.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(
                participant.RuntimeKind);
            ArgumentException.ThrowIfNullOrWhiteSpace(participant.Accent);
            if (participant.MindDataHash is { } mindDataHash
                && !IsLowercaseSha256(mindDataHash))
            {
                throw new ArgumentException(
                    "Replay-v3 mind-data hashes must be lowercase SHA-256 values.");
            }
        }
    }

    private static void ValidateInitialFrame(
        ReplayV3.ReplayInitialFrame frame,
        ReplayV3.ReplayHeader header,
        CanonicalContractIndex contract)
    {
        ArgumentNullException.ThrowIfNull(frame.State);
        if (frame.State.NextTick != 0)
        {
            throw new ArgumentException(
                "Replay-v3 initial state must precede tick zero.");
        }
        ValidateWorldState(frame.State, contract, "initial state");
        ValidateLifeStarts(
            frame.LifeStarts,
            header,
            contract,
            "initial life starts");
        ValidateEvents(frame.Events, 0, "initial events");
    }

    private static void ValidateWorldState(
        ReplayV3.WorldState state,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.Equals(
                state.MatchContractFingerprint,
                contract.Fingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} does not reference the header contract.");
        }
        if (state.NextTick < 0
            || !IsCanonicalInt64(
                state.NextProjectileId,
                nonNegative: true))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} counters are invalid.");
        }

        RequireCanonicalOrder(
            state.Participants,
            static (left, right) =>
                left.ParticipantId.CompareTo(right.ParticipantId),
            $"{context} participants");
        if (!state.Participants
                .Select(value =>
                    new ContractParticipant(
                        value.ParticipantId,
                        value.TeamId,
                        value.ClassId))
                .SequenceEqual(contract.Participants))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} participants must exactly match topology.");
        }

        RequireCanonicalOrder(
            state.Slots,
            static (left, right) =>
                CompareUnitKey(
                    left.TeamId,
                    left.UnitId,
                    right.TeamId,
                    right.UnitId),
            $"{context} slots");
        // The world state carries a slot's IDENTITY, never its chassis: a
        // slot's declared chassis is a contract fact, published once in the
        // topology, so comparing the identity triple is what "matches
        // topology" means under compositions.
        if (!state.Slots
                .Select(value =>
                    (value.TeamId, value.UnitId, value.ParticipantId))
                .SequenceEqual(
                    contract.UnitSlots.Select(value =>
                        (value.TeamId, value.UnitId, value.ParticipantId))))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} slots must exactly match topology.");
        }
        foreach (ReplayV3.SlotState slot in state.Slots)
        {
            ArgumentNullException.ThrowIfNull(slot);
            ArgumentNullException.ThrowIfNull(slot.State);
            ValidateUnitSlotState(slot.State, $"{context} slot state");
            if (slot.SplitReservation is { } reservation)
            {
                ValidatePendingReplication(
                    reservation,
                    contract,
                    $"{context} slot split reservation");
            }
        }

        RequireCanonicalOrder(
            state.ActiveLives,
            static (left, right) =>
                CompareActorId(left.ActorId, right.ActorId),
            $"{context} active lives");
        foreach (ReplayV3.LifeState life in state.ActiveLives)
        {
            ValidateLifeState(life, contract, $"{context} active life");
        }

        RequireCanonicalOrder(
            state.PendingReplications,
            static (left, right) =>
            {
                int actor = CompareActorId(
                    left.SourceActorId,
                    right.SourceActorId);
                if (actor != 0)
                    return actor;
                int transition = StringComparer.Ordinal.Compare(
                    left.TransitionId,
                    right.TransitionId);
                return transition != 0
                    ? transition
                    : StringComparer.Ordinal.Compare(
                        left.OperationId,
                        right.OperationId);
            },
            $"{context} pending replications");
        foreach (ReplayV3.PendingReplication replication in
                 state.PendingReplications)
        {
            ValidatePendingReplication(
                replication,
                contract,
                $"{context} pending replication");
        }
        ValidatePendingReplicationClaims(state, contract, context);

        RequireCanonicalOrder(
            state.Projectiles,
            static (left, right) =>
                ParseNonNegativeInt64(left.ProjectileId)
                    .CompareTo(
                        ParseNonNegativeInt64(
                            right.ProjectileId)),
            $"{context} projectiles");
        foreach (ReplayV3.ProjectileState projectile in
                 state.Projectiles)
        {
            ArgumentNullException.ThrowIfNull(projectile);
            if (!IsCanonicalInt64(
                    projectile.ProjectileId,
                    nonNegative: true)
                || projectile.CommittedPath.IsDefault
                || !IsProjectileHeading(projectile.LaunchHeading)
                || !IsProjectileHeading(projectile.Heading))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} projectile data is invalid.");
            }
        }

        ValidateScoreboard(
            state.Scoreboard,
            contract,
            $"{context} scoreboard");
        ValidateModeState(
            state.Mode,
            state.NextTick,
            contract,
            context);
    }

    private static void ValidateLifeState(
        ReplayV3.LifeState life,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(life);
        if (!contract.UnitSlots.Any(slot =>
                slot.TeamId == life.ActorId.TeamId
                && slot.UnitId == life.ActorId.UnitId
                && slot.ParticipantId == life.ParticipantId))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} does not belong to a topology slot.");
        }
        if (!IsDirection(life.Facing)
            || !IsSpawnReason(life.SpawnReason))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} contains an invalid facing or spawn reason.");
        }
        if (life.PreviousActionResolution is { } previous)
        {
            ValidateActionResolution(
                previous,
                contract,
                $"{context} previous action resolution");
        }
    }

    private static void ValidatePendingReplication(
        ReplayV3.PendingReplication value,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(value);
        ContractReplication? transition =
            contract.Replications.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.TransitionId,
                    value.TransitionId,
                    StringComparison.Ordinal));
        if (transition is null
            || value.Descendants.IsDefault
            || value.Descendants.Length != transition.DescendantCount
            || value.ParticipantId < 0
            || value.SourceGeneration < 0
            || value.SourceGeneration > transition.MaxSourceGeneration
            || !transition.SourceFormIds.Contains(
                value.SourceFormId,
                StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(value.OperationId)
            || value.QueuedTick < 0
            || (long)value.QueuedTick + transition.DurationTicks
                != value.DueTick
            || !IsDirection(value.SourceFacing)
            || value.Descendants.IsEmpty
            || value.Descendants[0].TeamId
                != value.SourceActorId.TeamId
            || value.Descendants[0].UnitId
                != value.SourceActorId.UnitId)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} does not match its Split transition.");
        }
        var claimedSlots = new HashSet<(int TeamId, int UnitId)>();
        var claimedPositions = new HashSet<(int X, int Y)>();
        int previousCandidateIndex = -1;
        int previousAdditionalUnitId = -1;
        foreach (ReplayV3.ReservedDescendant descendant in
                 value.Descendants)
        {
            ArgumentNullException.ThrowIfNull(descendant);
            ContractUnitSlot? slot = contract.UnitSlots.SingleOrDefault(
                candidate =>
                    candidate.TeamId == descendant.TeamId
                    && candidate.UnitId == descendant.UnitId);
            int candidateIndex = CandidateIndex(
                value.SourcePosition,
                value.SourceFacing,
                descendant.Position,
                transition.CandidateOffsets);
            bool additionalSlot =
                descendant.TeamId != value.SourceActorId.TeamId
                || descendant.UnitId != value.SourceActorId.UnitId;
            if (slot is null
                || slot.ParticipantId != value.ParticipantId
                || descendant.TeamId != value.SourceActorId.TeamId
                || !string.Equals(
                    descendant.FormId,
                    transition.OutputFormId,
                    StringComparison.Ordinal)
                || descendant.Generation
                    != (long)value.SourceGeneration + 1
                || !claimedSlots.Add(
                    (descendant.TeamId, descendant.UnitId))
                || !claimedPositions.Add(
                    (descendant.Position.X, descendant.Position.Y))
                || candidateIndex <= previousCandidateIndex
                || additionalSlot
                    && descendant.UnitId <= previousAdditionalUnitId)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} descendants violate canonical Split assignment.");
            }
            previousCandidateIndex = candidateIndex;
            if (additionalSlot)
                previousAdditionalUnitId = descendant.UnitId;
        }
    }

    private static void ValidatePendingReplicationClaims(
        ReplayV3.WorldState state,
        CanonicalContractIndex contract,
        string context)
    {
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var claimedSlots = new HashSet<(int TeamId, int UnitId)>();
        var claimedPositions = new HashSet<(int X, int Y)>();
        foreach (ReplayV3.PendingReplication reservation in
                 state.PendingReplications)
        {
            if (!operationIds.Add(reservation.OperationId))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} Split operation ids must be unique.");
            }
            ReplayV3.LifeState? source = state.ActiveLives
                .SingleOrDefault(life =>
                    life.ActorId == reservation.SourceActorId);
            if (source is null
                || source.ParticipantId != reservation.ParticipantId
                || source.Generation != reservation.SourceGeneration
                || !string.Equals(
                    source.FormId,
                    reservation.SourceFormId,
                    StringComparison.Ordinal)
                || source.Position != reservation.SourcePosition
                || !string.Equals(
                    source.Facing,
                    reservation.SourceFacing,
                    StringComparison.Ordinal)
                || reservation.DueTick < state.NextTick)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} Split source does not match its active life.");
            }

            foreach (ReplayV3.ReservedDescendant descendant in
                     reservation.Descendants)
            {
                if (!claimedSlots.Add(
                        (descendant.TeamId, descendant.UnitId))
                    || !claimedPositions.Add(
                        (descendant.Position.X,
                            descendant.Position.Y)))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} Split claims must be globally unique.");
                }
                bool sourceSlot =
                    descendant.TeamId
                        == reservation.SourceActorId.TeamId
                    && descendant.UnitId
                        == reservation.SourceActorId.UnitId;
                ReplayV3.SlotState slot = state.Slots.Single(value =>
                    value.TeamId == descendant.TeamId
                    && value.UnitId == descendant.UnitId);
                if (sourceSlot)
                {
                    if (slot.State is not
                        ReplayV3.UnitSlotState.Active active
                        || active.ActorId
                            != reservation.SourceActorId)
                    {
                        throw new ArgumentException(
                            $"Replay-v3 {context} Split source slot must remain active.");
                    }
                    continue;
                }

                if (slot.State is not
                        ReplayV3.UnitSlotState.ReplicationPending pending
                    || pending.DueTick != reservation.DueTick
                    || pending.SourceActorId
                        != reservation.SourceActorId
                    || !string.Equals(
                        pending.TransitionId,
                        reservation.TransitionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        pending.OperationId,
                        reservation.OperationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        pending.TargetFormId,
                        descendant.FormId,
                        StringComparison.Ordinal)
                    || pending.ReservedPosition != descendant.Position
                    || slot.SplitReservation is null
                    || !CanonicalReplicationEquals(
                        slot.SplitReservation,
                        reservation))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} Split target slot does not match its reservation.");
                }
            }
        }

        int expectedTargetReservationCount = state.PendingReplications
            .Sum(reservation => reservation.Descendants.Length - 1);
        int actualTargetReservationCount = state.Slots.Count(slot =>
            slot.SplitReservation is not null);
        if (actualTargetReservationCount
            != expectedTargetReservationCount)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} has orphaned Split slot reservations.");
        }

        _ = contract;
    }

    private static bool CanonicalReplicationEquals(
        ReplayV3.PendingReplication left,
        ReplayV3.PendingReplication right) =>
        Write(writer => WritePendingReplication(writer, left))
            .AsSpan()
            .SequenceEqual(
                Write(writer => WritePendingReplication(writer, right)));

    private static int CandidateIndex(
        ReplayV3.PositionValue source,
        string sourceFacing,
        ReplayV3.PositionValue candidate,
        ImmutableArray<ContractOffset> offsets)
    {
        for (int index = 0; index < offsets.Length; index++)
        {
            ContractOffset offset = offsets[index];
            long x = source.X;
            long y = source.Y;
            switch (sourceFacing)
            {
                case "north":
                    x += offset.Right;
                    y -= offset.Forward;
                    break;
                case "east":
                    x += offset.Forward;
                    y += offset.Right;
                    break;
                case "south":
                    x -= offset.Right;
                    y += offset.Forward;
                    break;
                case "west":
                    x -= offset.Forward;
                    y -= offset.Right;
                    break;
                default:
                    return -1;
            }
            if (x == candidate.X && y == candidate.Y)
                return index;
        }
        return -1;
    }

    private static void ValidateScoreboard(
        ReplayV3.Scoreboard scoreboard,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(scoreboard);
        RequireCanonicalOrder(
            scoreboard.Teams,
            static (left, right) =>
                left.TeamId.CompareTo(right.TeamId),
            $"{context} teams");
        if (!scoreboard.Teams.Select(team => team.TeamId)
                .SequenceEqual(contract.TeamIds))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} teams must exactly match topology.");
        }
        foreach (ReplayV3.TeamScore team in scoreboard.Teams)
        {
            ArgumentNullException.ThrowIfNull(team);
            ValidateScores(
                team.Scores,
                contract.ScoreChannels,
                $"{context} team {team.TeamId}");
        }
    }

    private static void ValidateScores(
        ImmutableArray<ReplayV3.ScoreValue> scores,
        ImmutableArray<string> expectedChannels,
        string context)
    {
        RequireInitialized(scores, $"{context} scores");
        if (!scores.Select(score => score.Channel)
                .SequenceEqual(expectedChannels))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} score channels must follow the embedded score catalog.");
        }
        foreach (ReplayV3.ScoreValue score in scores)
        {
            ArgumentNullException.ThrowIfNull(score);
            if (!IsCanonicalInt64(score.Value, nonNegative: false))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains a noncanonical score.");
            }
        }
    }

    private static void ValidateLifeStarts(
        ImmutableArray<ReplayV3.LifeStart> starts,
        ReplayV3.ReplayHeader header,
        CanonicalContractIndex contract,
        string context)
    {
        RequireCanonicalOrder(
            starts,
            static (left, right) =>
                CompareActorId(left.ActorId, right.ActorId),
            context);
        foreach (ReplayV3.LifeStart start in starts)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(start.Origin);
            if (start.SchemaVersion
                    != header.Runtime.MatchStartSchemaVersion
                || start.RuntimeContractVersion
                    != header.Runtime.RuntimeContractVersion
                || !IsCanonicalUInt64(start.ActorRandomSeed)
                || !string.Equals(
                    start.MatchContractFingerprint,
                    header.Contract.MatchContractFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} metadata does not match the header contract.");
            }
            if (start.TeamRandomSeed is { } teamRandomSeed)
            {
                // Re-derived rather than merely bounds-checked: the team
                // stream's whole value is that teammates provably share it,
                // so a forged or team-swapped seed is refused here.
                if (!IsCanonicalUInt64(teamRandomSeed)
                    || !ulong.TryParse(
                        teamRandomSeed,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out ulong recordedTeamSeed)
                    || !ulong.TryParse(
                        header.Seed,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out ulong matchSeed)
                    || recordedTeamSeed != SeedDerivation.DeriveTeamSeed(
                        matchSeed,
                        start.ActorId.TeamId,
                        contract.SeedProfileId))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} team seed does not match deterministic derivation.");
                }
            }
            if (!IsSpawnReason(start.Origin.Reason))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains an invalid spawn reason.");
            }
        }
    }

    private static void ValidateObservation(
        ReplayV3.Observation observation,
        ReplayV3.ReplayHeader header,
        CanonicalContractIndex contract,
        string context)
    {
        if (observation.SchemaVersion
                != header.Runtime.ObservationSchemaVersion
            || !string.Equals(
                observation.MatchContractFingerprint,
                contract.Fingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} observation metadata does not match the header contract.");
        }
        ArgumentNullException.ThrowIfNull(observation.Self);
        if (!IsDirection(observation.Self.Facing)
            || !ObservationClassMatches(
                observation.Self.ActorId,
                observation.Self.ClassId,
                contract))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} self facing or class is invalid.");
        }
        if (observation.Self.PreviousActionResolution is { } previous)
        {
            ValidateActionResolution(
                previous,
                contract,
                $"{context} previous action resolution");
        }

        ValidateRouteCooldowns(
            observation.Self.RouteCooldowns,
            observation.Tick,
            $"{context} self route cooldowns");

        RequireCanonicalOrder(
            observation.TeamUnits,
            static (left, right) =>
                CompareUnitKey(
                    left.TeamId,
                    left.UnitId,
                    right.TeamId,
                    right.UnitId),
            $"{context} team units");
        foreach (ReplayV3.ObservedUnitSlot unit in
                 observation.TeamUnits)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ValidateUnitSlotState(
                unit.State,
                $"{context} observed unit state");
        }

        RequireCanonicalOrder(
            observation.Participants,
            static (left, right) =>
                left.ParticipantId.CompareTo(right.ParticipantId),
            $"{context} participants");
        if (!observation.Participants
                .Select(value =>
                    new ContractParticipant(
                        value.ParticipantId,
                        value.TeamId,
                        value.ClassId))
                .SequenceEqual(contract.Participants))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} participant statuses must exactly match topology.");
        }

        RequireCanonicalOrder(
            observation.Allies,
            static (left, right) =>
                CompareActorId(left.ActorId, right.ActorId),
            $"{context} allies");
        foreach (ReplayV3.ObservedAlly ally in observation.Allies)
        {
            ArgumentNullException.ThrowIfNull(ally);
            if (!IsDirection(ally.Facing)
                || !ObservationClassMatches(
                    ally.ActorId,
                    ally.ClassId,
                    contract))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} ally facing or class is invalid.");
            }
            if (ally.PreviousActionResolution is { } allyPrevious)
            {
                ValidateActionResolution(
                    allyPrevious,
                    contract,
                    $"{context} ally previous action resolution");
            }

            ValidateRouteCooldowns(
                ally.RouteCooldowns,
                observation.Tick,
                $"{context} ally route cooldowns");
        }

        RequireCanonicalOrder(
            observation.Enemies,
            static (left, right) =>
                CompareActorId(left.ActorId, right.ActorId),
            $"{context} enemies");
        foreach (ReplayV3.ObservedEnemy enemy in observation.Enemies)
        {
            ArgumentNullException.ThrowIfNull(enemy);
            if (!IsDirection(enemy.Facing)
                || !ObservationClassMatches(
                    enemy.ActorId,
                    enemy.ClassId,
                    contract))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} enemy facing or class is invalid.");
            }
            ValidateActorIds(
                enemy.ObservedBy,
                $"{context} enemy observedBy");
        }

        RequireCanonicalOrder(
            observation.VisibleTiles,
            static (left, right) =>
                ComparePosition(left.Position, right.Position),
            $"{context} visible tiles");
        foreach (ReplayV3.ObservedTile tile in observation.VisibleTiles)
        {
            ArgumentNullException.ThrowIfNull(tile);
            ValidateActorIds(
                tile.ObservedBy,
                $"{context} visible tile observedBy");
            if (tile.SpawnReservation is { } reservation)
            {
                bool automatic = string.Equals(
                    reservation.Kind,
                    "automatic-return",
                    StringComparison.Ordinal);
                bool dynamic = reservation.Kind is
                    "fabrication" or "replication";
                if (!contract.UnitSlots.Any(slot =>
                        slot.TeamId == reservation.TeamId
                        && slot.UnitId == reservation.UnitId)
                    || !automatic && !dynamic
                    || automatic && reservation.DueTick is not null
                    || dynamic
                        && (reservation.DueTick is not int dueTick
                            || dueTick < observation.Tick))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} visible spawn reservation is invalid.");
                }
            }
        }

        if (observation.VisibleProjectiles is { } projectiles)
        {
            RequireCanonicalOrder(
                projectiles,
                static (left, right) =>
                    ParseNonNegativeInt64(left.ProjectileId)
                        .CompareTo(
                            ParseNonNegativeInt64(
                                right.ProjectileId)),
                $"{context} visible projectiles");
            foreach (ReplayV3.ObservedProjectile projectile in projectiles)
            {
                ArgumentNullException.ThrowIfNull(projectile);
                bool invalidLedger =
                    projectile.TicksPerAdvance <= 0
                    || projectile.TicksUntilAdvance
                        > projectile.TicksPerAdvance
                    || projectile.DamagePerHit <= 0;
                if (!IsCanonicalInt64(
                        projectile.ProjectileId,
                        nonNegative: true)
                    || !IsProjectileHeading(projectile.Heading)
                    || !contract.TeamIds.Contains(
                        projectile.OwnerTeamId)
                    || projectile.OwnerActorId is { } owner
                        && owner.TeamId != projectile.OwnerTeamId
                    || projectile.TilesPerAdvance <= 0
                    || projectile.TicksUntilAdvance <= 0
                    || projectile.RemainingTiles < 0
                    || invalidLedger)
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} projectile state is invalid.");
                }
                ValidateActorIds(
                    projectile.ObservedBy,
                    $"{context} projectile observedBy");
            }
        }

        RequireCanonicalOrder(
            observation.VisibleEvents,
            static (left, right) =>
            {
                int tick = left.SourceTick.CompareTo(right.SourceTick);
                return tick != 0
                    ? tick
                    : left.SourceOrdinal.CompareTo(
                        right.SourceOrdinal);
            },
            $"{context} visible events");
        foreach (ReplayV3.ObservedEvent value in
                 observation.VisibleEvents)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Payload);
            ValidateActorIds(
                value.ObservedBy,
                $"{context} visible event observedBy");
            ValidateEventKindAndPayload(
                value.Kind,
                value.Payload,
                $"{context} visible event");
        }

        if (observation.HeardSounds is { } sounds)
        {
            RequireCanonicalOrder(
                sounds,
                static (left, right) =>
                {
                    int tick = left.SourceTick.CompareTo(
                        right.SourceTick);
                    if (tick != 0)
                        return tick;
                    int ordinal = left.SourceOrdinal.CompareTo(
                        right.SourceOrdinal);
                    return ordinal != 0
                        ? ordinal
                        : CompareActorId(
                            left.ObserverActorId,
                            right.ObserverActorId);
                },
                $"{context} heard sounds");
        }

        ValidateScoreboard(
            observation.Scoreboard,
            contract,
            $"{context} scoreboard");
        ValidateModeState(
            observation.Mode,
            observation.Tick,
            contract,
            context);
        ValidateActionLegalities(
            observation.ActionLegalities,
            contract,
            context);
    }

    /// <summary>
    /// A published route-cooldown list is canonical only while every clock
    /// is live: a lapsed entry (ready at or before the observed tick) is an
    /// impossible history, and the canonical writer never emits an empty
    /// list, so presence implies at least one element.
    /// </summary>
    private static void ValidateRouteCooldowns(
        ImmutableArray<ReplayV3.RouteCooldown> value,
        int observedTick,
        string context)
    {
        if (value.IsDefaultOrEmpty)
            return;
        RequireCanonicalOrder(
            value,
            static (left, right) => string.CompareOrdinal(
                left.TransitionId,
                right.TransitionId),
            context);
        foreach (ReplayV3.RouteCooldown cooldown in value)
        {
            ArgumentNullException.ThrowIfNull(cooldown);
            if (string.IsNullOrEmpty(cooldown.TransitionId)
                || cooldown.ReadyAtTick <= observedTick)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains a lapsed or unnamed route cooldown.");
            }
        }
    }

    private static bool ObservationClassMatches(
        ReplayV3.ActorId actorId,
        string? classId,
        CanonicalContractIndex contract)
    {
        ContractUnitSlot? slot = contract.UnitSlots.FirstOrDefault(
            value => value.TeamId == actorId.TeamId
                && value.UnitId == actorId.UnitId);
        ContractParticipant? participant = slot is null
            ? null
            : contract.Participants.FirstOrDefault(
                value => value.ParticipantId == slot.ParticipantId);
        // A BODY's published chassis is its SLOT's where the slot declares
        // one, and its participant's otherwise (DECISIONS #191 §9.2 as shipped
        // by #194's compositions). Under a mixed army the participant's ID is
        // a composition token, not a chassis, so a body must not be checked
        // against it.
        return participant is not null
            && string.Equals(
                classId,
                slot?.ClassId ?? participant.ClassId,
                StringComparison.Ordinal);
    }

    private static void ValidateObservationAgainstState(
        ReplayV3.Observation observation,
        ReplayV3.WorldState state,
        CanonicalContractIndex contract,
        string context)
    {
        if (!ModeObservationMatches(
                observation.Mode,
                state.Mode,
                observation.Self.ActorId.TeamId,
                observation.VisibleTiles.Select(value => value.Position),
                state.ActiveLives
                    .Where(value => value.ActorId.TeamId
                        == observation.Self.ActorId.TeamId)
                    .Select(value => value.Position),
                contract))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} observed mode must exactly match the authoritative pre-state.");
        }

        foreach (ReplayV3.ObservedTile tile in observation.VisibleTiles)
        {
            ReplayV3.SpawnReservation? expected =
                SpawnReservationAt(
                    tile.Position,
                    state,
                    contract);
            if (tile.SpawnReservation != expected)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} visible spawn reservation does not match the authoritative pre-state.");
            }
        }

        if (observation.VisibleProjectiles is not { } projectiles)
            return;
        foreach (ReplayV3.ObservedProjectile observed in projectiles)
        {
            ReplayV3.ProjectileState? authoritative =
                state.Projectiles.FirstOrDefault(value =>
                    string.Equals(
                        value.ProjectileId,
                        observed.ProjectileId,
                        StringComparison.Ordinal));
            ContractAttackProfile? profile = authoritative is null
                ? null
                : contract.AttackProfiles.FirstOrDefault(value =>
                    string.Equals(
                        value.Id,
                        authoritative.AttackProfileId,
                        StringComparison.Ordinal));
            ReplayV3.ActorId? expectedOwnerActorId =
                authoritative is not null
                    && (authoritative.OwnerTeamId
                            == observation.Self.ActorId.TeamId
                        || observation.Enemies.Any(enemy =>
                            enemy.ActorId
                                == authoritative.OwnerActorId))
                    ? authoritative.OwnerActorId
                    : null;
            if (authoritative is null
                || profile is null
                || observed.OwnerTeamId
                    != authoritative.OwnerTeamId
                || observed.OwnerActorId
                    != expectedOwnerActorId
                || observed.Position != authoritative.Position
                || !string.Equals(
                    observed.Heading,
                    authoritative.Heading,
                    StringComparison.Ordinal)
                || observed.TilesPerAdvance
                    != profile.TilesPerAdvance
                || observed.TicksPerAdvance
                    != profile.TicksPerAdvance
                || observed.TicksUntilAdvance
                    != authoritative.TicksUntilAdvance
                || observed.RemainingTiles
                    != authoritative.RemainingTiles
                || observed.DamagePerHit != profile.DamagePerHit)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} visible projectile does not match the authoritative pre-state and attack profile.");
            }
        }
    }

    private static bool ModeObservationMatches(
        ReplayV3.ModeState observed,
        ReplayV3.ModeState authoritative,
        int observingTeamId,
        IEnumerable<ReplayV3.PositionValue> visiblePositions,
        IEnumerable<ReplayV3.PositionValue> ownBodyPositions,
        CanonicalContractIndex contract)
    {
        if (observed is not ReplayV3.ModeState.ArcRelay arcObserved
            || authoritative is not ReplayV3.ModeState.ArcRelay arcAuthoritative)
        {
            return observed == authoritative;
        }

        HashSet<ReplayV3.PositionValue> visible = visiblePositions.ToHashSet();
        ReplayV3.PositionValue[] ownBodies = ownBodyPositions.ToArray();
        int tripNodeRevealRange = contract.Mode.ArcRelay!
            .TripNodeRevealRange;
        ImmutableArray<ReplayV3.ArcCore> expectedCores = arcAuthoritative
            .VisibleCores.Where(core =>
                core.CarrierActorId?.TeamId == observingTeamId
                || visible.Contains(core.Position))
            .ToImmutableArray();
        ImmutableArray<ReplayV3.ArcSignature> expectedSignatures =
            arcAuthoritative.VisibleSignatures.Where(signature =>
                    signature.OwnerTeamId == observingTeamId
                    || string.Equals(
                        signature.Phase,
                        "tell",
                        StringComparison.Ordinal)
                    || string.Equals(
                        signature.SignatureKind,
                        "trip-node",
                        StringComparison.Ordinal)
                        && signature.Positions.Any(position =>
                            ownBodies.Any(body => Math.Max(
                                Math.Abs(body.X - position.X),
                                Math.Abs(body.Y - position.Y))
                                    <= tripNodeRevealRange))
                    || signature.Positions.Any(visible.Contains))
                .ToImmutableArray();
        return string.Equals(
                arcObserved.Id,
                arcAuthoritative.Id,
                StringComparison.Ordinal)
            && arcObserved.Wells.SequenceEqual(arcAuthoritative.Wells)
            && arcObserved.Reactors.SequenceEqual(arcAuthoritative.Reactors)
            && arcObserved.VisibleCores.SequenceEqual(expectedCores)
            && arcObserved.VisibleSignatures.SequenceEqual(expectedSignatures)
            && arcObserved.LatestPulseTeamId
                == arcAuthoritative.LatestPulseTeamId
            && arcObserved.LatestPulseTick == arcAuthoritative.LatestPulseTick;
    }

    private static ReplayV3.SpawnReservation? SpawnReservationAt(
        ReplayV3.PositionValue position,
        ReplayV3.WorldState state,
        CanonicalContractIndex contract)
    {
        foreach (ReplayV3.PendingReplication replication in
                 state.PendingReplications)
        {
            ReplayV3.ReservedDescendant? descendant =
                replication.Descendants.FirstOrDefault(value =>
                    value.Position == position);
            if (descendant is not null)
            {
                return new ReplayV3.SpawnReservation(
                    descendant.TeamId,
                    descendant.UnitId,
                    "replication",
                    replication.DueTick);
            }
        }
        foreach (ReplayV3.SlotState slot in state.Slots)
        {
            switch (slot.State)
            {
                case ReplayV3.UnitSlotState.FabricationPending pending
                    when pending.ReservedPosition == position:
                    return new ReplayV3.SpawnReservation(
                        slot.TeamId,
                        slot.UnitId,
                        "fabrication",
                        pending.DueTick);
            }
        }
        ContractPermanentReservation? permanent =
            contract.PermanentReservations.FirstOrDefault(value =>
                value.Position == position);
        return permanent is null
            ? null
            : new ReplayV3.SpawnReservation(
                permanent.TeamId,
                permanent.UnitId,
                "automatic-return",
                null);
    }

    private static void ValidateActionLegalities(
        ImmutableArray<ReplayV3.ActionLegality> legalities,
        CanonicalContractIndex contract,
        string context)
    {
        RequireCanonicalOrder(
            legalities,
            static (left, right) =>
                left.ActionCode.CompareTo(right.ActionCode),
            $"{context} action legalities");
        if (legalities.Length != contract.Actions.Length)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} action legalities must cover the exact action catalog.");
        }
        for (int index = 0; index < legalities.Length; index++)
        {
            ReplayV3.ActionLegality legality = legalities[index];
            ContractAction action = contract.Actions[index];
            if (legality.ActionCode != action.Code
                || !string.Equals(
                    legality.ActionId,
                    action.Id,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} action legality does not match the embedded action catalog.");
            }
            RequireInitialized(
                legality.Constraints,
                $"{context} action constraints");
            if (!legality.Constraints.Select(value => value.Kind)
                    .SequenceEqual(action.ParameterKinds))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} action constraints must follow the embedded parameter schema.");
            }
            foreach (ReplayV3.ActionConstraint constraint in
                     legality.Constraints)
            {
                ArgumentNullException.ThrowIfNull(constraint);
                switch (constraint)
                {
                    case ReplayV3.ActionConstraint.Direction direction:
                        RequireInitialized(
                            direction.AllowedValues,
                            $"{context} allowed directions");
                        if (direction.AllowedValues.Any(
                                value => !IsDirection(value))
                            || direction.AllowedValues.Distinct(
                                    StringComparer.Ordinal).Count()
                                != direction.AllowedValues.Length)
                        {
                            throw new ArgumentException(
                                $"Replay-v3 {context} contains invalid allowed directions.");
                        }
                        break;
                    case ReplayV3.ActionConstraint.UnitTarget targets:
                        RequireCanonicalOrder(
                            targets.AllowedValues,
                            static (left, right) =>
                                CompareUnitKey(
                                    left.TeamId,
                                    left.UnitId,
                                    right.TeamId,
                                    right.UnitId),
                            $"{context} allowed unit targets");
                        break;
                    case ReplayV3.ActionConstraint.FormTarget forms:
                        RequireCanonicalOrder(
                            forms.AllowedFormIds,
                            static (left, right) =>
                                StringComparer.Ordinal.Compare(
                                    left,
                                    right),
                            $"{context} allowed form targets");
                        break;
                    case ReplayV3.ActionConstraint.ProjectileHeading
                        headings:
                        RequireInitialized(
                            headings.AllowedValues,
                            $"{context} allowed projectile headings");
                        if (headings.AllowedValues.Any(
                                value => !IsProjectileHeading(value))
                            || headings.AllowedValues.Distinct(
                                    StringComparer.Ordinal).Count()
                                != headings.AllowedValues.Length)
                        {
                            throw new ArgumentException(
                                $"Replay-v3 {context} contains invalid allowed projectile headings.");
                        }
                        break;
                    case ReplayV3.ActionConstraint.UpgradeTrack tracks:
                        RequireCanonicalOrder(
                            tracks.AllowedTrackIds,
                            static (left, right) =>
                                StringComparer.Ordinal.Compare(
                                    left,
                                    right),
                            $"{context} allowed upgrade tracks");
                        break;
                }
            }
        }
    }

    private static void ValidateActionResolution(
        ReplayV3.ActionResolution resolution,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!IsActionOutcome(resolution.Outcome))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} contains an invalid action outcome.");
        }
        if (resolution.SubmittedAction is { } submitted)
            ValidateResolvedAction(submitted, contract, context);
        ValidateResolvedAction(
            resolution.AcceptedAction,
            contract,
            context);
        ValidateResolvedAction(
            resolution.ValidatedAction,
            contract,
            context);
        if (resolution.RuntimeFault is { } fault)
        {
            ArgumentNullException.ThrowIfNull(fault.ActorId);
            if (!IsCanonicalInt64(
                    fault.CumulativeFaultCount,
                    nonNegative: true)
                || !IsFaultStage(fault.Stage))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} runtime fault count is invalid.");
            }
        }
    }

    private static void ValidateResolvedAction(
        ReplayV3.ResolvedAction action,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ContractAction? known = contract.Actions.FirstOrDefault(
            value => value.Code == action.ActionCode);
        if (known is null
            || !string.Equals(
                known.Id,
                action.ActionId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} resolved action does not match the embedded catalog.");
        }
        RequireInitialized(action.Arguments, $"{context} arguments");
        bool exactArguments = action.Arguments
            .Select(value => value.Kind)
            .SequenceEqual(known.ParameterKinds);
        bool omittedOptionalArguments =
            action.Arguments.IsEmpty
            && known.AllowsOmittedArguments;
        if (!exactArguments && !omittedOptionalArguments)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} resolved arguments must follow the embedded parameter schema.");
        }
        foreach (ReplayV3.ActionArgument argument in action.Arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
            if (argument is ReplayV3.ActionArgument.Direction direction
                && !IsDirection(direction.Value)
                || argument
                    is ReplayV3.ActionArgument.ProjectileHeading heading
                && !IsProjectileHeading(heading.Value))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains an invalid resolved enum argument.");
            }
        }
    }

    private static void ValidateEvents(
        ImmutableArray<ReplayV3.AuthoritativeEvent> events,
        int expectedTick,
        string context)
    {
        RequireCanonicalOrder(
            events,
            static (left, right) =>
                ParseNonNegativeInt64(left.GlobalOrdinal)
                    .CompareTo(
                        ParseNonNegativeInt64(
                            right.GlobalOrdinal)),
            context);
        foreach (ReplayV3.AuthoritativeEvent value in events)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Payload);
            ArgumentNullException.ThrowIfNull(value.Audience);
            if (value.Tick != expectedTick
                || !IsCanonicalInt64(
                    value.GlobalOrdinal,
                    nonNegative: true))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains invalid tick or ordinal metadata.");
            }
            ValidateEventKindAndPayload(
                value.Kind,
                value.Payload,
                context);
        }
    }

    private static void ValidateTraversals(
        ImmutableArray<ReplayV3.ProjectileTraversal> traversals,
        int expectedTick,
        string context)
    {
        RequireCanonicalOrder(
            traversals,
            static (left, right) =>
                ParseNonNegativeInt64(left.GlobalOrdinal)
                    .CompareTo(
                        ParseNonNegativeInt64(
                            right.GlobalOrdinal)),
            context);
        foreach (ReplayV3.ProjectileTraversal value in traversals)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(value.Terminal);
            if (value.Tick != expectedTick
                || !IsCanonicalInt64(
                    value.GlobalOrdinal,
                    nonNegative: true)
                || !IsCanonicalInt64(
                    value.ProjectileId,
                    nonNegative: true)
                || value.Path.IsDefault)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains invalid traversal metadata.");
            }
            if (!IsTraversalPhase(value.Phase)
                || !IsTraversalTrigger(value.Trigger)
                || !IsProjectileHeading(value.LaunchHeading)
                || !IsProjectileHeading(value.FinalHeading))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} contains invalid traversal vocabulary.");
            }
        }
    }

    private static void AppendFactPhase(
        ImmutableArray<ReplayV3.AuthoritativeEvent> events,
        ImmutableArray<ReplayV3.ProjectileTraversal> traversals,
        List<long> chronologicalOrdinals,
        Dictionary<int, List<int>> eventSourceOrdinals,
        string context)
    {
        RequireInitialized(events, $"{context} events");
        RequireInitialized(traversals, $"{context} traversals");
        long[] phaseOrdinals =
        [
            .. events.Select(value =>
                ParseNonNegativeInt64(value.GlobalOrdinal)),
            .. traversals.Select(value =>
                ParseNonNegativeInt64(value.GlobalOrdinal)),
        ];
        Array.Sort(phaseOrdinals);
        if (phaseOrdinals.Length
                != phaseOrdinals.Distinct().Count())
        {
            throw new ArgumentException(
                $"Replay-v3 {context} events and traversals cannot share a global ordinal.");
        }
        chronologicalOrdinals.AddRange(phaseOrdinals);
        foreach (ReplayV3.AuthoritativeEvent value in events)
        {
            if (!eventSourceOrdinals.TryGetValue(
                    value.Tick,
                    out List<int>? ordinals))
            {
                ordinals = [];
                eventSourceOrdinals.Add(value.Tick, ordinals);
            }
            ordinals.Add(value.SourceOrdinal);
        }
    }

    private static void ValidateResult(
        ReplayV3.MatchResult? result,
        ImmutableArray<ReplayV3.TickFrame> ticks,
        ReplayV3.WorldState finalState,
        CanonicalContractIndex contract)
    {
        if (result is null)
            return;
        if (ticks.Length == 0)
        {
            if (result.EndTick is not null)
            {
                throw new ArgumentException(
                    "A zero-tick replay-v3 result cannot name an end tick.");
            }
        }
        else if (result.EndTick != ticks[^1].Tick)
        {
            throw new ArgumentException(
                "Replay-v3 result endTick must equal its final executed tick.");
        }

        RequireCanonicalOrder(
            result.EligibleTeamIds,
            static (left, right) => left.CompareTo(right),
            "result eligible teams");
        if (result.EligibleTeamIds.Any(
                teamId => !contract.TeamIds.Contains(teamId)))
        {
            throw new ArgumentException(
                "Replay-v3 result contains an unknown eligible team.");
        }
        int[] expectedEligibleTeams = finalState.Scoreboard.Teams
            .Where(team => team.Eligible)
            .Select(team => team.TeamId)
            .ToArray();
        if (!result.EligibleTeamIds.SequenceEqual(
                expectedEligibleTeams))
        {
            throw new ArgumentException(
                "Replay-v3 result eligible teams must match the final scoreboard.");
        }

        ArgumentNullException.ThrowIfNull(result.Standings);
        RequireCanonicalOrder(
            result.Standings.Teams,
            static (left, right) =>
            {
                int rank = left.Rank.CompareTo(right.Rank);
                return rank != 0
                    ? rank
                    : left.TeamId.CompareTo(right.TeamId);
            },
            "result standings");
        if (!result.Standings.Teams
                .Select(value => value.TeamId)
                .Order()
                .SequenceEqual(contract.TeamIds))
        {
            throw new ArgumentException(
                "Replay-v3 result standings must cover every topology team.");
        }
        foreach (ReplayV3.TeamStanding standing in
                 result.Standings.Teams)
        {
            if (!IsStandingOutcome(standing.Outcome))
            {
                throw new ArgumentException(
                    "Replay-v3 standing outcome is invalid.");
            }
            ValidateScores(
                standing.Scores,
                contract.ScoreChannels,
                $"standing team {standing.TeamId}");
            ReplayV3.TeamScore finalScore =
                finalState.Scoreboard.Teams.Single(
                    team => team.TeamId == standing.TeamId);
            if (!standing.Scores.SequenceEqual(finalScore.Scores))
            {
                throw new ArgumentException(
                    "Replay-v3 standing scores must equal the final scoreboard.");
            }
        }
        if (result.Standings.WinnerTeamId is int winner
            && !contract.TeamIds.Contains(winner))
        {
            throw new ArgumentException(
                "Replay-v3 result winner is not a topology team.");
        }

        RequireCanonicalOrder(
            result.Units,
            static (left, right) =>
                CompareUnitKey(
                    left.Slot.TeamId,
                    left.Slot.UnitId,
                    right.Slot.TeamId,
                    right.Slot.UnitId),
            "result units");
        // Identity, not chassis: a slot's declared chassis lives in the
        // topology and nowhere else, so a result unit matches on its triple.
        if (!result.Units
                .Select(value =>
                    (value.Slot.TeamId,
                        value.Slot.UnitId,
                        value.Slot.ParticipantId))
                .SequenceEqual(
                    contract.UnitSlots.Select(value =>
                        (value.TeamId, value.UnitId, value.ParticipantId))))
        {
            throw new ArgumentException(
                "Replay-v3 result units must exactly match topology slots.");
        }
        foreach (ReplayV3.UnitTerminalFact unit in result.Units)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(unit.Slot);
            ValidateUnitSlotState(unit.Slot.State, "result unit slot");
            if (unit.ActiveLife is { } life)
            {
                ValidateLifeState(life, contract, "result active life");
            }
            ReplayV3.SlotState finalSlot = finalState.Slots.Single(
                value => value.TeamId == unit.Slot.TeamId
                         && value.UnitId == unit.Slot.UnitId);
            if (!CanonicalSlotEquals(finalSlot, unit.Slot))
            {
                throw new ArgumentException(
                    "Replay-v3 terminal slot facts must equal the final world.");
            }
            ReplayV3.LifeState? finalLife = finalState.ActiveLives
                .SingleOrDefault(value =>
                    value.ActorId.TeamId == unit.Slot.TeamId
                    && value.ActorId.UnitId == unit.Slot.UnitId);
            if (finalLife is null != (unit.ActiveLife is null)
                || finalLife is not null
                && !CanonicalLifeEquals(
                    finalLife,
                    unit.ActiveLife!))
            {
                throw new ArgumentException(
                    "Replay-v3 terminal life facts must equal the final world.");
            }
        }

        ArgumentNullException.ThrowIfNull(result.Mode);
        if (result.Mode is ReplayV3.ModeResult.Deathmatch deathmatch)
        {
            if (!IsDeathmatchEndReason(deathmatch.Reason)
                || !string.Equals(
                    result.CompletionReason,
                    deathmatch.Reason,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Replay-v3 deathmatch completion reason is invalid or inconsistent.");
            }
            RequireCanonicalOrder(
                deathmatch.Scores,
                static (left, right) =>
                    left.TeamId.CompareTo(right.TeamId),
                "deathmatch result scores");
            if (!deathmatch.Scores.Select(value => value.TeamId)
                    .SequenceEqual(contract.TeamIds))
            {
                throw new ArgumentException(
                    "Replay-v3 deathmatch result scores must cover every topology team.");
            }
            if (finalState.Mode is not ReplayV3.ModeState.Deathmatch)
            {
                throw new ArgumentException(
                    "Replay-v3 deathmatch result must match final mode state.");
            }
            foreach (ReplayV3.DeathmatchTeamScore score in
                     deathmatch.Scores)
            {
                ReplayV3.TeamScore finalScore =
                    finalState.Scoreboard.Teams.Single(
                        team => team.TeamId == score.TeamId);
                if (!ScoreEquals(finalScore, "kills", score.Kills)
                    || !ScoreEquals(
                        finalScore,
                        "deaths",
                        score.Deaths)
                    || !ScoreEquals(
                        finalScore,
                        "damage-dealt",
                        score.DamageDealt))
                {
                    throw new ArgumentException(
                        "Replay-v3 deathmatch counters must equal the final scoreboard.");
                }
            }
            ValidateDeathmatchStandings(
                result,
                deathmatch,
                finalState,
                contract);
        }
        else if (result.Mode is ReplayV3.ModeResult.Frontline frontline)
        {
            ValidateFrontlineResult(
                result,
                frontline,
                finalState,
                contract);
        }
        else if (result.Mode is ReplayV3.ModeResult.ArcRelay arcRelay)
        {
            ValidateArcRelayResult(
                result,
                arcRelay,
                finalState,
                contract);
        }
        else
        {
            throw new ArgumentException(
                "Replay-v3 result contains an unsupported terminal mode arm.");
        }
        ValidateCompetitionRanks(result.Standings);
    }

    private static void ValidateArcRelayResult(
        ReplayV3.MatchResult result,
        ReplayV3.ModeResult.ArcRelay arcRelay,
        ReplayV3.WorldState finalState,
        CanonicalContractIndex contract)
    {
        if (!string.Equals(
                contract.Mode.Kind,
                "arc-relay",
                StringComparison.Ordinal)
            || contract.Mode.ArcRelay is not { } configuration)
        {
            throw new ArgumentException(
                "Replay-v3 Arc Relay result does not match the embedded game mode.");
        }
        if (!IsArcRelayEndReason(arcRelay.Reason)
            || !string.Equals(
                result.CompletionReason,
                arcRelay.Reason,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay-v3 Arc Relay completion reason is invalid or inconsistent.");
        }
        if (finalState.Mode is not ReplayV3.ModeState.ArcRelay finalArc
            || !Equals(arcRelay.State, finalArc))
        {
            throw new ArgumentException(
                "Replay-v3 Arc Relay terminal state must exactly equal the final mode state.");
        }

        HashSet<int> eligible = result.EligibleTeamIds.ToHashSet();
        bool legalReason = arcRelay.Reason switch
        {
            "fault-eligibility" =>
                eligible.Count <= 1
                && result.EndTick < contract.Mode.MaxTicks,
            "reactor-destroyed" =>
                eligible.Count > 1
                && result.EndTick < contract.Mode.MaxTicks
                && finalArc.Reactors.Any(value =>
                    value.IntegritySegments == 0),
            "max-ticks" =>
                eligible.Count > 1
                && result.EndTick == contract.Mode.MaxTicks - 1
                && finalArc.Reactors.All(value =>
                    value.IntegritySegments > 0),
            _ => false,
        };
        if (!legalReason)
        {
            throw new ArgumentException(
                "Replay-v3 Arc Relay completion reason is not legal for the final state.");
        }

        Dictionary<int, (long Pulses, long Charge)> scores = [];
        foreach (int teamId in contract.TeamIds)
        {
            ReplayV3.ArcReactor own = finalArc.Reactors.Single(value =>
                value.TeamId == teamId);
            ReplayV3.ArcReactor opposing = finalArc.Reactors.Single(value =>
                value.TeamId != teamId);
            long pulses = configuration.PulsesToDestroyReactor
                - opposing.IntegritySegments;
            ReplayV3.TeamScore finalScore = finalState.Scoreboard.Teams.Single(
                value => value.TeamId == teamId);
            if (!ScoreEquals(
                    finalScore,
                    "pulses",
                    pulses.ToString(CultureInfo.InvariantCulture))
                || !ScoreEquals(
                    finalScore,
                    "reactor-charge",
                    own.ChargePips.ToString(CultureInfo.InvariantCulture)))
            {
                throw new ArgumentException(
                    "Replay-v3 Arc Relay reactor facts and scoreboard disagree.");
            }
            scores.Add(teamId, (pulses, own.ChargePips));
        }

        int Compare(int left, int right)
        {
            int pulses = scores[right].Pulses.CompareTo(scores[left].Pulses);
            return pulses != 0
                ? pulses
                : scores[right].Charge.CompareTo(scores[left].Charge);
        }
        int[] rankedEligible = [.. eligible];
        Array.Sort(rankedEligible, (left, right) =>
        {
            int comparison = Compare(left, right);
            return comparison != 0 ? comparison : left.CompareTo(right);
        });
        var ranks = new Dictionary<int, int>();
        for (int index = 0; index < rankedEligible.Length; index++)
        {
            ranks[rankedEligible[index]] = index == 0
                ? 1
                : Compare(rankedEligible[index - 1], rankedEligible[index]) == 0
                    ? ranks[rankedEligible[index - 1]]
                    : index + 1;
        }
        int ineligibleRank = rankedEligible.Length + 1;
        foreach (int teamId in contract.TeamIds)
        {
            if (!eligible.Contains(teamId))
                ranks.Add(teamId, ineligibleRank);
        }
        int topCount = ranks.Count(value => value.Value == 1);
        ReplayV3.TeamStanding[] expected = contract.TeamIds.Select(teamId =>
                new ReplayV3.TeamStanding(
                    teamId,
                    ranks[teamId],
                    ranks[teamId] == 1
                        ? topCount == 1 ? "win" : "draw"
                        : "loss",
                    []))
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.TeamId)
            .ToArray();
        if (!result.Standings.Teams.Zip(expected).All(pair =>
                pair.First.TeamId == pair.Second.TeamId
                && pair.First.Rank == pair.Second.Rank
                && string.Equals(
                    pair.First.Outcome,
                    pair.Second.Outcome,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Replay-v3 Arc Relay standings do not follow the embedded victory policy.");
        }
    }

    private static void ValidateFrontlineResult(
        ReplayV3.MatchResult result,
        ReplayV3.ModeResult.Frontline frontline,
        ReplayV3.WorldState finalState,
        CanonicalContractIndex contract)
    {
        if (!string.Equals(
                contract.Mode.Kind,
                "frontline",
                StringComparison.Ordinal)
            || contract.Mode.Frontline is not { } configuration)
        {
            throw new ArgumentException(
                "Replay-v3 Frontline result does not match the embedded game mode.");
        }
        if (!IsFrontlineEndReason(frontline.Reason)
            || !string.Equals(
                result.CompletionReason,
                frontline.Reason,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay-v3 Frontline completion reason is invalid or inconsistent.");
        }
        if (finalState.Mode
                is not ReplayV3.ModeState.Frontline finalControl
            || !Equals(frontline.Control, finalControl))
        {
            throw new ArgumentException(
                "Replay-v3 Frontline terminal control must exactly equal the final mode state.");
        }

        RequireCanonicalOrder(
            frontline.Scores,
            static (left, right) =>
                left.TeamId.CompareTo(right.TeamId),
            "Frontline result scores");
        if (!frontline.Scores.Select(value => value.TeamId)
                .SequenceEqual(contract.TeamIds))
        {
            throw new ArgumentException(
                "Replay-v3 Frontline result scores must cover every topology team.");
        }

        Dictionary<int, long> territorialScores = [];
        foreach (ReplayV3.FrontlineTeamScore score in frontline.Scores)
        {
            long actual = ParseSignedInt64(score.TerritorialProgress);
            long expected = FrontlineTerritorialProgress(
                score.TeamId,
                frontline.Control,
                configuration);
            ReplayV3.ScoreValue finalScore = finalState.Scoreboard.Teams
                .Single(team => team.TeamId == score.TeamId)
                .Scores.Single(value => string.Equals(
                    value.Channel,
                    "territorial-progress",
                    StringComparison.Ordinal));
            if (actual != expected
                || !string.Equals(
                    score.TerritorialProgress,
                    finalScore.Value,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Replay-v3 Frontline territorial scores must equal the final control formula and scoreboard.");
            }
            territorialScores.Add(score.TeamId, actual);
        }

        ValidateFrontlineStandings(
            result,
            frontline,
            territorialScores,
            configuration,
            finalState,
            contract);
    }

    private static long FrontlineTerritorialProgress(
        int teamId,
        ReplayV3.ModeState.Frontline control,
        ContractFrontline configuration)
    {
        ContractTeamAdvance advance =
            configuration.TeamAdvances.Single(value =>
                value.TeamId == teamId);
        int centre = configuration.PositionCount / 2;
        long position = checked(
            (long)advance.ObjectiveIndexDelta
            * (control.ActivePositionIndex - centre)
            * configuration.CaptureThreshold);
        long claim = control.ClaimingTeamId switch
        {
            null => 0,
            int claimant when claimant == teamId =>
                control.CaptureProgress,
            _ => -control.CaptureProgress,
        };
        return checked(position + claim);
    }

    private static void ValidateFrontlineStandings(
        ReplayV3.MatchResult result,
        ReplayV3.ModeResult.Frontline frontline,
        IReadOnlyDictionary<int, long> territorialScores,
        ContractFrontline configuration,
        ReplayV3.WorldState finalState,
        CanonicalContractIndex contract)
    {
        HashSet<int> eligible = result.EligibleTeamIds.ToHashSet();
        int CompareTerritory(int leftTeamId, int rightTeamId) =>
            -territorialScores[leftTeamId].CompareTo(
                territorialScores[rightTeamId]);

        Comparison<int> authoritativeComparison;
        switch (frontline.Reason)
        {
            case "fault-eligibility"
                when eligible.Count <= 1
                     && result.EndTick < contract.Mode.MaxTicks:
                authoritativeComparison = CompareTerritory;
                break;
            case "max-ticks"
                when eligible.Count > 1
                     && result.EndTick == contract.Mode.MaxTicks - 1:
                authoritativeComparison = CompareTerritory;
                break;
            case "base-breach"
                when eligible.Count > 1
                     && result.EndTick < contract.Mode.MaxTicks
                     && frontline.Control.ClaimingTeamId is null
                     && frontline.Control.CaptureProgress == 0
                     && frontline.Control.DecayTicksElapsed == 0
                     && frontline.Control.ControlResumesAtTick
                         <= finalState.NextTick:
                {
                    ContractTeamAdvance[] breachCandidates =
                        configuration.TeamAdvances
                            .Where(advance =>
                                frontline.Control.ActivePositionIndex
                                == (advance.ObjectiveIndexDelta > 0
                                    ? configuration.PositionCount - 1
                                    : 0))
                            .ToArray();
                    if (breachCandidates.Length != 1
                        || !eligible.Contains(
                            breachCandidates[0].TeamId))
                    {
                        throw new ArgumentException(
                            "Replay-v3 Frontline base-breach reason is not legal for the final control state.");
                    }
                    int breachWinner = breachCandidates[0].TeamId;
                    authoritativeComparison = (left, right) =>
                        left == breachWinner
                            ? -1
                            : right == breachWinner
                                ? 1
                                : 0;
                    break;
                }
            default:
                throw new ArgumentException(
                    "Replay-v3 Frontline completion reason is not legal for the final state.");
        }

        int[] rankedEligible = eligible.ToArray();
        Array.Sort(
            rankedEligible,
            (left, right) =>
            {
                int comparison = authoritativeComparison(left, right);
                return comparison != 0
                    ? comparison
                    : left.CompareTo(right);
            });
        var ranks = new Dictionary<int, int>();
        for (int index = 0; index < rankedEligible.Length; index++)
        {
            int rank = index == 0
                ? 1
                : authoritativeComparison(
                    rankedEligible[index - 1],
                    rankedEligible[index]) == 0
                    ? ranks[rankedEligible[index - 1]]
                    : index + 1;
            ranks.Add(rankedEligible[index], rank);
        }
        int ineligibleRank = rankedEligible.Length + 1;
        foreach (int teamId in contract.TeamIds)
        {
            if (!eligible.Contains(teamId))
                ranks.Add(teamId, ineligibleRank);
        }

        int topCount = ranks.Count(value => value.Value == 1);
        ReplayV3.TeamStanding[] expected = contract.TeamIds
            .Select(teamId =>
            {
                int rank = ranks[teamId];
                string outcome = rank switch
                {
                    1 when topCount == 1 => "win",
                    1 => "draw",
                    _ => "loss",
                };
                return new ReplayV3.TeamStanding(
                    teamId,
                    rank,
                    outcome,
                    []);
            })
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.TeamId)
            .ToArray();
        if (!result.Standings.Teams.Zip(expected).All(pair =>
                pair.First.TeamId == pair.Second.TeamId
                && pair.First.Rank == pair.Second.Rank
                && string.Equals(
                    pair.First.Outcome,
                    pair.Second.Outcome,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Replay-v3 Frontline standings do not follow the embedded victory policy.");
        }
    }

    private static void ValidateDeathmatchStandings(
        ReplayV3.MatchResult result,
        ReplayV3.ModeResult.Deathmatch deathmatch,
        ReplayV3.WorldState finalState,
        CanonicalContractIndex contract)
    {
        if (!string.Equals(
                contract.Mode.Kind,
                "deathmatch",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Replay-v3 Deathmatch result does not match the embedded game mode.");
        }

        HashSet<int> eligible = result.EligibleTeamIds.ToHashSet();
        Dictionary<int, ReplayV3.TeamScore> scores = finalState
            .Scoreboard.Teams.ToDictionary(value => value.TeamId);
        int CompareTimeout(int leftTeamId, int rightTeamId)
        {
            foreach (ContractRanking ranking in
                     contract.Mode.TimeoutRanking)
            {
                long left = ScoreValue(
                    scores[leftTeamId],
                    ranking.Channel);
                long right = ScoreValue(
                    scores[rightTeamId],
                    ranking.Channel);
                int comparison = left.CompareTo(right);
                if (comparison == 0)
                    continue;
                return string.Equals(
                    ranking.Direction,
                    "higher-wins",
                    StringComparison.Ordinal)
                    ? -comparison
                    : comparison;
            }
            return 0;
        }

        int CompareKills(int leftTeamId, int rightTeamId) =>
            -DeathmatchKills(
                    deathmatch.Scores.Single(value =>
                        value.TeamId == leftTeamId))
                .CompareTo(
                    DeathmatchKills(
                        deathmatch.Scores.Single(value =>
                            value.TeamId == rightTeamId)));

        Comparison<int> authoritativeComparison =
            deathmatch.Reason switch
            {
                "fault-eligibility" when eligible.Count <= 1 =>
                    CompareTimeout,
                "kill-limit"
                    when eligible.Count > 1
                         && contract.Mode.KillsToWin is int killsToWin
                         && eligible.Max(teamId =>
                             DeathmatchKills(
                                 deathmatch.Scores.Single(value =>
                                     value.TeamId == teamId)))
                         >= killsToWin =>
                    CompareKills,
                "max-ticks"
                    when eligible.Count > 1
                         && result.EndTick
                         == contract.Mode.MaxTicks - 1
                         && !KillLimitReached(
                             deathmatch,
                             eligible,
                             contract.Mode.KillsToWin) =>
                    CompareTimeout,
                _ => throw new ArgumentException(
                    "Replay-v3 Deathmatch completion reason is not legal for the final state."),
            };

        int[] rankedEligible = eligible.ToArray();
        Array.Sort(
            rankedEligible,
            (left, right) =>
            {
                int comparison = authoritativeComparison(left, right);
                return comparison != 0
                    ? comparison
                    : left.CompareTo(right);
            });
        var ranks = new Dictionary<int, int>();
        for (int index = 0; index < rankedEligible.Length; index++)
        {
            int rank = index == 0
                ? 1
                : authoritativeComparison(
                    rankedEligible[index - 1],
                    rankedEligible[index]) == 0
                    ? ranks[rankedEligible[index - 1]]
                    : index + 1;
            ranks.Add(rankedEligible[index], rank);
        }
        int ineligibleRank = rankedEligible.Length + 1;
        foreach (int teamId in contract.TeamIds)
        {
            if (!eligible.Contains(teamId))
                ranks.Add(teamId, ineligibleRank);
        }

        int topCount = ranks.Count(value => value.Value == 1);
        ReplayV3.TeamStanding[] expected = contract.TeamIds
            .Select(teamId =>
            {
                int rank = ranks[teamId];
                string outcome = rank switch
                {
                    1 when topCount == 1 => "win",
                    1 => "draw",
                    _ => "loss",
                };
                return new ReplayV3.TeamStanding(
                    teamId,
                    rank,
                    outcome,
                    scores[teamId].Scores);
            })
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.TeamId)
            .ToArray();
        if (!result.Standings.Teams.Zip(expected).All(pair =>
                pair.First.TeamId == pair.Second.TeamId
                && pair.First.Rank == pair.Second.Rank
                && string.Equals(
                    pair.First.Outcome,
                    pair.Second.Outcome,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Replay-v3 Deathmatch standings do not follow the embedded victory policy.");
        }
    }

    private static bool KillLimitReached(
        ReplayV3.ModeResult.Deathmatch deathmatch,
        IReadOnlySet<int> eligible,
        int? killsToWin) =>
        killsToWin is int threshold
        && eligible.Count != 0
        && eligible.Max(teamId =>
            DeathmatchKills(
                deathmatch.Scores.Single(value =>
                    value.TeamId == teamId))) >= threshold;

    private static long DeathmatchKills(
        ReplayV3.DeathmatchTeamScore score) =>
        ParseNonNegativeInt64(score.Kills);

    private static long ScoreValue(
        ReplayV3.TeamScore score,
        string channel)
    {
        ReplayV3.ScoreValue value = score.Scores.Single(item =>
            string.Equals(
                item.Channel,
                channel,
                StringComparison.Ordinal));
        return long.Parse(
            value.Value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);
    }

    private static bool CanonicalSlotEquals(
        ReplayV3.SlotState left,
        ReplayV3.SlotState right) =>
        Write(writer => WriteSlotState(writer, left))
            .AsSpan()
            .SequenceEqual(
                Write(writer => WriteSlotState(writer, right)));

    private static bool CanonicalLifeEquals(
        ReplayV3.LifeState left,
        ReplayV3.LifeState right) =>
        Write(writer => WriteLifeState(writer, left))
            .AsSpan()
            .SequenceEqual(
                Write(writer => WriteLifeState(writer, right)));

    private static bool ScoreEquals(
        ReplayV3.TeamScore team,
        string channel,
        string expected) =>
        team.Scores.SingleOrDefault(score =>
                string.Equals(
                    score.Channel,
                    channel,
                    StringComparison.Ordinal))
            is not { } value
        || string.Equals(
            value.Value,
            expected,
            StringComparison.Ordinal);

    private static void ValidateCompetitionRanks(
        ReplayV3.Standings standings)
    {
        int groupStart = 0;
        while (groupStart < standings.Teams.Length)
        {
            int rank = standings.Teams[groupStart].Rank;
            if (rank != groupStart + 1)
            {
                throw new ArgumentException(
                    "Replay-v3 standings must use competition ranking.");
            }
            do
            {
                groupStart++;
            }
            while (groupStart < standings.Teams.Length
                   && standings.Teams[groupStart].Rank == rank);
        }

        int topCount = standings.Teams.Count(value => value.Rank == 1);
        int? expectedWinner = topCount == 1
            ? standings.Teams[0].TeamId
            : null;
        if (standings.WinnerTeamId != expectedWinner
            || standings.Teams.Any(value =>
                value.Outcome != (value.Rank switch
                {
                    1 when topCount == 1 => "win",
                    1 => "draw",
                    _ => "loss",
                })))
        {
            throw new ArgumentException(
                "Replay-v3 standing outcomes and winner must follow their ranks.");
        }
    }

    private static void ValidateUnitSlotState(
        ReplayV3.UnitSlotState state,
        string context)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (state)
        {
            case ReplayV3.UnitSlotState.AvailabilityPending pending
                when !IsAvailabilityReason(pending.Reason):
                throw new ArgumentException(
                    $"Replay-v3 {context} availability reason is invalid.");
            case ReplayV3.UnitSlotState.Active active:
                ArgumentNullException.ThrowIfNull(active.ActorId);
                break;
            case ReplayV3.UnitSlotState.FabricationPending pending:
                ArgumentNullException.ThrowIfNull(
                    pending.SourceActorId);
                ArgumentNullException.ThrowIfNull(
                    pending.ReservedPosition);
                break;
            case ReplayV3.UnitSlotState.ReplicationPending pending:
                ArgumentNullException.ThrowIfNull(
                    pending.SourceActorId);
                ArgumentNullException.ThrowIfNull(
                    pending.ReservedPosition);
                break;
        }
    }

    private static void ValidateModeState(
        ReplayV3.ModeState mode,
        int nextTick,
        CanonicalContractIndex contract,
        string context)
    {
        ArgumentNullException.ThrowIfNull(mode);
        if (!string.Equals(
                mode.Kind,
                contract.Mode.Kind,
                StringComparison.Ordinal)
            || !string.Equals(
                mode.ModeId,
                contract.Mode.ModeId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} mode state does not match the embedded game mode.");
        }

        if (mode is ReplayV3.ModeState.ArcRelay arcRelay)
        {
            ValidateArcRelayModeState(
                arcRelay,
                nextTick,
                contract,
                context);
            return;
        }
        if (mode is not ReplayV3.ModeState.Frontline frontline)
            return;
        if (contract.Mode.Frontline is not { } configuration)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Frontline state does not match the embedded game mode.");
        }

        bool decayDisabled =
            configuration.DecayAmount == 0
            && configuration.DecayIntervalTicks == 0;
        bool invalid =
            nextTick < 0
            || frontline.ActivePositionIndex < 0
            || frontline.ActivePositionIndex >= configuration.PositionCount
            || frontline.ClaimingTeamId is int claimant
                && !contract.TeamIds.Contains(claimant)
            || frontline.CaptureProgress < 0
            || frontline.CaptureProgress
                >= configuration.CaptureThreshold
            || frontline.DecayTicksElapsed < 0
            || decayDisabled && frontline.DecayTicksElapsed != 0
            || !decayDisabled
                && frontline.DecayTicksElapsed
                    >= configuration.DecayIntervalTicks
            || frontline.ControlResumesAtTick < 0
            || frontline.ClaimingTeamId is null
                && frontline.CaptureProgress != 0
            || frontline.ClaimingTeamId is null
                && frontline.DecayTicksElapsed != 0
            || frontline.ClaimingTeamId is not null
                && frontline.CaptureProgress == 0
            || nextTick < frontline.ControlResumesAtTick
                && (frontline.ClaimingTeamId is not null
                    || frontline.CaptureProgress != 0
                    || frontline.DecayTicksElapsed != 0)
            || (long)frontline.ControlResumesAtTick - nextTick
                > configuration.RedeployPauseTicks
            // The hold clocks travel as a pair, only a ratchet ruleset may
            // carry them at all, an owner must be a real scoring team, and a
            // published hold is by definition still live and cannot outlast
            // the declared duration measured from this tick.
            || (frontline.HoldOwnerTeamId is null)
                != (frontline.HoldEndsAtTick is null)
            || frontline.HoldOwnerTeamId is not null && !configuration.Ratchet
            || frontline.HoldOwnerTeamId is int holdOwner
                && !contract.TeamIds.Contains(holdOwner)
            // A hold is created on the advance tick T with expiry T+hold+1,
            // and the earliest boundary that can publish it has nextTick T+1,
            // so the widest honest gap is exactly the declared duration.
            || frontline.HoldEndsAtTick is int holdEnds
                && (holdEnds <= nextTick
                    || (long)holdEnds - nextTick
                        > configuration.RatchetHoldTicks)
            // Only a mode that declares a side objective may publish one:
            // its owner and its claimant must be real scoring teams, they
            // cannot be the same team (the owner has nothing left to claim),
            // and a standing claim is strictly below the declared threshold
            // because reaching it latches ownership on that very tick.
            || (frontline.SecondaryOwnerTeamId is not null
                    || frontline.SecondaryClaimProgress != 0)
                && !configuration.SecondaryControl
            || frontline.SecondaryOwnerTeamId is int secondaryOwner
                && !contract.TeamIds.Contains(secondaryOwner)
            || Math.Abs((long)frontline.SecondaryClaimProgress)
                >= Math.Max(
                    configuration.SecondaryCaptureThresholdTicks,
                    1)
            || SecondaryClaimant(frontline.SecondaryClaimProgress)
                is int secondaryClaimant
                && (!contract.TeamIds.Contains(secondaryClaimant)
                    || secondaryClaimant == frontline.SecondaryOwnerTeamId);
        if (invalid)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Frontline control violates the embedded capture bounds.");
        }
    }

    private static void ValidateArcRelayModeState(
        ReplayV3.ModeState.ArcRelay state,
        int nextTick,
        CanonicalContractIndex contract,
        string context)
    {
        if (contract.Mode.ArcRelay is not { } configuration)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Arc Relay state does not match the embedded game mode.");
        }
        RequireInitialized(state.Wells, $"{context} Arc Relay Wells");
        RequireInitialized(state.Reactors, $"{context} Arc Relay reactors");
        RequireInitialized(state.VisibleCores, $"{context} Arc Relay Cores");
        RequireInitialized(
            state.VisibleSignatures,
            $"{context} Arc Relay signatures");
        if (!state.Wells.Select(value => value.WellId)
                .SequenceEqual(configuration.WellIds)
            || !state.Reactors.Select(value => value.TeamId)
                .SequenceEqual(contract.TeamIds)
            || (state.LatestPulseTeamId is null)
                != (state.LatestPulseTick is null)
            || state.LatestPulseTeamId is int pulseTeam
                && !contract.TeamIds.Contains(pulseTeam)
            || state.LatestPulseTick is int pulseTick
                && (pulseTick < 0 || pulseTick >= nextTick))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Arc Relay public ledger is invalid.");
        }

        foreach (ReplayV3.ArcWell well in state.Wells)
        {
            if ((well.PendingCharge || well.OutstandingCoreId is not null)
                    && well.NextScheduledBirthTick is int scheduled
                    && scheduled < 0
                || (well.RearmCompletesAtTick is not null)
                    != (well.PendingCharge
                        && well.OutstandingCoreId is null)
                || well.RearmCompletesAtTick is int rearm && rearm < nextTick
                || well.OutstandingCoreId is { } outstanding
                    && (!string.Equals(
                        outstanding.SourceWellId,
                        well.WellId,
                        StringComparison.Ordinal)
                        || outstanding.SourceOrdinal < 0))
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} Arc Relay Well state is invalid.");
            }
        }
        if (state.VisibleCores.Select(value => value.CoreId).Distinct().Count()
                != state.VisibleCores.Length
            || state.VisibleCores.Any(core =>
                !configuration.WellIds.Contains(
                    core.CoreId.SourceWellId,
                    StringComparer.Ordinal)
                || core.CoreId.SourceOrdinal < 0
                || core.NextRelocationTick < 0
                || core.Disposition is not ("loose" or "carried" or "in-flight")
                || (core.Disposition == "carried") != (core.CarrierActorId is not null)
                || (core.Disposition == "in-flight")
                    != (core.FlightTarget is not null
                        && core.FlightCompletesAtTick is not null)
                || core.FlightCompletesAtTick < nextTick))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Arc Relay Core state is invalid.");
        }
        foreach (ReplayV3.ArcReactor reactor in state.Reactors)
        {
            if (reactor.ChargePips < 0
                || reactor.ChargePips >= configuration.CoresPerPulse
                || reactor.IntegritySegments < 0
                || reactor.IntegritySegments
                    > configuration.PulsesToDestroyReactor)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} Arc Relay reactor state is invalid.");
            }
        }
        if (state.VisibleSignatures.Select(value => value.OperationId)
                .Distinct(StringComparer.Ordinal).Count()
                != state.VisibleSignatures.Length
            || state.VisibleSignatures.Any(value =>
                string.IsNullOrWhiteSpace(value.OperationId)
                || string.IsNullOrWhiteSpace(value.SignatureId)
                || value.OwnerActorId.TeamId != value.OwnerTeamId
                || !contract.TeamIds.Contains(value.OwnerTeamId)
                || value.Phase is not ("tell" or "active" or "channel" or "in-flight")
                || value.StartedTick < 0
                || value.StartedTick >= nextTick
                || value.CompletesAtTick < nextTick
                || value.EndsAtTick < nextTick
                || value.Positions.IsDefaultOrEmpty
                || value.Positions.Distinct().Count() != value.Positions.Length
                || value.RemainingCapacity < 0))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Arc Relay signature state is invalid.");
        }
    }

    /// <summary>
    /// The team a signed side-objective claim belongs to: positive counts for
    /// team 0 and negative for team 1, the direction the public team-advance
    /// ordering uses. Zero is no claim at all.
    /// </summary>
    private static int? SecondaryClaimant(int claimProgress) =>
        claimProgress switch
        {
            0 => null,
            > 0 => 0,
            _ => 1,
        };

    private static void ValidateEventKindAndPayload(
        string kind,
        ReplayV3.EventPayload payload,
        string context)
    {
        string expectedPayloadKind = kind switch
        {
            "rotation" => "rotation",
            "movement" => "movement",
            "movement-blocked" => "movement-blocked",
            "attack" => "attack",
            "damage" => "damage",
            "destruction" => "destruction",
            "life-spawned" => "life-spawned",
            "life-retired" => "life-retired",
            "runtime-fault" => "runtime-fault",
            "mind-runtime-fault" => "mind-runtime-fault",
            "participant-disqualified" => "participant",
            "lifecycle-queued"
                or "lifecycle-cancelled"
                or "lifecycle-completed" => "lifecycle",
            "form-transition-started"
                or "form-transition-completed"
                or "form-transition-cancelled" => "form-transition",
            "score-changed" => "score-changed",
            "mode-changed" => "mode-changed",
            "lifecycle-clock-cancelled" =>
                "lifecycle-clock-cancelled",
            "projectile-deflected" => "projectile-deflected",
            "arc-relay" => "arc-relay",
            _ => throw new ArgumentException(
                $"Replay-v3 {context} event kind '{kind}' is invalid."),
        };
        if (!string.Equals(
                expectedPayloadKind,
                payload.Kind,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} event kind and payload kind disagree.");
        }

        switch (payload)
        {
            case ReplayV3.EventPayload.Rotation rotation:
                if (!IsDirection(rotation.FromFacing)
                    || !IsDirection(rotation.ToFacing))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} rotation facing is invalid.");
                }
                break;
            case ReplayV3.EventPayload.Movement movement:
                if (!IsDirection(movement.Facing))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} movement facing is invalid.");
                }
                break;
            case ReplayV3.EventPayload.MovementBlocked blocked:
                if (!IsDirection(blocked.Facing))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} blocked movement facing is invalid.");
                }
                break;
            case ReplayV3.EventPayload.Attack attack:
                if (!IsProjectileHeading(attack.Heading))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} attack heading is invalid.");
                }
                break;
            case ReplayV3.EventPayload.ProjectileDeflected deflected:
                if (!IsProjectileHeading(deflected.Heading)
                    || !IsDirection(deflected.TargetFacing))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} deflected-projectile heading or facing is invalid.");
                }
                break;
            case ReplayV3.EventPayload.FormTransition transition:
                // Absent means requested. An explicit "requested" would be a
                // second encoding of the same history, so it is refused here
                // exactly as the contract mirrors refuse an inert guard.
                if (transition.Reason is string reason
                    && !IsFormTransitionReason(reason))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} form-transition reason is invalid; " +
                        "a requested transition omits the property.");
                }
                break;
            case ReplayV3.EventPayload.LifeSpawned spawned:
                if (!IsSpawnReason(spawned.Reason))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} spawn reason is invalid.");
                }
                break;
            case ReplayV3.EventPayload.LifeRetired retired:
                if (!IsRetirementReason(retired.Reason))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} retirement reason is invalid.");
                }
                break;
            case ReplayV3.EventPayload.RuntimeFaultValue runtimeFault:
                if (!IsFaultStage(runtimeFault.Fault.Stage))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} runtime-fault stage is invalid.");
                }
                break;
            case ReplayV3.EventPayload.MindRuntimeFaultValue mindFault:
                if (!IsFaultStage(mindFault.Fault.Stage)
                    || !IsCanonicalInt64(
                        mindFault.Fault.CumulativeFaultCount,
                        nonNegative: true))
                {
                    throw new ArgumentException(
                        $"Replay-v3 {context} mind-runtime-fault evidence is invalid.");
                }
                if (mindFault.Fault.ActorId is not null)
                {
                    // The mind-scoped event exists ONLY for the fault with no
                    // body to attribute it to; a fault that HAS a body keeps
                    // publishing one per-body event, so a document carrying
                    // both encodings for the same fault is malformed.
                    throw new ArgumentException(
                        $"Replay-v3 {context} mind-runtime-fault must carry no actor identity.");
                }
                break;
            case ReplayV3.EventPayload.ArcRelay arcRelay:
                ValidateArcRelayFact(arcRelay.Fact, context);
                break;
        }
    }

    private static void ValidateArcRelayFact(
        ReplayV3.ArcRelayFact fact,
        string context)
    {
        ArgumentNullException.ThrowIfNull(fact);
        static bool InvalidCoreId(ReplayV3.ArcCoreId value) =>
            string.IsNullOrWhiteSpace(value.SourceWellId)
            || value.SourceOrdinal < 0;
        bool invalid = fact switch
        {
            ReplayV3.ArcRelayFact.CoreBorn value =>
                InvalidCoreId(value.CoreId)
                || value.ChargeValue < 1,
            ReplayV3.ArcRelayFact.CoreRipened value =>
                InvalidCoreId(value.CoreId)
                || value.Value < 2,
            ReplayV3.ArcRelayFact.LeveledUp value =>
                value.Level < 2,
            ReplayV3.ArcRelayFact.ZoneHealed value =>
                value.Amount < 1 || value.NewHealth < 1,
            ReplayV3.ArcRelayFact.CorePickedUp value =>
                InvalidCoreId(value.CoreId)
                || value.NextRelocationTick < 0,
            ReplayV3.ArcRelayFact.CoreRelocated value =>
                InvalidCoreId(value.CoreId)
                || value.From == value.To
                || value.NextRelocationTick < 0
                || value.RelocationKind is not (
                    "carried-movement" or "forced-displacement"
                    or "arc-toss-landing"),
            ReplayV3.ArcRelayFact.CoreHandedOff value =>
                InvalidCoreId(value.CoreId)
                || value.SourceActorId.TeamId != value.TargetActorId.TeamId
                || value.SourceActorId == value.TargetActorId
                || value.NextRelocationTick < 0,
            ReplayV3.ArcRelayFact.CoreDropped value =>
                InvalidCoreId(value.CoreId)
                || value.NextRelocationTick < 0
                || value.DropKind is not (
                    "voluntary" or "destruction" or "signature-departure"
                    or "arc-toss-landing"),
            ReplayV3.ArcRelayFact.CoreBanked value =>
                InvalidCoreId(value.CoreId)
                || value.CarrierActorId.TeamId != value.TeamId
                || value.ChargePips < 0,
            ReplayV3.ArcRelayFact.WellChanged value =>
                string.IsNullOrWhiteSpace(value.WellId)
                || value.RearmCompletesAtTick < 0
                || value.OutstandingCoreId is { } coreId
                    && InvalidCoreId(coreId),
            ReplayV3.ArcRelayFact.Pulse value =>
                value.TeamId < 0
                || value.PulseOrdinal <= 0
                || value.OpposingReactorIntegrity < 0,
            ReplayV3.ArcRelayFact.SignatureChanged value =>
                string.IsNullOrWhiteSpace(value.OperationId)
                || string.IsNullOrWhiteSpace(value.SignatureId)
                || value.Phase is not null
                    && value.Phase is not (
                        "tell" or "active" or "channel" or "in-flight")
                || string.IsNullOrWhiteSpace(value.Reason),
            ReplayV3.ArcRelayFact.BodyRelocated value =>
                string.IsNullOrWhiteSpace(value.OperationId)
                || string.IsNullOrWhiteSpace(value.SignatureId)
                || value.From == value.To,
            ReplayV3.ArcRelayFact.SignatureDamage value =>
                InvalidArcSignatureHealthFact(value.OperationId,
                    value.SignatureId, value.Amount, value.NewHealth),
            ReplayV3.ArcRelayFact.SignatureRepair value =>
                InvalidArcSignatureHealthFact(value.OperationId,
                    value.SignatureId, value.Amount, value.NewHealth),
            _ => true,
        };
        if (invalid)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} Arc Relay fact is invalid.");
        }
    }

    private static bool InvalidArcSignatureHealthFact(
        string operationId,
        string signatureId,
        int amount,
        int newHealth) =>
        string.IsNullOrWhiteSpace(operationId)
        || string.IsNullOrWhiteSpace(signatureId)
        || amount <= 0
        || newHealth < 0;

    private static void ValidateClosedVocabulary(ReplayV3 replay)
    {
        IEnumerable<ReplayV3.ModeState> modes =
        [
            replay.InitialFrame.State.Mode,
            .. replay.Ticks.Select(frame => frame.TickStart.State.Mode),
            .. replay.Ticks.Select(frame => frame.PostState.Mode),
            .. replay.Ticks.SelectMany(frame =>
                frame.ActorTurns.IsDefaultOrEmpty
                    ? []
                    : frame.ActorTurns.Select(
                        turn => turn.Observation.Mode)),
            .. replay.Ticks.SelectMany(frame =>
                frame.MindTurns.IsDefaultOrEmpty
                    ? []
                    : frame.MindTurns.Select(
                        turn => turn.Observation.Mode)),
        ];
        foreach (ReplayV3.ModeState mode in modes)
        {
            ArgumentNullException.ThrowIfNull(mode);
            ArgumentException.ThrowIfNullOrWhiteSpace(mode.ModeId);
        }

        if (replay.Result is
            {
                Mode:
                ReplayV3.ModeResult.Deathmatch deathmatch
            }
            && !IsDeathmatchEndReason(deathmatch.Reason))
        {
            throw new ArgumentException(
                "Replay-v3 deathmatch result reason is invalid.");
        }
        if (replay.Result is
            {
                Mode:
                ReplayV3.ModeResult.Frontline frontline
            }
            && !IsFrontlineEndReason(frontline.Reason))
        {
            throw new ArgumentException(
                "Replay-v3 Frontline result reason is invalid.");
        }
        if (replay.Result is
            {
                Mode:
                ReplayV3.ModeResult.ArcRelay arcRelay
            }
            && !IsArcRelayEndReason(arcRelay.Reason))
        {
            throw new ArgumentException(
                "Replay-v3 Arc Relay result reason is invalid.");
        }
    }

    private static void ValidateActorIds(
        ImmutableArray<ReplayV3.ActorId> actorIds,
        string context) =>
        RequireCanonicalOrder(
            actorIds,
            CompareActorId,
            context);

    private static void RequireInitialized<T>(
        ImmutableArray<T> values,
        string context)
    {
        if (values.IsDefault)
        {
            throw new ArgumentException(
                $"Replay-v3 {context} must be initialized.");
        }
        foreach (T value in values)
        {
            if (value is null)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} cannot contain null.");
            }
        }
    }

    private static void RequireCanonicalOrder<T>(
        ImmutableArray<T> values,
        Func<T, T, int> compare,
        string context)
    {
        RequireInitialized(values, context);
        for (int index = 1; index < values.Length; index++)
        {
            if (compare(values[index - 1], values[index]) >= 0)
            {
                throw new ArgumentException(
                    $"Replay-v3 {context} must be unique and in canonical order.");
            }
        }
    }

    private static int CompareActorId(
        ReplayV3.ActorId left,
        ReplayV3.ActorId right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        int team = left.TeamId.CompareTo(right.TeamId);
        if (team != 0)
            return team;
        int unit = left.UnitId.CompareTo(right.UnitId);
        return unit != 0
            ? unit
            : left.LifeId.CompareTo(right.LifeId);
    }

    private static int CompareUnitKey(
        int leftTeamId,
        int leftUnitId,
        int rightTeamId,
        int rightUnitId)
    {
        int team = leftTeamId.CompareTo(rightTeamId);
        return team != 0
            ? team
            : leftUnitId.CompareTo(rightUnitId);
    }

    private static int ComparePosition(
        ReplayV3.PositionValue left,
        ReplayV3.PositionValue right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        int y = left.Y.CompareTo(right.Y);
        return y != 0 ? y : left.X.CompareTo(right.X);
    }

    private static long ParseNonNegativeInt64(string value)
    {
        if (!IsCanonicalInt64(value, nonNegative: true))
        {
            throw new ArgumentException(
                "Replay-v3 canonical decimal identifier is invalid.");
        }
        return long.Parse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture);
    }

    private static long ParseSignedInt64(string value)
    {
        if (!IsCanonicalInt64(value, nonNegative: false))
        {
            throw new ArgumentException(
                "Replay-v3 canonical signed decimal value is invalid.");
        }
        return long.Parse(
            value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);
    }

    private static void RequireExactObject(
        JsonElement value,
        string context,
        params string[] expectedProperties)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject()
                .Select(property => property.Name)
                .SequenceEqual(expectedProperties))
        {
            throw new ArgumentException(
                $"Replay-v3 {context} does not have its exact canonical object shape.");
        }
    }

    private static JsonElement RequiredObject(
        JsonElement value,
        string propertyName)
    {
        JsonElement result = value.GetProperty(propertyName);
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"Replay-v3 '{propertyName}' must be an object.");
        }
        return result;
    }

    private static JsonElement RequiredArray(
        JsonElement value,
        string propertyName)
    {
        JsonElement result = value.GetProperty(propertyName);
        if (result.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException(
                $"Replay-v3 '{propertyName}' must be an array.");
        }
        return result;
    }

    private static bool IsDirection(string value) =>
        value is "north" or "east" or "south" or "west";

    private static bool IsProjectileHeading(string value) =>
        value is "north"
            or "north-east"
            or "east"
            or "south-east"
            or "south"
            or "south-west"
            or "west"
            or "north-west";

    private static bool IsSpawnReason(string value) =>
        value is "initial"
            or "automatic-return"
            or "fabrication"
            or "replication"
            or "automatic-activation"
            or "root-factory-seed";

    /// <summary>
    /// "requested" is deliberately absent: the inert cause is encoded by
    /// omitting the property, so naming it explicitly is refused.
    /// </summary>
    private static bool IsFormTransitionReason(string value) =>
        value is "automatic-threshold-return";

    private static bool IsRetirementReason(string value) =>
        value is "replication" or "participant-disqualified";

    private static bool IsAvailabilityReason(string value) =>
        value is "initial-unlock" or "destruction-recovery";

    private static bool IsActionOutcome(string value) =>
        value is "success" or "blocked" or "rejected" or "faulted";

    private static bool IsFaultStage(string value) =>
        value is "runtime-create"
            or "life-start"
            or "tick-execution"
            or "decision-validation";

    private static bool IsTraversalPhase(string value) =>
        value is "tick-start" or "resolution";

    private static bool IsTraversalTrigger(string value) =>
        value is "lifecycle-placement"
            or "movement-contact"
            or "scheduled-advance"
            or "attack-launch"
            or "guard-deflection"
            or "participant-disqualification";

    private static bool IsStandingOutcome(string value) =>
        value is "win" or "loss" or "draw";

    private static bool IsDeathmatchEndReason(string value) =>
        value is "fault-eligibility" or "kill-limit" or "max-ticks";

    private static bool IsFrontlineEndReason(string value) =>
        value is "fault-eligibility" or "base-breach" or "max-ticks";

    private static bool IsArcRelayEndReason(string value) =>
        value is "fault-eligibility" or "reactor-destroyed" or "max-ticks";

    private sealed record CanonicalContractIndex(
        string Fingerprint,
        string SeedProfileId,
        ImmutableArray<int> TeamIds,
        ImmutableArray<ContractParticipant> Participants,
        ImmutableArray<ContractUnitSlot> UnitSlots,
        ImmutableArray<string> ScoreChannels,
        ImmutableArray<ContractAction> Actions,
        ImmutableArray<ContractAttackProfile> AttackProfiles,
        ImmutableArray<ContractReplication> Replications,
        ImmutableArray<ContractPermanentReservation>
            PermanentReservations,
        ContractMode Mode);

    private sealed record ContractParticipant(
        int ParticipantId,
        int TeamId,
        string? ClassId);

    private sealed record ContractTeam(
        int TeamId,
        string? ClassId);

    private sealed record ContractUnitSlot(
        int TeamId,
        int UnitId,
        int ParticipantId,
        string? ClassId = null);

    private sealed record ContractAction(
        string Id,
        int Code,
        ImmutableArray<string> ParameterKinds,
        bool AllowsOmittedArguments);

    private sealed record ContractAttackProfile(
        string Id,
        int TilesPerAdvance,
        int TicksPerAdvance,
        int DamagePerHit);

    private sealed record ContractPermanentReservation(
        int TeamId,
        int UnitId,
        ReplayV3.PositionValue Position);

    private sealed record ContractReplication(
        string TransitionId,
        ImmutableArray<string> SourceFormIds,
        string OutputFormId,
        int DescendantCount,
        int MaxSourceGeneration,
        int DurationTicks,
        ImmutableArray<ContractOffset> CandidateOffsets);

    private sealed record ContractOffset(int Forward, int Right);

    private sealed record ContractMode(
        string Kind,
        string ModeId,
        int MaxTicks,
        int? KillsToWin,
        ImmutableArray<ContractRanking> TimeoutRanking,
        ContractFrontline? Frontline,
        ContractArcRelay? ArcRelay);

    private sealed record ContractRanking(
        string Channel,
        string Direction);

    private sealed record ContractFrontline(
        int PositionCount,
        int CaptureThreshold,
        int DecayAmount,
        int DecayIntervalTicks,
        int RedeployPauseTicks,
        ImmutableArray<ContractTeamAdvance> TeamAdvances,
        bool Ratchet,
        int RatchetHoldTicks,
        bool SecondaryControl,
        int SecondaryCaptureThresholdTicks);

    private sealed record ContractTeamAdvance(
        int TeamId,
        int ObjectiveIndexDelta);

    private sealed record ContractArcRelay(
        int PulsesToDestroyReactor,
        int CoresPerPulse,
        int CoreRelocationIntervalTicks,
        int TripNodeRevealRange,
        ImmutableArray<string> WellIds);

    private static void ValidatePresentation(
        ReplayV3.PresentationMetadata? presentation)
    {
        if (presentation is null)
            return;

        ValidateOptionalPresentationId(
            presentation.ThemeId,
            "themeId");
        if (presentation.Forms.IsDefault)
        {
            throw new ArgumentException(
                "Replay-v3 presentation forms must be initialized.");
        }
        string? previousFormId = null;
        foreach (ReplayV3.FormPresentationMetadata form in
                 presentation.Forms)
        {
            ArgumentNullException.ThrowIfNull(form);
            ValidatePresentationId(form.FormId, "formId");
            ValidateOptionalPresentationId(form.LookId, "lookId");
            ValidateOptionalPresentationId(
                form.ProjectileLookId,
                "projectileLookId");
            if (previousFormId is not null
                && StringComparer.Ordinal.Compare(
                    previousFormId,
                    form.FormId) >= 0)
            {
                throw new ArgumentException(
                    "Replay-v3 presentation forms must have unique ordinally sorted form ids.");
            }
            previousFormId = form.FormId;
        }

        if (presentation.Map is not { } map)
            return;
        ValidatePresentationId(map.BoundaryWall, "boundaryWall");
        ValidatePresentationId(map.InteriorWall, "interiorWall");
        if (map.WallGroups.IsDefault)
        {
            throw new ArgumentException(
                "Replay-v3 presentation wall groups must be initialized.");
        }

        string? previousFamily = null;
        var claimedTiles = new HashSet<(int X, int Y)>();
        foreach (ReplayV3.WallGroupPresentationMetadata group in
                 map.WallGroups)
        {
            ArgumentNullException.ThrowIfNull(group);
            ValidatePresentationId(group.Family, "family");
            if (previousFamily is not null
                && StringComparer.Ordinal.Compare(
                    previousFamily,
                    group.Family) >= 0)
            {
                throw new ArgumentException(
                    "Replay-v3 presentation wall groups must have unique ordinally sorted families.");
            }
            previousFamily = group.Family;
            if (group.Tiles.IsDefault)
            {
                throw new ArgumentException(
                    "Replay-v3 presentation tiles must be initialized.");
            }

            (int X, int Y)? previousTile = null;
            foreach (ReplayV3.PositionValue tile in group.Tiles)
            {
                ArgumentNullException.ThrowIfNull(tile);
                var current = (tile.X, tile.Y);
                if (previousTile is { } prior
                    && (prior.Y > current.Y
                        || prior.Y == current.Y
                        && prior.X >= current.X))
                {
                    throw new ArgumentException(
                        "Replay-v3 presentation tiles must be unique and sorted by y then x.");
                }
                if (!claimedTiles.Add(current))
                {
                    throw new ArgumentException(
                        "A replay-v3 presentation tile cannot belong to multiple wall groups.");
                }
                previousTile = current;
            }
        }
    }

    private static void ValidatePresentationId(
        string value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"Replay-v3 presentation '{name}' cannot be blank.");
        }
    }

    private static void ValidateOptionalPresentationId(
        string? value,
        string name)
    {
        if (value is not null)
            ValidatePresentationId(value, name);
    }

    private static bool HasCanonicalWireIntegers(
        JsonElement root,
        out string? failure)
    {
        bool valid = ValidateWireElement(
            root,
            parentPropertyName: null,
            out string? propertyName);
        failure = valid
            ? null
            : $"Replay-v3 '{propertyName}' must use a canonical decimal-safe string.";
        return valid;
    }

    private static bool ValidateWireElement(
        JsonElement element,
        string? parentPropertyName,
        out string? invalidPropertyName)
    {
        invalidPropertyName = null;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    bool scoreObject = element.TryGetProperty(
                        "channel",
                        out _);
                    // A reserved inter-mind intent carries an int64 in the
                    // same "value" key a score does; both must stay
                    // decimal-safe strings rather than widen to a float.
                    bool intentObject = element.TryGetProperty(
                        "tagId",
                        out _);
                    foreach (JsonProperty property in
                             element.EnumerateObject())
                    {
                        if (property.NameEquals("contract")
                            && parentPropertyName == "header")
                        {
                            continue;
                        }

                        bool valid = property.Name switch
                        {
                            "seed"
                                or "actorRandomSeed"
                                or "teamRandomSeed"
                                or "bodyRandomSeed" =>
                                property.Value.ValueKind
                                    == JsonValueKind.String
                                && IsCanonicalUInt64(
                                    property.Value.GetString()),
                            "runtimeFaultCount"
                                or "cumulativeFaultCount"
                                or "fuelBudget"
                                or "globalOrdinal"
                                or "projectileId"
                                or "nextProjectileId"
                                or "kills"
                                or "deaths"
                                or "damageDealt" =>
                                property.Value.ValueKind == JsonValueKind.Null
                                || property.Value.ValueKind
                                    == JsonValueKind.String
                                && IsCanonicalInt64(
                                    property.Value.GetString(),
                                    nonNegative: true),
                            "newValue" =>
                                property.Value.ValueKind
                                    == JsonValueKind.String
                                && IsCanonicalInt64(
                                    property.Value.GetString(),
                                    nonNegative: false),
                            "territorialProgress" =>
                                property.Value.ValueKind
                                    == JsonValueKind.String
                                && IsCanonicalInt64(
                                    property.Value.GetString(),
                                    nonNegative: false),
                            "value" when scoreObject || intentObject =>
                                property.Value.ValueKind
                                    == JsonValueKind.String
                                && IsCanonicalInt64(
                                    property.Value.GetString(),
                                    nonNegative: false),
                            _ => true,
                        };
                        if (!valid)
                        {
                            invalidPropertyName = property.Name;
                            return false;
                        }
                        if (!ValidateWireElement(
                                property.Value,
                                property.Name,
                                out invalidPropertyName))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (!ValidateWireElement(
                            item,
                            parentPropertyName,
                            out invalidPropertyName))
                    {
                        return false;
                    }
                }
                return true;
            default:
                return true;
        }
    }

    private static bool IsCanonicalUInt64(string? value) =>
        value is not null
        && ulong.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out ulong parsed)
        && string.Equals(
            value,
            parsed.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private static bool IsCanonicalInt64(
        string? value,
        bool nonNegative) =>
        value is not null
        && long.TryParse(
            value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out long parsed)
        && (!nonNegative || parsed >= 0)
        && string.Equals(
            value,
            parsed.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private static bool IsLowercaseSha256(string value) =>
        value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

    private static JsonSerializerOptions CreateReadOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowDuplicateProperties = false,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new ResolvedContractJsonConverter());
        options.Converters.Add(new RawActionArgumentJsonConverter());
        options.Converters.Add(new ActionArgumentJsonConverter());
        options.Converters.Add(new ModeStateJsonConverter());
        options.Converters.Add(
            new TaggedUnionJsonConverter<ReplayV3.UnitSlotState>(
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["active"] =
                        typeof(ReplayV3.UnitSlotState.Active),
                    ["availability-pending"] =
                        typeof(ReplayV3.UnitSlotState.AvailabilityPending),
                    ["automatic-return-pending"] =
                        typeof(ReplayV3.UnitSlotState.AutomaticReturnPending),
                    ["ready"] =
                        typeof(ReplayV3.UnitSlotState.Ready),
                    ["fabrication-pending"] =
                        typeof(ReplayV3.UnitSlotState.FabricationPending),
                    ["replication-pending"] =
                        typeof(ReplayV3.UnitSlotState.ReplicationPending),
                    ["permanently-dormant"] =
                        typeof(ReplayV3.UnitSlotState.PermanentlyDormant),
                }));
        options.Converters.Add(
            new TaggedUnionJsonConverter<ReplayV3.ActionConstraint>(
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["shot-program"] =
                        typeof(ReplayV3.ActionConstraint.ShotProgram),
                    ["direction"] =
                        typeof(ReplayV3.ActionConstraint.Direction),
                    ["unit-target"] =
                        typeof(ReplayV3.ActionConstraint.UnitTarget),
                    ["form-target"] =
                        typeof(ReplayV3.ActionConstraint.FormTarget),
                    ["projectile-heading"] =
                        typeof(
                            ReplayV3.ActionConstraint.ProjectileHeading),
                    ["upgrade-track"] =
                        typeof(ReplayV3.ActionConstraint.UpgradeTrack),
                    ["position-target"] =
                        typeof(ReplayV3.ActionConstraint.PositionTarget),
                }));
        options.Converters.Add(
            new TaggedUnionJsonConverter<ReplayV3.EventPayload>(
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["rotation"] =
                        typeof(ReplayV3.EventPayload.Rotation),
                    ["movement"] =
                        typeof(ReplayV3.EventPayload.Movement),
                    ["movement-blocked"] =
                        typeof(ReplayV3.EventPayload.MovementBlocked),
                    ["attack"] =
                        typeof(ReplayV3.EventPayload.Attack),
                    ["damage"] =
                        typeof(ReplayV3.EventPayload.Damage),
                    ["destruction"] =
                        typeof(ReplayV3.EventPayload.Destruction),
                    ["life-spawned"] =
                        typeof(ReplayV3.EventPayload.LifeSpawned),
                    ["life-retired"] =
                        typeof(ReplayV3.EventPayload.LifeRetired),
                    ["runtime-fault"] =
                        typeof(ReplayV3.EventPayload.RuntimeFaultValue),
                    ["mind-runtime-fault"] =
                        typeof(
                            ReplayV3.EventPayload.MindRuntimeFaultValue),
                    ["participant"] =
                        typeof(ReplayV3.EventPayload.Participant),
                    ["lifecycle"] =
                        typeof(ReplayV3.EventPayload.Lifecycle),
                    ["form-transition"] =
                        typeof(ReplayV3.EventPayload.FormTransition),
                    ["score-changed"] =
                        typeof(ReplayV3.EventPayload.ScoreChanged),
                    ["mode-changed"] =
                        typeof(ReplayV3.EventPayload.ModeChanged),
                    ["lifecycle-clock-cancelled"] =
                        typeof(
                            ReplayV3.EventPayload
                                .LifecycleClockCancelled),
                    ["projectile-deflected"] =
                        typeof(ReplayV3.EventPayload.ProjectileDeflected),
                    ["arc-relay"] =
                        typeof(ReplayV3.EventPayload.ArcRelay),
                }));
        options.Converters.Add(
            new TaggedUnionJsonConverter<ReplayV3.ArcRelayFact>(
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["core-born"] =
                        typeof(ReplayV3.ArcRelayFact.CoreBorn),
                    ["core-ripened"] =
                        typeof(ReplayV3.ArcRelayFact.CoreRipened),
                    ["leveled-up"] =
                        typeof(ReplayV3.ArcRelayFact.LeveledUp),
                    ["zone-healed"] =
                        typeof(ReplayV3.ArcRelayFact.ZoneHealed),
                    ["core-picked-up"] =
                        typeof(ReplayV3.ArcRelayFact.CorePickedUp),
                    ["core-relocated"] =
                        typeof(ReplayV3.ArcRelayFact.CoreRelocated),
                    ["core-handed-off"] =
                        typeof(ReplayV3.ArcRelayFact.CoreHandedOff),
                    ["core-dropped"] =
                        typeof(ReplayV3.ArcRelayFact.CoreDropped),
                    ["core-banked"] =
                        typeof(ReplayV3.ArcRelayFact.CoreBanked),
                    ["well-changed"] =
                        typeof(ReplayV3.ArcRelayFact.WellChanged),
                    ["pulse"] =
                        typeof(ReplayV3.ArcRelayFact.Pulse),
                    ["signature-changed"] =
                        typeof(ReplayV3.ArcRelayFact.SignatureChanged),
                    ["body-relocated"] =
                        typeof(ReplayV3.ArcRelayFact.BodyRelocated),
                    ["signature-damage"] =
                        typeof(ReplayV3.ArcRelayFact.SignatureDamage),
                    ["signature-repair"] =
                        typeof(ReplayV3.ArcRelayFact.SignatureRepair),
                }));
        options.Converters.Add(
            new TaggedUnionJsonConverter<ReplayV3.EventAudience>(
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["public"] =
                        typeof(ReplayV3.EventAudience.Public),
                    ["spatial"] =
                        typeof(ReplayV3.EventAudience.Spatial),
                    ["team-private"] =
                        typeof(ReplayV3.EventAudience.TeamPrivate),
                }));
        options.Converters.Add(
            new TaggedUnionJsonConverter<ReplayV3.TraversalTerminal>(
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["retained"] =
                        typeof(ReplayV3.TraversalTerminal.Retained),
                    ["wall-or-path-exhausted"] =
                        typeof(
                            ReplayV3.TraversalTerminal
                                .WallOrPathExhausted),
                    ["range-exhausted"] =
                        typeof(
                            ReplayV3.TraversalTerminal.RangeExhausted),
                    ["actor-contact"] =
                        typeof(
                            ReplayV3.TraversalTerminal.ActorContact),
                    ["movement-contact"] =
                        typeof(
                            ReplayV3.TraversalTerminal.MovementContact),
                    ["lifecycle-placement-purge"] =
                        typeof(
                            ReplayV3.TraversalTerminal
                                .LifecyclePlacementPurge),
                    ["participant-disqualification"] =
                        typeof(
                            ReplayV3.TraversalTerminal
                                .ParticipantDisqualification),
                }));
        options.Converters.Add(new ModeResultJsonConverter());
        return options;
    }

    /// <summary>
    /// One live economy ledger, read from the document. Amounts and expiries
    /// are re-derived by the chronology validator, so this reader's job is
    /// exactly shape.
    /// </summary>
    private static ImmutableArray<ReplayV3.ScrapTeam> ReadScrapTeams(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                "Replay-v3 'scrapTeams' must be an array.");
        }
        var teams = ImmutableArray.CreateBuilder<ReplayV3.ScrapTeam>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            RequireProperties(item, "teamId", "bank", "tierLevels");
            JsonElement tiers = item.GetProperty("tierLevels");
            if (tiers.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(
                    "Replay-v3 'tierLevels' must be an array.");
            }
            teams.Add(new ReplayV3.ScrapTeam(
                RequiredInt32(item, "teamId"),
                RequiredInt32(item, "bank"),
                [
                    .. tiers.EnumerateArray().Select(tier =>
                        tier.ValueKind == JsonValueKind.Number
                        && tier.TryGetInt32(out int level)
                            ? level
                            : throw new JsonException(
                                "Replay-v3 tier level must be an int32.")),
                ]));
        }
        return teams.ToImmutable();
    }

    private static ImmutableArray<ReplayV3.ScrapPile> ReadScrapPiles(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                "Replay-v3 'scrapPiles' must be an array.");
        }
        var piles = ImmutableArray.CreateBuilder<ReplayV3.ScrapPile>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            RequireProperties(item, "position", "amount", "expiresAtTick");
            JsonElement position = item.GetProperty("position");
            RequireProperties(position, "x", "y");
            piles.Add(new ReplayV3.ScrapPile(
                new ReplayV3.PositionValue(
                    RequiredInt32(position, "x"),
                    RequiredInt32(position, "y")),
                RequiredInt32(item, "amount"),
                RequiredInt32(item, "expiresAtTick")));
        }
        return piles.ToImmutable();
    }

    private static void RequireProperties(
        JsonElement value,
        params string[] names)
    {
        if (!value.EnumerateObject()
                .Select(property => property.Name)
                .SequenceEqual(names))
        {
            throw new JsonException(
                $"Replay-v3 object must contain exactly: {string.Join(", ", names)}.");
        }
    }

    private static string RequiredString(
        JsonElement value,
        string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.String
            ? property.GetString()!
            : throw new JsonException(
                $"Replay-v3 '{propertyName}' must be a string.");
    }

    private static string? NullableString(
        JsonElement value,
        string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => property.GetString(),
            _ => throw new JsonException(
                $"Replay-v3 '{propertyName}' must be string or null."),
        };
    }

    private static int RequiredInt32(
        JsonElement value,
        string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out int result)
                ? result
                : throw new JsonException(
                    $"Replay-v3 '{propertyName}' must be an Int32.");
    }

    /// <summary>
    /// Reads a nullable Int32 whose PROPERTY is mandatory: an explicit null
    /// is the value, an absent property is a malformed document. This is the
    /// shape every nullable mode fact uses (<c>claimingTeamId</c> and, since
    /// DECISIONS #169, the two ratchet-hold clocks).
    /// </summary>
    private static int? NullableInt32(
        JsonElement value,
        string propertyName)
    {
        JsonElement property = value.GetProperty(propertyName);
        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Number when property.TryGetInt32(out int result) =>
                result,
            _ => throw new JsonException(
                $"Replay-v3 '{propertyName}' must be Int32 or null."),
        };
    }

    private static T RequiredValue<T>(
        JsonElement value,
        string propertyName,
        JsonSerializerOptions options) where T : class =>
        value.GetProperty(propertyName).Deserialize<T>(options)
        ?? throw new JsonException(
            $"Replay-v3 '{propertyName}' cannot be null.");

    private sealed class ResolvedContractJsonConverter
        : JsonConverter<ReplayV3.ResolvedContract>
    {
        public override ReplayV3.ResolvedContract Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document =
                JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("Replay-v3 contract must be an object.");

            string canonicalJson = root.GetRawText();
            ActorContractProfileAdmission.ValidateCanonicalMatch(
                canonicalJson);
            return new ReplayV3.ResolvedContract(
                RequiredInt32(root, "schemaVersion"),
                RequiredString(
                    root,
                    "matchContractFingerprint"),
                canonicalJson);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReplayV3.ResolvedContract value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Replay-v3 uses its explicit canonical writer.");
    }

    private sealed class RawActionArgumentJsonConverter
        : JsonConverter<ReplayV3.RawActionArgument>
    {
        public override ReplayV3.RawActionArgument Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document =
                JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    "Raw replay-v3 action argument must be an object.");
            }

            string kind = RequiredString(root, "kind");
            switch (kind)
            {
                case "shot-program":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.RawActionArgument.ShotProgram(
                        RequiredValue<ReplayV3.ShotProgramValue>(
                            root,
                            "value",
                            options));
                case "direction":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.RawActionArgument.Direction(
                        RequiredInt32(root, "value"));
                case "unit-target":
                    {
                        RequireProperties(root, "kind", "value");
                        JsonElement target = root.GetProperty("value");
                        if (target.ValueKind != JsonValueKind.Object)
                        {
                            throw new JsonException(
                                "Raw replay-v3 unit target must be an object.");
                        }
                        RequireProperties(target, "teamId", "unitId");
                        return new ReplayV3.RawActionArgument.UnitTarget(
                            RequiredInt32(target, "teamId"),
                            RequiredInt32(target, "unitId"));
                    }
                case "form-target":
                    RequireProperties(root, "kind", "formId");
                    return new ReplayV3.RawActionArgument.FormTarget(
                        NullableString(root, "formId"));
                case "projectile-heading":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.RawActionArgument
                        .ProjectileHeading(
                            RequiredInt32(root, "value"));
                case "upgrade-track":
                    RequireProperties(root, "kind", "trackId");
                    return new ReplayV3.RawActionArgument.UpgradeTrack(
                        NullableString(root, "trackId"));
                case "position-target":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.RawActionArgument.PositionTarget(
                        RequiredValue<ReplayV3.PositionValue>(
                            root,
                            "value",
                            options));
                default:
                    throw new JsonException(
                        $"Unknown raw replay-v3 action argument kind '{kind}'.");
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReplayV3.RawActionArgument value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Replay-v3 uses its explicit canonical writer.");
    }

    private sealed class ActionArgumentJsonConverter
        : JsonConverter<ReplayV3.ActionArgument>
    {
        public override ReplayV3.ActionArgument Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document =
                JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    "Resolved replay-v3 action argument must be an object.");
            }

            string kind = RequiredString(root, "kind");
            switch (kind)
            {
                case "shot-program":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.ActionArgument.ShotProgram(
                        RequiredValue<ReplayV3.ShotProgramValue>(
                            root,
                            "value",
                            options));
                case "direction":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.ActionArgument.Direction(
                        RequiredString(root, "value"));
                case "unit-target":
                    {
                        RequireProperties(root, "kind", "value");
                        JsonElement target = root.GetProperty("value");
                        if (target.ValueKind != JsonValueKind.Object)
                        {
                            throw new JsonException(
                                "Resolved replay-v3 unit target must be an object.");
                        }
                        RequireProperties(target, "teamId", "unitId");
                        return new ReplayV3.ActionArgument.UnitTarget(
                            RequiredInt32(target, "teamId"),
                            RequiredInt32(target, "unitId"));
                    }
                case "form-target":
                    RequireProperties(root, "kind", "formId");
                    return new ReplayV3.ActionArgument.FormTarget(
                        RequiredString(root, "formId"));
                case "projectile-heading":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.ActionArgument
                        .ProjectileHeading(
                            RequiredString(root, "value"));
                case "upgrade-track":
                    RequireProperties(root, "kind", "trackId");
                    return new ReplayV3.ActionArgument.UpgradeTrack(
                        RequiredString(root, "trackId"));
                case "position-target":
                    RequireProperties(root, "kind", "value");
                    return new ReplayV3.ActionArgument.PositionTarget(
                        RequiredValue<ReplayV3.PositionValue>(
                            root,
                            "value",
                            options));
                default:
                    throw new JsonException(
                        $"Unknown resolved replay-v3 action argument kind '{kind}'.");
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReplayV3.ActionArgument value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Replay-v3 uses its explicit canonical writer.");
    }

    private sealed class ModeStateJsonConverter
        : JsonConverter<ReplayV3.ModeState>
    {
        public override ReplayV3.ModeState Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document =
                JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("Replay-v3 mode must be an object.");

            string kind = RequiredString(root, "kind");
            string modeId = RequiredString(root, "modeId");
            switch (kind)
            {
                case "deathmatch":
                    RequireProperties(root, "kind", "modeId");
                    return new ReplayV3.ModeState.Deathmatch(modeId);
                case "frontline":
                    {
                        // The economy's two collections are TRAILING and
                        // optional: they appear only on a ruleset that
                        // declares an economy, in that exact order, so a
                        // document from before the capability existed reads
                        // byte-identically.
                        bool scrapTeams = root.TryGetProperty(
                            "scrapTeams",
                            out JsonElement teams);
                        bool scrapPiles = root.TryGetProperty(
                            "scrapPiles",
                            out JsonElement piles);
                        RequireProperties(
                            root,
                            [
                                "kind",
                                "modeId",
                                "activePositionIndex",
                                "claimingTeamId",
                                "captureProgress",
                                "decayTicksElapsed",
                                "controlResumesAtTick",
                                "holdOwnerTeamId",
                                "holdEndsAtTick",
                                "secondaryOwnerTeamId",
                                "secondaryClaimProgress",
                                .. scrapTeams
                                    ? new[] { "scrapTeams" }
                                    : [],
                                .. scrapPiles
                                    ? new[] { "scrapPiles" }
                                    : [],
                            ]);
                        return new ReplayV3.ModeState.Frontline(
                            modeId,
                            RequiredInt32(
                                root,
                                "activePositionIndex"),
                            NullableInt32(root, "claimingTeamId"),
                            RequiredInt32(root, "captureProgress"),
                            RequiredInt32(root, "decayTicksElapsed"),
                            RequiredInt32(
                                root,
                                "controlResumesAtTick"),
                            NullableInt32(root, "holdOwnerTeamId"),
                            NullableInt32(root, "holdEndsAtTick"),
                            NullableInt32(root, "secondaryOwnerTeamId"),
                            RequiredInt32(
                                root,
                                "secondaryClaimProgress"),
                            scrapTeams ? ReadScrapTeams(teams) : [],
                            scrapPiles ? ReadScrapPiles(piles) : []);
                    }
                case "arc-relay":
                    {
                        // Declared strikes exist only on strike-windup
                        // rulesets (DECISIONS #212); their absence is the
                        // historical document shape.
                        bool pendingStrikes = root.TryGetProperty(
                            "pendingStrikes", out _);
                        RequireProperties(
                            root,
                            [
                                "kind",
                                "modeId",
                                "wells",
                                "reactors",
                                "visibleCores",
                                "visibleSignatures",
                                "latestPulseTeamId",
                                "latestPulseTick",
                                .. pendingStrikes
                                    ? new[] { "pendingStrikes" }
                                    : System.Array.Empty<string>(),
                            ]);
                        return new ReplayV3.ModeState.ArcRelay(
                            modeId,
                            RequiredArrayValue<ReplayV3.ArcWell>(
                                root, "wells", options),
                            RequiredArrayValue<ReplayV3.ArcReactor>(
                                root, "reactors", options),
                            RequiredArrayValue<ReplayV3.ArcCore>(
                                root, "visibleCores", options),
                            RequiredArrayValue<ReplayV3.ArcSignature>(
                                root, "visibleSignatures", options),
                            NullableInt32(root, "latestPulseTeamId"),
                            NullableInt32(root, "latestPulseTick"))
                        {
                            PendingStrikes = pendingStrikes
                                ? RequiredArrayValue<ReplayV3.ArcPendingStrike>(
                                    root, "pendingStrikes", options)
                                : [],
                        };
                    }
                default:
                    throw new JsonException(
                        $"Unknown replay-v3 mode kind '{kind}'.");
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReplayV3.ModeState value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Replay-v3 uses its explicit canonical writer.");

        private static ImmutableArray<T> RequiredArrayValue<T>(
            JsonElement value,
            string propertyName,
            JsonSerializerOptions options)
        {
            JsonElement property = value.GetProperty(propertyName);
            if (property.ValueKind != JsonValueKind.Array)
                throw new JsonException($"Replay-v3 '{propertyName}' must be an array.");
            ImmutableArray<T> result =
                property.Deserialize<ImmutableArray<T>>(options);
            if (result.IsDefault)
                throw new JsonException($"Replay-v3 '{propertyName}' must be initialized.");
            return result;
        }
    }

    private sealed class ModeResultJsonConverter
        : JsonConverter<ReplayV3.ModeResult>
    {
        public override ReplayV3.ModeResult Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document =
                JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    "Replay-v3 terminal mode result must be an object.");
            }

            string kind = RequiredString(root, "kind");
            switch (kind)
            {
                case "deathmatch":
                    RequireProperties(
                        root,
                        "kind",
                        "reason",
                        "scores");
                    return new ReplayV3.ModeResult.Deathmatch(
                        RequiredString(root, "reason"),
                        RequiredArrayValue<
                            ReplayV3.DeathmatchTeamScore>(
                                root,
                                "scores",
                                options));
                case "frontline":
                    {
                        RequireProperties(
                            root,
                            "kind",
                            "reason",
                            "control",
                            "scores");
                        ReplayV3.ModeState control =
                            root.GetProperty("control")
                                .Deserialize<ReplayV3.ModeState>(
                                    options)
                            ?? throw new JsonException(
                                "Replay-v3 Frontline terminal control cannot be null.");
                        if (control
                            is not ReplayV3.ModeState.Frontline frontline)
                        {
                            throw new JsonException(
                                "Replay-v3 Frontline terminal control must use the Frontline state arm.");
                        }
                        return new ReplayV3.ModeResult.Frontline(
                            RequiredString(root, "reason"),
                            frontline,
                            RequiredArrayValue<
                                ReplayV3.FrontlineTeamScore>(
                                    root,
                                    "scores",
                            options));
                    }
                case "arc-relay":
                    {
                        RequireProperties(root, "kind", "reason", "state");
                        ReplayV3.ModeState state = root.GetProperty("state")
                            .Deserialize<ReplayV3.ModeState>(options)
                            ?? throw new JsonException(
                                "Replay-v3 Arc Relay terminal state cannot be null.");
                        if (state is not ReplayV3.ModeState.ArcRelay arcRelay)
                        {
                            throw new JsonException(
                                "Replay-v3 Arc Relay terminal state must use the Arc Relay state arm.");
                        }
                        return new ReplayV3.ModeResult.ArcRelay(
                            RequiredString(root, "reason"),
                            arcRelay);
                    }
                default:
                    throw new JsonException(
                        $"Unknown replay-v3 terminal mode result kind '{kind}'.");
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReplayV3.ModeResult value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Replay-v3 uses its explicit canonical writer.");

        private static ImmutableArray<T> RequiredArrayValue<T>(
            JsonElement value,
            string propertyName,
            JsonSerializerOptions options)
        {
            JsonElement property = value.GetProperty(propertyName);
            if (property.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(
                    $"Replay-v3 '{propertyName}' must be an array.");
            }
            ImmutableArray<T> result =
                property.Deserialize<ImmutableArray<T>>(options);
            if (result.IsDefault)
            {
                throw new JsonException(
                    $"Replay-v3 '{propertyName}' must be initialized.");
            }
            return result;
        }
    }

    private sealed class TaggedUnionJsonConverter<TBase>(
        IReadOnlyDictionary<string, Type> derivedTypes)
        : JsonConverter<TBase> where TBase : class
    {
        public override TBase Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document =
                JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    $"Replay-v3 {typeof(TBase).Name} must be an object.");
            }

            string kind = RequiredString(root, "kind");
            if (!derivedTypes.TryGetValue(
                    kind,
                    out Type? derivedType))
            {
                throw new JsonException(
                    $"Unknown replay-v3 {typeof(TBase).Name} kind '{kind}'.");
            }

            object? value = JsonSerializer.Deserialize(
                root.GetRawText(),
                derivedType,
                options);
            return value as TBase
                ?? throw new JsonException(
                    $"Replay-v3 {typeof(TBase).Name} was null.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            TBase value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Replay-v3 uses its explicit canonical writer.");
    }

    private static byte[] Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Indented = false,
                       SkipValidation = false,
                   }))
        {
            write(writer);
        }
        return stream.ToArray();
    }
}
