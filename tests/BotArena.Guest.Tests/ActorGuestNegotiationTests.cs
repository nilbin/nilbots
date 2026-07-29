using BotArena.Guest;
using BotArena.Sdk;

namespace BotArena.Guest.Tests;

public sealed class ActorGuestNegotiationTests
{
    [Fact]
    public void ProfileAbsentSelectsExactLegacyAckAndReady()
    {
        ActorGuestFrame frame = ActorGuestProtocol.ParseHostFrame(
            GenericGuestTestFixture.LegacyActorHello());
        ActorProtocolHello hello = ActorGuestProtocol.ParseHello(frame);

        ActorGuestContractGeneration generation =
            ActorGuestProtocol.SelectContractGeneration(hello);
        ActorWireHelloAck ack = ActorWireProtocol.DecodeHelloAckContract(
            ActorGuestProtocol.FormatHelloAck(hello, generation));
        ActorWireReady ready = ActorWireProtocol.DecodeReady(
            ActorGuestProtocol.FormatReady(generation));

        Assert.Equal(
            ActorGuestContractGeneration.LegacyActorV1,
            generation);
        Assert.Null(hello.RequiredProfile);
        Assert.Null(ack.SelectedProfile);
        Assert.Null(ready.SelectedProfile);
        Assert.Equal(
            ActorContractVersions.RuntimeContractVersion,
            ready.RuntimeContractVersion);
        Assert.Equal(
            ActorContractVersions.MatchStartSchemaVersion,
            ready.MatchStartSchemaVersion);
        Assert.Equal(
            ActorContractVersions.ObservationSchemaVersion,
            ready.ObservationSchemaVersion);
        Assert.Equal(
            ActorContractVersions.DecisionSchemaVersion,
            ready.DecisionSchemaVersion);
    }

    [Fact]
    public void ExactGenericProfileSelectsExactGenericAckAndReady()
    {
        ActorGuestFrame frame = ActorGuestProtocol.ParseHostFrame(
            GenericGuestTestFixture.GenericHello());
        ActorProtocolHello hello = ActorGuestProtocol.ParseHello(frame);

        ActorGuestContractGeneration generation =
            ActorGuestProtocol.SelectContractGeneration(hello);
        ActorWireHelloAck ack = ActorWireProtocol.DecodeHelloAckContract(
            ActorGuestProtocol.FormatHelloAck(hello, generation));
        ActorWireReady ready = ActorWireProtocol.DecodeReady(
            ActorGuestProtocol.FormatReady(generation));

        Assert.Equal(
            ActorGuestContractGeneration.GenericActorV3,
            generation);
        Assert.Equal(
            ActorContractProfile.GenericV3,
            hello.RequiredProfile);
        Assert.Equal(
            ActorContractProfile.GenericV3,
            ack.SelectedProfile);
        Assert.Equal(
            ActorContractProfile.GenericV3,
            ready.SelectedProfile);
        Assert.Equal(
            GenericActorContractVersions.RuntimeContractVersion,
            ready.RuntimeContractVersion);
        Assert.Equal(
            GenericActorContractVersions.MatchStartSchemaVersion,
            ready.MatchStartSchemaVersion);
        Assert.Equal(
            GenericActorContractVersions.ObservationSchemaVersion,
            ready.ObservationSchemaVersion);
        Assert.Equal(
            GenericActorContractVersions.DecisionSchemaVersion,
            ready.DecisionSchemaVersion);
    }

    [Fact]
    public void FrozenGenericV2ProfileStillSelectsItsExactAckAndReady()
    {
        ActorProtocolHello hello = ActorGuestProtocol.ParseHello(
            ActorGuestProtocol.ParseHostFrame(
                GenericGuestTestFixture.GenericV2Hello()));
        ActorGuestContractGeneration generation =
            ActorGuestProtocol.SelectContractGeneration(hello);
        ActorWireHelloAck ack = ActorWireProtocol.DecodeHelloAckContract(
            ActorGuestProtocol.FormatHelloAck(hello, generation));
        ActorWireReady ready = ActorWireProtocol.DecodeReady(
            ActorGuestProtocol.FormatReady(generation));

        Assert.Equal(
            ActorGuestContractGeneration.GenericActorV2,
            generation);
        Assert.Equal(ActorContractProfile.GenericV2, ack.SelectedProfile);
        Assert.Equal(ActorContractProfile.GenericV2, ready.SelectedProfile);
    }

    [Fact]
    public void UnknownOrMismatchedProfileIsNotDowngraded()
    {
        ActorContractProfile[] unsupported =
        [
            ActorContractProfile.GenericV2 with
            {
                ProfileId = "future-profile",
            },
            ActorContractProfile.GenericV2 with
            {
                DecisionSchemaVersion =
                    ActorContractProfile.GenericV2.DecisionSchemaVersion + 1,
            },
            ActorContractProfile.GenericV2 with
            {
                MatchContractSchemaVersion =
                    ActorContractProfile.GenericV2
                        .MatchContractSchemaVersion + 1,
            },
        ];

        foreach (ActorContractProfile profile in unsupported)
        {
            ActorProtocolHello hello = ActorGuestProtocol.ParseHello(
                ActorGuestProtocol.ParseHostFrame(
                    ActorWireProtocol.EncodeHello(
                        ActorWireProtocol.MajorVersion,
                        ActorWireProtocol.MajorVersion,
                        profile)));

            ActorCapabilityNotSupportedException error =
                Assert.Throws<ActorCapabilityNotSupportedException>(
                    () => ActorGuestProtocol.SelectContractGeneration(
                        hello));
            Assert.Equal("actor-contract-profile", error.Capability);
        }
    }

