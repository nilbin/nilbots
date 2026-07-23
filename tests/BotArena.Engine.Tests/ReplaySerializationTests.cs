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
}
