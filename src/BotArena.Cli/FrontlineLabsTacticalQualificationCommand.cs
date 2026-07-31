using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli;

/// <summary>
/// Immutable cumulative T3 tactical-geometry profile. It first reruns the
/// exact cumulative T2 prerequisite, then executes bounded public-contract
/// geometry, cadence, tempo, and local-commitment probes.
/// </summary>
internal static class FrontlineLabsTacticalQualificationCommand
{
    private const string WaitControllerFingerprint =
        "frontline-qualification-wait-controller-v1";
    private const string OneShotControllerFingerprint =
        "frontline-qualification-one-shot-controller-v1";
    private const string AnalyzerFingerprint =
        "frontline-qualification-4-t3-analyzer-v1";
    private const string PredicateFingerprint =
        "frontline-qualification-4-t3-predicates-v1";

    /// <summary>
    /// Every T3 case derives from the default duel-depth map arm; the
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
        CurvedIntercept,
        StrictCorner,
        CadenceHarmless,
        CadenceThreatening,
        CooldownWindow,
        LocalFormSafety,
    }

    private sealed record CasePlan(
        string ProbeId,
        string VariantId,
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
        int CurvedAttackCount,
        int CurvedProjectileHitCount,
        int CurvedDamageDealt,
        int ApparentThreatTurnCount,
        int RealThreatTurnCount,
        int SuccessfulApparentThreatMoveCount,
        int SuccessfulRealThreatMoveCount,
        int DamageTaken,
        int ControllerAttackCount,
        int? ObjectiveDistanceAtControllerAttack,
        int? MinimumObjectiveDistanceDuringCooldown,
        int DamageDealtDuringCooldown,
        int UnsafeCommitmentCount,
        int MaxConsecutiveCaptureTicks,
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
        bool printSummary = true,
        bool mindProfile = false,
        bool withViewer = false)
    {
        string suiteId = mindProfile
            ? FrontlineLabsQualificationDefinition.MindTacticalSuiteId
            : FrontlineLabsQualificationDefinition.TacticalSuiteId;
        string profileId = mindProfile
            ? FrontlineLabsQualificationDefinition.MindTacticalProfileId
            : FrontlineLabsQualificationDefinition.TacticalProfileId;
        if (runtimeKind != "wasm")
        {
            throw new InvalidOperationException(
                $"{suiteId} requires the canonical WASM runtime.");
        }

        string prerequisiteDirectory = Path.Combine(
            outputDirectory,
            "prerequisite-t2");
        int prerequisiteExit =
            FrontlineLabsFundamentalsQualificationCommand.Run(
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
                "T3 qualification stopped: cumulative T2 evidence was " +
                "runtime/contract invalid.");
            Console.WriteLine(
                $"Prerequisite report: {prerequisiteReportPath}");
            return 2;
        }

        CasePlan[] plans =
        [
            new(
                FrontlineLabsQualificationDefinition
                    .WallTerminatedBendProbeId,
                "off-axis-visible-target",
                ControllerKind.Wait,
                AnalysisKind.CurvedIntercept,
                FrontlineLabsQualificationDefinition
                    .CreateWallTerminatedBendProbe),
            new(
                FrontlineLabsQualificationDefinition.StrictCornerProbeId,
                "strict-corner-invalid-intercept",
                ControllerKind.Wait,
                AnalysisKind.StrictCorner,
                FrontlineLabsQualificationDefinition
                    .CreateStrictCornerProbe),
            new(
                FrontlineLabsQualificationDefinition.CadenceParityProbeId,
                "range-3-harmless",
                ControllerKind.OneShot,
                AnalysisKind.CadenceHarmless,
                teamId => FrontlineLabsQualificationDefinition
                    .CreateCadenceParityProbe(teamId, projectileRange: 3)),
            new(
                FrontlineLabsQualificationDefinition.CadenceParityProbeId,
                "range-4-threatening",
                ControllerKind.OneShot,
                AnalysisKind.CadenceThreatening,
                teamId => FrontlineLabsQualificationDefinition
                    .CreateCadenceParityProbe(teamId, projectileRange: 4)),
            new(
                FrontlineLabsQualificationDefinition.CooldownWindowProbeId,
                "declared-mobile-cooldown",
                ControllerKind.OneShot,
                AnalysisKind.CooldownWindow,
                FrontlineLabsQualificationDefinition
                    .CreateCooldownWindowProbe),
            new(
                FrontlineLabsQualificationDefinition.LocalFormSafetyProbeId,
                "objective-weight-zero-transform",
                ControllerKind.Wait,
                AnalysisKind.LocalFormSafety,
                FrontlineLabsQualificationDefinition
                    .CreateLocalFormSafetyProbe),
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
                                MapArm,
                                ControllerRole(plan.Controller),
                                botTeamId),
                            run.FailedCriteria)
                    ));
            }
        }

        string resolvedArtifactName = artifactName
            ?? throw new InvalidOperationException(
                "T3 qualification produced no artifact identity.");
        string resolvedArtifactHash = artifactHash
            ?? throw new InvalidOperationException(
                "T3 qualification produced no artifact hash.");
        string resolvedRuntimeKind = actualRuntimeKind
            ?? throw new InvalidOperationException(
                "T3 qualification produced no runtime identity.");
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
                    "T2 prerequisite and T3 probes.");
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
                        "T3",
                        evidence.All(item => item.Passed),
                        evidence);
                })
                .OrderBy(probe =>
                    ProbeOrder(probe.ProbeId)),
        ];
        bool t3Passed =
            prerequisite.Passed
            && probes.All(probe => probe.Passed);
        string? tierAwarded = t3Passed
            ? "T3"
            : prerequisite.TierAwarded;
        var fingerprintParts = new List<string>
        {
            FrontlineLabsQualificationDefinition.TacticalSuiteId,
            FrontlineLabsQualificationDefinition.TacticalSuiteVersion
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            FrontlineLabsQualificationDefinition.TacticalProfileId,
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
            SchemaVersion: 5,
            suiteId,
            FrontlineLabsQualificationDefinition.TacticalSuiteVersion,
            profileId,
            qualificationFingerprint,
            resolvedArtifactName,
            resolvedArtifactHash,
            resolvedRuntimeKind,
            seed,
            prerequisite,
            Passed: t3Passed,
            ProfileComplete: true,
            tierAwarded,
            // The C-axis folds into the tiers for a mind artifact (§6.2).
            CoordinationGradeAwarded: mindProfile ? "folded-into-tiers" : null,
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
            Console.WriteLine(
                $"prerequisite T2       " +
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
        }

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
        return invalid ? 2 : t3Passed ? 0 : 3;
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
            runDirectory,
            themeId: null,
            withViewer);
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
        HashSet<string> attackActions = ActionIds(
            contract,
            "attack");
        HashSet<string> commitmentActions = ActionIds(
            contract,
            "same-life-transition");
        Position[] objective = ActiveObjectiveTiles(
            contract,
            root.GetProperty("initialFrame").GetProperty("state"));

        int botTurnCount = 0;
        int faultedTurnCount = 0;
        int apparentThreatTurnCount = 0;
        int realThreatTurnCount = 0;
        int apparentThreatMoves = 0;
        int realThreatMoves = 0;
        int unsafeCommitmentCount = 0;
        var objectiveDistances = new Dictionary<int, int>();
        var postObjectiveDistances = new Dictionary<int, int>();
        int? controllerAttackTick = null;
        int? controllerCooldownTicks = null;
        foreach (JsonElement tick in root
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            // Profile-neutral: a per-life document yields one entry per body
            // turn, a mind document yields one per body RESOLUTION inside the
            // participant's single turn. The probe measures behaviour, and
            // behaviour is the same question on both.
            foreach (ProbeTurn turn in ProbeReplay.Turns(tick))
            {
                JsonElement resolution =
                    turn.ActionResolution;
                string? acceptedActionId = AcceptedActionId(resolution);
                bool success =
                    resolution.GetProperty("outcome").GetString()
                    == "success";
                int participantId =
                    turn.ParticipantId;
                if (participantId != botParticipantId)
                {
                    if (
                        controllerAttackTick is null
                        && success
                        && acceptedActionId is not null
                        && attackActions.Contains(acceptedActionId))
                    {
                        controllerAttackTick =
                            turn.Tick;
                        string formId = turn.Self
                            .GetProperty("formId")
                            .GetString()!;
                        controllerCooldownTicks =
                            CooldownForForm(contract, formId);
                    }
                    continue;
                }

                botTurnCount++;
                if (
                    resolution.GetProperty("outcome").GetString()
                        == "faulted"
                    || resolution.GetProperty("runtimeFault").ValueKind
                        != JsonValueKind.Null)
                {
                    faultedTurnCount++;
                }
                if (success
                    && acceptedActionId is not null
                    && commitmentActions.Contains(acceptedActionId))
                {
                    unsafeCommitmentCount++;
                }

                JsonElement observation =
                    turn.Observation;
                Position self = ReadPosition(
                    turn.Self
                        .GetProperty("position"));
                int tickNumber = turn.Tick;
                objectiveDistances[tickNumber] =
                    objective.Length == 0
                        ? 0
                        : objective.Min(self.ChebyshevDistance);
                bool apparent = false;
                bool real = false;
                foreach (JsonElement projectile in observation
                             .GetProperty("visibleProjectiles")
                             .EnumerateArray()
                             .Where(projectile =>
                                 projectile
                                     .GetProperty("ownerTeamId")
                                     .GetInt32() != botTeamId))
                {
                    if (!ProjectileRayDistance(
                            projectile,
                            self,
                            out int distance))
                    {
                        continue;
                    }
                    int twoAdvances = projectile
                        .GetProperty("tilesPerAdvance")
                        .GetInt32() * 2;
                    apparent |= distance <= twoAdvances;
                    real |= distance <= Math.Min(
                        twoAdvances,
                        projectile.GetProperty("remainingTiles")
                            .GetInt32());
                }
                bool successfulMove =
                    success
                    && acceptedActionId is not null
                    && movementActions.Contains(acceptedActionId);
                if (apparent)
                {
                    apparentThreatTurnCount++;
                    if (successfulMove)
                        apparentThreatMoves++;
                }
                if (real)
                {
                    realThreatTurnCount++;
                    if (successfulMove)
                        realThreatMoves++;
                }
            }

            JsonElement postState = tick.GetProperty("postState");
            JsonElement? testedLife = postState
                .GetProperty("activeLives")
                .EnumerateArray()
                .Cast<JsonElement?>()
                .SingleOrDefault(life =>
                    life!.Value.GetProperty("actorId")
                        .GetProperty("teamId")
                        .GetInt32() == botTeamId
                    && life.Value.GetProperty("actorId")
                        .GetProperty("unitId")
                        .GetInt32() == 0);
            if (testedLife is JsonElement life)
            {
                Position[] postObjective = ActiveObjectiveTiles(
                    contract,
                    postState);
                Position postPosition = ReadPosition(
                    life.GetProperty("position"));
                postObjectiveDistances[
                    tick.GetProperty("tick").GetInt32()] =
                    postObjective.Length == 0
                        ? 0
                        : postObjective.Min(
                            postPosition.ChebyshevDistance);
            }
        }

        var curvedProjectileIds = new HashSet<string>(
            StringComparer.Ordinal);
        int curvedAttackCount = 0;
        int controllerAttackCount = 0;
        foreach (JsonElement attackEvent in root
                     .GetProperty("ticks")
                     .EnumerateArray()
                     .SelectMany(tick =>
                         tick.GetProperty("events").EnumerateArray())
                     .Where(item =>
                         item.GetProperty("kind").GetString()
                            == "attack"))
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
            if (!IsCurved(payload.GetProperty("action")))
                continue;
            curvedAttackCount++;
            curvedProjectileIds.Add(
                payload.GetProperty("projectileId").GetString()!);
        }

        int curvedDamageDealt = 0;
        var curvedProjectileHits = new HashSet<string>(
            StringComparer.Ordinal);
        int damageTaken = 0;
        int cooldownDamage = 0;
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
            if (
                payload.GetProperty("sourceTeamId").GetInt32()
                    == botTeamId)
            {
                string projectileId =
                    payload.GetProperty("projectileId").GetString()!;
                if (curvedProjectileIds.Contains(projectileId))
                {
                    curvedDamageDealt += amount;
                    curvedProjectileHits.Add(projectileId);
                }
                int eventTick = damage.GetProperty("tick").GetInt32();
                if (
                    controllerAttackTick is int attackTick
                    && controllerCooldownTicks is int cooldown
                    && eventTick > attackTick
                    && eventTick <= attackTick + cooldown)
                {
                    cooldownDamage += amount;
                }
            }
            if (
                payload.GetProperty("targetActorId")
                    .GetProperty("teamId")
                    .GetInt32() == botTeamId)
            {
                damageTaken += amount;
            }
        }

        int? distanceAtAttack =
            controllerAttackTick is int shotTick
                ? objectiveDistances.GetValueOrDefault(shotTick)
                : null;
        int? minimumCooldownDistance =
            controllerAttackTick is int attack
            && controllerCooldownTicks is int cooldownTicks
                ? postObjectiveDistances
                    .Where(pair =>
                        pair.Key >= attack
                        && pair.Key <= attack + cooldownTicks)
                    .Select(pair => (int?)pair.Value)
                    .DefaultIfEmpty()
                    .Min()
                : null;
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
            case AnalysisKind.CurvedIntercept:
                criteria.Add((
                    "fired-at-least-one-curved-shot",
                    curvedAttackCount > 0));
                criteria.Add((
                    "wasted-no-curved-shot-every-curve-fired-hit",
                    curvedProjectileHits.Count == curvedAttackCount));
                criteria.Add((
                    "curved-fire-dealt-damage",
                    curvedDamageDealt > 0));
                break;
            case AnalysisKind.StrictCorner:
                criteria.Add((
                    "fired-no-curved-shot",
                    curvedAttackCount == 0));
                criteria.Add(("took-no-damage", damageTaken == 0));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxCaptureTicks >= 3));
                break;
            case AnalysisKind.CadenceHarmless:
                criteria.Add((
                    "controller-committed-its-one-shot",
                    controllerAttackCount > 0));
                criteria.Add((
                    "stood-on-the-apparent-two-advance-line-of-that-shot",
                    apparentThreatTurnCount > 0));
                criteria.Add((
                    "never-entered-the-shot-declared-remaining-range",
                    realThreatTurnCount == 0));
                criteria.Add((
                    "made-no-evasive-move-while-the-threat-was-apparent-"
                    + "only",
                    apparentThreatMoves == 0));
                criteria.Add(("took-no-damage", damageTaken == 0));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxCaptureTicks >= 3));
                break;
            case AnalysisKind.CadenceThreatening:
                criteria.Add((
                    "controller-committed-its-one-shot",
                    controllerAttackCount > 0));
                criteria.Add((
                    "was-inside-the-shot-declared-remaining-range",
                    realThreatTurnCount > 0));
                criteria.Add((
                    "moved-successfully-off-a-really-threatened-turn",
                    realThreatMoves > 0));
                criteria.Add(("took-no-damage", damageTaken == 0));
                break;
            case AnalysisKind.CooldownWindow:
                criteria.Add((
                    "controller-committed-its-one-shot",
                    controllerAttackCount > 0));
                criteria.Add((
                    "closed-on-the-objective-or-dealt-damage-during-the-"
                    + "declared-cooldown-window",
                    (
                        distanceAtAttack is int initialDistance
                        && minimumCooldownDistance is int minimumDistance
                        && minimumDistance < initialDistance)
                    || cooldownDamage > 0));
                break;
            case AnalysisKind.LocalFormSafety:
                criteria.Add((
                    "accepted-no-same-life-transition",
                    unsafeCommitmentCount == 0));
                criteria.Add((
                    "held-sole-objective-control-for-an-uninterrupted-run",
                    maxCaptureTicks >= 4));
                criteria.Add(("took-no-damage", damageTaken == 0));
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
            curvedAttackCount,
            curvedProjectileHits.Count,
            curvedDamageDealt,
            apparentThreatTurnCount,
            realThreatTurnCount,
            apparentThreatMoves,
            realThreatMoves,
            damageTaken,
            controllerAttackCount,
            distanceAtAttack,
            minimumCooldownDistance,
            cooldownDamage,
            unsafeCommitmentCount,
            maxCaptureTicks,
            replayHash,
            replayPath,
            passed,
            failedCriteria);
    }

    /// <summary>
    /// One plain-language line per case stating the shape of a passing run.
    /// It is derived from the same clauses the analyzer evaluates, so a
    /// failing report never leaves an author replaying evidence to guess
    /// whether a metric of zero was the goal or the fault.
    /// </summary>
    private static string Expectation(AnalysisKind analysis) =>
        analysis switch
        {
            AnalysisKind.CurvedIntercept =>
                "Land at least one curved (one-bend) shot on the off-axis "
                + "target and waste no commitment: every curved shot fired "
                + "must hit, and the curved fire must deal damage. Both "
                + "halves count: firing no curve fails, and firing extra "
                + "curves that miss fails even when one of them hits.",
            AnalysisKind.StrictCorner =>
                "Refuse the intercept that only the lax preview allows: "
                + "fire no curved shot at all here, take no damage, and "
                + "keep sole control of the objective you already stand on "
                + "for an uninterrupted run of ticks.",
            AnalysisKind.CadenceHarmless =>
                "Stand your ground against a shot that cannot reach you: "
                + "the controller's single straight shot is aimed along "
                + "your line and still looks live two of its advances out, "
                + "but its declared remaining travel expires before your "
                + "tile, so hold the objective, make no evasive move while "
                + "the threat is apparent-only, never step into the range "
                + "where it could really connect, take no damage, and keep "
                + "sole objective control uninterrupted. The mirrored "
                + "range-4-threatening variant is the one that requires a "
                + "dodge; these two differ only in the resolved projectile "
                + "maxTravelTiles reported below.",
            AnalysisKind.CadenceThreatening =>
                "The mirrored real threat: this shot's declared travel does "
                + "reach your tile, so standing on its line is genuinely "
                + "dangerous; leave that line with a successful move on at "
                + "least one really-threatened turn and take no damage.",
            AnalysisKind.CooldownWindow =>
                "Use the window the controller's committed shot opens: "
                + "while its declared cooldown runs, either get strictly "
                + "closer to the active objective than you were when it "
                + "fired, or deal damage; either one alone passes.",
            AnalysisKind.LocalFormSafety =>
                "Keep the objective weight you already have: accept no "
                + "same-life transition at all, hold sole objective control "
                + "uninterrupted, and take no damage.",
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
                    ? FrontlineLabsQualificationDefinition.MindFundamentalsSuiteId
                    : FrontlineLabsQualificationDefinition.FundamentalsSuiteId)
            || suiteVersion
                != FrontlineLabsQualificationDefinition
                    .FundamentalsSuiteVersion
            || profileId
                != (mindProfile
                    ? FrontlineLabsQualificationDefinition.MindFundamentalsProfileId
                    : FrontlineLabsQualificationDefinition.FundamentalsProfileId))
        {
            throw new InvalidOperationException(
                "T3 qualification requires the exact immutable cumulative " +
                "T2 prerequisite profile.");
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
            bool occupies = state.GetProperty("activeLives")
                .EnumerateArray()
                .Any(life =>
                    life.GetProperty("actorId")
                        .GetProperty("teamId")
                        .GetInt32() == botTeamId
                    && objective.Contains(
                        ReadPosition(life.GetProperty("position"))));
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

    private static int CooldownForForm(
        JsonElement contract,
        string formId)
    {
        JsonElement form = contract.GetProperty("rules")
            .GetProperty("forms")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("id").GetString() == formId);
        string attackProfileId =
            form.GetProperty("attackProfileId").GetString()!;
        return contract.GetProperty("rules")
            .GetProperty("attackProfiles")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("id").GetString()
                    == attackProfileId)
            .GetProperty("cooldownTicks")
            .GetInt32();
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
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static string ControllerName(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => "Qualification Passive Controller",
            ControllerKind.OneShot => "Qualification One-Shot Controller",
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static string ControllerFingerprint(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => WaitControllerFingerprint,
            ControllerKind.OneShot => OneShotControllerFingerprint,
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static string ControllerRole(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => "passive-wait-controller",
            ControllerKind.OneShot => "one-shot-straight-controller",
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
        };

    private static int ProbeOrder(string probeId) =>
        probeId switch
        {
            FrontlineLabsQualificationDefinition
                .WallTerminatedBendProbeId => 0,
            FrontlineLabsQualificationDefinition.StrictCornerProbeId => 1,
            FrontlineLabsQualificationDefinition.CadenceParityProbeId => 2,
            FrontlineLabsQualificationDefinition.CooldownWindowProbeId => 3,
            FrontlineLabsQualificationDefinition.LocalFormSafetyProbeId => 4,
            _ => 100,
        };

    private static string Fingerprint(string value) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
