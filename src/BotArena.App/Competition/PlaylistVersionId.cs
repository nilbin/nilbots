namespace BotArena.App.Competition;

/// <summary>
/// Opaque identity for one immutable playlist definition revision.
/// </summary>
public readonly record struct PlaylistVersionId
{
    private PlaylistVersionId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static PlaylistVersionId New() => new(Guid.NewGuid());

    public static PlaylistVersionId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException(
                "A playlist-version id cannot be empty.",
                nameof(value))
            : new PlaylistVersionId(value);

    public static PlaylistVersionId Parse(string value) =>
        TryParse(value, out PlaylistVersionId playlistVersionId)
            ? playlistVersionId
            : throw new FormatException(
                "A playlist-version id must be a non-empty GUID.");

    public static bool TryParse(
        string? value,
        out PlaylistVersionId playlistVersionId)
    {
        bool valid =
            Guid.TryParse(value?.Trim(), out Guid parsed) &&
            parsed != Guid.Empty;
        playlistVersionId = valid ? new PlaylistVersionId(parsed) : default;
        return valid;
    }

    public override string ToString() => Value.ToString("D");
}
