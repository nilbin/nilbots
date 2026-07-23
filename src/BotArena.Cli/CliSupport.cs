using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

public static class CliSupport
{
    /// <summary>--rules 0.3 (default) | 0.2 | 0.1 | strafe | hill | slate | energy —
    /// see GameRules.Resolve.</summary>
    public static GameRules ResolveRules(Dictionary<string, string> options)
    {
        var rules = GameRules.Resolve(options.GetValueOrDefault("rules", "0.3"));
        if (options.TryGetValue("max-ticks", out string? maxTicks))
            rules = rules with { MaxTicks = int.Parse(maxTicks, CultureInfo.InvariantCulture) };
        return rules;
    }

    public static string? FindUpward(string relativePath) =>
        BotArena.Toolchain.RepoPaths.FindUpward(relativePath);

    public static ArenaMap LoadMap(string idOrPath)
    {
        string? path = File.Exists(idOrPath)
            ? idOrPath
            : FindUpward(Path.Combine("maps", idOrPath + ".json"));
        if (path is null)
            throw new InvalidOperationException(
                $"Map '{idOrPath}' not found (looked for maps/{idOrPath}.json in parent directories).");
        return ArenaMap.FromJson(File.ReadAllText(path));
    }

    public static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected argument '{args[i]}'.");
            string key = args[i][2..];
            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                options[key] = "true";
            else
                options[key] = args[++i];
        }
        return options;
    }
}
