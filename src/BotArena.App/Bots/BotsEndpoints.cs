using System.Security.Claims;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record CreateBotRequest(
    string Name,
    string? Accent,
    string? LookId = null,
    string? ProjectileLookId = null);
public sealed record SubmitVersionRequest(
    string EntryType,
    List<SourceFileDto> Files,
    string? LookId = null,
    string? ProjectileLookId = null);
public sealed record UpdateBotAppearanceRequest(
    string Accent,
    string LookId,
    string ProjectileLookId);
public sealed record SourceFileDto(string Name, string Content);

public static class BotsEndpoints
{
    public static void MapBots(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/bots");

        group.MapGet("/", async (AppDbContext db, MatchExecutionSettings matchSettings) =>
        {
            // Order before projecting: EF sees through an anonymous type's members but
            // not a record constructor's, so ordering on the projection no longer
            // translates to SQL.
            var bots = await db.Bots
                .OrderBy(b => b.CreatedAt)
                .Select(b => new BotSummaryResponse(
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.Accent,
                    b.LookId,
                    b.ProjectileLookId,
                    b.CreatedAt,
                    // One rating per rules-version ladder (DECISIONS #54), newest first.
                    b.Ratings
                        .OrderByDescending(r => r.RulesVersion)
                        .Select(r => new BotLadderRatingResponse(
                            r.RulesVersion, Math.Round(r.Rating), r.RankedSets))
                        .ToList(),
                    db.Users.Where(u => u.Id == b.OwnerUserId).Select(u => u.DisplayName).First(),
                    b.Versions
                        .Where(v => v.IsActive && v.Status == BuildStatus.Built)
                        .Select(v => new BotActiveVersionResponse(
                            v.Id, v.VersionNumber, v.ArtifactHash))
                        .FirstOrDefault(),
                    b.Versions.Count(v => v.Status == BuildStatus.Built)))
                .ToListAsync();

            // Rank depends on the whole ladder, so it is resolved once here rather than
            // left to each client to join against /api/leaderboard — which would also cap
            // rank at whatever slice that endpoint returns.
            var standings = await db.BotRatings.ForRulesAsync(matchSettings.MatchRules.RulesVersion);
            return Results.Ok(bots
                .Select(bot => standings.TryGetValue(bot.Id, out LadderStanding? standing)
                    ? bot with { CurrentStanding = standing }
                    : bot)
                .ToList());
        }).Produces<IReadOnlyList<BotSummaryResponse>>();

        group.MapPost("/", async (
            CreateBotRequest request,
            ClaimsPrincipal principal,
            ApplicationActorFactory actorFactory,
            CreateBotUseCase useCase,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            ApplicationActor actor = await actorFactory.ResolveAsync(
                principal,
                cancellationToken);
            ApplicationResult<CreatedBot> result = await useCase.ExecuteAsync(
                actor,
                new CreateBotCommand(
                    request.Name,
                    request.Accent,
                    request.LookId,
                    request.ProjectileLookId),
                cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : result.Error!.ToProblemDetails(http);
        }).Produces<CreatedBot>().RequireAuthorization();

        // Keyed by slug OR id. Every bot has a unique, immutable slug, so the public
        // URL of a bot can read `/bots/murder-roomba` instead of a raw GUID; the id form
        // keeps working for anything already holding one.
        group.MapGet("/{key}", async (
            string key,
            ClaimsPrincipal principal,
            AppDbContext db,
            MatchExecutionSettings matchSettings) =>
        {
            var bot = Guid.TryParse(key, out var botId)
                ? await db.Bots.Include(b => b.Versions).SingleOrDefaultAsync(b => b.Id == botId)
                : await db.Bots.Include(b => b.Versions).SingleOrDefaultAsync(b => b.Slug == key);
            if (bot is null)
                return Results.NotFound();
            bool isOwner = principal.UserId() == bot.OwnerUserId;
            string owner = await db.Users.Where(u => u.Id == bot.OwnerUserId)
                .Select(u => u.DisplayName).FirstAsync();
            string currentRulesVersion = matchSettings.MatchRules.RulesVersion;
            var currentStanding = await db.BotRatings
                .ForBotAsync(currentRulesVersion, bot.Id);
            return Results.Ok(new BotDetailResponse(
                bot.Id,
                bot.Name,
                bot.Slug,
                bot.Accent,
                bot.LookId,
                bot.ProjectileLookId,
                bot.CreatedAt,
                owner,
                isOwner,
                currentStanding,
                bot.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => new BotVersionResponse(
                        v.Id,
                        v.VersionNumber,
                        v.Status.ToString(),
                        v.ArtifactHash,
                        v.IsActive,
                        v.CreatedAt,
                        DeserializeReceipt(v.BuildReceiptJson),
                        // Build logs and sources are owner-only (plan §13.3, §14).
                        isOwner ? v.BuildLog : null,
                        isOwner ? v.EntryType : null,
                        isOwner
                            ? JsonSerializer.Deserialize<List<SourceFile>>(v.SourcesJson)
                            : null))
                    .ToList()));
        }).Produces<BotDetailResponse>();

        // Appearance is mutable independently of source versions. This endpoint is
        // also the future entitlement-enforcement boundary: ownership is checked when
        // equipping, while historical match snapshots remain immutable.
        group.MapPut(
            "/{botId:guid}/appearance",
            async (
                Guid botId,
                UpdateBotAppearanceRequest request,
                ClaimsPrincipal principal,
                ApplicationActorFactory actorFactory,
                UpdateBotAppearanceUseCase useCase,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                ApplicationActor actor = await actorFactory.ResolveAsync(
                    principal,
                    cancellationToken);
                ApplicationResult<UpdatedBotAppearance> result = await useCase.ExecuteAsync(
                    actor,
                    new UpdateBotAppearanceCommand(
                        botId,
                        request.Accent,
                        request.LookId,
                        request.ProjectileLookId),
                    cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : result.Error!.ToProblemDetails(http);
            }).Produces<UpdatedBotAppearance>()
            .RequireAuthorization();

        // Slim polling view (gen-2 finding #8): build-status pollers shouldn't re-download
        // every version's sources and log on each poll.
        group.MapGet("/{botId:guid}/build-status", async (Guid botId, AppDbContext db) =>
        {
            var versions = await db.BotVersions
                .Where(v => v.BotId == botId)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new BotBuildStatusResponse(
                    v.Id, v.VersionNumber, v.Status.ToString(), v.ArtifactHash,
                    v.IsActive, v.CreatedAt, v.BuiltAt))
                .ToListAsync();
            return versions.Count == 0 && !await db.Bots.AnyAsync(b => b.Id == botId)
                ? Results.NotFound()
                : Results.Ok(versions);
        }).Produces<IReadOnlyList<BotBuildStatusResponse>>();

        group.MapGet(
            "/{botId:guid}/versions/{versionId:guid}/artifact",
            async (
                Guid botId,
                Guid versionId,
                AppDbContext db,
                IObjectStore objectStore,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var artifact = await db.BotVersions
                    .Where(v => v.Id == versionId && v.BotId == botId &&
                                v.Status == BuildStatus.Built &&
                                v.ArtifactKey != null && v.ArtifactHash != null)
                    .Join(
                        db.Bots,
                        version => version.BotId,
                        bot => bot.Id,
                        (version, bot) => new
                        {
                            version.ArtifactKey,
                            version.ArtifactHash,
                            version.VersionNumber,
                            bot.Slug,
                        })
                    .SingleOrDefaultAsync(cancellationToken);
                if (artifact is null)
                    return Results.NotFound();

                Stream? stream = await objectStore.OpenReadAsync(
                    artifact.ArtifactKey!,
                    cancellationToken);
                if (stream is null)
                    return Results.NotFound();

                http.Response.Headers.ETag = $"\"sha256-{artifact.ArtifactHash}\"";
                http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                http.Response.Headers["X-Content-SHA256"] = artifact.ArtifactHash;
                return Results.Stream(
                    stream,
                    "application/wasm",
                    $"{artifact.Slug}-v{artifact.VersionNumber}.wasm",
                    enableRangeProcessing: true);
            });

        group.MapPost("/{botId:guid}/versions",
            async (
                Guid botId,
                SubmitVersionRequest request,
                ClaimsPrincipal principal,
                ApplicationActorFactory actorFactory,
                SubmitBotVersionUseCase useCase,
                HttpContext http,
                CancellationToken cancellationToken) =>
        {
            // Null-tolerant: absent name/content must 400 via validation below, not 500 here.
            var sources = (request.Files ?? [])
                .Select(f => new SourceFile((f.Name ?? "").Trim(), f.Content ?? ""))
                .ToArray();
            ApplicationActor actor = await actorFactory.ResolveAsync(
                principal,
                cancellationToken);
            ApplicationResult<SubmittedBotVersion> result = await useCase.ExecuteAsync(
                actor,
                new SubmitBotVersionCommand(
                    botId,
                    request.EntryType,
                    sources,
                    request.LookId,
                    request.ProjectileLookId,
                    http.Connection.RemoteIpAddress),
                cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : result.Error!.ToProblemDetails(http);
        }).Produces<SubmittedBotVersion>().RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Submission);

        group.MapGet("/{botId:guid}/matches", async (
            Guid botId,
            AppDbContext db,
            TimeProvider timeProvider) =>
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            var matches = await db.Matches
                .Include(m => m.Participants)
                .Where(m => m.Participants.Any(p => p.BotId == botId))
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .ToListAsync();
            BotMatchHistoryRowResponse[] rows = matches
                .Select(match => MatchPublicProjection.ToBotHistory(
                    match,
                    botId,
                    now))
                .ToArray();
            return Results.Ok(new BotMatchHistoryResponse(
                rows.Count(row => row.Outcome == "Win"),
                rows.Count(row => row.Outcome == "Loss"),
                rows.Count(row => row.Outcome == "Draw"),
                rows));
        }).Produces<BotMatchHistoryResponse>();

        group.MapGet(
            "/{botId:guid}/stats",
            async (
                Guid botId,
                BotStatisticsQuery query,
                CancellationToken cancellationToken) =>
            {
                BotStatistics? statistics = await query.ExecuteAsync(
                    botId,
                    cancellationToken);
                return statistics is null
                    ? Results.NotFound()
                    : Results.Ok(statistics);
            }).Produces<BotStatistics>();

        group.MapGet("/mine", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            var bots = await db.Bots
                .Where(b => b.OwnerUserId == userId)
                .OrderBy(b => b.Name)
                .Select(b => new MyBotResponse(
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.Accent,
                    b.LookId,
                    b.ProjectileLookId,
                    b.Versions.OrderByDescending(v => v.VersionNumber)
                        .Select(v => new MyBotVersionResponse(
                            v.VersionNumber, v.Status.ToString(), v.IsActive))
                        .FirstOrDefault()))
                .ToListAsync();
            return Results.Ok(bots);
        }).Produces<IReadOnlyList<MyBotResponse>>().RequireAuthorization();
    }

    private static BuildReceipt? DeserializeReceipt(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<BuildReceipt>(json);
}
