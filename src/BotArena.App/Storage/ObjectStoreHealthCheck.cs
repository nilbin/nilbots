using BotArena.App.Shared;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BotArena.App.Storage;

public sealed class ObjectStoreHealthCheck(
    IObjectStore objectStore,
    ApplicationMode mode) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        bool requireWrite = mode.IsAll || mode.RunsAnyWorker || mode.RunsMigrations;
        bool ready = await objectStore.IsReadyAsync(requireWrite, cancellationToken);
        return ready
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                requireWrite
                    ? "Object store is not writable."
                    : "Object store is not readable.");
    }
}
