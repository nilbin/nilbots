using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli;

/// <summary>
/// Immutable cumulative T2 union profile. Every probe is an ordinary
/// replay-v3 match against an SDK-only deterministic controller. The suite
/// tests both explicit Fabricate and declared automatic activation so one
/// artifact can later run unchanged across the progression factorial.
/// </summary>
internal static class FrontlineLabsFundamentalsQualificationCommand
{
    private const string WaitControllerFingerprint =
        "frontline-qualification-wait-controller-v1";
    private const string OneShotControllerFingerprint =
        "frontline-qualification-one-shot-controller-v1";
    private const string AnalyzerFingerprint =
        "frontline-qualification-3-t2-analyzer-v1";
    private const string PredicateFingerprint =
        "frontline-qualification-3-t2-predicates-v1";

    /// <summary>
    /// Every T2 probe derives from the default duel-depth map arm; the
    /// report states it so an author never infers it from a map ID.
    /// </summary>
    private const string MapArm = "current";

    private enum ControllerKind
    {
        Wait,
        OneShot,
    }

    private enum AnalysisKind
    {
        Contract,
        AutomaticLife,
        ObjectivePath,
        DirectFire,
        StraightEvade,
        ManualFabrication,
    }

    private sealed record ProbePlan(
        string ProbeId,
        string CapabilityComponent,
        ControllerKind Controller,
        AnalysisKind Analysis,
        bool DeterminismRepeat,
        Func<int, ActorResolvedMatchDefinition> Definition);

    private sealed record RunEvidence(
        bool ContractValid,
        bool BotEligible,
        long RuntimeFaultCount,
        bool Disqualified,
        bool ProbeControllerValid,
        bool InitialLifeStarted,
        int BotTurnCount,
        int FaultedTurnCount,
        int AutomaticLifeStartCount,
        int UsefulAutomaticChildCount,
        int? ObjectiveEntryTick,
        int MaxConsecutiveCaptureTicks,
        int AcceptedAttackCount,
        int DamageDealt,
        int ThreatenedTurnCount,
        int SuccessfulThreatMoveCount,
        int DamageTaken,
        int AcceptedFabricationCount,
        int FabricatedLifeStartCount,
        int ChildTurnCount,
        string ReplayHash,
        string ReplayPath,
        bool Passed,
        IReadOnlyList<string> FailedCriteria);

