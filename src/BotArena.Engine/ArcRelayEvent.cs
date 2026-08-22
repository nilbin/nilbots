namespace BotArena.Engine;

/// <summary>Closed authoritative Arc Relay fact union.</summary>
public abstract record ArcRelayEvent
{
    private ArcRelayEvent()
    {
    }

    public sealed record CoreBorn(
        ArcRelayCoreId CoreId,
        Position Position) : ArcRelayEvent
    {
        public int ChargeValue { get; init; } = 1;
    }

    public sealed record CoreRipened(
        ArcRelayCoreId CoreId,
        Position Position,
        int Value) : ArcRelayEvent;

    public sealed record LeveledUp(
        ActorIdentity ActorId,
        int Level,
        Position Position) : ArcRelayEvent;

    public sealed record ZoneHealed(
        ActorIdentity ActorId,
        int Amount,
        int NewHealth,
        Position Position) : ArcRelayEvent;

    public sealed record CorePickedUp(
        ArcRelayCoreId CoreId,
        ActorIdentity CarrierActorId,
        Position Position,
        int NextRelocationTick) : ArcRelayEvent;

    public sealed record CoreRelocated(
        ArcRelayCoreId CoreId,
        ActorIdentity? CarrierActorId,
        Position From,
        Position To,
        int NextRelocationTick,
        CoreRelocationKind Kind) : ArcRelayEvent;

    public sealed record CoreHandedOff(
        ArcRelayCoreId CoreId,
        ActorIdentity SourceActorId,
        ActorIdentity TargetActorId,
        Position Position,
        int NextRelocationTick) : ArcRelayEvent;

    public sealed record CoreDropped(
        ArcRelayCoreId CoreId,
        ActorIdentity SourceActorId,
        Position Position,
        int NextRelocationTick,
        CoreDropKind Kind) : ArcRelayEvent;

    public sealed record CoreBanked(
        ArcRelayCoreId CoreId,
        ActorIdentity CarrierActorId,
        int TeamId,
        Position Position,
        int ChargePips) : ArcRelayEvent
    {
        /// <summary>
        /// Charge this bank actually awarded. One is omitted from historical
        /// replay encodings; depth-score banks declare 1, 2, or 4.
        /// </summary>
        public int ChargeAwarded { get; init; } = 1;
    }

    public sealed record WellChanged(
        string WellId,
        bool PendingCharge,
        int? RearmCompletesAtTick,
        ArcRelayCoreId? OutstandingCoreId) : ArcRelayEvent;

    public sealed record Pulse(
        int TeamId,
        int PulseOrdinal,
        int OpposingReactorIntegrity) : ArcRelayEvent;

    public sealed record SignatureChanged(
        string OperationId,
        string SignatureId,
        ActorIdentity OwnerActorId,
        ArcRelaySignatureState.SignaturePhase? Phase,
        string Reason) : ArcRelayEvent;

    public sealed record BodyRelocated(
        string OperationId,
        string SignatureId,
        ActorIdentity OwnerActorId,
        ActorIdentity TargetActorId,
        Position From,
        Position To) : ArcRelayEvent;

    public sealed record SignatureDamage(
        string OperationId,
        string SignatureId,
        ActorIdentity OwnerActorId,
        ActorIdentity TargetActorId,
        int Amount,
        int NewHealth,
        Position Position) : ArcRelayEvent;

    public sealed record SignatureRepair(
        string OperationId,
        string SignatureId,
        ActorIdentity OwnerActorId,
        ActorIdentity TargetActorId,
        int Amount,
        int NewHealth,
        Position Position) : ArcRelayEvent;

    /// <summary>The Workshop became available: initial unlock or the end of
    /// a post-completion dormancy.</summary>
    public sealed record WorkshopReady(
        string WorkshopId,
        Position TrainingTile) : ArcRelayEvent;

    /// <summary>A training channel opened: its first qualifying tick.</summary>
    public sealed record WorkshopTrainingStarted(
        string WorkshopId,
        int TeamId,
        Position TrainingTile) : ArcRelayEvent;

    /// <summary>An active channel reset to zero, with the public reason.</summary>
    public sealed record WorkshopTrainingInterrupted(
        string WorkshopId,
        int TeamId,
        WorkshopInterruptionKind Reason,
        int ProgressLost,
        Position TrainingTile) : ArcRelayEvent;

    /// <summary>A session completed: the XP landed and dormancy began.</summary>
    public sealed record WorkshopTrainingCompleted(
        string WorkshopId,
        int TeamId,
        ActorIdentity TraineeActorId,
        int XpAward,
        int ReadyAgainTick,
        Position TrainingTile) : ArcRelayEvent;

    public enum WorkshopInterruptionKind
    {
        LeftTile = 0,
        Moved = 1,
        Damaged = 2,
        ActedInCombat = 3,
        AcquiredCore = 4,
        Died = 5,
        Contested = 6,
    }

    public enum CoreRelocationKind
    {
        CarriedMovement = 0,
        ForcedDisplacement = 1,
        ArcTossLanding = 2,
    }

    public enum CoreDropKind
    {
        Voluntary = 0,
        Destruction = 1,
        SignatureDeparture = 2,
        ArcTossLanding = 3,
    }
}
