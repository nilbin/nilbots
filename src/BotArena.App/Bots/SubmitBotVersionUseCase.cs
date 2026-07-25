using System.Diagnostics;
using System.Net;
using BotArena.App.Shared;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record SubmitBotVersionCommand(
    Guid BotId,
    string? EntryType,
    IReadOnlyList<SourceFile> Sources,
    string? BotLookId,
    string? ProjectileLookId,
    IPAddress? RemoteAddress);

public sealed record SubmittedBotVersion(
    Guid Id,
    int VersionNumber,
    string Status);

public sealed class SubmitBotVersionUseCase(
    AppDbContext db,
    BotAppearancePolicy appearancePolicy,
    CompilerSubmissionService submissions,
    ILogger<SubmitBotVersionUseCase> logger)
{
    /// <summary>
    /// Transaction: CompilerSubmissionService holds PostgreSQL advisory locks and
    /// commits the optional appearance update, version, and durable job together.
    /// Retry: a denied/failed attempt may be retried; an accepted request is not
    /// idempotent and receives a new version number if submitted again after its build
    /// leaves the active queue. Worker death after commit is recovered from the job row.
    /// </summary>
    public async Task<ApplicationResult<SubmittedBotVersion>> ExecuteAsync(
        ApplicationActor actor,
        SubmitBotVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            ApplicationTelemetry.ActivitySource.StartActivity("bots.submit_version");
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
                "You can only submit versions for your own bot.",
                accountId,
                command.BotId);
        }

        ApplicationResult<BotAppearance> appearance =
            await appearancePolicy.ValidateForSubmissionAsync(
                accountId,
                bot,
                command.BotLookId,
                command.ProjectileLookId,
                cancellationToken);
        if (!appearance.Succeeded)
            return Failure(appearance.Error!, accountId, bot.Id);

        string entryType = command.EntryType?.Trim() ?? "";
        try
        {
            BotBuilder.ValidateSubmission(command.Sources, entryType);
            if (command.Sources.Count == 0)
                return InvalidSubmission("At least one source file is required.", accountId, bot.Id);
            if (command.Sources.Any(source =>
                    !source.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                return InvalidSubmission("Only .cs files are accepted.", accountId, bot.Id);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return InvalidSubmission(exception.Message, accountId, bot.Id);
        }

        BotAppearance value = appearance.Value!;
        bot.Accent = value.Accent.Value;
        bot.LookId = value.BotLook.Value;
        bot.ProjectileLookId = value.ProjectileLook.Value;
        CompilerSubmissionDecision decision = await submissions.EnqueueAsync(
            bot.Id,
            accountId,
            entryType,
            command.Sources,
            command.RemoteAddress,
            cancellationToken);
        if (!decision.Accepted)
        {
            CompilerSubmissionDenial denial = decision.Denial!;
            return Failure(
                new ApplicationError(
                    ApplicationErrorCodes.SubmissionRateLimited,
                    ApplicationErrorType.RateLimit,
                    denial.Message,
                    denial.RetryAfter),
                accountId,
                bot.Id);
        }

        BotVersion version = decision.Version!;
        logger.LogInformation(
            "Bot {BotId} version {BotVersionId} submitted by account {AccountId}",
            bot.Id,
            version.Id,
            accountId);
        ApplicationTelemetry.Record("bots.submit_version", "accepted", accountId, bot.Id);
        return ApplicationResult<SubmittedBotVersion>.Success(new(
            version.Id,
            version.VersionNumber,
            version.Status.ToString()));
    }

    private static ApplicationResult<SubmittedBotVersion> InvalidSubmission(
        string detail,
        Guid accountId,
        Guid botId) =>
        Failure(
            ApplicationErrorCodes.SubmissionInvalid,
            ApplicationErrorType.Validation,
            detail,
            accountId,
            botId);

    private static ApplicationResult<SubmittedBotVersion> Failure(
        string code,
        ApplicationErrorType type,
        string detail,
        Guid? accountId,
        Guid botId) =>
        Failure(new ApplicationError(code, type, detail), accountId, botId);

    private static ApplicationResult<SubmittedBotVersion> Failure(
        ApplicationError error,
        Guid? accountId,
        Guid botId)
    {
        ApplicationTelemetry.Record("bots.submit_version", error.Code, accountId, botId);
        return ApplicationResult<SubmittedBotVersion>.Failure(error);
    }
}
