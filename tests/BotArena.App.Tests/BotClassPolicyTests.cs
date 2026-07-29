using BotArena.App.Bots;
using BotArena.App.Shared;

namespace BotArena.App.Tests;

public sealed class BotClassPolicyTests
{
    private readonly BotClassPolicy policy = new();

    [Fact]
    public void CreationWithoutClass_PreservesLegacyNull()
    {
        ApplicationResult<string?> result = policy.ValidateForCreation(null);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(" STRIKER ", "striker")]
    [InlineData("Bulwark", "bulwark")]
    [InlineData("fabricator", "fabricator")]
    public void KnownClass_IsCanonicalized(string input, string expected)
    {
        ApplicationResult<string?> result = policy.ValidateForAssignment(input);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("-striker")]
    [InlineData("strike_team")]
    [InlineData("strïker")]
    public void MalformedClass_ReturnsStableInvalidCode(string? input)
    {
        ApplicationResult<string?> result = policy.ValidateForAssignment(input);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorCodes.BotClassIdInvalid, result.Error!.Code);
        Assert.Equal(ApplicationErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void OversizedClass_ReturnsStableInvalidCode()
    {
        ApplicationResult<string?> result =
            policy.ValidateForAssignment(new string('a', 65));

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorCodes.BotClassIdInvalid, result.Error!.Code);
    }

    [Fact]
    public void WellFormedUnregisteredClass_ReturnsStableUnknownCode()
    {
        ApplicationResult<string?> result =
            policy.ValidateForAssignment("scout");

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorCodes.BotClassUnknown, result.Error!.Code);
        Assert.Equal(ApplicationErrorType.Validation, result.Error.Type);
    }
}
