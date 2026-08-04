using System.Collections.Immutable;
using System.Text.Json;
using BotArena.Sdk;

internal sealed class TacticalPlaybookPackage
{
    private const int EnvelopeMagic = 0x31505441;
    private const int MaximumPayloadBytes = 64 * 1024;
    private const string PlaybookSchema = "arc-relay-tactical-playbook-v1";
    private const string LayoutSchema = "arc-relay-tactical-layout-v1";
    private readonly LayoutTransform _transform;
    private readonly Dictionary<string, Zone> _zones;
    private readonly Dictionary<string, Route> _routes;
    private readonly Dictionary<string, Anchor> _anchors;
    private readonly IReadOnlyDictionary<string, string> _routeAliases;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    private TacticalPlaybookPackage(
        Playbook source,
        Layout layout,
        string playbookSha256,
        string layoutSha256,
        LayoutTransform transform,
        int mapWidth,
        int mapHeight,
        IReadOnlyDictionary<string, string> routeAliases)
    {
        Source = source;
        LayoutSource = layout;
        PlaybookSha256 = playbookSha256;
        LayoutSha256 = layoutSha256;
        _transform = transform;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _routeAliases = routeAliases;
        _zones = layout.Zones.ToDictionary(value => value.ZoneId,
            StringComparer.Ordinal);
        _routes = layout.Routes.ToDictionary(value => value.RouteId,
            StringComparer.Ordinal);
        _anchors = layout.Anchors.ToDictionary(value => value.AnchorId,
            StringComparer.Ordinal);
    }

    internal Playbook Source { get; }
    internal Layout LayoutSource { get; }
    internal string PlaybookSha256 { get; }
    internal string LayoutSha256 { get; }

