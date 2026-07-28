namespace BotArena.App.Competition;

/// <summary>
/// Opaque identity for one publishing and competition window. Human-readable
/// season keys are metadata and are not used as foreign-key identity.
/// </summary>
public readonly record struct SeasonId
{
    private SeasonId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static SeasonId New() => new(Guid.NewGuid());

    public static SeasonId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException(
                "A season id cannot be empty.",
                nameof(value))
            : new SeasonId(value);

    public static SeasonId Parse(string value) =>
        TryParse(value, out SeasonId seasonId)
            ? seasonId
            : throw new FormatException(
                "A season id must be a non-empty GUID.");

    public static bool TryParse(string? value, out SeasonId seasonId)
    {
        bool valid =
            Guid.TryParse(value?.Trim(), out Guid parsed) &&
            parsed != Guid.Empty;
        seasonId = valid ? new SeasonId(parsed) : default;
        return valid;
    }

    public override string ToString() => Value.ToString("D");
}
