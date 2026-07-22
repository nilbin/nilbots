namespace BotArena.Sdk;

/// <summary>
/// The entire bot programming model (plan §7): synchronous, one action per tick.
/// One instance is created per match; instance fields persist for that match only.
/// </summary>
public interface IBot
{
    BotAction Tick(BotContext context);
}

/// <summary>Deterministic randomness owned by Bot Arena (plan §6). Never use System.Random.</summary>
public interface IBotRandom
{
    int NextInt(int minimumInclusive, int maximumExclusive);
    bool NextBool();
    double NextDouble();
}

/// <summary>Bounded per-tick diagnostic output (plan §9). Never affects match outcome.</summary>
public interface IBotDebug
{
    void Write(string message);
    void Write(string format, params object?[] arguments);
}
