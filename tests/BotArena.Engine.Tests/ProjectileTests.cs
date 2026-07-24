using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ProjectileTests
{
    private static GameRules BoltRules => GameRules.V0_1 with
    {
        RulesVersion = "test-bolts",
        ProjectileTicksPerTile = 2,
        ShotRange = 8,
    };

    private static ArenaMap Room() => ArenaMap.Create("test-bolt-room", [
        "############",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "############",
    ], [new Spawn(3, 3, Direction.East), new Spawn(9, 3, Direction.West)]);

    private static void Steps(MatchSession session, params (BotAction A, BotAction B)[] ticks)
    {
        foreach (var (a, b) in ticks)
            session.Step([BotDecision.Of(a), BotDecision.Of(b)]);
    }

    [Fact]
    public void Bolt_SpawnsAdjacent_AndAdvancesEverySecondTick()
    {
        var session = new MatchSession(Room(), BoltRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(4, 3), Assert.Single(session.State.Projectiles).Position);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(4, 3), session.State.Projectiles[0].Position); // phase tick
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(5, 3), session.State.Projectiles[0].Position); // advanced
    }

    [Fact]
    public void StationaryTarget_IsHitWhenTheBoltArrives()
    {
        var session = new MatchSession(Room(), BoltRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        // Bolt path: (4,3)@t0 → (5,3)@t2 → (6,3)@t4 → (7,3)@t6 → (8,3)@t8 → (9,3)@t10.
        for (int tick = 1; tick <= 9; tick++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(3, session.State.Bots[1].Health); // still in flight
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]); // t10
        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Empty(session.State.Projectiles); // consumed by the hit
    }

    [Fact]
    public void WalkingIntoABolt_IsLethal()
    {
        var session = new MatchSession(Room(), BoltRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]); // bolt at (5,3)
        // Bot 1 marches west: (8,3)@t3, (7,3)@t4, (6,3)@t5 — meeting the bolt, which
        // occupies (6,3) from t6... bot 1 walks ONTO (6,3) exactly as the bolt advances there.
        Steps(session,
            (BotAction.Wait, BotAction.MoveForward),
            (BotAction.Wait, BotAction.MoveForward),
            (BotAction.Wait, BotAction.MoveForward));
        // t6: bolt advances (5,3)→(6,3); bot 1 stands there → hit.
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(2, session.State.Bots[1].Health);
    }

    [Fact]
    public void ABoltNeverHitsItsOwner()
    {
        var session = new MatchSession(Room(), BoltRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        // Owner overtakes its own slow bolt (the Vanguard Push): walk into its tile.
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(4, 3), session.State.Bots[0].Position);
        Assert.Equal(new Position(4, 3), session.State.Projectiles[0].Position);
        Assert.Equal(3, session.State.Bots[0].Health); // shared tile, no self-hit
    }

    [Fact]
    public void PointBlankShot_HitsImmediately_NoBoltSpawned()
    {
        var session = new MatchSession(Room(), BoltRules);
        // March bot 1 adjacent to bot 0: (9,3) → (4,3) takes 5 moves.
        for (int i = 0; i < 5; i++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.MoveForward)]);
        Assert.Equal(new Position(4, 3), session.State.Bots[1].Position);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Empty(session.State.Projectiles);
    }

    [Fact]
    public void PhaseSurfing_EnteringTheTileABoltIsLeaving_IsAHit()
    {
        // The §H item 2 gap: the bolt at (4,3) advances on t2; the victim walks ONTO
        // (4,3) that very tick. Occupancy must be checked before the advance too —
        // under the old post-only check the bolt slid away and the walker survived.
        var map = ArenaMap.Create("test-surf-room", [
            "############",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "############",
        ], [new Spawn(3, 3, Direction.East), new Spawn(7, 3, Direction.West)]);
        var session = new MatchSession(map, BoltRules);
        Steps(session,
            (BotAction.Shoot, BotAction.MoveForward),  // t0: bolt → (4,3); walker → (6,3)
            (BotAction.Wait, BotAction.MoveForward),   // t1: bolt dwells; walker → (5,3)
            (BotAction.Wait, BotAction.MoveForward));  // t2: walker → (4,3); bolt advance is due
        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Empty(session.State.Projectiles); // consumed by the hit, not surfed past
    }

    [Fact]
    public void Observation_ExposesBoltTiming_ThatPredictsTheActualAdvance()
    {
        var session = new MatchSession(Room(), BoltRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);

        // Start of t1: the bolt dwells this tick, advances on the NEXT one.
        var bolt = Assert.Single(session.BuildObservation(0).VisibleProjectiles!);
        Assert.Equal(2, bolt.TicksUntilAdvance);
        Assert.Equal(7, bolt.RemainingTiles); // range 8, spawned on tile 1

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        // Start of t2: TicksUntilAdvance == 1 must mean "advances THIS tick".
        bolt = Assert.Single(session.BuildObservation(0).VisibleProjectiles!);
        Assert.Equal(1, bolt.TicksUntilAdvance);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(5, 3), session.State.Projectiles[0].Position);

        // After the advance the countdown resets and a range tile is spent.
        bolt = Assert.Single(session.BuildObservation(0).VisibleProjectiles!);
        Assert.Equal(2, bolt.TicksUntilAdvance);
        Assert.Equal(6, bolt.RemainingTiles);
    }

    [Fact]
    public void Bolt_DespawnsOnWalls_AndAtRange()
    {
        // Wall: shoot with a wall directly ahead.
        var session = new MatchSession(Room(), BoltRules);
        Steps(session,
            (BotAction.TurnLeft, BotAction.Wait),   // face North
            (BotAction.MoveForward, BotAction.Wait),
            (BotAction.MoveForward, BotAction.Wait)); // at (3,1), wall at (3,0)
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Empty(session.State.Projectiles); // swallowed by the wall at spawn

        // Range: an unobstructed bolt dies after ShotRange tiles.
        var far = new MatchSession(ArenaMap.Create("test-bolt-long", [
            "####################",
            "#..................#",
            "####################",
        ], [new Spawn(1, 1, Direction.East), new Spawn(18, 1, Direction.East)]),
            BoltRules with { ShotRange = 4 });
        far.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        for (int i = 0; i < 6; i++)
            far.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Empty(far.State.Projectiles); // (2,1)@spawn=1 tile … 4th tile reached & expired
    }

    [Fact]
    public void SpeedTwoBolt_SpawnsAdjacent_ThenTraversesTwoOrderedTiles()
    {
        var session = new MatchSession(Room(), BoltRules with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
        });

        var launch = session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(4, 3), Assert.Single(session.State.Projectiles).Position);
        Assert.Equal([new Position(4, 3)], Assert.Single(launch.ProjectileTraversals).Path);

        var advance = session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(new Position(6, 3), Assert.Single(session.State.Projectiles).Position);
        Assert.Equal(
            [new Position(5, 3), new Position(6, 3)],
            Assert.Single(advance.ProjectileTraversals).Path);
    }

    [Fact]
    public void SpeedTwoBolt_HitsIntermediateTarget_AndStopsBeforeSecondSubstep()
    {
        var map = ArenaMap.Create("test-speed-two-target", [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########",
        ], [new Spawn(2, 2, Direction.East), new Spawn(4, 2, Direction.West)]);
        var session = new MatchSession(map, BoltRules with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
        });
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);

        var result = session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Empty(session.State.Projectiles);
        Assert.Equal([new Position(4, 2)], Assert.Single(result.ProjectileTraversals).Path);
    }

    [Fact]
    public void SpeedTwoBolt_CannotTunnelThroughIntermediateWall()
    {
        var map = ArenaMap.Create("test-speed-two-wall", [
            "##########",
            "#........#",
            "#...#....#",
            "#........#",
            "##########",
        ], [new Spawn(2, 2, Direction.East), new Spawn(8, 2, Direction.West)]);
        var session = new MatchSession(map, BoltRules with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
        });
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);

        var result = session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Empty(session.State.Projectiles);
        Assert.Empty(result.ProjectileTraversals);
        Assert.Equal(3, session.State.Bots[1].Health);
    }

    [Fact]
    public void SpeedTwoBolt_FinalRangeTileIsLethalBeforeDespawn()
    {
        var map = ArenaMap.Create("test-speed-two-range", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(1, 2, Direction.East), new Spawn(3, 2, Direction.West)]);
        var session = new MatchSession(map, BoltRules with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
            ShotRange = 2,
        });
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Empty(session.State.Projectiles);
    }

    [Fact]
    public void SpeedTwoObservation_ExposesAdvanceDistanceAndTiming()
    {
        var session = new MatchSession(Room(), BoltRules with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
        });
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);

        var bolt = Assert.Single(session.BuildObservation(0).VisibleProjectiles!);

        Assert.Equal(2, bolt.TilesPerAdvance);
        Assert.Equal(1, bolt.TicksUntilAdvance);
        Assert.Equal(7, bolt.RemainingTiles);
    }
}
