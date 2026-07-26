using System.Reflection;
using System.Text.Json;

namespace BotArena.App.Cosmetics;

public sealed record CosmeticUnlock(
    string SourceKind,
    string SourceId,
    string Hint);

public sealed record CosmeticCatalogItem(
    string Key,
    string Kind,
    string Id,
    string Label,
    string Availability,
    CosmeticUnlock? Unlock = null);

/// <summary>
/// A bundle sold as one thing.
/// <para>
/// A chassis and the shot that belongs with it are one look, not two purchases — buying
/// the hull and then discovering its projectile costs extra is the shape of a store nobody
/// trusts. The pack is also what the payment provider prices, so it is the unit of
/// everything: one catalogue entry, one price, one entitlement source.
/// </para>
/// </summary>
public sealed record CosmeticPack(
    string Id,
    string Label,
    string Description,
    /// <summary>Catalog keys, in display order — the chassis first.</summary>
    IReadOnlyList<string> Items);

public sealed record CosmeticCatalogDocument(
    int Version,
    IReadOnlyList<CosmeticCatalogItem> Items,
    IReadOnlyList<CosmeticPack>? Packs = null);

/// <summary>
/// The version-controlled authority for cosmetic identity and availability.
/// Rendering details stay in the web asset manifests; tests keep both sets aligned.
/// </summary>
public sealed class CosmeticCatalog
{
    public const string BotLookKind = "bot-look";
    public const string ProjectileLookKind = "projectile-look";
    public const string StarterAvailability = "starter";
    public const string EntitlementAvailability = "entitlement";

    /// <summary>
    /// The unlock source kind for anything bought rather than earned.
    /// <para>
    /// A pack's id is the source id, so granting a purchase is
    /// <c>GrantForEventAsync(user, Purchase, packId)</c> and the existing dedupe makes a
    /// replayed webhook silent — the same property a retried job already relies on.
    /// </para>
    /// </summary>
    public const string PurchaseSource = "purchase";

    private const string ResourceName = "BotArena.Cosmetics.catalog.json";
    private readonly IReadOnlyDictionary<string, CosmeticCatalogItem> byKey;

    private CosmeticCatalog(CosmeticCatalogDocument document)
    {
        if (document.Version < 1)
            throw new InvalidOperationException("Cosmetic catalog version must be positive.");
        if (document.Items.Count == 0)
            throw new InvalidOperationException("Cosmetic catalog must contain at least one item.");

        var items = new Dictionary<string, CosmeticCatalogItem>(StringComparer.Ordinal);
        foreach (CosmeticCatalogItem item in document.Items)
        {
            Validate(item);
            if (!items.TryAdd(item.Key, item))
                throw new InvalidOperationException($"Duplicate cosmetic key '{item.Key}'.");
        }
        Version = document.Version;
        Items = document.Items.ToArray();
        byKey = items;
        Packs = (document.Packs ?? []).ToArray();
        ValidatePacks();
    }

    public int Version { get; }
    public IReadOnlyList<CosmeticCatalogItem> Items { get; }
    public IReadOnlyList<CosmeticPack> Packs { get; }

    public CosmeticPack? FindPack(string id) =>
        Packs.FirstOrDefault(pack => pack.Id == id);

