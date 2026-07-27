using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public class GameModeDefinitionTests
{
    [Fact]
    public void ClosedModeVariantsCarryTypedVictoryAndScoreChannels()
    {
        var deathmatchVictory = new DeathmatchVictoryDefinition(
            killsToWin: null,
            [
                new(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreChannelDefinition.SortDirection.HigherWins),
            ]);
        GameModeDefinition deathmatch = new DeathmatchGameModeDefinition(
            deathmatchVictory,
            respawnDelayTicks: 3);
        var frontlineVictory = new FrontlineVictoryDefinition(
            pushesToBreach: 3,
            [
                new(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                    ScoreChannelDefinition.SortDirection.HigherWins),
            ]);
        GameModeDefinition frontline = new FrontlineGameModeDefinition(
            frontlineVictory,
            frontlinePositionCount: 5);

        Assert.Equal(
            GameModeDefinition.GameModeDefinitionKind.Deathmatch,
            deathmatch.Kind);
        Assert.Equal(
            VictoryDefinition.VictoryDefinitionKind.Deathmatch,
            deathmatch.Victory.Kind);
        Assert.Equal(
            ScoreChannelDefinition.ChannelKind.Kills,
            deathmatch.Victory.RankingChannels.Single().Channel);
        Assert.Equal(
            GameModeDefinition.GameModeDefinitionKind.Frontline,
            frontline.Kind);
        Assert.Equal(
            VictoryDefinition.VictoryDefinitionKind.Frontline,
            frontline.Victory.Kind);
    }

    [Fact]
    public void VictoryVariantsRejectWrongPrimaryScoreChannel()
    {
        ImmutableArray<ScoreChannelDefinition> health =
        [
            new(
                ScoreChannelDefinition.ChannelKind.ActiveHealth,
                ScoreChannelDefinition.SortDirection.HigherWins),
        ];

        Assert.Throws<ArgumentException>(() =>
            new DeathmatchVictoryDefinition(killsToWin: null, health));
        Assert.Throws<ArgumentException>(() =>
            new FrontlineVictoryDefinition(pushesToBreach: 3, health));
    }

    [Fact]
    public void SemanticIdsAreFixedAndScoreEnumsFailClosed()
    {
        var kills = new ScoreChannelDefinition(
            ScoreChannelDefinition.ChannelKind.Kills,
            ScoreChannelDefinition.SortDirection.HigherWins);
        var deathmatch = new DeathmatchGameModeDefinition(
            new DeathmatchVictoryDefinition(null, [kills]),
            respawnDelayTicks: 3);

        Assert.Equal(DeathmatchGameModeDefinition.Id, deathmatch.ModeId);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoreChannelDefinition(
                (ScoreChannelDefinition.ChannelKind)999,
                ScoreChannelDefinition.SortDirection.HigherWins));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoreChannelDefinition(
                ScoreChannelDefinition.ChannelKind.Kills,
                (ScoreChannelDefinition.SortDirection)999));
    }
}
