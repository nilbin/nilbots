using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class DuelArenaDefinitionTests
{
    [Fact]
    public void OfficialDefinitionPinsExistingArenaChoices()
    {
        DuelArenaDefinition definition = DuelArenaDefinition.Official;

        Assert.Equal(1, DuelArenaDefinition.UnrankedGamesPerMatch);
        Assert.Equal("arena-01", definition.DefaultUnrankedMapId);
        Assert.Equal(
            [
                "basic-01",
                "arena-01",
                "crossfire-01",
                "bastion-01",
                "gallery-01",
            ],
            definition.RankedMapPool);
        Assert.Same(DuelMirrored6V1.Instance, definition.RankedSeriesPolicy);
    }

    [Theory]
    [InlineData(null, "arena-01")]
    [InlineData("", "arena-01")]
    [InlineData("basic-01", "basic-01")]
    [InlineData(" ", " ")]
    public void UnrankedMapResolutionPreservesChallengeRequestSemantics(
        string? requestedMapId,
        string expected)
    {
        Assert.Equal(
            expected,
            DuelArenaDefinition.Official.ResolveUnrankedMapId(requestedMapId));
    }
}
