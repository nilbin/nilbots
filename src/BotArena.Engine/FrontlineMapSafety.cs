using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Deterministic rules-aware checks for map geometry that cannot be proven by
/// the format-v2 parser alone.
/// </summary>
public static class FrontlineMapSafety
{
    /// <summary>
    /// Returns one representative firing program for every otherwise legal
    /// Anchor tile that can reach an authored Prime spawn. An omnidirectional
    /// turret may launch on any of the eight public projectile headings; a
    /// directional turret may still arrive with any cardinal body facing.
    /// </summary>
    public static ImmutableArray<FrontlineAnchorSpawnThreat> FindAnchorSpawnThreats(
        GameRules outerRules,
        ArenaMap map,
        FrontlineRules frontlineRules,
        FrontlineMapProfile profile)
    {
        ArgumentNullException.ThrowIfNull(outerRules);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(frontlineRules);
        ArgumentNullException.ThrowIfNull(profile);

        if (outerRules.ShotRange < 0
            || frontlineRules.TurretForm is not { CanShoot: true })
        {
            return [];
        }

        int traceRange = outerRules.ShotRange == 0
            ? checked(map.Width * map.Height * 8)
            : outerRules.ShotRange;
        GameRules traceRules = outerRules with { ShotRange = traceRange };
        ImmutableArray<ShotProgram> programs = EnumerateShotPrograms(
            outerRules,
            frontlineRules.TurretForm.AllowsProgrammedShots);
        ImmutableArray<ProjectileHeading> launchHeadings =
            EnumerateLaunchHeadings(frontlineRules.TurretForm);
        var forbidden = profile.AnchorForbiddenTiles.ToHashSet();
        var threats = ImmutableArray.CreateBuilder<FrontlineAnchorSpawnThreat>();

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var candidate = new Position(x, y);
                if (map.IsWall(candidate) || forbidden.Contains(candidate))
                    continue;

                FrontlineAnchorSpawnThreat? threat = FindThreat(
                    map,
                    traceRules,
                    profile,
                    launchHeadings,
                    programs,
                    candidate);
                if (threat is not null)
                    threats.Add(threat);
            }
        }

        return threats.ToImmutable();
    }

    private static FrontlineAnchorSpawnThreat? FindThreat(
        ArenaMap map,
        GameRules traceRules,
        FrontlineMapProfile profile,
        ImmutableArray<ProjectileHeading> launchHeadings,
        ImmutableArray<ShotProgram> programs,
        Position candidate)
    {
        foreach (FrontlineTeamHome home in profile.TeamHomes
                     .OrderBy(home => home.TeamId))
        {
            var spawn = new Position(home.PrimeSpawn.X, home.PrimeSpawn.Y);
            foreach (ProjectileHeading launchHeading in launchHeadings)
            {
                foreach (ShotProgram program in programs)
                {
                    if (!ProgrammedProjectilePath
                            .Trace(
                                map,
                                candidate,
                                launchHeading,
                                program,
                                traceRules)
                            .Contains(spawn))
                    {
                        continue;
                    }

                    return new FrontlineAnchorSpawnThreat(
                        candidate,
                        home.TeamId,
                        spawn,
                        launchHeading,
                        program);
                }
            }
        }

        return null;
    }

    private static ImmutableArray<ProjectileHeading> EnumerateLaunchHeadings(
        UnitFormRules turretForm) =>
        turretForm.OmnidirectionalShooting
            ? Enum.GetValues<ProjectileHeading>().ToImmutableArray()
            :
            [
                ProjectileHeading.North,
                ProjectileHeading.East,
                ProjectileHeading.South,
                ProjectileHeading.West,
            ];

    private static ImmutableArray<ShotProgram> EnumerateShotPrograms(
        GameRules rules,
        bool formAllowsProgrammedShots)
    {
        if (!rules.AllowProgrammedShots || !formAllowsProgrammedShots)
            return [ShotProgram.Straight];

        var programs = new HashSet<ShotProgram> { ShotProgram.Straight };
        int maxInitialAim = Math.Max(0, rules.ProgrammedShotMaxInitialAimOctants);
        int maxBendAfter = Math.Max(0, rules.ProgrammedShotMaxBendAfterTiles);
        int maxBendEvery = Math.Max(0, rules.ProgrammedShotMaxBendEveryTiles);
        int maxBendCount = Math.Max(0, rules.ProgrammedShotMaxBendCount);

        for (int initialAim = -maxInitialAim; initialAim <= maxInitialAim; initialAim++)
        {
            programs.Add(new ShotProgram(initialAim, 0, 0, 1, 0));
            foreach (int bendDirection in new[] { -1, 1 })
            {
                for (int bendAfter = 1; bendAfter <= maxBendAfter; bendAfter++)
                {
                    for (int bendEvery = 1; bendEvery <= maxBendEvery; bendEvery++)
                    {
                        for (int bendCount = 1; bendCount <= maxBendCount; bendCount++)
                        {
                            programs.Add(new ShotProgram(
                                initialAim,
                                bendDirection,
                                bendAfter,
                                bendEvery,
                                bendCount));
                        }
                    }
                }
            }
        }

        return programs
            .OrderBy(program => program.InitialAimOffset)
            .ThenBy(program => program.BendDirection)
            .ThenBy(program => program.BendAfterTiles)
            .ThenBy(program => program.BendEveryTiles)
            .ThenBy(program => program.BendCount)
            .ToImmutableArray();
    }
}

/// <summary>Representative rules-valid path from one legal Anchor to a Prime spawn.</summary>
public sealed record FrontlineAnchorSpawnThreat(
    Position AnchorTile,
    int TeamId,
    Position PrimeSpawn,
    ProjectileHeading LaunchHeading,
    ShotProgram Program);
