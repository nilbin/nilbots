using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Strict compiler for the consolidated Arc Relay sheet DSL. The compiler
/// validates the words the author wrote, binds the separately hashed layout,
/// and emits one ATL1 package. There is no expanded compatibility IR.
/// </summary>
public static partial class ArcRelayTacticalPlaybookCompiler
{
    public const string PlaybookSchema = "arc-relay-tactical-playbook-v1";
    public const string LayoutSchema = "arc-relay-tactical-layout-v1";
    public const int DirectEnvelopeMagic = 0x314C5441; // ATL1, little-endian.
    private const int MaximumLinkedBytes = 64 * 1024;

    public static TacticalPlaybookCompilation Compile(string playbookPath)
    {
        string fullPlaybookPath = Path.GetFullPath(playbookPath);
        byte[] playbookSource = File.ReadAllBytes(fullPlaybookPath);
        using JsonDocument playbookDocument = Parse(
            playbookSource, fullPlaybookPath);
        ValidateConsolidatedSheet(playbookDocument.RootElement, fullPlaybookPath);

        JsonElement layoutReference = playbookDocument.RootElement
            .GetProperty("layout");
        string relativeLayoutPath = RequiredString(
            layoutReference, "path", $"{fullPlaybookPath}.layout");
        string parent = Path.GetDirectoryName(fullPlaybookPath)
            ?? throw Error(fullPlaybookPath,
                "playbook has no parent directory.");
        string fullLayoutPath = Path.GetFullPath(Path.Combine(
            parent, relativeLayoutPath));
        byte[] layoutSource = File.ReadAllBytes(fullLayoutPath);
        return CompileValidated(
            playbookSource,
            fullPlaybookPath,
            playbookDocument,
            layoutSource,
            fullLayoutPath);
    }

    /// <summary>Compiles an in-memory playbook/layout pair through the same
    /// validation and linking path used by the CLI and hosted App.</summary>
    public static TacticalPlaybookCompilation Compile(
        byte[] playbookSource,
        byte[] layoutSource,
        string playbookName = "playbook.json",
        string layoutName = "layout.json")
    {
        ArgumentNullException.ThrowIfNull(playbookSource);
        ArgumentNullException.ThrowIfNull(layoutSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(playbookName);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutName);

        using JsonDocument playbookDocument = Parse(
            playbookSource, playbookName);
        ValidateConsolidatedSheet(playbookDocument.RootElement, playbookName);
        return CompileValidated(
            playbookSource,
            playbookName,
            playbookDocument,
            layoutSource,
            layoutName);
    }

    private static TacticalPlaybookCompilation CompileValidated(
        byte[] playbookSource,
        string playbookPath,
        JsonDocument playbookDocument,
        byte[] layoutSource,
        string layoutPath)
    {
        JsonElement playbook = playbookDocument.RootElement;
        string layoutSha256 = Sha256(layoutSource);
        string expectedLayoutSha256 = RequiredString(
            playbook.GetProperty("layout"), "sha256",
            $"{playbookPath}.layout");
        if (!string.Equals(
                expectedLayoutSha256,
                layoutSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{playbookPath}: layout hash mismatch; expected "
                + $"{expectedLayoutSha256}, found {layoutSha256}.");
        }

        using JsonDocument layoutDocument = Parse(layoutSource, layoutPath);
        JsonElement layout = layoutDocument.RootElement;
        ValidateLayout(layout, layoutPath);
        ValidateLayoutReferences(playbook, layout, playbookPath);

        byte[] canonicalPlaybook = NormalizePlaybook(playbook);
        byte[] canonicalLayout = Canonicalize(layout);
        string playbookSha256 = Sha256(playbookSource);
        byte[] linked = Encode(
            playbookSha256,
            layoutSha256,
            canonicalPlaybook,
            canonicalLayout);
        string[] composition = playbook.GetProperty("composition")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        return new TacticalPlaybookCompilation(
            playbookPath,
            playbookSha256,
            layoutPath,
            layoutSha256,
            composition,
            canonicalPlaybook,
            canonicalLayout,
            linked,
            UsesDirectLadder: true);
    }

