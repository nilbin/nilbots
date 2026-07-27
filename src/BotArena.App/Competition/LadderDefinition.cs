namespace BotArena.App.Competition;

/// <summary>
/// Immutable snapshot of one isolated rating population. Rating-policy
/// identity belongs here rather than to the gameplay playlist.
/// </summary>
public sealed class LadderDefinition
{
    public LadderDefinition(
        LadderId id,
        PlaylistVersionId playlistVersionId,
        SeasonId seasonId,
        LadderStatus status,
        string ratingPolicyId)
    {
        if (id.IsEmpty)
            throw new ArgumentException(
                "A ladder requires a non-empty id.",
                nameof(id));
        if (playlistVersionId.IsEmpty)
            throw new ArgumentException(
                "A ladder requires a non-empty playlist-version id.",
                nameof(playlistVersionId));
        if (seasonId.IsEmpty)
            throw new ArgumentException(
                "A ladder requires a non-empty season id.",
                nameof(seasonId));
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        Id = id;
        PlaylistVersionId = playlistVersionId;
        SeasonId = seasonId;
        Status = status;
        RatingPolicyId = Required(ratingPolicyId, nameof(ratingPolicyId));
    }

    public LadderId Id { get; }
    public PlaylistVersionId PlaylistVersionId { get; }
    public SeasonId SeasonId { get; }
    public LadderStatus Status { get; }
    public string RatingPolicyId { get; }

    private static string Required(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? "";
        return normalized.Length > 0
            ? normalized
            : throw new ArgumentException(
                "A ladder definition field cannot be blank.",
                parameterName);
    }
}
