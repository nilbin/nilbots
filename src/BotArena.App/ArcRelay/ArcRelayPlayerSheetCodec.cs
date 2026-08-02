using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine;

namespace BotArena.App.ArcRelay;

/// <summary>
/// Canonical product sheet codec. This is the player-facing document, not the
/// provisional Gate 3 evaluation JSON. Compilation produces an internal ARS1
/// data link using the frozen evaluation envelope so the stock algorithm and
/// its admission-tested WASM artifact remain byte-identical and build-once.
/// </summary>
public sealed class ArcRelayPlayerSheetCodec(ArcRelayClassCatalog catalog)
{
    public const int SchemaVersion = 1;
    public const string SchemaId = "arc-relay-player-sheet-v1";
    public const int SlotCount = 8;
    public const int MaximumCopiesPerClass = 2;
    public const int MaximumGambits = 3;

    public static IReadOnlyList<string> SupportedTheaters { get; } =
        ["north", "centre", "south", "reserve"];
    public static IReadOnlyList<string> SupportedRoles { get; } =
        ["carrier", "screen", "intercept", "reserve"];
    public static IReadOnlyList<string> SupportedTriggers { get; } =
        [
            "after-enemy-pulse",
            "after-own-pulse",
            "double-enemy-possession",
        ];

    private static readonly HashSet<string> Theaters = new(
        SupportedTheaters,
        StringComparer.Ordinal);
    private static readonly HashSet<string> Roles = new(
        SupportedRoles,
        StringComparer.Ordinal);
    private static readonly HashSet<string> Triggers = new(
        SupportedTriggers,
        StringComparer.Ordinal);

    public ArcRelaySheetCompilation Compile(
        ArcRelaySheetDocument document,
        IReadOnlySet<string> unlockedClassIds,
        string sheetIdentity)
    {
        Validate(document, unlockedClassIds);
        byte[] canonicalUtf8 = WriteCanonical(document);
        string hash = Convert.ToHexStringLower(SHA256.HashData(canonicalUtf8));
        byte[] linkedData = Link(document, sheetIdentity, hash);
        return new ArcRelaySheetCompilation(
            Encoding.UTF8.GetString(canonicalUtf8),
            hash,
            linkedData,
            document.Slots.OrderBy(slot => slot.UnitId)
                .Select(slot => slot.ClassId).ToArray());
    }

    public ArcRelaySheetDocument Read(string canonicalJson) =>
        JsonSerializer.Deserialize<ArcRelaySheetDocument>(
            canonicalJson,
            JsonOptions)
        ?? throw new InvalidDataException("Arc Relay sheet JSON is empty.");

    public void Validate(
        ArcRelaySheetDocument document,
        IReadOnlySet<string> unlockedClassIds)
        => ValidateForProfile(document, unlockedClassIds, ArcRelayLoopProfile.Current);

    /// <summary>
    /// Deterministically advances a saved Home Gates sheet to the current map.
    /// Waypoints that became cover move to the nearest walkable tile with a
    /// stable Manhattan-distance, Y, X tie-break; all other authored data stays
    /// unchanged.
    /// </summary>
    public ArcRelaySheetDocument UpgradeToCurrentMap(
        ArcRelaySheetDocument document,
        IReadOnlySet<string> unlockedClassIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.Equals(
                document.MapId,
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal))
        {
            Validate(document, unlockedClassIds);
            return document;
        }
        if (!string.Equals(
                document.MapId,
                ArcRelayLoopProfile.HomeGatesWide.MapId,
                StringComparison.Ordinal))
        {
            throw Invalid(
                "mapId",
                $"cannot migrate unsupported map '{document.MapId}'");
        }

