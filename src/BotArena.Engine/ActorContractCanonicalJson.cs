using System.Text;
using System.Text.Json;

namespace BotArena.Engine;

/// <summary>Low-level helpers shared by the generation-3 contract writers.</summary>
internal static class ActorContractCanonicalJson
{
    public static string Write(Action<Utf8JsonWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Indented = false,
                       SkipValidation = false,
                   }))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void WritePosition(
        Utf8JsonWriter writer,
        Position position)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(position.X);
        writer.WriteNumberValue(position.Y);
        writer.WriteEndArray();
    }

    public static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        int? value)
    {
        if (value is { } number)
            writer.WriteNumber(propertyName, number);
        else
            writer.WriteNull(propertyName);
    }

    public static void WriteNullableString(
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value is not null)
            writer.WriteString(propertyName, value);
        else
            writer.WriteNull(propertyName);
    }
}
