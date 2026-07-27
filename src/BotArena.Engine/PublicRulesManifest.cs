using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable, runtime-neutral description of the rules a bot may reason about.
/// It is not the engine's mutable configuration surface and is not yet delivered
/// through runtime protocol 0.1.
/// </summary>
public sealed record PublicRulesManifest
{
    public required int SchemaVersion { get; init; }
    public required string RulesetId { get; init; }
    public required string RulesFingerprint { get; init; }
    public required PublicMatchLimits Limits { get; init; }
    public required PublicObjectiveRules Objective { get; init; }
    /// <summary>
    /// Experimental Frontline definition data. This subtree exposes rules that
    /// are not yet represented by runnable action-schema entries; in particular,
    /// its lifecycle timings do not imply that Fabricate or Anchor actions exist.
    /// </summary>
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
    /// <summary>
    /// Maximum applied runtime faults. Zero means this session rejects runtime
    /// faults at its host boundary instead of resolving them as gameplay.
    /// </summary>
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
    None,
    ZoneTicks,
    SharedPressure,
    Frontline,
}

public enum PublicScoreMetric
{
    Objective,
    Health,
    DamageDealt,
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

/// <summary>
/// Typed definition-only contract for the experimental Frontline mode. Unit
/// capabilities live in the shared <see cref="PublicRulesManifest.Forms"/>
/// catalog; this subtree owns objective and lifecycle rules.
/// </summary>
public sealed record PublicFrontlineDefinition(
    int TeamCount,
    int ParticipantsPerTeam,
    int FrontlinePositionCount,
    int InitialUnitsPerTeam,
    int MaxUnitsPerTeam,
    TeamPerceptionMode TeamPerception,
    PublicFrontlineCaptureDefinition Capture,
    PublicFrontlineLifecycleDefinition Lifecycle,
    PublicFrontlineAnchorDefinition Anchor,
    PublicFrontlineAlliedCombatDefinition AlliedCombat);

public sealed record PublicFrontlineCaptureDefinition(
    int Threshold,
    int GainPerSoleTeamTick,
    int DecayAmount,
    int DecayIntervalTicks,
    int RedeployPauseTicks,
    int PushesToBreach);

/// <summary>
/// Lifecycle configuration only. Fabrication unlocks and rebuild timing are
/// public rules inputs, not claims that a Fabricate action is currently runnable.
/// </summary>
public sealed record PublicFrontlineLifecycleDefinition(
    int PrimeRespawnTicks,
    int ChildRebuildTicks,
    ImmutableArray<int> FabricationUnlockTicks);

public sealed record PublicFrontlineAnchorDefinition(
    int WindupTicks,
    int HealthGain,
    bool IrreversibleForLife);

/// <summary>
/// Frontline-specific override for allied non-owner projectile contact. Enemy
/// contact still consumes and damages; the exact firing life is always
/// ignored. With friendly fire off, allies either consume without damage or
/// are passed through according to <see cref="AlliedProjectilesBlock"/>.
/// </summary>
public sealed record PublicFrontlineAlliedCombatDefinition(
    bool FriendlyFireEnabled,
    bool AlliedProjectilesBlock);

public sealed record PublicEnergyRules(
    bool Enabled,
    int MaxEnergy,
    int ShotEnergyCost,
    int RegenerationIntervalTicks,
    int RegenerationAmount);

public enum PublicMovementLayer
{
    Ground,
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
    Wait,
    Movement,
    Rotation,
    Attack,
}

public enum PublicActionParameterKind
{
    ShotProgram = 0,
    Direction = 1,
    UnitTarget = 2,
    FormTarget = 3,
}

public sealed record PublicActionDefinition(
    string Id,
    int Code,
    PublicActionKind Kind,
    ImmutableArray<PublicActionParameterKind> ParameterKinds,
    bool Enabled);

public enum PublicProjectileMode
{
    InstantRay,
    Discrete,
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
    Blocked,
    Faulted,
    Rejected,
}

public enum PublicDistanceMetric
{
    Chebyshev,
}

public enum PublicVisionShape
{
    Omnidirectional,
    FacingQuadrant,
}

public enum PublicLineOfSightModel
{
    CornerStrictSupercover,
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
    ImmutableArray<GameEventType> LoudEventTypes);

/// <summary>
/// Generic collision contract. For Frontline, the more specific
/// <see cref="PublicFrontlineDefinition.AlliedCombat"/> contract overrides
/// <see cref="ProjectilesStopOnFirstNonOwnerUnit"/> for allied lives.
/// </summary>
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
    FreezeObservations,
    CollectJointDecisions,
    ValidateActions,
    Rotate,
    Move,
    AdvanceExistingProjectiles,
    LaunchShotsAndApplyDamage,
    UpdateCooldownsAndEnergy,
    ApplyRuntimeFaults,
    UpdateObjective,
    ResolveMatchCompletion,
    ApplyTickStartLifecycle,
    QueueDestroyedLives,
}

public sealed record PublicTickResolutionRules(
    bool ObservationsUsePreTickState,
    bool DecisionsResolveAsJointStep,
    ImmutableArray<PublicTickResolutionPhase> Phases);
