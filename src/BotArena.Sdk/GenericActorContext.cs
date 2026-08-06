using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Canonical schema-3 public pre-tick input for one generic actor life. Static
/// rules, map, topology, and counts are joined through the match fingerprint
/// delivered at life start.
/// </summary>
public sealed record GenericActorContext
{
    /// <summary>Only observation schema accepted by this SDK build.</summary>
    public const int CurrentSchemaVersion =
        GenericActorContractVersions.ObservationSchemaVersion;

    /// <summary>Creates one immutable pre-tick generic actor observation.</summary>
    /// <param name="schemaVersion">Negotiated observation schema version.</param>
    /// <param name="tick">Zero-based authoritative tick about to execute.</param>
    /// <param name="matchContractFingerprint">
    /// Fingerprint of the static contract delivered at life start.
    /// </param>
    /// <param name="self">Complete private state of the observing life.</param>
    /// <param name="teamUnits">Every stable unit slot on the observing team.</param>
    /// <param name="participants">Public runtime status for every participant.</param>
    /// <param name="allies">Complete active-body state shared by team policy.</param>
    /// <param name="enemies">Sensor-visible enemy body state.</param>
    /// <param name="visibleTiles">Sensor-visible map tiles.</param>
    /// <param name="visibleProjectiles">
    /// Visible projectiles, or <see langword="null"/> when unsupported.
    /// </param>
    /// <param name="visibleEvents">Sight-visible events from prior ticks.</param>
    /// <param name="heardSounds">
    /// Redacted heard events, or <see langword="null"/> when unsupported.
    /// </param>
    /// <param name="scoreboard">Authoritative public scores and eligibility.</param>
    /// <param name="mode">Mode-specific public objective state.</param>
    /// <param name="actionLegalities">Per-action pre-tick legality mask.</param>
    public GenericActorContext(
        int schemaVersion,
        int tick,
        string matchContractFingerprint,
        ObservedSelfState self,
        IEnumerable<ObservedUnitSlot> teamUnits,
        IEnumerable<ObservedParticipantStatus> participants,
        IEnumerable<ObservedAllyState> allies,
        IEnumerable<ObservedEnemyState> enemies,
        IEnumerable<ObservedTile> visibleTiles,
        IEnumerable<ObservedProjectile>? visibleProjectiles,
        IEnumerable<ObservedEvent> visibleEvents,
        IEnumerable<ObservedSound>? heardSounds,
        ScoreboardState scoreboard,
        ModeObservationState mode,
        IEnumerable<GenericActorActionLegality> actionLegalities)
    {
        if (schemaVersion
            != GenericActorContractVersions.ObservationSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                "Generic actor observations require the exact profile's observation schema.");
        }
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(scoreboard);
        ArgumentNullException.ThrowIfNull(mode);

        SchemaVersion = schemaVersion;
        Tick = tick;
        MatchContractFingerprint = GenericActorDynamicValueRules.Fingerprint(
            matchContractFingerprint,
            nameof(matchContractFingerprint));
        Self = self;
        TeamUnits = Canonicalize(
            teamUnits,
            nameof(teamUnits),
            slot => (slot.TeamId, slot.UnitId));
        Participants = Canonicalize(
            participants,
            nameof(participants),
            participant => participant.ParticipantId);
        Allies = Canonicalize(
            allies,
            nameof(allies),
            ally => ally.ActorId);
        Enemies = Canonicalize(
            enemies,
            nameof(enemies),
            enemy => enemy.ActorId);
        VisibleTiles = Canonicalize(
            visibleTiles,
            nameof(visibleTiles),
            tile => (tile.Position.Y, tile.Position.X));
        VisibleProjectiles = visibleProjectiles is null
            ? null
            : Canonicalize(
                visibleProjectiles,
                nameof(visibleProjectiles),
                projectile => projectile.ProjectileId);
        VisibleEvents = ValidateEvents(
            visibleEvents,
            tick,
            nameof(visibleEvents));
        HeardSounds = heardSounds is null
            ? null
            : ValidateSounds(heardSounds, tick, nameof(heardSounds));
        Scoreboard = scoreboard;
        Mode = mode;
        ActionLegalities = CanonicalizeActionLegalities(
            actionLegalities,
            nameof(actionLegalities));

