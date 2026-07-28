namespace BotArena.Engine;

/// <summary>Completion-time fresh descendant state before runtime creation.</summary>
public sealed record SplitReplicationSpawn(
    int TeamId,
    int UnitId,
    string FormId,
    int Generation,
    int Health,
    Position Position);
