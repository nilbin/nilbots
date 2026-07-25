using System.Text.Json;
using BotArena.App.Cosmetics;
using BotArena.Toolchain;

namespace BotArena.App.Tests;

public class CosmeticCatalogTests
{
    [Fact]
    public void DefaultCatalog_MatchesEveryRuntimeAppearanceManifest()
    {
        CosmeticCatalog catalog = CosmeticCatalog.LoadDefault();
        string root = RepoPaths.ToolchainRoot();
        var manifestKeys = new HashSet<string>(StringComparer.Ordinal);

        ReadManifests(
            Path.Combine(root, "web", "src", "assets", "bot-looks"),
            CosmeticCatalog.BotLookKind,
            catalog,
            manifestKeys);
        ReadManifests(
            Path.Combine(root, "web", "src", "assets", "projectile-looks"),
            CosmeticCatalog.ProjectileLookKind,
            catalog,
            manifestKeys);

        Assert.Equal(
            catalog.Items.Select(item => item.Key).Order(),
            manifestKeys.Order());
    }

    [Fact]
    public void DefaultCatalog_PinsTheNonPaymentUnlocks()
    {
        CosmeticCatalog catalog = CosmeticCatalog.LoadDefault();

        CosmeticCatalogItem lancer =
            Assert.IsType<CosmeticCatalogItem>(
                catalog.Find(CosmeticCatalog.BotLookKind, "lancer"));
        Assert.Equal(CosmeticCatalog.EntitlementAvailability, lancer.Availability);
        Assert.Equal(CosmeticUnlockEvents.Achievement, lancer.Unlock!.SourceKind);
        Assert.Equal(CosmeticUnlockEvents.FirstSuccessfulBuild, lancer.Unlock.SourceId);

        CosmeticCatalogItem arcSpark =
            Assert.IsType<CosmeticCatalogItem>(
                catalog.Find(CosmeticCatalog.ProjectileLookKind, "arc-spark"));
        Assert.Equal(CosmeticCatalog.EntitlementAvailability, arcSpark.Availability);
        Assert.Equal(CosmeticUnlockEvents.Challenge, arcSpark.Unlock!.SourceKind);
        Assert.Equal(CosmeticUnlockEvents.FirstUnrankedMatch, arcSpark.Unlock.SourceId);

        CosmeticCatalogItem aureateWarden =
            Assert.IsType<CosmeticCatalogItem>(
                catalog.Find(CosmeticCatalog.BotLookKind, "aureate-warden"));
        CosmeticCatalogItem regentLance =
            Assert.IsType<CosmeticCatalogItem>(
                catalog.Find(CosmeticCatalog.ProjectileLookKind, "regent-lance"));
        Assert.Equal(
            CosmeticUnlockEvents.RankedMatches100,
            aureateWarden.Unlock!.SourceId);
        Assert.Equal(
            CosmeticUnlockEvents.RankedMatches100,
            regentLance.Unlock!.SourceId);
        Assert.Equal(
            aureateWarden.Unlock,
            regentLance.Unlock);

        Assert.Equal(
            10,
            catalog.Items.Count(item =>
                item.Availability == CosmeticCatalog.EntitlementAvailability));
    }

    private static void ReadManifests(
        string directory,
        string kind,
        CosmeticCatalog catalog,
        ISet<string> manifestKeys)
    {
        foreach (string path in Directory.EnumerateFiles(
                     directory,
                     "look.json",
                     SearchOption.AllDirectories))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            string id = document.RootElement.GetProperty("id").GetString()!;
            string label = document.RootElement.GetProperty("label").GetString()!;
            CosmeticCatalogItem item =
                Assert.IsType<CosmeticCatalogItem>(catalog.Find(kind, id));
            Assert.Equal(label, item.Label);
            Assert.True(manifestKeys.Add(item.Key), $"Duplicate manifest for {item.Key}.");
        }
    }
}
