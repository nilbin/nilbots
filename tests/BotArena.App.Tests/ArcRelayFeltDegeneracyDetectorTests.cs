using System.Text.Json;
using BotArena.App.ArcRelay;

namespace BotArena.App.Tests;

public sealed class ArcRelayFeltDegeneracyDetectorTests
{
    [Fact]
    public void Three_same_core_actor_tile_pickup_drop_cycles_trip_the_hosted_bar()
    {
        ArcRelayDegeneracyRead read = ArcRelayFeltDegeneracyDetector.Analyze(
            Broadcast(cycles: 3));

        Assert.True(read.Tripped(0));
        Assert.Contains("pickup-drop cycle", read.ReasonsByTeam[0]);
        Assert.False(read.Tripped(1));
    }

    [Fact]
    public void Two_pickup_drop_cycles_remain_below_the_hosted_bar()
    {
        ArcRelayDegeneracyRead read = ArcRelayFeltDegeneracyDetector.Analyze(
            Broadcast(cycles: 2));

        Assert.False(read.Tripped(0));
        Assert.False(read.Tripped(1));
    }

    private static byte[] Broadcast(int cycles)
    {
        object[] startEvents = Enumerable.Range(0, cycles)
            .Select(_ => (object)new[] { PickupEvent() })
            .ToArray();
        object[] events = Enumerable.Range(0, cycles)
            .Select(_ => (object)new[] { DropEvent() })
            .ToArray();
        object[] worlds = Enumerable.Range(0, cycles)
            .Select(_ => (object)World(initial: false))
            .ToArray();
        object[] turns = Enumerable.Range(0, cycles)
            .Select(_ => (object)Array.Empty<object>())
            .ToArray();
        var document = new
        {
            header = new
            {
                contract = new
                {
                    map = new
                    {
                        tileRows = new[] { ".....", ".....", "....." },
                    },
                },
            },
            initial = World(initial: true),
            worlds,
            turns,
            startEvents,
            events,
        };
        return JsonSerializer.SerializeToUtf8Bytes(document);
    }

    private static object?[] World(bool initial)
    {
        object?[] world = new object?[8];
        world[4] = Array.Empty<object>();
        world[7] = new
        {
            kind = "arc-relay",
            wells = initial
                ? new[] { new { nextScheduledBirthTick = 25 } }
                : null,
            reactors = initial
                ? new[]
                {
                    new { teamId = 0, position = new { x = 1, y = 1 } },
                    new { teamId = 1, position = new { x = 3, y = 1 } },
                }
                : null,
            visibleCores = Array.Empty<object>(),
        };
        return world;
    }

    private static object PickupEvent() => new
    {
        kind = "arc-relay",
        payload = new
        {
            fact = new
            {
                kind = "core-picked-up",
                coreId = new { sourceWellId = "south", sourceOrdinal = 3 },
                carrierActorId = new { teamId = 0, unitId = 7, lifeId = 5 },
                position = new { x = 8, y = 11 },
            },
        },
    };

    private static object DropEvent() => new
    {
        kind = "arc-relay",
        payload = new
        {
            fact = new
            {
                kind = "core-dropped",
                coreId = new { sourceWellId = "south", sourceOrdinal = 3 },
                sourceActorId = new { teamId = 0, unitId = 7, lifeId = 5 },
                position = new { x = 8, y = 11 },
                dropKind = "voluntary",
            },
        },
    };
}
