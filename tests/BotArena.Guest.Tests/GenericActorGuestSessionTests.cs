using System.Buffers.Binary;
using System.Text;
using System.Reflection;
using BotArena.Guest;
using BotArena.Sdk;

namespace BotArena.Guest.Tests;

public sealed class GenericActorGuestSessionTests
{
    [Fact]
    public void DispatcherRunsGenericMatchStartObservationAndEndLifecycle()
    {
        var bot = new RecordingGenericBot();
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: null,
            genericActorFactory: name =>
            {
                Assert.Equal("selected-bot", name);
                return bot;
            });
        GenericActorMatchStart start = GenericGuestTestFixture.Start();

        byte[] ack = Assert.IsType<byte[]>(
            dispatcher.Handle(GenericGuestTestFixture.GenericHello()));
        byte[] ready = Assert.IsType<byte[]>(
            dispatcher.Handle(
                ActorWireProtocol.EncodeGenericMatchStart(
                    "selected-bot",
                    start)));
        byte[] decision0 = Assert.IsType<byte[]>(
            dispatcher.Handle(
                ActorWireProtocol.EncodeGenericObservation(
                    GenericGuestTestFixture.Context(start, tick: 0))));
        byte[] decision1 = Assert.IsType<byte[]>(
            dispatcher.Handle(
                ActorWireProtocol.EncodeGenericObservation(
                    GenericGuestTestFixture.Context(start, tick: 1))));
        byte[]? end = dispatcher.Handle(
            ActorWireProtocol.EncodeMatchEnd("life-ended"));

