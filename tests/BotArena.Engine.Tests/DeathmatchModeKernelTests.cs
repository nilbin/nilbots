using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class DeathmatchModeKernelTests
{
    [Fact]
    public void HostileLethalDamageCreditsExactSourceAndTargetTeams()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 7, 2);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            [
                new DeathmatchDamageContact(
                    sourceTeamId: 7,
                    targetTeamId: 2,
                    actualHealthRemoved: 3,
                    causedDestruction: true),
            ],
            Health((7, 0), (2, 0)),
            [7, 2]);

        Assert.False(result.KillLimitCompleted);
        Assert.Null(result.WinnerTeamId);
        AssertScore(result.ScoreState, 7, kills: 1, deaths: 0, damage: 3);
        AssertScore(result.ScoreState, 2, kills: 0, deaths: 1, damage: 0);
    }

    [Fact]
    public void OneKernelScoresFreeForAllWithoutSlotAssumptions()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 8, 1, 5);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            [
                Contact(8, 1, damage: 2),
                Contact(8, 1, damage: 1, lethal: true),
                Contact(1, 5, damage: 4, lethal: true),
                Contact(5, 8, damage: 0),
            ],
            Health((8, 2), (1, 3), (5, 0)),
            [8, 1, 5]);

        AssertScore(result.ScoreState, 8, kills: 1, deaths: 0, damage: 3);
        AssertScore(result.ScoreState, 1, kills: 1, deaths: 1, damage: 4);
        AssertScore(result.ScoreState, 5, kills: 0, deaths: 1, damage: 0);
    }

    [Fact]
    public void AlliedOrSelfDamageCreditsOnlyTheVictimDeath()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 1, 2);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            [
                Contact(1, 1, damage: 2),
                Contact(1, 1, damage: 1, lethal: true),
            ],
            Health((1, 0), (2, 3)),
            [1, 2]);

        AssertScore(result.ScoreState, 1, kills: 0, deaths: 1, damage: 0);
        AssertScore(result.ScoreState, 2, kills: 0, deaths: 0, damage: 0);
    }

    [Fact]
    public void UnattributedLethalDamageCreditsADeathButNoKillOrDamage()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 1, 2);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            kernel.CreateInitialState(),
            [
                new DeathmatchDamageContact(
                    sourceTeamId: null,
                    targetTeamId: 2,
                    actualHealthRemoved: 4,
                    causedDestruction: true),
            ],
            Health((1, 3), (2, 0)),
            [1, 2]);

        AssertScore(result.ScoreState, 1, kills: 0, deaths: 0, damage: 0);
        AssertScore(result.ScoreState, 2, kills: 0, deaths: 1, damage: 0);
    }

    [Fact]
    public void EmptyDamageBatchAddsNothingForNonDamageRetirement()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: 1, 1, 2);
        DeathmatchScoreState before = kernel.CreateInitialState();

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            before,
            [],
            Health((1, 1), (2, 1)),
            [1, 2]);

        Assert.False(result.KillLimitCompleted);
        Assert.Equal(Project(before), Project(result.ScoreState));
    }

    [Fact]
    public void SimultaneousMutualThresholdKillsCompleteAsTiedTopDraw()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: 10, 1, 2);
        var before = new DeathmatchScoreState(
            [
                Score(1, kills: 9),
                Score(2, kills: 9),
            ]);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            before,
            [
                Contact(1, 2, damage: 1, lethal: true),
                Contact(2, 1, damage: 1, lethal: true),
            ],
            Health((1, 0), (2, 0)),
            [1, 2]);

        Assert.True(result.KillLimitCompleted);
        Assert.Null(result.WinnerTeamId);
        Assert.Equal(
            [1, 1],
            result.KillLimitStandings!.Standings
                .Select(standing => standing.Rank)
                .ToArray());
        Assert.All(
            result.KillLimitStandings.Standings,
            standing => Assert.Equal(
                TeamStandingOutcome.Draw,
                standing.Outcome));
        AssertScore(result.ScoreState, 1, kills: 10, deaths: 1, damage: 1);
        AssertScore(result.ScoreState, 2, kills: 10, deaths: 1, damage: 1);
    }

    [Fact]
    public void KillLimitIsCheckedOnlyAfterTheWholeJointBatch()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: 10, 1, 2, 3);
        var before = new DeathmatchScoreState(
            [
                Score(1, kills: 9),
                Score(2, kills: 8),
                Score(3),
            ]);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            before,
            [
                Contact(1, 3, damage: 1, lethal: true),
                Contact(2, 1, damage: 2, lethal: true),
                Contact(1, 2, damage: 3, lethal: true),
            ],
            Health((1, 0), (2, 0), (3, 0)),
            [1, 2, 3]);

        Assert.True(result.KillLimitCompleted);
        Assert.Equal(1, result.WinnerTeamId);
        AssertScore(result.ScoreState, 1, kills: 11, deaths: 1, damage: 4);
        AssertScore(result.ScoreState, 2, kills: 9, deaths: 1, damage: 2);
        AssertScore(result.ScoreState, 3, kills: 0, deaths: 1, damage: 0);
    }

    [Fact]
    public void ReorderingAnAlreadyCanonicalBatchCannotChangeModeTotals()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: 3, 1, 2, 3);
        DeathmatchDamageContact[] contacts =
        [
            Contact(1, 2, damage: 2),
            Contact(3, 2, damage: 1, lethal: true),
            Contact(2, 1, damage: 3, lethal: true),
            Contact(1, 3, damage: 4, lethal: true),
        ];
        DeathmatchScoreState before = new(
            [
                Score(1, kills: 2),
                Score(2, kills: 2),
                Score(3, kills: 2),
            ]);

        DeathmatchJointTickResult forward = kernel.ApplyJointTick(
            before,
            contacts,
            Health((1, 0), (2, 0), (3, 0)),
            [1, 2, 3]);
        DeathmatchJointTickResult reverse = kernel.ApplyJointTick(
            before,
            contacts.Reverse().ToArray(),
            Health((3, 0), (2, 0), (1, 0)),
            [3, 2, 1]);

        Assert.Equal(Project(forward.ScoreState), Project(reverse.ScoreState));
        Assert.Equal(
            Project(forward.KillLimitStandings!),
            Project(reverse.KillLimitStandings!));
    }

    [Fact]
    public void TimeoutUsesDeclaredRankingAndCallerActiveHealthSnapshot()
    {
        DeathmatchModeKernel kernel = Kernel(
            killsToWin: null,
            rankings:
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.ActiveHealth,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ],
            teamIds: [4, 7, 2]);
        var scores = new DeathmatchScoreState(
            [
                Score(2, kills: 5, deaths: 1),
                Score(4, kills: 5, deaths: 4),
                Score(7, kills: 5, deaths: 0),
            ]);

        TeamStandings standings = kernel.ResolveTimeoutStandings(
            scores,
            Health((2, 3), (4, 9), (7, 9)),
            [2, 4, 7]);

        Assert.Equal(7, standings.WinnerTeamId);
        Assert.Equal(
            [7, 4, 2],
            standings.Standings
                .Select(standing => standing.TeamId)
                .ToArray());
        Assert.Equal(
            [1, 2, 3],
            standings.Standings
                .Select(standing => standing.Rank)
                .ToArray());
        Assert.Equal(
            9,
            ScoreValue(
                standings,
                teamId: 7,
                ScoreChannelDefinition.ChannelKind.ActiveHealth));
    }

    [Fact]
    public void TimeoutRanksTiesWithCompetitionRanks()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 10, 20, 30);
        var scores = new DeathmatchScoreState(
            [
                Score(10, kills: 4, deaths: 2),
                Score(20, kills: 4, deaths: 2),
                Score(30, kills: 3, deaths: 0),
            ]);

        TeamStandings standings = kernel.ResolveTimeoutStandings(
            scores,
            Health((10, 8), (20, 1), (30, 20)),
            [30, 20, 10]);

        Assert.Null(standings.WinnerTeamId);
        Assert.Equal(
            [1, 1, 3],
            standings.Standings
                .Select(standing => standing.Rank)
                .ToArray());
        Assert.Equal(
            [10, 20, 30],
            standings.Standings
                .Select(standing => standing.TeamId)
                .ToArray());
    }

    [Fact]
    public void IneligibleTeamsAreTiedBelowEveryEligibleTeam()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 1, 2, 3, 4);
        var scores = new DeathmatchScoreState(
            [
                Score(1, kills: 1),
                Score(2, kills: 100),
                Score(3, kills: 0),
                Score(4, kills: 200),
            ]);

        TeamStandings standings = kernel.ResolveTimeoutStandings(
            scores,
            Health((1, 1), (2, 50), (3, 1), (4, 50)),
            [3, 1]);

        Assert.Equal(1, standings.WinnerTeamId);
        Assert.Equal(
            ["1:1:Win", "2:3:Loss", "3:2:Loss", "3:4:Loss"],
            standings.Standings
                .Select(standing =>
                    $"{standing.Rank}:{standing.TeamId}:{standing.Outcome}")
                .ToArray());
    }

    [Fact]
    public void IneligibleKillsCannotTriggerOrWinTheEarlyLimit()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: 10, 1, 2, 3);
        var scores = new DeathmatchScoreState(
            [
                Score(1, kills: 9),
                Score(2, kills: 100),
                Score(3, kills: 8),
            ]);

        DeathmatchJointTickResult beforeEligibleThreshold =
            kernel.ApplyJointTick(
                scores,
                [],
                Health((1, 1), (2, 1), (3, 1)),
                [1, 3]);
        DeathmatchJointTickResult completed = kernel.ApplyJointTick(
            beforeEligibleThreshold.ScoreState,
            [Contact(1, 3, damage: 1, lethal: true)],
            Health((1, 1), (2, 1), (3, 0)),
            [1, 3]);

        Assert.False(beforeEligibleThreshold.KillLimitCompleted);
        Assert.True(completed.KillLimitCompleted);
        Assert.Equal(1, completed.WinnerTeamId);
        TeamStanding disqualified = completed
            .KillLimitStandings!
            .Standings
            .Single(standing => standing.TeamId == 2);
        Assert.Equal(3, disqualified.Rank);
        Assert.Equal(TeamStandingOutcome.Loss, disqualified.Outcome);
    }

    [Fact]
    public void NoEligibleTeamsProduceOneBottomTie()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: 1, 1, 2);
        var scores = new DeathmatchScoreState(
            [
                Score(1, kills: 100),
                Score(2, kills: 0),
            ]);

        DeathmatchJointTickResult tick = kernel.ApplyJointTick(
            scores,
            [],
            Health((1, 3), (2, 3)),
            []);
        TeamStandings timeout = kernel.ResolveTimeoutStandings(
            scores,
            Health((1, 3), (2, 3)),
            []);

        Assert.False(tick.KillLimitCompleted);
        Assert.Null(timeout.WinnerTeamId);
        Assert.All(
            timeout.Standings,
            standing =>
            {
                Assert.Equal(1, standing.Rank);
                Assert.Equal(TeamStandingOutcome.Draw, standing.Outcome);
            });
    }

    [Fact]
    public void CheckedOverflowFailsAtomically()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 1, 2);
        var damageAtLimit = new DeathmatchScoreState(
            [
                Score(1, damage: long.MaxValue),
                Score(2),
            ]);
        var killAtLimit = new DeathmatchScoreState(
            [
                Score(1, kills: long.MaxValue),
                Score(2),
            ]);
        var deathAtLimit = new DeathmatchScoreState(
            [
                Score(1),
                Score(2, deaths: long.MaxValue),
            ]);

        Assert.Throws<OverflowException>(() =>
            kernel.ApplyJointTick(
                damageAtLimit,
                [Contact(1, 2, damage: 1)],
                Health((1, 1), (2, 1)),
                [1, 2]));
        Assert.Throws<OverflowException>(() =>
            kernel.ApplyJointTick(
                killAtLimit,
                [Contact(1, 2, damage: 1, lethal: true)],
                Health((1, 1), (2, 0)),
                [1, 2]));
        Assert.Throws<OverflowException>(() =>
            kernel.ApplyJointTick(
                deathAtLimit,
                [Contact(1, 2, damage: 1, lethal: true)],
                Health((1, 1), (2, 0)),
                [1, 2]));

        Assert.Equal(
            ["1:0:0:9223372036854775807", "2:0:0:0"],
            Project(damageAtLimit));
        Assert.Equal(
            ["1:9223372036854775807:0:0", "2:0:0:0"],
            Project(killAtLimit));
        Assert.Equal(
            ["1:0:0:0", "2:0:9223372036854775807:0"],
            Project(deathAtLimit));
    }

    [Fact]
    public void NonnegativeLongBoundsAreAcceptedExactly()
    {
        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 1, 2);
        var before = new DeathmatchScoreState(
            [
                Score(1),
                Score(2),
            ]);

        DeathmatchJointTickResult result = kernel.ApplyJointTick(
            before,
            [Contact(1, 2, damage: long.MaxValue)],
            Health((1, long.MaxValue), (2, 0)),
            [1, 2]);

        AssertScore(
            result.ScoreState,
            1,
            kills: 0,
            deaths: 0,
            damage: long.MaxValue);
        Assert.Equal(
            long.MaxValue,
            ScoreValue(
                kernel.ResolveTimeoutStandings(
                    result.ScoreState,
                    Health((1, long.MaxValue), (2, 0)),
                    [1, 2]),
                1,
                ScoreChannelDefinition.ChannelKind.ActiveHealth));
    }

    [Fact]
    public void InvalidTopologyStateContactsAndSnapshotsAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Kernel(killsToWin: null, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeathmatchDamageContact(
                sourceTeamId: -1,
                targetTeamId: 2,
                actualHealthRemoved: 1,
                causedDestruction: false));
        Assert.Throws<ArgumentException>(() =>
            new DeathmatchDamageContact(
                sourceTeamId: 1,
                targetTeamId: 2,
                actualHealthRemoved: 0,
                causedDestruction: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Score(1, kills: -1));

        DeathmatchModeKernel kernel = Kernel(killsToWin: null, 1, 2);
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                new DeathmatchScoreState([Score(1), Score(3)]),
                [],
                Health((1, 1), (2, 1)),
                [1, 2]));
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                [Contact(3, 2, damage: 1)],
                Health((1, 1), (2, 1)),
                [1, 2]));
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                [],
                Health((1, 1)),
                [1, 2]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                [],
                Health((1, -1), (2, 1)),
                [1, 2]));
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                [],
                Health((1, 1), (2, 1)),
                [1, 1]));
        Assert.Throws<ArgumentException>(() =>
            kernel.ApplyJointTick(
                kernel.CreateInitialState(),
                [],
                Health((1, 1), (2, 1)),
                [1, 3]));
    }

    private static DeathmatchModeKernel Kernel(
        int? killsToWin,
        params int[] teamIds) =>
        Kernel(
            killsToWin,
            [
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                Ranking(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ],
            teamIds);

    private static DeathmatchModeKernel Kernel(
        int? killsToWin,
        ImmutableArray<ScoreRankingDefinition> rankings,
        params int[] teamIds) =>
        new(
            Topology(teamIds),
            new DeathmatchGameModeDefinition(
                new DeathmatchVictoryDefinition(killsToWin, rankings),
                [
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind.Kills),
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind.Deaths),
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind.DamageDealt),
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind.ActiveHealth),
                ],
                DeathmatchScoringDefinition.RawHostileKillV1));

    private static DeathmatchDamageContact Contact(
        int sourceTeamId,
        int targetTeamId,
        long damage,
        bool lethal = false) =>
        new(sourceTeamId, targetTeamId, damage, lethal);

    private static DeathmatchTeamScore Score(
        int teamId,
        long kills = 0,
        long deaths = 0,
        long damage = 0) =>
        new(teamId, kills, deaths, damage);

    private static ScoreRankingDefinition Ranking(
        ScoreChannelDefinition.ChannelKind channel,
        ScoreRankingDefinition.SortDirection direction) =>
        new(channel, direction);

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

    private static IReadOnlyDictionary<int, long> Health(
        params (int TeamId, long Health)[] values) =>
        values.ToDictionary(value => value.TeamId, value => value.Health);

    private static void AssertScore(
        DeathmatchScoreState state,
        int teamId,
        long kills,
        long deaths,
        long damage)
    {
        DeathmatchTeamScore score =
            state.Teams.Single(team => team.TeamId == teamId);
        Assert.Equal(kills, score.Kills);
        Assert.Equal(deaths, score.Deaths);
        Assert.Equal(damage, score.DamageDealt);
    }

    private static long ScoreValue(
        TeamStandings standings,
        int teamId,
        ScoreChannelDefinition.ChannelKind channel) =>
        standings.Standings
            .Single(standing => standing.TeamId == teamId)
            .Scores
            .Single(score => score.Channel == channel)
            .Value;

    private static string[] Project(DeathmatchScoreState state) =>
        state.Teams
            .Select(score =>
                $"{score.TeamId}:{score.Kills}:{score.Deaths}:{score.DamageDealt}")
            .ToArray();

    private static string[] Project(TeamStandings standings) =>
        standings.Standings
            .Select(standing =>
                $"{standing.Rank}:{standing.TeamId}:{standing.Outcome}:"
                + string.Join(
                    ",",
                    standing.Scores.Select(score =>
                        $"{score.Channel}={score.Value}")))
            .ToArray();
}
