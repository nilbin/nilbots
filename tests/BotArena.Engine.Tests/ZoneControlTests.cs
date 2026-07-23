using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ZoneControlTests
{
    private static GameRules ZoneRules(int dominationTicks = 5, int maxTicks = 20) =>
        GameRules.V0_1 with
        {
            RulesVersion = "test-zone",
            ZoneControl = true,
            ZoneDominationTicks = dominationTicks,
            MaxTicks = maxTicks,
        };

    private static GameRules ExclusiveRules(int dominationTicks = 5, int maxTicks = 20) =>
        ZoneRules(dominationTicks, maxTicks) with
        {
            RulesVersion = "test-zonex",
            ZoneExclusiveAccrual = true,
        };

    /// <summary>7x5 room with a declared 1-tile zone at (3,2); spawn 0 sits beside it.</summary>
    private static ArenaMap ZoneMap() => ArenaMap.Create("test-zone", [
        "#######",
        "#.....#",
        "#.....#",
        "#.....#",
        "#######",
    ], [new Spawn(2, 2, Direction.East), new Spawn(5, 2, Direction.West)], zone: [new Position(3, 2)]);

    /// <summary>Same room with a 2-tile zone so both bots can stand on it at once.</summary>
    private static ArenaMap WideZoneMap() => ArenaMap.Create("test-zone2", [
        "#######",
        "#.....#",
        "#.....#",
        "#.....#",
        "#######",
    ], [new Spawn(2, 2, Direction.East), new Spawn(5, 2, Direction.West)],
        zone: [new Position(3, 2), new Position(4, 2)]);

    [Fact]
    public void StandingOnZone_AccruesTicks_AndDominates()
    {
        var session = new MatchSession(ZoneMap(), ZoneRules(dominationTicks: 3));
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]); // onto (3,2)
        for (int i = 0; i < 2 && !session.IsCompleted; i++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.True(session.IsCompleted);
        Assert.Equal(MatchEndReason.Domination, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.Equal(3, session.Result.Bots[0].ZoneTicks);
        Assert.Equal(0, session.Result.Bots[1].ZoneTicks);
    }

    [Fact]
    public void MaxTicksTiebreak_ZoneBeatsHealth()
    {
        // Bot 1 shoots bot 0 once (health lead), then bot 0 camps the zone: at MaxTicks
        // the zone holder must win DESPITE lower health — the anti-entrenchment rule.
        var session = new MatchSession(ZoneMap(), ZoneRules(dominationTicks: 0, maxTicks: 6));
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Shoot)]); // 0 hit: h2
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]); // onto zone
        while (!session.IsCompleted)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(MatchEndReason.MaxTicks, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.True(session.Result.Bots[0].FinalHealth < session.Result.Bots[1].FinalHealth);
        Assert.True(session.Result.Bots[0].ZoneTicks > session.Result.Bots[1].ZoneTicks);
    }

    [Fact]
    public void ExclusiveAccrual_ContestedZone_PaysNobody()
    {
        var session = new MatchSession(WideZoneMap(), ExclusiveRules(dominationTicks: 0, maxTicks: 6));
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.MoveForward)]); // 0→(3,2), 1→(4,2)
        while (!session.IsCompleted)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(MatchEndReason.MaxTicks, session.Result!.Reason);
        Assert.Equal(0, session.Result.Bots[0].ZoneTicks);
        Assert.Equal(0, session.Result.Bots[1].ZoneTicks);
        Assert.Null(session.Result.WinnerSlot); // no zone edge, equal health/damage → draw
    }

    [Fact]
    public void ExclusiveAccrual_SoleOccupant_StillDominates()
    {
        var session = new MatchSession(ZoneMap(), ExclusiveRules(dominationTicks: 3));
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]); // onto (3,2)
        for (int i = 0; i < 2 && !session.IsCompleted; i++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.True(session.IsCompleted);
        Assert.Equal(MatchEndReason.Domination, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.Equal(3, session.Result.Bots[0].ZoneTicks);
    }

    [Fact]
    public void SharedAccrual_ContestedZone_PaysBoth()
    {
        var session = new MatchSession(WideZoneMap(), ZoneRules(dominationTicks: 0, maxTicks: 4));
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.MoveForward)]);
        while (!session.IsCompleted)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(4, session.Result!.Bots[0].ZoneTicks); // accrues post-move, tick 0 included
        Assert.Equal(4, session.Result.Bots[1].ZoneTicks);
        Assert.Null(session.Result.WinnerSlot);
    }

    [Fact]
    public void ZonelessRules_ProduceNullZoneTicks_AndOldTiebreak()
    {
        var session = new MatchSession(ZoneMap(), GameRules.V0_1 with { MaxTicks = 3 });
        while (!session.IsCompleted)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.All(session.Result!.Bots, b => Assert.Null(b.ZoneTicks));
        Assert.Null(session.Result.WinnerSlot); // equal health/damage → draw, as always
    }

    [Fact]
    public void EffectiveZone_FallsBackToOpenCenter()
    {
        var map = TestMaps.OpenRoom(); // no declared zone
        var zone = map.EffectiveZone();
        Assert.NotEmpty(zone);
        Assert.All(zone, p => Assert.False(map.IsWall(p)));
        Assert.Contains(new Position(map.Width / 2, map.Height / 2), zone);
    }

    [Fact]
    public void Observations_CarryZoneAndMapData()
    {
        var session = new MatchSession(ZoneMap(), ZoneRules());
        var observation = session.BuildObservation(0);
        Assert.Equal(7, observation.MapWidth);
        Assert.Equal(5, observation.MapHeight);
        Assert.Equal([new Position(3, 2)], observation.ZoneTiles);
        Assert.Equal(0, observation.MyZoneTicks);
        Assert.Equal(0, observation.EnemyZoneTicks);

        var plain = new MatchSession(ZoneMap(), GameRules.V0_1).BuildObservation(0);
        Assert.Null(plain.ZoneTiles);
        Assert.Null(plain.MyZoneTicks);
    }
}
