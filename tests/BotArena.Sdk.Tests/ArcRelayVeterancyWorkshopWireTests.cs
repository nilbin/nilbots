using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

public sealed class ArcRelayVeterancyWorkshopWireTests
{
    [Fact]
    public void WorkshopStatesSurviveTheSharedModeWireRoundTrip()
    {
        var training = new GenericActorContext.ArcRelayVeterancyWorkshopState(
            "north",
            new Position(18, 4),
            GenericActorContext.ArcRelayVeterancyWorkshopPhase.Training,
            TrainingTeamId: 0,
            ProgressTicks: 8,
            RequiredTicks: 15,
            ReadyAtTick: null);
        var dormant = new GenericActorContext.ArcRelayVeterancyWorkshopState(
            "south",
            new Position(18, 22),
            GenericActorContext.ArcRelayVeterancyWorkshopPhase.Dormant,
            TrainingTeamId: null,
            ProgressTicks: 0,
            RequiredTicks: 15,
            ReadyAtTick: 206);
        var mode = new GenericActorContext.ModeObservationState.ArcRelay(
            "arc-relay-h0",
            [],
            [],
            [],
            [],
            latestPulseTeamId: null,
            latestPulseTick: null)
        {
            Workshops = [training, dormant],
        };
        GenericActorContext source =
            GenericActorDynamicTestFixture.Context(mode: mode);

        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(source));

        var arc = Assert.IsType<
            GenericActorContext.ModeObservationState.ArcRelay>(decoded.Mode);
        Assert.Equal(2, arc.Workshops.Length);
        Assert.Equal(training, arc.Workshops[0]);
        Assert.Equal(dormant, arc.Workshops[1]);
    }

    [Fact]
    public void HistoricalContextsWithoutWorkshopsDecodeToTheEmptyDefault()
    {
        var mode = new GenericActorContext.ModeObservationState.ArcRelay(
            "arc-relay-h0",
            [],
            [],
            [],
            [],
            latestPulseTeamId: null,
            latestPulseTick: null);
        GenericActorContext source =
            GenericActorDynamicTestFixture.Context(mode: mode);

        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(source));

        var arc = Assert.IsType<
            GenericActorContext.ModeObservationState.ArcRelay>(decoded.Mode);
        Assert.True(arc.Workshops.IsEmpty);
    }

    [Fact]
    public void WorkshopLifecycleEventsSurviveTheEventWireRoundTrip()
    {
        GenericActorContext.ArcRelayEvent[] facts =
        [
            new GenericActorContext.ArcRelayEvent.WorkshopReady(
                "north", new Position(18, 4)),
            new GenericActorContext.ArcRelayEvent.WorkshopTrainingStarted(
                "north", TeamId: 0, new Position(18, 4)),
            new GenericActorContext.ArcRelayEvent.WorkshopTrainingInterrupted(
                "north",
                TeamId: 0,
                Reason: "contested",
                ProgressLost: 7,
                new Position(18, 4)),
            new GenericActorContext.ArcRelayEvent.WorkshopTrainingCompleted(
                "south",
                TeamId: 1,
                new ActorIdentity(1, 2, 0),
                XpAward: 1,
                ReadyAgainTick: 86,
                new Position(18, 22)),
        ];
        GenericActorContext source = GenericActorDynamicTestFixture.Context(
            visibleEvents:
            [
                .. facts.Select((fact, index) =>
                    new GenericActorContext.ObservedEvent(
                        $"workshop-event-{index}",
                        sourceTick: 8,
                        sourceOrdinal: index,
                        GenericActorContext.EventKind.ArcRelay,
                        new GenericActorContext.EventPayload.ArcRelay(fact),
                        ImmutableArray<ActorIdentity>.Empty)),
            ]);

        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(source));

        Assert.Equal(4, decoded.VisibleEvents.Length);
        GenericActorContext.ArcRelayEvent[] decodedFacts = [.. decoded
            .VisibleEvents
            .Select(value => value.Payload)
            .OfType<GenericActorContext.EventPayload.ArcRelay>()
            .Select(value => value.Fact)];
        Assert.Equal(facts.AsEnumerable(), decodedFacts.AsEnumerable());
    }
}
