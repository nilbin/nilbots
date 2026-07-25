using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Cosmetics;
using BotArena.App.Jobs;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record CreateBotRequest(
    string Name,
    string? Accent,
    string? LookId = null,
    string? ProjectileLookId = null);
public sealed record SubmitVersionRequest(
    string EntryType,
    List<SourceFileDto> Files,
    string? LookId = null,
    string? ProjectileLookId = null);
public sealed record UpdateBotAppearanceRequest(
    string Accent,
    string LookId,
    string ProjectileLookId);
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
                    b.LookId,
                    b.ProjectileLookId,
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

        group.MapPost("/", async (
            CreateBotRequest request,
            ClaimsPrincipal principal,
            AppDbContext db,
            CosmeticEntitlementService entitlements,
            CancellationToken cancellationToken) =>
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
            string lookId = (request.LookId ?? "vanguard").Trim().ToLowerInvariant();
            if (!IsPresentationId(lookId))
                return Results.Problem("Bot look must be a lowercase kebab-case ID.", statusCode: 400);
            string projectileLookId = (request.ProjectileLookId ?? "pulse-bolt")
                .Trim()
                .ToLowerInvariant();
            if (!IsPresentationId(projectileLookId))
                return Results.Problem(
                    "Projectile look must be a lowercase kebab-case ID.",
                    statusCode: 400);
            if (await CosmeticAccessProblem(
                    entitlements,
                    userId,
                    CosmeticCatalog.BotLookKind,
                    lookId,
                    cancellationToken) is { } lookProblem)
            {
                return lookProblem;
            }
            if (await CosmeticAccessProblem(
                    entitlements,
                    userId,
                    CosmeticCatalog.ProjectileLookKind,
                    projectileLookId,
                    cancellationToken) is { } projectileProblem)
            {
                return projectileProblem;
            }
            var bot = new Bot
            {
                OwnerUserId = userId,
                Name = name,
                Slug = slug,
                Accent = accent,
                LookId = lookId,
                ProjectileLookId = projectileLookId,
            };
            db.Bots.Add(bot);
            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                bot.Id,
                bot.Name,
                bot.Slug,
                bot.Accent,
                bot.LookId,
                bot.ProjectileLookId,
            });
        }).RequireAuthorization();

        // Keyed by slug OR id. Every bot has a unique, immutable slug, so the public
        // URL of a bot can read `/bots/murder-roomba` instead of a raw GUID; the id form
        // keeps working for anything already holding one.
        group.MapGet("/{key}", async (string key, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var bot = Guid.TryParse(key, out var botId)
                ? await db.Bots.Include(b => b.Versions).SingleOrDefaultAsync(b => b.Id == botId)
                : await db.Bots.Include(b => b.Versions).SingleOrDefaultAsync(b => b.Slug == key);
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
                bot.LookId,
                bot.ProjectileLookId,
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
                        BuildReceipt = DeserializeReceipt(v.BuildReceiptJson),
                        // Build logs and sources are owner-only (plan §13.3, §14).
                        BuildLog = isOwner ? v.BuildLog : null,
                        EntryType = isOwner ? v.EntryType : null,
                        Sources = isOwner
                            ? JsonSerializer.Deserialize<List<SourceFile>>(v.SourcesJson)
                            : null,
                    }),
            });
        });

        // Appearance is mutable independently of source versions. This endpoint is
        // also the future entitlement-enforcement boundary: ownership is checked when
        // equipping, while historical match snapshots remain immutable.
        group.MapPut(
            "/{botId:guid}/appearance",
            async (
                Guid botId,
                UpdateBotAppearanceRequest request,
                ClaimsPrincipal principal,
                AppDbContext db,
                CosmeticEntitlementService entitlements,
                CancellationToken cancellationToken) =>
            {
                var bot = await db.Bots.SingleOrDefaultAsync(b => b.Id == botId);
                if (bot is null)
                    return Results.NotFound();
                if (principal.UserId() is not Guid userId || userId != bot.OwnerUserId)
                    return Results.Forbid();

                string accent = request.Accent?.Trim() ?? "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(accent, "^#[0-9a-fA-F]{6}$"))
                    return Results.Problem(
                        "Accent must be a six-digit hexadecimal color.",
                        statusCode: 400);
                string lookId = request.LookId?.Trim().ToLowerInvariant() ?? "";
                if (!IsPresentationId(lookId))
                    return Results.Problem(
                        "Bot look must be a lowercase kebab-case ID.",
                        statusCode: 400);
                string projectileLookId =
                    request.ProjectileLookId?.Trim().ToLowerInvariant() ?? "";
                if (!IsPresentationId(projectileLookId))
                    return Results.Problem(
                        "Projectile look must be a lowercase kebab-case ID.",
                        statusCode: 400);
                if (await CosmeticAccessProblem(
                        entitlements,
                        userId,
                        CosmeticCatalog.BotLookKind,
                        lookId,
                        cancellationToken) is { } lookProblem)
                {
                    return lookProblem;
                }
                if (await CosmeticAccessProblem(
                        entitlements,
                        userId,
                        CosmeticCatalog.ProjectileLookKind,
                        projectileLookId,
                        cancellationToken) is { } projectileProblem)
                {
                    return projectileProblem;
                }

                bot.Accent = accent;
                bot.LookId = lookId;
                bot.ProjectileLookId = projectileLookId;
                await db.SaveChangesAsync();
                return Results.Ok(new
                {
                    bot.Id,
                    bot.Accent,
                    bot.LookId,
                    bot.ProjectileLookId,
                });
            })
            .RequireAuthorization();

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

        group.MapGet(
            "/{botId:guid}/versions/{versionId:guid}/artifact",
            async (
                Guid botId,
                Guid versionId,
                AppDbContext db,
                IObjectStore objectStore,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var artifact = await db.BotVersions
                    .Where(v => v.Id == versionId && v.BotId == botId &&
                                v.Status == BuildStatus.Built &&
                                v.ArtifactKey != null && v.ArtifactHash != null)
                    .Join(
                        db.Bots,
                        version => version.BotId,
                        bot => bot.Id,
                        (version, bot) => new
                        {
                            version.ArtifactKey,
                            version.ArtifactHash,
                            version.VersionNumber,
                            bot.Slug,
                        })
                    .SingleOrDefaultAsync(cancellationToken);
                if (artifact is null)
                    return Results.NotFound();

                Stream? stream = await objectStore.OpenReadAsync(
                    artifact.ArtifactKey!,
                    cancellationToken);
                if (stream is null)
                    return Results.NotFound();

                http.Response.Headers.ETag = $"\"sha256-{artifact.ArtifactHash}\"";
                http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                http.Response.Headers["X-Content-SHA256"] = artifact.ArtifactHash;
                return Results.Stream(
                    stream,
                    "application/wasm",
                    $"{artifact.Slug}-v{artifact.VersionNumber}.wasm",
                    enableRangeProcessing: true);
            });

        group.MapPost("/{botId:guid}/versions",
            async (
                Guid botId,
                SubmitVersionRequest request,
                ClaimsPrincipal principal,
                AppDbContext db,
                CosmeticEntitlementService entitlements,
                CompilerSubmissionService submissions,
                HttpContext http,
                CancellationToken cancellationToken) =>
        {
            var bot = await db.Bots.SingleOrDefaultAsync(
                b => b.Id == botId,
                cancellationToken);
            if (bot is null)
                return Results.NotFound();
            if (principal.UserId() is not Guid userId || userId != bot.OwnerUserId)
                return Results.Forbid();

            // Null-tolerant: absent name/content must 400 via validation below, not 500 here.
            var sources = (request.Files ?? [])
                .Select(f => new SourceFile((f.Name ?? "").Trim(), f.Content ?? ""))
                .ToArray();
            string entryType = request.EntryType?.Trim() ?? "";
            string? lookId = request.LookId?.Trim().ToLowerInvariant();
            if (lookId is not null && !IsPresentationId(lookId))
                return Results.Problem("Bot look must be a lowercase kebab-case ID.", statusCode: 400);
            string? projectileLookId =
                request.ProjectileLookId?.Trim().ToLowerInvariant();
            if (projectileLookId is not null && !IsPresentationId(projectileLookId))
                return Results.Problem(
                    "Projectile look must be a lowercase kebab-case ID.",
                    statusCode: 400);
            if (lookId is not null &&
                await CosmeticAccessProblem(
                    entitlements,
                    userId,
                    CosmeticCatalog.BotLookKind,
                    lookId,
                    cancellationToken) is { } lookProblem)
            {
                return lookProblem;
            }
            if (projectileLookId is not null &&
                await CosmeticAccessProblem(
                    entitlements,
                    userId,
                    CosmeticCatalog.ProjectileLookKind,
                    projectileLookId,
                    cancellationToken) is { } projectileProblem)
            {
                return projectileProblem;
            }
            try
            {
                // Fail fast on obviously invalid submissions; the job re-validates.
                BotBuilder.ValidateSubmission(sources, entryType);
                if (sources.Length == 0)
                    return Results.Problem("At least one source file is required.", statusCode: 400);
                if (sources.Any(s => !s.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                    return Results.Problem("Only .cs files are accepted.", statusCode: 400);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }

            if (lookId is not null)
                bot.LookId = lookId;
            if (projectileLookId is not null)
                bot.ProjectileLookId = projectileLookId;
            CompilerSubmissionDecision decision = await submissions.EnqueueAsync(
                bot.Id,
                userId,
                entryType,
                sources,
                http.Connection.RemoteIpAddress,
                cancellationToken);
            if (!decision.Accepted)
            {
                CompilerSubmissionDenial denial = decision.Denial!;
                http.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(denial.RetryAfter.TotalSeconds)).ToString();
                return Results.Problem(denial.Message, statusCode: StatusCodes.Status429TooManyRequests);
            }

            BotVersion version = decision.Version!;
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
                    b.LookId,
                    b.ProjectileLookId,
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

    private static BuildReceipt? DeserializeReceipt(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<BuildReceipt>(json);

    private static async Task<IResult?> CosmeticAccessProblem(
        CosmeticEntitlementService entitlements,
        Guid userId,
        string kind,
        string id,
        CancellationToken cancellationToken)
    {
        CosmeticAccess access = await entitlements.CheckAccessAsync(
            userId,
            kind,
            id,
            cancellationToken);
        if (access.Item is null)
        {
            string label = kind == CosmeticCatalog.BotLookKind
                ? "bot look"
                : "projectile look";
            return Results.Problem(
                $"Unknown {label} '{id}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (!access.Owned)
        {
            string hint = access.Item.Unlock?.Hint is { Length: > 0 } value
                ? $" {value}"
                : "";
            return Results.Problem(
                $"{access.Item.Label} is locked.{hint}",
                statusCode: StatusCodes.Status403Forbidden);
        }
        return null;
    }

    private static bool IsPresentationId(string value) =>
        value.Length is > 0 and <= 64 &&
        value[0] is >= 'a' and <= 'z' &&
        value[^1] != '-' &&
        value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
