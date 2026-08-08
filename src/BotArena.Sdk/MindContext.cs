using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One tick's frozen public state for one MIND, plus the surface it commands
/// through. Delivered exactly once per tick, for every tick of the match.
///
/// <para><b>Read the shape as three lifetimes.</b> <see cref="Bodies"/> and
/// <see cref="Slots"/> are yours and are the new centre of gravity. The team
/// perception block — <see cref="Allies"/>, <see cref="Enemies"/>,
/// <see cref="VisibleTiles"/>, <see cref="VisibleProjectiles"/>,
/// <see cref="VisibleEvents"/>, <see cref="HeardSounds"/> — is the scoring
/// team's observable union, computed once per team per tick and delivered ONCE
/// rather than copied into every body. <see cref="Mode"/>,
/// <see cref="Scoreboard"/> and <see cref="Participants"/> are the public match
/// state, also delivered once.</para>
///
/// <para><b>The three body collections mean three different things, and the
/// distinction is the whole participant boundary.</b> <see cref="Bodies"/> is
/// "bodies I command". <see cref="Allies"/> is "allied bodies I do NOT
/// command" — it is ALWAYS EMPTY in head-to-head and in free-for-all, because
/// there is one participant per scoring team in both, and it carries another
/// mind's bodies only in a team format. <see cref="Enemies"/> is visible enemy
/// bodies. There is no collection you can write a command into except your
/// own.</para>
///
/// <para><b>What is NOT here, on purpose.</b> The static contract — rules, map,
/// topology, action catalog, mode binding — arrives once at
/// <see cref="IGenericMindBot.StartMatch"/> and is joined to this observation by
/// <see cref="MatchContractFingerprint"/>. It is not repeated per tick, so read
/// thresholds and prices from <see cref="MindStart.Contract"/> and keep them in
/// your own fields.</para>
///
/// <para><b>Everything here is frozen before any decision executes.</b> No body
/// on either side has acted for this tick yet, so <see cref="Enemies"/> is
/// where they were, not where they are about to be, and nothing you write to a
/// body changes what another body reads. That invariant is what makes the match
/// deterministic and replayable, and it is why an intent channel between allied
/// minds would have to arrive a tick late.</para>
/// </summary>
public sealed record MindContext
{
    /// <summary>Only mind observation schema accepted by this SDK build.</summary>
    public const int CurrentSchemaVersion =
        GenericMindContractVersions.ObservationSchemaVersion;

