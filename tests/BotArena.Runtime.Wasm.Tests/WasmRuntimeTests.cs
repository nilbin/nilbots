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

    private static MatchRunResult Run(
        IBotRuntime bot0,
        IBotRuntime bot1,
        ulong seed,
        string map = "basic-01",
        GameRules? rules = null)
    {
        using (bot0 as IDisposable)
        using (bot1 as IDisposable)
        {
            return new MatchEngine().Run(new MatchConfiguration
            {
                Map = LoadMap(map),
                Rules = rules ?? GameRules.V0_1,
                Seed = seed,
                Participants =
                [
                    new MatchParticipantConfig { Name = "bot0", Runtime = bot0, RuntimeKind = "wasm" },
                    new MatchParticipantConfig { Name = "bot1", Runtime = bot1, RuntimeKind = "wasm" },
                ],
            });
        }
    }

    [Fact]
    public void ActiveControlObservation_EncodesLegacyZoneScoresAsAbsent()
    {
        string wire = WasmProtocol.FormatObservation(new BotObservation
        {
            Tick = 1,
            Slot = 0,
            Position = new Position(1, 1),
            Facing = Direction.North,
            Health = 3,
            Cooldown = 0,
            MapWidth = 5,
            MapHeight = 5,
            ZoneTiles = [new Position(1, 1)],
            ControlPressure = 4,
            ControlPressureLimit = 100,
            PreviousActionResult = ActionResult.Success,
            VisibleTiles = [],
            VisibleEnemies = [],
            VisibleEvents = [],
        });

        Assert.Contains(" ZT -1 -1 C 4 100", wire);
    }

    [Fact]
    public void ProgrammedShotWire_EncodesLimitsCurrentHeadingAndPrivateDecision()
    {
        string wire = WasmProtocol.FormatObservation(new BotObservation
        {
            Tick = 1,
            Slot = 0,
            Position = new Position(1, 1),
            Facing = Direction.East,
            Health = 3,
            Cooldown = 0,
            PreviousActionResult = ActionResult.Success,
            VisibleTiles = [],
            VisibleEnemies = [],
            VisibleProjectiles =
            [
                new ObservedProjectile(
                    new Position(2, 2),
                    Direction.East,
                    1,
                    2,
                    1,
                    6,
                    ProjectileHeading.NorthEast),
            ],
            ShotPrograms = new ShotProgramLimits(1, 4, 3, 3, 8, 1, 2),
            VisibleEvents = [],
        });

        Assert.Contains(" P 1 2:2:1:1:2:1:6", wire);
        Assert.EndsWith(" SP 1:4:3:3:8:1:2 PH 1 1", wire);

        var decision = WasmProtocol.ParseReply("A 4 SP -1:1:2:3:2 D YXJj");
        Assert.Equal(BotAction.Shoot, decision.Action);
        Assert.Equal(new ShotProgram(-1, 1, 2, 3, 2), decision.ShotProgram);
        Assert.Equal("arc", decision.DebugMessage);
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
    public void WasmAndInProcessRuntimes_ProduceIdenticalActiveControlMatch()
    {
        RequireArtifact();
        var rules = GameRules.Resolve("cone-active-bolt2");
        var wasm = Run(Wasm("hunter"), Wasm("wander"), seed: 17, rules: rules);
        var inProcess = new MatchEngine().Run(new MatchConfiguration
        {
            Map = LoadMap("basic-01"),
            Rules = rules,
            Seed = 17,
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
    public void WasmAndInProcessRuntimes_ProduceIdenticalProgrammedShotMatch()
    {
        RequireArtifact();
        var rules = GameRules.Resolve("cone-active-bolt2-arcs") with { MaxTicks = 20 };
        var wasm = Run(Wasm("guest-arc"), Wasm("idle"), seed: 17, rules: rules);
        var inProcess = new MatchEngine().Run(new MatchConfiguration
        {
            Map = LoadMap("basic-01"),
            Rules = rules,
            Seed = 17,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = "bot0", RuntimeKind = "wasm",
                    Runtime = new InProcessBotRuntime(() => new ArcSdkBot()),
                },
                new MatchParticipantConfig
                {
                    Name = "bot1", RuntimeKind = "wasm",
                    Runtime = new InProcessBotRuntime(() => new IdleBot()),
                },
            ],
        });

        Assert.Equal(
            new ShotProgram(0, 1, 2, 1, 2),
            wasm.Replay.Ticks[0].Bots[0].ShotProgram);
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

    private sealed class ArcSdkBot : BotArena.Sdk.IBot
    {
        public BotArena.Sdk.BotAction Tick(BotArena.Sdk.BotContext context) =>
            context.Tick == 0 && context.ShotPrograms is not null
                ? BotArena.Sdk.Actions.Shoot(new BotArena.Sdk.ShotProgram(0, 1, 2, 1, 2))
                : BotArena.Sdk.Actions.Wait();
    }

    [SkippableFact]
    public void BotUsingWallClockAndAmbientRandomness_IsStillDeterministic()
    {
        // guest-clock calls DateTime.UtcNow, Random.Shared and Environment.TickCount
        // every tick (all prohibited by §6.1). The host's WASI shims must make even
        // that bot fully deterministic — defense in depth below the analyzers.
        RequireArtifact();
        var first = Run(Wasm("guest-clock"), Wasm("hunter"), seed: 5);
        var second = Run(Wasm("guest-clock"), Wasm("hunter"), seed: 5);
        Assert.Equal(first.ReplayHash, second.ReplayHash);
        Assert.Contains(first.Replay.Ticks[0].Bots[0].Debug ?? "", second.Replay.Ticks[0].Bots[0].Debug ?? "");
    }

    [SkippableFact]
    public void DebugMessages_CrossTheWasmBoundary()
    {
        RequireArtifact();
        var run = Run(Wasm("hunter"), Wasm("idle"), seed: 42);
        Assert.Contains(run.Replay.Ticks, t => !string.IsNullOrEmpty(t.Bots[1].Debug));
    }
}
