using System.Text.Json;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli;

/// <summary>
/// Runs versioned, local-only capability probes against one generic actor
/// artifact. Probe results never mutate competition state and do not infer a
/// tier from ordinary match standings.
/// </summary>
public static class FrontlineLabsQualificationCommand
{
    private sealed record AssignmentEvidence(
        int BotTeamId,
        bool ContractValid,
        bool BotEligible,
        int SentinelAttackCount,
        int? FirstObjectiveTick,
        int? FirstLifeObjectiveTick,
        IReadOnlyDictionary<string, int> BotActionCounts,
        string ReplayHash,
        string ReplayPath,
        bool Passed);

    private sealed record QualificationReport(
        string SuiteId,
        string ProbeId,
        string ArtifactName,
        string ArtifactHash,
        string RuntimeKind,
        ulong Seed,
        string RulesFingerprint,
        string MapFingerprint,
        string MatchFingerprint,
        bool Passed,
        string? TierAwarded,
        IReadOnlyList<AssignmentEvidence> Assignments);

    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(
            options,
            "bot",
            "runtime",
            "seed",
            "suite",
            "out");
        string botSpec = RequiredOption(options, "bot");
        string runtimeKind = options
            .GetValueOrDefault("runtime", "wasm")
            .ToLowerInvariant();
        if (runtimeKind is not ("wasm" or "in-process"))
        {
            throw new InvalidOperationException(
                $"Unknown runtime '{runtimeKind}' " +
                "(use wasm or in-process).");
        }

        string suiteId = options.GetValueOrDefault(
            "suite",
            FrontlineLabsQualificationDefinition.SuiteId);
        if (suiteId
            != FrontlineLabsQualificationDefinition.SuiteId)
        {
            throw new InvalidOperationException(
                $"Unknown qualification suite '{suiteId}'.");
        }
        ulong seed = ParseSeed(
            options.GetValueOrDefault("seed", "104729"));
        string outputDirectory = Path.GetFullPath(
            options.GetValueOrDefault(
                "out",
                Path.Combine("out", suiteId)));
        Directory.CreateDirectory(outputDirectory);

        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition.CreateEntryProbe();
        string rulesFingerprint =
            ActorContractFingerprint.ComputeRules(definition.Rules);
        string mapFingerprint =
            ActorContractFingerprint.ComputeMap(definition.Map);
        string matchFingerprint =
            ActorContractFingerprint.ComputeMatch(definition);

        var evidence = new List<AssignmentEvidence>();
        string? artifactName = null;
        string? artifactHash = null;
        string? actualRuntimeKind = null;
        foreach (int botTeamId in new[] { 0, 1 })
        {
            using ResolvedGenericActorBot bot =
                ResolvedGenericActorBot.Resolve(
                    botSpec,
                    runtimeKind,
                    quiet: botTeamId != 0);
            artifactName ??= bot.Name;
            artifactHash ??= bot.ArtifactHash;
            actualRuntimeKind ??= bot.RuntimeKind;
            if (artifactName != bot.Name
                || artifactHash != bot.ArtifactHash
                || actualRuntimeKind != bot.RuntimeKind)
            {
                throw new InvalidOperationException(
                    "Qualification artifact identity changed between " +
                    "mirrored assignments.");
            }
            using var sentinelFactory =
                new InProcessGenericActorRuntimeFactory(
                    () => new FrontlineLabsQualificationSentinel());
            int sentinelTeamId = 1 - botTeamId;
            GenericActorParticipantConfiguration[] participants =
                botTeamId == 0
                    ?
                    [
                        bot.ToParticipant(participantId: 0, teamId: 0),
                        SentinelParticipant(
                            sentinelFactory,
                            participantId: 1,
                            teamId: 1),
                    ]
                    :
                    [
                        SentinelParticipant(
                            sentinelFactory,
                            participantId: 0,
                            teamId: 0),
                        bot.ToParticipant(participantId: 1, teamId: 1),
                    ];
            GenericActorMatchResult result;
            GenericActorReplayDocument replay;
            using (var session = new GenericActorMatchSession(
                       definition,
                       participants,
                       seed))
            {
                result = session.Run();
                replay = GenericActorReplayDocument.Create(session);
            }
            bool contractValid = GenericActorReplayDocument.VerifyHash(
                replay.CanonicalJson,
                out string? verificationFailure);
            if (!contractValid)
            {
                throw new InvalidOperationException(
                    "Qualification replay verification failed: " +
                    verificationFailure);
            }

            string assignmentDirectory = Path.Combine(
                outputDirectory,
                FrontlineLabsQualificationDefinition.EntryProbeId,
                $"bot-team-{botTeamId}");
            WrittenReplay written = ReplayOutput.WriteJson(
                replay.CanonicalJson,
                assignmentDirectory);
            evidence.Add(
                Analyze(
                    replay.CanonicalJson,
                    botTeamId,
                    sentinelTeamId,
                    result.EligibleTeamIds.Contains(botTeamId),
                    replay.ReplayHash,
                    Path.GetRelativePath(
                        outputDirectory,
                        written.ReplayPath),
                    contractValid));
        }

