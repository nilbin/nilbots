using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BotArena.App.Shared;

/// <summary>
/// Narrows numeric schemas to just their numeric type.
/// <para>
/// ASP.NET emits numbers as <c>"type": ["integer", "string"]</c> with a validation pattern,
/// because a client *may* send the string form on input. Nothing we serve does:
/// System.Text.Json always writes numbers as numbers. Left alone, the union propagates into
/// every generated client — TypeScript gets <c>number | string</c>, so `rating &gt; 0` stops
/// compiling and each call site needs a cast that asserts something the server already
/// guarantees.
/// </para>
/// <para>
/// Fixing it here keeps the document honest for every consumer instead of pushing the
/// problem into one of them. The pattern goes too — it exists only to validate the string
/// alternative being removed.
/// </para>
/// </summary>
public sealed class NumericSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Type is not { } type || !type.HasFlag(JsonSchemaType.String))
            return Task.CompletedTask;

        bool numeric = type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number);
        if (!numeric)
            return Task.CompletedTask;

        schema.Type = type & ~JsonSchemaType.String;
        schema.Pattern = null;
        return Task.CompletedTask;
    }
}
