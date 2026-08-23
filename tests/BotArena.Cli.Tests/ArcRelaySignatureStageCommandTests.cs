using System.IO.Compression;
using System.Text.Json;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

public sealed class ArcRelaySignatureStageCommandTests
{
    [Fact]
    public void FocusedStageWritesShortCompleteCompactReplay()
    {
        string output = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-signature-stage-{Guid.NewGuid():N}");
        try
        {
            Assert.Equal(
                0,
                ArcRelaySignatureStageCommand.Run(
                [
                    "--signature", "vector-dash",
                    "--ticks", "48",
                    "--out", output,
                ]));

            using FileStream file = File.OpenRead(
                Path.Combine(output, "replay.json.gz"));
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using JsonDocument replay = JsonDocument.Parse(gzip);
            JsonElement root = replay.RootElement;
            Assert.Equal(2, root.GetProperty("broadcastVersion").GetInt32());
            Assert.False(root.GetProperty("partial").GetBoolean());
            Assert.Equal(48, root.GetProperty("worlds").GetArrayLength());
            Assert.Equal(
                "arc-relay-stage-vector-dash-01",
                root.GetProperty("header")
                    .GetProperty("contract")
                    .GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());

            using JsonDocument receipt = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "stage.json")));
            Assert.Equal(
                "presentation-diagnostic-no-balance-claim",
                receipt.RootElement.GetProperty("purpose").GetString());
            Assert.True(receipt.RootElement.GetProperty("attempts").GetInt32() >= 2);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void FocusedStageRejectsUnknownSignatureBeforeWriting()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ArcRelaySignatureStageCommand.Run(
                ["--signature", "giant-mystery-arrow"]));
        Assert.Contains("Unknown Arc Relay signature", error.Message);
    }

    [Theory]
    [InlineData("arc-toss")]
    [InlineData("exchange")]
    [InlineData("tractor-hook")]
    [InlineData("kinetic-burst")]
    [InlineData("sentinel-seed")]
    public void FocusedStageRejectsSilentOrUnreadableSignatureDemos(
        string signatureId)
    {
        string output = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-signature-stage-{signatureId}-{Guid.NewGuid():N}");
        try
        {
            Assert.Equal(
                0,
                ArcRelaySignatureStageCommand.Run(
                [
                    "--signature", signatureId,
                    "--ticks", "48",
                    "--out", output,
                ]));
            Assert.True(File.Exists(Path.Combine(output, "replay.json.gz")));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }
}
