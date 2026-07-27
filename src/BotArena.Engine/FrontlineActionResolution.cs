namespace BotArena.Engine;

/// <summary>One actor's chosen and validated contribution to a joint step.</summary>
public sealed record FrontlineActionResolution(
    FrontlineActorId ActorId,
    string ChosenActionId,
    int ChosenActionCode,
    ActorActionPayload? ChosenPayload,
    string ValidatedActionId,
    int ValidatedActionCode,
    ActorActionPayload? ValidatedPayload,
    ActionResult Result)
{
    /// <summary>
    /// Compatibility view for historical actions. Entity actions intentionally
    /// return null rather than masquerading as a legacy enum value.
    /// </summary>
    public BotAction? ChosenAction =>
        Enum.IsDefined(typeof(BotAction), ChosenActionCode)
            ? (BotAction)ChosenActionCode
            : null;

    public BotAction? ValidatedAction =>
        Enum.IsDefined(typeof(BotAction), ValidatedActionCode)
            ? (BotAction)ValidatedActionCode
            : null;

    public ShotProgram? ChosenShotProgram => ChosenPayload?.ShotProgram;
    public ShotProgram? ValidatedShotProgram => ValidatedPayload?.ShotProgram;

    public FrontlineActionResolution(
        FrontlineActorId actorId,
        BotAction chosenAction,
        BotAction validatedAction,
        ActionResult result,
        ShotProgram? chosenShotProgram = null,
        ShotProgram? validatedShotProgram = null)
        : this(
            actorId,
            ActionId(chosenAction),
            (int)chosenAction,
            chosenShotProgram is null
                ? null
                : new ActorActionPayload { ShotProgram = chosenShotProgram },
            ActionId(validatedAction),
            (int)validatedAction,
            validatedShotProgram is null
                ? null
                : new ActorActionPayload { ShotProgram = validatedShotProgram },
            result)
    {
    }

    private static string ActionId(BotAction action) => action switch
    {
        BotAction.Wait => PublicActionIds.Wait,
        BotAction.MoveForward => PublicActionIds.MoveForward,
        BotAction.TurnLeft => PublicActionIds.TurnLeft,
        BotAction.TurnRight => PublicActionIds.TurnRight,
        BotAction.Shoot => PublicActionIds.Shoot,
        BotAction.StrafeLeft => PublicActionIds.StrafeLeft,
        BotAction.StrafeRight => PublicActionIds.StrafeRight,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
