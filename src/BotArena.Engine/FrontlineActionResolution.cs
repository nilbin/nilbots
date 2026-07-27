namespace BotArena.Engine;

/// <summary>One actor's chosen and validated contribution to a joint step.</summary>
public sealed record FrontlineActionResolution(
    FrontlineActorId ActorId,
    BotAction ChosenAction,
    BotAction ValidatedAction,
    ActionResult Result,
    ShotProgram? ChosenShotProgram = null,
    ShotProgram? ValidatedShotProgram = null);
