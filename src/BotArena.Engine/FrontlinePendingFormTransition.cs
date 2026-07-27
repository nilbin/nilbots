namespace BotArena.Engine;

/// <summary>
/// Life-scoped, public form-transition state. The stable unit keeps its
/// deployment default; only this exact runtime life may complete the change.
/// </summary>
public sealed record FrontlinePendingFormTransition(
    string FromFormId,
    string ToFormId,
    int StartedAtTick,
    int CompletesAtTick);
