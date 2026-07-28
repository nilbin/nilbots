namespace BotArena.Engine.Tests;

public class ExperimentalFrontlineRulesTests
{
    [Fact]
    public void Alpha1_ResolvesAgainstTheExperimentalMapOnly()
    {
        GameRules rules = ExperimentalFrontlineRules.Resolve(
            ExperimentalFrontlineRules.DefaultName);
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "maps",
            "experimental",
            "frontline-01.json")));

        ResolvedMatchDefinition definition =
            MatchDefinitionResolver.Resolve(rules, map);
        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(rules, map);

        Assert.True(definition.IsFrontline);
        Assert.Equal("frontline-alpha-1", rules.RulesVersion);
        Assert.Equal("frontline-alpha-1", rules.SeedProfile);
        Assert.Equal(500, rules.MaxTicks);
        Assert.NotNull(rules.Frontline);
        Assert.Equal(
            "96a91a1c91adbc6b61650aff9e49b4c4bc7b244a22aab625c25260b17047df19",
            contract.Rules.RulesFingerprint);
        Assert.Equal(
            "b0d3d42946fa80306694597eea3faefce5e64ce5da7324ab2c95e2d8b3db52cd",
            contract.Map.MapFingerprint);
        Assert.Equal(
            "27c6c3822b79758e053a3e293d28354f5df4c6852ccb7f2a0345a037a16bfd6f",
            contract.MatchContractFingerprint);
    }

    [Fact]
    public void Alpha1_DoesNotEnterHistoricalRulesCatalogs()
    {
        Assert.DoesNotContain(
            ExperimentalFrontlineRules.DefaultName,
            GameRules.KnownNames);
        Assert.DoesNotContain(
            ExperimentalFrontlineRules.DefaultName,
            GameRules.ShippedNames);
        Assert.Throws<ArgumentException>(
            () => GameRules.Resolve(
                ExperimentalFrontlineRules.DefaultName));
    }

    [Fact]
    public void Resolve_RejectsUnknownExperimentalArms()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => ExperimentalFrontlineRules.Resolve("frontline-future"));

        Assert.Contains(
            ExperimentalFrontlineRules.DefaultName,
            exception.Message);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
