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

public sealed record CosmeticCatalogDocument(
    int Version,
    IReadOnlyList<CosmeticCatalogItem> Items);

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
    }

    public int Version { get; }
    public IReadOnlyList<CosmeticCatalogItem> Items { get; }

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
