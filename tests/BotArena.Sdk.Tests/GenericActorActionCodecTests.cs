using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

public sealed class GenericActorActionCodecTests
{
    [Fact]
    public void DecisionRoundTripPreservesEveryTypedArgument()
    {
        GenericActorDecision source =
            GenericActorDynamicTestFixture.FullDecision();

        byte[] encoded = GenericActorWireDecisionCodec.Encode(source);
        GenericActorDecision decoded =
            GenericActorWireDecisionCodec.Decode(encoded);

        Assert.Equal(source.ActionId, decoded.ActionId);
        Assert.Equal(source.ActionCode, decoded.ActionCode);
        Assert.Equal(source.DebugMessage, decoded.DebugMessage);
        Assert.Equal(
            Enum.GetValues<
                GenericActorRulesContract.ActionParameterKind>(),
            decoded.Arguments.Select(argument => argument.Kind));
        Assert.Equal(
            new ShotProgram(1, -1, 2, 3, 2),
            Assert.IsType<
                GenericActorActionArgument.ShotProgramArgument>(
                    decoded.Arguments[0]).Value);
        Assert.Equal(
            Direction.West,
            Assert.IsType<
                GenericActorActionArgument.DirectionArgument>(
                    decoded.Arguments[1]).Value);
        Assert.Equal(
            new GenericActorActionArgument.UnitTarget(0, 2),
            Assert.IsType<
                GenericActorActionArgument.UnitTargetArgument>(
                    decoded.Arguments[2]).Value);
        Assert.Equal(
            "turret",
            Assert.IsType<
                GenericActorActionArgument.FormTargetArgument>(
                    decoded.Arguments[3]).FormId);
        Assert.Equal(
            ProjectileHeading.NorthEast,
            Assert.IsType<
                GenericActorActionArgument.ProjectileHeadingArgument>(
                    decoded.Arguments[4]).Value);
        Assert.Equal(
            "plate",
            Assert.IsType<
                GenericActorActionArgument.UpgradeTrackArgument>(
                    decoded.Arguments[5]).TrackId);
    }

    [Fact]
    public void LegalityRoundTripPreservesEveryConstraintAndEmptyMasks()
    {
        GenericActorActionLegality source =
            GenericActorDynamicTestFixture.FullLegality();

        GenericActorActionLegality decoded =
            GenericActorWireActionCodec.DecodeLegality(
                GenericActorWireActionCodec.EncodeLegality(source),
                depth: 0);

        Assert.True(decoded.AllowedByForm);
        Assert.True(decoded.Available);
        Assert.Equal(
            Enum.GetValues<
                GenericActorRulesContract.ActionParameterKind>(),
            decoded.Constraints.Select(constraint => constraint.Kind));
        Assert.True(
            Assert.IsType<
                GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint>(
                        decoded.Constraints[0]).Allowed);
        Assert.Equal(
            [Direction.North, Direction.South],
            Assert.IsType<
                GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(
                        decoded.Constraints[1]).AllowedValues.ToArray());
        Assert.Equal(
            ["mobile", "turret"],
            Assert.IsType<
                GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>(
                        decoded.Constraints[3]).AllowedFormIds.ToArray());
        Assert.Equal(
            ["edge", "plate"],
            Assert.IsType<
                GenericActorActionLegality.ArgumentConstraint
                    .UpgradeTrackConstraint>(
                        decoded.Constraints[5]).AllowedTrackIds.ToArray());

        var empty = new GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint([]);
        GenericActorActionLegality emptyDecoded =
            GenericActorWireActionCodec.DecodeLegality(
                GenericActorWireActionCodec.EncodeLegality(
                    new GenericActorActionLegality(
                        "shoot",
                        4,
                        true,
                        false,
                        [empty])),
                0);
        Assert.Empty(
            Assert.IsType<
                GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>(
                        Assert.Single(emptyDecoded.Constraints))
                .AllowedValues);
    }

    [Theory]
    [InlineData(GenericActorActionResolution.ActionOutcome.Success)]
    [InlineData(GenericActorActionResolution.ActionOutcome.Blocked)]
    [InlineData(GenericActorActionResolution.ActionOutcome.Rejected)]
    [InlineData(GenericActorActionResolution.ActionOutcome.Faulted)]
    public void ResolutionRoundTripKeepsRequestedActionAndFaultSemantics(
        GenericActorActionResolution.ActionOutcome outcome)
    {
        GenericActorActionResolution source =
            GenericActorDynamicTestFixture.Resolution(outcome);

        GenericActorActionResolution decoded =
            GenericActorWireActionCodec.DecodeResolution(
                GenericActorWireActionCodec.EncodeResolution(source),
                0);

        Assert.Equal(outcome, decoded.Outcome);
        Assert.Equal("move", decoded.SubmittedAction?.ActionId);
        if (outcome == GenericActorActionResolution.ActionOutcome.Faulted)
        {
            Assert.Equal("wait", decoded.ValidatedAction.ActionId);
            Assert.NotNull(decoded.RuntimeFault);
            Assert.Equal(
                long.MaxValue,
                decoded.RuntimeFault.CumulativeFaultCount);
        }
        else
        {
            Assert.Equal("move", decoded.ValidatedAction.ActionId);
            Assert.Null(decoded.RuntimeFault);
        }
    }

