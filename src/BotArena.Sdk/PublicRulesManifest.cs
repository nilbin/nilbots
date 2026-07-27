using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Exact immutable gameplay rules for this match. Values may vary by curated
/// map, ruleset, or season; bots should reason from this contract rather than
/// hard-coded global constants.
/// </summary>
public sealed record PublicRulesManifest
{
    public required int SchemaVersion { get; init; }
    public required string RulesetId { get; init; }
    public required string RulesFingerprint { get; init; }
    public required PublicMatchLimits Limits { get; init; }
    public required PublicObjectiveRules Objective { get; init; }
    public required PublicFrontlineDefinition? Frontline { get; init; }
    public required PublicEnergyRules Energy { get; init; }
    public required ImmutableArray<PublicFormDefinition> Forms { get; init; }
    public required ImmutableArray<PublicActionDefinition> Actions { get; init; }
    public required PublicProjectileRules Projectiles { get; init; }
    public required PublicShotProgramRules ShotPrograms { get; init; }
    public required PublicVisionRules Vision { get; init; }
    public required PublicCollisionRules Collisions { get; init; }
    public required PublicTickResolutionRules TickResolution { get; init; }
}

public sealed record PublicMatchLimits(
    int MaxTicks,
    int FaultLimit,
    int TeamCount,
    int ParticipantCount,
    int UnitSlotCount,
    int InitialUnitsPerTeam,
    int MaxUnitsPerTeam,
    bool DestructionEndsMatch,
    bool RespawnsEnabled);

public enum PublicObjectiveMode
{
    None = 0,
    ZoneTicks = 1,
    SharedPressure = 2,
    Frontline = 3,
}

public enum PublicScoreMetric
{
    Objective = 0,
    Health = 1,
    DamageDealt = 2,
}

public sealed record PublicObjectiveRules(
    PublicObjectiveMode Mode,
    bool ZoneControlEnabled,
    int ZoneDominationTicks,
    bool ZoneExclusiveAccrual,
    bool SharedPressureEnabled,
    bool ControlBySoleOccupancy,
    int ControlPressureLimit,
    int ControlPressureGain,
    int ControlPressureDecayInterval,
    PublicObjectiveOvertimeRules Overtime,
    ImmutableArray<PublicScoreMetric> MaxTickTiebreakers);

public sealed record PublicObjectiveOvertimeRules(
    int StartTick,
    int PressureLimit,
    int PressureGain,
    bool StopsDecay);

public sealed record PublicFrontlineDefinition(
    int TeamCount,
    int ParticipantsPerTeam,
    int FrontlinePositionCount,
    int InitialUnitsPerTeam,
    int MaxUnitsPerTeam,
    TeamPerceptionMode TeamPerception,
    PublicFrontlineCaptureDefinition Capture,
    PublicFrontlineVictoryDefinition Victory,
    PublicFrontlineLifecycleDefinition Lifecycle,
    PublicFrontlineDeploymentDefinition Deployment,
    PublicFrontlineFabricationDefinition Fabrication,
    PublicFrontlineAnchorDefinition Anchor,
    PublicFrontlineAlliedCombatDefinition AlliedCombat)
{
    public PublicFrontlineTurretFireDefinition TurretFire { get; init; } =
        PublicFrontlineTurretFireDefinition.Default;
}

public sealed record PublicFrontlineCaptureDefinition(
    int Threshold,
    int GainPerSoleTeamTick,
    int DecayAmount,
    int DecayIntervalTicks,
    int RedeployPauseTicks,
    int PushesToBreach,
    PublicFrontlineCapturePresencePolicy Presence,
    PublicFrontlineNonSolePresencePolicy NonSolePresence,
    PublicFrontlineCounterCapturePolicy CounterCapture);

public enum PublicFrontlineCapturePresencePolicy
{
    BinaryPositiveWeightPerTeamNoStacking = 0,
}

public enum PublicFrontlineNonSolePresencePolicy
{
    DecayExistingClaim = 0,
}

public enum PublicFrontlineCounterCapturePolicy
{
    ErodeToNeutralBeforeClaim = 0,
}

public sealed record PublicFrontlineLifecycleDefinition(
    int PrimeRespawnTicks,
    int ChildRebuildTicks,
    ImmutableArray<int> FabricationUnlockTicks);

