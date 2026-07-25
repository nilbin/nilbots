namespace BotArena.App.Bots;

public readonly record struct AppearanceId
{
    private AppearanceId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? input, out AppearanceId appearanceId)
    {
        string value = input?.Trim().ToLowerInvariant() ?? "";
        bool valid =
            value.Length is > 0 and <= 64 &&
            value[0] is >= 'a' and <= 'z' &&
            value[^1] != '-' &&
            value.All(character =>
                character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
        appearanceId = valid ? new AppearanceId(value) : default;
        return valid;
    }

    public override string ToString() => Value;
}
