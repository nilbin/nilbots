using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Engine;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayTacticalPlaybookCompilerTests
{
    private static readonly string[] CurrentSheets =
    [
        "starter-tapline-v2",
        "tempered-standard-v2",
        "relay-circuit-v1",
        "crowned-rail-v1",
        "lockbox-v1",
        "demolition-march-v1",
        "rising-standard-v1",
        "wavebreak-v1",
        "idle-field-v1",
    ];

    [Fact]
    public void EveryCurrentSheetCompilesDirectlyToAtl2()
    {
        foreach (string name in CurrentSheets)
        {
            using JsonDocument source = JsonDocument.Parse(
                File.ReadAllBytes(Playbook(name)));
            Assert.Equal(
                $"../map-plans/tapline-6/{name}.json",
                source.RootElement.GetProperty("mapPlan").GetString());

            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(Playbook(name));

            Assert.True(compilation.UsesOrderTree);
            Assert.Equal(
                ArcRelayTacticalPlaybookCompiler.DirectEnvelopeMagic,
                BitConverter.ToInt32(compilation.LinkedData));
            Assert.InRange(compilation.LinkedData.Length, 1, 65_535);
            Assert.Equal(8, compilation.Composition.Length);
            using JsonDocument layout = JsonDocument.Parse(
                compilation.NormalizedLayout);
            Assert.Equal(
                ArcRelayLoopProfile.Current.MapId,
                layout.RootElement.GetProperty("mapId").GetString());
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

    [Theory]
    [InlineData("tapline-two-heists-v1")]
    [InlineData("tapline-turncoat-intercept-v1")]
    [InlineData("tapline-crowned-rail-v1")]
    [InlineData("tapline-emergent-standard-v1")]
    [InlineData("tapline-cutoff-execution-v1")]
    [InlineData("tapline-pre-wave-evacuation-v1")]
    [InlineData("tapline-double-mason-lockbox-v1")]
    [InlineData("tapline-demolition-march-v1")]
    [InlineData("tapline-tempered-standard-v1")]
    [InlineData("tapline-iron-interval-v1")]
    [InlineData("tapline-twin-relay-circuit-v1")]
    [InlineData("tapline-depth-relay-circuit-v1")]
    public void TaplineDepthCandidatesCompileAsCompactDirectOrders(
        string name)
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(DepthCandidate(name));

        Assert.True(compilation.UsesOrderTree);
        Assert.Equal(8, compilation.Composition.Length);
        Assert.InRange(compilation.Composition.Distinct().Count(), 7, 8);
        Assert.InRange(compilation.LinkedData.Length, 1, 8_192);
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
    public void ArcTossMayRequireItsNamedReceiverToBeAheadOnAConsumedRoute()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject map = MapPlan("starter-tapline-v2");
        map["routes"]!["relay-return"] = new JsonObject
        {
            ["waypoints"] = new JsonArray
            {
                new JsonArray(19, 20),
                new JsonArray(18, 19),
                new JsonArray(2, 5),
            },
        };
        JsonObject home = source["roles"]![0]!.AsObject();
        JsonObject toss = new()
        {
            ["id"] = "forward-relay-toss",
            ["when"] = "carrying-core",
            ["use"] = new JsonObject
            {
                ["skill"] = "arc-toss",
                ["target"] = new JsonObject
                {
                    ["role"] = "centre-team",
                    ["aheadVia"] = "relay-return",
                    ["minimumForwardTiles"] = 4,
                },
            },
        };
        home["orders"]!.AsArray().Insert(0, toss);

        Assert.True(Compile(source, map).UsesOrderTree);

        toss["use"]!["target"]!.AsObject()
            .Remove("minimumForwardTiles");
        InvalidDataException incomplete = Assert.Throws<InvalidDataException>(
            () => Compile(source, map));
        Assert.Contains("needs both aheadVia and minimumForwardTiles",
            incomplete.Message);

        toss["use"]!["target"]!["minimumForwardTiles"] = 4;
        toss["use"]!["target"]!["aheadVia"] = "missing-route";
        InvalidDataException missing = Assert.Throws<InvalidDataException>(
            () => Compile(source, map));
        Assert.Contains("unknown route 'missing-route'", missing.Message);
    }

    [Fact]
    public void RouteForwardTargetIsKestrelOwnedCompleteAndTerminal()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject map = MapPlan("starter-tapline-v2");
        map["routes"]!["safe-return"] = new JsonObject
        {
            ["waypoints"] = new JsonArray
            {
                new JsonArray(24, 22),
                new JsonArray(20, 22),
                new JsonArray(16, 22),
            },
        };
        JsonObject far = source["roles"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(role => role["id"]!.GetValue<string>() == "far-team");
        JsonObject dash = new()
        {
            ["id"] = "forward-route-dash",
            ["use"] = new JsonObject
            {
                ["skill"] = "vector-dash",
                ["target"] = new JsonObject
                {
                    ["route"] = "safe-return",
                    ["progress"] = "forward",
                    ["minimumForwardTiles"] = 2,
                },
            },
        };
        far["orders"]!.AsArray().Insert(0, dash);

        Assert.True(Compile(source, map).UsesOrderTree);

        dash["use"]!["target"]!.AsObject().Remove("progress");
        Assert.Contains("missing required field 'progress'",
            Assert.Throws<InvalidDataException>(() => Compile(source, map)).Message);
        dash["use"]!["target"]!["progress"] = "reverse";
        Assert.Contains("forward",
            Assert.Throws<InvalidDataException>(() => Compile(source, map)).Message);
        dash["use"]!["target"]!["progress"] = "forward";
        dash["use"]!["skill"] = "tractor-hook";
        Assert.Contains("cannot aim forward on a route",
            Assert.Throws<InvalidDataException>(() => Compile(source, map)).Message);
        dash["use"]!["skill"] = "vector-dash";
        map["routes"]!["safe-return"]!["waypoints"] = new JsonArray
        {
            new JsonArray(24, 22),
            new JsonArray(20, 22),
            new JsonArray(24, 22),
        };
        Assert.Contains("must name a terminal route",
            Assert.Throws<InvalidDataException>(() => Compile(source, map)).Message);
    }

    [Fact]
    public void SignatureCommitmentFactAndIntentionalHoldAreBodyLocalWords()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject home = source["roles"]![0]!.AsObject();
        source["conditions"]!["own-signature-committed"] = new JsonObject
        {
            ["fact"] = "signature-commitment-active",
            ["operator"] = "equals",
            ["value"] = 1,
        };
        home["orders"]!.AsArray().Insert(0, new JsonObject
        {
            ["id"] = "preserve-signature-commitment",
            ["when"] = "own-signature-committed",
            ["move"] = new JsonObject { ["to"] = "current-position" },
        });

        Assert.True(Compile(source, MapPlan("starter-tapline-v2")).UsesOrderTree);

        source["conditions"]!["own-signature-committed"]!["value"] = 2;
        Assert.Contains("compares against 0 or 1",
            Assert.Throws<InvalidDataException>(() =>
                Compile(source, MapPlan("starter-tapline-v2"))).Message);
    }

    [Fact]
    public void PlaceableConstructMayTargetAnExactConsumedAnchor()
    {
        JsonObject source = Source("hunter-tapline-v2");
        JsonObject map = MapPlan("hunter-tapline-v2");
        JsonObject builder = source["roles"]!.AsArray().Select(value =>
                value!.AsObject())
            .Single(role => role["id"]!.GetValue<string>() == "builder");
        JsonObject[] orders = builder["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .ToArray();
        JsonObject use = orders.Single(order => order["id"]?.GetValue<string>()
                == "fortify-enemy-lane-gate")["use"]!.AsObject();
        JsonObject move = orders.Single(order => order["id"]?.GetValue<string>()
                == "stage-at-enemy-lane-gate")["move"]!.AsObject();

        Assert.Equal("enemy-lane-gate", use["at"]!.GetValue<string>());
        Assert.Equal("enemy-lane-gate",
            move["to"]!["adjacentTo"]!.GetValue<string>());
        Assert.Single(map["anchors"]!.AsObject());
        Assert.True(Compile(source, map).UsesOrderTree);

        source = Source("hunter-tapline-v2");
        map = MapPlan("hunter-tapline-v2");
        builder = source["roles"]!.AsArray().Select(value => value!.AsObject())
            .Single(role => role["id"]!.GetValue<string>() == "builder");
        use = builder["orders"]!.AsArray().Select(value => value!.AsObject())
            .Single(order => order["id"]?.GetValue<string>()
                == "fortify-enemy-lane-gate")["use"]!.AsObject();
        use["at"] = "missing-anchor";

        InvalidDataException missing = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("unknown anchor 'missing-anchor'", missing.Message);

        source = Source("hunter-tapline-v2");
        map = MapPlan("hunter-tapline-v2");
        builder = source["roles"]!.AsArray().Select(value => value!.AsObject())
            .Single(role => role["id"]!.GetValue<string>() == "builder");
        move = builder["orders"]!.AsArray().Select(value => value!.AsObject())
            .Single(order => order["id"]?.GetValue<string>()
                == "stage-at-enemy-lane-gate")["move"]!.AsObject();
        move["to"]!["adjacentTo"] = "missing-anchor";

        missing = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("unknown anchor 'missing-anchor'", missing.Message);

        source = Source("hunter-tapline-v2");
        map = MapPlan("hunter-tapline-v2");
        builder = source["roles"]!.AsArray().Select(value => value!.AsObject())
            .Single(role => role["id"]!.GetValue<string>() == "builder");
        use = builder["orders"]!.AsArray().Select(value => value!.AsObject())
            .Single(order => order["id"]?.GetValue<string>()
                == "fortify-enemy-lane-gate")["use"]!.AsObject();
        use["skill"] = "repair-beam";
        use["target"] = "weakest-ally";
        builder["classes"] = new JsonArray("patchbay");

        InvalidDataException wrongSkill = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("does not place a construct at an anchor",
            wrongSkill.Message);
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
    public void DepthPackFactsAndTargetsStayInsideTheOrdersGrammar()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject map = MapPlan("starter-tapline-v2");
        JsonObject centre = source["roles"]!.AsArray().Select(value =>
                value!.AsObject())
            .Single(role => role["id"]!.GetValue<string>() == "centre-team");
        JsonArray orders = centre["orders"]!.AsArray();
        orders.Insert(0, new JsonObject
        {
            ["id"] = "support-authored-ace",
            ["when"] = "ace-exists",
            ["use"] = new JsonObject
            {
                ["skill"] = "repair-beam",
                ["target"] = new JsonObject { ["role"] = "home-team" },
            },
        });
        orders.Insert(1, new JsonObject
        {
            ["id"] = "rally-to-emergent-ace",
            ["move"] = new JsonObject
            {
                ["follow"] = new JsonObject
                {
                    ["selector"] = "most-experienced-ally",
                },
            },
        });
        orders.Insert(2, new JsonObject
        {
            ["id"] = "clear-veteran-or-deployment",
            ["fight"] = new JsonObject
            {
                ["targets"] = new JsonArray("veteran", "deployment", "nearest"),
                ["style"] = "advance",
                ["chaseLimit"] = 7,
            },
        });
        source["conditions"]!["ace-exists"] = new JsonObject
        {
            ["fact"] = "role-highest-level",
            ["operator"] = "greater-than",
            ["value"] = 1,
            ["role"] = "home-team",
        };

        Assert.True(Compile(source, map).UsesOrderTree);

        string[] bodyAndTeamFacts =
        [
            "self-carried-core-bank-value",
            "enemy-reactor-charge",
            "reactor-pulses",
            "live-enemies",
            "friendly-respawns-next-wave",
            "enemy-respawns-next-wave",
            "ticks-until-respawn-wave",
            "deployment-active",
            "deployment-lifetime-percent",
            "deployment-durability-percent",
        ];
        foreach (string fact in bodyAndTeamFacts)
        {
            source = Source("starter-tapline-v2");
            JsonObject last = source["roles"]![0]!["orders"]!
                .AsArray().Last()!.AsObject();
            source["conditions"]!["candidate-fact"] = new JsonObject
            {
                ["fact"] = fact,
                ["operator"] = "at-least",
                ["value"] = 0,
            };
            last["when"] = "candidate-fact";
            Assert.True(Compile(source, MapPlan("starter-tapline-v2"))
                .UsesOrderTree);
        }
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
        Assert.Contains("firing-approach distance must be in [0, 31]",
            error.Message);
    }

    [Fact]
    public void DestinationFacingIsNarrowAndConsumesItsAnchor()
    {
        JsonObject source = Source("lockbox-v1");
        JsonObject map = MapPlan("lockbox-v1");
        map["anchors"]!["facing-destination"] = new JsonArray(14, 13);
        map["anchors"]!["facing-target"] = new JsonArray(18, 13);
        JsonObject move = source["roles"]![1]!["orders"]!.AsArray()
            .Last()!["move"]!.AsObject();
        move["to"] = new JsonObject { ["anchor"] = "facing-destination" };
        move["face"] = new JsonObject { ["anchor"] = "facing-target" };

        Assert.True(Compile(source, map).UsesOrderTree);

        move["to"] = "own-reactor";
        InvalidDataException dynamic = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("authored zone or anchor", dynamic.Message);

        source = Source("crowned-rail-v1");
        map = MapPlan("crowned-rail-v1");
        JsonObject patrol = source["roles"]![0]!["orders"]!.AsArray()
            .Last()!["move"]!.AsObject();
        patrol["face"] = new JsonObject { ["anchor"] = "north-rail-entry" };
        InvalidDataException route = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("face is legal only on move.to", route.Message);
    }

    [Theory]
    [InlineData("observed-enemy-entries-in-zone")]
    [InlineData("distinct-enemy-lives-observed-in-zone")]
    [InlineData("confirmed-enemy-destructions-in-zone")]
    [InlineData("zone-fully-observed-throughout-interval")]
    public void HistoricalEvidenceRequiresOneBoundedInterval(string fact)
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject map = MapPlan("starter-tapline-v2");
        map["zones"]!["watched-gate"] = new JsonObject
        {
            ["rect"] = new JsonArray(14, 12, 16, 14),
        };
        JsonObject condition = new()
        {
            ["fact"] = fact,
            ["zone"] = "watched-gate",
            ["windowTicks"] = 90,
            ["operator"] = "at-least",
            ["value"] = 0,
        };
        source["conditions"]!["gate-history"] = condition;
        source["roles"]![0]!["orders"]!.AsArray().Last()!["when"] =
            "gate-history";

        Assert.True(Compile(source, map).UsesOrderTree);

        condition.Remove("windowTicks");
        InvalidDataException none = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("exactly one of windowTicks or since", none.Message);

        condition["windowTicks"] = 90;
        condition["since"] = "match-start";
        InvalidDataException both = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("exactly one of windowTicks or since", both.Message);
    }

    [Fact]
    public void NodeEntryEvidenceIsLegalOnlyFromAnUntilExpression()
    {
        JsonObject source = Source("starter-tapline-v2");
        JsonObject map = MapPlan("starter-tapline-v2");
        map["zones"]!["watched-gate"] = new JsonObject
        {
            ["rect"] = new JsonArray(14, 12, 16, 14),
        };
        source["conditions"]!["episode-kills"] = new JsonObject
        {
            ["fact"] = "confirmed-enemy-destructions-in-zone",
            ["zone"] = "watched-gate",
            ["since"] = "node-entry",
            ["operator"] = "at-least",
            ["value"] = 2,
        };
        JsonObject order = source["roles"]![0]!["orders"]!.AsArray()
            .Last()!.AsObject();
        order["until"] = "episode-kills";

        Assert.True(Compile(source, map).UsesOrderTree);

        order.Remove("until");
        order["when"] = "episode-kills";
        InvalidDataException when = Assert.Throws<InvalidDataException>(() =>
            Compile(source, map));
        Assert.Contains("legal only in until", when.Message);
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

    private static JsonObject MapPlan(string name)
    {
        string directory = Path.Combine(
            Root(), "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "map-plans");
        string current = Path.Combine(directory, "tapline-6", $"{name}.json");
        string path = File.Exists(current)
            ? current
            : Path.Combine(directory, $"{name}.json");
        return JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
    }

    private static string Playbook(string name) => Path.Combine(
        Root(), "arena-bots", "arc-relay",
        "tactical-playbook-v1-2026-08-03", "playbooks", $"{name}.json");

    private static string DepthCandidate(string name) => Path.Combine(
        Root(), "arena-bots", "arc-relay",
        "tactical-playbook-v1-2026-08-03", "strategy-depth", name,
        "orders.json");

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
