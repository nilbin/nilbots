using System.Collections.Immutable;
using System.Text.Json;
using BotArena.Sdk;

/// <summary>
/// The ATL2 runtime image. It is a direct parse of the authored Orders DSL;
/// no doctrine, custody, group, engagement, mode, or rung records exist here.
/// </summary>
internal sealed class OrdersPackage
{
    private const int EnvelopeMagic = 0x324C5441; // ATL2.
    private const int OptionsMagic = 0x50455453; // STEP.
    private const int MaximumPayloadBytes = 64 * 1024;
    private const string SheetSchema = "arc-relay-orders-v2";
    private const string MapPlanSchema = "arc-relay-map-plan-v2";

    private readonly Dictionary<string, Zone> _zones;
    private readonly Dictionary<string, Route> _routes;
    private readonly Dictionary<string, Position> _anchors;
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private readonly bool _rotate;

    private OrdersPackage(
        string sheetId,
        string mapPlanId,
        string mapId,
        string sheetSha256,
        string mapPlanSha256,
        Role[] roles,
        Dictionary<string, int> capacities,
        Dictionary<string, Condition> conditions,
        Dictionary<string, Zone> zones,
        Dictionary<string, Route> routes,
        Dictionary<string, Position> anchors,
        int mapWidth,
        int mapHeight,
        bool rotate,
        string stepperMode)
    {
        SheetId = sheetId;
        MapPlanId = mapPlanId;
        MapId = mapId;
        SheetSha256 = sheetSha256;
        MapPlanSha256 = mapPlanSha256;
        Roles = roles;
        Capacities = capacities;
        Conditions = conditions;
        _zones = zones;
        _routes = routes;
        _anchors = anchors;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _rotate = rotate;
        StepperMode = stepperMode;

        var byUnit = new Dictionary<int, Role>();
        int unitId = 0;
        foreach (Role role in roles)
        foreach (string _ in role.Classes)
            byUnit.Add(unitId++, role);
        RolesByUnit = byUnit;
    }

    public string SheetId { get; }
    public string MapPlanId { get; }
    public string MapId { get; }
    public string SheetSha256 { get; }
    public string MapPlanSha256 { get; }
    public Role[] Roles { get; }
    public IReadOnlyDictionary<int, Role> RolesByUnit { get; }
    public IReadOnlyDictionary<string, int> Capacities { get; }
    public IReadOnlyDictionary<string, Condition> Conditions { get; }
    public string StepperMode { get; }

