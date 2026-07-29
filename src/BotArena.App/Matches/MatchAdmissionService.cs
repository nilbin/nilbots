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

    /// <summary>
    /// Projects authoritative admission for many potential opponents with a
    /// bounded number of database queries. Callers that need to prove ownership
    /// of one initiating bot should continue to use <see cref="AdmitAsync"/>.
    /// </summary>
    public Task<
        IReadOnlyDictionary<Guid, ApplicationResult<AdmittedMatchBot>>>
        AdmitManyAsync(
            IReadOnlyCollection<Guid> botIds,
            CancellationToken cancellationToken = default) =>
        AdmitManyForProfileAsync(
            botIds,
            BotContractProfiles.LegacyDuel,
            cancellationToken);

    public async Task<
        IReadOnlyDictionary<Guid, ApplicationResult<AdmittedMatchBot>>>
        AdmitManyForProfileAsync(
            IReadOnlyCollection<Guid> botIds,
            string requiredContractProfileId,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(botIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            requiredContractProfileId);

        Guid[] ids = [.. botIds.Distinct()];
        if (ids.Length == 0)
        {
            return new Dictionary<
                Guid,
                ApplicationResult<AdmittedMatchBot>>();
        }

        List<Bot> bots = await db.Bots
            .AsNoTracking()
            .Where(bot => ids.Contains(bot.Id))
            .ToListAsync(cancellationToken);
        var botsById = bots.ToDictionary(bot => bot.Id);
        IReadOnlyDictionary<
            Guid,
            ApplicationResult<BotAppearance>> appearances =
            await appearancePolicy.ValidateForMatchAdmissionBatchAsync(
                bots,
                cancellationToken);
        List<BotVersion> activeBuiltVersions = await db.BotVersions
            .AsNoTracking()
            .Where(version =>
                ids.Contains(version.BotId) &&
                version.IsActive &&
                version.Status == BuildStatus.Built)
            .ToListAsync(cancellationToken);
        ILookup<Guid, BotVersion> versionsByBot =
            activeBuiltVersions.ToLookup(version => version.BotId);
        Guid[] ownerIds =
            [.. bots.Select(bot => bot.OwnerUserId).Distinct()];
        Dictionary<Guid, string> ownerNames = await db.Users
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.DisplayName,
                cancellationToken);

        var results =
            new Dictionary<
                Guid,
                ApplicationResult<AdmittedMatchBot>>(ids.Length);
        foreach (Guid botId in ids)
        {
            if (!botsById.TryGetValue(botId, out Bot? bot))
            {
                results.Add(
                    botId,
                    BatchFailure(
                        new ApplicationError(
                            ApplicationErrorCodes.BotNotFound,
                            ApplicationErrorType.NotFound,
                            "Bot not found.")));
                continue;
            }

            ApplicationResult<BotAppearance> appearance =
                appearances[bot.Id];
            if (!appearance.Succeeded)
            {
                results.Add(
                    bot.Id,
                    BatchFailure(
                        appearance.Error!));
                continue;
            }

            BotVersion? version =
                versionsByBot[bot.Id].SingleOrDefault();
            if (version is null)
            {
                results.Add(
                    bot.Id,
                    BatchFailure(
                        new ApplicationError(
                            ApplicationErrorCodes
                                .MatchActiveVersionRequired,
                            ApplicationErrorType.Conflict,
                            $"{bot.Name} has no successfully built active " +
                            "version.")));
                continue;
            }
            if (!BotContractProfiles.Supports(
                    version.SupportedContractProfiles,
                    requiredContractProfileId))
            {
                results.Add(
                    bot.Id,
                    BatchFailure(
                        new ApplicationError(
                            ApplicationErrorCodes
                                .MatchContractProfileRequired,
                            ApplicationErrorType.Conflict,
                            $"{bot.Name}'s active version does not support " +
                            $"contract profile " +
                            $"'{requiredContractProfileId}'.")));
                continue;
            }

            if (!ownerNames.TryGetValue(
                    bot.OwnerUserId,
                    out string? ownerDisplayName))
            {
                throw new InvalidOperationException(
                    $"Owner {bot.OwnerUserId} for bot {bot.Id} was not found.");
            }
            results.Add(
                bot.Id,
                ApplicationResult<AdmittedMatchBot>.Success(
                    new AdmittedMatchBot(
                        bot,
                        version,
                        ownerDisplayName)));
        }

        return results;
    }

    private static ApplicationResult<AdmittedMatchBot> BatchFailure(
        ApplicationError error) =>
        ApplicationResult<AdmittedMatchBot>.Failure(error);

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
