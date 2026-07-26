namespace BotArena.Engine;

/// <summary>
/// Public gameplay capabilities of one Frontline unit form. These values are
/// part of the rules input even before the multi-unit session consumes them.
/// </summary>
public sealed record UnitFormRules(
    string FormId,
    int MaxHealth,
    int VisionRange,
    int ShootCooldownTicks,
    bool OmnidirectionalVision,
    bool OmnidirectionalShooting,
    int ObjectiveWeight,
    bool CanMove,
    bool CanShoot,
    bool AllowsProgrammedShots);
