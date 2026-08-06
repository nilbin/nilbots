using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private readonly IReadOnlyDictionary<string, string> _formationAliases;
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
        IReadOnlyDictionary<string, string> routeAliases,
        IReadOnlyDictionary<string, string> formationAliases)
    {
        Source = source;
        LayoutSource = layout;
        PlaybookSha256 = playbookSha256;
        LayoutSha256 = layoutSha256;
        _transform = transform;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _routeAliases = routeAliases;
        _formationAliases = formationAliases;
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

        Layout layout = JsonSerializer.Deserialize<Layout>(
            layoutJson, TacticalPlaybookJsonContext.Default.Layout)
            ?? throw new InvalidDataException("Tactical layout is empty.");
        if (!string.Equals(layout.Schema, LayoutSchema,
                StringComparison.Ordinal)
            || !string.Equals(layout.MapId, contract.Map.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Tactical layout schema or map is invalid.");
        }
        string side = ownReactor.X < contract.Map.Width / 2 ? "west" : "east";
        // Exact contract bindings win; a side may also declare one
        // "any-composition" binding so a sheet can meet opponents whose
        // composition pair it has never seen without layout surgery.
        Binding binding = layout.Bindings.SingleOrDefault(value =>
                string.Equals(value.MatchContractFingerprint,
                    contract.MatchContractFingerprint, StringComparison.Ordinal)
                && string.Equals(value.OwnReactorSide, side,
                    StringComparison.Ordinal))
            ?? layout.Bindings.SingleOrDefault(value =>
                string.Equals(value.MatchContractFingerprint,
                    "any-composition", StringComparison.Ordinal)
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
        // Side-keyed parameter overrides: the selected binding may re-value
        // authored parameters (the two orientations of a map can genuinely
        // want different sensitivities). The compiler keeps each condition's
        // 'valueParameter' provenance beside its baked 'value', so the
        // override is a plain re-bake before deserialization.
        if (binding.ParameterOverrides is { Count: > 0 } overrides)
            playbookJson = ApplyParameterOverrides(playbookJson, overrides);
        Playbook playbook = JsonSerializer.Deserialize<Playbook>(
            playbookJson, TacticalPlaybookJsonContext.Default.Playbook)
            ?? throw new InvalidDataException("Tactical playbook is empty.");
        if (!string.Equals(playbook.Schema, PlaybookSchema,
                StringComparison.Ordinal)
            || playbook.Composition.Length != 8)
        {
            throw new InvalidDataException(
                "Tactical playbook schema or composition is invalid.");
        }
        return new TacticalPlaybookPackage(
            playbook, layout, playbookSha256, layoutSha256, transform,
            contract.Map.Width, contract.Map.Height, binding.RouteAliases,
            binding.FormationAliases ?? new Dictionary<string, string>(
                StringComparer.Ordinal));
    }

    private static byte[] ApplyParameterOverrides(
        byte[] playbookJson,
        Dictionary<string, int> overrides)
    {
        JsonNode root = JsonNode.Parse(playbookJson)
            ?? throw new InvalidDataException("Tactical playbook is empty.");
        RebakeParameters(root, overrides);
        return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static void RebakeParameters(
        JsonNode node,
        Dictionary<string, int> overrides)
    {
        if (node is JsonObject value)
        {
            if (value["valueParameter"] is JsonValue parameter
                && parameter.TryGetValue(out string? parameterId)
                && parameterId is not null
                && overrides.TryGetValue(parameterId, out int selected))
            {
                value["value"] = selected;
            }
            foreach (KeyValuePair<string, JsonNode?> child in value.ToArray())
            {
                if (child.Value is not null)
                    RebakeParameters(child.Value, overrides);
            }
            return;
        }
        if (node is not JsonArray array)
            return;
        foreach (JsonNode? child in array)
        {
            if (child is not null)
                RebakeParameters(child, overrides);
        }
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

    /// <summary>
    /// Tolerant lookup for a commit withdraw destination: a route yields
    /// its waypoints, a zone its centre, an anchor its tile; an unknown id
    /// yields nothing (the body simply keeps its ordered movement).
    /// </summary>
    internal Position[] WithdrawPoints(string id)
    {
        string resolved = _routeAliases.GetValueOrDefault(id, id);
        if (_routes.TryGetValue(resolved, out Route? route))
        {
            return route.Waypoints
                .Select(value => World(new Position(value[0], value[1])))
                .ToArray();
        }
        if (_zones.TryGetValue(id, out Zone? zone))
        {
            return
            [
                World(new Position(
                    (zone.Rect[0] + zone.Rect[2]) / 2,
                    (zone.Rect[1] + zone.Rect[3]) / 2)),
            ];
        }
        if (_anchors.TryGetValue(id, out Anchor? anchor))
        {
            return
            [
                World(new Position(
                    anchor.Position[0], anchor.Position[1])),
            ];
        }
        return [];
    }

    internal int RouteCorridorWidth(string routeId)
    {
        string resolved = _routeAliases.GetValueOrDefault(routeId, routeId);
        Route route = _routes.GetValueOrDefault(resolved)
            ?? throw new InvalidDataException(
                $"Unknown tactical route '{routeId}'.");
        return route.CorridorWidth;
    }

    internal Formation ResolveFormation(Playbook playbook, string formationId)
    {
        string resolved = _formationAliases.GetValueOrDefault(
            formationId, formationId);
        return playbook.Formations.Single(value => string.Equals(
            value.FormationId, resolved, StringComparison.Ordinal));
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
        string OverflowRoleId,
        string[]? Build = null);

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
        int[] Offset,
        string? Facing = null);

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
        string LockPreemption,
        int MaximumAttackersPerTarget,
        int OverkillDamage,
        int ChaseLeash,
        string AimPreparation,
        string SignatureCoordination,
        DodgeCoverage DodgeCoverage,
        ReleasePolicy Release,
        SelfDefensePolicy SelfDefense,
        HoldFire? HoldFire = null,
        string Posture = "committed",
        Isolation? Isolation = null,
        Commit? Commit = null);

    /// <summary>
    /// Sheet-owned commit discipline (owner design 2026-08, ghost doctrine
    /// v2): when this engagement's participants pick a fight, how far they
    /// chase, and where they break off to. Every predicate reads from the
    /// contract and the mind's remembered-enemy picture.
    /// </summary>
    internal sealed record Commit(
        CommitEngageWhen EngageWhen,
        CommitAwareness? Awareness = null,
        CommitChase? Chase = null,
        CommitDisengageWhen? DisengageWhen = null,
        string[]? Roles = null);

    /// <summary>The threat picture: visible enemies plus remembered
    /// positions no staler than MemoryTicks, within Radius of the body.
    /// </summary>
    internal sealed record CommitAwareness(int Radius, int MemoryTicks);

    /// <summary>Commit only when the picture shows at most MaxThreats and
    /// the candidate dies within KillWithinTicks (ceil(health / own damage)
    /// x own attack cadence). Zero means the gate is not applied.</summary>
    internal sealed record CommitEngageWhen(
        int MaxThreats = 0,
        int KillWithinTicks = 0);

    /// <summary>Chase discipline: OnlyCatchable compares movement cadences
    /// (never chase what you cannot close on); a target at or below
    /// ExecuteBelowHealth suspends the leash - the kill is worth the
    /// ground.</summary>
    internal sealed record CommitChase(
        int Leash = 0,
        bool OnlyCatchable = false,
        int ExecuteBelowHealth = 0);

    /// <summary>Break off when the picture reaches Threats, withdrawing
    /// toward the named route or zone until it thins back to the engage
    /// gate.</summary>
    internal sealed record CommitDisengageWhen(
        int Threats,
        string? WithdrawTo = null);

    /// <summary>
    /// Assassin discipline: participants only acquire targets that have no
    /// ally within the support range - strike the straggler, shadow the
    /// escorted (wellwright carriers measured alone just 12% of ticks).
    /// </summary>
    internal sealed record Isolation(int SupportRange);

    /// <summary>
    /// Ambush discipline: participants acquire no focus and fire nothing
    /// until a target is within the gate distance of a participant, or the
    /// release conditions fire. Self-defense stays live throughout.
    /// </summary>
    internal sealed record HoldFire(
        int WithinDistance,
        ConditionGroup[]? ReleaseAll = null);

    /// <summary>
    /// Accepted for sheet compatibility; INERT since the positional remake
    /// (DECISIONS #212/#213). The declared strike wedge and nearest-body
    /// resolution moved dodge/coverage judgment into the engine, so the
    /// executor no longer reads any of these fields. The engagement's
    /// Posture field is inert the same way (the kite cadence it selected
    /// is gone).
    /// </summary>
    internal sealed record DodgeCoverage(
        string Mode,
        int HorizonTicks,
        int MinimumDirectShots,
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
        ConditionGroup[] SafeConversionAll,
        string[]? EmergencyRecoveryZones = null,
        int EmergencyPickupRadius = 0,
        ConditionGroup[]? EmergencyRecoveryAll = null,
        string[]? EmergencyRecoveryRoles = null,
        string[]? EmergencyRecoverySourceWells = null,
        string? EmergencyRecoveryDisposition = null,
        string? EmergencyDisplacementTarget = null,
        int EmergencyDisplacementReleaseRadius = 0,
        string ForwardPass = "none",
        BaitDrop? BaitDrop = null);

    /// <summary>
    /// Bait delivery: instead of banking, the authorized carrier tosses its
    /// Core into the zone (an uncaught arc-toss landing is a loose Core with
    /// nobody standing on it — a voluntary drop would be re-collected from
    /// the dropper's own tile at the next tick start). While the reclaim
    /// conditions stay false the bait is untouchable to own bodies; once
    /// they fire, normal collection resumes.
    /// </summary>
    internal sealed record BaitDrop(
        string Zone,
        ConditionGroup[] ReclaimAll);

    internal sealed record Order(
        string OrderId,
        string GroupId,
        int Priority,
        MemberSelection Members,
        Movement Movement,
        string FormationId,
        string EngagementId,
        string SupportId,
        string CustodyId,
        string LocalState,
        Fallback Fallback,
        string Stance = "");

    internal sealed record MemberSelection(
        string Kind,
        string[]? Roles,
        string[]? Classes,
        int? Count);

    internal sealed record Movement(
        string Kind,
        string Target,
        int ArrivalRadius,
        string Completion,
        int StuckTicks,
        string StuckRecovery,
        int ChaseLeash,
        string Pace,
        int LeadTiles = 0,
        string? Scan = null);

    internal sealed record Fallback(
        string OnNoPath,
        string OnUnderstrength,
        string OnInvalidTarget,
        string PhaseId);

    internal sealed record Coordination(
        string InitialPhase,
        Phase[] Phases,
        TacticalTask[] Tasks);

    internal sealed record Phase(
        string PhaseId,
        int MinimumTicks,
        string[] OrderIds,
        Transition[] Transitions);

    internal sealed record TacticalTask(
        string TaskId,
        int Priority,
        string Activation,
        string Preemption,
        string ParticipantLoss,
        int TriggerStableTicks,
        int MinimumTicks,
        int TimeoutTicks,
        int CooldownTicks,
        int MinimumPrimaryBodies,
        string[] EligiblePhases,
        TaskAssignment[] Assignments,
        ConditionGroup[] When,
        ConditionGroup[] CompleteWhen,
        ConditionGroup[] FailWhen,
        Reintegration Reintegration,
        int MinimumParticipants = 0,
        int MaximumParticipants = 0,
        ConditionGroup[]? CompletionArmWhen = null,
        string? CompletionArmMode = null,
        string? CompletionReleaseMode = null,
        string? CancellationMode = null);

    internal sealed record TaskAssignment(
        string AssignmentId,
        string OrderId,
        string[] Roles,
        string[] Classes,
        int Minimum,
        int Preferred,
        int Maximum,
        string Carrier,
        SelectionDistance Distance);

    internal sealed record SelectionDistance(
        string Kind,
        string Target,
        int Maximum = 0);

    internal sealed record Reintegration(
        string Mode,
        string[] OrderIds,
        ConditionGroup[] CompleteWhen,
        int TimeoutTicks);

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
        int FreshnessTicks,
        string ValueParameter = "");

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
        Dictionary<string, string> RouteAliases,
        Dictionary<string, string>? FormationAliases = null,
        Dictionary<string, int>? ParameterOverrides = null);

    internal sealed record Zone(string ZoneId, int[] Rect);

    internal sealed record Route(
        string RouteId,
        int[][] Waypoints,
        int CorridorWidth);

    internal sealed record Anchor(string AnchorId, int[] Position);
}
