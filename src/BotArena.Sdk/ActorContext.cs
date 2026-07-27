using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Public lifecycle state of one stable allied unit slot. Enemy slots are not
/// mirrored here because doing so would leak hidden life timing.
/// </summary>
public sealed record ObservedUnitSlot(
    int TeamId,
    int UnitId,
    string FormId,
    FrontlineLifecycleStatus LifecycleStatus,
    ActorIdentity? ActiveActorId,
    int? RespawnAtTick,
    int? UnlockAtTick = null,
    int? RebuildReadyAtTick = null,
    int? FabricationAtTick = null);

/// <summary>The observing body's complete private and public state.</summary>
public sealed record ObservedSelf(
    ActorIdentity ActorId,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    ActionResult PreviousActionResult)
{
    public ObservedFormTransition? PendingFormTransition { get; init; }
}

/// <summary>
/// An active ally. Teammates share gameplay state, but never private runtime
/// memory or same-tick decisions.
/// </summary>
public sealed record ObservedAlly(
    ActorIdentity ActorId,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    ActionResult PreviousActionResult)
{
    public ObservedFormTransition? PendingFormTransition { get; init; }
}

/// <summary>
/// Audience-local reference to an enemy life. Team and stable unit are public;
/// the opaque handle deliberately hides its authoritative life counter.
/// </summary>
public sealed record ObservedEnemyActorRef(
    int TeamId,
    int UnitId,
    string LifeHandle);

/// <summary>An enemy body seen by at least one allied sensor.</summary>
public sealed record ObservedEnemy(
    ObservedEnemyActorRef Actor,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    ImmutableArray<ActorIdentity> ObservedBy)
{
    public ObservedFormTransition? PendingFormTransition { get; init; }
}

/// <summary>Public telegraph of a life-scoped form transition.</summary>
public sealed record ObservedFormTransition(
    string FromFormId,
    string ToFormId,
    int StartedAtTick,
    int CompletesAtTick);

/// <summary>A visible map tile and the exact allied sensors contributing it.</summary>
public sealed record ObservedMapTile(
    Position Position,
    bool IsWall,
    ImmutableArray<ActorIdentity> ObservedBy);

/// <summary>
/// A visible projectile. Current heading and dodge timing are exact, while a
/// private future curve remains absent.
/// </summary>
public sealed record ObservedActorProjectile(
    string ProjectileHandle,
    int OwnerTeamId,
    ActorIdentity? AlliedOwnerActorId,
    ObservedEnemyActorRef? VisibleEnemyOwner,
    Position Position,
    ProjectileHeading Heading,
    int TilesPerAdvance,
    int TicksUntilAdvance,
    int RemainingTiles,
    ImmutableArray<ActorIdentity> ObservedBy);

/// <summary>
/// Stable event vocabulary. Values 0-7 retain the historical event codes;
/// entity-match additions append and are never reordered or reused.
/// </summary>
public enum ObservedMatchEventType
{
    Turn = 0,
    Move = 1,
    MoveBlocked = 2,
    Shot = 3,
    Damage = 4,
    Destroyed = 5,
    Fault = 6,
    Disqualified = 7,
    Respawned = 8,
    FrontlineProgressChanged = 9,
    FrontlinePositionAdvanced = 10,
    BaseBreached = 11,
    FabricationUnlocked = 12,
    FabricationQueued = 13,
    Fabricated = 14,
    RebuildReady = 15,
    FormTransitionStarted = 16,
    FormChanged = 17,
    FormTransitionCancelled = 18,
}