public sealed record PublicFrontlineAnchorDefinition(
    int WindupTicks,
    int HealthGain,
    bool IrreversibleForLife)
{
    public string ActionId { get; init; } = ActorActionIds.Transform;
    public string SourceFormId { get; init; } = "child-mobile";
    public string TargetFormId { get; init; } = "turret";
    public bool ConsumesTick { get; init; } = true;
    public PublicFrontlineAnchorCompletionPolicy Completion { get; init; } =
        PublicFrontlineAnchorCompletionPolicy
            .EndOfStartedTickPlusWindupMinusOneAfterObjective;
    public PublicFrontlineAnchorPendingActionPolicy PendingActions { get; init; } =
        PublicFrontlineAnchorPendingActionPolicy.WaitOnly;
    public PublicFrontlineAnchorSurvivingDamagePolicy SurvivingDamage
    {
        get;
        init;
    } = PublicFrontlineAnchorSurvivingDamagePolicy.DoesNotCancel;
    public PublicFrontlineAnchorDeathPolicy Death { get; init; } =
        PublicFrontlineAnchorDeathPolicy.CancelsWithExplicitEvent;
    public PublicFrontlineAnchorForbiddenTilePolicy ForbiddenTiles { get; init; } =
        PublicFrontlineAnchorForbiddenTilePolicy
            .AllMapAnchorForbiddenTilesIllegal;
    public PublicFrontlineAnchorPendingFormPolicy PendingForm { get; init; } =
        PublicFrontlineAnchorPendingFormPolicy.SourceFormUntilCompletion;
    public PublicFrontlineAnchorHealthPolicy Health { get; init; } =
        PublicFrontlineAnchorHealthPolicy
            .MinimumTargetMaximumAndCurrentPlusGain;
    public PublicFrontlineAnchorStateContinuityPolicy StateContinuity
    {
        get;
        init;
    } = PublicFrontlineAnchorStateContinuityPolicy
        .SameLifeRuntimeMemoryPositionFacingCooldownEnergyAndDamage;
    public PublicFrontlineAnchorTerminalPolicy Terminal { get; init; } =
        PublicFrontlineAnchorTerminalPolicy
            .PreserveFuturePendingWithoutSyntheticCancellation;
}

public sealed record PublicFrontlineTurretFireDefinition(
    string ActionId,
    string FormId,
    ImmutableArray<ProjectileHeading> AllowedProjectileHeadings,
    PublicFrontlineTurretFireAimPolicy Aim,
    PublicFrontlineTurretFireProjectilePolicy Projectile,
    PublicFrontlineTurretFireFacingPolicy Facing,
    PublicFrontlineTurretFireRangePolicy Range,
    PublicFrontlineTurretFireResourcePolicy Resources,
    PublicFrontlineTurretFireTraversalPolicy Traversal)
{
    public static PublicFrontlineTurretFireDefinition Default => new(
        ActorActionIds.ShootDirection,
        "turret",
        Enum.GetValues<ProjectileHeading>().ToImmutableArray(),
        PublicFrontlineTurretFireAimPolicy.AbsoluteEightWayLaunchHeading,
        PublicFrontlineTurretFireProjectilePolicy
            .OneStraightNonProgrammedProjectile,
        PublicFrontlineTurretFireFacingPolicy.BodyFacingUnchanged,
        PublicFrontlineTurretFireRangePolicy.GlobalProjectileRange,
        PublicFrontlineTurretFireResourcePolicy
            .StandardEnergyCooldownAndDamage,
        PublicFrontlineTurretFireTraversalPolicy
            .StandardTraversalStrictDiagonalCorners);
}

public sealed record PublicFrontlineAlliedCombatDefinition(
    bool FriendlyFireEnabled,
    bool AlliedProjectilesBlock,
    PublicFrontlineProjectileAttributionPolicy ProjectileAttribution);

public sealed record PublicEnergyRules(
    bool Enabled,
    int MaxEnergy,
    int ShotEnergyCost,
    int RegenerationIntervalTicks,
    int RegenerationAmount);

public enum PublicMovementLayer
{
    Ground = 0,
}

public sealed record PublicFormDefinition(
    string Id,
    int MaxHealth,
    int VisionRange,
    int ShootCooldownTicks,
    bool OmnidirectionalVision,
    bool OmnidirectionalShooting,
    PublicMovementLayer MovementLayer,
    int ObjectiveWeight,
    bool CanMove,
    bool CanShoot,
    bool AllowsProgrammedShots,
    ImmutableArray<string> AllowedActionIds);

