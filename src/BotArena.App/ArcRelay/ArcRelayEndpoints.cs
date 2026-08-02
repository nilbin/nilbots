using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

public static class ArcRelayEndpoints
{
    public static void MapArcRelay(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/arc-relay");

        group.MapGet("/catalog", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayClassCatalog catalog,
            ArcRelayClassEntitlementService entitlements,
            CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.UserId();
            IReadOnlySet<string> unlocked = userId is Guid accountId
                ? await entitlements.UnlockedAsync(accountId, cancellationToken)
                : catalog.StarterIds;
            Guid playlistVersionId = await (
                    from version in db.PlaylistVersions.AsNoTracking()
                    join playlist in db.Playlists.AsNoTracking()
                        on version.PlaylistId equals playlist.Id
                    where playlist.Key == ArcRelayPlaylistDefinition.PlaylistKey
                        && version.Version == ArcRelayPlaylistDefinition.Version
                    select version.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (playlistVersionId == Guid.Empty)
                return Results.Problem("Arc Relay is not seeded on this deployment.", statusCode: 503);
            return Results.Ok(new ArcRelayCatalogResponse(
                ArcRelayPlaylistDefinition.PlaylistKey,
                playlistVersionId,
                ArcRelayLoopProfile.HomeGatesWide.MapId,
                ArcRelayH0Definition.CreateMap(ArcRelayLoopProfile.HomeGatesWide)
                    .TileRows,
                ArcRelayPlayerSheetCodec.SlotCount,
                ArcRelayPlayerSheetCodec.MaximumCopiesPerClass,
                ArcRelayPlayerSheetCodec.SupportedTheaters,
                ArcRelayPlayerSheetCodec.SupportedRoles,
                ArcRelayPlayerSheetCodec.SupportedTriggers,
                catalog.All.Select(value => new ArcRelayClassResponse(
                    value.Id,
                    value.Name,
                    value.SignatureName,
                    value.Fantasy,
                    value.Starter,
                    unlocked.Contains(value.Id))).ToArray(),
                ArcRelayPlayerSheetCodec.NewSheetTemplate()));
        }).Produces<ArcRelayCatalogResponse>();

        group.MapGet("/sheets", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayPlayerSheetCodec codec,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            ArcRelaySheet[] sheets = await db.ArcRelaySheets.AsNoTracking()
                .Where(sheet => sheet.OwnerUserId == userId)
                .OrderByDescending(sheet => sheet.UpdatedAt)
                .ThenBy(sheet => sheet.Id)
                .ToArrayAsync(cancellationToken);
            return Results.Ok(sheets.Select(sheet => Response(sheet, codec)).ToArray());
        }).Produces<IReadOnlyList<ArcRelaySheetResponse>>()
            .RequireAuthorization();

