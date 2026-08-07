using BotArena.Sdk;

/// <summary>
/// The mind's ONE movement seam. Every displacing command in the executor
/// goes through one of six <see cref="ArenaBasics"/> wrappers
/// (TryMoveDirect, TryMoveToward, TryMoveHomeward, TryEvade, TryStepAway,
/// TryMoveAside); each of those keeps its name, its legality handling and
/// its reason strings — traces read them — and asks a stepper the single
/// question "which adjacent tile does this body take". Distance and
/// reachability stay free functions on <see cref="ArenaBasics"/>: the
/// flow field is the heuristic, never the chooser.
///
/// <para>The stepper instance belongs to ONE mind (one participant). It is
/// carried on the per-tick <see cref="ArenaBasics.Claims"/> rather than a
/// static, because an in-process mirror cell runs both participants inside
/// the same loaded assembly.</para>
/// </summary>
internal interface IArenaStepper
{
    /// <summary>
    /// Whether the executor should widen its per-tick body order with the
    /// "in a fight" tier. Greedy stepping keeps the historical order
    /// exactly; a cooperative planner wants contact resolved before free
    /// traffic, because a body in contact is the one whose tile everybody
    /// else must route around.
    /// </summary>
    bool WantsFightPrecedence { get; }

    /// <summary>Reset per-tick planning state. Called once per Think.</summary>
    void BeginTick(
        GenericActorResolvedMatchContract contract,
        MindContext mind);

    /// <summary>
    /// The adjacent tile this body should take, or null for "no step".
    /// </summary>
    Position? Step(StepRequest request);

    /// <summary>
    /// Reservation hook: the wrapper actually submitted a move onto
    /// <paramref name="destination"/>. Fired beside the tile claim, so a
    /// planner's space-time table and the executor's claims never disagree.
    /// </summary>
    void NoteCommitted(MindBody body, Position destination);
}

/// <summary>What a wrapper is asking for. One request type for one
/// <see cref="IArenaStepper.Step"/>; the intent names which of the six
/// wrappers asked and how <see cref="Positions"/> reads.</summary>
internal enum StepIntent
{
    /// <summary>TryMoveToward / TryEvade: reach any of the goal tiles.</summary>
    Toward,

    /// <summary>TryMoveHomeward: one goal, never lengthening the route.</summary>
    Homeward,

    /// <summary>TryStepAway: open the range from the threat tiles.</summary>
    Away,

    /// <summary>TryMoveAside: leave the forbidden tiles behind.</summary>
    Aside,

    /// <summary>TryMoveDirect: this exact adjacent tile, or nothing.</summary>
    Direct,
}

/// <summary>One movement question, tightly coupled to
/// <see cref="IArenaStepper"/> and colocated with it.</summary>
/// <param name="Positions">Goals for <see cref="StepIntent.Toward"/>, the
/// single goal for <see cref="StepIntent.Homeward"/>, the threats for
/// <see cref="StepIntent.Away"/>, the forbidden tiles for
/// <see cref="StepIntent.Aside"/>, the single destination for
/// <see cref="StepIntent.Direct"/>.</param>
internal sealed record StepRequest(
    StepIntent Intent,
    GenericActorResolvedMatchContract Contract,
    MindContext Mind,
    MindBody Body,
    IReadOnlyCollection<Position> Positions,
    ArenaBasics.Claims Claims);
