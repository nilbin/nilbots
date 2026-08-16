namespace BotArena.Engine;

/// <summary>
/// Public current state of one ordered Arc Well. The next-birth field is the
/// one effective countdown: initial opening or a source-local recharge. It is
/// null while this Well still has an active Core.
/// </summary>
public sealed record ArcRelayWellState(
    string WellId,
    Position Position,
    int? NextScheduledBirthTick,
    ArcRelayCoreId? OutstandingCoreId,
    bool PendingCharge,
    int? RearmCompletesAtTick);
