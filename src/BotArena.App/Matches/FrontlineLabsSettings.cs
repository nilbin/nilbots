using BotArena.App.Shared;

namespace BotArena.App.Matches;

/// <summary>
/// Deployment gate for the hosted Frontline experiment. While disabled, new
/// builds must retain Duel compatibility; enabling it admits generic-only
/// artifacts and new Labs matches. Turning it off never deactivates an
/// existing artifact or prevents workers from executing an already queued,
/// identity-pinned match.
/// </summary>
public sealed record FrontlineLabsSettings(
    bool Enabled,
    int AccountDailyLimit = 10,
    int AccountActiveLimit = 1,
    int GlobalActiveLimit = 4)
{
    public const string ConfigurationKey =
        "BOTARENA_FRONTLINE_LABS_ENABLED";
    public const string AccountDailyConfigurationKey =
        "BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY";
    public const string AccountActiveConfigurationKey =
        "BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE";
    public const string GlobalActiveConfigurationKey =
        "BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE";

    public static FrontlineLabsSettings FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new FrontlineLabsSettings(
            configuration.GetValue<bool>(ConfigurationKey),
            AdmissionSupport.ReadLimit(
                configuration,
                AccountDailyConfigurationKey,
                fallback: 10,
                minimum: 1,
                maximum: 100),
            AdmissionSupport.ReadLimit(
                configuration,
                AccountActiveConfigurationKey,
                fallback: 1,
                minimum: 1,
                maximum: 10),
            AdmissionSupport.ReadLimit(
                configuration,
                GlobalActiveConfigurationKey,
                fallback: 4,
                minimum: 1,
                maximum: 100));
    }
}
