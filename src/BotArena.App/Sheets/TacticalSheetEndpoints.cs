using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.ArcRelay;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Sheets;

public static class TacticalSheetEndpoints
{
    public static void MapSheets(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/sheets")
            .RequireAuthorization();

        group.MapGet("/catalog", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayClassCatalog classes,
            ArcRelayClassEntitlementService entitlements,
            TacticalSheetTemplateCatalog templates,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            Guid playlistVersionId = await (
                    from version in db.PlaylistVersions.AsNoTracking()
                    join playlist in db.Playlists.AsNoTracking()
                        on version.PlaylistId equals playlist.Id
                    where playlist.Key ==
                            ArcRelayEntrantPlaylistDefinition.PlaylistKey
                        && version.Version ==
                            ArcRelayEntrantPlaylistDefinition.Version
                    select version.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (playlistVersionId == Guid.Empty)
            {
                return Results.Problem(
                    "Arc Relay is not seeded on this deployment.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            IReadOnlySet<string> unlocked = await entitlements.UnlockedAsync(
                userId, cancellationToken);
            ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
                ArcRelayLoopProfile.Current);
            return Results.Ok(new TacticalSheetCatalogResponse(
                ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                playlistVersionId,
                Map(map),
                ArcRelayComposition.SlotCount,
                ArcRelayComposition.MaximumCopiesPerClass,
                classes.All.Select(value => new TacticalSheetClassResponse(
                    value.Id,
                    value.Name,
                    value.SignatureName,
                    value.Fantasy,
                    value.Starter,
                    unlocked.Contains(value.Id))).ToArray(),
                templates.Template.PlaybookJson,
                templates.Template.LayoutJson,
                templates.Stock.Select(value => new TacticalStockSheetResponse(
                    value.Id,
                    value.Name,
                    value.Description,
                    value.Composition)).ToArray()));
        }).Produces<TacticalSheetCatalogResponse>();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayEntrantProjector projector,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            TacticalSheet[] sheets = await db.TacticalSheets.AsNoTracking()
                .Where(value => value.OwnerUserId == userId)
                .OrderByDescending(value => value.UpdatedAt)
                .ThenBy(value => value.Id)
                .ToArrayAsync(cancellationToken);
            var result = new List<TacticalSheetSummaryResponse>(sheets.Length);
            foreach (TacticalSheet sheet in sheets)
            {
                ArcRelayEntrant entrant = await db.ArcRelayEntrants.AsNoTracking()
                    .SingleAsync(value => value.Id == sheet.Id, cancellationToken);
                result.Add(new TacticalSheetSummaryResponse(
                    sheet.Id,
                    sheet.Name,
                    sheet.Revision,
                    sheet.ContentHash,
                    sheet.CreatedAt,
                    sheet.UpdatedAt,
                    await projector.ProjectAsync(
                        entrant, userId, ladderId, cancellationToken)));
            }
            return Results.Ok(result);
        }).Produces<IReadOnlyList<TacticalSheetSummaryResponse>>();

        group.MapGet("/{sheetId:guid}", async (
            Guid sheetId,
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayEntrantProjector projector,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            TacticalSheet? sheet = await db.TacticalSheets.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == sheetId
                    && value.OwnerUserId == userId, cancellationToken);
            if (sheet is null)
                return Results.NotFound();
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.AsNoTracking()
                .SingleAsync(value => value.Id == sheet.Id, cancellationToken);
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            return Results.Ok(await Response(
                sheet, entrant, userId, ladderId, projector, cancellationToken));
        }).Produces<TacticalSheetResponse>();

        group.MapPost("/", async (
            SaveTacticalSheetRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            TacticalSheetCompiler compiler,
            ArcRelayClassEntitlementService entitlements,
            ArcRelayEntrantProjector projector,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            string? name = NormalizeName(request.Name);
            if (name is null || request.ExpectedRevision is not null)
                return Invalid(
                    "A new sheet needs a 1-60 character name and no expectedRevision.");
            ValidatedTacticalSheet compiled;
            try
            {
                compiled = compiler.Compile(
                    request.PlaybookJson,
                    request.LayoutJson,
                    await entitlements.UnlockedAsync(userId, cancellationToken),
                    "new-sheet");
            }
            catch (Exception exception) when (IsAuthorError(exception))
            {
                return Invalid(exception.Message);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken);
            await db.Database.TakeAdmissionLockAsync(
                AdmissionLocks.ArcRelayLadder(userId), cancellationToken);
            bool enterLadder = request.EnterLadder ?? true;
            if (enterLadder && await ActiveEntrantCount(
                    db, userId, null, cancellationToken)
                >= ArcRelayLadderPolicy.MaximumOptedInPerAccount)
            {
                return Results.Problem(
                    "An account may field at most three Arc Relay entrants at once. Save with ladder entry disabled or retire another entrant.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            Guid id = Guid.NewGuid();
            var sheet = new TacticalSheet
            {
                Id = id,
                OwnerUserId = userId,
                Name = name,
                PlaybookJson = request.PlaybookJson,
                LayoutJson = request.LayoutJson,
                ContentHash = compiled.ContentHash,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var entrant = new ArcRelayEntrant
            {
                Id = id,
                OwnerUserId = userId,
                Kind = ArcRelayEntrantKind.Sheet,
                Name = name,
                CrestVariant = 0,
                PreflightStatus = ArcRelayPreflightStatus.NotRequired,
                LadderOptedIn = enterLadder,
                LadderOptedInAt = enterLadder ? now : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.TacticalSheets.Add(sheet);
            db.ArcRelayEntrants.Add(entrant);
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            if (enterLadder)
            {
                db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
                {
                    EntrantId = id,
                    LadderId = ladderId,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await Response(
                sheet, entrant, userId, ladderId, projector, cancellationToken));
        }).Produces<TacticalSheetResponse>();

        group.MapPut("/{sheetId:guid}", async (
            Guid sheetId,
            SaveTacticalSheetRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            TacticalSheetCompiler compiler,
            ArcRelayClassEntitlementService entitlements,
            ArcRelayEntrantProjector projector,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken);
            await db.Database.TakeAdmissionLockAsync(
                AdmissionLocks.ArcRelayLadder(userId), cancellationToken);
            TacticalSheet? sheet = await db.TacticalSheets.SingleOrDefaultAsync(
                value => value.Id == sheetId && value.OwnerUserId == userId,
                cancellationToken);
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants
                .SingleOrDefaultAsync(value => value.Id == sheetId
                    && value.OwnerUserId == userId, cancellationToken);
            if (sheet is null || entrant is null)
                return Results.NotFound();
            if (request.ExpectedRevision != sheet.Revision)
            {
                return Results.Problem(
                    "The sheet changed since it was opened. Reload before saving.",
                    statusCode: StatusCodes.Status409Conflict);
            }
            string? name = NormalizeName(request.Name);
            if (name is null)
                return Invalid("Sheet names need 1 to 60 visible characters.");
            ValidatedTacticalSheet compiled;
            try
            {
                compiled = compiler.Compile(
                    request.PlaybookJson,
                    request.LayoutJson,
                    await entitlements.UnlockedAsync(userId, cancellationToken),
                    $"{sheet.Id}:r{sheet.Revision + 1}");
            }
            catch (Exception exception) when (IsAuthorError(exception))
            {
                return Invalid(exception.Message);
            }
            bool enterLadder = request.EnterLadder ?? true;
            if (enterLadder && !entrant.LadderOptedIn
                && await ActiveEntrantCount(
                    db, userId, entrant.Id, cancellationToken)
                    >= ArcRelayLadderPolicy.MaximumOptedInPerAccount)
            {
                return Results.Problem(
                    "An account may field at most three Arc Relay entrants at once. Save with ladder entry disabled or retire another entrant.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            sheet.Name = entrant.Name = name;
            sheet.Revision++;
            sheet.PlaybookJson = request.PlaybookJson;
            sheet.LayoutJson = request.LayoutJson;
            sheet.ContentHash = compiled.ContentHash;
            sheet.UpdatedAt = entrant.UpdatedAt = now;
            entrant.LadderOptedIn = enterLadder;
            entrant.LadderOptedInAt = enterLadder ? now : null;
            entrant.SuspensionReason = null;
            entrant.SuspensionMatchId = null;
            entrant.SuspendedAt = null;
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            if (enterLadder && !await db.ArcRelayEntrantRatings.AnyAsync(
                    value => value.EntrantId == entrant.Id
                        && value.LadderId == ladderId, cancellationToken))
            {
                db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
                {
                    EntrantId = entrant.Id,
                    LadderId = ladderId,
                });
            }
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(
                    "The sheet changed since it was opened. Reload before saving.",
                    statusCode: StatusCodes.Status409Conflict);
            }
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await Response(
                sheet, entrant, userId, ladderId, projector, cancellationToken));
        }).Produces<TacticalSheetResponse>();

        group.MapDelete("/{sheetId:guid}", async (
            Guid sheetId,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            TacticalSheet? sheet = await db.TacticalSheets.SingleOrDefaultAsync(
                value => value.Id == sheetId && value.OwnerUserId == userId,
                cancellationToken);
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants
                .SingleOrDefaultAsync(value => value.Id == sheetId
                    && value.OwnerUserId == userId, cancellationToken);
            if (sheet is null || entrant is null)
                return Results.NotFound();
            if (await db.ArcRelayRankedMatches.AnyAsync(value =>
                    value.EntrantAId == entrant.Id
                    || value.EntrantBId == entrant.Id, cancellationToken))
            {
                return Results.Problem(
                    "A sheet with ranked history cannot be deleted; leave the ladder instead.",
                    statusCode: StatusCodes.Status409Conflict);
            }
            db.TacticalSheets.Remove(sheet);
            db.ArcRelayEntrants.Remove(entrant);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TacticalSheetDeletedResponse(sheetId));
        }).Produces<TacticalSheetDeletedResponse>();

        group.MapPost("/{sheetId:guid}/trial", async (
            Guid sheetId,
            TrialTacticalSheetRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayMatchAdmissionService admission,
            UnrankedMatchLimits limits,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken);
            await db.Database.TakeAdmissionLockAsync(
                AdmissionLocks.Unranked(userId), cancellationToken);
            DateTime dayAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
            int started = await db.Matches.CountAsync(value =>
                value.InitiatedByUserId == userId
                && value.ArcRelayLane == ArcRelayMatchLane.Scrimmage
                && value.CreatedAt >= dayAgo, cancellationToken);
            if (started >= limits.AccountDailyLimit)
            {
                return Results.Problem(
                    "The daily scrimmage limit has been reached.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants
                .SingleOrDefaultAsync(value => value.Id == sheetId
                    && value.OwnerUserId == userId
                    && value.Kind == ArcRelayEntrantKind.Sheet,
                    cancellationToken);
            if (entrant is null
                || !await db.TacticalSheets.AnyAsync(
                    value => value.Id == sheetId, cancellationToken))
            {
                return Results.NotFound();
            }
            Match match;
            try
            {
                match = await admission.CreateTrialAsync(
                    entrant,
                    request.StockSheetId,
                    userId,
                    request.Seed,
                    cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                return Invalid(exception.Message);
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new CreatedMatchResponse(match.Id));
        }).Produces<CreatedMatchResponse>()
            .RequireRateLimiting(RateLimitPolicies.Challenge);
    }

    private static async Task<TacticalSheetResponse> Response(
        TacticalSheet sheet,
        ArcRelayEntrant entrant,
        Guid userId,
        Guid ladderId,
        ArcRelayEntrantProjector projector,
        CancellationToken cancellationToken) =>
        new(
            sheet.Id,
            sheet.Name,
            sheet.Revision,
            sheet.ContentHash,
            sheet.CreatedAt,
            sheet.UpdatedAt,
            sheet.PlaybookJson,
            sheet.LayoutJson,
            await projector.ProjectAsync(
                entrant, userId, ladderId, cancellationToken));

    private static Task<int> ActiveEntrantCount(
        AppDbContext db,
        Guid userId,
        Guid? excluding,
        CancellationToken cancellationToken) =>
        db.ArcRelayEntrants.CountAsync(value => value.OwnerUserId == userId
            && value.LadderOptedIn
            && value.Id != excluding, cancellationToken);

    private static TacticalSheetMapResponse Map(ActorMapDefinition map) =>
        new(
            map.Id,
            map.Version,
            map.FormatVersion,
            map.Width,
            map.Height,
            map.TileRows,
            map.Regions.Select(value => new TacticalSheetMapRegionResponse(
                value.RegionId,
                value.Kind.ToString(),
                value.Tiles.Select(Point).ToArray())).ToArray(),
            map.SpawnAnchors.Select(value => new TacticalSheetMapSpawnResponse(
                value.Spawn.SpawnId,
                Point(value.Spawn.Position),
                value.Spawn.Facing.ToString())).ToArray(),
            map.TileTags.Select(value => new TacticalSheetMapTagResponse(
                value.TagId,
                value.Kind.ToString(),
                value.Tiles.Select(Point).ToArray())).ToArray());

    private static TacticalSheetPointResponse Point(Position value) =>
        new(value.X, value.Y);

    private static string? NormalizeName(string? value)
    {
        if (value is null)
            return null;
        string normalized = string.Join(' ', value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries));
        return normalized.Length is > 0 and <= 60 ? normalized : null;
    }

    private static bool IsAuthorError(Exception exception) =>
        exception is InvalidDataException
            or System.Text.Json.JsonException
            or ArgumentException;

    private static IResult Invalid(string detail) => Results.Problem(
        detail,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid tactical sheet.");
}
