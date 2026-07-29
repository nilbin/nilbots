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
    public void TheDeflectionEventRoundTripsOverTheWire()
    {
        var payload =
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                new ActorIdentity(0, 0, 3),
                new ActorIdentity(1, 2, 4),
                projectileId: long.MaxValue,
                deflectedProjectileId: long.MaxValue - 1,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.NorthEast,
                new Position(12, 13));
        var source = new GenericActorContext.ObservedEvent(
            "event:1:2",
            sourceTick: 1,
            sourceOrdinal: 2,
            GenericActorContext.EventKind.ProjectileDeflected,
            payload,
            [new ActorIdentity(1, 2, 4)]);

        GenericActorContext.ObservedEvent decoded =
            GenericActorWireEventCodec.DecodeEvent(
                GenericActorWireEventCodec.EncodeEvent(source),
                depth: 0);

        Assert.Equal(
            GenericActorContext.EventKind.ProjectileDeflected,
            decoded.Kind);
        Assert.Equal(
            payload,
            Assert.IsType<
                GenericActorContext.EventPayload.ProjectileDeflected>(
                    decoded.Payload));
        Assert.Equal(source.EventHandle, decoded.EventHandle);
        Assert.Equal(source.SourceOrdinal, decoded.SourceOrdinal);
    }

    [Fact]
    public void ADeflectionPayloadOnlyFitsItsOwnEventKind()
    {
        var payload =
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: 7,
                deflectedProjectileId: 8,
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
    public void ADeflectionSourceMustAgreeWithItsReportedTeam()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                new ActorIdentity(1, 0, 0),
                new ActorIdentity(1, 2, 4),
                projectileId: 1,
                deflectedProjectileId: 2,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
    }

    [Fact]
    public void ADeflectionNamesAFormAndAValidPose()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: 1,
                deflectedProjectileId: 2,
                targetFormId: "  ",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: -1,
                deflectedProjectileId: 2,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
    }

    /// <summary>
    /// The returned bolt is a genuinely new projectile. A payload that reuses
    /// the consumed identity would let a forged history claim a deflection
    /// while producing nothing, so the type itself refuses it.
    /// </summary>
    [Fact]
    public void ADeflectionCannotReturnTheProjectileItConsumed()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: 5,
                deflectedProjectileId: 5,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorContext.EventPayload.ProjectileDeflected(
                sourceTeamId: 0,
                sourceActorId: null,
                new ActorIdentity(1, 2, 4),
                projectileId: 5,
                deflectedProjectileId: -1,
                "bulwark-child-aegis-shell",
                Direction.West,
                ProjectileHeading.East,
                new Position(1, 1)));
    }
}
