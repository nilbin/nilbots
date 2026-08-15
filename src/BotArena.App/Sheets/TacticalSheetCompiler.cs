using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.ArcRelay;
using BotArena.Engine;
using BotArena.TacticalPlaybooks;

namespace BotArena.App.Sheets;

public sealed record ValidatedTacticalSheet(
    TacticalPlaybookCompilation Compilation,
    string ContentHash,
    ArcRelayCompositionCompilation Composition);

/// <summary>The only hosted validation path for tactical-sheet source.</summary>
public sealed class TacticalSheetCompiler(ArcRelayClassCatalog classCatalog)
{
    public const int MaximumOrdersBytes = 192 * 1024;
    public const int MaximumMapPlanBytes = 96 * 1024;

    public ValidatedTacticalSheet Compile(
        string ordersJson,
        string mapPlanJson,
        IReadOnlySet<string> unlockedClassIds,
        string diagnosticName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ordersJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapPlanJson);
        byte[] orders = Encoding.UTF8.GetBytes(ordersJson);
        byte[] mapPlan = Encoding.UTF8.GetBytes(mapPlanJson);
        if (orders.Length > MaximumOrdersBytes)
            throw new InvalidDataException(
                $"Orders document exceeds {MaximumOrdersBytes} UTF-8 bytes.");
        if (mapPlan.Length > MaximumMapPlanBytes)
            throw new InvalidDataException(
                $"Map plan exceeds {MaximumMapPlanBytes} UTF-8 bytes.");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                orders,
                mapPlan,
                $"{diagnosticName}/orders.json",
                $"{diagnosticName}/map-plan.json");
        string mapId = MapPlanMapId(compilation.NormalizedLayout);
        if (!string.Equals(
                mapId,
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Map plan targets '{mapId}'. Hosted Arc Relay requires "
                + $"the native '{ArcRelayLoopProfile.Current.MapId}' "
                + "Tapline plan; geometry from older maps is never migrated.");
        }
        ValidateMapPlanGeometry(
            compilation.NormalizedPlaybook,
            compilation.NormalizedLayout,
            ArcRelayH0Definition.CreateMap(ArcRelayLoopProfile.Current));
        ArcRelayCompositionCompilation composition = ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(compilation.Composition),
            classCatalog,
            unlockedClassIds);
        int minimumDistinctClasses = ((ArcRelayGameModeDefinition)
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.Current).GameMode).MinimumDistinctClasses;
        int distinctClasses = compilation.Composition.Distinct(
            StringComparer.Ordinal).Count();
        if (distinctClasses < minimumDistinctClasses)
        {
            throw new InvalidDataException(
                $"Current Arc Relay requires at least {minimumDistinctClasses} "
                + $"distinct classes; this sheet fields {distinctClasses}.");
        }
        return new ValidatedTacticalSheet(
            compilation,
            Convert.ToHexStringLower(SHA256.HashData(compilation.LinkedData)),
            composition);
    }

    public static bool IsAdmissibleToCurrentPlaylist(string mapPlanJson)
    {
        try
        {
            return string.Equals(
                MapPlanMapId(Encoding.UTF8.GetBytes(mapPlanJson)),
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the exact eight-body class row from an already validated Orders
    /// document. Roles are the single composition source in Orders v2.
    /// </summary>
    public static IReadOnlyList<string> ReadRoleClasses(string ordersJson)
    {
        using JsonDocument document = JsonDocument.Parse(ordersJson);
        return document.RootElement.GetProperty("roles")
            .EnumerateArray()
            .SelectMany(role => role.GetProperty("classes").EnumerateArray())
            .Select(value => value.GetString()
                ?? throw new InvalidDataException(
                    "An Orders role contains a null class id."))
            .ToArray();
    }

    private static string MapPlanMapId(ReadOnlyMemory<byte> mapPlanJson)
    {
        using JsonDocument document = JsonDocument.Parse(mapPlanJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(
                "mapId", out JsonElement mapId)
            || mapId.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(mapId.GetString()))
        {
            throw new InvalidDataException("Map plan has no mapId.");
        }
        return mapId.GetString()!;
    }

    private static void ValidateMapPlanGeometry(
        ReadOnlyMemory<byte> ordersJson,
        ReadOnlyMemory<byte> mapPlanJson,
        ActorMapDefinition map)
    {
        using JsonDocument ordersDocument = JsonDocument.Parse(ordersJson);
        using JsonDocument document = JsonDocument.Parse(mapPlanJson);
        JsonElement root = document.RootElement;

        bool IsFloor(int x, int y) =>
            x >= 0 && y >= 0 && x < map.Width && y < map.Height
            && !map.IsWall(x, y);
        bool IsFloorForBothSides(int x, int y) =>
            IsFloor(x, y)
            && IsFloor(map.Width - 1 - x, map.Height - 1 - y);
        HashSet<Position> reservedSpawns = map.SpawnAnchors
            .Select(value => value.Spawn.Position)
            .ToHashSet();
        HashSet<Position> spawnProtected = map.TileTags
            .Where(value => value.Kind
                == ActorMapTileTagDefinition.TileTagKind.SpawnProtected)
            .SelectMany(value => value.Tiles)
            .ToHashSet();
        HashSet<Position> westProtected = TaggedComponentContaining(
            spawnProtected,
            map.Regions.Single(value => value.RegionId == "reactor-west")
                .Tiles.Single());
        HashSet<Position> eastProtected = TaggedComponentContaining(
            spawnProtected,
            map.Regions.Single(value => value.RegionId == "reactor-east")
                .Tiles.Single());
        var westForbidden = new HashSet<Position>(westProtected);
        westForbidden.UnionWith(map.Regions.Single(
            value => value.RegionId == "home-west").Tiles);
        var eastForbidden = new HashSet<Position>(eastProtected);
        eastForbidden.UnionWith(map.Regions.Single(
            value => value.RegionId == "home-east").Tiles);
        bool IsReservedSpawnForEitherSide(int x, int y) =>
            reservedSpawns.Contains(new Position(x, y))
            || reservedSpawns.Contains(new Position(
                map.Width - 1 - x,
                map.Height - 1 - y));
        bool IsOpponentProtectedForEitherBinding(int x, int y) =>
            eastForbidden.Contains(new Position(x, y))
            || westForbidden.Contains(new Position(
                map.Width - 1 - x,
                map.Height - 1 - y));

        foreach (JsonProperty route in root.GetProperty("routes")
                     .EnumerateObject())
        {
            int index = 0;
            foreach (JsonElement waypoint in route.Value
                         .GetProperty("waypoints").EnumerateArray())
            {
                int[] point = waypoint.EnumerateArray()
                    .Select(value => value.GetInt32()).ToArray();
                bool bothSidesFloor = IsFloorForBothSides(point[0], point[1]);
                bool reserved = IsReservedSpawnForEitherSide(
                    point[0], point[1]);
                bool protectedTile = IsOpponentProtectedForEitherBinding(
                    point[0], point[1]);
                if (!bothSidesFloor || reserved || protectedTile)
                {
                    string reason = !bothSidesFloor
                        ? "not traversable for both team bindings"
                        : reserved
                            ? "a reserved body spawn"
                            : "inside the opponent's protected home region";
                    throw new InvalidDataException(
                        $"Route '{route.Name}' waypoint {index} "
                        + $"({point[0]},{point[1]}) is {reason}; route "
                        + "waypoints must be legal for either team side.");
                }
                index++;
            }
        }

        foreach (JsonProperty anchor in root.GetProperty("anchors")
                     .EnumerateObject())
        {
            int[] point = anchor.Value.EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (!IsFloorForBothSides(point[0], point[1])
                || IsReservedSpawnForEitherSide(point[0], point[1]))
            {
                throw new InvalidDataException(
                    $"Anchor '{anchor.Name}' ({point[0]},{point[1]}) "
                    + "must be traversable and not a reserved body spawn "
                    + "for either team side.");
            }
        }

        foreach (JsonProperty zone in root.GetProperty("zones")
                     .EnumerateObject())
        {
            int[] rect = zone.Value.GetProperty("rect").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            bool westHasFloor = false;
            bool eastHasFloor = false;
            for (int y = rect[1]; y <= rect[3]; y++)
            for (int x = rect[0]; x <= rect[2]; x++)
            {
                westHasFloor |= IsFloor(x, y);
                eastHasFloor |= IsFloor(
                    map.Width - 1 - x,
                    map.Height - 1 - y);
            }
            if (!westHasFloor || !eastHasFloor)
            {
                throw new InvalidDataException(
                    $"Zone '{zone.Name}' must contain traversable floor "
                    + "for both team sides.");
            }
        }

        IReadOnlyDictionary<string, int> zoneDemand = MovementZoneDemand(
            ordersDocument.RootElement);
        foreach ((string zoneId, int required) in zoneDemand)
        {
            JsonElement zone = root.GetProperty("zones").GetProperty(zoneId);
            int[] rect = zone.GetProperty("rect").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (ZoneContainsAny(
                    map, rect, mirror: false, reservedSpawns, eastForbidden)
                || ZoneContainsAny(
                    map, rect, mirror: true, reservedSpawns, westForbidden))
            {
                throw new InvalidDataException(
                    $"Movement zone '{zoneId}' overlaps a reserved spawn or "
                    + "the opponent's protected home region in at least one "
                    + "team binding.");
            }
            int west = LargestLegalZoneComponent(
                map, rect, mirror: false, reservedSpawns, eastForbidden);
            int east = LargestLegalZoneComponent(
                map, rect, mirror: true, reservedSpawns, westForbidden);
            int available = Math.Min(west, east);
            if (available < required)
            {
                throw new InvalidDataException(
                    $"Movement zone '{zoneId}' provides {available} connected "
                    + "legal staging tiles, but the order tree can send "
                    + $"{required} bodies there. Enlarge or relocate the zone, "
                    + "or add an authored capacity.");
            }
        }
    }

    /// <summary>
    /// Returns a conservative simultaneous-body floor for each authored move
    /// zone. Unbounded and inline-capacity leaves add across roles. A named
    /// capacity is shared by target at runtime, so it is counted once. The
    /// larger of those two independently real demands is enough to reject the
    /// common impossible-layout fault without pretending conditions are a
    /// theorem prover.
    /// </summary>
    private static IReadOnlyDictionary<string, int> MovementZoneDemand(
        JsonElement root)
    {
        IReadOnlyDictionary<string, JsonElement> shared = root
            .GetProperty("sharedOrders")
            .EnumerateObject()
            .ToDictionary(value => value.Name, value => value.Value,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> capacities = root
            .GetProperty("capacities")
            .EnumerateObject()
            .ToDictionary(value => value.Name, value => value.Value.GetInt32(),
                StringComparer.Ordinal);
        var unscopedByZone = new Dictionary<string, int>(StringComparer.Ordinal);
        var namedByZoneScope = new Dictionary<
            (string Zone, string Scope), int>();

        foreach (JsonElement role in root.GetProperty("roles").EnumerateArray())
        {
            int bodies = role.GetProperty("classes").GetArrayLength();
            var roleUnscoped = new Dictionary<string, int>(StringComparer.Ordinal);
            var roleNamed = new Dictionary<(string Zone, string Scope), int>();
            foreach (JsonElement order in role.GetProperty("orders")
                         .EnumerateArray())
            {
                VisitMovementZones(
                    order, bodies, shared, capacities, roleUnscoped, roleNamed,
                    new HashSet<string>(StringComparer.Ordinal));
            }
            foreach ((string zone, int count) in roleUnscoped)
            {
                unscopedByZone[zone] = unscopedByZone.GetValueOrDefault(zone)
                    + count;
            }
            foreach (KeyValuePair<(string Zone, string Scope), int> pair in
                     roleNamed)
            {
                // A role can consume the same named capacity through several
                // mutually exclusive rungs, so VisitMovementZones already
                // collapses those to one role-local maximum. Different roles
                // are independent claimants and therefore add until the one
                // shared capacity clamps their total.
                namedByZoneScope[pair.Key] =
                    namedByZoneScope.GetValueOrDefault(pair.Key) + pair.Value;
            }
        }

        Dictionary<string, int> namedByZone = namedByZoneScope
            .GroupBy(value => value.Key.Zone, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => Math.Min(
                    capacities[value.Key.Scope], value.Value)),
                StringComparer.Ordinal);
        return unscopedByZone.Keys.Concat(namedByZone.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                zone => zone,
                zone => Math.Max(
                    unscopedByZone.GetValueOrDefault(zone),
                    namedByZone.GetValueOrDefault(zone)),
                StringComparer.Ordinal);
    }

    private static void VisitMovementZones(
        JsonElement order,
        int bodies,
        IReadOnlyDictionary<string, JsonElement> shared,
        IReadOnlyDictionary<string, int> capacities,
        Dictionary<string, int> unscoped,
        Dictionary<(string Zone, string Scope), int> named,
        HashSet<string> sharedStack)
    {
        if (order.TryGetProperty("shared", out JsonElement sharedName))
        {
            string id = sharedName.GetString()!;
            if (!sharedStack.Add(id))
                return; // The shared compiler reports the cycle precisely.
            VisitMovementZones(
                shared[id], bodies, shared, capacities, unscoped, named,
                sharedStack);
            sharedStack.Remove(id);
            return;
        }

        if (order.TryGetProperty("move", out JsonElement move)
            && move.TryGetProperty("to", out JsonElement to)
            && to.ValueKind == JsonValueKind.Object
            && to.TryGetProperty("zone", out JsonElement zoneValue))
        {
            string zone = zoneValue.GetString()!;
            int limit = bodies;
            if (order.TryGetProperty("capacity", out JsonElement capacity))
            {
                if (capacity.ValueKind == JsonValueKind.Number)
                    limit = Math.Min(bodies, capacity.GetInt32());
                else
                {
                    string scope = capacity.GetString()!;
                    limit = Math.Min(bodies, capacities[scope]);
                    var key = (zone, scope);
                    named[key] = Math.Max(named.GetValueOrDefault(key), limit);
                    return;
                }
            }
            unscoped[zone] = Math.Max(unscoped.GetValueOrDefault(zone), limit);
        }

        if (order.TryGetProperty("orders", out JsonElement children))
        {
            foreach (JsonElement child in children.EnumerateArray())
            {
                VisitMovementZones(
                    child, bodies, shared, capacities, unscoped, named,
                    sharedStack);
            }
        }
    }

    private static int LargestLegalZoneComponent(
        ActorMapDefinition map,
        IReadOnlyList<int> rect,
        bool mirror,
        IReadOnlySet<Position> reservedSpawns,
        IReadOnlySet<Position> opponentProtected)
    {
        var legal = new HashSet<Position>();
        for (int y = rect[1]; y <= rect[3]; y++)
        for (int x = rect[0]; x <= rect[2]; x++)
        {
            Position tile = mirror
                ? new Position(map.Width - 1 - x, map.Height - 1 - y)
                : new Position(x, y);
            if (!map.IsWall(tile)
                && !reservedSpawns.Contains(tile)
                && !opponentProtected.Contains(tile))
            {
                legal.Add(tile);
            }
        }

        int largest = 0;
        var unseen = new HashSet<Position>(legal);
        while (unseen.Count > 0)
        {
            Position start = unseen.First();
            unseen.Remove(start);
            var queue = new Queue<Position>();
            queue.Enqueue(start);
            int count = 0;
            while (queue.Count > 0)
            {
                Position current = queue.Dequeue();
                count++;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var next = new Position(current.X + dx, current.Y + dy);
                    if (!unseen.Contains(next))
                        continue;
                    if (dx != 0 && dy != 0
                        && (map.IsWall(current.X + dx, current.Y)
                            || map.IsWall(current.X, current.Y + dy)))
                    {
                        continue;
                    }
                    unseen.Remove(next);
                    queue.Enqueue(next);
                }
            }
            largest = Math.Max(largest, count);
        }
        return largest;
    }

    private static bool ZoneContainsAny(
        ActorMapDefinition map,
        IReadOnlyList<int> rect,
        bool mirror,
        IReadOnlySet<Position> reservedSpawns,
        IReadOnlySet<Position> opponentProtected)
    {
        for (int y = rect[1]; y <= rect[3]; y++)
        for (int x = rect[0]; x <= rect[2]; x++)
        {
            Position tile = mirror
                ? new Position(map.Width - 1 - x, map.Height - 1 - y)
                : new Position(x, y);
            if (reservedSpawns.Contains(tile)
                || opponentProtected.Contains(tile))
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<Position> TaggedComponentContaining(
        IReadOnlySet<Position> tagged,
        Position start)
    {
        if (!tagged.Contains(start))
            throw new InvalidDataException(
                $"Reactor tile {start} is outside its protected spawn pad.");
        var result = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            foreach ((int dx, int dy) in new[]
                     {
                         (-1, 0), (1, 0), (0, -1), (0, 1),
                     })
            {
                var next = new Position(current.X + dx, current.Y + dy);
                if (tagged.Contains(next) && result.Add(next))
                    queue.Enqueue(next);
            }
        }
        return result;
    }
}
