using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

/// <summary>One projection for cards, ladder rows and match presentation.</summary>
public sealed class ArcRelayEntrantProjector(
    AppDbContext db,
    ArcRelayPlayerSheetCodec sheetCodec,
    ArcRelayClassCatalog classCatalog,
    TimeProvider timeProvider)
{
    public async Task<ArcRelayEntrantCardResponse> ProjectAsync(
        ArcRelayEntrant entrant,
        Guid? viewerId,
        Guid ladderId,
        CancellationToken cancellationToken)
    {
        string owner = await db.Users.AsNoTracking()
            .Where(value => value.Id == entrant.OwnerUserId)
            .Select(value => value.DisplayName)
            .SingleAsync(cancellationToken);
        ArcRelayEntrantRating? rating = await db.ArcRelayEntrantRatings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EntrantId == entrant.Id && value.LadderId == ladderId, cancellationToken);

        // A felt-degeneracy bar removes the entrant from server-side pairing immediately,
        // but must not leak a just-finished match through the public ladder before its
        // causal broadcast has caught up. Keep the prior public state until reveal.
        bool suspensionDisclosed = false;
        if (entrant.SuspensionReason is not null && entrant.SuspensionMatchId is Guid suspensionMatchId)
        {
            Match? suspensionMatch = await db.Matches.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == suspensionMatchId, cancellationToken);
            suspensionDisclosed = suspensionMatch?.BroadcastComplete(timeProvider.GetUtcNow().UtcDateTime) == true;
        }

        int revision;
        string contentHash;
        string? artifactHash = null;
        IReadOnlyList<string> classes;
        string status;
        if (entrant.Kind == ArcRelayEntrantKind.Sheet)
        {
            ArcRelaySheet sheet = await db.ArcRelaySheets.AsNoTracking()
                .SingleAsync(value => value.Id == entrant.Id, cancellationToken);
            revision = sheet.Revision;
            contentHash = sheet.ContentHash;
            classes = sheetCodec.Read(sheet.CanonicalJson).Slots
                .OrderBy(value => value.UnitId).Select(value => value.ClassId).ToArray();
            status = suspensionDisclosed ? "suspended" : "ready";
        }
        else
        {
            BotVersion? version = await db.BotVersions.AsNoTracking()
                .Where(value => value.BotId == entrant.MindBotId)
                .OrderByDescending(value => value.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            revision = version?.VersionNumber ?? 0;
            artifactHash = version?.ArtifactHash;
            contentHash = entrant.CompositionHash ?? "";
            classes = ArcRelayComposition.Read(entrant.CompositionJson!).ClassIds;
            status = version?.Status.ToString().ToLowerInvariant() ?? "pending";
            if (version?.Status == BuildStatus.Built)
                status = entrant.PreflightStatus.ToString().ToLowerInvariant();
            if (suspensionDisclosed)
                status = "suspended";
        }

        return new ArcRelayEntrantCardResponse(
            entrant.Id,
            entrant.Kind == ArcRelayEntrantKind.Sheet ? "sheet" : "mind",
            entrant.Name,
            owner,
            revision,
            ArcRelayCrestGenerator.Create(entrant.Id, entrant.CrestVariant),
            classes.Select((classId, index) => new ArcRelayCompositionSlotResponse(
                index,
                classId,
                classCatalog.Get(classId).Name,
                classId)).ToArray(),
            Math.Round(rating?.Rating ?? BotRating.DefaultRating),
            rating?.RankedMatches ?? 0,
            entrant.LadderOptedIn || (entrant.SuspensionReason is not null && !suspensionDisclosed),
            status,
            suspensionDisclosed ? entrant.SuspensionReason : null,
            suspensionDisclosed ? entrant.SuspensionMatchId : null,
            artifactHash,
            contentHash,
            viewerId == entrant.OwnerUserId);
    }

    public async Task<Guid> LadderIdAsync(CancellationToken cancellationToken) =>
        await (from ladder in db.Ladders.AsNoTracking()
               join version in db.PlaylistVersions.AsNoTracking() on ladder.PlaylistVersionId equals version.Id
               join playlist in db.Playlists.AsNoTracking() on version.PlaylistId equals playlist.Id
               where playlist.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey &&
                   version.Version == ArcRelayEntrantPlaylistDefinition.Version &&
                   ladder.Status == LadderStatus.Open
               select ladder.Id).SingleAsync(cancellationToken);
}
