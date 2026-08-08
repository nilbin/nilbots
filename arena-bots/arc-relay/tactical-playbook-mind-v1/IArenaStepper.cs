using BotArena.Sdk;

/// <summary>
/// The mind's ONE movement arbiter. Every displacing command in the
/// executor goes through an <see cref="ArenaBasics"/> wrapper
/// (TryMoveDirect, TryMoveToward, TryMoveHomeward, TryMoveRouted, TryEvade,
/// TryStepAway, TryMakeWay); each of those keeps its name, its legality
/// handling and its reason strings — traces read them — and asks the
/// stepper the single question "which adjacent tile does this body take".
/// Distance and reachability stay free functions on
/// <see cref="ArenaBasics"/>: the flow field is the heuristic, never the
/// chooser.
///
/// <para><b>Nothing else displaces a body.</b> Carrier lanes, escort
/// right-of-way and a routed delivery's committed corridor are all
/// RESERVATIONS on the per-tick <see cref="ArenaBasics.Claims"/> plane,
/// which the arbiter reads BEFORE it chooses a tile — they are inputs, not
/// vetoes applied after the fact. A body that ends up standing on ground
/// the plane owes a stronger body is stepped aside by the arbiter itself,
/// through <see cref="StepIntent.MakeWay"/>, with one reason string naming
/// one author. (Owner scene 2026-08-10: "a brief dance that was
/// unwarranted just because the carrier was close" was three authorities —
/// the wall, the shove, and the replan — disagreeing about one tile.)</para>
///
/// <para>The stepper instance belongs to ONE mind (one participant). It is
/// carried on the per-tick <see cref="ArenaBasics.Claims"/> rather than a
/// static, because an in-process mirror cell runs both participants inside
/// the same loaded assembly.</para>
/// </summary>
internal interface IArenaStepper
{
    /// <summary>
    /// Whether the plane's precedence list carries its "in a fight" tier
    /// into the executor's per-tick body order. Greedy stepping keeps the
    /// historical order exactly (fighters collapse into <c>rest</c>); a
    /// cooperative planner wants contact resolved before free traffic,
    /// because a body in contact is the one whose tile everybody else must
    /// route around. Right-of-way itself always reads the full list —
    /// this knob is about plan ORDER, not about who yields.
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

    /// <summary>
    /// TryMoveRouted: a carrier's own COMMITTED plan — the sticky delivery
    /// corridor waypoint, or the reactor — walked as one goal with visible
    /// spawn reservations treated as durable obstacles for the whole route
    /// rather than for one step. It is the carrier's plan expressed through
    /// this seam instead of beside it, which is what lets a cooperative
    /// planner reserve the corridor the carrier is going to walk.
    /// </summary>
    Routed,

    /// <summary>
    /// The politeness rule: somebody stronger wants the exact tile this
    /// body is standing on, so it steps aside. The ONE displacement the
    /// movement plane authors on its own — carrier lane relief, escort
    /// yield, return-lane clearance and "that bot could just move out of
    /// the way" are all this question, asked once.
    /// <see cref="StepRequest.Positions"/> is the ground it may not take
    /// (its own tile, the lanes it yields to, every requested tile) and
    /// <see cref="StepRequest.Anchor"/> is the mover it is making way for.
    /// </summary>
    MakeWay,

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
/// single goal for <see cref="StepIntent.Homeward"/> and
/// <see cref="StepIntent.Routed"/>, the threats for
/// <see cref="StepIntent.Away"/>, the forbidden tiles for
/// <see cref="StepIntent.Aside"/> and <see cref="StepIntent.MakeWay"/>, the
/// single destination for <see cref="StepIntent.Direct"/>.</param>
/// <param name="Anchor">Only <see cref="StepIntent.MakeWay"/> uses it: the
/// tile of the body the ground is owed to, so "straight on, away from
/// whoever wants through" is expressible as a preference instead of as a
/// second mover.</param>
internal sealed record StepRequest(
    StepIntent Intent,
    GenericActorResolvedMatchContract Contract,
    MindContext Mind,
    MindBody Body,
    IReadOnlyCollection<Position> Positions,
    ArenaBasics.Claims Claims,
    Position? Anchor = null);
