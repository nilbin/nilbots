using BotArena.Engine;

namespace BotArena.Cli;

public sealed record WrittenReplay(string ReplayPath, string? ViewerPath);

/// <summary>
/// Writes replay.json and, when the built web viewer is available, a self-contained
/// viewer.html with the replay embedded (the same viewer the future website uses — plan §45.13).
/// </summary>
public static class ReplayOutput
{
    public const string InjectionMarker = "<!--BOTARENA_REPLAY-->";

    /// <summary>Stands in when a replay names no theme, or one this install does not ship.</summary>
    private const string FallbackTheme = "control-room";

    public static WrittenReplay Write(Replay replay, string outDir)
    {
        string json = ReplaySerializer.ToJson(replay);
        return WriteJson(json, outDir, replay.Header.ThemeId);
    }

    /// <summary>
    /// Writes an already-canonical replay document. Frontline uses this
    /// boundary so replay-v2's evolving DTO graph remains internal to Engine
    /// while the CLI can still preserve and display the exact hashed bytes.
    /// </summary>
    public static WrittenReplay WriteJson(
        string json,
        string outDir,
        string? themeId = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        Directory.CreateDirectory(outDir);
        string replayPath = Path.GetFullPath(Path.Combine(outDir, "replay.json"));
        File.WriteAllText(replayPath, json);
        string? viewerPath = WriteViewer(json, outDir, themeId);
        return new WrittenReplay(replayPath, viewerPath);
    }

    public static string? WriteViewer(string replayJson, string outDir, string? themeId = null)
    {
        string? template = FindTemplate(themeId);
        if (template is null || !File.Exists(template))
            return null;
        string html = File.ReadAllText(template);
        if (!html.Contains(InjectionMarker))
            return null;
        // </script> inside JSON strings would terminate the inline script early.
        string safeJson = replayJson.Replace("</", "<\\/");
        html = html.Replace(InjectionMarker,
            $"<script>window.__BOTARENA_REPLAY__ = {safeJson};</script>");
        string viewerPath = Path.GetFullPath(Path.Combine(outDir, "viewer.html"));
        File.WriteAllText(viewerPath, html);
        return viewerPath;
    }

    /// <summary>
    /// The viewer built for this replay's theme.
    /// </summary>
    /// <remarks>
    /// There is one artifact per map theme, because a viewer.html has to work from disk
    /// and therefore inlines its assets — and themes are effectively all of that weight
    /// (14 MB against 236 KB for every chassis, projectile look and audio cue combined).
    /// Shipping all four in every viewer cost 15 MB per file and grew with the content
    /// library; scoped, the same replay is 3.6–6.7 MB.
    /// <para>
    /// An unknown theme falls back rather than failing: a replay recorded against a theme
    /// this install does not ship should still be watchable — in the wrong colours — rather
    /// than not at all.
    /// </para>
    /// </remarks>
    internal static string? FindTemplate(string? themeId)
    {
        string? explicitPath = Environment.GetEnvironmentVariable("BOTARENA_VIEWER");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        foreach (string candidate in Candidates(themeId))
        {
            string? found = CliSupport.FindUpward(candidate);
            if (found is not null && File.Exists(found))
                return found;
        }
        return null;
    }

    private static IEnumerable<string> Candidates(string? themeId)
    {
        if (!string.IsNullOrWhiteSpace(themeId))
            yield return Path.Combine("web", "dist-cli", themeId, "index.html");
        yield return Path.Combine("web", "dist-cli", FallbackTheme, "index.html");
        // An unscoped build, which is what `vite build --config vite.cli.config.ts` emits
        // without BOTARENA_CLI_THEME. Convenient locally; never what the package ships.
        yield return Path.Combine("web", "dist-cli", "index.html");
    }
}
