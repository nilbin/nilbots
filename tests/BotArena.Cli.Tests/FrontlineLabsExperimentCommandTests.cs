using System.Text.Json;
using BotArena.Cli;
using BotArena.Engine;

namespace BotArena.Cli.Tests;

[Collection("Console")]
public sealed class FrontlineLabsExperimentCommandTests
{
    [Fact]
    public void InProcessRun_WritesDeterministicVerifiableReplayV3()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-cli-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string first = Path.Combine(temporary, "first");
            string second = Path.Combine(temporary, "second");

            Assert.Equal(0, Run(alpha, beta, first));
            Assert.Equal(0, Run(alpha, beta, second));

            string firstPath = Path.Combine(first, "replay.json");
            string firstJson = File.ReadAllText(firstPath);
            string secondJson = File.ReadAllText(
                Path.Combine(second, "replay.json"));
            Assert.Equal(firstJson, secondJson);

            using JsonDocument document = JsonDocument.Parse(firstJson);
            JsonElement root = document.RootElement;
            Assert.False(root.GetProperty("partial").GetBoolean());
            Assert.Equal(
                3,
                root.GetProperty("header")
                    .GetProperty("replayVersion")
                    .GetInt32());
            Assert.Equal(
                "frontline-labs-1",
                root.GetProperty("header")
                    .GetProperty("gameRulesVersion")
                    .GetString());
            Assert.Equal(
                "frontline-labs-01",
                root.GetProperty("header")
                    .GetProperty("contract")
                    .GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());
            Assert.Equal(
                64,
                root.GetProperty("replayHash").GetString()!.Length);
            Assert.Equal(0, Verify(firstPath));

