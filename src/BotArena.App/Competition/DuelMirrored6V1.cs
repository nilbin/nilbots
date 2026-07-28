namespace BotArena.App.Competition;

/// <summary>
/// The shipped Duel ranked-series policy: three distinct map/seed pairs,
/// each played once in the original slot order and once with mirrored slots.
/// </summary>
public sealed class DuelMirrored6V1
{
    public const string Id = "duel-mirrored-6-v1";
    public const int MapPairCount = 3;
    public const int GamesPerMapPair = 2;
    public const int GameCount = MapPairCount * GamesPerMapPair;
    public const bool UsesMirroredSlots = true;
    public const double WinSeriesPoints = 1;
    public const double DrawSeriesPoints = 0.5;

    public static DuelMirrored6V1 Instance { get; } = new();

    private DuelMirrored6V1()
    {
    }

    /// <summary>
    /// Samples three maps from <paramref name="mapPool"/> and materializes the
    /// six games in their execution order. One seed is generated per map and
    /// reused for its mirrored game.
    /// </summary>
    public IReadOnlyList<ScheduledGame> CreateSchedule(
        IReadOnlyList<string> mapPool,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(mapPool);
        ArgumentNullException.ThrowIfNull(random);

        string[] maps = [.. mapPool];
        if (maps.Length < MapPairCount)
        {
            throw new ArgumentException(
                $"Duel ranked play requires at least {MapPairCount} maps.",
                nameof(mapPool));
        }
        if (maps.Any(string.IsNullOrWhiteSpace) ||
            maps.Distinct(StringComparer.Ordinal).Count() != maps.Length)
        {
            throw new ArgumentException(
                "The Duel ranked map pool requires distinct, non-blank map ids.",
                nameof(mapPool));
        }

        string[] selectedMaps =
        [
            .. maps
                .OrderBy(_ => random.Next())
                .Take(MapPairCount),
        ];
        var schedule = new ScheduledGame[GameCount];
        int game = 0;
        foreach (string mapId in selectedMaps)
        {
            long seed = random.NextInt64();
            schedule[game] = new ScheduledGame(
                GameNumber: game + 1,
                mapId,
                seed,
                Mirrored: false);
            game++;
            schedule[game] = new ScheduledGame(
                GameNumber: game + 1,
                mapId,
                seed,
                Mirrored: true);
            game++;
        }
        return schedule;
    }

    /// <summary>
    /// One scheduled game. <see cref="Mirrored"/> means Bot B occupies slot 0
    /// and Bot A occupies slot 1; false preserves A/B slot order.
    /// </summary>
    public sealed record ScheduledGame(
        int GameNumber,
        string MapId,
        long Seed,
        bool Mirrored);
}
