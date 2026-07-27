namespace BotArena.Engine;

/// <summary>
/// A destruction on tick D schedules the due transition at tick start
/// D + 1 + the applicable public delay.
/// </summary>
public enum PublicFrontlineDestructionTransitionClock
{
    TickStartAtDestroyedTickPlusOnePlusDelay = 0,
}

/// <summary>
/// Prime lives return automatically at the map-authored Prime spawn and
/// facing when their lifecycle transition becomes due.
/// </summary>
public enum PublicFrontlinePrimeReturnPolicy
{
    AutomaticAtAuthoredPrimeSpawn = 0,
}

/// <summary>
/// A rebuilt child becomes Ready when due and remains absent until its Prime
/// successfully submits another explicit Fabricate action.
/// </summary>
public enum PublicFrontlineChildReturnPolicy
{
    ReadyThenExplicitFabrication = 0,
}

/// <summary>
/// Every new life receives a fresh runtime and private memory. It starts with
/// form max health, zero cooldown, max enabled energy, no previous action,
/// zero life damage, and the team's authored Prime-spawn facing. Because due
/// transitions run before observations, it may decide on its creation tick.
/// </summary>
public enum PublicFrontlineNewLifePolicy
{
    FreshRuntimeFormDefaultsHomeFacingCanActOnCreationTick = 0,
}

/// <summary>
/// The map-authored Prime spawn is permanently unavailable to that team's
/// child ground movement, whether or not the Prime is currently alive.
/// </summary>
public enum PublicFrontlinePrimeSpawnReservationPolicy
{
    PermanentAgainstOwnChildren = 0,
}

/// <summary>
/// Enemy ground movement cannot enter a protected pad. The pad grants no
/// damage immunity and does not block or consume projectiles.
/// </summary>
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

/// <summary>
/// Objective resolution wins a base breach before the same final allowed
/// tick can terminate as a max-tick result.
/// </summary>
public enum PublicFrontlineCompletionPrecedence
{
    BaseBreachBeforeMaxTicks = 0,
}

/// <summary>
/// At timeout, score is
/// (active index - centre index) * capture threshold, plus capture progress
/// signed by the claiming team's position-index delta. Positive awards the
/// increasing-index team, negative the decreasing-index team, and zero draws.
/// Health and damage are recorded facts and never break the tie.
/// </summary>
public enum PublicFrontlineTimeoutResolution
{
    SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers = 0,
}

/// <summary>
/// Projectiles retain their exact firing life as owner after that life is
/// destroyed. They continue travelling, and actual health removed is credited
/// to the firing life's stable unit rather than a later life.
/// </summary>
public enum PublicFrontlineProjectileAttributionPolicy
{
    ExactFiringLifePersistsCreditsStableUnitByActualHealthRemoved = 0,
}

/// <summary>
/// A transition submitted on tick T completes after that tick's objective at
/// the end of T + N - 1, where N is the public windup.
/// </summary>
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
