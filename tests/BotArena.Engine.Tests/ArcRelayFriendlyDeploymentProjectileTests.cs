using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Tapline-15's deployment projectile-contact matrix. Arc Relay has no
/// separate Mine deployment: Minesmith's mine is the Trip Node signature.
/// </summary>
public sealed class ArcRelayFriendlyDeploymentProjectileTests
{
    [Theory]
    [InlineData(ArcRelayLaunchClassIds.Nest, "sentinel-seed", 20, 10)]
    [InlineData(ArcRelayLaunchClassIds.Minesmith, "trip-node", 10, 0)]
    [InlineData(ArcRelayLaunchClassIds.Mason, "hardlight-block", 30, 20)]
    [InlineData(ArcRelayLaunchClassIds.Palisade, "prism-wall", 30, 20)]
    public void FriendlyShotsAreTransparentAndHostileShotsDamageTheConstruct(
        string classId,
        string actionId,
        int initialHull,
        int hostileRemainingHull)
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity owner, Position tile) =
            Deployed(classId, actionId);

        ArcRelaySignatureState initial = Assert.Single(runtime.Project(2));
        Assert.Equal(initialHull, initial.RemainingCapacity);

        Assert.False(runtime.TryConsumeProjectile(
            tile,
            projectileTeamId: owner.TeamId,
            tick: 2,
            damage: 10,
            out GenericActorModeEvent? friendlyContact));
        Assert.Null(friendlyContact);
        Assert.Equal(
            initialHull,
            Assert.Single(runtime.Project(2)).RemainingCapacity);

        Assert.True(runtime.TryConsumeProjectile(
            tile,
            projectileTeamId: 1,
            tick: 2,
            damage: 10,
            out GenericActorModeEvent? hostileContact));
        Assert.NotNull(hostileContact);
        Assert.Equal(
            hostileRemainingHull,
            runtime.Project(2).SingleOrDefault()?.RemainingCapacity ?? 0);
        Assert.Equal(
            hostileRemainingHull == 0
                ? "destroyed-projectile"
                : "damaged-projectile",
            Changed(hostileContact!).Reason);
    }

    private static (
        ArcRelaySignatureRuntime Runtime,
        ActorIdentity Owner,
        Position Tile) Deployed(
        string classId,
        string actionId)
    {
        string[] composition =
        [
            classId,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Relay,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.Tapline15);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity owner = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        Position source = new(14, 13);
        var life = new ArcRelaySignatureRuntime.Life(
            owner,
            source,
            Health: 50,
            MaxHealth: 50,
            Direction.East);
        ImmutableArray<GenericActorRuntimeActionArgument> arguments;
        Position tile;
        if (actionId == "prism-wall")
        {
            tile = new Position(15, 13);
            arguments =
            [
                new GenericActorRuntimeActionArgument.DirectionArgument(
                    Direction.East),
            ];
        }
        else
        {
            tile = new Position(15, 13);
            arguments =
            [
                new GenericActorRuntimeActionArgument.PositionTargetArgument(
                    tile),
            ];
        }

        runtime.Start(0, owner, source, actionId, arguments, [life]);
        runtime.Advance(1, [life]);
        runtime.ResolvePostMovement(1, [life], (_, _) => true);
        return (runtime, owner, tile);
    }

    private static ArcRelayEvent.SignatureChanged Changed(
        GenericActorModeEvent modeEvent) => Assert.IsType<
            ArcRelayEvent.SignatureChanged>(Assert.IsType<
                GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                modeEvent.Payload).Fact);
}
