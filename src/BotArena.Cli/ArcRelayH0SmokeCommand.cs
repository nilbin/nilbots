using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Writes deterministic mind-profile mechanic smoke replays for Phase C.
/// It is an engineering probe, not a player doctrine or balance harness.
/// </summary>
public static class ArcRelayH0SmokeCommand
{
    private static readonly ulong[] Seeds = [17, 29];

    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(options, "out", "viewer", "seed");
        string root = Path.GetFullPath(
            options.GetValueOrDefault("out")
            ?? Path.Combine("out", "arc-relay-h0-smoke"));
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create();
        var summaries = new List<GenericActorArcRelayReplaySummary>();

        IEnumerable<ulong> seeds = options.TryGetValue(
                "seed",
                out string? seedText)
            ? [ulong.Parse(seedText, NumberStyles.None,
                CultureInfo.InvariantCulture)]
            : Seeds;
        foreach (ulong seed in seeds)
        {
            bool protectRelay = seed == Seeds[0];
            using var first = new ArcRelayH0SmokeMindFactory(
                teamId: 0,
                protectRelay);
            using var second = new ArcRelayH0SmokeMindFactory(
                teamId: 1,
                protectRelay);
            GenericActorParticipantConfiguration[] participants =
            [
                Participant(0, 0, "arc-smoke-west", first, "#38bdf8"),
                Participant(1, 1, "arc-smoke-east", second, "#fb7185"),
            ];
            using var session = new GenericActorMatchSession(
                definition,
                participants,
                seed);
            try
            {
                _ = session.Run();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Arc Relay smoke seed {seed} failed during simulation: "
                    + $"session tick {session.Tick}. {exception}",
                    exception);
            }
            GenericActorReplayDocument replay =
                GenericActorReplayDocument.Create(
                    session,
                    ArcRelayH0ReplayPresentation.Create(definition));
            GenericActorArcRelayReplaySummary summary =
                GenericActorArcRelayReplaySummary.Read(replay.CanonicalJson);
            summaries.Add(summary);
            string output = Path.Combine(root, $"seed-{seed}");
            WrittenReplay written = ReplayOutput.WriteJson(
                replay.CanonicalJson,
                output,
                ArcRelayH0ReplayPresentation.ThemeId,
                withViewer: options.ContainsKey("viewer"));
            Console.WriteLine($"seed {seed}: {written.ReplayPath}");
            Console.WriteLine(
                $"  births {summary.ActualBirths}, pickups {summary.Pickups}, "
                + $"steals {summary.Steals}, handoffs {summary.Handoffs}, "
                + $"arc-tosses {summary.ArcTosses}, "
                + $"death-drops {summary.DeathDrops}, banks {summary.Banks}, "
                + $"pulses {summary.Pulses}");
        }

        PrintAggregate(summaries);
        return 0;
    }

    private static GenericActorParticipantConfiguration Participant(
        int participantId,
        int teamId,
        string name,
        IGenericMindRuntimeFactory factory,
        string accent) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = name,
            MindRuntimeFactory = factory,
            RuntimeKind = "in-process-generic-mind-mechanic-smoke",
            ArtifactHash = $"arc-relay-h0-smoke-mind-{teamId}",
            Accent = accent,
        };

    private static void PrintAggregate(
        IReadOnlyCollection<GenericActorArcRelayReplaySummary> summaries)
    {
        int signatureKinds = summaries
            .SelectMany(summary => summary.Signatures)
            .Where(pair => pair.Value.Attempts > 0)
            .Select(pair => pair.Key)
            .Distinct(StringComparer.Ordinal)
            .Count();
        Console.WriteLine();
        Console.WriteLine(
            $"Replay files: {summaries.Count}; "
            + $"births {summaries.Sum(value => value.ActualBirths)}, "
            + $"pickups {summaries.Sum(value => value.Pickups)}, "
            + $"steals {summaries.Sum(value => value.Steals)}, "
            + $"handoffs {summaries.Sum(value => value.Handoffs)}, "
            + $"arc-tosses {summaries.Sum(value => value.ArcTosses)}, "
            + $"death-drops {summaries.Sum(value => value.DeathDrops)}, "
            + $"banks {summaries.Sum(value => value.Banks)}, "
            + $"pulses {summaries.Sum(value => value.Pulses)}, "
            + $"signature-kinds {signatureKinds}.");
    }
}