            string tamperedPath = Path.Combine(
                temporary,
                "tampered.json");
            File.WriteAllText(
                tamperedPath,
                firstJson.Replace(
                    "\"gameRulesVersion\":\"frontline-labs-1\"",
                    "\"gameRulesVersion\":\"frontline-labs-2\"",
                    StringComparison.Ordinal));
            Assert.Equal(1, Verify(tamperedPath));
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void MissingEntrantsAndUnknownOptions_FailBeforeRunning()
    {
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run([]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--future-option",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--seeds",
                    "1,2",
                    "--open",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-threshold",
                    "0",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-gain-phase",
                    "300",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-threshold",
                    "12",
                    "--capture-gain-phase",
                    "300:2",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-threshold",
                    "12",
                    "--mobilize-turrets",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--mobilize-turrets",
                    "false",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--mobilize-turrets",
                    "--remote-fabrication",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--net-control",
                    "--one-bend-shots",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--duel-map",
                    "unknown-map",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--one-bend-shots",
                    "--duel-map",
                    "thin-fronts",
                ]));
    }

    [Fact]
    public void DeclaredManifestClasses_ResolveArmAndTeamBinding()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-class-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        string previousDirectory = Directory.GetCurrentDirectory();
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(temporary);
            Console.SetOut(output);
            Assert.Equal(
                0,
                NewCommand.Run(
                    "Pathfinder",
                    ["--profile", "generic-actor"]));
            Assert.Equal(
                0,
                NewCommand.Run(
                    "Holdfast",
                    ["--profile", "generic-actor"]));
            DeclareClass(
                Path.Combine(temporary, "Pathfinder"), "striker");
            DeclareClass(
                Path.Combine(temporary, "Holdfast"), "bulwark");

            // The striker project is --bot, but bulwark sorts first: the
            // command must auto-bind each bot to its class's canonical team.
            Assert.Equal(
                0,
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--bot",
                        Path.Combine(temporary, "Pathfinder"),
                        "--opponent",
                        Path.Combine(temporary, "Holdfast"),
                        "--runtime",
                        "in-process",
                        "--seed",
                        "7",
                    ]));
        }
        finally
        {
            Console.SetOut(original);
            Directory.SetCurrentDirectory(previousDirectory);
            try
            {
                Directory.Delete(temporary, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        string log = output.ToString();
        Assert.Contains(
            "Classes resolved from bot manifests: bulwark-vs-striker.",
            log);
        Assert.Contains(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            log);
        Assert.Contains("Holdfast", log);

        InvalidOperationException mismatch =
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--bot",
                        "missing-project",
                        "--opponent",
                        "missing-project",
                        "--classes",
                        "striker-vs-warlock",
                        "--print-candidate-contract",
                    ]));
        Assert.Contains("Unknown Frontline Labs class", mismatch.Message);
    }

    private static void DeclareClass(string projectDirectory, string classId)
    {
        string manifestPath = Path.Combine(
            projectDirectory, "botarena.json");
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        var fields = new Dictionary<string, object?>();
        foreach (JsonProperty property in manifest.RootElement.EnumerateObject())
            fields[property.Name] = property.Value.Clone();
        fields["class"] = classId;
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                fields,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void ClassesArm_EmitsCanonicalIdentityAndRejectsSwappedOrder()
    {
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(
                0,
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "fabricator-vs-striker",
                        "--duel-map",
                        "thin-fronts",
                    ]));
        }
        finally
        {
            Console.SetOut(original);
        }

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(
            "frontline-labs-1-experiment-classes-fabricator-vs-striker",
            root.GetProperty("rulesetId").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.ClassesSeedProfileId,
            root.GetProperty("seedProfileId").GetString());
        Assert.Equal(
            "frontline-labs-01-thin-fronts-classes",
            root.GetProperty("mapId").GetString());

        InvalidOperationException swapped =
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "striker-vs-fabricator",
                    ]));
        Assert.Contains("fabricator-vs-striker", swapped.Message);

        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "striker-vs-warlock",
                ]));
    }

    [Fact]
    public void ClassesArm_ComposesWithMovementAndKeepsTheBaselineIdentity()
    {
        JsonElement uncoupled = PrintedContract(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        JsonElement inert = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--movement",
                "preserve-facing",
            ]);
        JsonElement coupled = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--movement",
                "facing-locked",
                "--duel-map",
                "thin-fronts",
            ]);

        // The inert default must not perturb an existing arm's identity.
        Assert.Equal(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            inert.GetProperty("rulesetId").GetString());
        Assert.Equal(
            uncoupled.GetProperty("rulesFingerprint").GetString(),
            inert.GetProperty("rulesFingerprint").GetString());
        Assert.Equal(
            uncoupled.GetProperty("matchContractFingerprint").GetString(),
            inert.GetProperty("matchContractFingerprint").GetString());

        Assert.Equal(
            "frontline-labs-1-classes-bulwark-vs-striker-facing-locked",
            coupled.GetProperty("rulesetId").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.ClassesSeedProfileId,
            coupled.GetProperty("seedProfileId").GetString());
        Assert.Equal(
            "frontline-labs-01-thin-fronts-classes",
            coupled.GetProperty("mapId").GetString());
        Assert.NotEqual(
            uncoupled.GetProperty("rulesFingerprint").GetString(),
            coupled.GetProperty("rulesFingerprint").GetString());
    }

    [Fact]
    public void SideObjectiveArm_SelectsTheMusterMapAndNeedsACellToSitIn()
    {
        JsonElement plain = PrintedContract(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        JsonElement muster = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--side-objective",
                "muster",
            ]);

        // The arm mints its own ruleset, its own map generation, and its own
        // fingerprints; nothing about the plain pair moves.
        Assert.Equal(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            plain.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-muster",
            muster.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-01-classes",
            plain.GetProperty("mapId").GetString());
        Assert.Equal(
            "frontline-labs-02-muster-classes",
            muster.GetProperty("mapId").GetString());
        Assert.NotEqual(
            plain.GetProperty("matchContractFingerprint").GetString(),
            muster.GetProperty("matchContractFingerprint").GetString());

        // Naming the baseline explicitly changes nothing at all.
        Assert.Equal(
            plain.GetProperty("matchContractFingerprint").GetString(),
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--side-objective",
                    "none",
                ]).GetProperty("matchContractFingerprint").GetString());

        // It composes with a pendulum level without a class pair...
        Assert.Equal(
            "frontline-labs-1-experiment-keel-muster",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--pendulum",
                    "keel",
                    "--side-objective",
                    "muster",
                ]).GetProperty("rulesetId").GetString());

        // ...but a side objective with no cell to sit in is refused, and an
        // unknown level names the ones that exist.
        Assert.Contains(
            "needs a cell to sit in",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--side-objective",
                        "muster",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "use none or muster",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--side-objective",
                        "relay",
                    ])).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureArm_MintsTheChannelAndNeedsACellToSitIn()
    {
        JsonElement plain = PrintedContract(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        JsonElement channel = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--capture",
                "channel",
            ]);

        // A capture-core change mints its own ruleset and its own
        // fingerprints, on the same map — the front is a rules fact.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-channel",
            channel.GetProperty("rulesetId").GetString());
        Assert.Equal(
            plain.GetProperty("mapId").GetString(),
            channel.GetProperty("mapId").GetString());
        Assert.NotEqual(
            plain.GetProperty("rulesFingerprint").GetString(),
            channel.GetProperty("rulesFingerprint").GetString());

        // Naming the baseline explicitly changes nothing at all.
        Assert.Equal(
            plain.GetProperty("matchContractFingerprint").GetString(),
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--capture",
                    "frozen",
                ]).GetProperty("matchContractFingerprint").GetString());

        // It composes with a pendulum level without a class pair, and the
        // whole candidate game plus the channel is the registered `storm`
        // (the wave-8 erosion-4 mint `siege` still names those exact bytes).
        Assert.Equal(
            "frontline-labs-1-experiment-keel-channel",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--pendulum",
                    "keel",
                    "--capture",
                    "channel",
                ]).GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-storm-facing-locked",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--movement",
                    "facing-locked",
                    "--pendulum",
                    "keel",
                    "--skills",
                    "kit",
                    "--bend",
                    "universal",
                    "--aim",
                    "offset",
                    "--stance-ground",
                    "open",
                    "--cooldown",
                    "ticking",
                    "--volley",
                    "salvo",
                    "--capture",
                    "channel",
                ]).GetProperty("rulesetId").GetString());

        // A channel with no cell to sit in is refused, and an unknown level
        // names the ones that exist.
        Assert.Contains(
            "needs a cell to sit in",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--capture",
                        "channel",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "use frozen or channel",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--capture",
                        "keystone",
                    ])).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The battlefield economy through the CLI: its own ruleset on the SAME
    /// map (the deposit addresses are rules facts, not map regions), the
    /// registered composite in the shipped cell, the control level spelling
    /// itself, the mutual exclusion with the side objective, and the
    /// needs-a-cell guard.
    /// </summary>
    [Fact]
    public void EconomyArm_MintsScrapKeepsTheMapAndRefusesTheSideObjective()
    {
        JsonElement plain = PrintedContract(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        JsonElement scrap = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--economy",
                "scrap",
            ]);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-scrap",
            scrap.GetProperty("rulesetId").GetString());
        Assert.NotEqual(
            plain.GetProperty("rulesFingerprint").GetString(),
            scrap.GetProperty("rulesFingerprint").GetString());
        // The one concrete improvement over the side objective: no new map
        // generation, so the arm stays fingerprint-comparable to every arm
        // measured to date.
        Assert.Equal(
            plain.GetProperty("mapId").GetString(),
            scrap.GetProperty("mapId").GetString());
        Assert.Equal(
            plain.GetProperty("mapFingerprint").GetString(),
            scrap.GetProperty("mapFingerprint").GetString());

        // The control level is its own ruleset, never a registered composite.
        // It spells `flat` rather than the flag's own `scrap-flat`: the
        // composite it appends to already names the economy, and the extra
        // characters do not fit beside the worst class pair.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-flat",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--economy",
                    "scrap-flat",
                ]).GetProperty("rulesetId").GetString());

        // The shipped game: swell + channel + scrap is `citadel` at v1.1
        // (the wave-8 pricing's own mint, `bastion`, still names those bytes).
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-citadel-facing-locked",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--movement",
                    "facing-locked",
                    "--pendulum",
                    "keel",
                    "--skills",
                    "kit",
                    "--bend",
                    "universal",
                    "--stance-ground",
                    "open",
                    "--aim",
                    "offset",
                    "--cooldown",
                    "ticking",
                    "--volley",
                    "salvo",
                    "--capture",
                    "channel",
                    "--economy",
                    "scrap",
                ]).GetProperty("rulesetId").GetString());

        // Both side-lane arms in one cell is refused, as is a cell-free arm
        // and an unknown level.
        Assert.Contains(
            "cannot run in the same cell",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--side-objective",
                        "muster",
                        "--economy",
                        "scrap",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "needs a cell to sit in",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--economy",
                        "scrap",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "use none, scrap or scrap-flat",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--economy",
                        "forge",
                    ])).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The roster flag: it mints its own map generation and its own topology
    /// profile, needs a class pair, refuses to share a cell with the side
    /// objective, and names the levels that exist when misspelled.
    /// </summary>
    [Fact]
    public void RosterArm_MintsTheLegionMapAndNeedsAClassPair()
    {
        JsonElement plain = PrintedContract(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        JsonElement legion = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--roster",
                "legion",
            ]);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-legion",
            legion.GetProperty("rulesetId").GetString());
        // Unlike the economy, this arm DOES mint a map: the extra reserved
        // spawn anchors are map facts, and a map is never edited in place.
        Assert.Equal(
            "frontline-labs-03-legion-classes",
            legion.GetProperty("mapId").GetString());
        Assert.NotEqual(
            plain.GetProperty("mapFingerprint").GetString(),
            legion.GetProperty("mapFingerprint").GetString());
        Assert.NotEqual(
            plain.GetProperty("matchContractFingerprint").GetString(),
            legion.GetProperty("matchContractFingerprint").GetString());

        // The full v1.1 game plus the roster is the registered `garrison`.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-garrison-facing-locked",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--movement",
                    "facing-locked",
                    "--pendulum",
                    "keel",
                    "--skills",
                    "kit",
                    "--bend",
                    "universal",
                    "--stance-ground",
                    "open",
                    "--aim",
                    "offset",
                    "--cooldown",
                    "ticking",
                    "--volley",
                    "salvo",
                    "--capture",
                    "channel",
                    "--economy",
                    "scrap",
                    "--roster",
                    "legion",
                ]).GetProperty("rulesetId").GetString());

        // A roster with no class pair to state its shape is refused, as is a
        // cell carrying both map-minting arms, and an unknown level names the
        // ones that exist.
        Assert.Contains(
            "needs a class pair",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--pendulum",
                        "keel",
                        "--roster",
                        "legion",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "cannot run in the same cell",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--side-objective",
                        "muster",
                        "--roster",
                        "legion",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "use none or legion",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--roster",
                        "horde",
                    ])).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The next round's two new flags: the keel without its rally, and the
    /// longer horizon. Both are registered levels with their own identity, and
    /// the whole package collapses to one token per shape.
    /// </summary>
    [Fact]
    public void HullAndHorizon_MintTheNextRoundsPackage()
    {
        JsonElement hull = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--pendulum",
                "hull",
            ]);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-hull",
            hull.GetProperty("rulesetId").GetString());

        JsonElement longer = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--pendulum",
                "hull",
                "--horizon",
                "long",
            ]);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-hull-long",
            longer.GetProperty("rulesetId").GetString());
        Assert.NotEqual(
            hull.GetProperty("rulesFingerprint").GetString(),
            longer.GetProperty("rulesFingerprint").GetString());

        // The whole package: one registered token per shape, and the roster
        // stays a composable flag on top of it.
        string[] package =
        [
            "--print-candidate-contract",
            "--classes",
            "fabricator-vs-fabricator",
            "--movement",
            "facing-locked",
            "--pendulum",
            "hull",
            "--skills",
            "kit",
            "--bend",
            "universal",
            "--five-slots",
            "wane",
            "--stance-ground",
            "open",
            "--aim",
            "offset",
            "--cooldown",
            "ticking",
            "--volley",
            "salvo",
            "--capture",
            "channel",
            "--economy",
            "scrap",
            "--horizon",
            "long",
        ];
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-fabricator-warren-facing-locked",
            PrintedContract(package).GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-fabricator-swarm-facing-locked",
            PrintedContract([.. package, "--roster", "legion"])
                .GetProperty("rulesetId")
                .GetString());

        // A horizon with no cell to sit in is refused, and an unknown level
        // names the ones that exist.
        Assert.Contains(
            "needs a cell to sit in",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--horizon",
                        "long",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "use standard or long",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--horizon",
                        "epic",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "use control, ratchet, ratchet-contest, keel, hull",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--pendulum",
                        "hulk",
                    ])).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PendulumArms_ComposeIntoTheFourPhaseOneFactorLevels()
    {
        JsonElement control = PrintedContract(
            ["--print-candidate-contract", "--pendulum", "control"]);
        JsonElement ratchet = PrintedContract(
            ["--print-candidate-contract", "--pendulum", "ratchet"]);
        JsonElement ratchetContest = PrintedContract(
            ["--print-candidate-contract", "--pendulum", "ratchet-contest"]);
        JsonElement numbersOnly = PrintedContract(
            [
                "--print-candidate-contract",
                "--capture-threshold",
                "9",
                "--prime-respawn-ticks",
                "9",
            ]);

        // The explicit control token is the hosted contract, byte for byte.
        Assert.Equal(
            FrontlineLabsDefinition.RulesetId,
            control.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-experiment-ratchet",
            ratchet.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-experiment-ratchet-contest",
            ratchetContest.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-experiment-capture-9-respawn-9",
            numbersOnly.GetProperty("rulesetId").GetString());
        Assert.Equal(
            4,
            new HashSet<string?>
            {
                control.GetProperty("rulesFingerprint").GetString(),
                ratchet.GetProperty("rulesFingerprint").GetString(),
                ratchetContest.GetProperty("rulesFingerprint").GetString(),
                numbersOnly.GetProperty("rulesFingerprint").GetString(),
            }.Count);
        // Only the rules move: every level plays the same map.
        Assert.Single(
            new HashSet<string?>
            {
                control.GetProperty("mapFingerprint").GetString(),
                ratchet.GetProperty("mapFingerprint").GetString(),
                ratchetContest.GetProperty("mapFingerprint").GetString(),
                numbersOnly.GetProperty("mapFingerprint").GetString(),
            });
    }

    [Fact]
    public void PendulumArms_ComposeWithClassesMovementAndDuelMap()
    {
        JsonElement cell = PrintedContract(
            [
                "--print-candidate-contract",
                "--pendulum",
                "ratchet-contest",
                "--classes",
                "bulwark-vs-striker",
                "--movement",
                "facing-locked",
                "--duel-map",
                "thin-fronts",
            ]);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-contest-facing-locked",
            cell.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-01-thin-fronts-classes",
            cell.GetProperty("mapId").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.ClassesSeedProfileId,
            cell.GetProperty("seedProfileId").GetString());

        JsonElement numbersCell = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--capture-threshold",
                "9",
                "--prime-respawn-ticks",
                "9",
            ]);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-c9-r9",
            numbersCell.GetProperty("rulesetId").GetString());
    }

    /// <summary>
    /// Phase 1b (DECISIONS #166) is one registered token for every built
    /// counterweight. It is a level of its own, it fits the worst class cell,
    /// and — because a level is identified by what it composes rather than by
    /// how it was typed — naming its four factors one by one on a short class
    /// pair, where the spelled form still fits, resolves to that same ruleset.
    /// </summary>
    [Fact]
    public void PendulumKeel_IsTheRegisteredEveryCounterweightLevel()
    {
        JsonElement ratchetContest = PrintedContract(
            ["--print-candidate-contract", "--pendulum", "ratchet-contest"]);
        JsonElement keel = PrintedContract(
            ["--print-candidate-contract", "--pendulum", "keel"]);

        Assert.Equal(
            "frontline-labs-1-experiment-keel",
            keel.GetProperty("rulesetId").GetString());
        Assert.NotEqual(
            ratchetContest.GetProperty("rulesFingerprint").GetString(),
            keel.GetProperty("rulesFingerprint").GetString());
        Assert.Equal(
            ratchetContest.GetProperty("mapFingerprint").GetString(),
            keel.GetProperty("mapFingerprint").GetString());

        // The cell the short token exists for: per-factor, the same four
        // would need 83 of the 64 canonical characters here.
        JsonElement worst = PrintedContract(
            [
                "--print-candidate-contract",
                "--pendulum",
                "keel",
                "--classes",
                "fabricator-vs-fabricator",
                "--movement",
                "facing-locked",
            ]);
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-fabricator-keel-facing-locked",
            worst.GetProperty("rulesetId").GetString());

        // Same combination, spelled instead of named, on a pair short enough
        // that both spellings fit: one ruleset, one fingerprint.
        JsonElement named = PrintedContract(
            [
                "--print-candidate-contract",
                "--pendulum",
                "keel",
                "--classes",
                "bulwark-vs-striker",
            ]);
        JsonElement spelled = PrintedContract(
            [
                "--print-candidate-contract",
                "--pendulum",
                "sticky-frontline,forward-rally,contest-majority,"
                + "enemy-sole-decay",
                "--classes",
                "bulwark-vs-striker",
            ]);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-keel",
            named.GetProperty("rulesetId").GetString());
        Assert.Equal(
            named.GetProperty("rulesetId").GetString(),
            spelled.GetProperty("rulesetId").GetString());
        Assert.Equal(
            named.GetProperty("matchContractFingerprint").GetString(),
            spelled.GetProperty("matchContractFingerprint").GetString());
    }

    /// <summary>
    /// Phase 2 (DECISIONS #169) keeps the spelled flag form — the factors are
    /// still three separate options on the command line — and resolves it to
    /// the registered composite identity, which is what makes the widest cells
    /// expressible at all. The kit resolves per class exactly as
    /// <c>--skills kit</c> already does, so on a single-class pair the whole
    /// kit and that class's one skill are the same ruleset.
    /// </summary>
    [Fact]
    public void PhaseTwoFlagsComposeIntoTheRegisteredCompositeIdentities()
    {
        (string Skills, string Bend, string Token)[] cells =
        [
            ("none", "striker-only", "keel"),
            ("kit", "striker-only", "helm"),
            ("none", "universal", "veer"),
            ("kit", "universal", "rig"),
        ];
        var fingerprints = new HashSet<string>();
        foreach (var cell in cells)
        {
            JsonElement contract = PrintedContract(
                [
                    "--print-candidate-contract",
                    "--pendulum",
                    "keel",
                    "--skills",
                    cell.Skills,
                    "--bend",
                    cell.Bend,
                    "--classes",
                    "fabricator-vs-fabricator",
                    "--movement",
                    "facing-locked",
                ]);
            Assert.Equal(
                "frontline-labs-1-fabricator-vs-fabricator-"
                + $"{cell.Token}-facing-locked",
                contract.GetProperty("rulesetId").GetString());
            Assert.True(
                fingerprints.Add(
                    contract.GetProperty("matchContractFingerprint")
                        .GetString()
                    ?? string.Empty),
                $"duplicate contract bytes for {cell.Token}");
        }

        // Named kit and per-class kit are one ruleset: this cell can only
        // carry FIVE SLOTS, so asking for all three asks for exactly that.
        JsonElement wholeKit = PrintedContract(
            [
                "--print-candidate-contract",
                "--pendulum",
                "keel",
                "--skills",
                "kit",
                "--bend",
                "universal",
                "--classes",
                "fabricator-vs-fabricator",
            ]);
        JsonElement cellSkill = PrintedContract(
            [
                "--print-candidate-contract",
                "--pendulum",
                "keel",
                "--skills",
                "five-slots",
                "--bend",
                "universal",
                "--classes",
                "fabricator-vs-fabricator",
            ]);
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-fabricator-rig",
            wholeKit.GetProperty("rulesetId").GetString());
        Assert.Equal(
            wholeKit.GetProperty("rulesetId").GetString(),
            cellSkill.GetProperty("rulesetId").GetString());
        Assert.Equal(
            wholeKit.GetProperty("matchContractFingerprint").GetString(),
            cellSkill.GetProperty("matchContractFingerprint").GetString());
    }

    [Fact]
    public void PendulumArms_RejectUnknownTokensAndIncompatibleArms()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                [
                    "--print-candidate-contract",
                    "--pendulum",
                    "overtime",
                ]));
        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                [
                    "--print-candidate-contract",
                    "--pendulum",
                    "control,ratchet",
                ]));
        InvalidOperationException exclusive =
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--pendulum",
                        "ratchet",
                        "--net-control",
                    ]));
        Assert.Contains(
            "one Frontline Labs experiment option at a time",
            exclusive.Message);
    }

    [Fact]
    public void PendulumRatchetArm_WritesItsTypedPoliciesIntoTheReplay()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-pendulum-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "ratchet-contest");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    pendulum: "ratchet-contest"));

            string replayPath = Path.Combine(output, "replay.json");
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(replayPath));
            JsonElement rules = document.RootElement
                .GetProperty("header")
                .GetProperty("contract")
                .GetProperty("rules");
            JsonElement capture = rules
                .GetProperty("gameMode")
                .GetProperty("capture");

            Assert.Equal(
                "frontline-labs-1-experiment-ratchet-contest",
                rules.GetProperty("rulesetId").GetString());
            Assert.Equal(
                "advance-immediately-then-deny-enemy-regression-past-the-" +
                "high-water-mark-through-configured-hold-ticks",
                capture.GetProperty("redeployPolicy").GetString());
            Assert.Equal(
                40,
                capture.GetProperty("ratchetHoldTicks").GetInt32());
            Assert.Equal(
                "net-positive-objective-weight-difference-scales-gain-" +
                "non-positive-applies-configured-decay-opposition-erodes-" +
                "to-neutral",
                capture.GetProperty("controlPolicy").GetString());
            Assert.Equal(
                "own-side-chain-adjacent-objective-tile-in-team-advance-" +
                "order-then-assigned-spawn",
                rules.GetProperty("lifecycle")
                    .GetProperty("automaticReturnPlacement")
                    .GetString());
            Assert.Equal(0, Verify(replayPath));
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void MovementArm_WritesDistinctKinematicsIdentity()
    {
        JsonElement baseline =
            PrintedContract(["--print-candidate-contract"]);
        JsonElement setsFacing = PrintedContract(
            ["--print-candidate-contract", "--movement", "move-sets-facing"]);
        JsonElement locked = PrintedContract(
            ["--print-candidate-contract", "--movement", "facing-locked"]);
        JsonElement inert = PrintedContract(
            ["--print-candidate-contract", "--movement", "preserve-facing"]);

        Assert.Equal(
            FrontlineLabsDefinition.RulesetId,
            inert.GetProperty("rulesetId").GetString());
        Assert.Equal(
            baseline.GetProperty("matchContractFingerprint").GetString(),
            inert.GetProperty("matchContractFingerprint").GetString());
        Assert.Equal(
            "frontline-labs-1-experiment-move-sets-facing",
            setsFacing.GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-experiment-facing-locked",
            locked.GetProperty("rulesetId").GetString());
        Assert.Equal(
            baseline.GetProperty("mapFingerprint").GetString(),
            setsFacing.GetProperty("mapFingerprint").GetString());
        Assert.Equal(
            3,
            new HashSet<string?>
            {
                baseline.GetProperty("rulesFingerprint").GetString(),
                setsFacing.GetProperty("rulesFingerprint").GetString(),
                locked.GetProperty("rulesFingerprint").GetString(),
            }.Count);

        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                [
                    "--print-candidate-contract",
                    "--movement",
                    "tank-controls",
                ]));
        InvalidOperationException exclusive =
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--movement",
                        "facing-locked",
                        "--one-bend-shots",
                    ]));
        Assert.Contains(
            "one Frontline Labs experiment option at a time",
            exclusive.Message);
    }

    [Fact]
    public void SkillArms_CarryOnlyTheSkillsTheCellsClassesOwn()
    {
        JsonElement plain = PrintedContract(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        JsonElement none = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--skills",
                "none",
            ]);
        JsonElement kit = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--skills",
                "kit",
            ]);
        JsonElement subset = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--skills",
                "volley,shell",
            ]);

        // The inert default must not perturb an existing arm's identity.
        Assert.Equal(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            none.GetProperty("rulesetId").GetString());
        Assert.Equal(
            plain.GetProperty("matchContractFingerprint").GetString(),
            none.GetProperty("matchContractFingerprint").GetString());

        // FIVE SLOTS has no owner in this cell, so `kit` and the explicit
        // subset are the same content-identified arm.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-cast-break",
            kit.GetProperty("rulesetId").GetString());
        Assert.Equal(
            subset.GetProperty("rulesetId").GetString(),
            kit.GetProperty("rulesetId").GetString());
        Assert.Equal(
            subset.GetProperty("matchContractFingerprint").GetString(),
            kit.GetProperty("matchContractFingerprint").GetString());
        Assert.NotEqual(
            plain.GetProperty("rulesFingerprint").GetString(),
            kit.GetProperty("rulesFingerprint").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.TopologyProfileId,
            kit.GetProperty("topologyProfileId").GetString());
    }

    /// <summary>
    /// The curve grammar is a rules-wide factor rather than a class
    /// capability, so it gets its own flag, its own token, and the same
    /// inert-default discipline: naming the baseline explicitly must leave an
    /// existing arm byte for byte what it was.
    /// </summary>
    [Fact]
    public void BendArm_IsItsOwnFactorAndLeavesTheBaselineUntouched()
    {
        JsonElement plain = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--pendulum",
                "contest-majority",
            ]);
        JsonElement baseline = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--pendulum",
                "contest-majority",
                "--bend",
                "striker-only",
            ]);
        JsonElement universal = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--pendulum",
                "contest-majority",
                "--bend",
                "universal",
            ]);

        Assert.Equal(
            plain.GetProperty("rulesetId").GetString(),
            baseline.GetProperty("rulesetId").GetString());
        Assert.Equal(
            plain.GetProperty("matchContractFingerprint").GetString(),
            baseline.GetProperty("matchContractFingerprint").GetString());
        Assert.Equal(
            $"{plain.GetProperty("rulesetId").GetString()}-bend",
            universal.GetProperty("rulesetId").GetString());
        Assert.NotEqual(
            plain.GetProperty("rulesFingerprint").GetString(),
            universal.GetProperty("rulesFingerprint").GetString());
        // A grammar change moves guns, never bodies.
        Assert.Equal(
            plain.GetProperty("topologyProfileId").GetString(),
            universal.GetProperty("topologyProfileId").GetString());
    }

    [Fact]
    public void BendArm_ComposesWithTheSkillKitAndNeedsAClassPair()
    {
        JsonElement kit = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "bulwark-vs-striker",
                "--pendulum",
                "ratchet-contest",
                "--skills",
                "kit",
                "--bend",
                "universal",
            ]);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-contest-cast-break-bend",
            kit.GetProperty("rulesetId").GetString());

        Assert.Contains(
            "needs a class pair",
            Assert.Throws<InvalidOperationException>(() =>
                PrintedContract(
                    [
                        "--print-candidate-contract",
                        "--bend",
                        "universal",
                    ])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "striker-only or universal",
            Assert.Throws<InvalidOperationException>(() =>
                PrintedContract(
                    [
                        "--print-candidate-contract",
                        "--classes",
                        "bulwark-vs-striker",
                        "--bend",
                        "everyone",
                    ])).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FiveSlotArm_PublishesTheAsymmetricTopologyProfile()
    {
        JsonElement plain = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "fabricator-vs-striker",
            ]);
        JsonElement slots = PrintedContract(
            [
                "--print-candidate-contract",
                "--classes",
                "fabricator-vs-striker",
                "--skills",
                "five-slots",
            ]);

        Assert.Equal(
            "frontline-labs-1-fabricator-vs-striker-slot5",
            slots.GetProperty("rulesetId").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.AsymmetricSlotsTopologyProfileId,
            slots.GetProperty("topologyProfileId").GetString());
        Assert.NotEqual(
            plain.GetProperty("topologyFingerprint").GetString(),
            slots.GetProperty("topologyFingerprint").GetString());
        // The map is held constant so the factor is the slot count alone.
        Assert.Equal(
            plain.GetProperty("mapFingerprint").GetString(),
            slots.GetProperty("mapFingerprint").GetString());
    }

    [Fact]
    public void SkillArms_ComposeWithMovementAndPendulumAndRejectOrphans()
    {
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-cast-break-facing-locked",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--skills",
                    "kit",
                    "--movement",
                    "facing-locked",
                ]).GetProperty("rulesetId").GetString());
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-ratchet-break",
            PrintedContract(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--skills",
                    "shell",
                    "--pendulum",
                    "ratchet",
                ]).GetProperty("rulesetId").GetString());

        // A skill needs its owning class, and skills need a class cell.
        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--skills",
                    "five-slots",
                ]));
        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                ["--print-candidate-contract", "--skills", "volley"]));
        Assert.Throws<InvalidOperationException>(() =>
            FrontlineLabsExperimentCommand.Run(
                [
                    "--print-candidate-contract",
                    "--classes",
                    "bulwark-vs-striker",
                    "--skills",
                    "barricade",
                ]));
    }

    private static JsonElement PrintedContract(string[] args)
    {
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, FrontlineLabsExperimentCommand.Run(args));
        }
        finally
        {
            Console.SetOut(original);
        }

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        return document.RootElement.Clone();
    }

    [Fact]
    public void PrintCandidateContract_RequiresNoBotsAndEmitsExactIdentity()
    {
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(
                0,
                FrontlineLabsExperimentCommand.Run(
                    [
                        "--print-candidate-contract",
                        "--auto-companions",
                        "--duel-map",
                        "thin-fronts",
                    ]));
        }
        finally
        {
            Console.SetOut(original);
        }

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(
            "frontline-labs-1-experiment-one-bend-auto-companions",
            root.GetProperty("rulesetId").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.DuelDepthSeedProfileId,
            root.GetProperty("seedProfileId").GetString());
        Assert.Equal(
            "frontline-labs-01-thin-fronts-auto-companions",
            root.GetProperty("mapId").GetString());
        Assert.Equal(
            FrontlineLabsDefinition.TopologyProfileId,
            root.GetProperty("topologyProfileId").GetString());
        Assert.Equal(
            64,
            root.GetProperty("matchContractFingerprint")
                .GetString()!
                .Length);
    }

    [Fact]
    public void CaptureThresholdArm_WritesDistinctRulesetIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-arm-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "capture-12");

            Assert.Equal(0, Run(alpha, beta, output, "12"));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement header = document.RootElement.GetProperty("header");
            Assert.Equal(
                "frontline-labs-1-experiment-capture-12",
                header.GetProperty("gameRulesVersion").GetString());
            Assert.Equal(
                12,
                header.GetProperty("contract")
                    .GetProperty("rules")
                    .GetProperty("gameMode")
                    .GetProperty("capture")
                    .GetProperty("threshold")
                    .GetInt32());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void CaptureGainPhaseArm_WritesScheduleAndDistinctIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-gain-arm-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "gain-t300-2");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    captureGainPhase: "300:2"));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement header = document.RootElement.GetProperty("header");
            Assert.Equal(
                "frontline-labs-1-experiment-gain-t300-2",
                header.GetProperty("gameRulesVersion").GetString());
            JsonElement schedule = header.GetProperty("contract")
                .GetProperty("rules")
                .GetProperty("gameMode")
                .GetProperty("capture")
                .GetProperty("gainSchedule");
            Assert.Equal(2, schedule.GetArrayLength());
            Assert.Equal(
                ("late-escalation", 300, 2),
                (
                    schedule[1].GetProperty("phaseId").GetString(),
                    schedule[1].GetProperty("startsAtTick").GetInt32(),
                    schedule[1]
                        .GetProperty("gainPerSoleTeamTick")
                        .GetInt32()));
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void MobilizeArm_WritesActionTransitionAndDistinctIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-mobilize-arm-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "mobilize");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    mobilizeTurrets: true));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement rules = document.RootElement
                .GetProperty("header")
                .GetProperty("contract")
                .GetProperty("rules");
            Assert.Equal(
                "frontline-labs-1-experiment-mobilize",
                rules.GetProperty("rulesetId").GetString());
            Assert.Contains(
                rules.GetProperty("actions").EnumerateArray(),
                action =>
                    action.GetProperty("id").GetString() == "mobilize"
                    && action.GetProperty("code").GetInt32() == 104);
            JsonElement transition = Assert.Single(
                rules.GetProperty("sameLifeTransitions").EnumerateArray(),
                value =>
                    value.GetProperty("transitionId").GetString()
                    == "mobilize-child");
            Assert.Equal(
                "turret",
                transition.GetProperty("sourceFormId").GetString());
            Assert.Equal(
                "child-mobile",
                transition.GetProperty("targetFormId").GetString());
            Assert.True(
                transition.GetProperty("irreversibleForLife").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void RemoteFabricationArm_WritesDistinctSourceRegion()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-remote-fabrication-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "remote-fabrication");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    remoteFabrication: true));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement contract = document.RootElement
                .GetProperty("header")
                .GetProperty("contract");
            Assert.Equal(
                "frontline-labs-1-experiment-remote-fabrication",
                contract.GetProperty("rules")
                    .GetProperty("rulesetId")
                    .GetString());
            Assert.Equal(
                "frontline-labs-01-remote-fabrication-experiment",
                contract.GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());
            JsonElement transition = Assert.Single(
                contract.GetProperty("rules")
                    .GetProperty("fabricationTransitions")
                    .EnumerateArray());
            Assert.Empty(
                transition.GetProperty("requiredSourceTileTags")
                    .EnumerateArray());
            Assert.Contains(
                contract.GetProperty("participantRegionAssignments")
                    .EnumerateArray(),
                assignment =>
                    assignment.GetProperty("regionRoleId").GetString()
                        == "fabrication-source"
                    && assignment.GetProperty("mapRegionId").GetString()
                        == "fabrication-source-anywhere");
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void NetControlArm_WritesDistinctControlPolicy()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-net-control-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "net-control");

            Assert.Equal(
                0,
                Run(alpha, beta, output, netControl: true));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement rules = document.RootElement
                .GetProperty("header")
                .GetProperty("contract")
                .GetProperty("rules");
            Assert.Equal(
                "frontline-labs-1-experiment-net-control",
                rules.GetProperty("rulesetId").GetString());
            Assert.Equal(
                "net-positive-objective-weight-difference-scales-gain-" +
                "non-positive-applies-configured-decay-opposition-erodes-" +
                "to-neutral",
                rules.GetProperty("gameMode")
                    .GetProperty("capture")
                    .GetProperty("controlPolicy")
                    .GetString());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void OneBendShotsArm_WritesSmallPrivateProgramEnvelope()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-one-bend-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "one-bend");

            Assert.Equal(
                0,
                Run(alpha, beta, output, oneBendShots: true));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement rules = document.RootElement
                .GetProperty("header")
                .GetProperty("contract")
                .GetProperty("rules");
            Assert.Equal(
                "frontline-labs-1-experiment-one-bend-shots",
                rules.GetProperty("rulesetId").GetString());
            JsonElement program = rules.GetProperty("attackProfiles")
                .EnumerateArray()
                .Single(profile =>
                    profile.GetProperty("id").GetString()
                    == "mobile-bolt")
                .GetProperty("shotProgram");
            Assert.Equal(0, program
                .GetProperty("minInitialAimSteps").GetInt32());
            Assert.Equal(0, program
                .GetProperty("maxInitialAimSteps").GetInt32());
            Assert.Equal(1, program
                .GetProperty("minBendCount").GetInt32());
            Assert.Equal(1, program
                .GetProperty("maxBendCount").GetInt32());
            Assert.True(program.GetProperty("payloadOptional").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void DuelMapArm_WritesOneBendRulesAndDistinctMapIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-duel-map-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "thin-fronts");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    duelMap: "thin-fronts"));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement contract = document.RootElement
                .GetProperty("header")
                .GetProperty("contract");
            Assert.Equal(
                "frontline-labs-1-experiment-one-bend-shots",
                contract.GetProperty("rules")
                    .GetProperty("rulesetId")
                    .GetString());
            Assert.Equal(
                "frontline-labs-01-thin-fronts-experiment",
                contract.GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());
            Assert.All(
                contract.GetProperty("map")
                    .GetProperty("regions")
                    .EnumerateArray()
                    .Where(region => region.GetProperty("regionId")
                        .GetString()!
                        .StartsWith(
                            "frontline-position-",
                            StringComparison.Ordinal)),
                region => Assert.Equal(
                    3,
                    region.GetProperty("tiles").GetArrayLength()));
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void AutomaticCompanionsArm_WritesDeclaredProgressionContract()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-auto-companions-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "auto-companions");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    automaticCompanions: true,
                    duelMap: "thin-fronts"));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement contract = document.RootElement
                .GetProperty("header")
                .GetProperty("contract");
            Assert.Equal(
                "frontline-labs-1-experiment-one-bend-auto-companions",
                contract.GetProperty("rules")
                    .GetProperty("rulesetId")
                    .GetString());
            Assert.Equal(
                "frontline-labs-01-thin-fronts-auto-companions",
                contract.GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());
            Assert.Equal(
                4,
                contract.GetProperty("lifecycleAssignments")
                    .EnumerateArray()
                    .Count(assignment =>
                        assignment.GetProperty("initialAvailability")
                            .GetString()
                        == "dormant-automatic-activation-at-tick"));
            Assert.Contains(
                document.RootElement.GetProperty("ticks")
                    .EnumerateArray()
                    .SelectMany(tick => tick.GetProperty("tickStart")
                        .GetProperty("lifeStarts")
                        .EnumerateArray()),
                start => start.GetProperty("origin")
                    .GetProperty("reason")
                    .GetString() == "automatic-activation");
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    private static int Run(
        string bot,
        string opponent,
        string output,
        string? captureThreshold = null,
        string? captureGainPhase = null,
        bool mobilizeTurrets = false,
        bool remoteFabrication = false,
        bool netControl = false,
        bool oneBendShots = false,
        bool automaticCompanions = false,
        string? duelMap = null,
        string? pendulum = null)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            var arguments = new List<string>
            {
                "--bot",
                bot,
                "--opponent",
                opponent,
                "--runtime",
                "in-process",
                "--seed",
                "42",
                "--out",
                output,
            };
            if (captureThreshold is not null)
            {
                arguments.Add("--capture-threshold");
                arguments.Add(captureThreshold);
            }
            if (captureGainPhase is not null)
            {
                arguments.Add("--capture-gain-phase");
                arguments.Add(captureGainPhase);
            }
            if (mobilizeTurrets)
                arguments.Add("--mobilize-turrets");
            if (remoteFabrication)
                arguments.Add("--remote-fabrication");
            if (netControl)
                arguments.Add("--net-control");
            if (oneBendShots)
                arguments.Add("--one-bend-shots");
            if (automaticCompanions)
                arguments.Add("--auto-companions");
            if (duelMap is not null)
            {
                arguments.Add("--duel-map");
                arguments.Add(duelMap);
            }
            if (pendulum is not null)
            {
                arguments.Add("--pendulum");
                arguments.Add(pendulum);
            }
            return FrontlineLabsExperimentCommand.Run(arguments);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static int Verify(string replayPath)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return VerifyCommand.Run(replayPath);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static string CreateWaitBot(
        string root,
        string name)
    {
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        string sdkProject = Path.Combine(
            FindRepoRoot(),
            "src",
            "BotArena.Sdk",
            "BotArena.Sdk.csproj");
        File.WriteAllText(
            Path.Combine(directory, $"{name}.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net10.0</TargetFramework>
                 <Nullable>enable</Nullable>
                 <ImplicitUsings>enable</ImplicitUsings>
               </PropertyGroup>
               <ItemGroup>
                 <ProjectReference Include="{sdkProject}" />
               </ItemGroup>
             </Project>
             """);
        File.WriteAllText(
            Path.Combine(directory, $"{name}.cs"),
            $$"""
              using BotArena.Sdk;

              public sealed class {{name}} : IGenericActorBot
              {
                  public GenericActorDecision Tick(
                      GenericActorContext context)
                  {
                      GenericActorActionLegality wait =
                          context.Action("wait")!;
                      return GenericActorDecision.WithoutArguments(
                          wait.ActionId,
                          wait.ActionCode);
                  }
              }
              """);
        File.WriteAllText(
            Path.Combine(directory, "botarena.json"),
            $$"""
              {
                "name": "{{name}}",
                "entryType": "{{name}}",
                "sdkVersion": "0.10.4"
              }
              """);
        return directory;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
