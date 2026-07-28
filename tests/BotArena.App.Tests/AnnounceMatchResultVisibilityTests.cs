using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public sealed class AnnounceMatchResultVisibilityTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task LabsSkipAndNonLabsGenericAnnouncementFollowVisibility()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using AppDbContext db =
            await database.CreateMigratedContextAsync();
        PlaylistVersion labs =
            await new FrontlineLabsPlaylistSeeder(db).SeedAsync();
        PlaylistVersion publicGeneric =
            AddPublicGenericPlaylist(db);
        var initiator = new User
        {
            DisplayName = "Announcement Initiator",
            Email = "announce-initiator@example.test",
            PasswordHash = "not-used",
        };
        var recipient = new User
        {
            DisplayName = "Announcement Recipient",
            Email = "announce-recipient@example.test",
            PasswordHash = "not-used",
        };
        var challenger = new Bot
        {
            OwnerUserId = initiator.Id,
            Name = "Announce Challenger",
            Slug = "announce-challenger",
        };
        var challenged = new Bot
        {
            OwnerUserId = recipient.Id,
            Name = "Announce Challenged",
            Slug = "announce-challenged",
        };
        db.AddRange(initiator, recipient, challenger, challenged);
        Match labsMatch = AddCompletedGenericMatch(
            db,
            labs.Id,
            challenger,
            challenged,
            initiator.Id);
        Match publicMatch = AddCompletedGenericMatch(
            db,
            publicGeneric.Id,
            challenger,
            challenged,
            initiator.Id);
        await db.SaveChangesAsync();

        var handler = new AnnounceMatchResultJobHandler(
            db,
            new UserNotificationWriter(db),
            TimeProvider.System);

        JobExecutionResult labsResult =
            await handler.HandleAsync(labsMatch.Id, CancellationToken.None);
        Assert.Equal("labs_match_skipped", labsResult.Outcome);
        Assert.Empty(await db.UserNotifications.ToListAsync());

        JobExecutionResult publicResult =
            await handler.HandleAsync(publicMatch.Id, CancellationToken.None);
        Assert.Equal("announced", publicResult.Outcome);
        UserNotification notification =
            Assert.Single(await db.UserNotifications.ToListAsync());
        Assert.Equal(recipient.Id, notification.UserId);
        Assert.Equal(UserNotificationKinds.MatchSettled, notification.Kind);
        Assert.Contains(
            publicMatch.Id.ToString(),
            notification.DedupeKey,
            StringComparison.Ordinal);
    }

    private static PlaylistVersion AddPublicGenericPlaylist(
        AppDbContext db)
    {
        FrontlineLabsPlaylistDefinition definition =
            FrontlineLabsPlaylistDefinition.Create();
        var playlist = new Playlist
        {
            Key = "announce-public-generic-policy-test",
            DisplayName = "Announce Public Generic Policy Test",
        };
        var version = new PlaylistVersion
        {
            PlaylistId = playlist.Id,
            Version = 1,
            GameModeId = definition.GameModeId,
            RulesetId = definition.RulesetId,
            MatchFormatId = definition.MatchFormatId,
            MapPoolId = definition.MapPoolId,
            SeriesPolicyId =
                FrontlineLabsPlaylistDefinition.SeriesPolicyId,
            MatchmakingPolicyId =
                FrontlineLabsPlaylistDefinition.MatchmakingPolicyId,
            AdmissionPolicyId =
                BotArenaVersions.GenericActorContractProfileId,
            ExecutionPolicyId =
                PlaylistExecutionPolicyIds.GenericActor,
            ExecutionEngineVersion =
                BotArenaVersions.GenericActorEngineVersion,
            CanonicalDefinition = definition.CanonicalDefinition,
            DefinitionFingerprint = definition.DefinitionFingerprint,
            Provenance = definition.Provenance,
            Visibility = "public",
        };
        db.Playlists.Add(playlist);
        db.PlaylistVersions.Add(version);
        return version;
    }

    private static Match AddCompletedGenericMatch(
        AppDbContext db,
        Guid playlistVersionId,
        Bot challenger,
        Bot challenged,
        Guid initiatedByUserId)
    {
        var match = new Match
        {
            MapId = "announcement-policy-test",
            Seed = 1,
            Status = MatchStatus.Completed,
            WinnerSlot = 0,
            EndReason = "test-complete",
            EndTick = 1,
            ReplayFormatVersion =
                BotArenaVersions.GenericActorReplayFormatVersion,
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            BroadcastStartedAt = DateTime.UtcNow.AddHours(-1),
            PlaylistVersionId = playlistVersionId,
            InitiatedByUserId = initiatedByUserId,
        };
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 0,
            TeamId = 0,
            BotId = challenger.Id,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = challenger.Name,
            AccentSnapshot = challenger.Accent,
            Outcome = "Win",
            FinalHealth = 3,
        });
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 1,
            TeamId = 1,
            BotId = challenged.Id,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = challenged.Name,
            AccentSnapshot = challenged.Accent,
            Outcome = "Loss",
            FinalHealth = 0,
        });
        db.Matches.Add(match);
        return match;
    }
}
