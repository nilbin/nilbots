using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Tagged schema-1 codec for one MIND reply, written EXACTLY on the field IDs
/// reserved in <see cref="GenericMindWireFieldIds"/>.
///
/// <para>Arguments go through <see cref="GenericActorWireActionCodec"/>
/// unchanged, so a mind's <c>move north</c> is byte-identical to a per-life
/// bot's. The role tag SHIPS on command field 6; declared intents are RESERVED
/// on decisions field 20 and a non-empty collection is refused outright rather
/// than encoded into bytes no host will honour.</para>
///
/// <para>Nine commands with role tags sit well under 1 KB against the 64 KiB
/// guest frame cap. The decision side was never the constraint and still is
/// not.</para>
/// </summary>
internal static class GenericMindWireDecisionCodec
{
    public static byte[] Encode(MindDecisions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.Intents.IsEmpty)
        {
            throw new InvalidOperationException(
                "Declared intents are reserved: no shipped format has allied "
                + "minds, so a non-empty collection cannot be encoded.");
        }

        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.MindDecisions.SchemaVersion,
            ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(
            GenericMindWireFieldIds.MindDecisions.Tick,
            ActorWireValue.Int32(value.Tick));
        writer.Optional(
            GenericMindWireFieldIds.MindDecisions.DebugMessage,
            value.DebugMessage is null
                ? null
                : ActorWireValue.String(value.DebugMessage, 4096));
        writer.Field(
            GenericMindWireFieldIds.MindDecisions.Commands,
            GenericActorWireCodecValues.Array(value.Commands, EncodeCommand));
        writer.Field(
            GenericMindWireFieldIds.MindDecisions.Intents,
            GenericActorWireCodecValues.Array(
                ImmutableArray<MindDeclaredIntent>.Empty,
                EncodeIntent));
        return GenericActorWireCodecValues.RequirePayloadLimit(
            writer.ToArray(),
            GenericActorWireCodecValues.MaximumGuestPayloadBytes,
            "Mind decisions");
    }

    public static MindDecisions Decode(byte[] bytes, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        GenericActorWireCodecValues.RequirePayloadLimit(
            bytes,
            GenericActorWireCodecValues.MaximumGuestPayloadBytes,
            "Mind decisions",
            decoding: true);
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? intents = reader.Optional(
            GenericMindWireFieldIds.MindDecisions.Intents);
        byte[]? debug = reader.Optional(
            GenericMindWireFieldIds.MindDecisions.DebugMessage);
        return GenericActorWireCodecValues.Decode(
            () => new MindDecisions(
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindDecisions.SchemaVersion),
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindDecisions.Tick),
                GenericActorWireCodecValues.Array(
                    reader,
                    GenericMindWireFieldIds.MindDecisions.Commands,
                    item => DecodeCommand(item, depth + 1)),
                DecodeIntents(intents, depth + 1),
                debug is null
                    ? null
                    : ActorWireValue.String(debug, 4096)),
            "mind decisions");
    }

    private static byte[] EncodeCommand(MindCommand value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.MindCommand.UnitId,
            ActorWireValue.Int32(value.UnitId));
        writer.Field(
            GenericMindWireFieldIds.MindCommand.LifeId,
            ActorWireValue.Int32(value.LifeId));
        writer.Field(
            GenericMindWireFieldIds.MindCommand.ActionId,
            GenericActorWireCodecValues.SemanticId(value.ActionId));
        writer.Field(
            GenericMindWireFieldIds.MindCommand.ActionCode,
            ActorWireValue.Int32(value.ActionCode));
        writer.Field(
            GenericMindWireFieldIds.MindCommand.Arguments,
            GenericActorWireActionCodec.EncodeArguments(value.Arguments));
        // Absent means "leave this body's published tag unchanged"; the empty
        // string means "clear it". Two meanings, one field, no third encoding.
        writer.Optional(
            GenericMindWireFieldIds.MindCommand.RoleTag,
            value.RoleTag is null
                ? null
                : ActorWireValue.String(
                    value.RoleTag,
                    GenericMindContractVersions.MaxRoleTagUtf8Bytes));
        writer.Optional(
            GenericMindWireFieldIds.MindCommand.DebugMessage,
            value.DebugMessage is null
                ? null
                : ActorWireValue.String(value.DebugMessage, 4096));
        return writer.ToArray();
    }

    private static MindCommand DecodeCommand(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? roleTag = reader.Optional(
            GenericMindWireFieldIds.MindCommand.RoleTag);
        byte[]? debug = reader.Optional(
            GenericMindWireFieldIds.MindCommand.DebugMessage);
        return GenericActorWireCodecValues.Decode(
            () => new MindCommand(
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindCommand.UnitId),
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindCommand.LifeId),
                GenericActorWireCodecValues.SemanticId(
                    reader.Required(
                        GenericMindWireFieldIds.MindCommand.ActionId)),
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindCommand.ActionCode),
                GenericActorWireActionCodec.DecodeArguments(
                    reader.Required(
                        GenericMindWireFieldIds.MindCommand.Arguments),
                    depth + 1),
                roleTag is null
                    ? null
                    : ActorWireValue.String(
                        roleTag,
                        GenericMindContractVersions.MaxRoleTagUtf8Bytes),
                debug is null
                    ? null
                    : ActorWireValue.String(debug, 4096)),
            "mind command");
    }

    private static byte[] EncodeIntent(MindDeclaredIntent value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.DeclaredIntent.TagId,
            GenericActorWireCodecValues.SemanticId(value.TagId));
        writer.Field(
            GenericMindWireFieldIds.DeclaredIntent.Value,
            GenericActorWireCodecValues.Int64(value.Value));
        return writer.ToArray();
    }

    /// <summary>
    /// Tolerates an absent field and an EMPTY collection; refuses a populated
    /// one, because a declaration no format admits cannot be honoured and
    /// silently dropping it would be worse than refusing it.
    /// </summary>
    private static ImmutableArray<MindDeclaredIntent> DecodeIntents(
        byte[]? bytes,
        int depth)
    {
        if (bytes is null)
            return [];
        ImmutableArray<MindDeclaredIntent> decoded = ActorWireValue.Array(
            bytes,
            item => GenericActorWireCodecValues.Decode(
                () =>
                {
                    var reader = new ActorWireObjectReader(item, depth);
                    return new MindDeclaredIntent(
                        GenericActorWireCodecValues.SemanticId(
                            reader.Required(
                                GenericMindWireFieldIds.DeclaredIntent.TagId)),
                        GenericActorWireCodecValues.Int64(
                            reader.Required(
                                GenericMindWireFieldIds.DeclaredIntent.Value)));
                },
                "declared intent"));
        if (!decoded.IsEmpty)
        {
            throw new FormatException(
                "Declared intents are reserved and must be empty until a "
                + "format with allied minds is admitted.");
        }
        return decoded;
    }
}
