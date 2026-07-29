using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

/// <summary>
/// The SDK half of the mirror set for the class-skill kit: the appended
/// observed-event value must survive the wire codec with its meaning intact,
/// and its payload must refuse the shapes the engine can never produce. The
/// canonical-contract half of the mirror (an absent guard means None, an
/// absent volley means one bolt, and an explicitly inert encoding of either is
/// rejected) is exercised end to end through
/// <c>GenericActorCanonicalContractValidator</c>, which calls this assembly's
/// reader, in the engine's skill-arm suites.
/// </summary>
public sealed class GenericActorSkillContractMirrorTests
{
    [Fact]
    public void TheAbsorptionEventRoundTripsOverTheWire()
    {
        var payload =
            new GenericActorContext.EventPayload.ProjectileAbsorbed(
                sourceTeamId: 0,
                new ActorIdentity(0, 0, 3),
                new ActorIdentity(1, 2, 4),
                projectileId: long.MaxValue,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.NorthEast,
                new Position(12, 13));
        var source = new GenericActorContext.ObservedEvent(
            "event:1:2",
            sourceTick: 1,
            sourceOrdinal: 2,
            GenericActorContext.EventKind.ProjectileAbsorbed,
            payload,
            [new ActorIdentity(1, 2, 4)]);

        GenericActorContext.ObservedEvent decoded =
            GenericActorWireEventCodec.DecodeEvent(
                GenericActorWireEventCodec.EncodeEvent(source),
                depth: 0);

        Assert.Equal(
            GenericActorContext.EventKind.ProjectileAbsorbed,
            decoded.Kind);
        Assert.Equal(
            payload,
            Assert.IsType<
                GenericActorContext.EventPayload.ProjectileAbsorbed>(
                    decoded.Payload));
        Assert.Equal(source.EventHandle, decoded.EventHandle);
        Assert.Equal(source.SourceOrdinal, decoded.SourceOrdinal);
    }

    [Fact]
    public void AnAbsorptionPayloadOnlyFitsItsOwnEventKind()
    {
        var payload =
            new GenericActorContext.EventPayload.ProjectileAbsorbed(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: 7,
                "bulwark-prime-aegis-shell",
                Direction.North,
                ProjectileHeading.South,
                new Position(3, 4));

        Assert.Throws<ArgumentException>(() =>
            new GenericActorContext.ObservedEvent(
                "event:1:2",
                sourceTick: 1,
                sourceOrdinal: 2,
                GenericActorContext.EventKind.Damage,
                payload,
                [new ActorIdentity(1, 2, 4)]));
    }

    [Fact]
    public void AnAbsorptionSourceMustAgreeWithItsReportedTeam()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenericActorContext.EventPayload.ProjectileAbsorbed(
                sourceTeamId: 0,
                new ActorIdentity(1, 0, 0),
                new ActorIdentity(1, 2, 4),
                projectileId: 1,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
    }

    [Fact]
    public void AnAbsorptionNamesAFormAndAValidPose()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenericActorContext.EventPayload.ProjectileAbsorbed(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: 1,
                targetFormId: "  ",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorContext.EventPayload.ProjectileAbsorbed(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: -1,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
    }
}
