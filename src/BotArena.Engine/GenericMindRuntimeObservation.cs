using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Runtime-neutral public pre-tick input for ONE SUBMITTED PARTICIPANT under
/// the mind profile (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.3,
/// §4.5). It replaces the per-life observation: the team-shared union arrives
/// once in <see cref="Team"/>, the participant's own bodies arrive in
/// <see cref="Bodies"/>, and its complete slot table arrives every tick in
/// <see cref="Slots"/>.
/// <para>
/// The three collections split what the per-life <c>Allies</c> conflated.
/// <see cref="Bodies"/> is "bodies I command"; <see cref="Allies"/> is "allied
/// bodies I do NOT command" and is ALWAYS EMPTY in head-to-head, because there
/// is one participant per scoring team. That split is the #190 rider made
/// structural: there is no way to express "move my ally's body" (§1.3).
/// </para>
/// </summary>
/// <param name="ParticipantId">The mind this observation is addressed to.</param>
/// <param name="TeamId">Its scoring team.</param>
/// <param name="Bodies">
/// Own live bodies in canonical actor-identity order. Empty on a tick the mind
/// owns nothing — and the mind still ticks (§2.7).
/// </param>
/// <param name="Slots">
/// EVERY own slot, live or not, published every tick rather than only at start
/// (§13.2). It costs a few hundred bytes and it is the difference between a
/// draft phase being a feature and being a migration.
/// </param>
/// <param name="Allies">
/// Allied MINDS' bodies, in exactly today's <c>ObservedAllyState</c> shape and
/// under exactly today's team-perception policy.
/// </param>
/// <param name="Team">The union computed once per team per tick.</param>
/// <param name="AlliedIntents">
/// RESERVED (§11). Always empty in v1; the engine writes the empty collection
/// so the field ID is spent and the shape is negotiated.
/// </param>
public sealed record GenericMindRuntimeObservation(
    int SchemaVersion,
    int Tick,
    string MatchContractFingerprint,
    int ParticipantId,
    int TeamId,
    ImmutableArray<GenericMindRuntimeObservation.ObservedBodyState> Bodies,
    ImmutableArray<GenericMindRuntimeObservation.ObservedOwnSlot> Slots,
    ImmutableArray<GenericActorRuntimeObservation.ObservedAllyState> Allies,
    GenericMindTeamProjection Team,
    ImmutableArray<GenericMindRuntimeObservation.AlliedIntent> AlliedIntents)
{
    /// <inheritdoc cref="GenericMindTeamProjection.Enemies"/>
    public ImmutableArray<GenericActorRuntimeObservation.ObservedEnemyState>
        Enemies => Team.Enemies;

    /// <inheritdoc cref="GenericMindTeamProjection.VisibleTiles"/>
    public ImmutableArray<GenericActorRuntimeObservation.ObservedTile>
        VisibleTiles => Team.VisibleTiles;

    /// <inheritdoc cref="GenericMindTeamProjection.VisibleEvents"/>
    public ImmutableArray<GenericActorRuntimeObservation.ObservedEvent>
        VisibleEvents => Team.VisibleEvents;

    /// <inheritdoc cref="GenericMindTeamProjection.Mode"/>
    public GenericActorRuntimeObservation.ModeObservationState Mode =>
        Team.Mode;

    /// <inheritdoc cref="GenericMindTeamProjection.Scoreboard"/>
    public GenericActorRuntimeObservation.ScoreboardState Scoreboard =>
        Team.Scoreboard;

    /// <summary>
    /// One body this mind commands. Fields down to
    /// <see cref="CarriedScrap"/> are exactly today's
    /// <c>ObservedSelfState</c> field set; the rest are facts the mind is
    /// entitled to and a per-life bot was not.
    /// </summary>
    /// <param name="ActorId">
    /// The runtime life identity. <c>ActorId.UnitId</c> is the STABLE handle
    /// that survives death — the thing a mind's plans are keyed by.
    /// </param>
    /// <param name="PreviousPosition">
    /// Where this body stood at the end of the previous tick, or null on a
    /// life's first tick.
    /// </param>
    /// <param name="MovedLastTick">
    /// The top-ranked platform ask of wave 8 — 8 of 8 authors asked the
    /// platform to publish it, because "the channel's central fact is
    /// derivable only from life-scoped memory a newborn lacks". Under the mind
    /// it is free: the mind held last tick's positions anyway. Publishing it
    /// removes the last nine-line reconstruction footgun (§2.3).
    /// </param>
    /// <param name="LifeStartedTick">The tick this life first existed.</param>
    /// <param name="Origin">
    /// Why this body exists. It was a start-time fact about "me" under the
    /// per-life model; under the mind it is a per-BODY fact delivered on the
    /// tick that body first appears (§2.7).
    /// </param>
    /// <param name="RoleTag">
    /// RESERVED (§12): the label this mind last attached to the body.
    /// Non-authoritative — the engine never branches on it — free vocabulary,
    /// sticky, and null throughout v1 because P2 ships <c>SetRole</c>.
    /// </param>
    public sealed record ObservedBodyState(
        ActorIdentity ActorId,
        int Generation,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int? Energy,
        GenericActorRuntimeActionResolution? PreviousActionResolution,
        GenericActorRuntimeObservation.PendingSameLifeTransition?
            PendingSameLifeTransition,
        Position? PreviousPosition,
        bool MovedLastTick,
        int LifeStartedTick,
        GenericActorRuntimeStart.LifeOrigin Origin,
        ImmutableArray<GenericActorRuntimeActionLegality> ActionLegalities)
    {
        /// <summary>The stable team-local slot handle, which survives death.</summary>
        public int UnitId => ActorId.UnitId;

        /// <inheritdoc cref="GenericActorRuntimeObservation.ObservedSelfState.ClassId"/>
        public string? ClassId { get; init; }

        /// <inheritdoc cref="GenericActorRuntimeObservation.ObservedSelfState.RouteCooldowns"/>
        public ImmutableArray<
            GenericActorRuntimeObservation.ObservedRouteCooldown>
            RouteCooldowns
        { get; init; } = [];

        /// <inheritdoc cref="GenericActorRuntimeObservation.ObservedSelfState.CarriedScrap"/>
        public int CarriedScrap { get; init; }

        /// <summary>RESERVED (§12). Always null in v1.</summary>
        public string? RoleTag { get; init; }
    }

    /// <summary>
    /// One of the participant's own stable slots, published every tick.
    /// <see cref="State"/> is the existing closed union, unchanged.
    /// </summary>
    /// <param name="ClassId">
    /// The slot's declared chassis (§9.2), or null on a classless ruleset. It
    /// is a SLOT fact rather than a participant fact so a mixed composition
    /// has one honest answer to "what does slot 4 become".
    /// </param>
    /// <param name="CandidateClassIds">
    /// RESERVED (§10.2). Empty means the slot's chassis is <c>fixed</c>, which
    /// is the only kind v1 admits.
    /// </param>
    /// <param name="SelectedClassId">
    /// RESERVED (§10.2). Null until a chassis is chosen at activation, which
    /// never happens in v1.
    /// </param>
    public sealed record ObservedOwnSlot(
        int TeamId,
        int UnitId,
        GenericActorRuntimeObservation.UnitSlotState State,
        string? ClassId = null)
    {
        public ImmutableArray<string> CandidateClassIds { get; init; } = [];

        public string? SelectedClassId { get; init; }
    }

    /// <summary>
    /// RESERVED (§11.1). An allied mind's declaration from the PREVIOUS tick —
    /// the one-tick delay is what preserves the frozen-observation invariant
    /// and makes intents replay-verifiable. Never populated in v1.
    /// </summary>
    public sealed record AlliedIntent(
        int ParticipantId,
        string TagId,
        long Value);
}
