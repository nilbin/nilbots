using System.Diagnostics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record AssignBotClassCommand(Guid BotId, string? ClassId);

public sealed record AssignedBotClass(Guid Id, string ClassId);

public sealed class AssignBotClassUseCase(
    AppDbContext db,
    BotClassPolicy classPolicy,
    ILogger<AssignBotClassUseCase> logger)
{
    /// <summary>
    /// The conditional update is the concurrency boundary: only a null class may be
    /// changed. Two competing first assignments cannot both succeed, while a retry of
    /// the winning value remains idempotent after the row is re-read.
    /// </summary>
    public async Task<ApplicationResult<AssignedBotClass>> ExecuteAsync(
        ApplicationActor actor,
        AssignBotClassCommand command,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            ApplicationTelemetry.ActivitySource.StartActivity("bots.assign_class");
        activity?.SetTag("account.id", actor.AccountId);
        activity?.SetTag("bot.id", command.BotId);

        if (actor.AccountId is not Guid accountId)
        {
            return Failure(
                ApplicationErrorCodes.AuthenticationRequired,
                ApplicationErrorType.Authentication,
                "Authentication is required.",
                null,
                command.BotId);
        }

        var current = await db.Bots
            .AsNoTracking()
            .Where(bot => bot.Id == command.BotId)
            .Select(bot => new { bot.OwnerUserId, bot.ClassId })
            .SingleOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return Failure(
                ApplicationErrorCodes.BotNotFound,
                ApplicationErrorType.NotFound,
                "Bot not found.",
                accountId,
                command.BotId);
        }
        if (current.OwnerUserId != accountId)
        {
            return Failure(
                ApplicationErrorCodes.BotOwnershipRequired,
                ApplicationErrorType.Authorization,
                "You can only assign a class to your own bot.",
                accountId,
                command.BotId);
        }

        ApplicationResult<string?> validated =
            classPolicy.ValidateForAssignment(command.ClassId);
        if (!validated.Succeeded)
            return Failure(validated.Error!, accountId, command.BotId);
        string classId = validated.Value!;
        activity?.SetTag("bot.class_id", classId);

        if (current.ClassId == classId)
            return Success(command.BotId, classId, accountId, changed: false);
        if (current.ClassId is not null)
            return AlreadyAssigned(accountId, command.BotId);

        int affected = await db.Bots
            .Where(bot =>
                bot.Id == command.BotId &&
                bot.OwnerUserId == accountId &&
                bot.ClassId == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(bot => bot.ClassId, classId),
                cancellationToken);
        if (affected == 1)
            return Success(command.BotId, classId, accountId, changed: true);

        // A concurrent request won after the initial read. Repeating its exact value is
        // still a success; a different winner is the immutable-assignment conflict.
        string? assigned = await db.Bots
            .AsNoTracking()
            .Where(bot => bot.Id == command.BotId && bot.OwnerUserId == accountId)
            .Select(bot => bot.ClassId)
            .SingleOrDefaultAsync(cancellationToken);
        return assigned == classId
            ? Success(command.BotId, classId, accountId, changed: false)
            : AlreadyAssigned(accountId, command.BotId);
    }

    private ApplicationResult<AssignedBotClass> Success(
        Guid botId,
        string classId,
        Guid accountId,
        bool changed)
    {
        if (changed)
        {
            logger.LogInformation(
                "Bot {BotId} assigned class {ClassId} by account {AccountId}",
                botId,
                classId,
                accountId);
        }
        ApplicationTelemetry.Record(
            "bots.assign_class",
            changed ? "assigned" : "unchanged",
            accountId,
            botId);
        return ApplicationResult<AssignedBotClass>.Success(new(botId, classId));
    }

    private static ApplicationResult<AssignedBotClass> AlreadyAssigned(
        Guid accountId,
        Guid botId) =>
        Failure(
            ApplicationErrorCodes.BotClassAlreadyAssigned,
            ApplicationErrorType.Conflict,
            "This bot already has an immutable class assignment.",
            accountId,
            botId);

    private static ApplicationResult<AssignedBotClass> Failure(
        string code,
        ApplicationErrorType type,
        string detail,
        Guid? accountId,
        Guid botId) =>
        Failure(new ApplicationError(code, type, detail), accountId, botId);

    private static ApplicationResult<AssignedBotClass> Failure(
        ApplicationError error,
        Guid? accountId,
        Guid botId)
    {
        ApplicationTelemetry.Record("bots.assign_class", error.Code, accountId, botId);
        return ApplicationResult<AssignedBotClass>.Failure(error);
    }
}
