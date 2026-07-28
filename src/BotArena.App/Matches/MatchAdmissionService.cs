using System.Diagnostics;
using BotArena.App.Bots;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

public sealed record AdmittedMatchBot(
    Bot Bot,
    BotVersion Version,
    string OwnerDisplayName);

/// <summary>
/// Owns the eligibility shared by ranked and unranked match creation: existence,
/// optional challenger ownership, currently authorized appearance, and one active
/// successfully built version.
/// </summary>
public sealed class MatchAdmissionService(
    AppDbContext db,
    BotAppearancePolicy appearancePolicy)
{
    public Task<ApplicationResult<AdmittedMatchBot>> AdmitAsync(
        Guid botId,
        Guid? requiredOwnerUserId,
        CancellationToken cancellationToken = default) =>
        AdmitForProfileAsync(
            botId,
            requiredOwnerUserId,
            BotContractProfiles.LegacyDuel,
            cancellationToken);

    public async Task<ApplicationResult<AdmittedMatchBot>> AdmitForProfileAsync(
        Guid botId,
        Guid? requiredOwnerUserId,
        string requiredContractProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredContractProfileId);
        using Activity? activity =
            ApplicationTelemetry.ActivitySource.StartActivity("matches.admit_participant");
        activity?.SetTag("account.id", requiredOwnerUserId);
        activity?.SetTag("bot.id", botId);
        activity?.SetTag(
            "bot.required_contract_profile",
            requiredContractProfileId);

        Bot? bot = await db.Bots.SingleOrDefaultAsync(
            candidate => candidate.Id == botId,
            cancellationToken);
        if (bot is null)
        {
            return Failure(
                new ApplicationError(
                    ApplicationErrorCodes.BotNotFound,
                    ApplicationErrorType.NotFound,
                    "Bot not found."),
                requiredOwnerUserId,
                botId);
        }
        if (requiredOwnerUserId is Guid ownerUserId &&
            bot.OwnerUserId != ownerUserId)
        {
            return Failure(
                new ApplicationError(
                    ApplicationErrorCodes.BotOwnershipRequired,
                    ApplicationErrorType.Authorization,
                    "You can only start matches with your own bot."),
                ownerUserId,
                bot.Id);
        }

        ApplicationResult<BotAppearance> appearance =
            await appearancePolicy.ValidateForMatchAdmissionAsync(
                bot,
                cancellationToken);
        if (!appearance.Succeeded)
            return Failure(appearance.Error!, requiredOwnerUserId, bot.Id);

        BotVersion? version = await db.BotVersions
            .Where(candidate =>
                candidate.BotId == bot.Id &&
                candidate.IsActive &&
                candidate.Status == BuildStatus.Built)
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return Failure(
                new ApplicationError(
                    ApplicationErrorCodes.MatchActiveVersionRequired,
                    ApplicationErrorType.Conflict,
                    $"{bot.Name} has no successfully built active version."),
                requiredOwnerUserId,
                bot.Id);
        }
        if (!BotContractProfiles.Supports(
                version.SupportedContractProfiles,
                requiredContractProfileId))
        {
            return Failure(
                new ApplicationError(
                    ApplicationErrorCodes.MatchContractProfileRequired,
                    ApplicationErrorType.Conflict,
                    $"{bot.Name}'s active version does not support contract " +
                    $"profile '{requiredContractProfileId}'."),
                requiredOwnerUserId,
                bot.Id);
        }

        string ownerDisplayName = await db.Users
            .Where(user => user.Id == bot.OwnerUserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);
        ApplicationTelemetry.Record(
            "matches.admit_participant",
            "admitted",
            requiredOwnerUserId,
            bot.Id);
        return ApplicationResult<AdmittedMatchBot>.Success(
            new AdmittedMatchBot(bot, version, ownerDisplayName));
    }

    private static ApplicationResult<AdmittedMatchBot> Failure(
        ApplicationError error,
        Guid? accountId,
        Guid botId)
    {
        ApplicationTelemetry.Record(
            "matches.admit_participant",
            error.Code,
            accountId,
            botId);
        return ApplicationResult<AdmittedMatchBot>.Failure(error);
    }
}
