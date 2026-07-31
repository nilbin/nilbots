using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli;

/// <summary>
/// Immutable cumulative T4 positional profile. It reruns the exact cumulative
/// T3 prerequisite, then exercises suppression, initiative, objective-aware
/// response, front rotation, and a declared map holdout.
/// </summary>
internal static class FrontlineLabsPositionalQualificationCommand
{
    private const string WaitControllerFingerprint =
        "frontline-qualification-wait-controller-v1";
    private const string OneShotControllerFingerprint =
        "frontline-qualification-one-shot-controller-v1";
    private const string PressureControllerFingerprint =
        "frontline-qualification-pressure-controller-v1";
    private const string AnalyzerFingerprint =
        "frontline-qualification-5-t4-analyzer-v1";
    private const string PredicateFingerprint =
        "frontline-qualification-5-t4-predicates-v1";

    private enum ControllerKind
    {
        Wait,
        OneShot,
        Pressure,
    }

    private enum AnalysisKind
    {
        Suppression,
        Entry,
        Prediction,
        Rotation,
        MapHoldout,
        EscortIntegrity,
    }

    private sealed record CasePlan(
        string ProbeId,
        string VariantId,
        string MapArm,
        ControllerKind Controller,
        AnalysisKind Analysis,
        Func<int, ActorResolvedMatchDefinition> Definition);

    private sealed record PrerequisiteEvidence(
        string SuiteId,
        int SuiteVersion,
        string QualificationProfileId,
        string QualificationContractFingerprint,
        string ReportPath,
        string ReportSha256,
        bool Passed,
        string? TierAwarded);

    private sealed record RunEvidence(
        bool ContractValid,
        bool BotEligible,
        long RuntimeFaultCount,
        bool Disqualified,
        bool ProbeControllerValid,
        int BotTurnCount,
        int FaultedTurnCount,
        string? FirstAcceptedActionId,
        int StraightAttackCount,
        int CurvedAttackCount,
        int DamageDealt,
        int DamageTaken,
        int ControllerAttackCount,
        int ThreatTurnCount,
        int ThreatMoveIntoObjectiveCount,
        int? FirstLifeObjectiveTick,
        int? DamageTakenBeforeEntry,
        int MaxConsecutiveCaptureTicks,
        int InitialActivePositionIndex,
        int? FirstAdvancedPositionIndex,
        int? FirstAdvancedPositionTick,
        int? FirstAdvancedObjectiveEntryTick,
        int MaxAdvancedObjectiveResidenceTicks,
        string ReplayHash,
        string ReplayPath,
        bool Passed,
        IReadOnlyList<string> FailedCriteria);

    private sealed record CaseEvidence(
        string VariantId,
        int BotTeamId,
        int BotParticipantId,
        string RulesFingerprint,
        string MapFingerprint,
        string FormatFingerprint,
        string TopologyFingerprint,
        string MatchFingerprint,
        string ControllerFingerprint,
        string AnalyzerFingerprint,
        RunEvidence Run,
        bool Passed,
        string Expectation,
        FrontlineLabsQualificationScenario ResolvedScenario,
        IReadOnlyList<string> FailedCriteria);

