using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Tagged schema-1 codec for one MIND observation, written EXACTLY on the
/// field IDs reserved in <see cref="GenericMindWireFieldIds"/>.
///
/// <para>Every nested record is the existing SDK type encoded by the EXISTING
/// codec — allies, enemies, tiles, projectiles, events, sounds, the scoreboard,
/// the mode union, unit-slot states and legality masks all go through
/// <see cref="GenericActorWireObservationCodec"/> unchanged, and a body's
/// fields 1..13 go through the same <c>EncodeBody</c> the per-life self and
/// ally use. That reuse is not tidiness: it is what makes a mind observation and
/// a per-life observation carry the same facts in the same bytes, and therefore
/// what makes the wrap adapter a projection rather than a translation.</para>
/// </summary>
internal static class GenericMindWireObservationCodec
{
    public static byte[] Encode(MindContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.MindObservation.SchemaVersion,
            ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Tick,
            ActorWireValue.Int32(value.Tick));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.MatchContractFingerprint,
            GenericActorWireCodecValues.Fingerprint(
                value.MatchContractFingerprint));

        // ---- delivered ONCE ------------------------------------------------
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Allies,
            Array(
                value.Allies,
                GenericActorWireObservationCodec.EncodeAlly));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Enemies,
            Array(
                value.Enemies,
                GenericActorWireObservationCodec.EncodeEnemy));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.VisibleTiles,
            Array(
                value.VisibleTiles,
                GenericActorWireObservationCodec.EncodeTile));
        writer.Optional(
            GenericMindWireFieldIds.MindObservation.VisibleProjectiles,
            value.VisibleProjectiles is { } projectiles
                ? Array(
                    projectiles,
                    GenericActorWireObservationCodec.EncodeProjectile)
                : null);
        writer.Field(
            GenericMindWireFieldIds.MindObservation.VisibleEvents,
            Array(
                value.VisibleEvents,
                GenericActorWireEventCodec.EncodeEvent));
        writer.Optional(
            GenericMindWireFieldIds.MindObservation.HeardSounds,
            value.HeardSounds is { } sounds
                ? Array(sounds, GenericActorWireEventCodec.EncodeSound)
                : null);
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Scoreboard,
            GenericActorWireObservationCodec.EncodeScoreboard(
                value.Scoreboard));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Mode,
            GenericActorWireObservationCodec.EncodeMode(value.Mode));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Participants,
            Array(
                value.Participants,
                GenericActorWireObservationCodec.EncodeParticipant));

        // ---- per body / per slot -------------------------------------------
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Bodies,
            Array(value.Bodies, EncodeBody));
        writer.Field(
            GenericMindWireFieldIds.MindObservation.Slots,
            Array(value.Slots, EncodeSlot));

        // ---- reserved -------------------------------------------------------
        // The empty collection is written unconditionally: the field ID is
        // spent, the shape is negotiated, and the cost is one tagged field with
        // a zero count.
        if (!value.AlliedIntents.IsEmpty)
        {
            throw new InvalidOperationException(
                "Allied intents are reserved: no shipped format has allied "
                + "minds, so a non-empty collection cannot be encoded.");
        }
        writer.Field(
            GenericMindWireFieldIds.MindObservation.AlliedIntents,
            Array(
                ImmutableArray<MindContext.AlliedIntent>.Empty,
                EncodeAlliedIntent));

        return GenericActorWireCodecValues.RequirePayloadLimit(
            writer.ToArray(),
            GenericActorWireCodecValues.MaximumHostPayloadBytes,
            "Mind observation");
    }

    /// <summary>
    /// Decodes one mind observation.
    /// </summary>
    /// <param name="bytes">The tagged payload.</param>
    /// <param name="waitAction">
    /// The contract's wait action, resolved once at match start so
    /// <see cref="MindBody.Hold(string?)"/> never searches the catalog mid-tick.
    /// </param>
    /// <param name="depth">Current nesting depth.</param>
    public static MindContext Decode(
        byte[] bytes,
        MindWaitAction waitAction,
        int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        GenericActorWireCodecValues.RequirePayloadLimit(
            bytes,
            GenericActorWireCodecValues.MaximumHostPayloadBytes,
            "Mind observation",
            decoding: true);
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? projectiles = reader.Optional(
            GenericMindWireFieldIds.MindObservation.VisibleProjectiles);
        byte[]? sounds = reader.Optional(
            GenericMindWireFieldIds.MindObservation.HeardSounds);
        byte[]? alliedIntents = reader.Optional(
            GenericMindWireFieldIds.MindObservation.AlliedIntents);
        return GenericActorWireCodecValues.Decode(
            () => new MindContext(
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindObservation.SchemaVersion),
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindObservation.Tick),
                GenericActorWireCodecValues.Fingerprint(
                    reader.Required(
                        GenericMindWireFieldIds.MindObservation
                            .MatchContractFingerprint)),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.Bodies,
                    item => DecodeBody(item, waitAction, depth + 1)),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.Slots,
                    item => DecodeSlot(item, depth + 1)),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.Allies,
                    item => GenericActorWireObservationCodec.DecodeAlly(
                        item,
                        depth + 1)),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.Enemies,
                    item => GenericActorWireObservationCodec.DecodeEnemy(
                        item,
                        depth + 1)),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.VisibleTiles,
                    item => GenericActorWireObservationCodec.DecodeTile(
                        item,
                        depth + 1)),
                projectiles is null
                    ? null
                    : ActorWireValue.Array(
                        projectiles,
                        item =>
                            GenericActorWireObservationCodec.DecodeProjectile(
                                item,
                                depth + 1)),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.VisibleEvents,
                    item => GenericActorWireEventCodec.DecodeEvent(
                        item,
                        depth + 1)),
                sounds is null
                    ? null
                    : ActorWireValue.Array(
                        sounds,
                        item => GenericActorWireEventCodec.DecodeSound(
                            item,
                            depth + 1)),
                GenericActorWireObservationCodec.DecodeScoreboard(
                    reader.Required(
                        GenericMindWireFieldIds.MindObservation.Scoreboard),
                    depth + 1),
                GenericActorWireObservationCodec.DecodeMode(
                    reader.Required(
                        GenericMindWireFieldIds.MindObservation.Mode),
                    depth + 1),
                Array(
                    reader,
                    GenericMindWireFieldIds.MindObservation.Participants,
                    item => GenericActorWireObservationCodec.DecodeParticipant(
                        item,
                        depth + 1)),
                DecodeAlliedIntents(alliedIntents, depth + 1)),
            "mind observation");
    }

    private static byte[] EncodeBody(MindBody value)
    {
        var writer = new ActorWireObjectWriter();
        // Fields 1..13: the shared body encoding, verbatim.
        GenericActorWireObservationCodec.EncodeBody(
            writer,
            value.ActorId,
            value.Generation,
            value.FormId,
            value.Position,
            value.Facing,
            value.Health,
            value.Cooldown,
            value.Energy,
            value.PreviousActionResolution,
            value.PendingSameLifeTransition,
            value.ClassId,
            value.RouteCooldowns,
            value.CarriedScrap);

        // Fields 14..19: the facts a mind is entitled to and a per-life bot
        // was not. Absent previous position means "this life's first tick".
        writer.Optional(
            GenericMindWireFieldIds.MindBodyState.PreviousPosition,
            value.PreviousPosition is { } previous
                ? GenericActorWireCodecValues.EncodePosition(previous)
                : null);
        writer.Field(
            GenericMindWireFieldIds.MindBodyState.MovedLastTick,
            ActorWireValue.Boolean(value.MovedLastTick));
        writer.Field(
            GenericMindWireFieldIds.MindBodyState.LifeStartedTick,
            ActorWireValue.Int32(value.LifeStartedTick));
        writer.Field(
            GenericMindWireFieldIds.MindBodyState.Origin,
            GenericActorWireContractCodec.EncodeOrigin(value.Origin));
        writer.Optional(
            GenericMindWireFieldIds.MindBodyState.RoleTag,
            value.RoleTag is null
                ? null
                : ActorWireValue.String(
                    value.RoleTag,
                    GenericMindContractVersions.MaxRoleTagUtf8Bytes));
        writer.Field(
            GenericMindWireFieldIds.MindBodyState.ActionLegalities,
            Array(
                value.ActionLegalities,
                GenericActorWireActionCodec.EncodeLegality));
        // Field 20, P3's addition, written last because IDs ascend: the body's
        // own per-life stream seed.
        writer.Field(
            GenericMindWireFieldIds.MindBodyState.BodyRandomSeed,
            ActorWireValue.UInt64(value.BodyRandomSeed));
        return writer.ToArray();
    }

    private static MindBody DecodeBody(
        byte[] bytes,
        MindWaitAction waitAction,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? previousPosition = reader.Optional(
            GenericMindWireFieldIds.MindBodyState.PreviousPosition);
        byte[]? roleTag = reader.Optional(
            GenericMindWireFieldIds.MindBodyState.RoleTag);
        return GenericActorWireCodecValues.Decode(
            () =>
            {
                GenericActorWireObservationCodec.SharedBodyState shared =
                    GenericActorWireObservationCodec.DecodeSharedBody(
                        reader,
                        depth);
                return new MindBody(
                    shared.ActorId,
                    shared.Generation,
                    shared.FormId,
                    shared.Position,
                    shared.Facing,
                    shared.Health,
                    shared.Cooldown,
                    shared.Energy,
                    shared.PreviousActionResolution,
                    shared.PendingSameLifeTransition,
                    shared.ClassId,
                    shared.RouteCooldowns,
                    shared.CarriedScrap,
                    previousPosition is null
                        ? null
                        : GenericActorWireCodecValues.DecodePosition(
                            previousPosition,
                            depth + 1),
                    GenericActorWireCodecValues.Boolean(
                        reader,
                        GenericMindWireFieldIds.MindBodyState.MovedLastTick),
                    GenericActorWireCodecValues.Int32(
                        reader,
                        GenericMindWireFieldIds.MindBodyState.LifeStartedTick),
                    GenericActorWireContractCodec.DecodeOrigin(
                        reader.Required(
                            GenericMindWireFieldIds.MindBodyState.Origin),
                        depth + 1),
                    roleTag is null
                        ? null
                        : ActorWireValue.String(
                            roleTag,
                            GenericMindContractVersions.MaxRoleTagUtf8Bytes),
                    ActorWireValue.UInt64(
                        reader.Required(
                            GenericMindWireFieldIds.MindBodyState
                                .BodyRandomSeed)),
                    Array(
                        reader,
                        GenericMindWireFieldIds.MindBodyState.ActionLegalities,
                        item => GenericActorWireActionCodec.DecodeLegality(
                            item,
                            depth + 1)),
                    waitAction);
            },
            "mind body observation");
    }

    private static byte[] EncodeSlot(MindSlot value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.MindSlotState.UnitId,
            ActorWireValue.Int32(value.UnitId));
        writer.Field(
            GenericMindWireFieldIds.MindSlotState.State,
            GenericActorWireObservationCodec.EncodeUnitSlotState(value.State));
        writer.Optional(
            GenericMindWireFieldIds.MindSlotState.ClassId,
            value.ClassId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(value.ClassId));
        // Absent means "this slot's chassis is fixed", which is the only kind
        // v1 admits, so a fixed slot spends no bytes on the reservation.
        writer.Optional(
            GenericMindWireFieldIds.MindSlotState.CandidateClassIds,
            value.CandidateClassIds.IsEmpty
                ? null
                : Array(
                    value.CandidateClassIds,
                    GenericActorWireCodecValues.SemanticId));
        writer.Optional(
            GenericMindWireFieldIds.MindSlotState.SelectedClassId,
            value.SelectedClassId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(
                    value.SelectedClassId));
        return writer.ToArray();
    }

    private static MindSlot DecodeSlot(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? classId = reader.Optional(
            GenericMindWireFieldIds.MindSlotState.ClassId);
        byte[]? candidates = reader.Optional(
            GenericMindWireFieldIds.MindSlotState.CandidateClassIds);
        byte[]? selected = reader.Optional(
            GenericMindWireFieldIds.MindSlotState.SelectedClassId);
        return GenericActorWireCodecValues.Decode(
            () => new MindSlot(
                GenericActorWireCodecValues.Int32(
                    reader,
                    GenericMindWireFieldIds.MindSlotState.UnitId),
                GenericActorWireObservationCodec.DecodeUnitSlotState(
                    reader.Required(
                        GenericMindWireFieldIds.MindSlotState.State),
                    depth + 1),
                classId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(classId),
                candidates is null
                    ? []
                    : ActorWireValue.Array(
                        candidates,
                        GenericActorWireCodecValues.SemanticId),
                selected is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(selected)),
            "mind slot observation");
    }

    private static byte[] EncodeAlliedIntent(MindContext.AlliedIntent value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.AlliedIntent.ParticipantId,
            ActorWireValue.Int32(value.ParticipantId));
        writer.Field(
            GenericMindWireFieldIds.AlliedIntent.TagId,
            GenericActorWireCodecValues.SemanticId(value.TagId));
        writer.Field(
            GenericMindWireFieldIds.AlliedIntent.Value,
            GenericActorWireCodecValues.Int64(value.Value));
        return writer.ToArray();
    }

    /// <summary>
    /// Tolerates an absent field and an EMPTY collection; refuses a populated
    /// one. A host that delivered allied intents would be running a format this
    /// guest cannot reason about, and failing closed is the rule.
    /// </summary>
    private static ImmutableArray<MindContext.AlliedIntent>
        DecodeAlliedIntents(byte[]? bytes, int depth)
    {
        if (bytes is null)
            return [];
        ImmutableArray<MindContext.AlliedIntent> decoded =
            ActorWireValue.Array(
                bytes,
                item => GenericActorWireCodecValues.Decode(
                    () =>
                    {
                        var reader = new ActorWireObjectReader(item, depth);
                        return new MindContext.AlliedIntent(
                            GenericActorWireCodecValues.Int32(
                                reader,
                                GenericMindWireFieldIds.AlliedIntent
                                    .ParticipantId),
                            GenericActorWireCodecValues.SemanticId(
                                reader.Required(
                                    GenericMindWireFieldIds.AlliedIntent.TagId)),
                            GenericActorWireCodecValues.Int64(
                                reader.Required(
                                    GenericMindWireFieldIds.AlliedIntent.Value)));
                    },
                    "allied intent"));
        if (!decoded.IsEmpty)
        {
            throw new FormatException(
                "Allied intents are reserved and must be empty; this host "
                + "delivered a declaration this contract generation cannot "
                + "honour.");
        }
        return decoded;
    }

    private static byte[] Array<T>(
        IEnumerable<T> values,
        Func<T, byte[]> encode) =>
        GenericActorWireCodecValues.Array(values, encode);

    private static ImmutableArray<T> Array<T>(
        ActorWireObjectReader reader,
        ushort fieldId,
        Func<byte[], T> decode) =>
        GenericActorWireCodecValues.Array(reader, fieldId, decode);
}
