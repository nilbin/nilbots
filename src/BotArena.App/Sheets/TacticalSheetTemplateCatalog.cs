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
    string ContentHash,
    string? TacticsJson = null,
    string Classification = "practice",
    bool Recommended = false);

/// <summary>
/// Reproducible product sheet sources: tactics-native starters materialized
/// and verified at construction, and tracked Orders v2 stock opponents.
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
        TacticalSheetSource[] starters =
        [
            ResolveTactics(
                "first-stand-v1",
                "Tapline First Stand",
                "A clear first sheet: post up on Mid, hold the ground, and when a Core lands in your hands the runner brings it home under escort.",
                Resource("BotArena.Sheets.first-stand-v1.tactics.json"),
                classification: "recommended",
                recommended: true),
            ResolveTactics(
                "mid-pincer-v1",
                "Mid Pincer",
                "A small guard baits at Mid while six stage behind their lines; once everyone is on station and enemies commit, the flankers converge.",
                Resource("BotArena.Sheets.mid-pincer-v1.tactics.json"),
                classification: "advanced"),
        ];
        Starters = starters;
        TacticalSheetSource[] opponents =
        [
            Resolve(
                "demolition-march-v1",
                "Demolition March",
                "Longshots, Mortar, and Hush clear deployments before Relay and Kestrel turn the breach into a raid.",
                Resource("BotArena.Sheets.demolition-march-v1.sheet.json"),
                Resource("BotArena.Sheets.demolition-march-v1.map-plan.json")),
            Resolve(
                "lockbox-v1",
                "Lockbox",
                "Two Masons fortify the near and centre Wells while a deployment net and support unit hold the enclosure.",
                Resource("BotArena.Sheets.lockbox-v1.sheet.json"),
                Resource("BotArena.Sheets.lockbox-v1.map-plan.json")),
            Resolve(
                "idle-field-v1",
                "Idle Field",
                "Eight stationary bodies that never act: a clean control for routes, timing, skills, and traffic checks.",
                Resource("BotArena.Sheets.idle-field-v1.sheet.json"),
                Resource("BotArena.Sheets.idle-field-v1.map-plan.json")),
            Resolve(
                "mid-overload-drill-v1",
                "Mid Overload drill",
                "A deterministic marching formation for checking Posts, readiness, walls, response commitment, and Kestrel route work.",
                Resource("BotArena.Sheets.mid-overload-drill-v1.sheet.json"),
                Resource("BotArena.Sheets.mid-overload-drill-v1.map-plan.json")),
            Resolve(
                "running-relay-solo-v1",
                "Running Relay solo control",
                "One Relay carries through the exact Running Relay corridor without Tosses, for a direct Practice timing comparison.",
                Resource("BotArena.Sheets.running-relay-solo-v1.sheet.json"),
                Resource("BotArena.Sheets.running-relay-solo-v1.map-plan.json")),
            Resolve(
                "tempered-standard-v2",
                "Tempered Standard",
                "A balanced eight-class line: guns contest centre, support keeps the line alive, and couriers convert loose Cores.",
                Resource("BotArena.Sheets.tempered-standard-v2.sheet.json"),
                Resource("BotArena.Sheets.tempered-standard-v2.map-plan.json")),
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

    public IReadOnlyList<TacticalSheetSource> Starters { get; }
    public IReadOnlyList<TacticalSheetSource> Stock { get; }

    public TacticalSheetSource GetStock(string id) =>
        stock.TryGetValue(id, out TacticalSheetSource? value)
            ? value
            : throw new InvalidDataException(
                $"Unknown stock opponent sheet '{id}'.");

    private static TacticalSheetSource ResolveTactics(
        string id,
        string name,
        string description,
        string tacticsJson,
        string classification,
        bool recommended = false)
    {
        JsonObject tactics = JsonNode.Parse(tacticsJson)?.AsObject()
            ?? throw new InvalidDataException(
                $"Embedded tactics starter '{id}' is empty.");
        tactics["sheetId"] = id;
        string resolvedTactics = tactics.ToJsonString(Pretty);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(resolvedTactics),
                $"starters/{id}/tactics.json");
        return new TacticalSheetSource(
            id,
            name,
            description,
            Encoding.UTF8.GetString(compilation.MaterializedOrders!),
            Encoding.UTF8.GetString(compilation.MaterializedMapPlan!),
            compilation.Composition,
            compilation.LinkedData,
            Convert.ToHexStringLower(SHA256.HashData(compilation.LinkedData)),
            resolvedTactics,
            classification,
            recommended);
    }

    private static TacticalSheetSource Resolve(
        string id,
        string name,
        string description,
        string ordersJson,
        string mapPlanJson,
        string classification = "practice",
        bool recommended = false)
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
            Convert.ToHexStringLower(SHA256.HashData(compilation.LinkedData)),
            TacticsJson: null,
            classification,
            recommended);
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
