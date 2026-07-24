using BotArena.Sdk;

namespace BotArena.WasmGuest;

/// <summary>Throws on every tick — exercises the fault path end to end.</summary>
internal sealed class FaultyBot : IBot
{
    public BotAction Tick(BotContext context) =>
        throw new InvalidOperationException("FaultyBot always fails");
}

/// <summary>Burns fuel until the limit trips. The instance-field LCG gives every
/// iteration an observable side effect so the optimizer cannot elide the loop.</summary>
internal sealed class HogBot : IBot
{
    private ulong _state = 1;

    public BotAction Tick(BotContext context)
    {
        while (_state != 0)
            _state = _state * 6364136223846793005UL + 1442695040888963407UL;
        return Actions.Wait();
    }
}

/// <summary>
/// Uses APIs prohibited by plan §6.1 (wall clock, ambient randomness) on purpose:
/// the runtime must stay deterministic even when a bot cheats, because the host
/// shims WASI clock/entropy (defense in depth below the analyzers).
/// </summary>
internal sealed class ClockBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        long clockTicks = DateTime.UtcNow.Ticks;
        int ambient = Random.Shared.Next(0, 1_000_000);
        int tickCount = Environment.TickCount;
        context.Debug.Write("clock={0} rand={1} env={2}", clockTicks, ambient, tickCount);
        return (clockTicks + ambient + tickCount) % 2 == 0
            ? Actions.TurnRight()
            : Actions.MoveForward();
    }
}

/// <summary>Exercises the additive programmed-shot observation and action wire.</summary>
internal sealed class ArcProtocolBot : IBot
{
    public BotAction Tick(BotContext context) =>
        context.Tick == 0 && context.ShotPrograms is not null
            ? Actions.Shoot(new ShotProgram(0, 1, 2, 1, 2))
            : Actions.Wait();
}
