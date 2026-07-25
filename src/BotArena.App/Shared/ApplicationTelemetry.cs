using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BotArena.App.Shared;

public static class ApplicationTelemetry
{
    public const string InstrumentationName = "BotArena.App.Application";

    public static readonly ActivitySource ActivitySource = new(InstrumentationName);
    public static readonly Meter Meter = new(InstrumentationName);
    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "botarena.application.operations",
        description: "Application use-case outcomes.");

    public static void Record(
        string operation,
        string outcome,
        Guid? accountId = null,
        Guid? botId = null)
    {
        TagList tags = new()
        {
            { "operation", operation },
            { "outcome", outcome },
        };
        if (accountId is Guid account)
            Activity.Current?.SetTag("account.id", account);
        if (botId is Guid bot)
            Activity.Current?.SetTag("bot.id", bot);
        Operations.Add(1, tags);
        Activity.Current?.SetTag("application.outcome", outcome);
    }
}
