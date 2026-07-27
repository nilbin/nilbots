namespace BotArena.App.Competition;

/// <summary>
/// Opaque identities backing one legacy rules-version compatibility alias.
/// </summary>
public sealed record LegacyCompetitionIdentity(
    string RulesVersion,
    Guid PlaylistId,
    Guid PlaylistVersionId,
    Guid SeasonId,
    Guid LadderId);
