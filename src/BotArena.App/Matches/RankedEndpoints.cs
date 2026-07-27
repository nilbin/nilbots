using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
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
                   MatchAdmissionService admission,
                   MatchParticipantSnapshotFactory snapshots,
                   MatchExecutionSettings matchSettings,
                   LegacyCompetitionIdentityResolver identityResolver,
                   RankedSetLimits rankedLimits,
                   TimeProvider timeProvider,
                   HttpContext http,
                   CancellationToken cancellationToken) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();

            // Durable admission, in a transaction with an advisory lock on the account —
            // the same shape compilation uses, and for the same reason. The HTTP limiter
            // lives in one web process's memory, so it multiplies by replica and forgets
            // everything on restart; a ranked set is six WASM matches and needs a limit
            // that actually holds.
            await using var admissionScope =
                await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({AdmissionLocks.Ranked(userId)})",
                cancellationToken);

            DateTime rankedDayAgo = timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);
            var ownedSets =
                from candidate in db.MatchSets
                join ownedBot in db.Bots on candidate.BotAId equals ownedBot.Id
                where ownedBot.OwnerUserId == userId
                select candidate;

            var rankedSnapshot = new RankedSetSnapshot(
                AccountDailyCount: await ownedSets.CountAsync(
                    candidate => candidate.CreatedAt >= rankedDayAgo,
                    cancellationToken),
                AccountUnfinishedCount: await ownedSets.CountAsync(
                    candidate => candidate.Status == MatchSetStatus.Running,
                    cancellationToken));

            List<string> rankedEntitlements = await db.EntitlementGrants
                .Where(grant => grant.UserId == userId && grant.RevokedAt == null)
                .Select(grant => grant.EntitlementKey)
                .ToListAsync(cancellationToken);

            if (RankedSetPolicy.Evaluate(
                    rankedSnapshot,
                    rankedLimits.ForAccount(rankedEntitlements)) is string refusal)
            {
                return Results.Problem(refusal, statusCode: 429);
            }
            ApplicationResult<AdmittedMatchBot> admittedA =
                await admission.AdmitAsync(
                    request.BotId,
                    userId,
                    cancellationToken);
            if (!admittedA.Succeeded)
                return admittedA.Error!.ToProblemDetails(http);
            AdmittedMatchBot participantA = admittedA.Value!;
            Bot botA = participantA.Bot;

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
                    if (setRules.RulesVersion != matchSettings.MatchRules.RulesVersion &&
                        !AllowsPinnedOpponents(mode, configuration))
                        return Results.Problem(
                            $"The {setRules.RulesVersion} ladder is closed to new sets — this " +
                            $"server plays {matchSettings.MatchRules.RulesVersion}. Past results and " +
                            "ratings stay visible at /api/leaderboard?rules=" + setRules.RulesVersion + ".",
                            statusCode: 400);
                }
                else
                {
                    setRules = matchSettings.MatchRules;
                }
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }

            Guid botBId;
            if (request.OpponentBotId is Guid pinned)
            {
                if (!AllowsPinnedOpponents(mode, configuration))
                    return Results.Problem(
                        "Ranked opponents are matchmade by rating; omit opponentBotId. " +
                        "To choose who you play, use an unranked match (POST /api/matches/challenge).",
                        statusCode: 400);
                if (pinned == request.BotId)
                    return Results.Problem("A bot cannot play a ranked set against itself.", statusCode: 400);
                botBId = pinned;
            }
            else
            {
                Guid? opponentId = await MatchmakeAsync(
                    db,
                    botA,
                    userId,
                    setRules.RulesVersion,
                    cancellationToken);
                if (opponentId is null)
                    return Results.Problem(
                        "No opponent is available on this ladder yet — every ranked set needs " +
                        "another bot with a successfully built version.", statusCode: 409);
                botBId = opponentId.Value;
            }

            ApplicationResult<AdmittedMatchBot> admittedB =
                await admission.AdmitAsync(
                    botBId,
                    requiredOwnerUserId: null,
                    cancellationToken);
            if (!admittedB.Succeeded)
                return admittedB.Error!.ToProblemDetails(http);
            AdmittedMatchBot participantB = admittedB.Value!;
            Bot botB = participantB.Bot;
            LegacyCompetitionIdentity identity =
                await identityResolver.ResolveOrCreateAsync(
                    setRules.RulesVersion,
                    matchSettings.MatchRules.RulesVersion,
                    cancellationToken);

            var set = new MatchSet
            {
                BotAId = botA.Id,
                BotBId = botB.Id,
                BotAVersionId = participantA.Version.Id,
                BotBVersionId = participantB.Version.Id,
                RulesName = request.Rules is { Length: > 0 } ? request.Rules : null,
                GameRulesVersion = setRules.RulesVersion,
                PlaylistVersionId = identity.PlaylistVersionId,
                LadderId = identity.LadderId,
                RatingABefore = await LadderRating(
                    db,
                    botA.Id,
                    setRules.RulesVersion,
                    cancellationToken),
                RatingBBefore = await LadderRating(
                    db,
                    botB.Id,
                    setRules.RulesVersion,
                    cancellationToken),
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
                        GameRulesVersion = setRules.RulesVersion,
                        PlaylistVersionId = identity.PlaylistVersionId,
                        RuntimeConfigurationVersion =
                            set.RuntimeConfigurationVersion,
                    };
                    AdmittedMatchBot first =
                        mirrored ? participantB : participantA;
                    AdmittedMatchBot second =
                        mirrored ? participantA : participantB;
                    match.Participants.Add(snapshots.Create(match.Id, 0, first));
                    match.Participants.Add(snapshots.Create(match.Id, 1, second));
                    db.Matches.Add(match);
                    db.BackgroundJobs.Add(BackgroundJob.ExecuteMatch(match.Id));
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            // Committed only now: the count the next request reads has to include this set,
            // and the lock has to survive until it does.
            await admissionScope.CommitAsync(cancellationToken);
            return Results.Ok(new CreatedMatchSetResponse(set.Id));
        }).Produces<CreatedMatchSetResponse>()
          .RequireAuthorization()
          .RequireRateLimiting(RateLimitPolicies.Ranked);

        routes.MapGet("/api/matchsets/{setId:guid}", async (
            Guid setId,
            AppDbContext db,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var set = await db.MatchSets.FindAsync([setId], cancellationToken);
            if (set is null)
                return Results.NotFound();
            var botA = await db.Bots.FindAsync([set.BotAId], cancellationToken);
            var botB = await db.Bots.FindAsync([set.BotBId], cancellationToken);
            var matches = await db.Matches.Include(m => m.Participants)
                .Where(m => m.MatchSetId == setId)
                .OrderBy(m => m.SetGame)
                .ToListAsync(cancellationToken);
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            return Results.Ok(MatchPublicProjection.ToMatchSet(
                set,
                botA,
                botB,
                matches,
                now));
        }).Produces<MatchSetResponse>();

        // One ladder per rules version (DECISIONS #54). ?rules=<version string> picks
        // the ladder; default = the server's current ruleset. `ladders` lists every
        // ladder that has results, newest-looking first.
        routes.MapGet("/api/leaderboard", async (
            string? rules,
            AppDbContext db,
            MatchExecutionSettings matchSettings) =>
        {
            string activeRulesVersion = matchSettings.MatchRules.RulesVersion;
            string version = rules is { Length: > 0 } ? rules : activeRulesVersion;
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
                            v == activeRulesVersion ||
                            Engine.GameRules.ShippedNames.Contains(v))
                .ToList();
            var ratedBots = await db.BotRatings
                .RankedForRules(version)
                .OrderByDescending(rating => rating.Rating)
                .ThenBy(rating => rating.BotId)
                .Take(100)
                .Join(db.Bots, rating => rating.BotId, b => b.Id, (rating, b) => new
                {
                    b.Id,
                    b.Slug,
                    b.Name,
                    b.Accent,
                    b.LookId,
                    Owner = db.Users.Where(u => u.Id == b.OwnerUserId).Select(u => u.DisplayName).First(),
                    rating.Rating,
                    rating.RankedSets,
                })
                .OrderByDescending(entry => entry.Rating)
                .ThenBy(entry => entry.Id)
                .ToListAsync();
            double[] ladderRatings = ratedBots.Select(entry => entry.Rating).ToArray();
            var entries = ratedBots.Select(entry => new LeaderboardEntryResponse(
                entry.Id,
                entry.Slug,
                entry.Name,
                entry.Accent,
                entry.LookId,
                entry.Owner,
                Math.Round(entry.Rating),
                entry.RankedSets,
                LadderStandings.CompetitionRank(ladderRatings, entry.Rating))).ToList();
            // ActiveRulesVersion tells a reader which ladder still accepts sets; every
            // other one is a historical record (DECISIONS #97).
            return Results.Ok(new LeaderboardResponse(
                version,
                activeRulesVersion,
                ladders,
                entries));
        }).Produces<LeaderboardResponse>();
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
        AppDbContext db,
        Bot challenger,
        Guid userId,
        string rulesVersion,
        CancellationToken cancellationToken)
    {
        var candidates = await db.Bots
            .Where(b => b.Id != challenger.Id)
            .Where(b => b.Versions.Any(v => v.IsActive && v.Status == BuildStatus.Built))
            .Select(b => new MatchmakingCandidate(
                b.Id,
                b.Ratings.Where(r => r.RulesVersion == rulesVersion)
                    .Select(r => (double?)r.Rating).FirstOrDefault() ?? BotRating.DefaultRating,
                b.OwnerUserId == userId))
            .ToListAsync(cancellationToken);
        double mine = await LadderRating(
            db,
            challenger.Id,
            rulesVersion,
            cancellationToken);
        return RankedMatchmaking.Choose(candidates, mine, Random.Shared.Next);
    }

    private static async Task<double> LadderRating(
        AppDbContext db,
        Guid botId,
        string rulesVersion,
        CancellationToken cancellationToken = default) =>
        await db.BotRatings
            .Where(r => r.BotId == botId && r.RulesVersion == rulesVersion)
            .Select(r => (double?)r.Rating)
            .SingleOrDefaultAsync(cancellationToken) ?? BotRating.DefaultRating;
}
