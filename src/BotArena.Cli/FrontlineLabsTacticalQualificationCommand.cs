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
        bool Passed);

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
        bool Passed);

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
        string outputDirectory)
    {
        if (runtimeKind != "wasm")
        {
            throw new InvalidOperationException(
                $"{FrontlineLabsQualificationDefinition.TacticalSuiteId} " +
                "requires the canonical WASM runtime.");
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
                printSummary: false);
        string prerequisiteReportPath = Path.Combine(
            prerequisiteDirectory,
            "qualification.json");
        PrerequisiteEvidence prerequisite = ReadPrerequisite(
            prerequisiteReportPath,
            outputDirectory);
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
                            ControllerFingerprint(plan.Controller),
                            AnalyzerFingerprint,
                            run,
                            run.Passed)
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
            SchemaVersion: 4,
            FrontlineLabsQualificationDefinition.TacticalSuiteId,
            FrontlineLabsQualificationDefinition.TacticalSuiteVersion,
            FrontlineLabsQualificationDefinition.TacticalProfileId,
            qualificationFingerprint,
            resolvedArtifactName,
            resolvedArtifactHash,
            resolvedRuntimeKind,
            seed,
            prerequisite,
            Passed: t3Passed,
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
            foreach (JsonElement turn in tick
                         .GetProperty("actorTurns")
                         .EnumerateArray())
            {
                JsonElement resolution =
                    turn.GetProperty("actionResolution");
                string? acceptedActionId = AcceptedActionId(resolution);
                bool success =
                    resolution.GetProperty("outcome").GetString()
                    == "success";
                int participantId =
                    turn.GetProperty("participantId").GetInt32();
                if (participantId != botParticipantId)
                {
                    if (
                        controllerAttackTick is null
                        && success
                        && acceptedActionId is not null
                        && attackActions.Contains(acceptedActionId))
                    {
                        controllerAttackTick =
                            turn.GetProperty("tick").GetInt32();
                        string formId = turn.GetProperty("observation")
                            .GetProperty("self")
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
                    turn.GetProperty("observation");
                Position self = ReadPosition(
                    observation.GetProperty("self")
                        .GetProperty("position"));
                int tickNumber = turn.GetProperty("tick").GetInt32();
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
        bool common = contractValid
            && botEligible
            && runtimeFaultCount == 0
            && !disqualified
            && controllerValid
            && botTurnCount > 0
            && faultedTurnCount == 0;
        bool passed = common && analysis switch
        {
            AnalysisKind.CurvedIntercept =>
                curvedAttackCount > 0
                && curvedProjectileHits.Count == curvedAttackCount
                && curvedDamageDealt > 0,
            AnalysisKind.StrictCorner =>
                curvedAttackCount == 0
                && damageTaken == 0
                && maxCaptureTicks >= 3,
            AnalysisKind.CadenceHarmless =>
                controllerAttackCount > 0
                && apparentThreatTurnCount > 0
                && realThreatTurnCount == 0
                && apparentThreatMoves == 0
                && damageTaken == 0
                && maxCaptureTicks >= 3,
            AnalysisKind.CadenceThreatening =>
                controllerAttackCount > 0
                && realThreatTurnCount > 0
                && realThreatMoves > 0
                && damageTaken == 0,
            AnalysisKind.CooldownWindow =>
                controllerAttackCount > 0
                && ((
                    distanceAtAttack is int initialDistance
                    && minimumCooldownDistance is int minimumDistance
                    && minimumDistance < initialDistance)
                    || cooldownDamage > 0),
            AnalysisKind.LocalFormSafety =>
                unsafeCommitmentCount == 0
                && maxCaptureTicks >= 4
                && damageTaken == 0,
            _ => false,
        };
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
            passed);
    }

    private static PrerequisiteEvidence ReadPrerequisite(
        string reportPath,
        string outputDirectory)
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
                != FrontlineLabsQualificationDefinition
                    .FundamentalsSuiteId
            || suiteVersion
                != FrontlineLabsQualificationDefinition
                    .FundamentalsSuiteVersion
            || profileId
                != FrontlineLabsQualificationDefinition
                    .FundamentalsProfileId)
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

    private static IGenericActorRuntimeFactory ControllerFactory(
        ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait =>
                new InProcessGenericActorRuntimeFactory(
                    () => new FrontlineLabsQualificationWaitController()),
            ControllerKind.OneShot =>
                new InProcessGenericActorRuntimeFactory(
                    () => new FrontlineLabsQualificationOneShotController()),
            _ => throw new InvalidOperationException(
                "Unknown qualification controller."),
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
            Name = kind switch
            {
                ControllerKind.Wait =>
                    "Qualification Passive Controller",
                ControllerKind.OneShot =>
                    "Qualification One-Shot Controller",
                _ => "Qualification Controller",
            },
            RuntimeFactory = runtimeFactory,
            RuntimeKind = "in-process-qualification-controller",
            ArtifactHash = ControllerFingerprint(kind),
            Accent = "#f97316",
            LookId = "bastion",
            ProjectileLookId = "ember-lance",
        };

    private static string ControllerFingerprint(ControllerKind kind) =>
        kind switch
        {
            ControllerKind.Wait => WaitControllerFingerprint,
            ControllerKind.OneShot => OneShotControllerFingerprint,
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
