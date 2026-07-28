using BotArena.App.Competition;
using BotArena.App.Matches;

namespace BotArena.App.Tests;

public sealed class DuelMirrored6V1Tests
{
    [Fact]
    public void PolicyIdentityPinsTheExistingRankedFormat()
    {
        Assert.Equal("duel-mirrored-6-v1", DuelMirrored6V1.Id);
        Assert.Equal(3, DuelMirrored6V1.MapPairCount);
        Assert.Equal(2, DuelMirrored6V1.GamesPerMapPair);
        Assert.Equal(6, DuelMirrored6V1.GameCount);
        Assert.True(DuelMirrored6V1.UsesMirroredSlots);
        Assert.Equal(1, DuelMirrored6V1.WinSeriesPoints);
        Assert.Equal(0.5, DuelMirrored6V1.DrawSeriesPoints);
        Assert.Equal(DuelMirrored6V1.GameCount, MatchSet.Games);
    }

    [Fact]
    public void ScheduleUsesThreePairedSeedsAndMirrorsEachMap()
    {
        IReadOnlyList<DuelMirrored6V1.ScheduledGame> schedule =
            DuelArenaDefinition.Official.CreateRankedSchedule(new Random(12345));

        Assert.Equal(DuelMirrored6V1.GameCount, schedule.Count);
        Assert.Equal(
            Enumerable.Range(1, DuelMirrored6V1.GameCount),
            schedule.Select(game => game.GameNumber));
        Assert.Equal(
            DuelMirrored6V1.MapPairCount,
            schedule.Select(game => game.MapId).Distinct().Count());
        Assert.All(
            schedule,
            game => Assert.Contains(
                game.MapId,
                DuelArenaDefinition.Official.RankedMapPool));

        foreach (DuelMirrored6V1.ScheduledGame[] pair in
                 schedule.Chunk(DuelMirrored6V1.GamesPerMapPair))
        {
            Assert.Equal(pair[0].MapId, pair[1].MapId);
            Assert.Equal(pair[0].Seed, pair[1].Seed);
            Assert.False(pair[0].Mirrored);
            Assert.True(pair[1].Mirrored);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidMapPools))]
    public void InvalidMapPoolsFailBeforeWorkIsQueued(string[] mapPool)
    {
        Assert.Throws<ArgumentException>(
            () => DuelMirrored6V1.Instance.CreateSchedule(
                mapPool,
                new Random(1)));
    }

    public static TheoryData<string[]> InvalidMapPools => new()
    {
        { ["basic-01", "arena-01"] },
        { ["basic-01", "arena-01", "arena-01"] },
        { ["basic-01", "arena-01", ""] },
    };
}
