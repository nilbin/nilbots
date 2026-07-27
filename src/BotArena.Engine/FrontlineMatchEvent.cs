namespace BotArena.Engine;

/// <summary>
/// Flat authoritative event shape. Fields that do not apply to a given event
/// remain null; list position is the authoritative event order.
/// </summary>
public sealed record FrontlineMatchEvent
{
    public required int Tick { get; init; }
    public required FrontlineMatchEventType Type { get; init; }
    public int? TeamId { get; init; }
    public int? UnitId { get; init; }
    public FrontlineActorId? ActorId { get; init; }
    public FrontlineActorId? OtherActorId { get; init; }
    public long? ProjectileId { get; init; }
    public Position? From { get; init; }
    public Position? To { get; init; }
    public Direction? FromFacing { get; init; }
    public Direction? ToFacing { get; init; }
    public ProjectileHeading? ProjectileHeading { get; init; }
    public ShotProgram? ShotProgram { get; init; }
    public BotAction? Action { get; init; }
    public string? ActionId { get; init; }
    public int? ActionCode { get; init; }
    public ActorActionPayload? ActionPayload { get; init; }
    public ActionResult? ActionResult { get; init; }
    public string? FromFormId { get; init; }
    public string? ToFormId { get; init; }
    public int? FormTransitionStartedAtTick { get; init; }
    public int? FormTransitionCompletesAtTick { get; init; }
    public int? Amount { get; init; }
    public int? NewHealth { get; init; }
    public FrontlineLifecycleStatus? LifecycleStatus { get; init; }
    public ActorSpawnReason? SpawnReason { get; init; }
    public int? RespawnAtTick { get; init; }
    public int? UnlockAtTick { get; init; }
    public int? RebuildReadyAtTick { get; init; }
    public int? FabricationAtTick { get; init; }
    public int? FromPositionIndex { get; init; }
    public int? ToPositionIndex { get; init; }
    public int? ClaimingTeamId { get; init; }
    public int? CaptureProgress { get; init; }
    public int? ControlResumesAtTick { get; init; }
}
