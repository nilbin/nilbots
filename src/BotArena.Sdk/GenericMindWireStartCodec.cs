using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Tagged binary envelope for <c>MindStart</c>. The embedded static contract is
/// always the original Engine-validated canonical UTF-8 JSON, never a second
/// SDK serialization of the typed view — which is what keeps the mind profile's
/// contract fingerprint identical to the per-life profile's for the same match.
/// </summary>
internal static class GenericMindWireStartCodec
{
    public static byte[] Encode(MindStart value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value);

        var writer = new ActorWireObjectWriter();
        writer.Field(
            GenericMindWireFieldIds.MindStart.SchemaVersion,
            ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(
            GenericMindWireFieldIds.MindStart.RuntimeContractVersion,
            ActorWireValue.Int32(value.RuntimeContractVersion));
        writer.Field(
            GenericMindWireFieldIds.MindStart.ParticipantId,
            ActorWireValue.Int32(value.ParticipantId));
        writer.Field(
            GenericMindWireFieldIds.MindStart.TeamId,
            ActorWireValue.Int32(value.TeamId));
        writer.Field(
            GenericMindWireFieldIds.MindStart.MindRandomSeed,
            ActorWireValue.UInt64(value.MindRandomSeed));
        writer.Field(
            GenericMindWireFieldIds.MindStart.TeamRandomSeed,
            ActorWireValue.UInt64(value.TeamRandomSeed));
        writer.Field(
            GenericMindWireFieldIds.MindStart.Contract,
            ActorWireValue.String(
                value.Contract.CanonicalJson,
                GenericActorContractVersions.MaxCanonicalContractBytes));
        writer.Field(
            GenericMindWireFieldIds.MindStart.AlliedParticipantIds,
            GenericActorWireCodecValues.Array(
                value.AlliedParticipantIds,
                ActorWireValue.Int32));
        writer.Optional(
            GenericMindWireFieldIds.MindStart.EvaluationData,
            value.EvaluationData.IsDefaultOrEmpty
                ? null
                : value.EvaluationData.ToArray());
        return writer.ToArray();
    }

    public static MindStart Decode(byte[] bytes, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            var reader = new ActorWireObjectReader(bytes, depth);
            var result = new MindStart
            {
                SchemaVersion = ActorWireValue.Int32(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart.SchemaVersion)),
                RuntimeContractVersion = ActorWireValue.Int32(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart
                            .RuntimeContractVersion)),
                ParticipantId = ActorWireValue.Int32(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart.ParticipantId)),
                TeamId = ActorWireValue.Int32(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart.TeamId)),
                MindRandomSeed = ActorWireValue.UInt64(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart.MindRandomSeed)),
                TeamRandomSeed = ActorWireValue.UInt64(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart.TeamRandomSeed)),
                Contract = ActorCanonicalContractReader.ParseUtf8(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart.Contract)),
                AlliedParticipantIds = ActorWireValue.Array(
                    reader.Required(
                        GenericMindWireFieldIds.MindStart
                            .AlliedParticipantIds),
                    ActorWireValue.Int32),
                EvaluationData = reader.Optional(
                        GenericMindWireFieldIds.MindStart.EvaluationData)
                    ?.ToImmutableArray() ?? [],
            };
            Validate(result);
            return result;
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                "The MindStart envelope is invalid.",
                exception);
        }
    }

    private static void Validate(MindStart value)
    {
        if (value.SchemaVersion < 0
            || value.RuntimeContractVersion < 0
            || value.ParticipantId < 0
            || value.TeamId < 0)
        {
            throw new FormatException(
                "MindStart versions and identities cannot be negative.");
        }
        if (value.AlliedParticipantIds.IsDefault)
        {
            throw new FormatException(
                "MindStart must carry an allied-participant collection, "
                + "empty in head-to-head.");
        }
        if (value.EvaluationData.IsDefault)
        {
            throw new FormatException(
                "MindStart evaluation data must be empty rather than default.");
        }
        if (value.EvaluationData.Length > 64 * 1024)
        {
            throw new FormatException(
                "MindStart evaluation data exceeds 64 KiB.");
        }
        if (value.AlliedParticipantIds.Contains(value.ParticipantId))
        {
            throw new FormatException(
                "A mind is not its own ally.");
        }
        ImmutableArray<int> allies = value.AlliedParticipantIds;
        for (int index = 1; index < allies.Length; index++)
        {
            if (allies[index] <= allies[index - 1])
            {
                throw new FormatException(
                    "Allied participant IDs must be strictly ascending.");
            }
        }
    }
}
