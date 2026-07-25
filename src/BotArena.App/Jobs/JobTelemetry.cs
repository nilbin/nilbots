using System.Diagnostics;
using System.Diagnostics.Metrics;
using BotArena.App.Shared;

namespace BotArena.App.Jobs;

internal static class JobTelemetry
{
    private static readonly Counter<long> Claims = ApplicationTelemetry.Meter.CreateCounter<long>(
        "botarena.jobs.claims",
        description: "Background jobs claimed from the durable queue.");
    private static readonly Counter<long> Outcomes = ApplicationTelemetry.Meter.CreateCounter<long>(
        "botarena.jobs.outcomes",
        description: "Background job execution outcomes.");
    private static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>(
        "botarena.jobs.duration",
        unit: "ms",
        description: "Background job execution duration.");

    public static void RecordClaim(string jobType) =>
        Claims.Add(1, new KeyValuePair<string, object?>("job.type", jobType));

    public static void RecordJob(string jobType, string outcome, TimeSpan elapsed)
    {
        TagList tags = new()
        {
            { "job.type", jobType },
            { "outcome", outcome },
        };
        Outcomes.Add(1, tags);
        Duration.Record(elapsed.TotalMilliseconds, tags);
    }
}
