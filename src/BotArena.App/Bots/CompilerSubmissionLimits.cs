namespace BotArena.App.Bots;

/// <summary>
/// Admission limits for expensive C# to WASM builds. The in-memory ASP.NET
/// limiter is only a cheap first layer; these limits are enforced transactionally
/// in PostgreSQL and therefore survive restarts and additional web replicas.
/// </summary>
public sealed record CompilerSubmissionLimits(
    int AccountTenMinuteLimit,
    int AccountDailyLimit,
    int NetworkTenMinuteLimit,
    int NetworkDailyLimit,
    int AccountQueuedLimit,
    int GlobalQueuedLimit)
{
    public const long MaxRequestBodyBytes = 1024 * 1024;

    /// <summary>
    /// These limits as they apply to one account, after anything it has bought.
    /// <para>
    /// Only the daily allowance moves. The ten-minute and queued caps protect the compiler
    /// from bursts and the ladder from bought tempo, and the network limits are about the
    /// connection rather than the account — raising any of those for a payer would either
    /// hand them an in-the-moment edge or let one purchase lift a shared IP's ceiling.
    /// </para>
    /// <para>
    /// Applied here rather than at the policy, so <see cref="CompilerSubmissionPolicy"/>
    /// stays a pure function of a snapshot and a set of limits and does not learn what an
    /// entitlement is.
    /// </para>
    /// </summary>
    public CompilerSubmissionLimits ForAccount(IReadOnlyCollection<string> entitlementKeys) =>
        this with
        {
            AccountDailyLimit =
                AccountDailyLimit + Store.AccountCapacity.ExtraDailyBuildsFor(entitlementKeys),
        };

    public static CompilerSubmissionLimits FromConfiguration(IConfiguration configuration) => new(
        Shared.AdmissionSupport.ReadLimit(configuration, "BOTARENA_COMPILE_ACCOUNT_10M", 6, 1, 100),
        Shared.AdmissionSupport.ReadLimit(configuration, "BOTARENA_COMPILE_ACCOUNT_DAILY", 30, 1, 1000),
        Shared.AdmissionSupport.ReadLimit(configuration, "BOTARENA_COMPILE_NETWORK_10M", 12, 1, 500),
        Shared.AdmissionSupport.ReadLimit(configuration, "BOTARENA_COMPILE_NETWORK_DAILY", 60, 1, 5000),
        Shared.AdmissionSupport.ReadLimit(configuration, "BOTARENA_COMPILE_ACCOUNT_QUEUED", 2, 1, 20),
        Shared.AdmissionSupport.ReadLimit(configuration, "BOTARENA_COMPILE_GLOBAL_QUEUED", 20, 1, 1000));

    private static int Read(
        IConfiguration configuration,
        string name,
        int fallback,
        int minimum,
        int maximum)
    {
        int value = configuration.GetValue<int?>(name) ?? fallback;
        return Math.Clamp(value, minimum, maximum);
    }
}

