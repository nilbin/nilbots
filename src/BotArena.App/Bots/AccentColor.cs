using System.Globalization;

namespace BotArena.App.Bots;

public readonly record struct AccentColor
{
    public const string DefaultValue = "#22d3ee";

    private AccentColor(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static AccentColor Default => new(DefaultValue);

    public static bool TryCreate(string? input, out AccentColor accent)
    {
        string value = input?.Trim() ?? "";
        bool valid = value.Length == 7 && value[0] == '#' &&
            uint.TryParse(
                value.AsSpan(1),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out _);
        accent = valid ? new AccentColor(value.ToLowerInvariant()) : default;
        return valid;
    }

    public override string ToString() => Value;
}