        if (Allies.Any(ally => ally.ActorId == self.ActorId))
        {
            throw new ArgumentException(
                "The observing life cannot also appear in Allies.",
                nameof(allies));
        }
        ValidateAudience(
            self,
            TeamUnits,
            Allies,
            Enemies);
    }

    /// <summary>Negotiated observation schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Zero-based authoritative tick about to execute.</summary>
    public int Tick { get; }
    /// <summary>
    /// Fingerprint joining this dynamic observation to the static MatchStart
    /// contract.
    /// </summary>
    public string MatchContractFingerprint { get; }
    /// <summary>Complete private state of the observing body life.</summary>
    public ObservedSelfState Self { get; }
    /// <summary>
    /// Every stable unit slot on the observing team, including dormant and
    /// pending slots.
    /// </summary>
    public ImmutableArray<ObservedUnitSlot> TeamUnits { get; }
    /// <summary>Public runtime fault and eligibility status for all participants.</summary>
    public ImmutableArray<ObservedParticipantStatus> Participants { get; }
    /// <summary>
    /// Complete active allied states shared by the frozen team-perception policy.
    /// </summary>
    public ImmutableArray<ObservedAllyState> Allies { get; }
    /// <summary>Enemy states currently visible to at least one declared observer.</summary>
    public ImmutableArray<ObservedEnemyState> Enemies { get; }
    /// <summary>Map tiles currently visible to at least one declared observer.</summary>
    public ImmutableArray<ObservedTile> VisibleTiles { get; }

    /// <summary>
    /// Null means projectile observation is unsupported; empty means supported
    /// and no projectile is currently visible.
    /// </summary>
    public ImmutableArray<ObservedProjectile>? VisibleProjectiles { get; }

    /// <summary>
    /// Events visible through sight, ordered by source tick then source ordinal.
    /// A heard-only event is represented in <see cref="HeardSounds"/> instead.
    /// </summary>
    public ImmutableArray<ObservedEvent> VisibleEvents { get; }

    /// <summary>
    /// Null means hearing is unsupported; empty means supported with no report.
    /// </summary>
    public ImmutableArray<ObservedSound>? HeardSounds { get; }

    /// <summary>Authoritative public team scores and ranking eligibility.</summary>
    public ScoreboardState Scoreboard { get; }
    /// <summary>Mode-specific public objective state for this pre-tick snapshot.</summary>
    public ModeObservationState Mode { get; }
    /// <summary>
    /// Per-catalog-action legality in this pre-tick state. Availability cannot
    /// predict simultaneous conflicts with other actors' decisions.
    /// </summary>
    public ImmutableArray<GenericActorActionLegality> ActionLegalities { get; }

    /// <summary>
    /// Deterministic randomness scoped to the exact observing life. Every life
    /// draws a DIFFERENT sequence, so a teammate never reproduces your draw —
    /// use it for private tie-breaks only. A plan that must be shared with the
    /// team belongs on <see cref="TeamRandom"/>.
    /// </summary>
    public IBotRandom Random { get; init; } = null!;

    /// <summary>
    /// Deterministic randomness shared by the whole scoring team, so a
    /// randomized choice can still be a coordinated one.
    ///
    /// <para>
    /// Teams have no channel to talk over. Coordination works because every
    /// life on the team receives this identical frozen observation, so any
    /// pure function of it is a plan all teammates independently agree on.
    /// <see cref="Random"/> breaks that — it differs per life, so a plan that
    /// touches it silently diverges between teammates. <c>TeamRandom</c>
    /// restores it: draws are unpredictable to the opponent, identical inside
    /// the team, and byte-identical on replay.
    /// </para>
    ///
    /// <para><b>What is guaranteed equal.</b> Within one tick, the Nth draw of
    /// this stream returns the same value for every life on your team,
    /// whatever their unit, form, position, or age — including a life created
    /// this very tick, which agrees with its teammates on its FIRST tick. The
    /// two teams draw unrelated sequences, and neither can derive the other's.
    /// </para>
    ///
    /// <para><b>When values change.</b> The stream is re-derived from
    /// (team seed, <see cref="Tick"/>) at the start of every tick — it is
    /// never carried across ticks. So the same draw index gives a different
    /// value on the next tick, and re-drawing it is how a team re-rolls a
    /// shared choice. Nothing you drew on an earlier tick shifts what you draw
    /// on this one, which is exactly why a mid-match respawn stays in step.
    /// </para>
    ///
    /// <para><b>The one rule.</b> Agreement holds while teammates draw the
    /// same number of values in the same order within a tick. Draw before you
    /// branch on private state — <c>bool flank = context.TeamRandom.NextBool();
    /// if (amScout) …</c> agrees; drawing inside a branch only some lives take
    /// does not. A mismatch costs you only that tick: the next tick reseeds
    /// and the team is back in step.
    /// </para>
    /// </summary>
    public IBotRandom TeamRandom { get; init; } = null!;

    /// <summary>Bounded diagnostic output; never part of the wire observation.</summary>
    public IBotDebug Debug { get; init; } = null!;

    /// <summary>Finds one pre-tick legality entry by stable action ID.</summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <returns>The matching entry, or <see langword="null"/> if none exists.</returns>
    public GenericActorActionLegality? Action(string actionId) =>
        ActionLegalities.FirstOrDefault(action =>
            string.Equals(action.ActionId, actionId, StringComparison.Ordinal));

    /// <summary>
    /// Complete private state of the observing body life. Unlike enemy state,
    /// cooldown, energy, prior resolution, and generation are never redacted.
    /// </summary>
    public sealed record ObservedSelfState
    {
        /// <summary>Creates the observing life's complete pre-tick state.</summary>
        /// <param name="actorId">Exact body-life identity.</param>
        /// <param name="generation">Replication/return generation.</param>
        /// <param name="formId">Current form catalog identifier.</param>
        /// <param name="position">Current map tile coordinate.</param>
        /// <param name="facing">Current absolute map direction.</param>
        /// <param name="health">Current positive health.</param>
        /// <param name="cooldown">Ticks remaining on the attack cooldown.</param>
        /// <param name="energy">
        /// Current attack energy, or <see langword="null"/> when the form has
        /// no energy-bearing attack profile.
        /// </param>
        /// <param name="previousActionResolution">
        /// Prior tick result, or <see langword="null"/> before this life has
        /// submitted an action.
        /// </param>
        /// <param name="pendingSameLifeTransition">
        /// Current form-transition windup, or <see langword="null"/>.
        /// </param>
        /// <param name="classId">
        /// Immutable chassis class, or <see langword="null"/> for a classless
        /// contract.
        /// </param>
        /// <param name="routeCooldowns">
        /// Live slot-scoped route cooldowns, or empty when none are live.
        /// </param>
        /// <param name="carriedScrap">
        /// Scrap this body is carrying, or zero — which is also every
        /// contract without a declared economy.
        /// </param>
        public ObservedSelfState(
            ActorIdentity actorId,
            int generation,
            string formId,
            Position position,
            Direction facing,
            int health,
            int cooldown,
            int? energy,
            GenericActorActionResolution? previousActionResolution,
            PendingSameLifeTransition? pendingSameLifeTransition,
            string? classId = null,
            ImmutableArray<ObservedRouteCooldown> routeCooldowns = default,
            int carriedScrap = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(carriedScrap);
            ArgumentNullException.ThrowIfNull(actorId);
            ValidateBody(
                generation,
                formId,
                position,
                facing,
                health,
                cooldown,
                energy);
            ActorId = actorId;
            Generation = generation;
            FormId = formId;
            Position = position;
            Facing = facing;
            Health = health;
            Cooldown = cooldown;
            Energy = energy;
            PreviousActionResolution = previousActionResolution;
            PendingSameLifeTransition = pendingSameLifeTransition;
            ClassId = classId is null
                ? null
                : GenericActorDynamicValueRules.SemanticId(
                    classId,
                    nameof(classId));
            RouteCooldowns = routeCooldowns.IsDefault
                ? []
                : routeCooldowns;
            CarriedScrap = carriedScrap;
        }

        /// <summary>Exact body-life identity.</summary>
        public ActorIdentity ActorId { get; }
        /// <summary>Replication/return generation for this life.</summary>
        public int Generation { get; }
        /// <summary>Current form catalog identifier.</summary>
        public string FormId { get; }
        /// <summary>Current map tile coordinate.</summary>
        public Position Position { get; }
        /// <summary>Current absolute map direction.</summary>
        public Direction Facing { get; }
        /// <summary>Current positive health in health points.</summary>
        public int Health { get; }
        /// <summary>Ticks remaining on the attack cooldown.</summary>
        public int Cooldown { get; }
        /// <summary>
        /// Current attack energy, or <see langword="null"/> when unsupported by
        /// the current form.
        /// </summary>
        public int? Energy { get; }
        /// <summary>
        /// Prior tick result, or <see langword="null"/> before this life has
        /// produced a decision.
        /// </summary>
        public GenericActorActionResolution? PreviousActionResolution { get; }
        /// <summary>Current same-life transition windup, if any.</summary>
        public PendingSameLifeTransition? PendingSameLifeTransition { get; }
        /// <summary>
        /// Immutable chassis class, or null for a classless contract.
        /// </summary>
        public string? ClassId { get; }

        /// <summary>
        /// Every same-life route of this body's stable unit slot currently
        /// held shut by a declared route cooldown, ordered by transition ID.
        /// Each entry names the first tick its route accepts a request
        /// again; the route refuses re-entry while the observed tick is
        /// strictly below it. The clock is scoped to the unit slot, so it
        /// survives this life's death. Empty when no route cooldown is live
        /// — including every contract that declares none.
        /// </summary>
        public ImmutableArray<ObservedRouteCooldown> RouteCooldowns { get; }

        /// <summary>
        /// Scrap this body is currently carrying, which is exactly what a
        /// death on this tile would put on the floor. Stepping onto a pile
        /// banks the declared assay instantly and loads the remainder up to
        /// the declared carry capacity; standing on your own team's declared
        /// banking region converts the whole load, automatically and free.
        /// Zero when carrying nothing, and zero for the whole match on every
        /// contract whose
        /// <c>GenericActorRulesContract.FrontlineGameMode.ScrapEconomy</c> is
        /// absent.
        /// </summary>
        public int CarriedScrap { get; }
    }

    /// <summary>
    /// One live slot-scoped route cooldown: the named same-life transition
    /// refuses requests until the published tick arrives.
    /// </summary>
    public sealed record ObservedRouteCooldown
    {
        /// <summary>Creates one live route-cooldown clock.</summary>
        /// <param name="transitionId">Held same-life transition.</param>
        /// <param name="readyAtTick">First tick the route accepts a request.</param>
        public ObservedRouteCooldown(string transitionId, int readyAtTick)
        {
            TransitionId = GenericActorDynamicValueRules.SemanticId(
                transitionId,
                nameof(transitionId));
            ArgumentOutOfRangeException.ThrowIfNegative(readyAtTick);
            ReadyAtTick = readyAtTick;
        }

        /// <summary>Held same-life transition identifier.</summary>
        public string TransitionId { get; }

        /// <summary>First tick the route accepts a request again.</summary>
        public int ReadyAtTick { get; }
    }

    /// <summary>
    /// Complete pre-tick state of an active allied life made available by the
    /// frozen team-perception policy.
    /// </summary>
    public sealed record ObservedAllyState
    {
        /// <summary>Creates a shared allied body state.</summary>
        /// <param name="actorId">Exact allied body-life identity.</param>
        /// <param name="generation">Replication/return generation.</param>
        /// <param name="formId">Current form catalog identifier.</param>
        /// <param name="position">Current map tile coordinate.</param>
        /// <param name="facing">Current absolute map direction.</param>
        /// <param name="health">Current positive health.</param>
        /// <param name="cooldown">Ticks remaining on attack cooldown.</param>
        /// <param name="energy">Current energy, or <see langword="null"/> if unsupported.</param>
        /// <param name="previousActionResolution">Allied prior action result, if any.</param>
        /// <param name="pendingSameLifeTransition">Allied transition windup, if any.</param>
        /// <param name="classId">Immutable allied chassis class, if declared.</param>
        /// <param name="routeCooldowns">
        /// Live allied slot route cooldowns, or empty when none are live.
        /// </param>
        /// <param name="carriedScrap">The ally's load, or zero.</param>
        public ObservedAllyState(
            ActorIdentity actorId,
            int generation,
            string formId,
            Position position,
            Direction facing,
            int health,
            int cooldown,
            int? energy,
            GenericActorActionResolution? previousActionResolution,
            PendingSameLifeTransition? pendingSameLifeTransition,
            string? classId = null,
            ImmutableArray<ObservedRouteCooldown> routeCooldowns = default,
            int carriedScrap = 0)
        {
            ArgumentNullException.ThrowIfNull(actorId);
            ArgumentOutOfRangeException.ThrowIfNegative(carriedScrap);
            ValidateBody(
                generation,
                formId,
                position,
                facing,
                health,
                cooldown,
                energy);
            ActorId = actorId;
            Generation = generation;
            FormId = formId;
            Position = position;
            Facing = facing;
            Health = health;
            Cooldown = cooldown;
            Energy = energy;
            PreviousActionResolution = previousActionResolution;
            PendingSameLifeTransition = pendingSameLifeTransition;
            ClassId = classId is null
                ? null
                : GenericActorDynamicValueRules.SemanticId(
                    classId,
                    nameof(classId));
            RouteCooldowns = routeCooldowns.IsDefault
                ? []
                : routeCooldowns;
            CarriedScrap = carriedScrap;
        }

        /// <summary>Exact allied body-life identity.</summary>
        public ActorIdentity ActorId { get; }
        /// <summary>Replication/return generation for the allied life.</summary>
        public int Generation { get; }
        /// <summary>Current allied form catalog identifier.</summary>
        public string FormId { get; }
        /// <summary>Current allied map tile coordinate.</summary>
        public Position Position { get; }
        /// <summary>Current allied absolute map direction.</summary>
        public Direction Facing { get; }
        /// <summary>Current positive allied health.</summary>
        public int Health { get; }
        /// <summary>Ticks remaining on the allied attack cooldown.</summary>
        public int Cooldown { get; }
        /// <summary>Current allied energy, or <see langword="null"/> if unsupported.</summary>
        public int? Energy { get; }
        /// <summary>Allied prior action result, if the life has one.</summary>
        public GenericActorActionResolution? PreviousActionResolution { get; }
        /// <summary>Allied same-life transition windup, if any.</summary>
        public PendingSameLifeTransition? PendingSameLifeTransition { get; }
        /// <summary>Immutable allied chassis class, if declared.</summary>
        public string? ClassId { get; }

        /// <summary>
        /// Live allied slot route cooldowns, published under the same
        /// grammar as <see cref="ObservedSelfState.RouteCooldowns"/> —
        /// allies share their complete gameplay state.
        /// </summary>
        public ImmutableArray<ObservedRouteCooldown> RouteCooldowns { get; }

        /// <summary>
        /// The ally's load, published under the same grammar as
        /// <see cref="ObservedSelfState.CarriedScrap"/> — allies share their
        /// complete gameplay state, so an escort knows what it is escorting
        /// and a team can decide whether the walk home is worth covering.
        /// </summary>
        public int CarriedScrap { get; }
    }

    /// <summary>
    /// One in-progress form change that preserves actor identity and private
    /// runtime memory.
    /// </summary>
    public sealed record PendingSameLifeTransition
    {
        /// <summary>Creates transition-windup state.</summary>
        /// <param name="transitionId">Static transition catalog identifier.</param>
        /// <param name="operationId">Unique occurrence handle for this request.</param>
        /// <param name="targetFormId">Form scheduled at completion.</param>
        /// <param name="startedTick">Tick on which the operation was accepted.</param>
        /// <param name="dueTick">Tick on which completion is scheduled.</param>
        public PendingSameLifeTransition(
            string transitionId,
            string operationId,
            string targetFormId,
            int startedTick,
            int dueTick)
        {
            if (startedTick < 0)
                throw new ArgumentOutOfRangeException(nameof(startedTick));
            if (dueTick <= startedTick)
                throw new ArgumentOutOfRangeException(nameof(dueTick));
            TransitionId = GenericActorDynamicValueRules.SemanticId(
                transitionId,
                nameof(transitionId));
            OperationId = GenericActorDynamicValueRules.Handle(
                operationId,
                nameof(operationId));
            TargetFormId = GenericActorDynamicValueRules.SemanticId(
                targetFormId,
                nameof(targetFormId));
            StartedTick = startedTick;
            DueTick = dueTick;
        }

        /// <summary>Static transition catalog identifier.</summary>
        public string TransitionId { get; }
        /// <summary>
        /// Unique occurrence handle shared by events emitted for this operation.
        /// </summary>
        public string OperationId { get; }
        /// <summary>Form scheduled at completion.</summary>
        public string TargetFormId { get; }
        /// <summary>Tick on which the operation was accepted.</summary>
        public int StartedTick { get; }
        /// <summary>Tick on which completion is scheduled.</summary>
        public int DueTick { get; }
    }

    /// <summary>
    /// One stable team/unit slot. A slot persists while successive body-life
    /// identities become active, pending, ready, or permanently dormant.
    /// </summary>
    public sealed record ObservedUnitSlot
    {
        /// <summary>Creates one stable-slot observation.</summary>
        /// <param name="teamId">Owning scoring-team identifier.</param>
        /// <param name="unitId">Stable unit identifier within the team.</param>
        /// <param name="state">Current slot lifecycle state.</param>
        public ObservedUnitSlot(
            int teamId,
            int unitId,
            UnitSlotState state)
        {
            if (teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (unitId < 0)
                throw new ArgumentOutOfRangeException(nameof(unitId));
            ArgumentNullException.ThrowIfNull(state);
            TeamId = teamId;
            UnitId = unitId;
            State = state;
        }

        /// <summary>Owning scoring-team identifier.</summary>
        public int TeamId { get; }
        /// <summary>Stable unit identifier within the team.</summary>
        public int UnitId { get; }
        /// <summary>Current slot lifecycle state.</summary>
        public UnitSlotState State { get; }
    }

    /// <summary>Closed union of stable unit-slot lifecycle states.</summary>
    public abstract record UnitSlotState
    {
        private UnitSlotState()
        {
        }

        /// <summary>Lifecycle-state discriminator.</summary>
        public abstract UnitSlotStateKind Kind { get; }

        /// <summary>The slot currently contains an independently executing life.</summary>
        public sealed record Active : UnitSlotState
        {
            /// <summary>Creates an active-slot state.</summary>
            /// <param name="actorId">Exact active body-life identity.</param>
            /// <param name="generation">Active life generation.</param>
            /// <param name="formId">Current form catalog identifier.</param>
            public Active(
                ActorIdentity actorId,
                int generation,
                string formId)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                if (generation < 0)
                    throw new ArgumentOutOfRangeException(nameof(generation));
                ActorId = actorId;
                Generation = generation;
                FormId = GenericActorDynamicValueRules.SemanticId(
                    formId,
                    nameof(formId));
            }

            /// <inheritdoc />
            public override UnitSlotStateKind Kind =>
                UnitSlotStateKind.Active;
            /// <summary>Exact active body-life identity.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Active life generation.</summary>
            public int Generation { get; }
            /// <summary>Current form catalog identifier.</summary>
            public string FormId { get; }
        }

        /// <summary>The slot is dormant until a declared availability tick.</summary>
        public sealed record AvailabilityPending : UnitSlotState
        {
            /// <summary>Creates a scheduled-availability state.</summary>
            /// <param name="reason">Why the slot is waiting.</param>
            /// <param name="dueTick">Tick on which it becomes available.</param>
            public AvailabilityPending(
                AvailabilityReason reason,
                int dueTick)
            {
                if (dueTick < 0)
                    throw new ArgumentOutOfRangeException(nameof(dueTick));
                Reason = GenericActorDynamicValueRules.EnumValue(
                    reason,
                    nameof(reason));
                DueTick = dueTick;
            }

            /// <inheritdoc />
            public override UnitSlotStateKind Kind =>
                UnitSlotStateKind.AvailabilityPending;
            /// <summary>Why the slot is waiting.</summary>
            public AvailabilityReason Reason { get; }
            /// <summary>Tick on which the slot becomes available.</summary>
            public int DueTick { get; }
        }

        /// <summary>A destroyed slot is scheduled to return automatically.</summary>
        public sealed record AutomaticReturnPending : UnitSlotState
        {
            /// <summary>Creates an automatic-return state.</summary>
            /// <param name="dueTick">Scheduled spawn tick.</param>
            /// <param name="targetFormId">Form of the returning life.</param>
            /// <param name="generation">Generation assigned to the returning life.</param>
            public AutomaticReturnPending(
                int dueTick,
                string targetFormId,
                int generation)
            {
                if (dueTick < 0)
                    throw new ArgumentOutOfRangeException(nameof(dueTick));
                if (generation < 0)
                    throw new ArgumentOutOfRangeException(nameof(generation));
                DueTick = dueTick;
                TargetFormId = GenericActorDynamicValueRules.SemanticId(
                    targetFormId,
                    nameof(targetFormId));
                Generation = generation;
            }

            /// <inheritdoc />
            public override UnitSlotStateKind Kind =>
                UnitSlotStateKind.AutomaticReturnPending;
            /// <summary>Scheduled spawn tick.</summary>
            public int DueTick { get; }
            /// <summary>Form of the returning life.</summary>
            public string TargetFormId { get; }
            /// <summary>Generation assigned to the returning life.</summary>
            public int Generation { get; }
        }

        /// <summary>The slot is available for an eligible lifecycle transition.</summary>
        public sealed record Ready : UnitSlotState
        {
            /// <inheritdoc />
            public override UnitSlotStateKind Kind => UnitSlotStateKind.Ready;
        }

        /// <summary>
        /// Base state for a reserved lifecycle operation that will create a new
        /// life in this slot.
        /// </summary>
        public abstract record LifecyclePending : UnitSlotState
        {
            private protected LifecyclePending(
                int dueTick,
                ActorIdentity sourceActorId,
                string transitionId,
                string operationId,
                string targetFormId,
                Position reservedPosition)
            {
                if (dueTick < 0)
                    throw new ArgumentOutOfRangeException(nameof(dueTick));
                ArgumentNullException.ThrowIfNull(sourceActorId);
                ValidatePosition(reservedPosition, nameof(reservedPosition));
                DueTick = dueTick;
                SourceActorId = sourceActorId;
                TransitionId = GenericActorDynamicValueRules.SemanticId(
                    transitionId,
                    nameof(transitionId));
                OperationId = GenericActorDynamicValueRules.Handle(
                    operationId,
                    nameof(operationId));
                TargetFormId = GenericActorDynamicValueRules.SemanticId(
                    targetFormId,
                    nameof(targetFormId));
                ReservedPosition = reservedPosition;
            }

            /// <summary>Scheduled completion tick.</summary>
            public int DueTick { get; }
            /// <summary>Exact source life that initiated the operation.</summary>
            public ActorIdentity SourceActorId { get; }
            /// <summary>Static transition catalog identifier.</summary>
            public string TransitionId { get; }
            /// <summary>
            /// Unique occurrence handle shared by every output in this operation.
            /// </summary>
            public string OperationId { get; }
            /// <summary>Form assigned to the life created in this slot.</summary>
            public string TargetFormId { get; }
            /// <summary>Tile reserved for the future life.</summary>
            public Position ReservedPosition { get; }
        }

        /// <summary>A fabrication operation has reserved this slot and tile.</summary>
        public sealed record FabricationPending : LifecyclePending
        {
            /// <summary>Creates a pending fabrication reservation.</summary>
            /// <param name="dueTick">Scheduled completion tick.</param>
            /// <param name="sourceActorId">Exact source life.</param>
            /// <param name="transitionId">Static transition catalog identifier.</param>
            /// <param name="operationId">Unique occurrence handle.</param>
            /// <param name="targetFormId">Future child form.</param>
            /// <param name="reservedPosition">Future child tile.</param>
            public FabricationPending(
                int dueTick,
                ActorIdentity sourceActorId,
                string transitionId,
                string operationId,
                string targetFormId,
                Position reservedPosition)
                : base(
                    dueTick,
                    sourceActorId,
                    transitionId,
                    operationId,
                    targetFormId,
                    reservedPosition)
            {
            }

            /// <inheritdoc />
            public override UnitSlotStateKind Kind =>
                UnitSlotStateKind.FabricationPending;
        }

        /// <summary>A replication operation has reserved this slot and tile.</summary>
        public sealed record ReplicationPending : LifecyclePending
        {
            /// <summary>Creates a pending replication reservation.</summary>
            /// <param name="dueTick">Scheduled completion tick.</param>
            /// <param name="sourceActorId">Exact source life.</param>
            /// <param name="transitionId">Static transition catalog identifier.</param>
            /// <param name="operationId">Unique occurrence handle.</param>
            /// <param name="targetFormId">Future descendant form.</param>
            /// <param name="reservedPosition">Future descendant tile.</param>
            public ReplicationPending(
                int dueTick,
                ActorIdentity sourceActorId,
                string transitionId,
                string operationId,
                string targetFormId,
                Position reservedPosition)
                : base(
                    dueTick,
                    sourceActorId,
                    transitionId,
                    operationId,
                    targetFormId,
                    reservedPosition)
            {
            }

            /// <inheritdoc />
            public override UnitSlotStateKind Kind =>
                UnitSlotStateKind.ReplicationPending;
        }

        /// <summary>The slot cannot become active during this match.</summary>
        public sealed record PermanentlyDormant : UnitSlotState
        {
            /// <inheritdoc />
            public override UnitSlotStateKind Kind =>
                UnitSlotStateKind.PermanentlyDormant;
        }
    }

    /// <summary>Discriminator for stable unit-slot lifecycle states.</summary>
    public enum UnitSlotStateKind
    {
        /// <summary>An active body life occupies the slot.</summary>
        Active = 0,
        /// <summary>The slot is waiting for availability.</summary>
        AvailabilityPending = 1,
        /// <summary>A destroyed life is scheduled to return.</summary>
        AutomaticReturnPending = 2,
        /// <summary>The slot is available for lifecycle assignment.</summary>
        Ready = 3,
        /// <summary>A fabrication operation reserved the slot.</summary>
        FabricationPending = 4,
        /// <summary>A replication operation reserved the slot.</summary>
        ReplicationPending = 5,
        /// <summary>The slot cannot activate during this match.</summary>
        PermanentlyDormant = 6,
    }

    /// <summary>Reason a slot is waiting to become available.</summary>
    public enum AvailabilityReason
    {
        /// <summary>Tick-zero deployment declared a delayed unlock.</summary>
        InitialUnlock = 0,
        /// <summary>The slot is recovering after life destruction.</summary>
        DestructionRecovery = 1,
    }

    /// <summary>Public runtime-fault and disqualification state for one participant.</summary>
    public sealed record ObservedParticipantStatus
    {
        /// <summary>Creates participant runtime status.</summary>
        /// <param name="participantId">Submitted-program identifier.</param>
        /// <param name="teamId">Assigned scoring-team identifier.</param>
        /// <param name="runtimeFaultCount">Cumulative participant fault count.</param>
        /// <param name="disqualified">Whether the participant is ineligible to continue.</param>
        /// <param name="classId">Immutable participant chassis class, if declared.</param>
        public ObservedParticipantStatus(
            int participantId,
            int teamId,
            long runtimeFaultCount,
            bool disqualified,
            string? classId = null)
        {
            if (participantId < 0)
                throw new ArgumentOutOfRangeException(nameof(participantId));
            if (teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (runtimeFaultCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runtimeFaultCount));
            }
            ParticipantId = participantId;
            TeamId = teamId;
            RuntimeFaultCount = runtimeFaultCount;
            Disqualified = disqualified;
            ClassId = classId is null
                ? null
                : GenericActorDynamicValueRules.SemanticId(
                    classId,
                    nameof(classId));
        }

        /// <summary>Submitted-program identifier.</summary>
        public int ParticipantId { get; }
        /// <summary>Assigned scoring-team identifier.</summary>
        public int TeamId { get; }
        /// <summary>Cumulative participant fault count.</summary>
        public long RuntimeFaultCount { get; }
        /// <summary>Whether the participant is ineligible to continue.</summary>
        public bool Disqualified { get; }
        /// <summary>Immutable participant chassis class, if declared.</summary>
        public string? ClassId { get; }
    }

    /// <summary>
    /// Sensor-visible enemy body state. Private generation, cooldown, energy,
    /// prior resolution, and runtime memory are intentionally redacted.
    /// </summary>
    public sealed record ObservedEnemyState
    {
        /// <summary>Creates a visible enemy state.</summary>
        /// <param name="actorId">Exact visible enemy body-life identity.</param>
        /// <param name="formId">Visible form catalog identifier.</param>
        /// <param name="position">Visible map tile coordinate.</param>
        /// <param name="facing">Visible absolute map direction.</param>
        /// <param name="health">Visible current positive health.</param>
        /// <param name="pendingSameLifeTransition">Visible form windup, if any.</param>
        /// <param name="observedBy">
        /// Exact allied life identities whose sensors revealed this state.
        /// </param>
        /// <param name="classId">Immutable enemy chassis class, if declared.</param>
        /// <param name="carriedScrap">The enemy's visible load, or zero.</param>
        /// <param name="roleTag">
        /// The label the enemy's own mind published for this body, or null.
        /// It is entirely non-authoritative and it is deliberately PUBLIC: a
        /// label the engine never reads and the enemy can read is a free
        /// deception channel, so a body tagged <c>screen</c> may well be the
        /// channeler. Always null against a per-life opponent, which has no way
        /// to set one.
        /// </param>
        public ObservedEnemyState(
            ActorIdentity actorId,
            string formId,
            Position position,
            Direction facing,
            int health,
            PendingSameLifeTransition? pendingSameLifeTransition,
            IEnumerable<ActorIdentity> observedBy,
            string? classId = null,
            int carriedScrap = 0,
            string? roleTag = null)
        {
            ArgumentNullException.ThrowIfNull(actorId);
            ArgumentOutOfRangeException.ThrowIfNegative(carriedScrap);
            ValidatePosition(position, nameof(position));
            if (health <= 0)
                throw new ArgumentOutOfRangeException(nameof(health));
            ActorId = actorId;
            FormId = GenericActorDynamicValueRules.SemanticId(
                formId,
                nameof(formId));
            Position = position;
            Facing = GenericActorDynamicValueRules.EnumValue(
                facing,
                nameof(facing));
            Health = health;
            PendingSameLifeTransition = pendingSameLifeTransition;
            ObservedBy = GenericActorDynamicValueRules.CanonicalActors(
                observedBy,
                nameof(observedBy));
            ClassId = classId is null
                ? null
                : GenericActorDynamicValueRules.SemanticId(
                    classId,
                    nameof(classId));
            CarriedScrap = carriedScrap;
            RoleTag = roleTag is null
                ? null
                : MindValueRules.RoleTag(roleTag, nameof(roleTag));
        }

        /// <summary>
        /// The label the enemy's own mind published for this body, or null.
        /// Non-authoritative, public on purpose, and never to be trusted: it is
        /// what the opponent SAYS this body's job is.
        /// </summary>
        public string? RoleTag { get; }

        /// <summary>Exact visible enemy body-life identity.</summary>
        public ActorIdentity ActorId { get; }
        /// <summary>Visible form catalog identifier.</summary>
        public string FormId { get; }
        /// <summary>Visible map tile coordinate.</summary>
        public Position Position { get; }
        /// <summary>Visible absolute map direction.</summary>
        public Direction Facing { get; }
        /// <summary>Visible current positive health.</summary>
        public int Health { get; }
        /// <summary>Visible same-life transition windup, if any.</summary>
        public PendingSameLifeTransition? PendingSameLifeTransition { get; }
        /// <summary>
        /// Exact allied life identities whose sensors revealed this state.
        /// Empty is valid only under a provenance policy that declares it.
        /// </summary>
        public ImmutableArray<ActorIdentity> ObservedBy { get; }
        /// <summary>Immutable enemy chassis class, if declared.</summary>
        public string? ClassId { get; }

        /// <summary>
        /// A visible enemy's load. This is the fact that makes interception a
        /// decision rather than a guess: killing a loaded carrier drops its
        /// whole load plus its wreck on one tile, so "is that body worth
        /// chasing" has an answer. Zero when it carries nothing, and zero for
        /// the whole match on every contract without a declared economy.
        /// </summary>
        public int CarriedScrap { get; }
    }

    /// <summary>One sensor-visible map tile and its observation provenance.</summary>
    public sealed record ObservedTile
    {
        /// <summary>Creates a visible tile.</summary>
        /// <param name="position">Map coordinate in tiles.</param>
        /// <param name="isWall">Whether the gameplay tile blocks as a wall.</param>
        /// <param name="observedBy">Allied lives whose sensors revealed the tile.</param>
        /// <param name="spawnReservation">
        /// Visible lifecycle output claim, or <see langword="null"/>.
        /// </param>
        public ObservedTile(
            Position position,
            bool isWall,
            IEnumerable<ActorIdentity> observedBy,
            SpawnReservation? spawnReservation = null)
        {
            ValidatePosition(position, nameof(position));
            Position = position;
            IsWall = isWall;
            ObservedBy = GenericActorDynamicValueRules.CanonicalActors(
                observedBy,
                nameof(observedBy));
            SpawnReservation = spawnReservation;
        }

        /// <summary>Map coordinate in tiles.</summary>
        public Position Position { get; }
        /// <summary>Whether the gameplay tile blocks as a wall.</summary>
        public bool IsWall { get; }
        /// <summary>Exact allied lives whose sensors revealed the tile.</summary>
        public ImmutableArray<ActorIdentity> ObservedBy { get; }
        /// <summary>
        /// Visible lifecycle output claim, or null when this tile is not
        /// reserved for a future or recurring spawn.
        /// </summary>
        public SpawnReservation? SpawnReservation { get; }
    }

    /// <summary>One spawn claim attached to an already visible tile.</summary>
    public sealed record SpawnReservation
    {
        /// <summary>Creates a visible spawn claim.</summary>
        public SpawnReservation(
            int teamId,
            int unitId,
            SpawnReservationKind kind,
            int? dueTick)
        {
            if (teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (unitId < 0)
                throw new ArgumentOutOfRangeException(nameof(unitId));
            Kind = GenericActorDynamicValueRules.EnumValue(
                kind,
                nameof(kind));
            if (kind == SpawnReservationKind.AutomaticReturn
                    && dueTick is not null
                || kind != SpawnReservationKind.AutomaticReturn
                    && dueTick is null or < 0)
            {
                throw new ArgumentException(
                    "Permanent automatic-return claims omit dueTick; pending lifecycle claims require a non-negative dueTick.",
                    nameof(dueTick));
            }
            TeamId = teamId;
            UnitId = unitId;
            DueTick = dueTick;
        }

        public int TeamId { get; }
        public int UnitId { get; }
        public SpawnReservationKind Kind { get; }
        public int? DueTick { get; }
    }

    /// <summary>Why a visible tile is unavailable for another spawn.</summary>
    public enum SpawnReservationKind
    {
        /// <summary>A slot's authored return anchor is permanently claimed.</summary>
        AutomaticReturn = 0,
        /// <summary>A queued fabrication claimed this output tile.</summary>
        Fabrication = 1,
        /// <summary>A queued replication claimed this output tile.</summary>
        Replication = 2,
    }

    /// <summary>
    /// One sensor-visible persistent projectile. Source life identity may be
    /// redacted even when the owning team is known.
    /// </summary>
    public sealed record ObservedProjectile
    {
        /// <summary>Creates a visible projectile state.</summary>
        /// <param name="projectileId">Match-unique projectile identifier.</param>
        /// <param name="ownerTeamId">Owning scoring-team identifier.</param>
        /// <param name="ownerActorId">
        /// Exact firing life when visible, otherwise <see langword="null"/>.
        /// </param>
        /// <param name="position">Current projectile tile coordinate.</param>
        /// <param name="heading">Current absolute heading sector.</param>
        /// <param name="tilesPerAdvance">Tiles crossed on each advance.</param>
        /// <param name="ticksUntilAdvance">Ticks until the next advance.</param>
        /// <param name="remainingTiles">Remaining travel budget in tiles.</param>
        /// <param name="observedBy">Allied lives whose sensors revealed it.</param>
        /// <param name="ticksPerAdvance">
        /// Declared tick cadence between advances for the profile that fired
        /// this projectile.
        /// </param>
        /// <param name="damagePerHit">Health removed by one contact.</param>
        public ObservedProjectile(
            long projectileId,
            int ownerTeamId,
            ActorIdentity? ownerActorId,
            Position position,
            ProjectileHeading heading,
            int tilesPerAdvance,
            int ticksUntilAdvance,
            int remainingTiles,
            IEnumerable<ActorIdentity> observedBy,
            int ticksPerAdvance,
            int damagePerHit)
        {
            if (projectileId < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileId));
            if (ownerTeamId < 0)
                throw new ArgumentOutOfRangeException(nameof(ownerTeamId));
            if (ownerActorId is not null
                && ownerActorId.TeamId != ownerTeamId)
            {
                throw new ArgumentException(
                    "A revealed projectile owner must belong to OwnerTeamId.",
                    nameof(ownerActorId));
            }
            ValidatePosition(position, nameof(position));
            if (tilesPerAdvance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tilesPerAdvance));
            if (ticksUntilAdvance <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksUntilAdvance));
            if (remainingTiles < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingTiles));
            if (ticksPerAdvance <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksPerAdvance));
            if (damagePerHit <= 0)
                throw new ArgumentOutOfRangeException(nameof(damagePerHit));

            ProjectileId = projectileId;
            OwnerTeamId = ownerTeamId;
            OwnerActorId = ownerActorId;
            Position = position;
            Heading = GenericActorDynamicValueRules.EnumValue(
                heading,
                nameof(heading));
            TilesPerAdvance = tilesPerAdvance;
            TicksUntilAdvance = ticksUntilAdvance;
            RemainingTiles = remainingTiles;
            ObservedBy = GenericActorDynamicValueRules.CanonicalActors(
                observedBy,
                nameof(observedBy));
            TicksPerAdvance = ticksPerAdvance;
            DamagePerHit = damagePerHit;
        }

        /// <summary>Match-unique projectile identifier.</summary>
        public long ProjectileId { get; }
        /// <summary>Owning scoring-team identifier.</summary>
        public int OwnerTeamId { get; }
        /// <summary>
        /// Exact firing life when observation policy reveals it; otherwise
        /// <see langword="null"/>.
        /// </summary>
        public ActorIdentity? OwnerActorId { get; }
        /// <summary>Current projectile map tile.</summary>
        public Position Position { get; }
        /// <summary>Current absolute projectile heading sector.</summary>
        public ProjectileHeading Heading { get; }
        /// <summary>Map tiles traversed by each scheduled advance.</summary>
        public int TilesPerAdvance { get; }
        /// <summary>Authoritative ticks remaining until the next advance.</summary>
        public int TicksUntilAdvance { get; }
        /// <summary>Remaining projectile travel budget in map tiles.</summary>
        public int RemainingTiles { get; }
        /// <summary>Exact allied lives whose sensors revealed the projectile.</summary>
        public ImmutableArray<ActorIdentity> ObservedBy { get; }
        /// <summary>
        /// Ticks the firing profile spends between advances. With
        /// <see cref="TicksUntilAdvance"/> and <see cref="TilesPerAdvance"/>
        /// this closes the timing question a bot actually asks — when does
        /// this bolt reach my tile — without reverse-engineering the attack
        /// profile that fired it, which a redacted owner may not even name.
        /// </summary>
        public int TicksPerAdvance { get; }
        /// <summary>
        /// Health one contact with this projectile removes. Published per
        /// projectile because a volley bolt and an ordinary bolt need not
        /// agree, so "should I eat this?" is answerable from the bolt itself.
        /// </summary>
        public int DamagePerHit { get; }
    }

    /// <summary>
    /// Redacted hearing report for an event that was not sight-visible to the
    /// named observer. It exposes kind and coarse sensor bins, never coordinates
    /// or an unobserved source identity.
    /// </summary>
    public sealed record ObservedSound
    {
        /// <summary>Creates a redacted heard-event report.</summary>
        /// <param name="eventHandle">Opaque handle shared with the source event.</param>
        /// <param name="sourceTick">Tick on which the source event occurred.</param>
        /// <param name="sourceOrdinal">Deterministic event ordinal within that tick.</param>
        /// <param name="observerActorId">Allied life that heard this report.</param>
        /// <param name="kind">Redacted source event kind.</param>
        /// <param name="bearing">
        /// Coarse sector index under the observer's vision profile; it is not a
        /// degree value.
        /// </param>
        /// <param name="distance">
        /// Coarse distance-band index under the observer's vision profile; it
        /// is not an exact tile distance.
        /// </param>
        public ObservedSound(
            string eventHandle,
            int sourceTick,
            int sourceOrdinal,
            ActorIdentity observerActorId,
            EventKind kind,
            int bearing,
            int distance)
        {
            if (sourceTick < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceTick));
            if (sourceOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceOrdinal));
            if (bearing < 0)
                throw new ArgumentOutOfRangeException(nameof(bearing));
            if (distance < 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            ArgumentNullException.ThrowIfNull(observerActorId);
            EventHandle = GenericActorDynamicValueRules.Handle(
                eventHandle,
                nameof(eventHandle));
            SourceTick = sourceTick;
            SourceOrdinal = sourceOrdinal;
            ObserverActorId = observerActorId;
            Kind = GenericActorDynamicValueRules.EnumValue(kind, nameof(kind));
            Bearing = bearing;
            Distance = distance;
        }

        /// <summary>Opaque handle shared with the source event.</summary>
        public string EventHandle { get; }
        /// <summary>Tick on which the source event occurred.</summary>
        public int SourceTick { get; }
        /// <summary>Deterministic event ordinal within <see cref="SourceTick"/>.</summary>
        public int SourceOrdinal { get; }
        /// <summary>Exact allied life that heard this report.</summary>
        public ActorIdentity ObserverActorId { get; }
        /// <summary>Redacted source event kind.</summary>
        public EventKind Kind { get; }
        /// <summary>
        /// Coarse bearing-sector index defined by the observer's vision profile.
        /// </summary>
        public int Bearing { get; }
        /// <summary>
        /// Coarse distance-band index defined by the observer's vision profile.
        /// </summary>
        public int Distance { get; }
    }

    /// <summary>
    /// Sight-visible authoritative event with typed payload and exact allied
    /// observation provenance.
    /// </summary>
    public sealed record ObservedEvent
    {
        /// <summary>Creates one visible event.</summary>
        /// <param name="eventHandle">Opaque match-scoped event handle.</param>
        /// <param name="sourceTick">Tick on which the event occurred.</param>
        /// <param name="sourceOrdinal">Deterministic event ordinal within that tick.</param>
        /// <param name="kind">Event discriminator.</param>
        /// <param name="payload">Typed payload matching <paramref name="kind"/>.</param>
        /// <param name="observedBy">Allied lives whose sight revealed the event.</param>
        public ObservedEvent(
            string eventHandle,
            int sourceTick,
            int sourceOrdinal,
            EventKind kind,
            EventPayload payload,
            IEnumerable<ActorIdentity> observedBy)
        {
            if (sourceTick < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceTick));
            if (sourceOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceOrdinal));
            ArgumentNullException.ThrowIfNull(payload);
            kind = GenericActorDynamicValueRules.EnumValue(kind, nameof(kind));
            if (!payload.Supports(kind))
            {
                throw new ArgumentException(
                    "The event payload does not match its event kind.",
                    nameof(payload));
            }

            EventHandle = GenericActorDynamicValueRules.Handle(
                eventHandle,
                nameof(eventHandle));
            SourceTick = sourceTick;
            SourceOrdinal = sourceOrdinal;
            Kind = kind;
            Payload = payload;
            ObservedBy = GenericActorDynamicValueRules.CanonicalActors(
                observedBy,
                nameof(observedBy));
        }

        /// <summary>Opaque match-scoped event handle.</summary>
        public string EventHandle { get; }
        /// <summary>Tick on which the event occurred.</summary>
        public int SourceTick { get; }
        /// <summary>Deterministic event ordinal within <see cref="SourceTick"/>.</summary>
        public int SourceOrdinal { get; }
        /// <summary>Event payload discriminator.</summary>
        public EventKind Kind { get; }
        /// <summary>Typed event data matching <see cref="Kind"/>.</summary>
        public EventPayload Payload { get; }
        /// <summary>Exact allied lives whose sight revealed the event.</summary>
        public ImmutableArray<ActorIdentity> ObservedBy { get; }
    }

    /// <summary>Closed union of typed event payloads.</summary>
    public abstract record EventPayload
    {
        private EventPayload()
        {
        }

        internal abstract bool Supports(EventKind kind);

        /// <summary>An accepted rotation changed one life's facing.</summary>
        public sealed record Rotation : EventPayload
        {
            /// <summary>Creates a rotation event payload.</summary>
            /// <param name="actorId">Life that rotated.</param>
            /// <param name="action">Normalized rotation action.</param>
            /// <param name="position">Actor tile when rotation resolved.</param>
            /// <param name="fromFacing">Facing before resolution.</param>
            /// <param name="toFacing">Facing after resolution.</param>
            public Rotation(
                ActorIdentity actorId,
                GenericActorActionResolution.ResolvedAction action,
                Position position,
                Direction fromFacing,
                Direction toFacing)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                ArgumentNullException.ThrowIfNull(action);
                ValidatePosition(position, nameof(position));
                ActorId = actorId;
                Action = action;
                Position = position;
                FromFacing = GenericActorDynamicValueRules.EnumValue(
                    fromFacing,
                    nameof(fromFacing));
                ToFacing = GenericActorDynamicValueRules.EnumValue(
                    toFacing,
                    nameof(toFacing));
            }

            /// <summary>Life that rotated.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Normalized rotation action.</summary>
            public GenericActorActionResolution.ResolvedAction Action { get; }
            /// <summary>Actor tile when rotation resolved.</summary>
            public Position Position { get; }
            /// <summary>Facing before resolution.</summary>
            public Direction FromFacing { get; }
            /// <summary>Facing after resolution.</summary>
            public Direction ToFacing { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.Rotation;
        }

        /// <summary>An accepted movement changed one life's tile.</summary>
        public sealed record Movement : EventPayload
        {
            /// <summary>Creates a successful movement payload.</summary>
            /// <param name="actorId">Life that moved.</param>
            /// <param name="action">Normalized movement action.</param>
            /// <param name="from">Origin tile.</param>
            /// <param name="to">Resolved destination tile.</param>
            /// <param name="facing">Actor facing retained during movement.</param>
            public Movement(
                ActorIdentity actorId,
                GenericActorActionResolution.ResolvedAction action,
                Position from,
                Position to,
                Direction facing)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                ArgumentNullException.ThrowIfNull(action);
                ValidatePosition(from, nameof(from));
                ValidatePosition(to, nameof(to));
                ActorId = actorId;
                Action = action;
                From = from;
                To = to;
                Facing = GenericActorDynamicValueRules.EnumValue(
                    facing,
                    nameof(facing));
            }

            /// <summary>Life that moved.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Normalized movement action.</summary>
            public GenericActorActionResolution.ResolvedAction Action { get; }
            /// <summary>Origin tile.</summary>
            public Position From { get; }
            /// <summary>Resolved destination tile.</summary>
            public Position To { get; }
            /// <summary>Actor facing retained during movement.</summary>
            public Direction Facing { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.Movement;
        }

        /// <summary>A valid movement attempt was blocked during joint resolution.</summary>
        public sealed record MovementBlocked : EventPayload
        {
            /// <summary>Creates a blocked-movement payload.</summary>
            /// <param name="actorId">Life whose movement was blocked.</param>
            /// <param name="action">Normalized movement action.</param>
            /// <param name="from">Tile the actor retained.</param>
            /// <param name="attemptedTo">Requested destination tile.</param>
            /// <param name="facing">Actor facing retained during the attempt.</param>
            public MovementBlocked(
                ActorIdentity actorId,
                GenericActorActionResolution.ResolvedAction action,
                Position from,
                Position attemptedTo,
                Direction facing)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                ArgumentNullException.ThrowIfNull(action);
                ValidatePosition(from, nameof(from));
                ValidatePosition(attemptedTo, nameof(attemptedTo));
                ActorId = actorId;
                Action = action;
                From = from;
                AttemptedTo = attemptedTo;
                Facing = GenericActorDynamicValueRules.EnumValue(
                    facing,
                    nameof(facing));
            }

            /// <summary>Life whose movement was blocked.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Normalized movement action.</summary>
            public GenericActorActionResolution.ResolvedAction Action { get; }
            /// <summary>Tile the actor retained.</summary>
            public Position From { get; }
            /// <summary>Requested destination tile.</summary>
            public Position AttemptedTo { get; }
            /// <summary>Actor facing retained during the attempt.</summary>
            public Direction Facing { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.MovementBlocked;
        }

        /// <summary>An accepted attack launched or resolved a projectile identity.</summary>
        public sealed record Attack : EventPayload
        {
            /// <summary>Creates an attack payload.</summary>
            /// <param name="actorId">Firing life.</param>
            /// <param name="action">Normalized attack action.</param>
            /// <param name="projectileId">Match-unique projectile identity.</param>
            /// <param name="origin">Projectile launch tile.</param>
            /// <param name="heading">Initial absolute heading sector.</param>
            public Attack(
                ActorIdentity actorId,
                GenericActorActionResolution.ResolvedAction action,
                long projectileId,
                Position origin,
                ProjectileHeading heading)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                ArgumentNullException.ThrowIfNull(action);
                if (projectileId < 0)
                    throw new ArgumentOutOfRangeException(nameof(projectileId));
                ValidatePosition(origin, nameof(origin));
                ActorId = actorId;
                Action = action;
                ProjectileId = projectileId;
                Origin = origin;
                Heading = GenericActorDynamicValueRules.EnumValue(
                    heading,
                    nameof(heading));
            }

            /// <summary>Firing life.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Normalized attack action.</summary>
            public GenericActorActionResolution.ResolvedAction Action { get; }
            /// <summary>Match-unique projectile identity.</summary>
            public long ProjectileId { get; }
            /// <summary>Projectile launch tile.</summary>
            public Position Origin { get; }
            /// <summary>Initial absolute projectile heading sector.</summary>
            public ProjectileHeading Heading { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.Attack;
        }

        /// <summary>A projectile contact applied damage to one body life.</summary>
        public sealed record Damage : EventPayload
        {
            /// <summary>Creates a damage payload.</summary>
            /// <param name="sourceTeamId">Projectile's owning scoring team.</param>
            /// <param name="sourceActorId">
            /// Exact firing life when visible, otherwise <see langword="null"/>.
            /// </param>
            /// <param name="targetActorId">Life that received damage.</param>
            /// <param name="projectileId">Projectile responsible for contact.</param>
            /// <param name="amount">Positive health points applied as damage.</param>
            /// <param name="newHealth">Target health after this damage batch.</param>
            /// <param name="position">Target tile at contact.</param>
            public Damage(
                int sourceTeamId,
                ActorIdentity? sourceActorId,
                ActorIdentity targetActorId,
                long projectileId,
                int amount,
                int newHealth,
                Position position)
            {
                ArgumentNullException.ThrowIfNull(targetActorId);
                if (sourceTeamId < 0)
                    throw new ArgumentOutOfRangeException(nameof(sourceTeamId));
                if (sourceActorId is not null
                    && sourceActorId.TeamId != sourceTeamId)
                {
                    throw new ArgumentException(
                        "A visible source actor must belong to the reported source team.",
                        nameof(sourceActorId));
                }
                if (projectileId < 0)
                    throw new ArgumentOutOfRangeException(nameof(projectileId));
                if (amount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(amount));
                if (newHealth < 0)
                    throw new ArgumentOutOfRangeException(nameof(newHealth));
                ValidatePosition(position, nameof(position));
                SourceTeamId = sourceTeamId;
                SourceActorId = sourceActorId;
                TargetActorId = targetActorId;
                ProjectileId = projectileId;
                Amount = amount;
                NewHealth = newHealth;
                Position = position;
            }

            /// <summary>Projectile's owning scoring team.</summary>
            public int SourceTeamId { get; }
            /// <summary>
            /// Exact firing life when observation policy reveals it; otherwise
            /// <see langword="null"/>.
            /// </summary>
            public ActorIdentity? SourceActorId { get; }
            /// <summary>Life that received damage.</summary>
            public ActorIdentity TargetActorId { get; }
            /// <summary>Projectile responsible for contact.</summary>
            public long ProjectileId { get; }
            /// <summary>Positive health points applied as damage.</summary>
            public int Amount { get; }
            /// <summary>Target health after this damage batch.</summary>
            public int NewHealth { get; }
            /// <summary>Target map tile at contact.</summary>
            public Position Position { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.Damage;
        }

        /// <summary>
        /// A hostile projectile died on the target form's declared projectile
        /// guard instead of damaging it, and the guard returned a new bolt
        /// from its own tile along the exactly reversed heading under its own
        /// team's ownership. Contracts whose forms declare no guard never
        /// produce this event.
        /// </summary>
        public sealed record ProjectileDeflected : EventPayload
        {
            /// <summary>Creates a projectile-deflection payload.</summary>
            /// <param name="sourceTeamId">Consumed bolt's owning scoring team.</param>
            /// <param name="sourceActorId">
            /// Exact firing life when visible, otherwise <see langword="null"/>.
            /// </param>
            /// <param name="targetActorId">Life whose guard turned the bolt.</param>
            /// <param name="projectileId">Incoming projectile consumed without damage.</param>
            /// <param name="deflectedProjectileId">
            /// Returned projectile launched by the guard. It belongs to the
            /// guard's team and flies the reverse of <paramref name="heading"/>
            /// from <paramref name="position"/>.
            /// </param>
            /// <param name="targetFormId">Guarding form at contact.</param>
            /// <param name="targetFacing">Guard facing at contact.</param>
            /// <param name="heading">Incoming travel heading at contact.</param>
            /// <param name="position">Contact tile, and the return's origin.</param>
            public ProjectileDeflected(
                int sourceTeamId,
                ActorIdentity? sourceActorId,
                ActorIdentity targetActorId,
                long projectileId,
                long deflectedProjectileId,
                string targetFormId,
                Direction targetFacing,
                ProjectileHeading heading,
                Position position)
            {
                ArgumentNullException.ThrowIfNull(targetActorId);
                if (sourceTeamId < 0)
                    throw new ArgumentOutOfRangeException(nameof(sourceTeamId));
                if (sourceActorId is not null
                    && sourceActorId.TeamId != sourceTeamId)
                {
                    throw new ArgumentException(
                        "A visible source actor must belong to the reported source team.",
                        nameof(sourceActorId));
                }
                if (projectileId < 0)
                    throw new ArgumentOutOfRangeException(nameof(projectileId));
                if (deflectedProjectileId < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(deflectedProjectileId));
                }
                if (deflectedProjectileId == projectileId)
                {
                    throw new ArgumentException(
                        "A deflection returns a new projectile identity, never the consumed one.",
                        nameof(deflectedProjectileId));
                }
                if (!Enum.IsDefined(targetFacing))
                    throw new ArgumentOutOfRangeException(nameof(targetFacing));
                if (!Enum.IsDefined(heading))
                    throw new ArgumentOutOfRangeException(nameof(heading));
                ValidatePosition(position, nameof(position));
                SourceTeamId = sourceTeamId;
                SourceActorId = sourceActorId;
                TargetActorId = targetActorId;
                ProjectileId = projectileId;
                DeflectedProjectileId = deflectedProjectileId;
                TargetFormId = GenericActorDynamicValueRules.SemanticId(
                    targetFormId,
                    nameof(targetFormId));
                TargetFacing = targetFacing;
                Heading = heading;
                Position = position;
            }

            /// <summary>Consumed bolt's owning scoring team.</summary>
            public int SourceTeamId { get; }
            /// <summary>
            /// Exact firing life when observation policy reveals it; otherwise
            /// <see langword="null"/>.
            /// </summary>
            public ActorIdentity? SourceActorId { get; }
            /// <summary>Life whose guard turned the bolt.</summary>
            public ActorIdentity TargetActorId { get; }
            /// <summary>Incoming projectile consumed without damage.</summary>
            public long ProjectileId { get; }
            /// <summary>
            /// Returned projectile: owned by the deflecting life's team,
            /// launched from <see cref="Position"/> along the reverse of
            /// <see cref="Heading"/>, and lethal to whoever fired the original.
            /// </summary>
            public long DeflectedProjectileId { get; }
            /// <summary>Guarding form at contact.</summary>
            public string TargetFormId { get; }
            /// <summary>Guard facing at contact.</summary>
            public Direction TargetFacing { get; }
            /// <summary>Incoming projectile travel heading at contact.</summary>
            public ProjectileHeading Heading { get; }
            /// <summary>Contact map tile, and the return bolt's origin.</summary>
            public Position Position { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.ProjectileDeflected;
        }

        /// <summary>One body life reached the ruleset's destruction condition.</summary>
        public sealed record Destruction : EventPayload
        {
            /// <summary>Creates a life-destruction payload.</summary>
            /// <param name="actorId">Destroyed body-life identity.</param>
            /// <param name="sourceTeamId">
            /// Attributed projectile team, or <see langword="null"/> for
            /// non-projectile destruction.
            /// </param>
            /// <param name="sourceActorId">
            /// Visible firing life, or <see langword="null"/> when redacted or
            /// no projectile source exists.
            /// </param>
            /// <param name="projectileId">
            /// Attributed projectile, or <see langword="null"/> for
            /// non-projectile destruction.
            /// </param>
            /// <param name="generation">Destroyed life generation.</param>
            /// <param name="formId">Destroyed life form.</param>
            /// <param name="position">Destruction tile.</param>
            public Destruction(
                ActorIdentity actorId,
                int? sourceTeamId,
                ActorIdentity? sourceActorId,
                long? projectileId,
                int generation,
                string formId,
                Position position)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                if (sourceTeamId is < 0)
                    throw new ArgumentOutOfRangeException(nameof(sourceTeamId));
                if (projectileId is < 0)
                    throw new ArgumentOutOfRangeException(nameof(projectileId));
                if (sourceTeamId.HasValue != projectileId.HasValue)
                {
                    throw new ArgumentException(
                        "Destruction source team and projectile identity must both be present or both be absent.");
                }
                if (sourceActorId is not null
                    && sourceActorId.TeamId != sourceTeamId)
                {
                    throw new ArgumentException(
                        "A visible source actor must belong to the reported source team.",
                        nameof(sourceActorId));
                }
                if (generation < 0)
                    throw new ArgumentOutOfRangeException(nameof(generation));
                ValidatePosition(position, nameof(position));
                ActorId = actorId;
                SourceTeamId = sourceTeamId;
                SourceActorId = sourceActorId;
                ProjectileId = projectileId;
                Generation = generation;
                FormId = GenericActorDynamicValueRules.SemanticId(
                    formId,
                    nameof(formId));
                Position = position;
            }

            /// <summary>Destroyed body-life identity.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>
            /// Attributed projectile team, or <see langword="null"/> for
            /// non-projectile destruction.
            /// </summary>
            public int? SourceTeamId { get; }
            /// <summary>Visible firing life, if one is attributable and revealed.</summary>
            public ActorIdentity? SourceActorId { get; }
            /// <summary>Attributed projectile, if destruction was projectile-caused.</summary>
            public long? ProjectileId { get; }
            /// <summary>Destroyed life generation.</summary>
            public int Generation { get; }
            /// <summary>Destroyed life form catalog identifier.</summary>
            public string FormId { get; }
            /// <summary>Destruction map tile.</summary>
            public Position Position { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.Destruction;
        }

        /// <summary>A fresh independently executing body life became active.</summary>
        public sealed record LifeSpawned : EventPayload
        {
            /// <summary>Creates a life-spawned payload with immutable lineage.</summary>
            /// <param name="actorId">New body-life identity.</param>
            /// <param name="participantId">Participant controlling the life.</param>
            /// <param name="parentActorId">
            /// Revealed prior/source life, or <see langword="null"/> for an
            /// initial life or when sensor policy redacts that identity.
            /// </param>
            /// <param name="generation">New life generation.</param>
            /// <param name="formId">Initial form catalog identifier.</param>
            /// <param name="health">Initial positive health.</param>
            /// <param name="position">Initial map tile.</param>
            /// <param name="reason">Creation reason.</param>
            /// <param name="sourceTransitionId">
            /// Static transition catalog ID for transition-created lives.
            /// </param>
            /// <param name="sourceOperationId">
            /// Unique occurrence handle shared by sibling outputs, or
            /// <see langword="null"/> when observation policy redacts the
            /// transition occurrence.
            /// </param>
            public LifeSpawned(
                ActorIdentity actorId,
                int participantId,
                ActorIdentity? parentActorId,
                int generation,
                string formId,
                int health,
                Position position,
                GenericActorMatchStart.SpawnReason reason,
                string? sourceTransitionId,
                string? sourceOperationId)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                if (participantId < 0)
                    throw new ArgumentOutOfRangeException(nameof(participantId));
                if (generation < 0)
                    throw new ArgumentOutOfRangeException(nameof(generation));
                if (health <= 0)
                    throw new ArgumentOutOfRangeException(nameof(health));
                ValidatePosition(position, nameof(position));
                reason = GenericActorDynamicValueRules.EnumValue(
                    reason,
                    nameof(reason));
                ValidateSpawnLineage(
                    reason,
                    generation,
                    parentActorId,
                    sourceTransitionId,
                    sourceOperationId);

                ActorId = actorId;
                ParticipantId = participantId;
                ParentActorId = parentActorId;
                Generation = generation;
                FormId = GenericActorDynamicValueRules.SemanticId(
                    formId,
                    nameof(formId));
                Health = health;
                Position = position;
                Reason = reason;
                SourceTransitionId = sourceTransitionId is null
                    ? null
                    : GenericActorDynamicValueRules.SemanticId(
                        sourceTransitionId,
                        nameof(sourceTransitionId));
                SourceOperationId = sourceOperationId is null
                    ? null
                    : GenericActorDynamicValueRules.Handle(
                        sourceOperationId,
                        nameof(sourceOperationId));
            }

            /// <summary>New body-life identity.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Participant controlling the life.</summary>
            public int ParticipantId { get; }
            /// <summary>
            /// Revealed prior/source life, or <see langword="null"/> when
            /// inapplicable or redacted by observation policy.
            /// </summary>
            public ActorIdentity? ParentActorId { get; }
            /// <summary>New life generation.</summary>
            public int Generation { get; }
            /// <summary>Initial form catalog identifier.</summary>
            public string FormId { get; }
            /// <summary>Initial positive health.</summary>
            public int Health { get; }
            /// <summary>Initial map tile.</summary>
            public Position Position { get; }
            /// <summary>Reason this life was created.</summary>
            public GenericActorMatchStart.SpawnReason Reason { get; }
            /// <summary>
            /// Static transition catalog ID, or <see langword="null"/> when not
            /// transition-created.
            /// </summary>
            public string? SourceTransitionId { get; }
            /// <summary>
            /// Unique operation occurrence handle shared by sibling outputs,
            /// or <see langword="null"/> when not transition-created or
            /// redacted by observation policy.
            /// </summary>
            public string? SourceOperationId { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.LifeSpawned;
        }

        /// <summary>A body life ended without necessarily being destroyed.</summary>
        public sealed record LifeRetired : EventPayload
        {
            /// <summary>Creates a life-retired payload.</summary>
            /// <param name="actorId">Retired body-life identity.</param>
            /// <param name="generation">Retired life generation.</param>
            /// <param name="formId">Form at retirement.</param>
            /// <param name="position">Tile at retirement.</param>
            /// <param name="reason">Stable retirement-reason ID.</param>
            /// <param name="sourceTransitionId">Static transition ID, if applicable.</param>
            /// <param name="sourceOperationId">Operation occurrence handle, if applicable.</param>
            public LifeRetired(
                ActorIdentity actorId,
                int generation,
                string formId,
                Position position,
                string reason,
                string? sourceTransitionId,
                string? sourceOperationId)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                if (generation < 0)
                    throw new ArgumentOutOfRangeException(nameof(generation));
                ValidatePosition(position, nameof(position));
                ValidateOptionalLineage(
                    sourceTransitionId,
                    sourceOperationId);
                ActorId = actorId;
                Generation = generation;
                FormId = GenericActorDynamicValueRules.SemanticId(
                    formId,
                    nameof(formId));
                Position = position;
                Reason = GenericActorDynamicValueRules.SemanticId(
                    reason,
                    nameof(reason));
                SourceTransitionId = sourceTransitionId is null
                    ? null
                    : GenericActorDynamicValueRules.SemanticId(
                        sourceTransitionId,
                        nameof(sourceTransitionId));
                SourceOperationId = sourceOperationId is null
                    ? null
                    : GenericActorDynamicValueRules.Handle(
                        sourceOperationId,
                        nameof(sourceOperationId));
            }

            /// <summary>Retired body-life identity.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>Retired life generation.</summary>
            public int Generation { get; }
            /// <summary>Form at retirement.</summary>
            public string FormId { get; }
            /// <summary>Map tile at retirement.</summary>
            public Position Position { get; }
            /// <summary>Stable retirement-reason identifier.</summary>
            public string Reason { get; }
            /// <summary>Static transition ID, if retirement was transition-driven.</summary>
            public string? SourceTransitionId { get; }
            /// <summary>Unique operation occurrence handle, if transition-driven.</summary>
            public string? SourceOperationId { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.LifeRetired;
        }

        /// <summary>A participant-scoped runtime fault occurred.</summary>
        public sealed record RuntimeFault : EventPayload
        {
            /// <summary>Creates a runtime-fault payload.</summary>
            /// <param name="fault">Stable fault evidence without raw diagnostics.</param>
            public RuntimeFault(GenericActorRuntimeFaultContext fault)
            {
                ArgumentNullException.ThrowIfNull(fault);
                Fault = fault;
            }

            /// <summary>Stable fault evidence without raw diagnostics.</summary>
            public GenericActorRuntimeFaultContext Fault { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.RuntimeFault;
        }

        /// <summary>
        /// A participant-scoped MIND fault with no body to attribute it to.
        /// Under a threshold-0 contract this is the frame on which a mind
        /// forgot the match and lost it.
        /// </summary>
        public sealed record MindRuntimeFault : EventPayload
        {
            /// <summary>Creates a participant-scoped mind-fault payload.</summary>
            /// <param name="participantId">Participant charged with the fault.</param>
            /// <param name="teamId">Its scoring team.</param>
            /// <param name="stage">Runtime lifecycle stage that faulted.</param>
            /// <param name="faultCode">Stable, non-diagnostic fault category.</param>
            /// <param name="cumulativeFaultCount">Count after this fault.</param>
            /// <param name="disqualificationTriggered">
            /// Whether this fault crossed the disqualification threshold.
            /// </param>
            public MindRuntimeFault(
                int participantId,
                int teamId,
                GenericActorRuntimeFaultContext.FaultStage stage,
                string faultCode,
                long cumulativeFaultCount,
                bool disqualificationTriggered)
            {
                if (participantId < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(participantId));
                }
                if (teamId < 0)
                    throw new ArgumentOutOfRangeException(nameof(teamId));
                if (cumulativeFaultCount < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cumulativeFaultCount));
                }
                ParticipantId = participantId;
                TeamId = teamId;
                Stage = GenericActorDynamicValueRules.EnumValue(
                    stage,
                    nameof(stage));
                FaultCode = GenericActorDynamicValueRules.SemanticId(
                    faultCode,
                    nameof(faultCode));
                CumulativeFaultCount = cumulativeFaultCount;
                DisqualificationTriggered = disqualificationTriggered;
            }

            /// <summary>Participant charged with the fault.</summary>
            public int ParticipantId { get; }
            /// <summary>Its scoring team.</summary>
            public int TeamId { get; }
            /// <summary>Runtime lifecycle stage that faulted.</summary>
            public GenericActorRuntimeFaultContext.FaultStage Stage { get; }
            /// <summary>Stable, non-diagnostic fault category.</summary>
            public string FaultCode { get; }
            /// <summary>Participant fault count after applying this fault.</summary>
            public long CumulativeFaultCount { get; }
            /// <summary>Whether this fault triggered disqualification.</summary>
            public bool DisqualificationTriggered { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.MindRuntimeFault;
        }

        /// <summary>A participant was disqualified from the match.</summary>
        public sealed record Participant : EventPayload
        {
            /// <summary>Creates a participant-disqualification payload.</summary>
            /// <param name="participantId">Disqualified participant identifier.</param>
            /// <param name="teamId">Participant's scoring-team identifier.</param>
            public Participant(int participantId, int teamId)
            {
                if (participantId < 0)
                    throw new ArgumentOutOfRangeException(nameof(participantId));
                if (teamId < 0)
                    throw new ArgumentOutOfRangeException(nameof(teamId));
                ParticipantId = participantId;
                TeamId = teamId;
            }

            /// <summary>Disqualified participant identifier.</summary>
            public int ParticipantId { get; }
            /// <summary>Participant's scoring-team identifier.</summary>
            public int TeamId { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.ParticipantDisqualified;
        }

        /// <summary>A fabrication or replication operation changed lifecycle state.</summary>
        public sealed record Lifecycle : EventPayload
        {
            /// <summary>Creates a lifecycle queue, cancellation, or completion payload.</summary>
            /// <param name="transitionId">Static transition catalog identifier.</param>
            /// <param name="operationId">Unique operation occurrence handle.</param>
            /// <param name="sourceActorId">Source body-life identity.</param>
            /// <param name="targetTeamId">Reserved output team, if a slot is targeted.</param>
            /// <param name="targetUnitId">Reserved output unit, if a slot is targeted.</param>
            /// <param name="dueTick">Scheduled completion tick, when applicable.</param>
            /// <param name="cancellationReason">
            /// Stable reason ID for cancellation events; otherwise
            /// <see langword="null"/>.
            /// </param>
            public Lifecycle(
                string transitionId,
                string operationId,
                ActorIdentity sourceActorId,
                int? targetTeamId,
                int? targetUnitId,
                int? dueTick,
                string? cancellationReason)
            {
                ArgumentNullException.ThrowIfNull(sourceActorId);
                if (targetTeamId is < 0)
                    throw new ArgumentOutOfRangeException(nameof(targetTeamId));
                if (targetUnitId is < 0)
                    throw new ArgumentOutOfRangeException(nameof(targetUnitId));
                if (targetTeamId.HasValue != targetUnitId.HasValue)
                {
                    throw new ArgumentException(
                        "Lifecycle targets require both team and unit IDs.");
                }
                if (dueTick is < 0)
                    throw new ArgumentOutOfRangeException(nameof(dueTick));
                TransitionId = GenericActorDynamicValueRules.SemanticId(
                    transitionId,
                    nameof(transitionId));
                OperationId = GenericActorDynamicValueRules.Handle(
                    operationId,
                    nameof(operationId));
                SourceActorId = sourceActorId;
                TargetTeamId = targetTeamId;
                TargetUnitId = targetUnitId;
                DueTick = dueTick;
                CancellationReason = cancellationReason is null
                    ? null
                    : GenericActorDynamicValueRules.SemanticId(
                        cancellationReason,
                        nameof(cancellationReason));
            }

            /// <summary>Static transition catalog identifier.</summary>
            public string TransitionId { get; }
            /// <summary>Unique operation occurrence handle.</summary>
            public string OperationId { get; }
            /// <summary>Source body-life identity.</summary>
            public ActorIdentity SourceActorId { get; }
            /// <summary>Reserved output team, if this event targets a slot.</summary>
            public int? TargetTeamId { get; }
            /// <summary>Reserved output unit, if this event targets a slot.</summary>
            public int? TargetUnitId { get; }
            /// <summary>Scheduled completion tick, when applicable.</summary>
            public int? DueTick { get; }
            /// <summary>Stable cancellation reason on cancellation events only.</summary>
            public string? CancellationReason { get; }

            internal override bool Supports(EventKind kind) =>
                kind switch
                {
                    EventKind.LifecycleCancelled =>
                        CancellationReason is not null,
                    EventKind.LifecycleQueued
                        or EventKind.LifecycleCompleted =>
                        CancellationReason is null,
                    _ => false,
                };
        }

        /// <summary>A same-life form transition started, completed, or cancelled.</summary>
        public sealed record FormTransition : EventPayload
        {
            /// <summary>Creates a form-transition chronology payload.</summary>
            /// <param name="actorId">Life retaining identity through the transition.</param>
            /// <param name="transitionId">Static transition catalog identifier.</param>
            /// <param name="operationId">Unique operation occurrence handle.</param>
            /// <param name="fromFormId">Source form.</param>
            /// <param name="toFormId">Target form.</param>
            /// <param name="startedTick">Tick on which the operation was accepted.</param>
            /// <param name="dueTick">Scheduled completion tick.</param>
            /// <param name="automatic">
            /// True when the engine started this route with no action because
            /// the source form's declared counter reached its threshold — the
            /// stance's own budget spending itself. False when an action
            /// requested it, which is every transition on a contract that
            /// declares no automatic return.
            /// </param>
            public FormTransition(
                ActorIdentity actorId,
                string transitionId,
                string operationId,
                string fromFormId,
                string toFormId,
                int startedTick,
                int dueTick,
                bool automatic = false)
            {
                ArgumentNullException.ThrowIfNull(actorId);
                if (startedTick < 0)
                    throw new ArgumentOutOfRangeException(nameof(startedTick));
                // End-of-started-tick transitions with a one-tick declared
                // duration legitimately emit started/completed events whose
                // canonical due tick equals the started tick. Pending
                // transition state remains strictly future-due.
                if (dueTick < startedTick)
                    throw new ArgumentOutOfRangeException(nameof(dueTick));
                ActorId = actorId;
                TransitionId = GenericActorDynamicValueRules.SemanticId(
                    transitionId,
                    nameof(transitionId));
                OperationId = GenericActorDynamicValueRules.Handle(
                    operationId,
                    nameof(operationId));
                FromFormId = GenericActorDynamicValueRules.SemanticId(
                    fromFormId,
                    nameof(fromFormId));
                ToFormId = GenericActorDynamicValueRules.SemanticId(
                    toFormId,
                    nameof(toFormId));
                StartedTick = startedTick;
                DueTick = dueTick;
                Automatic = automatic;
            }

            /// <summary>Life retaining identity through the transition.</summary>
            public ActorIdentity ActorId { get; }
            /// <summary>
            /// Whether the engine started this route on its own because the
            /// source form's automatic-return threshold was reached.
            /// </summary>
            public bool Automatic { get; }
            /// <summary>Static transition catalog identifier.</summary>
            public string TransitionId { get; }
            /// <summary>Unique operation occurrence handle.</summary>
            public string OperationId { get; }
            /// <summary>Source form catalog identifier.</summary>
            public string FromFormId { get; }
            /// <summary>Target form catalog identifier.</summary>
            public string ToFormId { get; }
            /// <summary>Tick on which the operation was accepted.</summary>
            public int StartedTick { get; }
            /// <summary>Scheduled completion tick.</summary>
            public int DueTick { get; }

            internal override bool Supports(EventKind kind) =>
                kind is EventKind.FormTransitionStarted
                    or EventKind.FormTransitionCompleted
                    or EventKind.FormTransitionCancelled;
        }

        /// <summary>One authoritative team score channel changed value.</summary>
        public sealed record ScoreChanged : EventPayload
        {
            /// <summary>Creates a score-changed payload.</summary>
            /// <param name="teamId">Scoring-team identifier.</param>
            /// <param name="channel">Stable score-channel identifier.</param>
            /// <param name="newValue">Authoritative signed 64-bit value after change.</param>
            public ScoreChanged(int teamId, string channel, long newValue)
            {
                if (teamId < 0)
                    throw new ArgumentOutOfRangeException(nameof(teamId));
                TeamId = teamId;
                Channel = GenericActorDynamicValueRules.SemanticId(
                    channel,
                    nameof(channel));
                NewValue = newValue;
            }

            /// <summary>Scoring-team identifier.</summary>
            public int TeamId { get; }
            /// <summary>Stable score-channel identifier.</summary>
            public string Channel { get; }
            /// <summary>Authoritative signed 64-bit value after change.</summary>
            public long NewValue { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.ScoreChanged;
        }

        /// <summary>The selected game mode's public objective state changed.</summary>
        public sealed record ModeChanged : EventPayload
        {
            /// <summary>Creates a mode-state change payload.</summary>
            /// <param name="state">Complete public mode state after the change.</param>
            public ModeChanged(ModeObservationState state)
            {
                ArgumentNullException.ThrowIfNull(state);
                State = state;
            }

            /// <summary>Complete public mode state after the change.</summary>
            public ModeObservationState State { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.ModeChanged;
        }

        /// <summary>
        /// A participant disqualification cancelled a pending stable-slot
        /// availability or automatic-return clock.
        /// </summary>
        public sealed record LifecycleClockCancelled : EventPayload
        {
            /// <summary>Creates an exact lifecycle-clock cancellation fact.</summary>
            /// <param name="targetTeamId">Owning scoring team of the target slot.</param>
            /// <param name="targetUnitId">Stable target unit identifier.</param>
            /// <param name="cancelledState">
            /// Exact pending clock state immediately before cancellation.
            /// </param>
            /// <param name="cancellationReason">Stable cancellation reason ID.</param>
            public LifecycleClockCancelled(
                int targetTeamId,
                int targetUnitId,
                UnitSlotState cancelledState,
                string cancellationReason)
            {
                if (targetTeamId < 0)
                    throw new ArgumentOutOfRangeException(nameof(targetTeamId));
                if (targetUnitId < 0)
                    throw new ArgumentOutOfRangeException(nameof(targetUnitId));
                ArgumentNullException.ThrowIfNull(cancelledState);
                if (cancelledState is not UnitSlotState.AvailabilityPending
                    and not UnitSlotState.AutomaticReturnPending)
                {
                    throw new ArgumentException(
                        "A lifecycle clock cancellation must snapshot an availability or automatic-return clock.",
                        nameof(cancelledState));
                }

                TargetTeamId = targetTeamId;
                TargetUnitId = targetUnitId;
                CancelledState = cancelledState;
                CancellationReason =
                    GenericActorDynamicValueRules.SemanticId(
                        cancellationReason,
                        nameof(cancellationReason));
            }

            /// <summary>Owning scoring team of the target slot.</summary>
            public int TargetTeamId { get; }
            /// <summary>Stable target unit identifier.</summary>
            public int TargetUnitId { get; }
            /// <summary>Pending state immediately before cancellation.</summary>
            public UnitSlotState CancelledState { get; }
            /// <summary>Stable cancellation reason ID.</summary>
            public string CancellationReason { get; }

            internal override bool Supports(EventKind kind) =>
                kind == EventKind.LifecycleClockCancelled;
        }

        /// <summary>One authoritative Arc Relay objective/signature fact.</summary>
        public sealed record ArcRelay : EventPayload
        {
            /// <summary>Creates an Arc Relay fact payload.</summary>
            public ArcRelay(ArcRelayEvent fact)
            {
                ArgumentNullException.ThrowIfNull(fact);
                Fact = fact;
            }

            /// <summary>The typed Arc Relay fact.</summary>
            public ArcRelayEvent Fact { get; }
            internal override bool Supports(EventKind kind) =>
                kind == EventKind.ArcRelay;
        }
    }

    /// <summary>Discriminator for visible events and redacted heard-event kinds.</summary>
    public enum EventKind
    {
        /// <summary>A life changed facing.</summary>
        Rotation = 0,
        /// <summary>A life changed tile.</summary>
        Movement = 1,
        /// <summary>A valid movement attempt was blocked.</summary>
        MovementBlocked = 2,
        /// <summary>A life fired an attack.</summary>
        Attack = 3,
        /// <summary>Projectile contact applied damage.</summary>
        Damage = 4,
        /// <summary>A body life was destroyed.</summary>
        Destruction = 5,
        /// <summary>A fresh body life became active.</summary>
        LifeSpawned = 6,
        /// <summary>A body life retired without a destruction event.</summary>
        LifeRetired = 7,
        /// <summary>A participant runtime fault occurred.</summary>
        RuntimeFault = 8,
        /// <summary>A participant was disqualified.</summary>
        ParticipantDisqualified = 9,
        /// <summary>A fabrication or replication operation was queued.</summary>
        LifecycleQueued = 10,
        /// <summary>A fabrication or replication operation was cancelled.</summary>
        LifecycleCancelled = 11,
        /// <summary>A fabrication or replication operation completed.</summary>
        LifecycleCompleted = 12,
        /// <summary>A same-life form transition entered windup.</summary>
        FormTransitionStarted = 13,
        /// <summary>A same-life form transition completed.</summary>
        FormTransitionCompleted = 14,
        /// <summary>A same-life form transition was cancelled.</summary>
        FormTransitionCancelled = 15,
        /// <summary>An authoritative score channel changed.</summary>
        ScoreChanged = 16,
        /// <summary>Mode-specific public objective state changed.</summary>
        ModeChanged = 17,
        /// <summary>A pending stable-slot lifecycle clock was cancelled.</summary>
        LifecycleClockCancelled = 18,
        /// <summary>
        /// A form's projectile guard killed a hostile bolt on its arc and
        /// returned a team-flipped bolt along the reversed heading. Inert on
        /// every contract whose forms declare no guard.
        /// </summary>
        ProjectileDeflected = 19,

        /// <summary>
        /// A PARTICIPANT-SCOPED runtime fault with no body to attribute it
        /// to. Only the mind profile can produce one — a mind ticks even on a
        /// tick it owns nothing, so it can also trap on one — and no per-life
        /// contract ever emits it.
        /// </summary>
        MindRuntimeFault = 20,
        /// <summary>An Arc Relay Core, Well, Pulse, or signature fact.</summary>
        ArcRelay = 21,
    }

    /// <summary>Authoritative score channels for every public scoring team.</summary>
    public sealed record ScoreboardState
    {
        /// <summary>Creates a canonical scoreboard.</summary>
        /// <param name="teams">One entry for every public scoring team.</param>
        public ScoreboardState(IEnumerable<TeamScoreState> teams)
        {
            Teams = Canonicalize(
                teams,
                nameof(teams),
                team => team.TeamId);
            if (Teams.IsEmpty)
            {
                throw new ArgumentException(
                    "A scoreboard must contain at least one team.",
                    nameof(teams));
            }
        }

        /// <summary>Canonical team scores ordered by stable team ID.</summary>
        public ImmutableArray<TeamScoreState> Teams { get; }
    }

    /// <summary>Ranking eligibility and complete score channels for one team.</summary>
    public sealed record TeamScoreState
    {
        /// <summary>Creates one team's authoritative score state.</summary>
        /// <param name="teamId">Stable scoring-team identifier.</param>
        /// <param name="eligible">Whether the team remains eligible for ranking.</param>
        /// <param name="scores">
        /// One value for each channel in the mode's frozen score catalog.
        /// </param>
        public TeamScoreState(
            int teamId,
            bool eligible,
            IEnumerable<ScoreValue> scores)
        {
            if (teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            TeamId = teamId;
            Eligible = eligible;
            Scores = Canonicalize(
                scores,
                nameof(scores),
                score => score.Channel,
                StringComparer.Ordinal);
            if (Scores.IsEmpty)
            {
                throw new ArgumentException(
                    "A team score must contain at least one channel.",
                    nameof(scores));
            }
        }

        /// <summary>Stable scoring-team identifier.</summary>
        public int TeamId { get; }
        /// <summary>Whether the team remains eligible for final ranking.</summary>
        public bool Eligible { get; }
        /// <summary>Canonical score values ordered by stable channel ID.</summary>
        public ImmutableArray<ScoreValue> Scores { get; }
    }

    /// <summary>One signed 64-bit authoritative score-channel value.</summary>
    public sealed record ScoreValue
    {
        /// <summary>Creates a score-channel value.</summary>
        /// <param name="channel">Stable channel identifier from the mode catalog.</param>
        /// <param name="value">Authoritative signed 64-bit value.</param>
        public ScoreValue(string channel, long value)
        {
            Channel = GenericActorDynamicValueRules.SemanticId(
                channel,
                nameof(channel));
            Value = value;
        }

        /// <summary>Stable channel identifier from the mode catalog.</summary>
        public string Channel { get; }
        /// <summary>Authoritative signed 64-bit value.</summary>
        public long Value { get; }
    }

    /// <summary>Closed union of mode-specific public objective states.</summary>
    public abstract record ModeObservationState
    {
        private ModeObservationState(string modeId)
        {
            ModeId = GenericActorDynamicValueRules.SemanticId(
                modeId,
                nameof(modeId));
        }

        /// <summary>Stable mode identifier matching the frozen rules contract.</summary>
        public string ModeId { get; }
        /// <summary>Mode-state discriminator.</summary>
        public abstract GenericActorRulesContract.GameModeKind Kind { get; }

        /// <summary>
        /// Deathmatch has no additional objective state; scores carry progress.
        /// </summary>
        public sealed record Deathmatch : ModeObservationState
        {
            /// <summary>Creates public Deathmatch mode state.</summary>
            /// <param name="modeId">Stable mode identifier.</param>
            public Deathmatch(string modeId)
                : base(modeId)
            {
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.GameModeKind Kind =>
                GenericActorRulesContract.GameModeKind.Deathmatch;
        }

        /// <summary>Current ordered-objective control state for Frontline.</summary>
        public sealed record Frontline : ModeObservationState
        {
            /// <summary>Creates public Frontline objective state.</summary>
            /// <param name="modeId">Stable mode identifier.</param>
            /// <param name="activePositionIndex">Current ordered objective index.</param>
            /// <param name="claimingTeamId">
            /// Team currently accumulating progress, or <see langword="null"/>
            /// when no team has an active claim.
            /// </param>
            /// <param name="captureProgress">Current non-negative capture progress.</param>
            /// <param name="decayTicksElapsed">Ticks elapsed on the decay clock.</param>
            /// <param name="controlResumesAtTick">
            /// Earliest tick on which objective control may resume.
            /// </param>
            /// <param name="holdOwnerTeamId">
            /// Team whose advance a live territory-ratchet hold protects, or
            /// <see langword="null"/> when no hold is live.
            /// </param>
            /// <param name="holdEndsAtTick">
            /// First tick on which that hold no longer denies regression, or
            /// <see langword="null"/> when no hold is live.
            /// </param>
            /// <param name="secondaryOwnerTeamId">
            /// Team that owns the declared side objective, or
            /// <see langword="null"/> when it is neutral — including every
            /// ruleset that declares none.
            /// </param>
            /// <param name="secondaryClaimProgress">
            /// Signed sole-presence ticks accumulated toward the next side
            /// objective capture: positive for team 0, negative for team 1,
            /// zero when no claim stands.
            /// </param>
            /// <param name="scrapTeams">
            /// Both teams' bank and tier vector, ordered by team ID, or empty
            /// on every ruleset without a declared economy.
            /// </param>
            /// <param name="scrapPiles">
            /// Live piles of loose scrap ordered by (y, x), or empty.
            /// </param>
            public Frontline(
                string modeId,
                int activePositionIndex,
                int? claimingTeamId,
                int captureProgress,
                int decayTicksElapsed,
                int controlResumesAtTick,
                int? holdOwnerTeamId = null,
                int? holdEndsAtTick = null,
                int? secondaryOwnerTeamId = null,
                int secondaryClaimProgress = 0,
                ImmutableArray<ScrapTeamState> scrapTeams = default,
                ImmutableArray<ScrapPile> scrapPiles = default)
                : base(modeId)
            {
                if (activePositionIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(activePositionIndex));
                }
                if (claimingTeamId is < 0)
                    throw new ArgumentOutOfRangeException(nameof(claimingTeamId));
                if (captureProgress < 0)
                    throw new ArgumentOutOfRangeException(nameof(captureProgress));
                if (decayTicksElapsed < 0)
                    throw new ArgumentOutOfRangeException(nameof(decayTicksElapsed));
                if (controlResumesAtTick < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(controlResumesAtTick));
                }
                if (holdOwnerTeamId is < 0)
                    throw new ArgumentOutOfRangeException(nameof(holdOwnerTeamId));
                if (holdEndsAtTick is < 0)
                    throw new ArgumentOutOfRangeException(nameof(holdEndsAtTick));
                if ((holdOwnerTeamId is null) != (holdEndsAtTick is null))
                {
                    throw new ArgumentException(
                        "A territory-ratchet hold publishes its owner and its "
                        + "expiry together or not at all.",
                        nameof(holdOwnerTeamId));
                }
                if (secondaryOwnerTeamId is < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(secondaryOwnerTeamId));
                }
                ActivePositionIndex = activePositionIndex;
                ClaimingTeamId = claimingTeamId;
                CaptureProgress = captureProgress;
                DecayTicksElapsed = decayTicksElapsed;
                ControlResumesAtTick = controlResumesAtTick;
                HoldOwnerTeamId = holdOwnerTeamId;
                HoldEndsAtTick = holdEndsAtTick;
                SecondaryOwnerTeamId = secondaryOwnerTeamId;
                SecondaryClaimProgress = secondaryClaimProgress;
                ScrapTeams = scrapTeams.IsDefault ? [] : scrapTeams;
                ScrapPiles = scrapPiles.IsDefault ? [] : scrapPiles;
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.GameModeKind Kind =>
                GenericActorRulesContract.GameModeKind.Frontline;
            /// <summary>Current ordered objective index.</summary>
            public int ActivePositionIndex { get; }
            /// <summary>Team actively accumulating progress, if any.</summary>
            public int? ClaimingTeamId { get; }
            /// <summary>Current capture progress under the declared arithmetic.</summary>
            public int CaptureProgress { get; }
            /// <summary>Ticks elapsed on the declared decay clock.</summary>
            public int DecayTicksElapsed { get; }
            /// <summary>Earliest authoritative tick on which control may resume.</summary>
            public int ControlResumesAtTick { get; }
            /// <summary>
            /// The team whose completed advance the territory ratchet is
            /// currently protecting, or <see langword="null"/> when no hold is
            /// live — which includes every ruleset whose redeploy policy has
            /// no ratchet. Read it instead of inferring ownership from front
            /// displacement: that inference is wrong after the first
            /// regression and impossible for a life born inside the hold.
            /// </summary>
            public int? HoldOwnerTeamId { get; }
            /// <summary>
            /// The first tick on which the live hold stops denying enemy
            /// regression, or <see langword="null"/> when no hold is live.
            /// Same grammar as <see cref="ControlResumesAtTick"/>, so the hold
            /// binds while the observed tick is strictly below it.
            /// </summary>
            public int? HoldEndsAtTick { get; }
            /// <summary>
            /// The team that owns the declared side objective, or
            /// <see langword="null"/> while it is neutral — which includes
            /// every ruleset that declares no side objective at all. Read the
            /// site's regions, its latch threshold, and what owning it does
            /// from
            /// <c>GenericActorRulesContract.FrontlineGameMode.SecondaryControl</c>;
            /// this field is only who holds it right now. It is the read that
            /// says "they are one body light at the front".
            /// </summary>
            public int? SecondaryOwnerTeamId { get; }
            /// <summary>
            /// The running claim on the side objective as signed
            /// sole-presence ticks: positive counts for team 0, negative for
            /// team 1, and zero means no claim stands. Compare its magnitude
            /// against the contract's declared
            /// <c>CaptureThresholdTicks</c> to price a contest; any empty or
            /// contested tick puts it straight back to zero, so walking in
            /// once is a real denial rather than a pause.
            /// </summary>
            public int SecondaryClaimProgress { get; }

            /// <summary>
            /// Both teams' complete economic position — liquid bank and the
            /// tier held on each declared track — ordered by team ID and
            /// public to everybody. Read what a tier DOES, what the next one
            /// costs, and how deep a track goes from
            /// <c>GenericActorRulesContract.FrontlineGameMode.ScrapEconomy</c>;
            /// this field is only where both teams stand right now.
            /// <para>Every purchase is public on the tick it happens: a tier
            /// change moves this state, and a changed mode state rides the
            /// ordinary mode-changed event, so the enemy's bank dropping and
            /// its tier rising arrive together with no inference and no
            /// visibility requirement. Empty on every ruleset that declares no
            /// economy.</para>
            /// </summary>
            public ImmutableArray<ScrapTeamState> ScrapTeams { get; }

            /// <summary>
            /// Every live pile of loose scrap, ordered by (y, x). The deposit
            /// schedule is static contract data, but WHETHER a deposit is
            /// still standing is not — and neither is where a body you never
            /// saw died. Empty on every ruleset that declares no economy.
            /// </summary>
            public ImmutableArray<ScrapPile> ScrapPiles { get; }
        }

        /// <summary>Current Arc Relay public and visibility-filtered state.</summary>
        public sealed record ArcRelay : ModeObservationState
        {
            /// <summary>Creates one Arc Relay mode observation.</summary>
            public ArcRelay(
                string modeId,
                ImmutableArray<ArcRelayWellState> wells,
                ImmutableArray<ArcRelayReactorState> reactors,
                ImmutableArray<ArcRelayCoreState> visibleCores,
                ImmutableArray<ArcRelaySignatureState> visibleSignatures,
                int? latestPulseTeamId,
                int? latestPulseTick)
                : base(modeId)
            {
                Wells = wells.IsDefault ? [] : wells;
                Reactors = reactors.IsDefault ? [] : reactors;
                VisibleCores = visibleCores.IsDefault ? [] : visibleCores;
                VisibleSignatures = visibleSignatures.IsDefault
                    ? []
                    : visibleSignatures;
                LatestPulseTeamId = latestPulseTeamId;
                LatestPulseTick = latestPulseTick;
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.GameModeKind Kind =>
                GenericActorRulesContract.GameModeKind.ArcRelay;
            /// <summary>All public Well clocks and capacity facts.</summary>
            public ImmutableArray<ArcRelayWellState> Wells { get; }
            /// <summary>Both public reactor states.</summary>
            public ImmutableArray<ArcRelayReactorState> Reactors { get; }
            /// <summary>Cores visible to this team.</summary>
            public ImmutableArray<ArcRelayCoreState> VisibleCores { get; }
            /// <summary>Signature state visible to this team.</summary>
            public ImmutableArray<ArcRelaySignatureState> VisibleSignatures
            { get; }
            /// <summary>Team owning the most recent Pulse, if any.</summary>
            public int? LatestPulseTeamId { get; }
            /// <summary>Tick of the most recent Pulse, if any.</summary>
            public int? LatestPulseTick { get; }

            /// <summary>
            /// Every declared strike currently winding up, visible to BOTH
            /// teams (DECISIONS #212): the announcement is the mechanic. The
            /// tiles are frozen at declaration; a body still standing on them
            /// at <see cref="ArcRelayPendingStrike.ResolveAtTick"/> eats the
            /// hit. Empty on every ruleset without strike windups.
            /// </summary>
            public ImmutableArray<ArcRelayPendingStrike> PendingStrikes
            { get; init; } = [];
        }
    }

    /// <summary>
    /// One publicly declared strike in windup (DECISIONS #212). Tiles are
    /// the union of the strike's bolt paths in trace order.
    /// </summary>
    public sealed record ArcRelayPendingStrike(
        ActorIdentity Shooter,
        int ResolveAtTick,
        Position Origin,
        ProjectileHeading CentralHeading,
        ImmutableArray<Position> Tiles)
    {
        public bool Equals(ArcRelayPendingStrike? other) =>
            other is not null
            && Shooter == other.Shooter
            && ResolveAtTick == other.ResolveAtTick
            && Origin == other.Origin
            && CentralHeading == other.CentralHeading
            && Tiles.SequenceEqual(other.Tiles);

        public override int GetHashCode() => HashCode.Combine(
            Shooter,
            ResolveAtTick,
            Origin,
            CentralHeading,
            Tiles.Length);
    }

    /// <summary>Stable source-local Core identity.</summary>
    public sealed record ArcRelayCoreId(string SourceWellId, int SourceOrdinal);

    /// <summary>One public Well state.</summary>
    public sealed record ArcRelayWellState(
        string WellId,
        Position Position,
        int? NextScheduledBirthTick,
        ArcRelayCoreId? OutstandingCoreId,
        bool PendingCharge,
        int? RearmCompletesAtTick);

    /// <summary>One public reactor state.</summary>
    public sealed record ArcRelayReactorState(
        int TeamId,
        Position Position,
        int ChargePips,
        int IntegritySegments)
    {
        /// <summary>
        /// Filled Threefold socket well ids, in the contract's canonical
        /// well order. Empty outside threefold rulesets; under threefold,
        /// <see cref="ChargePips"/> equals this count.
        /// </summary>
        public ImmutableArray<string> FilledSocketWellIds { get; init; } = [];

        public bool Equals(ArcRelayReactorState? other) =>
            other is not null
            && TeamId == other.TeamId
            && Position == other.Position
            && ChargePips == other.ChargePips
            && IntegritySegments == other.IntegritySegments
            && FilledSocketWellIds.SequenceEqual(
                other.FilledSocketWellIds, StringComparer.Ordinal);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(TeamId);
            hash.Add(Position);
            hash.Add(ChargePips);
            hash.Add(IntegritySegments);
            foreach (string wellId in FilledSocketWellIds)
                hash.Add(wellId, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    /// <summary>One currently visible Core.</summary>
    public sealed record ArcRelayCoreState(
        ArcRelayCoreId CoreId,
        Position Position,
        ArcRelayCoreDisposition Disposition,
        ActorIdentity? CarrierActorId,
        int NextRelocationTick,
        Position? FlightTarget,
        int? FlightCompletesAtTick)
    {
        /// <summary>
        /// Charge this Core banks for (1 historically; base 2 under the
        /// charge-value primitive, up to the ripening cap).
        /// </summary>
        public int ChargeValue { get; init; } = 1;
    }

    /// <summary>One visible signature tell/effect/construct.</summary>
    public sealed record ArcRelaySignatureState(
        string OperationId,
        string SignatureId,
        string Kind,
        ActorIdentity OwnerActorId,
        int OwnerTeamId,
        ArcRelaySignaturePhase Phase,
        int StartedTick,
        int? CompletesAtTick,
        int? EndsAtTick,
        ImmutableArray<Position> Positions,
        ActorIdentity? TargetActorId,
        int RemainingCapacity,
        bool Suppressed);

    /// <summary>Current physical disposition of a visible Core.</summary>
    public enum ArcRelayCoreDisposition
    {
        /// <summary>Neutral on a floor tile.</summary>
        Loose = 0,
        /// <summary>Owned by a live body.</summary>
        Carried = 1,
        /// <summary>Travelling through an Arc Toss.</summary>
        InFlight = 2,
    }

    /// <summary>Presentation/resolution phase of a signature occurrence.</summary>
    public enum ArcRelaySignaturePhase
    {
        /// <summary>Public counterplay tell.</summary>
        Tell = 0,
        /// <summary>Active field or construct.</summary>
        Active = 1,
        /// <summary>Maintained channel.</summary>
        Channel = 2,
        /// <summary>Visible travel.</summary>
        InFlight = 3,
    }

    /// <summary>Closed SDK mirror of Arc Relay authoritative facts.</summary>
    public abstract record ArcRelayEvent
    {
        private ArcRelayEvent() { }
        /// <summary>A scheduled or rearmed Core appeared.</summary>
        public sealed record CoreBorn(
            ArcRelayCoreId CoreId,
            Position Position) : ArcRelayEvent
        {
            /// <summary>Charge at birth; 1 outside charge-value rulesets.</summary>
            public int ChargeValue { get; init; } = 1;
        }
        /// <summary>A loose Core's charge value grew by one.</summary>
        public sealed record CoreRipened(
            ArcRelayCoreId CoreId,
            Position Position,
            int Value) : ArcRelayEvent;
        /// <summary>A body's kill earned it a veterancy level.</summary>
        public sealed record LeveledUp(
            ActorIdentity ActorId,
            int Level,
            Position Position) : ArcRelayEvent;
        /// <summary>A body channeled a point of health on a heal tile.</summary>
        public sealed record ZoneHealed(
            ActorIdentity ActorId,
            int Amount,
            int NewHealth,
            Position Position) : ArcRelayEvent;
        /// <summary>A body acquired a loose Core.</summary>
        public sealed record CorePickedUp(
            ArcRelayCoreId CoreId,
            ActorIdentity CarrierActorId,
            Position Position,
            int NextRelocationTick) : ArcRelayEvent;
        /// <summary>A carried/displaced/tossed Core changed tiles.</summary>
        public sealed record CoreRelocated(
            ArcRelayCoreId CoreId,
            ActorIdentity? CarrierActorId,
            Position From,
            Position To,
            int NextRelocationTick,
            string Kind) : ArcRelayEvent;
        /// <summary>An adjacent committed handoff succeeded.</summary>
        public sealed record CoreHandedOff(
            ArcRelayCoreId CoreId,
            ActorIdentity SourceActorId,
            ActorIdentity TargetActorId,
            Position Position,
            int NextRelocationTick) : ArcRelayEvent;
        /// <summary>A Core became neutral.</summary>
        public sealed record CoreDropped(
            ArcRelayCoreId CoreId,
            ActorIdentity SourceActorId,
            Position Position,
            int NextRelocationTick,
            string Kind) : ArcRelayEvent;
        /// <summary>A surviving carrier delivered a Core.</summary>
        public sealed record CoreBanked(
            ArcRelayCoreId CoreId,
            ActorIdentity CarrierActorId,
            int TeamId,
            Position Position,
            int ChargePips) : ArcRelayEvent;
        /// <summary>A Well's capacity/rearm state changed.</summary>
        public sealed record WellChanged(
            string WellId,
            bool PendingCharge,
            int? RearmCompletesAtTick,
            ArcRelayCoreId? OutstandingCoreId) : ArcRelayEvent;
        /// <summary>A delivery completed a Pulse.</summary>
        public sealed record Pulse(
            int TeamId,
            int PulseOrdinal,
            int OpposingReactorIntegrity) : ArcRelayEvent;
        /// <summary>A signature occurrence changed phase or ended.</summary>
        public sealed record SignatureChanged(
            string OperationId,
            string SignatureId,
            ActorIdentity OwnerActorId,
            ArcRelaySignaturePhase? Phase,
            string Reason) : ArcRelayEvent;
        /// <summary>A signature moved a body between exact tiles.</summary>
        public sealed record BodyRelocated(
            string OperationId,
            string SignatureId,
            ActorIdentity OwnerActorId,
            ActorIdentity TargetActorId,
            Position From,
            Position To) : ArcRelayEvent;
        /// <summary>A signature applied direct hull damage.</summary>
        public sealed record SignatureDamage(
            string OperationId,
            string SignatureId,
            ActorIdentity OwnerActorId,
            ActorIdentity TargetActorId,
            int Amount,
            int NewHealth,
            Position Position) : ArcRelayEvent;
        /// <summary>A signature restored hull.</summary>
        public sealed record SignatureRepair(
            string OperationId,
            string SignatureId,
            ActorIdentity OwnerActorId,
            ActorIdentity TargetActorId,
            int Amount,
            int NewHealth,
            Position Position) : ArcRelayEvent;
    }

    /// <summary>One team's published economic position.</summary>
    public sealed record ScrapTeamState
    {
        /// <summary>Creates one team's economic position.</summary>
        /// <param name="teamId">The scoring team.</param>
        /// <param name="bank">Unspent scrap.</param>
        /// <param name="tierLevels">
        /// Tier held on each track, positionally against the contract's
        /// declared track order.
        /// </param>
        public ScrapTeamState(
            int teamId,
            int bank,
            ImmutableArray<int> tierLevels)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(teamId);
            ArgumentOutOfRangeException.ThrowIfNegative(bank);
            ImmutableArray<int> tiers =
                tierLevels.IsDefault ? [] : tierLevels;
            if (tiers.Any(tier => tier < 0))
            {
                throw new ArgumentOutOfRangeException(nameof(tierLevels));
            }
            TeamId = teamId;
            Bank = bank;
            TierLevels = tiers;
        }

        /// <summary>The scoring team.</summary>
        public int TeamId { get; }
        /// <summary>Unspent scrap. Both teams' banks are public.</summary>
        public int Bank { get; }
        /// <summary>
        /// Tier held on each declared track, in the contract's declared track
        /// order. Multiply by that track's declared per-tier magnitude to get
        /// the effective modifier — the form catalog carries the BASE number,
        /// this carries the step.
        /// </summary>
        public ImmutableArray<int> TierLevels { get; }
    }

    /// <summary>
    /// One live pile of loose scrap. Piles merge by tile, so there is no
    /// origin discriminator — a wreck landing on a live deposit is one pile,
    /// and a killed carrier is simply a bigger wreck.
    /// </summary>
    public sealed record ScrapPile
    {
        /// <summary>Creates one live pile.</summary>
        /// <param name="position">The tile it sits on.</param>
        /// <param name="amount">Scrap on it.</param>
        /// <param name="expiresAtTick">
        /// The pile is gone the first tick <c>tick &gt;= expiresAtTick</c>.
        /// </param>
        public ScrapPile(Position position, int amount, int expiresAtTick)
        {
            ValidatePosition(position, nameof(position));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            ArgumentOutOfRangeException.ThrowIfNegative(expiresAtTick);
            Position = position;
            Amount = amount;
            ExpiresAtTick = expiresAtTick;
        }

        /// <summary>The tile it sits on.</summary>
        public Position Position { get; }
        /// <summary>Scrap on it.</summary>
        public int Amount { get; }
        /// <summary>
        /// The first tick on which this pile no longer exists — the same clock
        /// grammar as a route cooldown's ready tick and the ratchet hold's
        /// end tick. It binds while the observed tick is strictly below it.
        /// </summary>
        public int ExpiresAtTick { get; }
    }

    private static void ValidateBody(
        int generation,
        string formId,
        Position position,
        Direction facing,
        int health,
        int cooldown,
        int? energy)
    {
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
        GenericActorDynamicValueRules.SemanticId(formId, nameof(formId));
        ValidatePosition(position, nameof(position));
        GenericActorDynamicValueRules.EnumValue(facing, nameof(facing));
        if (health <= 0)
            throw new ArgumentOutOfRangeException(nameof(health));
        if (cooldown < 0)
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        if (energy is < 0)
            throw new ArgumentOutOfRangeException(nameof(energy));
    }

    private static void ValidateAudience(
        ObservedSelfState self,
        ImmutableArray<ObservedUnitSlot> teamUnits,
        ImmutableArray<ObservedAllyState> allies,
        ImmutableArray<ObservedEnemyState> enemies)
    {
        int observingTeamId = self.ActorId.TeamId;
        foreach (ObservedUnitSlot slot in teamUnits)
        {
            if (slot.TeamId != observingTeamId)
            {
                throw new ArgumentException(
                    "TeamUnits may contain only the observing team.",
                    nameof(teamUnits));
            }

            ActorIdentity? actorId = slot.State switch
            {
                UnitSlotState.Active active => active.ActorId,
                UnitSlotState.LifecyclePending pending =>
                    pending.SourceActorId,
                _ => null,
            };
            if (actorId is not null
                && (actorId.TeamId != slot.TeamId
                    || slot.State is UnitSlotState.Active
                    && actorId.UnitId != slot.UnitId))
            {
                throw new ArgumentException(
                    "A team-unit state contains an actor inconsistent with its stable slot.",
                    nameof(teamUnits));
            }
        }

        if (allies.Any(ally =>
                ally.ActorId.TeamId != observingTeamId))
        {
            throw new ArgumentException(
                "Allies may contain only the observing team.",
                nameof(allies));
        }
        if (enemies.Any(enemy =>
                enemy.ActorId.TeamId == observingTeamId))
        {
            throw new ArgumentException(
                "Enemies cannot contain a life from the observing team.",
                nameof(enemies));
        }
    }

    private static void ValidatePosition(
        Position position,
        string parameterName)
    {
        if (position.X < 0 || position.Y < 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static ImmutableArray<ObservedEvent> ValidateEvents(
        IEnumerable<ObservedEvent> events,
        int tick,
        string parameterName)
    {
        ImmutableArray<ObservedEvent> snapshot =
            GenericActorDynamicValueRules.Snapshot(events, parameterName);
        if (snapshot.Any(value => value.SourceTick > tick))
        {
            throw new ArgumentException(
                "Visible events cannot originate in the future.",
                parameterName);
        }
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(value => value.EventHandle),
            parameterName);
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(value =>
                (value.SourceTick, value.SourceOrdinal)),
            parameterName);
        return snapshot
            .OrderBy(value => value.SourceTick)
            .ThenBy(value => value.SourceOrdinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<ObservedSound> ValidateSounds(
        IEnumerable<ObservedSound> sounds,
        int tick,
        string parameterName)
    {
        ImmutableArray<ObservedSound> snapshot =
            GenericActorDynamicValueRules.Snapshot(sounds, parameterName);
        if (snapshot.Any(value => value.SourceTick > tick))
        {
            throw new ArgumentException(
                "Heard sounds cannot originate in the future.",
                parameterName);
        }
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(value =>
                (value.EventHandle, value.ObserverActorId)),
            parameterName);
        return snapshot
            .OrderBy(value => value.SourceTick)
            .ThenBy(value => value.SourceOrdinal)
            .ThenBy(value => value.ObserverActorId)
            .ThenBy(value => value.EventHandle, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<GenericActorActionLegality>
        CanonicalizeActionLegalities(
            IEnumerable<GenericActorActionLegality> values,
            string parameterName)
    {
        ImmutableArray<GenericActorActionLegality> snapshot =
            GenericActorDynamicValueRules.Snapshot(values, parameterName);
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(value => value.ActionId),
            parameterName);
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(value => value.ActionCode),
            parameterName);
        return snapshot
            .OrderBy(value => value.ActionCode)
            .ThenBy(value => value.ActionId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void ValidateSpawnLineage(
        GenericActorMatchStart.SpawnReason reason,
        int generation,
        ActorIdentity? parentActorId,
        string? sourceTransitionId,
        string? sourceOperationId)
    {
        bool valid = reason switch
        {
            GenericActorMatchStart.SpawnReason.Initial =>
                generation == 0
                && parentActorId is null
                && sourceTransitionId is null
                && sourceOperationId is null,
            GenericActorMatchStart.SpawnReason.AutomaticActivation
                or GenericActorMatchStart.SpawnReason.RootFactorySeed =>
                generation >= 0
                && parentActorId is null
                && sourceTransitionId is null
                && sourceOperationId is null,
            GenericActorMatchStart.SpawnReason.AutomaticReturn =>
                sourceTransitionId is null
                && sourceOperationId is null,
            GenericActorMatchStart.SpawnReason.Fabrication
                or GenericActorMatchStart.SpawnReason.Replication =>
                sourceTransitionId is not null
                && (parentActorId is null
                    || sourceOperationId is not null),
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "Life-spawn lineage does not match its spawn reason.");
        }
    }

    private static void ValidateOptionalLineage(
        string? sourceTransitionId,
        string? sourceOperationId)
    {
        if ((sourceTransitionId is null) != (sourceOperationId is null))
        {
            throw new ArgumentException(
                "Transition and operation lineage must both be present or both be absent.");
        }
    }

    private static ImmutableArray<T> Canonicalize<T, TKey>(
        IEnumerable<T> values,
        string parameterName,
        Func<T, TKey> key)
        where T : class =>
        Canonicalize(values, parameterName, key, Comparer<TKey>.Default);

    private static ImmutableArray<T> Canonicalize<T, TKey>(
        IEnumerable<T> values,
        string parameterName,
        Func<T, TKey> key,
        IComparer<TKey> comparer)
        where T : class
    {
        ImmutableArray<T> snapshot =
            GenericActorDynamicValueRules.Snapshot(values, parameterName);
        T[] ordered = snapshot
            .OrderBy(key, comparer)
            .ToArray();
        GenericActorDynamicValueRules.EnsureUnique(
            ordered.Select(key),
            parameterName);
        return ordered.ToImmutableArray();
    }
}
