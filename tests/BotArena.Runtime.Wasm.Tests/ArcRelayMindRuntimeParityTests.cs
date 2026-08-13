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
/// real Tapline contract, and real Orders packages through an entire match.
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
        const string firstSheet = "hunter-tapline-v2.json";
        const string secondSheet = "wellwright-tapline-v2.json";
        const string loopProfile = "tapline-1";
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
        AssertTaplineHasNoCarrierTrafficDeadlock(inProcess.CanonicalJson);
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
    /// The exact owner-caught Tapline regression. Both squads formed an
    /// allied queue behind a loaded carrier, the carrier entered a terminal
    /// delivery fallback, and five or six bodies per side remained on one
    /// tile for more than 100 ticks. Keep the threshold behavioral rather
    /// than hash-shaped so tactical evolution remains possible.
    /// </summary>
    private static void AssertTaplineHasNoCarrierTrafficDeadlock(
        string replayJson)
    {
        using JsonDocument replay = JsonDocument.Parse(replayJson);
        JsonElement root = replay.RootElement;
        var runs = new Dictionary<(int Team, int Unit, int Life),
            ((int X, int Y) Position, int Ticks)>();
        var longest = new Dictionary<(int Team, int Unit), int>();
        ObserveLives(
            root.GetProperty("initialFrame")
                .GetProperty("state").GetProperty("activeLives"),
            runs,
            longest);

        var timeoutTicks = new Dictionary<int, int>();
        foreach (JsonElement tick in root.GetProperty("ticks").EnumerateArray())
        {
            ObserveLives(
                tick.GetProperty("postState").GetProperty("activeLives"),
                runs,
                longest);
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

        // Do not require a particular recovery mechanism to fire here. The
        // coordinated planner may prevent the allied queue from forming at
        // all; the product invariant is that no carrier traffic deadlock or
        // terminal delivery fallback survives in the completed match.
        foreach (int team in new[] { 0, 1 })
        {
            int worst = longest
                .Where(value => value.Key.Team == team)
                .Select(value => value.Value)
                .DefaultIfEmpty()
                .Max();
            int over100 = longest.Count(value =>
                value.Key.Team == team && value.Value > 100);
            Assert.True(worst <= 100,
                $"Tapline team {team} stalled on one tile for {worst} ticks.");
            Assert.Equal(0, over100);
            Assert.Equal(0, timeoutTicks.GetValueOrDefault(team));
        }
    }

    private static void ObserveLives(
        JsonElement lives,
        IDictionary<(int Team, int Unit, int Life),
            ((int X, int Y) Position, int Ticks)> runs,
        IDictionary<(int Team, int Unit), int> longest)
    {
        foreach (JsonElement life in lives.EnumerateArray())
        {
            JsonElement actor = life.GetProperty("actorId");
            var key = (
                Team: actor.GetProperty("teamId").GetInt32(),
                Unit: actor.GetProperty("unitId").GetInt32(),
                Life: actor.GetProperty("lifeId").GetInt32());
            JsonElement tile = life.GetProperty("position");
            var position = (
                X: tile.GetProperty("x").GetInt32(),
                Y: tile.GetProperty("y").GetInt32());
            int ticks = runs.TryGetValue(key, out var prior)
                && prior.Position == position
                    ? prior.Ticks + 1
                    : 0;
            runs[key] = (position, ticks);
            var unit = (key.Team, key.Unit);
            int priorLongest = longest.TryGetValue(unit, out int value)
                ? value
                : 0;
            longest[unit] = Math.Max(priorLongest, ticks);
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

    private static bool ReleaseWasmRequired() =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "BOTARENA_REQUIRE_CURRENT_TACTICAL_WASM"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
