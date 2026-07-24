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

    public static CompilerSubmissionLimits FromConfiguration(IConfiguration configuration) => new(
        Read(configuration, "BOTARENA_COMPILE_ACCOUNT_10M", 6, 1, 100),
        Read(configuration, "BOTARENA_COMPILE_ACCOUNT_DAILY", 30, 1, 1000),
        Read(configuration, "BOTARENA_COMPILE_NETWORK_10M", 12, 1, 500),
        Read(configuration, "BOTARENA_COMPILE_NETWORK_DAILY", 60, 1, 5000),
        Read(configuration, "BOTARENA_COMPILE_ACCOUNT_QUEUED", 2, 1, 20),
        Read(configuration, "BOTARENA_COMPILE_GLOBAL_QUEUED", 20, 1, 1000));

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

