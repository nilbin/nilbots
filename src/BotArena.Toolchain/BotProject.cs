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
    /// <summary>The bot's class, chosen at creation (DECISIONS #154): a
    /// Frontline Labs class ID such as "striker". A classed bot always plays
    /// its declared chassis — the experiment command resolves the class arm
    /// and team binding from both entrants' declarations. Null means the bot
    /// is class-agnostic (base contracts and smoke populations).</summary>
    [JsonPropertyName("class")] public string? Class { get; init; }
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
    /// needs a NEW tool version to upgrade to (DECISIONS #93). 0.9.x carries
    /// SDK/Guest 0.10.x, build pipeline 4, the frozen local Frontline
    /// actor/replay-v2 experiment, and the negotiated generic actor-match
    /// programming model. 0.9.1 adds the exact local Frontline Labs runner,
    /// generic-actor scaffold, and replay-v3 verification without changing
    /// SDK/Guest bytes. 0.9.4 carries SDK/Guest 0.10.3, whose generic
    /// Frontline contract can expose optional capture-gain schedules while
    /// retaining the exact static hosted-v1 bytes. It also carries the
    /// approved Obsidian Foundry effects
    /// in self-contained replay viewers and the generated HTTP contracts used
    /// by CLI server commands. 0.9.5 carries SDK/Guest 0.10.4 and the additive
    /// declared automatic-activation lifecycle contract used only by explicit
    /// experimental candidates. 0.9.6 adds the profile-scoped, WASM-only
    /// foundation and cumulative qualification runners without changing
    /// SDK/Guest bytes. 0.9.7 selects the platform-matched Docker NativeAOT
    /// compiler host and guards the emulated fallback against multi-node
    /// stalls, while preserving player artifact bytes and cache keys. 0.9.8
    /// adds the pendulum counterweight arms. 0.9.9 adds the class-skill kit
    /// (VOLLEY / AEGIS SHELL / FIVE SLOTS) — multi-projectile attacks, a
    /// form-level projectile guard with its own observed event, and asymmetric
    /// slot topology — all additive, so player artifact bytes and cache keys
    /// are unchanged. 0.9.10 converts AEGIS SHELL from absorption to
    /// DEFLECTION on owner ruling: the guard returns a team-flipped bolt along
    /// the reversed heading, the observed event becomes projectile-deflected
    /// and names both bolts, the stance's arc locks on entry, and the arm is
    /// identified `parry`. 0.9.11 makes the kit adoption-grade and follows
    /// the fight: one threshold-triggered automatic return serves both
    /// stances (VOLLEY after one fan, AEGIS SHELL after three deflections),
    /// so a form-transition event now carries its cause and a same-life
    /// route may declare an automatic-return trigger — both omitted while
    /// inert, so player artifact bytes and cache keys are unchanged; the
    /// two stance arms are reidentified `cast` and `break` because their
    /// behaviour changed, and `--bend universal` hands every class's mobile
    /// gun the one-bend grammar at its own depth. The same version carries
    /// the packaged viewer's camera (follows the active lives, with manual
    /// override and a fit toggle) and arrival materialization for
    /// fabrication, automatic return, and automatic activation — viewer
    /// changes ride the CLI compatibility surface. 0.9.12 makes the forward
    /// rally mirror-fair: the rally arms select a new lifecycle placement
    /// that orders the own-side objective region along the placing team's own
    /// advance axis instead of by absolute map order, so the two mirror-image
    /// rally regions hand the two sides reflected tiles. The historical
    /// absolute-order placement stays defined and resolvable for archived
    /// replays; rally-carrying arm fingerprints move, every other arm and the
    /// hosted contract stay byte-identical, and player artifact bytes and
    /// cache keys are unchanged. 0.9.13 registers `--pendulum keel`, the
    /// phase-1b level that composes every built counterweight
    /// (ratchet-contest plus enemy-sole-decay) under one short token, so
    /// those cells fit the 64-character canonical ID budget beside a class
    /// pair; it renames nothing, every existing arm and the hosted contract
    /// stay byte-identical, and player artifact bytes and cache keys are
    /// unchanged. Keep in lockstep with
    /// BotArena.Cli.csproj's
    /// Version — PackagedCliVersionTests pins them together.</summary>
    public const string CliVersion = "0.9.13";
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
    // 0.9.0: independent entity-life API, typed Frontline manifest and
    // observations/actions, plus actor protocol 1.0's shared tagged codec.
    // Legacy IBot and line protocol 0.1 remain byte-for-byte compatible.
    // 0.10.0: exact-profile generic actor-match contracts, variable topology
    // and entity collections, typed dynamic action/event/score/mode unions,
    // and generic replay-3-ready lineage. Legacy and Frontline-alpha surfaces
    // remain separate compatibility generations.
    // 0.10.1: generic form-transition events accept a due tick equal to their
    // started tick for end-of-started-tick completion. Pending transition
    // state remains strictly future-due.
    // 0.10.2: transition-created LifeSpawned observations accept intentionally
    // redacted parent/operation lineage while retaining the public transition ID.
    // 0.10.3: optional Frontline capture-gain schedules are parsed from the
    // canonical contract and resolvable against the authoritative tick.
    // 0.10.4: dormant slots may declare a first automatic activation tick and
    // life starts identify that parentless origin separately from deployment,
    // return, fabrication, and replication.
    public const string SdkVersion = "0.10.4";
    public const string IlcLlvmVersion = "10.0.0-rc.1.26306.1";
    public const string GuestAdapterVersion = "0.10.4";
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
    // 4: generated entry-point capability detection exposes every implemented
    //    bot interface without constructing a throwaway instance. This changes
    //    generated source and therefore every controlled artifact cache key.
    public const string BuildPipelineVersion = "4";

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
