using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.App.ArcRelay;

/// <summary>
/// Ordered launch composition. The adaptive fields are reserved wire space and
/// must remain empty in the first hosted entrant version.
/// </summary>
public sealed record ArcRelayCompositionDeclaration(
    IReadOnlyList<string> ClassIds,
    string? AdaptivePolicyId = null,
    IReadOnlyList<string>? AdaptiveClassIds = null);

public sealed record ArcRelayCompositionCompilation(
    string CanonicalJson,
    string ContentHash,
    IReadOnlyList<string> ClassIds);

public static class ArcRelayComposition
{
    public static ArcRelayCompositionCompilation Compile(
        ArcRelayCompositionDeclaration declaration,
        ArcRelayPlayerSheetCodec sheetCodec,
        IReadOnlySet<string> unlockedClassIds)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.AdaptivePolicyId is not null ||
            declaration.AdaptiveClassIds is { Count: > 0 })
        {
            throw new InvalidDataException(
                "Adaptive composition is reserved and must be empty in v1.");
        }

        string[] classes = sheetCodec.ValidateComposition(
            declaration.ClassIds,
            unlockedClassIds);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WritePropertyName("classIds");
            writer.WriteStartArray();
            foreach (string classId in classes)
                writer.WriteStringValue(classId);
            writer.WriteEndArray();
            writer.WriteNull("adaptivePolicyId");
            writer.WritePropertyName("adaptiveClassIds");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        byte[] canonical = stream.ToArray();
        return new ArcRelayCompositionCompilation(
            Encoding.UTF8.GetString(canonical),
            Convert.ToHexStringLower(SHA256.HashData(canonical)),
            classes);
    }

    public static ArcRelayCompositionDeclaration Read(string canonicalJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalJson);
        using JsonDocument document = JsonDocument.Parse(canonicalJson);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unknown Arc Relay composition schema.");
        return new ArcRelayCompositionDeclaration(
            root.GetProperty("classIds")
                .EnumerateArray()
                .Select(value => value.GetString()
                    ?? throw new InvalidDataException("Composition class id is null."))
                .ToArray(),
            root.GetProperty("adaptivePolicyId").ValueKind == JsonValueKind.Null
                ? null
                : root.GetProperty("adaptivePolicyId").GetString(),
            root.GetProperty("adaptiveClassIds")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());
    }
}
