namespace BotArena.Engine;

/// <summary>
/// Closed bearing quantizers supported by actor observations. A sector count
/// alone is not enough to reproduce boundary behavior.
/// </summary>
public enum ActorHearingBearingModel
{
    Disabled = 0,
    EightOctantsStrictTwoToOneCardinalV1 = 1,
}
