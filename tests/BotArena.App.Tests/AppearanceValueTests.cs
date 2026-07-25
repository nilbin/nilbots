using BotArena.App.Bots;

namespace BotArena.App.Tests;

public class AppearanceValueTests
{
    [Theory]
    [InlineData(" Vanguard ", "vanguard")]
    [InlineData("ARC-SPARK", "arc-spark")]
    public void AppearanceId_NormalizesValidCatalogIdentifiers(
        string input,
        string expected)
    {
        Assert.True(AppearanceId.TryCreate(input, out AppearanceId value));
        Assert.Equal(expected, value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-vanguard")]
    [InlineData("vanguard-")]
    [InlineData("not_an_id")]
    [InlineData("two--dashes!")]
    public void AppearanceId_RejectsInvalidIdentifiers(string input)
    {
        Assert.False(AppearanceId.TryCreate(input, out _));
    }

    [Fact]
    public void AccentColor_NormalizesSixDigitHexToLowercase()
    {
        Assert.True(AccentColor.TryCreate(" #A1B2C3 ", out AccentColor value));
        Assert.Equal("#a1b2c3", value.Value);
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("a1b2c3")]
    [InlineData("#gg0000")]
    public void AccentColor_RejectsAnythingButSixDigitHex(string input)
    {
        Assert.False(AccentColor.TryCreate(input, out _));
    }
}
