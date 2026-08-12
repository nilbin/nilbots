using BotArena.Engine;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class ArcRelaySignatureSdkMappingTests
{
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