    private sealed record ProbeEvidence(
        string ProbeId,
        string CapabilityComponent,
        bool Passed,
        IReadOnlyList<CaseEvidence> Cases);

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
        PrerequisiteEvidence Prerequisite,
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
        bool mindProfile = false,
        bool withViewer = false)
    {
        string suiteId = mindProfile
            ? FrontlineLabsQualificationDefinition.MindPositionalSuiteId
            : FrontlineLabsQualificationDefinition.PositionalSuiteId;
        string profileId = mindProfile
            ? FrontlineLabsQualificationDefinition.MindPositionalProfileId
            : FrontlineLabsQualificationDefinition.PositionalProfileId;
        if (runtimeKind != "wasm")
        {
            throw new InvalidOperationException(
                $"{suiteId} requires the canonical WASM runtime.");
        }

        string prerequisiteDirectory = Path.Combine(
            outputDirectory,
            "prerequisite-t3");
        int prerequisiteExit =
            FrontlineLabsTacticalQualificationCommand.Run(
                botSpec,
                runtimeKind,
                seed,
                prerequisiteDirectory,
                printSummary: false,
                mindProfile,
                withViewer);
        string prerequisiteReportPath = Path.Combine(
            prerequisiteDirectory,
            "qualification.json");
        PrerequisiteEvidence prerequisite = ReadPrerequisite(
            prerequisiteReportPath,
            outputDirectory,
            mindProfile);
        if (prerequisiteExit == 2)
        {
            Console.WriteLine(
                "T4 qualification stopped: cumulative T3 evidence was " +
                "runtime/contract invalid.");
            Console.WriteLine(
                $"Prerequisite report: {prerequisiteReportPath}");
            return 2;
        }

        CasePlan[] plans =
        [
            new(
                FrontlineLabsQualificationDefinition
                    .SuppressionChokeProbeId,
                "objective-hold-straight-pressure",
                "current",
                ControllerKind.Wait,
                AnalysisKind.Suppression,
                FrontlineLabsQualificationDefinition
                    .CreateSuppressionChokeProbe),
            new(
                FrontlineLabsQualificationDefinition.EntryProbeId,
                "current-map-pressure-entry",
                "current",
                ControllerKind.Pressure,
                AnalysisKind.Entry,
                FrontlineLabsQualificationDefinition
                    .CreatePositionalEntryProbe),
            new(
                FrontlineLabsQualificationDefinition
                    .PredictionChamberProbeId,
                "objective-preserving-response",
                "current",
                ControllerKind.OneShot,
                AnalysisKind.Prediction,
                FrontlineLabsQualificationDefinition
                    .CreatePredictionChamberProbe),
            new(
                FrontlineLabsQualificationDefinition.FrontRotationProbeId,
                "advance-after-capture",
                "current",
                ControllerKind.Wait,
                AnalysisKind.Rotation,
                FrontlineLabsQualificationDefinition
                    .CreateFrontRotationProbe),
            new(
                FrontlineLabsQualificationDefinition.MapHoldoutProbeId,
                "thin-fronts-pressure-entry",
                "thin-fronts",
                ControllerKind.Pressure,
                AnalysisKind.MapHoldout,
                FrontlineLabsQualificationDefinition
                    .CreateMapHoldoutProbe),
            // The mind-native T4 case: the escorted channel, which is the
            // set-piece the profile exists to make authorable and the one the
            // last per-life cohort measured as the intended play its authors
            // could barely execute.
            .. mindProfile
                ? new CasePlan[]
                {
                    new(
                        FrontlineLabsQualificationDefinition
                            .EscortIntegrityProbeId,
                        "stationary-claim-with-escort",
                        "current",
                        ControllerKind.Wait,
                        AnalysisKind.EscortIntegrity,
                        FrontlineLabsQualificationDefinition
                            .CreateEscortIntegrityProbe),
                }
                : [],
        ];

        string? artifactName = null;
        string? artifactHash = null;
        string? actualRuntimeKind = null;
        var cases = new List<(CasePlan Plan, CaseEvidence Evidence)>();
        foreach (CasePlan plan in plans)
        {
            foreach (int botTeamId in new[] { 0, 1 })
            {
                ActorResolvedMatchDefinition definition =
                    plan.Definition(botTeamId);
                if (mindProfile)
                {
                    definition = definition.OnProfile(
                        ActorMatchCapabilityVersions.Mind);
                }
                int botParticipantId = definition.Topology.Participants
                    .Single(participant =>
                        participant.TeamId == botTeamId)
                    .ParticipantId;
                RunEvidence run = Execute(
                    plan,
                    definition,
                    botSpec,
                    runtimeKind,
                    seed,
                    botTeamId,
                    botParticipantId,
                    outputDirectory,
                    quiet: artifactName is not null,
                    mindProfile,
                    withViewer,
                    ref artifactName,
                    ref artifactHash,
                    ref actualRuntimeKind);
                cases.Add(
                    (
                        plan,
                        new CaseEvidence(
                            plan.VariantId,
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
                            ActorContractFingerprint.ComputeMatch(
                                definition),
                            QualificationControllerHost.Fingerprint(
                                ControllerFingerprint(plan.Controller),
                                mindProfile),
                            AnalyzerFingerprint,
                            run,
                            run.Passed,
                            Expectation(plan.Analysis),
                            FrontlineLabsQualificationScenario.Resolve(
                                definition,
                                plan.ProbeId,
                                plan.VariantId,
                                plan.MapArm,
                                ControllerRole(plan.Controller),
                                botTeamId),
                            run.FailedCriteria)
                    ));
            }
        }

        string resolvedArtifactName = artifactName
            ?? throw new InvalidOperationException(
                "T4 qualification produced no artifact identity.");
        string resolvedArtifactHash = artifactHash
            ?? throw new InvalidOperationException(
                "T4 qualification produced no artifact hash.");
        string resolvedRuntimeKind = actualRuntimeKind
            ?? throw new InvalidOperationException(
                "T4 qualification produced no runtime identity.");
        using (JsonDocument prerequisiteDocument = JsonDocument.Parse(
                   File.ReadAllText(prerequisiteReportPath)))
        {
            JsonElement root = prerequisiteDocument.RootElement;
            if (
                root.GetProperty("artifactName").GetString()
                    != resolvedArtifactName
                || root.GetProperty("artifactHash").GetString()
                    != resolvedArtifactHash
                || root.GetProperty("runtimeKind").GetString()
                    != resolvedRuntimeKind)
            {
                throw new InvalidOperationException(
                    "Qualification artifact identity changed between the " +
                    "T3 prerequisite and T4 probes.");
            }
        }

        ProbeEvidence[] probes =
        [
            .. cases
                .GroupBy(item => item.Plan.ProbeId, StringComparer.Ordinal)
                .Select(group =>
                {
                    CaseEvidence[] evidence =
                    [
                        .. group.Select(item => item.Evidence),
                    ];
                    return new ProbeEvidence(
                        group.Key,
                        "T4",
                        evidence.All(item => item.Passed),
                        evidence);
                })
                .OrderBy(probe => ProbeOrder(probe.ProbeId)),
        ];
        bool t4Passed =
            prerequisite.Passed
            && probes.All(probe => probe.Passed);
        string? tierAwarded = t4Passed
            ? "T4"
            : prerequisite.TierAwarded;
        var fingerprintParts = new List<string>
        {
            suiteId,
            FrontlineLabsQualificationDefinition.PositionalSuiteVersion
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            profileId,
            prerequisite.QualificationContractFingerprint,
            PredicateFingerprint,
        };
        fingerprintParts.AddRange(
            probes.SelectMany(probe =>
                probe.Cases.Select(item =>
                    string.Join(
                        ":",
                        probe.ProbeId,
                        item.VariantId,
                        item.BotTeamId,
                        item.MatchFingerprint,
                        item.ControllerFingerprint,
                        item.AnalyzerFingerprint))));
        string qualificationFingerprint = Fingerprint(
            string.Join("\n", fingerprintParts));
        var report = new QualificationReport(
            SchemaVersion: 6,
            suiteId,
            FrontlineLabsQualificationDefinition.PositionalSuiteVersion,
            profileId,
            qualificationFingerprint,
            resolvedArtifactName,
            resolvedArtifactHash,
            resolvedRuntimeKind,
            seed,
            prerequisite,
            Passed: t4Passed,
            ProfileComplete: true,
            tierAwarded,
            // The C-axis folds into the tiers for a mind artifact (§6.2).
            CoordinationGradeAwarded: mindProfile ? "folded-into-tiers" : null,
            BalanceEvidenceEligible: t4Passed,
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

        Console.WriteLine($"Qualification suite: {report.SuiteId}");
        Console.WriteLine(
            $"Profile:             {report.QualificationProfileId}");
        Console.WriteLine(
            $"Artifact:            {resolvedArtifactName} " +
            $"[{resolvedArtifactHash[..12]}…]");
        Console.WriteLine(
            $"prerequisite T3       " +
            $"{(prerequisite.Passed ? "PASS" : "FAIL")}");
        foreach (ProbeEvidence probe in probes)
        {
            Console.WriteLine(
                $"{probe.ProbeId,-24} " +
                $"{(probe.Passed ? "PASS" : "FAIL")}");
        }
        Console.WriteLine($"Report:              {reportPath}");
        Console.WriteLine(
            $"Tier awarded:        {tierAwarded ?? "none"}");

        bool invalid = probes
            .SelectMany(probe => probe.Cases)
            .Select(item => item.Run)
            .Any(run =>
                !run.ContractValid
                || !run.BotEligible
                || run.RuntimeFaultCount != 0
                || run.Disqualified
                || !run.ProbeControllerValid
                || run.FaultedTurnCount != 0);
        return invalid ? 2 : t4Passed ? 0 : 3;
    }

    private static RunEvidence Execute(
        CasePlan plan,
        ActorResolvedMatchDefinition definition,
        string botSpec,
        string runtimeKind,
        ulong seed,
        int botTeamId,
        int botParticipantId,
        string outputDirectory,
        bool quiet,
        bool mindProfile,
        bool withViewer,
        ref string? artifactName,
        ref string? artifactHash,
        ref string? actualRuntimeKind)
    {
        using ResolvedLabsEntrant bot =
            ResolvedLabsEntrant.Resolve(
                botSpec,
                runtimeKind,
                mindProfile,
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
        using QualificationControllerHost controllerHost =
            QualificationControllerHost.Create(
                ControllerName(plan.Controller),
                () => Controller(plan.Controller),
                mindProfile);
        GenericActorParticipantConfiguration botParticipant =
            bot.ToParticipant(botParticipantId, botTeamId);
        GenericActorParticipantConfiguration controllerParticipant =
            controllerHost.ToParticipant(
                controllerParticipantId,
                controllerTeamId,
                ControllerName(plan.Controller),
                QualificationControllerHost.Fingerprint(
                    ControllerFingerprint(plan.Controller),
                    mindProfile));
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
            plan.ProbeId,
            plan.VariantId,
            $"bot-team-{botTeamId}");
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
        int initialActiveIndex = root
            .GetProperty("initialFrame")
            .GetProperty("state")
            .GetProperty("mode")
            .GetProperty("activePositionIndex")
            .GetInt32();

        int botTurnCount = 0;
        int faultedTurnCount = 0;
        string? firstAcceptedActionId = null;
        int? initialFirstLifeHealth = null;
        int? healthAtFirstLifeObjective = null;
        int? firstLifeObjectiveTick = null;
        int threatTurnCount = 0;
        var threatMoveTicks = new HashSet<int>();
        // Escort integrity needs where every own body stood, tick by tick:
        // one still on the point, another beside it. Both halves are a fact
        // about the ARMY rather than about any body, which is why the per-life
        // suites never had to collect it.
        var ownPositionsByTick = new SortedDictionary<int, List<(int UnitId,
            Position Position)>>();
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            // Profile-neutral: a per-life document yields one entry per body
            // turn, a mind document yields one per body RESOLUTION inside the
            // participant's single turn.
            foreach (ProbeTurn turn in ProbeReplay.Turns(tick)
                         .Where(turn =>
                             turn.ParticipantId == botParticipantId))
            {
                botTurnCount++;
                JsonElement resolution =
                    turn.ActionResolution;
                string? acceptedActionId = AcceptedActionId(resolution);
                bool success =
                    resolution.GetProperty("outcome").GetString()
                        == "success";
                firstAcceptedActionId ??= success
                    ? acceptedActionId
                    : null;
                if (
                    resolution.GetProperty("outcome").GetString()
                        == "faulted"
                    || resolution.GetProperty("runtimeFault").ValueKind
                        != JsonValueKind.Null)
                {
                    faultedTurnCount++;
                }

                JsonElement observation =
                    turn.Observation;
                JsonElement self = turn.Self;
                if (!ownPositionsByTick.TryGetValue(
                        turn.Tick,
                        out List<(int, Position)>? standing))
                {
                    standing = [];
                    ownPositionsByTick[turn.Tick] = standing;
                }
                standing.Add((
                    turn.UnitId,
                    ReadPosition(self.GetProperty("position"))));
                JsonElement actorId = self.GetProperty("actorId");
                int lifeId = actorId.GetProperty("lifeId").GetInt32();
                int health = self.GetProperty("health").GetInt32();
                if (lifeId == 0)
                    initialFirstLifeHealth ??= health;
                Position selfPosition = ReadPosition(
                    self.GetProperty("position"));
                Position[] objective = ActiveObjectiveTiles(
                    contract,
                    observation);
                if (
                    lifeId == 0
                    && objective.Contains(selfPosition)
                    && firstLifeObjectiveTick is null)
                {
                    firstLifeObjectiveTick =
                        turn.Tick;
                    healthAtFirstLifeObjective = health;
                }

                bool threatened = observation
                    .GetProperty("visibleProjectiles")
                    .EnumerateArray()
                    .Where(projectile =>
                        projectile.GetProperty("ownerTeamId").GetInt32()
                            != botTeamId)
                    .Any(projectile =>
                        ProjectileRayDistance(
                            projectile,
                            selfPosition,
                            out int distance)
                        && distance <= Math.Min(
                            projectile.GetProperty("remainingTiles")
                                .GetInt32(),
                            projectile.GetProperty("tilesPerAdvance")
                                .GetInt32() * 2));
                if (!threatened)
                    continue;
                threatTurnCount++;
                if (
                    success
                    && acceptedActionId is not null
                    && movementActions.Contains(acceptedActionId))
                {
                    threatMoveTicks.Add(
                        turn.Tick);
                }
            }
        }

        int straightAttackCount = 0;
        int curvedAttackCount = 0;
        int controllerAttackCount = 0;
        foreach (JsonElement attackEvent in Events(root, "attack"))
        {
            JsonElement payload = attackEvent.GetProperty("payload");
            int sourceTeam = payload.GetProperty("actorId")
                .GetProperty("teamId")
                .GetInt32();
            if (sourceTeam != botTeamId)
            {
                controllerAttackCount++;
                continue;
            }
            if (IsCurved(payload.GetProperty("action")))
                curvedAttackCount++;
            else
                straightAttackCount++;
        }

        int damageDealt = 0;
        int damageTaken = 0;
        foreach (JsonElement damage in Events(root, "damage"))
        {
            JsonElement payload = damage.GetProperty("payload");
            int amount = payload.GetProperty("amount").GetInt32();
            if (payload.GetProperty("sourceTeamId").GetInt32()
                == botTeamId)
            {
                damageDealt += amount;
            }
            if (
                payload.GetProperty("targetActorId")
                    .GetProperty("teamId")
                    .GetInt32() == botTeamId)
            {
                damageTaken += amount;
            }
        }

        int threatMoveIntoObjectiveCount = 0;
        int? firstAdvancedIndex = null;
        int? firstAdvancedTick = null;
        int? firstAdvancedEntryTick = null;
        int advancedResidence = 0;
        int maxAdvancedResidence = 0;
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            int tickNumber = tick.GetProperty("tick").GetInt32();
            JsonElement state = tick.GetProperty("postState");
            int activeIndex = state.GetProperty("mode")
                .GetProperty("activePositionIndex")
                .GetInt32();
            if (activeIndex != initialActiveIndex
                && firstAdvancedIndex is null)
            {
                firstAdvancedIndex = activeIndex;
                firstAdvancedTick = tickNumber;
            }
            Position[] objective = ActiveObjectiveTiles(contract, state);
            JsonElement? testedLife = FindTestedLife(state, botTeamId);
            bool occupies = testedLife is JsonElement life
                && objective.Contains(
                    ReadPosition(life.GetProperty("position")));
            if (threatMoveTicks.Contains(tickNumber) && occupies)
                threatMoveIntoObjectiveCount++;
            if (firstAdvancedIndex is not null
                && activeIndex == firstAdvancedIndex)
            {
                if (occupies)
                {
                    firstAdvancedEntryTick ??= tickNumber;
                    advancedResidence++;
                    maxAdvancedResidence = Math.Max(
                        maxAdvancedResidence,
                        advancedResidence);
                }
                else
                {
                    advancedResidence = 0;
                }
            }
        }

        int maxEscortTicks = MaxConsecutiveEscortTicks(
            root,
            contract,
            ownPositionsByTick);
        int maxCaptureTicks = MaxConsecutiveCaptureTicks(
            root,
            contract,
            botTeamId);
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
        int? damageBeforeEntry =
            initialFirstLifeHealth is int initialHealth
            && healthAtFirstLifeObjective is int entryHealth
                ? initialHealth - entryHealth
                : null;
        var criteria = new List<(string Name, bool Satisfied)>
        {
            ("replay-verifies-against-its-own-hash", contractValid),
            ("tested-team-eligible-at-match-end", botEligible),
            ("no-runtime-faults", runtimeFaultCount == 0),
            ("not-disqualified", !disqualified),
            ("probe-controller-ran-without-fault", controllerValid),
            ("tested-artifact-took-at-least-one-turn", botTurnCount > 0),
            ("no-faulted-turns", faultedTurnCount == 0),
        };
        switch (analysis)
        {
            case AnalysisKind.Suppression:
                criteria.Add((
                    "first-accepted-action-was-an-attack",
                    firstAcceptedActionId is not null
                    && ActionIds(contract, "attack")
                        .Contains(firstAcceptedActionId)));
                criteria.Add((
                    "fired-at-least-one-straight-shot",
                    straightAttackCount > 0));
                criteria.Add((
                    "fired-no-curved-shot",
                    curvedAttackCount == 0));
                criteria.Add((
                    "dealt-at-least-one-damage",
                    damageDealt > 0));
                break;
            case AnalysisKind.Entry:
            case AnalysisKind.MapHoldout:
                criteria.Add((
                    "controller-applied-straight-pressure",
                    controllerAttackCount > 0));
                criteria.Add((
                    "first-life-reached-the-active-objective",
                    firstLifeObjectiveTick is not null));
                criteria.Add((
                    "entered-having-taken-at-most-one-hit",
                    damageBeforeEntry is >= 0 and <= 1));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxCaptureTicks >= 5));
                break;
            case AnalysisKind.EscortIntegrity:
                criteria.Add((
                    "kept-a-body-still-on-the-point-with-another-beside-it-"
                    + $"for-{FrontlineLabsQualificationDefinition
                        .EscortIntegrityTicks}-consecutive-ticks",
                    maxEscortTicks
                        >= FrontlineLabsQualificationDefinition
                            .EscortIntegrityTicks));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxCaptureTicks
                        >= FrontlineLabsQualificationDefinition
                            .EscortIntegrityTicks));
                criteria.Add(("took-no-damage", damageTaken == 0));
                break;
            case AnalysisKind.Prediction:
                criteria.Add((
                    "controller-committed-its-one-shot",
                    controllerAttackCount > 0));
                criteria.Add((
                    "was-really-threatened-at-least-once",
                    threatTurnCount > 0));
                criteria.Add((
                    "answered-a-threat-with-a-move-that-stayed-on-the-"
                    + "active-objective",
                    threatMoveIntoObjectiveCount > 0));
                criteria.Add(("took-no-damage", damageTaken == 0));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxCaptureTicks >= 3));
                break;
            case AnalysisKind.Rotation:
                criteria.Add((
                    "captured-so-the-front-advanced-one-step-in-the-tested-"
                    + "team-direction",
                    firstAdvancedIndex
                        == initialActiveIndex + (botTeamId == 0 ? 1 : -1)
                    && firstAdvancedTick is not null));
                criteria.Add((
                    "entered-the-newly-active-objective",
                    firstAdvancedEntryTick is not null));
                criteria.Add((
                    "entered-it-after-the-rotation-rather-than-already-"
                    + "standing-there",
                    firstAdvancedEntryTick > firstAdvancedTick));
                criteria.Add((
                    "held-residence-on-the-new-objective-for-an-"
                    + "uninterrupted-run",
                    maxAdvancedResidence >= 4));
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
            botTurnCount,
            faultedTurnCount,
            firstAcceptedActionId,
            straightAttackCount,
            curvedAttackCount,
            damageDealt,
            damageTaken,
            controllerAttackCount,
            threatTurnCount,
            threatMoveIntoObjectiveCount,
            firstLifeObjectiveTick,
            damageBeforeEntry,
            maxCaptureTicks,
            initialActiveIndex,
            firstAdvancedIndex,
            firstAdvancedTick,
            firstAdvancedEntryTick,
            maxAdvancedResidence,
            replayHash,
            replayPath,
            passed,
            failedCriteria);
    }

    /// <summary>
    /// One plain-language line per case stating the shape of a passing run,
    /// derived from the same clauses the analyzer evaluates.
    /// </summary>
    private static string Expectation(AnalysisKind analysis) =>
        analysis switch
        {
            AnalysisKind.EscortIntegrity =>
                "Run the escorted channel: keep one body STILL on the active "
                + "objective with another of your bodies adjacent to it for "
                + $"{FrontlineLabsQualificationDefinition
                    .EscortIntegrityTicks} consecutive ticks, holding sole "
                + "control across that run and taking no damage. Under the "
                + "channel the claim is built by bodies that did not change "
                + "tile, and the screen is what makes standing still "
                + "survivable.",
            AnalysisKind.Suppression =>
                "Suppress from the ground you already hold: your first "
                + "accepted action must be an attack, all of your fire must "
                + "be straight (no curved shot at all, because every bend "
                + "here is consumed by the approach walls), and it must "
                + "deal damage.",
            AnalysisKind.Entry =>
                "Cross under fire and hold: with the pressure controller "
                + "shooting down the approach, get your first life onto the "
                + "active objective having taken at most one hit on the way "
                + "in, then keep sole capture control uninterrupted. "
                + "Waiting for a perfectly safe moment fails, and so does "
                + "rushing in damaged.",
            AnalysisKind.Prediction =>
                "Answer the real shot without conceding the ground: while a "
                + "projectile that can actually reach you is on your line, "
                + "make at least one successful move whose resulting "
                + "position is still on the active objective, take no "
                + "damage, and keep sole capture control uninterrupted.",
            AnalysisKind.Rotation =>
                "Rotate with the front: finish the short declared capture "
                + "so the active position advances one step in your team's "
                + "advance direction, then enter that newly active "
                + "objective after it becomes active (standing on it "
                + "beforehand does not count) and hold residence there for "
                + "an uninterrupted run of ticks.",
            AnalysisKind.MapHoldout =>
                "The same cross-under-fire-and-hold task on the held-out "
                + "thin-fronts objective topology: reach the active "
                + "objective with your first life having taken at most one "
                + "hit on the way in while the pressure controller shoots, "
                + "then keep sole capture control uninterrupted. The "
                + "resolved map and objective tiles differ from "
                + "entry-initiative, so coordinate constants do not carry "
                + "over.",
            _ => "Unknown probe analysis.",
        };

    private static PrerequisiteEvidence ReadPrerequisite(
        string reportPath,
        string outputDirectory,
        bool mindProfile)
    {
        byte[] bytes = File.ReadAllBytes(reportPath);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string suiteId = root.GetProperty("suiteId").GetString()!;
        int suiteVersion = root.GetProperty("suiteVersion").GetInt32();
        string profileId =
            root.GetProperty("qualificationProfileId").GetString()!;
        if (
            suiteId
                != (mindProfile
                    ? FrontlineLabsQualificationDefinition.MindTacticalSuiteId
                    : FrontlineLabsQualificationDefinition.TacticalSuiteId)
            || suiteVersion
                != FrontlineLabsQualificationDefinition
                    .TacticalSuiteVersion
            || profileId
                != (mindProfile
                    ? FrontlineLabsQualificationDefinition
                        .MindTacticalProfileId
                    : FrontlineLabsQualificationDefinition.TacticalProfileId))
        {
            throw new InvalidOperationException(
                "T4 qualification requires the exact immutable cumulative " +
                "T3 prerequisite profile.");
        }
        return new PrerequisiteEvidence(
            suiteId,
            suiteVersion,
            profileId,
            root.GetProperty("qualificationContractFingerprint")
                .GetString()!,
            Path.GetRelativePath(outputDirectory, reportPath),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            root.GetProperty("passed").GetBoolean(),
            root.GetProperty("tierAwarded").ValueKind
                == JsonValueKind.String
                ? root.GetProperty("tierAwarded").GetString()
                : null);
    }

    private static IEnumerable<JsonElement> Events(
        JsonElement root,
        string kind) =>
        root.GetProperty("ticks")
            .EnumerateArray()
            .SelectMany(tick =>
                tick.GetProperty("events").EnumerateArray())
            .Where(item =>
                item.GetProperty("kind").GetString() == kind);

    /// <summary>
    /// The longest run of consecutive ticks on which one own body stood STILL
    /// on an active objective tile while another own body stood adjacent to
    /// it.
    ///
    /// <para>Stillness is positional, exactly as the capture channel defines
    /// it: a body that did not change tile since the previous tick was still,
    /// whatever it asked for and whatever it was doing. Adjacency is
    /// Chebyshev-1, which is where a screen has to be to be between the
    /// channeler and anything.</para>
    /// </summary>
    private static int MaxConsecutiveEscortTicks(
        JsonElement root,
        JsonElement contract,
        IReadOnlyDictionary<int, List<(int UnitId, Position Position)>>
            ownPositionsByTick)
    {
        _ = root;
        var previous = new Dictionary<int, Position>();
        int consecutive = 0;
        int maximum = 0;
        foreach ((int tick, List<(int UnitId, Position Position)> standing)
                 in ownPositionsByTick.OrderBy(entry => entry.Key))
        {
            Position[] objective = ActiveObjectiveTiles(
                contract,
                root.GetProperty("ticks")
                    .EnumerateArray()
                    .First(frame =>
                        frame.GetProperty("tick").GetInt32() == tick)
                    .GetProperty("postState"));
            bool escorted = standing.Any(holder =>
                objective.Contains(holder.Position)
                && previous.TryGetValue(
                    holder.UnitId,
                    out Position before)
                && before == holder.Position
                && standing.Any(screen =>
                    screen.UnitId != holder.UnitId
                    && screen.Position.ChebyshevDistance(holder.Position)
                        == 1));
            consecutive = escorted ? consecutive + 1 : 0;
            maximum = Math.Max(maximum, consecutive);
            previous.Clear();
            foreach ((int unitId, Position position) in standing)
                previous[unitId] = position;
        }
        return maximum;
    }

    private static int MaxConsecutiveCaptureTicks(
        JsonElement root,
        JsonElement contract,
        int botTeamId)
    {
        int consecutive = 0;
        int maximum = 0;
        foreach (JsonElement tick in root.GetProperty("ticks")
                     .EnumerateArray())
        {
            JsonElement state = tick.GetProperty("postState");
            Position[] objective = ActiveObjectiveTiles(contract, state);
            JsonElement? testedLife = FindTestedLife(state, botTeamId);
            bool occupies = testedLife is JsonElement life
                && objective.Contains(
                    ReadPosition(life.GetProperty("position")));
            JsonElement mode = state.GetProperty("mode");
            bool contributes = occupies
                && mode.GetProperty("claimingTeamId").ValueKind
                    == JsonValueKind.Number
                && mode.GetProperty("claimingTeamId").GetInt32()
                    == botTeamId;
            consecutive = contributes ? consecutive + 1 : 0;
            maximum = Math.Max(maximum, consecutive);
        }
        return maximum;
    }

    private static JsonElement? FindTestedLife(
        JsonElement state,
        int botTeamId) =>
        state.GetProperty("activeLives")
            .EnumerateArray()
            .Cast<JsonElement?>()
            .SingleOrDefault(life =>
                life!.Value.GetProperty("actorId")
                    .GetProperty("teamId")
                    .GetInt32() == botTeamId
                && life.Value.GetProperty("actorId")
                    .GetProperty("unitId")
                    .GetInt32() == 0);

    private static Position[] ActiveObjectiveTiles(
        JsonElement contract,
        JsonElement state)
    {
        int activeIndex = state.GetProperty("mode")
            .GetProperty("activePositionIndex")
            .GetInt32();
        JsonElement ids = contract.GetProperty("modeMapBinding")
            .GetProperty("orderedObjectiveRegionIds");
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

    private static bool IsCurved(JsonElement action) =>
        action.GetProperty("arguments")
            .EnumerateArray()
            .Where(argument =>
                argument.GetProperty("kind").GetString()
                    == "shot-program")
            .Select(argument => argument.GetProperty("value"))
            .Any(value =>
                value.GetProperty("bendCount").GetInt32() > 0);

    private static bool ProjectileRayDistance(
        JsonElement projectile,
        Position target,
        out int distance)
    {
        (int dx, int dy) = projectile.GetProperty("heading")
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
        Position source = ReadPosition(
            projectile.GetProperty("position"));
        int deltaX = target.X - source.X;
        int deltaY = target.Y - source.Y;
        distance = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        return distance > 0
            && Math.Sign(deltaX) == dx
            && Math.Sign(deltaY) == dy
            && (
                dx == 0 && deltaX == 0
                || dy == 0 && deltaY == 0
                || dx != 0
                && dy != 0
                && Math.Abs(deltaX) == Math.Abs(deltaY)
            );
    }

    private static Position ReadPosition(JsonElement value) =>
        new(
            value.GetProperty("x").GetInt32(),
            value.GetProperty("y").GetInt32());

    private static Sdk.IGenericActorBot Controller(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait =>
                new FrontlineLabsQualificationWaitController(),
            ControllerKind.OneShot =>
                new FrontlineLabsQualificationOneShotController(),
            ControllerKind.Pressure =>
                new FrontlineLabsQualificationSentinel(),
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static string ControllerName(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => "Qualification Passive Controller",
            ControllerKind.OneShot => "Qualification One-Shot Controller",
            ControllerKind.Pressure => "Qualification Pressure Controller",
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static string ControllerFingerprint(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => WaitControllerFingerprint,
            ControllerKind.OneShot => OneShotControllerFingerprint,
            ControllerKind.Pressure => PressureControllerFingerprint,
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static string ControllerRole(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => "passive-wait-controller",
            ControllerKind.OneShot => "one-shot-straight-controller",
            ControllerKind.Pressure => "repeated-straight-pressure-controller",
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static int ProbeOrder(string probeId) =>
        probeId switch
        {
            FrontlineLabsQualificationDefinition
                .SuppressionChokeProbeId => 0,
            FrontlineLabsQualificationDefinition.EntryProbeId => 1,
            FrontlineLabsQualificationDefinition
                .PredictionChamberProbeId => 2,
            FrontlineLabsQualificationDefinition.FrontRotationProbeId => 3,
            FrontlineLabsQualificationDefinition.MapHoldoutProbeId => 4,
            _ => 100,
        };

    private static string Fingerprint(string value) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
