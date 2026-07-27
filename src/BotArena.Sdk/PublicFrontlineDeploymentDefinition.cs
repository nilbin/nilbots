namespace BotArena.Sdk;

/// <summary>Stable Frontline spawn, return, and protected-pad semantics.</summary>
public sealed record PublicFrontlineDeploymentDefinition(
    string PrimeDefaultFormId,
    string ChildDefaultFormId,
    PublicFrontlineDestructionTransitionClock DestructionTransitionClock,
    PublicFrontlinePrimeReturnPolicy PrimeReturn,
    PublicFrontlineChildReturnPolicy ChildReturn,
    PublicFrontlineNewLifePolicy NewLife,
    PublicFrontlinePrimeSpawnReservationPolicy PrimeSpawnReservation,
    PublicFrontlineProtectedPadPolicy ProtectedPad);
