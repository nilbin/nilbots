using System.Collections.Immutable;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BotArena.App.Tests;

public sealed class MatchResultPersistenceTests
{
    [Fact]
    public void ExpandFieldsPreserveHistoricalNulls()
    {
        using AppDbContext db = CreateModelContext();

        IProperty profiles =
            Property<BotVersion>(
                db,
                nameof(BotVersion.SupportedContractProfiles));
        Assert.Equal(typeof(string[]), profiles.ClrType);
        Assert.True(profiles.IsNullable);
        Assert.Equal("text[]", profiles.GetColumnType());

        IProperty teamId =
            Property<MatchParticipant>(
                db,
                nameof(MatchParticipant.TeamId));
        Assert.Equal(typeof(int?), teamId.ClrType);
        Assert.True(teamId.IsNullable);

        IProperty replayFormat =
            Property<Match>(
                db,
                nameof(Match.ReplayFormatVersion));
        Assert.Equal(typeof(int?), replayFormat.ClrType);
        Assert.True(replayFormat.IsNullable);
    }

    [Fact]
    public void ResultAndScoreKeysUseMatchLocalTeamIdentity()
    {
        using AppDbContext db = CreateModelContext();

        Assert.Equal(
            [nameof(MatchTeamResult.MatchId), nameof(MatchTeamResult.TeamId)],
            PrimaryKeyNames<MatchTeamResult>(db));
        Assert.Equal(
            [
                nameof(MatchTeamScore.MatchId),
                nameof(MatchTeamScore.TeamId),
                nameof(MatchTeamScore.ScoreChannelId),
            ],
            PrimaryKeyNames<MatchTeamScore>(db));

        IProperty score =
            Property<MatchTeamScore>(db, nameof(MatchTeamScore.Value));
        Assert.Equal(typeof(long), score.ClrType);
        Assert.Equal("bigint", score.GetColumnType());

        Assert.Equal(
            100,
            Property<MatchTeamScore>(
                db,
                nameof(MatchTeamScore.ScoreChannelId)).GetMaxLength());
        Assert.Equal(
            20,
            Property<MatchTeamResult>(
                db,
                nameof(MatchTeamResult.Outcome)).GetMaxLength());
    }

    [Fact]
    public void MatchResultRelationshipsCascadeAlongTheAggregate()
    {
        using AppDbContext db = CreateModelContext();

        IForeignKey resultMatch =
            ForeignKey<MatchTeamResult, Match>(db);
        Assert.Equal(
            [nameof(MatchTeamResult.MatchId)],
            PropertyNames(resultMatch.Properties));
        Assert.Equal(DeleteBehavior.Cascade, resultMatch.DeleteBehavior);

        IForeignKey scoreResult =
            ForeignKey<MatchTeamScore, MatchTeamResult>(db);
        Assert.Equal(
            [
                nameof(MatchTeamScore.MatchId),
                nameof(MatchTeamScore.TeamId),
            ],
            PropertyNames(scoreResult.Properties));
        Assert.Equal(DeleteBehavior.Cascade, scoreResult.DeleteBehavior);

        INavigation navigation =
            Entity<Match>(db).FindNavigation(nameof(Match.TeamResults))
            ?? throw new InvalidOperationException(
                $"{nameof(Match.TeamResults)} is not in the EF model.");
        Assert.Equal(typeof(MatchTeamResult), navigation.TargetEntityType.ClrType);
    }

    [Fact]
    public void MatchExpandChecksBoundTeamPlacementOutcomeAndReplayVersion()
    {
        using AppDbContext db = CreateModelContext();
        IModel model = db.GetService<IDesignTimeModel>().Model;

        AssertConstraint(
            model,
            typeof(Match),
            "CK_Matches_ReplayFormatVersion_Positive",
            "\"ReplayFormatVersion\" IS NULL OR \"ReplayFormatVersion\" > 0");
        AssertConstraint(
            model,
            typeof(MatchParticipant),
            "CK_MatchParticipants_TeamId_NonNegative",
            "\"TeamId\" IS NULL OR \"TeamId\" >= 0");
        AssertConstraint(
            model,
            typeof(MatchTeamResult),
            "CK_MatchTeamResults_TeamId_NonNegative",
            "\"TeamId\" >= 0");
        AssertConstraint(
            model,
            typeof(MatchTeamResult),
            "CK_MatchTeamResults_Placement_Positive",
            "\"Placement\" > 0");
        AssertConstraint(
            model,
            typeof(MatchTeamResult),
            "CK_MatchTeamResults_Outcome",
            "\"Outcome\" IN ('Win', 'Loss', 'Draw')");
    }