        bool passed = evidence.All(item => item.Passed);
        string resolvedArtifactName = artifactName
            ?? throw new InvalidOperationException(
                "Qualification produced no artifact identity.");
        string resolvedArtifactHash = artifactHash
            ?? throw new InvalidOperationException(
                "Qualification produced no artifact hash.");
        string resolvedRuntimeKind = actualRuntimeKind
            ?? throw new InvalidOperationException(
                "Qualification produced no runtime identity.");
        var report = new QualificationReport(
            suiteId,
            FrontlineLabsQualificationDefinition.EntryProbeId,
            resolvedArtifactName,
            resolvedArtifactHash,
            resolvedRuntimeKind,
            seed,
            rulesFingerprint,
            mapFingerprint,
            matchFingerprint,
            passed,
            TierAwarded: null,
            evidence);
        string reportPath = Path.Combine(
            outputDirectory,
            "qualification.json");
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                })
            + Environment.NewLine);

        Console.WriteLine(
            $"Qualification suite: {suiteId}");
        Console.WriteLine(
            $"Probe:               " +
            $"{FrontlineLabsQualificationDefinition.EntryProbeId}");
        Console.WriteLine(
            $"Artifact:            {resolvedArtifactName} " +
            $"[{resolvedArtifactHash[..12]}…]");
        foreach (AssignmentEvidence item in evidence)
        {
            Console.WriteLine(
                $"bot team {item.BotTeamId}:        " +
                $"{(item.Passed ? "PASS" : "FAIL")} " +
                $"first-life-entry=" +
                $"{item.FirstLifeObjectiveTick?.ToString() ?? "never"} " +
                $"sentinel-shots={item.SentinelAttackCount}");
        }
        Console.WriteLine($"Report:              {reportPath}");
        Console.WriteLine(
            "Tier awarded:        none " +
            "(this is one T4 component, not a cumulative suite)");

        if (evidence.Any(item =>
                !item.ContractValid || !item.BotEligible))
        {
            return 2;
        }
        return passed ? 0 : 3;
    }

    private static GenericActorParticipantConfiguration
        SentinelParticipant(
            IGenericActorRuntimeFactory runtimeFactory,
            int participantId,
            int teamId) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = "Entry Sentinel",
            RuntimeFactory = runtimeFactory,
            RuntimeKind = "in-process-qualification-controller",
            ArtifactHash =
                "frontline-qualification-1-entry-sentinel",
            Accent = "#f97316",
            LookId = "bastion",
            ProjectileLookId = "ember-lance",
        };

    private static AssignmentEvidence Analyze(
        string replayJson,
        int botTeamId,
        int sentinelTeamId,
        bool botEligible,
        string replayHash,
        string replayPath,
        bool contractValid)
    {
        using JsonDocument document = JsonDocument.Parse(replayJson);
        JsonElement root = document.RootElement;
        JsonElement contract = root
            .GetProperty("header")
            .GetProperty("contract");
        HashSet<(int X, int Y)> objectiveTiles = contract
            .GetProperty("map")
            .GetProperty("regions")
            .EnumerateArray()
            .Single(region =>
                region.GetProperty("regionId").GetString()
                    == "frontline-position-2")
            .GetProperty("tiles")
            .EnumerateArray()
            .Select(tile =>
                (
                    tile[0].GetInt32(),
                    tile[1].GetInt32()
                ))
            .ToHashSet();
        int botParticipantId = botTeamId;
        int sentinelParticipantId = sentinelTeamId;
        int? firstObjectiveTick = null;
        int? firstLifeObjectiveTick = null;
        int sentinelAttackCount = 0;
        var actionCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            foreach (JsonElement turn in tick
                         .GetProperty("actorTurns")
                         .EnumerateArray())
            {
                int participantId = turn
                    .GetProperty("participantId")
                    .GetInt32();
                JsonElement submitted =
                    turn.GetProperty("submittedDecision");
                string? actionId = submitted.ValueKind
                        == JsonValueKind.Object
                    ? submitted
                        .GetProperty("actionId")
                        .GetString()
                    : null;
                if (participantId == sentinelParticipantId
                    && actionId == "shoot")
                {
                    sentinelAttackCount++;
                }
                if (participantId != botParticipantId)
                    continue;

                if (actionId is not null)
                {
                    actionCounts[actionId] =
                        actionCounts.GetValueOrDefault(actionId) + 1;
                }
                JsonElement self = turn
                    .GetProperty("observation")
                    .GetProperty("self");
                JsonElement actorId = self.GetProperty("actorId");
                if (actorId.GetProperty("unitId").GetInt32() != 0)
                    continue;
                JsonElement position = self.GetProperty("position");
                if (!objectiveTiles.Contains(
                        (
                            position.GetProperty("x").GetInt32(),
                            position.GetProperty("y").GetInt32()
                        )))
                {
                    continue;
                }

                int tickNumber = turn.GetProperty("tick").GetInt32();
                firstObjectiveTick ??= tickNumber;
                if (actorId.GetProperty("lifeId").GetInt32() == 0)
                    firstLifeObjectiveTick ??= tickNumber;
            }
        }

        bool passed = contractValid
            && botEligible
            && sentinelAttackCount > 0
            && firstLifeObjectiveTick is not null;
        return new AssignmentEvidence(
            botTeamId,
            contractValid,
            botEligible,
            sentinelAttackCount,
            firstObjectiveTick,
            firstLifeObjectiveTick,
            actionCounts,
            replayHash,
            replayPath,
            passed);
    }

    private static string RequiredOption(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value)
            || string.IsNullOrWhiteSpace(value)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"nilbots experiment frontline-labs qualify requires " +
                $"--{name} <value>.");
        }
        return value;
    }

    private static ulong ParseSeed(string value)
    {
        if (!ulong.TryParse(value, out ulong seed))
        {
            throw new InvalidOperationException(
                $"Invalid qualification seed '{value}'.");
        }
        return seed;
    }
}
