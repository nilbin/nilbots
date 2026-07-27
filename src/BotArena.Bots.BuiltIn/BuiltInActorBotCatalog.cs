using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Framework-owned entity bots used by actor-runtime contract tests and local
/// diagnostics. Player-facing doctrine bots can be added after balance work.
/// </summary>
public static class BuiltInActorBotCatalog
{
    public static IActorBot Create(string name) => name switch
    {
        "frontline-probe" => new FrontlineProbeBot(),
        _ => throw new ArgumentException(
            $"Unknown built-in actor bot '{name}'.",
            nameof(name)),
    };
}
