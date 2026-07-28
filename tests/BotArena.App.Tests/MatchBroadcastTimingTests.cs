using BotArena.App.Matches;

namespace BotArena.App.Tests;

public sealed class MatchBroadcastTimingTests
{
    [Fact]
    public void ZeroTickResultRemainsHiddenUntilBroadcastStarts()
    {
        DateTime start = new(
            2026,
            7,
            28,
            12,
            0,
            0,
            DateTimeKind.Utc);
        var match = new Match
        {
            MapId = "zero-tick-map",
            Seed = 1,
            Status = MatchStatus.Completed,
            EndTick = null,
            BroadcastStartedAt = start,
            PresentationTicksPerSecond = 5,
        };

        DateTime countdown = start.AddTicks(-1);
        Assert.False(match.BroadcastComplete(countdown));
        Assert.Null(
            MatchPublicProjection.ToLive(match, countdown).TotalTicks);
        Assert.True(match.BroadcastComplete(start));
        Assert.Equal(
            0,
            MatchPublicProjection.ToLive(match, start).TotalTicks);
        Assert.Equal(start, BroadcastSchedule.CompletesAt(match));
        Assert.Equal(start.AddSeconds(2), BroadcastSchedule.AnnounceAt(match));
    }

    [Fact]
    public void PublicDetailDoesNotExposeStoredExecutionException()
    {
        var match = new Match
        {
            MapId = "failed-map",
            Seed = 1,
            Status = MatchStatus.Failed,
            Error = "S3_ACCESS_KEY=operator-secret",
        };

        MatchDetailResponse detail =
            MatchPublicProjection.ToDetail(match, DateTime.UtcNow);

        Assert.Equal("execution_failed", detail.Error);
        Assert.DoesNotContain("operator-secret", detail.Error!);
    }
}
