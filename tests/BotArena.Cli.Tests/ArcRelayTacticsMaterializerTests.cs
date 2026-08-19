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
        // Every one of the eight bots is individually demanded — at its
        // ground or dead — with no quorum approximation anywhere.
        JsonElement[] pairs = when.GetProperty("all").EnumerateArray().ToArray();
        Assert.Equal(8, pairs.Length);
        string[] gateNames = pairs
            .SelectMany(entry => entry.GetProperty("any").EnumerateArray()
                .Select(name => name.GetString()!))
            .ToArray();
        Assert.Contains("c-t1-g1-at-0", gateNames);
        Assert.Contains("dn-ember", gateNames);
        Assert.Contains("c-t1-g2-at-5", gateNames);
        Assert.Contains("dn-cinder", gateNames);
        string[] rows = when.GetProperty("any").EnumerateArray()
            .Select(entry => entry.GetString()!).ToArray();
        Assert.Equal(["c-t1-p1-enemies", "enemy-contact"], rows);
        // No rejoin anywhere: the coordinator's squad-level phase makes the
        // per-body release latch — and its rejoin escape — unnecessary.
        Assert.DoesNotContain(
            "\"rejoin\"",
            Encoding.UTF8.GetString(compilation.MaterializedOrders!));
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
        Assert.Equal("next", releaseRule.GetProperty("to").GetString());
        JsonElement finishRule = phases[1].GetProperty("continue")
            .EnumerateArray().Single();
        Assert.Equal("finish", finishRule.GetProperty("to").GetString());
        // A v2 document passes through migration untouched.
        Assert.Equal(migrated,
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(migrated));
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
        // floor's march stance follows the travel posture (hold → line).
        JsonElement shared = orders.RootElement.GetProperty("sharedOrders");
        JsonElement travelFight = shared.GetProperty("t1-p1-g2-travel-fight");
        Assert.Equal("hold",
            travelFight.GetProperty("fight").GetProperty("style").GetString());
        Assert.Equal(1,
            travelFight.GetProperty("fight").GetProperty("chaseLimit").GetInt32());
        string[] travelWhen = travelFight.GetProperty("when").GetProperty("all")
            .EnumerateArray().Select(entry => entry.GetString()!).ToArray();
        Assert.Contains("c-t1-g2-off-station", travelWhen);
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
        // 100 boundaries a 90/98 band would deadlock on.
        JsonElement healUp = shared.GetProperty("t1-p1-g1-heal-up");
        Assert.Equal("below-topped-up",
            healUp.GetProperty("when").GetString());
        Assert.Equal("topped-up", healUp.GetProperty("until").GetString());
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
    public void ForwardOnlyTransitionsAreEnforced()
    {
        JsonObject document = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                Fixture("pincer")))!.AsObject();
        JsonObject rule = document["tactics"]![0]!["phases"]![1]!["continue"]!
            .AsArray()[0]!.AsObject();
        rule["to"] = "stage";
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(document.ToJsonString())));
        Assert.Contains("forward-only", error.Message);
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
        Assert.Equal(40, tactic.GetProperty("rearm").GetInt32());
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
        Assert.Contains("we-hold-a-core", outcomes);
        JsonElement kills = orders.RootElement.GetProperty("conditions")
            .GetProperty("c-t1-kills");
        Assert.Equal("phase-entry", kills.GetProperty("since").GetString());

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
