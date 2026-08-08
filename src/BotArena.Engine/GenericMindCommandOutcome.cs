namespace BotArena.Engine;

/// <summary>
/// Host admission verdict for one command in a mind's decision map. It reuses
/// the existing grammar verbatim (GAME-MODE-ARCHITECTURE.md §7): unknown or
/// malformed actions are Faulted — recorded on the TURN, because a fault is
/// participant-scoped — and everything the host declines to route is Rejected,
/// which never touches the fault counter.
/// </summary>
public enum GenericMindCommandOutcome
{
    /// <summary>Routed to the named own live body.</summary>
    Accepted = 0,

    /// <summary>
    /// The key named a body the participant does not own, or one that is not
    /// live this tick. Commanding a body that died this tick is an easy and
    /// FORGIVABLE mistake under persistent memory, so it must not be a fault
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.4).
    /// </summary>
    Rejected = 1,
}
