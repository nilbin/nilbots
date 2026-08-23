using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.App.Sheets;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class TacticalSheetCompilerTests
{
    [Fact]
    public void Embedded_orders_template_and_stock_compile_reproducibly()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);

        TacticalSheetSource stock = templates.Stock[0];
        Assert.Equal("demolition-march-v2", stock.Id);
        Assert.Equal(
            [
                "demolition-march-v2",
                "idle-field-v2",
            ],
            templates.Stock.Select(value => value.Id));
        foreach (TacticalSheetSource source in
                 templates.Starters.Concat(templates.Stock))
        {
            ValidatedTacticalSheet compiled = compiler.Compile(
                source.OrdersJson,
                source.MapPlanJson,
                unlocked,
                source.Id);

            Assert.Equal(source.ContentHash, compiled.ContentHash);
            Assert.Equal(source.LinkedData, compiled.Compilation.LinkedData);
            using JsonDocument sheet = JsonDocument.Parse(source.OrdersJson);
            // Frozen stock sheets stay on the legacy v2 grammar; templates
            // materialized from Tactics documents carry the v3 program.
            Assert.Contains(
                sheet.RootElement.GetProperty("schema").GetString(),
                (string[])["arc-relay-orders-v2", "arc-relay-orders-v3"]);
            Assert.Equal("map-plan.json",
                sheet.RootElement.GetProperty("mapPlan").GetString());
            Assert.False(sheet.RootElement.TryGetProperty("layout", out _));
        }
    }

    [Fact]
    public void Starters_are_tactics_native_and_reproduce_through_the_save_path()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            ["first-stand-v2", "mid-pincer-v2"],
            templates.Starters.Select(value => value.Id));
        Assert.True(templates.Starters[0].Recommended);
        Assert.Equal("recommended", templates.Starters[0].Classification);
        Assert.Equal("advanced", templates.Starters[1].Classification);
        ActorMapDefinition current = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);

        foreach (TacticalSheetSource starter in templates.Starters)
        {
            string tacticsJson = Assert.IsType<string>(starter.TacticsJson);
            using JsonDocument tactics = JsonDocument.Parse(tacticsJson);
            Assert.Equal("arc-relay-tactics-v2",
                tactics.RootElement.GetProperty("schema").GetString());
            Assert.Equal(8,
                tactics.RootElement.GetProperty("squad").GetArrayLength());
            using JsonDocument mapPlan = JsonDocument.Parse(starter.MapPlanJson);
            Assert.Equal(current.Id,
                mapPlan.RootElement.GetProperty("mapId").GetString());
            IReadOnlyList<string> composition =
                TacticalSheetCompiler.ReadRoleClasses(starter.OrdersJson);
            Assert.Equal(8, composition.Count);
            Assert.True(
                composition.Distinct(StringComparer.Ordinal).Count() >= 7);
            // The hosted save path reproduces the embedded materialization
            // byte for byte.
            ValidatedTacticalSheet saved = compiler.CompileTactics(
                tacticsJson, unlocked, starter.Id);
            Assert.Equal(starter.ContentHash, saved.ContentHash);
            Assert.Equal(
                starter.OrdersJson,
                System.Text.Encoding.UTF8.GetString(
                    saved.Compilation.MaterializedOrders!));
        }
    }

    [Fact]
    public void Loomsplit_candidate_reproduces_through_the_hosted_save_path()
    {
        string root = Path.GetDirectoryName(
            BotArena.Toolchain.RepoPaths.FindUpward("BotArena.sln")!)!;
        string tactics = File.ReadAllText(Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "tactics",
            "loomsplit-v1.json"));
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);

        ValidatedTacticalSheet compiled = compiler.CompileTactics(
            tactics, unlocked, "loomsplit-v1");

        Assert.Equal(
            "e890295d74bdb2f62b900ef5173dd9f55ae13b0cc03417c9e947bd42fb4bc438",
            compiled.ContentHash);
        Assert.Equal(tactics, compiled.TacticsJson);
        using JsonDocument orders = JsonDocument.Parse(
            compiled.Compilation.MaterializedOrders!);
        Assert.Equal("loomsplit-v1",
            orders.RootElement.GetProperty("sheetId").GetString());
    }

    [Fact]
    public void ExistingV2PhaseTargetsNormalizeBeforeHostedSaveAndReadback()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        ValidatedTacticalSheet seed = compiler.CompileTactics(
            templates.Starters[1].TacticsJson!, unlocked, "canonical-seed");
        JsonObject tactics = JsonNode.Parse(seed.TacticsJson!)!.AsObject();
        JsonArray phases = tactics["tactics"]!.AsArray()
            .Select(node => node!["phases"]!.AsArray())
            .First(value => value.Count > 1);
        foreach (JsonNode? rule in phases[0]!["continue"]!.AsArray())
            rule!["to"] = phases[1]!["id"]!.GetValue<string>();
        if (phases[phases.Count - 1]!["continue"] is JsonArray finalRules)
        {
            foreach (JsonNode? rule in finalRules)
                rule!["to"] = "finish";
        }

        ValidatedTacticalSheet saved = compiler.CompileTactics(
            tactics.ToJsonString(), unlocked, "legacy-phase-targets");
        Assert.DoesNotContain("\"to\"", saved.TacticsJson);
        Assert.True(saved.Compilation.UsesOrderTree);
    }

    [Fact]
    public void Tactics_path_rejects_a_wrongly_declared_map_size()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        JsonObject tactics = JsonNode.Parse(
            templates.Starters[0].TacticsJson!)!.AsObject();
        tactics["mapWidth"] = 21;

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.CompileTactics(
                tactics.ToJsonString(), unlocked, "wrong-map-size"));

        Assert.Contains("declares a 21x27 map", error.Message);
    }

    [Fact]
    public void Tactics_path_rejects_a_post_on_a_wall_with_real_map_geometry()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        Position wall = Enumerable.Range(1, map.Height - 2)
            .SelectMany(y => Enumerable.Range(1, map.Width - 2)
                .Select(x => new Position(x, y)))
            .First(map.IsWall);
        JsonObject tactics = JsonNode.Parse(
            templates.Starters[0].TacticsJson!)!.AsObject();
        JsonArray posts = tactics["tactics"]![1]!["phases"]![0]!["jobs"]!
            ["holders"]!["posts"]!.AsArray();
        posts[0]!["at"] = new JsonArray(wall.X, wall.Y);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.CompileTactics(
                tactics.ToJsonString(), unlocked, "post-on-wall"));

        // The pre-materialization pass names the drawn geometry, never a
        // generated anchor id.
        Assert.Contains("tactics[1].phases[0].jobs.holders.posts[0]", error.Message);
        Assert.Contains("stands on a wall", error.Message);
    }

    [Fact]
    public void Tactics_path_accepts_a_post_whose_cardinal_facing_points_into_a_wall()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        HashSet<Position> reserved = map.SpawnAnchors
            .Select(value => value.Spawn.Position).ToHashSet();
        bool Floor(int x, int y) =>
            x >= 0 && y >= 0 && x < map.Width && y < map.Height
            && !map.IsWall(x, y);
        // A tile that is a legal post from both seats but faces east into a
        // wall — the exact shape the owner hit.
        Position at = Enumerable.Range(1, map.Height - 2)
            .SelectMany(y => Enumerable.Range(1, map.Width - 2)
                .Select(x => new Position(x, y)))
            .First(p => Floor(p.X, p.Y)
                && Floor(map.Width - 1 - p.X, map.Height - 1 - p.Y)
                && !reserved.Contains(p)
                && !reserved.Contains(new Position(
                    map.Width - 1 - p.X, map.Height - 1 - p.Y))
                && map.IsWall(p.X + 1, p.Y));
        JsonObject tactics = JsonNode.Parse(
            templates.Starters[0].TacticsJson!)!.AsObject();
        JsonObject post = tactics["tactics"]![1]!["phases"]![0]!["jobs"]!
            ["holders"]!["posts"]!.AsArray()[0]!.AsObject();
        post["at"] = new JsonArray(at.X, at.Y);
        post["facing"] = "e";

        ValidatedTacticalSheet compiled = compiler.CompileTactics(
            tactics.ToJsonString(), unlocked, "post-facing-wall");

        Assert.NotNull(compiled.Compilation.MaterializedOrders);
    }

    [Fact]
    public void Hosted_compiler_rejects_retired_fields_copy_cap_and_wrong_map()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);

        (string temperedOrders, JsonObject _) = BroadloomOrdersFixture();
        JsonObject retired = JsonNode.Parse(temperedOrders)!.AsObject();
        retired["layout"] = new JsonObject();
        InvalidDataException retiredError = Assert.Throws<InvalidDataException>(
            () => compiler.Compile(
                retired.ToJsonString(),
                BroadloomOrdersFixture().MapPlan.ToJsonString(),
                unlocked, "retired-layout"));
        Assert.Contains("unknown field 'layout'", retiredError.Message);

        JsonObject copies = JsonNode.Parse(temperedOrders)!.AsObject();
        copies["roles"]![0]!["classes"] = new JsonArray(
            "relay", "relay", "relay");
        InvalidDataException copyError = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                copies.ToJsonString(),
                BroadloomOrdersFixture().MapPlan.ToJsonString(),
                unlocked, "copy-cap"));
        Assert.Contains("two-copy class cap", copyError.Message);

        TacticalSheetSource idleField = templates.GetStock("idle-field-v2");
        JsonObject insufficientVariety = JsonNode.Parse(
            idleField.OrdersJson)!.AsObject();
        insufficientVariety["roles"]![0]!["classes"] = new JsonArray(
            "relay", "relay", "kestrel", "kestrel", "palisade", "palisade",
            "towline", "towline");
        InvalidDataException varietyError = Assert.Throws<InvalidDataException>(
            () => compiler.Compile(
                insufficientVariety.ToJsonString(),
                idleField.MapPlanJson,
                unlocked,
                "roster-variety"));
        Assert.Contains("at least 7 distinct classes", varietyError.Message);

        JsonObject wrongMap = BroadloomOrdersFixture().MapPlan;
        wrongMap["mapId"] = ArcRelayLoopProfile.ForwardCombat.MapId;
        string wrongMapJson = wrongMap.ToJsonString();
        InvalidDataException mapError = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                temperedOrders, wrongMapJson,
                unlocked, "wrong-map"));
        Assert.Contains("never migrated", mapError.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(TacticalSheetCompiler.IsAdmissibleToCurrentPlaylist(
            wrongMapJson));
    }

    [Fact]
    public void Hosted_compiler_rejects_a_route_waypoint_on_a_wall()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        (string fixtureOrders, JsonObject mapPlan) = BroadloomOrdersFixture();
        JsonObject orders = JsonNode.Parse(fixtureOrders)!.AsObject();
        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        Position wall = Enumerable.Range(0, map.Height)
            .SelectMany(y => Enumerable.Range(0, map.Width)
                .Select(x => new Position(x, y)))
            .First(map.IsWall);

        mapPlan["routes"]!["wall-route"] = new JsonObject
        {
            ["waypoints"] = new JsonArray
            {
                new JsonArray(wall.X, wall.Y),
                new JsonArray(1, 1),
            },
        };
        FirstMoveWithoutVia(orders)["via"] = "wall-route";

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                orders.ToJsonString(), mapPlan.ToJsonString(), unlocked,
                "wall-route"));

        Assert.Contains("not traversable", error.Message);
    }

    [Fact]
    public void Hosted_compiler_rejects_a_route_waypoint_on_a_reserved_spawn()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        (string fixtureOrders, JsonObject mapPlan) = BroadloomOrdersFixture();
        JsonObject orders = JsonNode.Parse(fixtureOrders)!.AsObject();
        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        Position spawn = map.SpawnAnchors[0].Spawn.Position;

        mapPlan["routes"]!["spawn-route"] = new JsonObject
        {
            ["waypoints"] = new JsonArray
            {
                new JsonArray(spawn.X, spawn.Y),
                new JsonArray(8, 3),
            },
        };
        FirstMoveWithoutVia(orders)["via"] = "spawn-route";

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                orders.ToJsonString(), mapPlan.ToJsonString(), unlocked,
                "spawn-route"));

        Assert.Contains("reserved body spawn", error.Message);
    }

    [Fact]
    public void Hosted_compiler_rejects_a_movement_zone_without_a_slot_per_body()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        (string fixtureOrders, JsonObject mapPlan) = BroadloomOrdersFixture();

        // This was the caught Tapline-6 fault: four interceptors were sent to
        // a wall-split rectangle with only three legal staging tiles.
        mapPlan["zones"]!["centre-overwatch"]!["rect"] = new JsonArray(
            8, 2, 11, 2);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                fixtureOrders,
                mapPlan.ToJsonString(),
                unlocked,
                "undersized-movement-zone"));

        Assert.Contains("Movement zone 'centre-overwatch'", error.Message);
        Assert.Contains("can send", error.Message);
    }

    [Fact]
    public void Hosted_compiler_rejects_a_movement_zone_in_the_enemy_home()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        (string fixtureOrders, JsonObject mapPlan) = BroadloomOrdersFixture();

        // A proposed four-body repair had enough floor, but included the
        // opponent's unenterable delivery tile. Floor count alone is not a
        // valid native-map audit.
        mapPlan["zones"]!["centre-overwatch"]!["rect"] = new JsonArray(
            32, 12, 34, 14);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                fixtureOrders,
                mapPlan.ToJsonString(),
                unlocked,
                "enemy-home-movement-zone"));

        Assert.Contains("opponent's protected home region", error.Message);
    }

    [Fact]
    public void Hosted_compiler_counts_named_zone_capacity_across_roles()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        (string fixtureOrders, JsonObject mapPlan) = BroadloomOrdersFixture();

        // Palisade and both Repulsors consume centre-screen-three through the
        // same shared order. The shared cap admits all three even though no
        // individual role contains three bodies.
        mapPlan["zones"]!["centre-overwatch"]!["rect"] = new JsonArray(
            8, 2, 10, 2);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                fixtureOrders,
                mapPlan.ToJsonString(),
                unlocked,
                "cross-role-named-capacity"));

        Assert.Contains("Movement zone 'centre-overwatch'", error.Message);
        Assert.Contains("can send", error.Message);
    }

    [Fact]
    public void Broadloom_orders_fixture_compiles_clean_before_mutation()
    {
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        (string orders, JsonObject mapPlan) = BroadloomOrdersFixture();

        ValidatedTacticalSheet compiled = compiler.Compile(
            orders, mapPlan.ToJsonString(), unlocked, "fixture-control");

        Assert.NotNull(compiled.Compilation.LinkedData);
    }

    /// <summary>
    /// The frozen Tapline demolition Orders doc rehosted on a Broadloom-native
    /// plan: the rejection tests below need hand-authored zone/route/anchor
    /// names to mutate, which tactics-materialized product templates no longer
    /// carry.
    /// </summary>
    private static (string Orders, JsonObject MapPlan) BroadloomOrdersFixture()
    {
        string root = Path.GetDirectoryName(
            BotArena.Toolchain.RepoPaths.FindUpward("BotArena.sln")!)!;
        JsonObject orders = JsonNode.Parse(File.ReadAllText(Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "playbooks",
            "demolition-march-v1.json")))!.AsObject();
        orders["sheetId"] = "demolition-fixture-v1";
        orders["mapPlan"] = "map-plan.json";
        var mapPlan = new JsonObject
        {
            ["schema"] = "arc-relay-map-plan-v2",
            ["mapPlanId"] = "demolition-fixture-v1",
            ["mapId"] = "arc-relay-broadloom-01",
            ["zones"] = new JsonObject
            {
                ["centre-overwatch"] = new JsonObject
                {
                    ["rect"] = new JsonArray(12, 12, 18, 14),
                },
            },
            ["routes"] = new JsonObject(),
            ["anchors"] = new JsonObject
            {
                ["enemy-centre-approach"] = new JsonArray(20, 13),
            },
        };
        return (orders.ToJsonString(), mapPlan);
    }

    private static JsonObject FirstMoveWithoutVia(JsonNode node)
    {
        JsonObject move = FirstMove(node, requireNoVia: true);
        return move;
    }

    private static JsonObject FirstMove(JsonNode node, bool requireNoVia = false)
    {
        if (TryFirstMove(node, requireNoVia, out JsonObject? found))
            return found!;
        throw new InvalidOperationException("Fixture contains no movement Instruction.");
    }

    private static bool TryFirstMove(
        JsonNode node,
        bool requireNoVia,
        out JsonObject? move)
    {
        if (node is JsonObject value)
        {
            // Attaching an ingress to a move that already has one would
            // orphan its original route and trip consumer honesty instead of
            // the geometry check under test.
            if (value["move"] is JsonObject candidate
                && candidate["to"] is not null
                && (!requireNoVia || candidate["via"] is null))
            {
                move = candidate;
                return true;
            }
            foreach ((_, JsonNode? child) in value)
                if (child is not null && TryFirstMove(child, requireNoVia, out move))
                    return true;
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
                if (child is not null && TryFirstMove(child, requireNoVia, out move))
                    return true;
        }
        move = null;
        return false;
    }
}
