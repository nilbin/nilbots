namespace BotArena.Engine;

/// <summary>
/// Public gameplay capabilities of one Frontline unit form. These values are
/// authoritative rules input for validation, action masks, and resolution.
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
    bool AllowsProgrammedShots)
{
    /// <summary>
    /// Whether the cardinal body facing may change. Kept as an additive
    /// capability so historical form construction remains source-compatible.
    /// </summary>
    public bool CanRotate { get; init; } = true;
}
