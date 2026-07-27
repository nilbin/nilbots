namespace BotArena.Engine.Tests;

public class CanonicalFingerprintGoldenTests
{
    [Theory]
    [InlineData(
        "0.1",
        "b030b17da2e1278bb84b9c0f58df717a371b9ef0320783310f04fd6c7a08eb50",
        "78ddee2c9ba7c0701edd67e68c1a88c4ceaf4a3a848f1c34408a8d55107e66ab")]
    [InlineData(
        "0.2",
        "9f6ca0eda8cedece89ae5b69fefe1fbe01d6af25afed5aa2ad5e737547cce5f0",
        "77dd4c29a7127d7b8137f6b581132ebc09f50b553e17c4eea81fce444000e54f")]
    [InlineData(
        "0.3",
        "d722b80489a066ff237ae48a64103c07774908be78186184bcaab83440685de0",
        "6115ef3be5679d17662c6dc70832a88ca0d993ee5e50d7aa2fb75139f052b5f5")]
    [InlineData(
        "0.4",
        "1f4a65a29be91762fe61cc06ba4e836c25dd89da8b1e578431d398b02338eb27",
        "48a163b90678b7f2f7179ad4edea414eb82dbebad6fcf93d487fbd36278b7dd5")]
    [InlineData(
        "0.5",
        "255d841f1c5118e467cad88971c87d1774920a1aaff161b1be578aae29c5a72e",
        "c9330634f67975495fbeeeba93e0770677e02f9758d429739da8a1702975baa5")]
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
            "255d841f1c5118e467cad88971c87d1774920a1aaff161b1be578aae29c5a72e",
            contract.Rules.RulesFingerprint);
        Assert.Equal(
            "5a9f405755716ade6466243d9fa3a5c2a5cce9ccf42353cc7a6136ee8cd8ef0d",
            contract.Map.MapFingerprint);
        Assert.Equal(
            "c9330634f67975495fbeeeba93e0770677e02f9758d429739da8a1702975baa5",
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
            "358efbe4bc32de5b761d4fb999541d08c16991483d021d939dec1e6d100989f4",
            contract.Rules.RulesFingerprint);
        Assert.Equal(
            "b0d3d42946fa80306694597eea3faefce5e64ce5da7324ab2c95e2d8b3db52cd",
            contract.Map.MapFingerprint);
        Assert.Equal(
            "e4a0447d5dac26cf3f05aaa6e7388f9db5a64311b9fbab202bcc0d1e084e5765",
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
