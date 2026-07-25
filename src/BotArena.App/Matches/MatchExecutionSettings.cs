using BotArena.Engine;

namespace BotArena.App.Matches;

/// <summary>
/// Server-owned match execution and presentation settings shared by HTTP
/// admission and background execution. A ranked request must compare against
/// the same rules the worker will actually run.
/// </summary>
public sealed record MatchExecutionSettings(
    GameRules MatchRules,
    int BroadcastTicksPerSecond,
    int BroadcastDelaySeconds)
{
    public static MatchExecutionSettings FromEnvironment()
    {
        GameRules rules =
            Environment.GetEnvironmentVariable("BOTARENA_RULES") is { Length: > 0 } name
                ? GameRules.Resolve(name)
                : GameRules.Current;
        return new MatchExecutionSettings(
            rules,
            ReadInt("BOTARENA_BROADCAST_TPS", fallback: 5, min: 1, max: 1000),
            ReadInt("BOTARENA_BROADCAST_DELAY_SECONDS", fallback: 3, min: 0, max: 300));
    }

    private static int ReadInt(string name, int fallback, int min, int max) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value)
            ? Math.Clamp(value, min, max)
            : fallback;
}
