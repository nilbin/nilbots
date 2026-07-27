using BotArena.Engine;

namespace BotArena.Engine.Tests.Support;

/// <summary>
/// Small, resolver-valid Frontline definitions for entity-session tests.
/// Individual helpers opt into Prime-only or replicated stable-unit topology.
/// </summary>
public static class FrontlineTestDefinitions
{
    public static GameRules ReplicationRules(
        int maxTicks = 20,
        int firstUnlockTick = 1,
        int secondUnlockTick = 2,
        int childRebuildTicks = 2,
        int primeRespawnTicks = 3,
        int shootCooldownTicks = 0)
    {
        GameRules baseline = PrimeOnlyRules(
            maxTicks,
            primeRespawnTicks,
            shootCooldownTicks: shootCooldownTicks);
        return baseline with
        {
            RulesVersion = "frontline-replication-test",
            Frontline = baseline.Frontline! with
            {
                MaxUnitsPerTeam = 3,
                ChildRebuildTicks = childRebuildTicks,
                FabricationUnlockTicks =
                    [firstUnlockTick, secondUnlockTick],
            },
        };
    }

    public static GameRules PrimeOnlyRules(
        int maxTicks = 100,
        int primeRespawnTicks = 3,
        int captureThreshold = 3,
        int captureGainPerSoleTeamTick = 1,
        int captureDecayAmount = 1,
        int captureDecayIntervalTicks = 2,
        int redeployPauseTicks = 1,
        int shootCooldownTicks = 2,
        int projectileTicksPerTile = 1,
        int projectileTilesPerAdvance = 2)
    {
        FrontlineRules defaults = new();
        var frontline = defaults with
        {
            FrontlinePositionCount = 3,
            InitialUnitsPerTeam = 1,
            MaxUnitsPerTeam = 1,
            CaptureThreshold = captureThreshold,
            CaptureGainPerSoleTeamTick = captureGainPerSoleTeamTick,
            CaptureDecayAmount = captureDecayAmount,
            CaptureDecayIntervalTicks = captureDecayIntervalTicks,
            RedeployPauseTicks = redeployPauseTicks,
            PushesToBreach = 2,
            PrimeRespawnTicks = primeRespawnTicks,
            FabricationUnlockTicks = [],
            PrimeForm = defaults.PrimeForm with
            {
                ShootCooldownTicks = shootCooldownTicks,
            },
        };

        return GameRules.V0_1 with
        {
            RulesVersion = "frontline-prime-only-test",
            MaxTicks = maxTicks,
            ShootCooldownTicks = shootCooldownTicks,
            ShotRange = 8,
            SeedSpawnVariation = false,
            ZoneControl = false,
            ZoneDominationTicks = 0,
            ZoneExclusiveAccrual = false,
            ActiveZoneControl = false,
            ControlBySoleOccupancy = false,
            ControlPressureLimit = 0,
            ControlOvertimeStartTick = 0,
            SpawnLaneSafety = false,
            ZoneSpawnFairness = false,
            ExhaustiveSpawns = false,
            ReplayZoneTallies = false,
            SeedProfile = null,
            VisionCone = true,
            HearingRadius = 8,
            ProjectileTicksPerTile = projectileTicksPerTile,
            ProjectileTilesPerAdvance = projectileTilesPerAdvance,
            AllowProgrammedShots = true,
            ProgrammedShotMaxInitialAimOctants = 1,
            ProgrammedShotMaxBendAfterTiles = 4,
            ProgrammedShotMaxBendEveryTiles = 3,
            ProgrammedShotMaxBendCount = 3,
            ProgrammedShotLaunchTiles = 1,
            Frontline = frontline,
        };
    }

