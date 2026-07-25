using BotArena.App.Matches;

namespace BotArena.App.Tests;

public class RankedMatchmakingTests
{
    private static Guid Id(int n) => new($"{n:D8}-0000-0000-0000-000000000000");

    private static MatchmakingCandidate Bot(int n, double rating, bool sameOwner = false) =>
        new(Id(n), rating, sameOwner);

    /// <summary>Always take index 0, so "who is in the pool" is testable separately from
    /// "which of the pool was drawn".</summary>
    private static readonly Func<int, int> First = _ => 0;

    [Fact]
    public void NobodyToPlayMeansNoSet()
    {
        Assert.Null(RankedMatchmaking.Choose([], 1200, First));
    }

    [Fact]
    public void TheNearestRatedBotIsInThePool()
    {
        MatchmakingCandidate[] candidates =
        [
            Bot(1, 2000),
            Bot(2, 1210),
            Bot(3, 800),
        ];
        Assert.Equal(Id(2), RankedMatchmaking.Choose(candidates, 1200, First));
    }

    [Fact]
    public void OnlyTheNearestFewAreEverDrawn()
    {
        // Ten candidates spread far apart: the pool must be the closest PoolSize, so the
        // distant ones can never come up however the draw falls.
        var candidates = Enumerable.Range(1, 10).Select(n => Bot(n, 1200 + n * 100)).ToList();
        var drawn = Enumerable.Range(0, RankedMatchmaking.PoolSize)
            .Select(i => RankedMatchmaking.Choose(candidates, 1200, _ => i))
            .ToHashSet();

        var nearest = candidates
            .OrderBy(c => c.Rating)
            .Take(RankedMatchmaking.PoolSize)
            .Select(c => (Guid?)c.BotId)
            .ToHashSet();
        Assert.Equal(RankedMatchmaking.PoolSize, drawn.Count);
        Assert.True(drawn.IsSubsetOf(nearest), "a distant bot was drawn into the pool");
    }

    [Fact]
    public void YourOwnBotsAreSkippedWhenSomeoneElseIsAvailable()
    {
        // The same-owner bot is a far better rating match, and is still not chosen.
        MatchmakingCandidate[] candidates =
        [
            Bot(1, 1200, sameOwner: true),
            Bot(2, 1900),
        ];
        Assert.Equal(Id(2), RankedMatchmaking.Choose(candidates, 1200, First));
    }

    [Fact]
    public void YourOwnBotsAreTheFallbackOnAnEmptyLadder()
    {
        // A ladder where you are the only player should still be playable, or a new
        // server has nothing to do at all.
        MatchmakingCandidate[] candidates = [Bot(1, 1150, sameOwner: true)];
        Assert.Equal(Id(1), RankedMatchmaking.Choose(candidates, 1200, First));
    }

    [Fact]
    public void EquallyDistantCandidatesResolveStablyRatherThanByQueryOrder()
    {
        MatchmakingCandidate[] ascending = [Bot(1, 1100), Bot(2, 1300)];
        MatchmakingCandidate[] reversed = [Bot(2, 1300), Bot(1, 1100)];
        Assert.Equal(
            RankedMatchmaking.Choose(ascending, 1200, First),
            RankedMatchmaking.Choose(reversed, 1200, First));
    }
}
