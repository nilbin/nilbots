using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Cosmetics;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Store;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Shared;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Bot> Bots => Set<Bot>();
    public DbSet<BotRating> BotRatings => Set<BotRating>();
    public DbSet<BotVersion> BotVersions => Set<BotVersion>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistVersion> PlaylistVersions => Set<PlaylistVersion>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Ladder> Ladders => Set<Ladder>();
    public DbSet<EntitlementGrant> EntitlementGrants => Set<EntitlementGrant>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<MatchTeamResult> MatchTeamResults => Set<MatchTeamResult>();
    public DbSet<MatchTeamScore> MatchTeamScores => Set<MatchTeamScore>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            // Display names are unique, case-insensitively — the index is a functional one
            // on lower("DisplayName"), created in the AccountDisplayNames migration because
            // EF cannot express an expression index. Case-insensitive on purpose: "Pincer"
            // and "pincer" in the same ladder are indistinguishable at a glance, which is
            // the whole of an impersonation.
            entity.Property(u => u.DisplayName).HasMaxLength(60);
            entity.Property(u => u.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Bot>(entity =>
        {
            entity.HasIndex(b => b.Slug).IsUnique();
            entity.Property(b => b.Name).HasMaxLength(60);
            entity.Property(b => b.Slug).HasMaxLength(80);
            entity.Property(b => b.ClassId).HasMaxLength(64);
            entity.Property(b => b.Accent).HasMaxLength(16);
            entity.Property(b => b.LookId).HasMaxLength(64);
            entity.Property(b => b.ProjectileLookId).HasMaxLength(64);
            entity.HasMany(b => b.Versions).WithOne().HasForeignKey(v => v.BotId);
            entity.HasMany(b => b.Ratings).WithOne().HasForeignKey(r => r.BotId);
            entity.HasOne<User>().WithMany().HasForeignKey(b => b.OwnerUserId);
        });

        modelBuilder.Entity<BotRating>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_BotRatings_SeasonOpeningRank_Positive",
                    "\"SeasonOpeningRank\" IS NULL OR \"SeasonOpeningRank\" > 0");
                table.HasCheckConstraint(
                    "CK_BotRatings_SeasonOpeningRank_RequiresLadder",
                    "\"SeasonOpeningRank\" IS NULL OR \"LadderId\" IS NOT NULL");
            });
            entity.HasIndex(r => new { r.BotId, r.RulesVersion }).IsUnique();
            // The leaderboard reads one ladder ordered by rating; the unique index above
            // leads with BotId and cannot serve that, so it was a seq scan over every
            // ladder plus a sort (DECISIONS #100).
            entity.HasIndex(r => new { r.RulesVersion, r.Rating }).IsDescending(false, true);
            entity.Property(r => r.RulesVersion).HasMaxLength(100);
            entity.Property(r => r.Rating).HasDefaultValue(1200.0);
            entity.HasIndex(r => new { r.BotId, r.LadderId })
                .IsUnique()
                .HasFilter("\"LadderId\" IS NOT NULL");
            entity.HasIndex(r => new { r.LadderId, r.Rating, r.BotId })
                .IsDescending(false, true, false)
                .HasFilter("\"LadderId\" IS NOT NULL");
            entity.HasOne<Ladder>()
                .WithMany()
                .HasForeignKey(r => r.LadderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasIndex(playlist => playlist.Key).IsUnique();
            entity.Property(playlist => playlist.Key).HasMaxLength(100);
            entity.Property(playlist => playlist.DisplayName).HasMaxLength(120);
            entity.HasMany(playlist => playlist.Versions)
                .WithOne()
                .HasForeignKey(version => version.PlaylistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlaylistVersion>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PlaylistVersions_Version_Positive",
                    "\"Version\" > 0");
                table.HasCheckConstraint(
                    "CK_PlaylistVersions_DefinitionFingerprint",
                    "\"DefinitionFingerprint\" ~ '^[0-9a-f]{64}$'");
            });
            entity.HasIndex(version => new
            {
                version.PlaylistId,
                version.Version,
            }).IsUnique();
            entity.Property(version => version.GameModeId).HasMaxLength(100);
            entity.Property(version => version.RulesetId).HasMaxLength(100);
            entity.Property(version => version.MatchFormatId).HasMaxLength(100);
            entity.Property(version => version.MapPoolId).HasMaxLength(100);
            entity.Property(version => version.SeriesPolicyId).HasMaxLength(100);
            entity.Property(version => version.MatchmakingPolicyId).HasMaxLength(100);
            entity.Property(version => version.AdmissionPolicyId).HasMaxLength(100);
            entity.Property(version => version.ExecutionPolicyId)
                .HasMaxLength(100)
                .HasDefaultValue(PlaylistExecutionPolicyIds.LegacyDuel);
            entity.Property(version => version.ExecutionEngineVersion)
                .HasMaxLength(100)
                .HasDefaultValue(
                    BotArena.Engine.BotArenaVersions.EngineVersion);
            entity.Property(version => version.CanonicalDefinition).HasColumnType("jsonb");
            entity.Property(version => version.DefinitionFingerprint)
                .HasMaxLength(64)
                .IsFixedLength();
            entity.Property(version => version.Provenance)
                .HasColumnType("jsonb");
            entity.Property(version => version.Visibility).HasMaxLength(20);
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Seasons_TimeWindow",
                "\"StartsAt\" IS NULL OR \"EndsAt\" IS NULL OR \"EndsAt\" > \"StartsAt\""));
            entity.HasIndex(season => season.Key).IsUnique();
            entity.Property(season => season.Key).HasMaxLength(100);
            entity.Property(season => season.DisplayName).HasMaxLength(120);
        });

        modelBuilder.Entity<Ladder>(entity =>
        {
            entity.Property(ladder => ladder.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(ladder => ladder.RatingPolicyId).HasMaxLength(100);
            entity.Property(ladder => ladder.LegacyRulesVersion).HasMaxLength(100);
            entity.HasIndex(ladder => new
            {
                ladder.PlaylistVersionId,
                ladder.SeasonId,
            }).IsUnique();
            entity.HasIndex(ladder => ladder.LegacyRulesVersion)
                .IsUnique()
                .HasFilter("\"LegacyRulesVersion\" IS NOT NULL");
            entity.HasIndex(ladder => ladder.PlaylistVersionId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Open'")
                .HasDatabaseName("IX_Ladders_OneOpenPerPlaylistVersion");
            entity.HasOne<PlaylistVersion>()
                .WithMany()
                .HasForeignKey(ladder => ladder.PlaylistVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Season>()
                .WithMany()
                .HasForeignKey(ladder => ladder.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BotVersion>(entity =>
        {
            entity.HasIndex(v => new { v.BotId, v.VersionNumber }).IsUnique();
            entity.HasIndex(v => new { v.SubmissionNetworkHash, v.CreatedAt });
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(v => v.SourcesJson).HasColumnType("jsonb");
            entity.Property(v => v.BuildReceiptJson).HasColumnType("jsonb");
            entity.Property(v => v.SubmissionNetworkHash).HasMaxLength(64);
            entity.Property(v => v.SupportedContractProfiles)
                .HasColumnType("text[]");
        });

        modelBuilder.Entity<EntitlementGrant>(entity =>
        {
            entity.HasIndex(grant => new
            {
                grant.UserId,
                grant.EntitlementKey,
                grant.SourceKind,
                grant.SourceId,
            }).IsUnique();
            entity.HasIndex(grant => new
            {
                grant.UserId,
                grant.EntitlementKey,
                grant.RevokedAt,
            });
            entity.Property(grant => grant.EntitlementKey).HasMaxLength(128);
            entity.Property(grant => grant.SourceKind).HasMaxLength(40);
            entity.Property(grant => grant.SourceId).HasMaxLength(80);
            entity.Property(grant => grant.MetadataJson).HasColumnType("jsonb");
            entity.HasOne<User>().WithMany().HasForeignKey(grant => grant.UserId);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasIndex(notification => new
            {
                notification.UserId,
                notification.DedupeKey,
            }).IsUnique();
            entity.HasIndex(notification => new
            {
                notification.UserId,
                notification.ReadAt,
                notification.CreatedAt,
            });
            entity.Property(notification => notification.Kind).HasMaxLength(50);
            entity.Property(notification => notification.DedupeKey).HasMaxLength(200);
            entity.Property(notification => notification.PayloadJson).HasColumnType("jsonb");
            entity.HasOne<User>().WithMany().HasForeignKey(notification => notification.UserId);
        });

        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            // Both counted per window, so both are indexed with the timestamp.
            entity.HasIndex(attempt => new { attempt.Identifier, attempt.OccurredAt });
            entity.HasIndex(attempt => new { attempt.NetworkHash, attempt.OccurredAt });
            entity.Property(attempt => attempt.Identifier).HasMaxLength(200);
            entity.Property(attempt => attempt.NetworkHash).HasMaxLength(128);
            // No foreign key: the whole point is recording attempts against addresses that
            // may not be accounts at all.
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            // The idempotence key for webhooks: providers retry, and replay by hand.
            entity.HasIndex(purchase => new { purchase.Provider, purchase.ProviderReference })
                .IsUnique();
            entity.HasIndex(purchase => new { purchase.UserId, purchase.PackId });
            entity.Property(purchase => purchase.PackId).HasMaxLength(80);
            entity.Property(purchase => purchase.Provider).HasMaxLength(30);
            entity.Property(purchase => purchase.ProviderReference).HasMaxLength(200);
            entity.Property(purchase => purchase.State).HasMaxLength(20);
            entity.Property(purchase => purchase.Currency).HasMaxLength(3);
            // Restrict, not cascade: a purchase is a financial record and must outlive
            // tidying up an account.
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(purchase => purchase.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            // One local account per provider identity. Unique on (provider, subject) rather
            // than including the user, so a second account cannot claim an identity that is
            // already linked — which is what would let someone attach their Google login to
            // a victim's account and then sign in as them.
            entity.HasIndex(login => new { login.Provider, login.Subject }).IsUnique();
            entity.Property(login => login.Provider).HasMaxLength(30);
            entity.Property(login => login.Subject).HasMaxLength(200);
            entity.Property(login => login.Email).HasMaxLength(200);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(login => login.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceRegistration>(entity =>
        {
            // The token is what the transport addresses, so uniqueness holds there — and
            // globally, not per account: a phone that signs into a second account must
            // move its token rather than receive both accounts' notifications.
            entity.HasIndex(device => device.PushToken).IsUnique();
            entity.HasIndex(device => new { device.UserId, device.DeviceId }).IsUnique();
            entity.Property(device => device.PushToken).HasMaxLength(300);
            entity.Property(device => device.DeviceId).HasMaxLength(200);
            entity.Property(device => device.Platform).HasMaxLength(20);
            entity.HasOne<User>().WithMany().HasForeignKey(device => device.UserId);
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasIndex(preference => new { preference.UserId, preference.Kind }).IsUnique();
            entity.Property(preference => preference.Kind).HasMaxLength(50);
            entity.HasOne<User>().WithMany().HasForeignKey(preference => preference.UserId);
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.HasIndex(delivery => new { delivery.NotificationId, delivery.Channel });
            entity.Property(delivery => delivery.Channel).HasMaxLength(20);
            entity.Property(delivery => delivery.State).HasMaxLength(20);
            entity.Property(delivery => delivery.Detail).HasMaxLength(500);
            // Cascade: a delivery record describes a notification and has no meaning once
            // that row is gone.
            entity.HasOne<UserNotification>()
                .WithMany()
                .HasForeignKey(delivery => delivery.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Matches_ReplayFormatVersion_Positive",
                "\"ReplayFormatVersion\" IS NULL OR \"ReplayFormatVersion\" > 0"));
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(m => m.Participants).WithOne().HasForeignKey(p => p.MatchId);
            entity.HasMany(m => m.TeamResults)
                .WithOne()
                .HasForeignKey(result => result.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => m.CreatedAt);
            entity.HasIndex(m => m.MatchSetId);
            entity.HasIndex(m => m.PlaylistVersionId);
            // Feed filtered by map, newest first. Without the CreatedAt column here,
            // filtering to an uncommon map scanned the whole table (DECISIONS #100).
            entity.HasIndex(m => new { m.MapId, m.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(m => m.InitiatedByUserId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.InitiatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<PlaylistVersion>()
                .WithMany()
                .HasForeignKey(m => m.PlaylistVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchSet>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_MatchSets_CompetitionIdentity_Paired",
                "(\"PlaylistVersionId\" IS NULL AND \"LadderId\" IS NULL) OR " +
                "(\"PlaylistVersionId\" IS NOT NULL AND \"LadderId\" IS NOT NULL)"));
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(s => s.CreatedAt);
            entity.HasIndex(s => s.PlaylistVersionId);
            entity.HasIndex(s => new { s.LadderId, s.CreatedAt });
            entity.HasOne<PlaylistVersion>()
                .WithMany()
                .HasForeignKey(s => s.PlaylistVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Ladder>()
                .WithMany()
                .HasForeignKey(s => s.LadderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_MatchParticipants_TeamId_NonNegative",
                "\"TeamId\" IS NULL OR \"TeamId\" >= 0"));
            entity.HasIndex(p => new { p.MatchId, p.Slot }).IsUnique();
            // "Every match this bot played" — the feed's bot filter and the bot page's
            // history. MatchId trails BotId so the lookup is index-only.
            entity.HasIndex(p => new { p.BotId, p.MatchId });
            entity.Property(p => p.OwnerDisplayNameSnapshot).HasMaxLength(60);
            entity.Property(p => p.LookIdSnapshot).HasMaxLength(64);
            entity.Property(p => p.ProjectileLookIdSnapshot).HasMaxLength(64);
        });

        modelBuilder.Entity<MatchTeamResult>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_MatchTeamResults_TeamId_NonNegative",
                    "\"TeamId\" >= 0");
                table.HasCheckConstraint(
                    "CK_MatchTeamResults_Placement_Positive",
                    "\"Placement\" > 0");
                table.HasCheckConstraint(
                    "CK_MatchTeamResults_Outcome",
                    "\"Outcome\" IN ('Win', 'Loss', 'Draw')");
            });
            entity.HasKey(result => new { result.MatchId, result.TeamId });
            entity.Property(result => result.Outcome)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.HasMany(result => result.Scores)
                .WithOne()
                .HasForeignKey(score => new { score.MatchId, score.TeamId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchTeamScore>(entity =>
        {
            entity.HasKey(score => new
            {
                score.MatchId,
                score.TeamId,
                score.ScoreChannelId,
            });
            entity.Property(score => score.ScoreChannelId).HasMaxLength(100);
        });

        modelBuilder.Entity<BackgroundJob>(entity =>
        {
            entity.Property(j => j.PayloadJson).HasColumnType("jsonb");
            entity.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(j => j.LockedBy).HasMaxLength(160);
            entity.HasIndex(j => new { j.Status, j.AvailableAt });
        });
    }
}
