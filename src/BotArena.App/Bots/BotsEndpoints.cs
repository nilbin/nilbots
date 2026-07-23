using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record CreateBotRequest(string Name, string? Accent);
public sealed record SubmitVersionRequest(string EntryType, List<SourceFileDto> Files);
public sealed record SourceFileDto(string Name, string Content);

public static class BotsEndpoints
{
    public static void MapBots(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/bots");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var bots = await db.Bots
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.Accent,
                    b.CreatedAt,
                    // One rating per rules-version ladder (DECISIONS #54), newest first.
                    Ratings = b.Ratings
                        .OrderByDescending(r => r.RulesVersion)
                        .Select(r => new { r.RulesVersion, Rating = Math.Round(r.Rating), r.RankedSets }),
                    Owner = db.Users.Where(u => u.Id == b.OwnerUserId).Select(u => u.DisplayName).First(),
                    ActiveVersion = b.Versions
                        .Where(v => v.IsActive && v.Status == BuildStatus.Built)
                        .Select(v => new { v.Id, v.VersionNumber, v.ArtifactHash })
                        .FirstOrDefault(),
                    VersionCount = b.Versions.Count(v => v.Status == BuildStatus.Built),
                })
                .OrderBy(b => b.CreatedAt)
                .ToListAsync();
            return Results.Ok(bots);
        });

        group.MapPost("/", async (CreateBotRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            string name = request.Name.Trim();
            if (name.Length is < 2 or > 40)
                return Results.Problem("Bot name must be 2-40 characters.", statusCode: 400);
            string slug = Slugify(name);
            if (await db.Bots.AnyAsync(b => b.Slug == slug))
                return Results.Problem($"A bot named '{name}' already exists.", statusCode: 409);
            string accent = request.Accent is { Length: > 0 } a &&
                            System.Text.RegularExpressions.Regex.IsMatch(a, "^#[0-9a-fA-F]{6}$")
                ? a
                : "#22d3ee";
            var bot = new Bot { OwnerUserId = userId, Name = name, Slug = slug, Accent = accent };
            db.Bots.Add(bot);
            await db.SaveChangesAsync();
            return Results.Ok(new { bot.Id, bot.Name, bot.Slug, bot.Accent });
        }).RequireAuthorization();

        group.MapGet("/{botId:guid}", async (Guid botId, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var bot = await db.Bots.Include(b => b.Versions).SingleOrDefaultAsync(b => b.Id == botId);
            if (bot is null)
                return Results.NotFound();
            bool isOwner = principal.UserId() == bot.OwnerUserId;
            string owner = await db.Users.Where(u => u.Id == bot.OwnerUserId)
                .Select(u => u.DisplayName).FirstAsync();
            return Results.Ok(new
            {
                bot.Id,
                bot.Name,
                bot.Slug,
                bot.Accent,
                bot.CreatedAt,
                Owner = owner,
                IsOwner = isOwner,
                Versions = bot.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => new
                    {
                        v.Id,
                        v.VersionNumber,
                        Status = v.Status.ToString(),
                        v.ArtifactHash,
                        v.IsActive,
                        v.CreatedAt,
                        // Build logs and sources are owner-only (plan §13.3, §14).
                        BuildLog = isOwner ? v.BuildLog : null,
                        EntryType = isOwner ? v.EntryType : null,
                        Sources = isOwner
                            ? JsonSerializer.Deserialize<List<SourceFile>>(v.SourcesJson)
                            : null,
                    }),
            });
        });

        // Slim polling view (gen-2 finding #8): build-status pollers shouldn't re-download
        // every version's sources and log on each poll.
        group.MapGet("/{botId:guid}/build-status", async (Guid botId, AppDbContext db) =>
        {
            var versions = await db.BotVersions
                .Where(v => v.BotId == botId)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new
                {
                    v.Id, v.VersionNumber, Status = v.Status.ToString(), v.ArtifactHash,
                    v.IsActive, v.CreatedAt, v.BuiltAt,
                })
                .ToListAsync();
            return versions.Count == 0 && !await db.Bots.AnyAsync(b => b.Id == botId)
                ? Results.NotFound()
                : Results.Ok(versions);
        });

        group.MapPost("/{botId:guid}/versions",
            async (Guid botId, SubmitVersionRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var bot = await db.Bots.Include(b => b.Versions).SingleOrDefaultAsync(b => b.Id == botId);
            if (bot is null)
                return Results.NotFound();
            if (principal.UserId() != bot.OwnerUserId)
                return Results.Forbid();
            if (bot.Versions.Any(v => v.Status is BuildStatus.Pending or BuildStatus.Building))
                return Results.Problem("A build is already in progress for this bot.", statusCode: 409);

            // Null-tolerant: absent name/content must 400 via validation below, not 500 here.
            var sources = (request.Files ?? [])
                .Select(f => new SourceFile((f.Name ?? "").Trim(), f.Content ?? ""))
                .ToArray();
            try
            {
                // Fail fast on obviously invalid submissions; the job re-validates.
                _ = BotBuilder.ComputeCacheKey(sources, request.EntryType);
                if (sources.Length == 0)
                    return Results.Problem("At least one source file is required.", statusCode: 400);
                if (sources.Any(s => !s.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                    return Results.Problem("Only .cs files are accepted.", statusCode: 400);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }

            var version = new BotVersion
            {
                BotId = bot.Id,
                VersionNumber = bot.Versions.Count == 0 ? 1 : bot.Versions.Max(v => v.VersionNumber) + 1,
                EntryType = request.EntryType.Trim(),
                SourcesJson = JsonSerializer.Serialize(sources),
                SourceHash = Convert.ToHexStringLower(SHA256.HashData(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sources)))),
            };
            db.BotVersions.Add(version);
            db.BackgroundJobs.Add(BackgroundJob.CompileSubmission(version.Id));
            await db.SaveChangesAsync();
            return Results.Ok(new { version.Id, version.VersionNumber, Status = version.Status.ToString() });
        }).RequireAuthorization().RequireRateLimiting("submission");

        group.MapGet("/{botId:guid}/matches", async (Guid botId, AppDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var matches = await db.Matches
                .Include(m => m.Participants)
                .Where(m => m.Participants.Any(p => p.BotId == botId))
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .ToListAsync();
            int wins = 0, losses = 0, draws = 0;
            var rows = matches.Select(m =>
            {
                bool visible = m.BroadcastComplete(now);
                var self = m.Participants.Single(p => p.BotId == botId);
                if (visible && m.Status == Matches.MatchStatus.Completed)
                {
                    if (m.WinnerSlot == self.Slot) wins++;
                    else if (m.WinnerSlot is null) draws++;
                    else losses++;
                }
                return new
                {
                    m.Id,
                    m.MapId,
                    Status = m.Status.ToString(),
                    Broadcasting = m.Status == Matches.MatchStatus.Completed && !visible,
                    m.MatchSetId,
                    m.SetGame,
                    m.CreatedAt,
                    Outcome = visible ? (m.WinnerSlot == self.Slot ? "Win" : m.WinnerSlot is null ? "Draw" : "Loss") : null,
                    Opponent = m.Participants.Where(p => p.BotId != botId)
                        .Select(p => new { p.BotId, p.NameSnapshot, p.AccentSnapshot })
                        .FirstOrDefault(),
                };
            }).ToList();
            return Results.Ok(new { Wins = wins, Losses = losses, Draws = draws, Matches = rows });
        });

        group.MapGet("/mine", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            var bots = await db.Bots
                .Where(b => b.OwnerUserId == userId)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.Accent,
                    LatestVersion = b.Versions.OrderByDescending(v => v.VersionNumber)
                        .Select(v => new { v.VersionNumber, Status = v.Status.ToString(), v.IsActive })
                        .FirstOrDefault(),
                })
                .OrderBy(b => b.Name)
                .ToListAsync();
            return Results.Ok(bots);
        }).RequireAuthorization();
    }

    public static string Slugify(string name)
    {
        var builder = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }
}
