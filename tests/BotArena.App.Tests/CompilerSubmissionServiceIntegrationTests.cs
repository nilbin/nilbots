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
    public async Task PostgreSqlAdmission_IsAtomicAcrossBotsForOneAccount()
    {
        string? connection = Environment.GetEnvironmentVariable("BOTARENA_TEST_DB");
        Skip.If(string.IsNullOrWhiteSpace(connection),
            "Set BOTARENA_TEST_DB to a disposable PostgreSQL database.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .UseOpenIddict()
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var user = new User
        {
            DisplayName = "quota-test",
            Email = "quota@example.test",
            PasswordHash = "not-used",
        };
        var bots = Enumerable.Range(1, 3)
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

        var limits = new CompilerSubmissionLimits(6, 30, 12, 60, 2, 20);
        var service = new CompilerSubmissionService(
            db,
            limits,
            new SubmissionNetwork("test-only-network-hmac-key-32-characters"));
        SourceFile[] source =
        [
            new(
                "Bot.cs",
                "using BotArena.Sdk; public sealed class Bot : IBot " +
                "{ public BotAction Tick(BotContext c) => Actions.Wait(); }"),
        ];

        CompilerSubmissionDecision first = await service.EnqueueAsync(
            bots[0].Id,
            user.Id,
            "Bot",
            source,
            IPAddress.Parse("203.0.113.10"),
            CancellationToken.None);
        CompilerSubmissionDecision second = await service.EnqueueAsync(
            bots[1].Id,
            user.Id,
            "Bot",
            source,
            IPAddress.Parse("203.0.113.11"),
            CancellationToken.None);
        CompilerSubmissionDecision denied = await service.EnqueueAsync(
            bots[2].Id,
            user.Id,
            "Bot",
            source,
            IPAddress.Parse("203.0.113.12"),
            CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.False(denied.Accepted);
        Assert.Contains("queued", denied.Denial!.Message);
        Assert.Equal(2, await db.BackgroundJobs.CountAsync());
    }
}
