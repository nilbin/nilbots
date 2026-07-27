using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Internal, additive entity-replay contract. It is deliberately not wired to
/// the shipped replay-v1 readers or writers.
/// </summary>
internal sealed record ReplayV2(
    ReplayV2Header Header,
    ImmutableArray<ReplayV2Tick> Ticks,
    ReplayV2Result Result);

internal sealed record ReplayV2Document(
    ReplayV2Header Header,
    ImmutableArray<ReplayV2Tick> Ticks,
    ReplayV2Result? Result,
    string? ReplayHash,
    bool Partial);

internal readonly record struct ReplayV2ActorId(
    int TeamId,
    int UnitId,
    int LifeId) : IComparable<ReplayV2ActorId>
{
    public int CompareTo(ReplayV2ActorId other)
    {
        int team = TeamId.CompareTo(other.TeamId);
        if (team != 0)
            return team;

        int unit = UnitId.CompareTo(other.UnitId);
        return unit != 0
            ? unit
            : LifeId.CompareTo(other.LifeId);
    }
}

internal sealed record ReplayV2ParticipantController(
    int ParticipantId,
    int TeamId,
    string Name,
    string RuntimeKind,
    string ArtifactHash,
    string Accent,
    string? LookId,
    string? ProjectileLookId);

internal sealed record ReplayV2WallGroup(
    string Family,
    ImmutableArray<Position> Tiles);

internal sealed record ReplayV2MapPresentation(
    string BoundaryWall,
    string InteriorWall,
    ImmutableArray<ReplayV2WallGroup> WallGroups);

internal sealed record ReplayV2Presentation(
    string? ThemeId,
    ReplayV2MapPresentation? Map);

internal sealed record ReplayV2ActorRuntimeContract(
    string Family,
    int Version,
    int MatchStartSchemaVersion,
    int ObservationSchemaVersion,
    int DecisionSchemaVersion);

internal sealed record ReplayV2Header(
    int ReplayVersion,
    string EngineVersion,
    string GameRulesVersion,
    ReplayV2ActorRuntimeContract ActorRuntime,
    string Seed,
    PublicMatchContractManifest Contract,
    ReplayV2Presentation? Presentation,
    ImmutableArray<ReplayV2ParticipantController> Participants);

internal sealed record ReplayV2Tick(
    int Tick,
    ReplayV2TickStart TickStart,
    ImmutableArray<ReplayV2ActorTurn> Actors,
    ReplayV2AuthoritativeResolution Resolution,
    ReplayV2WorldState PostState);

internal sealed record ReplayV2TickStart(
    ReplayV2WorldState State,
    ImmutableArray<ReplayV2ActorId> ActiveActors,
    ImmutableArray<ReplayV2Event> LifecycleEvents);

internal sealed record ReplayV2ActorTurn(
    ReplayV2ActorId ActorId,
    ReplayV2LifeStart? LifeStart,
    ReplayV2ActorObservation Observation,
    ReplayV2ObservationAliases Aliases,
    ReplayV2ActorDecision RuntimeReply,
    ReplayV2ActorDecision AcceptedDecision,
    ReplayV2ActionResolution ActionResolution);

internal sealed record ReplayV2LifeStart(
    int SchemaVersion,
    int RuntimeContractVersion,
    ReplayV2ActorId ActorId,
    int ParticipantId,
    string ActorRandomSeed,
    ActorSpawnReason SpawnReason,
    string MatchContractFingerprint);

internal sealed record ReplayV2ObservationAliases(
    ImmutableArray<ReplayV2EnemyLifeAlias> EnemyLives,
    ImmutableArray<ReplayV2ProjectileAlias> Projectiles,
    ImmutableArray<ReplayV2EventAlias> Events);

internal sealed record ReplayV2EnemyLifeAlias(
    string LifeHandle,
    ReplayV2ActorId ActorId);

internal sealed record ReplayV2ProjectileAlias(
    string ProjectileHandle,
    string ProjectileId);

internal sealed record ReplayV2EventAlias(
    string EventHandle,
    string EventId);

