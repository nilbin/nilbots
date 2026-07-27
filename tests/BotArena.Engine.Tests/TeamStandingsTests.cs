using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class TeamStandingsTests
{
    [Fact]
    public void AllUnorderedInputsProduceTheSameCanonicalStandings()
    {
        GameModeDefinition mode = DeathmatchMode();
        TeamStanding[] forwardStandings =
        [
            Standing(
                5,
                1,
                TeamStandingOutcome.Win,
                (ScoreChannelDefinition.ChannelKind.Kills, 6),
                (ScoreChannelDefinition.ChannelKind.Deaths, 2),
                (ScoreChannelDefinition.ChannelKind.DamageDealt, 18),
                (ScoreChannelDefinition.ChannelKind.ActiveHealth, 4)),
            Standing(
                9,
                2,
                TeamStandingOutcome.Loss,
                (ScoreChannelDefinition.ChannelKind.Kills, 4),
                (ScoreChannelDefinition.ChannelKind.Deaths, 3),
                (ScoreChannelDefinition.ChannelKind.DamageDealt, 12),
                (ScoreChannelDefinition.ChannelKind.ActiveHealth, 2)),
        ];
        var forward = new TeamStandings(
            Topology(5, 9),
            mode,
            forwardStandings);
        var reverse = new TeamStandings(
            Topology(9, 5),
            DeathmatchMode(reverseCatalog: true),
            forwardStandings
                .Reverse()
                .Select(ReverseScores)
                .ToArray());

        Assert.Equal(Project(forward), Project(reverse));
        Assert.Equal(
            [
                ScoreChannelDefinition.ChannelKind.Kills,
                ScoreChannelDefinition.ChannelKind.Deaths,
                ScoreChannelDefinition.ChannelKind.DamageDealt,
                ScoreChannelDefinition.ChannelKind.ActiveHealth,
            ],
            reverse.Standings[0].Scores
                .Select(score => score.Channel)
                .ToArray());
    }

    [Fact]
    public void RichScoreboardMayContainFactsNotUsedForTimeoutRanking()
    {
        GameModeDefinition mode = DeathmatchMode();

        Assert.Single(mode.Victory.TimeoutRanking);
        Assert.Equal(4, mode.ScoreCatalog.Length);

        var standings = new TeamStandings(
            Topology(0, 1),
            mode,
            [
                Standing(
                    0,
                    1,
                    TeamStandingOutcome.Win,
                    (ScoreChannelDefinition.ChannelKind.Kills, 8),
                    (ScoreChannelDefinition.ChannelKind.Deaths, 5),
                    (ScoreChannelDefinition.ChannelKind.DamageDealt, 21),
                    (ScoreChannelDefinition.ChannelKind.ActiveHealth, 1)),
                Standing(
                    1,
                    2,
                    TeamStandingOutcome.Loss,
                    (ScoreChannelDefinition.ChannelKind.Kills, 7),
                    (ScoreChannelDefinition.ChannelKind.Deaths, 1),
                    (ScoreChannelDefinition.ChannelKind.DamageDealt, 35),
                    (ScoreChannelDefinition.ChannelKind.ActiveHealth, 9)),
            ]);

        Assert.Equal(0, standings.WinnerTeamId);
        Assert.Equal(21, standings.Standings[0].Scores[2].Value);
        Assert.Equal(1, standings.Standings[0].Scores[3].Value);
    }

    [Fact]
    public void EarlyWinnerNeedNotMatchTimeoutScoreVector()
    {
        var standings = new TeamStandings(
            Topology(0, 1),
            DeathmatchMode(),
            [
                Standing(
                    0,
                    1,
                    TeamStandingOutcome.Win,
                    (ScoreChannelDefinition.ChannelKind.Kills, 3),
                    (ScoreChannelDefinition.ChannelKind.Deaths, 7),
                    (ScoreChannelDefinition.ChannelKind.DamageDealt, 9),
                    (ScoreChannelDefinition.ChannelKind.ActiveHealth, 1)),
                Standing(
                    1,
                    2,
                    TeamStandingOutcome.Loss,
                    (ScoreChannelDefinition.ChannelKind.Kills, 99),
                    (ScoreChannelDefinition.ChannelKind.Deaths, 0),
                    (ScoreChannelDefinition.ChannelKind.DamageDealt, 500),
                    (ScoreChannelDefinition.ChannelKind.ActiveHealth, 20)),
            ]);

        Assert.Equal(0, standings.WinnerTeamId);
        Assert.Equal(3, standings.Standings[0].Scores[0].Value);
        Assert.Equal(99, standings.Standings[1].Scores[0].Value);
    }

    [Fact]
    public void TerritorialProgressAcceptsSignedLongExtremes()
    {
        var mode = new FrontlineGameModeDefinition(
            new FrontlineVictoryDefinition(
                pushesToBreach: 3,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.TerritorialProgress),
            ],
            frontlinePositionCount: 5,
            capture: new FrontlineCaptureDefinition(
                threshold: 10,
                gainPerSoleTeamTick: 1,
                decayAmount: 1,
                decayIntervalTicks: 2,
                redeployPauseTicks: 3));
        var standings = new TeamStandings(
            Topology(10, 20),
            mode,
            [
                Standing(
                    20,
                    2,
                    TeamStandingOutcome.Loss,
                    (
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress,
                        long.MaxValue)),
                Standing(
                    10,
                    1,
                    TeamStandingOutcome.Win,
                    (
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress,
                        long.MinValue)),
            ]);

        Assert.Equal(
            ScoreChannelDefinition.ValueDomain.Signed,
            mode.ScoreCatalog.Single().Domain);
        Assert.Single(mode.FrontlineVictory.TimeoutRanking);
        Assert.Equal(long.MinValue, standings.Standings[0].Scores[0].Value);
        Assert.Equal(long.MaxValue, standings.Standings[1].Scores[0].Value);
    }

    [Theory]
    [InlineData(ScoreChannelDefinition.ChannelKind.Kills)]
    [InlineData(ScoreChannelDefinition.ChannelKind.Deaths)]
    [InlineData(ScoreChannelDefinition.ChannelKind.DamageDealt)]
    [InlineData(ScoreChannelDefinition.ChannelKind.ActiveHealth)]
    public void NonnegativeScoreChannelsRejectNegativeValues(
        ScoreChannelDefinition.ChannelKind channel)
    {
        var definition = new ScoreChannelDefinition(channel);

        Assert.Equal(
            ScoreChannelDefinition.ValueDomain.NonNegative,
            definition.Domain);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TeamScoreValue(channel, -1));
        Assert.Equal(0, new TeamScoreValue(channel, 0).Value);
        Assert.Equal(
            long.MaxValue,
            new TeamScoreValue(channel, long.MaxValue).Value);
    }

    [Fact]
    public void ScoreValuesRejectUnknownChannelKinds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TeamScoreValue(
                (ScoreChannelDefinition.ChannelKind)999,
                value: 0));
    }

    [Fact]
    public void ScoreboardsMustExactlyCoverTheDeclaredCatalog()
    {
        Assert.Throws<ArgumentException>(() =>
            new TeamStanding(
                teamId: 0,
                rank: 1,
                TeamStandingOutcome.Win,
                [
                    new TeamScoreValue(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        2),
                    new TeamScoreValue(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        1),
                ]));

        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 1),
                DeathmatchMode(),
                [
                    Standing(
                        0,
                        1,
                        TeamStandingOutcome.Win,
                        (ScoreChannelDefinition.ChannelKind.Kills, 2),
                        (ScoreChannelDefinition.ChannelKind.Deaths, 0),
                        (
                            ScoreChannelDefinition.ChannelKind.DamageDealt,
                            6),
                        (
                            ScoreChannelDefinition.ChannelKind.ActiveHealth,
                            3)),
                    Standing(
                        1,
                        2,
                        TeamStandingOutcome.Loss,
                        (ScoreChannelDefinition.ChannelKind.Kills, 1),
                        (ScoreChannelDefinition.ChannelKind.Deaths, 1),
                        (
                            ScoreChannelDefinition.ChannelKind.DamageDealt,
                            3)),
                ]));
    }

    [Fact]
    public void StandingsMustCoverExactlyUniqueTopologyTeams()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Standing(
                -1,
                1,
                TeamStandingOutcome.Win,
                (ScoreChannelDefinition.ChannelKind.Kills, 1)));
        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 0),
                DeathmatchMode(),
                FullStandings(0, 1)));
        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 1),
                DeathmatchMode(),
                FullStandings(0, 2)));
        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 1),
                DeathmatchMode(),
                [
                    FullStanding(0, 1, TeamStandingOutcome.Win),
                    FullStanding(0, 2, TeamStandingOutcome.Loss),
                ]));
    }

    [Fact]
    public void CompetitionRanksAndOutcomesAreValidatedWithoutScoreInference()
    {
        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 1, 2),
                DeathmatchMode(),
                [
                    FullStanding(0, 1, TeamStandingOutcome.Draw),
                    FullStanding(1, 1, TeamStandingOutcome.Draw),
                    FullStanding(2, 2, TeamStandingOutcome.Loss),
                ]));
        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 1),
                DeathmatchMode(),
                [
                    FullStanding(0, 1, TeamStandingOutcome.Draw),
                    FullStanding(1, 2, TeamStandingOutcome.Loss),
                ]));
        Assert.Throws<ArgumentException>(() =>
            new TeamStandings(
                Topology(0, 1, 2),
                DeathmatchMode(),
                [
                    FullStanding(0, 1, TeamStandingOutcome.Win),
                    FullStanding(1, 1, TeamStandingOutcome.Draw),
                    FullStanding(2, 3, TeamStandingOutcome.Loss),
                ]));
    }

    [Fact]
    public void TiedTopRanksHaveNoUniqueWinner()
    {
        var standings = new TeamStandings(
            Topology(30, 10, 20),
            DeathmatchMode(),
            [
                FullStanding(30, 3, TeamStandingOutcome.Loss),
                FullStanding(20, 1, TeamStandingOutcome.Draw),
                FullStanding(10, 1, TeamStandingOutcome.Draw),
            ]);

        Assert.Null(standings.WinnerTeamId);
        Assert.Equal(
            [10, 20, 30],
            standings.Standings.Select(standing => standing.TeamId).ToArray());
        Assert.Equal(
            [1, 1, 3],
            standings.Standings.Select(standing => standing.Rank).ToArray());
    }

    private static DeathmatchGameModeDefinition DeathmatchMode(
        bool reverseCatalog = false)
    {
        ScoreChannelDefinition[] catalog =
        [
            new(ScoreChannelDefinition.ChannelKind.Kills),
            new(ScoreChannelDefinition.ChannelKind.Deaths),
            new(ScoreChannelDefinition.ChannelKind.DamageDealt),
            new(ScoreChannelDefinition.ChannelKind.ActiveHealth),
        ];
        if (reverseCatalog)
            Array.Reverse(catalog);

        return new(
            new DeathmatchVictoryDefinition(
                killsToWin: 10,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            catalog.ToImmutableArray(),
            DeathmatchScoringDefinition.RawHostileKillV1);
    }

    private static TeamStanding FullStanding(
        int teamId,
        int rank,
        TeamStandingOutcome outcome) =>
        Standing(
            teamId,
            rank,
            outcome,
            (ScoreChannelDefinition.ChannelKind.Kills, teamId + 1L),
            (ScoreChannelDefinition.ChannelKind.Deaths, teamId + 2L),
            (ScoreChannelDefinition.ChannelKind.DamageDealt, teamId + 3L),
            (ScoreChannelDefinition.ChannelKind.ActiveHealth, teamId + 4L));

    private static TeamStanding[] FullStandings(
        int winnerTeamId,
        int loserTeamId) =>
        [
            FullStanding(winnerTeamId, 1, TeamStandingOutcome.Win),
            FullStanding(loserTeamId, 2, TeamStandingOutcome.Loss),
        ];

    private static TeamStanding Standing(
        int teamId,
        int rank,
        TeamStandingOutcome outcome,
        params (ScoreChannelDefinition.ChannelKind Channel, long Value)[]
            scores) =>
        new(
            teamId,
            rank,
            outcome,
            scores
                .Select(score => new TeamScoreValue(
                    score.Channel,
                    score.Value))
                .ToArray());

    private static TeamStanding ReverseScores(TeamStanding standing) =>
        new(
            standing.TeamId,
            standing.Rank,
            standing.Outcome,
            standing.Scores.Reverse().ToArray());

    private static string[] Project(TeamStandings standings) =>
        standings.Standings
            .Select(standing =>
                $"{standing.Rank}:{standing.TeamId}:{standing.Outcome}:"
                + string.Join(
                    ",",
                    standing.Scores.Select(score =>
                        $"{(int)score.Channel}={score.Value}")))
            .ToArray();

    private static PublicMatchTopology Topology(params int[] teamIds) =>
        new()
        {
            Teams = teamIds
                .Select(teamId => new PublicScoringTeam(teamId))
                .ToImmutableArray(),
            Participants = [],
            UnitSlots = [],
            InitialLives = [],
        };
}
