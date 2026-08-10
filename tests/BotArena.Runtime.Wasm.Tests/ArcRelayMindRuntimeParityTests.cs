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
/// real Warren contract, and real playbook packages through an entire match.
/// </summary>
public sealed class ArcRelayMindRuntimeParityTests
{
    [Theory]
    [InlineData("hunter-v1.json", "wellwright-v1.json", "ambush-warren-11", false)]
    [InlineData("wellwright-drawbench-v1.json", "hunter-drawbench-v1.json", "drawbench-1", true)]
    [InlineData("wellwright-tapline-v1.json", "hunter-tapline-v1.json", "tapline-1", false)]
    public void TacticalMindProducesByteIdenticalReplayInProcessAndInWasm(
        string firstSheet,
        string secondSheet,
        string loopProfile,
        bool assertDrawbenchHunterProgress)
    {
        string root = FindRepoRoot();
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
            loopProfile: loopProfile switch
            {
                "ambush-warren-11" => ArcRelayLoopProfile.AmbushWarren11,
                "drawbench-1" => ArcRelayLoopProfile.Drawbench1,
                "tapline-1" => ArcRelayLoopProfile.Tapline1,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(loopProfile), loopProfile, null),
            });
        GenericActorReplayPresentation presentation =
            ArcRelayH0ReplayPresentation.Create(definition);

        GenericActorReplayDocument viaWasm;
        using (var first = new WasmGenericMindRuntimeFactory(
                   new WasmMindRuntimeOptions { ModulePath = artifact }))
        using (var second = new WasmGenericMindRuntimeFactory(
                   new WasmMindRuntimeOptions { ModulePath = artifact }))
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
        if (assertDrawbenchHunterProgress)
            AssertDrawbenchHunterProgress(inProcess.CanonicalJson);
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
        Assert.True(result.EndTick > 0);
        return GenericActorReplayDocument.Create(session, presentation);
    }

    /// <summary>
    /// The exact owner-caught regression: Drawbench team 1 unit 0 used only
    /// three tiles and yielded to an A-B circuit on 59% of its commands. Keep
    /// this broad enough to allow tactical evolution while proving that an
    /// authored loop cannot silently become a stationary move order again.
    /// </summary>
    private static void AssertDrawbenchHunterProgress(string replayJson)
    {
        using JsonDocument replay = JsonDocument.Parse(replayJson);
        JsonElement root = replay.RootElement;
        var tiles = new HashSet<(int X, int Y)>();
        AddHunterTiles(root.GetProperty("initialFrame")
            .GetProperty("state").GetProperty("activeLives"), tiles);

        int commands = 0;
        int yields = 0;
        foreach (JsonElement tick in root.GetProperty("ticks").EnumerateArray())
        {
            AddHunterTiles(
                tick.GetProperty("postState").GetProperty("activeLives"),
                tiles);
            foreach (JsonElement turn in tick.GetProperty("mindTurns")
                         .EnumerateArray()
                         .Where(value => value.GetProperty("teamId").GetInt32()
                             == 1))
            foreach (JsonElement command in turn.GetProperty("commands")
                         .EnumerateArray()
                         .Where(value => value.GetProperty("unitId").GetInt32()
                             == 0))
            {
                commands++;
                if ((command.GetProperty("debugMessage").GetString() ?? "")
                    .Contains("oscillation-yield", StringComparison.Ordinal))
                {
                    yields++;
                }
            }
        }

        Assert.True(tiles.Count >= 20,
            $"Drawbench Hunter travelled only {tiles.Count} distinct tiles.");
        Assert.True(commands > 0);
        Assert.True(yields * 5 < commands,
            $"Drawbench Hunter yielded {yields}/{commands} commands.");
    }

    private static void AddHunterTiles(
        JsonElement lives,
        ISet<(int X, int Y)> tiles)
    {
        foreach (JsonElement life in lives.EnumerateArray())
        {
            JsonElement actor = life.GetProperty("actorId");
            if (actor.GetProperty("teamId").GetInt32() != 1
                || actor.GetProperty("unitId").GetInt32() != 0)
            {
                continue;
            }
            JsonElement position = life.GetProperty("position");
            tiles.Add((
                position.GetProperty("x").GetInt32(),
                position.GetProperty("y").GetInt32()));
        }
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

    private static TacticalPlaybookCompilation Compile(
        string root,
        string fileName) => ArcRelayTacticalPlaybookCompiler.Compile(
        Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "playbooks",
            fileName));

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
}
