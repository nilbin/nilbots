using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Sheets;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

/// <summary>One projection for cards, ladder rows and match presentation.</summary>
public sealed class ArcRelayEntrantProjector(
    AppDbContext db,
    ArcRelayClassCatalog classCatalog,
    TimeProvider timeProvider)
{
    public async Task<ArcRelayEntrantCardResponse> ProjectAsync(
        ArcRelayEntrant entrant,
        Guid? viewerId,
        Guid ladderId,
        CancellationToken cancellationToken) =>
        (await ProjectManyAsync(
            [entrant], viewerId, ladderId, cancellationToken))[0];

    /// <summary>
    /// Projects a standings or roster page in bounded queries. A Season can
    /// grow without turning one row into owner + rating + sheet/artifact +
    /// suspension round trips.
    /// </summary>
    public async Task<IReadOnlyList<ArcRelayEntrantCardResponse>> ProjectManyAsync(
        IReadOnlyList<ArcRelayEntrant> entrantRows,
        Guid? viewerId,
        Guid ladderId,
        CancellationToken cancellationToken)
    {
        if (entrantRows.Count == 0)
            return [];
        Guid[] entrantIds = entrantRows.Select(value => value.Id).ToArray();
        Guid[] ownerIds = entrantRows.Select(value => value.OwnerUserId)
            .Distinct().ToArray();
        Guid[] sheetIds = entrantRows
            .Where(value => value.Kind == ArcRelayEntrantKind.Sheet)
            .Select(value => value.Id).ToArray();
        Guid[] mindBotIds = entrantRows
            .Where(value => value.MindBotId is not null)
            .Select(value => value.MindBotId!.Value).Distinct().ToArray();
        Guid[] suspensionMatchIds = entrantRows
            .Where(value => value.SuspensionMatchId is not null)
            .Select(value => value.SuspensionMatchId!.Value).Distinct().ToArray();

        Dictionary<Guid, string> owners = await db.Users.AsNoTracking()
            .Where(value => ownerIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.DisplayName,
                cancellationToken);
        Dictionary<Guid, ArcRelayEntrantRating> ratings = await db
            .ArcRelayEntrantRatings.AsNoTracking()
            .Where(value => value.LadderId == ladderId &&
                entrantIds.Contains(value.EntrantId))
            .ToDictionaryAsync(value => value.EntrantId, cancellationToken);
        Dictionary<Guid, TacticalSheet> sheets = await db.TacticalSheets
            .AsNoTracking().Where(value => sheetIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        BotVersion[] versions = await db.BotVersions.AsNoTracking()
            .Where(value => mindBotIds.Contains(value.BotId))
            .OrderBy(value => value.BotId)
            .ThenByDescending(value => value.VersionNumber)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, BotVersion> latestVersions = versions
            .GroupBy(value => value.BotId)
            .ToDictionary(value => value.Key, value => value.First());
        Dictionary<Guid, Match> suspensionMatches = await db.Matches
            .AsNoTracking()
            .Where(value => suspensionMatchIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var result = new List<ArcRelayEntrantCardResponse>(entrantRows.Count);

        foreach (ArcRelayEntrant entrant in entrantRows)
        {
            string owner = owners[entrant.OwnerUserId];
            ratings.TryGetValue(entrant.Id, out ArcRelayEntrantRating? rating);

            // A felt-degeneracy bar removes the entrant from server-side pairing
            // immediately, but its public status follows the causal replay clock.
            bool suspensionDisclosed = entrant.SuspensionReason is not null &&
                entrant.SuspensionMatchId is Guid suspensionMatchId &&
                suspensionMatches.TryGetValue(suspensionMatchId,
                    out Match? suspensionMatch) &&
                suspensionMatch.BroadcastComplete(now);

            int revision;
            string contentHash;
            string? artifactHash = null;
            IReadOnlyList<string> classes;
            string status;
            if (entrant.Kind == ArcRelayEntrantKind.Sheet)
            {
                TacticalSheet sheet = sheets[entrant.Id];
                revision = sheet.Revision;
                contentHash = sheet.ContentHash;
                classes = TacticalSheetCompiler.ReadRoleClasses(
                    sheet.PlaybookJson);
                status = !TacticalSheetCompiler.IsAdmissibleToCurrentPlaylist(
                        sheet.LayoutJson)
                    ? "legacy"
                    : suspensionDisclosed ? "suspended" : "ready";
            }
            else
            {
                latestVersions.TryGetValue(entrant.MindBotId!.Value,
                    out BotVersion? version);
                revision = version?.VersionNumber ?? 0;
                artifactHash = version?.ArtifactHash;
                contentHash = entrant.CompositionHash ?? "";
                classes = ArcRelayComposition.Read(
                    entrant.CompositionJson!).ClassIds;
                status = version?.Status.ToString().ToLowerInvariant() ??
                    "pending";
                if (version?.Status == BuildStatus.Built)
                    status = entrant.PreflightStatus.ToString()
                        .ToLowerInvariant();
                if (suspensionDisclosed)
                    status = "suspended";
            }

            result.Add(new ArcRelayEntrantCardResponse(
                entrant.Id,
                entrant.Kind == ArcRelayEntrantKind.Sheet ? "sheet" : "mind",
                entrant.Name,
                owner,
                revision,
                ArcRelayCrestGenerator.Create(entrant.Id, entrant.CrestVariant),
                classes.Select((classId, index) =>
                    new ArcRelayCompositionSlotResponse(
                        index,
                        classId,
                        classCatalog.Get(classId).Name,
                        classId)).ToArray(),
                Math.Round(rating?.Rating ?? BotRating.DefaultRating),
                rating?.RankedMatches ?? 0,
                status != "legacy" &&
                    (entrant.LadderOptedIn ||
                        (entrant.SuspensionReason is not null &&
                            !suspensionDisclosed)),
                status,
                suspensionDisclosed ? entrant.SuspensionReason : null,
                suspensionDisclosed ? entrant.SuspensionMatchId : null,
                artifactHash,
                contentHash,
                viewerId == entrant.OwnerUserId));
        }

        return result;
    }

    public async Task<Guid> LadderIdAsync(CancellationToken cancellationToken) =>
        await (from ladder in db.Ladders.AsNoTracking()
               join version in db.PlaylistVersions.AsNoTracking() on ladder.PlaylistVersionId equals version.Id
               join playlist in db.Playlists.AsNoTracking() on version.PlaylistId equals playlist.Id
               join season in db.Seasons.AsNoTracking() on ladder.SeasonId equals season.Id
               where playlist.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey &&
                   version.Version == ArcRelayEntrantPlaylistDefinition.Version &&
                   season.Key == ArcRelayLadderPolicy.SeasonKey
               select ladder.Id).SingleAsync(cancellationToken);
}
