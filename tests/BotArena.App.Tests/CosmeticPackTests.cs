using System.Text.Json;
using BotArena.App.Cosmetics;

namespace BotArena.App.Tests;

/// <summary>
/// A pack and the cosmetics it sells have to describe each other, in both directions.
/// <para>
/// Each half fails silently on its own. An item gated on <c>purchase</c> that no pack sells
/// is unobtainable and merely looks locked; a pack listing an item gated on something else
/// takes money and grants nothing. Neither shows up until a real customer hits it.
/// </para>
/// </summary>
public class CosmeticPackTests
{
    private static CosmeticCatalog Load(string json) =>
        Build(JsonSerializer.Deserialize<CosmeticCatalogDocument>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);

    /// <summary>The constructor is private, so the loader is exercised through reflection.</summary>
    private static CosmeticCatalog Build(CosmeticCatalogDocument document)
    {
        try
        {
            return (CosmeticCatalog)Activator.CreateInstance(
                typeof(CosmeticCatalog),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: [document],
                culture: null)!;
        }
        catch (System.Reflection.TargetInvocationException wrapped)
            when (wrapped.InnerException is not null)
        {
            throw wrapped.InnerException;
        }
    }

    private const string PackedPair = """
        {
          "version": 1,
          "items": [
            { "key": "bot-look:kite", "kind": "bot-look", "id": "kite", "label": "Kite",
              "availability": "entitlement",
              "unlock": { "sourceKind": "purchase", "sourceId": "kite", "hint": "Store." } },
            { "key": "projectile-look:dart", "kind": "projectile-look", "id": "dart",
              "label": "Dart", "availability": "entitlement",
              "unlock": { "sourceKind": "purchase", "sourceId": "kite", "hint": "Store." } }
          ],
          "packs": [
            { "id": "kite", "label": "Kite", "description": "A pair.",
              "items": ["bot-look:kite", "projectile-look:dart"] }
          ]
        }
        """;

    [Fact]
    public void APackAndItsItemsAgree()
    {
        var catalog = Load(PackedPair);

        CosmeticPack pack = Assert.Single(catalog.Packs);
        Assert.Equal(2, pack.Items.Count);
        // The pack id is the entitlement source id, which is what makes paying for it grant
        // both halves through the same path an achievement uses.
        Assert.Equal(2, catalog.EntitlementsFor(CosmeticCatalog.PurchaseSource, "kite").Count);
    }

    [Fact]
    public void APurchasableCosmeticNoPackSellsIsRejected()
    {
        string orphaned = PackedPair.Replace("""
            "items": ["bot-look:kite", "projectile-look:dart"]
            """.Trim(), """
            "items": ["bot-look:kite"]
            """.Trim());

        var error = Assert.Throws<InvalidOperationException>(() => Load(orphaned));
        // Unobtainable, and indistinguishable from an ordinary locked cosmetic in the UI.
        Assert.Contains("no pack sells it", error.Message);
    }

    /// <summary>The dart is sold by the kite pack but unlocks from a different one.</summary>
    private const string MismatchedPack = """
        {
          "version": 1,
          "items": [
            { "key": "bot-look:kite", "kind": "bot-look", "id": "kite", "label": "Kite",
              "availability": "entitlement",
              "unlock": { "sourceKind": "purchase", "sourceId": "kite", "hint": "Store." } },
            { "key": "projectile-look:dart", "kind": "projectile-look", "id": "dart",
              "label": "Dart", "availability": "entitlement",
              "unlock": { "sourceKind": "achievement", "sourceId": "rating-1300", "hint": "Earn." } }
          ],
          "packs": [
            { "id": "kite", "label": "Kite", "description": "A pair.",
              "items": ["bot-look:kite", "projectile-look:dart"] }
          ]
        }
        """;

    [Fact]
    public void APackListingSomethingItDoesNotUnlockIsRejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Load(MismatchedPack));
        // The expensive failure: the customer pays and receives nothing.
        Assert.Contains("would not grant it", error.Message);
    }

    [Fact]
    public void EveryPackSitsOnAKnownShelf()
    {
        var catalog = CosmeticCatalog.LoadDefault();

        // A pack with no category would silently vanish from a store that renders by
        // category — present in the API, invisible on the page.
        Assert.All(catalog.Packs, pack => Assert.Contains(pack.Category, CosmeticCatalog.Categories));
        Assert.Contains(catalog.Packs, pack => pack.Category == CosmeticCatalog.CapacityCategory);
    }

    [Fact]
    public void EveryAppearancePackSellsACompletePair()
    {
        var catalog = CosmeticCatalog.LoadDefault();

        var appearance = catalog.Packs
            .Where(pack => pack.Category == CosmeticCatalog.AppearanceCategory)
            .ToArray();
        Assert.NotEmpty(appearance);
        foreach (CosmeticPack pack in appearance)
        {
            IReadOnlyList<CosmeticCatalogItem> granted =
                catalog.EntitlementsFor(CosmeticCatalog.PurchaseSource, pack.Id);

            // Every pack is a chassis and the shot that belongs with it. Selling a hull and
            // then charging again for its projectile is the shape of a store nobody trusts.
            Assert.Equal(pack.Items.Count, granted.Count);
            Assert.Contains(granted, item => item.Kind == CosmeticCatalog.BotLookKind);
            Assert.Contains(granted, item => item.Kind == CosmeticCatalog.ProjectileLookKind);
        }
    }

    [Fact]
    public void NothingEarnableBecamePurchasable()
    {
        var catalog = CosmeticCatalog.LoadDefault();

        // The line the store must not cross. Six cosmetics are earned by playing — 1300
        // rating, 100 ranked matches, a first successful build — and putting any of them
        // behind a payment devalues both the grind and the toast that celebrates it.
        string[] earned = ["mantis", "lancer", "aureate-warden", "talon", "arc-spark", "regent-lance"];
        foreach (string id in earned)
        {
            CosmeticCatalogItem item = catalog.Items.Single(candidate => candidate.Id == id);
            Assert.NotEqual(CosmeticCatalog.PurchaseSource, item.Unlock?.SourceKind);
        }
    }
}
