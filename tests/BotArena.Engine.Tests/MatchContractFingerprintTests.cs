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
    public void AggregateFingerprint_UsesStoredSchemaAndRejectsMixedSchemas()
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
        int storedSchema = current.SchemaVersion + 1;
        PublicMatchContractManifest futureShaped = current with
        {
            SchemaVersion = storedSchema,
            MatchContractFingerprint = "",
            Rules = current.Rules with { SchemaVersion = storedSchema },
            Map = current.Map with { SchemaVersion = storedSchema },
        };

        string futureFingerprint = MatchContractFingerprint.ComputeMatch(futureShaped);

        Assert.NotEqual(current.MatchContractFingerprint, futureFingerprint);
        Assert.Equal(
            futureFingerprint,
            MatchContractFingerprint.ComputeMatch(futureShaped));
        Assert.Throws<ArgumentException>(() =>
            MatchContractFingerprint.ComputeMatch(
                futureShaped with
                {
                    Map = futureShaped.Map with { SchemaVersion = storedSchema + 1 },
                }));
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
