using System.Diagnostics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BotArena.App.Bots;

public sealed record CreateBotCommand(
    string? Name,
    string? Accent,
    string? BotLookId,
    string? ProjectileLookId);

public sealed record CreatedBot(
    Guid Id,
    string Name,
    string Slug,
    string Accent,
    string LookId,
    string ProjectileLookId);

public sealed class CreateBotUseCase(
    AppDbContext db,
    BotAppearancePolicy appearancePolicy,
    TimeProvider timeProvider,
    ILogger<CreateBotUseCase> logger)
{
    /// <summary>
    /// Transaction: the bot is one SaveChanges transaction; the slug unique index is
    /// authoritative after the friendly pre-check. Retry: safe only after observing a
    /// failure. Idempotency: none—successful retries intentionally create no duplicate
    /// because the immutable slug conflicts, but callers receive no idempotency key.
    /// </summary>
    public async Task<ApplicationResult<CreatedBot>> ExecuteAsync(
        ApplicationActor actor,
        CreateBotCommand command,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ApplicationTelemetry.ActivitySource.StartActivity("bots.create");
        activity?.SetTag("account.id", actor.AccountId);
        if (actor.AccountId is not Guid accountId)
            return Failure(ApplicationErrorCodes.AuthenticationRequired, ApplicationErrorType.Authentication,
                "Authentication is required.");

        string name = command.Name?.Trim() ?? "";
        if (name.Length is < 2 or > 40)
        {
            return Failure(
                ApplicationErrorCodes.BotNameInvalid,
                ApplicationErrorType.Validation,
                "Bot name must be 2-40 characters.",
                accountId);
        }
        string slug = Slugify(name);
        if (await db.Bots.AnyAsync(bot => bot.Slug == slug, cancellationToken))
        {
            return Failure(
                ApplicationErrorCodes.BotNameConflict,
                ApplicationErrorType.Conflict,
                $"A bot named '{name}' already exists.",
                accountId);
        }

        ApplicationResult<BotAppearance> appearance =
            await appearancePolicy.ValidateForCreationAsync(
                accountId,
                command.Accent,
                command.BotLookId,
                command.ProjectileLookId,
                cancellationToken);
        if (!appearance.Succeeded)
            return Failure(appearance.Error!, accountId);

        BotAppearance value = appearance.Value!;
        var bot = new Bot
        {
            OwnerUserId = accountId,
            Name = name,
            Slug = slug,
            Accent = value.Accent.Value,
            LookId = value.BotLook.Value,
            ProjectileLookId = value.ProjectileLook.Value,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Bots.Add(bot);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Bots_Slug",
            })
        {
            return Failure(
                ApplicationErrorCodes.BotNameConflict,
                ApplicationErrorType.Conflict,
                $"A bot named '{name}' already exists.",
                accountId);
        }

        logger.LogInformation(
            "Bot {BotId} created by account {AccountId} with look {BotLookId} and projectile {ProjectileLookId}",
            bot.Id,
            accountId,
            bot.LookId,
            bot.ProjectileLookId);
        ApplicationTelemetry.Record("bots.create", "created", accountId, bot.Id);
        return ApplicationResult<CreatedBot>.Success(new(
            bot.Id,
            bot.Name,
            bot.Slug,
            bot.Accent,
            bot.LookId,
            bot.ProjectileLookId));
    }

    private static string Slugify(string name)
    {
        var builder = new System.Text.StringBuilder();
        foreach (char character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
                builder.Append(character);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }

    private static ApplicationResult<CreatedBot> Failure(
        string code,
        ApplicationErrorType type,
        string detail,
        Guid? accountId = null) =>
        Failure(new ApplicationError(code, type, detail), accountId);

    private static ApplicationResult<CreatedBot> Failure(
        ApplicationError error,
        Guid? accountId = null)
    {
        ApplicationTelemetry.Record("bots.create", error.Code, accountId);
        return ApplicationResult<CreatedBot>.Failure(error);
    }
}
