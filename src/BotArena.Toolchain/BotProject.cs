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
    /// <summary>Optional default for the CLI's --rules when this project is the --bot
    /// (gen-3 finding: repeating --rules on every command is easy to silently drop
    /// while practicing for a rules experiment). An explicit flag always wins.</summary>
    [JsonPropertyName("rules")] public string? Rules { get; init; }
}

public sealed record BotAppearance
{
    [JsonPropertyName("accent")] public string? Accent { get; init; }
    [JsonPropertyName("look")] public string? Look { get; init; }
    [JsonPropertyName("projectile")] public string? Projectile { get; init; }
}

/// <summary>Pinned toolchain identity. Every value participates in the build-cache key —
/// bump any of them and every bot rebuilds (plan §12).</summary>
public static class ToolchainInfo
{
    /// <summary>The published `Nilbots` tool version. MUST be bumped whenever
    /// SdkVersion or BuildPipelineVersion changes: those decide artifact bytes, and
    /// `submit` refuses against a server the installed tool cannot match, so a player
    /// needs a NEW tool version to upgrade to (DECISIONS #93). 0.5.x carries SDK 0.8.1
    /// and build pipeline 3; published 0.4.0 predates both. Keep in lockstep with
    /// BotArena.Cli.csproj's Version — PackagedCliVersionTests pins them together.</summary>
    public const string CliVersion = "0.5.3";
    // 0.2.0: BotContext.Slot + documented event semantics (DECISIONS #46).
    // 0.3.0: BotContext.Energy for the energy-shot rules candidate (DECISIONS #47).
    // 0.4.0: strafe actions + map dims + zone-control fields for the rules 0.3 slate
    // (RULES-0.3-DESIGN). All carried as trailing observation sections / additive
    // action values, so the wire protocol stays 0.1 and older artifacts keep running.
    // 0.5.0: VisibleProjectiles (trailing P section) for the 0.5 watchability slate
    // (RULES-0.5-DESIGN) — same additive pattern, wire protocol still 0.1.
    // 0.6.0: hardened 0.5 (§H / DECISIONS #59): HeardSounds (trailing H section) and
    // computable bolt timing — the P section grew 4→6 fields, which 0.5.0 adapters
    // cannot parse; wire protocol still 0.1 for every pre-bolt adapter.
    // 0.7.0: active shared control pressure (trailing C section) and ordered
    // multi-tile projectile advances (P section 6→7 fields; DECISIONS #62).
    // 0.8.0: private programmed-shot actions (trailing SP action payload),
    // public limits (trailing SP observation), and exact currently revealed
    // eight-way projectile headings (trailing PH observation).
    // 0.8.1: no wire change — XML documentation now ships beside the SDK dll, and the
    // members that are inert outside the research arms (strafe actions, Energy) are
    // marked [Obsolete] + [EditorBrowsable(Never)] so they no longer read as playable
    // API. Compile-surface change for player projects, hence a version bump.
    public const string SdkVersion = "0.8.1";
    public const string IlcLlvmVersion = "10.0.0-rc.1.26306.1";
    public const string GuestAdapterVersion = "0.8.0";
    // Compiler invocation/container changes that affect artifact bytes without changing
    // the SDK or guest contract. Included in every player-bot cache key.
    // 2: reproducible builds (DECISIONS #81) — the workspace path is mapped to a fixed
    //    virtual root and debug info is dropped, so the same sources produce the same
    //    bytes no matter which directory (or host) compiled them. Every pre-existing
    //    cache entry is invalid because artifact bytes change.
    // 3: the staged assembly closure stopped depending on WHICH host compiled
    //    (DECISIONS #84). Three things changed together: the workspace stages exactly
    //    BotArena.Sdk/Guest instead of every dll beside the invoking host (the CLI
    //    staged 9, the server 74); Sdk/Guest compile identically in any configuration
    //    from any directory; and their fallback build cache is keyed by source content
    //    rather than by GuestAdapterVersion. Artifact bytes change.
    public const string BuildPipelineVersion = "3";

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
    public string LookId
    {
        get
        {
            string value = Manifest.Appearance?.Look ?? "vanguard";
            if (!IsPresentationId(value))
                throw new InvalidOperationException(
                    $"Invalid appearance.look '{value}' in botarena.json; use a lowercase kebab-case ID.");
            return value;
        }
    }
    public string ProjectileLookId
    {
        get
        {
            string value = Manifest.Appearance?.Projectile ?? "pulse-bolt";
            if (!IsPresentationId(value))
                throw new InvalidOperationException(
                    $"Invalid appearance.projectile '{value}' in botarena.json; use a lowercase kebab-case ID.");
            return value;
        }
    }

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

    private static bool IsPresentationId(string value) =>
        value.Length is > 0 and <= 64 &&
        value[0] is >= 'a' and <= 'z' &&
        value[^1] != '-' &&
        value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
