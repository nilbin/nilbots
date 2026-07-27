namespace BotArena.Engine.Tests;

public class CanonicalFingerprintGoldenTests
{
    [Theory]
    [InlineData(
        "0.1",
        "9b30d8f2da91d919a726a09e847591a2d92489a4b82c7d06f1925223f55386a3",
        "be49a666ec3f54cf78ad21f00b4490f098af5e194f34cd21734e2395a7cb90c6")]
    [InlineData(
        "0.2",
        "90370a9784ea59d6ab24d47142fbde76628162e7f5cd9593ad3fb0e82c1d9c2a",
        "309acba54f8dcdceb349638de644240d5d6fcecc2c6a4a2e5b10f79dc589fbf6")]
    [InlineData(
        "0.3",
        "4562aa71a96f86ebe8c17c643c968eccfae168fb582d8c4b59de7971194a561a",
        "43c471e414bee83534298787a2d47e394541b2b171ab6136c90dd6fcc53ddd19")]
    [InlineData(
        "0.4",
        "58ad0b24cec382506966c7bb94c775a7cea35f87415ff42c69728c34c53201ac",
        "12e8d8b0428e903274060e7f7e74b87b210b894539de459cc6786c46d64a9913")]
    [InlineData(
        "0.5",
        "d83258ec401c5033f22891489cee2ccaf9ad044e0bac8ac336ccd727f17e9a1e",
        "9335f7471f8bf1676866d20c231ca3b0020e86f490189bce549fbdb542babe68")]
    public void OfficialRulesAndFormatV1Map_PinEveryRulesAndAggregateFingerprint(
        string rulesName,
        string expectedRulesFingerprint,
        string expectedMatchFingerprint)
    {
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(FindRepoRoot(), "maps", "basic-01.json")));

        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(
                GameRules.Resolve(rulesName),
                map);

        Assert.Equal(expectedRulesFingerprint, contract.Rules.RulesFingerprint);
        Assert.Equal(
            "5a9f405755716ade6466243d9fa3a5c2a5cce9ccf42353cc7a6136ee8cd8ef0d",
            contract.Map.MapFingerprint);
        Assert.Equal(expectedMatchFingerprint, contract.MatchContractFingerprint);
    }

    [Fact]
    public void CurrentRulesAndBasicMap_PinCanonicalRulesMapAndMatchFingerprints()
    {
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(FindRepoRoot(), "maps", "basic-01.json")));
        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);

        Assert.Equal(
            "d83258ec401c5033f22891489cee2ccaf9ad044e0bac8ac336ccd727f17e9a1e",
            contract.Rules.RulesFingerprint);
        Assert.Equal(
            "5a9f405755716ade6466243d9fa3a5c2a5cce9ccf42353cc7a6136ee8cd8ef0d",
            contract.Map.MapFingerprint);
        Assert.Equal(
            "9335f7471f8bf1676866d20c231ca3b0020e86f490189bce549fbdb542babe68",
            contract.MatchContractFingerprint);
    }

    [Fact]
    public void FrontlinePackage2Defaults_PinCanonicalRulesMapAndMatchFingerprints()
    {
        GameRules rules = GameRules.V0_1 with
        {
            RulesVersion = "frontline-package2-default",
            SeedProfile = "frontline-package2-default",
            Frontline = new FrontlineRules(),
        };
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(
                FindRepoRoot(),
                "maps",
                "experimental",
                "frontline-01.json")));

        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(rules, map);

        Assert.Equal(
            "1a6e115f3693aa76d9977762a838c65301f86acb820e3227c17a20c6d9010702",
            contract.Rules.RulesFingerprint);
        Assert.Equal(
            "b0d3d42946fa80306694597eea3faefce5e64ce5da7324ab2c95e2d8b3db52cd",
            contract.Map.MapFingerprint);
        Assert.Equal(
            "0b4c7fe326d9985aeb6906476e8249c699cbd1d00f78b1540adcb890b5349708",
            contract.MatchContractFingerprint);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("BotArena.sln not found above test output.");
    }
}
