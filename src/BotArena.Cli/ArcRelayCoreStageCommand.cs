using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Produces one compact, real-rules Arc Relay replay for judging Core custody
/// animation across differently sized chassis. Exactly eight bodies share one
/// Core through carried movement, handoffs, voluntary drops, reacquisition,
/// and one long Arc Toss. It is presentation diagnostics, not gameplay or
/// balance evidence.
/// </summary>
public static class ArcRelayCoreStageCommand
{
    private const int DefaultTicks = 104;
    private const int MinimumTicks = 88;
    private const int MaximumTicks = 160;
    private const int BodiesPerTeam = 4;
    private const int FirstCoreTick = 6;

    private static readonly IReadOnlyList<string> WestClasses =
    [
        ArcRelayLaunchClassIds.Relay,
        ArcRelayLaunchClassIds.Kestrel,
        ArcRelayLaunchClassIds.Mason,
        ArcRelayLaunchClassIds.Mortar,
        ArcRelayLaunchClassIds.Lantern,
        ArcRelayLaunchClassIds.Minesmith,
        ArcRelayLaunchClassIds.Hush,
        ArcRelayLaunchClassIds.Sunder,
    ];

    private static readonly IReadOnlyList<string> EastClasses =
    [
        ArcRelayLaunchClassIds.Nest,
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Towline,
        ArcRelayLaunchClassIds.Patchbay,
        ArcRelayLaunchClassIds.Switchback,
        ArcRelayLaunchClassIds.Longshot,
        ArcRelayLaunchClassIds.Repulsor,
        ArcRelayLaunchClassIds.Veil,
    ];

    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(options, "seed", "ticks", "out");
        ulong seed = ulong.Parse(
            options.GetValueOrDefault("seed") ?? "8108",
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
                $"Core stage --ticks must be {MinimumTicks}–{MaximumTicks}.");
        }

        ActorResolvedMatchDefinition definition = CreateDefinition(maxTicks);
        using var west = new ArcRelayCoreStageMindFactory(teamId: 0);
        using var east = new ArcRelayCoreStageMindFactory(teamId: 1);
        GenericActorParticipantConfiguration[] participants =
        [
            Participant(0, 0, "west-core-relay", west, "#38bdf8"),
            Participant(1, 1, "east-core-relay", east, "#fb7185"),
        ];

        using var session = new GenericActorMatchSession(
            definition,
            participants,
            seed);
        ArcRelayBroadcastDocument broadcast =
            ArcRelayBroadcastDocument.CreateAndRun(
                session,
                ArcRelayH0ReplayPresentation.Create(definition));
        ArcRelayEvent[] arcFacts = ArcFacts(session).ToArray();
        ArcRelayCoreStageEvidence evidence = ValidateStageEvidence(
            definition,
            arcFacts);

        string output = Path.GetFullPath(
            options.GetValueOrDefault("out")
            ?? Path.Combine("out", "arc-relay-core-stage"));
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
                    purpose = "core-presentation-diagnostic-no-balance-claim",
                    mapId = definition.Map.Id,
                    rulesetId = definition.Rules.RulesetId,
                    seed = seed.ToString(CultureInfo.InvariantCulture),
                    ticks = broadcast.Result.EndTick is int endTick
                        ? endTick + 1
                        : 0,
                    bodyCount = definition.Topology.InitialLives.Length,
                    classes = definition.Topology.UnitSlots
                        .OrderBy(value => value.TeamId)
                        .ThenBy(value => value.UnitId)
                        .Select(value => new
                        {
                            teamId = value.TeamId,
                            unitId = value.UnitId,
                            classId = value.ClassId,
                        }),
                    choreography = new
                    {
                        coreBirths = evidence.CoreBirths,
                        pickups = evidence.Pickups,
                        handoffs = evidence.Handoffs,
                        voluntaryDrops = evidence.VoluntaryDrops,
                        carriedMoves = evidence.CarriedMoves,
                        distinctMovingHandlers = evidence.DistinctMovingHandlers,
                        distinctHandlers = evidence.DistinctHandlers,
                        arcTossLandings = evidence.ArcTossLandings,
                        longestArcTossTiles = evidence.LongestArcTossTiles,
                    },
                    replay = Path.GetFileName(written.ReplayPath),
                    replayHash = broadcast.ReplayHash,
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            $"Focused Core stage: {evidence.DistinctHandlers} handlers, "
            + $"{evidence.Handoffs} handoffs, "
            + $"{evidence.VoluntaryDrops} voluntary drops, "
            + $"{evidence.LongestArcTossTiles}-tile Arc Toss.");
        Console.WriteLine($"Replay: {written.ReplayPath}");
        Console.WriteLine($"Receipt: {Path.GetFullPath(receiptPath)}");
        Console.WriteLine(
            "Presentation diagnostic only; this replay makes no balance or audit claim.");
        return 0;
    }

    private static ActorResolvedMatchDefinition CreateDefinition(int maxTicks)
    {
        ActorResolvedMatchDefinition source = ArcRelayH0Definition.Create(
            WestClasses,
            EastClasses,
            loopProfile: ArcRelayLoopProfile.Current);
        ActorRulesDefinition rules = source.Rules;
        ArcRelayGameModeDefinition mode = (ArcRelayGameModeDefinition)
            rules.GameMode;
        var stagedMode = new ArcRelayGameModeDefinition(
            mode.ModeId,
            mode.ArcRelayVictory,
            mode.ScoreCatalog,
            mode.Wells
                .Where(well => string.Equals(
                    well.WellId,
                    "centre",
                    StringComparison.Ordinal))
                .Select(well => new ArcRelayWellScheduleDefinition(
                    well.WellId,
                    FirstCoreTick,
                    maxTicks,
                    FirstCoreTick)),
            mode.PendingRearmTicks,
            mode.CoreRelocationIntervalTicks,
            mode.CoresPerPulse,
            BodiesPerTeam,
            maxCopiesPerClass: 1,
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
            minimumDistinctClasses: Math.Min(
                mode.MinimumDistinctClasses,
                BodiesPerTeam),
            durableDeployments: mode.DurableDeployments,
            veterancyDamagePerPoint: mode.VeterancyDamagePerPoint,
            veterancyDamageBonusCap: mode.VeterancyDamageBonusCap,
            veterancyBonusAfterRearArc: mode.VeterancyBonusAfterRearArc,
            vitalityHealthPerPoint: mode.VitalityHealthPerPoint,
            healZoneHealthPerActivation:
                mode.HealZoneHealthPerActivation);
        var stagedRules = new ActorRulesDefinition(
            $"arc-relay-core-stage-t{maxTicks}-01",
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

        ImmutableArray<PublicUnitSlot> slots = source.Topology.UnitSlots
            .Where(value => value.UnitId < BodiesPerTeam)
            .ToImmutableArray();
        ImmutableArray<PublicInitialLife> initialLives =
            source.Topology.InitialLives
                .Where(value => value.UnitId < BodiesPerTeam)
                .ToImmutableArray();
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
            UnitSlots = slots,
            InitialLives = initialLives,
        };

        Dictionary<string, Position> positions = Enumerable.Range(0, 2)
            .SelectMany(teamId => Enumerable.Range(0, BodiesPerTeam)
                .Select(unitId => new
                {
                    SpawnId = $"team-{teamId}-unit-{unitId}",
                    Position = ArcRelayCoreStageMindFactory.StartPosition(
                        teamId,
                        unitId),
                }))
            .ToDictionary(
                value => value.SpawnId,
                value => value.Position,
                StringComparer.Ordinal);
        ImmutableArray<ActorMapSpawnAnchorDefinition> anchors =
            source.Map.SpawnAnchors
                .Where(value => positions.ContainsKey(value.Spawn.SpawnId))
                .Select(value => new ActorMapSpawnAnchorDefinition(
                    new InitialSpawnDefinition(
                        value.Spawn.SpawnId,
                        positions[value.Spawn.SpawnId],
                        value.Spawn.SpawnId.StartsWith(
                            "team-0-",
                            StringComparison.Ordinal)
                            ? Direction.East
                            : string.Equals(
                                value.Spawn.SpawnId,
                                "team-1-unit-0",
                                StringComparison.Ordinal)
                                ? Direction.North
                            : Direction.West),
                    value.CompatibleMovementLayers))
                .ToImmutableArray();
        var map = new ActorMapDefinition(
            "arc-relay-core-stage-01",
            1,
            source.Map.TileRows,
            anchors,
            source.Map.Regions,
            source.Map.TileTags);
        ImmutableArray<InitialLifeDeployment> lives =
            source.InitialDeployment.Lives
                .Where(value => value.UnitId < BodiesPerTeam)
                .ToImmutableArray();
        var deployment = new InitialDeploymentDefinition(
            anchors.Select(value => value.Spawn).ToImmutableArray(),
            lives);

        return new ActorResolvedMatchDefinition(
            stagedRules,
            map,
            source.Format,
            topology,
            deployment,
            source.LifecycleAssignments
                .Where(value => value.UnitId < BodiesPerTeam),
            source.ParticipantRegionAssignments,
            new ArcRelayActorModeMapBindingDefinition(
                ["well-centre"],
                ArcRelayH0Definition.ReactorRoleId,
                ArcRelayH0Definition.HomePadRoleId),
            source.CapabilityVersions);
    }

    private static ArcRelayCoreStageEvidence ValidateStageEvidence(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<ArcRelayEvent> arcFacts)
    {
        int coreBirths = arcFacts.OfType<ArcRelayEvent.CoreBorn>().Count();
        int pickups = arcFacts.OfType<ArcRelayEvent.CorePickedUp>().Count();
        int handoffs = arcFacts.OfType<ArcRelayEvent.CoreHandedOff>().Count();
        int voluntaryDrops = arcFacts
            .OfType<ArcRelayEvent.CoreDropped>()
            .Count(value => value.Kind
                == ArcRelayEvent.CoreDropKind.Voluntary);
        ArcRelayEvent.CoreRelocated[] carried = arcFacts
            .OfType<ArcRelayEvent.CoreRelocated>()
            .Where(value => value.Kind
                == ArcRelayEvent.CoreRelocationKind.CarriedMovement)
            .ToArray();
        ArcRelayEvent.CoreRelocated[] tosses = arcFacts
            .OfType<ArcRelayEvent.CoreRelocated>()
            .Where(value => value.Kind
                == ArcRelayEvent.CoreRelocationKind.ArcTossLanding)
            .ToArray();
        int longestArcToss = tosses.Length == 0
            ? 0
            : tosses.Max(value => value.From.ChebyshevDistance(value.To));
        int movingHandlers = carried
            .Select(value => value.CarrierActorId)
            .Distinct()
            .Count();
        HashSet<ActorIdentity> handlers = arcFacts
            .OfType<ArcRelayEvent.CorePickedUp>()
            .Select(value => value.CarrierActorId)
            .Concat(arcFacts.OfType<ArcRelayEvent.CoreHandedOff>()
                .Select(value => value.TargetActorId))
            .ToHashSet();
        int expectedBodies = BodiesPerTeam * 2;
        int distinctClasses = definition.Topology.UnitSlots
            .Select(value => value.ClassId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (definition.Topology.InitialLives.Length != expectedBodies
            || distinctClasses != expectedBodies
            || coreBirths != 1
            || handoffs < 6
            || voluntaryDrops < 2
            || carried.Length < expectedBodies
            || movingHandlers != expectedBodies
            || handlers.Count != expectedBodies
            || longestArcToss < 4)
        {
            throw new InvalidOperationException(
                "Focused Core stage did not complete its eight-body custody choreography; "
                + $"observed {coreBirths} birth, {handoffs} handoffs, "
                + $"{voluntaryDrops} drops, {carried.Length} carried moves, "
                + $"{movingHandlers} moving handlers, {handlers.Count} handlers, "
                + $"and a {longestArcToss}-tile Arc Toss. "
                + "No review replay was written.");
        }
        return new ArcRelayCoreStageEvidence(
            coreBirths,
            pickups,
            handoffs,
            voluntaryDrops,
            carried.Length,
            movingHandlers,
            handlers.Count,
            tosses.Length,
            longestArcToss);
    }

    private static IEnumerable<ArcRelayEvent> ArcFacts(
        GenericActorMatchSession session) =>
        session.Chronology.Ticks
            .SelectMany(frame => frame.TickStart.Events.Concat(frame.Events))
            .Select(value => value.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.ArcRelay>()
            .Select(value => value.Fact);

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
            RuntimeKind = "in-process-core-presentation-stage",
            ArtifactHash = $"arc-core-stage-{teamId}",
            Accent = accent,
        };

    private sealed record ArcRelayCoreStageEvidence(
        int CoreBirths,
        int Pickups,
        int Handoffs,
        int VoluntaryDrops,
        int CarriedMoves,
        int DistinctMovingHandlers,
        int DistinctHandlers,
        int ArcTossLandings,
        int LongestArcTossTiles);
}
