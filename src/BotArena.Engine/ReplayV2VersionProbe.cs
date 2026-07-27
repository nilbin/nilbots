using System.Text.Json;

namespace BotArena.Engine;

internal enum ReplayV2DocumentFormat
{
    LegacyV1 = 1,
    EntityV2 = 2,
}

/// <summary>
/// Minimal discriminator for future callers that need to select a codec before
/// typed deserialization. It intentionally does not alter ReplaySerializer.
/// </summary>
internal static class ReplayV2VersionProbe
{
    public static ReplayV2DocumentFormat Probe(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("header", out JsonElement header)
            || header.ValueKind != JsonValueKind.Object
            || !header.TryGetProperty(
                "replayVersion",
                out JsonElement replayVersion)
            || replayVersion.ValueKind != JsonValueKind.Number
            || !replayVersion.TryGetInt32(out int version))
        {
            throw new InvalidDataException(
                "Replay header must contain an integer replayVersion.");
        }

        return version switch
        {
            BotArenaVersions.ReplayFormatVersion =>
                ReplayV2DocumentFormat.LegacyV1,
            BotArenaVersions.EntityReplayFormatVersion =>
                ReplayV2DocumentFormat.EntityV2,
            _ => throw new NotSupportedException(
                $"Unsupported replay format version {version}."),
        };
    }
}
