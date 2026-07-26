using System.Collections.Immutable;
using System.Text.Json.Nodes;
using BotArena.Engine;

namespace BotArena.Engine.Tests;

public class MatchDefinitionResolverTests
{
    [Fact]
    public void LegacyRulesAndMap_ResolveWithoutFrontline()
    {
        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(
            GameRules.Current,
            LegacyMap());

        Assert.False(definition.IsFrontline);
        Assert.Null(definition.FrontlineRules);
        Assert.Null(definition.FrontlineMapProfile);
        Assert.Equal(2, definition.Topology.Teams.Length);
        Assert.Equal(2, definition.Topology.UnitSlots.Length);
    }

    [Fact]
    public void FrontlineDefaults_ResolveTwoParticipantsAndThreeSlotsPerTeam()
    {
        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(
            FrontlineGameRules(),
            FrontlineMap());

        Assert.True(definition.IsFrontline);
        Assert.Equal([0, 1],
            definition.Topology.Teams.Select(team => team.TeamId));
        Assert.Equal(2, definition.Topology.Participants.Length);
        Assert.Equal(6, definition.Topology.UnitSlots.Length);
        Assert.All(
            definition.Topology.Teams,
            team => Assert.Equal(
                [0, 1, 2],
                definition.Topology.UnitSlots
                    .Where(slot => slot.TeamId == team.TeamId)
                    .Select(slot => slot.UnitId)));
        Assert.Equal(
            [
                new PublicInitialLife(0, 0, 0, "prime-mobile"),
                new PublicInitialLife(1, 0, 0, "prime-mobile"),
            ],
            definition.Topology.InitialLives.ToArray());
    }

    [Fact]
    public void MoreUnitSlotsRemainAResolvedRulesAndTopologyChange()
    {
        FrontlineRules frontline = new()
        {
            MaxUnitsPerTeam = 5,
            FabricationUnlockTicks = [120, 260, 320, 400],
        };

        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(
            FrontlineGameRules(frontline),
            FrontlineMap());

        Assert.Equal(10, definition.Topology.UnitSlots.Length);
        Assert.All(
            definition.Topology.Teams,
            team => Assert.Equal(
                [0, 1, 2, 3, 4],
                definition.Topology.UnitSlots
                    .Where(slot => slot.TeamId == team.TeamId)
                    .Select(slot => slot.UnitId)));
        Assert.Equal(2, definition.Topology.InitialLives.Length);
    }

    [Fact]
    public void RulesResolveAnotherOddMapPositionCountAndPushCount()
    {
        JsonNode root = LoadFrontlineJson();
        JsonArray positions = root["frontline"]!["positions"]!.AsArray();
        positions.RemoveAt(positions.Count - 1);
        positions.RemoveAt(0);
        ArenaMap map = ArenaMap.FromJson(root.ToJsonString());
        FrontlineRules frontline = new()
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
        };

        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(
            FrontlineGameRules(frontline),
            map);

