namespace BotArena.Engine;

/// <summary>One complete, serializer-neutral programmed-shot value.</summary>
public readonly record struct ActorShotProgramValue(
    int InitialAimOffset,
    int BendDirection,
    int BendAfterTiles,
    int BendEveryTiles,
    int BendCount);
