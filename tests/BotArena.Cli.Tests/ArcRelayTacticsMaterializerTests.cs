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
    public void RelayDeliveryMaterializesSoloTossThenOrdinaryReclaim()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("deliver-core"));
        string orders = Encoding.UTF8.GetString(compilation.MaterializedOrders!);

        int reclaim = orders.IndexOf(
            "\"id\": \"t1-p1-g1-reclaim-toss\"", StringComparison.Ordinal);
        int toss = orders.IndexOf(
            "\"id\": \"t1-p1-g1-pass\"", StringComparison.Ordinal);
        Assert.True(reclaim >= 0 && toss > reclaim);
        Assert.Contains("\"to\": \"own-tossed-core\"", orders);
        Assert.Contains("\"skill\": \"arc-toss\"", orders);
        Assert.Contains("\"target\": \"own-reactor\"", orders);
        Assert.DoesNotContain(
            "\"target\": \"nearest-ally-toward-own-reactor\"", orders);
    }

    [Fact]
    public void ASingleMasonMarkIsACompleteHardlightPlan()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        string orders = Encoding.UTF8.GetString(compilation.MaterializedOrders!);

        // Pincer's Mason authors exactly one placement. The catalog limit of
        // two is a maximum of alternatives, never a required pair.
        Assert.Contains("\"id\": \"t1-p2-g2-s3-mark-0\"", orders);
        Assert.Contains("\"skill\": \"hardlight-block\"", orders);
        Assert.DoesNotContain("t1-p2-g2-s3-mark-1", orders);
    }

    [Theory]
    [InlineData("mason", "hardlight-block")]
    [InlineData("minesmith", "trip-node")]
    [InlineData("nest", "sentinel-seed")]
    public void ExactDurableMarksCarryTheirOwnDeploymentIntent(
        string classId,
        string skillId)
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["squad"]![3]!["class"] = classId;

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement role = orders.RootElement.GetProperty("roles")
            .EnumerateArray().Single(value =>
                value.GetProperty("classes").EnumerateArray()
                    .Any(item => item.GetString() == classId));
        JsonElement use = Descendants(role)
            .Where(node => node.ValueKind == JsonValueKind.Object
                && node.TryGetProperty("use", out _))
            .Select(node => node.GetProperty("use"))
            .Single(value => value.GetProperty("skill").GetString()
                == skillId);

        Assert.Equal("self", use.GetProperty("target").GetString());
        Assert.True(use.TryGetProperty("at", out _));
    }

    [Fact]
    public void SmartLongshotPatrolFiresWithoutAHiddenRailStation()
    {
        JsonObject document = JsonNode.Parse(Fixture("patrol"))!.AsObject();
        document["tactics"]![0]!["groups"]![0]!["act"]!["posture"] =
            "aggressive";
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        string orders = Encoding.UTF8.GetString(compilation.MaterializedOrders!);

        Assert.Contains("\"skill\": \"rail-line\"", orders);
        Assert.Contains("\"patrol\":", orders);
        // A closed patrol has no single forward end. Smart Dash waits for the
        // player to draw its own terminal signature lane instead of emitting
        // an invalid loop target.
        Assert.DoesNotContain("smart-dash", orders);
        Assert.DoesNotContain("rail-move", orders);
        Assert.DoesNotContain("rail-station", orders);
    }

    [Fact]
    public void SmartLongshotUsesSignatureReachInsteadOfGenericCombatRange()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement[] rails = Descendants(orders.RootElement)
            .Where(node => node.ValueKind == JsonValueKind.Object
                && node.TryGetProperty("use", out JsonElement use)
                && use.GetProperty("skill").GetString() == "rail-line")
            .ToArray();

        Assert.NotEmpty(rails);
        Assert.All(rails, rail =>
        {
            string[] conditions = rail.GetProperty("when").GetProperty("all")
                .EnumerateArray().Select(value => value.GetString()!).ToArray();
            Assert.Contains("signature-ready", conditions);
            Assert.DoesNotContain("enemy-nearby", conditions);
        });
    }

    [Fact]
    public void RetiredLongshotRayMigratesToContextualUse()
    {
        using JsonDocument migrated = JsonDocument.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(Fixture("pincer")));
        JsonElement plan = migrated.RootElement.GetProperty("tactics")[0]
            .GetProperty("phases")[0].GetProperty("jobs")
            .GetProperty("mid-guard").GetProperty("sigs")
            .GetProperty("2");

        Assert.Equal("smart", plan.GetProperty("policy").GetString());
        Assert.False(plan.TryGetProperty("line", out _));

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        string orders = Encoding.UTF8.GetString(compilation.MaterializedOrders!);
        Assert.Contains("\"skill\": \"rail-line\"", orders);
        Assert.DoesNotContain("rail-move", orders);
        Assert.DoesNotContain("rail-station", orders);
    }

    [Fact]
    public void MaximalDocumentStaysInsideTheRuntimeEnvelope()
    {
        // Eight tactics, three groups each, every feature on every tactic:
        // the 160 KiB ATL2 envelope and the 128-entry map-plan tables are the
        // binding budgets, and Compile throws past either — so a clean
        // compile IS the budget assertion.
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                MaximalDocument(everyFeatureEverywhere: true));
        Assert.True(compilation.LinkedData.Length <= 160 * 1024);
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
    public void VeteranTransitionTargetsSelectedRosterSlots()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!
            ["required"]!["veterancy"] = JsonNode.Parse(
                """{"level":3,"slots":[0,3],"count":2}""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        string orders = Encoding.UTF8.GetString(
            compilation.MaterializedOrders!);

        Assert.Contains("\"fact\": \"friendly-veterans-in-roles\"", orders);
        Assert.Contains("\"level\": 3", orders);
        Assert.Contains("\"operator\": \"at-least\"", orders);
        Assert.Contains("\"value\": 2", orders);
        Assert.Contains("\"vex\"", orders);
        Assert.Contains("\"flux\"", orders);
    }

    [Fact]
    public void SeatRelativeWorkshopChoiceMaterializesForMoveAndEvidence()
    {
        string source = File.ReadAllText(Path.Combine(
            Root(), "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "tactics",
            "tempered-broadloom-v1.json"));
        JsonObject document = JsonNode.Parse(source)!.AsObject();
        JsonObject phase = document["tactics"]![0]!["phases"]![0]!.AsObject();
        phase["jobs"]!["trainee"]!["workshop"] = "own";
        phase["continue"] = JsonNode.Parse(
            """[{"required":{"workshopDone":{"workshop":"own","count":2}}}]""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        string orders = Encoding.UTF8.GetString(
            compilation.MaterializedOrders!);

        Assert.Contains("\"workshop\": \"own\"", orders);
        Assert.Contains("\"fact\": \"friendly-workshop-completions\"", orders);
    }

    [Fact]
    public void PublicEnemyTrainingCanAbortAnActiveTactic()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["abort"] = JsonNode.Parse(
            """{"cases":[{"required":{"workshopTraining":{"workshop":"enemy","team":"enemy"}}}]}""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);

        JsonElement abort = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("abort").EnumerateArray().Single();
        string conditionId = abort.GetProperty("all").EnumerateArray()
            .Single().GetString()!;
        JsonElement condition = orders.RootElement.GetProperty("conditions")
            .GetProperty(conditionId);
        Assert.Equal("workshop-training-enemy",
            condition.GetProperty("fact").GetString());
        Assert.Equal("enemy", condition.GetProperty("workshop").GetString());
    }

    [Fact]
    public void PostFacingMaterializesAsCardinalIntentWithoutFakeGeometry()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        string orders = Encoding.UTF8.GetString(
            compilation.MaterializedOrders!);
        string mapPlan = Encoding.UTF8.GetString(
            compilation.MaterializedMapPlan!);

        Assert.Contains("\"face\": {", orders);
        Assert.Contains("\"direction\": \"e\"", orders);
        Assert.DoesNotContain("-face-", orders);
        Assert.DoesNotContain("-face-", mapPlan);
    }

    [Fact]
    public void ReleaseOrsSeveralTriggerRowsInsideTheReadinessGate()
    {
        // Two ticked release rows OR together in the transition case's any()
        // side, beside the all() side's exact per-bot readiness pairs.
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        document["tactics"]![0]!["release"] = JsonNode.Parse(
            """
            {"mode":"ready-when","conds":{
              "enemiesAt":{"count":2,"zone":{"rect":[13,11,17,15]}},
              "contact":true}}
            """);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement when = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("phases").EnumerateArray().First()
            .GetProperty("continue").EnumerateArray().Single()
            .GetProperty("when");
        // Formation readiness is one executor fact. It freezes the phase-entry
        // lives and counts how many of that cohort remain late, rather than
        // expanding eight subtly different role/zone predicates.
        JsonElement[] pairs = when.GetProperty("all").EnumerateArray().ToArray();
        Assert.Single(pairs);
        Assert.Equal("c-t1-p1-formation-ready", pairs[0].GetString());
        JsonElement formation = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-p1-formation-ready");
        Assert.Equal("formation-late-count",
            formation.GetProperty("fact").GetString());
        Assert.Equal("at-most", formation.GetProperty("operator").GetString());
        Assert.Equal(0, formation.GetProperty("value").GetInt32());
        Assert.Equal(8, formation.GetProperty("roles").GetArrayLength());
        Assert.Equal(8, formation.GetProperty("zones").GetArrayLength());
        string[] rows = when.GetProperty("any").EnumerateArray()
            .Select(entry => entry.GetString()!).ToArray();
        Assert.Equal(["c-t1-p1-enemies", "enemy-contact"], rows);
        // No rejoin anywhere: the coordinator's squad-level phase makes the
        // per-body release latch — and its rejoin escape — unnecessary.
        Assert.DoesNotContain(
            "\"rejoin\"",
            Encoding.UTF8.GetString(compilation.MaterializedOrders!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FormationReadinessMaterializesTheAuthoredLateBodyAllowance(
        int allowedLate)
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!["required"]!
            ["groupsSet"] = JsonNode.Parse(
                $$"""{"allowedLate":{{allowedLate}}}""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement formation = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-p1-formation-ready");
        Assert.Equal(allowedLate, formation.GetProperty("value").GetInt32());
    }

    [Fact]
    public void FormationReadinessRejectsMoreThanTwoLateBodies()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!["required"]!
            ["groupsSet"] = JsonNode.Parse("""{"allowedLate":3}""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("allowedLate", error.Message);
    }

    [Fact]
    public void FormationReadinessAcceptsGeometryCarriedFromThePreviousPhase()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject tactic = document["tactics"]![0]!.AsObject();
        JsonObject act = tactic["phases"]![1]!.AsObject();
        JsonObject jobs = act["jobs"]!.AsObject();
        foreach (KeyValuePair<string, JsonNode?> entry in jobs.ToArray())
        {
            JsonObject previous = entry.Value!.AsObject();
            jobs[entry.Key] = new JsonObject
            {
                ["kind"] = "none",
                ["posture"] = previous["posture"]?.DeepClone() ?? "observe",
                ["recovery"] = previous["recovery"]?.DeepClone() ?? "fight-on",
            };
        }
        act["continue"]![0]!["required"] =
            new JsonObject { ["groupsSet"] = true };

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement actTransition = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().Single()
            .GetProperty("phases").EnumerateArray().ElementAt(1)
            .GetProperty("continue").EnumerateArray().Single();
        string readinessId = Descendants(actTransition.GetProperty("when"))
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)
            .Single(value => value.Contains("formation-ready", StringComparison.Ordinal));
        JsonElement formation = orders.RootElement.GetProperty("conditions")
            .GetProperty(readinessId);
        Assert.Equal(8, formation.GetProperty("roles").GetArrayLength());
        Assert.Equal(8, formation.GetProperty("zones").GetArrayLength());
    }

    [Fact]
    public void FormationReadinessRejectsAGroupSetWithNoGroundInAnyPhase()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject tactic = document["tactics"]![0]!.AsObject();
        foreach (JsonNode? phaseNode in tactic["phases"]!.AsArray())
        {
            JsonObject phase = phaseNode!.AsObject();
            JsonObject jobs = phase["jobs"]!.AsObject();
            jobs["mid-guard"] = phase == tactic["phases"]![0]
                ? new JsonObject
                {
                    ["kind"] = "return",
                    ["posture"] = "observe",
                    ["recovery"] = "fight-on",
                }
                : new JsonObject
                {
                    ["kind"] = "none",
                    ["posture"] = "observe",
                    ["recovery"] = "fight-on",
                };
        }
        tactic["phases"]![1]!["continue"]![0]!["required"] =
            new JsonObject { ["groupsSet"] = true };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("every group", error.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ZoneConditionsAcceptOneToThreeAreas(int count)
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonArray zones = new(Enumerable.Range(0, count)
            .Select(index => (JsonNode?)JsonNode.Parse(
                $$"""{"name":"area-{{index + 1}}","rect":[{{10 + index}},10,{{10 + index}},10]}"""))
            .ToArray());
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!["triggers"] =
            new JsonObject
            {
                ["enemiesAt"] = new JsonObject
                {
                    ["count"] = 2,
                    ["zones"] = zones,
                },
            };

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement condition = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-p1-enemies");
        Assert.Equal(count, condition.GetProperty("zones").GetArrayLength());
    }

    [Fact]
    public void IdenticalAreasCollapseIntoOneSemanticUnionMember()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!["triggers"] =
            new JsonObject
            {
                ["enemiesAt"] = new JsonObject
                {
                    ["count"] = 2,
                    ["zones"] = new JsonArray(
                        JsonNode.Parse("""{"name":"first","rect":[10,10,12,12]}"""),
                        JsonNode.Parse("""{"name":"clone","rect":[10,10,12,12]}""")),
                },
            };

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement condition = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-p1-enemies");
        Assert.Single(condition.GetProperty("zones").EnumerateArray());
    }

    [Fact]
    public void RecentlySeenEnemyAreasUseThePromisedTwentyTickWindow()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement condition = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-p1-enemies");
        Assert.Equal(20, condition.GetProperty("freshnessTicks").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void ZoneConditionsRejectEmptyOrOversizedAreaUnions(int count)
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonArray zones = new(Enumerable.Range(0, count)
            .Select(index => (JsonNode?)JsonNode.Parse(
                $$"""{"rect":[{{10 + index}},10,{{10 + index}},10]}"""))
            .ToArray());
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!["triggers"] =
            new JsonObject
            {
                ["enemiesAt"] = new JsonObject
                {
                    ["count"] = 2,
                    ["zones"] = zones,
                },
            };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("zones", error.Message);
    }

    [Fact]
    public void AVersionOneDocumentMigratesToTheProgramShape()
    {
        byte[] migrated = ArcRelayTacticalPlaybookCompiler.MigrateTactics(
            Fixture("pincer"));
        using JsonDocument document = JsonDocument.Parse(migrated);
        JsonElement root = document.RootElement;
        Assert.Equal("arc-relay-tactics-v2",
            root.GetProperty("schema").GetString());
        JsonElement tactic = root.GetProperty("tactics").EnumerateArray()
            .First();
        // Stage → phase 1, Release → its continue, Act → phase 2, Finish →
        // the final finish transition; groups shrink to membership.
        Assert.False(tactic.TryGetProperty("release", out _));
        Assert.False(tactic.TryGetProperty("finish", out _));
        JsonElement group = tactic.GetProperty("groups").EnumerateArray()
            .First();
        Assert.False(group.TryGetProperty("stage", out _));
        Assert.False(group.TryGetProperty("act", out _));
        JsonElement[] phases = tactic.GetProperty("phases").EnumerateArray()
            .ToArray();
        Assert.Equal(2, phases.Length);
        Assert.Equal("stage", phases[0].GetProperty("id").GetString());
        Assert.Equal("act", phases[1].GetProperty("id").GetString());
        JsonElement releaseRule = phases[0].GetProperty("continue")
            .EnumerateArray().Single();
        Assert.True(releaseRule.GetProperty("required")
            .GetProperty("groupsSet").GetBoolean());
        Assert.False(releaseRule.TryGetProperty("to", out _));
        JsonElement finishRule = phases[1].GetProperty("continue")
            .EnumerateArray().Single();
        Assert.False(finishRule.TryGetProperty("to", out _));
        // A canonical v2 document passes through migration byte-exactly.
        Assert.Equal(migrated,
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(migrated));
    }

    [Fact]
    public void EveryLegacyExplicitPhaseTargetNormalizesBeforeValidation()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonArray stageRules = document["tactics"]![0]!["phases"]![0]!
            ["continue"]!.AsArray();
        JsonObject template = stageRules[0]!.AsObject();
        stageRules.Clear();
        foreach (string target in new[] { "act", "act", "next" })
        {
            JsonObject rule = template.DeepClone().AsObject();
            rule["to"] = target;
            stageRules.Add(rule);
        }
        document["tactics"]![0]!["phases"]![1]!["continue"]![0]!["to"] =
            "finish";

        byte[] normalized = ArcRelayTacticalPlaybookCompiler.MigrateTactics(
            Encoding.UTF8.GetBytes(document.ToJsonString()));
        Assert.DoesNotContain("\"to\"", Encoding.UTF8.GetString(normalized));
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(normalized);
        Assert.True(compilation.UsesOrderTree);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("some-later-phase")]
    public void ObsoleteBackwardAndSkipTargetsReduceToSequentialBehavior(
        string obsoleteTarget)
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["phases"]![0]!["continue"]![0]!["to"] =
            obsoleteTarget;
        byte[] normalized = ArcRelayTacticalPlaybookCompiler.MigrateTactics(
            Encoding.UTF8.GetBytes(document.ToJsonString()));
        Assert.DoesNotContain("\"to\"", Encoding.UTF8.GetString(normalized));
        Assert.True(ArcRelayTacticalPlaybookCompiler
            .CompileTactics(normalized).UsesOrderTree);
    }

    [Fact]
    public void EventRowsAndTogetherThroughRequired()
    {
        // "Enemies gather AND a Core births soon": both rows in the case's
        // required side land in the program case's all(), with no trigger —
        // Required is the AND for every row, not just readiness.
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject rule = document["tactics"]![0]!["phases"]![0]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule.Remove("triggers");
        rule["required"] = JsonNode.Parse(
            """
            {"groupsSet":true,
             "enemiesAt":{"count":5,"zone":{"rect":[12,10,18,16]}},
             "coreSoon":{"well":"centre","ticks":20}}
            """);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement when = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("phases").EnumerateArray().First()
            .GetProperty("continue").EnumerateArray().Single()
            .GetProperty("when");
        Assert.False(when.TryGetProperty("any", out _));
        string[] names = when.GetProperty("all").EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetString()!)
            .ToArray();
        Assert.Contains("c-t1-p1-enemies", names);
        Assert.Contains("c-t1-p1-core-soon", names);
    }

    [Fact]
    public void TravelPostureHealedGateAndHealUpRecoveryMaterialize()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject stagePhase = document["tactics"]![0]!["phases"]![0]!
            .AsObject();
        // Flankers answer attacks on the way in but wait once staged; the
        // guards top up at the heal tile; the release also demands everyone
        // healed to 80%.
        JsonObject flankers = stagePhase["jobs"]!["flankers"]!.AsObject();
        flankers["travel"] = "hold";
        JsonObject guards = stagePhase["jobs"]!["mid-guard"]!.AsObject();
        guards["recovery"] = "heal-up";
        JsonObject rule = stagePhase["continue"]!.AsArray()[0]!.AsObject();
        rule["required"]!.AsObject()["healed"] = JsonNode.Parse(
            """{"percent":80}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);

        // The travel response answers attacks ONLY off the station: once a
        // flanker stands in its staging rect the leaf goes dark, and wait —
        // which mints no fight of its own — truly means quiet there. The
        // Answer-attacks is a bounded advance fight rather than standing
        // Hold: it answers any actionable contact but may move only one tile
        // from the episode's entry point. The episode remains latched until
        // contact ends, custody starts, or this group reaches its station.
        // The floor's march stance still follows travel (hold → line).
        JsonElement shared = orders.RootElement.GetProperty("sharedOrders");
        JsonElement travelFight = shared.GetProperty("t1-p1-g2-travel-fight");
        Assert.Equal("advance",
            travelFight.GetProperty("fight").GetProperty("style").GetString());
        Assert.Equal(1,
            travelFight.GetProperty("fight").GetProperty("chaseLimit").GetInt32());
        string[] travelWhen = travelFight.GetProperty("when").GetProperty("all")
            .EnumerateArray().Select(entry => entry.GetString()!).ToArray();
        Assert.Contains("c-t1-g2-off-station", travelWhen);
        Assert.Equal(
            ["carrying-core", "enemy-no-longer-nearby",
                "c-t1-g2-on-station"],
            travelFight.GetProperty("until").GetProperty("any")
                .EnumerateArray().Select(entry => entry.GetString()!).ToArray());
        JsonElement offStation = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-g2-off-station");
        Assert.Equal("self-in-zone", offStation.GetProperty("fact").GetString());
        Assert.Equal("equals", offStation.GetProperty("operator").GetString());
        Assert.Equal(0, offStation.GetProperty("value").GetInt32());
        Assert.False(shared.TryGetProperty("t1-p1-g2-fight", out _));
        JsonElement floor = shared.GetProperty("t1-p1-g2-move");
        Assert.Equal("line",
            floor.GetProperty("move").GetProperty("posture").GetString());

        // heal-up: any damage at all sends the bot to heal (≤99) and it
        // stays until completely full (≥100), so EVERY selectable healed
        // gate 1..100 is eventually satisfiable — including the 91, 98 and
        // 100 boundaries a 90/98 band would deadlock on. Carrying is an
        // explicit veto: custody bodies stay on their delivery/screen task.
        JsonElement healUp = shared.GetProperty("t1-p1-g1-heal-up");
        string[] healWhen = healUp.GetProperty("when").GetProperty("all")
            .EnumerateArray().Select(entry => entry.GetString()!).ToArray();
        Assert.Equal(["empty-handed", "below-topped-up"], healWhen);
        Assert.Equal("topped-up", healUp.GetProperty("until").GetString());
        // "Nearest" heal is nearest to the PHASE'S ground, not to the bot:
        // the move anchors on the guards' posts footprint (zone ids intern
        // by shape, so assert the rect).
        string nearZone = healUp.GetProperty("move")
            .GetProperty("near").GetString()!;
        using JsonDocument nearPlan = JsonDocument.Parse(
            compilation.MaterializedMapPlan!);
        int[] nearRect = nearPlan.RootElement.GetProperty("zones")
            .GetProperty(nearZone).GetProperty("rect").EnumerateArray()
            .Select(value => value.GetInt32()).ToArray();
        Assert.Equal([13, 11, 17, 15], nearRect);
        JsonElement conditionTable = orders.RootElement.GetProperty("conditions");
        int seekBelow = conditionTable.GetProperty("below-topped-up")
            .GetProperty("value").GetInt32();
        Assert.Equal("at-most", conditionTable.GetProperty("below-topped-up")
            .GetProperty("operator").GetString());
        int stayUntil = conditionTable.GetProperty("topped-up")
            .GetProperty("value").GetInt32();
        Assert.Equal("at-least", conditionTable.GetProperty("topped-up")
            .GetProperty("operator").GetString());
        foreach (int gate in new[] { 91, 98, 100 })
        {
            // A bot below the gate (≤ gate-1) must trigger the trip, and
            // the trip must not end before the gate is met.
            Assert.True(seekBelow >= gate - 1);
            Assert.True(stayUntil >= gate);
        }

        // healed 80 lands as one exact per-bot entry per squad slot.
        JsonElement when = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("phases").EnumerateArray().First()
            .GetProperty("continue").EnumerateArray().Single()
            .GetProperty("when");
        string[] names = when.GetProperty("all").EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetString()!)
            .Where(name => name.StartsWith("hp-", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(8, names.Length);
        Assert.Contains("hp-vex-80", names);
        JsonElement condition = orders.RootElement.GetProperty("conditions")
            .GetProperty("hp-vex-80");
        Assert.Equal("role-lowest-health-percent",
            condition.GetProperty("fact").GetString());
        Assert.Equal(80, condition.GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("hold")]
    [InlineData("aggressive")]
    public void MatchingTravelAndDestinationPosturesRemainDistinct(
        string posture)
    {
        // A posts job has two spatial states even when both behavior choices
        // have the same label: fight on the route while off-post, then fight
        // for the destination once on-post. Collapsing equal strings removes
        // the travel fight and makes an explicitly authored Brawl-through or
        // Respond-to-contact group silently march past enemies.
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject flankers = document["tactics"]![0]!["phases"]![0]!
            ["jobs"]!["flankers"]!.AsObject();
        flankers["posture"] = posture;
        flankers["travel"] = posture;

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement shared = orders.RootElement.GetProperty("sharedOrders");

        Assert.True(shared.TryGetProperty(
            "t1-p1-g2-travel-fight", out JsonElement travelFight));
        Assert.True(shared.TryGetProperty(
            "t1-p1-g2-fight", out JsonElement destinationFight));
        Assert.Contains(
            "c-t1-g2-off-station",
            travelFight.GetProperty("when").GetProperty("all")
                .EnumerateArray().Select(value => value.GetString()!));
        Assert.Contains(
            "c-t1-g2-on-station",
            destinationFight.GetProperty("when").GetProperty("all")
                .EnumerateArray().Select(value => value.GetString()!));
    }

    [Fact]
    public void HoldPosturePrefersCarriersAndPublishesItsBoundedDefenceLeash()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Fixture("hold-area"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement[] holdNodes = Descendants(orders.RootElement)
            .Where(value => value.ValueKind == JsonValueKind.Object
                && value.TryGetProperty("fight", out JsonElement fight)
                && fight.TryGetProperty("style", out JsonElement style)
                && style.GetString() == "hold"
                && fight.GetProperty("chaseLimit").GetInt32() == 5)
            .ToArray();

        Assert.NotEmpty(holdNodes);
        Assert.All(holdNodes, node =>
        {
            JsonElement fight = node.GetProperty("fight");
            Assert.Equal(5, fight.GetProperty("chaseLimit").GetInt32());
            Assert.Equal(
                ["carrier", "nearest", "weakest"],
                fight.GetProperty("targets").EnumerateArray()
                    .Select(value => value.GetString()!).ToArray());
            Assert.Equal(
                ["carrying-core", "enemy-no-longer-nearby"],
                node.GetProperty("until").GetProperty("any")
                    .EnumerateArray()
                    .Select(value => value.GetString()!).ToArray());
        });
        Assert.Contains(Descendants(orders.RootElement), node =>
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("fight", out JsonElement fight)
            && fight.GetProperty("chaseLimit").GetInt32() == 0
            && node.TryGetProperty("when", out JsonElement when)
            && when.ToString().Contains("carrying-core", StringComparison.Ordinal));
    }

    [Fact]
    public void QuietTravelGatesAMoversFightToItsDestination()
    {
        // "Quietly on the way, Aggressive there" on a converge: the march
        // must actually be quiet. Without travel the aggressive fight is
        // ungated (brawling in IS converge-aggressive); with travel "wait"
        // the fight gains a destination gate and no travel leaf exists.
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        TacticalPlaybookCompilation baseline =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument baselineOrders = JsonDocument.Parse(
            baseline.MaterializedOrders!);
        string[] baselineWhen = baselineOrders.RootElement
            .GetProperty("sharedOrders").GetProperty("t1-p2-g2-fight")
            .GetProperty("when").GetProperty("all").EnumerateArray()
            .Select(entry => entry.GetString()!).ToArray();
        Assert.DoesNotContain(baselineWhen,
            name => name.Contains("station", StringComparison.Ordinal));

        document["tactics"]![0]!["phases"]![1]!["jobs"]!["flankers"]!
            .AsObject()["travel"] = "wait";
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement shared = orders.RootElement.GetProperty("sharedOrders");
        Assert.False(shared.TryGetProperty("t1-p2-g2-travel-fight", out _));
        string[] gatedWhen = shared.GetProperty("t1-p2-g2-fight")
            .GetProperty("when").GetProperty("all").EnumerateArray()
            .Select(entry => entry.GetString()!).ToArray();
        Assert.Contains("c-t1-g2-on-station", gatedWhen);
        JsonElement onStation = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-g2-on-station");
        Assert.Equal("equals", onStation.GetProperty("operator").GetString());
        Assert.Equal(1, onStation.GetProperty("value").GetInt32());
        // The gate is the convergence point ±2 — the destination, not the
        // staging ground.
        string zoneId = onStation.GetProperty("zone").GetString()!;
        using JsonDocument mapPlan = JsonDocument.Parse(
            compilation.MaterializedMapPlan!);
        int[] rect = mapPlan.RootElement.GetProperty("zones")
            .GetProperty(zoneId).GetProperty("rect").EnumerateArray()
            .Select(value => value.GetInt32()).ToArray();
        Assert.Equal([13, 10, 17, 14], rect);
    }

    [Fact]
    public void UnlessRowsInvertIntoTheAllSide()
    {
        // "Continue when …, unless any enemy contact or a Core is up at
        // Centre": each unless row is one threshold condition, inverted by
        // an exact operator flip and ANDed in — none of them may hold.
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject rule = document["tactics"]![0]!["phases"]![0]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule["unless"] = JsonNode.Parse(
            """{"contact":true,"coreLoose":{"well":"centre"}}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement when = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("phases").EnumerateArray().First()
            .GetProperty("continue").EnumerateArray().Single()
            .GetProperty("when");
        string[] names = when.GetProperty("all").EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetString()!)
            .ToArray();
        Assert.Contains("no-enemy-contact", names);
        Assert.Contains("no-core-up-at-centre", names);
        JsonElement conditions = orders.RootElement.GetProperty("conditions");
        JsonElement noContact = conditions.GetProperty("no-enemy-contact");
        Assert.Equal("visible-enemies-in-zone",
            noContact.GetProperty("fact").GetString());
        Assert.Equal("at-most", noContact.GetProperty("operator").GetString());
        Assert.Equal(0, noContact.GetProperty("value").GetInt32());
        JsonElement noCore = conditions.GetProperty("no-core-up-at-centre");
        Assert.Equal("at-most", noCore.GetProperty("operator").GetString());
        Assert.Equal(0, noCore.GetProperty("value").GetInt32());

        // Readiness reads one way; unless refuses it.
        rule["unless"] = JsonNode.Parse("""{"groupsSet":true}""");
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("readiness belongs in required", error.Message);
    }

    [Fact]
    public void PhaseCustodyMovesDeliveryIntoTheOverridingPhase()
    {
        // Custody per phase: p1 keeps carrying (a stolen Core stays in
        // hand), p2 banks under escort along a drawn carrier route. Any
        // override moves the WHOLE delivery emission into the phase
        // wrappers — no tactic-scoped bank leaf remains to shadow it.
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonArray phases = document["tactics"]![0]!["phases"]!.AsArray();
        phases[0]!.AsObject()["custody"] = JsonNode.Parse(
            """{"plan":"keep"}""");
        phases[1]!.AsObject()["custody"] = JsonNode.Parse(
            """{"plan":"escort","via":[[15,12],[8,8],[3,5]]}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement shared = orders.RootElement.GetProperty("sharedOrders");
        Assert.False(shared.TryGetProperty("t1-g2-bank", out _));
        Assert.False(shared.TryGetProperty("t1-p1-g2-bank", out _));
        JsonElement bank = shared.GetProperty("t1-p2-g2-bank");
        Assert.Equal("own-reactor",
            bank.GetProperty("move").GetProperty("to").GetString());
        string route = bank.GetProperty("move").GetProperty("via").GetString()!;
        Assert.Equal("g-t1-p2-carry", route);
        // The corridor belongs to the phase's ground: the via'd bank gates
        // on standing there, and a Core grabbed elsewhere banks direct.
        string[] bankWhen = bank.GetProperty("when").GetProperty("all")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Contains("c-t1-p2-home-from-ground", bankWhen);
        JsonElement direct = shared.GetProperty("t1-p2-g2-bank-direct");
        Assert.False(direct.GetProperty("move").TryGetProperty("via", out _));
        Assert.True(shared.TryGetProperty("t1-p2-g2-screen-carrier", out _));
        using JsonDocument mapPlan = JsonDocument.Parse(
            compilation.MaterializedMapPlan!);
        Assert.True(mapPlan.RootElement.GetProperty("routes")
            .TryGetProperty(route, out _));
    }

    [Fact]
    public void CurrentStateRowsReadInEveryContextAndHealedInverts()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject tactic = document["tactics"]![0]!.AsObject();
        // Start gates on contact AND everyone topped up; the transition
        // takes "unless everyone is healed to 80%" — the inverted health
        // gate, one nested any of the per-role flips.
        tactic["start"] = JsonNode.Parse(
            """{"mode":"when","cases":[{"required":{"contact":true,"healed":{"percent":50}}}]}""");
        JsonObject rule = tactic["phases"]![0]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule["unless"] = JsonNode.Parse("""{"healed":{"percent":80}}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement start = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("start").EnumerateArray().Single();
        string[] startAll = start.GetProperty("all").EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetString()!).ToArray();
        Assert.Contains("enemy-contact", startAll);
        Assert.Contains("hp-vex-50", startAll);

        JsonElement when = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().First()
            .GetProperty("phases").EnumerateArray().First()
            .GetProperty("continue").EnumerateArray().Single()
            .GetProperty("when");
        // The all side also carries the groupsSet readiness pairs; the
        // inverted health gate is the one nested any of no-hp names.
        JsonElement nested = when.GetProperty("all").EnumerateArray()
            .Single(entry => entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("any", out JsonElement any)
                && any.EnumerateArray().Any(name =>
                    name.ValueKind == JsonValueKind.String
                    && name.GetString()!.StartsWith(
                        "no-hp-", StringComparison.Ordinal)));
        string[] anyNames = nested.GetProperty("any").EnumerateArray()
            .Select(entry => entry.GetString()!).ToArray();
        Assert.Equal(8, anyNames.Length);
        Assert.Contains("no-hp-vex-80", anyNames);
        JsonElement flipped = orders.RootElement.GetProperty("conditions")
            .GetProperty("no-hp-vex-80");
        Assert.Equal("at-most", flipped.GetProperty("operator").GetString());
        Assert.Equal(79, flipped.GetProperty("value").GetInt32());
    }

    [Fact]
    public void ARoutWindowCompilesAsWindowedEnemyDestructions()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject rule = document["tactics"]![0]!["phases"]![0]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule["required"]!.AsObject()["killsWithin"] = JsonNode.Parse(
            """{"count":4,"ticks":40}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement rout = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-killed-4-in-40");
        Assert.Equal("confirmed-enemy-destructions-in-zone",
            rout.GetProperty("fact").GetString());
        Assert.Equal(40, rout.GetProperty("windowTicks").GetInt32());
        Assert.False(rout.TryGetProperty("since", out _));
    }

    [Fact]
    public void AWellScopedCoreClaimCompilesWithItsSourceFilter()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject rule = document["tactics"]![0]!["phases"]![1]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule["required"] = JsonNode.Parse(
            """{"coreAcquired":{"well":"enemy"}}""");
        rule.Remove("triggers");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement acquired = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-acquired-enemy");
        Assert.Equal("friendly-core-acquisitions",
            acquired.GetProperty("fact").GetString());
        Assert.Equal("enemy", acquired.GetProperty("well").GetString());
        Assert.Equal("phase-entry", acquired.GetProperty("since").GetString());
    }

    [Fact]
    public void TravelOnAnAllMarchJobIsRefused()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject job = document["tactics"]![0]!["phases"]![1]!["jobs"]!
            ["flankers"]!.AsObject();
        job["kind"] = "patrol";
        job.Remove("point");
        job["path"] = JsonNode.Parse("[[15,12],[18,12],[15,12]]");
        job["travel"] = "hold";
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("all march", error.Message);
    }

    [Fact]
    public void LegacyBackwardTransitionIsReducedToSequentialBehavior()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject rule = document["tactics"]![0]!["phases"]![1]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule["to"] = "stage";
        byte[] source = Encoding.UTF8.GetBytes(document.ToJsonString());
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(source);
        Assert.True(compilation.UsesOrderTree);
        Assert.DoesNotContain("\"to\"",
            Encoding.UTF8.GetString(
                ArcRelayTacticalPlaybookCompiler.MigrateTactics(source)));
    }

    [Fact]
    public void CoreSoonAndEnemiesDownRowsMaterialize()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        document["tactics"]![0]!["start"] = JsonNode.Parse(
            """{"mode":"when","conds":{"enemiesDown":{"count":3}}}""");
        document["tactics"]![0]!["release"] = JsonNode.Parse(
            """{"mode":"ready-when","conds":{"coreSoon":{"well":"centre","ticks":20}}}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement conditions = orders.RootElement.GetProperty("conditions");
        JsonElement coreSoon = conditions.GetProperty("c-t1-p1-core-soon");
        Assert.Equal(
            "well-ticks-until-birth", coreSoon.GetProperty("fact").GetString());
        Assert.Equal("at-most", coreSoon.GetProperty("operator").GetString());
        Assert.Equal(20, coreSoon.GetProperty("value").GetInt32());
        Assert.Equal("centre", coreSoon.GetProperty("well").GetString());
        JsonElement enemiesDown = conditions.GetProperty("c-t1-start-enemies-down");
        Assert.Equal("live-enemies", enemiesDown.GetProperty("fact").GetString());
        Assert.Equal("at-most", enemiesDown.GetProperty("operator").GetString());
        Assert.Equal(5, enemiesDown.GetProperty("value").GetInt32());
    }

    [Fact]
    public void APostsOwnWayInOverridesTheGroupRoute()
    {
        // Ember's post gains a drawn way in; Gantry keeps the direct path.
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonArray posts = document["tactics"]![0]!["groups"]![0]!["stage"]!
            ["posts"]!.AsArray();
        posts[0]!["via"] = new JsonArray(
            new JsonArray(6, 21), new JsonArray(10, 23));
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        string RoleStageVia(string roleId)
        {
            JsonElement role = orders.RootElement.GetProperty("roles")
                .EnumerateArray()
                .Single(value => value.GetProperty("id").GetString() == roleId);
            JsonElement move = role.GetProperty("orders").EnumerateArray()
                .First().GetProperty("orders").EnumerateArray()
                .Single(node => node.TryGetProperty("id", out JsonElement id)
                    && id.GetString() == "t1-p1")
                .GetProperty("orders").EnumerateArray()
                .Single(node => node.TryGetProperty("id", out JsonElement id)
                    && id.GetString() == "t1-p1-g1-move");
            return move.GetProperty("move").TryGetProperty(
                    "via", out JsonElement via)
                ? via.GetString()!
                : "none";
        }
        Assert.Equal("g-t1-g1-in-0", RoleStageVia("ember"));
        Assert.Equal("none", RoleStageVia("gantry"));
    }

    [Fact]
    public void PincerEncodesTheLifecycleContract()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        Assert.Equal("arc-relay-orders-v3",
            orders.RootElement.GetProperty("schema").GetString());

        // The program table owns the lifecycle: standard attempt becomes the
        // tactic-wide duration, rearm keeps it a standing order, and the two
        // migrated phases carry the release and finish transitions.
        JsonElement tactic = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().Single();
        Assert.Equal("t1", tactic.GetProperty("id").GetString());
        Assert.Equal("Pincer", tactic.GetProperty("name").GetString());
        Assert.Equal(180, tactic.GetProperty("maxTicks").GetInt32());
        Assert.Equal(0, tactic.GetProperty("rearm").GetInt32());
        JsonElement[] phases = tactic.GetProperty("phases").EnumerateArray()
            .ToArray();
        Assert.Equal(
            ["p1", "p2"],
            phases.Select(phase => phase.GetProperty("id").GetString()!)
                .ToArray());
        // The finish transition ORs the outcomes, and evidence outcomes
        // anchor at the phase's entry so kills count from release.
        JsonElement finish = phases[1].GetProperty("continue")
            .EnumerateArray().Single();
        Assert.Equal("finish", finish.GetProperty("to").GetString());
        string[] outcomes = finish.GetProperty("when").GetProperty("any")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Contains("c-t1-kills", outcomes);
        Assert.Contains("c-t1-cleared", outcomes);
        Assert.Contains("c-t1-acquired", outcomes);
        JsonElement kills = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-kills");
        Assert.Equal("phase-entry", kills.GetProperty("since").GetString());
        // "Acquired" is the pickup EVENT anchored at phase entry — the
        // carrier snapshot let an inherited Core finish a fresh phase on
        // its second tick.
        JsonElement acquired = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-acquired");
        Assert.Equal("friendly-core-acquisitions",
            acquired.GetProperty("fact").GetString());
        Assert.Equal("phase-entry",
            acquired.GetProperty("since").GetString());

        // The role tree is wrappers all the way: the tactic wrapper (tagged
        // with the tactic id) holds the tactic-scoped Core habits and one
        // tagged wrapper per phase — no release branch, no until latch, no
        // rejoin, no per-leaf gating machinery.
        JsonElement ember = orders.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Single(role => role.GetProperty("id").GetString() == "ember");
        JsonElement wrapper = ember.GetProperty("orders").EnumerateArray()
            .First();
        Assert.Equal("t1", wrapper.GetProperty("id").GetString());
        Assert.Equal("t1", wrapper.GetProperty("phase").GetString());
        Assert.False(wrapper.TryGetProperty("when", out _));
        Assert.False(wrapper.TryGetProperty("until", out _));
        Assert.False(wrapper.TryGetProperty("timeout", out _));
        JsonElement stagePhase = wrapper.GetProperty("orders").EnumerateArray()
            .Single(node => node.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "t1-p1");
        Assert.Equal("t1/p1", stagePhase.GetProperty("phase").GetString());
        Assert.Contains(stagePhase.GetProperty("orders").EnumerateArray(),
            node => node.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "t1-p1-g1-move");
    }

    [Fact]
    public void ClearedPhaseTransitionGeneratesVisibleLocalScanBetweenCombatAndFloor()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(Fixture("pincer"));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement vex = orders.RootElement.GetProperty("roles")
            .EnumerateArray().Single(role =>
                role.GetProperty("id").GetString() == "vex");
        JsonElement phase = vex.GetProperty("orders").EnumerateArray().First()
            .GetProperty("orders").EnumerateArray().Single(node =>
                node.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "t1-p2");
        JsonElement[] leaves = phase.GetProperty("orders").EnumerateArray()
            .ToArray();
        int fight = Array.FindIndex(leaves, node =>
            node.TryGetProperty("shared", out JsonElement shared)
            && shared.GetString() == "t1-p2-g2-fight");
        int recovery = Array.FindIndex(leaves, node =>
            node.TryGetProperty("shared", out JsonElement shared)
            && shared.GetString() == "t1-p2-g2-recovery");
        int commitment = Array.FindIndex(leaves, node =>
            node.TryGetProperty("id", out JsonElement id)
            && id.GetString()!.EndsWith(
                "-signature-commitment", StringComparison.Ordinal));
        int scan = Array.FindIndex(leaves, node =>
            node.TryGetProperty("scan", out _));
        int floor = Array.FindIndex(leaves, node =>
            node.TryGetProperty("shared", out JsonElement shared)
            && shared.GetString() == "t1-p2-g2-move");

        Assert.True(fight >= 0 && scan > fight && floor > scan);
        if (recovery >= 0)
            Assert.True(recovery < scan);
        Assert.True(commitment >= 0 && commitment < scan);
        JsonElement scanAction = leaves[scan].GetProperty("scan");
        Assert.Equal(20,
            scanAction.GetProperty("freshWithinTicks").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(
            scanAction.GetProperty("zone").GetString()));
        string scanWhen = leaves[scan].GetProperty("when").GetString()!;
        JsonElement scanCondition = orders.RootElement.GetProperty("conditions")
            .GetProperty(scanWhen);
        Assert.Equal("zone-cleared",
            scanCondition.GetProperty("fact").GetString());
        Assert.Equal(0, scanCondition.GetProperty("value").GetInt32());
        JsonElement fightOrder = orders.RootElement.GetProperty("sharedOrders")
            .GetProperty("t1-p2-g2-fight");
        Assert.Contains("empty-handed",
            fightOrder.GetProperty("when").GetProperty("all")
                .EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void MultiZoneClearanceRecruitsOnlyGroupsLocalToEachArea()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject phase = document["tactics"]![0]!["phases"]![1]!.AsObject();
        phase["jobs"]!["mid-guard"] = JsonNode.Parse(
            """
            {"kind":"posts","posture":"hold","recovery":"fight-on",
             "posts":[{"at":[4,4],"facing":"e"},{"at":[5,4],"facing":"e"}]}
            """);
        phase["jobs"]!["flankers"] = JsonNode.Parse(
            """
            {"kind":"converge","posture":"hold","recovery":"fight-on",
             "point":[25,22]}
            """);
        phase["continue"] = JsonNode.Parse(
            """
            [{"required":{"cleared":{"zones":[
              {"name":"west-check","rect":[3,3,6,5]},
              {"name":"east-check","rect":[24,21,26,23]}
            ]}}}]
            """);

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);

        static string[] ScanZones(JsonElement root, string roleId) =>
            root.GetProperty("roles").EnumerateArray()
                .Single(role => role.GetProperty("id").GetString() == roleId)
                .GetProperty("orders").EnumerateArray().First()
                .GetProperty("orders").EnumerateArray()
                .Single(node => node.TryGetProperty("id", out JsonElement id)
                    && id.GetString() == "t1-p2")
                .GetProperty("orders").EnumerateArray()
                .Where(node => node.TryGetProperty("scan", out _))
                .Select(node => node.GetProperty("scan").GetProperty("zone")
                    .GetString()!)
                .ToArray();

        string[] guard = ScanZones(orders.RootElement, "ember");
        string[] flanker = ScanZones(orders.RootElement, "vex");
        Assert.Single(guard);
        Assert.Single(flanker);
        Assert.NotEqual(guard[0], flanker[0]);
    }

    [Fact]
    public void EscortCustodyCarriesPickupPhaseGroupMembershipIntoTheProgram()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        document["tactics"]![0]!["phases"]![0]!["custody"] =
            JsonNode.Parse("""{"plan":"escort"}""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement groups = orders.RootElement.GetProperty("program")
            .GetProperty("tactics").EnumerateArray().Single()
            .GetProperty("phases").EnumerateArray().First()
            .GetProperty("custodyGroups");
        Assert.Equal(2, groups.GetArrayLength());
        Assert.Equal([5, 2], groups[0].EnumerateArray()
            .Select(value => value.GetInt32()).ToArray());
        Assert.Equal([0, 1, 3, 4, 6, 7], groups[1].EnumerateArray()
            .Select(value => value.GetInt32()).ToArray());
    }

    [Fact]
    public void ClearedStartAndAbortRemainObservationOnly()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonObject tactic = document["tactics"]![0]!.AsObject();
        tactic["finish"]!["outcomes"]!.AsObject().Remove("cleared");
        tactic["start"] = JsonNode.Parse(
            """{"mode":"when","cases":[{"required":{"cleared":{"zone":{"name":"mid","rect":[13,11,17,15]}}}}]}""");
        tactic["abort"] = JsonNode.Parse(
            """{"cases":[{"required":{"cleared":{"zone":{"name":"mid","rect":[13,11,17,15]}}}}]}""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);

        Assert.DoesNotContain(Descendants(orders.RootElement), element =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("scan", out _));
    }

    [Fact]
    public void ClearedTransitionRejectsAreaWithoutLocalPhaseGroup()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        document["tactics"]![0]!["finish"]!["outcomes"]!["cleared"] =
            JsonNode.Parse(
                """{"zone":{"name":"remote","rect":[0,0,1,1]}}""");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));

        Assert.Contains("no phase group has ground overlapping or touching",
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearedAndNotFightingReuseOneZoneWithDistinctFacts()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        document["tactics"]![0]!["finish"]!["outcomes"]!["notFighting"] =
            JsonNode.Parse(
                """{"zone":{"name":"mid","rect":[13,11,17,15]}}""");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement[] conditions = orders.RootElement.GetProperty("conditions")
            .EnumerateObject().Select(value => value.Value).ToArray();
        JsonElement cleared = conditions.Single(value =>
            value.GetProperty("fact").GetString() == "zone-cleared"
            && value.GetProperty("operator").GetString() == "equals"
            && value.GetProperty("value").GetInt32() == 1);
        JsonElement quiet = conditions.Single(value =>
            value.GetProperty("fact").GetString()
                == "friendly-fights-in-zone");

        Assert.Equal(
            cleared.GetProperty("zones").EnumerateArray()
                .Select(value => value.GetString()).ToArray(),
            quiet.GetProperty("zones").EnumerateArray()
                .Select(value => value.GetString()).ToArray());
    }

    [Theory]
    [InlineData("start", """{"mode":"when","conds":{}}""",
        "at least one active condition row")]
    [InlineData("groups[1].slots", "[0, 1, 3, 4, 6, 7, 5]",
        "belongs to more than one group")]
    [InlineData("groups[0].act",
        """{"kind":"none","posture":"hold","recovery":"patch","via":[[2,2],[4,4]]}""",
        "kind 'none' does not take via")]
    [InlineData("groups[1].act",
        """{"kind":"converge","posture":"aggressive","recovery":"fight-on","point":[15,12],"via":[[3,3]]}""",
        "expected 2..16 entries")]
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
    public void SlotNamedPostsPairByBotNotByPosition()
    {
        // The editor names each post's bot with its squad slot so a Group
        // reshuffle can carry posts along. Listing them in any order must
        // not shuffle who stands where: reversed here, Gantry (slot 2) owns
        // the FIRST entry and its drawn way in; Ember walks in direct.
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        document["tactics"]![0]!["groups"]![0]!["stage"]!["posts"] =
            JsonNode.Parse(
                """
                [
                  {"at": [16, 14], "facing": "n", "slot": 2,
                   "via": [[6, 21], [10, 23]]},
                  {"at": [14, 12], "facing": "e", "slot": 5}
                ]
                """);
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        string RoleStageVia(string roleId)
        {
            JsonElement role = orders.RootElement.GetProperty("roles")
                .EnumerateArray()
                .Single(value => value.GetProperty("id").GetString() == roleId);
            JsonElement move = role.GetProperty("orders").EnumerateArray()
                .First().GetProperty("orders").EnumerateArray()
                .Single(node => node.TryGetProperty("id", out JsonElement id)
                    && id.GetString() == "t1-p1")
                .GetProperty("orders").EnumerateArray()
                .Single(node => node.TryGetProperty("id", out JsonElement id)
                    && id.GetString() == "t1-p1-g1-move");
            return move.GetProperty("move").TryGetProperty(
                    "via", out JsonElement via)
                ? via.GetString()!
                : "none";
        }
        Assert.Equal("g-t1-g1-in-1", RoleStageVia("gantry"));
        Assert.Equal("none", RoleStageVia("ember"));
    }

    [Fact]
    public void APostForABotOutsideTheGroupIsRefused()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonArray posts = document["tactics"]![0]!["groups"]![0]!["stage"]!
            ["posts"]!.AsArray();
        posts[0]!["slot"] = 7;
        posts[1]!["slot"] = 5;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("names one of the group's bots", error.Message);
    }

    [Fact]
    public void TwoPostsForTheSameBotAreRefused()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonArray posts = document["tactics"]![0]!["groups"]![0]!["stage"]!
            ["posts"]!.AsArray();
        posts[0]!["slot"] = 5;
        posts[1]!["slot"] = 5;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("two posts name the same bot", error.Message);
    }

    [Fact]
    public void PostsMustNameEveryBotOrNone()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonArray posts = document["tactics"]![0]!["groups"]![0]!["stage"]!
            ["posts"]!.AsArray();
        posts[0]!["slot"] = 5;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("mix named and unnamed bots", error.Message);
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
    public void LegacyClassMarksMigrateToEveryMatchingStableSlot()
    {
        JsonObject document = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        document["squad"]![1]!["class"] = "mason";

        byte[] normalized = ArcRelayTacticalPlaybookCompiler.MigrateTactics(
            Encoding.UTF8.GetBytes(document.ToJsonString()));
        JsonObject migrated = JsonNode.Parse(normalized)!.AsObject();
        JsonObject sigs = migrated["tactics"]![0]!["phases"]![1]!["jobs"]!
            ["flankers"]!["sigs"]!.AsObject();

        Assert.Equal(["1", "3"], sigs.Select(value => value.Key).ToArray());
        Assert.All(sigs, value => Assert.Equal(
            "only", value.Value!["policy"]!.GetValue<string>()));
    }

    [Fact]
    public void DuplicateBotsMaterializeIndependentSignaturePolicies()
    {
        JsonObject source = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        source["squad"]![1]!["class"] = "mason";
        JsonObject migrated = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Encoding.UTF8.GetBytes(source.ToJsonString())))!.AsObject();
        JsonObject sigs = migrated["tactics"]![0]!["phases"]![1]!["jobs"]!
            ["flankers"]!["sigs"]!.AsObject();
        sigs["1"] = JsonNode.Parse(
            """{"policy":"off"}""");
        sigs["3"] = JsonNode.Parse(
            """{"policy":"only","points":[[15,11]]}""");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(migrated.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement solder = orders.RootElement.GetProperty("roles")
            .EnumerateArray().Single(role =>
                role.GetProperty("id").GetString() == "solder");
        JsonElement flux = orders.RootElement.GetProperty("roles")
            .EnumerateArray().Single(role =>
                role.GetProperty("id").GetString() == "flux");
        Assert.DoesNotContain(Descendants(solder), node =>
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("use", out _));
        Assert.Contains(Descendants(flux), node =>
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("use", out JsonElement use)
            && use.GetProperty("skill").GetString() == "hardlight-block");
    }

    [Fact]
    public void ExactDurableDeploymentWaitsForJobGroundThenLatchesItsApproach()
    {
        JsonObject source = JsonNode.Parse(Fixture("pincer"))!.AsObject();
        JsonObject migrated = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Encoding.UTF8.GetBytes(source.ToJsonString())))!.AsObject();

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(migrated.ToJsonString()));
        using JsonDocument orders = JsonDocument.Parse(
            compilation.MaterializedOrders!);
        JsonElement flux = orders.RootElement.GetProperty("roles")
            .EnumerateArray().Single(role =>
                role.GetProperty("id").GetString() == "flux");
        JsonElement deployment = Descendants(flux).Single(node =>
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("use", out JsonElement use)
            && use.GetProperty("skill").GetString() == "hardlight-block");

        string[] when = deployment.GetProperty("when").GetProperty("all")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        JsonElement conditions = orders.RootElement.GetProperty("conditions");
        Assert.Contains(when, name =>
            conditions.TryGetProperty(name, out JsonElement condition)
            && condition.GetProperty("fact").GetString() == "self-in-zone");
        Assert.Equal(40, deployment.GetProperty("timeout").GetInt32());
        string until = deployment.GetProperty("until").GetString()!;
        Assert.Equal("self-deployment-in-zone",
            conditions.GetProperty(until).GetProperty("fact").GetString());
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
                Path.Combine(output, "map-plan.json")));
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
              "cleared": {"zone": {"rect": [19, {{{y}}}, 21, {{{y + 2}}}]}},
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
                "point": [20, {{{y + 1}}}],
                "via": [[4, {{{y}}}], [10, {{{y + 3}}}], [16, {{{y + 1}}}]]
              },
              "act": {
                "kind": "converge",
                "posture": "steal",
                "recovery": "fight-on",
                "point": [21, {{{y + 2}}}],
                "via": [[18, {{{y + 4}}}], [20, {{{y + 3}}}]]
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

    private static IEnumerable<JsonElement> Descendants(JsonElement value)
    {
        yield return value;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            foreach (JsonElement descendant in Descendants(property.Value))
                yield return descendant;
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            foreach (JsonElement descendant in Descendants(item))
                yield return descendant;
        }
    }

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
