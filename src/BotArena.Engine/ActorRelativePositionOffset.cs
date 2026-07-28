namespace BotArena.Engine;

/// <summary>
/// A source-facing-relative placement offset. Positive forward follows the
/// source facing; positive right is clockwise from it.
/// </summary>
public readonly record struct ActorRelativePositionOffset(
    int Forward,
    int Right);
