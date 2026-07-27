using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class LadderDefinitionTests
{
    [Fact]
    public void BindsOnePopulationToPlaylistSeasonAndRatingPolicy()
    {
        LadderId ladderId = LadderId.New();
        PlaylistVersionId playlistVersionId = PlaylistVersionId.New();

        var ladder = new LadderDefinition(
            ladderId,
            playlistVersionId,
            "season-2026-03",
            LadderStatus.Open,
            DuelEloV1.Id);

        Assert.Equal(ladderId, ladder.Id);
        Assert.Equal(playlistVersionId, ladder.PlaylistVersionId);
        Assert.Equal("season-2026-03", ladder.SeasonId);
        Assert.Equal(LadderStatus.Open, ladder.Status);
        Assert.Equal(DuelEloV1.Id, ladder.RatingPolicyId);
    }

    [Fact]
    public void InvalidBindingsFailBeforePersistence()
    {
        Assert.Throws<ArgumentException>(() => new LadderDefinition(
            default,
            PlaylistVersionId.New(),
            "season-1",
            LadderStatus.Draft,
            DuelEloV1.Id));
        Assert.Throws<ArgumentException>(() => new LadderDefinition(
            LadderId.New(),
            default,
            "season-1",
            LadderStatus.Draft,
            DuelEloV1.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LadderDefinition(
            LadderId.New(),
            PlaylistVersionId.New(),
            "season-1",
            (LadderStatus)999,
            DuelEloV1.Id));
    }
}
