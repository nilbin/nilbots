namespace BotArena.Engine;

/// <summary>
/// Stable common-host fault codes. Exception text and adapter-specific
/// diagnostics never become replay or scoring inputs.
/// </summary>
public static class GenericActorRuntimeFaultCodes
{
    public const string RuntimeCreateFailed = "runtime-create-failed";
    public const string RuntimeInstanceReused = "runtime-instance-reused";
    public const string LifeStartFailed = "life-start-failed";
    public const string TickExecutionFailed = "tick-execution-failed";
    public const string MalformedDecision = "malformed-decision";
    public const string InvalidDebugMessage = "invalid-debug-message";
    public const string UnknownAction = "unknown-action";
    public const string ActionSelectorMismatch = "action-selector-mismatch";
    public const string MalformedArgument = "malformed-argument";
    public const string DuplicateArgument = "duplicate-argument";
    public const string UnexpectedArgument = "unexpected-argument";
    public const string MissingArgument = "missing-argument";
    public const string ArgumentOutOfDomain = "argument-out-of-domain";
}
