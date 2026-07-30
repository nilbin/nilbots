namespace BotArena.Engine;

/// <summary>Stable public IDs for the historical action catalog.</summary>
public static class PublicActionIds
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
    public const string Invest = "invest";
}

/// <summary>
/// Stable public codes for entity actions that are deliberately outside the
/// historical <see cref="BotAction"/> enum.
/// </summary>
public static class PublicActionCodes
{
    public const int Fabricate = 100;
    public const int Transform = 101;
    public const int ShootDirection = 102;

    /// <summary>
    /// The mode store's verb. 103 (split), 104 (mobilize) and 105
    /// (shoot-straight) are taken by the Frontline Labs catalogs, so the next
    /// free code is 106.
    /// </summary>
    public const int Invest = 106;
}

/// <summary>
/// Open action-parameter envelope. Null means a parameter kind was not
/// submitted. New parameter kinds append fields; existing meanings never
/// change.
/// </summary>
public sealed record ActorActionPayload
{
    public ShotProgram? ShotProgram { get; init; }
    public Direction? Direction { get; init; }
    public ObservedUnitTarget? UnitTarget { get; init; }
    public string? FormTargetId { get; init; }
    public ProjectileHeading? LaunchHeading { get; init; }

    /// <summary>
    /// The declared upgrade track a mode-investment names. Null on every
    /// other action and on every contract that declares no store.
    /// </summary>
    public string? UpgradeTrackId { get; init; }
}

/// <summary>
/// Raw runtime reply. IDs are semantic and stable; optional numeric codes make
/// wire/runtime diagnostics exact without turning enum declaration order into
/// the public action contract.
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

    public static ActorDecision Wait(string? debug = null) =>
        Of(PublicActionIds.Wait, (int)BotAction.Wait, debug: debug);

    public static ActorDecision MoveForward(string? debug = null) =>
        Of(
            PublicActionIds.MoveForward,
            (int)BotAction.MoveForward,
            debug: debug);

    public static ActorDecision TurnLeft(string? debug = null) =>
        Of(PublicActionIds.TurnLeft, (int)BotAction.TurnLeft, debug: debug);

    public static ActorDecision TurnRight(string? debug = null) =>
        Of(PublicActionIds.TurnRight, (int)BotAction.TurnRight, debug: debug);

    public static ActorDecision Shoot(
        ShotProgram? program = null,
        string? debug = null) =>
        Of(
            PublicActionIds.Shoot,
            (int)BotAction.Shoot,
            program is null
                ? null
                : new ActorActionPayload { ShotProgram = program },
            debug);

    public static ActorDecision Fabricate(
        ObservedUnitTarget target,
        string? debug = null) =>
        Of(
            PublicActionIds.Fabricate,
            PublicActionCodes.Fabricate,
            new ActorActionPayload { UnitTarget = target },
            debug);

    public static ActorDecision Transform(
        string formTargetId,
        string? debug = null) =>
        Of(
            PublicActionIds.Transform,
            PublicActionCodes.Transform,
            new ActorActionPayload { FormTargetId = formTargetId },
            debug);

    public static ActorDecision ShootDirection(
        ProjectileHeading launchHeading,
        string? debug = null) =>
        Of(
            PublicActionIds.ShootDirection,
            PublicActionCodes.ShootDirection,
            new ActorActionPayload { LaunchHeading = launchHeading },
            debug);

    public static ActorDecision Fault(string message) =>
        new()
        {
            Faulted = true,
            FaultMessage = message,
        };
}

public enum ActorSpawnReason
{
    Initial = 0,
    Respawn = 1,
    Rebuild = 2,
    Fabrication = 3,
}

/// <summary>
/// Immutable initialization delivered once to a fresh runtime life. The full
/// public contract is delivered here, never rediscovered from observations.
/// </summary>
public sealed record ActorMatchStart
{
    public required int SchemaVersion { get; init; }
    public required int RuntimeContractVersion { get; init; }
    public required ActorIdentity ActorId { get; init; }
    public required int ParticipantId { get; init; }
    public required ulong ActorRandomSeed { get; init; }
    public required ActorSpawnReason SpawnReason { get; init; }
    public required PublicMatchContractManifest Contract { get; init; }
}

/// <summary>
/// One isolated runtime instance for exactly one actor life. Destruction
/// disposes it; a later life receives a new instance from the artifact factory.
/// </summary>
public interface IActorRuntime : IDisposable
{
    void StartLife(ActorMatchStart start);

    ActorDecision ExecuteTick(ActorObservation observation);

    void IDisposable.Dispose() { }
}

/// <summary>
/// Match-scoped artifact owner. A future WASM implementation can share one
/// compiled module here while returning isolated stores/instances per life.
/// </summary>
public interface IActorRuntimeFactory : IDisposable
{
    IActorRuntime CreateRuntime();

    void IDisposable.Dispose() { }
}

/// <summary>One submitted artifact/controller and its replay provenance.</summary>
public sealed record ActorParticipantConfiguration
{
    public required int ParticipantId { get; init; }
    public required int TeamId { get; init; }
    public required string Name { get; init; }
    public required IActorRuntimeFactory RuntimeFactory { get; init; }
    public string RuntimeKind { get; init; } = "in-process-actor";
    public string ArtifactHash { get; init; } = "";
    public string Accent { get; init; } = "#38bdf8";
    public string? LookId { get; init; }
    public string? ProjectileLookId { get; init; }
}
