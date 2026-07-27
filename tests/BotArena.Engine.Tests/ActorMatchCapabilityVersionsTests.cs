namespace BotArena.Engine.Tests;

public sealed class ActorMatchCapabilityVersionsTests
{
    [Fact]
    public void CurrentCapturesEveryIndependentActorHostAxis()
    {
        ActorMatchCapabilityVersions current =
            ActorMatchCapabilityVersions.Current;

        Assert.Equal(
            BotArenaVersions.GenericActorContractProfileId,
            current.ContractProfileId);
        Assert.Equal(
            BotArenaVersions.GenericActorRuntimeProtocolVersion,
            current.RuntimeProtocolVersion);
        Assert.Equal(
            BotArenaVersions.GenericActorRuntimeConfigurationVersion,
            current.RuntimeConfigurationVersion);
        Assert.Equal(
            BotArenaVersions.GenericActorRuntimeContractVersion,
            current.RuntimeContractVersion);
        Assert.Equal(
            BotArenaVersions.GenericActorMatchStartSchemaVersion,
            current.MatchStartSchemaVersion);
        Assert.Equal(
            BotArenaVersions.GenericActorObservationSchemaVersion,
            current.ObservationSchemaVersion);
        Assert.Equal(
            BotArenaVersions.GenericActorDecisionSchemaVersion,
            current.DecisionSchemaVersion);
        Assert.Equal(
            BotArenaVersions.GenericActorMatchContractSchemaVersion,
            current.MatchContractSchemaVersion);
    }

    [Fact]
    public void RejectsMissingOrNonPositiveCapabilityVersions()
    {
        Assert.Throws<ArgumentException>(() => Create(profile: " "));
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
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(matchContract: 0));
    }

    private static ActorMatchCapabilityVersions Create(
        string profile = "generic-actor-match-2",
        string protocol = "2.0",
        string configuration = "balanced-1",
        int runtimeContract = 2,
        int matchStart = 3,
        int observation = 4,
        int decision = 5,
        int matchContract = 6) =>
        new(
            profile,
            protocol,
            configuration,
            runtimeContract,
            matchStart,
            observation,
            decision,
            matchContract);
}
