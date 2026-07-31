using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

public sealed class GenericMindObservationCodecTests
{
    [Fact]
    public void AMaximalObservationRoundTripsEveryField()
    {
        MindContext original = GenericMindDynamicTestFixture.Context();

        byte[] encoded = GenericMindWireObservationCodec.Encode(original);
        MindContext decoded = GenericMindWireObservationCodec.Decode(
            encoded,
            GenericMindDynamicTestFixture.Wait);

        // Re-encoding the decoded frame must reproduce the exact bytes. That is
        // a stronger statement than field-by-field equality and it is the one
        // that matters on the wire: any field silently dropped, reordered or
        // re-defaulted by the decoder shows up here.
        Assert.Equal(
            encoded,
            GenericMindWireObservationCodec.Encode(decoded));

        Assert.Equal(original.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(original.Tick, decoded.Tick);
        Assert.Equal(
            original.MatchContractFingerprint,
            decoded.MatchContractFingerprint);
        Assert.Equal(original.Bodies.Length, decoded.Bodies.Length);
        Assert.Equal(original.Slots.Length, decoded.Slots.Length);
        Assert.Empty(decoded.AlliedIntents);

        foreach ((MindBody expected, MindBody actual) in
                 original.Bodies.Zip(decoded.Bodies))
        {
            Assert.Equal(expected.ActorId, actual.ActorId);
            Assert.Equal(expected.Generation, actual.Generation);
            Assert.Equal(expected.FormId, actual.FormId);
            Assert.Equal(expected.ClassId, actual.ClassId);
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.Facing, actual.Facing);
            Assert.Equal(expected.Health, actual.Health);
            Assert.Equal(expected.Cooldown, actual.Cooldown);
            Assert.Equal(expected.Energy, actual.Energy);
            Assert.Equal(expected.CarriedScrap, actual.CarriedScrap);
            Assert.Equal(
                expected.RouteCooldowns.AsEnumerable(),
                actual.RouteCooldowns.AsEnumerable());
            Assert.Equal(
                expected.PendingSameLifeTransition,
                actual.PendingSameLifeTransition);
            Assert.Equal(
                expected.PreviousActionResolution?.Outcome,
                actual.PreviousActionResolution?.Outcome);
            // The facts a mind is entitled to and a per-life bot was not.
            Assert.Equal(expected.PreviousPosition, actual.PreviousPosition);
            Assert.Equal(expected.MovedLastTick, actual.MovedLastTick);
            Assert.Equal(expected.LifeStartedTick, actual.LifeStartedTick);
            Assert.Equal(expected.Origin, actual.Origin);
            Assert.Equal(expected.RoleTag, actual.RoleTag);
            Assert.Equal(
                expected.ActionLegalities.Length,
                actual.ActionLegalities.Length);
        }

        Assert.Equal(
            original.Allies.Select(ally => ally.ActorId),
            decoded.Allies.Select(ally => ally.ActorId));
        Assert.Equal(
            original.Enemies.Select(enemy => enemy.ActorId),
            decoded.Enemies.Select(enemy => enemy.ActorId));
        Assert.Equal(
            original.VisibleTiles.Select(tile => tile.Position),
            decoded.VisibleTiles.Select(tile => tile.Position));
        Assert.Equal(
            original.VisibleProjectiles!.Value.Select(p => p.ProjectileId),
            decoded.VisibleProjectiles!.Value.Select(p => p.ProjectileId));
        Assert.Equal(
            original.VisibleEvents.Select(e => e.EventHandle),
            decoded.VisibleEvents.Select(e => e.EventHandle));
        Assert.Equal(
            original.HeardSounds!.Value.Select(s => s.EventHandle),
            decoded.HeardSounds!.Value.Select(s => s.EventHandle));
        Assert.Equal(
            original.Participants.Select(p => p.ParticipantId),
            decoded.Participants.Select(p => p.ParticipantId));
        Assert.Equal(original.Mode.ModeId, decoded.Mode.ModeId);
        Assert.Equal(
            original.Scoreboard.Teams.Length,
            decoded.Scoreboard.Teams.Length);
        Assert.Equal(
            original.Slots.Select(slot => (slot.UnitId, slot.ClassId)),
            decoded.Slots.Select(slot => (slot.UnitId, slot.ClassId)));
    }