    private static void ValidateLayout(JsonElement root, string path)
    {
        Object(root, path,
            [
                "schema", "layoutId", "mapId", "bindings", "zones",
                "routes", "anchors",
            ]);
        Exact(root, "schema", LayoutSchema, path);
        Identifier(root, "layoutId", path);
        NonEmptyString(root, "mapId", path);

        JsonElement[] bindings = BoundedArray(
            root.GetProperty("bindings"), $"{path}.bindings", 2, 16);
        var bindingKeys = new HashSet<string>(StringComparer.Ordinal);
        var sides = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement binding in bindings)
        {
            string at = $"{path}.bindings";
            Object(binding, at,
                [
                    "matchContractFingerprint", "ownReactorSide",
                    "transform", "routeAliases",
                ],
                ["formationAliases"]);
            string fingerprint = RequiredString(
                binding, "matchContractFingerprint", at);
            if (!string.Equals(
                    fingerprint, "any-composition", StringComparison.Ordinal))
            {
                Hash(binding, "matchContractFingerprint", at);
            }
            string side = OneOf(
                binding, "ownReactorSide", at, "west", "east");
            OneOf(binding, "transform", at,
                "identity", "mirror-x", "rotate-180");
            if (!bindingKeys.Add($"{fingerprint}|{side}"))
                throw Error(at, $"duplicate layout binding '{fingerprint}|{side}'.");
            sides.Add(side);
            ValidateStringMap(
                binding.GetProperty("routeAliases"),
                $"{at}.routeAliases", 32);
            if (binding.TryGetProperty(
                    "formationAliases", out JsonElement obsoleteAliases))
            {
                ValidateStringMap(
                    obsoleteAliases, $"{at}.formationAliases", 0);
            }
        }
        if (!sides.SetEquals(["west", "east"]))
            throw Error(path, "layout needs at least one west and one east binding.");

        JsonElement[] zones = BoundedArray(
            root.GetProperty("zones"), $"{path}.zones", 1, 64);
        UniqueIds(zones, "zoneId", $"{path}.zones");
        foreach (JsonElement zone in zones)
        {
            Object(zone, $"{path}.zones", ["zoneId", "rect"]);
            Rectangle(zone.GetProperty("rect"), $"{path}.zones.rect");
        }

        JsonElement[] routes = BoundedArray(
            root.GetProperty("routes"), $"{path}.routes", 1, 64);
        HashSet<string> routeIds = UniqueIds(
            routes, "routeId", $"{path}.routes");
        foreach (JsonElement route in routes)
        {
            Object(route, $"{path}.routes",
                ["routeId", "waypoints", "corridorWidth"]);
            foreach (JsonElement point in BoundedArray(
                         route.GetProperty("waypoints"),
                         $"{path}.routes.waypoints", 1, 128))
            {
                Point(point, $"{path}.routes.waypoints");
            }
            Range(route, "corridorWidth", $"{path}.routes", 0, 8);
        }
        foreach (JsonElement binding in bindings)
        foreach (JsonProperty alias in binding.GetProperty("routeAliases")
                     .EnumerateObject())
        {
            string target = alias.Value.GetString()!;
            if (!routeIds.Contains(alias.Name) || !routeIds.Contains(target))
            {
                throw Error(path,
                    $"route alias '{alias.Name}' -> '{target}' references "
                    + "an unknown route.");
            }
        }

