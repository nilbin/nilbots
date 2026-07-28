using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>
/// Creates or verifies the hidden immutable identity used by Frontline Labs.
/// It intentionally creates no season or ladder.
/// </summary>
public sealed class FrontlineLabsPlaylistSeeder(AppDbContext db)
{
    public async Task<PlaylistVersion> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        FrontlineLabsPlaylistDefinition expected =
            FrontlineLabsPlaylistDefinition.Create();
        Playlist? playlist = await db.Playlists.SingleOrDefaultAsync(
            candidate =>
                candidate.Key ==
                FrontlineLabsPlaylistDefinition.PlaylistKey,
            cancellationToken);
        if (playlist is null)
        {
            playlist = new Playlist
            {
                Key = FrontlineLabsPlaylistDefinition.PlaylistKey,
                DisplayName =
                    FrontlineLabsPlaylistDefinition.DisplayName,
            };
            db.Playlists.Add(playlist);
            await db.SaveChangesAsync(cancellationToken);
        }

        PlaylistVersion? version =
            await db.PlaylistVersions.SingleOrDefaultAsync(
                candidate =>
                    candidate.PlaylistId == playlist.Id &&
                    candidate.Version ==
                        FrontlineLabsPlaylistDefinition.Version,
                cancellationToken);
        if (version is null)
        {
            version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = FrontlineLabsPlaylistDefinition.Version,
                GameModeId = expected.GameModeId,
                RulesetId = expected.RulesetId,
                MatchFormatId = expected.MatchFormatId,
                MapPoolId = expected.MapPoolId,
                SeriesPolicyId =
                    FrontlineLabsPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId =
                    FrontlineLabsPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = expected.AdmissionPolicyId,
                ExecutionPolicyId = expected.ExecutionPolicyId,
                ExecutionEngineVersion =
                    expected.ExecutionEngineVersion,
                CanonicalDefinition = expected.CanonicalDefinition,
                DefinitionFingerprint =
                    expected.DefinitionFingerprint,
                Provenance = expected.Provenance,
                Visibility =
                    FrontlineLabsPlaylistDefinition.Visibility,
            };
            db.PlaylistVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);
        }

        expected.Validate(playlist, version);
        if (await db.Ladders.AnyAsync(
                ladder => ladder.PlaylistVersionId == version.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Frontline Labs v1 is an unranked experimental playlist and " +
                "must not have a ladder.");
        }

        return version;
    }
}
