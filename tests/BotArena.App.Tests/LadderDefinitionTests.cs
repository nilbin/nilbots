using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class LadderDefinitionTests
{
    [Fact]
    public void BindsOnePopulationToPlaylistSeasonAndRatingPolicy()
    {
        LadderId ladderId = LadderId.New();
        PlaylistVersionId playlistVersionId = PlaylistVersionId.New();
        SeasonId seasonId = SeasonId.New();

        var ladder = new LadderDefinition(
            ladderId,
            playlistVersionId,
            seasonId,
            LadderStatus.Open,
            DuelEloV1.Id);

        Assert.Equal(ladderId, ladder.Id);
        Assert.Equal(playlistVersionId, ladder.PlaylistVersionId);
        Assert.Equal(seasonId, ladder.SeasonId);
        Assert.Equal(LadderStatus.Open, ladder.Status);
        Assert.Equal(DuelEloV1.Id, ladder.RatingPolicyId);
    }

    [Fact]
    public void InvalidBindingsFailBeforePersistence()
    {
        Assert.Throws<ArgumentException>(() => new LadderDefinition(
            default,
            PlaylistVersionId.New(),
            SeasonId.New(),
            LadderStatus.Draft,
            DuelEloV1.Id));
        Assert.Throws<ArgumentException>(() => new LadderDefinition(
            LadderId.New(),
            default,
            SeasonId.New(),
            LadderStatus.Draft,
            DuelEloV1.Id));
        Assert.Throws<ArgumentException>(() => new LadderDefinition(
            LadderId.New(),
            PlaylistVersionId.New(),
            default,
            LadderStatus.Draft,
            DuelEloV1.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LadderDefinition(
            LadderId.New(),
            PlaylistVersionId.New(),
            SeasonId.New(),
            (LadderStatus)999,
            DuelEloV1.Id));
    }
}
