using System.Text.Json;
using BotArena.App.Competition;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class FrontlineLabsPlaylistDefinitionTests
{
    [Fact]
    public void CanonicalIdentityPinsExecutionRouteAndSemanticEngine()
    {
        FrontlineLabsPlaylistDefinition definition =
            FrontlineLabsPlaylistDefinition.Create();

        using JsonDocument canonical =
            JsonDocument.Parse(definition.CanonicalDefinition);
        Assert.Equal(
            PlaylistExecutionPolicyIds.GenericActor,
            canonical.RootElement
                .GetProperty("executionPolicyId")
                .GetString());
        Assert.Equal(
            BotArenaVersions.GenericActorEngineVersion,
            canonical.RootElement
                .GetProperty("executionEngineVersion")
                .GetString());
        Assert.Equal(
            FrontlineLabsReplayPresentation.ThemeId,
            definition.ReplayPresentation.ThemeId);
        Assert.Equal(
            FrontlineLabsReplayPresentation.BoundaryWallFamily,
            definition.ReplayPresentation.Map?.BoundaryWall);
        Assert.Equal(
            FrontlineLabsReplayPresentation.InteriorWallFamily,
            definition.ReplayPresentation.Map?.InteriorWall);
    }

    [Fact]
    public void DisplayMetadataIsNotPartOfExecutableValidation()
    {
        FrontlineLabsPlaylistDefinition definition =
            FrontlineLabsPlaylistDefinition.Create();
        var playlist = new Playlist
        {
            Key = FrontlineLabsPlaylistDefinition.PlaylistKey,
            DisplayName = "Renamed Labs",
        };
        PlaylistVersion version = PersistedVersion(
            playlist.Id,
            definition);

        definition.Validate(playlist, version);
    }

    private static PlaylistVersion PersistedVersion(
        Guid playlistId,
        FrontlineLabsPlaylistDefinition definition) =>
        new()
        {
            PlaylistId = playlistId,
            Version = FrontlineLabsPlaylistDefinition.Version,
            GameModeId = definition.GameModeId,
            RulesetId = definition.RulesetId,
            MatchFormatId = definition.MatchFormatId,
            MapPoolId = definition.MapPoolId,
            SeriesPolicyId =
                FrontlineLabsPlaylistDefinition.SeriesPolicyId,
            MatchmakingPolicyId =
                FrontlineLabsPlaylistDefinition.MatchmakingPolicyId,
            AdmissionPolicyId = definition.AdmissionPolicyId,
            ExecutionPolicyId = definition.ExecutionPolicyId,
            ExecutionEngineVersion =
                definition.ExecutionEngineVersion,
            CanonicalDefinition = definition.CanonicalDefinition,
            DefinitionFingerprint = definition.DefinitionFingerprint,
            Provenance = definition.Provenance,
            Visibility = FrontlineLabsPlaylistDefinition.Visibility,
        };
}
