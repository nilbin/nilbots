using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Produces one short, real-rules Arc Relay replay for judging a named
/// signature in isolation. This is presentation diagnostics, not balance or
/// audit evidence: one caster and only the context the signature needs are
/// staged in an open Tapline pocket while every other body waits.
/// </summary>
public static class ArcRelaySignatureStageCommand
{
    private const int DefaultTicks = 72;
    private const int MinimumTicks = 48;
    private const int MaximumTicks = 180;

    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(options, "signature", "seed", "ticks", "out");
        string signatureId = options.GetValueOrDefault("signature")
            ?? throw new InvalidOperationException(
                "Signature stage requires --signature <id>.");
        ArcRelaySignatureStageSpec spec =
            ArcRelaySignatureStageCatalog.Resolve(signatureId);
        ulong seed = ulong.Parse(
            options.GetValueOrDefault("seed") ?? "7001",
            NumberStyles.None,
            CultureInfo.InvariantCulture);
        int maxTicks = int.Parse(
            options.GetValueOrDefault("ticks")
                ?? DefaultTicks.ToString(CultureInfo.InvariantCulture),
            NumberStyles.None,
            CultureInfo.InvariantCulture);
        if (maxTicks is < MinimumTicks or > MaximumTicks)
        {
            throw new InvalidOperationException(
                $"Signature stage --ticks must be {MinimumTicks}–{MaximumTicks}.");
        }

        ActorResolvedMatchDefinition definition = CreateDefinition(spec, maxTicks);
        using var west = new ArcRelaySignatureStageMindFactory(spec, teamId: 0);
        using var east = new ArcRelaySignatureStageMindFactory(spec, teamId: 1);
        GenericActorParticipantConfiguration[] participants =
        [
            Participant(0, 0, $"{spec.ClassId}-signature-stage", west, "#38bdf8"),
            Participant(1, 1, "minimal-stage-context", east, "#fb7185"),
        ];

        using var session = new GenericActorMatchSession(
            definition,
            participants,
            seed);
        ArcRelayBroadcastDocument broadcast =
            ArcRelayBroadcastDocument.CreateAndRun(
                session,
                ArcRelayH0ReplayPresentation.Create(definition));
        ArcRelayEvent.SignatureChanged[] signatureFacts =
            SignatureFacts(session, spec.SignatureId).ToArray();
        int attempts = signatureFacts
            .Count(value => value.Reason == "started");
        if (attempts == 0)
        {
            throw new InvalidOperationException(
                $"Focused stage did not cast {spec.SignatureId}; no review replay was written.");
        }
        ValidateStageEvidence(session, spec, signatureFacts);

