using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace BotArena.App.Tests;

public sealed class CompetitionPersistenceTests
{
    [Fact]
    public void ExpandModelKeepsLegacyIdentityLinksNullable()
    {
        using AppDbContext db = CreateModelContext();

        Assert.Equal(
            typeof(Guid?),
            Property<MatchSet>(db, nameof(MatchSet.PlaylistVersionId))
                .ClrType);
        Assert.Equal(
            typeof(Guid?),
            Property<MatchSet>(db, nameof(MatchSet.LadderId)).ClrType);
        Assert.Equal(
            typeof(Guid?),
            Property<Match>(db, nameof(Match.PlaylistVersionId)).ClrType);
        Assert.Equal(
            typeof(Guid?),
            Property<BotRating>(db, nameof(BotRating.LadderId)).ClrType);
        Assert.Equal(
            100,
            Property<BotRating>(
                db,
                nameof(BotRating.RulesVersion)).GetMaxLength());
        Assert.Equal(
            100,
            Property<Ladder>(
                db,
                nameof(Ladder.LegacyRulesVersion)).GetMaxLength());
        Assert.Equal(
            typeof(int?),
            Property<BotRating>(
                db,
                nameof(BotRating.SeasonOpeningRank)).ClrType);
    }

    [Fact]
    public void ModelKeepsLegacyIndexesAlongsideOpaqueLadderIndexes()
    {
        using AppDbContext db = CreateModelContext();
        IEntityType rating = Entity<BotRating>(db);

        Assert.Contains(
            rating.GetIndexes(),
            index =>
                index.IsUnique &&
                PropertyNames(index).SequenceEqual(
                    [nameof(BotRating.BotId), nameof(BotRating.RulesVersion)]));
        Assert.Contains(
            rating.GetIndexes(),
            index =>
                index.IsUnique &&
                index.GetFilter() == "\"LadderId\" IS NOT NULL" &&
                PropertyNames(index).SequenceEqual(
                    [nameof(BotRating.BotId), nameof(BotRating.LadderId)]));
        Assert.Contains(
            rating.GetIndexes(),
            index =>
                index.GetFilter() == "\"LadderId\" IS NOT NULL" &&
                PropertyNames(index).SequenceEqual(
                    [
                        nameof(BotRating.LadderId),
                        nameof(BotRating.Rating),
                        nameof(BotRating.BotId),
                    ]));
    }

