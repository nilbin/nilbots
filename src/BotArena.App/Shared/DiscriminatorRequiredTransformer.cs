using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BotArena.App.Shared;

/// <summary>
/// Marks a polymorphic type's discriminator as required.
/// <para>
/// ASP.NET describes the discriminator of a <c>[JsonPolymorphic]</c> type as an ordinary
/// optional property, because nothing in the schema says the server always writes it. It
/// always does — System.Text.Json emits the discriminator on every payload serialized
/// through the base type.
/// </para>
/// <para>
/// Left optional, the cost lands entirely on clients and is easy to misdiagnose:
/// TypeScript cannot use an optional discriminant to narrow a union, so
/// <c>payload.kind === 'set-settled'</c> narrows in the true branch but not the false one —
/// both members remain possible, since either could have an absent <c>kind</c>. Every
/// consumer then works around it with a cast or a property probe, for a field the server
/// guarantees.
/// </para>
/// </summary>
public sealed class DiscriminatorRequiredTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        string? discriminator = schema.Discriminator?.PropertyName;
        if (discriminator is null)
            return Task.CompletedTask;

        // The discriminator is declared on the base schema, but it is each *derived*
        // schema that carries the property a client narrows on.
        MarkRequired(schema, discriminator);
        foreach (OpenApiSchema derived in DerivedSchemas(schema))
            MarkRequired(derived, discriminator);

        return Task.CompletedTask;
    }

    private static IEnumerable<OpenApiSchema> DerivedSchemas(OpenApiSchema schema) =>
        (schema.OneOf ?? []).OfType<OpenApiSchema>()
            .Concat((schema.AnyOf ?? []).OfType<OpenApiSchema>());

    private static void MarkRequired(OpenApiSchema schema, string property)
    {
        if (schema.Properties?.ContainsKey(property) is not true)
            return;
        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add(property);
    }
}
