using BotArena.Bots.BuiltIn;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;

namespace BotArena.Runtime.Wasm.Tests;

/// <summary>
/// Phase 0C proof (plan §16): the same bots, the same engine, executed as WASM artifacts
/// under official limits. Tests skip when the guest artifact has not been built —
/// run scripts/build-wasm-guest.sh first.
/// </summary>
public class WasmRuntimeTests
{
    private static readonly string ArtifactPath = FindArtifact();

    private static string FindArtifact()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "artifacts", "wasm", "builtin-bots.wasm");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return "";
    }

    private static void RequireArtifact() =>
        Skip.If(ArtifactPath.Length == 0,
            "WASM guest artifact missing — run scripts/build-wasm-guest.sh");

    private static ArenaMap LoadMap(string id) =>
        ArenaMap.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "maps", id + ".json")));

    private static WasmBotRuntime Wasm(string botName, ulong? fuelPerTick = null) =>
        new(new WasmRuntimeOptions
        {
            ModulePath = ArtifactPath,
            BotName = botName,
            FuelPerTick = fuelPerTick ?? 200_000_000,
        });

    private static MatchRunResult Run(IBotRuntime bot0, IBotRuntime bot1, ulong seed, string map = "basic-01")
    {
        using (bot0 as IDisposable)
        using (bot1 as IDisposable)
        {
            return new MatchEngine().Run(new MatchConfiguration
            {
                Map = LoadMap(map),
                Rules = GameRules.V0_1,
                Seed = seed,
                Participants =
                [
                    new MatchParticipantConfig { Name = "bot0", Runtime = bot0, RuntimeKind = "wasm" },
                    new MatchParticipantConfig { Name = "bot1", Runtime = bot1, RuntimeKind = "wasm" },
                ],
            });
        }
    }

    [SkippableFact]
    public void TwoWasmBots_CompleteAMatch()
    {
        RequireArtifact();
        var run = Run(Wasm("hunter"), Wasm("wander"), seed: 42);
        Assert.True(run.Replay.Ticks.Count > 0);
        Assert.NotNull(run.Result);
    }

    [SkippableFact]
    public void WasmMatch_IsDeterministicAcrossRuns()
    {
        RequireArtifact();
        var first = Run(Wasm("hunter"), Wasm("wander"), seed: 42);
        var second = Run(Wasm("hunter"), Wasm("wander"), seed: 42);
        Assert.Equal(first.ReplayHash, second.ReplayHash);
    }

    [SkippableFact]
    public void WasmAndInProcessRuntimes_ProduceIdenticalMatches()
    {
        // The runtime contract test (plan §40.4): same bots, same seed, different runtime.
        RequireArtifact();
        var wasm = Run(Wasm("hunter"), Wasm("wander"), seed: 7);
        var inProcess = new MatchEngine().Run(new MatchConfiguration
        {
            Map = LoadMap("basic-01"),
            Rules = GameRules.V0_1,
            Seed = 7,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = "bot0", RuntimeKind = "wasm",
                    Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create("hunter")),
                },
                new MatchParticipantConfig
                {
                    Name = "bot1", RuntimeKind = "wasm",
                    Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create("wander")),
                },
            ],
        });
        Assert.Equal(inProcess.ReplayHash, wasm.ReplayHash);
    }

    [SkippableFact]
    public void ThrowingWasmBot_FaultsAndIsDisqualified_WithoutCrashingTheHost()
    {
        RequireArtifact();
        var run = Run(Wasm("idle"), Wasm("guest-faulty"), seed: 1);
        Assert.Equal(0, run.Result.WinnerSlot);
        Assert.Equal(MatchEndReason.Disqualification, run.Result.Reason);
        Assert.Equal(BotStatus.Disqualified, run.Result.Bots[1].FinalStatus);
        Assert.Contains("FaultyBot", run.Replay.Ticks[0].Bots[1].Debug ?? "");
    }

    [SkippableFact]
    public void InfiniteLoopWasmBot_HitsFuelLimit_AndFaults()
    {
        RequireArtifact();
        // Small per-tick budget so the hog burns out quickly.
        var run = Run(Wasm("idle"), Wasm("guest-hog", fuelPerTick: 20_000_000), seed: 1);
        Assert.Equal(0, run.Result.WinnerSlot);
        Assert.Equal(MatchEndReason.Disqualification, run.Result.Reason);
        var faults = run.Replay.Ticks.SelectMany(t => t.Events)
            .Where(e => e.Type == GameEventType.Fault && e.Slot == 1)
            .ToList();
        Assert.NotEmpty(faults);
        Assert.Contains(faults, f => (f.Message ?? "").Contains("Fuel", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GuestRandom_MatchesEngineRandomStream()
    {
        // Bots using SDK randomness must see the exact stream the engine derives.
        // WanderBot's whole behavior is random-driven, so hash equality between the
        // in-process and WASM runs (test above) covers it; this pins the derivation too.
        RequireArtifact();
        var wasm = Run(Wasm("wander"), Wasm("coward"), seed: 123);
        var inProcess = new MatchEngine().Run(new MatchConfiguration
        {
            Map = LoadMap("basic-01"),
            Rules = GameRules.V0_1,
            Seed = 123,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = "bot0", RuntimeKind = "wasm",
                    Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create("wander")),
                },
                new MatchParticipantConfig
                {
                    Name = "bot1", RuntimeKind = "wasm",
                    Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create("coward")),
                },
            ],
        });
        Assert.Equal(inProcess.ReplayHash, wasm.ReplayHash);
    }

    [SkippableFact]
    public void DebugMessages_CrossTheWasmBoundary()
    {
        RequireArtifact();
        var run = Run(Wasm("hunter"), Wasm("idle"), seed: 42);
        Assert.Contains(run.Replay.Ticks, t => !string.IsNullOrEmpty(t.Bots[1].Debug));
    }
}