    [Fact]
    public void ApplyPreservesThreeTeamCompetitionTie()
    {
        Match match = MatchWithTeams(0, 1, 2);
        GenericActorMatchResult result = DeathmatchResult(
            [
                DeathmatchStanding(
                    teamId: 0,
                    rank: 1,
                    TeamStandingOutcome.Draw,
                    kills: 4),
                DeathmatchStanding(
                    teamId: 1,
                    rank: 1,
                    TeamStandingOutcome.Draw,
                    kills: 4),
                DeathmatchStanding(
                    teamId: 2,
                    rank: 3,
                    TeamStandingOutcome.Loss,
                    kills: 1),
            ],
            participantTeams: [0, 1, 2]);

        GenericMatchResultPersistence.Apply(match, result);

        Assert.Null(match.WinnerSlot);
        Assert.Equal("max-ticks", match.EndReason);
        Assert.Equal(19, match.EndTick);
        Assert.Collection(
            match.TeamResults,
            team =>
            {
                Assert.Equal(0, team.TeamId);
                Assert.Equal(1, team.Placement);
                Assert.Equal(MatchTeamOutcome.Draw, team.Outcome);
                Assert.Equal(
                    4,
                    team.Scores.Single(score =>
                        score.ScoreChannelId == "kills").Value);
            },
            team =>
            {
                Assert.Equal(1, team.TeamId);
                Assert.Equal(1, team.Placement);
                Assert.Equal(MatchTeamOutcome.Draw, team.Outcome);
            },
            team =>
            {
                Assert.Equal(2, team.TeamId);
                Assert.Equal(3, team.Placement);
                Assert.Equal(MatchTeamOutcome.Loss, team.Outcome);
            });
        Assert.Equal(
            new string?[] { "Draw", "Draw", "Loss" },
            match.Participants
                .OrderBy(participant => participant.Slot)
                .Select(participant => participant.Outcome)
                .ToArray());
    }

    [Fact]
    public void ApplyPreservesSignedFrontlineScores()
    {
        Match match = MatchWithTeams(0, 1);
        GenericActorMatchResult result = FrontlineResult(
            teamZeroScore: 7,
            teamOneScore: -7);

        GenericMatchResultPersistence.Apply(match, result);

        Assert.Equal(0, match.WinnerSlot);
        MatchTeamScore negative = Assert.Single(
            match.TeamResults.Single(team => team.TeamId == 1).Scores);
        Assert.Equal("territorial-progress", negative.ScoreChannelId);
        Assert.Equal(-7, negative.Value);
    }

