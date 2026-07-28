using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class PlaylistVersionDefinitionTests
{
    [Fact]
    public void CapturesTheCompletePlaylistTerminology()
    {
        PlaylistVersionId id = PlaylistVersionId.New();

        var definition = new PlaylistVersionDefinition(
            id,
            "frontline-ranked",
            2,
            "Frontline Ranked",
            "frontline",
            "frontline-0.2",
            "clone-squad-1v1",
            "frontline-ranked-maps-2",
            "mirrored-three-map-series-1",
            "nearest-rating-v1",
            "actor-contract-v3",
            new string('a', 64));

        Assert.Equal(id, definition.Id);
        Assert.Equal("frontline-ranked", definition.PlaylistKey);
        Assert.Equal(2, definition.Version);
        Assert.Equal("frontline", definition.GameModeId);
        Assert.Equal("frontline-0.2", definition.RulesetId);
        Assert.Equal("clone-squad-1v1", definition.MatchFormatId);
        Assert.Equal(
            "mirrored-three-map-series-1",
            definition.SeriesPolicyId);
        Assert.Equal(
            "nearest-rating-v1",
            definition.MatchmakingPolicyId);
        Assert.Equal(
            "actor-contract-v3",
            definition.AdmissionPolicyId);
    }

    [Fact]
    public void InvalidDefinitionsFailBeforeTheyReachACatalog()
    {
        Assert.Throws<ArgumentException>(() => new PlaylistVersionDefinition(
            default,
            "duel-ranked",
            1,
            "Duel",
            "duel",
            "0.5",
            "duel-1v1",
            "ranked",
            "mirrored-six",
            "nearest-rating-v1",
            "legacy-duel-v1",
            new string('a', 64)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaylistVersionDefinition(
            PlaylistVersionId.New(),
            "duel-ranked",
            0,
            "Duel",
            "duel",
            "0.5",
            "duel-1v1",
            "ranked",
            "mirrored-six",
            "nearest-rating-v1",
            "legacy-duel-v1",
            new string('a', 64)));
        Assert.Throws<ArgumentException>(() => new PlaylistVersionDefinition(
            PlaylistVersionId.New(),
            "duel-ranked",
            1,
            " ",
            "duel",
            "0.5",
            "duel-1v1",
            "ranked",
            "mirrored-six",
            "nearest-rating-v1",
            "legacy-duel-v1",
            new string('a', 64)));
        Assert.Throws<ArgumentException>(() => new PlaylistVersionDefinition(
            PlaylistVersionId.New(),
            "duel-ranked",
            1,
            "Duel",
            "duel",
            "0.5",
            "duel-1v1",
            "ranked",
            "mirrored-six",
            "nearest-rating-v1",
            "legacy-duel-v1",
            "fingerprint"));
    }
}
