using BotArena.Sdk;

public sealed class TacticalIngressGovernorTests
{
    [Fact]
    public void CompletedIngressSurvivesPreemptionWithinTheSameOrderEpisode()
    {
        var ingress = new TacticalIngressGovernor();
        var actor = new ActorIdentity(0, 3, 1);

        Assert.True(ingress.NeedsTraversal(
            actor, "raider > pressure-far-well", 12, "north-flank"));
        ingress.Complete(
            actor, "raider > pressure-far-well", 12, "north-flank");

        // A fight can win for any number of intervening ticks. The move leaf
        // resumes with the same activation identity and must not run via again.
        Assert.False(ingress.NeedsTraversal(
            actor, "raider > pressure-far-well", 12, "north-flank"));
    }

    [Fact]
    public void ExitDeathAndImpossibleRouteStartANewIngressEpisode()
    {
        var ingress = new TacticalIngressGovernor();
        var firstLife = new ActorIdentity(0, 3, 1);
        ingress.Complete(
            firstLife, "raider > pressure-far-well", 12, "north-flank");

        Assert.True(ingress.NeedsTraversal(
            firstLife, "raider > pressure-far-well", 41, "north-flank"));
        ingress.Complete(
            firstLife, "raider > pressure-far-well", 41, "north-flank");
        Assert.True(ingress.NeedsTraversal(
            new ActorIdentity(0, 3, 2),
            "raider > pressure-far-well", 41, "north-flank"));

        ingress.Complete(
            firstLife, "raider > pressure-far-well", 41, "north-flank");
        ingress.Reset(firstLife.UnitId);
        Assert.True(ingress.NeedsTraversal(
            firstLife, "raider > pressure-far-well", 41, "north-flank"));
    }
}