        Assert.Equal(3, definition.FrontlineMapProfile!.Positions.Length);
        Assert.Equal(2, definition.FrontlineRules!.PushesToBreach);
    }

    [Fact]
    public void FrontlineRulesAndProfile_MustBePresentTogether()
    {
        MatchDefinitionValidationException rulesOnly =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(),
                    LegacyMap()));
        MatchDefinitionValidationException mapOnly =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    GameRules.V0_1,
                    FrontlineMap()));

        Assert.Contains(rulesOnly.Errors,
            error => error.Contains("must be present together"));
        Assert.Contains(mapOnly.Errors,
            error => error.Contains("must be present together"));
    }

    [Fact]
    public void LegacyObjectivesAndSeedSpawns_CannotLeakIntoFrontline()
    {
        GameRules conflicting = GameRules.Current with
        {
            Frontline = new FrontlineRules(),
        };

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(conflicting, FrontlineMap()));

        Assert.Contains(exception.Errors,
            error => error.Contains("Legacy zone-control"));
        Assert.Contains(exception.Errors,
            error => error.Contains("Legacy seed-spawn variation"));
    }

    [Fact]
    public void FrontlineRejectsMoreTeamsWhileTopologyTypeRemainsGeneric()
    {
        ResolvedMatchDefinition valid = MatchDefinitionResolver.Resolve(
            FrontlineGameRules(),
            FrontlineMap());
        PublicMatchTopology threeTeams = valid.Topology with
        {
            Teams = [.. valid.Topology.Teams, new PublicScoringTeam(2)],
        };

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(),
                    FrontlineMap(),
                    threeTeams));

        Assert.Contains(exception.Errors,
            error => error.Contains("exactly team IDs 0 and 1"));
    }

    [Fact]
    public void EveryUnitSlotAndInitialPrimeMustMatchResolvedRules()
    {
        ResolvedMatchDefinition valid = MatchDefinitionResolver.Resolve(
            FrontlineGameRules(),
            FrontlineMap());
        PublicMatchTopology missingSlot = valid.Topology with
        {
            UnitSlots = valid.Topology.UnitSlots
                .Where(slot => slot != new PublicUnitSlot(1, 2, 1))
                .ToImmutableArray(),
        };
        PublicMatchTopology wrongPrime = valid.Topology with
        {
            InitialLives =
            [
                new(0, 0, 0, "child-mobile"),
                new(1, 0, 0, "prime-mobile"),
            ],
        };

        MatchDefinitionValidationException slotException =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(),
                    FrontlineMap(),
                    missingSlot));
        MatchDefinitionValidationException lifeException =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(),
                    FrontlineMap(),
                    wrongPrime));

        Assert.Contains(slotException.Errors,
            error => error.Contains("unit slots 0 through 2"));
        Assert.Contains(lifeException.Errors,
            error => error.Contains("form 'prime-mobile'"));
    }

    [Fact]
    public void MalformedNullPrimeForm_IsAggregatedWithoutANullReference()
    {
        FrontlineRules malformed = new()
        {
            PrimeForm = null!,
        };

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(malformed),
                    FrontlineMap()));

        Assert.Contains(exception.Errors,
            error => error.Contains("Prime, child, and turret forms are required"));
    }

    [Fact]
    public void DefaultTopologyCollections_AreAggregatedWithoutANullReference()
    {
        var malformed = new PublicMatchTopology
        {
            Teams = default,
            Participants = default,
            UnitSlots = default,
            InitialLives = default,
        };

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(),
                    FrontlineMap(),
                    malformed));

        Assert.Contains(exception.Errors,
            error => error.Contains("initialized and non-empty"));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(0)]
    public void EffectiveLongOrUnlimitedProjectileRange_RejectsUnsafeAnchors(
        int shotRange)
    {
        FrontlineRules frontline = new()
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
        };
        GameRules rules = FrontlineGameRules(frontline) with
        {
            ShotRange = shotRange,
        };

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    rules,
                    RangeSensitiveFrontlineMap()));

        Assert.Contains(exception.Errors,
            error => error.Contains("can fire into team 0 Prime spawn"));
    }

    [Fact]
    public void EffectiveRangeEight_AcceptsTheRangeSensitiveAnchorBoundary()
    {
        FrontlineRules frontline = new()
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
        };

        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(
            FrontlineGameRules(frontline),
            RangeSensitiveFrontlineMap());

        Assert.True(definition.IsFrontline);
    }

    [Fact]
    public void ProgrammedCurve_RejectsAnAnchorHiddenFromStraightLineSafety()
    {
        FrontlineRules frontline = new()
        {
            TurretForm = new FrontlineRules().TurretForm with
            {
                AllowsProgrammedShots = true,
            },
        };

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                MatchDefinitionResolver.Resolve(
                    FrontlineGameRules(frontline),
                    FrontlineMap()));

        Assert.Contains(exception.Errors,
            error => error.Contains("Anchor tile (4,3)")
                && error.Contains("program")
                && error.Contains("BendCount = 2"));
    }

    private static GameRules FrontlineGameRules(
        FrontlineRules? frontline = null) =>
        GameRules.V0_1 with
        {
            RulesVersion = "frontline-test",
            ShotRange = 8,
            VisionCone = true,
            HearingRadius = 8,
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
            AllowProgrammedShots = true,
            ProgrammedShotLaunchTiles = 1,
            Frontline = frontline ?? new FrontlineRules(),
        };

    private static ArenaMap LegacyMap() =>
        ArenaMap.Create(
            "legacy",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);

    private static ArenaMap FrontlineMap() =>
        ArenaMap.FromJson(File.ReadAllText(FrontlineMapPath()));

    private static ArenaMap RangeSensitiveFrontlineMap() =>
        ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "range-sensitive",
          "version": 1,
          "width": 23,
          "height": 3,
          "tiles": [
            "#######################",
            "#.....................#",
            "#######################"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 1, "facing": "East" },
            { "teamId": 1, "x": 21, "y": 1, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[5,1]] },
              { "tiles": [[11,1]] },
              { "tiles": [[17,1]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[1,1], [2,1]] },
              { "teamId": 1, "tiles": [[20,1], [21,1]] }
            ],
            "anchorForbiddenTiles": [
              [1,1], [2,1], [3,1], [4,1], [5,1], [6,1], [7,1], [8,1], [9,1],
              [11,1],
              [13,1], [14,1], [15,1], [16,1], [17,1], [18,1], [19,1], [20,1], [21,1]
            ]
          }
        }
        """);

    private static JsonNode LoadFrontlineJson() =>
        JsonNode.Parse(File.ReadAllText(FrontlineMapPath()))
        ?? throw new InvalidOperationException("Frontline map JSON was empty.");

    private static string FrontlineMapPath() =>
        Path.Combine(
            FindRepoRoot(),
            "maps",
            "experimental",
            "frontline-01.json");

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
}