    [Fact]
    public void AbsentCapabilitiesRoundTripAsNullRatherThanEmpty()
    {
        MindContext original =
            GenericMindDynamicTestFixture.Context(nullCapabilities: true);

        MindContext decoded = GenericMindWireObservationCodec.Decode(
            GenericMindWireObservationCodec.Encode(original),
            GenericMindDynamicTestFixture.Wait);

        // Null means "this contract has no such capability"; empty would mean
        // "it has one and saw nothing". Collapsing the two would make a bot
        // unable to tell a blind ruleset from a quiet tick.
        Assert.Null(decoded.VisibleProjectiles);
        Assert.Null(decoded.HeardSounds);
    }

    [Fact]
    public void AZeroBodyObservationEncodesAndDecodes()
    {
        // Every body dead at once is an ordinary state, and the frame has to
        // carry it: the mind still ticks, and its slot table is how it plans
        // the return.
        MindContext original =
            GenericMindDynamicTestFixture.Context(bodyCount: 0);

        MindContext decoded = GenericMindWireObservationCodec.Decode(
            GenericMindWireObservationCodec.Encode(original),
            GenericMindDynamicTestFixture.Wait);

        Assert.Empty(decoded.Bodies);
        Assert.Equal(3, decoded.Slots.Length);
    }

    [Fact]
    public void TheObservationFrameUsesExactlyTheReservedFieldIds()
    {
        byte[] payload = GenericMindWireObservationCodec.Encode(
            GenericMindDynamicTestFixture.Context());

        ImmutableDictionary<ushort, byte[]> fields =
            GenericMindDynamicTestFixture.Fields(payload);

        Assert.Equal(
            new ushort[]
            {
                GenericMindWireFieldIds.MindObservation.SchemaVersion,
                GenericMindWireFieldIds.MindObservation.Tick,
                GenericMindWireFieldIds.MindObservation
                    .MatchContractFingerprint,
                GenericMindWireFieldIds.MindObservation.Allies,
                GenericMindWireFieldIds.MindObservation.Enemies,
                GenericMindWireFieldIds.MindObservation.VisibleTiles,
                GenericMindWireFieldIds.MindObservation.VisibleProjectiles,
                GenericMindWireFieldIds.MindObservation.VisibleEvents,
                GenericMindWireFieldIds.MindObservation.HeardSounds,
                GenericMindWireFieldIds.MindObservation.Scoreboard,
                GenericMindWireFieldIds.MindObservation.Mode,
                GenericMindWireFieldIds.MindObservation.Participants,
                GenericMindWireFieldIds.MindObservation.Bodies,
                GenericMindWireFieldIds.MindObservation.Slots,
                GenericMindWireFieldIds.MindObservation.AlliedIntents,
            }.Order(),
            fields.Keys.Order());

        // The literal allocation, restated: a renumbering that kept the
        // constants in step with the encoder would still be a wire break.
        Assert.Equal(
            new ushort[] { 1, 2, 3, 10, 11, 12, 13, 14, 15, 16, 17, 18, 20, 21, 30 },
            fields.Keys.Order());
    }

    [Fact]
    public void TheBodyFrameUsesExactlyTheReservedFieldIds()
    {
        byte[] payload = GenericMindWireObservationCodec.Encode(
            GenericMindDynamicTestFixture.Context(bodyCount: 1));
        byte[] bodies = GenericMindDynamicTestFixture.Fields(payload)[
            GenericMindWireFieldIds.MindObservation.Bodies];
        byte[] body = GenericMindDynamicTestFixture.Items(bodies).Single();

        ImmutableDictionary<ushort, byte[]> fields =
            GenericMindDynamicTestFixture.Fields(body);

        // 1..13 are the SHARED body encoding, in the existing order; 14..19 are
        // the facts a mind is entitled to and a per-life bot was not.
        Assert.Equal(
            Enumerable.Range(1, 19).Select(id => (ushort)id),
            fields.Keys.Order());
        Assert.Equal(
            GenericMindWireFieldIds.MindBodyState.PreviousPosition,
            (ushort)14);
        Assert.Equal(
            GenericMindWireFieldIds.MindBodyState.MovedLastTick,
            (ushort)15);
        Assert.Equal(
            GenericMindWireFieldIds.MindBodyState.LifeStartedTick,
            (ushort)16);
        Assert.Equal(GenericMindWireFieldIds.MindBodyState.Origin, (ushort)17);
        Assert.Equal(GenericMindWireFieldIds.MindBodyState.RoleTag, (ushort)18);
        Assert.Equal(
            GenericMindWireFieldIds.MindBodyState.ActionLegalities,
            (ushort)19);
    }

