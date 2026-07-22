using System.Globalization;
using BotArena.Bots.BuiltIn;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;

namespace BotArena.Cli;

public static class PlayCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        var options = CliSupport.ParseOptions(args);
        string botName = options.GetValueOrDefault("bot", "hunter");
        string opponentName = options.GetValueOrDefault("opponent", "wander");
        string mapId = options.GetValueOrDefault("map", "basic-01");
        ulong seed = ulong.Parse(options.GetValueOrDefault("seed", "42"), CultureInfo.InvariantCulture);
        string runtimeKind = options.GetValueOrDefault("runtime", "wasm");
        string outDir = options.GetValueOrDefault("out", "out");

        var rules = GameRules.V0_1;
        if (options.TryGetValue("max-ticks", out string? maxTicks))
            rules = rules with { MaxTicks = int.Parse(maxTicks, CultureInfo.InvariantCulture) };

        var map = CliSupport.LoadMap(mapId);
        using var runtime0 = CreateRuntime(runtimeKind, botName);
        using var runtime1 = CreateRuntime(runtimeKind, opponentName);

        Console.WriteLine($"Runtime:          {runtimeKind}");
        Console.WriteLine($"Game rules:       {rules.RulesVersion}");
        Console.WriteLine($"Runtime protocol: {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"Map:              {map.Id} v{map.Version} ({map.Width}x{map.Height})");
        Console.WriteLine($"Seed:             {seed}");
        Console.WriteLine($"Match:            {botName} vs {opponentName}");
        Console.WriteLine();

        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = seed,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = botName,
                    Runtime = runtime0.Runtime,
                    RuntimeKind = runtimeKind,
                    ArtifactHash = runtime0.ArtifactHash,
                    Accent = BuiltInBotCatalog.Accent(botName),
                },
                new MatchParticipantConfig
                {
                    Name = opponentName,
                    Runtime = runtime1.Runtime,
                    RuntimeKind = runtimeKind,
                    ArtifactHash = runtime1.ArtifactHash,
                    Accent = BuiltInBotCatalog.Accent(opponentName),
                },
            ],
        });

        PrintResult(run, botName, opponentName);
        var written = ReplayOutput.Write(run.Replay, outDir);
        Console.WriteLine();
        Console.WriteLine($"Replay:  {written.ReplayPath}");
        Console.WriteLine(written.ViewerPath is not null
            ? $"Viewer:  {written.ViewerPath}"
            : "Viewer:  (web/dist not found — run `npm run build` in web/ for the visual viewer)");
        return 0;
    }

    private sealed record DisposableRuntime(IBotRuntime Runtime, string ArtifactHash) : IDisposable
    {
        public void Dispose() => Runtime.Dispose();
    }

    private static DisposableRuntime CreateRuntime(string kind, string botName)
    {
        switch (kind)
        {
            case "wasm":
                string? artifact = CliSupport.FindUpward(Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
                if (artifact is null)
                    throw new InvalidOperationException(
                        "WASM artifact not found — run scripts/build-wasm-guest.sh first, " +
                        "or use --runtime in-process (diagnostic mode, not submission-equivalent).");
                return new DisposableRuntime(
                    new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = artifact, BotName = botName }),
                    Sha256Of(artifact));
            case "in-process":
                Console.WriteLine("NOTE: in-process runtime is a diagnostic mode; it does not enforce");
                Console.WriteLine("      fuel or memory limits and is not submission-equivalent (plan §3.1).");
                return new DisposableRuntime(
                    new InProcessBotRuntime(() => BuiltInBotCatalog.Create(botName)), "");
            default:
                throw new InvalidOperationException($"Unknown runtime '{kind}' (use wasm or in-process).");
        }
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private static void PrintResult(MatchRunResult run, string bot0, string bot1)
    {
        var result = run.Result;
        string verdict = result.WinnerSlot switch
        {
            0 => $"{bot0} (slot 0) wins",
            1 => $"{bot1} (slot 1) wins",
            _ => "draw",
        };
        Console.WriteLine($"Result:  {verdict} — {result.Reason} at tick {result.EndTick}");
        foreach (var bot in result.Bots)
        {
            string name = bot.Slot == 0 ? bot0 : bot1;
            Console.WriteLine(
                $"  slot {bot.Slot} {name,-8} {bot.Outcome,-5} health {bot.FinalHealth} " +
                $"damage {bot.DamageDealt} faults {bot.Faults} ({bot.FinalStatus})");
        }
        Console.WriteLine($"Ticks:   {run.Replay.Ticks.Count}");
        Console.WriteLine($"Hash:    {run.ReplayHash}");
    }
}
