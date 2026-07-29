using BotArena.App.Cosmetics;
using BotArena.App.Shared;

namespace BotArena.App.Bots;

/// <summary>
/// Owns validation and account entitlement for every equipped bot appearance.
/// Called by create, update, submit, and match admission; replay reads never call it.
/// </summary>
public sealed class BotAppearancePolicy(CosmeticEntitlementService entitlements)
{
    public Task<ApplicationResult<BotAppearance>> ValidateForCreationAsync(
        Guid accountId,
        string? accent,
        string? botLookId,
        string? projectileLookId,
        CancellationToken cancellationToken = default)
    {
        AccentColor normalizedAccent = AccentColor.TryCreate(accent, out AccentColor parsed)
            ? parsed
            : AccentColor.Default;
        return ValidateAndAuthorizeAsync(
            accountId,
            normalizedAccent,
            botLookId ?? "vanguard",
            projectileLookId ?? "pulse-bolt",
            cancellationToken);
    }

    public Task<ApplicationResult<BotAppearance>> ValidateForUpdateAsync(
        Guid accountId,
        string? accent,
        string? botLookId,
        string? projectileLookId,
        CancellationToken cancellationToken = default)
    {
        if (!AccentColor.TryCreate(accent, out AccentColor normalizedAccent))
        {
            return Task.FromResult(
                ApplicationResult<BotAppearance>.Failure(
                    AccentInvalid()));
        }
        return ValidateAndAuthorizeAsync(
            accountId,
            normalizedAccent,
            botLookId,
            projectileLookId,
            cancellationToken);
    }

    public Task<ApplicationResult<BotAppearance>> ValidateForSubmissionAsync(
        Guid accountId,
        Bot bot,
        string? botLookId,
        string? projectileLookId,
        CancellationToken cancellationToken = default) =>
        ValidateForUpdateAsync(
            accountId,
            bot.Accent,
            botLookId ?? bot.LookId,
            projectileLookId ?? bot.ProjectileLookId,
            cancellationToken);

    public Task<ApplicationResult<BotAppearance>> ValidateForMatchAdmissionAsync(
        Bot bot,
        CancellationToken cancellationToken = default) =>
        ValidateForUpdateAsync(
            bot.OwnerUserId,
            bot.Accent,
            bot.LookId,
            bot.ProjectileLookId,
            cancellationToken);

    /// <summary>
    /// Applies the same validation and authorization precedence as single-bot
    /// admission while resolving catalog entitlements in a batch.
    /// </summary>
    public async Task<
        IReadOnlyDictionary<Guid, ApplicationResult<BotAppearance>>>
        ValidateForMatchAdmissionBatchAsync(
            IReadOnlyCollection<Bot> bots,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bots);

        var parsed =
            new Dictionary<Guid, ParsedAppearance>(bots.Count);
        var results =
            new Dictionary<Guid, ApplicationResult<BotAppearance>>(bots.Count);
        var accessRequests =
            new List<CosmeticAccessRequest>(bots.Count * 2);
        foreach (Bot bot in bots)
        {
            if (!AccentColor.TryCreate(bot.Accent, out AccentColor accent))
            {
                results.Add(
                    bot.Id,
                    ApplicationResult<BotAppearance>.Failure(
                        AccentInvalid()));
                continue;
            }
            if (!AppearanceId.TryCreate(
                    bot.LookId,
                    out AppearanceId botLook))
            {
                results.Add(
                    bot.Id,
                    ApplicationResult<BotAppearance>.Failure(
                        BotLookIdInvalid()));
                continue;
            }
            if (!AppearanceId.TryCreate(
                    bot.ProjectileLookId,
                    out AppearanceId projectileLook))
            {
                results.Add(
                    bot.Id,
                    ApplicationResult<BotAppearance>.Failure(
                        ProjectileLookIdInvalid()));
                continue;
            }

            var appearance =
                new ParsedAppearance(accent, botLook, projectileLook);
            parsed.Add(bot.Id, appearance);
            accessRequests.Add(new CosmeticAccessRequest(
                bot.OwnerUserId,
                CosmeticCatalog.BotLookKind,
                botLook.Value));
            accessRequests.Add(new CosmeticAccessRequest(
                bot.OwnerUserId,
                CosmeticCatalog.ProjectileLookKind,
                projectileLook.Value));
        }