    public static ArenaMap OpenMapV2() => ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "frontline-test-open",
          "version": 1,
          "width": 9,
          "height": 5,
          "tiles": [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 2, "facing": "East" },
            { "teamId": 1, "x": 7, "y": 2, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[2,1]] },
              { "tiles": [[4,1]] },
              { "tiles": [[6,1]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[1,2]] },
              { "teamId": 1, "tiles": [[7,2]] }
            ],
            "anchorForbiddenTiles": [
              [1,1], [2,1], [3,1], [4,1], [5,1], [6,1], [7,1],
              [1,2], [2,2], [3,2], [4,2], [5,2], [6,2], [7,2],
              [1,3], [2,3], [3,3], [4,3], [5,3], [6,3], [7,3]
            ]
          }
        }
        """);

    public static ArenaMap ReplicationMapV2() => ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "frontline-test-replication",
          "version": 1,
          "width": 13,
          "height": 7,
          "tiles": [
            "#############",
            "#...........#",
            "#...........#",
            "#...........#",
            "#...........#",
            "#...........#",
            "#############"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 3, "facing": "East" },
            { "teamId": 1, "x": 11, "y": 3, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[3,1]] },
              { "tiles": [[6,1]] },
              { "tiles": [[9,1]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[2,4],[1,3],[1,4]] },
              { "teamId": 1, "tiles": [[11,4],[10,4],[11,3]] }
            ],
            "anchorForbiddenTiles": [
              [1,1],[2,1],[3,1],[4,1],[5,1],[6,1],[7,1],[8,1],[9,1],[10,1],[11,1],
              [1,2],[2,2],[3,2],[4,2],[5,2],[6,2],[7,2],[8,2],[9,2],[10,2],[11,2],
              [1,3],[2,3],[3,3],[4,3],[5,3],[6,3],[7,3],[8,3],[9,3],[10,3],[11,3],
              [1,4],[2,4],[3,4],[4,4],[5,4],[6,4],[7,4],[8,4],[9,4],[10,4],[11,4],
              [1,5],[2,5],[3,5],[4,5],[5,5],[6,5],[7,5],[8,5],[9,5],[10,5],[11,5]
            ]
          }
        }
        """);

    public static ArenaMap ObjectiveMapV2() => ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "frontline-test-objective",
          "version": 1,
          "width": 9,
          "height": 5,
          "tiles": [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 2, "facing": "East" },
            { "teamId": 1, "x": 7, "y": 2, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[2,2]] },
              { "tiles": [[4,2]] },
              { "tiles": [[6,2]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[1,2]] },
              { "teamId": 1, "tiles": [[7,2]] }
            ],
            "anchorForbiddenTiles": [
              [1,1], [2,1], [3,1], [4,1], [5,1], [6,1], [7,1],
              [1,2], [2,2], [3,2], [4,2], [5,2], [6,2], [7,2],
              [1,3], [2,3], [3,3], [4,3], [5,3], [6,3], [7,3]
            ]
          }
        }
        """);

    public static ArenaMap AnchorMapV2() => ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "frontline-test-anchor",
          "version": 1,
          "width": 15,
          "height": 9,
          "tiles": [
            "###############",
            "#..#.......#..#",
            "#..#.......#..#",
            "#..#.......#..#",
            "#..#..#....#..#",
            "#..#.......#..#",
            "#.............#",
            "#..#.......#..#",
            "###############"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 4, "facing": "East" },
            { "teamId": 1, "x": 13, "y": 4, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[5,2]] },
              { "tiles": [[7,2]] },
              { "tiles": [[9,2]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[1,3],[2,3],[1,4],[2,4],[1,5],[2,5]] },
              { "teamId": 1, "tiles": [[12,3],[13,3],[12,4],[13,4],[12,5],[13,5]] }
            ],
            "anchorForbiddenTiles": [
              [1,1],[2,1],[12,1],[13,1],
              [1,2],[2,2],[5,2],[7,2],[9,2],[12,2],[13,2],
              [1,3],[2,3],[12,3],[13,3],
              [1,4],[2,4],[12,4],[13,4],
              [1,5],[2,5],[12,5],[13,5],
              [1,6],[2,6],[12,6],[13,6],
              [1,7],[2,7],[4,7],[10,7],[12,7],[13,7]
            ]
          }
        }
        """);

    public static ArenaMap OpenMapV1() => ArenaMap.FromJson("""
        {
          "formatVersion": 1,
          "id": "frontline-test-open-v1",
          "version": 1,
          "width": 9,
          "height": 5,
          "tiles": [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########"
          ],
          "spawns": [
            { "x": 1, "y": 2, "facing": "East" },
            { "x": 7, "y": 2, "facing": "West" }
          ]
        }
        """);

    public static ResolvedMatchDefinition ResolveOpen(GameRules? rules = null) =>
        MatchDefinitionResolver.Resolve(rules ?? PrimeOnlyRules(), OpenMapV2());

    public static ResolvedMatchDefinition ResolveObjective(GameRules? rules = null) =>
        MatchDefinitionResolver.Resolve(rules ?? PrimeOnlyRules(), ObjectiveMapV2());

    public static ArenaMap Frontline01() =>
        ArenaMap.FromJson(File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "maps",
            "experimental",
            "frontline-01.json")));

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
