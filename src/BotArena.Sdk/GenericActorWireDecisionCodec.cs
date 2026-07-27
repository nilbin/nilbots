namespace BotArena.Sdk;

/// <summary>Strict tagged payload codec for schema-2 generic decisions.</summary>
internal static class GenericActorWireDecisionCodec
{
    public static byte[] Encode(GenericActorDecision value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.ActionId));
        writer.Field(2, ActorWireValue.Int32(value.ActionCode));
        writer.Field(
            3,
            GenericActorWireActionCodec.EncodeArguments(value.Arguments));
        writer.Optional(
            4,
            value.DebugMessage is null
                ? null
                : ActorWireValue.String(value.DebugMessage, 4096));
        return GenericActorWireCodecValues.RequirePayloadLimit(
            writer.ToArray(),
            GenericActorWireCodecValues.MaximumGuestPayloadBytes,
            "Generic actor decision");
    }

    public static GenericActorDecision Decode(byte[] bytes, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        GenericActorWireCodecValues.RequirePayloadLimit(
            bytes,
            GenericActorWireCodecValues.MaximumGuestPayloadBytes,
            "Generic actor decision",
            decoding: true);
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? debug = reader.Optional(4);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorDecision(
                GenericActorWireCodecValues.SemanticId(reader.Required(1)),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireActionCodec.DecodeArguments(
                    reader.Required(3),
                    depth + 1),
                debug is null
                    ? null
                    : ActorWireValue.String(debug, 4096)),
            "decision");
    }
}
