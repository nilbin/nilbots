using System.Globalization;

namespace BotArena.Engine;

/// <summary>
/// Canonical omniscient IDs shared by replay projection and the private
/// observation-alias join. These values never enter an actor observation.
/// </summary>
internal static class ReplayV2Identifiers
{
    public static string WireInt64(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    public static string LifecycleEventId(int tick, int ordinal) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"lifecycle:{tick}:{ordinal}");

    public static string ResolutionEventId(int tick, int ordinal) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"resolution:{tick}:{ordinal}");
}
