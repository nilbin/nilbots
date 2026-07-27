using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Framework-owned entity bots used by actor-runtime contract tests, smoke
/// matches, and local evaluation plumbing. These deterministic reference
/// policies exercise mechanics; they are not independent balance evidence.
/// </summary>
public static class BuiltInActorBotCatalog
{
    public static readonly IReadOnlyList<string> Names =
    [
        "frontline-rusher",
        "frontline-swarm",
        "frontline-bastion",
        "frontline-counterpunch",
        "frontline-probe",
    ];

    public static IActorBot Create(string name) => name.ToLowerInvariant() switch
    {
        "frontline-probe" => new FrontlineProbeBot(),
        "frontline-rusher" => new FrontlineRusherBot(),
        "frontline-swarm" => new FrontlineSwarmBot(),
        "frontline-bastion" => new FrontlineBastionBot(),
        "frontline-counterpunch" => new FrontlineCounterpunchBot(),
        _ => throw new ArgumentException(
            $"Unknown built-in actor bot '{name}'. Available: " +
            $"{string.Join(", ", Names)}.",
            nameof(name)),
    };

    public static string Accent(string name) => name.ToLowerInvariant() switch
    {
        "frontline-rusher" => "#f97316",
        "frontline-swarm" => "#34d399",
        "frontline-bastion" => "#a78bfa",
        "frontline-counterpunch" => "#22d3ee",
        "frontline-probe" => "#94a3b8",
        _ => "#38bdf8",
    };

    public static string Look(string name) => name.ToLowerInvariant() switch
    {
        "frontline-rusher" => "vanguard",
        "frontline-swarm" => "needle",
        "frontline-bastion" => "bulwark",
        "frontline-counterpunch" => "orbiter",
        "frontline-probe" => "vanguard",
        _ => "vanguard",
    };

    public static string ProjectileLook(string name) =>
        name.ToLowerInvariant() switch
        {
            "frontline-rusher" => "pulse-bolt",
            "frontline-swarm" => "arc-spark",
            "frontline-bastion" => "razor-shard",
            "frontline-counterpunch" => "ion-orb",
            "frontline-probe" => "pulse-bolt",
            _ => "pulse-bolt",
        };

    public static string Describe(string name) => name.ToLowerInvariant() switch
    {
        "frontline-rusher" =>
            "presses the live objective immediately and never fabricates or Anchors",
        "frontline-swarm" =>
            "fabricates every available child, then keeps the whole team mobile on the objective",
        "frontline-bastion" =>
            "fabricates children, deploys them on legal defensive Anchor " +
            "tiles, and uses turret fire",
        "frontline-counterpunch" =>
            "builds one child, holds its own-side line, then closes when an enemy is seen",
        "frontline-probe" =>
            "mechanical contract probe that selects the first available typed action",
        _ => "",
    };
}
