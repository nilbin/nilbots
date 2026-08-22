using System.Collections.Immutable;
using BotArena.Engine;

namespace BotArena.Engine.Tests;

/// <summary>
/// Deterministic proof that Workshop state and lifecycle facts survive the
/// canonical replay-v3 byte round trip, and that any tick's post-state can be
/// read back (seek) without replaying events.
/// </summary>
public sealed class ArcRelayWorkshopReplayV3Tests
{
    private static readonly string[] Squad =
    [
        "kestrel", "relay", "longshot", "mortar",
        "sunder", "patchbay", "mason", "palisade",
    ];

    [Fact]
    public void WorkshopStateAndFactsSurviveTheCanonicalRoundTrip()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Squad,
            Squad,
            loopProfile: ArcRelayLoopProfile.Broadloom1);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories =
            GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_822UL);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        string json = ReplayV3Serializer.ToJson(replay);
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(json);

        Assert.Equal(
            ReplayV3Serializer.ComputeHash(replay),
            ReplayV3Serializer.ComputeHash(reread));

        // Seek: an arbitrary post-unlock tick's post-state names both public
        // Workshops without replaying any event stream.
        ReplayV3.TickFrame frame = reread.Ticks.Single(value => value.Tick == 30);
        var mode = Assert.IsType<ReplayV3.ModeState.ArcRelay>(
            frame.PostState.Mode);
        Assert.Equal(
            ["north", "south"],
            mode.Workshops.Select(value => value.WorkshopId).Order());
        Assert.All(mode.Workshops, workshop =>
        {
            Assert.Equal("ready", workshop.Phase);
            Assert.Null(workshop.TrainingTeamId);
            Assert.Equal(0, workshop.ProgressTicks);
            Assert.Equal(15, workshop.RequiredTicks);
        });
        ReplayV3.ArcWorkshop north = mode.Workshops
            .Single(value => value.WorkshopId == "north");
        Assert.Equal(18, north.TrainingTile.X);
        Assert.Equal(4, north.TrainingTile.Y);

        // The unlock beat itself round-trips as the two public ready facts.
        ReplayV3.ArcRelayFact.WorkshopReady[] readies = [.. reread.Ticks
            .SelectMany(value => value.TickStart.Events.Concat(value.Events))
            .Select(value => value.Payload)
            .OfType<ReplayV3.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .OfType<ReplayV3.ArcRelayFact.WorkshopReady>()];
        Assert.Equal(
            ["north", "south"],
            readies.Select(value => value.WorkshopId).Order());

        // Before the unlock the same seek shows the dormant countdown, so a
        // scrubbed viewer reads the truth at every position.
        ReplayV3.TickFrame locked = reread.Ticks.Single(value => value.Tick == 10);
        var lockedMode = Assert.IsType<ReplayV3.ModeState.ArcRelay>(
            locked.PostState.Mode);
        Assert.All(lockedMode.Workshops, workshop =>
        {
            Assert.Equal("dormant", workshop.Phase);
            Assert.Equal(25, workshop.ReadyAtTick);
        });
    }

    [Fact]
    public void EveryWorkshopFactKindSurvivesTheCanonicalRoundTrip()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Squad,
            Squad,
            loopProfile: ArcRelayLoopProfile.Broadloom1);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories =
            GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_823UL);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        // Splice one synthetic fact of each remaining lifecycle kind onto the
        // final frame so the serializer's writer, parser and validator cover
        // the complete vocabulary even in an untrained match.
        ReplayV3.TickFrame last = replay.Ticks[^1];
        var trainingTile = new ReplayV3.PositionValue(18, 4);
        ReplayV3.ArcRelayFact[] spliced =
        [
            new ReplayV3.ArcRelayFact.WorkshopTrainingStarted(
                "north", 0, trainingTile),
            new ReplayV3.ArcRelayFact.WorkshopTrainingInterrupted(
                "north", 0, "contested", 7, trainingTile),
            new ReplayV3.ArcRelayFact.WorkshopTrainingCompleted(
                "north",
                0,
                new ReplayV3.ActorId(0, 2, 0),
                1,
                86,
                trainingTile),
        ];
        int ordinal = last.TickStart.Events.Length + last.Events.Length;
        long globalOrdinal = replay.Ticks
            .SelectMany(value => value.TickStart.Events.Concat(value.Events))
            .Select(value => long.Parse(
                value.GlobalOrdinal,
                System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(-1)
            .Max();
        ReplayV3.TickFrame splicedFrame = last with
        {
            Events =
            [
                .. last.Events,
                .. spliced.Select((fact, index) =>
                    new ReplayV3.AuthoritativeEvent(
                        $"workshop-splice-{index}",
                        last.Tick,
                        (globalOrdinal + 1 + index).ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        ordinal + index,
                        "arc-relay",
                        new ReplayV3.EventPayload.ArcRelay(fact),
                        new ReplayV3.EventAudience.Public())),
            ],
        };
        ReplayV3 splicedReplay = replay with
        {
            Ticks = [.. replay.Ticks[..^1], splicedFrame],
        };

        string json = ReplayV3Serializer.ToJson(splicedReplay);
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(json);

        ReplayV3.ArcRelayFact[] facts = [.. reread.Ticks[^1].Events
            .Select(value => value.Payload)
            .OfType<ReplayV3.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .Where(value => value is
                ReplayV3.ArcRelayFact.WorkshopTrainingStarted or
                ReplayV3.ArcRelayFact.WorkshopTrainingInterrupted or
                ReplayV3.ArcRelayFact.WorkshopTrainingCompleted)];
        Assert.Equal(3, facts.Length);
        var interrupted = Assert.IsType<
            ReplayV3.ArcRelayFact.WorkshopTrainingInterrupted>(facts[1]);
        Assert.Equal("contested", interrupted.Reason);
        Assert.Equal(7, interrupted.ProgressLost);
        var completed = Assert.IsType<
            ReplayV3.ArcRelayFact.WorkshopTrainingCompleted>(facts[2]);
        Assert.Equal(1, completed.XpAward);
        Assert.Equal(86, completed.ReadyAgainTick);
    }
}
