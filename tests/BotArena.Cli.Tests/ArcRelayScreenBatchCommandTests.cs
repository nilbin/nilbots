using System.Text.Json;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayScreenBatchCommandTests
{
    [Fact]
    public void PlanSource_IsRequiredAndExclusive()
    {
        Assert.Throws<InvalidOperationException>(
            () => ArcRelayScreenBatchCommand.Run(["--out", "unused"]));
        Assert.Throws<InvalidOperationException>(
            () => ArcRelayScreenBatchCommand.Run(
            [
                "--plan", "custom.json",
                "--sweep-plan", "sweep.json",
                "--out", "unused",
            ]));
    }

    [Fact]
    public void SweepLimit_MustBeAPositiveInteger()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-arc-screen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string plan = Path.Combine(temporary, "plan.json");
            File.WriteAllText(
                plan,
                JsonSerializer.Serialize(new
                {
                    entrants = new Dictionary<string, object>(),
                    cells = Array.Empty<object>(),
                }));

            Assert.Throws<InvalidOperationException>(
                () => ArcRelayScreenBatchCommand.Run(
                [
                    "--sweep-plan", plan,
                    "--bot", "unused",
                    "--limit", "zero",
                    "--out", Path.Combine(temporary, "out"),
                ]));
            Assert.Throws<InvalidOperationException>(
                () => ArcRelayScreenBatchCommand.Run(
                [
                    "--sweep-plan", plan,
                    "--bot", "unused",
                    "--limit", "0",
                    "--out", Path.Combine(temporary, "out"),
                ]));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void CustomPlan_RejectsDuplicateCellIdsBeforeRunning()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-arc-screen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string plan = Path.Combine(temporary, "plan.json");
            File.WriteAllText(
                plan,
                JsonSerializer.Serialize(
                    new ArcRelayScreenBatchPlan(
                        "arc-relay-screen-batch-v1",
                        "unused",
                        "unused",
                        "h0",
                    [
                        new("duplicate", "a.json", "b.json", "1"),
                        new("duplicate", "a.json", "b.json", "2"),
                    ])));

            Assert.Throws<InvalidDataException>(
                () => ArcRelayScreenBatchCommand.Run(
                [
                    "--plan", plan,
                    "--out", Path.Combine(temporary, "out"),
                ]));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
