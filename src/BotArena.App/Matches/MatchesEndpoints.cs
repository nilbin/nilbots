using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

public sealed record ChallengeRequest(
    Guid BotId,
    Guid OpponentBotId,
    string? MapId = null,
    long? Seed = null);

public static class MatchesEndpoints
{
    public static void MapMatches(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/matches");

        group.MapPost("/challenge", async (
            ChallengeRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            MatchAdmissionService admission,
            MatchParticipantSnapshotFactory snapshots,
            MatchChallengeAnnouncer challenges,
            MatchExecutionSettings matchSettings,
            LegacyCompetitionIdentityResolver identityResolver,
            UnrankedMatchLimits unrankedLimits,
            TimeProvider timeProvider,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            if (request.BotId == request.OpponentBotId)
            {
                return new ApplicationError(
                    ApplicationErrorCodes.MatchSelfChallenge,
                    ApplicationErrorType.Validation,
                    "A bot cannot challenge itself.")
                    .ToProblemDetails(http);
            }
            string mapId =
                DuelArenaDefinition.Official.ResolveUnrankedMapId(request.MapId);
            try
            {
                _ = ArenaMapLoader.Load(mapId, matchSettings.MatchRules);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or MatchDefinitionValidationException
                    or NotSupportedException)
            {
                return new ApplicationError(
                    ApplicationErrorCodes.MatchMapUnavailable,
                    ApplicationErrorType.Validation,
                    $"Map '{mapId}' is not available for the current rules.")
                    .ToProblemDetails(http);
            }

            // Durable, for the same reason ranked is: the HTTP limiter is per web process
            // and forgotten on restart, and this creates a real WASM match. Counting the
            // matches themselves rather than a synthetic tally means the number cannot
            // drift from what actually ran.
            await using var admissionScope =
                await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.TakeAdmissionLockAsync(
                AdmissionLocks.Unranked(userId), cancellationToken);

            DateTime dayAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
            int startedToday = await db.Matches.CountAsync(
                candidate => candidate.InitiatedByUserId == userId
                    && candidate.MatchSetId == null
                    && candidate.CreatedAt >= dayAgo,
                cancellationToken);
            if (startedToday >= unrankedLimits.AccountDailyLimit)
            {
                return new ApplicationError(
                    ApplicationErrorCodes.MatchUnrankedDailyLimit,
                    ApplicationErrorType.RateLimit,
                    $"Accounts may start at most {unrankedLimits.AccountDailyLimit} " +
                    "unranked matches per 24 hours.")
                    .ToProblemDetails(http);
            }

            ApplicationResult<AdmittedMatchBot> challenger =
                await admission.AdmitAsync(
                    request.BotId,
                    userId,
                    cancellationToken);
            if (!challenger.Succeeded)
                return challenger.Error!.ToProblemDetails(http);
            ApplicationResult<AdmittedMatchBot> opponent =
                await admission.AdmitAsync(
                    request.OpponentBotId,
                    requiredOwnerUserId: null,
                    cancellationToken);
            if (!opponent.Succeeded)
                return opponent.Error!.ToProblemDetails(http);

            long seed = request.Seed ?? Random.Shared.NextInt64();
            LegacyCompetitionIdentity identity =
                await identityResolver.ResolveOrCreateAsync(
                    matchSettings.MatchRules.RulesVersion,
                    matchSettings.MatchRules.RulesVersion,
                    cancellationToken);

            var match = new Match
            {
                MapId = mapId,
                Seed = seed,
                InitiatedByUserId = userId,
                GameRulesVersion =
                    matchSettings.MatchRules.RulesVersion,
                PlaylistVersionId = identity.PlaylistVersionId,
            };
            match.Participants.Add(snapshots.Create(match.Id, 0, challenger.Value!));
            match.Participants.Add(snapshots.Create(match.Id, 1, opponent.Value!));
            db.Matches.Add(match);
            db.BackgroundJobs.Add(BackgroundJob.ExecuteMatch(match.Id));
            await db.SaveChangesAsync(cancellationToken);
            // Announced *after* the match is saved, not with it. The writer's INSERT and
            // pg_notify run immediately rather than at SaveChanges, so announcing first
            // would tell someone to go and watch a match that a failed save then left
            // nonexistent. This way the worse failure is a missing challenge notification,
            // and the result announcement still arrives.
            await admissionScope.CommitAsync(cancellationToken);
            await challenges.AnnounceAsync(match, cancellationToken);
            return Results.Ok(new CreatedMatchResponse(match.Id));
        })
        .Produces<CreatedMatchResponse>()
        .Produces<ApplicationProblemResponse>(
            StatusCodes.Status400BadRequest,
            "application/problem+json")
        .Produces<ApplicationProblemResponse>(
            StatusCodes.Status403Forbidden,
            "application/problem+json")
        .Produces<ApplicationProblemResponse>(
            StatusCodes.Status404NotFound,
            "application/problem+json")
        .Produces<ApplicationProblemResponse>(
            StatusCodes.Status409Conflict,
            "application/problem+json")
        .Produces<ApplicationProblemResponse>(
            StatusCodes.Status429TooManyRequests,
            "application/problem+json")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitPolicies.Challenge);

