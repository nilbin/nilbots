using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class CompetitionIdTests
{
    [Fact]
    public void LadderId_RoundTripsWithoutExposingRulesSemantics()
    {
        Guid value = Guid.NewGuid();

        LadderId id = LadderId.From(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(id, LadderId.Parse(id.ToString()));
        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void PlaylistVersionId_RoundTripsWithoutExposingCatalogSemantics()
    {
        Guid value = Guid.NewGuid();

        PlaylistVersionId id = PlaylistVersionId.From(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(id, PlaylistVersionId.Parse(id.ToString()));
        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void SeasonId_RoundTripsWithoutExposingDisplaySemantics()
    {
        Guid value = Guid.NewGuid();

        SeasonId id = SeasonId.From(value);

        Assert.Equal(value, id.Value);
        Assert.Equal(id, SeasonId.Parse(id.ToString()));
        Assert.False(id.IsEmpty);
    }

    [Fact]
    public void EmptyAndMalformedIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => LadderId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => PlaylistVersionId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => SeasonId.From(Guid.Empty));
        Assert.False(LadderId.TryParse(Guid.Empty.ToString(), out _));
        Assert.False(PlaylistVersionId.TryParse("not-a-guid", out _));
        Assert.False(SeasonId.TryParse("not-a-guid", out _));
    }
}
