using BotArena.App.Bots;
using BotArena.App.ArcRelay;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
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
    SubmissionContractProfileProbe contractProfileProbe,
    FrontlineLabsSettings labsSettings,
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
                SubmissionContractProfileProbe.Result profileProbe =
                    contractProfileProbe.Probe(built.WasmPath);
                bool customMind = await db.ArcRelayEntrants.AnyAsync(
                    entrant => entrant.MindBotId == version.BotId &&
                        entrant.Kind == ArcRelayEntrantKind.CustomMind,
                    cancellationToken);
                version.SupportedContractProfiles =
                    profileProbe.SupportedContractProfiles;
                if (profileProbe.SupportedContractProfiles.Length == 0)
                {
                    throw new InvalidOperationException(
                        "the artifact does not support any hosted contract " +
                        $"profile. {profileProbe.FailureSummary}");
                }
                if (customMind && !BotContractProfiles.Supports(
                        profileProbe.SupportedContractProfiles,
                        BotArena.Engine.BotArenaVersions.GenericMindContractProfileId))
                {
                    throw new InvalidOperationException(
                        "a custom Arc Relay mind must implement the generic-mind contract profile.");
                }
                if (!BotContractProfiles.CanActivateCompiledArtifact(
                        profileProbe.SupportedContractProfiles,
                        labsSettings.Enabled) && !customMind)
                {
                    throw new InvalidOperationException(
                        "generic-only artifacts cannot be activated while " +
                        "hosted Frontline Labs is disabled. Implement the " +
                        $"'{BotContractProfiles.LegacyDuel}' profile too, or " +
                        "submit again after Labs is enabled.");
                }

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
                        builtAt,
                        profileProbe.SupportedContractProfiles));
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

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
