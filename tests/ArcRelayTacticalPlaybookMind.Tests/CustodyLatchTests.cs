using BotArena.Sdk;

public sealed class CustodyLatchTests
{
    private static readonly ActorIdentity Carrier = new(0, 3, 0);
    private static readonly GenericActorContext.ArcRelayCoreId CoreA =
        new("centre", 0);
    private static readonly GenericActorContext.ArcRelayCoreId CoreB =
        new("north", 1);

    [Fact]
    public void ACarrierFinishesItsCarryUnderThePickupPhase()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p2");
        // The squad advances; the carrier's own view keeps p2 lit.
        var squad = new HashSet<string> { "t1", "t1/p3" };
        IReadOnlySet<string>? tags = latch.Augment(3, squad);
        Assert.NotNull(tags);
        Assert.Contains("t1/p2", tags!);
        Assert.DoesNotContain("t1/p3", tags!);
        Assert.Contains("t1", tags!);
        // Everyone else sees exactly the squad set, same instance.
        Assert.Same(squad, latch.Augment(4, squad));
    }

    [Fact]
    public void PickupPhaseCustodyClaimsOnlyTheCarriersOwnGroup()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p2",
            [[3, 4, 6], [0, 1, 2]]);
        var advanced = new HashSet<string> { "t1", "t1/p3" };

        Assert.Contains("t1/p2", latch.Augment(3, advanced)!);
        Assert.Contains("t1/p2", latch.Augment(4, advanced)!);
        Assert.Contains("t1/p2", latch.Augment(6, advanced)!);
        Assert.Same(advanced, latch.Augment(1, advanced));

        // A drop makes the provenance dormant for carrier and escorts while
        // the Core itself remains outstanding.
        Observe(latch, CoreA, null, "t1", "p3",
            [[3, 4, 6], [0, 1, 2]]);
        Assert.Same(advanced, latch.Augment(4, advanced));
    }

    [Fact]
    public void DropAndRepickKeepTheOriginalPickupPhase()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p2");
        Observe(latch, CoreA, null, "t1", "p3");
        var squad = new HashSet<string> { "t1", "t1/p3" };
        Assert.Same(squad, latch.Augment(3, squad));

        var replacement = new ActorIdentity(0, 6, 0);
        Observe(latch, CoreA, replacement, "t1", "p3");
        Assert.Equal("t1/p2", latch.For(6)!.PhaseTag);
    }

    [Fact]
    public void DeathDropAndRespawnedRepickKeepTheCoreProvenance()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p2");
        Observe(latch, CoreA, null, "t1", "p3");
        var nextLife = new ActorIdentity(0, 3, 1);
        Observe(latch, CoreA, nextLife, "t1", "p4");
        Assert.Equal("t1/p2", latch.For(3)!.PhaseTag);
        Assert.Equal(nextLife, latch.For(3)!.Carrier);
    }

    [Fact]
    public void HandoffMovesPossessionWithoutRebindingTheCore()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p2", [[3, 4]]);
        var catcher = new ActorIdentity(0, 6, 0);
        Observe(latch, CoreA, catcher, "t1", "p3", [[3, 4]]);
        Assert.Equal("t1/p2", latch.For(6)!.PhaseTag);
        // The pickup phase's authored escort stays attached after transfer.
        Assert.Equal("t1/p2", latch.For(4)!.PhaseTag);
        // The original carrier rejoins its pickup-phase escort group.
        Assert.Equal("t1/p2", latch.For(3)!.PhaseTag);
    }

    [Fact]
    public void NoProgramMeansNoLatchAndNullStaysNull()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, tactic: null, phase: null);
        Assert.Null(latch.For(3));
        Assert.Null(latch.Augment(3, null));
    }

    [Fact]
    public void ALatchWithinTheActivePhaseStillReturnsAnIsolatedCustodyView()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p2");
        var squad = new HashSet<string> { "t1", "t1/p2" };
        IReadOnlySet<string> tags = latch.Augment(3, squad)!;
        Assert.NotSame(squad, tags);
        Assert.Equal(squad, tags);
    }

    [Fact]
    public void ProvenanceIsTeamLocalAcrossEnemyPossessionAndRecapture()
    {
        var latch = new CustodyLatch(teamId: 0);
        var enemy = new ActorIdentity(1, 3, 0);

        // The enemy's first pickup cannot bind this team's policy.
        Observe(latch, CoreA, enemy, "t1", "p1");
        Assert.Null(latch.For(3));

        Observe(latch, CoreA, Carrier, "t1", "p2");
        Observe(latch, CoreA, enemy, "t1", "p3");
        Assert.Null(latch.For(3));

        var recapturer = new ActorIdentity(0, 5, 0);
        Observe(latch, CoreA, recapturer, "t1", "p4");
        Assert.Equal(0, latch.For(5)!.TeamId);
        Assert.Equal("t1/p2", latch.For(5)!.PhaseTag);
    }

    [Fact]
    public void BankingClearsProvenanceAndANewCoreBindsAfresh()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p1");

        latch.Observe([], [], "t1", "p3");
        Assert.Null(latch.For(3));

        Observe(latch, CoreB, Carrier, "t1", "p3");
        Assert.Equal("t1/p3", latch.For(3)!.PhaseTag);
    }

    [Fact]
    public void ACarrierOwnsItsCoreBeforeAnOverlappingEscortClaim()
    {
        var latch = new CustodyLatch(teamId: 0);
        Observe(latch, CoreA, Carrier, "t1", "p1", [[3, 4]]);
        var secondCarrier = new ActorIdentity(0, 4, 0);
        latch.Observe(
            [Core(CoreA, Carrier), Core(CoreB, secondCarrier)],
            [CoreA, CoreB],
            "t1", "p2", [[3, 4]]);

        Assert.Equal(CoreB, latch.For(4)!.CoreId);
        Assert.Equal("t1/p2", latch.For(4)!.PhaseTag);
    }

    private static void Observe(
        CustodyLatch latch,
        GenericActorContext.ArcRelayCoreId core,
        ActorIdentity? carrier,
        string? tactic,
        string? phase,
        IReadOnlyList<int[]>? groups = null) => latch.Observe(
            [Core(core, carrier)], [core], tactic, phase, groups);

    private static CustodyLatch.CoreObservation Core(
        GenericActorContext.ArcRelayCoreId core,
        ActorIdentity? carrier) => new(core, carrier);
}
