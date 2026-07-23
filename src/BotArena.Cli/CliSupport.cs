using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

public static class CliSupport
{
    /// <summary>The rules *name* a command will play, before GameRules.Resolve. Precedence:
    /// explicit --rules flag → the first given bot project's botarena.json "rules" pin
    /// (announced, since nothing on the command line asked for it) → the current default.
    /// Names: 0.4 (default) | 0.3 | 0.2 | 0.1 | strafe | hill | hill-shared | slate | energy.</summary>
    public static string ResolveRulesName(Dictionary<string, string> options, params string?[] botSpecs)
    {
        if (options.GetValueOrDefault("rules") is { } explicitName)
            return explicitName;
        foreach (var spec in botSpecs)
            if (spec is not null && Directory.Exists(spec)
                && BotArena.Toolchain.BotProject.LooksLikeProject(spec)
                && BotArena.Toolchain.BotProject.Load(spec).Manifest.Rules is { Length: > 0 } pinned)
            {
                Console.WriteLine($"Rules pinned by {Path.Combine(spec, "botarena.json")}: {pinned}");
                return pinned;
            }
        return "0.4";
    }

    public static GameRules ResolveRules(Dictionary<string, string> options, params string?[] botSpecs)
    {
        var rules = GameRules.Resolve(ResolveRulesName(options, botSpecs));
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
