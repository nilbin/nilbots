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
        if (options.ContainsKey("swap"))
            (botSpec, opponentSpec) = (opponentSpec, botSpec); // play as slot 1
        string mapId = options.GetValueOrDefault("map", "basic-01");
        string runtimeKind = options.GetValueOrDefault("runtime", "wasm");

        // Pin lookup uses the pre-swap specs: --swap changes which slot you play,
        // not whose project this is.
        var rules = CliSupport.ResolveRules(options,
            options.GetValueOrDefault("bot"), options.GetValueOrDefault("opponent"));

        // --seeds a,b,c batches N matches into one process: one CLI startup, one build,
        // one summary table — the iteration loop the gen-2 agents asked for (findings #5).
        ulong[] seeds = options.TryGetValue("seeds", out string? seedList)
            ? seedList.Split(',').Select(s => ulong.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray()
            : [ulong.Parse(options.GetValueOrDefault("seed", "42"), CultureInfo.InvariantCulture)];

        var map = CliSupport.LoadMap(mapId);
        Console.WriteLine($"Runtime:          {runtimeKind}");
        Console.WriteLine($"Game rules:       {rules.RulesVersion}");
        Console.WriteLine($"Runtime protocol: {BotArenaVersions.RuntimeProtocolVersion}");
        Console.WriteLine($"Map:              {map.Id} v{map.Version} ({map.Width}x{map.Height})");
        Console.WriteLine();

        int wins = 0, losses = 0, draws = 0;
        string slot0Name = "slot 0";
        foreach (ulong seed in seeds)
        {
            var (run, name0, name1, written, fuelNote) = RunSingle(
                botSpec, opponentSpec, map, seed, runtimeKind, rules,
                options.GetValueOrDefault("out"), quiet: seeds.Length > 1,
                mirrored: options.ContainsKey("swap"));
            if (seeds.Length == 1)
            {
                Console.WriteLine($"Seed:             {seed}");
                Console.WriteLine($"Match:            {name0} vs {name1}");
                Console.WriteLine();
                PrintResult(run, name0, name1);
                if (fuelNote is not null)
                    Console.WriteLine($"Fuel:    {fuelNote}");
                Console.WriteLine();
                Console.WriteLine($"Replay:  {written.ReplayPath}");
                Console.WriteLine(written.ViewerPath is not null
                    ? $"Viewer:  {written.ViewerPath}"
                    : "Viewer:  (web/dist not found — run `npm run build` in web/ for the visual viewer)");
            }
            else
            {
                string verdict = run.Result.WinnerSlot switch
                {
                    0 => $"{name0} wins", 1 => $"{name1} wins", _ => "draw",
                };
                _ = run.Result.WinnerSlot switch
                {
                    0 => wins++, 1 => losses++, _ => draws++,
                };
                Console.WriteLine($"seed {seed,-12} {verdict,-22} {run.Result.Reason,-16} t{run.Result.EndTick,-4} {written.ReplayPath}");
            }
            slot0Name = name0;
        }
        if (seeds.Length > 1)
        {
            Console.WriteLine();
            // Name the perspective explicitly: under --swap, slot 0 is the OPPONENT,
            // and a bare "slot-0 perspective" read as inverted totals (gen-6 finding).
            Console.WriteLine($"Total ({seeds.Length} seeds, W = {slot0Name} wins): {wins}W {losses}L {draws}D");
        }
        return 0;
    }

    /// <summary>Runs one match with freshly resolved runtimes (never reused across
    /// matches) and writes its replay. Shared by play, --seeds batches, and `set`.</summary>
    internal static (MatchRunResult Run, string Name0, string Name1, WrittenReplay Written, string? FuelNote) RunSingle(
        string botSpec, string opponentSpec, ArenaMap map, ulong seed,
        string runtimeKind, GameRules rules, string? outDirOverride, bool quiet,
        bool mirrored = false)
    {
        using var bot0 = ResolvedBot.Resolve(botSpec, runtimeKind, quiet);
        using var bot1 = ResolvedBot.Resolve(opponentSpec, runtimeKind, quiet);
        // Default output is unique per matchup so parallel runs (or two terminals in the
        // same project) never clobber each other's replays; identical reruns of a
        // deterministic match land in the same place, which is the right kind of overwrite.
        // Same-name matchups (mirror rehearsal) collapse both orientations to one name,
        // so the swapped game gets a -mirror suffix (gen-4 finding: g2 clobbered g1).
        string suffix = mirrored && Slug(bot0.Name) == Slug(bot1.Name) ? "-mirror" : "";
        string outDir = outDirOverride
            ?? Path.Combine("out", $"{Slug(bot0.Name)}-vs-{Slug(bot1.Name)}-{Slug(map.Id)}-s{seed}{suffix}");
        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = seed,
            Participants = [bot0.ToParticipant(), bot1.ToParticipant()],
        });
        var written = ReplayOutput.Write(run.Replay, outDir);
        // Peak per-tick fuel (gen-3: fuel DQs were undiagnosable). Captured before
        // dispose; only WASM participants meter fuel.
        var fuelParts = new[] { bot0, bot1 }
            .Where(b => b.Runtime is WasmBotRuntime)
            .Select(b => $"{b.Name} {((WasmBotRuntime)b.Runtime).MaxFuelUsedPerTick / 1_000_000.0:F1}M")
            .ToArray();
        string? fuelNote = fuelParts.Length > 0
            ? $"peak {string.Join(", ", fuelParts)} per tick (limit 200M)"
            : null;
        return (run, bot0.Name, bot1.Name, written, fuelNote);
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
        public required string LookId { get; init; }
        public required IBotRuntime Runtime { get; init; }
        public required string ArtifactHash { get; init; }
        /// <summary>The runtime this participant actually runs under — can differ from the
        /// match-level --runtime: a .wasm artifact always runs in the sandbox, so
        /// `--runtime in-process` sparring against a champion is a mixed-runtime match.</summary>
        public required string Kind { get; init; }

        public MatchParticipantConfig ToParticipant() => new()
        {
            Name = Name,
            Runtime = Runtime,
            RuntimeKind = Kind,
            ArtifactHash = ArtifactHash,
            Accent = Accent,
            LookId = LookId,
        };

        public void Dispose() => Runtime.Dispose();

        public static ResolvedBot Resolve(string spec, string runtimeKind, bool quiet = false)
        {
            if (BuiltInBotCatalog.Names.Contains(spec.ToLowerInvariant()) || spec.StartsWith("guest-", StringComparison.Ordinal))
                return ResolveBuiltIn(spec, runtimeKind, quiet);
            if (File.Exists(spec) && spec.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
            {
                // A prebuilt artifact has no sources, so it ALWAYS runs in the sandbox —
                // even in an in-process match. That mixed mode is the fast champion-sparring
                // loop: your bot skips fuel limits, the champion doesn't; verify all-WASM
                // before submitting.
                if (runtimeKind == "in-process" && !quiet)
                    Console.WriteLine($"note: {Path.GetFileName(spec)} is a prebuilt artifact — it runs in the " +
                                      "WASM sandbox (mixed runtimes, diagnostic).");
                // "bot.wasm" is every champion's file name; the parent directory is the
                // identity (champions/warden-gen1/bot.wasm → "warden-gen1"), otherwise
                // every champion collides on the label "bot" (gen-2 finding #4). A
                // project's own copy lives at <project>/out/bot.wasm — skip the "out".
                string stem = Path.GetFileNameWithoutExtension(spec);
                string name = stem;
                if (stem.Equals("bot", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(spec))!);
                    if (dir.Name == "out" && dir.Parent is not null)
                        dir = dir.Parent;
                    name = dir.Name;
                }
                return new ResolvedBot
                {
                    Name = name,
                    Accent = "#22d3ee",
                    LookId = "vanguard",
                    Runtime = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = Path.GetFullPath(spec) }),
                    ArtifactHash = BotBuilder.Sha256File(spec),
                    Kind = "wasm",
                };
            }
            if (Directory.Exists(spec) && BotProject.LooksLikeProject(spec))
            {
                var project = BotProject.Load(spec);
                if (runtimeKind == "in-process")
                {
                    // Fast inner loop (DECISIONS #44): plain assembly build, same engine.
                    return new ResolvedBot
                    {
                        Name = project.Manifest.Name,
                        Accent = project.Accent,
                        LookId = project.LookId,
                        Runtime = new InProcessBotRuntime(InProcessProject.LoadFactory(project, quiet)),
                        ArtifactHash = "",
                        Kind = "in-process",
                    };
                }
                var built = BotBuilder.EnsureBuilt(project, quiet: quiet);
                if (!quiet)
                    Console.WriteLine($"{project.Manifest.Name}: WASM artifact {built.ArtifactHash[..12]}… " +
                                      $"({(built.FromCache ? "cache" : "compiled")})");
                return new ResolvedBot
                {
                    Name = project.Manifest.Name,
                    Accent = project.Accent,
                    LookId = project.LookId,
                    Runtime = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = built.WasmPath }),
                    ArtifactHash = built.ArtifactHash,
                    Kind = "wasm",
                };
            }
            throw new InvalidOperationException(
                $"Cannot resolve bot '{spec}': not a built-in ({string.Join(", ", BuiltInBotCatalog.Names)}), " +
                "not a bot project directory, not a .wasm file.");
        }

        private static ResolvedBot ResolveBuiltIn(string name, string runtimeKind, bool quiet = false)
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
                        LookId = BuiltInBotCatalog.Look(name),
                        Runtime = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = artifact, BotName = name }),
                        ArtifactHash = BotBuilder.Sha256File(artifact),
                        Kind = "wasm",
                    };
                case "in-process":
                    if (!quiet)
                    {
                        Console.WriteLine("NOTE: in-process runtime is a diagnostic mode; it does not enforce");
                        Console.WriteLine("      fuel or memory limits and is not submission-equivalent (plan §3.1).");
                    }
                    return new ResolvedBot
                    {
                        Name = name,
                        Accent = BuiltInBotCatalog.Accent(name),
                        LookId = BuiltInBotCatalog.Look(name),
                        Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create(name)),
                        ArtifactHash = "",
                        Kind = "in-process",
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