/// <summary>Public projection of an authoritative match event.</summary>
public sealed record ObservedMatchEvent(
    string EventHandle,
    int SourceTick,
    ObservedMatchEventType Type,
    int? TeamId,
    ActorIdentity? AlliedActorId,
    ObservedEnemyActorRef? EnemyActor,
    string? ProjectileHandle,
    Position? Position,
    Direction? Facing,
    int? Amount,
    int? NewHealth,
    ImmutableArray<ActorIdentity> ObservedBy)
{
    public ProjectileHeading? ProjectileHeading { get; init; }
    public string? FromFormId { get; init; }
    public string? ToFormId { get; init; }
    public int? FormTransitionStartedAtTick { get; init; }
    public int? FormTransitionCompletesAtTick { get; init; }
    public string? ActionId { get; init; }
    public int? ActionCode { get; init; }
    public string? FormTargetId { get; init; }
    public ActionResult? ActionResult { get; init; }
}

/// <summary>
/// One redacted sound report. Bearing and distance are relative to the named
/// allied observer.
/// </summary>
public sealed record ObservedActorSound(
    string EventHandle,
    int SourceTick,
    ActorIdentity ObserverActorId,
    ObservedMatchEventType Type,
    int Bearing,
    int Distance);

/// <summary>Public pre-tick state of the moving Frontline objective.</summary>
public sealed record ObservedFrontlineObjective(
    int ActivePositionIndex,
    int? ClaimingTeamId,
    int CaptureProgress,
    int DecayTicksElapsed,
    int ControlResumesAtTick);

/// <summary>
/// Static capability plus the current policy mask. Enabled comes from the
/// immutable contract; Available also reflects this body's form and resources.
/// </summary>
public sealed record ObservedActionAvailability(
    string ActionId,
    int ActionCode,
    ImmutableArray<PublicActionParameterKind> ParameterKinds,
    bool Enabled,
    bool Available,
    bool? ShotProgramAvailable,
    ImmutableArray<Direction>? AllowedDirections,
    ImmutableArray<ObservedUnitTarget>? AllowedUnitTargets,
    ImmutableArray<string>? AllowedFormTargets)
{
    public ImmutableArray<ProjectileHeading>? AllowedProjectileHeadings
    {
        get;
        init;
    }
}

/// <summary>
/// Canonical public-only pre-tick input for one body. Collections have
/// variable cardinality and canonical ordering; array position is never an
/// identity. Null capabilities differ intentionally from present-but-empty
/// collections.
/// </summary>
public sealed record ActorContext
{
    public required int SchemaVersion { get; init; }
    public required int Tick { get; init; }
    public required string MatchContractFingerprint { get; init; }
    public required TeamPerceptionMode TeamPerception { get; init; }
    public required ObservedSelf Self { get; init; }
    public required ImmutableArray<ObservedUnitSlot> TeamUnits { get; init; }
    public required ImmutableArray<ObservedAlly> Allies { get; init; }
    public required ImmutableArray<ObservedEnemy> Enemies { get; init; }
    public required ImmutableArray<ObservedMapTile> VisibleTiles { get; init; }

    /// <summary>
    /// Null means unsupported; empty means supported with no visible projectile.
    /// </summary>
    public required ImmutableArray<ObservedActorProjectile>? VisibleProjectiles
    {
        get;
        init;
    }

    public required ImmutableArray<ObservedMatchEvent> VisibleEvents { get; init; }

    /// <summary>
    /// Null means unsupported; empty means supported with no heard report.
    /// </summary>
    public required ImmutableArray<ObservedActorSound>? HeardSounds { get; init; }

    public required ObservedFrontlineObjective? FrontlineObjective { get; init; }
    public required ImmutableArray<ObservedActionAvailability> Actions { get; init; }

    /// <summary>Deterministic randomness scoped to this exact life.</summary>
    public IBotRandom Random { get; init; } = null!;

    /// <summary>Bounded diagnostic output; never affects match outcome.</summary>
    public IBotDebug Debug { get; init; } = null!;

    /// <summary>Find the current mask for a stable action ID.</summary>
    public ObservedActionAvailability? Action(string actionId) =>
        Actions.FirstOrDefault(action =>
            string.Equals(action.ActionId, actionId, StringComparison.Ordinal));
}
