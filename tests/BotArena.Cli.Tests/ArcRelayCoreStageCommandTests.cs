using System.IO.Compression;
using System.Text.Json;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayCoreStageCommandTests
{
    [Fact]
    public void FocusedStageWritesEightBodyCustodyChoreography()
    {
        string output = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-core-stage-{Guid.NewGuid():N}");
        try
        {
            Assert.Equal(
                0,
                ArcRelayCoreStageCommand.Run(
                [
                    "--ticks", "104",
                    "--out", output,
                ]));

            using FileStream file = File.OpenRead(
                Path.Combine(output, "replay.json.gz"));
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using JsonDocument replay = JsonDocument.Parse(gzip);
            JsonElement root = replay.RootElement;
            Assert.Equal(2, root.GetProperty("broadcastVersion").GetInt32());
            Assert.False(root.GetProperty("partial").GetBoolean());
            Assert.Equal(104, root.GetProperty("worlds").GetArrayLength());
            Assert.Equal(
                "arc-relay-core-stage-01",
                root.GetProperty("header")
                    .GetProperty("contract")
                    .GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());

            using JsonDocument receipt = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "stage.json")));
            JsonElement stage = receipt.RootElement;
            Assert.Equal(
                "core-presentation-diagnostic-no-balance-claim",
                stage.GetProperty("purpose").GetString());
            Assert.Equal(8, stage.GetProperty("bodyCount").GetInt32());
            Assert.Equal(8, stage.GetProperty("classes").GetArrayLength());
            JsonElement choreography = stage.GetProperty("choreography");
            Assert.Equal(1, choreography.GetProperty("coreBirths").GetInt32());
            Assert.True(choreography.GetProperty("handoffs").GetInt32() >= 6);
            Assert.True(
                choreography.GetProperty("voluntaryDrops").GetInt32() >= 2);
            Assert.Equal(
                8,
                choreography.GetProperty("distinctMovingHandlers").GetInt32());
            Assert.Equal(
                8,
                choreography.GetProperty("distinctHandlers").GetInt32());
            Assert.True(
                choreography.GetProperty("longestArcTossTiles").GetInt32() >= 4);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void FocusedStageRejectsTooShortReplayBeforeWriting()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ArcRelayCoreStageCommand.Run(["--ticks", "40"]));
        Assert.Contains("Core stage --ticks", error.Message);
    }
}
