namespace BotArena.Engine;

/// <summary>Immutable programmed projectile arc. Values are interpreted only when
/// <see cref="GameRules.AllowProgrammedShots"/> is enabled. InitialAimOffset and
/// BendDirection are signed 45-degree octants relative to facing/current heading.</summary>
public readonly record struct ShotProgram(
    int InitialAimOffset,
    int BendDirection,
    int BendAfterTiles,
    int BendEveryTiles,
    int BendCount)
{
    public static ShotProgram Straight => new(0, 0, 0, 1, 0);
}
