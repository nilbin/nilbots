using BotArena.App.ArcRelay;
using BotArena.App.Sheets;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>
/// Seeds the current immutable entrant lane. Season 1 began on repaired v8
/// and continued through v12. Playlist v13 deliberately starts a fresh rating
/// book because its sticky authored-route course changes the registered stock
/// cell's gameplay-state digest; immutable v8-v12 ladders remain readable.
/// </summary>
public sealed class ArcRelayEntrantPlaylistSeeder(
    AppDbContext db,
    TimeProvider timeProvider,
    ArcRelaySeasonLifecycleService seasons)
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
        PlaylistVersion? ambushVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version == ArcRelayEntrantPlaylistDefinition.AmbushVersion,
                cancellationToken);
        if (ambushVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV7()
                .Validate(playlist, ambushVersion);
        }
        PlaylistVersion? wasmRepairVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.WasmRepairVersion,
                cancellationToken);
        if (wasmRepairVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV8()
                .Validate(playlist, wasmRepairVersion);
        }
        PlaylistVersion? directLadderVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.DirectLadderVersion,
                cancellationToken);
        if (directLadderVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV9()
                .Validate(playlist, directLadderVersion);
        }
        PlaylistVersion? semanticIntentVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.SemanticIntentVersion,
                cancellationToken);
        if (semanticIntentVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV10()
                .Validate(playlist, semanticIntentVersion);
        }
        PlaylistVersion? boundedMovementVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.BoundedMovementVersion,
                cancellationToken);
        if (boundedMovementVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV11()
                .Validate(playlist, boundedMovementVersion);
        }
        PlaylistVersion? relativeWellVersion = await db.PlaylistVersions
            .SingleOrDefaultAsync(value => value.PlaylistId == playlist.Id &&
                value.Version ==
                    ArcRelayEntrantPlaylistDefinition.RelativeWellVersion,
                cancellationToken);
        if (relativeWellVersion is not null)
        {
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV12()
                .Validate(playlist, relativeWellVersion);
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
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
                Number = ArcRelayLadderPolicy.SeasonNumber,
                Status = SeasonStatus.Scheduled,
                StartsAt = now,
                EndsAt = now.AddDays(
                    ArcRelayLadderPolicy.SeasonDurationDays),
                RatingSeedPolicyId =
                    ArcRelayLadderPolicy.RatingSeedPolicyId,
                CreatedAt = now,
            };
            db.Seasons.Add(season);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (season.Number != ArcRelayLadderPolicy.SeasonNumber ||
            !string.Equals(season.DisplayName,
                ArcRelayLadderPolicy.SeasonName,
                StringComparison.Ordinal) ||
            !string.Equals(season.RatingSeedPolicyId,
                ArcRelayLadderPolicy.RatingSeedPolicyId,
                StringComparison.Ordinal) ||
            season.StartsAt is null || season.EndsAt is null ||
            season.EndsAt.Value - season.StartsAt.Value !=
                TimeSpan.FromDays(ArcRelayLadderPolicy.SeasonDurationDays))
        {
            throw new InvalidOperationException(
                "Arc Relay Season 1 contradicts its registered definition.");
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
                Status = LadderStatus.Draft,
                RatingPolicyId = ArcRelayEloV1.Id,
                IsListed = false,
                AwardsAchievements = false,
            };
            db.Ladders.Add(ladder);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (!string.Equals(ladder.RatingPolicyId, ArcRelayEloV1.Id,
                StringComparison.Ordinal) || ladder.AwardsAchievements)
        {
            throw new InvalidOperationException(
                "Arc Relay Season 1 ladder contradicts its registered policy.");
        }

        if (season.Status == SeasonStatus.Open &&
            ladder.Status == LadderStatus.Draft)
        {
            await StartFreshOpenRatingBookAsync(
                playlist, season, ladder, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await seasons.AdvanceAsync(cancellationToken);
        ladder = await db.Ladders.AsNoTracking().SingleAsync(
            value => value.Id == ladder.Id, cancellationToken);
        return new ArcRelayEntrantSeedResult(version, ladder);
    }

    private async Task StartFreshOpenRatingBookAsync(
        Playlist playlist,
        Season season,
        Ladder current,
        CancellationToken cancellationToken)
    {
        var candidates = await (
            from ladder in db.Ladders
            join version in db.PlaylistVersions
                on ladder.PlaylistVersionId equals version.Id
            where version.PlaylistId == playlist.Id &&
                version.Version >=
                    ArcRelayEntrantPlaylistDefinition.WasmRepairVersion &&
                version.Version <=
                    ArcRelayEntrantPlaylistDefinition.RelativeWellVersion &&
                ladder.SeasonId == season.Id
            select new { Ladder = ladder, version.Version })
            .ToArrayAsync(cancellationToken);
        var open = candidates
            .Where(value => value.Ladder.Status == LadderStatus.Open)
            .ToArray();
        if (open.Length != 1)
        {
            throw new InvalidOperationException(
                "An open Arc Relay Season 1 can start the fresh v13 rating " +
                "book only from one open immutable v8-v12 ladder.");
        }
        Ladder previous = open[0].Ladder;
        if (await db.ArcRelayEntrantRatings.AnyAsync(
                value => value.LadderId == current.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The draft Arc Relay v13 ladder already contains ratings.");
        }

        Guid[] entrants = await db.ArcRelayEntrants
            .Where(value => value.LadderOptedIn)
            .OrderBy(value => value.Id)
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        foreach (Guid entrantId in entrants)
        {
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = entrantId,
                LadderId = current.Id,
            });
        }

        previous.Status = LadderStatus.Closed;
        previous.IsListed = false;
        current.Status = LadderStatus.Open;
        current.IsListed = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ArcRelayEntrantSeedResult(
    PlaylistVersion PlaylistVersion,
    Ladder Ladder);
