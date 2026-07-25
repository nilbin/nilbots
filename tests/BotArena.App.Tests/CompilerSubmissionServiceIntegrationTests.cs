using System.Net;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Shared;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public class CompilerSubmissionServiceIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task PostgreSqlAdmission_IsAtomicAcrossTwoConnectionsForOneAccount()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();
        var user = new User
        {
            DisplayName = "quota-test",
            Email = "quota@example.test",
            PasswordHash = "not-used",
        };
        var bots = Enumerable.Range(1, 2)
            .Select(index => new Bot
            {
                OwnerUserId = user.Id,
                Name = $"Quota {index}",
                Slug = $"quota-{index}",
            })
            .ToArray();
        db.Users.Add(user);
        db.Bots.AddRange(bots);
        await db.SaveChangesAsync();

        var limits = new CompilerSubmissionLimits(6, 30, 12, 60, 1, 20);
        SourceFile[] source =
        [
            new(
                "Bot.cs",
                "using BotArena.Sdk; public sealed class Bot : IBot " +
                "{ public BotAction Tick(BotContext c) => Actions.Wait(); }"),
        ];

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<CompilerSubmissionDecision>[] attempts = bots.Select((bot, index) =>
            SubmitFromIndependentConnection(bot.Id, index)).ToArray();
        gate.SetResult();
        CompilerSubmissionDecision[] decisions = await Task.WhenAll(attempts);

        Assert.Single(decisions, decision => decision.Accepted);
        CompilerSubmissionDecision denied = Assert.Single(
            decisions,
            decision => !decision.Accepted);
        Assert.Contains("queued", denied.Denial!.Message);
        Assert.Equal(1, await db.BackgroundJobs.CountAsync());

        async Task<CompilerSubmissionDecision> SubmitFromIndependentConnection(
            Guid botId,
            int index)
        {
            await gate.Task;
            await using AppDbContext connection = database.CreateContext();
            var service = new CompilerSubmissionService(
                connection,
                limits,
                new SubmissionNetwork("test-only-network-hmac-key-32-characters"),
                TimeProvider.System);
            return await service.EnqueueAsync(
                botId,
                user.Id,
                "Bot",
                source,
                IPAddress.Parse($"203.0.113.{index + 10}"),
                CancellationToken.None);
        }
    }
}
