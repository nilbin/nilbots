using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Shared;

namespace BotArena.App.Tests;

public class MatchAdmissionServiceIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Admission_CentralizesOwnershipAppearanceVersionAndOwnerSnapshotData()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();

        var owner = new User
        {
            DisplayName = "Admission Owner",
            Email = "admission-owner@example.test",
            PasswordHash = "not-used",
        };
        var stranger = new User
        {
            DisplayName = "Admission Stranger",
            Email = "admission-stranger@example.test",
            PasswordHash = "not-used",
        };
        var ready = new Bot
        {
            OwnerUserId = owner.Id,
            Name = "Ready Bot",
            Slug = "ready-bot",
        };
        var unbuilt = new Bot
        {
            OwnerUserId = owner.Id,
            Name = "Unbuilt Bot",
            Slug = "unbuilt-bot",
        };
        var version = new BotVersion
        {
            BotId = ready.Id,
            VersionNumber = 1,
            EntryType = "Bot",
            SourcesJson = "[]",
            SourceHash = "source",
            Status = BuildStatus.Built,
            ArtifactHash = "artifact",
            IsActive = true,
        };
        db.Users.AddRange(owner, stranger);
        db.Bots.AddRange(ready, unbuilt);
        db.BotVersions.Add(version);
        await db.SaveChangesAsync();

        var entitlements = new CosmeticEntitlementService(
            db,
            CosmeticCatalog.LoadDefault());
        var service = new MatchAdmissionService(
            db,
            new BotAppearancePolicy(entitlements));

        ApplicationResult<AdmittedMatchBot> wrongOwner =
            await service.AdmitAsync(ready.Id, stranger.Id);
        Assert.False(wrongOwner.Succeeded);
        Assert.Equal(
            ApplicationErrorCodes.BotOwnershipRequired,
            wrongOwner.Error?.Code);

        ApplicationResult<AdmittedMatchBot> missingVersion =
            await service.AdmitAsync(unbuilt.Id, owner.Id);
        Assert.False(missingVersion.Succeeded);
        Assert.Equal(
            ApplicationErrorCodes.MatchActiveVersionRequired,
            missingVersion.Error?.Code);

        ApplicationResult<AdmittedMatchBot> admitted =
            await service.AdmitAsync(ready.Id, owner.Id);
        Assert.True(admitted.Succeeded);
        Assert.Equal(version.Id, admitted.Value?.Version.Id);
        Assert.Equal(owner.DisplayName, admitted.Value?.OwnerDisplayName);
    }
}
