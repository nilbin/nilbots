using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalCustodyPrimitivesTests
{
    [Theory]
    [InlineData("require", 0)]
    [InlineData("prefer", 1)]
    [InlineData("allow", 2)]
    [InlineData("forbid", int.MaxValue)]
    public void CarrierPreferenceIsAnExecutableAllocationOrder(
        string preference,
        int expected) => Assert.Equal(
            expected,
            TacticalCustodyPrimitives.CarrierPreferenceRank(preference));

    [Theory]
    [InlineData("same-carrier", true, true, true)]
    [InlineData("same-carrier", false, true, false)]
    [InlineData("nearest-authorized", false, false, true)]
    [InlineData("guard-until-safe", false, false, false)]
    [InlineData("guard-until-safe", false, true, true)]
    public void DropRecoveryHonorsAuthoredPolicy(
        string policy,
        bool sameLife,
        bool safe,
        bool expected)
    {
        var source = new ActorIdentity(0, 3, 9);
        var candidate = sameLife
            ? source
            : new ActorIdentity(0, 4, 2);

        Assert.Equal(expected, TacticalCustodyPrimitives.MayRecoverDrop(
            policy, candidate, source, safe));
    }

    [Fact]
    public void EscortChoosesNearestCarrierThenStableActorIdentity()
    {
        var escort = new Position(5, 5);
        var later = (new ActorIdentity(0, 7, 1), new Position(6, 5));
        var earlier = (new ActorIdentity(0, 2, 4), new Position(4, 5));

        Assert.True(TacticalCustodyPrimitives.CompareEscortCandidate(
            escort, earlier, later) < 0);
        Assert.True(TacticalCustodyPrimitives.CompareEscortCandidate(
            new Position(0, 0), later, earlier) > 0);
    }

    [Theory]
    [InlineData(7, 8, true)]
    [InlineData(8, 8, false)]
    public void TransferWindowHasAnExactBound(
        int carriedTicks,
        int timeout,
        bool expected) => Assert.Equal(
            expected,
            TacticalCustodyPrimitives.TransferWindowOpen(
                carriedTicks, timeout));

    [Fact]
    public void AuthorizedCarrierOwnsTheBoundedTransferRendezvous()
    {
        Assert.Equal("authorized-carrier",
            TacticalCustodyPrimitives.TransferRendezvousMover(true));
        Assert.Equal("accidental-carrier-delivers",
            TacticalCustodyPrimitives.TransferRendezvousMover(false));
    }

    [Theory]
    [InlineData(119, 120, false)]
    [InlineData(120, 120, true)]
    public void DeliveryTimeoutCountsOnlyStagnantTicks(
        int stagnantTicks,
        int timeout,
        bool expected) => Assert.Equal(
            expected,
            TacticalCustodyPrimitives.DeliveryTimedOut(
                stagnantTicks, timeout));
}