    [Fact]
    public void ASlotWithNoReservationSpendsNoBytesOnOne()
    {
        byte[] payload = GenericMindWireObservationCodec.Encode(
            GenericMindDynamicTestFixture.Context());
        byte[] slots = GenericMindDynamicTestFixture.Fields(payload)[
            GenericMindWireFieldIds.MindObservation.Slots];
        byte[] classlessSlot =
            GenericMindDynamicTestFixture.Items(slots)[2];

        ImmutableDictionary<ushort, byte[]> fields =
            GenericMindDynamicTestFixture.Fields(classlessSlot);

        // A fixed, classless slot carries the unit and its state and nothing
        // else: the reserved candidate/selected chassis fields are absent, so a
        // ruleset without compositions pays no bytes for the reservation.
        Assert.Equal(
            new ushort[]
            {
                GenericMindWireFieldIds.MindSlotState.UnitId,
                GenericMindWireFieldIds.MindSlotState.State,
            },
            fields.Keys.Order());
    }

    [Fact]
    public void AlliedIntentsRideAsAnEmptyCollectionAndRefuseAPopulatedOne()
    {
        byte[] payload = GenericMindWireObservationCodec.Encode(
            GenericMindDynamicTestFixture.Context());
        byte[] alliedIntents = GenericMindDynamicTestFixture.Fields(payload)[
            GenericMindWireFieldIds.MindObservation.AlliedIntents];

        // Present, empty: the field ID is spent and the shape is negotiated for
        // the cost of one tagged field with a zero count.
        Assert.Empty(GenericMindDynamicTestFixture.Items(alliedIntents));

        MindContext populated = GenericMindDynamicTestFixture.Context();
        MindContext withIntent = new(
            populated.SchemaVersion,
            populated.Tick,
            populated.MatchContractFingerprint,
            populated.Bodies,
            populated.Slots,
            populated.Allies,
            populated.Enemies,
            populated.VisibleTiles,
            populated.VisibleProjectiles,
            populated.VisibleEvents,
            populated.HeardSounds,
            populated.Scoreboard,
            populated.Mode,
            populated.Participants,
            [new MindContext.AlliedIntent(2, "press-left", 1)]);

        Assert.Throws<InvalidOperationException>(
            () => GenericMindWireObservationCodec.Encode(withIntent));
    }

    [Fact]
    public void AnObservationSchemaOtherThanTheProfilesIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GenericMindDynamicTestFixture.Context(schemaVersion: 2));
    }

    [Fact]
    public void ABodyCannotAlsoAppearAsAnAlliedMindsBody()
    {
        MindContext source = GenericMindDynamicTestFixture.Context();
        MindBody own = source.Bodies[0];

        Assert.Throws<ArgumentException>(() => new MindContext(
            source.SchemaVersion,
            source.Tick,
            source.MatchContractFingerprint,
            source.Bodies,
            source.Slots,
            [
                new GenericActorContext.ObservedAllyState(
                    own.ActorId,
                    own.Generation,
                    own.FormId,
                    own.Position,
                    own.Facing,
                    own.Health,
                    own.Cooldown,
                    own.Energy,
                    null,
                    null),
            ],
            source.Enemies,
            source.VisibleTiles,
            source.VisibleProjectiles,
            source.VisibleEvents,
            source.HeardSounds,
            source.Scoreboard,
            source.Mode,
            source.Participants));
    }
}
