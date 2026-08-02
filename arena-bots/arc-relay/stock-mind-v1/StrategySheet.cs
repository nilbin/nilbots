using BotArena.Sdk;

/// <summary>
/// Stable data linker for the versioned strategy algorithm. The WASM module is
/// built once; each evaluation sheet is delivered as separately hashed data.
/// </summary>
internal sealed class StrategySheet
{
    private StrategySheet(
        string sheetId,
        string mapId,
        string sourceSha256,
        string[] composition,
        UnitPlan[] units,
        Dictionary<string, Zone> zones,
        Dictionary<string, Position[]> rallyLines,
        Dictionary<string, Position[]> paths,
        CarrierPolicy carrier,
        EscortPolicy escort,
        InterceptionPolicy interception,
        GambitPlan[] gambits)
    {
        SheetId = sheetId;
        MapId = mapId;
        SourceSha256 = sourceSha256;
        Composition = composition;
        Units = units;
        Zones = zones;
        RallyLines = rallyLines;
        Paths = paths;
        Carrier = carrier;
        Escort = escort;
        Interception = interception;
        Gambits = gambits;
    }

    internal string SheetId { get; }
    internal string MapId { get; }
    internal string SourceSha256 { get; }
    internal string[] Composition { get; }
    internal UnitPlan[] Units { get; }
    internal Dictionary<string, Zone> Zones { get; }
    internal Dictionary<string, Position[]> RallyLines { get; }
    internal Dictionary<string, Position[]> Paths { get; }
    internal CarrierPolicy Carrier { get; }
    internal EscortPolicy Escort { get; }
    internal InterceptionPolicy Interception { get; }
    internal GambitPlan[] Gambits { get; }

    internal static StrategySheet Load(
        System.Collections.Immutable.ImmutableArray<byte> evaluationData)
    {
        if (evaluationData.IsDefaultOrEmpty)
            throw new InvalidDataException("The strategy mind requires sheet data.");

        using var reader = new BinaryReader(
            new MemoryStream(evaluationData.ToArray()));
        if (reader.ReadInt32() != 0x31535241)
            throw new InvalidDataException("Unknown strategy sheet envelope.");
        string sourceSha256 = reader.ReadString();
        string schema = reader.ReadString();
        if (!string.Equals(
                schema,
                "arc-relay-evaluation-sheet-v1",
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
                Positions(reader),
                PositionIntent.BaseAssignment,
                "normal",
                "normal"))
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

        var paths = new Dictionary<string, Position[]>(StringComparer.Ordinal);
        for (int index = 0, count = ReadCount(reader); index < count; index++)
            paths.Add(reader.ReadString(), Positions(reader));

        Dictionary<int, UnitPlan> byUnit = units.ToDictionary(value => value.UnitId);
        for (int index = 0, count = ReadCount(reader); index < count; index++)
        {
            int unitId = reader.ReadInt32();
            UnitPlan prior = byUnit.TryGetValue(unitId, out UnitPlan? value)
                ? value
                : throw new InvalidDataException(
                    $"Default intent references unknown unit {unitId}.");
            byUnit[unitId] = prior with
            {
                DefaultPosition = ReadPositionIntent(reader),
                DefaultEngagementIntent = reader.ReadString(),
                DefaultSignatureIntent = reader.ReadString(),
            };
        }
        units = byUnit.Values.OrderBy(value => value.UnitId).ToArray();

        GambitPlan[] gambits = Enumerable.Range(0, ReadCount(reader))
            .Select(_ => ReadGambit(reader))
            .OrderBy(value => value.Priority)
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();

        if (composition.Length != 8 || units.Length != 8
            || units.Select(value => value.UnitId).Distinct().Count() != 8)
        {
            throw new InvalidDataException(
                "A strategy sheet needs eight composition and slot entries.");
        }
        foreach (GambitPlan gambit in gambits)
        {
            if (gambit.EnterAll.Length == 0
                || gambit.MinimumTicks is < 4 or > 120
                || gambit.MaximumTicks < gambit.MinimumTicks
                || gambit.MaximumTicks > 180
                || gambit.CooldownTicks is < 0 or > 300
                || (gambit.ScopeUnitIds.Length == 0
                    && gambit.ScopeRoles.Length == 0))
            {
                throw new InvalidDataException(
                    $"Gambit '{gambit.Id}' has invalid bounds or scope.");
            }
        }
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Trailing strategy sheet data.");
        return new StrategySheet(
            sheetId,
            mapId,
            sourceSha256,
            composition,
            units,
            zones,
            rallyLines,
            paths,
            carrier,
            escort,
            interception,
            gambits);
    }

