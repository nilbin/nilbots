namespace BotArena.Engine;

/// <summary>Closed authoritative Arc Relay fact union.</summary>
public abstract record ArcRelayEvent
{
    private ArcRelayEvent()
    {
    }

    public sealed record CoreBorn(
        ArcRelayCoreId CoreId,
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
        int ChargePips) : ArcRelayEvent;

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
