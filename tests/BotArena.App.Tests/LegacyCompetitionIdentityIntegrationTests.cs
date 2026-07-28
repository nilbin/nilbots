using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public sealed class LegacyCompetitionIdentityIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ConcurrentResolversCreateOneIdentity()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
                     await database.CreateMigratedContextAsync())
        {
        }

        await using AppDbContext firstDb = database.CreateContext();
        await using AppDbContext secondDb = database.CreateContext();
        LegacyCompetitionIdentity[] identities = await Task.WhenAll(
            new LegacyCompetitionIdentityResolver(firstDb)
                .ResolveOrCreateAsync("0.5", "0.5"),
            new LegacyCompetitionIdentityResolver(secondDb)
                .ResolveOrCreateAsync("0.5", "0.5"));

        Assert.Equal(
            identities[0].PlaylistVersionId,
            identities[1].PlaylistVersionId);
        Assert.Equal(identities[0].SeasonId, identities[1].SeasonId);
        Assert.Equal(identities[0].LadderId, identities[1].LadderId);

        await using AppDbContext verify = database.CreateContext();
        Assert.Equal(1, await verify.Playlists.CountAsync());
        Assert.Equal(1, await verify.PlaylistVersions.CountAsync());
        Assert.Equal(1, await verify.Seasons.CountAsync());
        Assert.Equal(1, await verify.Ladders.CountAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task BackfillCreatesStablePopulationIdentitiesAndRepairsNullLinks()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid ratingId;
        Guid longRulesRatingId;
        Guid currentSetId;
        Guid historicalSetId;
        Guid experimentMatchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            (Bot botA, Bot botB) = AddBots(db);
            var rating = new BotRating
            {
                BotId = botA.Id,
                RulesVersion = "0.4",
                Rating = 1375,
                RankedSets = 9,
            };
            var longRulesRating = new BotRating
            {
                BotId = botB.Id,
                RulesVersion =
                    "0.5-exp-cone-active-bolt2-overtime-gain-v6",
            };
            MatchSet currentSet =
                AddSet(db, botA.Id, botB.Id, "0.5");
            MatchSet historicalSet =
                AddSet(db, botA.Id, botB.Id, "0.4");
            Match currentMatch = AddMatch(
                db,
                "0.5",
                currentSet.Id);
            Match historicalMatch = AddMatch(
                db,
                "0.4",
                historicalSet.Id);
            Match experimentMatch = AddMatch(
                db,
                "0.5-exp-cone-active-bolt2-overtime-gain-v6",
                matchSetId: null);
            db.BotRatings.AddRange(rating, longRulesRating);
            await db.SaveChangesAsync();
            ratingId = rating.Id;
            longRulesRatingId = longRulesRating.Id;
            currentSetId = currentSet.Id;
            historicalSetId = historicalSet.Id;
            experimentMatchId = experimentMatch.Id;

            var resolver =
                new LegacyCompetitionIdentityResolver(db);
            var backfiller =
                new LegacyCompetitionIdentityBackfiller(db, resolver);
            await backfiller.RunAsync("0.5");

            Guid[] firstPlaylistIds =
                await db.Playlists
                    .OrderBy(playlist => playlist.Key)
                    .Select(playlist => playlist.Id)
                    .ToArrayAsync();
            Guid[] firstVersionIds =
                await db.PlaylistVersions
                    .OrderBy(version => version.RulesetId)
                    .Select(version => version.Id)
                    .ToArrayAsync();
            Guid[] firstLadderIds =
                await db.Ladders
                    .OrderBy(ladder => ladder.LegacyRulesVersion)
                    .Select(ladder => ladder.Id)
                    .ToArrayAsync();

            await backfiller.RunAsync("0.5");

            Assert.Equal(
                firstPlaylistIds,
                await db.Playlists
                    .OrderBy(playlist => playlist.Key)
                    .Select(playlist => playlist.Id)
                    .ToArrayAsync());
            Assert.Equal(
                firstVersionIds,
                await db.PlaylistVersions
                    .OrderBy(version => version.RulesetId)
                    .Select(version => version.Id)
                    .ToArrayAsync());
            Assert.Equal(
                firstLadderIds,
                await db.Ladders
                    .OrderBy(ladder => ladder.LegacyRulesVersion)
                    .Select(ladder => ladder.Id)
                    .ToArrayAsync());

            // A later deployment changes only which alias is current. Existing
            // imported aliases are still discovered and closed even if they have
            // no newly observed legacy rows.
            await backfiller.RunAsync("0.6");
            Assert.Equal(
                LadderStatus.Closed,
                (await db.Ladders.SingleAsync(
                    ladder => ladder.LegacyRulesVersion == "0.5"))
                .Status);
            Assert.Equal(
                LadderStatus.Open,
                (await db.Ladders.SingleAsync(
                    ladder => ladder.LegacyRulesVersion == "0.6"))
                .Status);
        }

        await using (AppDbContext verify = database.CreateContext())
        {
            Assert.Equal(4, await verify.Playlists.CountAsync());
            Assert.Equal(4, await verify.PlaylistVersions.CountAsync());
            Assert.Equal(4, await verify.Ladders.CountAsync());
            Season season = Assert.Single(
                await verify.Seasons.ToArrayAsync());
            Assert.Equal(
                LegacyCompetitionDefinition.SeasonKey,
                season.Key);
            Assert.Null(season.StartsAt);
            Assert.Null(season.EndsAt);

            Ladder current = await verify.Ladders.SingleAsync(
                ladder => ladder.LegacyRulesVersion == "0.5");
            Assert.Equal(LadderStatus.Closed, current.Status);
            Assert.True(current.IsListed);
            Ladder nextCurrent = await verify.Ladders.SingleAsync(
                ladder => ladder.LegacyRulesVersion == "0.6");
            Assert.Equal(LadderStatus.Open, nextCurrent.Status);
            Assert.True(nextCurrent.IsListed);
            Ladder historical = await verify.Ladders.SingleAsync(
                ladder => ladder.LegacyRulesVersion == "0.4");
            Assert.Equal(LadderStatus.Closed, historical.Status);
            Assert.True(historical.IsListed);
            Ladder experiment = await verify.Ladders.SingleAsync(
                ladder =>
                    ladder.LegacyRulesVersion ==
                    "0.5-exp-cone-active-bolt2-overtime-gain-v6");
            Assert.Equal(LadderStatus.Closed, experiment.Status);
            Assert.False(experiment.IsListed);
            Assert.False(experiment.AwardsAchievements);

            BotRating rating = await verify.BotRatings.SingleAsync(
                candidate => candidate.Id == ratingId);
            Assert.Equal(historical.Id, rating.LadderId);
            Assert.Equal(1375, rating.Rating);
            Assert.Equal(9, rating.RankedSets);
            Assert.Null(rating.SeasonOpeningRank);
            BotRating longRulesRating =
                await verify.BotRatings.SingleAsync(
                    candidate => candidate.Id == longRulesRatingId);
            Assert.Equal(experiment.Id, longRulesRating.LadderId);
            Assert.Null(longRulesRating.SeasonOpeningRank);

            MatchSet currentSet =
                await verify.MatchSets.SingleAsync(
                    set => set.Id == currentSetId);
            Assert.Equal(current.Id, currentSet.LadderId);
            Assert.NotNull(currentSet.PlaylistVersionId);
            Assert.All(
                await verify.Matches
                    .Where(match =>
                        match.MatchSetId == currentSetId)
                    .ToArrayAsync(),
                match => Assert.Equal(
                    currentSet.PlaylistVersionId,
                    match.PlaylistVersionId));

            MatchSet historicalSet =
                await verify.MatchSets.SingleAsync(
                    set => set.Id == historicalSetId);
            Assert.Equal(historical.Id, historicalSet.LadderId);
            Match experimentMatch =
                await verify.Matches.SingleAsync(
                    match => match.Id == experimentMatchId);
            Assert.Equal(
                experiment.PlaylistVersionId,
                experimentMatch.PlaylistVersionId);

            Assert.All(
                await verify.PlaylistVersions.ToArrayAsync(),
                version =>
                {
                    Assert.Equal(
                        LegacyCompetitionDefinition.UnknownDefinitionId,
                        version.MapPoolId);
                    Assert.Equal(
                        LegacyCompetitionDefinition.UnknownDefinitionId,
                        version.SeriesPolicyId);
                    Assert.Contains(
                        "\"source\": \"legacy-import\"",
                        version.Provenance);
                });
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task BackfillFailsContradictionsWithoutPartiallyRepairingRows()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid nullSetId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            (Bot botA, Bot botB) = AddBots(db);
            MatchSet historical =
                AddSet(db, botA.Id, botB.Id, "0.4");
            AddMatch(db, "0.4", historical.Id);
            await db.SaveChangesAsync();

            var resolver =
                new LegacyCompetitionIdentityResolver(db);
            var backfiller =
                new LegacyCompetitionIdentityBackfiller(db, resolver);
            await backfiller.RunAsync("0.5");

            Guid currentVersionId =
                (await resolver.ResolveExistingAsync("0.5"))
                .PlaylistVersionId;
            historical.PlaylistVersionId = currentVersionId;
            MatchSet oldImageRow =
                AddSet(db, botA.Id, botB.Id, "0.5");
            nullSetId = oldImageRow.Id;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => backfiller.RunAsync("0.5"));
            Assert.Contains(
                "contradiction",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        await using AppDbContext verify = database.CreateContext();
        MatchSet stillNull =
            await verify.MatchSets.SingleAsync(
                set => set.Id == nullSetId);
        Assert.Null(stillNull.PlaylistVersionId);
        Assert.Null(stillNull.LadderId);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task BackfillLeavesPinnedGenericLabsIdentityUntouched()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid labsPlaylistVersionId;
        Guid labsMatchId;
        Guid legacyMatchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            PlaylistVersion labs =
                await new FrontlineLabsPlaylistSeeder(db).SeedAsync();
            var match = new Match
            {
                MapId = FrontlineLabsDefinition.MapId,
                MapVersion = 1,
                GameRulesVersion = FrontlineLabsDefinition.RulesetId,
                RuntimeConfigurationVersion =
                    BotArenaVersions
                        .GenericActorRuntimeConfigurationVersion,
                PlaylistVersionId = labs.Id,
                Seed = 1,
            };
            db.Matches.Add(match);
            Match legacy = AddMatch(
                db,
                GameRules.Current.RulesVersion,
                matchSetId: null);
            await db.SaveChangesAsync();
            labsPlaylistVersionId = labs.Id;
            labsMatchId = match.Id;
            legacyMatchId = legacy.Id;

            var resolver =
                new LegacyCompetitionIdentityResolver(db);
            var backfiller =
                new LegacyCompetitionIdentityBackfiller(db, resolver);
            await backfiller.RunAsync(GameRules.Current.RulesVersion);
            await backfiller.RunAsync(GameRules.Current.RulesVersion);
            await backfiller.RunAsync("0.6");
        }

        await using AppDbContext verify = database.CreateContext();
        Match persisted = await verify.Matches.SingleAsync(
            match => match.Id == labsMatchId);
        Assert.Equal(
            labsPlaylistVersionId,
            persisted.PlaylistVersionId);
        Assert.NotNull(
            (await verify.Matches.SingleAsync(
                match => match.Id == legacyMatchId))
            .PlaylistVersionId);
        Assert.False(await verify.Ladders.AnyAsync(
            ladder =>
                ladder.LegacyRulesVersion ==
                FrontlineLabsDefinition.RulesetId));
        Assert.False(await verify.PlaylistVersions.AnyAsync(
            version =>
                version.RulesetId ==
                    FrontlineLabsDefinition.RulesetId &&
                version.ExecutionPolicyId ==
                    PlaylistExecutionPolicyIds.LegacyDuel));
        Assert.False(await verify.Ladders.AnyAsync(
            ladder =>
                ladder.PlaylistVersionId ==
                labsPlaylistVersionId));
    }

    private static (Bot A, Bot B) AddBots(AppDbContext db)
    {
        string suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            DisplayName = $"identity-{suffix[..8]}",
            Email = $"identity-{suffix}@example.test",
            PasswordHash = "not-used",
        };
        var botA = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Identity A",
            Slug = $"identity-a-{suffix}",
        };
        var botB = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Identity B",
            Slug = $"identity-b-{suffix}",
        };
        db.AddRange(user, botA, botB);
        return (botA, botB);
    }

    private static MatchSet AddSet(
        AppDbContext db,
        Guid botAId,
        Guid botBId,
        string rulesVersion)
    {
        var set = new MatchSet
        {
            BotAId = botAId,
            BotBId = botBId,
            BotAVersionId = Guid.NewGuid(),
            BotBVersionId = Guid.NewGuid(),
            GameRulesVersion = rulesVersion,
        };
        db.MatchSets.Add(set);
        return set;
    }

    private static Match AddMatch(
        AppDbContext db,
        string rulesVersion,
        Guid? matchSetId)
    {
        var match = new Match
        {
            MapId = "basic-01",
            GameRulesVersion = rulesVersion,
            MatchSetId = matchSetId,
        };
        db.Matches.Add(match);
        return match;
    }
}
