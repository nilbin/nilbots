namespace BotArena.Engine;

/// <summary>
/// One stable slot and output tile atomically claimed by a Split bundle.
/// Health is intentionally absent because it is evaluated at completion.
/// </summary>
public sealed record SplitReplicationReservedDescendant(
    int TeamId,
    int UnitId,
    string FormId,
    int Generation,
    Position Position);
