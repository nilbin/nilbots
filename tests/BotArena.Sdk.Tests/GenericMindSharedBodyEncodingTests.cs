using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

/// <summary>
/// THE FIELD-BY-FIELD PIN. A mind body's fields 1..13 must be byte-identical to
/// the per-life self and ally encoding of the same facts.
///
/// <para>This is what makes the whole comparison between the two profiles
/// checkable. If a mind body encoded the same facts in a different order, with
/// a different optional-field rule, or through a second serializer, then a
/// difference in outcome between the profiles could always be blamed on the
/// wire — and the null pin would prove nothing. Reusing the shared encoder
/// verbatim is what turns "the mind plays the same game" from a claim into a
/// byte comparison.</para>
/// </summary>
public sealed class GenericMindSharedBodyEncodingTests
{
    [Fact]
    public void AMindBodysFirstThirteenFieldsAreTheSelfEncodingVerbatim()
    {
        MindBody body = GenericMindDynamicTestFixture.Body(
            GenericActorDynamicTestFixture.SelfActor);
        var self = new GenericActorContext.ObservedSelfState(
            body.ActorId,
            body.Generation,
            body.FormId,
            body.Position,
            body.Facing,
            body.Health,
            body.Cooldown,
            body.Energy,
            body.PreviousActionResolution,
            body.PendingSameLifeTransition,
            body.ClassId,
            body.RouteCooldowns,
            body.CarriedScrap);

        Assert.Equal(SharedPrefix(SelfFields(self)), SharedPrefix(BodyFields(body)));
    }

    [Fact]
    public void AMindBodysFirstThirteenFieldsAreTheAllyEncodingVerbatim()
    {
        MindBody body = GenericMindDynamicTestFixture.Body(
            GenericActorDynamicTestFixture.AllyActor);
        var ally = new GenericActorContext.ObservedAllyState(
            body.ActorId,
            body.Generation,
            body.FormId,
            body.Position,
            body.Facing,
            body.Health,
            body.Cooldown,
            body.Energy,
            body.PreviousActionResolution,
            body.PendingSameLifeTransition,
            body.ClassId,
            body.RouteCooldowns,
            body.CarriedScrap);

        Assert.Equal(SharedPrefix(AllyFields(ally)), SharedPrefix(BodyFields(body)));
    }

    [Fact]
    public void TheNullPinHoldsOnTheInertDefaultsToo()
    {
        // A classless, economy-free, cooldown-free body: exactly the shape
        // every pre-composition ruleset produces. The optional-field rules must
        // agree here as well, or the two profiles would differ on the very
        // contracts the measured cohorts were frozen against.
        MindBody body = new(
            new ActorIdentity(0, 3, 0),
            generation: 0,
            "mobile",
            new Position(1, 1),
            Direction.North,
            health: 3,
            cooldown: 0,
            energy: null,
            previousActionResolution: null,
            pendingSameLifeTransition: null,
            classId: null,
            routeCooldowns: [],
            carriedScrap: 0,
            previousPosition: null,
            movedLastTick: false,
            lifeStartedTick: 0,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                0,
                null,
                null,
                null),
            roleTag: null,
            actionLegalities: [],
            GenericMindDynamicTestFixture.Wait);
        var self = new GenericActorContext.ObservedSelfState(
            body.ActorId,
            body.Generation,
            body.FormId,
            body.Position,
            body.Facing,
            body.Health,
            body.Cooldown,
            body.Energy,
            null,
            null);

        ImmutableDictionary<ushort, byte[]> bodyFields = BodyFields(body);

        Assert.Equal(SharedPrefix(SelfFields(self)), SharedPrefix(bodyFields));
        // Absent optionals stay absent on both sides rather than becoming a
        // second encoding of "nothing".
        Assert.Equal(
            new ushort[] { 1, 2, 3, 4, 5, 6, 7 },
            SharedPrefix(bodyFields).Keys.Order());
        // And the mind's own additions carry the inert defaults: no previous
        // position on a first tick, no role tag until one is set.
        Assert.False(
            bodyFields.ContainsKey(
                GenericMindWireFieldIds.MindBodyState.PreviousPosition));
        Assert.False(
            bodyFields.ContainsKey(
                GenericMindWireFieldIds.MindBodyState.RoleTag));
    }

    private static ImmutableDictionary<ushort, byte[]> SharedPrefix(
        ImmutableDictionary<ushort, byte[]> fields) =>
        fields
            .Where(field => field.Key <= 13)
            .ToImmutableDictionary(
                field => field.Key,
                field => field.Value);

    private static ImmutableDictionary<ushort, byte[]> BodyFields(
        MindBody body)
    {
        MindContext context = new(
            MindContext.CurrentSchemaVersion,
            0,
            new string('a', 64),
            [body],
            [],
            [],
            [],
            [],
            null,
            [],
            null,
            new GenericActorContext.ScoreboardState(
                [
                    new GenericActorContext.TeamScoreState(
                        0,
                        eligible: true,
                        [new GenericActorContext.ScoreValue("kills", 0)]),
                ]),
            new GenericActorContext.ModeObservationState.Deathmatch(
                "deathmatch"),
            []);
        byte[] payload = GenericMindWireObservationCodec.Encode(context);
        byte[] bodies = GenericMindDynamicTestFixture.Fields(payload)[
            GenericMindWireFieldIds.MindObservation.Bodies];
        return GenericMindDynamicTestFixture.Fields(
            GenericMindDynamicTestFixture.Items(bodies).Single());
    }

    private static ImmutableDictionary<ushort, byte[]> SelfFields(
        GenericActorContext.ObservedSelfState self)
    {
        byte[] payload = GenericActorWireObservationCodec.Encode(
            ActorContext(self, ally: null));
        return GenericMindDynamicTestFixture.Fields(
            GenericMindDynamicTestFixture.Fields(payload)[4]);
    }

    private static ImmutableDictionary<ushort, byte[]> AllyFields(
        GenericActorContext.ObservedAllyState ally)
    {
        byte[] payload = GenericActorWireObservationCodec.Encode(
            ActorContext(
                new GenericActorContext.ObservedSelfState(
                    new ActorIdentity(0, 9, 9),
                    0,
                    "mobile",
                    new Position(0, 0),
                    Direction.North,
                    3,
                    0,
                    null,
                    null,
                    null),
                ally));
        byte[] allies = GenericMindDynamicTestFixture.Fields(payload)[7];
        return GenericMindDynamicTestFixture.Fields(
            GenericMindDynamicTestFixture.Items(allies).Single());
    }

    private static GenericActorContext ActorContext(
        GenericActorContext.ObservedSelfState self,
        GenericActorContext.ObservedAllyState? ally) =>
        new(
            GenericActorContext.CurrentSchemaVersion,
            0,
            new string('a', 64),
            self,
            [],
            [],
            ally is null ? [] : [ally],
            [],
            [],
            null,
            [],
            null,
            new GenericActorContext.ScoreboardState(
                [
                    new GenericActorContext.TeamScoreState(
                        0,
                        eligible: true,
                        [new GenericActorContext.ScoreValue("kills", 0)]),
                ]),
            new GenericActorContext.ModeObservationState.Deathmatch(
                "deathmatch"),
            []);
}
