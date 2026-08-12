using System.Text.Json;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Sheets;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

/// <summary>
/// Owns the transactional edges of the hosted season. The wall clock can stop
/// admission, but only an explicit Closing pass can wait for reveal-ordered
/// settlement and freeze historical standings.
/// </summary>
public sealed class ArcRelaySeasonLifecycleService(
    AppDbContext db,
    TimeProvider timeProvider)
{
    public async Task AdvanceAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AdmissionLocks.LabsMatchPool})",
            cancellationToken);

        Season? season = await db.Seasons.SingleOrDefaultAsync(
            value => value.Key == ArcRelayLadderPolicy.SeasonKey,
            cancellationToken);
        if (season is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        Ladder ladder = await CurrentLadderQuery(season.Id)
            .SingleAsync(cancellationToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        if (season.Status == SeasonStatus.Scheduled &&
            season.StartsAt is DateTime startsAt && startsAt <= now)
        {
            Ladder[] priorOpen = await CurrentPlaylistLadders()
                .Where(value => value.Id != ladder.Id &&
                    value.Status == LadderStatus.Open)
                .ToArrayAsync(cancellationToken);
            foreach (Ladder prior in priorOpen)
            {
                prior.Status = LadderStatus.Closed;
                prior.IsListed = false;
            }

            ArcRelayEntrant[] entrants = await db.ArcRelayEntrants
                .Where(value => value.LadderOptedIn)
                .OrderBy(value => value.Id)
                .ToArrayAsync(cancellationToken);
            foreach (ArcRelayEntrant entrant in entrants)
            {
                db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
                {
                    EntrantId = entrant.Id,
                    LadderId = ladder.Id,
                });
            }
            await db.SaveChangesAsync(cancellationToken);

            ArcRelayEntrantRating[] openingRatings = await db
                .ArcRelayEntrantRatings
                .Where(value => value.LadderId == ladder.Id)
                .OrderByDescending(value => value.Rating)
                .ThenBy(value => value.EntrantId)
                .ToArrayAsync(cancellationToken);
            int index = 0;
            int rank = 0;
            double? previous = null;
            foreach (ArcRelayEntrantRating rating in openingRatings)
            {
                index++;
                if (previous is null || rating.Rating != previous.Value)
                    rank = index;
                previous = rating.Rating;
                ArcRelayEntrant entrant = entrants.Single(value =>
                    value.Id == rating.EntrantId);
                db.ArcRelaySeasonEntrants.Add(new ArcRelaySeasonEntrant
                {
                    SeasonId = season.Id,
                    EntrantId = entrant.Id,
                    EnteredAt = now,
                    PresentAtOpening = true,
                    OpeningRating = rating.Rating,
                    OpeningRank = rank,
                    OpeningRevision = await RevisionAsync(
                        entrant, cancellationToken),
                    OpeningPresentationJson = JsonSerializer.Serialize(
                        await PresentationAsync(entrant, cancellationToken)),
                });
            }

            season.Status = SeasonStatus.Open;
            season.OpenedAt = now;
            ladder.Status = LadderStatus.Open;
            ladder.IsListed = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (season.Status == SeasonStatus.Open &&
            season.EndsAt is DateTime endsAt && endsAt <= now)
        {
            season.Status = SeasonStatus.Closing;
            ladder.Status = LadderStatus.Closed;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (season.Status == SeasonStatus.Closing)
        {
            bool unsettled = await (
                from ranked in db.ArcRelayRankedMatches
                join match in db.Matches on ranked.MatchId equals match.Id
                where ranked.LadderId == ladder.Id &&
                    ranked.SettledAt == null &&
                    match.Status != MatchStatus.Failed
                select ranked).AnyAsync(cancellationToken);
            if (!unsettled)
            {
                await FreezeClosingStandingsAsync(
                    season, ladder, cancellationToken);
                season.Status = SeasonStatus.Closed;
                season.ClosedAt = now;
                ladder.Status = LadderStatus.Closed;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task TrackLateEntrantAsync(
        ArcRelayEntrant entrant,
        Guid ladderId,
        CancellationToken cancellationToken,
        int? revisionOverride = null,
        string? sheetPlaybookJsonOverride = null)
    {
        Season season = await (
            from ladder in db.Ladders
            join candidate in db.Seasons on ladder.SeasonId equals candidate.Id
            where ladder.Id == ladderId
            select candidate).SingleAsync(cancellationToken);
        if (season.Status != SeasonStatus.Open)
            throw new InvalidOperationException("The current season is not open.");
        bool exists = await db.ArcRelaySeasonEntrants.AnyAsync(
            value => value.SeasonId == season.Id &&
                value.EntrantId == entrant.Id,
            cancellationToken);
        if (exists)
            return;
        db.ArcRelaySeasonEntrants.Add(new ArcRelaySeasonEntrant
        {
            SeasonId = season.Id,
            EntrantId = entrant.Id,
            EnteredAt = timeProvider.GetUtcNow().UtcDateTime,
            PresentAtOpening = false,
            OpeningRating = BotRating.DefaultRating,
            OpeningRank = null,
            OpeningRevision = revisionOverride ?? await RevisionAsync(
                entrant, cancellationToken),
            OpeningPresentationJson = JsonSerializer.Serialize(
                await PresentationAsync(
                    entrant, cancellationToken, sheetPlaybookJsonOverride)),
        });
    }

    public async Task<(Season Season, Ladder Ladder)> CurrentAsync(
        CancellationToken cancellationToken)
    {
        Season season = await db.Seasons.AsNoTracking().SingleAsync(
            value => value.Key == ArcRelayLadderPolicy.SeasonKey,
            cancellationToken);
        Ladder ladder = await CurrentLadderQuery(season.Id)
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        return (season, ladder);
    }

    private IQueryable<Ladder> CurrentLadderQuery(Guid seasonId) =>
        from ladder in db.Ladders
        join version in db.PlaylistVersions on ladder.PlaylistVersionId equals version.Id
        join playlist in db.Playlists on version.PlaylistId equals playlist.Id
        where seasonId == ladder.SeasonId &&
            playlist.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey &&
            version.Version == ArcRelayEntrantPlaylistDefinition.Version
        select ladder;

    private IQueryable<Ladder> CurrentPlaylistLadders() =>
        from ladder in db.Ladders
        join version in db.PlaylistVersions on ladder.PlaylistVersionId equals version.Id
        join playlist in db.Playlists on version.PlaylistId equals playlist.Id
        where playlist.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey
        select ladder;

    private async Task FreezeClosingStandingsAsync(
        Season season,
        Ladder ladder,
        CancellationToken cancellationToken)
    {
        ArcRelayEntrantRating[] ratings = await db.ArcRelayEntrantRatings
            .Where(value => value.LadderId == ladder.Id)
            .OrderByDescending(value => value.Rating)
            .ThenBy(value => value.EntrantId)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, ArcRelayEntrant> entrants = await db.ArcRelayEntrants
            .Where(value => ratings.Select(rating => rating.EntrantId)
                .Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        Dictionary<Guid, ArcRelaySeasonEntrant> snapshots = await db
            .ArcRelaySeasonEntrants
            .Where(value => value.SeasonId == season.Id)
            .ToDictionaryAsync(value => value.EntrantId, cancellationToken);

        int index = 0;
        int rank = 0;
        double? previous = null;
        foreach (ArcRelayEntrantRating rating in ratings)
        {
            index++;
            if (previous is null || rating.Rating != previous.Value)
                rank = index;
            previous = rating.Rating;
            ArcRelayEntrant entrant = entrants[rating.EntrantId];
            if (!snapshots.TryGetValue(entrant.Id, out ArcRelaySeasonEntrant? snapshot))
            {
                snapshot = new ArcRelaySeasonEntrant
                {
                    SeasonId = season.Id,
                    EntrantId = entrant.Id,
                    EnteredAt = entrant.LadderOptedInAt ?? entrant.CreatedAt,
                    PresentAtOpening = false,
                    OpeningRating = BotRating.DefaultRating,
                    OpeningRevision = await RevisionAsync(
                        entrant, cancellationToken),
                    OpeningPresentationJson = JsonSerializer.Serialize(
                        await PresentationAsync(entrant, cancellationToken)),
                };
                db.ArcRelaySeasonEntrants.Add(snapshot);
            }
            snapshot.ClosingRating = rating.Rating;
            snapshot.ClosingRank = rank;
            snapshot.ClosingRevision = await RevisionAsync(
                entrant, cancellationToken);
            snapshot.ClosingPresentationJson = JsonSerializer.Serialize(
                await PresentationAsync(entrant, cancellationToken));
        }
    }

    private async Task<int> RevisionAsync(
        ArcRelayEntrant entrant,
        CancellationToken cancellationToken) =>
        entrant.Kind == ArcRelayEntrantKind.Sheet
            ? await db.TacticalSheets.Where(value => value.Id == entrant.Id)
                .Select(value => value.Revision)
                .SingleAsync(cancellationToken)
            : (await db.BotVersions
                .Where(value => value.BotId == entrant.MindBotId)
                .Select(value => (int?)value.VersionNumber)
                .MaxAsync(cancellationToken)) ?? 0;

    private async Task<ClosingPresentation> PresentationAsync(
        ArcRelayEntrant entrant,
        CancellationToken cancellationToken,
        string? sheetPlaybookJsonOverride = null)
    {
        string owner = await db.Users.Where(value =>
                value.Id == entrant.OwnerUserId)
            .Select(value => value.DisplayName)
            .SingleAsync(cancellationToken);
        IReadOnlyList<string> classes;
        if (entrant.Kind == ArcRelayEntrantKind.Sheet)
        {
            string ordersJson = sheetPlaybookJsonOverride ??
                await db.TacticalSheets.Where(value => value.Id == entrant.Id)
                    .Select(value => value.PlaybookJson)
                    .SingleAsync(cancellationToken);
            classes = TacticalSheetCompiler.ReadRoleClasses(ordersJson);
        }
        else
        {
            classes = ArcRelayComposition.Read(
                entrant.CompositionJson!).ClassIds;
        }
        return new ClosingPresentation(
            entrant.Name,
            owner,
            entrant.Kind == ArcRelayEntrantKind.Sheet ? "sheet" : "mind",
            entrant.CrestVariant,
            classes);
    }

    private sealed record ClosingPresentation(
        string Name,
        string OwnerDisplayName,
        string Kind,
        int CrestVariant,
        IReadOnlyList<string> ClassIds);
}
