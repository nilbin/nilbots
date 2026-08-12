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
    string PlaybookJson,
    string LayoutJson,
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
                ArcRelayEntrantPlaylistDefinition.OrdersStockSheetId,
                "Wellwright v2",
                "The Tapline stock opponent: works all three Wells and explicitly returns every carried Core.",
                Resource("BotArena.Sheets.wellwright-v2.sheet.json"),
                Resource("BotArena.Sheets.wellwright-v2.map-plan.json")),
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
        string playbookJson,
        string layoutJson)
    {
        JsonObject playbook = JsonNode.Parse(playbookJson)?.AsObject()
            ?? throw new InvalidDataException($"Embedded playbook '{id}' is empty.");
        JsonObject mapPlan = JsonNode.Parse(layoutJson)?.AsObject()
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
        string resolvedLayout = mapPlan.ToJsonString(Pretty);
        playbook["mapPlan"] = "map-plan.json";
        playbook["sheetId"] = id;
        string resolvedPlaybook = playbook.ToJsonString(Pretty);
        var everyClass = ArcRelayLaunchClassIds.All.ToHashSet(
            StringComparer.Ordinal);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                Encoding.UTF8.GetBytes(resolvedPlaybook),
                Encoding.UTF8.GetBytes(resolvedLayout),
                $"templates/{id}/playbook.json",
                $"templates/{id}/map-plan.json");
        if (compilation.Composition.Any(value => !everyClass.Contains(value)))
            throw new InvalidDataException(
                $"Embedded playbook '{id}' has an unknown class.");
        return new TacticalSheetSource(
            id,
            name,
            description,
            resolvedPlaybook,
            resolvedLayout,
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
