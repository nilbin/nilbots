namespace BotArena.Engine.Tests;

public class CanonicalFingerprintGoldenTests
{
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
