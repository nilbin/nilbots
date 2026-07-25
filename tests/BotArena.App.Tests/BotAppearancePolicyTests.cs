using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public class BotAppearancePolicyTests
{
    [Fact]
    public async Task Creation_DefaultsInvalidAccentAndAuthorizesStarterPair()
    {
        await using var db = CreateContext();
        var policy = new BotAppearancePolicy(
            new CosmeticEntitlementService(db, CosmeticCatalog.LoadDefault()));

        ApplicationResult<BotAppearance> result = await policy.ValidateForCreationAsync(
            Guid.NewGuid(),
            "not-a-color",
            null,
            null);

        Assert.True(result.Succeeded);
        Assert.Equal(AccentColor.DefaultValue, result.Value!.Accent.Value);
        Assert.Equal("vanguard", result.Value.BotLook.Value);
        Assert.Equal("pulse-bolt", result.Value.ProjectileLook.Value);
    }

    [Fact]
    public async Task UnknownLook_ReturnsStableTypedError()
    {
        await using var db = CreateContext();
        var policy = new BotAppearancePolicy(
            new CosmeticEntitlementService(db, CosmeticCatalog.LoadDefault()));

        ApplicationResult<BotAppearance> result = await policy.ValidateForUpdateAsync(
            Guid.NewGuid(),
            "#abcdef",
            "not-in-catalog",
            "pulse-bolt");

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorCodes.BotLookUnknown, result.Error!.Code);
        Assert.Equal(ApplicationErrorType.Validation, result.Error.Type);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().Options);
}
