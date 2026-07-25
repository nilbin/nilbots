namespace BotArena.App.Cosmetics;

/// <summary>
/// The cosmetic catalogue as one caller sees it: every entry carries its own ownership
/// and, for unowned entries behind a milestone, that milestone's measured progress.
/// <see cref="Version"/> is the catalogue document version, not a game rules version.
/// </summary>
public sealed record CosmeticCatalogResponse(
    int Version,
    IReadOnlyList<CosmeticCatalogEntry> Items);
