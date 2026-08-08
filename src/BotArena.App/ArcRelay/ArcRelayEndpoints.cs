using System.Net;
using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Sheets;
using BotArena.Engine;
using BotArena.Toolchain;
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
                    join playlist in db.Playlists.AsNoTracking() on version.PlaylistId equals playlist.Id
                    where playlist.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey &&
                        version.Version == ArcRelayEntrantPlaylistDefinition.Version
                    select version.Id).SingleOrDefaultAsync(cancellationToken);
            if (playlistVersionId == Guid.Empty)
                return Results.Problem("Arc Relay is not seeded on this deployment.", statusCode: 503);
            return Results.Ok(new ArcRelayCatalogResponse(
                ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                playlistVersionId,
                ArcRelayLoopProfile.Current.MapId,
                ArcRelayH0Definition.CreateMap(ArcRelayLoopProfile.Current).TileRows,
                ArcRelayComposition.SlotCount,
                ArcRelayComposition.MaximumCopiesPerClass,
                catalog.All.Select(value => new ArcRelayClassResponse(
                    value.Id, value.Name, value.SignatureName, value.Fantasy,
                    value.Starter, unlocked.Contains(value.Id))).ToArray()));
        }).Produces<ArcRelayCatalogResponse>();

        group.MapGet("/entrants", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayEntrantProjector projector,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            ArcRelayEntrant[] entrants = await db.ArcRelayEntrants.AsNoTracking()
                .Where(value => value.OwnerUserId == userId
                    && (value.Kind == ArcRelayEntrantKind.CustomMind
                        || db.TacticalSheets.Any(sheet => sheet.Id == value.Id)))
                .OrderByDescending(value => value.UpdatedAt).ThenBy(value => value.Id)
                .ToArrayAsync(cancellationToken);
            var response = new List<ArcRelayEntrantCardResponse>(entrants.Length);
            foreach (ArcRelayEntrant entrant in entrants)
                response.Add(await projector.ProjectAsync(entrant, userId, ladderId, cancellationToken));
            return Results.Ok(response);
        }).Produces<IReadOnlyList<ArcRelayEntrantCardResponse>>().RequireAuthorization();

        group.MapPost("/minds", async (
            CreateArcRelayMindRequest request,
            ClaimsPrincipal principal,
            HttpContext context,
            AppDbContext db,
            ArcRelayClassCatalog catalog,
            ArcRelayClassEntitlementService entitlements,
            CompilerSubmissionService submissions,
            ArcRelayEntrantProjector projector,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            string? name = NormalizeName(request.Name);
            if (name is null || request.CrestVariant is < 0 or > 4095)
                return Invalid("A mind needs a 1-60 character name and a valid crest variant.");
            ArcRelayCompositionCompilation composition;
            List<SourceFile> sources;
            try
            {
                composition = ArcRelayComposition.Compile(request.Composition, catalog,
                    await entitlements.UnlockedAsync(userId, cancellationToken));
                sources = ValidateSources(request.EntryType, request.Files);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            { return Invalid(exception.Message); }
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            Guid entrantId = Guid.NewGuid();
            var bot = new Bot
            {
                OwnerUserId = userId, Name = $"Arc Relay mind {entrantId:N}",
                Slug = $"relay-mind-{entrantId:N}", LookId = "internal-arc-relay-mind",
                ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
            };
            var entrant = new ArcRelayEntrant
            {
                Id = entrantId, OwnerUserId = userId, Kind = ArcRelayEntrantKind.CustomMind,
                Name = name, CrestVariant = request.CrestVariant, MindBotId = bot.Id,
                CompositionJson = composition.CanonicalJson, CompositionHash = composition.ContentHash,
                PreflightStatus = ArcRelayPreflightStatus.Required,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Bots.Add(bot);
            db.ArcRelayEntrants.Add(entrant);
            await db.SaveChangesAsync(cancellationToken);
            CompilerSubmissionDecision decision = await submissions.EnqueueAsync(
                bot.Id, userId, request.EntryType.Trim(), sources,
                context.Connection.RemoteIpAddress, cancellationToken);
            if (!decision.Accepted)
            {
                db.ArcRelayEntrants.Remove(entrant);
                db.Bots.Remove(bot);
                await db.SaveChangesAsync(cancellationToken);
                return Results.Problem(decision.Denial!.Message, statusCode: 429);
            }
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            return Results.Accepted($"/api/arc-relay/minds/{entrant.Id}", new ArcRelayMindResponse(
                await projector.ProjectAsync(entrant, userId, ladderId, cancellationToken),
                request.EntryType.Trim(), request.Files, request.Composition,
                entrant.CreatedAt, entrant.UpdatedAt, null));
        }).Produces<ArcRelayMindResponse>(202).RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Submission);

        group.MapPut("/minds/{entrantId:guid}", async (
            Guid entrantId,
            ReviseArcRelayMindRequest request,
            ClaimsPrincipal principal,
            HttpContext context,
            AppDbContext db,
            ArcRelayClassCatalog catalog,
            ArcRelayClassEntitlementService entitlements,
            CompilerSubmissionService submissions,
            ArcRelayEntrantProjector projector,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants.SingleOrDefaultAsync(value =>
                value.Id == entrantId && value.OwnerUserId == userId && value.Kind == ArcRelayEntrantKind.CustomMind,
                cancellationToken);
            if (entrant is null) return Results.NotFound();
            int revision = await db.BotVersions.Where(value => value.BotId == entrant.MindBotId)
                .Select(value => (int?)value.VersionNumber).MaxAsync(cancellationToken) ?? 0;
            if (revision != request.ExpectedRevision)
                return Results.Problem("The mind changed since it was opened. Reload before resubmitting.", statusCode: 409);
            string? name = NormalizeName(request.Name);
            if (name is null) return Invalid("Mind names need 1 to 60 visible characters.");
            ArcRelayCompositionCompilation composition;
            List<SourceFile> sources;
            try
            {
                composition = ArcRelayComposition.Compile(request.Composition, catalog,
                    await entitlements.UnlockedAsync(userId, cancellationToken));
                sources = ValidateSources(request.EntryType, request.Files);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            { return Invalid(exception.Message); }
            entrant.Name = name;
            entrant.CompositionJson = composition.CanonicalJson;
            entrant.CompositionHash = composition.ContentHash;
            entrant.PreflightStatus = ArcRelayPreflightStatus.Required;
            entrant.PreflightMatchId = null;
            entrant.PreflightFailure = null;
            entrant.LadderOptedIn = false;
            entrant.LadderOptedInAt = null;
            entrant.SuspensionReason = null;
            entrant.SuspensionMatchId = null;
            entrant.SuspendedAt = null;
            entrant.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            CompilerSubmissionDecision decision = await submissions.EnqueueAsync(
                entrant.MindBotId!.Value, userId, request.EntryType.Trim(), sources,
                context.Connection.RemoteIpAddress, cancellationToken);
            if (!decision.Accepted)
                return Results.Problem(decision.Denial!.Message, statusCode: 429);
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            return Results.Accepted($"/api/arc-relay/minds/{entrant.Id}", new ArcRelayMindResponse(
                await projector.ProjectAsync(entrant, userId, ladderId, cancellationToken),
                request.EntryType.Trim(), request.Files, request.Composition,
                entrant.CreatedAt, entrant.UpdatedAt, null));
        }).Produces<ArcRelayMindResponse>(202).RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Submission);

        group.MapGet("/minds/{entrantId:guid}", async (
            Guid entrantId, ClaimsPrincipal principal, AppDbContext db,
            ArcRelayEntrantProjector projector, CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants.AsNoTracking().SingleOrDefaultAsync(value =>
                value.Id == entrantId && value.OwnerUserId == userId && value.Kind == ArcRelayEntrantKind.CustomMind,
                cancellationToken);
            if (entrant is null) return Results.NotFound();
            BotVersion? version = await db.BotVersions.AsNoTracking().Where(value => value.BotId == entrant.MindBotId)
                .OrderByDescending(value => value.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            SourceFile[] sourceFiles = version is null
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<SourceFile[]>(version.SourcesJson) ?? [];
            return Results.Ok(new ArcRelayMindResponse(
                await projector.ProjectAsync(entrant, userId, ladderId, cancellationToken),
                version?.EntryType ?? "",
                sourceFiles.Select(value => new SourceFileDto(value.RelativePath, value.Content)).ToArray(),
                ArcRelayComposition.Read(entrant.CompositionJson!),
                entrant.CreatedAt, entrant.UpdatedAt, version?.BuildLog));
        }).Produces<ArcRelayMindResponse>().RequireAuthorization();

        group.MapGet("/entrants/{entrantId:guid}/crest-options", async (
            Guid entrantId, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants.AsNoTracking().SingleOrDefaultAsync(
                value => value.Id == entrantId && value.OwnerUserId == userId, cancellationToken);
            if (entrant is null) return Results.NotFound();
            int start = (entrant.CrestVariant + 1) % (ArcRelayCrestGenerator.MaximumVariant + 1);
            return Results.Ok(new ArcRelayCrestOptionsResponse(entrant.Id,
                Enumerable.Range(0, 8).Select(offset => ArcRelayCrestGenerator.Create(
                    entrant.Id, (start + offset) % (ArcRelayCrestGenerator.MaximumVariant + 1))).ToArray()));
        }).Produces<ArcRelayCrestOptionsResponse>().RequireAuthorization();

        group.MapPut("/entrants/{entrantId:guid}/crest", async (
            Guid entrantId, SetArcRelayCrestRequest request, ClaimsPrincipal principal,
            AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            if (request.Variant is < 0 or > 4095) return Invalid("Unknown crest variant.");
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants.SingleOrDefaultAsync(
                value => value.Id == entrantId && value.OwnerUserId == userId, cancellationToken);
            if (entrant is null) return Results.NotFound();
            entrant.CrestVariant = request.Variant;
            entrant.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ArcRelayCrestGenerator.Create(entrant.Id, entrant.CrestVariant));
        }).Produces<ArcRelayCrestDescriptor>().RequireAuthorization();

        group.MapPost("/entrants/{entrantId:guid}/preflight", async (
            Guid entrantId, ClaimsPrincipal principal, AppDbContext db,
            ArcRelayMatchAdmissionService admission, TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants.SingleOrDefaultAsync(value =>
                value.Id == entrantId && value.OwnerUserId == userId && value.Kind == ArcRelayEntrantKind.CustomMind,
                cancellationToken);
            if (entrant is null) return Results.NotFound();
            BotVersion? active = await db.BotVersions.SingleOrDefaultAsync(value =>
                value.BotId == entrant.MindBotId && value.IsActive && value.Status == BuildStatus.Built,
                cancellationToken);
            if (active is null) return Results.Problem("The custom mind must finish building before preflight.", statusCode: 409);
            Match match = await admission.CreatePreflightAsync(entrant, userId, null, cancellationToken);
            entrant.PreflightStatus = ArcRelayPreflightStatus.Pending;
            entrant.PreflightMatchId = match.Id;
            entrant.PreflightRevision = active.VersionNumber;
            entrant.PreflightFailure = null;
            entrant.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Accepted($"/api/matches/{match.Id}", new ArcRelayPreflightResponse(match.Id, "pending"));
        }).Produces<ArcRelayPreflightResponse>(202).RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Challenge);

        group.MapPut("/entrants/{entrantId:guid}/ladder", async (
            Guid entrantId, SetArcRelayLadderOptInRequest request, ClaimsPrincipal principal,
            AppDbContext db, ArcRelayEntrantProjector projector, TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.TakeAdmissionLockAsync(AdmissionLocks.ArcRelayLadder(userId), cancellationToken);
            ArcRelayEntrant? entrant = await db.ArcRelayEntrants.SingleOrDefaultAsync(
                value => value.Id == entrantId && value.OwnerUserId == userId, cancellationToken);
            if (entrant is null) return Results.NotFound();
            if (request.OptedIn)
            {
                if (entrant.SuspensionReason is not null)
                    return Results.Problem("This entrant is suspended until it is revised and passes admission again.", statusCode: 409);
                if (entrant.Kind == ArcRelayEntrantKind.CustomMind && entrant.PreflightStatus != ArcRelayPreflightStatus.Passed)
                    return Results.Problem("A custom mind must pass hosted preflight before entering the ladder.", statusCode: 409);
                int optedIn = await db.ArcRelayEntrants.CountAsync(value =>
                    value.OwnerUserId == userId && value.LadderOptedIn && value.Id != entrant.Id, cancellationToken);
                if (optedIn >= ArcRelayLadderPolicy.MaximumOptedInPerAccount)
                    return Results.Problem("An account may field at most three Arc Relay entrants at once.", statusCode: 409);
            }
            entrant.LadderOptedIn = request.OptedIn;
            entrant.LadderOptedInAt = request.OptedIn ? timeProvider.GetUtcNow().UtcDateTime : null;
            entrant.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            if (request.OptedIn && !await db.ArcRelayEntrantRatings.AnyAsync(
                    value => value.EntrantId == entrant.Id && value.LadderId == ladderId, cancellationToken))
                db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating { EntrantId = entrant.Id, LadderId = ladderId });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await projector.ProjectAsync(entrant, userId, ladderId, cancellationToken));
        }).Produces<ArcRelayEntrantCardResponse>().RequireAuthorization();

        group.MapGet("/ladder", async (
            ClaimsPrincipal principal, AppDbContext db, ArcRelayEntrantProjector projector,
            CancellationToken cancellationToken) =>
        {
            Guid ladderId = await projector.LadderIdAsync(cancellationToken);
            Guid? viewerId = principal.UserId();
            ArcRelayEntrant[] entrants = await (
                from entrant in db.ArcRelayEntrants.AsNoTracking()
                join rating in db.ArcRelayEntrantRatings.AsNoTracking() on entrant.Id equals rating.EntrantId
                where rating.LadderId == ladderId
                    && (entrant.Kind == ArcRelayEntrantKind.CustomMind
                        || db.TacticalSheets.Any(sheet => sheet.Id == entrant.Id))
                orderby rating.Rating descending, entrant.Id
                select entrant).ToArrayAsync(cancellationToken);
            var cards = new List<ArcRelayEntrantCardResponse>(entrants.Length);
            foreach (ArcRelayEntrant entrant in entrants)
                cards.Add(await projector.ProjectAsync(entrant, viewerId, ladderId, cancellationToken));
            return Results.Ok(new ArcRelayLadderResponse(
                ladderId, ArcRelayLadderPolicy.LadderName,
                ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                ArcRelayLadderPolicy.MaximumOptedInPerAccount,
                ArcRelayLadderPolicy.MaximumMatchesPerEntrantPerDay,
                cards));
        }).Produces<ArcRelayLadderResponse>();

        async Task<IResult> Scrimmage(
            CreateArcRelayScrimmageRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            ArcRelayMatchAdmissionService admission,
            UnrankedMatchLimits limits,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
        {
            if (principal.UserId() is not Guid userId) return Results.Unauthorized();
            if (request.EntrantId == request.OpponentEntrantId) return Invalid("Choose two distinct entrants.");
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.TakeAdmissionLockAsync(AdmissionLocks.Unranked(userId), cancellationToken);
            DateTime dayAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
            int started = await db.Matches.CountAsync(value => value.InitiatedByUserId == userId &&
                value.ArcRelayLane == ArcRelayMatchLane.Scrimmage && value.CreatedAt >= dayAgo, cancellationToken);
            if (started >= limits.AccountDailyLimit)
                return Results.Problem("The daily scrimmage limit has been reached.", statusCode: 429);
            ArcRelayEntrant[] entrants = await db.ArcRelayEntrants.Where(value =>
                value.OwnerUserId == userId
                && (value.Id == request.EntrantId || value.Id == request.OpponentEntrantId)
                && (value.Kind == ArcRelayEntrantKind.CustomMind
                    || db.TacticalSheets.Any(sheet => sheet.Id == value.Id)))
                .ToArrayAsync(cancellationToken);
            if (entrants.Length != 2) return Results.NotFound();
            Match match = await admission.CreateAsync(
                entrants.Single(value => value.Id == request.EntrantId),
                entrants.Single(value => value.Id == request.OpponentEntrantId),
                ArcRelayMatchLane.Scrimmage, userId, request.Seed, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new CreatedMatchResponse(match.Id));
        }
        group.MapPost("/scrimmages", Scrimmage).Produces<CreatedMatchResponse>()
            .RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Challenge);
    }

    private static List<SourceFile> ValidateSources(string? entryType, IReadOnlyList<SourceFileDto>? files)
    {
        string value = entryType?.Trim() ?? "";
        List<SourceFile> sources = files?.Select(file => new SourceFile(file.Name, file.Content)).ToList() ?? [];
        BotBuilder.ValidateSubmission(sources, value);
        if (sources.Count == 0) throw new ArgumentException("At least one source file is required.");
        if (sources.Any(source => !source.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Only .cs files are accepted.");
        return sources;
    }

    private static string? NormalizeName(string? value)
    {
        if (value is null) return null;
        string normalized = string.Join(' ', value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length is > 0 and <= 60 ? normalized : null;
    }

    private static IResult Invalid(string detail) => Results.Problem(
        detail, statusCode: 400, title: "Invalid Arc Relay entrant.");
}
