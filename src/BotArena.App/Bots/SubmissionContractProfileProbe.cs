using BotArena.App.Matches;
using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.Runtime;
using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;

namespace BotArena.App.Bots;

public sealed class SubmissionContractProfileProbe(
    MatchExecutionSettings matchSettings)
{
    public Result Probe(string wasmPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wasmPath);
        var supported = new List<string>(2);
        var failures = new List<string>(2);

        TryProbe(
            BotContractProfiles.LegacyDuel,
            () => ProbeLegacyDuel(wasmPath),
            supported,
            failures);
        TryProbe(
            BotArenaVersions.GenericActorContractProfileId,
            () => ProbeGenericActor(wasmPath),
            supported,
            failures);
        TryProbe(
            BotArenaVersions.GenericMindContractProfileId,
            () => ProbeGenericMind(wasmPath),
            supported,
            failures);

        return new Result(
            supported
                .Order(StringComparer.Ordinal)
                .ToArray(),
            string.Join(" ", failures));
    }

    private void ProbeLegacyDuel(string wasmPath)
    {
        string? builtin = RepoPaths.FindUpward(
            Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
        GameRules rules = matchSettings.MatchRules with { MaxTicks = 5 };
        ArenaMap map = ArenaMapLoader.Load("basic-01", rules);
        using var candidate =
            new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = wasmPath });
        using IBotRuntime idle = builtin is null
            ? new Runtime.InProcessBotRuntime(
                () => new BotArena.Bots.BuiltIn.IdleBot())
            : new WasmBotRuntime(new WasmRuntimeOptions
            {
                ModulePath = builtin,
                BotName = "idle",
            });
        MatchRunResult run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = 1,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = "candidate",
                    Runtime = candidate,
                },
                new MatchParticipantConfig
                {
                    Name = "idle",
                    Runtime = idle,
                },
            ],
        });
        BotMatchResult candidateResult = run.Result.Bots[0];
        if (candidateResult.Faults >= rules.FaultLimit)
        {
            throw new InvalidOperationException(
                "the bot faulted on every tick of the validation match " +
                "(it may crash at startup or return no action).");
        }
    }

    private static void ProbeGenericActor(string wasmPath)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        InitialLifeDeployment initialLife =
            definition.InitialDeployment.Lives[0];
        PublicUnitSlot unitSlot = definition.Topology.UnitSlots.Single(
            candidate =>
                candidate.TeamId == initialLife.TeamId &&
                candidate.UnitId == initialLife.UnitId);
        ActorUnitSlotLifecycleAssignmentDefinition lifecycle =
            definition.LifecycleAssignments.Single(
                candidate =>
                    candidate.TeamId == initialLife.TeamId &&
                    candidate.UnitId == initialLife.UnitId);
        int initialGeneration = lifecycle.InitialGeneration
            ?? throw new InvalidOperationException(
                "The Labs probe life must have an initial generation.");

        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = wasmPath,
                BotName = "candidate",
            });
        using IGenericActorRuntime runtime = factory.CreateRuntime();
        runtime.StartLife(new GenericActorRuntimeStart
        {
            SchemaVersion =
                definition.CapabilityVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                definition.CapabilityVersions.RuntimeContractVersion,
            ActorId = new ActorIdentity(
                initialLife.TeamId,
                initialLife.UnitId,
                initialLife.LifeId),
            ParticipantId = unitSlot.ControllerParticipantId,
            ActorRandomSeed = 1,
            TeamRandomSeed = 1,
            Origin = new GenericActorRuntimeStart.LifeOrigin(
                GenericActorRuntimeStart.SpawnReason.Initial,
                Generation: initialGeneration,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = definition,
        });
    }

    private static void ProbeGenericMind(string wasmPath)
    {
        string[] classes = ArcRelayPlayerSheetCodec.NewSheetTemplate().Slots
            .OrderBy(value => value.UnitId)
            .Select(value => value.ClassId)
            .ToArray();
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            classes,
            classes,
            loopProfile: ArcRelayLoopProfile.Current);
        var candidate = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions { ModulePath = wasmPath, BotName = "candidate" });
        var stock = new InProcessGenericMindRuntimeFactory(
            static () => new global::ArcRelayStrategyMind(),
            trustedArcRelayStockProjection: true);
        using var session = new GenericActorMatchSession(
            definition,
            [
                new GenericActorParticipantConfiguration
                {
                    ParticipantId = 0,
                    TeamId = 0,
                    Name = "candidate",
                    MindRuntimeFactory = candidate,
                    RuntimeKind = "wasm-preflight-probe",
                    ArtifactHash = candidate.ArtifactHash,
                },
                new GenericActorParticipantConfiguration
                {
                    ParticipantId = 1,
                    TeamId = 1,
                    Name = "stock",
                    MindRuntimeFactory = stock,
                    RuntimeKind = "trusted-stock-preflight-probe",
                    ArtifactHash = ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
                },
            ],
            matchSeed: 1,
            recordChronology: true);
        session.Step();
        GenericActorMatchMindTurn turn = session.Chronology.Ticks
            .SelectMany(value => value.MindTurns)
            .Single(value => value.ParticipantId == 0);
        if (turn.RuntimeFault is { } fault)
            throw new InvalidOperationException(fault.FaultCode);
    }

    private static void TryProbe(
        string profileId,
        Action probe,
        ICollection<string> supported,
        ICollection<string> failures)
    {
        try
        {
            probe();
            supported.Add(profileId);
        }
        catch (Exception exception)
        {
            failures.Add(
                $"{profileId}: {Tail(exception.Message, 1000)}");
        }
    }

    private static string Tail(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[^maxChars..];

    public sealed record Result(
        string[] SupportedContractProfiles,
        string FailureSummary);
}
