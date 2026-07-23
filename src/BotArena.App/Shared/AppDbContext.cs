using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Shared;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Bot> Bots => Set<Bot>();
    public DbSet<BotRating> BotRatings => Set<BotRating>();
    public DbSet<BotVersion> BotVersions => Set<BotVersion>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

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
            entity.HasMany(b => b.Versions).WithOne().HasForeignKey(v => v.BotId);
            entity.HasMany(b => b.Ratings).WithOne().HasForeignKey(r => r.BotId);
            entity.HasOne<User>().WithMany().HasForeignKey(b => b.OwnerUserId);
        });

        modelBuilder.Entity<BotRating>(entity =>
        {
            entity.HasIndex(r => new { r.BotId, r.RulesVersion }).IsUnique();
            entity.Property(r => r.RulesVersion).HasMaxLength(40);
            entity.Property(r => r.Rating).HasDefaultValue(1200.0);
        });

        modelBuilder.Entity<BotVersion>(entity =>
        {
            entity.HasIndex(v => new { v.BotId, v.VersionNumber }).IsUnique();
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(v => v.SourcesJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(m => m.Participants).WithOne().HasForeignKey(p => p.MatchId);
            entity.HasIndex(m => m.CreatedAt);
            entity.HasIndex(m => m.MatchSetId);
        });

        modelBuilder.Entity<MatchSet>(entity =>
        {
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(s => s.CreatedAt);
        });

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.HasIndex(p => new { p.MatchId, p.Slot }).IsUnique();
        });

        modelBuilder.Entity<BackgroundJob>(entity =>
        {
            entity.Property(j => j.PayloadJson).HasColumnType("jsonb");
            entity.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(j => new { j.Status, j.AvailableAt });
        });
    }
}

/// <summary>Filesystem layout for artifacts and replays (plan §38: local volume now,
/// IArtifactStore abstraction when a second backend appears).</summary>
public static class DataPaths
{
    public static string Root =>
        Environment.GetEnvironmentVariable("BOTARENA_DATA") is { Length: > 0 } data
            ? data
            : Path.Combine(Directory.GetCurrentDirectory(), "var");

    public static string Artifacts => Ensure(Path.Combine(Root, "artifacts"));
    public static string Replays => Ensure(Path.Combine(Root, "replays"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
