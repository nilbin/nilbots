using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record CompilerSubmissionDecision(
    BotVersion? Version,
    CompilerSubmissionDenial? Denial)
{
    public bool Accepted => Version is not null;
}

public sealed class CompilerSubmissionService(
    AppDbContext db,
    CompilerSubmissionLimits limits,
    SubmissionNetwork submissionNetwork,
    TimeProvider timeProvider)
{
    // Serializes the very short global queue-count check across web replicas.
    private const long GlobalAdmissionLock = 0x4e494c424f545301;

    public async Task<CompilerSubmissionDecision> EnqueueAsync(
        Guid botId,
        Guid userId,
        string entryType,
        IReadOnlyList<SourceFile> sources,
        IPAddress? remoteAddress,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        string networkHash = submissionNetwork.Hash(remoteAddress);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Always take locks in this order. Different users may arrive together,
        // but their global and account quota checks cannot race.
        await AcquireLock(GlobalAdmissionLock, cancellationToken);
        await AcquireLock(UserLock(userId), cancellationToken);

        bool ownsBot = await db.Bots.AnyAsync(
            b => b.Id == botId && b.OwnerUserId == userId,
            cancellationToken);
        if (!ownsBot)
            throw new InvalidOperationException("The bot no longer exists or changed owner.");

        DateTime tenMinutesAgo = now.AddMinutes(-10);
        DateTime dayAgo = now.AddHours(-24);
        var ownedVersions =
            from ownedVersion in db.BotVersions
            join bot in db.Bots on ownedVersion.BotId equals bot.Id
            where bot.OwnerUserId == userId
            select ownedVersion;

        var snapshot = new CompilerSubmissionSnapshot(
            BotHasActiveBuild: await db.BotVersions.AnyAsync(
                v => v.BotId == botId &&
                     (v.Status == BuildStatus.Pending || v.Status == BuildStatus.Building),
                cancellationToken),
            AccountTenMinuteCount: await ownedVersions.CountAsync(
                v => v.CreatedAt >= tenMinutesAgo,
                cancellationToken),
            AccountDailyCount: await ownedVersions.CountAsync(
                v => v.CreatedAt >= dayAgo,
                cancellationToken),
            NetworkTenMinuteCount: await db.BotVersions.CountAsync(
                v => v.SubmissionNetworkHash == networkHash && v.CreatedAt >= tenMinutesAgo,
                cancellationToken),
            NetworkDailyCount: await db.BotVersions.CountAsync(
                v => v.SubmissionNetworkHash == networkHash && v.CreatedAt >= dayAgo,
                cancellationToken),
            AccountQueuedCount: await ownedVersions.CountAsync(
                v => v.Status == BuildStatus.Pending,
                cancellationToken),
            GlobalQueuedCount: await db.BotVersions.CountAsync(
                v => v.Status == BuildStatus.Pending,
                cancellationToken));

        CompilerSubmissionDenial? denial = CompilerSubmissionPolicy.Evaluate(snapshot, limits);
        if (denial is not null)
            return new(null, denial);

        int latestVersion = await db.BotVersions
            .Where(v => v.BotId == botId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        string sourcesJson = JsonSerializer.Serialize(sources);
        var version = new BotVersion
        {
            BotId = botId,
            VersionNumber = latestVersion + 1,
            EntryType = entryType,
            SourcesJson = sourcesJson,
            SourceHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourcesJson))),
            SubmissionNetworkHash = networkHash,
            CreatedAt = now,
        };
        db.BotVersions.Add(version);
        db.BackgroundJobs.Add(BackgroundJob.CompileSubmission(version.Id));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(version, null);
    }

    private async Task AcquireLock(long key, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({key})",
            cancellationToken);
    }

    private static long UserLock(Guid userId) =>
        BinaryPrimitives.ReadInt64LittleEndian(userId.ToByteArray());
}
