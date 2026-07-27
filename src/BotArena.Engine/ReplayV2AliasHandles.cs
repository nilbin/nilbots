using System.Globalization;

namespace BotArena.Engine;

/// <summary>Canonical grammar for audience-local replay-v2 aliases.</summary>
internal static class ReplayV2AliasHandles
{
    public const string EnemyLifePrefix = "enemy-life";
    public const string ProjectilePrefix = "projectile";
    public const string EventPrefix = "event";

    public static int ParseOrdinal(string handle, string prefix)
    {
        if (string.IsNullOrWhiteSpace(handle)
            || !handle.StartsWith(prefix + "-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Alias handle must use the canonical '{prefix}-N' form.",
                nameof(handle));
        }

        string suffix = handle[(prefix.Length + 1)..];
        if (!int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int ordinal)
            || ordinal < 0
            || !string.Equals(
                ordinal.ToString(CultureInfo.InvariantCulture),
                suffix,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Alias handle must use the canonical '{prefix}-N' form.",
                nameof(handle));
        }
        return ordinal;
    }
}
