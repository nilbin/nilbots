using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the engine's derived surfaces to the source of truth so they cannot rot
/// silently. Every drift this suite guards against has actually happened once:
/// web/types.ts missed energy/zoneTicks/Domination/strafes for two experiment
/// cycles; the site docs described a stale ranked pool and rules version; SDK
/// comments claimed strafe shipped in 0.3. Mechanical agreement is testable —
/// these read the repo files at test time; prose accuracy stays a review duty
/// (see the rules-change checklist in CLAUDE.md).
/// </summary>
public class DocDriftTests
{
    private static readonly string Root = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BotArena.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("BotArena.sln not found above the test directory.");
    }

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root, .. parts]));

    [Fact]
    public void TypeScriptReplayMirror_ContainsEveryEnumValue()
    {
        string ts = ReadRepoFile("web", "src", "types.ts");
        Type[] enums = [typeof(BotAction), typeof(ActionResult), typeof(BotStatus),
                        typeof(MatchEndReason), typeof(GameEventType)];
        foreach (var type in enums)
            foreach (string name in Enum.GetNames(type))
                Assert.True(ts.Contains($"'{name}'", StringComparison.Ordinal),
                    $"web/src/types.ts is missing {type.Name} value '{name}' — keep the replay mirror in sync with Replay.cs.");
    }

    [Fact]
    public void TypeScriptReplayMirror_ContainsEveryReplayProperty()
    {
        string ts = ReadRepoFile("web", "src", "types.ts");
        Type[] records = [typeof(ReplayHeader), typeof(ReplayParticipant), typeof(ReplayBotTick),
                          typeof(ReplayBotState), typeof(ReplayVisibleEnemy), typeof(GameEvent),
                          typeof(ReplayTick), typeof(ReplayProjectile), typeof(ReplayProjectileTraversal),
                          typeof(ReplayHeardSound),
                          typeof(MatchResultInfo), typeof(BotMatchResult)];
        foreach (var type in records)
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string camel = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
                Assert.True(
                    System.Text.RegularExpressions.Regex.IsMatch(ts, $@"\b{camel}\??:"),
                    $"web/src/types.ts is missing {type.Name}.{property.Name} (expected '{camel}:' or '{camel}?:').");
            }
    }

    [Fact]
    public void PlayerDocs_DescribeTheCurrentRulesVersion()
    {
        // Both player-facing docs are now assembled rather than hand-written: the site
        // renders the canonical card and stamps the version from a constant, and the
        // template README splices the card in at `nilbots new` time. So assert the
        // version reaches each surface, not that a literal "v0.5" is typed there.
        Assert.Contains($"RULES_VERSION = '{BotArenaVersions.GameRulesVersion}'",
            ReadRepoFile("web", "src", "site", "pages", "DocsPage.tsx"));
        Assert.Contains(BotArenaVersions.GameRulesVersion,
            ReadRepoFile("docs", "PLAYER-GUIDE.md"));
    }

    /// <summary>The app serves web/dist/index.html directly, so a stale bundle silently
    /// shows players the PREVIOUS ruleset's docs while the source reads correctly — a
    /// friend's-agent test was taught "150 zone-ticks wins" (a pre-0.5 mechanic) from a
    /// two-day-old bundle. dist is gitignored, so only assert when it exists.</summary>
    [Fact]
    public void BuiltWebBundle_IfPresent_IsNotStaleAgainstTheDocsSource()
    {
        string dist = Path.Combine(Root, "web", "dist", "index.html");
        string source = Path.Combine(Root, "web", "src", "site", "pages", "DocsPage.tsx");
        if (!File.Exists(dist))
            return; // nothing built here (fresh clone / CI before the web build)
        Assert.True(File.GetLastWriteTimeUtc(dist) >= File.GetLastWriteTimeUtc(source),
            "web/dist is older than the docs source, so the site would serve stale rules " +
            "to players. Rebuild it: (cd web && npm run build).");
    }

    /// <summary>The canonical player rules card is now the ONE source of rules prose:
    /// the server serves it at /llms-full.txt and `nilbots new` splices it into every
    /// scaffolded README. So its numbers must match the engine — a stale number here
    /// now reaches every player at once, which is exactly how "150 zone-ticks wins"
    /// outlived the mechanic it described.</summary>
    [Fact]
    public void CanonicalRulesCard_AgreesWithTheEngineNumbers()
    {
        string card = ReadRepoFile("docs", "PLAYER-GUIDE.md");
        var rules = GameRules.Current;
        (string Label, string Value)[] mustAppear =
        [
            ("vision range", rules.VisionRange.ToString()),
            ("shoot cooldown", rules.ShootCooldownTicks.ToString()),
            ("max health", rules.MaxHealth.ToString()),
            ("max ticks", rules.MaxTicks.ToString()),
        ];
        foreach (var (label, value) in mustAppear)
            Assert.True(card.Contains(value, StringComparison.Ordinal),
                $"The player rules card never mentions the engine's {label} ({value}). " +
                "Either the card is stale or the value moved — players read this verbatim.");

        // The retired pre-0.5 win condition must not survive anywhere in it.
        Assert.False(card.Contains("zone-ticks wins", StringComparison.OrdinalIgnoreCase),
            "The rules card still describes the pre-0.5 zone-tick win condition.");
    }

    [Fact]
    public void CliHelp_ListsEveryResolvableRulesName()
    {
        string help = ReadRepoFile("src", "BotArena.Cli", "Program.cs");
        foreach (string name in GameRules.KnownNames)
            Assert.True(help.Contains(name, StringComparison.Ordinal),
                $"CLI help in Program.cs does not mention rules name '{name}' (GameRules.KnownNames).");
    }

    [Fact]
    public void CliDefault_FollowsTheCurrentRulesVersion()
    {
        string support = ReadRepoFile("src", "BotArena.Cli", "CliSupport.cs");
        Assert.Contains("return BotArenaVersions.GameRulesVersion;", support);
    }

    [Fact]
    public void EveryKnownRulesName_Resolves()
    {
        foreach (string name in GameRules.KnownNames)
            _ = GameRules.Resolve(name); // must not throw; KnownNames and Resolve move together
    }

    [Fact]
    public void SdkProjectVersion_MatchesToolchainVersion()
    {
        string toolchain = ReadRepoFile("src", "BotArena.Toolchain", "BotProject.cs");
        var versionMatch = Regex.Match(
            toolchain,
            """public const string SdkVersion = "([^"]+)";""");
        Assert.True(versionMatch.Success, "Could not find ToolchainInfo.SdkVersion.");

        var project = XDocument.Parse(
            ReadRepoFile("src", "BotArena.Sdk", "BotArena.Sdk.csproj"));
        string? projectVersion = project.Descendants("Version").SingleOrDefault()?.Value;
        Assert.Equal(versionMatch.Groups[1].Value, projectVersion);
    }
}