        JsonElement[] anchors = BoundedArray(
            root.GetProperty("anchors"), $"{path}.anchors", 1, 64);
        UniqueIds(anchors, "anchorId", $"{path}.anchors");
        foreach (JsonElement anchor in anchors)
        {
            Object(anchor, $"{path}.anchors", ["anchorId", "position"]);
            Point(anchor.GetProperty("position"), $"{path}.anchors.position");
        }
    }

    private static void ValidateLayoutReferences(
        JsonElement playbook,
        JsonElement layout,
        string path)
    {
        HashSet<string> zones = layout.GetProperty("zones")
            .EnumerateArray()
            .Select(value => value.GetProperty("zoneId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> routes = layout.GetProperty("routes")
            .EnumerateArray()
            .Select(value => value.GetProperty("routeId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> anchors = layout.GetProperty("anchors")
            .EnumerateArray()
            .Select(value => value.GetProperty("anchorId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (JsonProperty role in playbook.GetProperty("doctrines")
                     .GetProperty("roles").EnumerateObject())
        {
            string rolePath = $"{path}.doctrines.roles.{role.Name}";
            JsonElement doctrine = role.Value;
            if (doctrine.TryGetProperty("scavenge", out JsonElement scavenge))
            {
                IEnumerable<string> named = scavenge.ValueKind == JsonValueKind.String
                    ? [scavenge.GetString()!]
                    : scavenge.EnumerateArray().Select(value => value.GetString()!);
                foreach (string zone in named)
                    Reference(zones, zone, $"{rolePath}.scavenge", "zone");
            }
            int index = 0;
            foreach (JsonElement mode in doctrine.GetProperty("modes")
                         .EnumerateArray())
            {
                string modePath = $"{rolePath}.modes[{index++}]";
                foreach (string verb in new[] { "patrol", "assault" })
                {
                    if (!mode.TryGetProperty(verb, out JsonElement destination))
                        continue;
                    (string Kind, string Id) target = Destination(
                        destination, $"{modePath}.{verb}");
                    Reference(target.Kind == "route" ? routes : zones,
                        target.Id, $"{modePath}.{verb}", target.Kind);
                }
                if (mode.TryGetProperty("from", out JsonElement from))
                    Reference(anchors, from.GetString()!, modePath, "anchor");
            }
        }

        foreach (JsonProperty condition in playbook.GetProperty("conditions")
                     .EnumerateObject())
        {
            if (condition.Value.TryGetProperty("zone", out JsonElement zone))
            {
                Reference(zones, zone.GetString()!,
                    $"{path}.conditions.{condition.Name}", "zone");
            }
        }

        foreach (JsonElement custody in playbook.GetProperty("custodyPolicies")
                     .EnumerateArray())
        {
            string id = custody.GetProperty("custodyId").GetString()!;
            string at = $"{path}.custodyPolicies.{id}";
            if (custody.TryGetProperty(
                    "emergencyRecoveryZones", out JsonElement emergencyZones))
            {
                foreach (JsonElement zone in emergencyZones.EnumerateArray())
                    Reference(zones, zone.GetString()!, at, "zone");
            }
            if (custody.TryGetProperty(
                    "emergencyDisplacementTarget", out JsonElement target))
            {
                Reference(anchors, target.GetString()!, at, "anchor");
            }
            if (custody.TryGetProperty("deliveryRoutes", out JsonElement rules))
            {
                foreach (JsonElement rule in rules.EnumerateArray())
                {
                    Reference(zones, rule.GetProperty("zone").GetString()!,
                        $"{at}.deliveryRoutes", "zone");
                    Reference(routes, rule.GetProperty("route").GetString()!,
                        $"{at}.deliveryRoutes", "route");
                }
            }
            if (custody.TryGetProperty("baitDrop", out JsonElement bait))
                Reference(zones, bait.GetProperty("zone").GetString()!,
                    $"{at}.baitDrop", "zone");
        }
    }

    private static byte[] Encode(
        string playbookSha256,
        string layoutSha256,
        byte[] canonicalPlaybook,
        byte[] canonicalLayout)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(DirectEnvelopeMagic);
        writer.Write(PlaybookSchema);
        writer.Write(playbookSha256);
        writer.Write(layoutSha256);
        writer.Write(canonicalPlaybook.Length);
        writer.Write(canonicalPlaybook);
        writer.Write(canonicalLayout.Length);
        writer.Write(canonicalLayout);
        writer.Flush();
        if (stream.Length > MaximumLinkedBytes)
        {
            throw new InvalidDataException(
                $"Compiled tactical playbook is {stream.Length} bytes; "
                + $"normalized playbook is {canonicalPlaybook.Length} bytes "
                + $"and layout is {canonicalLayout.Length} bytes; the "
                + $"runtime limit is {MaximumLinkedBytes} bytes.");
        }
        return stream.ToArray();
    }

    private static JsonDocument Parse(byte[] source, string path)
    {
        try
        {
            return JsonDocument.Parse(source, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path}: invalid JSON.", exception);
        }
    }

    private static byte[] NormalizePlaybook(JsonElement source)
    {
        JsonNode root = JsonNode.Parse(source.GetRawText())
            ?? throw new InvalidDataException("Tactical playbook is empty.");
        NormalizeNode(root);
        using JsonDocument normalized = JsonDocument.Parse(root.ToJsonString());
        return Canonicalize(normalized.RootElement);
    }

    private static void NormalizeNode(JsonNode node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("fact"))
            {
                value.TryAdd("subject", "");
                value.TryAdd("zone", "");
                value.TryAdd("freshnessTicks", 0);
            }
            foreach (JsonNode? child in value.Select(item => item.Value).ToArray())
            {
                if (child is not null)
                    NormalizeNode(child);
            }
            return;
        }
        if (node is not JsonArray array)
            return;
        foreach (JsonNode? child in array)
        {
            if (child is not null)
                NormalizeNode(child);
        }
    }

    private static byte[] Canonicalize(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, root);
        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject()
                             .OrderBy(property => property.Name,
                                 StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number when value.TryGetInt32(out int number):
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            default:
                throw new InvalidDataException(
                    "Tactical sheets permit only objects, arrays, strings, "
                    + "32-bit integers, and booleans.");
        }
    }

    private static void Object(
        JsonElement value,
        string path,
        IReadOnlyCollection<string> required,
        IReadOnlyCollection<string>? optional = null)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected object.");
        optional ??= [];
        HashSet<string> allowed = required.Concat(optional)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw Error(path, $"duplicate field '{property.Name}'.");
            if (!allowed.Contains(property.Name))
                throw Error(path, $"unknown field '{property.Name}'.");
        }
        foreach (string name in required)
        {
            if (!seen.Contains(name))
                throw Error(path, $"missing required field '{name}'.");
        }
    }

    private static JsonElement[] BoundedArray(
        JsonElement value,
        string path,
        int minimum,
        int maximum)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Error(path, "expected array.");
        JsonElement[] entries = value.EnumerateArray().ToArray();
        if (entries.Length < minimum || entries.Length > maximum)
            throw Error(path, $"expected {minimum}..{maximum} entries.");
        return entries;
    }

    private static HashSet<string> UniqueIds(
        IReadOnlyCollection<JsonElement> values,
        string name,
        string path)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in values)
        {
            string id = Identifier(value, name, path);
            if (!result.Add(id))
                throw Error(path, $"duplicate {name} '{id}'.");
        }
        return result;
    }

    private static string Identifier(JsonElement owner, string name, string path)
    {
        string value = RequiredString(owner, name, path);
        IdentifierValue(value, $"{path}.{name}", name);
        return value;
    }

    private static void IdentifierValue(string value, string path, string name)
    {
        if (value.Length is < 1 or > 64
            || value.Any(character =>
                !char.IsAsciiLetterLower(character)
                && !char.IsAsciiDigit(character)
                && character != '-'))
        {
            throw Error(path,
                $"{name} must be 1..64 lowercase ASCII letters, digits, or '-'.");
        }
    }

    private static string RequiredString(
        JsonElement owner,
        string name,
        string path)
    {
        if (!owner.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
        {
            throw Error(path, $"missing string field '{name}'.");
        }
        return value.GetString()!;
    }

    private static string NonEmptyString(
        JsonElement owner,
        string name,
        string path)
    {
        string value = RequiredString(owner, name, path);
        if (string.IsNullOrWhiteSpace(value))
            throw Error(path, $"'{name}' must not be empty.");
        return value;
    }

    private static string StringValue(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw Error(path, "expected string.");
        return value.GetString()!;
    }

    private static string[] StringArray(
        JsonElement value,
        string path,
        int minimum,
        int maximum)
    {
        string[] entries = StringValues(value, path, minimum, maximum);
        if (entries.Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw Error(path, "values must be unique.");
        return entries;
    }

    private static string[] StringValues(
        JsonElement value,
        string path,
        int minimum,
        int maximum) => BoundedArray(value, path, minimum, maximum)
        .Select((entry, index) => StringValue(entry, $"{path}[{index}]"))
        .ToArray();

    private static int RequiredInt(
        JsonElement owner,
        string name,
        string path)
    {
        if (!owner.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out int result))
        {
            throw Error(path, $"'{name}' must be a 32-bit integer.");
        }
        return result;
    }

    private static int Range(
        JsonElement owner,
        string name,
        string path,
        int minimum,
        int maximum)
    {
        int value = RequiredInt(owner, name, path);
        if (value < minimum || value > maximum)
            throw Error(path, $"'{name}' must be in [{minimum}, {maximum}].");
        return value;
    }

    private static bool RequiredBool(
        JsonElement owner,
        string name,
        string path)
    {
        if (!owner.TryGetProperty(name, out JsonElement value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw Error(path, $"'{name}' must be a boolean.");
        }
        return value.GetBoolean();
    }

    private static string OneOf(
        JsonElement owner,
        string name,
        string path,
        params string[] legal)
    {
        string value = RequiredString(owner, name, path);
        if (!legal.Contains(value, StringComparer.Ordinal))
        {
            throw Error(path,
                $"'{name}' must be one of: {string.Join(", ", legal)}.");
        }
        return value;
    }

    private static void Exact(
        JsonElement owner,
        string name,
        string expected,
        string path)
    {
        string actual = RequiredString(owner, name, path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw Error(path, $"'{name}' must be '{expected}'.");
    }

    private static string Hash(JsonElement owner, string name, string path)
    {
        string value = RequiredString(owner, name, path);
        if (value.Length != 64 || value.Any(character =>
                !char.IsAsciiHexDigit(character)
                || char.IsAsciiLetterUpper(character)))
        {
            throw Error(path, $"'{name}' must be a lowercase SHA-256 hex digest.");
        }
        return value;
    }

    private static void Point(JsonElement value, string path)
    {
        JsonElement[] coordinates = BoundedArray(value, path, 2, 2);
        foreach (JsonElement coordinate in coordinates)
        {
            if (!coordinate.TryGetInt32(out int number) || number is < 0 or > 255)
                throw Error(path, "coordinates must be integers in [0, 255].");
        }
    }

    private static void Rectangle(JsonElement value, string path)
    {
        JsonElement[] coordinates = BoundedArray(value, path, 4, 4);
        int[] numbers = coordinates.Select(coordinate =>
            coordinate.TryGetInt32(out int number) ? number : -1).ToArray();
        if (numbers.Any(number => number is < 0 or > 255)
            || numbers[0] > numbers[2] || numbers[1] > numbers[3])
        {
            throw Error(path,
                "rectangle must be [x0,y0,x1,y1] integers in [0,255] with "
                + "ordered corners.");
        }
    }

    private static void ValidateStringMap(
        JsonElement value,
        string path,
        int maximum)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected object.");
        JsonProperty[] entries = value.EnumerateObject().ToArray();
        if (entries.Length > maximum
            || entries.Any(entry => entry.Value.ValueKind != JsonValueKind.String))
        {
            throw Error(path,
                $"expected at most {maximum} string-valued entries.");
        }
    }

    private static (string Kind, string Id) Destination(
        JsonElement value,
        string path)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string route = value.GetString()!;
            IdentifierValue(route, path, "route");
            return ("route", route);
        }
        Object(value, path, [], ["route", "zone"]);
        bool hasRoute = value.TryGetProperty("route", out JsonElement routeValue);
        bool hasZone = value.TryGetProperty("zone", out JsonElement zoneValue);
        if (hasRoute == hasZone)
            throw Error(path, "destination needs exactly one route or zone.");
        string kind = hasRoute ? "route" : "zone";
        string id = (hasRoute ? routeValue : zoneValue).GetString()!;
        IdentifierValue(id, path, kind);
        return (kind, id);
    }

    private static void Reference(
        IReadOnlySet<string> ids,
        string id,
        string path,
        string kind)
    {
        if (!ids.Contains(id))
            throw Error(path, $"references unknown {kind} '{id}'.");
    }

    private static string Sha256(byte[] source) =>
        Convert.ToHexStringLower(SHA256.HashData(source));

    private static InvalidDataException Error(string path, string message) =>
        new($"{path}: {message}");
}
