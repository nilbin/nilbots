using BotArena.Bots.BuiltIn;
using BotArena.Engine;

namespace BotArena.Cli;

public static class ReplayCommand
{
    public static int Run(string file, IReadOnlyList<string> args)
    {
        var options = CliSupport.ParseOptions(args);
        string outDir = options.GetValueOrDefault("out", Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".");
        string json = File.ReadAllText(file);
        var document = ReplaySerializer.FromJson(json); // Validates the format.
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
