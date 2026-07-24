using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// The background worker (plan §22): claims jobs from the database with
/// FOR UPDATE SKIP LOCKED and runs compilation and match execution in-process.
/// Bot execution is already sandboxed by the WASM runtime, so no extra process
/// isolation is needed for matches; compilation runs dotnet in a child process.
///
/// Jobs run in typed lanes (DECISIONS #42): exactly ONE match lane — set
/// finalization is race-free only because match jobs have a single consumer —
/// plus BOTARENA_COMPILE_WORKERS compile lanes (default 1), so a 3-minute
/// NativeAOT compile never blocks match execution and concurrent submissions
/// compile in parallel (BotBuilder serializes same-cache-key builds across
/// threads and processes).
/// </summary>
public sealed class JobWorker(IServiceScopeFactory scopeFactory, ILogger<JobWorker> logger)
    : BackgroundService
{
    private static readonly int CompileWorkers =
        ReadEnv("BOTARENA_COMPILE_WORKERS", fallback: 1, min: 1, max: 8);

    /// <summary>Presentation pacing (plan §28). Production default: 5 ticks/s after a
    /// 3 s countdown. Eval/CI deployments crank BOTARENA_BROADCAST_TPS so harnesses
    /// aren't rate-limited by the spectator clock (DECISIONS #41); the no-spoiler
    /// invariant is untouched — this configures the clock, never bypasses it.</summary>
    private static readonly int BroadcastTicksPerSecond =
        ReadEnv("BOTARENA_BROADCAST_TPS", fallback: 5, min: 1, max: 1000);
    private static readonly int BroadcastDelaySeconds =
        ReadEnv("BOTARENA_BROADCAST_DELAY_SECONDS", fallback: 3, min: 0, max: 300);

    /// <summary>The DEFAULT ruleset for matches on this server (BOTARENA_RULES, default
    /// GameRules.Current). A ranked set may pin a different ruleset per request
    /// (DECISIONS #54 — every ruleset has its own elo ladder, so legacy queues stay
    /// playable); sets without a pin follow this default. Eval deployments set
    /// "energy" etc. to run whole tournaments under a rules experiment.</summary>
    internal static readonly GameRules MatchRules =
        Environment.GetEnvironmentVariable("BOTARENA_RULES") is { Length: > 0 } name
            ? GameRules.Resolve(name)
            : GameRules.Current;

    private static int ReadEnv(string name, int fallback, int min, int max) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value)
            ? Math.Clamp(value, min, max)
            : fallback;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Job worker started: 1 match lane, {CompileWorkers} compile lane(s), broadcast {Tps} ticks/s + {Delay}s countdown, rules {Rules}",
            CompileWorkers, BroadcastTicksPerSecond, BroadcastDelaySeconds, MatchRules.RulesVersion);
        var lanes = new List<Task> { RunLane(BackgroundJob.ExecuteMatchType, stoppingToken) };
        for (int i = 0; i < CompileWorkers; i++)
            lanes.Add(RunLane(BackgroundJob.CompileSubmissionType, stoppingToken));
        await Task.WhenAll(lanes);
    }

    private async Task RunLane(string jobType, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork = false;
            try
            {
                didWork = await RunOneJob(jobType, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job lane ({Type}) iteration failed", jobType);
            }
            if (!didWork)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ContinueWith(_ => { });
        }
    }

    private async Task<bool> RunOneJob(string jobType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobs = await db.BackgroundJobs
            .FromSqlRaw("""
                UPDATE "BackgroundJobs" SET "Status" = 'Running', "LockedUntil" = now() + interval '10 minutes'
                WHERE "Id" = (
                    SELECT "Id" FROM "BackgroundJobs"
                    WHERE "Type" = {0}
                      AND (("Status" = 'Pending' AND "AvailableAt" <= now())
                       OR ("Status" = 'Running' AND "LockedUntil" < now()))
                    ORDER BY "Id"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """, jobType)
            .AsNoTracking()
            .ToListAsync(cancellationToken);   // ToList: no SQL composition over UPDATE..RETURNING
        var job = jobs.FirstOrDefault();
        if (job is null)
            return false;

        logger.LogInformation("Running job {JobId} ({Type})", job.Id, job.Type);
        try
        {
            switch (job.Type)
            {
                case BackgroundJob.CompileSubmissionType:
                    await CompileSubmission(db, job.PayloadId("botVersionId"), cancellationToken);
                    break;
                case BackgroundJob.ExecuteMatchType:
                    await ExecuteMatch(db, job.PayloadId("matchId"), cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown job type '{job.Type}'.");
            }
            await db.BackgroundJobs
                .Where(j => j.Id == job.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobStatus.Completed)
                    .SetProperty(j => j.CompletedAt, DateTime.UtcNow), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} ({Type}) failed", job.Id, job.Type);
            int attempts = job.Attempts + 1;
            bool retry = attempts < 3;
            await db.BackgroundJobs
                .Where(j => j.Id == job.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, retry ? JobStatus.Pending : JobStatus.Failed)
                    .SetProperty(j => j.Attempts, attempts)
                    .SetProperty(j => j.AvailableAt, DateTime.UtcNow.AddSeconds(10))
                    .SetProperty(j => j.LastError, ex.Message), CancellationToken.None);
        }
        return true;
    }

    private async Task CompileSubmission(AppDbContext db, Guid versionId, CancellationToken cancellationToken)
    {
        var version = await db.BotVersions.SingleAsync(v => v.Id == versionId, cancellationToken);
        version.Status = BuildStatus.Building;
        await db.SaveChangesAsync(cancellationToken);

        var sources = System.Text.Json.JsonSerializer.Deserialize<List<SourceFile>>(version.SourcesJson)!;
        try
        {
            var built = BotBuilder.BuildFromSources(sources, version.EntryType, $"version {version.VersionNumber}", quiet: true);
            SmokeTest(built.WasmPath);

            string stored = Path.Combine(DataPaths.Artifacts, built.ArtifactHash + ".wasm");
            if (!File.Exists(stored))
                File.Copy(built.WasmPath, stored);
            version.ArtifactPath = stored;
            version.ArtifactHash = built.ArtifactHash;
            version.Status = BuildStatus.Built;
            version.BuiltAt = DateTime.UtcNow;
            string logPath = Path.Combine(ToolchainInfo.CacheRoot, built.CacheKey[..24], "build.log");
            version.BuildLog = File.Exists(logPath) ? Tail(File.ReadAllText(logPath), 8000) : "(cached build)";

            // Pilot behavior: the newest successful build becomes the active version.
            var siblings = await db.BotVersions
                .Where(v => v.BotId == version.BotId && v.Id != version.Id)
                .ToListAsync(cancellationToken);
            foreach (var sibling in siblings)
                sibling.IsActive = false;
            version.IsActive = true;
        }
        catch (BotBuildException ex)
        {
            version.Status = BuildStatus.Failed;
            version.BuildLog = Tail(ex.BuildLog.Length > 0 ? ex.BuildLog : ex.Message, 8000);
        }
        catch (Exception ex)
        {
            version.Status = BuildStatus.Failed;
            version.BuildLog = Tail($"Artifact validation failed: {ex.Message}", 8000);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Minimal §15.4 artifact validation: the artifact must load, handshake and
    /// survive a short match against the built-in idle bot without crashing the host.</summary>
    private static void SmokeTest(string wasmPath)
    {
        string? builtin = RepoPaths.FindUpward(Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
        var map = LoadMap("basic-01");
        var rules = MatchRules with { MaxTicks = 5 };
        using var candidate = new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = wasmPath });
        using var idle = builtin is null
            ? (IBotRuntime)new Runtime.InProcessBotRuntime(() => new BotArena.Bots.BuiltIn.IdleBot())
            : new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = builtin, BotName = "idle" });
        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = 1,
            Participants =
            [
                new MatchParticipantConfig { Name = "candidate", Runtime = candidate },
                new MatchParticipantConfig { Name = "idle", Runtime = idle },
            ],
        });
        var candidateResult = run.Result.Bots[0];
        if (candidateResult.Faults >= rules.FaultLimit)
            throw new InvalidOperationException(
                "the bot faulted on every tick of the validation match " +
                "(it may crash at startup or return no action).");
    }

    private async Task ExecuteMatch(AppDbContext db, Guid matchId, CancellationToken cancellationToken)
    {
        var match = await db.Matches.Include(m => m.Participants)
            .SingleAsync(m => m.Id == matchId, cancellationToken);
        match.Status = MatchStatus.Running;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var participants = match.Participants.OrderBy(p => p.Slot).ToList();
            var versions = new List<BotVersion>();
            foreach (var participant in participants)
                versions.Add(await db.BotVersions.SingleAsync(v => v.Id == participant.BotVersionId, cancellationToken));

            // A set may pin its own ruleset (DECISIONS #54); unpinned sets and
            // setless matches play the server default.
            GameRules rules = MatchRules;
            if (match.MatchSetId is Guid rulesSetId &&
                await db.MatchSets.Where(s => s.Id == rulesSetId)
                    .Select(s => s.RulesName).SingleAsync(cancellationToken) is { Length: > 0 } pinned)
                rules = GameRules.Resolve(pinned);

            var runtimes = versions
                .Select(v => new WasmBotRuntime(new WasmRuntimeOptions
                {
                    ModulePath = v.ArtifactPath!,
                    BotName = v.GuestBotName ?? "",
                }))
                .ToList();
            try
            {
                var run = new MatchEngine().Run(new MatchConfiguration
                {
                    Map = LoadMap(match.MapId),
                    Rules = rules,
                    Seed = unchecked((ulong)match.Seed),
                    Participants = participants.Select((p, slot) => new MatchParticipantConfig
                    {
                        Name = p.NameSnapshot,
                        Runtime = runtimes[slot],
                        RuntimeKind = "wasm",
                        ArtifactHash = p.ArtifactHashSnapshot,
                        Accent = p.AccentSnapshot,
                    }).ToArray(),
                });

                string replayPath = Path.Combine(DataPaths.Replays, match.Id + ".json");
                await File.WriteAllTextAsync(replayPath, ReplaySerializer.ToJson(run.Replay), cancellationToken);

                match.ReplayPath = replayPath;
                match.ReplayHash = run.ReplayHash;
                match.GameRulesVersion = rules.RulesVersion; // actual, not creation-time default
                match.WinnerSlot = run.Result.WinnerSlot;
                match.EndReason = run.Result.Reason.ToString();
                match.EndTick = run.Result.EndTick;
                match.Status = MatchStatus.Completed;
                match.CompletedAt = DateTime.UtcNow;
                // Presentation timeline (plan §28): computed instantly, watched at human speed.
                match.BroadcastStartedAt = DateTime.UtcNow.AddSeconds(BroadcastDelaySeconds);
                match.PresentationTicksPerSecond = BroadcastTicksPerSecond;
                foreach (var participant in participants)
                {
                    var botResult = run.Result.Bots.Single(b => b.Slot == participant.Slot);
                    participant.Outcome = botResult.Outcome.ToString();
                    participant.FinalHealth = botResult.FinalHealth;
                    participant.DamageDealt = botResult.DamageDealt;
                    participant.Faults = botResult.Faults;
                }
            }
            finally
            {
                foreach (var runtime in runtimes)
                    runtime.Dispose();
            }
        }
        catch (Exception ex)
        {
            match.Status = MatchStatus.Failed;
            match.Error = ex.Message;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (match.MatchSetId is Guid setId)
            await TryFinalizeSet(db, setId, cancellationToken);
    }

    /// <summary>Applies Elo once all six games of a ranked set have executed (plan §36).
    /// The single-consumer worker makes this race-free.</summary>
    private static async Task TryFinalizeSet(AppDbContext db, Guid setId, CancellationToken cancellationToken)
    {
        var set = await db.MatchSets.SingleAsync(s => s.Id == setId, cancellationToken);
        if (set.Status != MatchSetStatus.Running)
            return;
        var games = await db.Matches.Include(m => m.Participants)
            .Where(m => m.MatchSetId == setId)
            .ToListAsync(cancellationToken);
        if (games.Count < MatchSet.Games ||
            games.Any(m => m.Status is MatchStatus.Pending or MatchStatus.Running))
            return;

        if (games.Any(m => m.Status == MatchStatus.Failed))
        {
            set.Status = MatchSetStatus.Failed;
            set.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        double scoreA = 0;
        foreach (var game in games)
        {
            if (game.WinnerSlot is int winner)
                scoreA += game.Participants.Single(p => p.Slot == winner).BotId == set.BotAId ? 1 : 0;
            else
                scoreA += 0.5;
        }
        set.ScoreA = scoreA;
        set.ScoreB = MatchSet.Games - scoreA;

        // Elo moves on the ladder of the rules the games were ACTUALLY played under
        // (DECISIONS #54): every rules version has its own ladder, created lazily.
        string ladder = games[0].GameRulesVersion;
        set.GameRulesVersion = ladder;
        var ratingA = await GetOrCreateRating(db, set.BotAId, ladder, cancellationToken);
        var ratingB = await GetOrCreateRating(db, set.BotBId, ladder, cancellationToken);
        set.RatingABefore = ratingA.Rating;
        set.RatingBBefore = ratingB.Rating;
        double expectedA = 1.0 / (1.0 + Math.Pow(10, (ratingB.Rating - ratingA.Rating) / 400.0));
        double change = MatchSet.EloK * (scoreA / MatchSet.Games - expectedA);
        set.RatingChangeA = change;
        set.RatingChangeB = -change;
        ratingA.Rating += change;
        ratingB.Rating -= change;
        ratingA.RankedSets++;
        ratingB.RankedSets++;
        set.WinnerBotId = scoreA > set.ScoreB ? set.BotAId : scoreA < set.ScoreB ? set.BotBId : null;
        set.Status = MatchSetStatus.Completed;
        set.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<BotRating> GetOrCreateRating(
        AppDbContext db, Guid botId, string rulesVersion, CancellationToken cancellationToken)
    {
        var rating = await db.BotRatings
            .SingleOrDefaultAsync(r => r.BotId == botId && r.RulesVersion == rulesVersion, cancellationToken);
        if (rating is null)
        {
            rating = new BotRating { BotId = botId, RulesVersion = rulesVersion };
            db.BotRatings.Add(rating); // explicit Add: pre-set Guid keys read as Modified otherwise
        }
        return rating;
    }

    private static ArenaMap LoadMap(string mapId)
    {
        string? path = RepoPaths.FindUpward(Path.Combine("maps", mapId + ".json"));
        if (path is null)
            throw new InvalidOperationException($"Map '{mapId}' not found.");
        return ArenaMap.FromJson(File.ReadAllText(path));
    }

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
