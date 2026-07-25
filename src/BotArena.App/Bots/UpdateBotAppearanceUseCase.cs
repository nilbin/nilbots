using System.Diagnostics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record UpdateBotAppearanceCommand(
    Guid BotId,
    string? Accent,
    string? BotLookId,
    string? ProjectileLookId);

public sealed record UpdatedBotAppearance(
    Guid Id,
    string Accent,
    string LookId,
    string ProjectileLookId);

public sealed class UpdateBotAppearanceUseCase(
    AppDbContext db,
    BotAppearancePolicy appearancePolicy,
    ILogger<UpdateBotAppearanceUseCase> logger)
{
    /// <summary>
    /// Transaction: one SaveChanges transaction updates one bot row. No explicit lock;
    /// concurrent valid edits are last-write-wins. Retry: safe after a transport
    /// failure. Idempotency: state-based—repeating the same appearance has no further
    /// product effect and does not create a ledger row.
    /// </summary>
    public async Task<ApplicationResult<UpdatedBotAppearance>> ExecuteAsync(
        ApplicationActor actor,
        UpdateBotAppearanceCommand command,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            ApplicationTelemetry.ActivitySource.StartActivity("bots.update_appearance");
        activity?.SetTag("account.id", actor.AccountId);
        activity?.SetTag("bot.id", command.BotId);
        if (actor.AccountId is not Guid accountId)
            return Failure(ApplicationErrorCodes.AuthenticationRequired, ApplicationErrorType.Authentication,
                "Authentication is required.", null, command.BotId);

        Bot? bot = await db.Bots.SingleOrDefaultAsync(
            candidate => candidate.Id == command.BotId,
            cancellationToken);
        if (bot is null)
        {
            return Failure(
                ApplicationErrorCodes.BotNotFound,
                ApplicationErrorType.NotFound,
                "Bot not found.",
                accountId,
                command.BotId);
        }
        if (bot.OwnerUserId != accountId)
        {
            return Failure(
                ApplicationErrorCodes.BotOwnershipRequired,
                ApplicationErrorType.Authorization,
                "You can only change the appearance of your own bot.",
                accountId,
                command.BotId);
        }

        ApplicationResult<BotAppearance> appearance =
            await appearancePolicy.ValidateForUpdateAsync(
                accountId,
                command.Accent,
                command.BotLookId,
                command.ProjectileLookId,
                cancellationToken);
        if (!appearance.Succeeded)
            return Failure(appearance.Error!, accountId, command.BotId);

        BotAppearance value = appearance.Value!;
        bot.Accent = value.Accent.Value;
        bot.LookId = value.BotLook.Value;
        bot.ProjectileLookId = value.ProjectileLook.Value;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bot {BotId} appearance changed by account {AccountId} to look {BotLookId} and projectile {ProjectileLookId}",
            bot.Id,
            accountId,
            bot.LookId,
            bot.ProjectileLookId);
        ApplicationTelemetry.Record("bots.update_appearance", "updated", accountId, bot.Id);
        return ApplicationResult<UpdatedBotAppearance>.Success(new(
            bot.Id,
            bot.Accent,
            bot.LookId,
            bot.ProjectileLookId));
    }

    private static ApplicationResult<UpdatedBotAppearance> Failure(
        string code,
        ApplicationErrorType type,
        string detail,
        Guid? accountId,
        Guid botId) =>
        Failure(new ApplicationError(code, type, detail), accountId, botId);

    private static ApplicationResult<UpdatedBotAppearance> Failure(
        ApplicationError error,
        Guid? accountId,
        Guid botId)
    {
        ApplicationTelemetry.Record("bots.update_appearance", error.Code, accountId, botId);
        return ApplicationResult<UpdatedBotAppearance>.Failure(error);
    }
}
