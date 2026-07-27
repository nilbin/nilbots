namespace BotArena.Engine.Tests;

public sealed class ActorMatchCapabilityVersionsTests
{
    [Fact]
    public void CurrentCapturesEveryIndependentActorHostAxis()
    {
        ActorMatchCapabilityVersions current =
            ActorMatchCapabilityVersions.Current;

        Assert.Equal(
            BotArenaVersions.ActorRuntimeProtocolVersion,
            current.RuntimeProtocolVersion);
        Assert.Equal(
            BotArenaVersions.ActorRuntimeConfigurationVersion,
            current.RuntimeConfigurationVersion);
        Assert.Equal(
            BotArenaVersions.ActorRuntimeContractVersion,
            current.RuntimeContractVersion);
        Assert.Equal(
            BotArenaVersions.ActorMatchStartSchemaVersion,
            current.MatchStartSchemaVersion);
        Assert.Equal(
            BotArenaVersions.ActorObservationSchemaVersion,
            current.ObservationSchemaVersion);
        Assert.Equal(
            BotArenaVersions.ActorDecisionSchemaVersion,
            current.DecisionSchemaVersion);
    }

    [Fact]
    public void RejectsMissingOrNonPositiveCapabilityVersions()
    {
        Assert.Throws<ArgumentException>(() => Create(protocol: " "));
        Assert.Throws<ArgumentException>(() => Create(configuration: ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(runtimeContract: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(matchStart: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(observation: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(decision: 0));
    }

    private static ActorMatchCapabilityVersions Create(
        string protocol = "2.0",
        string configuration = "balanced-1",
        int runtimeContract = 2,
        int matchStart = 3,
        int observation = 4,
        int decision = 5) =>
        new(
            protocol,
            configuration,
            runtimeContract,
            matchStart,
            observation,
            decision);
}
