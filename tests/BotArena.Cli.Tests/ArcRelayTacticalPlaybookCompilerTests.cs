using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayTacticalPlaybookCompilerTests
{
    private static readonly string[] CurrentSheets =
    [
        "hunter-tapline-v2",
        "wellwright-tapline-v2",
        "starter-tapline-v2",
    ];

    [Fact]
    public void EveryCurrentSheetCompilesDirectlyToAtl2()
    {
        foreach (string name in CurrentSheets)
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(Playbook(name));

            Assert.True(compilation.UsesOrderTree);
            Assert.Equal(
                ArcRelayTacticalPlaybookCompiler.DirectEnvelopeMagic,
                BitConverter.ToInt32(compilation.LinkedData));
            Assert.InRange(compilation.LinkedData.Length, 1, 65_535);
            Assert.Equal(8, compilation.Composition.Length);
            using JsonDocument normalized = JsonDocument.Parse(
                compilation.NormalizedPlaybook);
            Assert.Equal(
                [
                    "capacities", "conditions", "mapPlan", "roles", "schema",
                    "sharedOrders", "sheetId",
                ],
                normalized.RootElement.EnumerateObject()
                    .Select(value => value.Name)
                    .Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void FileAndInMemoryCompilationUseTheSamePath()
    {
        string sheetPath = Playbook("hunter-tapline-v2");
        JsonObject source = Source("hunter-tapline-v2");
        string mapPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sheetPath)!,
            source["mapPlan"]!.GetValue<string>()));

        TacticalPlaybookCompilation fromFile =
            ArcRelayTacticalPlaybookCompiler.Compile(sheetPath);
        TacticalPlaybookCompilation inMemory =
            ArcRelayTacticalPlaybookCompiler.Compile(
                File.ReadAllBytes(sheetPath),
                File.ReadAllBytes(mapPath),
                sheetPath,
                mapPath);

        Assert.Equal(fromFile.LinkedData, inMemory.LinkedData);
        Assert.Equal(fromFile.NormalizedPlaybook, inMemory.NormalizedPlaybook);
        Assert.Equal(fromFile.NormalizedLayout, inMemory.NormalizedLayout);
    }

    [Fact]
    public void RetiredDoctrineGrammarIsRejectedInsteadOfTranslated()
    {
        JsonObject source = Source("starter-tapline-v2");
        source["doctrines"] = new JsonObject();

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));

        Assert.Contains("unknown field 'doctrines'", error.Message);
    }

    [Fact]
    public void CompositionComesOnlyFromExactRoleClasses()
    {
        JsonObject source = Source("starter-tapline-v2");
        source["roles"]![0]!["classes"]!.AsArray().RemoveAt(0);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));

        Assert.Contains("exactly eight bodies", error.Message);
        Assert.Null(source["composition"]);
    }

    [Fact]
    public void MixedClassRoleMayAuthorOneUseOrderPerOwnedSkill()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject home = source["roles"]![0]!.AsObject();
        JsonArray orders = home["orders"]!.AsArray();
        orders.Insert(0, new JsonObject
        {
            ["id"] = "relay-pass",
            ["when"] = "signature-ready",
            ["use"] = new JsonObject
            {
                ["skill"] = "arc-toss",
                ["target"] = "own-reactor",
            },
        });
        orders.Insert(1, new JsonObject
        {
            ["id"] = "palisade-wall",
            ["when"] = "signature-ready",
            ["use"] = new JsonObject
            {
                ["skill"] = "prism-wall",
                ["target"] = "nearest-enemy",
            },
        });
        source["conditions"]!["signature-ready"] = new JsonObject
        {
            ["fact"] = "signature-ready",
            ["operator"] = "equals",
            ["value"] = 1,
        };

        TacticalPlaybookCompilation compilation = Compile(
            source, MapPlan("starter-tapline-v2"));

        Assert.True(compilation.UsesOrderTree);
    }

    [Fact]
    public void MixedClassRoleRejectsSkillOwnedByNoBodyInRole()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject home = source["roles"]![0]!.AsObject();
        home["orders"]!.AsArray().Insert(0, new JsonObject
        {
            ["id"] = "unowned-repair",
            ["use"] = new JsonObject
            {
                ["skill"] = "repair-beam",
                ["target"] = "weakest-ally",
            },
        });

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));

        Assert.Contains("no body in role 'home-team' owns 'repair-beam'",
            error.Message);
    }

    [Fact]
    public void SelfInZoneIsAnAuthoredBodyLocalSpatialCondition()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject map = MapPlan("starter-tapline-v2");
        map["zones"]!["test-area"] = new JsonObject
        {
            ["rect"] = new JsonArray(1, 1, 29, 25),
        };
        source["conditions"]!["inside-battlefield"] = new JsonObject
        {
            ["fact"] = "self-in-zone",
            ["operator"] = "equals",
            ["value"] = 1,
            ["zone"] = "test-area",
        };
        JsonObject lastHomeOrder = source["roles"]![0]!["orders"]!
            .AsArray().Last()!.AsObject();
        lastHomeOrder["when"] = "inside-battlefield";

        TacticalPlaybookCompilation compilation = Compile(
            source, map);

        Assert.True(compilation.UsesOrderTree);
    }

    [Fact]
    public void VisibleEnemyDistanceIsBodyLocalAndBoundedToTheMapScale()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject condition = source["conditions"]!["enemy-nearby"]!
            .AsObject();

        Assert.Equal("self-nearest-visible-enemy-distance",
            condition["fact"]!.GetValue<string>());
        Assert.True(Compile(
            source, MapPlan("starter-tapline-v2")).UsesOrderTree);

        condition["value"] = 32;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));
        Assert.Contains("visible-enemy distance must be in [0, 31]",
            error.Message);
    }

    [Fact]
    public void SharedOrdersAndCapacitiesRequireHonestReuse()
    {
        JsonObject source = Source("starter-tapline-v2");
        foreach (JsonObject role in source["roles"]!.AsArray()
                     .Skip(1).Select(value => value!.AsObject()))
        {
            JsonArray orders = role["orders"]!.AsArray();
            JsonNode? reference = orders.Single(value =>
                value!["shared"]?.GetValue<string>() == "collect-loose-core");
            orders.Remove(reference);
        }

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));

        Assert.Contains("shared order needs at least two", error.Message);
    }

    [Fact]
    public void ConditionTyposFailAtTheAuthoredOrder()
    {
        JsonObject source = Source("starter-tapline-v2");
        source["sharedOrders"]!["score-a-core"]!["when"] = "carying-core";

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));

        Assert.Contains("unknown condition 'carying-core'", error.Message);
    }

    [Fact]
    public void UrgencyIsOptionalOnBranchesAndLeavesButHasOnlyThreeWords()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject first = source["roles"]![0]!["orders"]!
            .AsArray().Last()!.AsObject();
        first["urgency"] = "urgent";

        Assert.True(Compile(
            source, MapPlan("starter-tapline-v2")).UsesOrderTree);

        first["urgency"] = "critical";
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(source, MapPlan("starter-tapline-v2")));
        Assert.Contains("urgency", error.Message);
    }

    [Fact]
    public void MapPlansRejectDecorativeUnusedGeometry()
    {
        JsonObject map = MapPlan("starter-tapline-v2");
        map["anchors"]!["unused-pin"] = new JsonArray(6, 8);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            Compile(Source("starter-tapline-v2"), map));

        Assert.Contains("anchor 'unused-pin' has no sheet consumer", error.Message);
    }

    [Fact]
    public void ExplainPreservesAuthoredOrdersAndAtl2()
    {
        string output = Directory.CreateTempSubdirectory(
            "arc-relay-orders-explain").FullName;
        try
        {
            Assert.Equal(0, ArcRelayTacticalPlaybookCommand.Run(
            [
                "--playbook", Playbook("hunter-tapline-v2"),
                "--out", output,
            ]));
            using JsonDocument explain = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(output, "explain.json")));

            Assert.Equal("arc-relay-orders-explain-v2",
                explain.RootElement.GetProperty("schema").GetString());
            Assert.Equal("ATL2", explain.RootElement
                .GetProperty("runtimePackage").GetProperty("envelope")
                .GetString());
            Assert.Contains(explain.RootElement.GetProperty("roles")
                .EnumerateArray(), role => role.GetProperty("id")
                .GetString() == "hunter");
            Assert.Equal(
                "orders:<role>:<authored-order-path>:<action>:<reason>",
                explain.RootElement.GetProperty("commandProvenance").GetString());
            Assert.True(File.Exists(Path.Combine(
                output, "normalized-map-plan.json")));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static TacticalPlaybookCompilation Compile(
        JsonObject source,
        JsonObject mapPlan) => ArcRelayTacticalPlaybookCompiler.Compile(
        Encoding.UTF8.GetBytes(source.ToJsonString()),
        Encoding.UTF8.GetBytes(mapPlan.ToJsonString()));

    private static JsonObject Source(string name) => JsonNode.Parse(
        File.ReadAllBytes(Playbook(name)))!.AsObject();

    private static JsonObject MapPlan(string name) => JsonNode.Parse(
        File.ReadAllBytes(Path.Combine(
            Root(), "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "map-plans",
            $"{name}.json")))!.AsObject();

    private static string Playbook(string name) => Path.Combine(
        Root(), "arena-bots", "arc-relay",
        "tactical-playbook-v1-2026-08-03", "playbooks", $"{name}.json");

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
