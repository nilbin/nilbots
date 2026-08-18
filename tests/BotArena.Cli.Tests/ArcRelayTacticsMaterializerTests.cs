using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Cli;
using BotArena.TacticalPlaybooks;

/// <summary>
/// The Tactics materializer contract: every flagship recipe materializes
/// deterministically into a compiling Orders v2 pair, goldens pin the exact
/// bytes, the maximal document stays inside the ATL2 envelope, and the
/// player-source grammar rejects malformed documents with field-path errors.
/// </summary>
public sealed class ArcRelayTacticsMaterializerTests
{
    public static readonly TheoryData<string> FlagshipRecipes = new()
    {
        "pincer", "centre-rush", "hold-area", "core-raid", "deliver-core",
        "patrol",
    };

    [Theory]
    [MemberData(nameof(FlagshipRecipes))]
    public void EveryFlagshipRecipeMaterializesAndCompiles(string name)
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture(name));
        Assert.True(compilation.UsesOrderTree);
        Assert.NotNull(compilation.TacticsSha256);
        Assert.NotNull(compilation.MaterializedOrders);
        Assert.NotNull(compilation.MaterializedMapPlan);
        Assert.Equal(8, compilation.Composition.Length);
    }

    [Theory]
    [MemberData(nameof(FlagshipRecipes))]
    public void EveryFlagshipRecipeMatchesItsGoldenMaterialization(string name)
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture(name));
        AssertGolden($"{name}.orders.json", compilation.MaterializedOrders!);
        AssertGolden($"{name}.map-plan.json", compilation.MaterializedMapPlan!);
    }

    [Fact]
    public void MaterializationIsByteDeterministic()
    {
        byte[] source = Fixture("pincer");
        TacticalPlaybookCompilation first =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(source);
        TacticalPlaybookCompilation second =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(source);
        Assert.Equal(first.MaterializedOrders, second.MaterializedOrders);
        Assert.Equal(first.MaterializedMapPlan, second.MaterializedMapPlan);
        Assert.Equal(first.LinkedData, second.LinkedData);
    }

    [Fact]
    public void MaximalDocumentStaysInsideTheRuntimeEnvelope()
    {
        // Eight tactics, three groups each, every feature on every tactic:
        // the 128 KiB ATL2 envelope and the 128-entry map-plan tables are the
        // binding budgets, and Compile throws past either — so a clean
        // compile IS the budget assertion.
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                MaximalDocument(everyFeatureEverywhere: true));
        Assert.True(compilation.LinkedData.Length <= 128 * 1024);
    }

    [Fact]
    public void GeneratedRoleIdsAreTheBotNames()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        string[] roleIds = orders.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(role => role.GetProperty("id").GetString()!)
            .ToArray();
        Assert.Equal(
            ["vex", "solder", "gantry", "flux", "ratchet", "ember", "piston", "cinder"],
            roleIds);
    }

    [Fact]
    public void PincerEncodesTheLifecycleContract()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement ember = orders.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Single(role => role.GetProperty("id").GetString() == "ember");
        JsonElement tactic = ember.GetProperty("orders").EnumerateArray().First();
        Assert.Equal("t1", tactic.GetProperty("id").GetString());
        // Attempt shell: timeout + cooldown, no outcome until at the root —
        // outcomes anchor at the release branch so they count from release.
        Assert.Equal(180, tactic.GetProperty("timeout").GetInt32());
        Assert.False(tactic.TryGetProperty("until", out _));
        JsonElement release = tactic.GetProperty("orders").EnumerateArray()
            .Single(node => node.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "t1-go");
        // The release gate is exact-post readiness (each guard bot bound to
        // its own post through its single-bot role) plus the enemies trigger.
        string[] gate = release.GetProperty("when").GetProperty("all")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Contains("c-t1-g1-at-post-0", gate);
        Assert.Contains("c-t1-g1-at-post-1", gate);
        Assert.Contains("c-t1-g2-ready", gate);
        Assert.Contains("c-t1-release-enemies", gate);
        // The release latch is the outcome until; evidence counts from entry.
        Assert.True(release.TryGetProperty("until", out JsonElement until));
        string[] outcomes = until.GetProperty("any").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        Assert.Contains("c-t1-kills", outcomes);
        Assert.Contains("c-t1-cleared", outcomes);
        Assert.Contains("we-hold-a-core", outcomes);
        JsonElement conditions = orders.RootElement.GetProperty("conditions");
        JsonElement kills = conditions.GetProperty("c-t1-kills");
        Assert.Equal("node-entry", kills.GetProperty("since").GetString());
        // The whole stage section dies at release: its wrapper's until IS the
        // release expression, so a finished tactic yields no-child-eligible
        // instead of re-staging.
        JsonElement stageWrapper = tactic.GetProperty("orders").EnumerateArray()
            .Single(node => node.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "t1-s");
        Assert.True(stageWrapper.TryGetProperty("until", out JsonElement latch));
        Assert.Equal(
            release.GetProperty("when").GetProperty("all").GetRawText(),
            latch.GetProperty("all").GetRawText());
        Assert.Contains(stageWrapper.GetProperty("orders").EnumerateArray(),
            node => node.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "t1-g1-s-move");
    }

    [Theory]
    [InlineData("release.conds",
        """{"enemiesAt":{"count":2,"zone":{"rect":[13,11,17,15]}},"contact":true}""",
        "exactly one active condition row")]
    [InlineData("groups[1].slots", "[0, 1, 3, 4, 6, 7, 5]",
        "belongs to more than one group")]
    [InlineData("finish", """{"outcomes":{},"maxAttempt":"none"}""",
        "pick at least one outcome")]
    public void MalformedDocumentsAreRejectedWithFieldPaths(
        string path,
        string replacementJson,
        string expectedMessage)
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonNode target = document["tactics"]![0]!;
        string[] segments = path.Replace("]", string.Empty)
            .Replace("[", ".").Split('.');
        for (int index = 0; index < segments.Length - 1; index++)
        {
            target = int.TryParse(segments[index], out int arrayIndex)
                ? target[arrayIndex]!
                : target[segments[index]]!;
        }
        string leaf = segments[^1];
        JsonNode replacement = JsonNode.Parse(replacementJson)!;
        if (int.TryParse(leaf, out int leafIndex))
            target[leafIndex] = replacement;
        else
            target[leaf] = replacement;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void PostsMustMatchTheGroupBotForBot()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonArray posts = document["tactics"]![0]!["groups"]![0]!["stage"]!
            ["posts"]!.AsArray();
        posts.RemoveAt(1);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("posts must match the group's 2 bots", error.Message);
    }

    [Fact]
    public void SignatureMarksRequireTheClassInTheGroup()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonObject sigs = document["tactics"]![0]!["groups"]![0]!["sigs"]!
            ["stage"]!.AsObject();
        sigs["mortar"] = JsonNode.Parse("""{"points":[[15,12]]}""");
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("no bot of class 'mortar'", error.Message);
    }

    [Fact]
    public void PostureBundledClassesRejectSpatialMarks()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonObject sigs = document["tactics"]![0]!["groups"]![0]!["sigs"]!
            ["stage"]!.AsObject();
        sigs["palisade"] = JsonNode.Parse("""{"points":[[15,12]]}""");
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("bundled by posture", error.Message);
    }

    [Fact]
    public void CliCompilesTacticsWithMaterializationProvenance()
    {
        string output = Directory.CreateTempSubdirectory(
            "arc-relay-tactics-cli").FullName;
        try
        {
            Assert.Equal(0, ArcRelayTacticalPlaybookCommand.Run(
            [
                "--tactics",
                Path.Combine(TacticsFixtureDirectory(), "pincer.tactics.json"),
                "--out", output,
            ]));
            Assert.True(File.Exists(
                Path.Combine(output, "playbook.atp")));
            Assert.True(File.Exists(
                Path.Combine(output, "materialized-orders.json")));
            Assert.True(File.Exists(
                Path.Combine(output, "materialized-map-plan.json")));
            using JsonDocument explain = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(output, "explain.json")));
            JsonElement provenance = explain.RootElement
                .GetProperty("materializedTactics");
            Assert.Equal("arc-relay-tactics-v1",
                provenance.GetProperty("schema").GetString());
            Assert.Equal(64, provenance.GetProperty("sourceSha256")
                .GetString()!.Length);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static byte[] MaximalDocument(bool everyFeatureEverywhere)
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        var tactics = new JsonArray();
        for (int index = 0; index < 8; index++)
        {
            JsonObject tactic = JsonNode.Parse(
                MaximalTactic(index))!.AsObject();
            if (!everyFeatureEverywhere)
            {
                // Rotate the geometry-heavy features so the union still
                // covers everything while the sheet respects the map-plan
                // table budget the way a real maximal sheet has to.
                if (index >= 2)
                    tactic["groups"]![0]!.AsObject().Remove("sigs");
                if (index >= 2)
                    tactic["groups"]![1]!.AsObject().Remove("sigs");
                if (index >= 3)
                {
                    tactic["groups"]![0]!["stage"] = JsonNode.Parse(
                        $$"""
                        {"kind": "area", "posture": "hold",
                         "recovery": "heal-tile",
                         "rect": [9, {{3 + index * 2}}, 11, {{4 + index * 2}}]}
                        """);
                }
                if (index >= 4)
                {
                    tactic["groups"]![1]!["stage"] = JsonNode.Parse(
                        $$"""
                        {"kind": "area", "posture": "observe",
                         "recovery": "patch",
                         "rect": [6, {{3 + index * 2}}, 8, {{5 + index * 2}}]}
                        """);
                }
                JsonNode watch = tactic["release"]!["conds"]!["enemiesAt"]!
                    ["zone"]!.DeepClone();
                tactic["start"]!["conds"]!["enemiesAt"]!["zone"] = watch;
                tactic["finish"]!["outcomes"]!["cleared"]!["zone"] =
                    watch.DeepClone();
            }
            tactics.Add(tactic);
        }
        document["tactics"] = tactics;
        return Encoding.UTF8.GetBytes(document.ToJsonString());
    }

    private static string MaximalTactic(int index)
    {
        int y = 3 + index * 2;
        return $$$"""
        {
          "id": "wave-{{{index}}}",
          "name": "Wave {{{index}}}",
          "recipe": "blank",
          "start": {
            "mode": "when",
            "conds": {
              "enemiesAt": {"count": {{{index + 1}}}, "zone": {"rect": [12, {{{y}}}, 18, {{{y + 3}}}]}},
              "coreLoose": {"well": "centre"},
              "carrier": true,
              "losses": {"count": {{{index % 7 + 1}}}}
            }
          },
          "release": {
            "mode": "ready-when",
            "conds": {
              "enemiesAt": {"count": 2, "zone": {"rect": [11, {{{y}}}, 19, {{{y + 4}}}]}}
            }
          },
          "finish": {
            "outcomes": {
              "kills": {"count": {{{index % 8 + 1}}}},
              "losses": {"count": {{{index % 8 + 1}}}},
              "cleared": {"zone": {"rect": [13, {{{y}}}, 17, {{{y + 2}}}]}},
              "coreAcquired": true,
              "coreBanked": true,
              "coreLost": true,
              "reached": true,
              "observed": true
            },
            "maxAttempt": "long"
          },
          "delivery": {"plan": "escort", "preferredSlots": [6, 0]},
          "groups": [
            {
              "id": "spear",
              "name": "Spear",
              "slots": [0, 1, 2],
              "coreRule": "hunt",
              "stage": {
                "kind": "posts",
                "posture": "hold",
                "recovery": "heal-tile",
                "posts": [
                  {"at": [9, {{{y}}}], "facing": "e"},
                  {"at": [10, {{{y + 1}}}], "facing": "n"},
                  {"at": [11, {{{y}}}], "facing": "s"}
                ]
              },
              "act": {
                "kind": "advance",
                "posture": "aggressive",
                "recovery": "fight-on",
                "path": [[12, {{{y}}}], [15, {{{y + 1}}}], [19, {{{y}}}], [22, {{{y + 1}}}]]
              },
              "sigs": {
                "stage": {"longshot": {"line": [[9, {{{y}}}], [13, {{{y}}}]]}},
                "act": {"kestrel": {"line": [[12, {{{y}}}], [16, {{{y}}}], [20, {{{y}}}]]}}
              }
            },
            {
              "id": "web",
              "name": "Web",
              "slots": [3, 4, 6],
              "coreRule": "grab",
              "stage": {
                "kind": "line",
                "posture": "observe",
                "recovery": "patch",
                "path": [[6, {{{y}}}], [7, {{{y + 2}}}], [8, {{{y + 4}}}]]
              },
              "act": {
                "kind": "patrol",
                "posture": "observe",
                "recovery": "patch",
                "path": [[6, {{{y}}}], [9, {{{y + 2}}}], [6, {{{y + 4}}}], [6, {{{y}}}]]
              },
              "sigs": {
                "stage": {
                  "mason": {"points": [[7, {{{y}}}], [8, {{{y + 1}}}]]},
                  "lantern": {"points": [[9, {{{y + 1}}}], [10, {{{y + 2}}}]]}
                }
              }
            },
            {
              "id": "keel",
              "name": "Keel",
              "slots": [5, 7],
              "coreRule": "ignore",
              "stage": {
                "kind": "approach",
                "posture": "wait",
                "recovery": "heal-tile",
                "point": [20, {{{y + 1}}}]
              },
              "act": {
                "kind": "converge",
                "posture": "steal",
                "recovery": "fight-on",
                "point": [21, {{{y + 2}}}]
              }
            }
          ]
        }
        """;
    }

    private static void AssertGolden(string fileName, byte[] actual)
    {
        string path = Path.Combine(TacticsFixtureDirectory(), fileName);
        if (Environment.GetEnvironmentVariable(
                "BOTARENA_UPDATE_TACTICS_GOLDENS") == "true")
        {
            File.WriteAllBytes(path, actual);
            return;
        }
        Assert.True(File.Exists(path),
            $"Missing golden '{fileName}'; run with "
            + "BOTARENA_UPDATE_TACTICS_GOLDENS=true to mint it.");
        byte[] expected = File.ReadAllBytes(path);
        Assert.Equal(
            Encoding.UTF8.GetString(expected).ReplaceLineEndings("\n"),
            Encoding.UTF8.GetString(actual).ReplaceLineEndings("\n"));
    }

    private static byte[] Fixture(string name) => File.ReadAllBytes(
        Path.Combine(TacticsFixtureDirectory(), $"{name}.tactics.json"));

    private static string TacticsFixtureDirectory() => Path.Combine(
        Root(), "tests", "BotArena.Cli.Tests", "Fixtures", "tactics");

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
