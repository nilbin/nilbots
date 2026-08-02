using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>Seeds immutable Arc Relay entrant version 2 and its open ladder.</summary>
public sealed class ArcRelayEntrantPlaylistSeeder(AppDbContext db)
{
    public async Task<ArcRelayEntrantSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        ArcRelayEntrantPlaylistDefinition expected =
            ArcRelayEntrantPlaylistDefinition.Create();
        Playlist playlist = await db.Playlists.SingleAsync(
            value => value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            cancellationToken);
        PlaylistVersion? version = await db.PlaylistVersions.SingleOrDefaultAsync(
            value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.Version,
            cancellationToken);
        if (version is null)
        {
            version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayEntrantPlaylistDefinition.Version,
                GameModeId = expected.Match.Rules.GameMode.ModeId,
                RulesetId = expected.Match.Rules.RulesetId,
                MatchFormatId = expected.Match.Format.FormatId,
                MapPoolId = expected.Match.Map.Id,
                SeriesPolicyId = ArcRelayEntrantPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId = ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = expected.AdmissionPolicyId,
                ExecutionPolicyId = expected.ExecutionPolicyId,
                ExecutionEngineVersion = expected.ExecutionEngineVersion,
                CanonicalDefinition = expected.CanonicalDefinition,
                DefinitionFingerprint = expected.DefinitionFingerprint,
                Provenance = expected.Provenance,
                Visibility = ArcRelayEntrantPlaylistDefinition.Visibility,
            };
            db.PlaylistVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);
        }
        expected.Validate(playlist, version);

        Season? season = await db.Seasons.SingleOrDefaultAsync(
            value => value.Key == ArcRelayLadderPolicy.SeasonKey,
            cancellationToken);
        if (season is null)
        {
            season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
            };
            db.Seasons.Add(season);
            await db.SaveChangesAsync(cancellationToken);
        }

        Ladder? ladder = await db.Ladders.SingleOrDefaultAsync(
            value => value.PlaylistVersionId == version.Id &&
                value.SeasonId == season.Id,
            cancellationToken);
        if (ladder is null)
        {
            ladder = new Ladder
            {
                PlaylistVersionId = version.Id,
                SeasonId = season.Id,
                Status = LadderStatus.Open,
                RatingPolicyId = ArcRelayEloV1.Id,
                IsListed = true,
                AwardsAchievements = false,
            };
            db.Ladders.Add(ladder);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (ladder.Status != LadderStatus.Open ||
            !string.Equals(ladder.RatingPolicyId, ArcRelayEloV1.Id, StringComparison.Ordinal) ||
            !ladder.IsListed || ladder.AwardsAchievements)
        {
            throw new InvalidOperationException("Arc Relay entrant ladder contradicts its immutable launch policy.");
        }
        return new ArcRelayEntrantSeedResult(version, ladder);
    }
}

public sealed record ArcRelayEntrantSeedResult(
    PlaylistVersion PlaylistVersion,
    Ladder Ladder);
