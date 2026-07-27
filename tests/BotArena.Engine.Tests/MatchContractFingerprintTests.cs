namespace BotArena.Engine.Tests;

public class MatchContractFingerprintTests
{
    [Fact]
    public void AggregateFingerprint_IsStableAndComposesContentFingerprints()
    {
        ArenaMap map = ArenaMap.Create(
            "contract",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);

        PublicMatchContractManifest first =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);
        PublicMatchContractManifest second =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);

        Assert.Equal(first.Rules.RulesFingerprint, second.Rules.RulesFingerprint);
        Assert.Equal(first.Map.MapFingerprint, second.Map.MapFingerprint);
        Assert.Equal(first.MatchContractFingerprint, second.MatchContractFingerprint);
        Assert.Matches("^[0-9a-f]{64}$", first.MatchContractFingerprint);
        Assert.Equal(
            first.MatchContractFingerprint,
            MatchContractFingerprint.ComputeMatch(first));
    }

    [Fact]
    public void AggregateFingerprint_ChangesWithEitherContentAxis()
    {
        ArenaMap map = ArenaMap.Create(
            "contract",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        ArenaMap changedMap = ArenaMap.Create(
            "contract",
            ["#####", "#...#", "#.#.#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);
        PublicMatchContractManifest rulesChanged =
            PublicRulesManifestFactory.CreateMatchContract(
                GameRules.Current with { VisionRange = GameRules.Current.VisionRange + 1 },
                map);
        PublicMatchContractManifest mapChanged =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, changedMap);

        Assert.NotEqual(
            baseline.MatchContractFingerprint,
            rulesChanged.MatchContractFingerprint);
        Assert.NotEqual(
            baseline.MatchContractFingerprint,
            mapChanged.MatchContractFingerprint);
    }

    [Fact]
    public void ContentFingerprints_IgnoreAliases_ButAggregateIncludesPublicIdentity()
    {
        ArenaMap firstMap = ArenaMap.Create(
            "first",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            version: 1);
        ArenaMap renamedMap = ArenaMap.Create(
            "second",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            version: 1);
        ArenaMap reversionedMap = ArenaMap.Create(
            "first",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            version: 2);
        GameRules firstRules = GameRules.Current;
        GameRules secondRules = firstRules with { RulesVersion = "alias" };

        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(firstRules, firstMap);
        PublicMatchContractManifest rulesAlias =
            PublicRulesManifestFactory.CreateMatchContract(secondRules, firstMap);
        PublicMatchContractManifest mapAlias =
            PublicRulesManifestFactory.CreateMatchContract(firstRules, renamedMap);
        PublicMatchContractManifest mapVersion =
            PublicRulesManifestFactory.CreateMatchContract(firstRules, reversionedMap);

        Assert.Equal(baseline.Rules.RulesFingerprint, rulesAlias.Rules.RulesFingerprint);
        Assert.Equal(baseline.Map.MapFingerprint, mapAlias.Map.MapFingerprint);
        Assert.Equal(baseline.Map.MapFingerprint, mapVersion.Map.MapFingerprint);
        Assert.NotEqual(
            baseline.MatchContractFingerprint,
            rulesAlias.MatchContractFingerprint);
        Assert.NotEqual(
            baseline.MatchContractFingerprint,
            mapAlias.MatchContractFingerprint);
        Assert.NotEqual(
            baseline.MatchContractFingerprint,
            mapVersion.MatchContractFingerprint);
    }

    [Fact]
    public void RulesSchemaBump_DoesNotChangeMapFingerprint_AndMixedSchemasAreAccepted()
    {
        ArenaMap map = ArenaMap.Create(
            "contract",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        PublicMatchContractManifest current =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);

        Assert.Equal(
            BotArenaVersions.PublicRulesManifestSchemaVersion,
            current.Rules.SchemaVersion);
        Assert.Equal(
            BotArenaVersions.PublicMapManifestSchemaVersion,
            current.Map.SchemaVersion);
        Assert.Equal(
            BotArenaVersions.PublicMatchContractSchemaVersion,
            current.SchemaVersion);

        PublicRulesManifest rulesSchemaBump = current.Rules with
        {
            SchemaVersion = current.Rules.SchemaVersion + 1,
            RulesFingerprint = "",
        };
        string bumpedRulesFingerprint =
            MatchContractFingerprint.ComputeRules(rulesSchemaBump, GameRules.Current);
        PublicMapManifest mapSchemaBump = current.Map with
        {
            SchemaVersion = current.Map.SchemaVersion + 1,
            MapFingerprint = "",
        };
        string bumpedMapFingerprint =
            MatchContractFingerprint.ComputeMap(mapSchemaBump);

        Assert.NotEqual(current.Rules.RulesFingerprint, bumpedRulesFingerprint);
        Assert.NotEqual(current.Map.MapFingerprint, bumpedMapFingerprint);
        Assert.Equal(
            current.Map.MapFingerprint,
            MatchContractFingerprint.ComputeMap(current.Map));

        PublicMatchContractManifest mixedSchemas = current with
        {
            SchemaVersion = current.SchemaVersion + 1,
            MatchContractFingerprint = "",
            Rules = rulesSchemaBump with
            {
                RulesFingerprint = bumpedRulesFingerprint,
            },
            Map = mapSchemaBump with
            {
                MapFingerprint = bumpedMapFingerprint,
            },
        };

        string mixedFingerprint =
            MatchContractFingerprint.ComputeMatch(mixedSchemas);

        Assert.NotEqual(current.MatchContractFingerprint, mixedFingerprint);
        Assert.Equal(
            mixedFingerprint,
            MatchContractFingerprint.ComputeMatch(mixedSchemas));
    }

    [Fact]
    public void AggregateFingerprint_IncludesEachStoredSchemaAxisDirectly()
    {
        ArenaMap map = ArenaMap.Create(
            "contract",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        PublicMatchContractManifest current =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);
        PublicMatchContractManifest rulesSchemaChanged = current with
        {
            MatchContractFingerprint = "",
            Rules = current.Rules with
            {
                SchemaVersion = current.Rules.SchemaVersion + 1,
            },
        };
        PublicMatchContractManifest mapSchemaChanged = current with
        {
            MatchContractFingerprint = "",
            Map = current.Map with
            {
                SchemaVersion = current.Map.SchemaVersion + 1,
            },
        };
        PublicMatchContractManifest matchSchemaChanged = current with
        {
            SchemaVersion = current.SchemaVersion + 1,
            MatchContractFingerprint = "",
        };

        string rulesSchemaFingerprint =
            MatchContractFingerprint.ComputeMatch(rulesSchemaChanged);
        string mapSchemaFingerprint =
            MatchContractFingerprint.ComputeMatch(mapSchemaChanged);
        string matchSchemaFingerprint =
            MatchContractFingerprint.ComputeMatch(matchSchemaChanged);

        Assert.Equal(
            current.Map.MapFingerprint,
            rulesSchemaChanged.Map.MapFingerprint);
        Assert.NotEqual(current.MatchContractFingerprint, rulesSchemaFingerprint);
        Assert.NotEqual(current.MatchContractFingerprint, mapSchemaFingerprint);
        Assert.NotEqual(current.MatchContractFingerprint, matchSchemaFingerprint);
        Assert.Equal(
            rulesSchemaFingerprint,
            MatchContractFingerprint.ComputeMatch(rulesSchemaChanged));
        Assert.Equal(
            mapSchemaFingerprint,
            MatchContractFingerprint.ComputeMatch(mapSchemaChanged));
        Assert.Equal(
            matchSchemaFingerprint,
            MatchContractFingerprint.ComputeMatch(matchSchemaChanged));
    }

    [Fact]
    public void AggregateFingerprint_IncludesExactTopologyWithoutChangingComponentHashes()
    {
        ArenaMap map = ArenaMap.Create(
            "contract",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);
        PublicMatchTopology reidentified = new()
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(11, 1)],
            UnitSlots = [new(0, 0, 10), new(1, 0, 11)],
            InitialLives = [new(0, 0, 0, "mobile"), new(1, 0, 0, "mobile")],
        };
        PublicMatchContractManifest changed = baseline with
        {
            MatchContractFingerprint = "",
            Topology = reidentified,
        };

        string changedFingerprint = MatchContractFingerprint.ComputeMatch(changed);

        Assert.Equal(baseline.Rules.RulesFingerprint, changed.Rules.RulesFingerprint);
        Assert.Equal(baseline.Map.MapFingerprint, changed.Map.MapFingerprint);
        Assert.NotEqual(baseline.MatchContractFingerprint, changedFingerprint);
    }
}