        string output = Path.GetFullPath(
            options.GetValueOrDefault("out")
            ?? Path.Combine("out", "arc-relay-signature-stage", spec.SignatureId));
        WrittenReplay written = ReplayOutput.WriteGzipJson(
            broadcast.CanonicalUtf8,
            output);
        string receiptPath = Path.Combine(output, "stage.json");
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    purpose = "presentation-diagnostic-no-balance-claim",
                    signatureId = spec.SignatureId,
                    classId = spec.ClassId,
                    mapId = definition.Map.Id,
                    rulesetId = definition.Rules.RulesetId,
                    seed = seed.ToString(CultureInfo.InvariantCulture),
                    ticks = broadcast.Result.EndTick is int endTick
                        ? endTick + 1
                        : 0,
                    attempts,
                    context = spec.Context.ToString(),
                    replay = Path.GetFileName(written.ReplayPath),
                    replayHash = broadcast.ReplayHash,
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            $"Focused {spec.SignatureId} stage: {attempts} cast(s), "
            + $"{broadcast.Result.EndTick + 1} ticks.");
        Console.WriteLine($"Replay: {written.ReplayPath}");
        Console.WriteLine($"Receipt: {Path.GetFullPath(receiptPath)}");
        Console.WriteLine(
            "Presentation diagnostic only; this replay makes no balance or audit claim.");
        return 0;
    }

    private static ActorResolvedMatchDefinition CreateDefinition(
        ArcRelaySignatureStageSpec spec,
        int maxTicks)
    {
        IReadOnlyList<string> westSheet = Sheet(spec.ClassId, ArcRelayLaunchClassIds.Palisade);
        IReadOnlyList<string> eastSheet = Sheet(
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Mason);
        ActorResolvedMatchDefinition source = ArcRelayH0Definition.Create(
            westSheet,
            eastSheet,
            loopProfile: spec.SignatureId == "prism-wall"
                ? ArcRelayLoopProfile.Tapline12
                : ArcRelayLoopProfile.Current);

        ActorRulesDefinition rules = source.Rules;
        ArcRelayGameModeDefinition mode = (ArcRelayGameModeDefinition)
            rules.GameMode;
        int firstCoreTick = spec.Context == ArcRelaySignatureStageContext.Core
            ? 12
            : Math.Max(MinimumTicks - 1, maxTicks - 1);
        var stagedMode = new ArcRelayGameModeDefinition(
            mode.ModeId,
            mode.ArcRelayVictory,
            mode.ScoreCatalog,
            mode.Wells.Select(well =>
                new ArcRelayWellScheduleDefinition(
                    well.WellId,
                    firstCoreTick,
                    maxTicks,
                    firstCoreTick)),
            mode.PendingRearmTicks,
            mode.CoreRelocationIntervalTicks,
            mode.CoresPerPulse,
            mode.FieldedSlotsPerTeam,
            mode.MaxCopiesPerClass,
            mode.RespawnDelayTicks,
            mode.Signatures,
            mode.SignatureGrammarVersion,
            wellBirthJitterTicks: 0,
            mode.AlternatingResolutionOrder,
            mode.ThreefoldSockets,
            mode.CoreBaseValue,
            mode.RipenIntervalTicks,
            mode.RipenMaxValue,
            mode.RipenResumeTicks,
            mode.RearArcDamageMultiplier,
            mode.VeterancyXpPerLevel,
            mode.VeterancyMaxLevel,
            mode.HealZoneTicksPerHp,
            mode.SeedPhasedResolutionOrder,
            mode.SeedPhasedWellLead,
            chiralWellBirths: false,
            ownWellCoreCharge: mode.OwnWellCoreCharge,
            centreWellCoreCharge: mode.CentreWellCoreCharge,
            enemyWellCoreCharge: mode.EnemyWellCoreCharge,
            veteranCommand: mode.VeteranCommand,
            respawnWavePeriodTicks: mode.RespawnWavePeriodTicks,
            respawnMinimumDeadTicks: mode.RespawnMinimumDeadTicks,
            minimumDistinctClasses: mode.MinimumDistinctClasses,
            durableDeployments: mode.DurableDeployments,
            veterancyDamagePerPoint: mode.VeterancyDamagePerPoint,
            veterancyDamageBonusCap: mode.VeterancyDamageBonusCap,
            veterancyBonusAfterRearArc: mode.VeterancyBonusAfterRearArc,
            vitalityHealthPerPoint: mode.VitalityHealthPerPoint,
            healZoneHealthPerActivation:
                mode.HealZoneHealthPerActivation);
        var stagedRules = new ActorRulesDefinition(
            $"arc-relay-signature-stage-t{maxTicks}-01",
            new ActorRulesLimits(maxTicks, rules.Limits.RuntimeFaults),
            rules.SeedMechanics,
            stagedMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles,
            rules.VisionProfiles,
            rules.AttackProfiles,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);

        var anchors = source.Map.SpawnAnchors
            .Select(anchor => StageAnchor(anchor, spec))
            .ToImmutableArray();
        var map = new ActorMapDefinition(
            $"arc-relay-stage-{spec.SignatureId}-01",
            1,
            source.Map.TileRows,
            anchors,
            source.Map.Regions,
            source.Map.TileTags);
        var deployment = new InitialDeploymentDefinition(
            anchors.Select(value => value.Spawn).ToImmutableArray(),
            source.InitialDeployment.Lives);

        return new ActorResolvedMatchDefinition(
            stagedRules,
            map,
            source.Format,
            source.Topology,
            deployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorMapSpawnAnchorDefinition StageAnchor(
        ActorMapSpawnAnchorDefinition source,
        ArcRelaySignatureStageSpec spec)
    {
        string id = source.Spawn.SpawnId;
        Position position = source.Spawn.Position;
        Direction facing = source.Spawn.Facing;
        if (id == "team-0-unit-0")
        {
            position = ArcRelaySignatureStageMindFactory.StartPosition(spec);
            facing = Direction.East;
        }
        else if (id == "team-0-unit-1" && spec.NeedsAlly)
        {
            position = ArcRelaySignatureStageMindFactory.AllyStart;
            facing = Direction.East;
        }
        else if (id == "team-1-unit-0" && spec.NeedsEnemy)
        {
            position = spec.Context == ArcRelaySignatureStageContext.KineticTarget
                ? ArcRelaySignatureStageMindFactory.KineticEnemyStart
                : ArcRelaySignatureStageMindFactory.EnemyStart;
            facing = Direction.West;
        }
        return new ActorMapSpawnAnchorDefinition(
            new InitialSpawnDefinition(id, position, facing),
            source.CompatibleMovementLayers);
    }

    private static IReadOnlyList<string> Sheet(string first, string second)
    {
        var classes = new List<string> { first, second };
        // Fill distinct classes first so this presentation-only stage remains
        // a legal current-roster match even when the caster and required
        // context body share a class. Only then spend the eighth slot on a
        // permitted duplicate.
        foreach (string candidate in ArcRelayLaunchClassIds.All)
        {
            if (classes.Count == 8)
                break;
            if (!classes.Contains(candidate, StringComparer.Ordinal))
                classes.Add(candidate);
        }
        foreach (string candidate in ArcRelayLaunchClassIds.All)
        {
            if (classes.Count == 8)
                break;
            if (classes.Count(value => value == candidate) < 2)
                classes.Add(candidate);
        }
        return classes;
    }

    private static IEnumerable<ArcRelayEvent.SignatureChanged> SignatureFacts(
        GenericActorMatchSession session,
        string signatureId) =>
        session.Chronology.Ticks
            .SelectMany(frame => frame.TickStart.Events.Concat(frame.Events))
            .Select(value => value.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .OfType<ArcRelayEvent.SignatureChanged>()
            .Where(value => string.Equals(
                value.SignatureId,
                signatureId,
                StringComparison.Ordinal));

    private static void ValidateStageEvidence(
        GenericActorMatchSession session,
        ArcRelaySignatureStageSpec spec,
        IReadOnlyCollection<ArcRelayEvent.SignatureChanged> signatureFacts)
    {
        IEnumerable<ArcRelayEvent> arcFacts = session.Chronology.Ticks
            .SelectMany(frame => frame.TickStart.Events.Concat(frame.Events))
            .Select(value => value.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.ArcRelay>()
            .Select(value => value.Fact);
        switch (spec.SignatureId)
        {
            case "exchange" when !arcFacts
                .OfType<ArcRelayEvent.BodyRelocated>()
                .Any(value => value.SignatureId == spec.SignatureId):
                throw new InvalidOperationException(
                    "Focused Exchange stage completed without a real body swap; "
                    + "signature phases: "
                    + string.Join(", ", signatureFacts.Select(value =>
                        $"{value.Phase}:{value.Reason}"))
                    + ".");
            case "arc-toss" when !arcFacts
                .OfType<ArcRelayEvent.CoreRelocated>()
                .Any(value => value.Kind
                    == ArcRelayEvent.CoreRelocationKind.ArcTossLanding
                    && value.From.ChebyshevDistance(value.To) >= 3):
                throw new InvalidOperationException(
                    "Focused Arc Toss stage did not produce a readable long toss.");
            case "sentinel-seed" when !session.Chronology.Ticks
                .SelectMany(frame => frame.Traversals)
                .Any(value => value.AttackProfileId == "sentinel-bolt"):
                throw new InvalidOperationException(
                    "Focused Sentinel stage deployed but never fired a real bolt.");
        }
        if (spec.SignatureId == "sentinel-seed"
            && !signatureFacts.Any(value => value.Reason == "deployed"))
        {
            throw new InvalidOperationException(
                "Focused Sentinel stage never reached its deployed phase.");
        }
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
            RuntimeKind = "in-process-signature-presentation-stage",
            ArtifactHash = $"arc-signature-stage-{teamId}",
            Accent = accent,
        };
}

internal enum ArcRelaySignatureStageContext
{
    None,
    AllyTarget,
    EnemyTarget,
    RepairTarget,
    KineticTarget,
    TripTarget,
    Core,
}

internal sealed record ArcRelaySignatureStageSpec(
    string SignatureId,
    string ClassId,
    ArcRelaySignatureStageContext Context)
{
    public bool NeedsAlly =>
        Context is ArcRelaySignatureStageContext.AllyTarget
            or ArcRelaySignatureStageContext.RepairTarget;

    public bool NeedsEnemy =>
        Context is ArcRelaySignatureStageContext.EnemyTarget
            or ArcRelaySignatureStageContext.RepairTarget
            or ArcRelaySignatureStageContext.KineticTarget
            or ArcRelaySignatureStageContext.TripTarget;
}

internal static class ArcRelaySignatureStageCatalog
{
    private static readonly IReadOnlyDictionary<string, ArcRelaySignatureStageSpec>
        Entries = new[]
        {
            Spec("vector-dash", ArcRelayLaunchClassIds.Kestrel),
            Spec("prism-wall", ArcRelayLaunchClassIds.Palisade),
            Spec("tractor-hook", ArcRelayLaunchClassIds.Towline,
                ArcRelaySignatureStageContext.EnemyTarget),
            Spec("repair-beam", ArcRelayLaunchClassIds.Patchbay,
                ArcRelaySignatureStageContext.RepairTarget),
            Spec("survey-flare", ArcRelayLaunchClassIds.Lantern),
            Spec("falling-star", ArcRelayLaunchClassIds.Mortar,
                ArcRelaySignatureStageContext.EnemyTarget),
            Spec("trip-node", ArcRelayLaunchClassIds.Minesmith,
                ArcRelaySignatureStageContext.TripTarget),
            Spec("null-field", ArcRelayLaunchClassIds.Hush),
            Spec("arc-toss", ArcRelayLaunchClassIds.Relay,
                ArcRelaySignatureStageContext.Core),
            Spec("exchange", ArcRelayLaunchClassIds.Switchback,
                ArcRelaySignatureStageContext.AllyTarget),
            Spec("rail-line", ArcRelayLaunchClassIds.Longshot,
                ArcRelaySignatureStageContext.EnemyTarget),
            Spec("hardlight-block", ArcRelayLaunchClassIds.Mason),
            Spec("target-paint", ArcRelayLaunchClassIds.Sunder,
                ArcRelaySignatureStageContext.EnemyTarget),
            Spec("kinetic-burst", ArcRelayLaunchClassIds.Repulsor,
                ArcRelaySignatureStageContext.KineticTarget),
            Spec("smoke-canister", ArcRelayLaunchClassIds.Veil),
            Spec("sentinel-seed", ArcRelayLaunchClassIds.Nest,
                ArcRelaySignatureStageContext.EnemyTarget),
        }.ToDictionary(value => value.SignatureId, StringComparer.Ordinal);

    public static ArcRelaySignatureStageSpec Resolve(string signatureId)
    {
        if (Entries.TryGetValue(signatureId, out ArcRelaySignatureStageSpec? spec))
            return spec;
        throw new InvalidOperationException(
            $"Unknown Arc Relay signature '{signatureId}'. Choose: "
            + string.Join(", ", Entries.Keys.Order(StringComparer.Ordinal)));
    }

    private static ArcRelaySignatureStageSpec Spec(
        string signatureId,
        string classId,
        ArcRelaySignatureStageContext context = ArcRelaySignatureStageContext.None) =>
        new(signatureId, classId, context);
}
