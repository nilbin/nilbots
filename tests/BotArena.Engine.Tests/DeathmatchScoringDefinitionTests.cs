namespace BotArena.Engine.Tests;

public sealed class DeathmatchScoringDefinitionTests
{
    [Fact]
    public void DisclosesRawAttributionAndRetirementExclusion()
    {
        DeathmatchScoringDefinition scoring =
            DeathmatchScoringDefinition.RawHostileKillV1;

        Assert.Equal(
            DeathmatchScoringDefinition.DeathIncrementKind
                .OneRawDeathToDestroyedActorTeamPerDamageCausedDestruction,
            scoring.DeathIncrement);
        Assert.Equal(
            DeathmatchScoringDefinition.KillIncrementKind
                .OneRawKillToExactHostileHealthToZeroDamageSourceTeam,
            scoring.KillIncrement);
        Assert.Equal(
            DeathmatchScoringDefinition.AlliedFinalDamageKind
                .VictimTeamDeathNoKill,
            scoring.AlliedFinalDamage);
        Assert.Equal(
            DeathmatchScoringDefinition.NonDamageRetirementKind
                .ReplicationRetirementAddsNeitherDeathNorKill,
            scoring.NonDamageRetirement);
        Assert.Equal(
            DeathmatchScoringDefinition.DamageDealtIncrementKind
                .HostileActualHealthRemovedToExactSourceTeam,
            scoring.DamageDealtIncrement);
        Assert.Equal(
            DeathmatchScoringDefinition.ActiveHealthSnapshotKind
                .TerminalSumAcrossActiveTeamLives,
            scoring.ActiveHealthSnapshot);
    }

    [Fact]
    public void MutualSimultaneousThresholdIsACompleteJointTickDraw()
    {
        DeathmatchGameModeDefinition mode = Mode(killsToWin: 10);

        Assert.Equal(10, mode.DeathmatchVictory.KillsToWin);
        Assert.Equal(
            DeathmatchScoringDefinition.EarlyKillLimitResolutionKind
                .CompleteJointTickThenHighestRawKillsWinTiedTopDraw,
            mode.Scoring.EarlyKillLimitResolution);
        Assert.Equal(
            [
                ScoreChannelDefinition.ChannelKind.Kills,
                ScoreChannelDefinition.ChannelKind.Deaths,
            ],
            mode.Victory.TimeoutRanking
                .Select(ranking => ranking.Channel)
                .ToArray());
    }

    [Fact]
    public void DeathmatchModeRequiresItsScoringContract()
    {
        DeathmatchVictoryDefinition victory = Victory(killsToWin: null);
        ScoreChannelDefinition[] scoreCatalog =
        [
            new(ScoreChannelDefinition.ChannelKind.Kills),
            new(ScoreChannelDefinition.ChannelKind.Deaths),
        ];
        DeathmatchScoringDefinition scoring =
            DeathmatchScoringDefinition.RawHostileKillV1;
        var mode = new DeathmatchGameModeDefinition(
            victory,
            [.. scoreCatalog],
            scoring);

        Assert.Same(scoring, mode.Scoring);
        Assert.Throws<ArgumentNullException>(() =>
            new DeathmatchGameModeDefinition(
                victory,
                [.. scoreCatalog],
                scoring: null!));
    }

    [Fact]
    public void RejectsUnknownPolicyEnums()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                deathIncrement:
                    (DeathmatchScoringDefinition.DeathIncrementKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                killIncrement:
                    (DeathmatchScoringDefinition.KillIncrementKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                alliedFinalDamage:
                    (DeathmatchScoringDefinition.AlliedFinalDamageKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                damageDealtIncrement:
                    (DeathmatchScoringDefinition
                        .DamageDealtIncrementKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                activeHealthSnapshot:
                    (DeathmatchScoringDefinition
                        .ActiveHealthSnapshotKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                nonDamageRetirement:
                    (DeathmatchScoringDefinition
                        .NonDamageRetirementKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Scoring(
                earlyKillLimitResolution:
                    (DeathmatchScoringDefinition
                        .EarlyKillLimitResolutionKind)99));
    }

    private static DeathmatchGameModeDefinition Mode(int? killsToWin) =>
        new(
            Victory(killsToWin),
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.Kills),
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.Deaths),
            ],
            DeathmatchScoringDefinition.RawHostileKillV1);

    private static DeathmatchVictoryDefinition Victory(int? killsToWin) =>
        new(
            killsToWin,
            [
                new ScoreRankingDefinition(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    ScoreRankingDefinition.SortDirection.HigherWins),
                new ScoreRankingDefinition(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    ScoreRankingDefinition.SortDirection.LowerWins),
            ]);

    private static DeathmatchScoringDefinition Scoring(
        DeathmatchScoringDefinition.DeathIncrementKind deathIncrement =
            DeathmatchScoringDefinition.DeathIncrementKind
                .OneRawDeathToDestroyedActorTeamPerDamageCausedDestruction,
        DeathmatchScoringDefinition.KillIncrementKind killIncrement =
            DeathmatchScoringDefinition.KillIncrementKind
                .OneRawKillToExactHostileHealthToZeroDamageSourceTeam,
        DeathmatchScoringDefinition.AlliedFinalDamageKind
            alliedFinalDamage =
                DeathmatchScoringDefinition.AlliedFinalDamageKind
                    .VictimTeamDeathNoKill,
        DeathmatchScoringDefinition.DamageDealtIncrementKind
            damageDealtIncrement =
                DeathmatchScoringDefinition.DamageDealtIncrementKind
                    .HostileActualHealthRemovedToExactSourceTeam,
        DeathmatchScoringDefinition.ActiveHealthSnapshotKind
            activeHealthSnapshot =
                DeathmatchScoringDefinition.ActiveHealthSnapshotKind
                    .TerminalSumAcrossActiveTeamLives,
        DeathmatchScoringDefinition.NonDamageRetirementKind
            nonDamageRetirement =
                DeathmatchScoringDefinition.NonDamageRetirementKind
                    .ReplicationRetirementAddsNeitherDeathNorKill,
        DeathmatchScoringDefinition.EarlyKillLimitResolutionKind
            earlyKillLimitResolution =
                DeathmatchScoringDefinition.EarlyKillLimitResolutionKind
                    .CompleteJointTickThenHighestRawKillsWinTiedTopDraw) =>
        new(
            deathIncrement,
            killIncrement,
            alliedFinalDamage,
            damageDealtIncrement,
            activeHealthSnapshot,
            nonDamageRetirement,
            earlyKillLimitResolution);
}
