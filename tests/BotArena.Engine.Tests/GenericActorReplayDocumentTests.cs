using System.Text.Json;

namespace BotArena.Engine.Tests;

public sealed class GenericActorReplayDocumentTests
{
    [Fact]
    public void Create_ExposesCanonicalCompleteJsonAndItsVerifiedHash()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                });
        using GenericActorMatchSession session = Session(definition);
        session.Run();

        GenericActorReplayDocument fromSession =
            GenericActorReplayDocument.Create(session);
        GenericActorReplayDocument fromChronology =
            GenericActorReplayDocument.Create(session.Chronology);
        using JsonDocument document =
            JsonDocument.Parse(fromSession.CanonicalJson);

        Assert.Equal(fromSession, fromChronology);
        Assert.Equal(
            fromSession.ReplayHash,
            document.RootElement
                .GetProperty("replayHash")
                .GetString());
        Assert.False(
            document.RootElement
                .GetProperty("partial")
                .GetBoolean());
        Assert.True(
            ReplayV3Serializer.VerifyHash(
                fromSession.CanonicalJson,
                out string? failure),
            failure);
    }

    [Fact]
    public void CreatePartialPrefix_PreservesOpeningAndRedactsTerminalFacts()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                });
        using GenericActorMatchSession session = Session(definition);
        session.Run();
        GenericActorReplayDocument complete =
            GenericActorReplayDocument.Create(session);

        string partialJson =
            GenericActorReplayDocument.CreatePartialPrefix(
                complete.CanonicalJson,
                visibleTickCount: 2);
        using JsonDocument completeDocument =
            JsonDocument.Parse(complete.CanonicalJson);
        using JsonDocument partialDocument =
            JsonDocument.Parse(partialJson);
        JsonElement completeRoot = completeDocument.RootElement;
        JsonElement partialRoot = partialDocument.RootElement;

        Assert.Equal(
            completeRoot.GetProperty("header").GetRawText(),
            partialRoot.GetProperty("header").GetRawText());
        Assert.Equal(
            completeRoot.GetProperty("initialFrame").GetRawText(),
            partialRoot.GetProperty("initialFrame").GetRawText());
        Assert.Equal(
            completeRoot.GetProperty("ticks")
                .EnumerateArray()
                .Take(2)
                .Select(tick => tick.GetRawText()),
            partialRoot.GetProperty("ticks")
                .EnumerateArray()
                .Select(tick => tick.GetRawText()));
        Assert.Equal(
            JsonValueKind.Null,
            partialRoot.GetProperty("result").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            partialRoot.GetProperty("replayHash").ValueKind);
        Assert.True(partialRoot.GetProperty("partial").GetBoolean());
    }

    [Fact]
    public void CreatePartialPrefix_ClampsPresentationClockPastTerminalTick()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        using GenericActorMatchSession session = Session(definition);
        session.Run();
        GenericActorReplayDocument complete =
            GenericActorReplayDocument.Create(session);

        string partial =
            GenericActorReplayDocument.CreatePartialPrefix(
                complete.CanonicalJson,
                visibleTickCount: int.MaxValue);
        using JsonDocument completeDocument =
            JsonDocument.Parse(complete.CanonicalJson);
        using JsonDocument partialDocument = JsonDocument.Parse(partial);

        Assert.Equal(
            completeDocument.RootElement
                .GetProperty("ticks")
                .GetArrayLength(),
            partialDocument.RootElement
                .GetProperty("ticks")
                .GetArrayLength());
        Assert.Equal(
            JsonValueKind.Null,
            partialDocument.RootElement
                .GetProperty("result")
                .ValueKind);
    }

    [Fact]
    public void Facade_RejectsIncompleteTamperedAndNegativePrefixInputs()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head");
        using GenericActorMatchSession session = Session(definition);

        Assert.Throws<InvalidOperationException>(
            () => GenericActorReplayDocument.Create(session));
        session.Run();
        GenericActorReplayDocument complete =
            GenericActorReplayDocument.Create(session);
        string tampered = complete.CanonicalJson.Replace(
            "\"seed\":\"9001\"",
            "\"seed\":\"9002\"",
            StringComparison.Ordinal);
        Assert.NotEqual(complete.CanonicalJson, tampered);

        Assert.Throws<InvalidDataException>(
            () => GenericActorReplayDocument.CreatePartialPrefix(
                tampered,
                0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GenericActorReplayDocument.CreatePartialPrefix(
                complete.CanonicalJson,
                -1));
    }

    private static GenericActorMatchSession Session(
        ActorResolvedMatchDefinition definition)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        return new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 9_001);
    }
}
