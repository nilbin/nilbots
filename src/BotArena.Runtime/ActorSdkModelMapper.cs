using System.Text.Json;
using System.Text.Json.Serialization;
using BotArena.Engine;

namespace BotArena.Runtime;

/// <summary>
/// Diagnostic in-process adapter between deliberately duplicated Engine and
/// SDK actor contracts. It reuses the vNext camel-case/numeric-enum shape so
/// in-process and canonical WASM exercise the same public field boundary.
/// Contract-drift tests pin both object graphs.
/// </summary>
internal static class ActorSdkModelMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 64,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Sdk.ActorMatchStart ToSdk(ActorMatchStart start) =>
        Convert<ActorMatchStart, Sdk.ActorMatchStart>(start);

    public static Sdk.ActorContext ToSdk(ActorObservation observation) =>
        Convert<ActorObservation, Sdk.ActorContext>(observation);

    public static ActorDecision ToEngine(Sdk.ActorDecision decision) =>
        Convert<Sdk.ActorDecision, ActorDecision>(decision);

    private static TTarget Convert<TSource, TTarget>(TSource source)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(source, Options);
        return JsonSerializer.Deserialize<TTarget>(json, Options)
            ?? throw new InvalidOperationException(
                $"Could not map {typeof(TSource).Name} to {typeof(TTarget).Name}.");
    }
}
