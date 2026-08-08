using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.App.ArcRelay;

public sealed record ArcRelayCrestDescriptor(
    string Key,
    int Variant,
    string Shape,
    string Pattern,
    string Mark,
    string Primary,
    string Secondary,
    string Detail);

/// <summary>
/// Stable procedural crest grammar. Only the selected variant is persisted;
/// every shape, part and palette choice is reproduced from identity + variant.
/// </summary>
public static class ArcRelayCrestGenerator
{
    public const int MinimumVariant = 0;
    public const int MaximumVariant = 4095;

    private static readonly string[] Shapes =
        ["hex", "shield", "roundel", "diamond", "notched"];
    private static readonly string[] Patterns =
        ["split", "band", "chevron", "quartered", "core"];
    private static readonly string[] Marks =
        ["arc", "relay", "trident", "orbit", "tower", "wing", "spark", "gate"];
    private static readonly (string Primary, string Secondary, string Detail)[] Palettes =
    [
        ("#d7903f", "#27333d", "#ffd386"),
        ("#4fc7b8", "#182f39", "#b4fff2"),
        ("#8e77d8", "#2c263c", "#dfd5ff"),
        ("#d35e67", "#36272c", "#ffc4c8"),
        ("#8fab58", "#263328", "#dfffa8"),
        ("#d7c05a", "#343026", "#fff0a3"),
        ("#6299d1", "#202e3b", "#c7e7ff"),
        ("#c276bd", "#35263a", "#ffd0fb"),
    ];

    public static ArcRelayCrestDescriptor Create(Guid entrantId, int variant)
    {
        if (entrantId == Guid.Empty)
            throw new ArgumentException("A crest needs an entrant identity.", nameof(entrantId));
        if (variant is < MinimumVariant or > MaximumVariant)
            throw new ArgumentOutOfRangeException(nameof(variant));

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"nilbots:arc-relay:crest:v1:{entrantId:D}:{variant}"));
        (string primary, string secondary, string detail) =
            Palettes[digest[3] % Palettes.Length];
        return new ArcRelayCrestDescriptor(
            Convert.ToHexStringLower(digest.AsSpan(0, 8)),
            variant,
            Shapes[digest[0] % Shapes.Length],
            Patterns[digest[1] % Patterns.Length],
            Marks[digest[2] % Marks.Length],
            primary,
            secondary,
            detail);
    }

    public static string Snapshot(Guid entrantId, int variant) =>
        JsonSerializer.Serialize(Create(entrantId, variant), JsonOptions);

    public static ArcRelayCrestDescriptor ReadSnapshot(string json) =>
        JsonSerializer.Deserialize<ArcRelayCrestDescriptor>(json, JsonOptions)
        ?? throw new InvalidDataException("Entrant crest snapshot is empty.");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
