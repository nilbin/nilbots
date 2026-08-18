using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Strict shared compiler for Arc Relay Orders v2. It validates the authored
/// sheet/map-plan pair and links their canonical source trees directly into an
/// ATL2 package; there is no intermediate tactical-playbook representation.
/// </summary>
public static partial class ArcRelayTacticalPlaybookCompiler
{
    public const string PlaybookSchema = "arc-relay-orders-v2";
    public const string LayoutSchema = "arc-relay-map-plan-v2";
    public const int DirectEnvelopeMagic = 0x324C5441; // ATL2, little-endian.
    // Raised from 64 KiB for whole-team Tactics materialization and again
    // 128→160 KiB for the release gate's dead-excuse pairs (owner call,
    // 2026-08-18); the mind payload cap and the MindStart evaluation wire
    // cap move in lockstep.
    private const int MaximumLinkedBytes = 160 * 1024;

    public static TacticalPlaybookCompilation Compile(string playbookPath)
    {
        string fullPlaybookPath = Path.GetFullPath(playbookPath);
        byte[] playbookSource = File.ReadAllBytes(fullPlaybookPath);
        using JsonDocument playbookDocument = Parse(
            playbookSource, fullPlaybookPath);
        SheetValidation sheet = ValidateOrdersSheet(
            playbookDocument.RootElement, fullPlaybookPath);

        string relativeMapPlan = RequiredString(
            playbookDocument.RootElement, "mapPlan", fullPlaybookPath);
        string parent = Path.GetDirectoryName(fullPlaybookPath)
            ?? throw Error(fullPlaybookPath, "sheet has no parent directory.");
        string fullMapPlanPath = Path.GetFullPath(Path.Combine(
            parent, relativeMapPlan));
        byte[] mapPlanSource = File.ReadAllBytes(fullMapPlanPath);
        return CompileValidated(
            playbookSource,
            fullPlaybookPath,
            playbookDocument,
            sheet,
            mapPlanSource,
            fullMapPlanPath);
    }

    /// <summary>Compile an in-memory pair through the same validation and
    /// canonical linking path used by the CLI and hosted App.</summary>
    public static TacticalPlaybookCompilation Compile(
        byte[] playbookSource,
        byte[] layoutSource,
        string playbookName = "sheet.json",
        string layoutName = "map-plan.json")
    {
        ArgumentNullException.ThrowIfNull(playbookSource);
        ArgumentNullException.ThrowIfNull(layoutSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(playbookName);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutName);
        using JsonDocument playbookDocument = Parse(
            playbookSource, playbookName);
        SheetValidation sheet = ValidateOrdersSheet(
            playbookDocument.RootElement, playbookName);
        return CompileValidated(
            playbookSource,
            playbookName,
            playbookDocument,
            sheet,
            layoutSource,
            layoutName);
    }

    private static TacticalPlaybookCompilation CompileValidated(
        byte[] playbookSource,
        string playbookPath,
        JsonDocument playbookDocument,
        SheetValidation sheet,
        byte[] layoutSource,
        string layoutPath)
    {
        using JsonDocument layoutDocument = Parse(layoutSource, layoutPath);
        MapPlanValidation mapPlan = ValidateMapPlan(
            layoutDocument.RootElement, layoutPath);
        ValidateMapReferences(sheet, mapPlan, playbookPath, layoutPath);

        byte[] canonicalPlaybook = Canonicalize(
            playbookDocument.RootElement);
        byte[] canonicalLayout = Canonicalize(layoutDocument.RootElement);
        string playbookSha256 = Sha256(playbookSource);
        string layoutSha256 = Sha256(layoutSource);
        byte[] linked = Encode(
            playbookSha256,
            layoutSha256,
            canonicalPlaybook,
            canonicalLayout);
        return new TacticalPlaybookCompilation(
            playbookPath,
            playbookSha256,
            layoutPath,
            layoutSha256,
            sheet.Composition,
            canonicalPlaybook,
            canonicalLayout,
            linked,
            UsesOrderTree: true);
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
                $"Compiled Arc Relay sheet is {stream.Length} bytes; "
                + $"normalized sheet is {canonicalPlaybook.Length} bytes "
                + $"and map plan is {canonicalLayout.Length} bytes; the "
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
                MaxDepth = 48,
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path}: invalid JSON.", exception);
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
                    "Arc Relay sheets permit only objects, arrays, strings, "
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

    private static string Identifier(
        JsonElement owner,
        string name,
        string path)
    {
        string value = RequiredString(owner, name, path);
        IdentifierValue(value, $"{path}.{name}", name);
        return value;
    }

    private static void IdentifierValue(string value, string path, string name)
    {
        if (value.Length is < 1 or > 48
            || value.Any(character =>
                !char.IsAsciiLetterLower(character)
                && !char.IsAsciiDigit(character)
                && character != '-'))
        {
            throw Error(path,
                $"{name} must be 1..48 lowercase ASCII letters, digits, or '-'.");
        }
    }

    private static string RequiredString(
        JsonElement owner,
        string name,
        string path)
    {
        if (!owner.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
            throw Error(path, $"missing string field '{name}'.");
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
            throw Error(path, $"'{name}' must be a 32-bit integer.");
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

    private static string OneOf(
        JsonElement owner,
        string name,
        string path,
        params string[] legal)
    {
        string value = RequiredString(owner, name, path);
        if (!legal.Contains(value, StringComparer.Ordinal))
            throw Error(path,
                $"'{name}' must be one of: {string.Join(", ", legal)}.");
        return value;
    }

    private static void Exact(
        JsonElement owner,
        string name,
        string expected,
        string path)
    {
        string value = RequiredString(owner, name, path);
        if (!string.Equals(value, expected, StringComparison.Ordinal))
            throw Error(path, $"'{name}' must be '{expected}'.");
    }

    private static void Point(JsonElement value, string path)
    {
        JsonElement[] coordinates = BoundedArray(value, path, 2, 2);
        for (int index = 0; index < coordinates.Length; index++)
        {
            if (coordinates[index].ValueKind != JsonValueKind.Number
                || !coordinates[index].TryGetInt32(out int coordinate)
                || coordinate < 0)
                throw Error(path, "point coordinates must be non-negative integers.");
        }
    }

    private static void Rectangle(JsonElement value, string path)
    {
        JsonElement[] coordinates = BoundedArray(value, path, 4, 4);
        foreach (JsonElement coordinate in coordinates)
        {
            if (coordinate.ValueKind != JsonValueKind.Number
                || !coordinate.TryGetInt32(out int number)
                || number < 0)
                throw Error(path,
                    "rectangle coordinates must be non-negative integers.");
        }
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static InvalidDataException Error(string path, string message) =>
        new($"{path}: {message}");
}
