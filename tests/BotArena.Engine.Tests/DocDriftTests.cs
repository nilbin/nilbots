using System.Reflection;

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
                          typeof(ReplayTick), typeof(ReplayProjectile), typeof(ReplayHeardSound),
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
        string expected = "v" + BotArenaVersions.GameRulesVersion;
        Assert.Contains(expected, ReadRepoFile("web", "src", "site", "pages", "DocsPage.tsx"));
        Assert.Contains(expected, ReadRepoFile("templates", "botarena-bot", "README.md"));
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
    public void EveryKnownRulesName_Resolves()
    {
        foreach (string name in GameRules.KnownNames)
            _ = GameRules.Resolve(name); // must not throw; KnownNames and Resolve move together
    }
}
