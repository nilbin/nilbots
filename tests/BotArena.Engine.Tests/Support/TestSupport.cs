using BotArena.Engine;

namespace BotArena.Engine.Tests.Support;

/// <summary>Feeds a fixed action sequence to the engine; Wait after the script runs out.</summary>
public sealed class ScriptedRuntime(params BotAction[] script) : IBotRuntime
{
    private int _index;

    public void StartMatch(BotMatchStart start) => _index = 0;

    public BotDecision ExecuteTick(BotObservation observation) =>
        BotDecision.Of(_index < script.Length ? script[_index++] : BotAction.Wait);
}

/// <summary>Decides each tick via a delegate — for observation-driven test bots.</summary>
public sealed class DelegateRuntime(Func<BotObservation, BotDecision> decide) : IBotRuntime
{
    public void StartMatch(BotMatchStart start)
    {
    }

    public BotDecision ExecuteTick(BotObservation observation) => decide(observation);
}

/// <summary>Throws on every tick, optionally after N good ticks.</summary>
public sealed class ThrowingRuntime(int goodTicks = 0) : IBotRuntime
{
    private int _tick;

    public void StartMatch(BotMatchStart start) => _tick = 0;

    public BotDecision ExecuteTick(BotObservation observation)
    {
        if (_tick++ < goodTicks)
            return BotDecision.Of(BotAction.Wait);
        throw new InvalidOperationException("Deliberate test failure");
    }
}

public static class TestMaps
{
    /// <summary>Open 7x5 room, bots facing each other on the middle row.</summary>
    public static ArenaMap OpenRoom(Direction facing0 = Direction.East, Direction facing1 = Direction.West) =>
        ArenaMap.Create("test-open", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(1, 2, facing0), new Spawn(5, 2, facing1)]);

    /// <summary>Room with a wall pillar between the spawns on the middle row.</summary>
    public static ArenaMap PillarRoom() =>
        ArenaMap.Create("test-pillar", [
            "#######",
            "#.....#",
            "#..#..#",
            "#.....#",
            "#######",
        ], [new Spawn(1, 2, Direction.East), new Spawn(5, 2, Direction.West)]);

    /// <summary>Wide open room for visibility-range tests (range 6 fits inside).</summary>
    public static ArenaMap WideRoom() =>
        ArenaMap.Create("test-wide", [
            "############",
            "#..........#",
            "#..........#",
            "#..........#",
            "############",
        ], [new Spawn(1, 2, Direction.East), new Spawn(10, 2, Direction.West)]);

    public static MatchConfiguration Config(
        ArenaMap map, IBotRuntime bot0, IBotRuntime bot1, ulong seed = 1, GameRules? rules = null) =>
        new()
        {
            Map = map,
            Rules = rules ?? GameRules.V0_1,
            Seed = seed,
            Participants =
            [
                new MatchParticipantConfig { Name = "Bot0", Runtime = bot0 },
                new MatchParticipantConfig { Name = "Bot1", Runtime = bot1 },
            ],
        };
}
