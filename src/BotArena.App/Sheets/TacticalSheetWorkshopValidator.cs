using System.Text;
using System.Text.Json;

namespace BotArena.App.Sheets;

/// <summary>
/// Bounds and versions editor-only linked-Play metadata without interpreting
/// it as gameplay. Orders plus map plan remain the only compiler inputs.
/// </summary>
public static class TacticalSheetWorkshopValidator
{
    public const string Schema = "arc-relay-workshop-v1";
    public const int MaximumBytes = 192 * 1024;

    public static void Validate(string? workshopJson)
    {
        if (workshopJson is null)
            return;
        if (Encoding.UTF8.GetByteCount(workshopJson) > MaximumBytes)
            throw new InvalidDataException(
                $"workshop.json exceeds {MaximumBytes} bytes.");

        using JsonDocument document = JsonDocument.Parse(workshopJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                "workshop.json must contain one JSON object.");
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("schema", out JsonElement schema)
            || schema.ValueKind != JsonValueKind.String
            || !string.Equals(
                schema.GetString(), Schema, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"workshop.json must use schema '{Schema}'.");
        }
        if (!root.TryGetProperty("plays", out JsonElement plays)
            || plays.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "workshop.json needs a plays array.");
        }
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("schema" or "plays"))
            {
                throw new InvalidDataException(
                    $"workshop.json has unknown root field '{property.Name}'.");
            }
        }
    }
}
