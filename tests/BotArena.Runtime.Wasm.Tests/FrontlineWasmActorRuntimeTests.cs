using System.Text.Json;
using BotArena.Bots.BuiltIn;
using BotArena.Engine;
using BotArena.Engine.Tests.Support;
using BotArena.Runtime;
using Wasmtime;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class FrontlineWasmActorRuntimeTests
{
    private static readonly string ArtifactPath = FindArtifact();
    private static readonly Lazy<ProbeRuns> Runs =
        new(RunProbeMatches, LazyThreadSafetyMode.ExecutionAndPublication);

    [SkippableFact]
    public void FrontlineProbe_WasmNegotiatesAndMatchesInProcessReplay()
    {
        RequireArtifact();
        ProbeRuns runs = Runs.Value;

        Assert.Equal(runs.InProcess.ReplayHash, runs.Wasm.ReplayHash);
        Assert.Equal(runs.InProcess.ReplayJson, runs.Wasm.ReplayJson);

        using JsonDocument document = JsonDocument.Parse(runs.Wasm.ReplayJson);
        JsonElement root = document.RootElement;
        JsonElement actorRuntime = root
            .GetProperty("header")
            .GetProperty("actorRuntime");

        Assert.Equal(
            "nilbots-actor",
            actorRuntime.GetProperty("family").GetString());
        Assert.Equal(
            BotArenaVersions.ActorRuntimeContractVersion,
            actorRuntime.GetProperty("version").GetInt32());
        Assert.Equal(3, root.GetProperty("ticks").GetArrayLength());
        Assert.Contains(
            root.GetProperty("ticks")
                .EnumerateArray()
                .SelectMany(tick =>
                    tick.GetProperty("actors").EnumerateArray())
                .Select(turn =>
                    turn.GetProperty("acceptedDecision")
                        .GetProperty("actionId")
                        .GetString()),
            actionId => string.Equals(
                actionId,
                PublicActionIds.Fabricate,
                StringComparison.Ordinal));
        Assert.All(
            root.GetProperty("ticks")
                .EnumerateArray()
                .SelectMany(tick =>
                    tick.GetProperty("actors").EnumerateArray()),
            turn => Assert.False(
                turn.GetProperty("runtimeReply")
                    .GetProperty("faulted")
                    .GetBoolean()));
    }

    [SkippableFact]
    public void FrontlineProbe_WasmKeepsLifeMemoryAndIsolatesFabricatedLives()
    {
        RequireArtifact();
        ProbeRuns runs = Runs.Value;

        using JsonDocument document = JsonDocument.Parse(runs.Wasm.ReplayJson);
        var observedCalls = new Dictionary<string, int>(
            StringComparer.Ordinal);

        foreach (JsonElement tick in document.RootElement
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            foreach (JsonElement turn in tick
                         .GetProperty("actors")
                         .EnumerateArray())
            {
                JsonElement actor = turn.GetProperty("actorId");
                string actorId = string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{actor.GetProperty("teamId").GetInt32()}:" +
                    $"{actor.GetProperty("unitId").GetInt32()}:" +
                    $"{actor.GetProperty("lifeId").GetInt32()}");
                int expectedCall =
                    observedCalls.GetValueOrDefault(actorId) + 1;
                string? debug = turn
                    .GetProperty("acceptedDecision")
                    .GetProperty("debugMessage")
                    .GetString();

                Assert.Equal(
                    $"actor={actorId};calls={expectedCall}",
                    debug);
                observedCalls[actorId] = expectedCall;
            }
        }

        Assert.Equal(3, observedCalls["0:0:0"]);
        Assert.Equal(3, observedCalls["1:0:0"]);
        Assert.Equal(1, observedCalls["0:1:0"]);
        Assert.Equal(1, observedCalls["1:1:0"]);
    }

    [SkippableFact]
    public void HistoricalProtocolArtifact_IsExplicitlyFrontlineIneligible()
    {
        RequireArtifact();
        string champion = FindRepoFile(
            "champions",
            "bastille-gen5",
            "bot.wasm");
        Skip.If(
            champion.Length == 0,
            "Historical champion artifact is missing.");
        GameRules rules =
            FrontlineTestDefinitions.ReplicationRules(maxTicks: 3);
        ArenaMap map = FrontlineTestDefinitions.ReplicationMapV2();
        using var historical = new WasmActorRuntimeFactory(
            new WasmRuntimeOptions { ModulePath = champion });
        using var actor = WasmFactory();

        FrontlineActorHostException error =
            Assert.Throws<FrontlineActorHostException>(
                () => Run(map, rules, historical, actor));

        Assert.Equal(
            FrontlineActorHostFaultCodes.RuntimeStartFailed,
            error.Code);
        Assert.IsType<ActorProtocolNotSupportedException>(
            error.GetBaseException());
    }

    [Fact]
    public void HostileGuest_CannotPostAReplyForAnUnconsumedRequest()
    {
        byte[] helloAck = Sdk.ActorWireProtocol.EncodeHelloAck(
            Sdk.ActorWireProtocol.MajorVersion);
        byte[] forgedReady = Sdk.ActorWireProtocol.EncodeReady(
            Sdk.ActorWireProtocol.MajorVersion,
            Sdk.ActorContractVersions.RuntimeContractVersion,
            Sdk.ActorContractVersions.MatchStartSchemaVersion,
            Sdk.ActorContractVersions.ObservationSchemaVersion,
            Sdk.ActorContractVersions.DecisionSchemaVersion);
        byte[] artifact = Module.ConvertText(
            $$"""
            (module
              (import "botarena" "next_observation"
                (func $next (param i32 i32) (result i32)))
              (import "botarena" "post_decision"
                (func $post (param i32 i32)))
              (memory (export "memory") 1)
              (data (i32.const 1024) "{{WatBytes(helloAck)}}")
              (data (i32.const 2048) "{{WatBytes(forgedReady)}}")
              (func (export "_start")
                (drop (call $next (i32.const 0) (i32.const 65536)))
                (call $post
                  (i32.const 1024)
                  (i32.const {{helloAck.Length}}))
                (call $post
                  (i32.const 2048)
                  (i32.const {{forgedReady.Length}}))
                (loop $spin (br $spin))))
            """);
        string path = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-double-reply-{Guid.NewGuid():N}.wasm");
        File.WriteAllBytes(path, artifact);
        try
        {
            using var factory = new WasmActorRuntimeFactory(
                new WasmRuntimeOptions
                {
                    ModulePath = path,
                    TickTimeoutMs = 1_000,
                });
            using IActorRuntime runtime = factory.CreateRuntime();

            InvalidOperationException error =
                Assert.Throws<InvalidOperationException>(
                    () => runtime.StartLife(CreateMatchStart()));

            Assert.Contains(
                "unsolicited or duplicate",
                error.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ProbeRuns RunProbeMatches()
    {
        GameRules rules = FrontlineTestDefinitions.ReplicationRules(
            maxTicks: 3);
        ArenaMap map = FrontlineTestDefinitions.ReplicationMapV2();

        using var wasmTeamZero = WasmFactory();
        using var wasmTeamOne = WasmFactory();
        FrontlineActorMatchRunResult wasm = Run(
            map,
            rules,
            wasmTeamZero,
            wasmTeamOne);

        FrontlineActorMatchRunResult inProcess = Run(
            map,
            rules,
            new InProcessActorRuntimeFactory(
                () => new FrontlineProbeBot()),
            new InProcessActorRuntimeFactory(
                () => new FrontlineProbeBot()));

        return new ProbeRuns(wasm, inProcess);
    }

    private static FrontlineActorMatchRunResult Run(
        ArenaMap map,
        GameRules rules,
        IActorRuntimeFactory teamZero,
        IActorRuntimeFactory teamOne) =>
        new FrontlineActorMatchEngine().Run(
            new FrontlineActorMatchConfiguration
            {
                Map = map,
                Rules = rules,
                Seed = 42,
                Participants =
                [
                    Participant(0, 0, teamZero),
                    Participant(1, 1, teamOne),
                ],
            });

    private static ActorParticipantConfiguration Participant(
        int participantId,
        int teamId,
        IActorRuntimeFactory factory) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = $"probe-{participantId}",
            RuntimeFactory = factory,
            RuntimeKind = "actor-contract-test",
            ArtifactHash = "frontline-probe",
            Accent = participantId == 0 ? "#00aaff" : "#ff5500",
        };

    private static WasmActorRuntimeFactory WasmFactory() =>
        new(new WasmRuntimeOptions
        {
            ModulePath = ArtifactPath,
            BotName = "frontline-probe",
        });

    private static void RequireArtifact() =>
        Skip.If(
            ArtifactPath.Length == 0,
            "WASM guest artifact missing — run scripts/build-wasm-guest.sh");

    private static ActorMatchStart CreateMatchStart()
    {
        GameRules rules =
            FrontlineTestDefinitions.ReplicationRules(maxTicks: 3);
        ArenaMap map = FrontlineTestDefinitions.ReplicationMapV2();
        return new ActorMatchStart
        {
            SchemaVersion =
                BotArenaVersions.ActorMatchStartSchemaVersion,
            RuntimeContractVersion =
                BotArenaVersions.ActorRuntimeContractVersion,
            ActorId = new ActorIdentity(0, 0, 0),
            ParticipantId = 0,
            ActorRandomSeed = 42,
            SpawnReason = ActorSpawnReason.Initial,
            Contract = PublicRulesManifestFactory.CreateMatchContract(
                rules,
                map),
        };
    }

    private static string WatBytes(IEnumerable<byte> bytes) =>
        string.Concat(bytes.Select(value => $"\\{value:x2}"));

    private static string FindArtifact()
        => FindRepoFile(
            "artifacts",
            "wasm",
            "builtin-bots.wasm");

    private static string FindRepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                [directory.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return "";
    }

    private sealed record ProbeRuns(
        FrontlineActorMatchRunResult Wasm,
        FrontlineActorMatchRunResult InProcess);
}
