using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ShotRangeTests
{
    private static readonly GameRules RangeRules = GameRules.V0_1 with
    {
        RulesVersion = "test-range",
        ShotRange = 3,
        ShootCooldownTicks = 0,
    };

    /// <summary>10-wide corridor: shooter at (1,1) facing East, target far beyond range.</summary>
    private static ArenaMap Corridor() => ArenaMap.Create("test-corridor", [
        "##########",
        "#........#",
        "##########",
    ], [new Spawn(1, 1, Direction.East), new Spawn(8, 1, Direction.West)]);

    [Fact]
    public void CappedShot_StopsAtRange_AndMisses()
    {
        var session = new MatchSession(Corridor(), RangeRules);
        var result = session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var shot = result.Events.Single(e => e.Type == GameEventType.Shot);
        Assert.Null(shot.HitSlot);
        Assert.Equal(4, shot.ToX); // 3 tiles of travel from x=1
        Assert.Equal(3, session.State.Bots[1].Health);
    }

    [Fact]
    public void CappedShot_HitsWithinRange()
    {
        var map = ArenaMap.Create("test-close", [
            "##########",
            "#........#",
            "##########",
        ], [new Spawn(1, 1, Direction.East), new Spawn(3, 1, Direction.West)]);
        var session = new MatchSession(map, RangeRules);
        var result = session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var shot = result.Events.Single(e => e.Type == GameEventType.Shot && e.Slot == 0);
        Assert.Equal(1, shot.HitSlot);
        Assert.Equal(2, session.State.Bots[1].Health);
    }

    [Fact]
    public void UnlimitedRange_IsUnchangedForOlderRules()
    {
        var session = new MatchSession(Corridor(), GameRules.V0_1 with { ShootCooldownTicks = 0 });
        var result = session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var shot = result.Events.Single(e => e.Type == GameEventType.Shot);
        Assert.Equal(1, shot.HitSlot); // full corridor reach, as before
    }
}