        Assert.Equal(
            ActorContractProfile.GenericV2,
            ActorWireProtocol.DecodeHelloAckContract(ack).SelectedProfile);
        ActorWireReady decodedReady =
            ActorWireProtocol.DecodeReady(ready);
        Assert.Equal(
            ActorContractProfile.GenericV2,
            decodedReady.SelectedProfile);
        Assert.Equal(
            GenericActorContractVersions.RuntimeContractVersion,
            decodedReady.RuntimeContractVersion);
        Assert.Equal(
            "wait",
            ActorWireProtocol.DecodeGenericDecision(
                decision0).ActionId);
        Assert.Equal(
            "wait",
            ActorWireProtocol.DecodeGenericDecision(
                decision1).ActionId);
        Assert.Null(end);
        Assert.True(dispatcher.Negotiated);
        Assert.True(dispatcher.HasSession);
        Assert.Single(bot.LifeStarts);
        GenericActorMatchStart deliveredStart = bot.LifeStarts[0];
        Assert.Equal(start.SchemaVersion, deliveredStart.SchemaVersion);
        Assert.Equal(
            start.RuntimeContractVersion,
            deliveredStart.RuntimeContractVersion);
        Assert.Equal(start.ActorId, deliveredStart.ActorId);
        Assert.Equal(start.ParticipantId, deliveredStart.ParticipantId);
        Assert.Equal(
            start.ActorRandomSeed,
            deliveredStart.ActorRandomSeed);
        Assert.Equal(start.Origin, deliveredStart.Origin);
        Assert.Equal(
            start.Contract.CanonicalJson,
            deliveredStart.Contract.CanonicalJson);
        Assert.Equal([0, 1],
            bot.ObservedContexts.Select(context => context.Tick));
        Assert.All(
            bot.ObservedContexts,
            context =>
            {
                Assert.NotNull(context.Random);
                Assert.NotNull(context.Debug);
            });
        Assert.Throws<FormatException>(
            () => dispatcher.Handle(
                ActorWireProtocol.EncodeMatchEnd("again")));
    }

    [Fact]
    public void MalformedMatchEndFaultsAndPermanentlyEndsDispatcher()
    {
        var dispatcher = new ActorGuestDispatcher(
            actorFactory: null,
            genericActorFactory: _ => new RecordingGenericBot());
        GenericActorMatchStart start = GenericGuestTestFixture.Start();
        _ = dispatcher.Handle(GenericGuestTestFixture.GenericHello());
        _ = dispatcher.Handle(
            ActorWireProtocol.EncodeGenericMatchStart("bot", start));

        byte[] malformed =
            ActorWireProtocol.EncodeMatchEnd("life-ended")
                [..ActorWireProtocol.HeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(
            malformed.AsSpan(8, 4),
            0);

        Assert.Throws<FormatException>(
            () => dispatcher.Handle(malformed));
        Assert.Throws<FormatException>(
            () => dispatcher.Handle(
                ActorWireProtocol.EncodeMatchEnd("retry")));
    }

    [Fact]
    public void SessionRejectsIdentityGenerationFingerprintAndTickChanges()
    {
        GenericActorMatchStart start = GenericGuestTestFixture.Start();

        (GenericActorGuestSession identitySession, RecordingGenericBot
                identityBot) =
            NewSession(start);
        Assert.Throws<FormatException>(
            () => identitySession.HandleTick(
                GenericGuestTestFixture.Context(
                    start,
                    actorId: new ActorIdentity(
                        start.ActorId.TeamId,
                        start.ActorId.UnitId,
                        start.ActorId.LifeId + 1))));
        Assert.Empty(identityBot.ObservedContexts);

        (GenericActorGuestSession generationSession, RecordingGenericBot
                generationBot) =
            NewSession(start);
        Assert.Throws<FormatException>(
            () => generationSession.HandleTick(
                GenericGuestTestFixture.Context(
                    start,
                    generation: start.Origin.Generation + 1)));
        Assert.Empty(generationBot.ObservedContexts);

        (GenericActorGuestSession fingerprintSession, RecordingGenericBot
                fingerprintBot) =
            NewSession(start);
        Assert.Throws<FormatException>(
            () => fingerprintSession.HandleTick(
                GenericGuestTestFixture.Context(
                    start,
                    fingerprint: new string('f', 64))));
        Assert.Empty(fingerprintBot.ObservedContexts);

        (GenericActorGuestSession tickSession, RecordingGenericBot tickBot) =
            NewSession(start);
        _ = tickSession.HandleTick(
            GenericGuestTestFixture.Context(start, tick: 3));
        Assert.Throws<FormatException>(
            () => tickSession.HandleTick(
                GenericGuestTestFixture.Context(start, tick: 3)));
        Assert.Single(tickBot.ObservedContexts);
    }

    [Fact]
    public void InvalidMatchStartFailsBeforeFactoryOrStartLife()
    {
        GenericActorMatchStart start =
            GenericGuestTestFixture.Start() with
            {
                SchemaVersion =
                    GenericActorContractVersions.MatchStartSchemaVersion + 1,
            };
        bool factoryCalled = false;

        Assert.Throws<FormatException>(
            () => GenericActorGuestSession.Start(
                new GenericActorMatchStartEnvelope("bot", start),
                _ =>
                {
                    factoryCalled = true;
                    return new RecordingGenericBot();
                }));
        Assert.False(factoryCalled);
    }

    [Fact]
    public void CombinedDebugIsBoundedToFourKiBWithoutSplittingUtf8()
    {
        GenericActorMatchStart start = GenericGuestTestFixture.Start();
        var bot = new RecordingGenericBot
        {
            ReturnedDebug = new string('a', 4093),
            CollectedDebug = "é",
        };
        GenericActorGuestSession session = GenericActorGuestSession.Start(
            new GenericActorMatchStartEnvelope("bot", start),
            _ => bot);

        GenericActorDecision exact = session.HandleTick(
            GenericGuestTestFixture.Context(start, tick: 0));
        Assert.NotNull(exact.DebugMessage);
        Assert.Equal(
            4096,
            Encoding.UTF8.GetByteCount(exact.DebugMessage));
        Assert.EndsWith("é", exact.DebugMessage, StringComparison.Ordinal);

        bot.ReturnedDebug = new string('a', 4094);
        GenericActorDecision truncated = session.HandleTick(
            GenericGuestTestFixture.Context(start, tick: 1));
        Assert.NotNull(truncated.DebugMessage);
        Assert.True(
            Encoding.UTF8.GetByteCount(truncated.DebugMessage) <= 4096);
        Assert.DoesNotContain(
            "\uFFFD",
            truncated.DebugMessage,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "\n",
            truncated.DebugMessage,
            StringComparison.Ordinal);

        GenericActorDecision wireDecoded =
            ActorWireProtocol.DecodeGenericDecision(
                ActorWireProtocol.EncodeGenericDecision(truncated));
        Assert.Equal(
            truncated.DebugMessage,
            wireDecoded.DebugMessage);
    }

    [Fact]
    public void ProtocolFaultBoundingNormalizesInvalidUtf16()
    {
        MethodInfo method = typeof(GuestHost).GetMethod(
            "BoundProtocolFault",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "GuestHost fault-boundary helper was not found.");

        string bounded = Assert.IsType<string>(
            method.Invoke(
                obj: null,
                parameters:
                [
                    "invalid-\uD800-" + new string('é', 4096),
                ]));
        var strictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        byte[] bytes = strictUtf8.GetBytes(bounded);
        Assert.True(bytes.Length <= 4096);
        Assert.Contains(
            "\uFFFD",
            bounded,
            StringComparison.Ordinal);
    }

    private static (
        GenericActorGuestSession Session,
        RecordingGenericBot Bot)
        NewSession(GenericActorMatchStart start)
    {
        var bot = new RecordingGenericBot();
        GenericActorGuestSession session = GenericActorGuestSession.Start(
            new GenericActorMatchStartEnvelope("bot", start),
            _ => bot);
        return (session, bot);
    }
}

internal sealed class RecordingGenericBot : IGenericActorBot
{
    public List<GenericActorMatchStart> LifeStarts { get; } = [];
    public List<GenericActorContext> ObservedContexts { get; } = [];
    public string? ReturnedDebug { get; set; }
    public string? CollectedDebug { get; set; }

    public void StartLife(GenericActorMatchStart start) =>
        LifeStarts.Add(start);

    public GenericActorDecision Tick(GenericActorContext context)
    {
        ObservedContexts.Add(context);
        if (CollectedDebug is not null)
            context.Debug.Write(CollectedDebug);
        return GenericActorDecision.WithoutArguments(
            "wait",
            0,
            ReturnedDebug);
    }
}