        IReadOnlyDictionary<CosmeticAccessRequest, CosmeticAccess> access =
            await entitlements.CheckAccessBatchAsync(
                accessRequests,
                cancellationToken);
        foreach (Bot bot in bots)
        {
            if (!parsed.TryGetValue(
                    bot.Id,
                    out ParsedAppearance appearance))
            {
                continue;
            }

            ApplicationError? botLookError = AccessError(
                access[new CosmeticAccessRequest(
                    bot.OwnerUserId,
                    CosmeticCatalog.BotLookKind,
                    appearance.BotLook.Value)],
                botLook: true,
                appearance.BotLook.Value);
            if (botLookError is not null)
            {
                results.Add(
                    bot.Id,
                    ApplicationResult<BotAppearance>.Failure(
                        botLookError));
                continue;
            }

            ApplicationError? projectileLookError = AccessError(
                access[new CosmeticAccessRequest(
                    bot.OwnerUserId,
                    CosmeticCatalog.ProjectileLookKind,
                    appearance.ProjectileLook.Value)],
                botLook: false,
                appearance.ProjectileLook.Value);
            results.Add(
                bot.Id,
                projectileLookError is null
                    ? ApplicationResult<BotAppearance>.Success(
                        new BotAppearance(
                            appearance.Accent,
                            appearance.BotLook,
                            appearance.ProjectileLook))
                    : ApplicationResult<BotAppearance>.Failure(
                        projectileLookError));
        }

        return results;
    }

    private async Task<ApplicationResult<BotAppearance>> ValidateAndAuthorizeAsync(
        Guid accountId,
        AccentColor accent,
        string? botLookId,
        string? projectileLookId,
        CancellationToken cancellationToken)
    {
        if (!AppearanceId.TryCreate(botLookId, out AppearanceId botLook))
        {
            return ApplicationResult<BotAppearance>.Failure(
                BotLookIdInvalid());
        }
        if (!AppearanceId.TryCreate(projectileLookId, out AppearanceId projectileLook))
        {
            return ApplicationResult<BotAppearance>.Failure(
                ProjectileLookIdInvalid());
        }

        ApplicationError? botLookError = await CheckAccessAsync(
            accountId,
            CosmeticCatalog.BotLookKind,
            botLook,
            cancellationToken);
        if (botLookError is not null)
            return ApplicationResult<BotAppearance>.Failure(botLookError);
        ApplicationError? projectileLookError = await CheckAccessAsync(
            accountId,
            CosmeticCatalog.ProjectileLookKind,
            projectileLook,
            cancellationToken);
        if (projectileLookError is not null)
            return ApplicationResult<BotAppearance>.Failure(projectileLookError);

        return ApplicationResult<BotAppearance>.Success(
            new BotAppearance(accent, botLook, projectileLook));
    }

    private async Task<ApplicationError?> CheckAccessAsync(
        Guid accountId,
        string kind,
        AppearanceId id,
        CancellationToken cancellationToken)
    {
        CosmeticAccess access = await entitlements.CheckAccessAsync(
            accountId,
            kind,
            id.Value,
            cancellationToken);
        return AccessError(
            access,
            kind == CosmeticCatalog.BotLookKind,
            id.Value);
    }

    private static ApplicationError? AccessError(
        CosmeticAccess access,
        bool botLook,
        string requestedId)
    {
        if (access.Item is null)
        {
            return new ApplicationError(
                botLook
                    ? ApplicationErrorCodes.BotLookUnknown
                    : ApplicationErrorCodes.ProjectileLookUnknown,
                ApplicationErrorType.Validation,
                $"Unknown {(botLook ? "bot look" : "projectile look")} " +
                $"'{requestedId}'.");
        }
        if (access.Owned)
            return null;

        string hint = access.Item.Unlock?.Hint is { Length: > 0 } value
            ? $" {value}"
            : "";
        return new ApplicationError(
            botLook
                ? ApplicationErrorCodes.BotLookLocked
                : ApplicationErrorCodes.ProjectileLookLocked,
            ApplicationErrorType.Authorization,
            $"{access.Item.Label} is locked.{hint}");
    }

    private static ApplicationError AccentInvalid() =>
        new(
            ApplicationErrorCodes.AccentInvalid,
            ApplicationErrorType.Validation,
            "Accent must be a six-digit hexadecimal color.");

    private static ApplicationError BotLookIdInvalid() =>
        new(
            ApplicationErrorCodes.BotLookIdInvalid,
            ApplicationErrorType.Validation,
            "Bot look must be a lowercase kebab-case ID.");

    private static ApplicationError ProjectileLookIdInvalid() =>
        new(
            ApplicationErrorCodes.ProjectileLookIdInvalid,
            ApplicationErrorType.Validation,
            "Projectile look must be a lowercase kebab-case ID.");

    private readonly record struct ParsedAppearance(
        AccentColor Accent,
        AppearanceId BotLook,
        AppearanceId ProjectileLook);
}
