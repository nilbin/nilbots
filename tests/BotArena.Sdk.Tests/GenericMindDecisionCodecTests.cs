using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

public sealed class GenericMindDecisionCodecTests
{
    [Fact]
    public void ADecisionMapRoundTripsEveryCommandField()
    {
        MindDecisions original = GenericMindDynamicTestFixture.Decisions();

        byte[] encoded = GenericMindWireDecisionCodec.Encode(original);
        MindDecisions decoded = GenericMindWireDecisionCodec.Decode(encoded);

        Assert.Equal(
            encoded,
            GenericMindWireDecisionCodec.Encode(decoded));
        Assert.Equal(original.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(original.Tick, decoded.Tick);
        Assert.Equal(original.DebugMessage, decoded.DebugMessage);
        Assert.Empty(decoded.Intents);
        Assert.Equal(
            original.Commands.Select(command => (
                command.UnitId,
                command.LifeId,
                command.ActionId,
                command.ActionCode,
                command.RoleTag,
                command.DebugMessage,
                command.Arguments.Length)),
            decoded.Commands.Select(command => (
                command.UnitId,
                command.LifeId,
                command.ActionId,
                command.ActionCode,
                command.RoleTag,
                command.DebugMessage,
                command.Arguments.Length)));
        Assert.Equal(
            original.Commands[0].Arguments[0],
            decoded.Commands[0].Arguments[0]);
    }

    [Fact]
    public void TheDecisionFrameUsesExactlyTheReservedFieldIds()
    {
        byte[] payload = GenericMindWireDecisionCodec.Encode(
            GenericMindDynamicTestFixture.Decisions());

        ImmutableDictionary<ushort, byte[]> fields =
            GenericMindDynamicTestFixture.Fields(payload);

        Assert.Equal(
            new ushort[]
            {
                GenericMindWireFieldIds.MindDecisions.SchemaVersion,
                GenericMindWireFieldIds.MindDecisions.Tick,
                GenericMindWireFieldIds.MindDecisions.DebugMessage,
                GenericMindWireFieldIds.MindDecisions.Commands,
                GenericMindWireFieldIds.MindDecisions.Intents,
            }.Order(),
            fields.Keys.Order());
        Assert.Equal(new ushort[] { 1, 2, 3, 10, 20 }, fields.Keys.Order());
    }

    [Fact]
    public void ACommandUsesExactlyTheReservedFieldIdsAndShipsItsRoleTag()
    {
        byte[] payload = GenericMindWireDecisionCodec.Encode(
            GenericMindDynamicTestFixture.Decisions());
        byte[] commands = GenericMindDynamicTestFixture.Fields(payload)[
            GenericMindWireFieldIds.MindDecisions.Commands];
        ImmutableArray<byte[]> items =
            GenericMindDynamicTestFixture.Items(commands);

        ImmutableDictionary<ushort, byte[]> tagged =
            GenericMindDynamicTestFixture.Fields(items[0]);
        ImmutableDictionary<ushort, byte[]> untagged =
            GenericMindDynamicTestFixture.Fields(items[1]);

        // The role tag SHIPS: field 6 is present on the command that set one.
        Assert.Equal(
            new ushort[] { 1, 2, 3, 4, 5, 6, 7 },
            tagged.Keys.Order());
        Assert.Equal(GenericMindWireFieldIds.MindCommand.RoleTag, (ushort)6);
        // Absent means "leave the published tag unchanged", so a command that
        // set none spends no bytes on one.
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5 }, untagged.Keys.Order());
    }

    [Fact]
    public void AnEmptyRoleTagIsLegalAndMeansClear()
    {
        MindDecisions original =
            GenericMindDynamicTestFixture.Decisions(roleTag: "");

        MindDecisions decoded = GenericMindWireDecisionCodec.Decode(
            GenericMindWireDecisionCodec.Encode(original));

        Assert.Equal("", decoded.Commands[0].RoleTag);
        Assert.Null(decoded.Commands[1].RoleTag);
    }

    [Theory]
    [InlineData("Channeler")]
    [InlineData("channel_er")]
    [InlineData("channeler-")]
    [InlineData("a-very-long-role-tag-name")]
    public void AnInvalidOrOversizedRoleTagIsRefused(string roleTag) =>
        Assert.Throws<ArgumentException>(
            () => new MindCommand(0, 0, "wait", 0, [], roleTag));

    [Fact]
    public void TheRoleTagCapIsTwentyFourUtf8BytesNotCharacters()
    {
        // Exactly 24 bytes of lowercase kebab is the boundary, and it is a
        // BYTE budget: a display label sent per body per tick should be
        // visibly tight.
        Assert.Equal(24, GenericMindContractVersions.MaxRoleTagUtf8Bytes);
        var atCap = new MindCommand(0, 0, "wait", 0, [], new string('a', 24));
        Assert.Equal(new string('a', 24), atCap.RoleTag);
        Assert.Throws<ArgumentException>(
            () => new MindCommand(0, 0, "wait", 0, [], new string('a', 25)));
    }

    [Fact]
    public void DeclaredIntentsRideAsAnEmptyCollectionAndRefuseAPopulatedOne()
    {
        byte[] payload = GenericMindWireDecisionCodec.Encode(
            GenericMindDynamicTestFixture.Decisions());
        byte[] intents = GenericMindDynamicTestFixture.Fields(payload)[
            GenericMindWireFieldIds.MindDecisions.Intents];

        Assert.Empty(GenericMindDynamicTestFixture.Items(intents));

        var declared = new MindDecisions(
            GenericMindContractVersions.DecisionSchemaVersion,
            9,
            [],
            [new MindDeclaredIntent("press-left", 1)]);
        Assert.Throws<InvalidOperationException>(
            () => GenericMindWireDecisionCodec.Encode(declared));
    }

    [Fact]
    public void ADecisionSchemaOtherThanTheProfilesIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MindDecisions(2, 0, []));

    [Fact]
    public void TwoCommandsForOneBodyStillEncode_TheHostIsWhatFaultsThem()
    {
        // The codec is not the admission boundary. A duplicate is a FAULT the
        // host records against the participant, and keeping the wire able to
        // carry it is what lets the replay show what was actually submitted
        // rather than a sanitized version of it.
        var duplicated = new MindDecisions(
            GenericMindContractVersions.DecisionSchemaVersion,
            9,
            [
                new MindCommand(0, 4, "wait", 0, []),
                new MindCommand(0, 4, "move", 1, []),
            ]);

        MindDecisions decoded = GenericMindWireDecisionCodec.Decode(
            GenericMindWireDecisionCodec.Encode(duplicated));

        Assert.Equal(2, decoded.Commands.Length);
        Assert.Equal(decoded.Commands[0].UnitId, decoded.Commands[1].UnitId);
    }
}
