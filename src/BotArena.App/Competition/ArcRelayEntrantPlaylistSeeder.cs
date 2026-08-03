using BotArena.App.ArcRelay;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>
/// Seeds the current immutable entrant lane and performs the one-way hosted-map
/// cutover without changing entrant identities or losing their rating record.
/// </summary>
public sealed class ArcRelayEntrantPlaylistSeeder(
    AppDbContext db,
    ArcRelayPlayerSheetCodec sheetCodec,
    ArcRelayClassCatalog classCatalog)
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
        PlaylistVersion? forwardVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.PreviousVersion,
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
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is not null)
        {
            List<ArcRelayEntrantRating> priorRatings = await db.ArcRelayEntrantRatings
                .Where(value => value.LadderId == previous.Ladder.Id)
                .ToListAsync(cancellationToken);
            HashSet<Guid> existing = await db.ArcRelayEntrantRatings
                .Where(value => value.LadderId == ladder.Id)
                .Select(value => value.EntrantId)
                .ToHashSetAsync(cancellationToken);
            db.ArcRelayEntrantRatings.AddRange(priorRatings
                .Where(value => !existing.Contains(value.EntrantId))
                .Select(value => new ArcRelayEntrantRating
                {
                    EntrantId = value.EntrantId,
                    LadderId = ladder.Id,
                    Rating = value.Rating,
                    RankedMatches = value.RankedMatches,
                }));

            if (previous.Version < ArcRelayEntrantPlaylistDefinition.PreviousVersion)
            {
                IReadOnlySet<string> allClasses = classCatalog.All
                    .Select(value => value.Id)
                    .ToHashSet(StringComparer.Ordinal);
                List<ArcRelaySheet> sheets = await db.ArcRelaySheets.ToListAsync(
                    cancellationToken);
                foreach (ArcRelaySheet sheet in sheets)
                {
                    ArcRelaySheetDocument source = sheetCodec.Read(
                        sheet.CanonicalJson);
                    if (string.Equals(
                            source.MapId,
                            ArcRelayLoopProfile.Current.MapId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    int revision = sheet.Revision + 1;
                    ArcRelaySheetDocument migrated = sheetCodec.UpgradeToCurrentMap(
                        source,
                        allClasses);
                    ArcRelaySheetCompilation compilation = sheetCodec.Compile(
                        migrated,
                        allClasses,
                        $"{sheet.Id}:r{revision}");
                    sheet.Revision = revision;
                    sheet.CanonicalJson = compilation.CanonicalJson;
                    sheet.ContentHash = compilation.ContentHash;
                    sheet.UpdatedAt = DateTime.UtcNow;
                    ArcRelayEntrant? entrant = await db.ArcRelayEntrants
                        .SingleOrDefaultAsync(
                            value => value.Id == sheet.Id,
                            cancellationToken);
                    if (entrant is not null)
                        entrant.UpdatedAt = sheet.UpdatedAt;
                }

                List<ArcRelayEntrant> minds = await db.ArcRelayEntrants
                    .Where(value => value.Kind == ArcRelayEntrantKind.CustomMind)
                    .ToListAsync(cancellationToken);
                foreach (ArcRelayEntrant mind in minds)
                {
                    mind.PreflightStatus = ArcRelayPreflightStatus.Required;
                    mind.PreflightMatchId = null;
                    mind.PreflightRevision = null;
                    mind.PreflightFailure = null;
                    mind.LadderOptedIn = false;
                    mind.LadderOptedInAt = null;
                    mind.UpdatedAt = DateTime.UtcNow;
                }
            }

            previous.Ladder.Status = LadderStatus.Closed;
            previous.Ladder.IsListed = false;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ArcRelayEntrantSeedResult(version, ladder);
    }
}

public sealed record ArcRelayEntrantSeedResult(
    PlaylistVersion PlaylistVersion,
    Ladder Ladder);
