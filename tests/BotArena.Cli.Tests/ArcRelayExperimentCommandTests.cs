using System.Text.Json;

namespace BotArena.Cli.Tests;

[Collection("Console")]
public sealed class ArcRelayExperimentCommandTests
{
    [Fact]
    public void CoordinatedMovementIsTheProductDefaultAndGreedyIsExplicit()
    {
        var noOptions = new Dictionary<string, string>();
        Assert.Equal(
            "coordinated",
            ArcRelayExperimentCommand.MindStepper(noOptions));

        byte[] linked = [1, 2, 3, 4];
        Assert.Same(
            linked,
            ArcRelayExperimentCommand.WithMindStepper(
                linked, "coordinated"));

        byte[] greedy = ArcRelayExperimentCommand.WithMindStepper(
            linked, "greedy");
        Assert.True(greedy.Length > linked.Length);
        using var reader = new BinaryReader(new MemoryStream(greedy));
        Assert.Equal(linked, reader.ReadBytes(linked.Length));
        Assert.Equal(0x50455453, reader.ReadInt32());
        Assert.Equal("greedy", reader.ReadString());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void AuthoredSheetIdentityOutranksGenericPackageFilename()
    {
        Assert.Equal(
            "tapline-depth-relay-circuit-v1",
            ArcRelayExperimentCommand.SheetIdentity(
                "ArcRelayTacticalPlaybookMindV1",
                "/candidate/tapline-depth-relay-circuit-v1/orders.json",
                "tapline-depth-relay-circuit-v1"));
        Assert.Equal(
            "starter-tapline-v2",
            ArcRelayExperimentCommand.SheetIdentity(
                "ArcRelayTacticalPlaybookMindV1",
                "/playbooks/starter-tapline-v2.json",
                sheetId: null));
        Assert.Equal(
            "ArcRelayTacticalPlaybookMindV1",
            ArcRelayExperimentCommand.SheetIdentity(
                "ArcRelayTacticalPlaybookMindV1",
                sheetPath: null,
                sheetId: null));
    }

    [Fact]
    public void Default_is_the_broadloom_workshop_product_line()
    {
        TextWriter stdout = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            Assert.Equal(0, ArcRelayExperimentCommand.Run(["--print-contract"]));
        }
        finally
        {
            Console.SetOut(stdout);
        }

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            "arc-relay-broadloom-3",
            document.RootElement.GetProperty("rules").GetProperty("rulesetId")
                .GetString());
        Assert.Equal(
            "arc-relay-broadloom-01",
            document.RootElement.GetProperty("map").GetProperty("mapId")
                .GetString());
        JsonElement workshops = document.RootElement.GetProperty("rules")
            .GetProperty("gameMode")
            .GetProperty("veterancyWorkshops");
        Assert.Equal(25, workshops.GetProperty("unlockTick").GetInt32());
        Assert.Equal(
            15, workshops.GetProperty("requiredChannelTicks").GetInt32());
        Assert.Equal(1, workshops.GetProperty("xpAward").GetInt32());
        Assert.Equal(0, workshops.GetProperty("dormancyTicks").GetInt32());
        Assert.Equal(
            3,
            document.RootElement.GetProperty("rules")
                .GetProperty("gameMode")
                .GetProperty("signatureGrammarVersion")
                .GetInt32());
        Assert.Equal(
            46,
            document.RootElement.GetProperty("rules")
                .GetProperty("gameMode")
                .GetProperty("bankTriggeredWellRechargeTicks")
                .GetInt32());
        Assert.Equal(
            5,
            document.RootElement.GetProperty("rules")
                .GetProperty("visionProfiles")[0]
                .GetProperty("range")
                .GetInt32());
        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["short-fast"] = 3,
                ["medium-steady"] = 4,
                ["long-slow"] = 6,
            },
            document.RootElement.GetProperty("rules")
                .GetProperty("attackProfiles")
                .EnumerateArray()
                .Where(value => value.GetProperty("id").GetString() is
                    "short-fast" or "medium-steady" or "long-slow")
                .ToDictionary(
                    value => value.GetProperty("id").GetString()!,
                    value => value.GetProperty("projectile")
                        .GetProperty("maxTravelTiles").GetInt32(),
                    StringComparer.Ordinal));
        JsonElement toss = document.RootElement.GetProperty("rules")
            .GetProperty("gameMode")
            .GetProperty("signatures")
            .EnumerateArray()
            .Single(value => value.GetProperty("signatureId").GetString()
                == "arc-toss");
        Assert.Equal(0, toss.GetProperty("tellTicks").GetInt32());
        Assert.Equal(
            "after-combat",
            toss.GetProperty("activationTiming").GetString());
        Assert.True(document.RootElement.GetProperty("rules")
            .GetProperty("gameMode")
            .GetProperty("sentryTeamVision").GetBoolean());
        Assert.Contains(
            document.RootElement.GetProperty("rules")
                .GetProperty("actions").EnumerateArray(),
            value => value.GetProperty("id").GetString()
                == "strafe-eight-way");
    }

    [Theory]
    [InlineData("stock-mind-v0/sheet.json")]
    [InlineData("dynamic-strategy-v4-2026-08-02/sheets/rear-ambush-dynamic.json")]
    [InlineData("intelligent-gambit-v1-2026-08-02/sheets/rear-hook-lantern-sweep.json")]
    [InlineData("standing-strategy-v1-2026-08-03/sheets/home-siege-north.json")]
    [InlineData("tactical-playbook-v1-2026-08-03/controls/coordination-parity-baseline.json")]
    public void EvaluationSheetVersionsLinkDeterministicallyIntoTheContract(
        string relativeSheet)
    {
        string sheet = Path.Combine(
            FindRepoRoot(),
            "arena-bots",
            "arc-relay",
            relativeSheet);
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        using var output = new StringWriter();
        Console.SetOut(output);
        Console.SetError(TextWriter.Null);
        try
        {
            Assert.Equal(
                0,
                ArcRelayExperimentCommand.Run(
                [
                    "--sheet0", sheet,
                    "--sheet1", sheet,
                    "--loop-profile", "depth-counterflow",
                    "--print-contract",
                ]));
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            "arc-relay-threefold-depth-counterflow-01",
            document.RootElement.GetProperty("map").GetProperty("mapId")
                .GetString());
    }

    [Fact]
    public void UnknownEvaluationSheetVersionsAreRefused()
    {
        string source = Path.Combine(
            FindRepoRoot(),
            "arena-bots",
            "arc-relay",
            "dynamic-strategy-v4-2026-08-02",
            "sheets",
            "rear-ambush-dynamic.json");
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-strategy-sheet-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                temporary,
                File.ReadAllText(source).Replace(
                    "arc-relay-evaluation-sheet-v1",
                    "arc-relay-evaluation-sheet-v999",
                    StringComparison.Ordinal));
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayExperimentCommand.Run(
                [
                    "--sheet0", temporary,
                    "--sheet1", temporary,
                    "--loop-profile", "h0",
                    "--print-contract",
                ]));
            Assert.Contains("Unsupported evaluation sheet schema", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
