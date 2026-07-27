using BotArena.Engine;

namespace BotArena.Engine.Tests.Support;

/// <summary>
/// Small, resolver-valid Frontline definitions for Package 3 session tests.
/// These fixtures deliberately model only the two initial Primes.
/// </summary>
public static class FrontlineTestDefinitions
{
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
