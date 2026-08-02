using System.Buffers;
using System.Text;
using System.IO.Compression;
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
        string? themeId = null,
        bool withViewer = true)
    {
        ArgumentNullException.ThrowIfNull(json);
        Directory.CreateDirectory(outDir);
        string replayPath = Path.GetFullPath(Path.Combine(outDir, "replay.json"));
        WriteVerified(replayPath, json);
        string? viewerPath = withViewer
            ? WriteViewer(json, outDir, themeId)
            : null;
        return new WrittenReplay(replayPath, viewerPath);
    }

    /// <summary>
    /// Writes one canonical replay as <c>replay.json.gz</c> without ever
    /// materialising an uncompressed durable sibling. Evaluation campaigns use
    /// this path: the canonical document remains available for audit and hash
    /// verification, while galleries receive a separately derived broadcast
    /// slice.
    /// </summary>
    public static WrittenReplay WriteGzipJson(
        string json,
        string outDir)
    {
        ArgumentNullException.ThrowIfNull(json);
        Directory.CreateDirectory(outDir);
        string replayPath = Path.GetFullPath(
            Path.Combine(outDir, "replay.json.gz"));
        string temp = replayPath + ".tmp";
        try
        {
            using (FileStream output = File.Create(temp))
            using (var compressed = new GZipStream(
                       output,
                       CompressionLevel.SmallestSize))
            using (var writer = new StreamWriter(
                       compressed,
                       new UTF8Encoding(
                           encoderShouldEmitUTF8Identifier: false,
                           throwOnInvalidBytes: true)))
            {
                writer.Write(json);
            }

            if (!GzipContentEquals(temp, json))
            {
                throw new IOException(
                    "Compressed replay write verification failed: the "
                    + "decompressed bytes differ from the canonical input.");
            }
            File.Move(temp, replayPath, overwrite: true);
            return new WrittenReplay(replayPath, ViewerPath: null);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Preserve the original failure.
            }
            throw;
        }
    }

    /// <summary>
    /// Byte-oriented twin of <see cref="WriteGzipJson(string,string)"/> for
    /// replay-v3 documents that already exist as canonical UTF-8.
    /// </summary>
    public static WrittenReplay WriteGzipJson(
        ReadOnlyMemory<byte> utf8Json,
        string outDir)
    {
        Directory.CreateDirectory(outDir);
        string replayPath = Path.GetFullPath(
            Path.Combine(outDir, "replay.json.gz"));
        string temp = replayPath + ".tmp";
        try
        {
            using (FileStream output = File.Create(temp))
            using (var compressed = new GZipStream(
                       output,
                       CompressionLevel.Optimal))
            {
                compressed.Write(utf8Json.Span);
            }

            if (!GzipContentEquals(temp, utf8Json))
            {
                throw new IOException(
                    "Compressed replay write verification failed: the "
                    + "decompressed bytes differ from the canonical input.");
            }
            File.Move(temp, replayPath, overwrite: true);
            return new WrittenReplay(replayPath, ViewerPath: null);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Preserve the original failure.
            }
            throw;
        }
    }

    private static bool GzipContentEquals(string path, string expected)
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(64 * 1024);
        try
        {
            using FileStream input = File.OpenRead(path);
            using var compressed = new GZipStream(
                input,
                CompressionMode.Decompress);
            using var reader = new StreamReader(
                compressed,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 64 * 1024,
                leaveOpen: false);
            int offset = 0;
            while (true)
            {
                int read = reader.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    return offset == expected.Length;
                if (read > expected.Length - offset
                    || !buffer.AsSpan(0, read).SequenceEqual(
                        expected.AsSpan(offset, read)))
                {
                    return false;
                }
                offset += read;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private static bool GzipContentEquals(
        string path,
        ReadOnlyMemory<byte> expected)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using FileStream input = File.OpenRead(path);
            using var compressed = new GZipStream(
                input,
                CompressionMode.Decompress);
            int offset = 0;
            while (true)
            {
                int read = compressed.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    return offset == expected.Length;
                if (read > expected.Length - offset
                    || !buffer.AsSpan(0, read).SequenceEqual(
                        expected.Span.Slice(offset, read)))
                {
                    return false;
                }
                offset += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Writes through a temp file, verifies the byte length on disk, and
    /// moves into place — so a full volume produces a loud failure instead
    /// of a truncated replay that parses, reports plausible standings, and
    /// lies (a wave-5 author lost a sweep to exactly that).
    /// </summary>
    private static void WriteVerified(string path, string content)
    {
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, content);
            long expected = new UTF8Encoding(false).GetByteCount(content);
            long actual = new FileInfo(temp).Length;
            if (actual != expected)
            {
                throw new IOException(
                    $"Replay write verification failed: {actual} bytes on "
                    + $"disk, {expected} expected — is the volume full?");
            }
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // The loud failure below matters more than temp hygiene.
            }
            throw;
        }
    }

    public static string? WriteViewer(string replayJson, string outDir, string? themeId = null)
    {
        string? template = FindTemplate(themeId);
        if (template is null || !File.Exists(template))
            return null;
        Directory.CreateDirectory(outDir);
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
