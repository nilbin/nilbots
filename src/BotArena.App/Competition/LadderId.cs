namespace BotArena.App.Competition;

/// <summary>
/// Opaque identity for one isolated rating population.
/// <para>
/// A ladder is deliberately not identified by a rules-version string: several
/// formats, playlists, or seasons may use the same rules without sharing ratings.
/// </para>
/// </summary>
public readonly record struct LadderId
{
    private LadderId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static LadderId New() => new(Guid.NewGuid());

    public static LadderId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A ladder id cannot be empty.", nameof(value))
            : new LadderId(value);

    public static LadderId Parse(string value) =>
        TryParse(value, out LadderId ladderId)
            ? ladderId
            : throw new FormatException("A ladder id must be a non-empty GUID.");

    public static bool TryParse(string? value, out LadderId ladderId)
    {
        bool valid =
            Guid.TryParse(value?.Trim(), out Guid parsed) &&
            parsed != Guid.Empty;
        ladderId = valid ? new LadderId(parsed) : default;
        return valid;
    }

    public override string ToString() => Value.ToString("D");
}
