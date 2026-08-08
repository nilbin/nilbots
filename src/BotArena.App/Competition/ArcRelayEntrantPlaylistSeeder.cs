using BotArena.App.ArcRelay;
using BotArena.App.Sheets;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>
/// Seeds the current immutable entrant lane. Playlist v7 is a new rating era:
/// entrant identities survive, while ratings and map admission start again on
/// the Warren rather than being projected or carried from Counterflow.
/// </summary>
public sealed class ArcRelayEntrantPlaylistSeeder(AppDbContext db)
{
    public async Task<ArcRelayEntrantSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        ArcRelayEntrantPlaylistDefinition expected =
            ArcRelayEntrantPlaylistDefinition.Create();
        Playlist playlist = await db.Playlists.SingleAsync(
            value => value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            cancellationToken);
        PlaylistVersion? historicalVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.HistoricalVersion,
                cancellationToken);
        if (historicalVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV2()
                .Validate(playlist, historicalVersion);
        }
        PlaylistVersion? counterflowVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.CounterflowVersion,
                cancellationToken);
        if (counterflowVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV3()
                .Validate(playlist, counterflowVersion);
        }
        PlaylistVersion? tacticalVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.TacticalVersion,
                cancellationToken);
        if (tacticalVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV6()
                .Validate(playlist, tacticalVersion);
        }
        PlaylistVersion? recoveryVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.RecoveryVersion,
                cancellationToken);
        if (recoveryVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV5()
                .Validate(playlist, recoveryVersion);
        }
        PlaylistVersion? forwardVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.ForwardVersion,
                cancellationToken);
        if (forwardVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV4()
                .Validate(playlist, forwardVersion);
        }
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

        var previous = await (
            from previousLadder in db.Ladders
            join previousVersion in db.PlaylistVersions
                on previousLadder.PlaylistVersionId equals previousVersion.Id
            where previousVersion.PlaylistId == playlist.Id
                && previousVersion.Version < ArcRelayEntrantPlaylistDefinition.Version
                && previousLadder.SeasonId == season.Id
                && previousLadder.Status == LadderStatus.Open
            orderby previousVersion.Version descending
            select new { Ladder = previousLadder, Version = previousVersion.Version })
            .ToArrayAsync(cancellationToken);

        if (previous.Length > 0)
        {
            HashSet<Guid> existing = await db.ArcRelayEntrantRatings
                .Where(value => value.LadderId == ladder.Id)
                .Select(value => value.EntrantId)
                .ToHashSetAsync(cancellationToken);
            List<ArcRelayEntrant> entrants = await db.ArcRelayEntrants
                .ToListAsync(cancellationToken);
            Dictionary<Guid, TacticalSheet> sheets = await db.TacticalSheets
                .ToDictionaryAsync(value => value.Id, cancellationToken);
            DateTime now = DateTime.UtcNow;
            foreach (ArcRelayEntrant entrant in entrants)
            {
                if (entrant.Kind == ArcRelayEntrantKind.CustomMind)
                {
                    entrant.PreflightStatus = ArcRelayPreflightStatus.Required;
                    entrant.PreflightMatchId = null;
                    entrant.PreflightRevision = null;
                    entrant.PreflightFailure = null;
                    entrant.LadderOptedIn = false;
                    entrant.LadderOptedInAt = null;
                    entrant.UpdatedAt = now;
                    continue;
                }

                bool admissible = sheets.TryGetValue(
                        entrant.Id, out TacticalSheet? sheet)
                    && TacticalSheetCompiler.IsAdmissibleToV7(
                        sheet.LayoutJson);
                if (!admissible)
                {
                    entrant.LadderOptedIn = false;
                    entrant.LadderOptedInAt = null;
                    entrant.UpdatedAt = now;
                    continue;
                }

                // This is deliberately a fresh default record, never a copy of
                // the prior ladder's rating or match count.
                if (entrant.LadderOptedIn && existing.Add(entrant.Id))
                {
                    db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
                    {
                        EntrantId = entrant.Id,
                        LadderId = ladder.Id,
                    });
                }
            }

            foreach (var historical in previous)
            {
                historical.Ladder.Status = LadderStatus.Closed;
                historical.Ladder.IsListed = false;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ArcRelayEntrantSeedResult(version, ladder);
    }
}

public sealed record ArcRelayEntrantSeedResult(
    PlaylistVersion PlaylistVersion,
    Ladder Ladder);
