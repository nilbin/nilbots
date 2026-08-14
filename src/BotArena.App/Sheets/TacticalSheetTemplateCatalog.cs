using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.Engine;
using BotArena.TacticalPlaybooks;

namespace BotArena.App.Sheets;

public sealed record TacticalSheetSource(
    string Id,
    string Name,
    string Description,
    string OrdersJson,
    string MapPlanJson,
    string[] Composition,
    byte[] LinkedData,
    string ContentHash);

/// <summary>
/// Reproducible two-file product templates derived from tracked Orders v2
/// sources.
/// </summary>
public sealed class TacticalSheetTemplateCatalog
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
    };

    private readonly IReadOnlyDictionary<string, TacticalSheetSource> stock;

    public TacticalSheetTemplateCatalog()
    {
        Template = Resolve(
            "starter-v2",
            "Starter sheet",
            "A readable Tapline plan: two bodies cover home, three take centre, and three contest far.",
            Resource("BotArena.Sheets.starter-v2.sheet.json"),
            Resource("BotArena.Sheets.starter-v2.map-plan.json"));
        TacticalSheetSource[] opponents =
        [
            Resolve(
                ArcRelayEntrantPlaylistDefinition.Tapline4StockSheetId,
                "Tempered Standard",
                "A balanced eight-class line: guns contest centre, support keeps the line alive, and couriers convert loose Cores.",
                Resource("BotArena.Sheets.tempered-standard-v1.sheet.json"),
                Resource("BotArena.Sheets.tempered-standard-v1.map-plan.json")),
            Resolve(
                "relay-circuit-v1",
                "Relay Circuit",
                "A premium-Core raid: one Relay steals deep while a second runs centre handoffs and the squad screens the return.",
                Resource("BotArena.Sheets.relay-circuit-v1.sheet.json"),
                Resource("BotArena.Sheets.relay-circuit-v1.map-plan.json")),
            Resolve(
                "crowned-rail-v1",
                "Crowned Rail",
                "A protected Longshot ace supported by repair, designator optics, a firing line, and a logistics reserve.",
                Resource("BotArena.Sheets.crowned-rail-v1.sheet.json"),
                Resource("BotArena.Sheets.crowned-rail-v1.map-plan.json")),
            Resolve(
                "lockbox-v1",
                "Lockbox",
                "Two Masons fortify the near and centre Wells while a deployment net and support unit hold the enclosure.",
                Resource("BotArena.Sheets.lockbox-v1.sheet.json"),
                Resource("BotArena.Sheets.lockbox-v1.map-plan.json")),
            Resolve(
                "demolition-march-v1",
                "Demolition March",
                "Longshots, Mortar, and Hush clear deployments before Relay and Kestrel turn the breach into a raid.",
                Resource("BotArena.Sheets.demolition-march-v1.sheet.json"),
                Resource("BotArena.Sheets.demolition-march-v1.map-plan.json")),
            Resolve(
                "rising-standard-v1",
                "Rising Standard",
                "A contact group grows an emergent veteran; support then rallies to that survivor while a reserve works objectives.",
                Resource("BotArena.Sheets.rising-standard-v1.sheet.json"),
                Resource("BotArena.Sheets.rising-standard-v1.map-plan.json")),
            Resolve(
                "wavebreak-v1",
                "Wavebreak",
                "A wave-aware force exchanges weak allies, screens its retreat, and evacuates before a large enemy return.",
                Resource("BotArena.Sheets.wavebreak-v1.sheet.json"),
                Resource("BotArena.Sheets.wavebreak-v1.map-plan.json")),
        ];
        stock = opponents.ToDictionary(value => value.Id, StringComparer.Ordinal);
        Stock = opponents;
    }

    public TacticalSheetSource Template { get; }
    public IReadOnlyList<TacticalSheetSource> Stock { get; }

    public TacticalSheetSource GetStock(string id) =>
        stock.TryGetValue(id, out TacticalSheetSource? value)
            ? value
            : throw new InvalidDataException(
                $"Unknown stock opponent sheet '{id}'.");

    private static TacticalSheetSource Resolve(
        string id,
        string name,
        string description,
        string ordersJson,
        string mapPlanJson)
    {
        JsonObject orders = JsonNode.Parse(ordersJson)?.AsObject()
            ?? throw new InvalidDataException($"Embedded Orders sheet '{id}' is empty.");
        JsonObject mapPlan = JsonNode.Parse(mapPlanJson)?.AsObject()
            ?? throw new InvalidDataException($"Embedded map plan '{id}' is empty.");
        mapPlan["mapPlanId"] = id;
        string mapId = mapPlan["mapId"]?.GetValue<string>()
            ?? throw new InvalidDataException(
                $"Embedded map plan '{id}' has no map id.");
        if (!string.Equals(
                mapId,
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Embedded map plan '{id}' targets '{mapId}', not the native "
                + $"'{ArcRelayLoopProfile.Current.MapId}' Tapline. "
                + "Template geometry is never projected between maps.");
        }
        string resolvedMapPlan = mapPlan.ToJsonString(Pretty);
        orders["mapPlan"] = "map-plan.json";
        orders["sheetId"] = id;
        string resolvedOrders = orders.ToJsonString(Pretty);
        var everyClass = ArcRelayLaunchClassIds.All.ToHashSet(
            StringComparer.Ordinal);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                Encoding.UTF8.GetBytes(resolvedOrders),
                Encoding.UTF8.GetBytes(resolvedMapPlan),
                $"templates/{id}/orders.json",
                $"templates/{id}/map-plan.json");
        if (compilation.Composition.Any(value => !everyClass.Contains(value)))
            throw new InvalidDataException(
                $"Embedded Orders sheet '{id}' has an unknown class.");
        return new TacticalSheetSource(
            id,
            name,
            description,
            resolvedOrders,
            resolvedMapPlan,
            compilation.Composition,
            compilation.LinkedData,
            Convert.ToHexStringLower(SHA256.HashData(compilation.LinkedData)));
    }

    private static string Resource(string name)
    {
        Assembly assembly = typeof(TacticalSheetTemplateCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Embedded tactical-sheet source '{name}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
