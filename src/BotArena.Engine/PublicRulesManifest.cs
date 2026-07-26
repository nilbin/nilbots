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
    None,
    ZoneTicks,
    SharedPressure,
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
    PublicMovementLayer MovementLayer,
    int ObjectiveWeight,
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
    None,
    ShotProgram,
}

public sealed record PublicActionDefinition(
    string Id,
    int Code,
    PublicActionKind Kind,
    PublicActionParameterKind ParameterKind,
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
}

public sealed record PublicTickResolutionRules(
    bool ObservationsUsePreTickState,
    bool DecisionsResolveAsJointStep,
    ImmutableArray<PublicTickResolutionPhase> Phases);
