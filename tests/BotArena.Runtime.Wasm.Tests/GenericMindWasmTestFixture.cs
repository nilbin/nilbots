using System.Collections.Immutable;
using BotArena.Engine.Tests;
using Wasmtime;
using Engine = BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

/// <summary>
/// Hand-written WAT minds plus the scripted world they run against.
///
/// <para>The happy artifact does one thing beyond replying: it READS THE TICK
/// out of the delivered frame and echoes it into its reply, and it branches on
/// that tick to answer a full roster differently from an empty one. Both matter.
/// The echo is what makes the correlated request/reply rule testable rather
/// than assumed, and the branch is what lets one static artifact play a
/// scripted match in which its army dies and comes back.</para>
/// </summary>
internal static class GenericMindWasmTestFixture
{
    // The frame layout puts the echoed tick at a fixed offset: 12 header bytes,
    // then field 1 (2-byte id, 4-byte length, 4-byte value), then field 2's
    // 6-byte header. Both the observation and the reply carry schemaVersion
    // then tick, so the guest copies four bytes from 28 to 28.
    private const int TickOffset = 28;
    private const int ReplyBase = 1_100_000;
    private const int HostFrameCapacity = 1_048_576;

    private static readonly Lazy<byte[]> HappyArtifact = new(
        () => Build(ArtifactBehavior.Happy),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> FuelHogArtifact = new(
        () => Build(ArtifactBehavior.FuelHog),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> WrongSchemaArtifact = new(
        () => Build(ArtifactBehavior.WrongSchemaReady),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> StaleTickArtifact = new(
        () => Build(ArtifactBehavior.StaleTickReply),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> OversizedMemoryArtifact = new(
        () => Build(ArtifactBehavior.OversizedMemory),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static TemporaryArtifact Happy() => new(HappyArtifact.Value);

    public static TemporaryArtifact FuelHog() => new(FuelHogArtifact.Value);

    public static TemporaryArtifact WrongSchemaReady() =>
        new(WrongSchemaArtifact.Value);

    public static TemporaryArtifact StaleTickReply() =>
        new(StaleTickArtifact.Value);

    public static TemporaryArtifact OversizedMemory() =>
        new(OversizedMemoryArtifact.Value);

    /// <summary>
    /// The mind-profile twin of an existing resolved definition: identical
    /// rules, map, format, topology and mode, with the capability tuple as the
    /// only difference.
    /// </summary>
    public static Engine.ActorResolvedMatchDefinition Contract()
    {
        Engine.ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        return new Engine.ActorResolvedMatchDefinition(
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            Engine.ActorMatchCapabilityVersions.Mind);
    }

    public static Engine.GenericMindRuntimeStart Start(
        Engine.ActorResolvedMatchDefinition contract,
        int participantId = 10,
        int teamId = 0) =>
        new()
        {
            SchemaVersion =
                Engine.BotArenaVersions.GenericMindMatchStartSchemaVersion,
            RuntimeContractVersion =
                Engine.BotArenaVersions.GenericMindRuntimeContractVersion,
            ParticipantId = participantId,
            TeamId = teamId,
            MindRandomSeed = 0xDEAD_BEEF_1234_5678UL,
            TeamRandomSeed = 0x0FED_CBA9_8765_4321UL,
            AlliedParticipantIds = [],
            Contract = contract,
        };

    /// <summary>
    /// One scripted tick. EVEN ticks carry two live bodies; ODD ticks carry
    /// none, which is the "the mind ticks with nothing alive" invariant made
    /// into a fixture rather than a claim.
    /// </summary>
    public static Engine.GenericMindRuntimeObservation Observation(
        Engine.ActorResolvedMatchDefinition contract,
        int tick,
        int participantId = 10,
        int teamId = 0)
    {
        bool populated = tick % 2 == 0;
        ImmutableArray<Engine.GenericMindRuntimeObservation.ObservedBodyState>
            bodies = populated
                ? [Body(teamId, 0, tick), Body(teamId, 1, tick)]
                : [];

        return new Engine.GenericMindRuntimeObservation(
            Engine.BotArenaVersions.GenericMindObservationSchemaVersion,
            tick,
            Engine.ActorContractFingerprint.ComputeMatch(contract),
            participantId,
            teamId,
            bodies,
            [
                new(
                    teamId,
                    0,
                    populated
                        ? new Engine.GenericActorRuntimeObservation
                            .UnitSlotState.Active(
                                new Engine.ActorIdentity(teamId, 0, 0),
                                0,
                                "mobile")
                        : new Engine.GenericActorRuntimeObservation
                            .UnitSlotState.AutomaticReturnPending(
                                tick + 19,
                                "mobile",
                                1)),
                new(
                    teamId,
                    1,
                    populated
                        ? new Engine.GenericActorRuntimeObservation
                            .UnitSlotState.Active(
                                new Engine.ActorIdentity(teamId, 1, 0),
                                0,
                                "mobile")
                        : new Engine.GenericActorRuntimeObservation
                            .UnitSlotState.AutomaticReturnPending(
                                tick + 19,
                                "mobile",
                                1)),
            ],
            [],
            new Engine.GenericMindTeamProjection(
                teamId,
                [],
                [
                    new(
                        ParticipantId: 10,
                        TeamId: 0,
                        RuntimeFaultCount: 0,
                        Disqualified: false),
                    new(
                        ParticipantId: 20,
                        TeamId: 1,
                        RuntimeFaultCount: 0,
                        Disqualified: false),
                ],
                [],
                [],
                null,
                [],
                null,
                new Engine.GenericActorRuntimeObservation.ScoreboardState(
                    [
                        new(TeamId: 0, Eligible: true, [new("kills", 0)]),
                        new(TeamId: 1, Eligible: true, [new("kills", 0)]),
                    ]),
                new Engine.GenericActorRuntimeObservation.ModeObservationState
                    .Deathmatch("deathmatch")),
            []);
    }

    private static Engine.GenericMindRuntimeObservation.ObservedBodyState Body(
        int teamId,
        int unitId,
        int tick) =>
        new(
            new Engine.ActorIdentity(teamId, unitId, 0),
            Generation: 0,
            FormId: "mobile",
            new Engine.Position(1 + unitId, 1),
            Engine.Direction.East,
            Health: 3,
            Cooldown: 0,
            Energy: 10,
            PreviousActionResolution: null,
            PendingSameLifeTransition: null,
            PreviousPosition: tick == 0 ? null : new Engine.Position(1 + unitId, 1),
            MovedLastTick: false,
            LifeStartedTick: 0,
            new Engine.GenericActorRuntimeStart.LifeOrigin(
                Engine.GenericActorRuntimeStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            [
                new("wait", 0, AllowedByForm: true, Available: true, []),
            ]);

    /// <summary>
    /// The reply the happy artifact posts on a populated tick: one command per
    /// live body, the first of them carrying a role tag so the shipped half of
    /// the reservation is exercised end to end.
    /// </summary>
    public static Sdk.MindDecisions PopulatedReply(int tick) =>
        new(
            Sdk.GenericMindContractVersions.DecisionSchemaVersion,
            tick,
            [
                new Sdk.MindCommand(0, 0, "wait", 0, [], "channeler", "claim"),
                new Sdk.MindCommand(1, 0, "wait", 0, [], "screen"),
            ]);

    public static Sdk.MindDecisions EmptyReply(int tick) =>
        new(
            Sdk.GenericMindContractVersions.DecisionSchemaVersion,
            tick,
            []);

    private static byte[] Build(ArtifactBehavior behavior)
    {
        byte[] helloAck = Sdk.ActorWireProtocol.EncodeHelloAck(
            Sdk.ActorWireProtocol.MajorVersion,
            Sdk.ActorContractProfile.MindV1);
        byte[] ready = behavior == ArtifactBehavior.WrongSchemaReady
            // The per-life tuple, attested against a mind Hello: executable,
            // and mind-profile-ineligible.
            ? Sdk.ActorWireProtocol.EncodeReady(
                Sdk.ActorWireProtocol.MajorVersion,
                Sdk.GenericActorContractVersions.RuntimeContractVersion,
                Sdk.GenericActorContractVersions.MatchStartSchemaVersion,
                Sdk.GenericActorContractVersions.ObservationSchemaVersion,
                Sdk.GenericActorContractVersions.DecisionSchemaVersion,
                Sdk.ActorContractProfile.GenericV2)
            : Sdk.ActorWireProtocol.EncodeReady(
                Sdk.ActorWireProtocol.MajorVersion,
                Sdk.GenericMindContractVersions.RuntimeContractVersion,
                Sdk.GenericMindContractVersions.MatchStartSchemaVersion,
                Sdk.GenericMindContractVersions.ObservationSchemaVersion,
                Sdk.GenericMindContractVersions.DecisionSchemaVersion,
                Sdk.ActorContractProfile.MindV1);
        byte[] populated = Sdk.ActorWireProtocol.EncodeMindDecisions(
            PopulatedReply(0));
        byte[] empty = Sdk.ActorWireProtocol.EncodeMindDecisions(
            EmptyReply(0));

        string body = behavior switch
        {
            ArtifactBehavior.FuelHog => FuelHogBody(helloAck, ready),
            ArtifactBehavior.OversizedMemory =>
                OversizedMemoryBody(helloAck, ready),
            ArtifactBehavior.StaleTickReply =>
                StaleTickBody(helloAck, ready, populated),
            _ => HappyBody(helloAck, ready, populated, empty),
        };
        return Module.ConvertText(body);
    }

    private static string HappyBody(
        byte[] helloAck,
        byte[] ready,
        byte[] populated,
        byte[] empty) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (data (i32.const {{ReplyBase}}) "{{Wat(helloAck)}}")
          (data (i32.const {{ReplyBase + 2_000}}) "{{Wat(ready)}}")
          (data (i32.const {{ReplyBase + 4_000}}) "{{Wat(populated)}}")
          (data (i32.const {{ReplyBase + 6_000}}) "{{Wat(empty)}}")
          (func (export "_start") (local $length i32) (local $tick i32)
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase + 2_000}})
              (i32.const {{ready.Length}}))
            (loop $receive
              (local.set $length
                (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
              (if (i32.eqz (local.get $length)) (then (return)))
              (if (i32.eq (i32.load8_u (i32.const 5)) (i32.const 7))
                (then (return)))
              (local.set $tick (i32.load (i32.const {{TickOffset}})))
              (if (i32.eqz (i32.and (local.get $tick) (i32.const 1)))
                (then
                  (i32.store
                    (i32.const {{ReplyBase + 4_000 + TickOffset}})
                    (local.get $tick))
                  (call $post
                    (i32.const {{ReplyBase + 4_000}})
                    (i32.const {{populated.Length}})))
                (else
                  (i32.store
                    (i32.const {{ReplyBase + 6_000 + TickOffset}})
                    (local.get $tick))
                  (call $post
                    (i32.const {{ReplyBase + 6_000}})
                    (i32.const {{empty.Length}}))))
              (br $receive))))
        """;

    private static string StaleTickBody(
        byte[] helloAck,
        byte[] ready,
        byte[] populated) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (data (i32.const {{ReplyBase}}) "{{Wat(helloAck)}}")
          (data (i32.const {{ReplyBase + 2_000}}) "{{Wat(ready)}}")
          (data (i32.const {{ReplyBase + 4_000}}) "{{Wat(populated)}}")
          (func (export "_start") (local $length i32)
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase + 2_000}})
              (i32.const {{ready.Length}}))
            (loop $receive
              (local.set $length
                (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
              (if (i32.eqz (local.get $length)) (then (return)))
              (if (i32.eq (i32.load8_u (i32.const 5)) (i32.const 7))
                (then (return)))
              ;; Never echoes the tick: the baked reply always answers tick 0.
              (call $post
                (i32.const {{ReplyBase + 4_000}})
                (i32.const {{populated.Length}}))
              (br $receive))))
        """;

    private static string FuelHogBody(byte[] helloAck, byte[] ready) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (global $burn (mut i32) (i32.const 0))
          (data (i32.const {{ReplyBase}}) "{{Wat(helloAck)}}")
          (data (i32.const {{ReplyBase + 2_000}}) "{{Wat(ready)}}")
          (func (export "_start")
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase + 2_000}})
              (i32.const {{ready.Length}}))
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            ;; A side effect per iteration so nothing can elide the loop.
            (loop $spin
              (global.set $burn
                (i32.add (global.get $burn) (i32.const 1)))
              (br $spin))))
        """;

    private static string OversizedMemoryBody(byte[] helloAck, byte[] ready) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          ;; 2049 pages = 134,283,264 bytes, one page past the 128 MiB ceiling.
          (memory (export "memory") 2049)
          (data (i32.const {{ReplyBase}}) "{{Wat(helloAck)}}")
          (data (i32.const {{ReplyBase + 2_000}}) "{{Wat(ready)}}")
          (func (export "_start")
            (drop (call $next (i32.const 0) (i32.const {{HostFrameCapacity}})))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))))
        """;

    private static string Wat(IEnumerable<byte> bytes) =>
        string.Concat(bytes.Select(value => $"\\{value:x2}"));

    private enum ArtifactBehavior
    {
        Happy,
        FuelHog,
        WrongSchemaReady,
        StaleTickReply,
        OversizedMemory,
    }

    internal sealed class TemporaryArtifact : IDisposable
    {
        public TemporaryArtifact(byte[] bytes)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nilbots-generic-mind-{Guid.NewGuid():N}.wasm");
            File.WriteAllBytes(Path, bytes);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
