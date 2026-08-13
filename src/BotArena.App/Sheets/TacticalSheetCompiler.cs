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
            compilation.NormalizedLayout,
            ArcRelayH0Definition.CreateMap(ArcRelayLoopProfile.Current));
        ArcRelayCompositionCompilation composition = ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(compilation.Composition),
            classCatalog,
            unlockedClassIds);
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
        ReadOnlyMemory<byte> mapPlanJson,
        ActorMapDefinition map)
    {
        using JsonDocument document = JsonDocument.Parse(mapPlanJson);
        JsonElement root = document.RootElement;

        bool IsFloor(int x, int y) =>
            x >= 0 && y >= 0 && x < map.Width && y < map.Height
            && !map.IsWall(x, y);
        bool IsFloorForBothSides(int x, int y) =>
            IsFloor(x, y)
            && IsFloor(map.Width - 1 - x, map.Height - 1 - y);

        foreach (JsonProperty route in root.GetProperty("routes")
                     .EnumerateObject())
        {
            int index = 0;
            foreach (JsonElement waypoint in route.Value
                         .GetProperty("waypoints").EnumerateArray())
            {
                int[] point = waypoint.EnumerateArray()
                    .Select(value => value.GetInt32()).ToArray();
                if (!IsFloorForBothSides(point[0], point[1]))
                {
                    throw new InvalidDataException(
                        $"Route '{route.Name}' waypoint {index} "
                        + $"({point[0]},{point[1]}) must be traversable "
                        + "for both team sides.");
                }
                index++;
            }
        }

        foreach (JsonProperty anchor in root.GetProperty("anchors")
                     .EnumerateObject())
        {
            int[] point = anchor.Value.EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            if (!IsFloorForBothSides(point[0], point[1]))
            {
                throw new InvalidDataException(
                    $"Anchor '{anchor.Name}' ({point[0]},{point[1]}) "
                    + "must be traversable for both team sides.");
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
    }
}
