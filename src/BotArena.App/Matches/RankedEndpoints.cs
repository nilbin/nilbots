using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

public sealed record RankedChallengeRequest(Guid BotId, Guid OpponentBotId);

public static class RankedEndpoints
{
    /// <summary>The three map/seed pairs of a set; each is played twice with mirrored slots.</summary>
    private static readonly string[] SetMaps = ["basic-01", "arena-01", "arena-01"];

    public static void MapRanked(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/matches/ranked",
            async (RankedChallengeRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            if (request.BotId == request.OpponentBotId)
                return Results.Problem("A bot cannot play a ranked set against itself.", statusCode: 400);
            var botA = await db.Bots.SingleOrDefaultAsync(b => b.Id == request.BotId);
            var botB = await db.Bots.SingleOrDefaultAsync(b => b.Id == request.OpponentBotId);
            if (botA is null || botB is null)
                return Results.Problem("Bot not found.", statusCode: 404);
            if (botA.OwnerUserId != userId)
                return Results.Problem("You can only start ranked sets with your own bot.", statusCode: 403);

            var versionA = await ActiveVersion(db, botA.Id);
            var versionB = await ActiveVersion(db, botB.Id);
            if (versionA is null || versionB is null)
                return Results.Problem("Both bots need a successfully built active version.", statusCode: 409);

            var set = new MatchSet
            {
                BotAId = botA.Id,
                BotBId = botB.Id,
                BotAVersionId = versionA.Id,
                BotBVersionId = versionB.Id,
                RatingABefore = botA.Rating,
                RatingBBefore = botB.Rating,
            };
            db.MatchSets.Add(set);

            int game = 0;
            foreach (string mapId in SetMaps)
            {
                long seed = Random.Shared.NextInt64();
                foreach (bool mirrored in new[] { false, true })
                {
                    game++;
                    var match = new Match
                    {
                        MapId = mapId,
                        Seed = seed,
                        MatchSetId = set.Id,
                        SetGame = game,
                    };
                    var (first, firstVersion) = mirrored ? (botB, versionB) : (botA, versionA);
                    var (second, secondVersion) = mirrored ? (botA, versionA) : (botB, versionB);
                    match.Participants.Add(Snapshot(match.Id, 0, first, firstVersion));
                    match.Participants.Add(Snapshot(match.Id, 1, second, secondVersion));
                    db.Matches.Add(match);
                    db.BackgroundJobs.Add(BackgroundJob.ExecuteMatch(match.Id));
                }
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { set.Id });
        }).RequireAuthorization().RequireRateLimiting("challenge");

        routes.MapGet("/api/matchsets/{setId:guid}", async (Guid setId, AppDbContext db) =>
        {
            var set = await db.MatchSets.FindAsync(setId);
            if (set is null)
                return Results.NotFound();
            var botA = await db.Bots.FindAsync(set.BotAId);
            var botB = await db.Bots.FindAsync(set.BotBId);
            var matches = await db.Matches.Include(m => m.Participants)
                .Where(m => m.MatchSetId == setId)
                .OrderBy(m => m.SetGame)
                .ToListAsync();

            var now = DateTime.UtcNow;
            // The aggregate (scores, rating changes) necessarily spoils the games; withhold
            // it until every game's broadcast has finished (plan §28: no early results).
            bool allWatched = matches.Count > 0 && matches.All(m =>
                m.Status == MatchStatus.Failed || m.BroadcastComplete(now));

            return Results.Ok(new
            {
                set.Id,
                Status = set.Status.ToString(),
                BotA = new { Id = set.BotAId, botA?.Name, botA?.Accent },
                BotB = new { Id = set.BotBId, botB?.Name, botB?.Accent },
                set.CreatedAt,
                Revealed = allWatched && set.Status != MatchSetStatus.Running,
                ScoreA = allWatched && set.Status == MatchSetStatus.Completed ? set.ScoreA : (double?)null,
                ScoreB = allWatched && set.Status == MatchSetStatus.Completed ? set.ScoreB : (double?)null,
                RatingChangeA = allWatched && set.Status == MatchSetStatus.Completed ? set.RatingChangeA : (double?)null,
                RatingChangeB = allWatched && set.Status == MatchSetStatus.Completed ? set.RatingChangeB : (double?)null,
                WinnerBotId = allWatched ? set.WinnerBotId : null,
                Games = matches.Select(m =>
                {
                    bool visible = m.BroadcastComplete(now);
                    return new
                    {
                        m.Id,
                        Game = m.SetGame,
                        m.MapId,
                        Status = m.Status.ToString(),
                        Broadcasting = m.Status == MatchStatus.Completed && !visible,
                        WinnerBotId = visible && m.WinnerSlot is int winner
                            ? m.Participants.Single(p => p.Slot == winner).BotId
                            : (Guid?)null,
                        Draw = visible && m.Status == MatchStatus.Completed && m.WinnerSlot is null,
                        Participants = m.Participants.OrderBy(p => p.Slot)
                            .Select(p => new { p.Slot, p.BotId, p.NameSnapshot, p.AccentSnapshot }),
                    };
                }),
            });
        });

        routes.MapGet("/api/leaderboard", async (AppDbContext db) =>
        {
            var bots = await db.Bots
                .Where(b => b.RankedSets > 0)
                .OrderByDescending(b => b.Rating)
                .Take(100)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Accent,
                    Owner = db.Users.Where(u => u.Id == b.OwnerUserId).Select(u => u.DisplayName).First(),
                    Rating = Math.Round(b.Rating),
                    b.RankedSets,
                })
                .ToListAsync();
            return Results.Ok(bots);
        });
    }

    private static MatchParticipant Snapshot(Guid matchId, int slot, Bot bot, BotVersion version) => new()
    {
        MatchId = matchId,
        Slot = slot,
        BotId = bot.Id,
        BotVersionId = version.Id,
        NameSnapshot = bot.Name,
        AccentSnapshot = bot.Accent,
        ArtifactHashSnapshot = version.ArtifactHash ?? "",
    };

    private static Task<BotVersion?> ActiveVersion(AppDbContext db, Guid botId) =>
        db.BotVersions
            .Where(v => v.BotId == botId && v.IsActive && v.Status == BuildStatus.Built)
            .SingleOrDefaultAsync();
}
