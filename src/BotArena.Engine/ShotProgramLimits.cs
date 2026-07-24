namespace BotArena.Engine;

/// <summary>Public rules envelope for privately programmed projectile paths.</summary>
public sealed record ShotProgramLimits(
    int MaxInitialAimOctants,
    int MaxBendAfterTiles,
    int MaxBendEveryTiles,
    int MaxBendCount,
    int MaxPathTiles,
    int LaunchTiles,
    int TilesPerAdvance);
