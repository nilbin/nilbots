using System.Globalization;
using BotArena.Toolchain;
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
        string botSpec = options.GetValueOrDefault("bot", "hunter");
        string opponentSpec = options.GetValueOrDefault("opponent", "wander");
        string mapId = options.GetValueOrDefault("map", "basic-01");
        ulong seed = ulong.Parse(options.GetValueOrDefault("seed", "42"), CultureInfo.InvariantCulture);
        string runtimeKind = options.GetValueOrDefault("runtime", "wasm");

        var rules = GameRules.V0_1;
        if (options.TryGetValue("max-ticks", out string? maxTicks))
            rules = rules with { MaxTicks = int.Parse(maxTicks, CultureInfo.InvariantCulture) };

        var map = CliSupport.LoadMap(mapId);
        using var bot0 = ResolvedBot.Resolve(botSpec, runtimeKind);
        using var bot1 = ResolvedBot.Resolve(opponentSpec, runtimeKind);

        // Default output is unique per matchup so parallel runs (or two terminals in the
        // same project) never clobber each other's replays; identical reruns of a
        // deterministic match land in the same place, which is the right kind of overwrite.
        string outDir = options.GetValueOrDefault("out",
            Path.Combine("out", $"{Slug(bot0.Name)}-vs-{Slug(bot1.Name)}-{Slug(map.Id)}-s{seed}"));

        Console.WriteLine($"Runtime:          {runtimeKind}");
        Console.WriteLine($"Game rules:       {rules.RulesVersion}");
        Console.WriteLine($"Runtime protocol: {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"Map:              {map.Id} v{map.Version} ({map.Width}x{map.Height})");
        Console.WriteLine($"Seed:             {seed}");
        Console.WriteLine($"Match:            {bot0.Name} vs {bot1.Name}");
        Console.WriteLine();

        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = seed,
            Participants = [bot0.ToParticipant(runtimeKind), bot1.ToParticipant(runtimeKind)],
        });

        PrintResult(run, bot0.Name, bot1.Name);
        var written = ReplayOutput.Write(run.Replay, outDir);
        Console.WriteLine();
        Console.WriteLine($"Replay:  {written.ReplayPath}");
        Console.WriteLine(written.ViewerPath is not null
            ? $"Viewer:  {written.ViewerPath}"
            : "Viewer:  (web/dist not found — run `npm run build` in web/ for the visual viewer)");
        return 0;
    }

    private static string Slug(string name)
    {
        var slug = new string(name.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return slug.Length == 0 ? "bot" : slug;
    }

    /// <summary>
    /// A participant resolved from a CLI spec: a built-in name ("hunter"), a bot project
    /// directory (built through the official toolchain, cached), or a path to a .wasm artifact.
    /// </summary>
    private sealed class ResolvedBot : IDisposable
    {
        public required string Name { get; init; }
        public required string Accent { get; init; }
        public required IBotRuntime Runtime { get; init; }
        public required string ArtifactHash { get; init; }

        public MatchParticipantConfig ToParticipant(string runtimeKind) => new()
        {
            Name = Name,
            Runtime = Runtime,
            RuntimeKind = runtimeKind,
            ArtifactHash = ArtifactHash,
            Accent = Accent,
        };

        public void Dispose() => Runtime.Dispose();

        public static ResolvedBot Resolve(string spec, string runtimeKind)
        {
            if (BuiltInBotCatalog.Names.Contains(spec.ToLowerInvariant()) || spec.StartsWith("guest-", StringComparison.Ordinal))
                return ResolveBuiltIn(spec, runtimeKind);
            if (File.Exists(spec) && spec.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
            {
                RequireWasmRuntime(runtimeKind, spec);
                return new ResolvedBot
                {
                    Name = Path.GetFileNameWithoutExtension(spec),
                    Accent = "#22d3ee",
                    Runtime = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = Path.GetFullPath(spec) }),
                    ArtifactHash = BotBuilder.Sha256File(spec),
                };
            }
            if (Directory.Exists(spec) && BotProject.LooksLikeProject(spec))
            {
                RequireWasmRuntime(runtimeKind, spec);
                var project = BotProject.Load(spec);
                var built = BotBuilder.EnsureBuilt(project);
                Console.WriteLine($"{project.Manifest.Name}: WASM artifact {built.ArtifactHash[..12]}… " +
                                  $"({(built.FromCache ? "cache" : "compiled")})");
                return new ResolvedBot
                {
                    Name = project.Manifest.Name,
                    Accent = project.Accent,
                    Runtime = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = built.WasmPath }),
                    ArtifactHash = built.ArtifactHash,
                };
            }
            throw new InvalidOperationException(
                $"Cannot resolve bot '{spec}': not a built-in ({string.Join(", ", BuiltInBotCatalog.Names)}), " +
                "not a bot project directory, not a .wasm file.");
        }

        private static void RequireWasmRuntime(string runtimeKind, string spec)
        {
            if (runtimeKind != "wasm")
                throw new InvalidOperationException(
                    $"'{spec}' is a WASM artifact/project; --runtime {runtimeKind} only supports built-in bots.");
        }

        private static ResolvedBot ResolveBuiltIn(string name, string runtimeKind)
        {
            switch (runtimeKind)
            {
                case "wasm":
                    string? artifact = CliSupport.FindUpward(Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
                    if (artifact is null)
                        throw new InvalidOperationException(
                            "WASM artifact not found — run scripts/build-wasm-guest.sh first, " +
                            "or use --runtime in-process (diagnostic mode, not submission-equivalent).");
                    return new ResolvedBot
                    {
                        Name = name,
                        Accent = BuiltInBotCatalog.Accent(name),
                        Runtime = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = artifact, BotName = name }),
                        ArtifactHash = BotBuilder.Sha256File(artifact),
                    };
                case "in-process":
                    Console.WriteLine("NOTE: in-process runtime is a diagnostic mode; it does not enforce");
                    Console.WriteLine("      fuel or memory limits and is not submission-equivalent (plan §3.1).");
                    return new ResolvedBot
                    {
                        Name = name,
                        Accent = BuiltInBotCatalog.Accent(name),
                        Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create(name)),
                        ArtifactHash = "",
                    };
                default:
                    throw new InvalidOperationException($"Unknown runtime '{runtimeKind}' (use wasm or in-process).");
            }
        }
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
