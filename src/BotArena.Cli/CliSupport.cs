using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

public static class CliSupport
{
    /// <summary>The rules *name* a command will play, before GameRules.Resolve. Precedence:
    /// explicit --rules flag → the first given bot project's botarena.json "rules" pin
    /// (announced, since nothing on the command line asked for it) → the current default.
    /// Names include 0.5 (default), historical official versions, and explicit
    /// experiment arms from <see cref="GameRules.KnownNames"/>.</summary>
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
        return BotArenaVersions.GameRulesVersion;
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

    /// <summary>
    /// Resolves the Labs contract profile a command runs on. The per-life
    /// generation is the default and stays the default: it is the shipped
    /// product, the pinned hosted playlist, and the generation every measured
    /// cohort was authored for.
    ///
    /// <para>Profile is a MATCH-level choice, not a per-entrant one, and that
    /// is the whole reason a mixed match works. One match resolves one
    /// contract, so both entrants answer the same observation shape; an
    /// artifact whose author only ever wrote a per-life bot answers a mind
    /// observation through the guest's wrap adapter, with no source edit and
    /// no flag of its own. Selecting per entrant would mean two contracts in
    /// one match, which is not a thing a fingerprinted match can be.</para>
    /// </summary>
    /// <returns>True when the mind profile was selected.</returns>
    public static bool ParseLabsProfile(string? value)
    {
        if (value is null)
            return false;
        return value.ToLowerInvariant() switch
        {
            "actor" or "generic-actor" or "generic-actor-match-2" or "per-life"
                => false,
            "mind" or "generic-mind" or "generic-mind-match-1" => true,
            _ => throw new InvalidOperationException(
                $"Unknown contract profile '{value}' (use actor or mind)."),
        };
    }

    public static void RejectUnknownOptions(
        IReadOnlyDictionary<string, string> options,
        params string[] allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] unknown = options.Keys
            .Where(key => !allowedSet.Contains(key))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException(
                $"Unknown option(s): {string.Join(", ", unknown.Select(key => $"--{key}"))}.");
    }
}
