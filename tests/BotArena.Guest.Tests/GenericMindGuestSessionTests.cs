using BotArena.Sdk;
using Position = BotArena.Sdk.Position;

namespace BotArena.Guest.Tests;

public sealed class GenericMindGuestSessionTests
{
    [Fact]
    public void AMindArtifactNegotiatesTheProfileAndAttestsItsOwnSchemas()
    {
        var mind = new RecordingMind();
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: null,
            genericActorFactory: null,
            _ => mind);

        byte[] helloAck = dispatcher.Handle(
            GenericMindGuestTestFixture.MindHello())!;
        Assert.Equal(
            ActorContractProfile.MindV1,
            ActorWireProtocol.DecodeHelloAckContract(helloAck).SelectedProfile);

        byte[] ready = dispatcher.Handle(
            ActorWireProtocol.EncodeMindStart(
                "mind",
                GenericMindGuestTestFixture.Start()))!;
        ActorWireReady attested = ActorWireProtocol.DecodeReady(ready);

        // The tuple is attested from the constants COMPILED INTO the artifact.
        Assert.Equal(ActorContractProfile.MindV1, attested.SelectedProfile);
        Assert.Equal(
            GenericMindContractVersions.RuntimeContractVersion,
            attested.RuntimeContractVersion);
        Assert.Equal(
            GenericMindContractVersions.MatchStartSchemaVersion,
            attested.MatchStartSchemaVersion);
        Assert.Equal(
            GenericMindContractVersions.ObservationSchemaVersion,
            attested.ObservationSchemaVersion);
        Assert.Equal(
            GenericMindContractVersions.DecisionSchemaVersion,
            attested.DecisionSchemaVersion);
        Assert.Equal(1, mind.StartCalls);
    }

    [Fact]
    public void ThinkRunsOncePerTickIncludingTicksWithNoBodies()
    {
        var mind = new RecordingMind();
        MindStart start = GenericMindGuestTestFixture.Start();
        ActorGuestDispatcher dispatcher = Negotiated(mind, start);

        for (int tick = 0; tick < 4; tick++)
        {
            MindBody[] bodies = tick == 2
                ? []
                : [GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2))];
            byte[] reply = dispatcher.Handle(
                ActorWireProtocol.EncodeMindObservation(
                    GenericMindGuestTestFixture.Context(
                        start,
                        tick,
                        bodies)))!;
            MindDecisions decisions =
                ActorWireProtocol.DecodeMindDecisions(reply);
            Assert.Equal(tick, decisions.Tick);
        }

        // Four ticks, four calls, including the one where the mind owned
        // nothing. "Am I alive?" is a data question, not a control-flow one.
        Assert.Equal(4, mind.ThinkCalls);
        Assert.Equal([1, 1, 0, 1], mind.BodyCounts);
    }

    [Fact]
    public void CommandsWrittenOntoBodiesAreHarvestedIntoTheReply()
    {
        var mind = new RecordingMind
        {
            Act = context =>
            {
                MindBody first = context.Bodies[0];
                first.SetRole("channeler");
                first.Hold("claim");
                context.Bodies[1].Command(
                    "move",
                    1,
                    new GenericActorActionArgument.DirectionArgument(
                        Direction.North));
                context.Debug.Write("one plan");
            },
        };
        MindStart start = GenericMindGuestTestFixture.Start();
        ActorGuestDispatcher dispatcher = Negotiated(mind, start);

        byte[] reply = dispatcher.Handle(
            ActorWireProtocol.EncodeMindObservation(
                GenericMindGuestTestFixture.Context(
                    start,
                    0,
                    GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2)),
                    GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2)))))!;
        MindDecisions decisions = ActorWireProtocol.DecodeMindDecisions(reply);

        Assert.Equal(2, decisions.Commands.Length);
        Assert.Equal("wait", decisions.Commands[0].ActionId);
        Assert.Equal("channeler", decisions.Commands[0].RoleTag);
        Assert.Equal("claim", decisions.Commands[0].DebugMessage);
        Assert.Equal("move", decisions.Commands[1].ActionId);
        Assert.Null(decisions.Commands[1].RoleTag);
        Assert.Equal("one plan", decisions.DebugMessage);
    }

    [Fact]
    public void AForgottenBodyProducesNoCommandAndTheHostsWaitStands()
    {
        var mind = new RecordingMind
        {
            Act = context => context.Bodies[0].Hold(),
        };
        MindStart start = GenericMindGuestTestFixture.Start();
        ActorGuestDispatcher dispatcher = Negotiated(mind, start);

        byte[] reply = dispatcher.Handle(
            ActorWireProtocol.EncodeMindObservation(
                GenericMindGuestTestFixture.Context(
                    start,
                    0,
                    GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2)),
                    GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2)))))!;
        MindDecisions decisions = ActorWireProtocol.DecodeMindDecisions(reply);

        // The map is SHORTER than the live-body set, and that is legal: the
        // untouched body keeps the host's pre-filled wait. Forgetting a body
        // costs that body a tick, visibly, and nothing else.
        MindCommand only = Assert.Single(decisions.Commands);
        Assert.Equal(0, only.UnitId);
    }

    [Fact]
    public void EndMatchRunsAfterTheTerminalTick()
    {
        var mind = new RecordingMind();
        MindStart start = GenericMindGuestTestFixture.Start();
        ActorGuestDispatcher dispatcher = Negotiated(mind, start);
        dispatcher.Handle(
            ActorWireProtocol.EncodeMindObservation(
                GenericMindGuestTestFixture.Context(start, 0)));

        Assert.Null(
            dispatcher.Handle(
                ActorWireProtocol.EncodeMatchEnd("match-ended")));

        Assert.Equal(1, mind.EndCalls);
        Assert.Equal("match-ended", mind.EndReason);
    }

    [Fact]
    public void AStaleOrRepeatedTickIsRefused()
    {
        var mind = new RecordingMind();
        MindStart start = GenericMindGuestTestFixture.Start();
        ActorGuestDispatcher dispatcher = Negotiated(mind, start);
        dispatcher.Handle(
            ActorWireProtocol.EncodeMindObservation(
                GenericMindGuestTestFixture.Context(start, 3)));

        Assert.Throws<FormatException>(() => dispatcher.Handle(
            ActorWireProtocol.EncodeMindObservation(
                GenericMindGuestTestFixture.Context(start, 3))));
    }

    [Fact]
    public void AnArtifactWithNoMindAndNoPerLifeBotRefusesTheProfile()
    {
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: null,
            genericActorFactory: null);

        ActorCapabilityNotSupportedException error =
            Assert.Throws<ActorCapabilityNotSupportedException>(
                () => dispatcher.Handle(
                    GenericMindGuestTestFixture.MindHello()));

        Assert.Equal("actor-contract-profile", error.Capability);
        Assert.Contains(
            GenericMindContractVersions.ContractProfileId,
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void APerLifeOnlyArtifactStillNegotiatesTheMindProfile()
    {
        // The migration, in one assertion: an artifact whose only bot is an
        // IGenericActorBot answers a mind Hello, because the dispatcher hands
        // it the wrap facade. Zero source edits, one rebuild.
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: null,
            _ => new GenericMindGuestTestFixture.PrecedenceBot());

        byte[] helloAck = dispatcher.Handle(
            GenericMindGuestTestFixture.MindHello())!;

        Assert.Equal(
            ActorContractProfile.MindV1,
            ActorWireProtocol.DecodeHelloAckContract(helloAck).SelectedProfile);
    }

    [Fact]
    public void StaticTypeAnalysisRoutesEachInterfaceToItsOwnProfile()
    {
        GuestHost.DetectedFactories<GenericMindGuestTestFixture.PrecedenceBot>
            perLife = GuestHost.Detect(
                () => new GenericMindGuestTestFixture.PrecedenceBot());
        GuestHost.DetectedFactories<RecordingMind> nativeMind =
            GuestHost.Detect(() => new RecordingMind());
        GuestHost.DetectedFactories<BothBot> both =
            GuestHost.Detect(() => new BothBot());

        Assert.NotNull(perLife.GenericActor);
        Assert.Null(perLife.Mind);
        Assert.Null(nativeMind.GenericActor);
        Assert.NotNull(nativeMind.Mind);
        // A type may deliberately implement both; the negotiated profile picks
        // the factory, and a hand-written mind is never shadowed by the wrap.
        Assert.NotNull(both.GenericActor);
        Assert.NotNull(both.Mind);

        Assert.Throws<InvalidOperationException>(
            () => GuestHost.Detect(() => new object()));
    }

    private static ActorGuestDispatcher Negotiated(
        IGenericMindBot mind,
        MindStart start)
    {
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: null,
            genericActorFactory: null,
            _ => mind);
        dispatcher.Handle(GenericMindGuestTestFixture.MindHello());
        dispatcher.Handle(
            ActorWireProtocol.EncodeMindStart("mind", start));
        return dispatcher;
    }

    private sealed class RecordingMind : IGenericMindBot
    {
        public int StartCalls { get; private set; }

        public int ThinkCalls { get; private set; }

        public int EndCalls { get; private set; }

        public string? EndReason { get; private set; }

        public List<int> BodyCounts { get; } = [];

        public Action<MindContext>? Act { get; init; }

        public void StartMatch(MindStart start) => StartCalls++;

        public void Think(MindContext mind)
        {
            ThinkCalls++;
            BodyCounts.Add(mind.Bodies.Length);
            Act?.Invoke(mind);
        }

        public void EndMatch(MindEnd end)
        {
            EndCalls++;
            EndReason = end.Reason;
        }
    }

    private sealed class BothBot : IGenericActorBot, IGenericMindBot
    {
        public GenericActorDecision Tick(GenericActorContext context) =>
            GenericActorDecision.WithoutArguments("wait", 0);

        public void Think(MindContext mind)
        {
        }
    }
}
