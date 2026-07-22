using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// The single background worker (plan §22): claims jobs from the database with
/// FOR UPDATE SKIP LOCKED and runs compilation and match execution in-process.
/// Bot execution is already sandboxed by the WASM runtime, so no extra process
/// isolation is needed for matches; compilation runs dotnet in a child process.
/// </summary>
public sealed class JobWorker(IServiceScopeFactory scopeFactory, ILogger<JobWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Job worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork = false;
            try
            {
                didWork = await RunOneJob(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job worker iteration failed");
            }
            if (!didWork)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ContinueWith(_ => { });
        }
    }

    private async Task<bool> RunOneJob(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobs = await db.BackgroundJobs
            .FromSqlRaw("""
                UPDATE "BackgroundJobs" SET "Status" = 'Running', "LockedUntil" = now() + interval '10 minutes'
                WHERE "Id" = (
                    SELECT "Id" FROM "BackgroundJobs"
                    WHERE ("Status" = 'Pending' AND "AvailableAt" <= now())
                       OR ("Status" = 'Running' AND "LockedUntil" < now())
                    ORDER BY "Id"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
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
        var rules = GameRules.V0_1 with { MaxTicks = 5 };
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
                    Rules = GameRules.V0_1,
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
                match.WinnerSlot = run.Result.WinnerSlot;
                match.EndReason = run.Result.Reason.ToString();
                match.EndTick = run.Result.EndTick;
                match.Status = MatchStatus.Completed;
                match.CompletedAt = DateTime.UtcNow;
                // Presentation timeline (plan §28): computed instantly, watched at human speed.
                match.BroadcastStartedAt = DateTime.UtcNow.AddSeconds(3);
                match.PresentationTicksPerSecond = 5;
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
