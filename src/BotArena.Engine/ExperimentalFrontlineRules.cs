namespace BotArena.Engine;

/// <summary>
/// Named, local-only Frontline experiment arms. These names deliberately stay
/// outside <see cref="GameRules.Resolve"/>, <see cref="GameRules.KnownNames"/>,
/// and <see cref="GameRules.ShippedNames"/> so an experimental actor match
/// cannot enter the historical duel or ranked paths by accident.
/// </summary>
public static class ExperimentalFrontlineRules
{
    public const string DefaultName = "frontline-alpha-1";

    public static readonly IReadOnlyList<string> KnownNames =
        [DefaultName];

    public static GameRules Resolve(string name) => name switch
    {
        DefaultName => GameRules.V0_5 with
        {
            RulesVersion = DefaultName,
            SeedProfile = DefaultName,
            ZoneControl = false,
            ActiveZoneControl = false,
            SeedSpawnVariation = false,
            Frontline = new FrontlineRules(),
        },
        _ => throw new ArgumentException(
            $"Unknown Frontline experiment '{name}'. Available: " +
            $"{string.Join(", ", KnownNames)}.",
            nameof(name)),
    };
}
