using BotArena.Sdk;

public sealed class CustodyLatchTests
{
    private static readonly ActorIdentity Carrier = new(0, 3, 0);

    [Fact]
    public void ACarrierFinishesItsCarryUnderThePickupPhase()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], "t1", "p2");
        // The squad advances; the carrier's own view keeps p2 lit.
        var squad = new HashSet<string> { "t1", "t1/p3" };
        IReadOnlySet<string>? tags = latch.Augment(3, squad);
        Assert.NotNull(tags);
        Assert.Contains("t1/p2", tags!);
        Assert.Contains("t1/p3", tags!);
        Assert.Contains("t1", tags!);
        // Everyone else sees exactly the squad set, same instance.
        Assert.Same(squad, latch.Augment(4, squad));
    }

    [Fact]
    public void PickupPhaseCustodyClaimsOnlyTheCarriersOwnGroup()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], "t1", "p2", [[3, 4, 6], [0, 1, 2]]);
        var advanced = new HashSet<string> { "t1", "t1/p3" };

        Assert.Contains("t1/p2", latch.Augment(3, advanced)!);
        Assert.Contains("t1/p2", latch.Augment(4, advanced)!);
        Assert.Contains("t1/p2", latch.Augment(6, advanced)!);
        Assert.Same(advanced, latch.Augment(1, advanced));

        // Bank, loss or drop ends the temporary branch for carrier and
        // potential escorts together; the squad remains in its current phase.
        latch.Observe([], "t1", "p3", [[3, 4, 6], [0, 1, 2]]);
        Assert.Same(advanced, latch.Augment(4, advanced));
    }

    [Fact]
    public void TheLatchDissolvesWhenTheCoreLeavesItsHands()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], "t1", "p2");
        // Bank/drop/pass/death all read the same way: not a carrier now.
        latch.Observe([], "t1", "p3");
        var squad = new HashSet<string> { "t1", "t1/p3" };
        Assert.Same(squad, latch.Augment(3, squad));
    }

    [Fact]
    public void ARespawnedLifeIsNotTheLatchedCarrier()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], "t1", "p2");
        // Same unit, next life, carrying again: the OLD latch dissolved and
        // the new carry latches at the CURRENT phase.
        var nextLife = new ActorIdentity(0, 3, 1);
        latch.Observe([nextLife], "t1", "p4");
        Assert.Equal("t1/p4", latch.For(3)!.PhaseTag);
    }

    [Fact]
    public void ACatcherLatchesAtItsOwnCurrentPhase()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], "t1", "p2");
        // The pass lands: the old carrier is empty-handed, the catcher
        // carries, and the squad has moved on.
        var catcher = new ActorIdentity(0, 6, 0);
        latch.Observe([catcher], "t1", "p3");
        Assert.Null(latch.For(3));
        Assert.Equal("t1/p3", latch.For(6)!.PhaseTag);
    }

    [Fact]
    public void NoProgramMeansNoLatchAndNullStaysNull()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], null, null);
        Assert.Null(latch.For(3));
        Assert.Null(latch.Augment(3, null));
    }

    [Fact]
    public void ALatchWithinTheActivePhaseAddsNothing()
    {
        var latch = new CustodyLatch();
        latch.Observe([Carrier], "t1", "p2");
        var squad = new HashSet<string> { "t1", "t1/p2" };
        Assert.Same(squad, latch.Augment(3, squad));
    }
}
