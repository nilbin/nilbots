namespace BotArena.Sdk;

public enum PublicFrontlineDestructionTransitionClock
{
    TickStartAtDestroyedTickPlusOnePlusDelay = 0,
}

public enum PublicFrontlinePrimeReturnPolicy
{
    AutomaticAtAuthoredPrimeSpawn = 0,
}

public enum PublicFrontlineChildReturnPolicy
{
    ReadyThenExplicitFabrication = 0,
}

public enum PublicFrontlineNewLifePolicy
{
    FreshRuntimeFormDefaultsHomeFacingCanActOnCreationTick = 0,
}

public enum PublicFrontlinePrimeSpawnReservationPolicy
{
    PermanentAgainstOwnChildren = 0,
}

public enum PublicFrontlineProtectedPadPolicy
{
    EnemyGroundEntryBlockedNoDamageImmunityNoProjectileBlocking = 0,
}

public enum PublicFrontlineInitialPositionPolicy
{
    CentrePositionIndex = 0,
}

public readonly record struct PublicFrontlineTeamAdvance(
    int TeamId,
    int PositionIndexDelta);

public enum PublicFrontlineCompletionPrecedence
{
    BaseBreachBeforeMaxTicks = 0,
}

public enum PublicFrontlineTimeoutResolution
{
    SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers = 0,
}

public enum PublicFrontlineProjectileAttributionPolicy
{
    ExactFiringLifePersistsCreditsStableUnitByActualHealthRemoved = 0,
}

public enum PublicFrontlineAnchorCompletionPolicy
{
    EndOfStartedTickPlusWindupMinusOneAfterObjective = 0,
}

public enum PublicFrontlineAnchorHealthPolicy
{
    MinimumTargetMaximumAndCurrentPlusGain = 0,
}

public enum PublicFrontlineAnchorPendingActionPolicy
{
    WaitOnly = 0,
}

public enum PublicFrontlineAnchorSurvivingDamagePolicy
{
    DoesNotCancel = 0,
}

public enum PublicFrontlineAnchorDeathPolicy
{
    CancelsWithExplicitEvent = 0,
}

public enum PublicFrontlineAnchorForbiddenTilePolicy
{
    AllMapAnchorForbiddenTilesIllegal = 0,
}

public enum PublicFrontlineAnchorPendingFormPolicy
{
    SourceFormUntilCompletion = 0,
}

public enum PublicFrontlineAnchorStateContinuityPolicy
{
    SameLifeRuntimeMemoryPositionFacingCooldownEnergyAndDamage = 0,
}

public enum PublicFrontlineAnchorTerminalPolicy
{
    PreserveFuturePendingWithoutSyntheticCancellation = 0,
}

public enum PublicFrontlineTurretFireAimPolicy
{
    AbsoluteEightWayLaunchHeading = 0,
}

public enum PublicFrontlineTurretFireProjectilePolicy
{
    OneStraightNonProgrammedProjectile = 0,
}

public enum PublicFrontlineTurretFireFacingPolicy
{
    BodyFacingUnchanged = 0,
}

public enum PublicFrontlineTurretFireRangePolicy
{
    GlobalProjectileRange = 0,
}

public enum PublicFrontlineTurretFireResourcePolicy
{
    StandardEnergyCooldownAndDamage = 0,
}

public enum PublicFrontlineTurretFireTraversalPolicy
{
    StandardTraversalStrictDiagonalCorners = 0,
}
