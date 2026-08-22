using BotArena.Engine;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class ArcRelaySignatureSdkMappingTests
{
    [Fact]
    public void VeterancyRetentionCrossesTheBotSdkBoundary()
    {
        var source = new ArcRelayEvent.VeterancyRetained(
            new ActorIdentity(1, 4, 7),
            PreviousLevel: 5,
            RetainedLevel: 3,
            RetainedTotalXp: 4,
            RetainedSpentPoints: 2,
            new Position(18, 22));

        var mapped = Assert.IsType<BotArena.Sdk.GenericActorContext
            .ArcRelayEvent.VeterancyRetained>(
                GenericActorSdkModelMapper.ToSdk(source));

        Assert.Equal(new BotArena.Sdk.ActorIdentity(1, 4, 7), mapped.ActorId);
        Assert.Equal(5, mapped.PreviousLevel);
        Assert.Equal(3, mapped.RetainedLevel);
        Assert.Equal(4, mapped.RetainedTotalXp);
        Assert.Equal(2, mapped.RetainedSpentPoints);
        Assert.Equal(new BotArena.Sdk.Position(18, 22), mapped.Position);
    }

    [Fact]
    public void EveryConcreteSignatureVersionCrossesTheBotSdkBoundary()
    {
        foreach (ArcRelaySignatureDefinition.SignatureKind kind in
                 Enum.GetValues<ArcRelaySignatureDefinition.SignatureKind>())
        {
            var state = new ArcRelaySignatureState(
                operationId: "operation",
                signatureId: ArcRelaySignatureDefinition.StableIdFor(kind),
                kind,
                ownerActorId: new ActorIdentity(0, 0, 0),
                ownerTeamId: 0,
                phase: ArcRelaySignatureState.SignaturePhase.Tell,
                startedTick: 1,
                completesAtTick: 2,
                endsAtTick: null,
                positions: [new Position(1, 1)],
                targetActorId: null,
                remainingCapacity: 0,
                suppressed: false);

            BotArena.Sdk.GenericActorContext.ArcRelaySignatureState mapped =
                GenericActorSdkModelMapper.ToSdk(state);

            Assert.Equal(ArcRelaySignatureDefinition.StableIdFor(kind),
                mapped.Kind);
        }
    }
}
