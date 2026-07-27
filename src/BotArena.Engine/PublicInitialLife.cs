namespace BotArena.Engine;

/// <summary>
/// A runtime life occupying a stable unit slot at tick zero. Later lives are
/// dynamic match state and receive a new life ID without changing the slot.
/// </summary>
public sealed record PublicInitialLife(
    int TeamId,
    int UnitId,
    int LifeId,
    string FormId);