    [Fact]
    public void MissingFactoryReturnsTypedCapabilityWithoutFallback()
    {
        var genericOnly = new ActorGuestDispatcher(
            actorFactory: null,
            genericActorFactory: _ => new RecordingGenericBot());
        ActorCapabilityNotSupportedException legacyError =
            Assert.Throws<ActorCapabilityNotSupportedException>(
                () => genericOnly.Handle(
                    GenericGuestTestFixture.LegacyActorHello()));

        var legacyOnly = new ActorGuestDispatcher(
            actorFactory: _ => new StubActorBot(),
            genericActorFactory: null);
        ActorCapabilityNotSupportedException genericError =
            Assert.Throws<ActorCapabilityNotSupportedException>(
                () => legacyOnly.Handle(
                    GenericGuestTestFixture.GenericHello()));

        Assert.Equal("actor-runtime", legacyError.Capability);
        Assert.Equal(
            "actor-contract-profile",
            genericError.Capability);
        Assert.True(genericOnly.HelloReceived);
        Assert.False(genericOnly.Negotiated);
        Assert.True(legacyOnly.HelloReceived);
        Assert.False(legacyOnly.Negotiated);
    }

    [Fact]
    public void FailedHelloStillConsumesTheOnlyNegotiationAttempt()
    {
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: _ => new StubActorBot(),
            genericActorFactory: null);

        Assert.Throws<ActorCapabilityNotSupportedException>(
            () => dispatcher.Handle(
                GenericGuestTestFixture.GenericHello()));
        Assert.Throws<FormatException>(
            () => dispatcher.Handle(
                GenericGuestTestFixture.LegacyActorHello()));
    }

    [Fact]
    public void SuccessfulHelloCannotSwitchContractFamily()
    {
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: _ => new StubActorBot(),
            genericActorFactory: _ => new RecordingGenericBot());

        byte[] ack = Assert.IsType<byte[]>(
            dispatcher.Handle(GenericGuestTestFixture.GenericHello()));
        Assert.Equal(
            ActorContractProfile.GenericV3,
            ActorWireProtocol.DecodeHelloAckContract(ack).SelectedProfile);
        Assert.Throws<FormatException>(
            () => dispatcher.Handle(
                GenericGuestTestFixture.LegacyActorHello()));
    }

    [Fact]
    public void NegotiatedLegacyFamilyDoesNotSniffGenericMatchStart()
    {
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: _ => new StubActorBot(),
            genericActorFactory: _ => new RecordingGenericBot());
        _ = dispatcher.Handle(
            GenericGuestTestFixture.LegacyActorHello());
        byte[] genericMatchStart =
            ActorWireProtocol.EncodeGenericMatchStart(
                "generic-bot",
                GenericGuestTestFixture.Start());

        Assert.Throws<FormatException>(
            () => dispatcher.Handle(genericMatchStart));
        Assert.True(dispatcher.Negotiated);
        Assert.False(dispatcher.HasSession);
        Assert.Throws<FormatException>(
            () => dispatcher.Handle(genericMatchStart));
    }

    [Fact]
    public void NegotiatedGenericFamilyDoesNotSniffLegacyObservation()
    {
        var bot = new RecordingGenericBot();
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: _ => new StubActorBot(),
            genericActorFactory: _ => bot);
        GenericActorMatchStart start = GenericGuestTestFixture.Start();
        _ = dispatcher.Handle(GenericGuestTestFixture.GenericHello());
        _ = dispatcher.Handle(
            ActorWireProtocol.EncodeGenericMatchStart(
                "generic-bot",
                start));

        Assert.Throws<FormatException>(
            () => dispatcher.Handle(
                ActorWireProtocol.EncodeObservation(
                    LegacyObservation(start))));
        Assert.Empty(bot.ObservedContexts);
        Assert.Throws<FormatException>(
            () => dispatcher.Handle(
                ActorWireProtocol.EncodeGenericObservation(
                    GenericGuestTestFixture.Context(start))));
    }

    private static ActorContext LegacyObservation(
        GenericActorMatchStart start) =>
        new()
        {
            SchemaVersion =
                ActorContractVersions.ObservationSchemaVersion,
            Tick = 0,
            MatchContractFingerprint =
                start.Contract.MatchContractFingerprint,
            TeamPerception = TeamPerceptionMode.ImmediateUnion,
            Self = new ObservedSelf(
                start.ActorId,
                "mobile",
                new Position(2, 2),
                Direction.North,
                Health: 4,
                Cooldown: 0,
                Energy: null,
                ActionResult.None),
            TeamUnits = [],
            Allies = [],
            Enemies = [],
            VisibleTiles = [],
            VisibleProjectiles = null,
            VisibleEvents = [],
            HeardSounds = null,
            FrontlineObjective = null,
            Actions = [],
        };

    private sealed class StubActorBot : IActorBot
    {
        public ActorDecision Tick(ActorContext context) => Actions.Wait();
    }
}
