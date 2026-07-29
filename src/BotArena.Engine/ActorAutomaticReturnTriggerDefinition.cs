namespace BotArena.Engine;

/// <summary>
/// Declares that a same-life route is <em>also</em> fired by the engine, with
/// no action, the moment a typed counter scoped to the route's source form
/// reaches <see cref="Threshold"/>. One primitive, two skills
/// (<c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>, "Current kit"): VOLLEY
/// returns after one fan, AEGIS SHELL shatters on its third deflection. The
/// budget stops being driver etiquette and becomes a rule.
///
/// The route keeps its declared action, so the manual early exit stays legal
/// below the threshold — leaving early is a choice, staying past it is not.
/// The trigger is an omitted-when-absent property (DECISIONS #156): a route
/// without one writes no bytes, so every contract authored before this
/// existed keeps its exact fingerprint, and both mirrors reject an object
/// that encodes "no trigger" a second way.
/// </summary>
public sealed record ActorAutomaticReturnTriggerDefinition
{
    public ActorAutomaticReturnTriggerDefinition(
        AutomaticReturnCounterKind counter,
        int threshold)
    {
        if (!Enum.IsDefined(counter))
            throw new ArgumentOutOfRangeException(nameof(counter));
        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                threshold,
                "An automatic-return threshold counts at least one event; a "
                + "route that never fires automatically omits the trigger "
                + "instead of declaring a zero threshold.");
        }

        Counter = counter;
        Threshold = threshold;
    }

    public AutomaticReturnCounterKind Counter { get; }

    /// <summary>
    /// The count at which the return begins. The engine queues on the exact
    /// tick the counter first reaches it, and the chronology refuses both a
    /// suppressed return and one forged before its count.
    /// </summary>
    public int Threshold { get; }

    /// <summary>
    /// What the trigger counts. Every counter is scoped to the current
    /// occupancy of the route's source form: it starts at zero when the life
    /// enters that form and it cannot survive the life, so nothing carries
    /// across a respawn or across a stance cycle.
    /// </summary>
    public enum AutomaticReturnCounterKind
    {
        /// <summary>
        /// Successful attack actions issued from the source form. One action
        /// is one count whatever its declared projectile count, so a volley
        /// fan is one cast rather than three.
        /// </summary>
        AttacksIssuedSinceEnteringSourceForm = 0,

        /// <summary>
        /// Hostile projectiles this life's form guard has deflected since
        /// entering the source form. Several may land on one tick; the
        /// threshold is reached the moment the running count reaches it.
        /// </summary>
        ProjectilesDeflectedSinceEnteringSourceForm = 1,
    }
}