    [Fact]
    public void DynamicInvariantsRejectAmbiguousOrImpossibleActions()
    {
        Assert.Throws<ArgumentException>(
            () => new GenericActorDecision(
                "move",
                1,
                [
                    new GenericActorActionArgument.DirectionArgument(
                        Direction.North),
                    new GenericActorActionArgument.DirectionArgument(
                        Direction.East),
                ]));
        Assert.Throws<ArgumentException>(
            () => new GenericActorActionLegality(
                "move",
                1,
                allowedByForm: false,
                available: true,
                []));
        Assert.Throws<ArgumentException>(
            () => new GenericActorActionResolution(
                submittedAction: null,
                GenericActorDynamicTestFixture.WaitAction(),
                GenericActorDynamicTestFixture.WaitAction(),
                GenericActorActionResolution.ActionOutcome.Success,
                GenericActorDynamicTestFixture.Fault()));
        Assert.Throws<ArgumentException>(
            () => new GenericActorActionResolution(
                submittedAction: null,
                GenericActorDynamicTestFixture.WaitAction(),
                GenericActorDynamicTestFixture.WaitAction(),
                GenericActorActionResolution.ActionOutcome.Blocked,
                runtimeFault: null));
        Assert.Throws<ArgumentException>(
            () => new GenericActorActionResolution(
                GenericActorDynamicTestFixture.MoveAction(),
                GenericActorDynamicTestFixture.MoveAction(),
                GenericActorDynamicTestFixture.WaitAction(),
                GenericActorActionResolution.ActionOutcome.Rejected,
                runtimeFault: null));
        Assert.Throws<ArgumentException>(
            () => new GenericActorActionResolution(
                GenericActorDynamicTestFixture.MoveAction(),
                GenericActorDynamicTestFixture.WaitAction(),
                GenericActorDynamicTestFixture.MoveAction(),
                GenericActorActionResolution.ActionOutcome.Faulted,
                GenericActorDynamicTestFixture.Fault()));
    }

    [Fact]
    public void MinimalDecisionHasPinnedCanonicalBytes()
    {
        byte[] bytes = GenericActorWireDecisionCodec.Encode(
            GenericActorDecision.WithoutArguments("wait", 0));

        Assert.Equal(
            "010004000000776169740200040000000000000003000400000000000000",
            Convert.ToHexString(bytes));
        Assert.Equal(
            GenericActorDecision.WithoutArguments("wait", 0),
            GenericActorWireDecisionCodec.Decode(bytes));
    }

    [Fact]
    public void MalformedAndOversizedDecisionPayloadsFailClosed()
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, GenericActorWireCodecValues.SemanticId("wait"));
        writer.Field(2, ActorWireValue.Int32(0));

        Assert.Throws<FormatException>(
            () => GenericActorWireDecisionCodec.Decode(writer.ToArray()));
        Assert.Throws<FormatException>(
            () => GenericActorWireCodecValues.Int64([0, 1, 2]));
        Assert.Throws<FormatException>(
            () => GenericActorWireDecisionCodec.Decode(
                new byte[
                    GenericActorWireCodecValues.MaximumGuestPayloadBytes
                    + 1]));
    }

    [Fact]
    public void TaggedDecisionCompatibilityRejectsDuplicatesDepthAndCounts()
    {
        byte[] encoded = GenericActorWireDecisionCodec.Encode(
            GenericActorDecision.WithoutArguments("wait", 0));
        var unknown = new ActorWireObjectWriter();
        unknown.Field(99, [0xDE, 0xAD]);
        GenericActorDecision extended =
            GenericActorWireDecisionCodec.Decode(
                [.. encoded, .. unknown.ToArray()]);
        Assert.Equal("wait", extended.ActionId);

        var duplicate = new ActorWireObjectWriter();
        duplicate.Field(
            1,
            GenericActorWireCodecValues.SemanticId("wait"));
        Assert.Throws<FormatException>(
            () => GenericActorWireDecisionCodec.Decode(
                [.. encoded, .. duplicate.ToArray()]));
        Assert.Throws<FormatException>(
            () => GenericActorWireDecisionCodec.Decode(
                encoded,
                ActorWireProtocol.MaxDepth + 1));

        var excessiveCount = new ActorWireObjectWriter();
        excessiveCount.Field(
            1,
            GenericActorWireCodecValues.SemanticId("wait"));
        excessiveCount.Field(2, ActorWireValue.Int32(0));
        excessiveCount.Field(
            3,
            ActorWireValue.Int32(
                ActorWireProtocol.MaxCollectionCount + 1));
        Assert.Throws<FormatException>(
            () => GenericActorWireDecisionCodec.Decode(
                excessiveCount.ToArray()));
    }

    [Fact]
    public void DecisionTextBoundsUseStrictUtf8Bytes()
    {
        string maximum = new('é', 2048);
        GenericActorDecision decoded =
            GenericActorWireDecisionCodec.Decode(
                GenericActorWireDecisionCodec.Encode(
                    GenericActorDecision.WithoutArguments(
                        "wait",
                        0,
                        maximum)));

        Assert.Equal(maximum, decoded.DebugMessage);
        Assert.Throws<ArgumentException>(
            () => GenericActorDecision.WithoutArguments(
                "wait",
                0,
                maximum + "é"));
        Assert.Throws<ArgumentException>(
            () => GenericActorDecision.WithoutArguments(
                new string('a', 65),
                0));
        Assert.Throws<ArgumentException>(
            () => GenericActorDecision.WithoutArguments(
                "wait",
                0,
                "\uD800"));
    }
}
