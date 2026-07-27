using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GameModeDefinitionTests
{
    [Fact]
    public void ClosedModesSeparateCompleteScoreCatalogFromTimeoutRanking()
    {
        var deathmatchVictory = new DeathmatchVictoryDefinition(
            killsToWin: null,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ]);
        GameModeDefinition deathmatch = new DeathmatchGameModeDefinition(
            deathmatchVictory,
            [
                Channel(ScoreChannelDefinition.ChannelKind.ActiveHealth),
                Channel(ScoreChannelDefinition.ChannelKind.DamageDealt),
                Channel(ScoreChannelDefinition.ChannelKind.Deaths),
                Channel(ScoreChannelDefinition.ChannelKind.Kills),
            ],
            DeathmatchScoringDefinition.RawHostileKillV1);
        var frontlineVictory = new FrontlineVictoryDefinition(
            pushesToBreach: 3,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);
        GameModeDefinition frontline = new FrontlineGameModeDefinition(
            frontlineVictory,
            [
                Channel(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress),
            ],
            frontlinePositionCount: 5,
            capture: Capture());

        Assert.Equal(
            GameModeDefinition.GameModeDefinitionKind.Deathmatch,
            deathmatch.Kind);
        Assert.Equal(
            VictoryDefinition.VictoryDefinitionKind.Deathmatch,
            deathmatch.Victory.Kind);
        Assert.Equal(
            [
                ScoreChannelDefinition.ChannelKind.Kills,
                ScoreChannelDefinition.ChannelKind.Deaths,
                ScoreChannelDefinition.ChannelKind.DamageDealt,
                ScoreChannelDefinition.ChannelKind.ActiveHealth,
            ],
            deathmatch.ScoreCatalog
                .Select(channel => channel.Channel)
                .ToArray());
        Assert.Equal(
            [
                ScoreChannelDefinition.ChannelKind.Kills,
                ScoreChannelDefinition.ChannelKind.Deaths,
            ],
            deathmatch.Victory.TimeoutRanking
                .Select(reference => reference.Channel)
                .ToArray());
        Assert.Equal(
            DeathmatchVictoryDefinition.TerminalTickPrecedenceKind
                .KillLimitAfterCompleteJointTickBeforeMaxTickTimeout,
            deathmatchVictory.TerminalTickPrecedence);
        Assert.Equal(
            GameModeDefinition.GameModeDefinitionKind.Frontline,
            frontline.Kind);
        Assert.Equal(
            VictoryDefinition.VictoryDefinitionKind.Frontline,
            frontline.Victory.Kind);
        Assert.Equal(
            ScoreChannelDefinition.ChannelKind.TerritorialProgress,
            frontline.ScoreCatalog.Single().Channel);
        Assert.Equal(
            ScoreChannelDefinition.ValueDomain.Signed,
            frontline.ScoreCatalog.Single().Domain);
        Assert.Equal(
            ScoreChannelDefinition.ChannelKind.TerritorialProgress,
            frontline.Victory.TimeoutRanking.Single().Channel);
    }

    [Fact]
    public void CatalogInputOrderDoesNotAffectCanonicalMode()
    {
        ScoreChannelDefinition[] channels =
        [
            Channel(ScoreChannelDefinition.ChannelKind.Kills),
            Channel(ScoreChannelDefinition.ChannelKind.Deaths),
            Channel(ScoreChannelDefinition.ChannelKind.DamageDealt),
        ];
        var victory = new DeathmatchVictoryDefinition(
            killsToWin: 10,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);
        var forward = new DeathmatchGameModeDefinition(
            victory,
            channels.ToImmutableArray(),
            DeathmatchScoringDefinition.RawHostileKillV1);
        var reverse = new DeathmatchGameModeDefinition(
            victory,
            channels.Reverse().ToImmutableArray(),
            DeathmatchScoringDefinition.RawHostileKillV1);

        Assert.Equal(
            forward.ScoreCatalog.Select(channel => channel.Channel),
            reverse.ScoreCatalog.Select(channel => channel.Channel));
    }

    [Fact]
    public void ModeConstructorsRejectUndeclaredTimeoutReferences()
    {
        var victory = new DeathmatchVictoryDefinition(
            killsToWin: null,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ]);

        Assert.Throws<ArgumentException>(() =>
            new DeathmatchGameModeDefinition(
                victory,
                [Channel(ScoreChannelDefinition.ChannelKind.Kills)],
                DeathmatchScoringDefinition.RawHostileKillV1));
    }

    [Fact]
    public void ModesRejectScoreChannelsTheirKernelsCannotProduce()
    {
        var victory = new DeathmatchVictoryDefinition(
            killsToWin: null,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);

        Assert.Throws<ArgumentException>(() =>
            new DeathmatchGameModeDefinition(
                victory,
                [
                    Channel(ScoreChannelDefinition.ChannelKind.Kills),
                    Channel(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                DeathmatchScoringDefinition.RawHostileKillV1));

        Assert.Throws<ArgumentException>(() =>
            new FrontlineGameModeDefinition(
                FrontlineVictory(),
                [
                    Channel(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                    Channel(ScoreChannelDefinition.ChannelKind.ActiveHealth),
                ],
                frontlinePositionCount: 5,
                capture: Capture()));
    }

    [Fact]
    public void FrontlineRejectsAdditionalTimeoutTiebreakers()
    {
        var victory = new FrontlineVictoryDefinition(
            pushesToBreach: 3,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.ActiveHealth,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);

        Assert.Throws<ArgumentException>(() =>
            new FrontlineGameModeDefinition(
                victory,
                [
                    Channel(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                    Channel(ScoreChannelDefinition.ChannelKind.ActiveHealth),
                ],
                frontlinePositionCount: 5,
                capture: Capture()));
    }

    [Fact]
    public void ModeConstructorsRequireTheirPrimaryChannelFirst()
    {
        var wrongDeathmatchRanking = new DeathmatchVictoryDefinition(
            killsToWin: null,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);
        var missingDeathmatchChannel = new DeathmatchVictoryDefinition(
            killsToWin: null,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.ActiveHealth,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);
        var wrongFrontlineRanking = new FrontlineVictoryDefinition(
            pushesToBreach: 3,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ]);

        Assert.Throws<ArgumentException>(() =>
            new DeathmatchGameModeDefinition(
                wrongDeathmatchRanking,
                [
                    Channel(ScoreChannelDefinition.ChannelKind.Kills),
                    Channel(ScoreChannelDefinition.ChannelKind.Deaths),
                ],
                DeathmatchScoringDefinition.RawHostileKillV1));
        Assert.Throws<ArgumentException>(() =>
            new DeathmatchGameModeDefinition(
                missingDeathmatchChannel,
                [Channel(ScoreChannelDefinition.ChannelKind.ActiveHealth)],
                DeathmatchScoringDefinition.RawHostileKillV1));
        Assert.Throws<ArgumentException>(() =>
            new FrontlineGameModeDefinition(
                wrongFrontlineRanking,
                [
                    Channel(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 5,
                capture: Capture()));
    }

    [Fact]
    public void ScoreCatalogAndTimeoutRankingRejectDuplicates()
    {
        ScoreChannelDefinition kills =
            Channel(ScoreChannelDefinition.ChannelKind.Kills);
        ScoreRankingDefinition killsRanking = Ranking(
            ScoreChannelDefinition.ChannelKind.Kills,
            ScoreRankingDefinition.SortDirection.HigherWins);

        Assert.Throws<ArgumentException>(() =>
            new DeathmatchVictoryDefinition(
                killsToWin: null,
                [killsRanking, killsRanking]));
        Assert.Throws<ArgumentException>(() =>
            new DeathmatchGameModeDefinition(
                new DeathmatchVictoryDefinition(null, [killsRanking]),
                [kills, kills],
                DeathmatchScoringDefinition.RawHostileKillV1));
    }

    [Fact]
    public void SemanticIdsAreFixedAndScoreEnumsFailClosed()
    {
        var deathmatch = new DeathmatchGameModeDefinition(
            new DeathmatchVictoryDefinition(
                null,
                [
                    Ranking(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [Channel(ScoreChannelDefinition.ChannelKind.Kills)],
            DeathmatchScoringDefinition.RawHostileKillV1);

        Assert.Equal(DeathmatchGameModeDefinition.Id, deathmatch.ModeId);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoreChannelDefinition(
                (ScoreChannelDefinition.ChannelKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoreRankingDefinition(
                (ScoreChannelDefinition.ChannelKind)999,
                ScoreRankingDefinition.SortDirection.HigherWins));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoreRankingDefinition(
                ScoreChannelDefinition.ChannelKind.Kills,
                (ScoreRankingDefinition.SortDirection)999));
    }

    private static ScoreChannelDefinition Channel(
        ScoreChannelDefinition.ChannelKind channel) =>
        new(channel);

    private static ScoreRankingDefinition Ranking(
        ScoreChannelDefinition.ChannelKind channel,
        ScoreRankingDefinition.SortDirection direction) =>
        new(channel, direction);

    private static FrontlineCaptureDefinition Capture() =>
        new(
            threshold: 10,
            gainPerSoleTeamTick: 1,
            decayAmount: 1,
            decayIntervalTicks: 2,
            redeployPauseTicks: 3);

    private static FrontlineVictoryDefinition FrontlineVictory() =>
        new(
            pushesToBreach: 3,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);
}