        ValidateForProfile(
            document,
            unlockedClassIds,
            ArcRelayLoopProfile.HomeGatesWide);
        ActorMapDefinition currentMap = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        ArcRelaySheetDocument migrated = document with
        {
            MapId = ArcRelayLoopProfile.Current.MapId,
            Slots = document.Slots.Select(slot => slot with
            {
                OutboundPath = RelocatePath(slot.OutboundPath, currentMap),
                ReturnPath = RelocatePath(slot.ReturnPath, currentMap),
            }).ToArray(),
            RallyLines = document.RallyLines.Select(line => line with
            {
                Points = RelocatePath(line.Points, currentMap),
            }).ToArray(),
        };
        Validate(migrated, unlockedClassIds);
        return migrated;
    }

    private void ValidateForProfile(
        ArcRelaySheetDocument document,
        IReadOnlySet<string> unlockedClassIds,
        ArcRelayLoopProfile loopProfile)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(unlockedClassIds);
        if (document.SchemaVersion != SchemaVersion)
            throw Invalid("schemaVersion", $"must be {SchemaVersion}");
        if (!string.Equals(
                document.MapId,
                loopProfile.MapId,
                StringComparison.Ordinal))
        {
            throw Invalid(
                "mapId",
                $"must be '{loopProfile.MapId}'");
        }
        if (document.Slots is null || document.Slots.Count != SlotCount)
            throw Invalid("slots", $"must contain exactly {SlotCount} entries");
        ArcRelaySheetSlot[] slots = document.Slots.OrderBy(slot => slot.UnitId)
            .ToArray();
        if (!slots.Select(slot => slot.UnitId).SequenceEqual(
                Enumerable.Range(0, SlotCount)))
        {
            throw Invalid("slots", "unitId values must be exactly 0 through 7");
        }
        _ = ValidateComposition(
            slots.Select(slot => slot.ClassId).ToArray(),
            unlockedClassIds,
            "slots.classId");

        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            loopProfile);
        foreach (ArcRelaySheetSlot slot in slots)
        {
            if (!Theaters.Contains(slot.Theater))
                throw Invalid($"slots[{slot.UnitId}].theater", "is not supported");
            if (!Roles.Contains(slot.Role))
                throw Invalid($"slots[{slot.UnitId}].role", "is not supported");
            if (slot.PartnerUnitId is < 0 or >= SlotCount
                || slot.PartnerUnitId == slot.UnitId)
            {
                throw Invalid(
                    $"slots[{slot.UnitId}].partnerUnitId",
                    "must name another slot");
            }
            ValidatePath(slot.OutboundPath, map, $"slots[{slot.UnitId}].outboundPath");
            ValidatePath(slot.ReturnPath, map, $"slots[{slot.UnitId}].returnPath");
        }

        if (document.Zones is null || document.Zones.Count is < 3 or > 8)
            throw Invalid("zones", "must contain 3 to 8 named rectangles");
        RequireUniqueIds(document.Zones.Select(zone => zone.Id), "zones");
        foreach (ArcRelaySheetZone zone in document.Zones)
        {
            SemanticId(zone.Id, "zones.id");
            if (zone.MinX < 0 || zone.MinY < 0
                || zone.MaxX >= map.Width || zone.MaxY >= map.Height
                || zone.MinX > zone.MaxX || zone.MinY > zone.MaxY)
            {
                throw Invalid($"zones.{zone.Id}", "has invalid map bounds");
            }
        }
        HashSet<string> zoneIds = document.Zones.Select(zone => zone.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string theater in slots.Select(slot => slot.Theater).Distinct())
        {
            if (!zoneIds.Contains(theater))
                throw Invalid("zones", $"needs a '{theater}' zone used by the lineup");
        }

        if (document.RallyLines is null || document.RallyLines.Count is < 1 or > 8)
            throw Invalid("rallyLines", "must contain 1 to 8 named lines");
        RequireUniqueIds(document.RallyLines.Select(line => line.Id), "rallyLines");
        foreach (ArcRelaySheetRallyLine line in document.RallyLines)
        {
            SemanticId(line.Id, "rallyLines.id");
            ValidatePath(line.Points, map, $"rallyLines.{line.Id}");
        }

        ArgumentNullException.ThrowIfNull(document.Policies);
        if (document.Policies.Carrier.HandoffHealthAtOrBelow is < 1 or > 5)
            throw Invalid("policies.carrier.handoffHealthAtOrBelow", "must be 1 to 5");
        if (document.Policies.Carrier.RouteFailureTicks is < 4 or > 60)
            throw Invalid("policies.carrier.routeFailureTicks", "must be 4 to 60");
        if (document.Policies.Escort.FollowDistance is < 1 or > 4)
            throw Invalid("policies.escort.followDistance", "must be 1 to 4");

        if (document.Gambits is null || document.Gambits.Count > MaximumGambits)
            throw Invalid("gambits", $"supports at most {MaximumGambits} ordered gambits");
        RequireUniqueIds(document.Gambits.Select(gambit => gambit.Id), "gambits");
        HashSet<string> rallyIds = document.RallyLines.Select(line => line.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (ArcRelaySheetGambit gambit in document.Gambits)
        {
            SemanticId(gambit.Id, "gambits.id");
            if (!Triggers.Contains(gambit.Trigger))
                throw Invalid($"gambits.{gambit.Id}.trigger", "is not supported");
            if (gambit.DurationTicks is < 4 or > 60)
                throw Invalid($"gambits.{gambit.Id}.durationTicks", "must be 4 to 60");
            if (gambit.CooldownTicks is < 8 or > 180)
                throw Invalid($"gambits.{gambit.Id}.cooldownTicks", "must be 8 to 180");
            if (gambit.ScopeRoles is null || gambit.ScopeRoles.Count is < 1 or > 4
                || gambit.ScopeRoles.Any(role => !Roles.Contains(role)))
            {
                throw Invalid($"gambits.{gambit.Id}.scopeRoles", "must contain 1 to 4 roles");
            }
            if (!Roles.Contains(gambit.RoleOverride))
                throw Invalid($"gambits.{gambit.Id}.roleOverride", "is not supported");
            if (!rallyIds.Contains(gambit.RallyLineId))
                throw Invalid($"gambits.{gambit.Id}.rallyLineId", "does not name a rally line");
        }
    }

    /// <summary>
    /// The one composition admission boundary shared by sheet slots and
    /// custom-mind declarations.
    /// </summary>
    public string[] ValidateComposition(
        IReadOnlyList<string>? classIds,
        IReadOnlySet<string> unlockedClassIds,
        string field = "composition.classIds")
    {
        ArgumentNullException.ThrowIfNull(unlockedClassIds);
        if (classIds is null || classIds.Count != SlotCount)
            throw Invalid(field, $"must contain exactly {SlotCount} entries");
        string[] classes = [.. classIds];
        foreach (IGrouping<string, string> group in classes.GroupBy(
                     classId => classId,
                     StringComparer.Ordinal))
        {
            if (!catalog.Contains(group.Key))
                throw Invalid(field, $"unknown class '{group.Key}'");
            if (!unlockedClassIds.Contains(group.Key))
                throw Invalid(field, $"class '{group.Key}' is locked");
            if (group.Count() > MaximumCopiesPerClass)
            {
                throw Invalid(
                    field,
                    $"class '{group.Key}' exceeds the two-copy limit");
            }
        }
        return classes;
    }

    public static ArcRelaySheetDocument NewSheetTemplate() => new(
        SchemaVersion,
        ArcRelayLoopProfile.Current.MapId,
        [
            Slot(0, "kestrel", "north", "carrier", 1,
                [(4, 9), (8, 8), (12, 6), (15, 4)],
                [(12, 6), (8, 8), (4, 10), (2, 11)]),
            Slot(1, "towline", "north", "screen", 0,
                [(4, 9), (8, 8), (12, 7), (14, 5)],
                [(11, 7), (7, 9), (4, 10), (2, 12)]),
            Slot(2, "relay", "centre", "carrier", 3,
                [(6, 10), (8, 9), (12, 9), (15, 11)],
                [(12, 13), (8, 13), (6, 13), (2, 11)]),
            Slot(3, "palisade", "centre", "screen", 2,
                [(4, 11), (8, 11), (13, 11), (14, 11)],
                [(9, 11), (7, 11), (4, 11), (2, 10)]),
            Slot(4, "lantern", "south", "carrier", 5,
                [(4, 13), (8, 15), (12, 16), (15, 18)],
                [(12, 16), (8, 14), (6, 13), (2, 11)]),
            Slot(5, "hush", "south", "intercept", 4,
                [(5, 14), (9, 15), (13, 16), (15, 18)],
                [(12, 15), (8, 13), (4, 12), (2, 12)]),
            Slot(6, "patchbay", "centre", "intercept", 2,
                [(5, 13), (8, 12), (13, 12), (14, 12)],
                [(9, 12), (6, 12), (3, 12), (2, 11)]),
            Slot(7, "switchback", "centre", "reserve", 3,
                [(5, 14), (8, 13), (11, 13), (13, 12)],
                [(9, 13), (6, 13), (3, 12), (2, 11)]),
        ],
        [
            new("north", 11, 1, 19, 8),
            new("centre", 10, 7, 20, 15),
            new("south", 11, 14, 19, 21),
            new("reserve", 1, 7, 10, 15),
        ],
        [
            Line("home", [(5, 8), (4, 10), (4, 12), (5, 14)]),
            Line("middle", [(9, 7), (9, 10), (9, 13), (9, 16)]),
            Line("forward", [(13, 6), (13, 9), (13, 12), (13, 16)]),
        ],
        new ArcRelaySheetPolicies(
            new ArcRelayCarrierPolicy(2, true, 12),
            new ArcRelayEscortPolicy(1, true),
            new ArcRelayInterceptionPolicy(true, true)),
        []);

    private static IReadOnlyList<ArcRelaySheetPoint> RelocatePath(
        IReadOnlyList<ArcRelaySheetPoint> points,
        ActorMapDefinition map) =>
        points.Select(point => RelocatePoint(point, map)).ToArray();

    private static ArcRelaySheetPoint RelocatePoint(
        ArcRelaySheetPoint point,
        ActorMapDefinition map)
    {
        if (!map.IsWall(point.X, point.Y))
            return point;
        return (from y in Enumerable.Range(0, map.Height)
                from x in Enumerable.Range(0, map.Width)
                where !map.IsWall(x, y)
                orderby Math.Abs(x - point.X) + Math.Abs(y - point.Y), y, x
                select new ArcRelaySheetPoint(x, y))
            .First();
    }

    private static ArcRelaySheetSlot Slot(
        int unitId,
        string classId,
        string theater,
        string role,
        int partnerUnitId,
        (int X, int Y)[] outbound,
        (int X, int Y)[] returned) =>
        new(unitId, classId, theater, role, partnerUnitId,
            outbound.Select(Point).ToArray(),
            returned.Select(Point).ToArray());

    private static ArcRelaySheetRallyLine Line(
        string id,
        (int X, int Y)[] points) =>
        new(id, points.Select(Point).ToArray());

    private static ArcRelaySheetPoint Point((int X, int Y) point) =>
        new(point.X, point.Y);

    private static void ValidatePath(
        IReadOnlyList<ArcRelaySheetPoint>? points,
        ActorMapDefinition map,
        string field)
    {
        if (points is null || points.Count is < 1 or > 24)
            throw Invalid(field, "must contain 1 to 24 waypoints");
        foreach (ArcRelaySheetPoint point in points)
        {
            if (map.IsWall(point.X, point.Y))
                throw Invalid(field, $"waypoint ({point.X},{point.Y}) is not walkable");
        }
    }

    private static void RequireUniqueIds(IEnumerable<string> values, string field)
    {
        string[] ids = values.ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw Invalid(field, "ids must be unique");
    }

    private static void SemanticId(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40
            || value.Any(character => !(char.IsLower(character)
                || char.IsDigit(character) || character == '-')))
        {
            throw Invalid(field, "must be a lowercase semantic id");
        }
    }

    private static InvalidDataException Invalid(string field, string reason) =>
        new($"Arc Relay sheet field '{field}' {reason}.");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    private static byte[] WriteCanonical(ArcRelaySheetDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", document.SchemaVersion);
            writer.WriteString("mapId", document.MapId);
            writer.WritePropertyName("slots");
            writer.WriteStartArray();
            foreach (ArcRelaySheetSlot slot in document.Slots.OrderBy(value => value.UnitId))
            {
                writer.WriteStartObject();
                writer.WriteNumber("unitId", slot.UnitId);
                writer.WriteString("classId", slot.ClassId);
                writer.WriteString("theater", slot.Theater);
                writer.WriteString("role", slot.Role);
                writer.WriteNumber("partnerUnitId", slot.PartnerUnitId);
                WritePoints(writer, "outboundPath", slot.OutboundPath);
                WritePoints(writer, "returnPath", slot.ReturnPath);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("zones");
            writer.WriteStartArray();
            foreach (ArcRelaySheetZone zone in document.Zones.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", zone.Id);
                writer.WriteNumber("minX", zone.MinX);
                writer.WriteNumber("minY", zone.MinY);
                writer.WriteNumber("maxX", zone.MaxX);
                writer.WriteNumber("maxY", zone.MaxY);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("rallyLines");
            writer.WriteStartArray();
            foreach (ArcRelaySheetRallyLine line in document.RallyLines.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", line.Id);
                WritePoints(writer, "points", line.Points);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("policies");
            writer.WriteStartObject();
            writer.WritePropertyName("carrier");
            writer.WriteStartObject();
            writer.WriteNumber("handoffHealthAtOrBelow", document.Policies.Carrier.HandoffHealthAtOrBelow);
            writer.WriteBoolean("preferAssignedTheater", document.Policies.Carrier.PreferAssignedTheater);
            writer.WriteNumber("routeFailureTicks", document.Policies.Carrier.RouteFailureTicks);
            writer.WriteEndObject();
            writer.WritePropertyName("escort");
            writer.WriteStartObject();
            writer.WriteNumber("followDistance", document.Policies.Escort.FollowDistance);
            writer.WriteBoolean("focusEnemyCarrier", document.Policies.Escort.FocusEnemyCarrier);
            writer.WriteEndObject();
            writer.WritePropertyName("interception");
            writer.WriteStartObject();
            writer.WriteBoolean("focusEnemyCarrier", document.Policies.Interception.FocusEnemyCarrier);
            writer.WriteBoolean("looseCoreFallback", document.Policies.Interception.LooseCoreFallback);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("gambits");
            writer.WriteStartArray();
            foreach (ArcRelaySheetGambit gambit in document.Gambits)
            {
                writer.WriteStartObject();
                writer.WriteString("id", gambit.Id);
                writer.WriteString("trigger", gambit.Trigger);
                writer.WriteNumber("durationTicks", gambit.DurationTicks);
                writer.WriteNumber("cooldownTicks", gambit.CooldownTicks);
                writer.WritePropertyName("scopeRoles");
                writer.WriteStartArray();
                foreach (string role in gambit.ScopeRoles.Order(StringComparer.Ordinal))
                    writer.WriteStringValue(role);
                writer.WriteEndArray();
                writer.WriteString("roleOverride", gambit.RoleOverride);
                writer.WriteString("rallyLineId", gambit.RallyLineId);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void WritePoints(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<ArcRelaySheetPoint> points)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (ArcRelaySheetPoint point in points)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", point.X);
            writer.WriteNumber("y", point.Y);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static byte[] Link(
        ArcRelaySheetDocument document,
        string sheetIdentity,
        string sourceSha256)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(0x31535241); // ARS1 internal stock-mind data link.
        writer.Write(sourceSha256);
        writer.Write("arc-relay-evaluation-sheet-v0");
        writer.Write(sheetIdentity);
        writer.Write(document.MapId);
        ArcRelaySheetSlot[] slots = document.Slots.OrderBy(value => value.UnitId).ToArray();
        writer.Write(slots.Length);
        foreach (ArcRelaySheetSlot slot in slots)
            writer.Write(slot.ClassId);
        writer.Write(slots.Length);
        foreach (ArcRelaySheetSlot slot in slots)
        {
            writer.Write(slot.UnitId);
            writer.Write(slot.Theater);
            writer.Write(slot.Role);
            writer.Write(slot.PartnerUnitId);
            WriteLinkedPoints(writer, slot.OutboundPath);
            WriteLinkedPoints(writer, slot.ReturnPath);
        }
        ArcRelaySheetZone[] zones = document.Zones.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        writer.Write(zones.Length);
        foreach (ArcRelaySheetZone zone in zones)
        {
            writer.Write(zone.Id);
            writer.Write(zone.MinX);
            writer.Write(zone.MinY);
            writer.Write(zone.MaxX);
            writer.Write(zone.MaxY);
        }
        ArcRelaySheetRallyLine[] lines = document.RallyLines.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        writer.Write(lines.Length);
        foreach (ArcRelaySheetRallyLine line in lines)
        {
            writer.Write(line.Id);
            WriteLinkedPoints(writer, line.Points);
        }
        writer.Write(document.Policies.Carrier.HandoffHealthAtOrBelow);
        writer.Write(document.Policies.Carrier.PreferAssignedTheater);
        writer.Write(document.Policies.Carrier.RouteFailureTicks);
        writer.Write(document.Policies.Escort.FollowDistance);
        writer.Write(document.Policies.Escort.FocusEnemyCarrier);
        writer.Write(document.Policies.Interception.FocusEnemyCarrier);
        writer.Write(document.Policies.Interception.LooseCoreFallback);
        writer.Write(document.Gambits.Count);
        for (int index = 0; index < document.Gambits.Count; index++)
        {
            ArcRelaySheetGambit gambit = document.Gambits[index];
            writer.Write(index);
            writer.Write(gambit.Id);
            writer.Write(gambit.Trigger);
            writer.Write(gambit.DurationTicks);
            writer.Write(gambit.CooldownTicks);
            writer.Write(gambit.ScopeRoles.Count);
            foreach (string role in gambit.ScopeRoles)
                writer.Write(role);
            writer.Write(gambit.RoleOverride);
            writer.Write(gambit.RallyLineId);
        }
        writer.Flush();
        if (stream.Length > 64 * 1024)
            throw Invalid("document", "exceeds the 64 KiB stock-mind link limit");
        return stream.ToArray();
    }

    private static void WriteLinkedPoints(
        BinaryWriter writer,
        IReadOnlyList<ArcRelaySheetPoint> points)
    {
        writer.Write(points.Count);
        foreach (ArcRelaySheetPoint point in points)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
        }
    }
}

public sealed record ArcRelaySheetCompilation(
    string CanonicalJson,
    string ContentHash,
    byte[] LinkedData,
    IReadOnlyList<string> Classes);
