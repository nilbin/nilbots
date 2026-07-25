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
            zone: [new Position(1, 2), new Position(2, 2), new Position(3, 2), new Position(4, 2)],
            themeId: "overgrown-lab",
            presentation: new MapPresentation(
                "perimeter",
                "cover",
                [new MapWallGroup("collapsed-cover", [new Position(0, 2)])]));
        var rules = GameRules.V0_1 with
        {
            RulesVersion = "test-replay-active",
            MaxTicks = 2,
            ShotRange = 8,
            ZoneControl = true,
            ActiveZoneControl = true,
            ControlBySoleOccupancy = true,
            ControlPressureLimit = 10,
            ControlPressureGain = 1,
            ControlPressureDecayInterval = 2,
            ControlOvertimeStartTick = 1,
            ControlOvertimePressureLimit = 4,
            ControlOvertimePressureGain = 2,
            ControlOvertimeStopsDecay = true,
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
                    LookId = "needle",
                },
                new MatchParticipantConfig
                {
                    Name = "b",
                    Runtime = new ScriptedRuntime(BotAction.Wait, BotAction.Wait),
                    LookId = "orbiter",
                },
            ],
        });

        var document = ReplaySerializer.FromJson(ReplaySerializer.ToJson(run.Replay));

        Assert.Equal("overgrown-lab", document.Header.ThemeId);
        Assert.Equal("perimeter", document.Header.Presentation!.BoundaryWall);
        Assert.Equal("collapsed-cover", Assert.Single(document.Header.Presentation.WallGroups).Family);
        Assert.Equal("needle", document.Header.Participants[0].LookId);
        Assert.Equal("orbiter", document.Header.Participants[1].LookId);
        Assert.Equal(10, document.Header.ControlPressureLimit);
        Assert.True(document.Header.ControlBySoleOccupancy);
        Assert.Equal(1, document.Header.ControlOvertimeStartTick);
        Assert.Equal(4, document.Header.ControlOvertimePressureLimit);
        Assert.Equal(2, document.Header.ControlOvertimePressureGain);
        Assert.True(document.Header.ControlOvertimeStopsDecay);
        Assert.Equal(3, document.Result.ControlPressure);
        Assert.Equal(2, Assert.Single(document.Ticks[1].ProjectileTraversals!).Path.Count);
        Assert.Equal(2, Assert.Single(document.Ticks[1].Projectiles!).TilesPerAdvance);
        Assert.Equal(run.ReplayHash, document.ReplayHash);
    }
}