    public static CosmeticCatalog LoadDefault()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded cosmetic catalog '{ResourceName}' was not found.");
        var document = JsonSerializer.Deserialize<CosmeticCatalogDocument>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Cosmetic catalog is empty.");
        return new CosmeticCatalog(document);
    }

    public CosmeticCatalogItem? Find(string kind, string id) =>
        byKey.GetValueOrDefault($"{kind}:{id}");

    public IReadOnlyList<CosmeticCatalogItem> EntitlementsFor(
        string sourceKind,
        string sourceId) =>
        Items.Where(item =>
                item.Availability == EntitlementAvailability &&
                item.Unlock?.SourceKind == sourceKind &&
                item.Unlock.SourceId == sourceId)
            .ToArray();

    /// <summary>
    /// Packs and purchasable items must describe each other exactly.
    /// <para>
    /// Both directions are checked, because each failure is silent on its own: an item
    /// gated on <c>purchase</c> with no pack containing it is unobtainable by anyone and
    /// looks merely locked, and a pack listing an item that is not gated on that pack sells
    /// something the buyer may already have — or worse, grants nothing on payment.
    /// </para>
    /// </summary>
    private void ValidatePacks()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (CosmeticPack pack in Packs)
        {
            if (!IsPresentationId(pack.Id))
                throw new InvalidOperationException($"Pack id '{pack.Id}' must be kebab-case.");
            if (!seen.Add(pack.Id))
                throw new InvalidOperationException($"Duplicate pack '{pack.Id}'.");
            if (string.IsNullOrWhiteSpace(pack.Label) || string.IsNullOrWhiteSpace(pack.Description))
                throw new InvalidOperationException($"Pack '{pack.Id}' needs a label and description.");
            if (pack.Items.Count == 0)
                throw new InvalidOperationException($"Pack '{pack.Id}' contains nothing.");

            foreach (string key in pack.Items)
            {
                CosmeticCatalogItem item = byKey.GetValueOrDefault(key)
                    ?? throw new InvalidOperationException(
                        $"Pack '{pack.Id}' lists unknown cosmetic '{key}'.");
                if (item.Unlock?.SourceKind != PurchaseSource || item.Unlock.SourceId != pack.Id)
                    throw new InvalidOperationException(
                        $"Pack '{pack.Id}' lists '{key}', but that cosmetic unlocks from " +
                        $"'{item.Unlock?.SourceKind}/{item.Unlock?.SourceId}' — paying for the " +
                        "pack would not grant it.");
            }
        }

        foreach (CosmeticCatalogItem item in Items)
        {
            if (item.Unlock?.SourceKind != PurchaseSource)
                continue;
            if (!Packs.Any(pack => pack.Items.Contains(item.Key)))
                throw new InvalidOperationException(
                    $"Cosmetic '{item.Key}' unlocks by purchase but no pack sells it, so " +
                    "nobody can ever obtain it.");
        }
    }

    private static void Validate(CosmeticCatalogItem item)
    {
        if (item.Kind is not BotLookKind and not ProjectileLookKind)
            throw new InvalidOperationException(
                $"Cosmetic '{item.Key}' has unsupported kind '{item.Kind}'.");
        if (!IsPresentationId(item.Id) || item.Key != $"{item.Kind}:{item.Id}")
            throw new InvalidOperationException(
                $"Cosmetic key '{item.Key}' must match its kind and kebab-case ID.");
        if (string.IsNullOrWhiteSpace(item.Label))
            throw new InvalidOperationException($"Cosmetic '{item.Key}' needs a label.");
        if (item.Availability is not StarterAvailability and not EntitlementAvailability)
            throw new InvalidOperationException(
                $"Cosmetic '{item.Key}' has unsupported availability '{item.Availability}'.");
        if (item.Availability == EntitlementAvailability &&
            (item.Unlock is null ||
             !IsPresentationId(item.Unlock.SourceKind) ||
             !IsPresentationId(item.Unlock.SourceId) ||
             string.IsNullOrWhiteSpace(item.Unlock.Hint)))
        {
            throw new InvalidOperationException(
                $"Entitlement cosmetic '{item.Key}' needs a valid unlock source and hint.");
        }
        if (item.Availability == StarterAvailability && item.Unlock is not null)
            throw new InvalidOperationException(
                $"Starter cosmetic '{item.Key}' cannot declare an unlock.");
    }

    private static bool IsPresentationId(string value) =>
        value.Length is > 0 and <= 80 &&
        value[0] is >= 'a' and <= 'z' &&
        value[^1] != '-' &&
        value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