        // Filters are server-side on purpose: a browser-side filter can only narrow the
        // page it already has, so "every match Bastille played" would silently mean
        // "the ones in the latest 30" (UI audit, DECISIONS #99).
        //   bot    slug or id — matches where that bot played, either slot
        //   map    map id
        //   ranked true = part of a ranked set, false = unranked only
        //   skip   offset, for the feed's Load more
        group.MapGet("/", async (
            AppDbContext db,
            TimeProvider timeProvider,
            int take,
            string? bot,
            string? map,
            bool? ranked,
            int? skip) =>
        {
            take = take is > 0 and <= 100 ? take : 25;
            int offset = skip is > 0 ? skip.Value : 0;
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;

            var query = db.Matches
                .Include(m => m.Participants)
                .Where(match =>
                    match.PlaylistVersionId == null ||
                    !db.PlaylistVersions.Any(version =>
                        version.Id == match.PlaylistVersionId &&
                        version.Visibility == PlaylistVisibilityIds.Labs))
                .AsQueryable();
            if (bot is { Length: > 0 } botKey)
            {
                Guid? botId = Guid.TryParse(botKey, out var parsed)
                    ? parsed
                    : await db.Bots.Where(b => b.Slug == botKey)
                        .Select(b => (Guid?)b.Id).SingleOrDefaultAsync();
                // An unknown bot filters to nothing rather than quietly showing everything.
                query = query.Where(m => m.Participants.Any(p => p.BotId == botId));
            }
            if (map is { Length: > 0 } mapId)
                query = query.Where(m => m.MapId == mapId);
            if (ranked is bool wantRanked)
                query = query.Where(m => wantRanked ? m.MatchSetId != null : m.MatchSetId == null);

            var matches = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip(offset)
                .Take(take)
                .ToListAsync();
            // Outcomes stay hidden until the broadcast catches up (plan §28).
            return Results.Ok(matches.Select(
                match => MatchPublicProjection.ToSummary(match, now)));
        }).Produces<IReadOnlyList<MatchSummaryResponse>>();

        group.MapGet("/{matchId:guid}", async (
            Guid matchId,
            AppDbContext db,
            TimeProvider timeProvider) =>
        {
            var match = await db.Matches
                .Include(m => m.Participants)
                .Include(m => m.TeamResults)
                    .ThenInclude(result => result.Scores)
                .SingleOrDefaultAsync(m => m.Id == matchId);
            if (match is null)
                return Results.NotFound();
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            return Results.Ok(MatchPublicProjection.ToDetail(match, now));
        }).Produces<MatchDetailResponse>();

        // Shared presentation clock (plan §28.3): all viewers derive the same tick.
        group.MapGet("/{matchId:guid}/live", async (
            Guid matchId,
            AppDbContext db,
            TimeProvider timeProvider) =>
        {
            var match = await db.Matches.FindAsync(matchId);
            if (match is null)
                return Results.NotFound();
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            return Results.Ok(MatchPublicProjection.ToLive(match, now));
        }).Produces<MatchLiveResponse>();

        // The replay never reveals events or the result ahead of the presentation
        // clock (plan §28.1): mid-broadcast requests get a truncated document.
        group.MapGet("/{matchId:guid}/replay", async (
            Guid matchId,
            AppDbContext db,
            IObjectStore objectStore,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var match = await db.Matches.FindAsync([matchId], cancellationToken);
            if (match?.ReplayKey is null)
                return Results.NotFound();
            Stream? replay = await objectStore.OpenReadAsync(match.ReplayKey, cancellationToken);
            if (replay is null)
                return Results.NotFound();

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            if (match.BroadcastComplete(now))
                return Results.Stream(replay, "application/json");

            await using (replay)
            {
                int visibleTicks = Math.Max(0, match.PresentationTick(now) + 1);
                using var reader = new StreamReader(replay);
                string replayJson =
                    await reader.ReadToEndAsync(cancellationToken);
                if (match.ReplayFormatVersion ==
                    BotArenaVersions.GenericActorReplayFormatVersion)
                {
                    string partialReplayJson =
                        GenericActorReplayDocument.CreatePartialPrefix(
                            replayJson,
                            visibleTicks);
                    return Results.Text(
                        partialReplayJson,
                        "application/json; charset=utf-8");
                }
                if (match.ReplayFormatVersion is not null and
                    not BotArenaVersions.ReplayFormatVersion)
                {
                    throw new InvalidOperationException(
                        $"Replay format {match.ReplayFormatVersion} is not " +
                        "supported by the hosted broadcast projector.");
                }

                var document =
                    Engine.ReplaySerializer.FromJson(replayJson);
                var partial = new
                {
                    document.Header,
                    Ticks = document.Ticks.Take(visibleTicks),
                    Result = (Engine.MatchResultInfo?)null,
                    ReplayHash = (string?)null,
                    Partial = true,
                };
                return Results.Json(partial, Engine.ReplaySerializer.Canonical);
            }
        });
    }
}
