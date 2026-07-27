using System.Collections.Immutable;
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
        Assert.All(
            root.GetProperty("rules").GetProperty("actions").EnumerateArray(),
            action => Assert.Equal(
                ["id", "code", "kind", "parameterKinds", "enabled"],
                action.EnumerateObject().Select(property => property.Name)));
        Assert.Empty(
            root.GetProperty("rules").GetProperty("actions")[0]
                .GetProperty("parameterKinds")
                .EnumerateArray());
        Assert.Equal(
            ["shot-program"],
            root.GetProperty("rules").GetProperty("actions")[4]
                .GetProperty("parameterKinds")
                .EnumerateArray()
                .Select(kind => kind.GetString()));
        Assert.Equal(
            [
                "id", "maxHealth", "visionRange", "shootCooldownTicks",
                "omnidirectionalVision", "omnidirectionalShooting",
                "movementLayer", "objectiveWeight", "canMove", "canShoot",
                "allowsProgrammedShots", "allowedActionIds",
            ],
            root.GetProperty("rules").GetProperty("forms")[0]
                .EnumerateObject()
                .Select(property => property.Name));
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

    [Fact]
    public void ActionParameterKinds_SerializeEveryKindInCanonicalEnumOrder()
    {
        PublicRulesManifest manifest =
            PublicRulesManifestFactory.CreateRules(GameRules.Current);
        PublicActionDefinition shoot =
            manifest.Actions.Single(action => action.Id == PublicActionIds.Shoot);
        PublicRulesManifest allParameterKinds = manifest with
        {
            Actions = manifest.Actions
                .Select(action => action.Id == shoot.Id
                    ? action with
                    {
                        ParameterKinds = Enum
                            .GetValues<PublicActionParameterKind>()
                            .ToImmutableArray(),
                    }
                    : action)
                .ToImmutableArray(),
        };

        using JsonDocument document = JsonDocument.Parse(
            RulesManifestSerializer.ToCanonicalJson(allParameterKinds));

        Assert.Equal(
            ["shot-program", "direction", "unit-target", "form-target"],
            document.RootElement.GetProperty("actions")
                .EnumerateArray()
                .Single(action =>
                    action.GetProperty("id").GetString() == PublicActionIds.Shoot)
                .GetProperty("parameterKinds")
                .EnumerateArray()
                .Select(kind => kind.GetString()));
    }

    [Theory]
    [MemberData(nameof(InvalidActionParameterKinds))]
    public void ActionParameterKinds_RejectInvalidCanonicalCollections(
        ImmutableArray<PublicActionParameterKind> parameterKinds)
    {
        PublicRulesManifest manifest =
            PublicRulesManifestFactory.CreateRules(GameRules.Current);
        PublicRulesManifest invalid = manifest with
        {
            Actions = manifest.Actions
                .Select(action => action.Id == PublicActionIds.Shoot
                    ? action with { ParameterKinds = parameterKinds }
                    : action)
                .ToImmutableArray(),
        };

        Assert.ThrowsAny<ArgumentException>(
            () => RulesManifestSerializer.ToCanonicalJson(invalid));
    }

    [Fact]
    public void FrontlineSerialization_AddsTypedDefinitionAndMapGeometryOnlyWhenPresent()
    {
        GameRules rules = GameRules.V0_1 with
        {
            RulesVersion = "frontline-serializer-test",
            Frontline = new FrontlineRules(),
        };
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(
                FindRepoRoot(),
                "maps",
                "experimental",
                "frontline-01.json")));

        using JsonDocument rulesDocument = JsonDocument.Parse(
            RulesManifestSerializer.ToCanonicalJson(
                PublicRulesManifestFactory.CreateRules(rules)));
        using JsonDocument mapDocument = JsonDocument.Parse(
            RulesManifestSerializer.ToCanonicalJson(
                PublicRulesManifestFactory.CreateMap(map)));
        JsonElement rulesRoot = rulesDocument.RootElement;
        JsonElement mapRoot = mapDocument.RootElement;

        Assert.Equal(
            [
                "schemaVersion", "rulesetId", "rulesFingerprint", "limits", "objective",
                "frontlineDefinition", "energy", "forms", "actions", "projectiles",
                "shotPrograms", "vision", "collisions", "tickResolution",
            ],
            rulesRoot.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "teamCount", "participantsPerTeam", "frontlinePositionCount",
                "initialUnitsPerTeam", "maxUnitsPerTeam", "teamPerception",
                "capture", "lifecycle", "anchor", "alliedCombat",
            ],
            rulesRoot.GetProperty("frontlineDefinition")
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.Equal(
            ["child-mobile", "prime-mobile", "turret"],
            rulesRoot.GetProperty("forms")
                .EnumerateArray()
                .Select(form => form.GetProperty("id").GetString()));
        Assert.Equal(
            [
                "id", "maxHealth", "visionRange", "shootCooldownTicks",
                "omnidirectionalVision", "omnidirectionalShooting",
                "movementLayer", "objectiveWeight", "canMove", "canShoot",
                "allowsProgrammedShots", "allowedActionIds",
            ],
            rulesRoot.GetProperty("forms")[0]
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.DoesNotContain(
            rulesRoot.GetProperty("actions").EnumerateArray(),
            action => action.GetProperty("id").GetString() is "fabricate" or "anchor");

        Assert.Equal(
            [
                "schemaVersion", "mapId", "mapVersion", "mapFingerprint", "formatVersion",
                "width", "height", "tileRows", "spawns", "objectiveTiles", "frontline",
            ],
            mapRoot.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["positions", "teamHomes", "anchorForbiddenTiles"],
            mapRoot.GetProperty("frontline")
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.Empty(mapRoot.GetProperty("objectiveTiles").EnumerateArray());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }

    public static TheoryData<ImmutableArray<PublicActionParameterKind>>
        InvalidActionParameterKinds
    {
        get
        {
            var cases =
                new TheoryData<ImmutableArray<PublicActionParameterKind>>();
            cases.Add(default);
            cases.Add(
            [
                PublicActionParameterKind.Direction,
                PublicActionParameterKind.Direction,
            ]);
            cases.Add(
            [
                PublicActionParameterKind.FormTarget,
                PublicActionParameterKind.UnitTarget,
            ]);
            cases.Add([(PublicActionParameterKind)int.MaxValue]);
            return cases;
        }
    }
}
