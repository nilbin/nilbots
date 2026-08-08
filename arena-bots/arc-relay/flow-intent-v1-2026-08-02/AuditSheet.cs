using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Minimal reader for the evaluation-only ARS1 envelope. The audit mind uses
/// composition and role/theater plans; product sheet UX is deliberately out of
/// scope.
/// </summary>
internal sealed record AuditSheet(
    string SheetId,
    string MapId,
    string SourceSha256,
    string[] Composition,
    AuditUnitPlan[] Units,
    Dictionary<string, AuditZone> Zones,
    Dictionary<string, Position[]> RallyLines,
    AuditCarrierPolicy Carrier,
    AuditEscortPolicy Escort,
    AuditInterceptionPolicy Interception,
    AuditGambitPlan[] Gambits)
{
    internal static AuditSheet Load(ImmutableArray<byte> data)
    {
        if (data.IsDefaultOrEmpty)
            throw new InvalidDataException("AuditMind requires evaluation data.");

        using var reader = new BinaryReader(new MemoryStream(data.ToArray()));
        if (reader.ReadInt32() != 0x31535241)
            throw new InvalidDataException("Unknown evaluation data envelope.");
        string sourceSha256 = reader.ReadString();
        string schema = reader.ReadString();
        if (!string.Equals(
                schema,
                "arc-relay-evaluation-sheet-v0",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported evaluation schema '{schema}'.");
        }

        string sheetId = reader.ReadString();
        string mapId = reader.ReadString();
        string[] composition = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => reader.ReadString())
            .ToArray();
        AuditUnitPlan[] units = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new AuditUnitPlan(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32(),
                ReadPositions(reader),
                ReadPositions(reader)))
            .ToArray();

        var zones = new Dictionary<string, AuditZone>(StringComparer.Ordinal);
        for (int index = 0, count = ReadCount(reader); index < count; index++)
        {
            zones.Add(
                reader.ReadString(),
                new AuditZone(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()));
        }

        var rallyLines = new Dictionary<string, Position[]>(
            StringComparer.Ordinal);
        for (int index = 0, count = ReadCount(reader); index < count; index++)
            rallyLines.Add(reader.ReadString(), ReadPositions(reader));

        var carrier = new AuditCarrierPolicy(
            reader.ReadInt32(),
            reader.ReadBoolean(),
            reader.ReadInt32());
        var escort = new AuditEscortPolicy(
            reader.ReadInt32(),
            reader.ReadBoolean());
        var interception = new AuditInterceptionPolicy(
            reader.ReadBoolean(),
            reader.ReadBoolean());
        AuditGambitPlan[] gambits = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new AuditGambitPlan(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                Enumerable.Range(0, ReadCount(reader))
                    .Select(_ => reader.ReadString()).ToArray(),
                reader.ReadString(),
                reader.ReadString()))
            .ToArray();
        if (composition.Length != 8 || units.Length != 8)
            throw new InvalidDataException("Audit sheets require eight units.");
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Trailing evaluation sheet data.");
        return new AuditSheet(
            sheetId,
            mapId,
            sourceSha256,
            composition,
            units,
            zones,
            rallyLines,
            carrier,
            escort,
            interception,
            gambits);
    }

    private static int ReadCount(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count is < 0 or > 1024)
            throw new InvalidDataException("Evaluation count is out of range.");
        return count;
    }

    private static Position[] ReadPositions(BinaryReader reader) =>
        Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new Position(reader.ReadInt32(), reader.ReadInt32()))
            .ToArray();
}

internal sealed record AuditUnitPlan(
    int UnitId,
    string Theater,
    string Role,
    int PartnerUnitId,
    Position[] OutboundPath,
    Position[] ReturnPath);

internal readonly record struct AuditZone(
    int MinX,
    int MinY,
    int MaxX,
    int MaxY)
{
    internal bool Contains(Position position) =>
        position.X >= MinX && position.X <= MaxX
        && position.Y >= MinY && position.Y <= MaxY;
}

internal sealed record AuditCarrierPolicy(
    int HandoffHealthAtOrBelow,
    bool PreferAssignedTheater,
    int RouteFailureTicks);

internal sealed record AuditEscortPolicy(
    int FollowDistance,
    bool FocusEnemyCarrier);

internal sealed record AuditInterceptionPolicy(
    bool FocusEnemyCarrier,
    bool LooseCoreFallback);

internal sealed record AuditGambitPlan(
    int Priority,
    string Id,
    string Trigger,
    int DurationTicks,
    int CooldownTicks,
    string[] ScopeRoles,
    string RoleOverride,
    string RallyLine);
