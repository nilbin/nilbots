using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using BotArena.App.Storage;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

public sealed record ChallengeRequest(Guid BotId, Guid OpponentBotId, string? MapId, long? Seed);

public static class MatchesEndpoints
{
    public static void MapMatches(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/matches");

        group.MapPost("/challenge", async (ChallengeRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            var bot = await db.Bots.SingleOrDefaultAsync(b => b.Id == request.BotId);
            var opponent = await db.Bots.SingleOrDefaultAsync(b => b.Id == request.OpponentBotId);
            if (bot is null || opponent is null)
                return Results.Problem("Bot not found.", statusCode: 404);
            if (bot.OwnerUserId != userId)
                return Results.Problem("You can only challenge with your own bot.", statusCode: 403);

            var version = await ActiveVersion(db, bot.Id);
            var opponentVersion = await ActiveVersion(db, opponent.Id);
            if (version is null)
                return Results.Problem($"{bot.Name} has no successfully built version yet.", statusCode: 409);
            if (opponentVersion is null)
                return Results.Problem($"{opponent.Name} has no successfully built version yet.", statusCode: 409);

            string mapId = request.MapId is { Length: > 0 } m ? m : "arena-01";
            long seed = request.Seed ?? Random.Shared.NextInt64();

            var match = new Match
            {
                MapId = mapId,
                Seed = seed,
                InitiatedByUserId = userId,
            };
            match.Participants.Add(new MatchParticipant
            {
                MatchId = match.Id, Slot = 0, BotId = bot.Id, BotVersionId = version.Id,
                NameSnapshot = bot.Name, AccentSnapshot = bot.Accent,
                LookIdSnapshot = bot.LookId,
                ProjectileLookIdSnapshot = bot.ProjectileLookId,
                ArtifactHashSnapshot = version.ArtifactHash ?? "",
            });
            match.Participants.Add(new MatchParticipant
            {
                MatchId = match.Id, Slot = 1, BotId = opponent.Id, BotVersionId = opponentVersion.Id,
                NameSnapshot = opponent.Name, AccentSnapshot = opponent.Accent,
                LookIdSnapshot = opponent.LookId,
                ProjectileLookIdSnapshot = opponent.ProjectileLookId,
                ArtifactHashSnapshot = opponentVersion.ArtifactHash ?? "",
            });
            db.Matches.Add(match);
            db.BackgroundJobs.Add(BackgroundJob.ExecuteMatch(match.Id));
            await db.SaveChangesAsync();
            return Results.Ok(new { match.Id });
        }).RequireAuthorization().RequireRateLimiting("challenge");

        // Filters are server-side on purpose: a browser-side filter can only narrow the
        // page it already has, so "every match Bastille played" would silently mean
        // "the ones in the latest 30" (UI audit, DECISIONS #99).
        //   bot    slug or id — matches where that bot played, either slot
        //   map    map id
        //   ranked true = part of a ranked set, false = unranked only
        //   skip   offset, for the feed's Load more
        group.MapGet("/", async (
            AppDbContext db, int take, string? bot, string? map, bool? ranked, int? skip) =>
        {
            take = take is > 0 and <= 100 ? take : 25;
            int offset = skip is > 0 ? skip.Value : 0;
            var now = DateTime.UtcNow;

            var query = db.Matches.Include(m => m.Participants).AsQueryable();
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
            return Results.Ok(matches.Select(m =>
            {
                bool visible = m.BroadcastComplete(now);
                return new
                {
                    m.Id,
                    m.MapId,
                    Status = m.Status.ToString(),
                    Broadcasting = m.Status == MatchStatus.Completed && !visible,
                    m.MatchSetId,
                    m.SetGame,
                    WinnerSlot = visible ? m.WinnerSlot : null,
                    EndReason = visible ? m.EndReason : null,
                    EndTick = visible ? m.EndTick : null,
                    m.CreatedAt,
                    m.CompletedAt,
                    Participants = m.Participants.OrderBy(p => p.Slot).Select(p => new
                    {
                        p.Slot,
                        p.NameSnapshot,
                        p.AccentSnapshot,
                        p.LookIdSnapshot,
                        p.ProjectileLookIdSnapshot,
                        Outcome = visible ? p.Outcome : null,
                        FinalHealth = visible ? p.FinalHealth : null,
                    }),
                };
            }));
        });

        group.MapGet("/{matchId:guid}", async (Guid matchId, AppDbContext db) =>
        {
            var match = await db.Matches.Include(m => m.Participants)
                .SingleOrDefaultAsync(m => m.Id == matchId);
            if (match is null)
                return Results.NotFound();
            bool visible = match.BroadcastComplete(DateTime.UtcNow);
            return Results.Ok(new
            {
                match.Id,
                match.MapId,
                match.Seed,
                Status = match.Status.ToString(),
                match.MatchSetId,
                match.SetGame,
                WinnerSlot = visible ? match.WinnerSlot : null,
                EndReason = visible ? match.EndReason : null,
                EndTick = visible ? match.EndTick : null,
                ReplayHash = visible ? match.ReplayHash : null,
                match.Error,
                match.CreatedAt,
                match.CompletedAt,
                Participants = match.Participants.OrderBy(p => p.Slot).Select(p => new
                {
                    p.Slot, p.BotId, p.NameSnapshot, p.AccentSnapshot, p.LookIdSnapshot,
                    p.ProjectileLookIdSnapshot,
                    p.ArtifactHashSnapshot,
                    Outcome = visible ? p.Outcome : null,
                    FinalHealth = visible ? p.FinalHealth : null,
                    DamageDealt = visible ? p.DamageDealt : null,
                    Faults = visible ? p.Faults : null,
                }),
            });
        });

        // Shared presentation clock (plan §28.3): all viewers derive the same tick.
        group.MapGet("/{matchId:guid}/live", async (Guid matchId, AppDbContext db) =>
        {
            var match = await db.Matches.FindAsync(matchId);
            if (match is null)
                return Results.NotFound();
            var now = DateTime.UtcNow;
            int presentationTick = Math.Clamp(
                match.PresentationTick(now), -1, match.EndTick.GetValueOrDefault(int.MaxValue - 1) + 1);
            return Results.Ok(new
            {
                Status = match.Status.ToString(),
                match.PresentationTicksPerSecond,
                PresentationTick = presentationTick,
                TotalTicks = match.EndTick + 1,
                BroadcastComplete = match.BroadcastComplete(now),
                CountdownMs = match.BroadcastStartedAt is DateTime start && start > now
                    ? (int)(start - now).TotalMilliseconds
                    : 0,
            });
        });

        // The replay never reveals events or the result ahead of the presentation
        // clock (plan §28.1): mid-broadcast requests get a truncated document.
        group.MapGet("/{matchId:guid}/replay", async (
            Guid matchId,
            AppDbContext db,
            IObjectStore objectStore,
            CancellationToken cancellationToken) =>
        {
            var match = await db.Matches.FindAsync([matchId], cancellationToken);
            if (match?.ReplayKey is null)
                return Results.NotFound();
            Stream? replay = await objectStore.OpenReadAsync(match.ReplayKey, cancellationToken);
            if (replay is null)
                return Results.NotFound();

            var now = DateTime.UtcNow;
            if (match.BroadcastComplete(now))
                return Results.Stream(replay, "application/json");

            await using (replay)
            {
                int visibleTicks = Math.Max(0, match.PresentationTick(now) + 1);
                using var reader = new StreamReader(replay);
                var document = Engine.ReplaySerializer.FromJson(
                    await reader.ReadToEndAsync(cancellationToken));
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

    private static Task<BotVersion?> ActiveVersion(AppDbContext db, Guid botId) =>
        db.BotVersions
            .Where(v => v.BotId == botId && v.IsActive && v.Status == BuildStatus.Built)
            .SingleOrDefaultAsync();
}