    [Fact]
    public void ApplyLeavesDuelWinnerProjectionNullForMultiParticipantTeam()
    {
        Match match = MatchWithTeams(0, 0, 1);
        GenericActorMatchResult result = DeathmatchResult(
            [
                DeathmatchStanding(
                    teamId: 0,
                    rank: 1,
                    TeamStandingOutcome.Win,
                    kills: 5),
                DeathmatchStanding(
                    teamId: 1,
                    rank: 2,
                    TeamStandingOutcome.Loss,
                    kills: 2),
            ],
            participantTeams: [0, 0, 1]);

        GenericMatchResultPersistence.Apply(match, result);

        Assert.Null(match.WinnerSlot);
        Assert.All(
            match.Participants.Where(participant => participant.TeamId == 0),
            participant => Assert.Equal("Win", participant.Outcome));
        Assert.Equal(
            MatchTeamOutcome.Win,
            match.TeamResults.Single(team => team.TeamId == 0).Outcome);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task PostgreSqlRoundTripPreservesNormalizedSignedResult()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            Match match = MatchWithTeams(0, 1);
            GenericMatchResultPersistence.Apply(
                match,
                FrontlineResult(
                    teamZeroScore: long.MaxValue,
                    teamOneScore: long.MinValue));
            db.Matches.Add(match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        await using (AppDbContext db = database.CreateContext())
        {
            Match stored = await db.Matches
                .Include(match => match.Participants)
                .Include(match => match.TeamResults)
                    .ThenInclude(result => result.Scores)
                .SingleAsync(match => match.Id == matchId);

            Assert.Equal(0, stored.WinnerSlot);
            Assert.Equal(
                long.MaxValue,
                stored.TeamResults
                    .Single(team => team.TeamId == 0)
                    .Scores
                    .Single()
                    .Value);
            Assert.Equal(
                long.MinValue,
                stored.TeamResults
                    .Single(team => team.TeamId == 1)
                    .Scores
                    .Single()
                    .Value);
            Assert.Equal(
                new string?[] { "Win", "Loss" },
                stored.Participants
                    .OrderBy(participant => participant.Slot)
                    .Select(participant => participant.Outcome)
                    .ToArray());
        }
    }

    private static AppDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=botarena_model_only")
            .UseOpenIddict()
            .Options;
        return new AppDbContext(options);
    }

    private static IEntityType Entity<TEntity>(AppDbContext db) =>
        db.Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException(
            $"{typeof(TEntity).Name} is not in the EF model.");

    private static IProperty Property<TEntity>(
        AppDbContext db,
        string propertyName) =>
        Entity<TEntity>(db).FindProperty(propertyName)
        ?? throw new InvalidOperationException(
            $"{typeof(TEntity).Name}.{propertyName} is not in the EF model.");

    private static IReadOnlyList<string> PrimaryKeyNames<TEntity>(
        AppDbContext db) =>
        PropertyNames(
            Entity<TEntity>(db).FindPrimaryKey()?.Properties
            ?? throw new InvalidOperationException(
                $"{typeof(TEntity).Name} has no primary key."));

