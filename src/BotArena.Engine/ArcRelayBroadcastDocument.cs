using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.Engine;

/// <summary>
/// Deterministic spectator replay for the trusted Arc Relay product lane.
/// It records authoritative world states, public combat/objective facts,
/// projectile paths and chosen body actions while deliberately excluding
/// private observations, legality constraints, fuel and debug text.
/// <para>
/// This is broadcast format 2, not canonical replay v3. Its own hash covers
/// its exact canonical payload. Audit and admission continue to use replay v3
/// and the frozen WASM path; the compact path is proven result-equivalent and
/// never substitutes for audit evidence.
/// </para>
/// </summary>
public sealed class ArcRelayBroadcastDocument
{
    public const int FormatVersion = BotArenaVersions.ArcRelayBroadcastFormatVersion;
    public const int BroadcastVersion = 2;

    private ArcRelayBroadcastDocument(
        byte[] canonicalUtf8,
        string replayHash,
        GenericActorMatchResult result,
        TimeSpan simulationElapsed,
        TimeSpan projectionElapsed)
    {
        CanonicalUtf8 = canonicalUtf8;
        ReplayHash = replayHash;
        Result = result;
        SimulationElapsed = simulationElapsed;
        ProjectionElapsed = projectionElapsed;
    }

    public ReadOnlyMemory<byte> CanonicalUtf8 { get; }
    public string ReplayHash { get; }
    public GenericActorMatchResult Result { get; }
    public TimeSpan SimulationElapsed { get; }
    public TimeSpan ProjectionElapsed { get; }

