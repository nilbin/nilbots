using BotArena.Bots.BuiltIn;
using BotArena.Engine;

namespace BotArena.Cli;

public static class ReplayCommand
{
    public static int Run(string file, IReadOnlyList<string> args)
    {
        var options = CliSupport.ParseOptions(args);
        string json = File.ReadAllText(file);
        var document = ReplaySerializer.FromJson(json); // Validates the format.
        if (options.ContainsKey("summary"))
            return Summarize(document,
                includeDebug: !options.ContainsKey("no-debug"),
                full: options.ContainsKey("full"));

        string outDir = options.GetValueOrDefault("out", Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".");
        Console.WriteLine($"Replay:  {document.Header.MapId} seed {document.Header.Seed} " +
                          $"({document.Ticks.Count} ticks, rules {document.Header.GameRulesVersion})");
        string? viewer = ReplayOutput.WriteViewer(json, outDir);
        if (viewer is null)
        {
            Console.Error.WriteLine("Viewer template not found — build it with `npm run build` in web/.");
            return 1;
        }
        Console.WriteLine($"Viewer:  {viewer}");
        return 0;
    }

    /// <summary>
    /// Compact, grep-friendly digest of a match (gen-2 findings: agents burned entire
    /// analysis turns parsing megabytes of replay JSON with ad-hoc scripts). Convention
    /// reminder, documented in docs/REPLAY-FORMAT.md: state lines show the POST-tick
    /// state; the actions on the same line were decided from the PREVIOUS line's state.
    /// </summary>
    private static int Summarize(ReplayDocument document, bool includeDebug, bool full = false)
    {
        var header = document.Header;
        var names = header.Participants.ToDictionary(p => p.Slot, p => p.Name);
        Console.WriteLine($"Match:  {string.Join(" vs ", header.Participants.OrderBy(p => p.Slot).Select(p => $"{p.Name} (s{p.Slot})"))}");
        Console.WriteLine($"Map:    {header.MapId} v{header.MapVersion} ({header.MapWidth}x{header.MapHeight})  seed {header.Seed}  rules {header.GameRulesVersion}");
        var result = document.Result;
        string verdict = result.WinnerSlot is int w ? $"{names[w]} (s{w}) wins" : "draw";
        Console.WriteLine($"Result: {verdict} — {result.Reason} at tick {result.EndTick}");
        foreach (var bot in result.Bots.OrderBy(b => b.Slot))
        {
            int fired = document.Ticks.SelectMany(t => t.Events)
                .Count(e => e.Type == GameEventType.Shot && e.Slot == bot.Slot);
            int hits = document.Ticks.SelectMany(t => t.Events)
                .Count(e => e.Type == GameEventType.Shot && e.Slot == bot.Slot && e.HitSlot is not null);
            Console.WriteLine($"        s{bot.Slot} {names[bot.Slot],-14} {bot.Outcome,-5} " +
                              $"health {bot.FinalHealth}  dealt {bot.DamageDealt}  " +
                              $"shots {fired} ({hits} hit)  faults {bot.Faults}");
        }
        Console.WriteLine();
        Console.WriteLine("Timeline (post-tick state; actions were decided from the previous line's state):");

        int lastPrinted = -100;
        foreach (var tick in document.Ticks)
        {
            bool significant = full
                || tick.Events.Any(e => e.Type is GameEventType.Shot or GameEventType.Damage
                    or GameEventType.Destroyed or GameEventType.Fault or GameEventType.Disqualified)
                || (includeDebug && tick.Bots.Any(b => b.Debug is not null));
            // Periodic keep-alive lines so quiet phases still show movement (--full prints all).
            if (!significant && tick.Tick - lastPrinted < 25)
                continue;
            lastPrinted = tick.Tick;

            string state = string.Join(" ", tick.State.OrderBy(s => s.Slot).Select(s =>
                $"s{s.Slot}@({s.X},{s.Y}){s.Facing.ToString()[0]}h{s.Health}c{s.Cooldown}" +
                (s.Energy is int energy ? $"e{energy}" : "")));
            string actions = string.Join(" ", tick.Bots.OrderBy(b => b.Slot).Select(b =>
                b.ChosenAction == b.ValidatedAction ? $"{b.ChosenAction}" : $"{b.ChosenAction}→{b.ValidatedAction}"));
            string events = string.Join("; ", tick.Events
                .Where(e => e.Type is not (GameEventType.Turn or GameEventType.Move))
                .Select(FormatEvent));
            string line = $"t{tick.Tick,4} | {state} | {actions}";
            if (events.Length > 0)
                line += $" | {events}";
            Console.WriteLine(line);
            if (includeDebug)
                foreach (var bot in tick.Bots.Where(b => b.Debug is not null).OrderBy(b => b.Slot))
                    foreach (var debugLine in bot.Debug!.Split('\n'))
                        Console.WriteLine($"      s{bot.Slot}» {(debugLine.Length > 100 ? debugLine[..100] + "…" : debugLine)}");
        }
        return 0;

        static string FormatEvent(GameEvent e) => e.Type switch
        {
            GameEventType.Shot => $"Shot s{e.Slot} ({e.FromX},{e.FromY})->({e.ToX},{e.ToY})" +
                                  (e.HitSlot is int hit ? $" HIT s{hit}" : " miss"),
            GameEventType.Damage => $"Damage s{e.TargetSlot} by s{e.Slot} (h->{e.NewHealth})",
            GameEventType.Destroyed => $"DESTROYED s{e.Slot}",
            GameEventType.Fault => $"Fault s{e.Slot}: {e.Message}",
            GameEventType.Disqualified => $"DISQUALIFIED s{e.Slot}",
            GameEventType.MoveBlocked => $"MoveBlocked s{e.Slot}",
            _ => e.Type.ToString(),
        };
    }
}

public static class VerifyCommand
{
    public static int Run(string file)
    {
        var document = ReplaySerializer.FromJson(File.ReadAllText(file));
        var rebuilt = new Replay
        {
            Header = document.Header,
            Ticks = document.Ticks,
            Result = document.Result,
        };
        string actual = ReplaySerializer.ComputeHash(rebuilt);
        Console.WriteLine($"Stored hash:   {document.ReplayHash}");
        Console.WriteLine($"Computed hash: {actual}");
        if (actual == document.ReplayHash)
        {
            Console.WriteLine("OK: replay content matches its hash.");
            return 0;
        }
        Console.Error.WriteLine("MISMATCH: replay content does not match its stored hash.");
        return 1;
    }
}

public static class ListCommand
{
    public static int Bots()
    {
        Console.WriteLine("Built-in bots:");
        foreach (var name in BuiltInBotCatalog.Names)
            Console.WriteLine($"  {name}");
        return 0;
    }

    public static int Maps()
    {
        string? mapsDir = CliSupport.FindUpward("maps");
        if (mapsDir is null)
        {
            Console.Error.WriteLine("No maps/ directory found.");
            return 1;
        }
        foreach (var file in Directory.EnumerateFiles(mapsDir, "*.json").Order())
        {
            var map = ArenaMap.FromJson(File.ReadAllText(file));
            Console.WriteLine($"  {map.Id,-12} {map.Width}x{map.Height} v{map.Version}");
        }
        return 0;
    }
}
