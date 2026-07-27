using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BotArena.App.Shared;

/// <summary>
/// The two things every durable admission check needs, and nothing else.
/// <para>
/// Deliberately not a policy framework. There are three of these — compilation, ranked
/// sets, login — and each reads different rows and refuses for different reasons; the only
/// genuinely common parts are taking the lock and reading a clamped number out of
/// configuration. Generalising the rest would replace three readable functions with one
/// that has to be decoded before any of them can be understood.
/// </para>
/// </summary>
public static class AdmissionSupport
{
    /// <summary>
    /// Serialise this account's admission check against itself.
    /// <para>
    /// Transaction-scoped, so it releases on commit or rollback with nothing to remember.
    /// Without it two simultaneous requests both read a count below the limit and both
    /// pass, which is the entire failure mode a durable limit exists to prevent — the
    /// in-memory limiter has the same race across replicas and is why this layer exists.
    /// </para>
    /// </summary>
    public static Task TakeAdmissionLockAsync(
        this DatabaseFacade database,
        long key,
        CancellationToken cancellationToken) =>
        database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({key})",
            cancellationToken);

    /// <summary>
    /// A limit from configuration, clamped to something sane.
    /// <para>
    /// The clamp is not defensive noise: these are environment variables, and a typo that
    /// reads as zero would close the feature to everyone while one that reads as a million
    /// would remove the protection entirely. Neither should be reachable by mistyping a
    /// deployment variable.
    /// </para>
    /// </summary>
    public static int ReadLimit(
        IConfiguration configuration,
        string name,
        int fallback,
        int minimum,
        int maximum) =>
        Math.Clamp(configuration.GetValue<int?>(name) ?? fallback, minimum, maximum);
}
