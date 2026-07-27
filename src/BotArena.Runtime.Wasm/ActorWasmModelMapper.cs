using System.Text.Json;
using System.Text.Json.Serialization;
using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Host-only adapter between the frozen Engine actor graph and its duplicated
/// SDK mirror. JSON is used only inside the trusted managed host; it never
/// enters the NativeAOT guest or the wire format.
/// </summary>
internal static class ActorWasmModelMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 64,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Sdk.ActorMatchStart ToSdk(ActorMatchStart value) =>
        Convert<ActorMatchStart, Sdk.ActorMatchStart>(value);

    public static Sdk.ActorContext ToSdk(ActorObservation value) =>
        Convert<ActorObservation, Sdk.ActorContext>(value);

    public static ActorDecision ToEngine(Sdk.ActorDecision value) =>
        Convert<Sdk.ActorDecision, ActorDecision>(value);

    private static TTarget Convert<TSource, TTarget>(TSource value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        return JsonSerializer.Deserialize<TTarget>(json, Options)
            ?? throw new InvalidOperationException(
                $"Could not map {typeof(TSource).Name} to {typeof(TTarget).Name}.");
    }
}
