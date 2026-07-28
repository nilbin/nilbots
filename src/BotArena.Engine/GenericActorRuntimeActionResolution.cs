using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Previous generic action in submitted, admitted, and authoritative forms.
/// </summary>
public sealed record GenericActorRuntimeActionResolution(
    GenericActorRuntimeActionResolution.ResolvedAction? SubmittedAction,
    GenericActorRuntimeActionResolution.ResolvedAction AcceptedAction,
    GenericActorRuntimeActionResolution.ResolvedAction ValidatedAction,
    GenericActorRuntimeActionResolution.ActionOutcome Outcome,
    GenericActorRuntimeFault? RuntimeFault)
{
    public sealed record ResolvedAction(
        string ActionId,
        int ActionCode,
        ImmutableArray<GenericActorRuntimeActionArgument> Arguments);

    public enum ActionOutcome
    {
        Success = 0,
        Blocked = 1,
        Rejected = 2,
        Faulted = 3,
    }
}
