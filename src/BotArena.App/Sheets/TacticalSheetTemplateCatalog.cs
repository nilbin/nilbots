using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.App.ArcRelay;
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
/// Reproducible product templates derived from tracked authoring sources.
/// External library references are resolved once here so every saved sheet is
/// still exactly the portable two-file format promised by the editor.
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
        string library = Resource("BotArena.Sheets.standard-v1.json");
        Template = Resolve(
            "starter-hunter",
            "Starter hunter",
            "A readable doctrine-first draft: one open hunter, two recruitable escorts, and a patrol floor.",
            Resource("BotArena.Sheets.hunter-v1.playbook.json"),
            Resource("BotArena.Sheets.hunter-v1.layout.json"),
            library,
            starterOnly: true);
        TacticalSheetSource[] opponents =
        [
            Resolve(
                "home-siege-v3",
                "Home Siege v3",
                "Commits to a living perimeter and converts control into safe returns.",
                Resource("BotArena.Sheets.home-siege-v3.playbook.json"),
                Resource("BotArena.Sheets.home-siege-v3.layout.json"),
                library),
            Resolve(
                "breakwater-v1",
                "Breakwater",
                "Recognizes committed pressure, absorbs it, then counterattacks the exposed route.",
                Resource("BotArena.Sheets.breakwater-v1.playbook.json"),
                Resource("BotArena.Sheets.breakwater-v1.layout.json"),
                library),
            Resolve(
                "ripen-harvest-v1",
                "Ripen Harvest",
                "Protects valuable loose Cores and chooses delayed collection windows.",
                Resource("BotArena.Sheets.ripen-harvest-v1.playbook.json"),
                Resource("BotArena.Sheets.ripen-harvest-v1.layout.json"),
                library),
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
        string layoutJson,
        string libraryJson,
        bool starterOnly = false)
    {
        JsonObject playbook = JsonNode.Parse(playbookJson)?.AsObject()
            ?? throw new InvalidDataException($"Embedded playbook '{id}' is empty.");
        JsonObject layout = JsonNode.Parse(layoutJson)?.AsObject()
            ?? throw new InvalidDataException($"Embedded layout '{id}' is empty.");
        JsonObject library = JsonNode.Parse(libraryJson)?.AsObject()
            ?? throw new InvalidDataException("Embedded tactical library is empty.");
        ResolveLibrary(playbook, library);
        NormalizeCustodyConditions(playbook, id);

        if (starterOnly)
        {
            MakeStarterComposition(playbook);
            CompleteStarterDoctrines(playbook);
            ProjectStarterLayoutToCurrentMap(layout);
        }
        layout["layoutId"] = id;
        layout["mapId"] = ArcRelayLoopProfile.Current.MapId;
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

    private static void ResolveLibrary(JsonObject playbook, JsonObject library)
    {
        JsonObject authoring = playbook["authoring"]?.AsObject()
            ?? throw new InvalidDataException(
                "Embedded playbook has no authoring catalog.");
        if (!authoring.Remove("library"))
            return;
        foreach (string section in new[]
                 {
                     "parameters", "fallbackPolicies", "assignmentProfiles",
                     "standingOrders", "maneuvers", "predicates",
                     "conditionSets",
                 })
        {
            JsonObject target = authoring[section]?.AsObject()
                ?? new JsonObject();
            if (library[section] is JsonObject shared)
            {
                foreach ((string key, JsonNode? value) in shared)
                {
                    if (target.ContainsKey(key))
                        throw new InvalidDataException(
                            $"Embedded playbook duplicates library entry '{key}'.");
                    target[key] = value?.DeepClone();
                }
            }
            authoring[section] = target;
        }
    }

    private static void MakeStarterComposition(JsonObject playbook)
    {
        playbook["composition"] = new JsonArray(
            "kestrel", "patchbay", "relay", "hush",
            "hush", "towline", "towline", "palisade");
        foreach (JsonNode? node in playbook["roles"]!.AsArray())
        {
            JsonObject role = node!.AsObject();
            switch (role["roleId"]!.GetValue<string>())
            {
                case "carrier":
                    role["candidateClasses"] = new JsonArray("hush");
                    break;
                case "eyes":
                    role["candidateClasses"] = new JsonArray("palisade");
                    break;
                case "reserve":
                    role["candidateClasses"] = new JsonArray(
                        "kestrel", "patchbay", "relay", "hush",
                        "towline", "palisade", "lantern", "switchback");
                    break;
            }
        }
    }

    private static void CompleteStarterDoctrines(JsonObject playbook)
    {
        JsonObject doctrines = playbook["doctrines"]?.AsObject()
            ?? throw new InvalidDataException(
                "Embedded starter playbook has no doctrines.");
        HashSet<string> covered = doctrines
            .Select(value => value.Value?["role"]?.GetValue<string>())
            .Where(value => value is not null)
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (JsonNode? node in playbook["roles"]!.AsArray())
        {
            string role = node!["roleId"]!.GetValue<string>();
            if (covered.Contains(role))
                continue;
            doctrines[$"{role}-baseline"] = new JsonObject
            {
                ["role"] = role,
                ["custody"] = "well-custody",
                ["conceal"] = false,
                ["modes"] = new JsonArray
                {
                    new JsonObject { ["squad"] = true },
                },
            };
        }
    }

    private static void ProjectStarterLayoutToCurrentMap(JsonObject layout)
    {
        // hunter-v1 is the sole tracked doctrine-first teaching source, but
        // its geometry was authored on the 31x27 ambush-warren study map.
        // The product contract is now the 31x23 Counterflow map. Preserve the
        // authored relative geography while projecting every plotted point
        // into the current replay-header dimensions; the editor then shows
        // those points over the exact engine map instead of silently serving
        // coordinates from a retired board.
        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        const int sourceMaximumX = 30;
        const int sourceMaximumY = 26;

        foreach (JsonNode? node in layout["zones"]!.AsArray())
            ProjectTuple(node!["rect"]!.AsArray(), 0, 1, 2, 3);
        foreach (JsonNode? node in layout["routes"]!.AsArray())
        foreach (JsonNode? waypoint in node!["waypoints"]!.AsArray())
            ProjectTuple(waypoint!.AsArray(), 0, 1);
        foreach (JsonNode? node in layout["anchors"]!.AsArray())
            ProjectTuple(node!["position"]!.AsArray(), 0, 1);

        void ProjectTuple(JsonArray values, params int[] indexes)
        {
            for (int offset = 0; offset < indexes.Length; offset += 2)
            {
                int xIndex = indexes[offset];
                int yIndex = indexes[offset + 1];
                int x = values[xIndex]!.GetValue<int>();
                int y = values[yIndex]!.GetValue<int>();
                values[xIndex] = Project(x, sourceMaximumX, map.Width - 1);
                values[yIndex] = Project(y, sourceMaximumY, map.Height - 1);
            }
        }

        static int Project(int value, int sourceMaximum, int targetMaximum) =>
            (int)Math.Round(
                value * targetMaximum / (double)sourceMaximum,
                MidpointRounding.AwayFromZero);
    }

    private static void NormalizeCustodyConditions(
        JsonObject playbook,
        string id)
    {
        JsonObject authoring = playbook["authoring"]!.AsObject();
        JsonObject predicates = authoring["predicates"]!.AsObject();
        JsonObject conditionSets = authoring["conditionSets"]!.AsObject();
        foreach (JsonNode? node in playbook["custodyPolicies"]!.AsArray())
        {
            JsonObject policy = node!.AsObject();
            if (policy.Remove(
                    "safeConversionConditionSetId",
                    out JsonNode? safeReference))
            {
                policy["safeConversionAll"] = ExpandConditions(
                    safeReference!.GetValue<string>(),
                    predicates,
                    conditionSets,
                    id);
            }
            if (policy["baitDrop"] is JsonObject bait
                && bait.Remove(
                    "reclaimConditionSetId",
                    out JsonNode? reclaimReference))
            {
                bait["reclaimAll"] = ExpandConditions(
                    reclaimReference!.GetValue<string>(),
                    predicates,
                    conditionSets,
                    id);
            }
        }
    }

    private static JsonArray ExpandConditions(
        string conditionSetId,
        JsonObject predicates,
        JsonObject conditionSets,
        string id)
    {
        JsonArray alternatives = conditionSets[conditionSetId]?.AsArray()
            ?? throw new InvalidDataException(
                $"Embedded playbook '{id}' references unknown condition set "
                + $"'{conditionSetId}'.");
        var expanded = new JsonArray();
        foreach (JsonNode? alternative in alternatives)
        {
            var all = new JsonArray();
            foreach (JsonNode? reference in alternative!.AsArray())
            {
                string predicateId = reference!.GetValue<string>();
                JsonNode predicate = predicates[predicateId]
                    ?? throw new InvalidDataException(
                        $"Embedded playbook '{id}' references unknown predicate "
                        + $"'{predicateId}'.");
                all.Add(predicate.DeepClone());
            }
            expanded.Add(new JsonObject { ["all"] = all });
        }
        return expanded;
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
