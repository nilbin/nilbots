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
/// Reproducible two-file product templates derived from tracked consolidated
/// sheet sources.
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
            "starter-v1",
            "Starter sheet",
            "A small Tapline plan: two Core runners, a midfield line, and one field medic.",
            Resource("BotArena.Sheets.starter-v1.playbook.json"),
            Resource("BotArena.Sheets.starter-v1.layout.json"));
        TacticalSheetSource[] opponents =
        [
            Resolve(
                ArcRelayEntrantPlaylistDefinition.StockSheetId,
                "Wellwright v1",
                "The Tapline stock entrant: races all three Wells with independently authored hauler and support ladders.",
                Resource("BotArena.Sheets.wellwright-v1.playbook.json"),
                Resource("BotArena.Sheets.tapline-base-v1.layout.json")),
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
        JsonObject layout = JsonNode.Parse(layoutJson)?.AsObject()
            ?? throw new InvalidDataException($"Embedded layout '{id}' is empty.");
        layout["layoutId"] = id;
        string mapId = layout["mapId"]?.GetValue<string>()
            ?? throw new InvalidDataException(
                $"Embedded layout '{id}' has no map id.");
        if (!string.Equals(
                mapId,
                ArcRelayLoopProfile.Current.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Embedded layout '{id}' targets '{mapId}', not the native "
                + $"'{ArcRelayLoopProfile.Current.MapId}' Tapline. "
                + "Template geometry is never projected between maps.");
        }
        NormalizeBindings(layout, id);

        string resolvedLayout = layout.ToJsonString(Pretty);
        string layoutHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(resolvedLayout)));
        JsonObject layoutReference = playbook["layout"]?.AsObject()
            ?? throw new InvalidDataException(
                $"Embedded playbook '{id}' has no layout reference.");
        layoutReference["path"] = "layout.json";
        layoutReference["sha256"] = layoutHash;
        playbook["playbookId"] = id;
        string resolvedPlaybook = playbook.ToJsonString(Pretty);
        var everyClass = ArcRelayLaunchClassIds.All.ToHashSet(
            StringComparer.Ordinal);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                Encoding.UTF8.GetBytes(resolvedPlaybook),
                Encoding.UTF8.GetBytes(resolvedLayout),
                $"templates/{id}/playbook.json",
                $"templates/{id}/layout.json");
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

    private static void NormalizeBindings(JsonObject layout, string id)
    {
        JsonArray source = layout["bindings"]?.AsArray()
            ?? throw new InvalidDataException(
                $"Embedded layout '{id}' has no bindings.");
        var normalized = new JsonArray();
        foreach (string side in new[] { "west", "east" })
        {
            JsonObject? selected = source
                .Select(value => value?.AsObject())
                .Where(value => string.Equals(
                    value?["ownReactorSide"]?.GetValue<string>(),
                    side,
                    StringComparison.Ordinal))
                .OrderByDescending(value => string.Equals(
                    value?["matchContractFingerprint"]?.GetValue<string>(),
                    "any-composition",
                    StringComparison.Ordinal))
                .FirstOrDefault();
            if (selected is null)
                throw new InvalidDataException(
                    $"Embedded layout '{id}' has no {side} binding.");
            JsonObject portable = selected.DeepClone().AsObject();
            portable["matchContractFingerprint"] = "any-composition";
            normalized.Add(portable);
        }
        layout["bindings"] = normalized;
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
