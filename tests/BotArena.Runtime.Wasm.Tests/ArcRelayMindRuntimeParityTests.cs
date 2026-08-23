using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using BotArena.TacticalPlaybooks;

namespace BotArena.Runtime.Wasm.Tests;

/// <summary>
/// Product-grade parity proof for the generic MIND profile. The scripted seam
/// test catches codec shape changes; this test runs the shipped tactical mind,
/// current Arc Relay contract, and real Orders packages through an entire match.
/// </summary>
public sealed class ArcRelayMindRuntimeParityTests
{
    [Fact]
    [Trait("Category", "ReleaseWasm")]
    public void ReleaseGate_TacticalMindProducesByteIdenticalReplayInProcessAndInWasm()
    {
        if (!ReleaseWasmRequired())
            return;

        string root = FindRepoRoot();
        const string firstSheet = "tempered-broadloom-v1.json";
        const string secondSheet = "broadloom-ball-v1.json";
        string artifact = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-mind-v1",
            "bot.wasm");
        TacticalPlaybookCompilation firstSheetCompilation = Compile(
            root, firstSheet);
        TacticalPlaybookCompilation secondSheetCompilation = Compile(
            root, secondSheet);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            firstSheetCompilation.Composition,
            secondSheetCompilation.Composition,
            loopProfile: ArcRelayLoopProfile.Current);
        GenericActorReplayPresentation presentation =
            ArcRelayH0ReplayPresentation.Create(definition);

        GenericActorReplayDocument viaWasm;
        using (var first = new WasmGenericMindRuntimeFactory(
                   ParityRuntimeOptions(artifact)))
        using (var second = new WasmGenericMindRuntimeFactory(
                   ParityRuntimeOptions(artifact)))
        {
            viaWasm = Run(
                definition,
                presentation,
                firstSheetCompilation,
                secondSheetCompilation,
                first,
                second);
            Assert.All(
                first.Diagnostics.Concat(second.Diagnostics),
                diagnostic => Assert.Null(diagnostic.FailureReason));
        }

        GenericActorReplayDocument inProcess = Run(
            definition,
            presentation,
            firstSheetCompilation,
            secondSheetCompilation,
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayTacticalPlaybookMind()),
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayTacticalPlaybookMind()));

        Assert.Equal(inProcess.ReplayHash, viaWasm.ReplayHash);
        AssertCurrentMatchHasNoTerminalDeliveryFallback(inProcess.CanonicalJson);
    }

    private static GenericActorReplayDocument Run(
        ActorResolvedMatchDefinition definition,
        GenericActorReplayPresentation presentation,
        TacticalPlaybookCompilation first,
        TacticalPlaybookCompilation second,
        IGenericMindRuntimeFactory firstFactory,
        IGenericMindRuntimeFactory secondFactory)
    {
        using var session = new GenericActorMatchSession(
            definition,
            [
                Participant(0, first, firstFactory),
                Participant(1, second, secondFactory),
            ],
            matchSeed: 9001,
            recordChronology: true);
        GenericActorMatchResult result = session.Run();
        if (result.EndTick is not > 0)
        {
            session.Dispose();
            string diagnostics = string.Join(
                " | ",
                new[] { firstFactory, secondFactory }
                    .OfType<WasmGenericMindRuntimeFactory>()
                    .SelectMany(factory => factory.Diagnostics)
                    .Select(value => value.FailureReason ?? "no failure"));
            Assert.Fail(
                $"match ended before tick zero: {result.CompletionReason}; "
                + diagnostics);
        }
        return GenericActorReplayDocument.Create(session, presentation);
    }

    /// <summary>
    /// A historical allied queue ended in the terminal delivery-timeout
    /// fallback. Stationary spans are not a sound proxy on current Tactics —
    /// Hold posts deliberately stay put — so pin the semantic failure itself.
    /// </summary>
    private static void AssertCurrentMatchHasNoTerminalDeliveryFallback(
        string replayJson)
    {
        using JsonDocument replay = JsonDocument.Parse(replayJson);
        JsonElement root = replay.RootElement;
        var timeoutTicks = new Dictionary<int, int>();
        foreach (JsonElement tick in root.GetProperty("ticks").EnumerateArray())
        {
            foreach (JsonElement turn in tick.GetProperty("mindTurns")
                         .EnumerateArray())
                foreach (JsonElement command in turn.GetProperty("commands")
                             .EnumerateArray())
                {
                    string reason = command.GetProperty("debugMessage")
                        .GetString() ?? "";
                    int team = turn.GetProperty("teamId").GetInt32();
                    if (reason.Contains(
                            "custody:delivery-timeout-",
                            StringComparison.Ordinal))
                    {
                        timeoutTicks[team] = timeoutTicks.GetValueOrDefault(team)
                            + 1;
                    }
                }
        }

        foreach (int team in new[] { 0, 1 })
            Assert.Equal(0, timeoutTicks.GetValueOrDefault(team));
    }

    private static GenericActorParticipantConfiguration Participant(
        int participantId,
        TacticalPlaybookCompilation playbook,
        IGenericMindRuntimeFactory factory) => new()
        {
            ParticipantId = participantId,
            TeamId = participantId,
            Name = participantId == 0 ? "Hunter" : "Wellwright",
            MindRuntimeFactory = factory,
            // Replay provenance is deliberately identical between the two host
            // implementations. Runtime choice is an execution detail; this is the
            // same participant artifact and data under the determinism promise.
            RuntimeKind = "tactical-mind-parity",
            ArtifactHash = "tactical-mind-parity-artifact",
            MindDataHash = Convert.ToHexStringLower(
            SHA256.HashData(playbook.LinkedData)),
            MindEvaluationData = ImmutableArray.Create(playbook.LinkedData),
            Accent = participantId == 0 ? "#22d3ee" : "#fb5360",
            LookId = "arc-relay-sheet",
            ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
        };

    /// <summary>
    /// Large materialized Tactics documents are a trusted in-process product
    /// surface, not public custom-mind qualification entrants. This semantic
    /// parity proof intentionally gives their NativeAOT mirror ample fuel; the
    /// sandbox budget has its own focused enforcement tests.
    /// </summary>
    private static WasmMindRuntimeOptions ParityRuntimeOptions(
        string artifact) => new()
        {
            ModulePath = artifact,
            BaseTickFuel = checked(
            (ulong)GenericMindTickBudget.StartupFuel
            + (ulong)GenericMindTickBudget.BaseTickFuel),
        };

    private static TacticalPlaybookCompilation Compile(
        string root,
        string fileName)
    {
        string playbook = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "playbooks",
            fileName);
        using JsonDocument playbookDocument = JsonDocument.Parse(
            File.ReadAllBytes(playbook));
        string mapPlan = Path.GetFullPath(
            playbookDocument.RootElement.GetProperty("mapPlan").GetString()!,
            Path.GetDirectoryName(playbook)!);
        return ArcRelayTacticalPlaybookCompiler.Compile(
            File.ReadAllBytes(playbook),
            File.ReadAllBytes(mapPlan),
            playbook,
            mapPlan);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root is missing.");
    }

    private static bool ReleaseWasmRequired() =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "BOTARENA_REQUIRE_CURRENT_TACTICAL_WASM"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
