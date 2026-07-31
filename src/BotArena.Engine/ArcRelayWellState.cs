namespace BotArena.Engine;

/// <summary>Public current state of one ordered Arc Well.</summary>
public sealed record ArcRelayWellState(
    string WellId,
    Position Position,
    int? NextScheduledBirthTick,
    ArcRelayCoreId? OutstandingCoreId,
    bool PendingCharge,
    int? RearmCompletesAtTick);
