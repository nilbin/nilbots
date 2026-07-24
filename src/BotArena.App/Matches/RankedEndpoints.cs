using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

/// <summary>Rules is an optional GameRules.Resolve name ("0.4", "0.3", "hill"…) —
/// omitted means the server's default ruleset. Every ruleset has its own elo ladder
/// (DECISIONS #54), so challenging on an old ruleset never touches current standings.</summary>
public sealed record RankedChallengeRequest(Guid BotId, Guid OpponentBotId, string? Rules = null);

public static class RankedEndpoints
{
    /// <summary>The ranked map pool: each set samples 3 distinct maps, each played
    /// twice with mirrored slots. crossfire-01 joined with rules 0.3 (broken
    /// sightlines, RULES-0.3-DESIGN §F). causeway remains available as an
    /// adversarial narrow-zone test map but left ranked play after the gen-7
    /// geometry review (DECISIONS #62).</summary>
    private static readonly string[] MapPool =
        ["basic-01", "arena-01", "crossfire-01", "bastion-01", "gallery-01"];

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

            Engine.GameRules setRules;
            try
            {
                setRules = request.Rules is { Length: > 0 } rulesName
                    ? Engine.GameRules.Resolve(rulesName)
                    : JobWorker.MatchRules;
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }

            var set = new MatchSet
            {
                BotAId = botA.Id,
                BotBId = botB.Id,
                BotAVersionId = versionA.Id,
                BotBVersionId = versionB.Id,
                RulesName = request.Rules is { Length: > 0 } ? request.Rules : null,
                GameRulesVersion = setRules.RulesVersion,
                RatingABefore = await LadderRating(db, botA.Id, setRules.RulesVersion),
                RatingBBefore = await LadderRating(db, botB.Id, setRules.RulesVersion),
            };
            db.MatchSets.Add(set);

            string[] setMaps = [.. MapPool.OrderBy(_ => Random.Shared.Next()).Take(3)];
            int game = 0;
            foreach (string mapId in setMaps)
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
                RulesVersion = set.GameRulesVersion,
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

        // One ladder per rules version (DECISIONS #54). ?rules=<version string> picks
        // the ladder; default = the server's current ruleset. `ladders` lists every
        // ladder that has results, newest-looking first.
        routes.MapGet("/api/leaderboard", async (string? rules, AppDbContext db) =>
        {
            string version = rules is { Length: > 0 } ? rules : JobWorker.MatchRules.RulesVersion;
            var ladders = await db.BotRatings
                .Where(r => r.RankedSets > 0)
                .Select(r => r.RulesVersion)
                .Distinct()
                .OrderByDescending(v => v)
                .ToListAsync();
            var entries = await db.BotRatings
                .Where(r => r.RulesVersion == version && r.RankedSets > 0)
                .OrderByDescending(r => r.Rating)
                .Take(100)
                .Join(db.Bots, r => r.BotId, b => b.Id, (r, b) => new
                {
                    b.Id,
                    b.Name,
                    b.Accent,
                    Owner = db.Users.Where(u => u.Id == b.OwnerUserId).Select(u => u.DisplayName).First(),
                    Rating = Math.Round(r.Rating),
                    r.RankedSets,
                })
                .ToListAsync();
            return Results.Ok(new { RulesVersion = version, Ladders = ladders, Entries = entries });
        });
    }

    private static async Task<double> LadderRating(AppDbContext db, Guid botId, string rulesVersion) =>
        await db.BotRatings
            .Where(r => r.BotId == botId && r.RulesVersion == rulesVersion)
            .Select(r => (double?)r.Rating)
            .SingleOrDefaultAsync() ?? 1200;

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
