using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

public sealed class CompileSubmissionJobHandler(
    AppDbContext db,
    IObjectStore objectStore,
    ISubmissionCompiler submissionCompiler,
    BuildProvenance buildProvenance,
    CosmeticEntitlementService entitlements,
    MatchExecutionSettings matchSettings,
    TimeProvider timeProvider)
{
    public async Task<JobExecutionResult> HandleAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        BotVersion version = await db.BotVersions.SingleAsync(
            candidate => candidate.Id == versionId,
            cancellationToken);
        if (version.Status == BuildStatus.Built &&
            version.ArtifactKey is { } existingKey &&
            await objectStore.ExistsAsync(existingKey, cancellationToken))
        {
            await AwardSuccessfulBuildAsync(version, cancellationToken);
            return new JobExecutionResult("already_built");
        }

        version.Status = BuildStatus.Building;
        await db.SaveChangesAsync(cancellationToken);

        List<SourceFile> sources =
            System.Text.Json.JsonSerializer.Deserialize<List<SourceFile>>(
                version.SourcesJson)!;
        try
        {
            try
            {
                CompiledSubmission built = await submissionCompiler.CompileAsync(
                    version.Id,
                    sources,
                    version.EntryType,
                    $"version {version.VersionNumber}",
                    cancellationToken);
                _ = WasmArtifactValidator.Validate(built.WasmPath);
                SmokeTest(built.WasmPath);

                string artifactKey = ObjectKeys.Artifact(built.ArtifactHash);
                await using (var stream = File.OpenRead(built.WasmPath))
                {
                    await objectStore.PutAsync(
                        artifactKey,
                        stream,
                        built.ArtifactHash,
                        cancellationToken);
                }

                version.ArtifactKey = artifactKey;
                version.ArtifactHash = built.ArtifactHash;
                version.Status = BuildStatus.Built;
                DateTime builtAt = timeProvider.GetUtcNow().UtcDateTime;
                version.BuiltAt = builtAt;
                version.BuildReceiptJson =
                    System.Text.Json.JsonSerializer.Serialize(new BuildReceipt(
                        version.Id,
                        version.SourceHash,
                        built.ArtifactHash,
                        new FileInfo(built.WasmPath).Length,
                        ToolchainInfo.SdkVersion,
                        ToolchainInfo.GuestAdapterVersion,
                        ToolchainInfo.IlcLlvmVersion,
                        ToolchainInfo.BuildPipelineVersion,
                        version.GameRulesVersion,
                        version.RuntimeProtocolVersion,
                        version.RuntimeConfigurationVersion,
                        buildProvenance.CompilerImageReference,
                        buildProvenance.GitCommit,
                        builtAt));
                version.BuildLog = Tail(built.BuildLog, 8000);

                List<BotVersion> siblings = await db.BotVersions
                    .Where(candidate =>
                        candidate.BotId == version.BotId &&
                        candidate.Id != version.Id)
                    .ToListAsync(cancellationToken);
                foreach (BotVersion sibling in siblings)
                    sibling.IsActive = false;
                version.IsActive = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (BotBuildException exception)
            {
                version.Status = BuildStatus.Failed;
                version.BuildLog = Tail(
                    exception.BuildLog.Length > 0
                        ? exception.BuildLog
                        : exception.Message,
                    8000);
            }
            catch (Exception exception)
            {
                version.Status = BuildStatus.Failed;
                version.BuildLog = Tail(
                    $"Artifact validation failed: {exception.Message}",
                    8000);
            }

            await db.SaveChangesAsync(cancellationToken);
            if (version.Status == BuildStatus.Built)
                await AwardSuccessfulBuildAsync(version, cancellationToken);
            return new JobExecutionResult(
                version.Status == BuildStatus.Built
                    ? "built"
                    : "build_failed");
        }
        finally
        {
            await submissionCompiler.CleanupAsync(version.Id);
        }
    }

    private async Task AwardSuccessfulBuildAsync(
        BotVersion version,
        CancellationToken cancellationToken)
    {
        Guid userId = await db.Bots
            .Where(bot => bot.Id == version.BotId)
            .Select(bot => bot.OwnerUserId)
            .SingleAsync(cancellationToken);
        await entitlements.GrantForEventAsync(
            userId,
            CosmeticUnlockEvents.Achievement,
            CosmeticUnlockEvents.FirstSuccessfulBuild,
            new { botVersionId = version.Id },
            cancellationToken);
    }

    private void SmokeTest(string wasmPath)
    {
        string? builtin = RepoPaths.FindUpward(
            Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
        GameRules rules = matchSettings.MatchRules with { MaxTicks = 5 };
        ArenaMap map = ArenaMapLoader.Load("basic-01", rules);
        using var candidate =
            new WasmBotRuntime(new WasmRuntimeOptions { ModulePath = wasmPath });
        using IBotRuntime idle = builtin is null
            ? new Runtime.InProcessBotRuntime(
                () => new BotArena.Bots.BuiltIn.IdleBot())
            : new WasmBotRuntime(new WasmRuntimeOptions
            {
                ModulePath = builtin,
                BotName = "idle",
            });
        MatchRunResult run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = 1,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = "candidate",
                    Runtime = candidate,
                },
                new MatchParticipantConfig
                {
                    Name = "idle",
                    Runtime = idle,
                },
            ],
        });
        BotMatchResult candidateResult = run.Result.Bots[0];
        if (candidateResult.Faults >= rules.FaultLimit)
        {
            throw new InvalidOperationException(
                "the bot faulted on every tick of the validation match " +
                "(it may crash at startup or return no action).");
        }
    }

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
