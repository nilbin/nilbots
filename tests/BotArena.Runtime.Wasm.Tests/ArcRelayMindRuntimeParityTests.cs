using System.Collections.Immutable;
using System.Security.Cryptography;
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
    [InlineData("hunter-v1.json", "wellwright-v1.json", false)]
    [InlineData("wellwright-drawbench-v1.json", "hunter-drawbench-v1.json", true)]
    public void TacticalMindProducesByteIdenticalReplayInProcessAndInWasm(
        string firstSheet,
        string secondSheet,
        bool drawbench)
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
            loopProfile: drawbench
                ? ArcRelayLoopProfile.Drawbench1
                : ArcRelayLoopProfile.AmbushWarren11);
        GenericActorReplayPresentation presentation =
            ArcRelayH0ReplayPresentation.Create(definition);

        string viaWasm;
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

        string inProcess = Run(
            definition,
            presentation,
            firstSheetCompilation,
            secondSheetCompilation,
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayTacticalPlaybookMind()),
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayTacticalPlaybookMind()));

        Assert.Equal(inProcess, viaWasm);
    }

    private static string Run(
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
        return GenericActorReplayDocument.Create(session, presentation)
            .ReplayHash;
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