    private sealed record AssignmentEvidence(
        int BotTeamId,
        int BotParticipantId,
        string RulesFingerprint,
        string MapFingerprint,
        string FormatFingerprint,
        string TopologyFingerprint,
        string MatchFingerprint,
        string ControllerFingerprint,
        string AnalyzerFingerprint,
        RunEvidence Primary,
        RunEvidence? DeterminismRepeat,
        bool? ReplayHashMatched,
        bool Passed,
        string Expectation,
        FrontlineLabsQualificationScenario ResolvedScenario,
        IReadOnlyList<string> FailedCriteria);

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
        bool printSummary = true)
    {
        if (runtimeKind != "wasm")
        {
            throw new InvalidOperationException(
                $"{FrontlineLabsQualificationDefinition.FundamentalsSuiteId} " +
                "requires the canonical WASM runtime.");
        }

        ProbePlan[] plans =
        [
            new(
                FrontlineLabsQualificationDefinition.ContractMatrixProbeId,
                "T1",
                ControllerKind.Wait,
                AnalysisKind.Contract,
                DeterminismRepeat: true,
                _ => FrontlineLabsQualificationDefinition
                    .CreateContractMatrixProbe()),
            new(
                FrontlineLabsQualificationDefinition
                    .AutomaticLifeCycleProbeId,
                "T2",
                ControllerKind.Wait,
                AnalysisKind.AutomaticLife,
                DeterminismRepeat: false,
                _ => FrontlineLabsQualificationDefinition
                    .CreateContractMatrixProbe()),
            new(
                FrontlineLabsQualificationDefinition.ObjectivePathProbeId,
                "T2",
                ControllerKind.Wait,
                AnalysisKind.ObjectivePath,
                DeterminismRepeat: false,
                FrontlineLabsQualificationDefinition
                    .CreateObjectivePathProbe),
            new(
                FrontlineLabsQualificationDefinition.DirectFireProbeId,
                "T2",
                ControllerKind.Wait,
                AnalysisKind.DirectFire,
                DeterminismRepeat: false,
                _ => FrontlineLabsQualificationDefinition
                    .CreateDirectFireProbe()),
            new(
                FrontlineLabsQualificationDefinition.StraightEvadeProbeId,
                "T2",
                ControllerKind.OneShot,
                AnalysisKind.StraightEvade,
                DeterminismRepeat: false,
                _ => FrontlineLabsQualificationDefinition
                    .CreateStraightEvadeProbe()),
            new(
                FrontlineLabsQualificationDefinition
                    .ManualFabricationProbeId,
                "T2",
                ControllerKind.Wait,
                AnalysisKind.ManualFabrication,
                DeterminismRepeat: false,
                _ => FrontlineLabsQualificationDefinition
                    .CreateManualFabricationProbe()),
        ];

        string? artifactName = null;
        string? artifactHash = null;
        string? actualRuntimeKind = null;
        var probes = new List<ProbeEvidence>();
        foreach (ProbePlan plan in plans)
        {
            var assignments = new List<AssignmentEvidence>();
            foreach (int botTeamId in new[] { 0, 1 })
            {
                ActorResolvedMatchDefinition definition =
                    plan.Definition(botTeamId);
                int botParticipantId = definition.Topology.Participants
                    .Single(participant =>
                        participant.TeamId == botTeamId)
                    .ParticipantId;
                RunEvidence primary = Execute(
                    plan,
                    definition,
                    botSpec,
                    runtimeKind,
                    seed,
                    botTeamId,
                    botParticipantId,
                    outputDirectory,
                    "primary",
                    quiet: artifactName is not null,
                    ref artifactName,
                    ref artifactHash,
                    ref actualRuntimeKind);
                RunEvidence? repeat = plan.DeterminismRepeat
                    ? Execute(
                        plan,
                        definition,
                        botSpec,
                        runtimeKind,
                        seed,
                        botTeamId,
                        botParticipantId,
                        outputDirectory,
                        "determinism-repeat",
                        quiet: true,
                        ref artifactName,
                        ref artifactHash,
                        ref actualRuntimeKind)
                    : null;
                bool? replayHashMatched = repeat is null
                    ? null
                    : primary.ReplayHash == repeat.ReplayHash;
                bool passed = primary.Passed
                    && (repeat?.Passed ?? true)
                    && (replayHashMatched ?? true);
                var failed = new List<string>(primary.FailedCriteria);
                if (repeat is not null)
                    failed.AddRange(repeat.FailedCriteria);
                if (replayHashMatched == false)
                    failed.Add("identical-replay-hash-on-repeat");
                string[] failedCriteria =
                [
                    .. failed.Distinct(StringComparer.Ordinal),
                ];
                assignments.Add(
                    new AssignmentEvidence(
                        botTeamId,
                        botParticipantId,
                        ActorContractFingerprint.ComputeRules(
                            definition.Rules),
                        ActorContractFingerprint.ComputeMap(
                            definition.Map),
                        ActorContractFingerprint.ComputeFormat(
                            definition.Format),
                        ActorContractFingerprint.ComputeTopology(
                            definition.Topology),
                        ActorContractFingerprint.ComputeMatch(definition),
                        ControllerFingerprint(plan.Controller),
                        AnalyzerFingerprint,
                        primary,
                        repeat,
                        replayHashMatched,
                        passed,
                        Expectation(plan.Analysis),
                        FrontlineLabsQualificationScenario.Resolve(
                            definition,
                            plan.ProbeId,
                            variantId: plan.ProbeId,
                            MapArm,
                            ControllerRole(plan.Controller),
                            botTeamId),
                        failedCriteria));
            }
            probes.Add(
                new ProbeEvidence(
                    plan.ProbeId,
                    plan.CapabilityComponent,
                    assignments.All(assignment => assignment.Passed),
                    assignments));
        }

        string resolvedArtifactName = artifactName
            ?? throw new InvalidOperationException(
                "Qualification produced no artifact identity.");
        string resolvedArtifactHash = artifactHash
            ?? throw new InvalidOperationException(
                "Qualification produced no artifact hash.");
        string resolvedRuntimeKind = actualRuntimeKind
            ?? throw new InvalidOperationException(
                "Qualification produced no runtime identity.");
        bool t1Passed = probes.Single(probe =>
            probe.ProbeId
                == FrontlineLabsQualificationDefinition
                    .ContractMatrixProbeId).Passed;
        bool t2Passed = probes.All(probe => probe.Passed);
        string? tierAwarded = t2Passed
            ? "T2"
            : t1Passed
                ? "T1"
                : null;
        var fingerprintParts = new List<string>
        {
            FrontlineLabsQualificationDefinition.FundamentalsSuiteId,
            FrontlineLabsQualificationDefinition
                .FundamentalsSuiteVersion
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            FrontlineLabsQualificationDefinition.FundamentalsProfileId,
            PredicateFingerprint,
        };
        fingerprintParts.AddRange(
            probes.SelectMany(probe =>
                probe.Assignments.Select(assignment =>
                    string.Join(
                        ":",
                        probe.ProbeId,
                        assignment.BotTeamId,
                        assignment.MatchFingerprint,
                        assignment.ControllerFingerprint,
                        assignment.AnalyzerFingerprint))));
        string qualificationFingerprint = Fingerprint(
            string.Join("\n", fingerprintParts));
        var report = new QualificationReport(
            SchemaVersion: 4,
            FrontlineLabsQualificationDefinition.FundamentalsSuiteId,
            FrontlineLabsQualificationDefinition.FundamentalsSuiteVersion,
            FrontlineLabsQualificationDefinition.FundamentalsProfileId,
            qualificationFingerprint,
            resolvedArtifactName,
            resolvedArtifactHash,
            resolvedRuntimeKind,
            seed,
            Passed: t2Passed,
            ProfileComplete: true,
            tierAwarded,
            CoordinationGradeAwarded: null,
            BalanceEvidenceEligible: false,
            probes);
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

        if (printSummary)
        {
            Console.WriteLine($"Qualification suite: {report.SuiteId}");
            Console.WriteLine(
                $"Profile:             {report.QualificationProfileId}");
            Console.WriteLine(
                $"Artifact:            {resolvedArtifactName} " +
                $"[{resolvedArtifactHash[..12]}…]");
            foreach (ProbeEvidence probe in probes)
            {
                Console.WriteLine(
                    $"{probe.ProbeId,-24} " +
                    $"{(probe.Passed ? "PASS" : "FAIL")}");
            }
            Console.WriteLine($"Report:              {reportPath}");
            Console.WriteLine(
                $"Tier awarded:        {tierAwarded ?? "none"}");
        }

        bool invalid = probes
            .SelectMany(probe => probe.Assignments)
            .SelectMany(assignment =>
                assignment.DeterminismRepeat is null
                    ? [assignment.Primary]
                    : new[]
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
                || run.FaultedTurnCount != 0);
        return invalid ? 2 : t2Passed ? 0 : 3;
    }

    private static RunEvidence Execute(
        ProbePlan plan,
        ActorResolvedMatchDefinition definition,
        string botSpec,
        string runtimeKind,
        ulong seed,
        int botTeamId,
        int botParticipantId,
        string outputDirectory,
        string runId,
        bool quiet,
        ref string? artifactName,
        ref string? artifactHash,
        ref string? actualRuntimeKind)
    {
        using ResolvedGenericActorBot bot =
            ResolvedGenericActorBot.Resolve(
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

        int controllerTeamId = 1 - botTeamId;
        int controllerParticipantId = definition.Topology.Participants
            .Single(participant =>
                participant.TeamId == controllerTeamId)
            .ParticipantId;
        using IGenericActorRuntimeFactory controllerFactory =
            ControllerFactory(plan.Controller);
        GenericActorParticipantConfiguration botParticipant =
            bot.ToParticipant(botParticipantId, botTeamId);
        GenericActorParticipantConfiguration controllerParticipant =
            ControllerParticipant(
                plan.Controller,
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
            replay = GenericActorReplayDocument.Create(session);
        }
        bool contractValid = GenericActorReplayDocument.VerifyHash(
            replay.CanonicalJson,
            out _);
        string runDirectory = Path.Combine(
            outputDirectory,
            plan.ProbeId,
            $"bot-team-{botTeamId}",
            runId);
        WrittenReplay written = ReplayOutput.WriteJson(
            replay.CanonicalJson,
            runDirectory);
        return Analyze(
            plan.Analysis,
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
        AnalysisKind analysis,
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
        JsonElement contract = root
            .GetProperty("header")
            .GetProperty("contract");
        HashSet<string> movementActions = ActionIds(
            contract,
            "movement");
        HashSet<string> attackActions = ActionIds(contract, "attack");
        HashSet<string> fabricationActions = ActionIds(
            contract,
            "fabrication");
        var lifeStarts = new List<JsonElement>();
        lifeStarts.AddRange(
            root.GetProperty("initialFrame")
                .GetProperty("lifeStarts")
                .EnumerateArray());
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            lifeStarts.AddRange(
                tick.GetProperty("tickStart")
                    .GetProperty("lifeStarts")
                    .EnumerateArray());
        }

        bool initialLifeStarted = lifeStarts.Any(start =>
            LifeStartMatches(
                start,
                botTeamId,
                unitId: 0,
                "initial"));
        int automaticLifeStartCount = lifeStarts.Count(start =>
            ActorTeam(start) == botTeamId
            && start.GetProperty("origin")
                .GetProperty("reason")
                .GetString() == "automatic-activation");
        int fabricatedLifeStartCount = lifeStarts.Count(start =>
            ActorTeam(start) == botTeamId
            && start.GetProperty("origin")
                .GetProperty("reason")
                .GetString() == "fabrication");

        int botTurnCount = 0;
        int faultedTurnCount = 0;
        int acceptedAttackCount = 0;
        int threatenedTurnCount = 0;
        int successfulThreatMoveCount = 0;
        int acceptedFabricationCount = 0;
        int childTurnCount = 0;
        var firstChildDistance = new Dictionary<int, int>();
        var minimumChildDistance = new Dictionary<int, int>();
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            foreach (JsonElement turn in tick
                         .GetProperty("actorTurns")
                         .EnumerateArray())
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
                string? acceptedActionId =
                    AcceptedActionId(resolution);
                if (acceptedActionId is not null
                    && attackActions.Contains(acceptedActionId))
                {
                    acceptedAttackCount++;
                }
                if (acceptedActionId is not null
                    && fabricationActions.Contains(acceptedActionId))
                {
                    acceptedFabricationCount++;
                }

                JsonElement observation =
                    turn.GetProperty("observation");
                JsonElement self = observation.GetProperty("self");
                int unitId = self.GetProperty("actorId")
                    .GetProperty("unitId")
                    .GetInt32();
                if (unitId != 0)
                {
                    childTurnCount++;
                    Position[] objective =
                        ActiveObjectiveTiles(contract, observation);
                    if (objective.Length > 0)
                    {
                        int distance = objective.Min(position =>
                            position.ChebyshevDistance(
                                ReadPosition(
                                    self.GetProperty("position"))));
                        firstChildDistance.TryAdd(unitId, distance);
                        minimumChildDistance[unitId] = Math.Min(
                            minimumChildDistance.GetValueOrDefault(
                                unitId,
                                distance),
                            distance);
                    }
                }

                bool threatened = observation
                    .GetProperty("visibleProjectiles")
                    .EnumerateArray()
                    .Any(projectile =>
                        projectile.GetProperty("ownerTeamId")
                            .GetInt32() != botTeamId
                        && ProjectileSweepsWithinAdvances(
                            projectile,
                            ReadPosition(
                                self.GetProperty("position")),
                            maxAdvances: 2));
                if (!threatened)
                    continue;
                threatenedTurnCount++;
                if (acceptedActionId is not null
                    && movementActions.Contains(acceptedActionId)
                    && resolution.GetProperty("outcome").GetString()
                        == "success")
                {
                    successfulThreatMoveCount++;
                }
            }
        }

        int damageDealt = 0;
        int damageTaken = 0;
        var damagingChildUnits = new HashSet<int>();
        foreach (JsonElement damage in root
                     .GetProperty("ticks")
                     .EnumerateArray()
                     .SelectMany(tick =>
                         tick.GetProperty("events").EnumerateArray())
                     .Where(item =>
                         item.GetProperty("kind").GetString()
                            == "damage"))
        {
            JsonElement payload = damage.GetProperty("payload");
            int amount = payload.GetProperty("amount").GetInt32();
            if (payload.GetProperty("sourceTeamId").GetInt32()
                == botTeamId)
            {
                damageDealt += amount;
                JsonElement source =
                    payload.GetProperty("sourceActorId");
                int unitId = source.GetProperty("unitId").GetInt32();
                if (unitId != 0)
                    damagingChildUnits.Add(unitId);
            }
            if (payload.GetProperty("targetActorId")
                    .GetProperty("teamId")
                    .GetInt32() == botTeamId)
            {
                damageTaken += amount;
            }
        }
        int usefulAutomaticChildCount = firstChildDistance.Count(pair =>
            minimumChildDistance[pair.Key] < pair.Value
            || damagingChildUnits.Contains(pair.Key));

        (int? objectiveEntryTick, int maxConsecutiveCaptureTicks) =
            ObjectiveEvidence(root, contract, botTeamId);
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
        bool controllerValid = finalState
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
        var criteria = new List<(string Name, bool Satisfied)>
        {
            ("replay-verifies-against-its-own-hash", contractValid),
            ("tested-team-eligible-at-match-end", botEligible),
            ("no-runtime-faults", runtimeFaultCount == 0),
            ("not-disqualified", !disqualified),
            ("probe-controller-ran-without-fault", controllerValid),
            ("initial-life-started", initialLifeStarted),
            ("tested-artifact-took-at-least-one-turn", botTurnCount > 0),
            ("no-faulted-turns", faultedTurnCount == 0),
        };
        switch (analysis)
        {
            case AnalysisKind.Contract:
                criteria.Add((
                    "both-declared-automatic-companions-started",
                    automaticLifeStartCount == 2));
                criteria.Add((
                    "at-least-one-child-life-took-a-turn",
                    childTurnCount > 0));
                break;
            case AnalysisKind.AutomaticLife:
                criteria.Add((
                    "both-declared-automatic-companions-started",
                    automaticLifeStartCount == 2));
                criteria.Add((
                    "every-automatic-child-closed-on-the-objective-or-"
                    + "dealt-damage",
                    usefulAutomaticChildCount == 2));
                break;
            case AnalysisKind.ObjectivePath:
                criteria.Add((
                    "reached-the-active-objective",
                    objectiveEntryTick is not null));
                criteria.Add((
                    "reached-it-early-rather-than-eventually",
                    objectiveEntryTick is <= 8));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxConsecutiveCaptureTicks >= 5));
                break;
            case AnalysisKind.DirectFire:
                criteria.Add((
                    "got-at-least-one-attack-accepted",
                    acceptedAttackCount > 0));
                criteria.Add((
                    "dealt-at-least-one-damage",
                    damageDealt > 0));
                break;
            case AnalysisKind.StraightEvade:
                criteria.Add((
                    "was-on-the-declared-two-advance-hazard-path",
                    threatenedTurnCount > 0));
                criteria.Add((
                    "moved-successfully-on-a-threatened-turn",
                    successfulThreatMoveCount > 0));
                criteria.Add(("took-no-damage", damageTaken == 0));
                break;
            case AnalysisKind.ManualFabrication:
                criteria.Add((
                    "got-at-least-one-fabrication-accepted",
                    acceptedFabricationCount > 0));
                criteria.Add((
                    "a-fabricated-life-started",
                    fabricatedLifeStartCount > 0));
                criteria.Add((
                    "at-least-one-child-life-took-a-turn",
                    childTurnCount > 0));
                break;
            default:
                criteria.Add(("known-analysis-kind", false));
                break;
        }
        string[] failedCriteria =
        [
            .. criteria
                .Where(criterion => !criterion.Satisfied)
                .Select(criterion => criterion.Name),
        ];
        bool passed = failedCriteria.Length == 0;
        return new RunEvidence(
            contractValid,
            botEligible,
            runtimeFaultCount,
            disqualified,
            controllerValid,
            initialLifeStarted,
            botTurnCount,
            faultedTurnCount,
            automaticLifeStartCount,
            usefulAutomaticChildCount,
            objectiveEntryTick,
            maxConsecutiveCaptureTicks,
            acceptedAttackCount,
            damageDealt,
            threatenedTurnCount,
            successfulThreatMoveCount,
            damageTaken,
            acceptedFabricationCount,
            fabricatedLifeStartCount,
            childTurnCount,
            replayHash,
            replayPath,
            passed,
            failedCriteria);
    }

    /// <summary>
    /// One plain-language line per probe stating the shape of a passing
    /// case. It is derived from the same clauses the analyzer evaluates so
    /// a failing report never leaves an author guessing what was wanted.
    /// </summary>
    private static string Expectation(AnalysisKind analysis) =>
        analysis switch
        {
            AnalysisKind.Contract =>
                "Run the whole declared contract cleanly under non-default "
                + "identities: both scheduled automatic companions start on "
                + "their declared unlock ticks, at least one child life "
                + "takes a turn, no turn faults, and the same seed replays "
                + "to an identical hash.",
            AnalysisKind.AutomaticLife =>
                "Give every automatic companion a job: both scheduled child "
                + "lives start, and each of them either closes distance "
                + "toward the active objective or deals damage; a child "
                + "that spawns and idles fails.",
            AnalysisKind.ObjectivePath =>
                "Walk the tested life onto the active objective and stay "
                + "there: reach it early in this short contract rather than "
                + "eventually, then hold sole capture control for an "
                + "uninterrupted run of ticks instead of touching it and "
                + "drifting off.",
            AnalysisKind.DirectFire =>
                "Take the free shot: with an open lane to an inert target, "
                + "get at least one attack accepted and deal at least one "
                + "point of damage.",
            AnalysisKind.StraightEvade =>
                "Step off the line of the controller's one straight shot: "
                + "be on its declared two-advance hazard path at least "
                + "once, answer at least one such turn with a successful "
                + "move, and finish having taken no damage.",
            AnalysisKind.ManualFabrication =>
                "Use the explicit lifecycle: get at least one fabrication "
                + "accepted, have the fabricated life actually start, and "
                + "let a child life take at least one turn.",
            _ => "Unknown probe analysis.",
        };

    private static (
        int? EntryTick,
        int MaxConsecutiveCaptureTicks)
        ObjectiveEvidence(
            JsonElement root,
            JsonElement contract,
            int botTeamId)
    {
        int? entryTick = null;
        int consecutive = 0;
        int maximum = 0;
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            JsonElement post = tick.GetProperty("postState");
            Position[] objective = ActiveObjectiveTiles(contract, post);
            bool occupies = post.GetProperty("activeLives")
                .EnumerateArray()
                .Any(life =>
                    life.GetProperty("actorId")
                        .GetProperty("teamId")
                        .GetInt32() == botTeamId
                    && objective.Contains(
                        ReadPosition(life.GetProperty("position"))));
            int tickNumber = tick.GetProperty("tick").GetInt32();
            if (occupies)
                entryTick ??= tickNumber;
            JsonElement mode = post.GetProperty("mode");
            bool contributes = occupies
                && mode.GetProperty("claimingTeamId").ValueKind
                    == JsonValueKind.Number
                && mode.GetProperty("claimingTeamId").GetInt32()
                    == botTeamId;
            consecutive = contributes ? consecutive + 1 : 0;
            maximum = Math.Max(maximum, consecutive);
        }
        return (entryTick, maximum);
    }

    private static Position[] ActiveObjectiveTiles(
        JsonElement contract,
        JsonElement state)
    {
        JsonElement mode = state.GetProperty("mode");
        int activeIndex = mode
            .GetProperty("activePositionIndex")
            .GetInt32();
        JsonElement binding =
            contract.GetProperty("modeMapBinding");
        JsonElement ids =
            binding.GetProperty("orderedObjectiveRegionIds");
        if (activeIndex < 0 || activeIndex >= ids.GetArrayLength())
            return [];
        string regionId = ids[activeIndex].GetString()!;
        return
        [
            .. contract.GetProperty("map")
                .GetProperty("regions")
                .EnumerateArray()
                .Single(region =>
                    region.GetProperty("regionId").GetString()
                        == regionId)
                .GetProperty("tiles")
                .EnumerateArray()
                .Select(tile =>
                    new Position(
                        tile[0].GetInt32(),
                        tile[1].GetInt32())),
        ];
    }

    private static HashSet<string> ActionIds(
        JsonElement contract,
        string kind) =>
        contract.GetProperty("rules")
            .GetProperty("actions")
            .EnumerateArray()
            .Where(action =>
                action.GetProperty("kind").GetString() == kind)
            .Select(action => action.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

    private static string? AcceptedActionId(JsonElement resolution)
    {
        JsonElement action = resolution.GetProperty("acceptedAction");
        return action.ValueKind == JsonValueKind.Object
            ? action.GetProperty("actionId").GetString()
            : null;
    }

    private static bool ProjectileSweepsWithinAdvances(
        JsonElement projectile,
        Position target,
        int maxAdvances)
    {
        if (maxAdvances < 1)
            return false;
        (int dx, int dy) = projectile
            .GetProperty("heading")
            .GetString() switch
        {
            "north" => (0, -1),
            "north-east" => (1, -1),
            "east" => (1, 0),
            "south-east" => (1, 1),
            "south" => (0, 1),
            "south-west" => (-1, 1),
            "west" => (-1, 0),
            "north-west" => (-1, -1),
            _ => (0, 0),
        };
        Position position = ReadPosition(
            projectile.GetProperty("position"));
        int tiles = Math.Min(
            projectile.GetProperty("tilesPerAdvance").GetInt32()
                * maxAdvances,
            projectile.GetProperty("remainingTiles").GetInt32());
        return Enumerable.Range(1, tiles).Any(step =>
            position.Offset(dx * step, dy * step) == target);
    }

    private static Position ReadPosition(JsonElement position) =>
        new(
            position.GetProperty("x").GetInt32(),
            position.GetProperty("y").GetInt32());

    private static int ActorTeam(JsonElement lifeStart) =>
        lifeStart.GetProperty("actorId")
            .GetProperty("teamId")
            .GetInt32();

    private static bool LifeStartMatches(
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

    private static IGenericActorRuntimeFactory ControllerFactory(
        ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait =>
                new InProcessGenericActorRuntimeFactory(
                    () => new FrontlineLabsQualificationWaitController()),
            ControllerKind.OneShot =>
                new InProcessGenericActorRuntimeFactory(
                    () =>
                        new FrontlineLabsQualificationOneShotController()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static GenericActorParticipantConfiguration
        ControllerParticipant(
            ControllerKind kind,
            IGenericActorRuntimeFactory runtimeFactory,
            int participantId,
            int teamId) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = kind == ControllerKind.Wait
                ? "Qualification Passive Controller"
                : "Qualification One-Shot Controller",
            RuntimeFactory = runtimeFactory,
            RuntimeKind = "in-process-qualification-controller",
            ArtifactHash = ControllerFingerprint(kind),
            Accent = "#f97316",
            LookId = "bastion",
            ProjectileLookId = "ember-lance",
        };

    private static string ControllerFingerprint(ControllerKind kind) =>
        kind == ControllerKind.Wait
            ? WaitControllerFingerprint
            : OneShotControllerFingerprint;

    private static string ControllerRole(ControllerKind kind) =>
        kind == ControllerKind.Wait
            ? "passive-wait-controller"
            : "one-shot-straight-controller";

    private static string Fingerprint(string value) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
