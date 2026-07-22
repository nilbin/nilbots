using BotArena.Engine;

namespace BotArena.Cli;

public static class CliSupport
{
    public static string? FindUpward(string relativePath)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }
        return null;
    }

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
