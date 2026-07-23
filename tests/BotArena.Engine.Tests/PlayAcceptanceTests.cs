using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

/// <summary>
/// The RULES-0.5-DESIGN executable plays as engine tests: each scene the spec promises
/// must actually be executable with the shipped numbers (and the documented limits —
/// like the counter-surf — must hold exactly as documented).
/// </summary>
public class PlayAcceptanceTests
{
    private static ArenaMap OpenRoom() => ArenaMap.Create("test-play-room", [
        "#############",
        "#...........#",
        "#...........#",
        "#...........#",
        "#...........#",
        "#...........#",
        "#############",
    ], [new Spawn(3, 3, Direction.East), new Spawn(11, 3, Direction.East)]);

    /// <summary>THE BACKSTAB: under cone vision an attacker can cross a target's blind
    /// arc and land a shot the target never saw coming.</summary>
    [Fact]
    public void Backstab_UnseenApproach_Connects()
    {
        var rules = GameRules.V0_1 with { RulesVersion = "test-backstab", VisionCone = true };
        var session = new MatchSession(OpenRoom(), rules);
        // Slot 1 faces East (away); slot 0 approaches from directly behind.
        for (int i = 0; i < 4; i++)
        {
            session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]);
            Assert.DoesNotContain(session.BuildObservation(1).VisibleEnemies, e => e.Slot == 0);
        }
        Assert.Equal(new Position(7, 3), session.State.Bots[0].Position);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(2, session.State.Bots[1].Health); // shot in the back, never seen
    }

    private static GameRules SqueezeRules => GameRules.V0_1 with
    {
        RulesVersion = "test-squeeze",
        ProjectileTicksPerTile = 2,
        ShotRange = 8,
        ZoneControl = true,
        ZoneDominationTicks = 0,
        MaxTicks = 40,
    };

    private static ArenaMap SqueezeMap() => ArenaMap.Create("test-squeeze-map", [
        "############",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "############",
    ], [new Spawn(4, 3, Direction.East), new Spawn(7, 3, Direction.South)],
        zone: [new Position(6, 3), new Position(7, 3), new Position(6, 4), new Position(7, 4)]);

    /// <summary>DOUBLE-LANE SQUEEZE, part 1: a bolt fired at an occupied zone tile is a
    /// real eviction threat — a camper that holds its tile takes the hit.</summary>
    [Fact]
    public void Squeeze_ACamperThatHoldsItsTile_IsHit()
    {
        var session = new MatchSession(SqueezeMap(), SqueezeRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        // Bolt: (5,3)@t0 → (6,3)@t2 → (7,3)@t4 — the camper's tile.
        for (int tick = 1; tick <= 4; tick++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(2, session.State.Bots[1].Health);
    }

    /// <summary>DOUBLE-LANE SQUEEZE, part 2 — the documented limit (RULES-0.5-DESIGN §C):
    /// against bolts ALONE, a correctly counter-surfing camper survives on-zone through a
    /// two-bolt volley. This pins the semantics that make the executable squeeze
    /// bolts + body, and must FAIL if bolt cadence ever changes silently.</summary>
    [Fact]
    public void Squeeze_CounterSurf_SurvivesBoltsAlone()
    {
        var session = new MatchSession(SqueezeMap(), SqueezeRules);
        var zone = new HashSet<Position>(SqueezeMap().EffectiveZone());
        // Attacker: volley row 3, reposition to row 4, volley row 4.
        // Camper (starts (7,3) facing South): surf to (7,4) before bolt1 lands, then
        // 180° back to (7,3) before bolt2 lands.
        (BotAction A, BotAction B)[] script =
        [
            (BotAction.Shoot, BotAction.Wait),        // t0: bolt1 → (5,3)
            (BotAction.TurnRight, BotAction.Wait),    // t1: attacker E→S
            (BotAction.MoveForward, BotAction.Wait),  // t2: attacker (4,4); bolt1 (6,3)
            (BotAction.TurnLeft, BotAction.MoveForward), // t3: attacker S→E; camper → (7,4)
            (BotAction.Shoot, BotAction.Wait),        // t4: bolt2 → (5,4); bolt1 (7,3) — empty
            (BotAction.Wait, BotAction.TurnLeft),     // t5: camper S→E
            (BotAction.Wait, BotAction.TurnLeft),     // t6: camper E→N; bolt1 (8,3), bolt2 (6,4)
            (BotAction.Wait, BotAction.MoveForward),  // t7: camper → (7,3)
            (BotAction.Wait, BotAction.Wait),         // t8: bolt2 (7,4) — vacated
            (BotAction.Wait, BotAction.Wait),         // t9
        ];
        foreach (var (a, b) in script)
        {
            session.Step([BotDecision.Of(a), BotDecision.Of(b)]);
            var camper = session.State.Bots[1];
            Assert.Equal(3, camper.Health);
            Assert.Contains(camper.Position, zone);
        }
    }
}
