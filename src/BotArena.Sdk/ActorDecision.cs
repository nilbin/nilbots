namespace BotArena.Sdk;

/// <summary>Stable public IDs for the extensible action catalog.</summary>
public static class ActorActionIds
{
    public const string Wait = "wait";
    public const string MoveForward = "move-forward";
    public const string TurnLeft = "turn-left";
    public const string TurnRight = "turn-right";
    public const string Shoot = "shoot";
    public const string StrafeLeft = "strafe-left";
    public const string StrafeRight = "strafe-right";
    public const string Fabricate = "fabricate";
    public const string Transform = "transform";
    public const string ShootDirection = "shoot-direction";
}

/// <summary>Stable numeric codes for entity actions outside the legacy enum.</summary>
public static class ActorActionCodes
{
    public const int Fabricate = 100;
    public const int Transform = 101;
    public const int ShootDirection = 102;
}

/// <summary>
/// Open typed parameter envelope. Null means that parameter kind was not
/// submitted; new parameter kinds append fields without changing old meanings.
/// </summary>
public sealed record ActorActionPayload
{
    public ShotProgram? ShotProgram { get; init; }
    public Direction? Direction { get; init; }
    public ObservedUnitTarget? UnitTarget { get; init; }
    public string? FormTargetId { get; init; }
    public ProjectileHeading? LaunchHeading { get; init; }
}

/// <summary>
/// One entity action. Prefer the helpers on <see cref="Actions"/>. The stable
/// string ID is semantic; the numeric code supports compact diagnostics and
/// must agree with it when both are supplied.
/// </summary>
public sealed record ActorDecision
{
    public string? ActionId { get; init; }
    public int? ActionCode { get; init; }
    public ActorActionPayload? Payload { get; init; }
    public string? DebugMessage { get; init; }
    public bool Faulted { get; init; }
    public string? FaultMessage { get; init; }

    public static ActorDecision Of(
        string actionId,
        int actionCode,
        ActorActionPayload? payload = null,
        string? debug = null) =>
        new()
        {
            ActionId = actionId,
            ActionCode = actionCode,
            Payload = payload,
            DebugMessage = debug,
        };

    /// <summary>
    /// Lets entity bots keep using the familiar Wait/Move/Turn/Shoot helpers.
    /// Legacy actions are completed with their stable entity action identity.
    /// </summary>
    public static implicit operator ActorDecision(BotAction action)
    {
        string actionId = action.Kind switch
        {
            BotActionKind.Wait => ActorActionIds.Wait,
            BotActionKind.MoveForward => ActorActionIds.MoveForward,
            BotActionKind.TurnLeft => ActorActionIds.TurnLeft,
            BotActionKind.TurnRight => ActorActionIds.TurnRight,
            BotActionKind.Shoot => ActorActionIds.Shoot,
            BotActionKind.StrafeLeft => ActorActionIds.StrafeLeft,
            BotActionKind.StrafeRight => ActorActionIds.StrafeRight,
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                $"Unknown legacy action kind {action.Kind}."),
        };
        return Of(
            actionId,
            (int)action.Kind,
            action.ShotProgram is { } program
                ? new ActorActionPayload { ShotProgram = program }
                : null);
    }

    internal static ActorDecision Fault(string message) =>
        new() { Faulted = true, FaultMessage = message };
}

/// <summary>Stable unit target used by fabrication and future unit actions.</summary>
public readonly record struct ObservedUnitTarget(int TeamId, int UnitId);
