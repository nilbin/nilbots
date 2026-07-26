using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Notifications;
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
    public DbSet<EntitlementGrant> EntitlementGrants => Set<EntitlementGrant>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.DisplayName).HasMaxLength(60);
            entity.Property(u => u.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Bot>(entity =>
        {
            entity.HasIndex(b => b.Slug).IsUnique();
            entity.Property(b => b.Name).HasMaxLength(60);
            entity.Property(b => b.Slug).HasMaxLength(80);
            entity.Property(b => b.Accent).HasMaxLength(16);
            entity.Property(b => b.LookId).HasMaxLength(64);
            entity.Property(b => b.ProjectileLookId).HasMaxLength(64);
            entity.HasMany(b => b.Versions).WithOne().HasForeignKey(v => v.BotId);
            entity.HasMany(b => b.Ratings).WithOne().HasForeignKey(r => r.BotId);
            entity.HasOne<User>().WithMany().HasForeignKey(b => b.OwnerUserId);
        });

        modelBuilder.Entity<BotRating>(entity =>
        {
            entity.HasIndex(r => new { r.BotId, r.RulesVersion }).IsUnique();
            // The leaderboard reads one ladder ordered by rating; the unique index above
            // leads with BotId and cannot serve that, so it was a seq scan over every
            // ladder plus a sort (DECISIONS #100).
            entity.HasIndex(r => new { r.RulesVersion, r.Rating }).IsDescending(false, true);
            entity.Property(r => r.RulesVersion).HasMaxLength(40);
            entity.Property(r => r.Rating).HasDefaultValue(1200.0);
        });

        modelBuilder.Entity<BotVersion>(entity =>
        {
            entity.HasIndex(v => new { v.BotId, v.VersionNumber }).IsUnique();
            entity.HasIndex(v => new { v.SubmissionNetworkHash, v.CreatedAt });
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(v => v.SourcesJson).HasColumnType("jsonb");
            entity.Property(v => v.BuildReceiptJson).HasColumnType("jsonb");
            entity.Property(v => v.SubmissionNetworkHash).HasMaxLength(64);
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
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(m => m.Participants).WithOne().HasForeignKey(p => p.MatchId);
            entity.HasIndex(m => m.CreatedAt);
            entity.HasIndex(m => m.MatchSetId);
            // Feed filtered by map, newest first. Without the CreatedAt column here,
            // filtering to an uncommon map scanned the whole table (DECISIONS #100).
            entity.HasIndex(m => new { m.MapId, m.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(m => m.InitiatedByUserId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.InitiatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MatchSet>(entity =>
        {
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(s => s.CreatedAt);
        });

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.HasIndex(p => new { p.MatchId, p.Slot }).IsUnique();
            // "Every match this bot played" — the feed's bot filter and the bot page's
            // history. MatchId trails BotId so the lookup is index-only.
            entity.HasIndex(p => new { p.BotId, p.MatchId });
            entity.Property(p => p.OwnerDisplayNameSnapshot).HasMaxLength(60);
            entity.Property(p => p.LookIdSnapshot).HasMaxLength(64);
            entity.Property(p => p.ProjectileLookIdSnapshot).HasMaxLength(64);
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
