namespace BotArena.Engine.Tests;

public sealed class ActorMovementDependencyResolverTests
{
    [Fact]
    public void AcyclicFollowingChainAdvancesIntoVacancy()
    {
        ActorIdentity a = Id(0);
        ActorIdentity b = Id(1);
        ActorIdentity c = Id(2);
        Dictionary<Position, ActorIdentity> occupants = Occupants(
            (a, P(1)), (b, P(2)), (c, P(3)));
        Dictionary<ActorIdentity, Position> targets = Targets(
            (a, P(2)), (b, P(3)), (c, P(4)));

        HashSet<ActorIdentity> blocked =
            ActorMovementDependencyResolver.ResolveBlocked(
                targets, occupants, [], followingVacatedActorAllowed: true);

        Assert.Empty(blocked);
    }

    [Fact]
    public void SwapAndLongerClosedCycleRemainBlocked()
    {
        ActorIdentity a = Id(0);
        ActorIdentity b = Id(1);
        ActorIdentity c = Id(2);
        Dictionary<Position, ActorIdentity> occupants = Occupants(
            (a, P(1)), (b, P(2)), (c, P(3)));

        Assert.Equal(
            [a, b],
            ActorMovementDependencyResolver.ResolveBlocked(
                    Targets((a, P(2)), (b, P(1))),
                    occupants,
                    [],
                    followingVacatedActorAllowed: true)
                .Order());
        Assert.Equal(
            [a, b, c],
            ActorMovementDependencyResolver.ResolveBlocked(
                    Targets((a, P(2)), (b, P(3)), (c, P(1))),
                    occupants,
                    [],
                    followingVacatedActorAllowed: true)
                .Order());
    }

    [Fact]
    public void StationaryOrBlockedHeadBlocksEveryFollower()
    {
        ActorIdentity a = Id(0);
        ActorIdentity b = Id(1);
        ActorIdentity c = Id(2);
        Dictionary<Position, ActorIdentity> occupants = Occupants(
            (a, P(1)), (b, P(2)), (c, P(3)));
        Dictionary<ActorIdentity, Position> targets = Targets(
            (a, P(2)), (b, P(3)));

        Assert.Equal(
            [a, b],
            ActorMovementDependencyResolver.ResolveBlocked(
                    targets,
                    occupants,
                    [],
                    followingVacatedActorAllowed: true)
                .Order());

        targets[c] = P(4);
        Assert.Equal(
            [a, b, c],
            ActorMovementDependencyResolver.ResolveBlocked(
                    targets,
                    occupants,
                    [c],
                    followingVacatedActorAllowed: true)
                .Order());
    }

    [Fact]
    public void ContestedDestinationBlocksClaimantsAndTheirFollowers()
    {
        ActorIdentity a = Id(0);
        ActorIdentity b = Id(1);
        ActorIdentity c = Id(2);
        Dictionary<Position, ActorIdentity> occupants = Occupants(
            (a, P(1)), (b, P(2)), (c, P(3)));
        Dictionary<ActorIdentity, Position> targets = Targets(
            (a, P(2)), (b, P(4)), (c, P(4)));

        Assert.Equal(
            [a, b, c],
            ActorMovementDependencyResolver.ResolveBlocked(
                    targets,
                    occupants,
                    [],
                    followingVacatedActorAllowed: true)
                .Order());
    }

    [Fact]
    public void ClosedProfileStillBlocksFollowingVacatedTile()
    {
        ActorIdentity a = Id(0);
        ActorIdentity b = Id(1);
        Dictionary<Position, ActorIdentity> occupants = Occupants(
            (a, P(1)), (b, P(2)));

        Assert.Equal(
            [a],
            ActorMovementDependencyResolver.ResolveBlocked(
                    Targets((a, P(2)), (b, P(3))),
                    occupants,
                    [],
                    followingVacatedActorAllowed: false)
                .Order());
    }

    private static ActorIdentity Id(int unitId) => new(0, unitId, 0);
    private static Position P(int x) => new(x, 1);

    private static Dictionary<Position, ActorIdentity> Occupants(
        params (ActorIdentity ActorId, Position Position)[] values) =>
        values.ToDictionary(value => value.Position, value => value.ActorId);

    private static Dictionary<ActorIdentity, Position> Targets(
        params (ActorIdentity ActorId, Position Position)[] values) =>
        values.ToDictionary(value => value.ActorId, value => value.Position);
}
