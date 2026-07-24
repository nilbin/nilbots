using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ReplaySerializationTests
{
    /// <summary>Gen-3 regression: canonical JSON omits null winnerSlot, so a drawn
    /// replay must deserialize without it (summary/verify crashed on every draw).</summary>
    [Fact]
    public void DrawnReplay_RoundTripsThroughJson()
    {
        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = TestMaps.OpenRoom(),
            Rules = GameRules.V0_1 with { MaxTicks = 4 },
            Seed = 1,
            Participants =
            [
                new MatchParticipantConfig { Name = "a", Runtime = new ScriptedRuntime() },
                new MatchParticipantConfig { Name = "b", Runtime = new ScriptedRuntime() },
            ],
        });
        Assert.Null(run.Result.WinnerSlot); // both idle → MaxTicks draw

        var document = ReplaySerializer.FromJson(ReplaySerializer.ToJson(run.Replay));
        Assert.Null(document.Result.WinnerSlot);
        Assert.Equal(run.ReplayHash, document.ReplayHash);
    }

    [Fact]
    public void ActiveControlAndSpeedTwoTraversal_RoundTripThroughReplay()
    {
        var map = ArenaMap.Create("test-replay-active", [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########",
        ], [new Spawn(1, 2, Direction.East), new Spawn(7, 2, Direction.West)],
            zone: [new Position(1, 2), new Position(2, 2), new Position(3, 2), new Position(4, 2)]);
        var rules = GameRules.V0_1 with
        {
            RulesVersion = "test-replay-active",
            MaxTicks = 2,
            ShotRange = 8,
            ZoneControl = true,
            ActiveZoneControl = true,
            ControlPressureLimit = 10,
            ControlPressureGain = 1,
            ControlPressureDecayInterval = 2,
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
        };
        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = map,
            Rules = rules,
            Seed = 1,
            Participants =
            [
                new MatchParticipantConfig
                {
                    Name = "a",
                    Runtime = new ScriptedRuntime(BotAction.Shoot, BotAction.Wait),
                },
                new MatchParticipantConfig
                {
                    Name = "b",
                    Runtime = new ScriptedRuntime(BotAction.Wait, BotAction.Wait),
                },
            ],
        });

        var document = ReplaySerializer.FromJson(ReplaySerializer.ToJson(run.Replay));

        Assert.Equal(10, document.Header.ControlPressureLimit);
        Assert.Equal(1, document.Result.ControlPressure);
        Assert.Equal(2, Assert.Single(document.Ticks[1].ProjectileTraversals!).Path.Count);
        Assert.Equal(2, Assert.Single(document.Ticks[1].Projectiles!).TilesPerAdvance);
        Assert.Equal(run.ReplayHash, document.ReplayHash);
    }
}