    private static IForeignKey ForeignKey<TEntity, TPrincipal>(
        AppDbContext db) =>
        Entity<TEntity>(db).GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TPrincipal));

    private static IReadOnlyList<string> PropertyNames(
        IEnumerable<IProperty> properties) =>
        properties.Select(property => property.Name).ToArray();

    private static void AssertConstraint(
        IModel model,
        Type entityType,
        string name,
        string sql)
    {
        IEntityType entity =
            model.FindEntityType(entityType)
            ?? throw new InvalidOperationException(
                $"{entityType.Name} is not in the EF design-time model.");
        ICheckConstraint constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate => candidate.Name == name);

        Assert.Equal(sql, constraint.Sql);
    }

    private static Match MatchWithTeams(params int[] teamIds)
    {
        var match = new Match
        {
            MapId = "result-persistence-test",
            Seed = 1,
        };
        for (int slot = 0; slot < teamIds.Length; slot++)
        {
            match.Participants.Add(new MatchParticipant
            {
                MatchId = match.Id,
                Slot = slot,
                TeamId = teamIds[slot],
                BotId = Guid.NewGuid(),
                BotVersionId = Guid.NewGuid(),
                NameSnapshot = $"Participant {slot}",
                AccentSnapshot = "#22d3ee",
            });
        }
        return match;
    }

    private static GenericActorMatchResult DeathmatchResult(
        IReadOnlyCollection<TeamStanding> standingValues,
        IReadOnlyList<int> participantTeams)
    {
        int[] teamIds = standingValues
            .Select(standing => standing.TeamId)
            .Order()
            .ToArray();
        var standings = new TeamStandings(
            Topology(teamIds),
            DeathmatchMode(),
            standingValues);
        var scores = new DeathmatchScoreState(
            standingValues
                .Select(standing =>
                    new DeathmatchTeamScore(
                        standing.TeamId,
                        standing.Scores.Single(score =>
                            score.Channel ==
                                ScoreChannelDefinition.ChannelKind.Kills)
                            .Value,
                        standing.Scores.Single(score =>
                            score.Channel ==
                                ScoreChannelDefinition.ChannelKind.Deaths)
                            .Value,
                        standing.Scores.Single(score =>
                            score.Channel ==
                                ScoreChannelDefinition.ChannelKind
                                    .DamageDealt)
                            .Value))
                .ToArray());
        return new GenericActorMatchResult(
            "max-ticks",
            endTick: 19,
            standings,
            eligibleTeamIds: teamIds,
            units: TerminalUnits(participantTeams),
            new GenericActorMatchModeResult.Deathmatch(
                GenericDeathmatchEndReason.MaxTicks,
                scores));
    }

    private static GenericActorMatchResult FrontlineResult(
        long teamZeroScore,
        long teamOneScore)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        FrontlineGameModeDefinition mode =
            Assert.IsType<FrontlineGameModeDefinition>(
                definition.Rules.GameMode);
        var standings = new TeamStandings(
            definition.Topology,
            mode,
            [
                new TeamStanding(
                    teamId: 0,
                    rank: 1,
                    TeamStandingOutcome.Win,
                    [
                        new TeamScoreValue(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            teamZeroScore),
                    ]),
                new TeamStanding(
                    teamId: 1,
                    rank: 2,
                    TeamStandingOutcome.Loss,
                    [
                        new TeamScoreValue(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            teamOneScore),
                    ]),
            ]);
        var scores = new FrontlineScoreState(
            [
                new FrontlineTeamScore(0, teamZeroScore),
                new FrontlineTeamScore(1, teamOneScore),
            ]);
        var control =
            new GenericActorRuntimeObservation.ModeObservationState.Frontline(
                FrontlineGameModeDefinition.Id,
                activePositionIndex:
                    mode.FrontlinePositionCount / 2,
                claimingTeamId: null,
                captureProgress: 0,
                decayTicksElapsed: 0,
                controlResumesAtTick: 0);
        return new GenericActorMatchResult(
            "max-ticks",
            endTick: 19,
            standings,
            eligibleTeamIds: [0, 1],
            units: TerminalUnits([0, 1]),
            new GenericActorMatchModeResult.Frontline(
                GenericFrontlineEndReason.MaxTicks,
                control,
                scores));
    }

    private static TeamStanding DeathmatchStanding(
        int teamId,
        int rank,
        TeamStandingOutcome outcome,
        long kills) =>
        new(
            teamId,
            rank,
            outcome,
            [
                new TeamScoreValue(
                    ScoreChannelDefinition.ChannelKind.Kills,
                    kills),
                new TeamScoreValue(
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    0),
                new TeamScoreValue(
                    ScoreChannelDefinition.ChannelKind.DamageDealt,
                    kills * 3),
                new TeamScoreValue(
                    ScoreChannelDefinition.ChannelKind.ActiveHealth,
                    0),
            ]);

    private static GenericActorMatchResult.UnitTerminalFact[]
        TerminalUnits(IReadOnlyList<int> participantTeams) =>
        participantTeams
            .Select((teamId, participantId) =>
                new GenericActorMatchResult.UnitTerminalFact(
                    new GenericActorWorldSnapshot.SlotSnapshot(
                        teamId,
                        unitId: participantTeams
                            .Take(participantId + 1)
                            .Count(team => team == teamId) - 1,
                        participantId,
                        nextLifeId: 0,
                        new GenericActorRuntimeObservation.UnitSlotState
                            .Ready(),
                        pendingParentActorId: null,
                        splitReservation: null),
                    activeLife: null))
            .ToArray();

    private static PublicMatchTopology Topology(
        IReadOnlyCollection<int> teamIds) =>
        new()
        {
            Teams = teamIds
                .Select(teamId => new PublicScoringTeam(teamId))
                .ToImmutableArray(),
            Participants = [],
            UnitSlots = [],
            InitialLives = [],
        };

    private static DeathmatchGameModeDefinition DeathmatchMode() =>
        new(
            new DeathmatchVictoryDefinition(
                killsToWin: null,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
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
            DeathmatchScoringDefinition.RawHostileKillV1);
}