internal sealed record ReplayV2ActorObservation(
    int SchemaVersion,
    int Tick,
    string MatchContractFingerprint,
    TeamPerceptionMode TeamPerception,
    ReplayV2ObservedSelf Self,
    ImmutableArray<ReplayV2ObservedUnitSlot> TeamUnits,
    ImmutableArray<ReplayV2ObservedAlly> Allies,
    ImmutableArray<ReplayV2ObservedEnemy> Enemies,
    ImmutableArray<ReplayV2ObservedMapTile> VisibleTiles,
    ImmutableArray<ReplayV2ObservedProjectile>? VisibleProjectiles,
    ImmutableArray<ReplayV2ObservedEvent> VisibleEvents,
    ImmutableArray<ReplayV2ObservedSound>? HeardSounds,
    ReplayV2ObservedFrontlineObjective? FrontlineObjective,
    ImmutableArray<ReplayV2ObservedActionAvailability> Actions);

internal sealed record ReplayV2ObservedUnitSlot(
    int TeamId,
    int UnitId,
    string FormId,
    FrontlineLifecycleStatus LifecycleStatus,
    ReplayV2ActorId? ActiveActorId,
    int? RespawnAtTick);

internal sealed record ReplayV2ObservedSelf(
    ReplayV2ActorId ActorId,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    ActionResult PreviousActionResult);

internal sealed record ReplayV2ObservedAlly(
    ReplayV2ActorId ActorId,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    ActionResult PreviousActionResult);

internal sealed record ReplayV2ObservedEnemyActorRef(
    int TeamId,
    int UnitId,
    string LifeHandle);

internal sealed record ReplayV2ObservedEnemy(
    ReplayV2ObservedEnemyActorRef Actor,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    ImmutableArray<ReplayV2ActorId> ObservedBy);

internal sealed record ReplayV2ObservedMapTile(
    Position Position,
    bool IsWall,
    ImmutableArray<ReplayV2ActorId> ObservedBy);

internal sealed record ReplayV2ObservedProjectile(
    string ProjectileHandle,
    int OwnerTeamId,
    ReplayV2ActorId? AlliedOwnerActorId,
    ReplayV2ObservedEnemyActorRef? VisibleEnemyOwner,
    Position Position,
    ProjectileHeading Heading,
    int TilesPerAdvance,
    int TicksUntilAdvance,
    int RemainingTiles,
    ImmutableArray<ReplayV2ActorId> ObservedBy);

internal sealed record ReplayV2ObservedEvent(
    string EventHandle,
    int SourceTick,
    ObservedMatchEventType Type,
    int? TeamId,
    ReplayV2ActorId? AlliedActorId,
    ReplayV2ObservedEnemyActorRef? EnemyActor,
    string? ProjectileHandle,
    Position? Position,
    Direction? Facing,
    int? Amount,
    int? NewHealth,
    ImmutableArray<ReplayV2ActorId> ObservedBy);

internal sealed record ReplayV2ObservedSound(
    string EventHandle,
    int SourceTick,
    ReplayV2ActorId ObserverActorId,
    ObservedMatchEventType Type,
    int Bearing,
    int Distance);

internal sealed record ReplayV2ObservedFrontlineObjective(
    int ActivePositionIndex,
    int? ClaimingTeamId,
    int CaptureProgress,
    int DecayTicksElapsed,
    int ControlResumesAtTick);

internal readonly record struct ReplayV2ObservedUnitTarget(
    int TeamId,
    int UnitId);

internal sealed record ReplayV2ObservedActionAvailability(
    string ActionId,
    int ActionCode,
    ImmutableArray<PublicActionParameterKind> ParameterKinds,
    bool Enabled,
    bool Available,
    bool? ShotProgramAvailable,
    ImmutableArray<Direction>? AllowedDirections,
    ImmutableArray<ReplayV2ObservedUnitTarget>? AllowedUnitTargets,
    ImmutableArray<string>? AllowedFormTargets);

internal sealed record ReplayV2ActionPayload(
    ShotProgram? ShotProgram,
    Direction? Direction,
    ReplayV2ObservedUnitTarget? UnitTarget,
    string? FormTargetId);

