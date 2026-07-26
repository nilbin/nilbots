using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class MatchEngineDefinitionTests
{
    [Fact]
    public void LegacyEngine_RejectsAFormat2MapInsteadOfRunningDuelSemantics()
    {
        MatchConfiguration configuration = TestMaps.Config(
            FrontlineMap(),
            new ScriptedRuntime(),
            new ScriptedRuntime(),
            rules: GameRules.V0_1);

        Assert.Throws<MatchDefinitionValidationException>(() =>
            new MatchEngine().Run(configuration));
    }

    [Fact]
    public void LegacyEngine_RejectsDefinitionOnlyFrontlineRules()
    {
        GameRules rules = GameRules.V0_1 with
        {
            ShotRange = 8,
            Frontline = new FrontlineRules(),
        };
        MatchConfiguration configuration = TestMaps.Config(
            FrontlineMap(),
            new ScriptedRuntime(),
            new ScriptedRuntime(),
            rules: rules);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            new MatchEngine().Run(configuration));

        Assert.Contains("definition-only", exception.Message);
    }

    private static ArenaMap FrontlineMap() =>
        ArenaMap.FromJson(File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "maps",
            "experimental",
            "frontline-01.json")));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