    internal static TacticalPlaybookPackage Load(
        ImmutableArray<byte> linkedData,
        GenericActorResolvedMatchContract contract,
        Position ownReactor)
    {
        if (linkedData.IsDefaultOrEmpty || linkedData.Length > MaximumPayloadBytes)
            throw new InvalidDataException("Tactical playbook data is missing or too large.");
        using var reader = new BinaryReader(
            new MemoryStream(linkedData.ToArray()));
        if (reader.ReadInt32() != EnvelopeMagic)
            throw new InvalidDataException("Unknown tactical playbook envelope.");
        string schema = reader.ReadString();
        if (!string.Equals(schema, PlaybookSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported tactical schema '{schema}'.");
        string playbookSha256 = ReadHash(reader, "playbook");
        string layoutSha256 = ReadHash(reader, "layout");
        byte[] playbookJson = ReadPayload(reader, "playbook");
        byte[] layoutJson = ReadPayload(reader, "layout");
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Trailing tactical playbook data.");

        Playbook playbook = JsonSerializer.Deserialize<Playbook>(
            playbookJson, TacticalPlaybookJsonContext.Default.Playbook)
            ?? throw new InvalidDataException("Tactical playbook is empty.");
        Layout layout = JsonSerializer.Deserialize<Layout>(
            layoutJson, TacticalPlaybookJsonContext.Default.Layout)
            ?? throw new InvalidDataException("Tactical layout is empty.");
        if (!string.Equals(playbook.Schema, PlaybookSchema,
                StringComparison.Ordinal)
            || !string.Equals(layout.Schema, LayoutSchema,
                StringComparison.Ordinal)
            || !string.Equals(layout.MapId, contract.Map.MapId,
                StringComparison.Ordinal)
            || playbook.Composition.Length != 8)
        {
            throw new InvalidDataException(
                "Tactical playbook schema, map, or composition is invalid.");
        }
        string side = ownReactor.X < contract.Map.Width / 2 ? "west" : "east";
        Binding binding = layout.Bindings.SingleOrDefault(value =>
                string.Equals(value.MatchContractFingerprint,
                    contract.MatchContractFingerprint, StringComparison.Ordinal)
                && string.Equals(value.OwnReactorSide, side,
                    StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Tactical layout is not bound to this exact match contract and side.");
        LayoutTransform transform = binding.Transform switch
        {
            "identity" => LayoutTransform.Identity,
            "mirror-x" => LayoutTransform.MirrorX,
            "rotate-180" => LayoutTransform.Rotate180,
            _ => throw new InvalidDataException(
                $"Unknown tactical layout transform '{binding.Transform}'."),
        };
        return new TacticalPlaybookPackage(
            playbook, layout, playbookSha256, layoutSha256, transform,
            contract.Map.Width, contract.Map.Height, binding.RouteAliases);
    }

    internal bool Contains(string zoneId, Position position)
    {
        Zone zone = _zones.GetValueOrDefault(zoneId)
            ?? throw new InvalidDataException($"Unknown tactical zone '{zoneId}'.");
        Position canonical = Canonical(position);
        return canonical.X >= zone.Rect[0] && canonical.X <= zone.Rect[2]
            && canonical.Y >= zone.Rect[1] && canonical.Y <= zone.Rect[3];
    }

    internal Position ZoneCenter(string zoneId)
    {
        Zone zone = _zones.GetValueOrDefault(zoneId)
            ?? throw new InvalidDataException($"Unknown tactical zone '{zoneId}'.");
        return World(new Position(
            (zone.Rect[0] + zone.Rect[2]) / 2,
            (zone.Rect[1] + zone.Rect[3]) / 2));
    }

    internal Position AnchorPosition(string anchorId)
    {
        Anchor anchor = _anchors.GetValueOrDefault(anchorId)
            ?? throw new InvalidDataException($"Unknown tactical anchor '{anchorId}'.");
        return World(new Position(anchor.Position[0], anchor.Position[1]));
    }

    internal Position[] RoutePoints(string routeId)
    {
        string resolved = _routeAliases.GetValueOrDefault(routeId, routeId);
        Route route = _routes.GetValueOrDefault(resolved)
            ?? throw new InvalidDataException($"Unknown tactical route '{routeId}'.");
        return route.Waypoints
            .Select(value => World(new Position(value[0], value[1])))
            .ToArray();
    }

    internal int RouteCorridorWidth(string routeId)
    {
        string resolved = _routeAliases.GetValueOrDefault(routeId, routeId);
        Route route = _routes.GetValueOrDefault(resolved)
            ?? throw new InvalidDataException(
                $"Unknown tactical route '{routeId}'.");
        return route.CorridorWidth;
    }

    internal Position FormationPosition(Position anchor, int[] offset)
    {
        int dx = _transform is LayoutTransform.MirrorX
            or LayoutTransform.Rotate180 ? -offset[0] : offset[0];
        int dy = _transform == LayoutTransform.Rotate180
            ? -offset[1] : offset[1];
        return anchor.Offset(dx, dy);
    }

    private Position Canonical(Position position) => _transform switch
    {
        LayoutTransform.MirrorX => new Position(
            _mapWidth - 1 - position.X, position.Y),
        LayoutTransform.Rotate180 => new Position(
            _mapWidth - 1 - position.X,
            _mapHeight - 1 - position.Y),
        _ => position,
    };

    private Position World(Position position) => Canonical(position);

    private static string ReadHash(BinaryReader reader, string name)
    {
        string value = reader.ReadString();
        if (value.Length != 64
            || value.Any(character => !Uri.IsHexDigit(character)
                || char.IsLetter(character) && char.IsUpper(character)))
            throw new InvalidDataException($"Invalid {name} SHA-256.");
        return value;
    }

    private static byte[] ReadPayload(BinaryReader reader, string name)
    {
        int length = reader.ReadInt32();
        if (length <= 0 || length > MaximumPayloadBytes)
            throw new InvalidDataException($"Invalid {name} payload length.");
        byte[] value = reader.ReadBytes(length);
        if (value.Length != length)
            throw new InvalidDataException($"Truncated {name} payload.");
        return value;
    }

    private enum LayoutTransform
    {
        Identity,
        MirrorX,
        Rotate180,
    }

    internal sealed record Playbook
    {
        public required string Schema { get; init; }
        public required string PlaybookId { get; init; }
        public required AuditStatus AuditStatus { get; init; }
        public required string[] Composition { get; init; }
        public required LayoutReference Layout { get; init; }
        public required string Perspective { get; init; }
        public required MemoryPolicy Memory { get; init; }
        public required ArbitrationPolicy Arbitration { get; init; }
        public required Role[] Roles { get; init; }
        public required Group[] Groups { get; init; }
        public required Formation[] Formations { get; init; }
        public required Engagement[] Engagements { get; init; }
        public required SupportPolicy[] SupportPolicies { get; init; }
        public required CustodyPolicy[] CustodyPolicies { get; init; }
        public required Order[] Orders { get; init; }
        public required Coordination Coordination { get; init; }
    }

    internal sealed record AuditStatus(
        bool ProvisionalEvaluationOnly,
        bool PlayerFacingProductSchema);

    internal sealed record LayoutReference(string Path, string Sha256);

    internal sealed record MemoryPolicy(
        int EnemyUnavailableTicks,
        int LastSeenEnemyTicks,
        int SecuredCoreTicks,
        int ObjectiveProgressTicks,
        int FormationStableTicks);

    internal sealed record ArbitrationPolicy(string Mode, string[] Channels);

    internal sealed record Role(
        string RoleId,
        string[] CandidateClasses,
        int Minimum,
        int Preferred,
        int Maximum,
        string CarrierPreference,
        string DeathPolicy,
        string RespawnPolicy,
        string OverflowRoleId);

    internal sealed record Group(
        string GroupId,
        string[] RoleIds,
        int Minimum,
        int Preferred,
        int Maximum,
        Membership Membership,
        StateMachine LocalStateMachine);

    internal sealed record Membership(
        string Persistence,
        string Casualty,
        string Preemption,
        string Overflow);

    internal sealed record StateMachine(string InitialState, LocalState[] States);

    internal sealed record LocalState(
        string StateId,
        int MinimumTicks,
        Transition[] Transitions);

    internal sealed record Formation(
        string FormationId,
        string Shape,
        string Orientation,
        Spacing Spacing,
        Placement[] Placements,
        Cohesion Cohesion,
        Reflow Reflow);

    internal sealed record Spacing(
        string Metric,
        int Minimum,
        int Preferred,
        int Maximum);

    internal sealed record Placement(
        string RoleId,
        string Sector,
        int Order,
        int[] Offset);

    internal sealed record Cohesion(
        int ArrivalRatioPercent,
        int BreakRatioPercent,
        int BreakTicks,
        int ReformTicks,
        string Pace);

    internal sealed record Reflow(
        string BlockedSlot,
        string Vacancy,
        int SearchRadius,
        int MedicSeparation);

    internal sealed record Engagement(
        string EngagementId,
        string[] Participants,
        string[] TargetPriorities,
        string[] TieBreakers,
        string CoordinationScope,
        int LockTicks,
        int MaximumAttackersPerTarget,
        int OverkillDamage,
        int ChaseLeash,
        string SignatureCoordination,
        DodgeCoverage DodgeCoverage,
        ReleasePolicy Release,
        SelfDefensePolicy SelfDefense);

    internal sealed record DodgeCoverage(
        string Mode,
        int HorizonTicks,
        int MinimumCoveredOptions,
        string Fallback);

    internal sealed record ReleasePolicy(
        int HiddenTicks,
        int UnreachableTicks,
        bool OutsideLeash,
        bool Destroyed);

    internal sealed record SelfDefensePolicy(
        bool Enabled,
        int ThreatDistance,
        bool ReturnToFormation);

    internal sealed record SupportPolicy(
        string SupportId,
        string[] Providers,
        string[] TargetPriorities,
        int MaximumProvidersPerTarget,
        int MinimumProviderSeparation,
        int ReserveHealthPercent,
        string SurvivalFallback);

    internal sealed record CustodyPolicy(
        string CustodyId,
        string[] AuthorizedCarrierRoles,
        string[] EscortGroups,
        string[] SourceWells,
        int PickupReservationTicks,
        int TransferTimeoutTicks,
        int DeliveryTimeoutTicks,
        string AccidentalPickup,
        string DropRecovery,
        string UnreachableFallback,
        ConditionGroup[] SafeConversionAll);

    internal sealed record Order(
        string OrderId,
        string GroupId,
        int Priority,
        Movement Movement,
        string FormationId,
        string EngagementId,
        string SupportId,
        string CustodyId,
        string LocalState,
        Fallback Fallback);

    internal sealed record Movement(
        string Kind,
        string Target,
        int ArrivalRadius,
        string Completion,
        int StuckTicks,
        string StuckRecovery,
        int ChaseLeash,
        string Pace);

    internal sealed record Fallback(
        string OnNoPath,
        string OnUnderstrength,
        string OnInvalidTarget,
        string PhaseId);

    internal sealed record Coordination(string InitialPhase, Phase[] Phases);

    internal sealed record Phase(
        string PhaseId,
        int MinimumTicks,
        string[] OrderIds,
        Transition[] Transitions);

    internal sealed record Transition(
        int Priority,
        string To,
        string Cause,
        string MinimumPolicy,
        int StableTicks,
        ConditionGroup[] When);

    internal sealed record ConditionGroup(Condition[] All, Condition[] Any);

    internal sealed record Condition(
        string Fact,
        string Operator,
        int Value,
        string Subject,
        string Zone,
        int FreshnessTicks);

    internal sealed record Layout
    {
        public required string Schema { get; init; }
        public required string LayoutId { get; init; }
        public required string MapId { get; init; }
        public required Binding[] Bindings { get; init; }
        public required Zone[] Zones { get; init; }
        public required Route[] Routes { get; init; }
        public required Anchor[] Anchors { get; init; }
    }

    internal sealed record Binding(
        string MatchContractFingerprint,
        string OwnReactorSide,
        string Transform,
        Dictionary<string, string> RouteAliases);

    internal sealed record Zone(string ZoneId, int[] Rect);

    internal sealed record Route(
        string RouteId,
        int[][] Waypoints,
        int CorridorWidth);

    internal sealed record Anchor(string AnchorId, int[] Position);
}
