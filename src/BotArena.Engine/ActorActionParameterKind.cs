namespace BotArena.Engine;

/// <summary>
/// Closed payload fields for actor decisions. Movement/rotation use cardinal
/// directions; omnidirectional attacks use eight-way projectile headings.
/// Legality masks supply the allowed values per tick.
/// </summary>
public enum ActorActionParameterKind
{
    ShotProgram = 0,
    Direction = 1,
    UnitTarget = 2,
    FormTarget = 3,
    ProjectileHeading = 4,

    /// <summary>
    /// Names one declared upgrade track. Additive append; the legality mask
    /// carries the tracks that are affordable and uncapped this tick, so a
    /// bot reading its mask never does the arithmetic.
    /// </summary>
    UpgradeTrack = 5,
}