        group.MapPost("/sheets", async (
            SaveArcRelaySheetRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayPlayerSheetCodec codec,
            ArcRelayClassEntitlementService entitlements,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            string? name = NormalizeName(request.Name);
            if (name is null)
                return Invalid("Sheet names need 1 to 60 visible characters.");
            if (request.ExpectedRevision is not null)
                return Invalid("expectedRevision is only used when updating a sheet.");
            IReadOnlySet<string> unlocked = await entitlements.UnlockedAsync(userId, cancellationToken);
            ArcRelaySheetCompilation compiled;
            try
            {
                compiled = codec.Compile(request.Document, unlocked, "new-sheet");
            }
            catch (InvalidDataException exception)
            {
                return Invalid(exception.Message);
            }
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            var sheet = new ArcRelaySheet
            {
                OwnerUserId = userId,
                Name = name,
                CanonicalJson = compiled.CanonicalJson,
                ContentHash = compiled.ContentHash,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.ArcRelaySheets.Add(sheet);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(Response(sheet, codec));
        }).Produces<ArcRelaySheetResponse>()
            .RequireAuthorization();

        group.MapPut("/sheets/{sheetId:guid}", async (
            Guid sheetId,
            SaveArcRelaySheetRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayPlayerSheetCodec codec,
            ArcRelayClassEntitlementService entitlements,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            ArcRelaySheet? sheet = await db.ArcRelaySheets.SingleOrDefaultAsync(
                value => value.Id == sheetId && value.OwnerUserId == userId,
                cancellationToken);
            if (sheet is null)
                return Results.NotFound();
            if (request.ExpectedRevision is not int revision || revision != sheet.Revision)
            {
                return Results.Problem(
                    "The sheet changed since it was opened. Reload before saving.",
                    statusCode: StatusCodes.Status409Conflict);
            }
            string? name = NormalizeName(request.Name);
            if (name is null)
                return Invalid("Sheet names need 1 to 60 visible characters.");
            IReadOnlySet<string> unlocked = await entitlements.UnlockedAsync(userId, cancellationToken);
            ArcRelaySheetCompilation compiled;
            try
            {
                compiled = codec.Compile(
                    request.Document,
                    unlocked,
                    $"{sheet.Id}:r{sheet.Revision + 1}");
            }
            catch (InvalidDataException exception)
            {
                return Invalid(exception.Message);
            }
            sheet.Name = name;
            sheet.Revision++;
            sheet.CanonicalJson = compiled.CanonicalJson;
            sheet.ContentHash = compiled.ContentHash;
            sheet.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
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
            return Results.Ok(Response(sheet, codec));
        }).Produces<ArcRelaySheetResponse>()
            .RequireAuthorization();

        group.MapPost("/matches", async (
            CreateArcRelayMatchRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayPlayerSheetCodec codec,
            ArcRelayClassEntitlementService entitlements,
            UnrankedMatchLimits limits,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            if (request.SheetId == request.OpponentSheetId)
                return Invalid("Choose two distinct saved sheets for a scrimmage.");

            await using var admissionScope = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.TakeAdmissionLockAsync(AdmissionLocks.Unranked(userId), cancellationToken);
            DateTime dayAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
            int startedToday = await db.Matches.CountAsync(
                match => match.InitiatedByUserId == userId
                    && match.MatchSetId == null
                    && match.CreatedAt >= dayAgo,
                cancellationToken);
            if (startedToday >= limits.AccountDailyLimit)
                return Results.Problem("The daily unranked match limit has been reached.", statusCode: 429);

            ArcRelaySheet[] sheets = await db.ArcRelaySheets.AsNoTracking()
                .Where(sheet => sheet.OwnerUserId == userId
                    && (sheet.Id == request.SheetId || sheet.Id == request.OpponentSheetId))
                .ToArrayAsync(cancellationToken);
            if (sheets.Length != 2)
                return Results.NotFound();
            ArcRelaySheet[] ordered =
            [
                sheets.Single(sheet => sheet.Id == request.SheetId),
                sheets.Single(sheet => sheet.Id == request.OpponentSheetId),
            ];
            IReadOnlySet<string> unlocked = await entitlements.UnlockedAsync(userId, cancellationToken);
            var compiled = new ArcRelaySheetCompilation[2];
            try
            {
                for (int index = 0; index < ordered.Length; index++)
                {
                    ArcRelaySheet sheet = ordered[index];
                    compiled[index] = codec.Compile(
                        codec.Read(sheet.CanonicalJson),
                        unlocked,
                        $"{sheet.Id}:r{sheet.Revision}");
                    if (!string.Equals(
                            compiled[index].ContentHash,
                            sheet.ContentHash,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("A saved sheet failed its content hash.");
                    }
                }
            }
            catch (InvalidDataException exception)
            {
                return Invalid(exception.Message);
            }

            ArcRelayPlaylistDefinition definition = ArcRelayPlaylistDefinition.Create();
            PlaylistVersion playlistVersion = await db.PlaylistVersions.SingleAsync(
                version => version.Version == ArcRelayPlaylistDefinition.Version
                    && db.Playlists.Any(playlist =>
                        playlist.Id == version.PlaylistId
                        && playlist.Key == ArcRelayPlaylistDefinition.PlaylistKey),
                cancellationToken);
            Bot stockBot = await db.Bots.AsNoTracking().SingleAsync(
                bot => bot.Slug == ArcRelayPlaylistSeeder.StockBotSlug,
                cancellationToken);
            BotVersion stockVersion = await db.BotVersions.AsNoTracking().SingleAsync(
                version => version.BotId == stockBot.Id
                    && version.IsActive
                    && version.ArtifactHash == ArcRelayPlaylistDefinition.StockArtifactHash,
                cancellationToken);
            string ownerName = await db.Users.Where(user => user.Id == userId)
                .Select(user => user.DisplayName)
                .SingleAsync(cancellationToken);
            ActorResolvedMatchDefinition matchDefinition = definition.ResolveMatch(
            [
                new HostedGenericParticipantInput(0, 0, compiled[0].Classes),
                new HostedGenericParticipantInput(1, 1, compiled[1].Classes),
            ]);
            var match = new Match
            {
                MapId = matchDefinition.Map.Id,
                MapVersion = matchDefinition.Map.Version,
                Seed = request.Seed ?? Random.Shared.NextInt64(),
                InitiatedByUserId = userId,
                GameRulesVersion = matchDefinition.Rules.RulesetId,
                RuntimeConfigurationVersion = matchDefinition.CapabilityVersions.RuntimeConfigurationVersion,
                PlaylistVersionId = playlistVersion.Id,
            };
            for (int index = 0; index < ordered.Length; index++)
            {
                ArcRelaySheet sheet = ordered[index];
                match.Participants.Add(new MatchParticipant
                {
                    MatchId = match.Id,
                    Slot = index,
                    TeamId = index,
                    BotId = stockBot.Id,
                    BotVersionId = stockVersion.Id,
                    NameSnapshot = sheet.Name,
                    OwnerDisplayNameSnapshot = ownerName,
                    AccentSnapshot = index == 0 ? "#22d3ee" : "#fb5360",
                    LookIdSnapshot = "arc-relay-sheet",
                    ProjectileLookIdSnapshot = ArcRelayH0ReplayPresentation.ProjectileLookId,
                    ArtifactHashSnapshot = ArcRelayPlaylistDefinition.StockArtifactHash,
                    SheetIdSnapshot = sheet.Id,
                    SheetRevisionSnapshot = sheet.Revision,
                    SheetNameSnapshot = sheet.Name,
                    SheetHashSnapshot = sheet.ContentHash,
                    SheetCanonicalJsonSnapshot = sheet.CanonicalJson,
                    MindDataSnapshot = compiled[index].LinkedData,
                });
            }
            db.Matches.Add(match);
            db.BackgroundJobs.Add(BackgroundJob.ExecuteGenericActorMatch(
                match.Id,
                ArcRelayPlaylistDefinition.PlaylistKey,
                ArcRelayPlaylistDefinition.Version));
            await db.SaveChangesAsync(cancellationToken);
            await admissionScope.CommitAsync(cancellationToken);
            return Results.Ok(new CreatedMatchResponse(match.Id));
        }).Produces<CreatedMatchResponse>()
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Challenge);
    }

    private static ArcRelaySheetResponse Response(
        ArcRelaySheet sheet,
        ArcRelayPlayerSheetCodec codec) =>
        new(
            sheet.Id,
            sheet.Name,
            sheet.Revision,
            sheet.ContentHash,
            sheet.CreatedAt,
            sheet.UpdatedAt,
            codec.Read(sheet.CanonicalJson));

    private static string? NormalizeName(string? value)
    {
        if (value is null)
            return null;
        string normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length is > 0 and <= 60 ? normalized : null;
    }

    private static IResult Invalid(string detail) => Results.Problem(
        detail,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid Arc Relay sheet.");
}
