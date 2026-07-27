using System.Security.Claims;
using System.Threading.RateLimiting;

namespace BotArena.App.Shared;

/// <summary>
/// Every HTTP rate limit, in one place.
/// <para>
/// These were four near-identical partition lambdas inline in <c>Program.cs</c>, which is
/// how they came to disagree about something that matters: the compile limiter partitioned
/// on the user id while the challenge limiter partitioned on
/// <c>User.Identity.Name</c> — the *display name*. Two accounts sharing a name shared a
/// quota, and a rename would have handed out a fresh one. Naming the partition once means
/// there is nothing left to disagree about.
/// </para>
/// <para>
/// **This layer is burst protection, nothing more.** It lives in each web process's memory,
/// so it multiplies by replica count and resets on restart. Anything that must actually
/// hold — because it costs real compute — is enforced transactionally in PostgreSQL as
/// well: see <see cref="Bots.CompilerSubmissionPolicy"/> and
/// <see cref="Matches.RankedSetPolicy"/>. A limit that exists only here is a limit that
/// stops applying the moment there are two web servers.
/// </para>
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Submission = "submission";
    public const string Challenge = "challenge";
    public const string Ranked = "ranked";

    public static void AddBotArenaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }
                return ValueTask.CompletedTask;
            };

            // Everything, by origin. A blunt ceiling so one host cannot exhaust the server
            // before any named policy is consulted.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => Window(Network(context), permits: 600, TimeSpan.FromMinutes(1)));

            // Credential endpoints: slow brute force. Deliberately by network rather than
            // by account — the caller has not proved who they are yet, and partitioning on
            // an unauthenticated claim is partitioning on the attacker's choice.
            options.AddPolicy(Auth, context =>
                Window(Network(context), permits: 10, TimeSpan.FromMinutes(1)));

            // Compilation is expensive. Also enforced durably; this only absorbs bursts.
            options.AddPolicy(Submission, context =>
                Window(AccountAndNetwork(context), permits: 6, TimeSpan.FromMinutes(10)));

            options.AddPolicy(Challenge, context =>
                Window(Account(context), permits: 20, TimeSpan.FromMinutes(1)));

            // Ranked is its own policy rather than sharing the challenge one: a ranked set
            // is six WASM matches, so the same permit count means six times the work. The
            // durable per-account limit is the real rule; this stops a script getting a
            // day's worth queued in the second before that rule is consulted.
            options.AddPolicy(Ranked, context =>
                Window(Account(context), permits: 5, TimeSpan.FromMinutes(1)));
        });
    }

    private static RateLimitPartition<string> Window(
        string key,
        int permits,
        TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window });

    /// <summary>
    /// The account, falling back to the origin when nobody is signed in.
    /// <para>
    /// The *id*, never the display name: names are chosen by the user, and until recently
    /// were not even unique, so two players called the same thing shared one quota.
    /// </para>
    /// </summary>
    private static string Account(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id
            ? $"user:{id}"
            : Network(context);

    /// <summary>
    /// Account *and* origin together, for limits that also exist to stop one machine
    /// churning through many accounts.
    /// </summary>
    private static string AccountAndNetwork(HttpContext context) =>
        $"{context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"}:" +
        $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static string Network(HttpContext context) =>
        $"net:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