    [Fact]
    public void SeasonOpeningRankIsPositiveWhenPresent()
    {
        using AppDbContext db = CreateModelContext();
        IModel designTimeModel = db.GetService<IDesignTimeModel>().Model;
        IEntityType rating =
            designTimeModel.FindEntityType(typeof(BotRating))
            ?? throw new InvalidOperationException(
                $"{nameof(BotRating)} is not in the EF design-time model.");
        ICheckConstraint constraint = Assert.Single(
            rating.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_BotRatings_SeasonOpeningRank_Positive");

        Assert.Equal(
            "\"SeasonOpeningRank\" IS NULL OR \"SeasonOpeningRank\" > 0",
            constraint.Sql);

        ICheckConstraint ladderConstraint = Assert.Single(
            rating.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_BotRatings_SeasonOpeningRank_RequiresLadder");
        Assert.Equal(
            "\"SeasonOpeningRank\" IS NULL OR \"LadderId\" IS NOT NULL",
            ladderConstraint.Sql);
    }

    [Fact]
    public void MatchSetCompetitionIdentityIsAllOrNothing()
    {
        using AppDbContext db = CreateModelContext();
        IModel designTimeModel = db.GetService<IDesignTimeModel>().Model;
        IEntityType set =
            designTimeModel.FindEntityType(typeof(MatchSet))
            ?? throw new InvalidOperationException(
                $"{nameof(MatchSet)} is not in the EF design-time model.");
        ICheckConstraint constraint = Assert.Single(
            set.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_MatchSets_CompetitionIdentity_Paired");

        Assert.Equal(
            "(\"PlaylistVersionId\" IS NULL AND \"LadderId\" IS NULL) OR " +
            "(\"PlaylistVersionId\" IS NOT NULL AND \"LadderId\" IS NOT NULL)",
            constraint.Sql);
    }

    [Fact]
    public void PlaylistAndLadderConstraintsEncodeIdentityBoundaries()
    {
        using AppDbContext db = CreateModelContext();
        IEntityType playlist = Entity<Playlist>(db);
        IEntityType version = Entity<PlaylistVersion>(db);
        IEntityType season = Entity<Season>(db);
        IEntityType ladder = Entity<Ladder>(db);

        Assert.Contains(
            playlist.GetIndexes(),
            index =>
                index.IsUnique &&
                PropertyNames(index).SequenceEqual([nameof(Playlist.Key)]));
        Assert.Contains(
            version.GetIndexes(),
            index =>
                index.IsUnique &&
                PropertyNames(index).SequenceEqual(
                    [
                        nameof(PlaylistVersion.PlaylistId),
                        nameof(PlaylistVersion.Version),
                    ]));
        Assert.Contains(
            season.GetIndexes(),
            index =>
                index.IsUnique &&
                PropertyNames(index).SequenceEqual([nameof(Season.Key)]));
        Assert.Contains(
            ladder.GetIndexes(),
            index =>
                index.IsUnique &&
                index.GetFilter() == "\"Status\" = 'Open'" &&
                PropertyNames(index).SequenceEqual(
                    [nameof(Ladder.PlaylistVersionId)]));
        Assert.Contains(
            ladder.GetIndexes(),
            index =>
                index.IsUnique &&
                index.GetFilter() == "\"LegacyRulesVersion\" IS NOT NULL" &&
                PropertyNames(index).SequenceEqual(
                    [nameof(Ladder.LegacyRulesVersion)]));

        Assert.All(
            new[]
            {
                ForeignKey<BotRating, Ladder>(db),
                ForeignKey<MatchSet, Ladder>(db),
                ForeignKey<MatchSet, PlaylistVersion>(db),
                ForeignKey<Match, PlaylistVersion>(db),
                ForeignKey<PlaylistVersion, Playlist>(db),
                ForeignKey<Ladder, PlaylistVersion>(db),
                ForeignKey<Ladder, Season>(db),
            },
            foreignKey => Assert.Equal(
                DeleteBehavior.Restrict,
                foreignKey.DeleteBehavior));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task MigratedDatabaseAcceptsLegacyRowsWithNoOpaqueIdentity()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using AppDbContext db =
            await database.CreateMigratedContextAsync();
        var user = new User
        {
            DisplayName = "legacy-expand",
            Email = $"legacy-expand-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used",
        };
        var bot = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Legacy",
            Slug = $"legacy-{Guid.NewGuid():N}",
        };
        var rating = new BotRating
        {
            BotId = bot.Id,
            RulesVersion = "legacy-rules",
        };
        var set = new MatchSet
        {
            BotAId = bot.Id,
            BotBId = Guid.NewGuid(),
            BotAVersionId = Guid.NewGuid(),
            BotBVersionId = Guid.NewGuid(),
        };
        var match = new Match
        {
            MapId = "legacy-map",
            MatchSetId = set.Id,
        };

        db.AddRange(user, bot, rating, set, match);
        await db.SaveChangesAsync();

        Assert.Null(rating.LadderId);
        Assert.Null(rating.SeasonOpeningRank);
        Assert.Null(set.PlaylistVersionId);
        Assert.Null(set.LadderId);
        Assert.Null(match.PlaylistVersionId);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task MigratedDatabaseRejectsInvalidSeasonOpeningRank()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using AppDbContext db =
            await database.CreateMigratedContextAsync();
        var user = new User
        {
            DisplayName = "season-opening-rank",
            Email = $"season-opening-rank-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used",
        };
        var bot = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Season rank",
            Slug = $"season-rank-{Guid.NewGuid():N}",
        };
        db.AddRange(user, bot);
        await db.SaveChangesAsync();
        LegacyCompetitionIdentity validIdentity =
            await new LegacyCompetitionIdentityResolver(db)
                .ResolveOrCreateAsync(
                    "season-rank-valid",
                    "season-rank-valid");

        var valid = new BotRating
        {
            BotId = bot.Id,
            RulesVersion = "season-rank-valid",
            LadderId = validIdentity.LadderId,
            SeasonOpeningRank = 1,
        };
        db.Add(valid);
        await db.SaveChangesAsync();

        LegacyCompetitionIdentity invalidIdentity =
            await new LegacyCompetitionIdentityResolver(db)
                .ResolveOrCreateAsync(
                    "season-rank-invalid",
                    "season-rank-valid");
        var invalid = new BotRating
        {
            BotId = bot.Id,
            RulesVersion = "season-rank-invalid",
            LadderId = invalidIdentity.LadderId,
            SeasonOpeningRank = 0,
        };
        db.Add(invalid);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync());
        var databaseException =
            Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, databaseException.SqlState);
        Assert.Equal(
            "CK_BotRatings_SeasonOpeningRank_Positive",
            databaseException.ConstraintName);

