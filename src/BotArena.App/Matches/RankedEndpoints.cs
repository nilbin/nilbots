using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

/// <summary>Rules is an optional GameRules.Resolve name ("0.5", "0.4", "hill"…) —
/// omitted means the server's default ruleset. Every ruleset has its own elo ladder
/// (DECISIONS #54), so challenging on an old ruleset never touches current standings.
///
/// OpponentBotId is NOT how ranked play works (DECISIONS #95): the server matchmakes by
/// rating. It survives only for evaluation harnesses running scripted pairings, and only
/// on servers that opt in — production refuses it.</summary>
public sealed record RankedChallengeRequest(Guid BotId, Guid? OpponentBotId = null, string? Rules = null);

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
            async (RankedChallengeRequest request, ClaimsPrincipal principal, AppDbContext db,
                   ApplicationMode mode, IConfiguration configuration,
                   BotAppearancePolicy appearancePolicy, HttpContext http,
                   CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            var botA = await db.Bots.SingleOrDefaultAsync(b => b.Id == request.BotId);
            if (botA is null)
                return Results.Problem("Bot not found.", statusCode: 404);
            if (botA.OwnerUserId != userId)
                return Results.Problem("You can only start ranked sets with your own bot.", statusCode: 403);

            ApplicationResult<BotAppearance> appearanceA =
                await appearancePolicy.ValidateForMatchAdmissionAsync(
                    botA,
                    cancellationToken);
            if (!appearanceA.Succeeded)
            {
                ApplicationTelemetry.Record(
                    "matches.admit_appearance",
                    appearanceA.Error!.Code,
                    userId,
                    botA.Id);
                return appearanceA.Error.ToProblemDetails(http);
            }

            var versionA = await ActiveVersion(db, botA.Id);
            if (versionA is null)
                return Results.Problem("Your bot needs a successfully built active version.", statusCode: 409);

            Engine.GameRules setRules;
            try
            {
                if (request.Rules is { Length: > 0 } rulesName)
                {
                    // One live ladder: the ruleset this server runs. Every other ladder is
                    // frozen history (DECISIONS #97) — still readable, closed to new sets.
                    // Resolve first so an unknown name gets its own error rather than
                    // being reported as a closed ladder.
                    setRules = Engine.GameRules.Resolve(rulesName);
                    if (setRules.RulesVersion != JobWorker.MatchRules.RulesVersion &&
                        !AllowsPinnedOpponents(mode, configuration))
                        return Results.Problem(
                            $"The {setRules.RulesVersion} ladder is closed to new sets — this " +
                            $"server plays {JobWorker.MatchRules.RulesVersion}. Past results and " +
                            "ratings stay visible at /api/leaderboard?rules=" + setRules.RulesVersion + ".",
                            statusCode: 400);
                }
                else
                {
                    setRules = JobWorker.MatchRules;
                }
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }

            Bot? botB;
            if (request.OpponentBotId is Guid pinned)
            {
                if (!AllowsPinnedOpponents(mode, configuration))
                    return Results.Problem(
                        "Ranked opponents are matchmade by rating; omit opponentBotId. " +
                        "To choose who you play, use an unranked match (POST /api/matches/challenge).",
                        statusCode: 400);
                if (pinned == request.BotId)
                    return Results.Problem("A bot cannot play a ranked set against itself.", statusCode: 400);
                botB = await db.Bots.SingleOrDefaultAsync(b => b.Id == pinned);
                if (botB is null)
                    return Results.Problem("Bot not found.", statusCode: 404);
            }
            else
            {
                var opponentId = await MatchmakeAsync(db, botA, userId, setRules.RulesVersion);
                if (opponentId is null)
                    return Results.Problem(
                        "No opponent is available on this ladder yet — every ranked set needs " +
                        "another bot with a successfully built version.", statusCode: 409);
                botB = await db.Bots.SingleAsync(b => b.Id == opponentId);
            }

            var versionB = await ActiveVersion(db, botB.Id);
            if (versionB is null)
                return Results.Problem("Both bots need a successfully built active version.", statusCode: 409);
            ApplicationResult<BotAppearance> appearanceB =
                await appearancePolicy.ValidateForMatchAdmissionAsync(
                    botB,
                    cancellationToken);
            if (!appearanceB.Succeeded)
            {
                ApplicationTelemetry.Record(
                    "matches.admit_appearance",
                    appearanceB.Error!.Code,
                    userId,
                    botB.Id);
                return appearanceB.Error.ToProblemDetails(http);
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
                            .Select(p => new
                            {
                                p.Slot,
                                p.BotId,
                                p.NameSnapshot,
                                p.AccentSnapshot,
                                p.LookIdSnapshot,
                                p.ProjectileLookIdSnapshot,
                            }),
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
            var allLadders = await db.BotRatings
                .Where(r => r.RankedSets > 0)
                .Select(r => r.RulesVersion)
                .Distinct()
                .OrderByDescending(v => v)
                .ToListAsync();
            // Offer the game's own ladders, not the research arms. GameRules.ShippedNames
            // exists because listing every arm as an equal choice reads as two dozen valid
            // games to a newcomer, and the leaderboard was making exactly that mistake in
            // its ruleset switcher (UI audit). Arms stay queryable by ?rules= for the
            // balance harness; they are just not offered as somewhere to go.
            var ladders = allLadders
                .Where(v => v == version ||
                            v == JobWorker.MatchRules.RulesVersion ||
                            Engine.GameRules.ShippedNames.Contains(v))
                .ToList();
            var entries = await db.BotRatings
                .Where(r => r.RulesVersion == version && r.RankedSets > 0)
                .OrderByDescending(r => r.Rating)
                .Take(100)
                .Join(db.Bots, r => r.BotId, b => b.Id, (r, b) => new
                {
                    b.Id,
                    b.Slug,
                    b.Name,
                    b.Accent,
                    Owner = db.Users.Where(u => u.Id == b.OwnerUserId).Select(u => u.DisplayName).First(),
                    Rating = Math.Round(r.Rating),
                    r.RankedSets,
                })
                .ToListAsync();
            // ActiveRulesVersion tells a reader which ladder still accepts sets; every
            // other one is a historical record (DECISIONS #97).
            return Results.Ok(new
            {
                RulesVersion = version,
                ActiveRulesVersion = JobWorker.MatchRules.RulesVersion,
                Ladders = ladders,
                Entries = entries,
            });
        });
    }

    /// <summary>Scripted pairings are an evaluation-harness need (agent-arena runs a
    /// round robin and crowns champions), not a player-facing one. Local `all` servers
    /// allow them by default so the harness keeps working; every other role refuses.
    /// Explicit configuration wins either way — which is also the only way to exercise
    /// the refusal on a machine running the single-process role.</summary>
    private static bool AllowsPinnedOpponents(ApplicationMode mode, IConfiguration configuration) =>
        configuration.GetValue<bool?>("BOTARENA_ALLOW_PINNED_RANKED") ?? mode.IsAll;

    /// <summary>Everyone with a playable bot, reduced to what the selection rule needs,
    /// then handed to <see cref="RankedMatchmaking"/>.</summary>
    private static async Task<Guid?> MatchmakeAsync(
        AppDbContext db, Bot challenger, Guid userId, string rulesVersion)
    {
        var candidates = await db.Bots
            .Where(b => b.Id != challenger.Id)
            .Where(b => b.Versions.Any(v => v.IsActive && v.Status == BuildStatus.Built))
            .Select(b => new MatchmakingCandidate(
                b.Id,
                b.Ratings.Where(r => r.RulesVersion == rulesVersion)
                    .Select(r => (double?)r.Rating).FirstOrDefault() ?? BotRating.DefaultRating,
                b.OwnerUserId == userId))
            .ToListAsync();
        double mine = await LadderRating(db, challenger.Id, rulesVersion);
        return RankedMatchmaking.Choose(candidates, mine, Random.Shared.Next);
    }

    private static async Task<double> LadderRating(AppDbContext db, Guid botId, string rulesVersion) =>
        await db.BotRatings
            .Where(r => r.BotId == botId && r.RulesVersion == rulesVersion)
            .Select(r => (double?)r.Rating)
            .SingleOrDefaultAsync() ?? BotRating.DefaultRating;

    private static MatchParticipant Snapshot(Guid matchId, int slot, Bot bot, BotVersion version) => new()
    {
        MatchId = matchId,
        Slot = slot,
        BotId = bot.Id,
        BotVersionId = version.Id,
        NameSnapshot = bot.Name,
        AccentSnapshot = bot.Accent,
        LookIdSnapshot = bot.LookId,
        ProjectileLookIdSnapshot = bot.ProjectileLookId,
        ArtifactHashSnapshot = version.ArtifactHash ?? "",
    };

    private static Task<BotVersion?> ActiveVersion(AppDbContext db, Guid botId) =>
        db.BotVersions
            .Where(v => v.BotId == botId && v.IsActive && v.Status == BuildStatus.Built)
            .SingleOrDefaultAsync();
}
