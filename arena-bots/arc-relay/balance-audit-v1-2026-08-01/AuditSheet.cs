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
    AuditUnitPlan[] Units)
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
        if (composition.Length != 8 || units.Length != 8)
            throw new InvalidDataException("Audit sheets require eight units.");
        return new AuditSheet(
            sheetId,
            mapId,
            sourceSha256,
            composition,
            units);
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
