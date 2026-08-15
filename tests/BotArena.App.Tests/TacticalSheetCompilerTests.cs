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
        Assert.Equal("demolition-march-v1", stock.Id);
        Assert.Equal(
            [
                "demolition-march-v1",
                "lockbox-v1",
                "idle-field-v1",
                "tempered-standard-v2",
                "relay-circuit-v1",
                "crowned-rail-v1",
                "rising-standard-v1",
                "wavebreak-v1",
            ],
            templates.Stock.Select(value => value.Id));
        foreach (TacticalSheetSource source in
                 new[] { templates.Template }.Concat(templates.Stock))
        {
            ValidatedTacticalSheet compiled = compiler.Compile(
                source.OrdersJson,
                source.MapPlanJson,
                unlocked,
                source.Id);

            Assert.Equal(source.ContentHash, compiled.ContentHash);
            Assert.Equal(source.LinkedData, compiled.Compilation.LinkedData);
            using JsonDocument sheet = JsonDocument.Parse(source.OrdersJson);
            Assert.Equal("arc-relay-orders-v2",
                sheet.RootElement.GetProperty("schema").GetString());
            Assert.Equal("map-plan.json",
                sheet.RootElement.GetProperty("mapPlan").GetString());
            Assert.False(sheet.RootElement.TryGetProperty("layout", out _));
        }
    }

    [Fact]
    public void Starter_is_a_small_readable_order_tree_on_current_tapline()
    {
        var templates = new TacticalSheetTemplateCatalog();
        using JsonDocument sheet = JsonDocument.Parse(
            templates.Template.OrdersJson);
        using JsonDocument mapPlan = JsonDocument.Parse(
            templates.Template.MapPlanJson);

        Assert.Equal("starter-v2", templates.Template.Id);
        Assert.Equal(
            [
                "capacities", "conditions", "mapPlan", "roles", "schema",
                "sharedOrders", "sheetId",
            ],
            sheet.RootElement.EnumerateObject().Select(value => value.Name)
                .Order(StringComparer.Ordinal));
        string[] composition = sheet.RootElement.GetProperty("roles")
            .EnumerateArray()
            .SelectMany(role => role.GetProperty("classes").EnumerateArray())
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Equal(8, composition.Length);
        Assert.Equal(
            ArcRelayClassCatalog.Default.StarterIds.Order(StringComparer.Ordinal),
            composition.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["home-team", "centre-team", "far-team"],
            sheet.RootElement.GetProperty("roles").EnumerateArray()
                .Select(value => value.GetProperty("id").GetString()));

        Assert.Empty(mapPlan.RootElement.GetProperty("zones")
            .EnumerateObject());
        Assert.Empty(mapPlan.RootElement.GetProperty("routes")
            .EnumerateObject());
        Assert.Empty(mapPlan.RootElement.GetProperty("anchors")
            .EnumerateObject());
        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        Assert.Equal(map.Id,
            mapPlan.RootElement.GetProperty("mapId").GetString());
        Assert.Equal(composition,
            TacticalSheetCompiler.ReadRoleClasses(
                templates.Template.OrdersJson));
    }

    [Fact]
    public void Hosted_compiler_rejects_retired_fields_copy_cap_and_wrong_map()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);

        JsonObject retired = JsonNode.Parse(
            templates.Template.OrdersJson)!.AsObject();
        retired["layout"] = new JsonObject();
        InvalidDataException retiredError = Assert.Throws<InvalidDataException>(
            () => compiler.Compile(
                retired.ToJsonString(), templates.Template.MapPlanJson,
                unlocked, "retired-layout"));
        Assert.Contains("unknown field 'layout'", retiredError.Message);

        JsonObject copies = JsonNode.Parse(
            templates.Template.OrdersJson)!.AsObject();
        copies["roles"]![0]!["classes"] = new JsonArray(
            "relay", "relay", "relay");
        InvalidDataException copyError = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                copies.ToJsonString(), templates.Template.MapPlanJson,
                unlocked, "copy-cap"));
        Assert.Contains("two-copy class cap", copyError.Message);

        JsonObject insufficientVariety = JsonNode.Parse(
            templates.Template.OrdersJson)!.AsObject();
        insufficientVariety["roles"]![2]!["classes"] = new JsonArray(
            "towline", "relay", "palisade");
        InvalidDataException varietyError = Assert.Throws<InvalidDataException>(
            () => compiler.Compile(
                insufficientVariety.ToJsonString(),
                templates.Template.MapPlanJson,
                unlocked,
                "roster-variety"));
        Assert.Contains("at least 7 distinct classes", varietyError.Message);

        JsonObject wrongMap = JsonNode.Parse(
            templates.Template.MapPlanJson)!.AsObject();
        wrongMap["mapId"] = ArcRelayLoopProfile.ForwardCombat.MapId;
        string wrongMapJson = wrongMap.ToJsonString();
        InvalidDataException mapError = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                templates.Template.OrdersJson, wrongMapJson,
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
        JsonObject orders = JsonNode.Parse(
            templates.Template.OrdersJson)!.AsObject();
        JsonObject mapPlan = JsonNode.Parse(
            templates.Template.MapPlanJson)!.AsObject();
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
        orders["roles"]![2]!["orders"]![3]!["move"]!["via"] =
            "wall-route";

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
        JsonObject orders = JsonNode.Parse(
            templates.Template.OrdersJson)!.AsObject();
        JsonObject mapPlan = JsonNode.Parse(
            templates.Template.MapPlanJson)!.AsObject();
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
        orders["roles"]![2]!["orders"]![3]!["move"]!["via"] =
            "spawn-route";

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
        TacticalSheetSource lockbox = templates.GetStock("lockbox-v1");
        JsonObject mapPlan = JsonNode.Parse(lockbox.MapPlanJson)!.AsObject();

        // This was the caught Tapline-6 fault: four interceptors were sent to
        // a wall-split rectangle with only three legal staging tiles.
        mapPlan["zones"]!["enemy-return-ambush"]!["rect"] = new JsonArray(
            18, 16, 22, 17);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                lockbox.OrdersJson,
                mapPlan.ToJsonString(),
                unlocked,
                "undersized-movement-zone"));

        Assert.Contains("Movement zone 'enemy-return-ambush'", error.Message);
        Assert.Contains("can send 4 bodies", error.Message);
    }

    [Fact]
    public void Hosted_compiler_rejects_a_movement_zone_in_the_enemy_home()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        TacticalSheetSource lockbox = templates.GetStock("lockbox-v1");
        JsonObject mapPlan = JsonNode.Parse(lockbox.MapPlanJson)!.AsObject();

        // A proposed four-body repair had enough floor, but included the
        // opponent's unenterable delivery tile. Floor count alone is not a
        // valid native-map audit.
        mapPlan["zones"]!["enemy-return-ambush"]!["rect"] = new JsonArray(
            22, 18, 24, 20);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                lockbox.OrdersJson,
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
        TacticalSheetSource lockbox = templates.GetStock("lockbox-v1");
        JsonObject mapPlan = JsonNode.Parse(lockbox.MapPlanJson)!.AsObject();

        // Palisade and both Repulsors consume centre-screen-three through the
        // same shared order. The shared cap admits all three even though no
        // individual role contains three bodies.
        mapPlan["zones"]!["centre-screen-pocket"]!["rect"] = new JsonArray(
            13, 13, 14, 13);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                lockbox.OrdersJson,
                mapPlan.ToJsonString(),
                unlocked,
                "cross-role-named-capacity"));

        Assert.Contains("Movement zone 'centre-screen-pocket'", error.Message);
        Assert.Contains("can send 3 bodies", error.Message);
    }
}