public enum PublicActionKind
{
    Wait = 0,
    Movement = 1,
    Rotation = 2,
    Attack = 3,
    Fabrication = 4,
    Transformation = 5,
}

public enum PublicActionParameterKind
{
    ShotProgram = 0,
    Direction = 1,
    UnitTarget = 2,
    FormTarget = 3,
    ProjectileHeading = 4,
}

public sealed record PublicActionDefinition(
    string Id,
    int Code,
    PublicActionKind Kind,
    ImmutableArray<PublicActionParameterKind> ParameterKinds,
    bool Enabled);

public enum PublicProjectileMode
{
    InstantRay = 0,
    Discrete = 1,
}

public sealed record PublicProjectileRules(
    PublicProjectileMode Mode,
    int DamagePerHit,
    int MaxTravelTiles,
    int ShootCooldownTicks,
    int TicksPerAdvance,
    int TilesPerAdvance,
    int LaunchTiles,
    bool AdvancesOnLaunchTick,
    bool DamageAppliedSimultaneously);

public sealed record PublicShotProgramRules(
    bool Enabled,
    int HeadingSectors,
    int BendStepOctants,
    int MinInitialAimOctants,
    int MaxInitialAimOctants,
    PublicAimOnlyShotProgramRules AimOnlyProgram,
    ImmutableArray<int> AllowedCurvedBendDirections,
    int MinBendAfterTiles,
    int MaxBendAfterTiles,
    int MinBendEveryTiles,
    int MaxBendEveryTiles,
    int MinBendCount,
    int MaxBendCount,
    int LaunchTiles,
    bool PayloadOptional,
    PublicShotProgramValue DefaultProgram,
    PublicActionRejectionResult? InvalidPayloadResult,
    PublicActionRejectionResult UnsupportedPayloadResult,
    bool DiagonalCornersMustBeClear);

public sealed record PublicAimOnlyShotProgramRules(
    int BendDirection,
    int BendAfterTiles,
    int BendEveryTiles,
    int BendCount);

public readonly record struct PublicShotProgramValue(
    int InitialAimOffset,
    int BendDirection,
    int BendAfterTiles,
    int BendEveryTiles,
    int BendCount);

public enum PublicActionRejectionResult
{
    Blocked = 0,
    Faulted = 1,
    Rejected = 2,
}

public enum PublicDistanceMetric
{
    Chebyshev = 0,
}

public enum PublicVisionShape
{
    Omnidirectional = 0,
    FacingQuadrant = 1,
}

public enum PublicLineOfSightModel
{
    CornerStrictSupercover = 0,
}

public sealed record PublicVisionRules(
    int Range,
    PublicDistanceMetric DistanceMetric,
    PublicVisionShape Shape,
    int OmnidirectionalProximityRange,
    PublicLineOfSightModel LineOfSight,
    int HearingRadius,
    int HearingBearingSectors,
    ImmutableArray<int> HearingDistanceBandUpperBounds,
    ImmutableArray<ObservedMatchEventType> LoudEventTypes);

public sealed record PublicCollisionRules(
    bool UnitsBlockWalls,
    bool UnitsBlockUnits,
    bool SameDestinationMovesBlockAll,
    bool SwapMovesBlocked,
    bool FollowingVacatedUnitAllowed,
    bool ProjectilesBlockMovement,
    bool MovingOntoProjectileCausesHit,
    bool WallsConsumeProjectiles,
    bool ProjectilesIgnoreOwner,
    bool ProjectilesStopOnFirstNonOwnerUnit,
    bool ProjectilesCollideWithProjectiles);

public enum PublicTickResolutionPhase
{
    FreezeObservations = 0,
    CollectJointDecisions = 1,
    ValidateActions = 2,
    Rotate = 3,
    Move = 4,
    AdvanceExistingProjectiles = 5,
    LaunchShotsAndApplyDamage = 6,
    UpdateCooldownsAndEnergy = 7,
    ApplyRuntimeFaults = 8,
    UpdateObjective = 9,
    ResolveMatchCompletion = 10,
    ApplyTickStartLifecycle = 11,
    QueueDestroyedLives = 12,
    QueueFabrications = 13,
    StartFormTransitions = 14,
    CompleteFormTransitions = 15,
}

public sealed record PublicTickResolutionRules(
    bool ObservationsUsePreTickState,
    bool DecisionsResolveAsJointStep,
    ImmutableArray<PublicTickResolutionPhase> Phases);
