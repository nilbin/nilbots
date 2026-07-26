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

    public static WrittenReplay Write(Replay replay, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string json = ReplaySerializer.ToJson(replay);
        string replayPath = Path.GetFullPath(Path.Combine(outDir, "replay.json"));
        File.WriteAllText(replayPath, json);
        string? viewerPath = WriteViewer(json, outDir);
        return new WrittenReplay(replayPath, viewerPath);
    }

    public static string? WriteViewer(string replayJson, string outDir)
    {
        string? template = Environment.GetEnvironmentVariable("BOTARENA_VIEWER")
            ?? CliSupport.FindUpward(Path.Combine("web", "dist-cli", "index.html"));
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
}
