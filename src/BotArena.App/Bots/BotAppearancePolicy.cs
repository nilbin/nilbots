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
            return Task.FromResult(ApplicationResult<BotAppearance>.Failure(new(
                ApplicationErrorCodes.AccentInvalid,
                ApplicationErrorType.Validation,
                "Accent must be a six-digit hexadecimal color.")));
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

    private async Task<ApplicationResult<BotAppearance>> ValidateAndAuthorizeAsync(
        Guid accountId,
        AccentColor accent,
        string? botLookId,
        string? projectileLookId,
        CancellationToken cancellationToken)
    {
        if (!AppearanceId.TryCreate(botLookId, out AppearanceId botLook))
        {
            return ApplicationResult<BotAppearance>.Failure(new(
                ApplicationErrorCodes.BotLookIdInvalid,
                ApplicationErrorType.Validation,
                "Bot look must be a lowercase kebab-case ID."));
        }
        if (!AppearanceId.TryCreate(projectileLookId, out AppearanceId projectileLook))
        {
            return ApplicationResult<BotAppearance>.Failure(new(
                ApplicationErrorCodes.ProjectileLookIdInvalid,
                ApplicationErrorType.Validation,
                "Projectile look must be a lowercase kebab-case ID."));
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
        bool botLook = kind == CosmeticCatalog.BotLookKind;
        if (access.Item is null)
        {
            return new ApplicationError(
                botLook
                    ? ApplicationErrorCodes.BotLookUnknown
                    : ApplicationErrorCodes.ProjectileLookUnknown,
                ApplicationErrorType.Validation,
                $"Unknown {(botLook ? "bot look" : "projectile look")} '{id.Value}'.");
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
}