internal sealed record ReplayV2ActorDecision(
    string? ActionId,
    int? ActionCode,
    ReplayV2ActionPayload? Payload,
    string? DebugMessage,
    bool Faulted,
    string? FaultMessage);

internal sealed record ReplayV2ActionResolution(
    ReplayV2ActorId ActorId,
    string ChosenActionId,
    int ChosenActionCode,
    ReplayV2ActionPayload? ChosenPayload,
    string ValidatedActionId,
    int ValidatedActionCode,
    ReplayV2ActionPayload? ValidatedPayload,
    ActionResult Result);

internal sealed record ReplayV2AuthoritativeResolution(
    ImmutableArray<ReplayV2Event> Events,
    ImmutableArray<ReplayV2ProjectileTraversal> ProjectileTraversals);

internal sealed record ReplayV2Event(
    string EventId,
    int Tick,
    FrontlineMatchEventType Type,
    int? TeamId,
    ReplayV2ActorId? SourceActorId,
    ReplayV2ActorId? TargetActorId,
    string? ProjectileId,
    Position? From,
    Position? To,
    Direction? FromFacing,
    Direction? ToFacing,
    ProjectileHeading? ProjectileHeading,
    string? ActionId,
    int? ActionCode,
    ReplayV2ActionPayload? ActionPayload,
    ActionResult? ActionResult,
    int? Amount,
    int? NewHealth,
    FrontlineLifecycleStatus? LifecycleStatus,
    int? RespawnAtTick,
    int? FromPositionIndex,
    int? ToPositionIndex,
    int? ClaimingTeamId,
    int? CaptureProgress,
    int? ControlResumesAtTick);

internal sealed record ReplayV2ProjectileTraversal(
    string ProjectileId,
    ReplayV2ActorId OwnerActorId,
    Direction LaunchDirection,
    Position From,
    ImmutableArray<Position> Path,
    ProjectileHeading? Heading,
    ShotProgram? ShotProgram,
    ImmutableArray<Position>? ProgrammedPath);

internal sealed record ReplayV2WorldState(
    ImmutableArray<ReplayV2TeamState> Teams,
    ImmutableArray<ReplayV2ProjectileState> Projectiles,
    ReplayV2ControlState Control);

internal sealed record ReplayV2TeamState(
    int TeamId,
    string DamageDealt,
    ImmutableArray<ReplayV2UnitState> Units);

internal sealed record ReplayV2UnitState(
    int TeamId,
    int UnitId,
    string FormId,
    FrontlineLifecycleStatus LifecycleStatus,
    int? RespawnAtTick,
    string DamageDealt,
    ReplayV2LifeState? ActiveLife);

internal sealed record ReplayV2LifeState(
    ReplayV2ActorId ActorId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    string DamageDealt,
    ActionResult PreviousActionResult,
    int SpawnedAtTick);

internal sealed record ReplayV2ProjectileState(
    string ProjectileId,
    ReplayV2ActorId OwnerActorId,
    Position Position,
    Direction LaunchDirection,
    ProjectileHeading? Heading,
    ShotProgram? ShotProgram,
    ImmutableArray<Position>? ProgrammedPath,
    int NextProgrammedPathIndex,
    int TilesTraveled,
    int Phase);

internal sealed record ReplayV2ControlState(
    int NextTick,
    int ActivePositionIndex,
    int? ClaimingTeamId,
    int CaptureProgress,
    int DecayTicksElapsed,
    int ControlResumesAtTick,
    int? WinnerTeamId);

internal sealed record ReplayV2Result(
    int? WinnerTeamId,
    FrontlineMatchEndReason Reason,
    int EndTick,
    string TerritorialScore,
    ReplayV2ControlState Control,
    ImmutableArray<ReplayV2TeamResult> Teams);

internal sealed record ReplayV2TeamResult(
    int TeamId,
    FrontlineTeamOutcome Outcome,
    int FinalHealth,
    string DamageDealt,
    FrontlineLifecycleStatus FinalLifecycleStatus);
