using BotArena.Engine.Tests;
using Wasmtime;
using Engine = BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

internal static class GenericActorWasmTestFixture
{
    private const int ReplyBase = 1_100_000;

    private static readonly Lazy<byte[]> HappyArtifact = new(
        () => CreateArtifactBytes(ArtifactBehavior.Happy),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> DoubleReplyArtifact = new(
        () => CreateArtifactBytes(ArtifactBehavior.DoubleReply),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> FuelHogArtifact = new(
        () => CreateArtifactBytes(ArtifactBehavior.FuelHog),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<byte[]> WrongTickReplyArtifact = new(
        () => CreateArtifactBytes(ArtifactBehavior.WrongTickReply),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static Engine.ActorResolvedMatchDefinition Contract() =>
        GenericActorContractTestFixture.Deathmatch("head-to-head");

    public static Engine.GenericActorRuntimeStart Start(
        Engine.ActorResolvedMatchDefinition contract,
        int teamId,
        ulong seed = 42)
    {
        int participantId = teamId switch
        {
            0 => 10,
            1 => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(teamId)),
        };
        return new Engine.GenericActorRuntimeStart
        {
            SchemaVersion =
                Engine.BotArenaVersions.GenericActorMatchStartSchemaVersion,
            RuntimeContractVersion =
                Engine.BotArenaVersions.GenericActorRuntimeContractVersion,
            ActorId = new Engine.ActorIdentity(teamId, 0, 0),
            ParticipantId = participantId,
            ActorRandomSeed = seed,
            Origin = new Engine.GenericActorRuntimeStart.LifeOrigin(
                Engine.GenericActorRuntimeStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = contract,
        };
    }

    public static Engine.GenericActorRuntimeObservation Observation(
        Engine.ActorResolvedMatchDefinition contract,
        int teamId,
        int tick)
    {
        Engine.GenericActorRuntimeStart start = Start(contract, teamId);
        Engine.ActorIdentity actorId = start.ActorId;
        return new Engine.GenericActorRuntimeObservation(
            Engine.BotArenaVersions.GenericActorObservationSchemaVersion,
            tick,
            Engine.ActorContractFingerprint.ComputeMatch(contract),
            new Engine.GenericActorRuntimeObservation.ObservedSelfState(
                actorId,
                Generation: 0,
                FormId: "mobile",
                new Engine.Position(teamId == 0 ? 1 : 6, 1),
                teamId == 0
                    ? Engine.Direction.East
                    : Engine.Direction.West,
                Health: 3,
                Cooldown: 0,
                Energy: 10,
                PreviousActionResolution: null,
                PendingSameLifeTransition: null),
            [
                new(
                    teamId,
                    0,
                    new Engine.GenericActorRuntimeObservation.UnitSlotState
                        .Active(
                            actorId,
                            Generation: 0,
                            FormId: "mobile")),
            ],
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
            [],
            [],
            [],
            [],
            new Engine.GenericActorRuntimeObservation.ScoreboardState(
                [
                    new(
                        TeamId: 0,
                        Eligible: true,
                        [new("kills", 0)]),
                    new(
                        TeamId: 1,
                        Eligible: true,
                        [new("kills", 0)]),
                ]),
            new Engine.GenericActorRuntimeObservation.ModeObservationState
                .Deathmatch("deathmatch"),
            [
                new(
                    "wait",
                    0,
                    AllowedByForm: true,
                    Available: true,
                    []),
            ]);
    }

    public static TemporaryArtifact CreateHappyArtifact() =>
        new(HappyArtifact.Value);

    public static TemporaryArtifact CreateDoubleReplyArtifact() =>
        new(DoubleReplyArtifact.Value);

    public static TemporaryArtifact CreateFuelHogArtifact() =>
        new(FuelHogArtifact.Value);

    public static TemporaryArtifact CreateWrongTickReplyArtifact() =>
        new(WrongTickReplyArtifact.Value);

    private static byte[] CreateArtifactBytes(ArtifactBehavior behavior)
    {
        byte[] helloAck = Sdk.ActorWireProtocol.EncodeHelloAck(
            Sdk.ActorWireProtocol.MajorVersion,
            Sdk.ActorContractProfile.GenericV2);
        byte[] ready = Sdk.ActorWireProtocol.EncodeReady(
            Sdk.ActorWireProtocol.MajorVersion,
            Sdk.GenericActorContractVersions.RuntimeContractVersion,
            Sdk.GenericActorContractVersions.MatchStartSchemaVersion,
            Sdk.GenericActorContractVersions.ObservationSchemaVersion,
            Sdk.GenericActorContractVersions.DecisionSchemaVersion,
            Sdk.ActorContractProfile.GenericV2);
        byte[] firstDecision =
            Sdk.ActorWireProtocol.EncodeGenericDecision(
                Sdk.GenericActorDecision.WithoutArguments(
                    "wait",
                    0,
                    "calls=1"));
        byte[] secondDecision =
            Sdk.ActorWireProtocol.EncodeGenericDecision(
                Sdk.GenericActorDecision.WithoutArguments(
                    "wait",
                    0,
                    "calls=2"));

        string body = behavior switch
        {
            ArtifactBehavior.Happy =>
                HappyBody(
                    helloAck,
                    ready,
                    firstDecision,
                    secondDecision),
            ArtifactBehavior.DoubleReply =>
                DoubleReplyBody(helloAck, ready),
            ArtifactBehavior.FuelHog =>
                FuelHogBody(helloAck, ready),
            ArtifactBehavior.WrongTickReply =>
                WrongTickReplyBody(helloAck, ready),
            _ => throw new ArgumentOutOfRangeException(nameof(behavior)),
        };
        return Module.ConvertText(body);
    }

    private static string HappyBody(
        byte[] helloAck,
        byte[] ready,
        byte[] firstDecision,
        byte[] secondDecision) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (global $calls (mut i32) (i32.const 0))
          (data (i32.const {{ReplyBase}})
            "{{WatBytes(helloAck)}}")
          (data (i32.const {{ReplyBase + 1_000}})
            "{{WatBytes(ready)}}")
          (data (i32.const {{ReplyBase + 2_000}})
            "{{WatBytes(firstDecision)}}")
          (data (i32.const {{ReplyBase + 3_000}})
            "{{WatBytes(secondDecision)}}")
          (func (export "_start") (local $length i32)
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase + 1_000}})
              (i32.const {{ready.Length}}))
            (loop $receive
              (local.set $length
                (call $next (i32.const 0) (i32.const 1048576)))
              (if (i32.eqz (local.get $length))
                (then (return)))
              (if
                (i32.eq
                  (i32.load8_u (i32.const 5))
                  (i32.const 7))
                (then (return)))
              (global.set $calls
                (i32.add (global.get $calls) (i32.const 1)))
              (if (i32.eq (global.get $calls) (i32.const 1))
                (then
                  (call $post
                    (i32.const {{ReplyBase + 2_000}})
                    (i32.const {{firstDecision.Length}})))
                (else
                  (call $post
                    (i32.const {{ReplyBase + 3_000}})
                    (i32.const {{secondDecision.Length}}))))
              (br $receive))))
        """;

    private static string DoubleReplyBody(
        byte[] helloAck,
        byte[] ready) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (data (i32.const {{ReplyBase}})
            "{{WatBytes(helloAck)}}")
          (data (i32.const {{ReplyBase + 1_000}})
            "{{WatBytes(ready)}}")
          (func (export "_start")
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (call $post
              (i32.const {{ReplyBase + 1_000}})
              (i32.const {{ready.Length}}))
            (loop $spin (br $spin))))
        """;

    private static string FuelHogBody(
        byte[] helloAck,
        byte[] ready) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (data (i32.const {{ReplyBase}})
            "{{WatBytes(helloAck)}}")
          (data (i32.const {{ReplyBase + 1_000}})
            "{{WatBytes(ready)}}")
          (func (export "_start")
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase + 1_000}})
              (i32.const {{ready.Length}}))
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (loop $spin (br $spin))))
        """;

    private static string WrongTickReplyBody(
        byte[] helloAck,
        byte[] ready) =>
        $$"""
        (module
          (import "botarena" "next_observation"
            (func $next (param i32 i32) (result i32)))
          (import "botarena" "post_decision"
            (func $post (param i32 i32)))
          (memory (export "memory") 32)
          (data (i32.const {{ReplyBase}})
            "{{WatBytes(helloAck)}}")
          (data (i32.const {{ReplyBase + 1_000}})
            "{{WatBytes(ready)}}")
          (func (export "_start")
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase}})
              (i32.const {{helloAck.Length}}))
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase + 1_000}})
              (i32.const {{ready.Length}}))
            (drop (call $next (i32.const 0) (i32.const 1048576)))
            (call $post
              (i32.const {{ReplyBase + 1_000}})
              (i32.const {{ready.Length}}))
            (drop (call $next (i32.const 0) (i32.const 1048576)))))
        """;

    private static string WatBytes(IEnumerable<byte> bytes) =>
        string.Concat(bytes.Select(value => $"\\{value:x2}"));

    private enum ArtifactBehavior
    {
        Happy,
        DoubleReply,
        FuelHog,
        WrongTickReply,
    }

    internal sealed class TemporaryArtifact : IDisposable
    {
        public TemporaryArtifact(byte[] bytes)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nilbots-generic-actor-{Guid.NewGuid():N}.wasm");
            File.WriteAllBytes(Path, bytes);
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
