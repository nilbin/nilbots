using System.Text.Json;

namespace BotArena.Engine.Tests;

public class RulesManifestSerializerTests
{
    [Fact]
    public void CanonicalSerialization_HasExplicitStablePropertyAndCollectionOrder()
    {
        ArenaMap map = ArenaMap.Create(
            "canonical",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            zone: [new Position(3, 1), new Position(1, 1)]);
        PublicMatchContractManifest manifest =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);

        string json = RulesManifestSerializer.ToCanonicalJson(manifest);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(
            ["schemaVersion", "matchContractFingerprint", "rules", "map", "topology"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "schemaVersion", "rulesetId", "rulesFingerprint", "limits", "objective",
                "energy", "forms", "actions", "projectiles", "shotPrograms", "vision",
                "collisions", "tickResolution",
            ],
            root.GetProperty("rules").EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "schemaVersion", "mapId", "mapVersion", "mapFingerprint", "formatVersion",
                "width", "height", "tileRows", "spawns", "objectiveTiles",
            ],
            root.GetProperty("map").EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "teamCount", "participantCount", "unitSlotCount", "initialLifeCount",
                "teams", "participants", "unitSlots", "initialLives",
            ],
            root.GetProperty("topology")
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.Equal(2, root.GetProperty("topology").GetProperty("teamCount").GetInt32());
        Assert.Equal(
            2,
            root.GetProperty("topology").GetProperty("participantCount").GetInt32());
        Assert.Equal(
            2,
            root.GetProperty("topology").GetProperty("unitSlotCount").GetInt32());
        Assert.Equal(
            Enum.GetValues<BotAction>().Select(action => (int)action),
            root.GetProperty("rules").GetProperty("actions")
                .EnumerateArray()
                .Select(action => action.GetProperty("code").GetInt32()));
        Assert.Equal(
            [[3, 1], [1, 1]],
            root.GetProperty("map").GetProperty("objectiveTiles")
                .EnumerateArray()
                .Select(tile => tile.EnumerateArray().Select(value => value.GetInt32()).ToArray()));
        Assert.Equal(
            [
                "enabled", "headingSectors", "bendStepOctants",
                "minInitialAimOctants", "maxInitialAimOctants",
                "aimOnlyProgram", "allowedCurvedBendDirections",
                "minBendAfterTiles", "maxBendAfterTiles",
                "minBendEveryTiles", "maxBendEveryTiles", "minBendCount",
                "maxBendCount", "launchTiles", "payloadOptional", "defaultProgram",
                "invalidPayloadResult", "unsupportedPayloadResult",
                "diagonalCornersMustBeClear",
            ],
            root.GetProperty("rules").GetProperty("shotPrograms")
                .EnumerateObject()
                .Select(property => property.Name));
    }

    [Fact]
    public void CanonicalSerialization_IsByteStableAcrossEquivalentInstances()
    {
        ArenaMap firstMap = ArenaMap.Create(
            "first-alias",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        ArenaMap secondMap = ArenaMap.Create(
            "second-alias",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        PublicRulesManifest firstRules = PublicRulesManifestFactory.CreateRules(GameRules.Current);
        PublicRulesManifest secondRules = PublicRulesManifestFactory.CreateRules(GameRules.Current);

        Assert.Equal(
            RulesManifestSerializer.ToCanonicalJson(firstRules),
            RulesManifestSerializer.ToCanonicalJson(secondRules));
        Assert.Equal(
            PublicRulesManifestFactory.CreateMap(firstMap).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(secondMap).MapFingerprint);
    }
}
