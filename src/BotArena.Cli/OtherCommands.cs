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
        // --full alone implies --summary (gen-6 finding: it silently printed only the
        // header as a sub-flag, which read as a bug).
        if (options.ContainsKey("summary") || options.ContainsKey("full"))
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
        // Zone-control matches (rules with ZoneControl): mark bots standing on zone
        // tiles with * in state lines and show per-bot zone-tick totals.
        var zone = (header.ZoneTiles ?? []).Select(t => (t[0], t[1])).ToHashSet();
        bool activeControl = header.ControlPressureLimit is not null;
        // The two 0.5 state channels (gen-6 blockers 6/3): bolts and cone contents
        // must be readable here, not only in the viewer.
        bool boltMode = document.Ticks.Any(t => t.Projectiles is { Count: > 0 });
        bool coneMode = header.VisionCone == true;
        Console.WriteLine($"Match:  {string.Join(" vs ", header.Participants.OrderBy(p => p.Slot).Select(p => $"{p.Name} (s{p.Slot})"))}");
        Console.WriteLine($"Map:    {header.MapId} v{header.MapVersion} ({header.MapWidth}x{header.MapHeight})  seed {header.Seed}  rules {header.GameRulesVersion}");
        if (zone.Count > 0)
            Console.WriteLine($"Zone:   {string.Join(" ", header.ZoneTiles!.Select(t => $"({t[0]},{t[1]})"))}  (* in state lines = on zone tile)");
        if (header.ControlPressureLimit is int pressureLimit)
            Console.WriteLine($"Control: successful Wait on-zone holds; signed slot-0 pressure, domination at ±{pressureLimit}");
        if (header.ControlOvertimeStartTick is int overtimeTick)
            Console.WriteLine($"Overtime: tick {overtimeTick}; domination at ±{header.ControlOvertimePressureLimit}" +
                              (header.ControlOvertimePressureGain is int overtimeGain
                                  ? $"; sole holds gain {overtimeGain}" : "") +
                              (header.ControlOvertimeStopsDecay == true ? "; abandoned pressure no longer decays" : ""));
        if (coneMode)
            Console.WriteLine("Vision: 90° cone toward facing + adjacent ring; `sees»`/`hears»` lines show each bot's actual contact");
        if (boltMode)
            Console.WriteLine("Bolts:  `bolts»` lines list flight state — xN = ordered tiles/advance, advN = ticks until advance, remN = tiles left");
        var result = document.Result;
        string verdict = result.WinnerSlot is int w ? $"{names[w]} (s{w}) wins" : "draw";
        Console.WriteLine($"Result: {verdict} — {result.Reason} at tick {result.EndTick}");
        if (result.ControlPressure is int finalPressure)
            Console.WriteLine($"        final control {(finalPressure > 0 ? "+" : "")}{finalPressure}");
        foreach (var bot in result.Bots.OrderBy(b => b.Slot))
        {
            int fired = document.Ticks.SelectMany(t => t.Events)
                .Count(e => e.Type == GameEventType.Shot && e.Slot == bot.Slot);
            int hits = document.Ticks.SelectMany(t => t.Events)
                .Count(e => e.Type == GameEventType.Damage && e.Slot == bot.Slot);
            Console.WriteLine($"        s{bot.Slot} {names[bot.Slot],-14} {bot.Outcome,-5} " +
                              $"health {bot.FinalHealth}  dealt {bot.DamageDealt}  " +
                              $"shots {fired} ({hits} hit)  faults {bot.Faults}" +
                              (bot.ZoneTicks is int zt ? $"  zone {zt}" : ""));
        }
        Console.WriteLine();
        Console.WriteLine("Timeline (post-tick state; actions were decided from the previous line's state):");

        int lastPrinted = -100;
        foreach (var tick in document.Ticks)
        {
            bool significant = full
                || tick.Events.Any(e => e.Type is GameEventType.Shot or GameEventType.Damage
                    or GameEventType.Destroyed or GameEventType.Fault or GameEventType.Disqualified)
                || tick.Tick == header.ControlOvertimeStartTick
                || (includeDebug && tick.Bots.Any(b => b.Debug is not null));
            // Periodic keep-alive lines so quiet phases still show movement (--full prints all).
            if (!significant && tick.Tick - lastPrinted < 25)
                continue;
            lastPrinted = tick.Tick;

            string state = string.Join(" ", tick.State.OrderBy(s => s.Slot).Select(s =>
                $"s{s.Slot}@({s.X},{s.Y}){(zone.Contains((s.X, s.Y)) ? "*" : "")}" +
                $"{s.Facing.ToString()[0]}h{s.Health}c{s.Cooldown}" +
                (s.Energy is int energy ? $"e{energy}" : "")));
            string actions = string.Join(" ", tick.Bots.OrderBy(b => b.Slot).Select(b =>
                b.ChosenAction == b.ValidatedAction ? $"{b.ChosenAction}" : $"{b.ChosenAction}→{b.ValidatedAction}"));
            string events = string.Join("; ", tick.Events
                .Where(e => e.Type is not (GameEventType.Turn or GameEventType.Move))
                .Select(FormatEvent));
            string line = $"t{tick.Tick,4} | {state} | {actions}";
            if (activeControl)
                line += $" | control {(tick.ControlPressure > 0 ? "+" : "")}{tick.ControlPressure ?? 0}";
            if (tick.Tick == header.ControlOvertimeStartTick)
                line += $" | OVERTIME ±{header.ControlOvertimePressureLimit}";
            if (events.Length > 0)
                line += $" | {events}";
            Console.WriteLine(line);
            if (tick.Projectiles is { Count: > 0 } inFlight)
                Console.WriteLine($"      bolts» {string.Join("  ", inFlight.Select(p =>
                    $"s{p.OwnerSlot}@({p.X},{p.Y}){p.Direction.ToString()[0]} x{p.TilesPerAdvance} adv{p.TicksUntilAdvance} rem{p.RemainingTiles}"))}");
            if (coneMode)
                foreach (var bot in tick.Bots.OrderBy(b => b.Slot))
                {
                    string sees = string.Join(" ", bot.VisibleEnemies
                        .Select(e => $"s{e.Slot}@({e.X},{e.Y}){e.Facing.ToString()[0]}h{e.Health}"));
                    string hears = string.Join(" ", (bot.HeardSounds ?? []).Select(h =>
                        $"{h.Type}:{"N,NE,E,SE,S,SW,W,NW".Split(',')[h.Bearing]}:{"near,med,far".Split(',')[h.Distance]}"));
                    if (sees.Length > 0)
                        Console.WriteLine($"      s{bot.Slot} sees» {sees}");
                    if (hears.Length > 0)
                        Console.WriteLine($"      s{bot.Slot} hears» {hears}");
                }
            if (includeDebug)
                foreach (var bot in tick.Bots.Where(b => b.Debug is not null).OrderBy(b => b.Slot))
                    foreach (var debugLine in bot.Debug!.Split('\n'))
                        Console.WriteLine($"      s{bot.Slot}» {(!full && debugLine.Length > 200 ? debugLine[..200] + "…" : debugLine)}");
        }
        return 0;

        string FormatEvent(GameEvent e) => e.Type switch
        {
            // Under projectile rules an unresolved Shot is a LAUNCH — the verdict
            // arrives as a later Damage event or never (gen-6 finding: "miss" at
            // launch time read as a bug for shots that then landed).
            GameEventType.Shot => $"Shot s{e.Slot} ({e.FromX},{e.FromY})->({e.ToX},{e.ToY})" +
                                  (e.HitSlot is int hit ? $" HIT s{hit}" : boltMode ? " launch" : " miss"),
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
