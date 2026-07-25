using System.Diagnostics.Metrics;
using BotArena.App.Shared;

namespace BotArena.App.Matches;

internal static class RankedFinalizationTelemetry
{
    private static readonly Counter<long> Outcomes =
        ApplicationTelemetry.Meter.CreateCounter<long>(
            "botarena.ranked_finalizations.outcomes",
            description: "Ranked-set finalization outcomes.");

    public static void Record(RankedSetFinalizationOutcome outcome) =>
        Record(outcome.ToString().ToLowerInvariant());

    public static void RecordException() => Record("exception");

    private static void Record(string outcome) =>
        Outcomes.Add(
            1,
            new KeyValuePair<string, object?>(
                "outcome",
                outcome));
}
