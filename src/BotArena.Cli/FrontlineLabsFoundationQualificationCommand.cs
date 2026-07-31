using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli;

/// <summary>
/// Runs the first cumulative-profile qualification component without
/// reinterpreting the immutable suite-1 entry probe. This slice proves
/// deterministic, fault-free WASM execution through a declared automatic
/// activation; it intentionally awards no tier until all T1/T2 probes exist.
/// </summary>
internal static class FrontlineLabsFoundationQualificationCommand
{
    private const string ControllerFingerprint =
        "frontline-qualification-2-passive-controller-v1";
    private const string PredicateVersion =
        "contract-auto-determinism-predicates-v1";

    private sealed record RunEvidence(
        bool ContractValid,
        bool BotEligible,
        long RuntimeFaultCount,
        bool Disqualified,
        bool ProbeControllerValid,
        bool FullProbeWindowObserved,
        bool InitialLifeStarted,
        bool AutomaticLifeStarted,
        int BotTurnCount,
        int FaultedTurnCount,
        string ReplayHash,
        string ReplayPath,
        bool Passed);

    private sealed record AssignmentEvidence(
        int BotTeamId,
        int BotParticipantId,
        RunEvidence Primary,
        RunEvidence DeterminismRepeat,
        bool ReplayHashMatched,
        bool Passed);

    private sealed record ProbeEvidence(
        string ProbeId,
        string CapabilityComponent,
        bool Passed,
        IReadOnlyList<AssignmentEvidence> Assignments);

    private sealed record QualificationReport(
        int SchemaVersion,
        string SuiteId,
        int SuiteVersion,
        string QualificationProfileId,
        string QualificationContractFingerprint,
        string ArtifactName,
        string ArtifactHash,
        string RuntimeKind,
        ulong Seed,
        string RulesFingerprint,
        string MapFingerprint,
        string FormatFingerprint,
        string TopologyFingerprint,
        string MatchFingerprint,
        string ControllerFingerprint,
        bool Passed,
        bool ProfileComplete,
        string? TierAwarded,
        string? CoordinationGradeAwarded,
        bool BalanceEvidenceEligible,
        IReadOnlyList<ProbeEvidence> Probes);

    public static int Run(
        string botSpec,
        string runtimeKind,
        ulong seed,
        string outputDirectory,
        bool withViewer = false)
    {
        if (runtimeKind != "wasm")
        {
            throw new InvalidOperationException(
                $"{FrontlineLabsQualificationDefinition.FoundationSuiteId} " +
                "requires the canonical WASM runtime.");
        }

        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition
                .CreateContractAutoDeterminismProbe();
        string matchFingerprint =
            ActorContractFingerprint.ComputeMatch(definition);
        string qualificationFingerprint = Fingerprint(
            string.Join(
                "\n",
                FrontlineLabsQualificationDefinition.FoundationSuiteId,
                FrontlineLabsQualificationDefinition
                    .FoundationSuiteVersion,
                FrontlineLabsQualificationDefinition.FoundationProfileId,
                FrontlineLabsQualificationDefinition
                    .ContractAutoDeterminismProbeId,
                matchFingerprint,
                ControllerFingerprint,
                PredicateVersion));

        var assignments = new List<AssignmentEvidence>();
        string? artifactName = null;
        string? artifactHash = null;
        string? actualRuntimeKind = null;
        foreach (int botTeamId in new[] { 0, 1 })
        {
            int botParticipantId = definition.Topology.Participants
                .Single(item => item.TeamId == botTeamId)
                .ParticipantId;
            RunEvidence primary = Execute(
                definition,
                botSpec,
                runtimeKind,
                seed,
                botTeamId,
                botParticipantId,
                outputDirectory,
                "primary",
                quiet: artifactName is not null,
                withViewer,
                ref artifactName,
                ref artifactHash,
                ref actualRuntimeKind);
            RunEvidence repeat = Execute(
                definition,
                botSpec,
                runtimeKind,
                seed,
                botTeamId,
                botParticipantId,
                outputDirectory,
                "determinism-repeat",
                quiet: true,
                withViewer,
                ref artifactName,
                ref artifactHash,
                ref actualRuntimeKind);
            bool replayHashMatched =
                primary.ReplayHash == repeat.ReplayHash;
            assignments.Add(
                new AssignmentEvidence(
                    botTeamId,
                    botParticipantId,
                    primary,
                    repeat,
                    replayHashMatched,
                    primary.Passed
                        && repeat.Passed
                        && replayHashMatched));
        }

        bool passed = assignments.All(item => item.Passed);
        string resolvedArtifactName = artifactName
            ?? throw new InvalidOperationException(
                "Qualification produced no artifact identity.");
        string resolvedArtifactHash = artifactHash
            ?? throw new InvalidOperationException(
                "Qualification produced no artifact hash.");
        string resolvedRuntimeKind = actualRuntimeKind
            ?? throw new InvalidOperationException(
                "Qualification produced no runtime identity.");
        var probe = new ProbeEvidence(
            FrontlineLabsQualificationDefinition
                .ContractAutoDeterminismProbeId,
            "T1-component",
            passed,
            assignments);
        var report = new QualificationReport(
            SchemaVersion: 2,
            FrontlineLabsQualificationDefinition.FoundationSuiteId,
            FrontlineLabsQualificationDefinition.FoundationSuiteVersion,
            FrontlineLabsQualificationDefinition.FoundationProfileId,
            qualificationFingerprint,
            resolvedArtifactName,
            resolvedArtifactHash,
            resolvedRuntimeKind,
            seed,
            ActorContractFingerprint.ComputeRules(definition.Rules),
            ActorContractFingerprint.ComputeMap(definition.Map),
            ActorContractFingerprint.ComputeFormat(definition.Format),
            ActorContractFingerprint.ComputeTopology(definition.Topology),
            matchFingerprint,
            ControllerFingerprint,
            passed,
            ProfileComplete: false,
            TierAwarded: null,
            CoordinationGradeAwarded: null,
            BalanceEvidenceEligible: false,
            [probe]);
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
            $"Qualification suite: {report.SuiteId}");
        Console.WriteLine(
            $"Profile:             {report.QualificationProfileId}");
        Console.WriteLine(
            $"Probe:               {probe.ProbeId}");
        Console.WriteLine(
            $"Artifact:            {resolvedArtifactName} " +
            $"[{resolvedArtifactHash[..12]}…]");
        foreach (AssignmentEvidence assignment in assignments)
        {
            Console.WriteLine(
                $"bot team {assignment.BotTeamId}:        " +
                $"{(assignment.Passed ? "PASS" : "FAIL")} " +
                $"turns={assignment.Primary.BotTurnCount} " +
                $"automatic-life=" +
                $"{assignment.Primary.AutomaticLifeStarted} " +
                $"deterministic={assignment.ReplayHashMatched}");
        }
        Console.WriteLine($"Report:              {reportPath}");
        Console.WriteLine(
            "Tier awarded:        none " +
            "(foundation profile remains incomplete)");

