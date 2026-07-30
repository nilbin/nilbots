using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Runtime;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class GenericActorObservabilityLedgerTests
{
    [Fact]
    public void EveryMirrorCarriesExactlyOneHoldAndProjectileEncoding()
    {
        // One encoding of one fact. The hold is published as the owning team
        // plus the tick the restriction lifts, in the same grammar as
        // ControlResumesAtTick; a second spelling of the same clock (a
        // remaining-ticks countdown) is a duplicate encoding, and so is a
        // second spelling of a projectile's damage. Both mirrors are checked
        // because the Engine and SDK graphs are deliberately separate.
        string[] holdSurface =
        [
            .. HoldAndProjectileMembers(
                typeof(GenericActorRuntimeObservation.ModeObservationState
                    .Frontline)),
            .. HoldAndProjectileMembers(
                typeof(Sdk.GenericActorContext.ModeObservationState
                    .Frontline)),
        ];
        string[] projectileSurface =
        [
            .. HoldAndProjectileMembers(
                typeof(GenericActorRuntimeObservation.ObservedProjectile)),
            .. HoldAndProjectileMembers(
                typeof(Sdk.GenericActorContext.ObservedProjectile)),
        ];

        Assert.Equal(
            [
                "HoldEndsAtTick",
                "HoldOwnerTeamId",
                "HoldEndsAtTick",
                "HoldOwnerTeamId",
            ],
            holdSurface);
        Assert.Equal(
            [
                "DamagePerHit",
                "TicksPerAdvance",
                "DamagePerHit",
                "TicksPerAdvance",
            ],
            projectileSurface);
    }

    private static IEnumerable<string> HoldAndProjectileMembers(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(name =>
                name.StartsWith("Hold", StringComparison.Ordinal)
                || name.Contains("Damage", StringComparison.Ordinal)
                || name.Equals("TicksPerAdvance", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

    [Fact]
    public void ClassFreeContractPublishesNoClassIdentityOrHold()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        var probe = new ClassAndHoldProbeBot();
        var opponent =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, _) => GenericDeathmatchSessionTestFixture.Wait());
        GenericActorParticipantConfiguration[] participants =
        [
            new()
            {
                ParticipantId = 0,
                TeamId = 0,
                Name = "profile2-probe",
                ArtifactHash = "fixture-profile2-probe",
                RuntimeFactory =
                    new InProcessGenericActorRuntimeFactory(() => probe),
            },
            new()
            {
                ParticipantId = 1,
                TeamId = 1,
                Name = "profile2-opponent",
                ArtifactHash = "fixture-profile2-opponent",
                RuntimeFactory = opponent,
            },
        ];

        using var session = new GenericActorMatchSession(
            definition,
            participants,
            GenericFrontlineReplayV3TestFixture.Seed);
        GenericActorMatchPreparedTick prepared = session.PrepareTick();
        session.Step(prepared.Observations);

        Sdk.GenericActorContext observed = Assert.Single(probe.Contexts);
        Assert.Equal(
            Sdk.ActorContractProfile.GenericV2.ObservationSchemaVersion,
            observed.SchemaVersion);
        // The hosted contract declares no classes, so the fact is absent
        // everywhere rather than spelled as an inert value — the same
        // discipline the canonical writer follows (#156).
        Assert.Null(observed.Self.ClassId);
        Assert.All(
            observed.Participants,
            participant => Assert.Null(participant.ClassId));
        Assert.All(
            observed.Enemies,
            enemy => Assert.Null(enemy.ClassId));
        var mode = Assert.IsType<
            Sdk.GenericActorContext.ModeObservationState.Frontline>(
                observed.Mode);
        // Its redeploy policy has no ratchet, so no hold ever binds.
        Assert.Null(mode.HoldOwnerTeamId);
        Assert.Null(mode.HoldEndsAtTick);
    }

    [Fact]
    public void RealSdkProbeReadsOpponentClassAndActiveHold()
    {
        ActorResolvedMatchDefinition definition =
            GenericFrontlineReplayV3TestFixture
                .ClassedStickyProbeDefinition();
        var probe = new ClassAndHoldProbeBot();
        var opponent =
            new GenericDeathmatchSessionTestFixture.RecordingFactory(
                (_, _) => GenericDeathmatchSessionTestFixture.Wait());
        GenericActorParticipantConfiguration[] participants =
        [
            new()
            {
                ParticipantId = 10,
                TeamId = 0,
                Name = "bulwark-opponent",
                ArtifactHash = "fixture-bulwark-opponent",
                RuntimeFactory = opponent,
            },
            new()
            {
                ParticipantId = 20,
                TeamId = 1,
                Name = "striker-probe",
                ArtifactHash = "fixture-striker-probe",
                RuntimeFactory =
                    new InProcessGenericActorRuntimeFactory(() => probe),
            },
        ];

        using var session = new GenericActorMatchSession(
            definition,
            participants,
            GenericFrontlineReplayV3TestFixture.Seed);
        session.Run();

        Sdk.GenericActorContext observed =
            Assert.Single(probe.Contexts, context => context.Tick == 1);
        Assert.Equal("striker", observed.Self.ClassId);
        Assert.Equal(
            "bulwark",
            observed.Participants.Single(status =>
                status.TeamId == 0).ClassId);
        Assert.Equal(
            "bulwark",
            Assert.Single(observed.Enemies).ClassId);
        var mode = Assert.IsType<
            Sdk.GenericActorContext.ModeObservationState.Frontline>(
                observed.Mode);
        Assert.Equal(0, mode.HoldOwnerTeamId);
        // The hold was created on this tick's advance, so its expiry sits the
        // full declared duration ahead of the observed tick.
        Assert.Equal(observed.Tick + 4, mode.HoldEndsAtTick);
    }

    [Fact]
    public void ChronologyRejectsClassAndHoldObservationDrift()
    {
        ActorResolvedMatchDefinition definition =
            GenericFrontlineReplayV3TestFixture
                .ClassedStickyProbeDefinition();
        ReplayV3 replay =
            GenericFrontlineReplayV3TestFixture.CreateReplay(definition);
        string json = ReplayV3Serializer.ToJson(replay);
        string classDrift = MutateAndRehash(
            json,
            root =>
            {
                JsonObject observation = TickOneTurn(root)["observation"]!
                    .AsObject();
                observation["self"]!["classId"] = "fabricator";
            });
        string holdDrift = MutateAndRehash(
            json,
            root =>
            {
                JsonObject mode = TickOneTurn(root)["observation"]!
                    .AsObject()["mode"]!.AsObject();
                // Still inside the declared duration, so only the kernel
                // re-derivation can catch it.
                mode["holdEndsAtTick"] =
                    mode["holdEndsAtTick"]!.GetValue<int>() - 1;
            });

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                classDrift,
                out string? classFailure));
        Assert.Contains(
            "class",
            classFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                holdDrift,
                out string? holdFailure));
        // A recorded observation is compared against the authoritative
        // pre-state, so the drift is refused at the document layer before the
        // kernel re-derivation ever runs.
        Assert.Contains(
            "observed mode must exactly match the authoritative pre-state",
            holdFailure,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChronologyRejectsForgedHoldStateAndModeChangeEvidence()
    {
        ActorResolvedMatchDefinition definition =
            GenericFrontlineReplayV3TestFixture
                .ClassedStickyProbeDefinition();
        GenericActorMatchChronology chronology =
            RunWaitChronology(definition);

        GenericActorMatchTickFrame finalFrame = chronology.Ticks[^1];
        var finalMode = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.Frontline>(
                finalFrame.PostState.Mode);
        Assert.NotNull(finalMode.HoldOwnerTeamId);
        var forgedFinalMode =
            new GenericActorRuntimeObservation.ModeObservationState.Frontline(
                finalMode.ModeId,
                finalMode.ActivePositionIndex,
                finalMode.ClaimingTeamId,
                finalMode.CaptureProgress,
                finalMode.DecayTicksElapsed,
                finalMode.ControlResumesAtTick,
                finalMode.HoldOwnerTeamId,
                finalMode.HoldEndsAtTick + 1);
        var forgedFinalFrame = new GenericActorMatchTickFrame(
            finalFrame.TickStart,
            finalFrame.ActorTurns,
            finalFrame.Events,
            finalFrame.Traversals,
            WorldWithMode(
                definition,
                finalFrame.PostState,
                forgedFinalMode));
        ArgumentException stateFailure =
            Assert.Throws<ArgumentException>(() =>
                new GenericActorMatchChronology(
                    chronology.Descriptor,
                    chronology.InitialFrame,
                    [.. chronology.Ticks.Select(frame =>
                        frame.Tick == finalFrame.Tick
                            ? forgedFinalFrame
                            : frame)],
                    chronology.Result));
        Assert.Contains(
            "objective kernel",
            stateFailure.Message,
            StringComparison.Ordinal);

        GenericActorMatchTickFrame advanceFrame = chronology.Ticks[0];
        GenericActorAuthoritativeEvent modeEvent =
            Assert.Single(advanceFrame.Events, item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.ModeChanged);
        var modePayload = Assert.IsType<
            GenericActorRuntimeObservation.EventPayload.ModeChanged>(
                modeEvent.Payload);
        var eventMode = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.Frontline>(
                modePayload.State);
        var forgedEventMode =
            new GenericActorRuntimeObservation.ModeObservationState.Frontline(
                eventMode.ModeId,
                eventMode.ActivePositionIndex,
                eventMode.ClaimingTeamId,
                eventMode.CaptureProgress,
                eventMode.DecayTicksElapsed,
                eventMode.ControlResumesAtTick,
                eventMode.HoldOwnerTeamId,
                eventMode.HoldEndsAtTick + 1);
        GenericActorAuthoritativeEvent[] forgedEvents =
        [
            .. advanceFrame.Events.Select(item =>
                item == modeEvent
                    ? new GenericActorAuthoritativeEvent(
                        item.EventHandle,
                        item.Tick,
                        item.GlobalOrdinal,
                        item.SourceOrdinal,
                        item.Kind,
                        new GenericActorRuntimeObservation.EventPayload
                            .ModeChanged(forgedEventMode),
                        item.EventAudience)
                    : item),
        ];
        var forgedAdvanceFrame = new GenericActorMatchTickFrame(
            advanceFrame.TickStart,
            advanceFrame.ActorTurns,
            forgedEvents,
            advanceFrame.Traversals,
            advanceFrame.PostState);
        ArgumentException eventFailure =
            Assert.Throws<ArgumentException>(() =>
                new GenericActorMatchChronology(
                    chronology.Descriptor,
                    chronology.InitialFrame,
                    [.. chronology.Ticks.Select(frame =>
                        frame.Tick == advanceFrame.Tick
                            ? forgedAdvanceFrame
                            : frame)],
                    chronology.Result));
        Assert.Contains(
            "mode-change evidence",
            eventFailure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FrontlineChronologyEvidenceDirectlyRejectsForgedHoldBoundary()
    {
        ActorResolvedMatchDefinition definition =
            GenericFrontlineReplayV3TestFixture
                .ClassedStickyProbeDefinition();
        GenericActorMatchChronology chronology =
            RunWaitChronology(definition);
        GenericActorMatchTickFrame finalFrame = chronology.Ticks[^1];
        var finalMode = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.Frontline>(
                finalFrame.PostState.Mode);
        Assert.NotNull(finalMode.HoldOwnerTeamId);
        var forgedMode =
            new GenericActorRuntimeObservation.ModeObservationState.Frontline(
                finalMode.ModeId,
                finalMode.ActivePositionIndex,
                finalMode.ClaimingTeamId,
                finalMode.CaptureProgress,
                finalMode.DecayTicksElapsed,
                finalMode.ControlResumesAtTick,
                finalMode.HoldOwnerTeamId,
                finalMode.HoldEndsAtTick + 1);
        var forgedFrame = new GenericActorMatchTickFrame(
            finalFrame.TickStart,
            finalFrame.ActorTurns,
            finalFrame.Events,
            finalFrame.Traversals,
            WorldWithMode(
                definition,
                finalFrame.PostState,
                forgedMode));
        GenericActorMatchTickFrame[] forgedTicks =
        [
            .. chronology.Ticks.Select(frame =>
                frame.Tick == finalFrame.Tick
                    ? forgedFrame
                    : frame),
        ];

        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            GenericFrontlineChronologyEvidence.Validate(
                definition,
                chronology.InitialFrame,
                forgedTicks,
                chronology.Result));

        Assert.Equal("ticks", failure.ParamName);
        Assert.Contains(
            "objective kernel",
            failure.Message,
            StringComparison.Ordinal);
    }

    private static GenericActorMatchChronology RunWaitChronology(
        ActorResolvedMatchDefinition definition)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            GenericFrontlineReplayV3TestFixture.Seed);
        session.Run();
        return session.Chronology;
    }

    private static GenericActorWorldSnapshot WorldWithMode(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot source,
        GenericActorRuntimeObservation.ModeObservationState mode) =>
        new(
            definition,
            source.NextTick,
            source.NextProjectileId,
            source.Participants,
            source.Slots,
            source.ActiveLives,
            source.PendingReplications,
            source.Projectiles,
            source.Scoreboard,
            mode);

    private static JsonObject TickOneTurn(JsonObject root) =>
        root["ticks"]!.AsArray()
            .Single(tick => tick!["tick"]!.GetValue<int>() == 1)![
                "actorTurns"]!.AsArray()[0]!.AsObject();

    private static string MutateAndRehash(
        string json,
        Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (string propertyName in
                     new[] { "header", "initialFrame", "ticks", "result" })
            {
                writer.WritePropertyName(propertyName);
                root[propertyName]!.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        root["replayHash"] = Convert.ToHexStringLower(
            SHA256.HashData(stream.ToArray()));
        return root.ToJsonString();
    }

    private sealed class ClassAndHoldProbeBot : Sdk.IGenericActorBot
    {
        public List<Sdk.GenericActorContext> Contexts { get; } = [];

        public Sdk.GenericActorDecision Tick(
            Sdk.GenericActorContext context)
        {
            Contexts.Add(context);
            return Sdk.GenericActorDecision.WithoutArguments("wait", 0);
        }
    }
}