    public static OrdersPackage Load(
        ImmutableArray<byte> linkedData,
        GenericActorResolvedMatchContract contract,
        Position ownReactor)
    {
        if (linkedData.IsDefaultOrEmpty || linkedData.Length > MaximumPayloadBytes)
            throw new InvalidDataException("Arc Relay sheet data is missing or too large.");
        using var reader = new BinaryReader(new MemoryStream(linkedData.ToArray()));
        if (reader.ReadInt32() != EnvelopeMagic)
            throw new InvalidDataException("Unknown Arc Relay sheet envelope.");
        string schema = reader.ReadString();
        if (!string.Equals(schema, SheetSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported Arc Relay sheet '{schema}'.");
        string sheetHash = ReadHash(reader, "sheet");
        string mapPlanHash = ReadHash(reader, "map plan");
        byte[] sheetJson = ReadPayload(reader, "sheet");
        byte[] mapPlanJson = ReadPayload(reader, "map plan");
        string stepperMode = "greedy";
        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            if (reader.ReadInt32() != OptionsMagic)
                throw new InvalidDataException("Trailing Arc Relay sheet data.");
            stepperMode = reader.ReadString();
            if (stepperMode is not ("greedy" or "coordinated"))
                throw new InvalidDataException($"Unknown mind stepper '{stepperMode}'.");
        }
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Trailing Arc Relay sheet data.");

        using JsonDocument sheetDocument = JsonDocument.Parse(sheetJson);
        using JsonDocument mapPlanDocument = JsonDocument.Parse(mapPlanJson);
        JsonElement sheet = sheetDocument.RootElement;
        JsonElement mapPlan = mapPlanDocument.RootElement;
        if (sheet.GetProperty("schema").GetString() != SheetSchema
            || mapPlan.GetProperty("schema").GetString() != MapPlanSchema)
            throw new InvalidDataException("Arc Relay sheet schemas are invalid.");
        string mapId = mapPlan.GetProperty("mapId").GetString()!;
        if (!string.Equals(mapId, contract.Map.MapId, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Map plan '{mapId}' cannot run on '{contract.Map.MapId}'.");

        Dictionary<string, JsonElement> shared = sheet.GetProperty("sharedOrders")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => value.Value,
                StringComparer.Ordinal);
        var roles = new List<Role>();
        foreach (JsonElement role in sheet.GetProperty("roles").EnumerateArray())
        {
            string id = role.GetProperty("id").GetString()!;
            string[] classes = role.GetProperty("classes").EnumerateArray()
                .Select(value => value.GetString()!).ToArray();
            string[] upgrades = role.GetProperty("upgradePath").EnumerateArray()
                .Select(value => value.GetString()!).ToArray();
            OrderNode[] orders = ParseOrders(
                role.GetProperty("orders"), shared, id, id, branchDepth: 0,
                inheritedUrgency: TacticalMovementUrgency.Normal);
            roles.Add(new Role(id, classes, upgrades, orders));
        }
        Dictionary<string, int> capacities = sheet.GetProperty("capacities")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => value.Value.GetInt32(),
                StringComparer.Ordinal);
        Dictionary<string, Condition> conditions = sheet.GetProperty("conditions")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => ParseCondition(value.Name, value.Value),
                StringComparer.Ordinal);
        Dictionary<string, Zone> zones = mapPlan.GetProperty("zones")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => new Zone(value.Name,
                    value.Value.GetProperty("rect").EnumerateArray()
                        .Select(point => point.GetInt32()).ToArray()),
                StringComparer.Ordinal);
        Dictionary<string, Route> routes = mapPlan.GetProperty("routes")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => new Route(value.Name,
                    value.Value.GetProperty("waypoints").EnumerateArray()
                        .Select(Point).ToArray()),
                StringComparer.Ordinal);
        Dictionary<string, Position> anchors = mapPlan.GetProperty("anchors")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => Point(value.Value),
                StringComparer.Ordinal);
        bool rotate = ownReactor.X > (contract.Map.Width - 1) / 2;
        ValidateGeometry(contract.Map, zones, routes, anchors, rotate);
        return new OrdersPackage(
            sheet.GetProperty("sheetId").GetString()!,
            mapPlan.GetProperty("mapPlanId").GetString()!,
            mapId,
            sheetHash,
            mapPlanHash,
            [.. roles],
            capacities,
            conditions,
            zones,
            routes,
            anchors,
            contract.Map.Width,
            contract.Map.Height,
            rotate,
            stepperMode);
    }

    public bool Contains(string zoneId, Position world)
    {
        Zone zone = _zones[zoneId];
        Position point = Canonical(world);
        return point.X >= zone.Rect[0] && point.X <= zone.Rect[2]
            && point.Y >= zone.Rect[1] && point.Y <= zone.Rect[3];
    }

    public Position[] ZoneTiles(
        string zoneId,
        GenericActorMapContract map)
    {
        Zone zone = _zones[zoneId];
        var result = new List<Position>();
        for (int y = zone.Rect[1]; y <= zone.Rect[3]; y++)
        for (int x = zone.Rect[0]; x <= zone.Rect[2]; x++)
        {
            Position world = World(new Position(x, y));
            if (ArenaBasics.CanEnter(map, world))
                result.Add(world);
        }
        return [.. result];
    }

    public Position ZoneCenter(string zoneId)
    {
        Zone zone = _zones[zoneId];
        return World(new Position(
            (zone.Rect[0] + zone.Rect[2]) / 2,
            (zone.Rect[1] + zone.Rect[3]) / 2));
    }

    public Position[] RoutePoints(string routeId) =>
        _routes[routeId].Waypoints.Select(World).ToArray();

    public Position AnchorPosition(string anchorId) => World(_anchors[anchorId]);

    private Position Canonical(Position position) => _rotate
        ? new Position(_mapWidth - 1 - position.X, _mapHeight - 1 - position.Y)
        : position;

    private Position World(Position position) => Canonical(position);

    private static OrderNode[] ParseOrders(
        JsonElement values,
        IReadOnlyDictionary<string, JsonElement> shared,
        string roleId,
        string parentPath,
        int branchDepth,
        TacticalMovementUrgency inheritedUrgency) => values.EnumerateArray()
        .Select(value => ParseOrder(
            value, shared, roleId, parentPath, branchDepth, externalId: null,
            inheritedUrgency))
        .ToArray();

    private static OrderNode ParseOrder(
        JsonElement value,
        IReadOnlyDictionary<string, JsonElement> shared,
        string roleId,
        string parentPath,
        int branchDepth,
        string? externalId,
        TacticalMovementUrgency inheritedUrgency)
    {
        if (value.TryGetProperty("shared", out JsonElement reference))
        {
            string sharedId = reference.GetString()!;
            return ParseOrder(
                shared[sharedId], shared, roleId, parentPath, branchDepth,
                sharedId, inheritedUrgency);
        }
        string id = externalId ?? value.GetProperty("id").GetString()!;
        string path = $"{parentPath} > {id}";
        ConditionExpression? when = value.TryGetProperty(
            "when", out JsonElement whenValue)
                ? ParseExpression(whenValue)
                : null;
        ConditionExpression? until = value.TryGetProperty(
            "until", out JsonElement untilValue)
                ? ParseExpression(untilValue)
                : null;
        int? timeout = value.TryGetProperty("timeout", out JsonElement timeoutValue)
            ? timeoutValue.GetInt32()
            : null;
        int cooldown = value.TryGetProperty("cooldown", out JsonElement cooldownValue)
            ? cooldownValue.GetInt32()
            : 0;
        TacticalMovementUrgency urgency = value.TryGetProperty(
            "urgency", out JsonElement urgencyValue)
                ? ParseUrgency(urgencyValue.GetString()!)
                : inheritedUrgency;
        Capacity? capacity = value.TryGetProperty(
            "capacity", out JsonElement capacityValue)
                ? capacityValue.ValueKind == JsonValueKind.String
                    ? new Capacity(capacityValue.GetString()!, null)
                    : new Capacity($"local:{roleId}:{path}",
                        capacityValue.GetInt32())
                : null;
        if (value.TryGetProperty("orders", out JsonElement children))
        {
            return new OrderNode(
                id, path, when, until, timeout, cooldown, urgency, null,
                ParseOrders(
                    children, shared, roleId, path, branchDepth + 1, urgency),
                null);
        }
        OrderAction action = value.TryGetProperty("move", out JsonElement move)
            ? ParseMove(move)
            : value.TryGetProperty("fight", out JsonElement fight)
                ? ParseFight(fight)
                : value.TryGetProperty("use", out JsonElement use)
                    ? new UseAction(
                        use.GetProperty("skill").GetString()!,
                        use.GetProperty("target").GetString()!)
                    : value.TryGetProperty("handoff", out JsonElement handoff)
                        ? ParseHandoff(handoff)
                        : new DropAction();
        return new OrderNode(
            id, path, when, until, timeout, cooldown, urgency, capacity, [], action);
    }

    private static TacticalMovementUrgency ParseUrgency(string value) =>
        value switch
        {
            "routine" => TacticalMovementUrgency.Routine,
            "normal" => TacticalMovementUrgency.Normal,
            "urgent" => TacticalMovementUrgency.Urgent,
            _ => throw new InvalidDataException(
                $"Unknown movement urgency '{value}'."),
        };

    private static MoveAction ParseMove(JsonElement value)
    {
        string? via = value.TryGetProperty("via", out JsonElement viaValue)
            ? viaValue.GetString()
            : null;
        string? posture = value.TryGetProperty("posture", out JsonElement postureValue)
            ? postureValue.GetString()
            : null;
        if (value.TryGetProperty("patrol", out JsonElement patrol))
            return new MoveAction("patrol", "route", patrol.GetString()!, via, posture);
        if (value.TryGetProperty("follow", out JsonElement follow))
            return new MoveAction("follow", "role", follow.GetString()!, null, posture);
        JsonElement to = value.GetProperty("to");
        if (to.ValueKind == JsonValueKind.String)
            return new MoveAction("to", "dynamic", to.GetString()!, via, posture);
        JsonProperty target = to.EnumerateObject().Single();
        return new MoveAction("to", target.Name, target.Value.GetString()!, via, posture);
    }

    private static FightAction ParseFight(JsonElement value) => new(
        value.GetProperty("targets").EnumerateArray()
            .Select(target => target.GetString()!).ToArray(),
        value.GetProperty("style").GetString()!,
        value.GetProperty("chaseLimit").GetInt32());

    private static HandoffAction ParseHandoff(JsonElement value)
    {
        JsonElement target = value.GetProperty("target");
        return target.ValueKind == JsonValueKind.String
            ? new HandoffAction("selector", target.GetString()!)
            : new HandoffAction("role", target.GetProperty("role").GetString()!);
    }

    private static ConditionExpression ParseExpression(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return new ConditionExpression("one", [value.GetString()!]);
        JsonProperty expression = value.EnumerateObject().Single();
        return new ConditionExpression(
            expression.Name,
            expression.Value.EnumerateArray()
                .Select(name => name.GetString()!).ToArray());
    }

    private static Condition ParseCondition(string id, JsonElement value) => new(
        id,
        value.GetProperty("fact").GetString()!,
        value.GetProperty("operator").GetString()!,
        value.GetProperty("value").GetInt32(),
        Optional(value, "role"),
        Optional(value, "zone"),
        Optional(value, "well"),
        value.TryGetProperty("freshnessTicks", out JsonElement freshness)
            ? freshness.GetInt32()
            : null);

    private static string? Optional(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property)
            ? property.GetString()
            : null;

    private static Position Point(JsonElement value)
    {
        int[] coordinates = value.EnumerateArray()
            .Select(point => point.GetInt32()).ToArray();
        return new Position(coordinates[0], coordinates[1]);
    }

    private static void ValidateGeometry(
        GenericActorMapContract map,
        IReadOnlyDictionary<string, Zone> zones,
        IReadOnlyDictionary<string, Route> routes,
        IReadOnlyDictionary<string, Position> anchors,
        bool rotate)
    {
        Position World(Position position) => rotate
            ? new Position(map.Width - 1 - position.X, map.Height - 1 - position.Y)
            : position;
        foreach ((string id, Zone zone) in zones)
        {
            Position first = World(new Position(zone.Rect[0], zone.Rect[1]));
            Position last = World(new Position(zone.Rect[2], zone.Rect[3]));
            if (!InBounds(map, first) || !InBounds(map, last))
                throw new InvalidDataException($"Zone '{id}' leaves the map.");
            bool hasFloor = false;
            for (int y = zone.Rect[1]; y <= zone.Rect[3]; y++)
            for (int x = zone.Rect[0]; x <= zone.Rect[2]; x++)
                hasFloor |= ArenaBasics.CanEnter(map, World(new Position(x, y)));
            if (!hasFloor)
                throw new InvalidDataException($"Zone '{id}' contains no floor.");
        }
        foreach ((string id, Route route) in routes)
        foreach (Position waypoint in route.Waypoints)
        {
            Position world = World(waypoint);
            if (!InBounds(map, world) || !ArenaBasics.CanEnter(map, world))
                throw new InvalidDataException(
                    $"Route '{id}' has blocked waypoint {waypoint}.");
        }
        foreach ((string id, Position anchor) in anchors)
        {
            Position world = World(anchor);
            if (!InBounds(map, world) || !ArenaBasics.CanEnter(map, world))
                throw new InvalidDataException(
                    $"Anchor '{id}' is not traversable.");
        }
    }

    private static bool InBounds(GenericActorMapContract map, Position value) =>
        value.X >= 0 && value.X < map.Width && value.Y >= 0 && value.Y < map.Height;

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

    internal sealed record Role(
        string Id,
        string[] Classes,
        string[] UpgradePath,
        OrderNode[] Orders);

    internal sealed record OrderNode(
        string Id,
        string Path,
        ConditionExpression? When,
        ConditionExpression? Until,
        int? Timeout,
        int Cooldown,
        TacticalMovementUrgency Urgency,
        Capacity? Capacity,
        OrderNode[] Children,
        OrderAction? Action);

    internal sealed record ConditionExpression(string Kind, string[] Names);
    internal sealed record Condition(
        string Id,
        string Fact,
        string Operator,
        int Value,
        string? Role,
        string? Zone,
        string? Well,
        int? FreshnessTicks);
    internal sealed record Capacity(string Id, int? InlineValue);

    internal abstract record OrderAction;
    internal sealed record MoveAction(
        string Kind,
        string TargetKind,
        string Target,
        string? Via,
        string? Posture) : OrderAction;
    internal sealed record FightAction(
        string[] Targets,
        string Style,
        int ChaseLimit) : OrderAction;
    internal sealed record UseAction(string Skill, string Target) : OrderAction;
    internal sealed record HandoffAction(string TargetKind, string Target)
        : OrderAction;
    internal sealed record DropAction : OrderAction;

    private sealed record Zone(string Id, int[] Rect);
    private sealed record Route(string Id, Position[] Waypoints);
}