    private static GambitPlan ReadGambit(BinaryReader reader) => new(
        reader.ReadInt32(),
        reader.ReadString(),
        reader.ReadString(),
        reader.ReadInt32(),
        reader.ReadInt32(),
        reader.ReadInt32(),
        Integers(reader),
        Strings(reader),
        Clauses(reader),
        Clauses(reader),
        reader.ReadString(),
        reader.ReadBoolean() ? ReadPositionIntent(reader) : null,
        Positions(reader),
        new PolicyOverlay(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32()),
        reader.ReadString(),
        reader.ReadString(),
        reader.ReadBoolean());

    private static ConditionClause[] Clauses(BinaryReader reader) =>
        Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new ConditionClause(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadString()))
            .ToArray();

    private static PositionIntent ReadPositionIntent(BinaryReader reader) => new(
        reader.ReadString(),
        reader.ReadString(),
        reader.ReadInt32(),
        reader.ReadInt32(),
        reader.ReadString(),
        reader.ReadString());

    private static int ReadCount(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count is < 0 or > 1024)
            throw new InvalidDataException("Strategy sheet count is out of range.");
        return count;
    }

    private static int[] Integers(BinaryReader reader) =>
        Enumerable.Range(0, ReadCount(reader))
            .Select(_ => reader.ReadInt32()).ToArray();

    private static string[] Strings(BinaryReader reader) =>
        Enumerable.Range(0, ReadCount(reader))
            .Select(_ => reader.ReadString()).ToArray();

    private static Position[] Positions(BinaryReader reader) =>
        Enumerable.Range(0, ReadCount(reader))
            .Select(_ => new Position(reader.ReadInt32(), reader.ReadInt32()))
            .ToArray();
}

internal sealed record UnitPlan(
    int UnitId,
    string Theater,
    string Role,
    int PartnerUnitId,
    Position[] OutboundPath,
    Position[] ReturnPath,
    PositionIntent DefaultPosition,
    string DefaultEngagementIntent,
    string DefaultSignatureIntent);

internal readonly record struct Zone(int MinX, int MinY, int MaxX, int MaxY)
{
    internal bool Contains(Position position) =>
        position.X >= MinX && position.X <= MaxX
        && position.Y >= MinY && position.Y <= MaxY;
}

internal sealed record CarrierPolicy(
    int HandoffHealthAtOrBelow,
    bool PreferAssignedTheater,
    int RouteFailureTicks);

internal sealed record EscortPolicy(int FollowDistance, bool FocusEnemyCarrier);

internal sealed record InterceptionPolicy(
    bool FocusEnemyCarrier,
    bool LooseCoreFallback);

internal sealed record GambitPlan(
    int Priority,
    string Id,
    string Activation,
    int MinimumTicks,
    int MaximumTicks,
    int CooldownTicks,
    int[] ScopeUnitIds,
    string[] ScopeRoles,
    ConditionClause[] EnterAll,
    ConditionClause[] ExitAny,
    string RoleOverride,
    PositionIntent? Position,
    Position[] FormationOffsets,
    PolicyOverlay Policies,
    string EngagementIntent,
    string SignatureIntent,
    bool AppliesWhileCarrying);

internal sealed record PositionIntent(
    string Kind,
    string Target,
    int OffsetX,
    int OffsetY,
    string Arrival,
    string FallbackZone)
{
    internal static PositionIntent BaseAssignment { get; } = new(
        "base-assignment", "", 0, 0, "base-assignment", "");
}

internal sealed record ConditionClause(
    string Fact,
    string Operator,
    int Value,
    string Zone);

internal sealed record PolicyOverlay(
    int HandoffHealthAtOrBelow,
    int PreferAssignedTheater,
    int RouteFailureTicks,
    int FollowDistance,
    int EscortFocusEnemyCarrier,
    int InterceptionFocusEnemyCarrier,
    int LooseCoreFallback);