    /// <summary>Creates one immutable pre-tick mind observation.</summary>
    /// <param name="schemaVersion">Negotiated observation schema version.</param>
    /// <param name="tick">Zero-based authoritative tick about to execute.</param>
    /// <param name="matchContractFingerprint">
    /// Fingerprint of the static contract delivered at match start.
    /// </param>
    /// <param name="bodies">Own live bodies, canonical order.</param>
    /// <param name="slots">Every own slot, live or not.</param>
    /// <param name="allies">Allied MINDS' bodies; empty outside team formats.</param>
    /// <param name="enemies">Sensor-visible enemy body state.</param>
    /// <param name="visibleTiles">The team's visible-tile union.</param>
    /// <param name="visibleProjectiles">
    /// Visible projectiles, or <see langword="null"/> when unsupported.
    /// </param>
    /// <param name="visibleEvents">Sight-visible events from prior ticks.</param>
    /// <param name="heardSounds">
    /// Redacted heard events, or <see langword="null"/> when unsupported.
    /// </param>
    /// <param name="scoreboard">Authoritative public scores and eligibility.</param>
    /// <param name="mode">Mode-specific public objective state.</param>
    /// <param name="participants">Public runtime status for every participant.</param>
    /// <param name="alliedIntents">Reserved; always empty.</param>
    public MindContext(
        int schemaVersion,
        int tick,
        string matchContractFingerprint,
        IEnumerable<MindBody> bodies,
        IEnumerable<MindSlot> slots,
        IEnumerable<GenericActorContext.ObservedAllyState> allies,
        IEnumerable<GenericActorContext.ObservedEnemyState> enemies,
        IEnumerable<GenericActorContext.ObservedTile> visibleTiles,
        IEnumerable<GenericActorContext.ObservedProjectile>? visibleProjectiles,
        IEnumerable<GenericActorContext.ObservedEvent> visibleEvents,
        IEnumerable<GenericActorContext.ObservedSound>? heardSounds,
        GenericActorContext.ScoreboardState scoreboard,
        GenericActorContext.ModeObservationState mode,
        IEnumerable<GenericActorContext.ObservedParticipantStatus> participants,
        IEnumerable<AlliedIntent>? alliedIntents = null)
    {
        if (schemaVersion
            != GenericMindContractVersions.ObservationSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                "Mind observations require the exact profile's observation schema.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(scoreboard);
        ArgumentNullException.ThrowIfNull(mode);

        SchemaVersion = schemaVersion;
        Tick = tick;
        MatchContractFingerprint = GenericActorDynamicValueRules.Fingerprint(
            matchContractFingerprint,
            nameof(matchContractFingerprint));
        Bodies = Canonical(bodies, nameof(bodies), body => body.ActorId);
        Slots = Canonical(slots, nameof(slots), slot => slot.UnitId);
        Allies = Canonical(allies, nameof(allies), ally => ally.ActorId);
        Enemies = Canonical(enemies, nameof(enemies), enemy => enemy.ActorId);
        VisibleTiles = Canonical(
            visibleTiles,
            nameof(visibleTiles),
            tile => (tile.Position.Y, tile.Position.X));
        VisibleProjectiles = visibleProjectiles is null
            ? null
            : Canonical(
                visibleProjectiles,
                nameof(visibleProjectiles),
                projectile => projectile.ProjectileId);
        VisibleEvents = GenericActorDynamicValueRules.Snapshot(
            visibleEvents,
            nameof(visibleEvents));
        HeardSounds = heardSounds is null
            ? null
            : GenericActorDynamicValueRules.Snapshot(
                heardSounds,
                nameof(heardSounds));
        Scoreboard = scoreboard;
        Mode = mode;
        Participants = Canonical(
            participants,
            nameof(participants),
            participant => participant.ParticipantId);
        AlliedIntents = alliedIntents is null
            ? []
            : GenericActorDynamicValueRules.Snapshot(
                alliedIntents,
                nameof(alliedIntents));

        var ownIdentities = Bodies
            .Select(body => body.ActorId)
            .ToHashSet();
        if (Allies.Any(ally => ownIdentities.Contains(ally.ActorId)))
        {
            throw new ArgumentException(
                "A body this mind commands cannot also appear in Allies.",
                nameof(allies));
        }
    }

    /// <summary>Negotiated observation schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Zero-based authoritative tick about to execute. It increases by exactly
    /// one every call, including ticks on which you own nothing, so counting
    /// ticks yourself is never necessary.
    /// </summary>
    public int Tick { get; }

    /// <summary>
    /// Fingerprint joining this observation to the static contract delivered at
    /// match start.
    /// </summary>
    public string MatchContractFingerprint { get; }

    /// <summary>
    /// The bodies this mind commands, in canonical identity order.
    ///
    /// <para>EMPTY is an ordinary state, not an error: every body can be dead
    /// at once while respawn timers run, and
    /// <see cref="IGenericMindBot.Think"/> still runs so the return can be
    /// planned. Ask <c>Bodies.Length</c> rather than writing "am I alive"
    /// branches.</para>
    ///
    /// <para>Writing a command onto one of these is how the mind acts — see
    /// <see cref="MindBody.Command(string, int, GenericActorActionArgument[])"/>.
    /// Bodies you do not touch wait.</para>
    /// </summary>
    public ImmutableArray<MindBody> Bodies { get; }

    /// <summary>
    /// EVERY slot this mind owns, live or not, published every tick — ascending
    /// by unit ID. A slot outlives the bodies in it, so this is where you read
    /// the exact tick a dead body comes back, whether a slot is ready to be
    /// fabricated into, and which chassis it will carry.
    /// </summary>
    public ImmutableArray<MindSlot> Slots { get; }

    /// <summary>
    /// Allied MINDS' bodies — allied bodies this mind does NOT command, in
    /// exactly today's shared-ally shape and under exactly today's
    /// team-perception policy.
    ///
    /// <para>ALWAYS EMPTY in head-to-head and free-for-all, because both put one
    /// participant on a scoring team. Do not treat an empty collection as "I
    /// have no teammates": your own bodies are in <see cref="Bodies"/>.</para>
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedAllyState> Allies { get; }

    /// <summary>
    /// Enemy bodies currently visible to at least one of the scoring team's
    /// declared observers, with the provenance of who saw them. Delivered once
    /// for the whole team rather than once per body.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedEnemyState> Enemies { get; }

    /// <summary>
    /// The team's visible-tile union with per-tile observation provenance. This
    /// is the largest single term in the observation and the reason the mind
    /// profile costs a fraction of the per-life one: the union is computed once
    /// per team per tick instead of once per body.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedTile> VisibleTiles { get; }

    /// <summary>
    /// <see langword="null"/> means projectile observation is unsupported by
    /// this contract; empty means supported with nothing currently visible.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedProjectile>?
        VisibleProjectiles
    { get; }

    /// <summary>
    /// Events visible through sight, ordered by source tick then source
    /// ordinal. A heard-only event appears in <see cref="HeardSounds"/> instead.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedEvent> VisibleEvents { get; }

    /// <summary>
    /// <see langword="null"/> means hearing is unsupported; empty means
    /// supported with no report this tick. Sounds carry a coarse bearing and
    /// distance band rather than a position — integrating them across ticks is
    /// exactly the kind of belief-keeping a mind's persistent fields make cheap.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedSound>? HeardSounds { get; }

    /// <summary>Authoritative public team scores and ranking eligibility.</summary>
    public GenericActorContext.ScoreboardState Scoreboard { get; }

    /// <summary>
    /// Mode-specific public objective state for this pre-tick snapshot — the
    /// objective position, capture progress, any live hold, and the economy's
    /// public banks and piles when the contract declares one.
    /// </summary>
    public GenericActorContext.ModeObservationState Mode { get; }

    /// <summary>
    /// Public runtime-fault and disqualification status for EVERY participant,
    /// including the opponent's. A rising fault count on the other side is
    /// public information.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedParticipantStatus>
        Participants
    { get; }

    /// <summary>
    /// RESERVED. Declarations from allied minds, delivered one tick after they
    /// were made so that observations stay frozen. ALWAYS EMPTY under every
    /// shipped format, because no shipped format has allied minds.
    /// </summary>
    public ImmutableArray<AlliedIntent> AlliedIntents { get; }

    /// <summary>
    /// Deterministic randomness private to this mind, advancing across ticks for
    /// the whole match. Nothing else can reproduce its sequence.
    /// </summary>
    public IBotRandom Random { get; init; } = null!;

    /// <summary>
    /// Deterministic randomness shared with ALLIED MINDS, re-derived from
    /// (team seed, <see cref="Tick"/>) at the start of every tick.
    ///
    /// <para>Inside a single mind it buys nothing — you do not need to agree
    /// with yourself — so in the shipped head-to-head format it is inert and
    /// <see cref="Random"/> is the one to use. It exists for the team formats
    /// where two separately authored minds share a scoring team and genuinely
    /// cannot compute each other's plan: there, a stream the opponent cannot
    /// derive is the only channel that exists before any intent has been
    /// delivered.</para>
    /// </summary>
    public IBotRandom TeamRandom { get; init; } = null!;

    /// <summary>Bounded diagnostic output; never part of the wire observation.</summary>
    public IBotDebug Debug { get; init; } = null!;

    /// <summary>
    /// Finds one of this mind's live bodies by its STABLE unit slot — the query
    /// a plan keyed by unit ID needs after a respawn changed the life ID.
    /// </summary>
    /// <param name="unitId">Stable team-local unit identifier.</param>
    /// <returns>
    /// The live body in that slot, or <see langword="null"/> when the slot has
    /// none this tick (it is dead, dormant, or pending).
    /// </returns>
    public MindBody? Body(int unitId) =>
        Bodies.FirstOrDefault(body => body.UnitId == unitId);

    /// <summary>
    /// Try-form of <see cref="Body(int)"/>, for the common shape "my plan named
    /// unit N; is unit N still alive to execute it?".
    /// </summary>
    /// <param name="unitId">Stable team-local unit identifier.</param>
    /// <param name="body">The live body, when one exists.</param>
    /// <returns>Whether that slot currently holds one of this mind's bodies.</returns>
    public bool TryBody(int unitId, out MindBody body)
    {
        MindBody? found = Body(unitId);
        body = found!;
        return found is not null;
    }

    /// <summary>Finds one of this mind's slots by unit ID.</summary>
    /// <param name="unitId">Stable team-local unit identifier.</param>
    /// <returns>The slot, or <see langword="null"/> if this mind owns none.</returns>
    public MindSlot? Slot(int unitId) =>
        Slots.FirstOrDefault(slot => slot.UnitId == unitId);

    /// <summary>Harvests every command the mind wrote, in canonical body order.</summary>
    internal ImmutableArray<MindCommand> HarvestCommands() =>
    [
        .. Bodies
            .Select(body => body.HarvestCommand())
            .Where(command => command is not null)
            .Select(command => command!),
    ];

    private static ImmutableArray<T> Canonical<T, TKey>(
        IEnumerable<T> values,
        string parameterName,
        Func<T, TKey> key)
        where T : class
        where TKey : IComparable<TKey>
    {
        ImmutableArray<T> snapshot = GenericActorDynamicValueRules.Snapshot(
            values,
            parameterName);
        T[] ordered = snapshot.OrderBy(key).ToArray();
        GenericActorDynamicValueRules.EnsureUnique(
            ordered.Select(key),
            parameterName);
        return ordered.ToImmutableArray();
    }

    /// <summary>
    /// RESERVED. One allied mind's declaration from the PREVIOUS tick. The
    /// one-tick delay is the design rather than a limitation: it is what keeps
    /// the frozen-observation invariant intact and makes a declaration
    /// re-derivable from the recorded decisions of the tick before, which is
    /// what makes it verifiable in a replay.
    /// </summary>
    /// <param name="ParticipantId">The allied mind that declared it.</param>
    /// <param name="TagId">Lowercase kebab semantic ID.</param>
    /// <param name="Value">The declaration's payload.</param>
    public sealed record AlliedIntent(
        int ParticipantId,
        string TagId,
        long Value);
}
