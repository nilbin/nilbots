using BotArena.App.Matches;
using BotArena.Toolchain;

namespace BotArena.App.Bots;

/// <summary>
/// A single bot with its version history. <see cref="IsOwner"/> gates the owner-only
/// fields on each version (plan §13.3, §14) — a non-owner receives the same shape with
/// those fields null, so clients never branch on which endpoint they called.
/// </summary>
public sealed record BotDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    string Accent,
    string LookId,
    string ProjectileLookId,
    DateTime CreatedAt,
    string Owner,
    bool IsOwner,
    LadderStanding? CurrentStanding,
    IReadOnlyList<BotVersionResponse> Versions);

/// <summary>
/// One submitted version of a bot. <see cref="BuildLog"/>, <see cref="EntryType"/> and
/// <see cref="Sources"/> are populated only for the owner.
/// </summary>
public sealed record BotVersionResponse(
    Guid Id,
    int VersionNumber,
    string Status,
    string? ArtifactHash,
    bool IsActive,
    DateTime CreatedAt,
    BuildReceipt? BuildReceipt,
    string? BuildLog,
    string? EntryType,
    IReadOnlyList<SourceFile>? Sources);