        db.Entry(invalid).State = EntityState.Detached;
        var missingLadder = new BotRating
        {
            BotId = bot.Id,
            RulesVersion = "season-rank-no-ladder",
            SeasonOpeningRank = 1,
        };
        db.Add(missingLadder);

        exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());
        databaseException =
            Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, databaseException.SqlState);
        Assert.Equal(
            "CK_BotRatings_SeasonOpeningRank_RequiresLadder",
            databaseException.ConstraintName);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task MigratedDatabaseRejectsPartialMatchSetIdentity()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using AppDbContext db =
            await database.CreateMigratedContextAsync();
        LegacyCompetitionIdentity identity =
            await new LegacyCompetitionIdentityResolver(db)
                .ResolveOrCreateAsync("paired-identity", "paired-identity");

        MatchSet playlistOnly = CreateSet();
        playlistOnly.PlaylistVersionId = identity.PlaylistVersionId;
        db.Add(playlistOnly);
        await AssertPairedIdentityViolationAsync(db);
        db.Entry(playlistOnly).State = EntityState.Detached;

        MatchSet ladderOnly = CreateSet();
        ladderOnly.LadderId = identity.LadderId;
        db.Add(ladderOnly);
        await AssertPairedIdentityViolationAsync(db);

        static MatchSet CreateSet() =>
            new()
            {
                BotAId = Guid.NewGuid(),
                BotBId = Guid.NewGuid(),
                BotAVersionId = Guid.NewGuid(),
                BotBVersionId = Guid.NewGuid(),
            };

        static async Task AssertPairedIdentityViolationAsync(AppDbContext db)
        {
            DbUpdateException exception =
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => db.SaveChangesAsync());
            var databaseException =
                Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                databaseException.SqlState);
            Assert.Equal(
                "CK_MatchSets_CompetitionIdentity_Paired",
                databaseException.ConstraintName);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task PersistedPlaylistVersionsCannotBeMutated()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using AppDbContext db =
            await database.CreateMigratedContextAsync();
        var playlist = new Playlist
        {
            Key = $"immutable-{Guid.NewGuid():N}",
            DisplayName = "Immutable",
        };
        var version = new PlaylistVersion
        {
            PlaylistId = playlist.Id,
            Version = 1,
            GameModeId = "deathmatch",
            RulesetId = "legacy-rules",
            MatchFormatId = "head-to-head",
            MapPoolId = "legacy-maps",
            SeriesPolicyId = "duel-mirrored-6-v1",
            MatchmakingPolicyId = "nearest-rating-v1",
            AdmissionPolicyId = "legacy-duel-v1",
            CanonicalDefinition = """{"schemaVersion":1}""",
            DefinitionFingerprint = new string('a', 64),
            Provenance = """{"source":"test"}""",
            Visibility = "private",
        };
        db.AddRange(playlist, version);
        await db.SaveChangesAsync();

        version.GameModeId = "changed";
        Exception exception =
            await Assert.ThrowsAnyAsync<Exception>(
                () => db.SaveChangesAsync());
        PostgresException databaseException =
            InnerException<PostgresException>(exception);
        Assert.Equal("55000", databaseException.SqlState);
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

    private static IForeignKey ForeignKey<TEntity, TPrincipal>(
        AppDbContext db) =>
        Entity<TEntity>(db).GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TPrincipal));

    private static IEnumerable<string> PropertyNames(IIndex index) =>
        index.Properties.Select(property => property.Name);

    private static TException InnerException<TException>(
        Exception exception)
        where TException : Exception
    {
        for (Exception? candidate = exception;
             candidate is not null;
             candidate = candidate.InnerException)
        {
            if (candidate is TException typed)
                return typed;
        }
        throw new Xunit.Sdk.XunitException(
            $"Expected {typeof(TException).Name} in the exception chain, " +
            $"but received {exception}.");
    }
}
