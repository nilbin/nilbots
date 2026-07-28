using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Shared tagged codec for generic actions, masks, and outcomes.</summary>
internal static class GenericActorWireActionCodec
{
    public static byte[] EncodeArgument(GenericActorActionArgument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(value.Kind));
        writer.Field(2, EncodeArgumentValue(value));
        return writer.ToArray();
    }

    public static GenericActorActionArgument DecodeArgument(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        GenericActorRulesContract.ActionParameterKind kind =
            ActorWireValue.Enum<
                GenericActorRulesContract.ActionParameterKind>(
                reader.Required(1));
        byte[] value = reader.Required(2);
        return GenericActorWireCodecValues.Decode<
            GenericActorActionArgument>(
            () => kind switch
            {
                GenericActorRulesContract.ActionParameterKind.ShotProgram =>
                    new GenericActorActionArgument.ShotProgramArgument(
                        GenericActorWireCodecValues.DecodeShotProgram(
                            value,
                            depth + 1)),
                GenericActorRulesContract.ActionParameterKind.Direction =>
                    new GenericActorActionArgument.DirectionArgument(
                        ActorWireValue.Enum<Direction>(value)),
                GenericActorRulesContract.ActionParameterKind.UnitTarget =>
                    new GenericActorActionArgument.UnitTargetArgument(
                        DecodeUnitTarget(value, depth + 1)),
                GenericActorRulesContract.ActionParameterKind.FormTarget =>
                    new GenericActorActionArgument.FormTargetArgument(
                        GenericActorWireCodecValues.SemanticId(value)),
                GenericActorRulesContract.ActionParameterKind
                        .ProjectileHeading =>
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        ActorWireValue.Enum<ProjectileHeading>(value)),
                _ => throw new FormatException(
                    "Unknown generic actor argument discriminator."),
            },
            "action argument");
    }

    public static byte[] EncodeArguments(
        IEnumerable<GenericActorActionArgument> values) =>
        GenericActorWireCodecValues.Array(values, EncodeArgument);

    public static ImmutableArray<GenericActorActionArgument> DecodeArguments(
        byte[] bytes,
        int depth) =>
        ActorWireValue.Array(
            bytes,
            item => DecodeArgument(item, depth + 1));

    public static byte[] EncodeLegality(GenericActorActionLegality value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.ActionId));
        writer.Field(2, ActorWireValue.Int32(value.ActionCode));
        writer.Field(3, ActorWireValue.Boolean(value.AllowedByForm));
        writer.Field(4, ActorWireValue.Boolean(value.Available));
        writer.Field(
            5,
            GenericActorWireCodecValues.Array(
                value.Constraints,
                EncodeConstraint));
        return writer.ToArray();
    }

    public static GenericActorActionLegality DecodeLegality(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode<
            GenericActorActionLegality>(
            () => new GenericActorActionLegality(
                GenericActorWireCodecValues.SemanticId(reader.Required(1)),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.Boolean(reader, 3),
                GenericActorWireCodecValues.Boolean(reader, 4),
                GenericActorWireCodecValues.Array(
                    reader,
                    5,
                    item => DecodeConstraint(item, depth + 1))),
            "action legality");
    }

    public static byte[] EncodeResolvedAction(
        GenericActorActionResolution.ResolvedAction value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.ActionId));
        writer.Field(2, ActorWireValue.Int32(value.ActionCode));
        writer.Field(3, EncodeArguments(value.Arguments));
        return writer.ToArray();
    }

    public static GenericActorActionResolution.ResolvedAction
        DecodeResolvedAction(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode<
            GenericActorActionResolution.ResolvedAction>(
            () => new GenericActorActionResolution.ResolvedAction(
                GenericActorWireCodecValues.SemanticId(reader.Required(1)),
                GenericActorWireCodecValues.Int32(reader, 2),
                DecodeArguments(reader.Required(3), depth + 1)),
            "resolved action");
    }

    public static byte[] EncodeResolution(
        GenericActorActionResolution value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Optional(
            1,
            value.SubmittedAction is { } submitted
                ? EncodeResolvedAction(submitted)
                : null);
        writer.Field(2, EncodeResolvedAction(value.AcceptedAction));
        writer.Field(3, EncodeResolvedAction(value.ValidatedAction));
        writer.Field(4, ActorWireValue.Enum(value.Outcome));
        writer.Optional(
            5,
            value.RuntimeFault is { } fault
                ? EncodeRuntimeFault(fault)
                : null);
        return writer.ToArray();
    }

    public static GenericActorActionResolution DecodeResolution(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? submitted = reader.Optional(1);
        byte[]? fault = reader.Optional(5);
        return GenericActorWireCodecValues.Decode<
            GenericActorActionResolution>(
            () => new GenericActorActionResolution(
                submitted is null
                    ? null
                    : DecodeResolvedAction(submitted, depth + 1),
                DecodeResolvedAction(reader.Required(2), depth + 1),
                DecodeResolvedAction(reader.Required(3), depth + 1),
                GenericActorWireCodecValues.Enum<
                    GenericActorActionResolution.ActionOutcome>(reader, 4),
                fault is null
                    ? null
                    : DecodeRuntimeFault(fault, depth + 1)),
            "action resolution");
    }

    public static byte[] EncodeRuntimeFault(
        GenericActorRuntimeFaultContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.ParticipantId));
        writer.Field(
            2,
            GenericActorWireCodecValues.EncodeIdentity(value.ActorId));
        writer.Field(3, ActorWireValue.Enum(value.Stage));
        writer.Field(
            4,
            GenericActorWireCodecValues.SemanticId(value.FaultCode));
        writer.Field(
            5,
            GenericActorWireCodecValues.Int64(value.CumulativeFaultCount));
        writer.Field(
            6,
            ActorWireValue.Boolean(value.DisqualificationTriggered));
        return writer.ToArray();
    }

    public static GenericActorRuntimeFaultContext DecodeRuntimeFault(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorRuntimeFaultContext(
                GenericActorWireCodecValues.Int32(reader, 1),
                GenericActorWireCodecValues.DecodeIdentity(
                    reader.Required(2),
                    depth + 1),
                GenericActorWireCodecValues.Enum<
                    GenericActorRuntimeFaultContext.FaultStage>(reader, 3),
                GenericActorWireCodecValues.SemanticId(reader.Required(4)),
                GenericActorWireCodecValues.Int64(reader.Required(5)),
                GenericActorWireCodecValues.Boolean(reader, 6)),
            "runtime fault");
    }

    private static byte[] EncodeArgumentValue(
        GenericActorActionArgument value) =>
        value switch
        {
            GenericActorActionArgument.ShotProgramArgument shot =>
                GenericActorWireCodecValues.EncodeShotProgram(shot.Value),
            GenericActorActionArgument.DirectionArgument direction =>
                ActorWireValue.Enum(direction.Value),
            GenericActorActionArgument.UnitTargetArgument target =>
                EncodeUnitTarget(target.Value),
            GenericActorActionArgument.FormTargetArgument form =>
                GenericActorWireCodecValues.SemanticId(form.FormId),
            GenericActorActionArgument.ProjectileHeadingArgument heading =>
                ActorWireValue.Enum(heading.Value),
            _ => throw new InvalidOperationException(
                "Unknown generic actor argument variant."),
        };

    private static byte[] EncodeUnitTarget(
        GenericActorActionArgument.UnitTarget value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        return writer.ToArray();
    }

    private static GenericActorActionArgument.UnitTarget DecodeUnitTarget(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorActionArgument.UnitTarget(
                GenericActorWireCodecValues.Int32(reader, 1),
                GenericActorWireCodecValues.Int32(reader, 2)),
            "unit target");
    }

    private static byte[] EncodeConstraint(
        GenericActorActionLegality.ArgumentConstraint value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(value.Kind));
        writer.Field(2, EncodeConstraintPayload(value));
        return writer.ToArray();
    }

    private static GenericActorActionLegality.ArgumentConstraint
        DecodeConstraint(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        GenericActorRulesContract.ActionParameterKind kind =
            GenericActorWireCodecValues.Enum<
                GenericActorRulesContract.ActionParameterKind>(reader, 1);
        var payload = new ActorWireObjectReader(
            reader.Required(2),
            depth + 1);
        return GenericActorWireCodecValues.Decode<
            GenericActorActionLegality.ArgumentConstraint>(
            () => kind switch
            {
                GenericActorRulesContract.ActionParameterKind.ShotProgram =>
                    new GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint(
                            GenericActorWireCodecValues.Boolean(payload, 1)),
                GenericActorRulesContract.ActionParameterKind.Direction =>
                    new GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint(
                            GenericActorWireCodecValues.Array(
                                payload,
                                1,
                                ActorWireValue.Enum<Direction>)),
                GenericActorRulesContract.ActionParameterKind.UnitTarget =>
                    new GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint(
                            GenericActorWireCodecValues.Array(
                                payload,
                                1,
                                item => DecodeUnitTarget(item, depth + 2))),
                GenericActorRulesContract.ActionParameterKind.FormTarget =>
                    new GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint(
                            GenericActorWireCodecValues.Array(
                                payload,
                                1,
                                GenericActorWireCodecValues.SemanticId)),
                GenericActorRulesContract.ActionParameterKind
                        .ProjectileHeading =>
                    new GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint(
                            GenericActorWireCodecValues.Array(
                                payload,
                                1,
                                ActorWireValue.Enum<ProjectileHeading>)),
                _ => throw new FormatException(
                    "Unknown generic actor constraint discriminator."),
            },
            "action constraint");
    }

    private static byte[] EncodeConstraintPayload(
        GenericActorActionLegality.ArgumentConstraint value)
    {
        var writer = new ActorWireObjectWriter();
        switch (value)
        {
            case GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint shot:
                writer.Field(1, ActorWireValue.Boolean(shot.Allowed));
                break;
            case GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint direction:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.Array(
                        direction.AllowedValues,
                        ActorWireValue.Enum));
                break;
            case GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint targets:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.Array(
                        targets.AllowedValues,
                        EncodeUnitTarget));
                break;
            case GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.Array(
                        forms.AllowedFormIds,
                        GenericActorWireCodecValues.SemanticId));
                break;
            case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.Array(
                        headings.AllowedValues,
                        ActorWireValue.Enum));
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown generic actor constraint variant.");
        }
        return writer.ToArray();
    }
}
