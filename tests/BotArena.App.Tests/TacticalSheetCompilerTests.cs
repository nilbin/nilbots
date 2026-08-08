using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.App.ArcRelay;
using BotArena.App.Sheets;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class TacticalSheetCompilerTests
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public void Embedded_authoring_template_and_stock_opponents_compile_reproducibly()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(3, templates.Stock.Count);
        foreach (TacticalSheetSource source in
                 new[] { templates.Template }.Concat(templates.Stock))
        {
            ValidatedTacticalSheet compiled = compiler.Compile(
                source.PlaybookJson,
                source.LayoutJson,
                unlocked,
                source.Id);

            Assert.Equal(source.ContentHash, compiled.ContentHash);
            Assert.Equal(source.LinkedData, compiled.Compilation.LinkedData);
            using JsonDocument playbook = JsonDocument.Parse(source.PlaybookJson);
            string pinned = playbook.RootElement.GetProperty("layout")
                .GetProperty("sha256").GetString()!;
            Assert.Equal(
                Convert.ToHexStringLower(SHA256.HashData(
                    Encoding.UTF8.GetBytes(source.LayoutJson))),
                pinned);
        }
    }

    [Fact]
    public void Starter_template_is_doctrine_only_and_plotted_on_the_current_map()
    {
        var templates = new TacticalSheetTemplateCatalog();
        using JsonDocument playbook = JsonDocument.Parse(
            templates.Template.PlaybookJson);
        using JsonDocument layout = JsonDocument.Parse(
            templates.Template.LayoutJson);

        Assert.False(playbook.RootElement.TryGetProperty("orders", out _));
        string[] roles = playbook.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(value => value.GetProperty("roleId").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] doctrineRoles = playbook.RootElement.GetProperty("doctrines")
            .EnumerateObject()
            .Select(value => value.Value.GetProperty("role").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(roles, doctrineRoles);

        ActorMapDefinition map = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Current);
        Assert.Equal(map.Id,
            layout.RootElement.GetProperty("mapId").GetString());
        foreach (JsonElement zone in layout.RootElement.GetProperty("zones")
                     .EnumerateArray())
        {
            int[] rect = zone.GetProperty("rect").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            Assert.InRange(rect[0], 0, map.Width - 1);
            Assert.InRange(rect[2], 0, map.Width - 1);
            Assert.InRange(rect[1], 0, map.Height - 1);
            Assert.InRange(rect[3], 0, map.Height - 1);
        }
        foreach (JsonElement route in layout.RootElement.GetProperty("routes")
                     .EnumerateArray())
        foreach (JsonElement waypoint in route.GetProperty("waypoints")
                     .EnumerateArray())
        {
            Assert.InRange(waypoint[0].GetInt32(), 0, map.Width - 1);
            Assert.InRange(waypoint[1].GetInt32(), 0, map.Height - 1);
        }
    }

    [Fact]
    public void Hosted_compiler_rejects_stale_layout_pin_and_copy_cap_violation()
    {
        var templates = new TacticalSheetTemplateCatalog();
        var compiler = new TacticalSheetCompiler(ArcRelayClassCatalog.Default);
        IReadOnlySet<string> unlocked = ArcRelayClassCatalog.Default.All
            .Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);
        JsonObject stale = JsonNode.Parse(
            templates.Template.PlaybookJson)!.AsObject();
        stale["layout"]!["sha256"] = new string('0', 64);

        InvalidDataException staleError = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                stale.ToJsonString(Pretty),
                templates.Template.LayoutJson,
                unlocked,
                "stale-layout"));
        Assert.Contains("layout hash", staleError.Message, StringComparison.OrdinalIgnoreCase);

        JsonObject copies = JsonNode.Parse(
            templates.Template.PlaybookJson)!.AsObject();
        copies["composition"] = new JsonArray(
            "kestrel", "kestrel", "kestrel", "hush",
            "relay", "towline", "palisade", "patchbay");
        InvalidDataException copyError = Assert.Throws<InvalidDataException>(() =>
            compiler.Compile(
                copies.ToJsonString(Pretty),
                templates.Template.LayoutJson,
                unlocked,
                "copy-cap"));
        Assert.Contains("two-copy cap", copyError.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}
