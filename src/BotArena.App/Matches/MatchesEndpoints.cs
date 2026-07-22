using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Shared;
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

            var match = new Match { MapId = mapId, Seed = seed };
            match.Participants.Add(new MatchParticipant
            {
                MatchId = match.Id, Slot = 0, BotId = bot.Id, BotVersionId = version.Id,
                NameSnapshot = bot.Name, AccentSnapshot = bot.Accent,
                ArtifactHashSnapshot = version.ArtifactHash ?? "",
            });
            match.Participants.Add(new MatchParticipant
            {
                MatchId = match.Id, Slot = 1, BotId = opponent.Id, BotVersionId = opponentVersion.Id,
                NameSnapshot = opponent.Name, AccentSnapshot = opponent.Accent,
                ArtifactHashSnapshot = opponentVersion.ArtifactHash ?? "",
            });
            db.Matches.Add(match);
            db.BackgroundJobs.Add(BackgroundJob.ExecuteMatch(match.Id));
            await db.SaveChangesAsync();
            return Results.Ok(new { match.Id });
        }).RequireAuthorization().RequireRateLimiting("challenge");

        group.MapGet("/", async (AppDbContext db, int take) =>
        {
            take = take is > 0 and <= 100 ? take : 25;
            var matches = await db.Matches
                .OrderByDescending(m => m.CreatedAt)
                .Take(take)
                .Select(m => new
                {
                    m.Id,
                    m.MapId,
                    Status = m.Status.ToString(),
                    m.WinnerSlot,
                    m.EndReason,
                    m.EndTick,
                    m.CreatedAt,
                    m.CompletedAt,
                    Participants = m.Participants.OrderBy(p => p.Slot).Select(p => new
                    {
                        p.Slot, p.NameSnapshot, p.AccentSnapshot, p.Outcome, p.FinalHealth,
                    }),
                })
                .ToListAsync();
            return Results.Ok(matches);
        });

        group.MapGet("/{matchId:guid}", async (Guid matchId, AppDbContext db) =>
        {
            var match = await db.Matches.Include(m => m.Participants)
                .SingleOrDefaultAsync(m => m.Id == matchId);
            if (match is null)
                return Results.NotFound();
            return Results.Ok(new
            {
                match.Id,
                match.MapId,
                match.Seed,
                Status = match.Status.ToString(),
                match.WinnerSlot,
                match.EndReason,
                match.EndTick,
                match.ReplayHash,
                match.Error,
                match.CreatedAt,
                match.CompletedAt,
                Participants = match.Participants.OrderBy(p => p.Slot).Select(p => new
                {
                    p.Slot, p.BotId, p.NameSnapshot, p.AccentSnapshot,
                    p.ArtifactHashSnapshot, p.Outcome, p.FinalHealth, p.DamageDealt, p.Faults,
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
        group.MapGet("/{matchId:guid}/replay", async (Guid matchId, AppDbContext db) =>
        {
            var match = await db.Matches.FindAsync(matchId);
            if (match?.ReplayPath is null || !File.Exists(match.ReplayPath))
                return Results.NotFound();
            var now = DateTime.UtcNow;
            if (match.BroadcastComplete(now))
                return Results.File(match.ReplayPath, "application/json");

            int visibleTicks = Math.Max(0, match.PresentationTick(now) + 1);
            var document = Engine.ReplaySerializer.FromJson(
                await File.ReadAllTextAsync(match.ReplayPath));
            var partial = new
            {
                document.Header,
                Ticks = document.Ticks.Take(visibleTicks),
                Result = (Engine.MatchResultInfo?)null,
                ReplayHash = (string?)null,
                Partial = true,
            };
            return Results.Json(partial, Engine.ReplaySerializer.Canonical);
        });
    }

    private static Task<BotVersion?> ActiveVersion(AppDbContext db, Guid botId) =>
        db.BotVersions
            .Where(v => v.BotId == botId && v.IsActive && v.Status == BuildStatus.Built)
            .SingleOrDefaultAsync();
}