        bool invalid = assignments
            .SelectMany(assignment =>
                new[]
                {
                    assignment.Primary,
                    assignment.DeterminismRepeat,
                })
            .Any(run =>
                !run.ContractValid
                || !run.BotEligible
                || run.RuntimeFaultCount != 0
                || run.Disqualified
                || !run.ProbeControllerValid
                || !run.FullProbeWindowObserved
                || run.FaultedTurnCount != 0);
        return invalid ? 2 : passed ? 0 : 3;
    }

    private static RunEvidence Execute(
        ActorResolvedMatchDefinition definition,
        string botSpec,
        string runtimeKind,
        ulong seed,
        int botTeamId,
        int botParticipantId,
        string outputDirectory,
        string runId,
        bool quiet,
        bool withViewer,
        ref string? artifactName,
        ref string? artifactHash,
        ref string? actualRuntimeKind)
    {
        using ResolvedLabsEntrant bot =
            ResolvedLabsEntrant.Resolve(
                botSpec,
                runtimeKind,
                quiet);
        artifactName ??= bot.Name;
        artifactHash ??= bot.ArtifactHash;
        actualRuntimeKind ??= bot.RuntimeKind;
        if (artifactName != bot.Name
            || artifactHash != bot.ArtifactHash
            || actualRuntimeKind != bot.RuntimeKind)
        {
            throw new InvalidOperationException(
                "Qualification artifact identity changed between runs.");
        }

        using var controllerFactory =
            new InProcessGenericActorRuntimeFactory(
                () => new FrontlineLabsQualificationWaitController());
        int controllerTeamId = 1 - botTeamId;
        int controllerParticipantId = definition.Topology.Participants
            .Single(item => item.TeamId == controllerTeamId)
            .ParticipantId;
        GenericActorParticipantConfiguration botParticipant =
            bot.ToParticipant(botParticipantId, botTeamId);
        GenericActorParticipantConfiguration controllerParticipant =
            PassiveParticipant(
                controllerFactory,
                controllerParticipantId,
                controllerTeamId);
        GenericActorParticipantConfiguration[] participants =
            botParticipantId < controllerParticipantId
                ? [botParticipant, controllerParticipant]
                : [controllerParticipant, botParticipant];

        GenericActorMatchResult result;
        GenericActorReplayDocument replay;
        using (var session = new GenericActorMatchSession(
                   definition,
                   participants,
                   seed))
        {
            result = session.Run();
            replay = GenericActorReplayDocument.Create(
                session,
                FrontlineLabsReplayPresentation.Create(definition));
        }
        bool contractValid = GenericActorReplayDocument.VerifyHash(
            replay.CanonicalJson,
            out _);
        string runDirectory = Path.Combine(
            outputDirectory,
            FrontlineLabsQualificationDefinition
                .ContractAutoDeterminismProbeId,
            $"bot-team-{botTeamId}",
            runId);
        WrittenReplay written = ReplayOutput.WriteJson(
            replay.CanonicalJson,
            runDirectory,
            themeId: null,
            withViewer);
        return Analyze(
            replay.CanonicalJson,
            botTeamId,
            botParticipantId,
            result.EligibleTeamIds.Contains(botTeamId),
            replay.ReplayHash,
            Path.GetRelativePath(
                outputDirectory,
                written.ReplayPath),
            contractValid);
    }

    private static RunEvidence Analyze(
        string replayJson,
        int botTeamId,
        int botParticipantId,
        bool botEligible,
        string replayHash,
        string replayPath,
        bool contractValid)
    {
        using JsonDocument document = JsonDocument.Parse(replayJson);
        JsonElement root = document.RootElement;
        var lifeStarts = new List<JsonElement>();
        lifeStarts.AddRange(
            root.GetProperty("initialFrame")
                .GetProperty("lifeStarts")
                .EnumerateArray());
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            if (
                tick.TryGetProperty(
                    "tickStart",
                    out JsonElement tickStart)
                && tickStart.TryGetProperty(
                    "lifeStarts",
                    out JsonElement tickStarts))
            {
                lifeStarts.AddRange(tickStarts.EnumerateArray());
            }
        }

        bool initialLifeStarted = lifeStarts.Any(start =>
            IsLifeStart(start, botTeamId, unitId: 0, "initial"));
        bool automaticLifeStarted = lifeStarts.Any(start =>
            IsLifeStart(
                start,
                botTeamId,
                unitId: 1,
                "automatic-activation"));
        int botTurnCount = 0;
        int faultedTurnCount = 0;
        foreach (JsonElement turn in root
                     .GetProperty("ticks")
                     .EnumerateArray()
                     .SelectMany(tick =>
                         tick.GetProperty("actorTurns").EnumerateArray()))
        {
            if (turn.GetProperty("participantId").GetInt32()
                != botParticipantId)
            {
                continue;
            }
            botTurnCount++;
            JsonElement resolution =
                turn.GetProperty("actionResolution");
            if (
                resolution.GetProperty("outcome").GetString()
                    == "faulted"
                || resolution.GetProperty("runtimeFault").ValueKind
                    != JsonValueKind.Null)
            {
                faultedTurnCount++;
            }
        }

        JsonElement finalState = root.GetProperty("ticks")
            .EnumerateArray()
            .Last()
            .GetProperty("postState");
        JsonElement participant = finalState
            .GetProperty("participants")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("participantId").GetInt32()
                    == botParticipantId);
        long runtimeFaultCount = long.Parse(
            participant.GetProperty("runtimeFaultCount").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        bool disqualified =
            participant.GetProperty("disqualified").GetBoolean();
        bool probeControllerValid = finalState
            .GetProperty("participants")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("participantId").GetInt32()
                    != botParticipantId)
            .All(item =>
                long.Parse(
                    item.GetProperty("runtimeFaultCount").GetString()!,
                    System.Globalization.CultureInfo.InvariantCulture) == 0
                && !item.GetProperty("disqualified").GetBoolean());
        JsonElement result = root.GetProperty("result");
        bool fullProbeWindowObserved =
            result.GetProperty("completionReason").GetString()
                == "max-ticks"
            && result.GetProperty("endTick").GetInt32() == 129;
        bool passed = contractValid
            && botEligible
            && runtimeFaultCount == 0
            && !disqualified
            && probeControllerValid
            && fullProbeWindowObserved
            && initialLifeStarted
            && automaticLifeStarted
            && botTurnCount > 0
            && faultedTurnCount == 0;
        return new RunEvidence(
            contractValid,
            botEligible,
            runtimeFaultCount,
            disqualified,
            probeControllerValid,
            fullProbeWindowObserved,
            initialLifeStarted,
            automaticLifeStarted,
            botTurnCount,
            faultedTurnCount,
            replayHash,
            replayPath,
            passed);
    }

    private static bool IsLifeStart(
        JsonElement start,
        int teamId,
        int unitId,
        string reason)
    {
        JsonElement actorId = start.GetProperty("actorId");
        return actorId.GetProperty("teamId").GetInt32() == teamId
            && actorId.GetProperty("unitId").GetInt32() == unitId
            && start.GetProperty("origin")
                .GetProperty("reason")
                .GetString() == reason;
    }

    private static GenericActorParticipantConfiguration PassiveParticipant(
        IGenericActorRuntimeFactory runtimeFactory,
        int participantId,
        int teamId) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = "Foundation Passive Controller",
            RuntimeFactory = runtimeFactory,
            RuntimeKind = "in-process-qualification-controller",
            ArtifactHash = ControllerFingerprint,
            Accent = "#f97316",
            LookId = "bastion",
            ProjectileLookId = "ember-lance",
        };

    private static string Fingerprint(string value) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
