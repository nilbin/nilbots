using BotArena.Bots.BuiltIn;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Determinism.Tests;

/// <summary>
/// Characterization shield for the shipped replay-v1 simulation. These hashes must
/// remain unchanged while Frontline is developed alongside the historical rulesets.
/// </summary>
public sealed class OfficialRulesReplayCompatibilityTests
{
    private const string MapId = "basic-01";
    private const ulong MatchSeed = 42;
    private const string FirstBot = "hunter";
    private const string SecondBot = "coward";

    [Theory]
    [InlineData("0.1", "f858ab8b87288d297dde3e48308fb84c44f1ed23ada0614c7c129a4e0d99eca3")]
    [InlineData("0.2", "e1c46f75ef430b89e92df6e8030e28f0f01f32c87efcdc6d8acf21d8a4dbff7b")]
    [InlineData("0.3", "d6e143196e701c987d493ad10f7ad8e26000d3bd4321fbc4969b7a776207e9cf")]
    [InlineData("0.4", "00caef6318bfb2efb5b530b05a3a67d1ec2c7caae4e28fc798a0ea4cd853b7cc")]
    [InlineData("0.5", "a878d0dd8849f597a84ce8cd352d67c6893d46958203fd89e3fc923c1a34b363")]
    public void OfficialRules_ReproduceTheirPinnedReplayV1Hash(
        string rulesVersion,
        string expectedReplayHash)
    {
        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = LoadMap(),
            Rules = GameRules.Resolve(rulesVersion),
            Seed = MatchSeed,
            Participants =
            [
                Participant(FirstBot),
                Participant(SecondBot),
            ],
        });

        Assert.Equal(1, run.Replay.Header.ReplayVersion);
        Assert.Equal(rulesVersion, run.Replay.Header.GameRulesVersion);
        Assert.True(
            string.Equals(expectedReplayHash, run.ReplayHash, StringComparison.Ordinal),
            $"Expected {expectedReplayHash}; actual {run.ReplayHash}.");
    }

    private static ArenaMap LoadMap() =>
        ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "maps", MapId + ".json")));

    private static MatchParticipantConfig Participant(string name) =>
        new()
        {
            Name = name,
            Runtime = new InProcessBotRuntime(() => BuiltInBotCatalog.Create(name)),
        };
}
