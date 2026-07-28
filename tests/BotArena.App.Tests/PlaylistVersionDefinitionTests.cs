using BotArena.App.Competition;
using BotArena.Engine;

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
            PlaylistExecutionPolicyIds.GenericActor,
            BotArenaVersions.GenericActorEngineVersion,
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
        Assert.Equal(
            PlaylistExecutionPolicyIds.GenericActor,
            definition.ExecutionPolicyId);
        Assert.Equal(
            BotArenaVersions.GenericActorEngineVersion,
            definition.ExecutionEngineVersion);
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
            PlaylistExecutionPolicyIds.LegacyDuel,
            BotArenaVersions.EngineVersion,
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
            PlaylistExecutionPolicyIds.LegacyDuel,
            BotArenaVersions.EngineVersion,
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
            PlaylistExecutionPolicyIds.LegacyDuel,
            BotArenaVersions.EngineVersion,
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
            PlaylistExecutionPolicyIds.LegacyDuel,
            BotArenaVersions.EngineVersion,
            "fingerprint"));
    }
}
