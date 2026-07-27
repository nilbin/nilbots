namespace BotArena.Engine;

/// <summary>
/// Stable, non-numeric Frontline deployment semantics. Numeric unlock,
/// respawn, and rebuild inputs remain in
/// <see cref="PublicFrontlineLifecycleDefinition"/>.
/// </summary>
public sealed record PublicFrontlineDeploymentDefinition(
    string PrimeDefaultFormId,
    string ChildDefaultFormId,
    PublicFrontlineDestructionTransitionClock DestructionTransitionClock,
    PublicFrontlinePrimeReturnPolicy PrimeReturn,
    PublicFrontlineChildReturnPolicy ChildReturn,
    PublicFrontlineNewLifePolicy NewLife,
    PublicFrontlinePrimeSpawnReservationPolicy PrimeSpawnReservation,
    PublicFrontlineProtectedPadPolicy ProtectedPad);
