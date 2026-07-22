using System.Text.Json;
using System.Text.Json.Serialization;
using BotArena.Engine;

namespace BotArena.Toolchain;

public sealed record BotManifest
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("entryType")] public required string EntryType { get; init; }
    [JsonPropertyName("sdkVersion")] public string SdkVersion { get; init; } = "0.1";
    [JsonPropertyName("appearance")] public BotAppearance? Appearance { get; init; }
}

public sealed record BotAppearance
{
    [JsonPropertyName("accent")] public string? Accent { get; init; }
}

/// <summary>Pinned toolchain identity. Every value participates in the build-cache key —
/// bump any of them and every bot rebuilds (plan §12).</summary>
public static class ToolchainInfo
{
    public const string CliVersion = "0.3.0";
    // 0.2.0: BotContext.Slot + documented event semantics (DECISIONS #46).
    // 0.3.0: BotContext.Energy for the energy-shot rules candidate (DECISIONS #47) —
    // carried as an optional trailing observation field, so the wire protocol stays 0.1
    // and older artifacts (champions) keep running under any rules.
    public const string SdkVersion = "0.3.0";
    public const string IlcLlvmVersion = "10.0.0-rc.1.26306.1";
    public const string GuestAdapterVersion = "0.3.0";

    public static string CacheRoot =>
        Environment.GetEnvironmentVariable("BOTARENA_HOME") is { Length: > 0 } home
            ? Path.Combine(home, "cache")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".botarena", "cache");

    /// <summary>env → system install (/opt, readable by the isolated build user) → legacy
    /// home dir. A home-dir sdk breaks isolated builds (botbuild cannot traverse /root),
    /// so it is the last resort, not the default.</summary>
    public static string ResolveWasiSdkPath()
    {
        if (Environment.GetEnvironmentVariable("WASI_SDK_PATH") is { Length: > 0 } env)
            return env;
        const string system = "/opt/botarena/wasi-sdk-29.0";
        if (File.Exists(Path.Combine(system, "bin", "clang")))
            return system;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".wasi-sdk", "wasi-sdk-29.0");
    }
}

/// <summary>A player bot project on disk: botarena.json + .cs sources.</summary>
public sealed class BotProject
{
    public required string Directory { get; init; }
    public required BotManifest Manifest { get; init; }
    public required IReadOnlyList<string> SourceFiles { get; init; }

    public string Accent => Manifest.Appearance?.Accent ?? "#22d3ee";

    public static bool LooksLikeProject(string directory) =>
        File.Exists(Path.Combine(directory, "botarena.json"));

    public static BotProject Load(string directory)
    {
        directory = Path.GetFullPath(directory);
        string manifestPath = Path.Combine(directory, "botarena.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"No botarena.json in {directory} — not a bot project.");
        var manifest = JsonSerializer.Deserialize<BotManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidOperationException("Empty botarena.json.");
        var sources = System.IO.Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        if (sources.Length == 0)
            throw new InvalidOperationException($"No .cs sources found in {directory}.");
        return new BotProject { Directory = directory, Manifest = manifest, SourceFiles = sources };
    }

    /// <summary>Deterministic cache key over sources + manifest + every pinned toolchain
    /// version (plan §12). Any relevant change produces a different key.</summary>
    public string ComputeCacheKey() =>
        BotBuilder.ComputeCacheKey(
            SourceFiles.Select(f => new SourceFile(
                Path.GetRelativePath(Directory, f), File.ReadAllText(f))).ToArray(),
            Manifest.EntryType);
}
