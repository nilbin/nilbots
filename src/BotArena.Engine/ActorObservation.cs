using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Runtime-neutral identity of one independently executing actor. Historical
/// duel slot <c>s</c> normalizes to <c>(s, 0, 0)</c>; entity matches use the
/// same stable team/unit lineage plus a monotonically increasing life.
/// </summary>
public sealed record ActorIdentity : IComparable<ActorIdentity>
{
    public ActorIdentity(int teamId, int unitId, int lifeId)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (unitId < 0)
            throw new ArgumentOutOfRangeException(nameof(unitId));
        if (lifeId < 0)
            throw new ArgumentOutOfRangeException(nameof(lifeId));
        TeamId = teamId;
        UnitId = unitId;
        LifeId = lifeId;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    public int LifeId { get; }

    public static ActorIdentity FromLegacySlot(int slot) => new(slot, 0, 0);

    public static ActorIdentity FromTeamUnitLife(
        int teamId,
        int unitId,
        int lifeId) =>
        new(teamId, unitId, lifeId);

    public static ActorIdentity FromFrontline(FrontlineActorId actorId) =>
        FromTeamUnitLife(
            actorId.TeamId,
            actorId.UnitId,
            actorId.LifeId);

    public FrontlineActorId ToFrontline() =>
        new(TeamId, UnitId, LifeId);

    public int CompareTo(ActorIdentity? other)
    {
        if (other is null)
            return 1;
        int team = TeamId.CompareTo(other.TeamId);
        if (team != 0)
            return team;

        int unit = UnitId.CompareTo(other.UnitId);
        return unit != 0
            ? unit
            : LifeId.CompareTo(other.LifeId);
    }

    public override string ToString() => $"{TeamId}:{UnitId}:{LifeId}";
}

/// <summary>How allied sensors are combined before runtimes are invoked.</summary>
public enum TeamPerceptionMode
{
    Individual = 0,
    ImmediateUnion = 1,
}

/// <summary>
/// Public lifecycle state of one stable allied unit slot. Enemy slots are not
/// mirrored here: doing so would leak hidden destruction and respawn timing.
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

/// <summary>The observing life's complete private/public state.</summary>
public sealed record ObservedSelf(
    ActorIdentity ActorId,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    ActionResult PreviousActionResult);

/// <summary>
/// An active allied life. Allies share their complete gameplay state, but not
/// runtime memory or same-tick decisions.
/// </summary>
public sealed record ObservedAlly(
    ActorIdentity ActorId,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    ActionResult PreviousActionResult);

/// <summary>
/// Audience-local reference to an enemy life. Team and stable unit are public,
/// while the opaque handle deliberately carries no authoritative life counter.
/// </summary>
public sealed record ObservedEnemyActorRef(
    int TeamId,
    int UnitId,
    string LifeHandle);

/// <summary>An enemy life seen by at least one allied sensor.</summary>
public sealed record ObservedEnemy(
    ObservedEnemyActorRef Actor,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    ImmutableArray<ActorIdentity> ObservedBy);

/// <summary>A visible map tile and the exact allied sensors contributing it.</summary>
public sealed record ObservedMapTile(
    Position Position,
    bool IsWall,
    ImmutableArray<ActorIdentity> ObservedBy);

/// <summary>
/// A visible projectile. <see cref="Heading"/> is only the currently
/// manifested heading; launch direction and the private committed future
/// program/path are intentionally absent. <see cref="TicksUntilAdvance"/>,
/// <see cref="TilesPerAdvance"/>, and <see cref="RemainingTiles"/> are
/// intentionally exact public dodge telemetry. Owner team is always public.
/// An allied owner uses <see cref="AlliedOwnerActorId"/>; a currently visible
/// enemy owner instead uses <see cref="VisibleEnemyOwner"/>, so an enemy's
/// authoritative life counter never enters an observation.
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
/// Stable public event vocabulary for entity observations. The first eight
/// values mirror the historical duel event codes; Frontline values are
/// appended and must never be reordered or reused.
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
}

/// <summary>
/// Public projection of an authoritative event. Private programs, unseen
/// secondary actors, hidden coordinates, and runtime messages are absent.
/// </summary>
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
    ImmutableArray<ActorIdentity> ObservedBy);

/// <summary>
/// One redacted sound report. Bearing and distance are relative to the named
/// allied observer; immediate team sharing forwards that report unchanged.
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
/// Static capability plus a per-tick policy mask. <c>Enabled</c> comes from
/// the exact public match contract. <c>Available</c> additionally reflects the
/// observing form and dynamic resources such as cooldown and energy.
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
    ImmutableArray<string>? AllowedFormTargets);

/// <summary>A stable unit target used by future parameterized actions.</summary>
public readonly record struct ObservedUnitTarget(int TeamId, int UnitId);

/// <summary>
/// Canonical, public-only pre-tick input for one life. Collections are
/// variable-cardinality and canonically ordered; array position is never
/// identity. Static rules, map, topology, and counts are joined through the
/// referenced match-contract fingerprint supplied at match start.
/// </summary>
public sealed record ActorObservation
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
    /// Null means projectile observations are unsupported; empty means the
    /// capability exists and the team currently sees none.
    /// </summary>
    public required ImmutableArray<ObservedActorProjectile>? VisibleProjectiles { get; init; }
    public required ImmutableArray<ObservedMatchEvent> VisibleEvents { get; init; }
    /// <summary>
    /// Null means hearing is unsupported; empty means it is supported and no
    /// allied sensor heard a report.
    /// </summary>
    public required ImmutableArray<ObservedActorSound>? HeardSounds { get; init; }
    public required ObservedFrontlineObjective? FrontlineObjective { get; init; }
    public required ImmutableArray<ObservedActionAvailability> Actions { get; init; }
}

/// <summary>All frozen actor observations for one joint decision tick.</summary>
public sealed record ActorObservationFrame(
    int Tick,
    ImmutableArray<ActorObservation> Actors)
{
    /// <summary>
    /// Omniscient joins are deliberately internal and separate from every
    /// runtime observation. Replay v2 records them beside the actor turn.
    /// </summary>
    internal ImmutableArray<ActorObservationReplayAliases> ReplayAliases
    {
        get;
        init;
    } = [];
}

internal sealed record ActorObservationReplayAliases(
    ActorIdentity ActorId,
    ImmutableArray<ActorObservationEnemyLifeAlias> EnemyLives,
    ImmutableArray<ActorObservationProjectileAlias> Projectiles,
    ImmutableArray<ActorObservationEventAlias> Events);

internal sealed record ActorObservationEnemyLifeAlias(
    string LifeHandle,
    ActorIdentity ActorId);

internal sealed record ActorObservationProjectileAlias(
    string ProjectileHandle,
    long ProjectileId);

internal sealed record ActorObservationEventAlias(
    string EventHandle,
    string EventId);
