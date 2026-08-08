using BotArena.Sdk;

namespace ArcRelayForwardStock;

/// <summary>
/// Stable data linker for the frozen stock algorithm. The WASM module is built
/// once; the evaluation harness supplies one separately hashed sheet through a
/// participant-local <c>MindStart.EvaluationData</c> payload.
/// </summary>
internal sealed class StockSheet
{
    private StockSheet(
        string schema,
        string sheetId,
        string mapId,
        string sourceSha256,
        string[] composition,
        UnitPlan[] units,
        Dictionary<string, Zone> zones,
        Dictionary<string, Position[]> rallyLines,
        CarrierPolicy carrier,
        EscortPolicy escort,
        InterceptionPolicy interception,
        GambitPlan[] gambits)
    {
        Schema = schema;
        SheetId = sheetId;
        MapId = mapId;
        SourceSha256 = sourceSha256;
        Composition = composition;
        Units = units;
        Zones = zones;
        RallyLines = rallyLines;
        Carrier = carrier;
        Escort = escort;
        Interception = interception;
        Gambits = gambits;
    }

    internal string Schema { get; }
    internal string SheetId { get; }
    internal string MapId { get; }
    internal string SourceSha256 { get; }
    internal string[] Composition { get; }
    internal UnitPlan[] Units { get; }
    internal Dictionary<string, Zone> Zones { get; }
    internal Dictionary<string, Position[]> RallyLines { get; }
    internal CarrierPolicy Carrier { get; }
    internal EscortPolicy Escort { get; }
    internal InterceptionPolicy Interception { get; }
    internal GambitPlan[] Gambits { get; }

    internal static StockSheet Load(
        System.Collections.Immutable.ImmutableArray<byte> evaluationData)
    {
        if (evaluationData.IsDefaultOrEmpty)
            throw new InvalidDataException("The stock mind requires sheet data.");

        using var reader = new BinaryReader(
            new MemoryStream(evaluationData.ToArray()));
        if (reader.ReadInt32() != 0x31535241)
            throw new InvalidDataException("Unknown stock sheet data envelope.");
        string sourceSha256 = reader.ReadString();
        string schema = reader.ReadString();
        if (!string.Equals(
                schema,
                "arc-relay-evaluation-sheet-v0",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported evaluation sheet schema '{schema}'.");
        }

        string sheetId = reader.ReadString();
        string mapId = reader.ReadString();
        string[] composition = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => reader.ReadString()).ToArray();
        UnitPlan[] units = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new UnitPlan(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32(),
                Positions(reader),
                Positions(reader)))
            .ToArray();

        var zones = new Dictionary<string, Zone>(StringComparer.Ordinal);
        for (int index = 0, count = ReadCount(reader); index < count; index++)
        {
            string name = reader.ReadString();
            zones.Add(
                name,
                new Zone(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()));
        }

        var rallyLines = new Dictionary<string, Position[]>(
            StringComparer.Ordinal);
        for (int index = 0, count = ReadCount(reader); index < count; index++)
            rallyLines.Add(reader.ReadString(), Positions(reader));

        var carrier = new CarrierPolicy(
            reader.ReadInt32(), reader.ReadBoolean(), reader.ReadInt32());
        var escort = new EscortPolicy(reader.ReadInt32(), reader.ReadBoolean());
        var interception = new InterceptionPolicy(
            reader.ReadBoolean(), reader.ReadBoolean());
        GambitPlan[] gambits = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new GambitPlan(
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
        {
            throw new InvalidDataException(
                "An evaluation sheet needs eight composition and slot entries.");
        }
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Trailing stock sheet data.");
        return new StockSheet(
            schema,
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
            throw new InvalidDataException("Stock sheet count is out of range.");
        return count;
    }

    private static Position[] Positions(BinaryReader reader) =>
        Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new Position(reader.ReadInt32(), reader.ReadInt32()))
            .ToArray();
}