    public static ArcRelayBroadcastDocument CreateAndRun(
        GenericActorMatchSession session,
        GenericActorReplayPresentation? presentation)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsCompleted)
            throw new InvalidOperationException("Arc Relay broadcast recording must start before tick zero.");
        if (session.Definition.Rules.GameMode is not ArcRelayGameModeDefinition arcRelay)
            throw new InvalidOperationException("Compact Arc Relay playback requires the Arc Relay mode.");

        ReplayV3.ReplayHeader header = ReplayV3Projection.Header(
            session.MatchDescriptor,
            GenericActorReplayDocument.Presentation(presentation));
        ReplayV3.WorldState initial = ReplayV3Projection.WorldState(
            session.CaptureWorldSnapshot());
        HashSet<string> signatureIds = arcRelay.Signatures
            .Select(value => value.ActionId)
            .ToHashSet(StringComparer.Ordinal);
        var worlds = new List<ReplayV3.WorldState>(session.Definition.Rules.Limits.MaxTicks);
        var turns = new List<ImmutableArray<CompactTurn>>(session.Definition.Rules.Limits.MaxTicks);
        var startEvents = new List<ImmutableArray<ReplayV3.AuthoritativeEvent>>(session.Definition.Rules.Limits.MaxTicks);
        var events = new List<ImmutableArray<ReplayV3.AuthoritativeEvent>>(session.Definition.Rules.Limits.MaxTicks);
        var traversals = new List<ImmutableArray<ReplayV3.ProjectileTraversal>>(session.Definition.Rules.Limits.MaxTicks);
        var births = new List<ImmutableArray<ReplayV3.LifeState>>(session.Definition.Rules.Limits.MaxTicks);
        HashSet<ActorIdentity> previousActors = initial.ActiveLives
            .Select(value => new ActorIdentity(
                value.ActorId.TeamId,
                value.ActorId.UnitId,
                value.ActorId.LifeId))
            .ToHashSet();

        long simulationTicks = 0;
        long projectionTicks = 0;
        while (!session.IsCompleted)
        {
            long phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            _ = session.PrepareTick();
            GenericActorMatchStepResult step = session.Step();
            simulationTicks += System.Diagnostics.Stopwatch.GetTimestamp() - phaseStarted;

            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            GenericActorMatchTickStart tickStart = step.AuthoritativeTickStart
                ?? throw new InvalidOperationException("Compact playback tick has no authoritative start.");
            ReplayV3.WorldState startWorld = ReplayV3Projection.WorldState(tickStart.State);
            worlds.Add(ReplayV3Projection.WorldState(step.PostState));
            turns.Add(ProjectTurns(step, signatureIds));
            startEvents.Add(ProjectEvents(tickStart.Events));
            events.Add(ProjectEvents(step.AuthoritativeEvents));
            traversals.Add(step.ProjectileTraversals
                .Select(ReplayV3Projection.Traversal)
                .ToImmutableArray());
            births.Add(startWorld.ActiveLives
                .Where(value => !previousActors.Contains(new ActorIdentity(
                    value.ActorId.TeamId,
                    value.ActorId.UnitId,
                    value.ActorId.LifeId)))
                .ToImmutableArray());
            previousActors = step.PostState.ActiveLives
                .Select(value => value.ActorId)
                .ToHashSet();
            projectionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - phaseStarted;
        }

        GenericActorMatchResult result = session.Result
            ?? throw new InvalidOperationException("Arc Relay match did not complete.");
        long encodingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        byte[] payload = WritePayload(
            header,
            initial,
            worlds,
            turns,
            startEvents,
            events,
            traversals,
            births,
            ReplayV3Projection.MatchResult(result));
        string replayHash = Convert.ToHexStringLower(SHA256.HashData(payload));
        byte[] suffix = Encoding.UTF8.GetBytes(
            $",\"replayHash\":\"{replayHash}\",\"partial\":false}}");
        byte[] envelope = GC.AllocateUninitializedArray<byte>(
            checked(payload.Length - 1 + suffix.Length));
        payload.AsSpan(0, payload.Length - 1).CopyTo(envelope);
        suffix.CopyTo(envelope, payload.Length - 1);
        projectionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - encodingStarted;
        return new ArcRelayBroadcastDocument(
            envelope,
            replayHash,
            result,
            System.Diagnostics.Stopwatch.GetElapsedTime(0, simulationTicks),
            System.Diagnostics.Stopwatch.GetElapsedTime(0, projectionTicks));
    }

    public static string CreatePartialPrefix(
        string completeJson,
        int visibleTickCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completeJson);
        if (visibleTickCount < 0)
            throw new ArgumentOutOfRangeException(nameof(visibleTickCount));
        using JsonDocument source = JsonDocument.Parse(completeJson);
        JsonElement root = source.RootElement;
        if (root.GetProperty("broadcastVersion").GetInt32() != BroadcastVersion
            || root.GetProperty("partial").GetBoolean())
        {
            throw new InvalidDataException("Arc Relay broadcast prefix requires a complete format-2 broadcast.");
        }
        string expectedHash = root.GetProperty("replayHash").GetString()
            ?? throw new InvalidDataException("Arc Relay broadcast has no replay hash.");
        int hashField = completeJson.LastIndexOf(
            ",\"replayHash\":",
            StringComparison.Ordinal);
        if (hashField < 0)
            throw new InvalidDataException("Arc Relay broadcast is not in canonical property order.");
        string payload = completeJson[..hashField] + "}";
        string actualHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            throw new InvalidDataException("Arc Relay broadcast replay hash does not match its payload.");
        int available = root.GetProperty("worlds").GetArrayLength();
        int count = Math.Min(visibleTickCount, available);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("broadcastVersion", BroadcastVersion);
            Copy(writer, root, "header");
            Copy(writer, root, "initial");
            CopyPrefix(writer, root, "worlds", count);
            CopyPrefix(writer, root, "turns", count);
            CopyPrefix(writer, root, "startEvents", count);
            CopyPrefix(writer, root, "events", count);
            CopyPrefix(writer, root, "traversals", count);
            CopyPrefix(writer, root, "births", count);
            writer.WriteNull("result");
            writer.WriteNull("replayHash");
            writer.WriteBoolean("partial", true);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ImmutableArray<CompactTurn> ProjectTurns(
        GenericActorMatchStepResult step,
        IReadOnlySet<string> signatureIds)
    {
        Dictionary<ActorIdentity, GenericActorRuntimeActionResolution> resolutions =
            step.ActionResolutions.ToDictionary(
                value => value.ActorId,
                value => value.Resolution);
        var projected = ImmutableArray.CreateBuilder<CompactTurn>();
        foreach (GenericMindRuntimeObservation observation in
                 step.TickStart.MindObservations.OrderBy(value => value.ParticipantId))
        {
            foreach (GenericMindRuntimeObservation.ObservedBodyState body in
                     observation.Bodies.OrderBy(value => value.ActorId))
            {
                ReplayV3.ActionResolution resolution = ReplayV3Projection.ActionResolution(
                    resolutions[body.ActorId]);
                projected.Add(new CompactTurn(
                    new ReplayV3.ActorId(
                        body.ActorId.TeamId,
                        body.ActorId.UnitId,
                        body.ActorId.LifeId),
                    observation.ParticipantId,
                    body.RoleTag,
                    body.ActionLegalities
                        .Where(value => value.AllowedByForm && signatureIds.Contains(value.ActionId))
                        .Select(value => new CompactLegality(
                            value.ActionId,
                            value.ActionCode,
                            value.Available))
                        .ToImmutableArray(),
                    resolution.AcceptedAction,
                    resolution.ValidatedAction,
                    resolution.Outcome));
            }
        }
        return projected.ToImmutable();
    }

    private static ImmutableArray<ReplayV3.AuthoritativeEvent> ProjectEvents(
        IEnumerable<GenericActorAuthoritativeEvent> values) =>
    [
        .. values
            .Where(value => value.Kind is
                GenericActorRuntimeObservation.EventKind.ArcRelay
                or GenericActorRuntimeObservation.EventKind.Damage
                or GenericActorRuntimeObservation.EventKind.Destruction)
            .Select(ReplayV3Projection.Event),
    ];

    private static byte[] WritePayload(
        ReplayV3.ReplayHeader header,
        ReplayV3.WorldState initial,
        IReadOnlyList<ReplayV3.WorldState> worlds,
        IReadOnlyList<ImmutableArray<CompactTurn>> turns,
        IReadOnlyList<ImmutableArray<ReplayV3.AuthoritativeEvent>> startEvents,
        IReadOnlyList<ImmutableArray<ReplayV3.AuthoritativeEvent>> events,
        IReadOnlyList<ImmutableArray<ReplayV3.ProjectileTraversal>> traversals,
        IReadOnlyList<ImmutableArray<ReplayV3.LifeState>> births,
        ReplayV3.MatchResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("broadcastVersion", BroadcastVersion);
            writer.WritePropertyName("header");
            ReplayV3Serializer.WriteHeader(writer, header);
            writer.WritePropertyName("initial");
            WriteWorld(writer, initial);
            WriteColumn(writer, "worlds", worlds, WriteWorld);
            WriteColumn(writer, "turns", turns, static (json, value) => WriteTurns(json, value));
            WriteColumn(writer, "startEvents", startEvents, static (json, value) => WriteEvents(json, value));
            WriteColumn(writer, "events", events, static (json, value) => WriteEvents(json, value));
            WriteColumn(writer, "traversals", traversals, static (json, value) => WriteTraversals(json, value));
            WriteColumn(writer, "births", births, static (json, value) => WriteLives(json, value));
            writer.WritePropertyName("result");
            ReplayV3Serializer.WriteResult(writer, result);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void WriteWorld(Utf8JsonWriter writer, ReplayV3.WorldState value)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.NextTick);
        writer.WriteStringValue(value.NextProjectileId);
        writer.WriteStartArray();
        foreach (ReplayV3.ParticipantStatus participant in value.Participants)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(participant.ParticipantId);
            writer.WriteNumberValue(participant.TeamId);
            writer.WriteStringValue(participant.RuntimeFaultCount);
            writer.WriteBooleanValue(participant.Disqualified);
            WriteNullableStringValue(writer, participant.ClassId);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteStartArray();
        foreach (ReplayV3.SlotState slot in value.Slots)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(slot.TeamId);
            writer.WriteNumberValue(slot.UnitId);
            writer.WriteNumberValue(slot.ParticipantId);
            writer.WriteNumberValue(slot.NextLifeId);
            WriteSlotState(writer, slot.State);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        WriteLives(writer, value.ActiveLives);
        writer.WriteStartArray();
        foreach (ReplayV3.ProjectileState projectile in value.Projectiles)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(projectile.ProjectileId);
            writer.WriteNumberValue(projectile.OwnerParticipantId);
            writer.WriteNumberValue(projectile.OwnerTeamId);
            WriteActor(writer, projectile.OwnerActorId);
            writer.WriteStringValue(projectile.AttackProfileId);
            writer.WriteNumberValue(projectile.SpawnedAtTick);
            WritePosition(writer, projectile.Origin);
            WritePosition(writer, projectile.Position);
            writer.WriteStringValue(projectile.LaunchHeading);
            writer.WriteStringValue(projectile.Heading);
            ReplayV3Serializer.WriteNullableShotProgram(writer, projectile.ShotProgram);
            writer.WriteStartArray();
            foreach (ReplayV3.PositionValue point in projectile.CommittedPath)
                WritePosition(writer, point);
            writer.WriteEndArray();
            writer.WriteNumberValue(projectile.NextPathIndex);
            writer.WriteNumberValue(projectile.RemainingTiles);
            writer.WriteNumberValue(projectile.TicksUntilAdvance);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        ReplayV3Serializer.WriteScoreboard(writer, value.Scoreboard);
        ReplayV3Serializer.WriteModeState(writer, value.Mode);
        writer.WriteEndArray();
    }

    private static void WriteSlotState(Utf8JsonWriter writer, ReplayV3.UnitSlotState value)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Kind);
        switch (value)
        {
            case ReplayV3.UnitSlotState.Active active:
                WriteActor(writer, active.ActorId);
                writer.WriteNumberValue(active.Generation);
                writer.WriteStringValue(active.FormId);
                break;
            case ReplayV3.UnitSlotState.AvailabilityPending pending:
                writer.WriteStringValue(pending.Reason);
                writer.WriteNumberValue(pending.DueTick);
                break;
            case ReplayV3.UnitSlotState.AutomaticReturnPending pending:
                writer.WriteNumberValue(pending.DueTick);
                writer.WriteStringValue(pending.TargetFormId);
                writer.WriteNumberValue(pending.Generation);
                break;
            case ReplayV3.UnitSlotState.FabricationPending pending:
                WritePendingSlot(writer, pending.DueTick, pending.SourceActorId,
                    pending.TransitionId, pending.OperationId, pending.TargetFormId,
                    pending.ReservedPosition);
                break;
            case ReplayV3.UnitSlotState.ReplicationPending pending:
                WritePendingSlot(writer, pending.DueTick, pending.SourceActorId,
                    pending.TransitionId, pending.OperationId, pending.TargetFormId,
                    pending.ReservedPosition);
                break;
            case ReplayV3.UnitSlotState.Ready:
            case ReplayV3.UnitSlotState.PermanentlyDormant:
                break;
            default:
                throw new NotSupportedException($"Unsupported compact slot state {value.GetType().Name}.");
        }
        writer.WriteEndArray();
    }

    private static void WritePendingSlot(
        Utf8JsonWriter writer,
        int dueTick,
        ReplayV3.ActorId source,
        string transitionId,
        string operationId,
        string formId,
        ReplayV3.PositionValue position)
    {
        writer.WriteNumberValue(dueTick);
        WriteActor(writer, source);
        writer.WriteStringValue(transitionId);
        writer.WriteStringValue(operationId);
        writer.WriteStringValue(formId);
        WritePosition(writer, position);
    }

    private static void WriteLives(
        Utf8JsonWriter writer,
        IReadOnlyList<ReplayV3.LifeState> values)
    {
        writer.WriteStartArray();
        foreach (ReplayV3.LifeState life in values)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(life.ActorId.TeamId);
            writer.WriteNumberValue(life.ActorId.UnitId);
            writer.WriteNumberValue(life.ActorId.LifeId);
            writer.WriteNumberValue(life.ParticipantId);
            writer.WriteNumberValue(life.Generation);
            writer.WriteStringValue(life.FormId);
            writer.WriteNumberValue(life.Position.X);
            writer.WriteNumberValue(life.Position.Y);
            writer.WriteStringValue(life.Facing);
            writer.WriteNumberValue(life.Health);
            writer.WriteNumberValue(life.Cooldown);
            if (life.Energy is int energy) writer.WriteNumberValue(energy);
            else writer.WriteNullValue();
            writer.WriteNumberValue(life.SpawnedAtTick);
            writer.WriteStringValue(life.SpawnReason);
            if (life.ParentActorId is null) writer.WriteNullValue();
            else WriteActor(writer, life.ParentActorId);
            WriteNullableStringValue(writer, life.SourceTransitionId);
            WriteNullableStringValue(writer, life.SourceOperationId);
            if (life.PendingSameLifeTransition is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartArray();
                writer.WriteStringValue(life.PendingSameLifeTransition.TransitionId);
                writer.WriteStringValue(life.PendingSameLifeTransition.OperationId);
                writer.WriteStringValue(life.PendingSameLifeTransition.TargetFormId);
                writer.WriteNumberValue(life.PendingSameLifeTransition.StartedTick);
                writer.WriteNumberValue(life.PendingSameLifeTransition.DueTick);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    private static void WriteTurns(
        Utf8JsonWriter writer,
        IReadOnlyList<CompactTurn> values)
    {
        writer.WriteStartArray();
        foreach (CompactTurn turn in values)
        {
            writer.WriteStartArray();
            WriteActor(writer, turn.ActorId);
            writer.WriteNumberValue(turn.ParticipantId);
            WriteNullableStringValue(writer, turn.RoleTag);
            writer.WriteStartArray();
            foreach (CompactLegality legality in turn.Legalities)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(legality.ActionId);
                writer.WriteNumberValue(legality.ActionCode);
                writer.WriteBooleanValue(legality.Available);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            WriteAction(writer, turn.AcceptedAction);
            WriteAction(writer, turn.ValidatedAction);
            writer.WriteStringValue(turn.Outcome);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    private static void WriteAction(Utf8JsonWriter writer, ReplayV3.ResolvedAction value)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.ActionId);
        writer.WriteNumberValue(value.ActionCode);
        writer.WriteStartArray();
        foreach (ReplayV3.ActionArgument argument in value.Arguments)
            ReplayV3Serializer.WriteActionArgument(writer, argument);
        writer.WriteEndArray();
        writer.WriteEndArray();
    }

    private static void WriteEvents(
        Utf8JsonWriter writer,
        IReadOnlyList<ReplayV3.AuthoritativeEvent> values)
    {
        writer.WriteStartArray();
        foreach (ReplayV3.AuthoritativeEvent value in values)
            ReplayV3Serializer.WriteEvent(writer, value);
        writer.WriteEndArray();
    }

    private static void WriteTraversals(
        Utf8JsonWriter writer,
        IReadOnlyList<ReplayV3.ProjectileTraversal> values)
    {
        writer.WriteStartArray();
        foreach (ReplayV3.ProjectileTraversal value in values)
            ReplayV3Serializer.WriteTraversal(writer, value);
        writer.WriteEndArray();
    }

    private static void WriteActor(Utf8JsonWriter writer, ReplayV3.ActorId value)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.TeamId);
        writer.WriteNumberValue(value.UnitId);
        writer.WriteNumberValue(value.LifeId);
        writer.WriteEndArray();
    }

    private static void WritePosition(Utf8JsonWriter writer, ReplayV3.PositionValue value)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }

    private static void WriteNullableStringValue(Utf8JsonWriter writer, string? value)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }

    private static void WriteColumn<T>(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<T> values,
        Action<Utf8JsonWriter, T> write)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (T value in values)
            write(writer, value);
        writer.WriteEndArray();
    }

    private static void Copy(Utf8JsonWriter writer, JsonElement root, string name)
    {
        writer.WritePropertyName(name);
        root.GetProperty(name).WriteTo(writer);
    }

    private static void CopyPrefix(
        Utf8JsonWriter writer,
        JsonElement root,
        string name,
        int count)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (JsonElement value in root.GetProperty(name).EnumerateArray().Take(count))
            value.WriteTo(writer);
        writer.WriteEndArray();
    }

    private sealed record CompactTurn(
        ReplayV3.ActorId ActorId,
        int ParticipantId,
        string? RoleTag,
        ImmutableArray<CompactLegality> Legalities,
        ReplayV3.ResolvedAction AcceptedAction,
        ReplayV3.ResolvedAction ValidatedAction,
        string Outcome);

    private sealed record CompactLegality(
        string ActionId,
        int ActionCode,
        bool Available);
}
