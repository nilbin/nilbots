namespace BotArena.App.Shared;

public enum ApplicationErrorType
{
    Authentication,
    Authorization,
    Validation,
    NotFound,
    Conflict,
    RateLimit,
}

/// <summary>An application failure whose code is stable across transports.</summary>
public sealed record ApplicationError(
    string Code,
    ApplicationErrorType Type,
    string Detail,
    TimeSpan? RetryAfter = null);

public static class ApplicationErrorCodes
{
    public const string AuthenticationRequired = "auth.required";
    public const string BotNotFound = "bots.not_found";
    public const string BotOwnershipRequired = "bots.ownership_required";
    public const string BotNameInvalid = "bots.name_invalid";
    public const string BotNameConflict = "bots.name_conflict";
    public const string AccentInvalid = "appearance.accent_invalid";
    public const string BotLookIdInvalid = "appearance.bot_look_id_invalid";
    public const string ProjectileLookIdInvalid = "appearance.projectile_look_id_invalid";
    public const string BotLookUnknown = "appearance.bot_look_unknown";
    public const string ProjectileLookUnknown = "appearance.projectile_look_unknown";
    public const string BotLookLocked = "appearance.bot_look_locked";
    public const string ProjectileLookLocked = "appearance.projectile_look_locked";
    public const string MatchActiveVersionRequired = "matches.active_version_required";
    public const string MatchContractProfileRequired =
        "matches.contract_profile_required";
    public const string SubmissionInvalid = "submissions.invalid";
    public const string SubmissionRateLimited = "submissions.rate_limited";
}
