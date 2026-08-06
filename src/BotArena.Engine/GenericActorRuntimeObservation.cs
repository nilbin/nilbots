using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Runtime-neutral public pre-tick input for one generic actor life. The common
/// host owns projection and canonical ordering; adapters validate their public
/// SDK representation at the execution boundary.
/// </summary>
public sealed record GenericActorRuntimeObservation(
    int SchemaVersion,
    int Tick,
    string MatchContractFingerprint,
    GenericActorRuntimeObservation.ObservedSelfState Self,
    ImmutableArray<GenericActorRuntimeObservation.ObservedUnitSlot> TeamUnits,
    ImmutableArray<GenericActorRuntimeObservation.ObservedParticipantStatus>
        Participants,
    ImmutableArray<GenericActorRuntimeObservation.ObservedAllyState> Allies,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEnemyState> Enemies,
    ImmutableArray<GenericActorRuntimeObservation.ObservedTile> VisibleTiles,
    ImmutableArray<GenericActorRuntimeObservation.ObservedProjectile>?
        VisibleProjectiles,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEvent> VisibleEvents,
    ImmutableArray<GenericActorRuntimeObservation.ObservedSound>? HeardSounds,
    GenericActorRuntimeObservation.ScoreboardState Scoreboard,
    GenericActorRuntimeObservation.ModeObservationState Mode,
    ImmutableArray<GenericActorRuntimeActionLegality> ActionLegalities)
{
    public sealed record ObservedSelfState(
        ActorIdentity ActorId,
        int Generation,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int? Energy,
        GenericActorRuntimeActionResolution? PreviousActionResolution,
        PendingSameLifeTransition? PendingSameLifeTransition)
    {
        /// <summary>
        /// Immutable chassis class, or null for a classless contract. It is
        /// copied from the controlling participant rather than inferred from
        /// <see cref="FormId"/>.
        /// </summary>
        public string? ClassId { get; init; }

        /// <summary>
        /// Every same-life route of this body's stable unit slot that is
        /// currently held shut by a declared route cooldown
        /// (<see cref="ActorSameLifeTransitionDefinition.CooldownTicks"/>),
        /// ordered by transition ID. The clock names the tick the
        /// restriction lifts — the route refuses re-entry while the observed
        /// tick is strictly below <see
        /// cref="ObservedRouteCooldown.ReadyAtTick"/>. Slot-scoped like the
        /// cooldown itself, so it survives this life's death. Empty when no
        /// route cooldown is live, which is also every contract declaring
        /// none — the additive inert default.
        /// </summary>
        public ImmutableArray<ObservedRouteCooldown> RouteCooldowns
        {
            get;
            init;
        } = [];

        /// <summary>
        /// Scrap this body is currently carrying, which is exactly what a
        /// death on this tile would put on the floor. Zero when carrying
        /// nothing, and zero for the whole match on every ruleset without a
        /// declared scrap economy — the additive inert default. Compare it
        /// against the contract's declared <c>carryCapacity</c> to know
        /// whether another pile would fit.
        /// </summary>
        public int CarriedScrap { get; init; }

        /// <summary>
        /// The free-vocabulary label this body's MIND last attached to it
        /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §12), or null
        /// when it carries none. Entirely non-authoritative: the engine never
        /// branches on it, it is never an action parameter, and it cannot
        /// change a single point of simulation state. It is null for the whole
        /// match on the per-life generation, which has no way to set one.
        /// </summary>
        public string? RoleTag { get; init; }
    }

    public sealed record ObservedAllyState(
        ActorIdentity ActorId,
        int Generation,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int? Energy,
        GenericActorRuntimeActionResolution? PreviousActionResolution,
        PendingSameLifeTransition? PendingSameLifeTransition)
    {
        public string? ClassId { get; init; }

        /// <summary>
        /// The ally slot's live route cooldowns, published under the same
        /// grammar as <see cref="ObservedSelfState.RouteCooldowns"/> —
        /// allies share their complete gameplay state.
        /// </summary>
        public ImmutableArray<ObservedRouteCooldown> RouteCooldowns
        {
            get;
            init;
        } = [];

        /// <summary>
        /// The ally's load, published under the same grammar as
        /// <see cref="ObservedSelfState.CarriedScrap"/> — allies share their
        /// complete gameplay state, so an escort knows what it is escorting.
        /// </summary>
        public int CarriedScrap { get; init; }

        /// <summary>
        /// The free-vocabulary label this body's MIND last attached to it
        /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §12), or null
        /// when it carries none. Entirely non-authoritative: the engine never
        /// branches on it, it is never an action parameter, and it cannot
        /// change a single point of simulation state. It is null for the whole
        /// match on the per-life generation, which has no way to set one.
        /// </summary>
        public string? RoleTag { get; init; }
    }

    /// <summary>
    /// One live slot-scoped route cooldown: the named same-life transition
    /// refuses re-entry while the observed tick is strictly below
    /// <paramref name="ReadyAtTick"/>.
    /// </summary>
    public sealed record ObservedRouteCooldown(
        string TransitionId,
        int ReadyAtTick);

    public sealed record PendingSameLifeTransition(
        string TransitionId,
        string OperationId,
        string TargetFormId,
        int StartedTick,
        int DueTick);

    public sealed record ObservedUnitSlot(
        int TeamId,
        int UnitId,
        UnitSlotState State);

    public abstract record UnitSlotState
    {
        private UnitSlotState()
        {
        }

        public sealed record Active(
            ActorIdentity ActorId,
            int Generation,
            string FormId) : UnitSlotState;

        public sealed record AvailabilityPending(
            AvailabilityReason Reason,
            int DueTick) : UnitSlotState;

        public sealed record AutomaticReturnPending(
            int DueTick,
            string TargetFormId,
            int Generation) : UnitSlotState;

        public sealed record Ready : UnitSlotState;

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
                DueTick = dueTick;
                SourceActorId = sourceActorId;
                TransitionId = transitionId;
                OperationId = operationId;
                TargetFormId = targetFormId;
                ReservedPosition = reservedPosition;
            }

            public int DueTick { get; }
            public ActorIdentity SourceActorId { get; }
            public string TransitionId { get; }
            public string OperationId { get; }
            public string TargetFormId { get; }
            public Position ReservedPosition { get; }
        }

        public sealed record FabricationPending : LifecyclePending
        {
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
        }

        public sealed record ReplicationPending : LifecyclePending
        {
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
        }

        public sealed record PermanentlyDormant : UnitSlotState;
    }

    public enum AvailabilityReason
    {
        InitialUnlock = 0,
        DestructionRecovery = 1,
    }

    /// <summary>
    /// What caused a same-life form transition. The route alone cannot say:
    /// a stance's return route serves both the author's early exit and the
    /// engine's threshold return, so the cause is a fact of its own — the
    /// same shape <see cref="GenericActorRuntimeStart.SpawnReason"/> gives a
    /// life's origin. <see cref="Requested"/> is today's only cause and the
    /// inert default; canonical replay omits it.
    /// </summary>
    public enum FormTransitionReason
    {
        /// <summary>A successful same-life transition action started it.</summary>
        Requested = 0,

        /// <summary>
        /// The engine started it with no action because the source form's
        /// declared automatic-return counter reached its threshold
        /// (<see cref="ActorAutomaticReturnTriggerDefinition"/>).
        /// </summary>
        AutomaticThresholdReturn = 1,
    }

    public sealed record ObservedParticipantStatus(
        int ParticipantId,
        int TeamId,
        long RuntimeFaultCount,
        bool Disqualified)
    {
        public string? ClassId { get; init; }
    }

    public sealed record ObservedEnemyState(
        ActorIdentity ActorId,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        PendingSameLifeTransition? PendingSameLifeTransition,
        ImmutableArray<ActorIdentity> ObservedBy)
    {
        public string? ClassId { get; init; }

        /// <summary>
        /// A visible enemy's load. It is the fact that makes interception a
        /// decision rather than a guess — "is that body worth chasing" is
        /// exactly the question — and without it the harass loop is a coin
        /// flip. Zero when carrying nothing, and zero for the whole match on
        /// every ruleset without a declared scrap economy.
        /// </summary>
        public int CarriedScrap { get; init; }

        /// <summary>
        /// The free-vocabulary label this body's MIND last attached to it
        /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §12), or null
        /// when it carries none. Entirely non-authoritative: the engine never
        /// branches on it, it is never an action parameter, and it cannot
        /// change a single point of simulation state. It is null for the whole
        /// match on the per-life generation, which has no way to set one.
        /// </summary>
        public string? RoleTag { get; init; }
    }

    public sealed record ObservedTile(
        Position Position,
        bool IsWall,
        ImmutableArray<ActorIdentity> ObservedBy)
    {
        /// <summary>
        /// A lifecycle output claim currently making this visible tile
        /// unavailable. Null means the tile has no spawn claim.
        /// </summary>
        public SpawnReservation? SpawnReservation { get; init; }
    }

    public sealed record SpawnReservation(
        int TeamId,
        int UnitId,
        SpawnReservationKind Kind,
        int? DueTick);

    public enum SpawnReservationKind
    {
        AutomaticReturn = 0,
        Fabrication = 1,
        Replication = 2,
    }

    public sealed record ObservedProjectile(
        long ProjectileId,
        int OwnerTeamId,
        ActorIdentity? OwnerActorId,
        Position Position,
        ProjectileHeading Heading,
        int TilesPerAdvance,
        int TicksUntilAdvance,
        int RemainingTiles,
        ImmutableArray<ActorIdentity> ObservedBy,
        int TicksPerAdvance,
        int DamagePerHit);

    public sealed record ObservedSound(
        string EventHandle,
        int SourceTick,
        int SourceOrdinal,
        ActorIdentity ObserverActorId,
        EventKind Kind,
        int Bearing,
        int Distance);

    public sealed record ObservedEvent(
        string EventHandle,
        int SourceTick,
        int SourceOrdinal,
        EventKind Kind,
        EventPayload Payload,
        ImmutableArray<ActorIdentity> ObservedBy);

    public abstract record EventPayload
    {
        private EventPayload()
        {
        }

        public sealed record Rotation(
            ActorIdentity ActorId,
            GenericActorRuntimeActionResolution.ResolvedAction Action,
            Position Position,
            Direction FromFacing,
            Direction ToFacing) : EventPayload;

        public sealed record Movement(
            ActorIdentity ActorId,
            GenericActorRuntimeActionResolution.ResolvedAction Action,
            Position From,
            Position To,
            Direction Facing) : EventPayload;

        public sealed record MovementBlocked(
            ActorIdentity ActorId,
            GenericActorRuntimeActionResolution.ResolvedAction Action,
            Position From,
            Position AttemptedTo,
            Direction Facing) : EventPayload;

        public sealed record Attack(
            ActorIdentity ActorId,
            GenericActorRuntimeActionResolution.ResolvedAction Action,
            long ProjectileId,
            Position Origin,
            ProjectileHeading Heading) : EventPayload;

        public sealed record Damage(
            int SourceTeamId,
            ActorIdentity? SourceActorId,
            ActorIdentity TargetActorId,
            long ProjectileId,
            int Amount,
            int NewHealth,
            Position Position) : EventPayload;

        /// <summary>
        /// An enemy projectile died on the target form's declared projectile
        /// guard instead of damaging it, and the guard launched a replacement
        /// bolt back along the reversed heading under its own team's
        /// ownership. The deflecting life, the firing life, the consumed bolt,
        /// and the returned bolt are all named, so an observer can attribute
        /// the return exactly as it attributes a hit — and so the returned
        /// bolt's launch has a single authoritative cause.
        /// </summary>
        public sealed record ProjectileDeflected(
            int SourceTeamId,
            ActorIdentity? SourceActorId,
            ActorIdentity TargetActorId,
            long ProjectileId,
            long DeflectedProjectileId,
            string TargetFormId,
            Direction TargetFacing,
            ProjectileHeading Heading,
            Position Position) : EventPayload;

        public sealed record Destruction(
            ActorIdentity ActorId,
            int? SourceTeamId,
            ActorIdentity? SourceActorId,
            long? ProjectileId,
            int Generation,
            string FormId,
            Position Position) : EventPayload;

        public sealed record LifeSpawned(
            ActorIdentity ActorId,
            int ParticipantId,
            ActorIdentity? ParentActorId,
            int Generation,
            string FormId,
            int Health,
            Position Position,
            GenericActorRuntimeStart.SpawnReason Reason,
            string? SourceTransitionId,
            string? SourceOperationId) : EventPayload;

        public sealed record LifeRetired(
            ActorIdentity ActorId,
            int Generation,
            string FormId,
            Position Position,
            string Reason,
            string? SourceTransitionId,
            string? SourceOperationId) : EventPayload;

        public sealed record RuntimeFault(
            GenericActorRuntimeFault Fault) : EventPayload;

        /// <summary>
        /// A participant-scoped mind fault carrying no actor identity (P3).
        /// </summary>
        public sealed record MindRuntimeFault(
            GenericMindRuntimeFault Fault) : EventPayload;

        public sealed record Participant(
            int ParticipantId,
            int TeamId) : EventPayload;

        public sealed record Lifecycle(
            string TransitionId,
            string OperationId,
            ActorIdentity SourceActorId,
            int? TargetTeamId,
            int? TargetUnitId,
            int? DueTick,
            string? CancellationReason) : EventPayload;

        public sealed record FormTransition(
            ActorIdentity ActorId,
            string TransitionId,
            string OperationId,
            string FromFormId,
            string ToFormId,
            int StartedTick,
            int DueTick,
            FormTransitionReason Reason = FormTransitionReason.Requested)
            : EventPayload;

        public sealed record ScoreChanged(
            int TeamId,
            string Channel,
            long NewValue) : EventPayload;

        public sealed record ModeChanged(
            ModeObservationState State) : EventPayload;

        public sealed record ArcRelay(
            ArcRelayEvent Fact) : EventPayload;

        public sealed record LifecycleClockCancelled : EventPayload
        {
            public LifecycleClockCancelled(
                int targetTeamId,
                int targetUnitId,
                UnitSlotState cancelledState,
                string cancellationReason)
            {
                ArgumentNullException.ThrowIfNull(cancelledState);
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    cancellationReason);
                if (targetTeamId < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(targetTeamId));
                }
                if (targetUnitId < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(targetUnitId));
                }
                if (cancelledState is not
                        UnitSlotState.AvailabilityPending
                    and not UnitSlotState.AutomaticReturnPending)
                {
                    throw new ArgumentException(
                        "A lifecycle clock cancellation must snapshot an availability or automatic-return clock.",
                        nameof(cancelledState));
                }

                TargetTeamId = targetTeamId;
                TargetUnitId = targetUnitId;
                CancelledState = cancelledState;
                CancellationReason = cancellationReason;
            }

            public int TargetTeamId { get; }
            public int TargetUnitId { get; }
            public UnitSlotState CancelledState { get; }
            public string CancellationReason { get; }
        }
    }

    public enum EventKind
    {
        Rotation = 0,
        Movement = 1,
        MovementBlocked = 2,
        Attack = 3,
        Damage = 4,
        Destruction = 5,
        LifeSpawned = 6,
        LifeRetired = 7,
        RuntimeFault = 8,
        ParticipantDisqualified = 9,
        LifecycleQueued = 10,
        LifecycleCancelled = 11,
        LifecycleCompleted = 12,
        FormTransitionStarted = 13,
        FormTransitionCompleted = 14,
        FormTransitionCancelled = 15,
        ScoreChanged = 16,
        ModeChanged = 17,
        LifecycleClockCancelled = 18,

        /// <summary>
        /// Additive append (DECISIONS #156's discipline applied to the
        /// observed-event enum): a projectile guard killed an enemy bolt on
        /// its arc and returned a team-flipped bolt along the reversed
        /// heading. Contracts whose forms declare no guard never emit it, so
        /// no existing replay or observation changes.
        /// </summary>
        ProjectileDeflected = 19,

        /// <summary>
        /// Additive append (P3, DECISIONS #191): a PARTICIPANT-SCOPED runtime
        /// fault with no body to attribute it to. Under the mind profile the
        /// faulting unit is the mind, and a mind still ticks on a tick it owns
        /// nothing (§2.7) — so it can also trap on one. The per-body
        /// <see cref="RuntimeFault"/> event has nowhere to land in that case,
        /// and under a threshold-0 contract that silent frame is precisely the
        /// moment a participant lost the match. Emitted ONLY when the mind
        /// held no live body; a fault with bodies keeps publishing one per-body
        /// event exactly as before, so no per-life replay changes.
        /// </summary>
        MindRuntimeFault = 20,

        /// <summary>
        /// One stable Arc Relay objective or signature fact. The payload owns
        /// a second closed discriminator so additions never overload a combat
        /// or lifecycle event meaning.
        /// </summary>
        ArcRelay = 21,
    }

    public sealed record ScoreboardState(
        ImmutableArray<TeamScoreState> Teams);

    public sealed record TeamScoreState(
        int TeamId,
        bool Eligible,
        ImmutableArray<ScoreValue> Scores);

    public sealed record ScoreValue(
        string Channel,
        long Value);

    public abstract record ModeObservationState
    {
        private ModeObservationState(string modeId)
        {
            ModeId = modeId;
        }

        public string ModeId { get; }

        public sealed record Deathmatch : ModeObservationState
        {
            public Deathmatch(string modeId)
                : base(modeId)
            {
            }
        }

        public sealed record Frontline : ModeObservationState
        {
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
                int secondaryClaimProgress = 0)
                : base(modeId)
            {
                if ((holdOwnerTeamId is null) != (holdEndsAtTick is null))
                {
                    throw new ArgumentException(
                        "A territory-ratchet hold publishes its owner and its "
                        + "expiry together or not at all.",
                        nameof(holdOwnerTeamId));
                }
                if (secondaryOwnerTeamId is not null
                    && secondaryOwnerTeamId == SecondaryClaimant(
                        secondaryClaimProgress))
                {
                    throw new ArgumentException(
                        "A side objective's owner cannot also be claiming it.",
                        nameof(secondaryClaimProgress));
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
            }

            /// <summary>
            /// Both teams' complete economic position — liquid bank and the
            /// tier held on each declared track — ordered by team ID and
            /// published to everybody. One field carries the whole ledger,
            /// which is what makes the purchase telegraph free: a tier change
            /// moves the mode state, a changed mode state rides the existing
            /// <see cref="EventKind.ModeChanged"/> fact, and the enemy sees
            /// the bank drop and the tier rise on the tick they happen with
            /// no visibility requirement and no inference.
            /// <para>Empty on every ruleset that declares no scrap economy —
            /// the additive inert default, so a bot never branches on whether
            /// the mechanic exists. <c>TierLevels</c> is positional against
            /// the contract's declared track order.</para>
            /// </summary>
            public ImmutableArray<ScrapTeamState> ScrapTeams
            {
                get;
                init;
            } = [];

            /// <summary>
            /// Every live pile of loose scrap, ordered by <c>(y, x)</c>.
            /// Neither half is derivable: the deposit schedule is static
            /// contract data but WHETHER a deposit is still there is not, and
            /// a wreck's location is unavailable to a body that was not
            /// present at the kill. Empty on every ruleset without the
            /// economy.
            /// </summary>
            public ImmutableArray<ScrapPile> ScrapPiles
            {
                get;
                init;
            } = [];

            /// <summary>
            /// Structural equality, spelled out because the two economy facts
            /// are <see cref="ImmutableArray{T}"/>: its own equality compares
            /// the underlying array by REFERENCE, so the synthesized record
            /// comparison would call two identical published states different
            /// and every consumer that asks "did the mode change this tick?"
            /// — the mode-changed telegraph and the replay validator's
            /// boundary check among them — would answer yes on every tick.
            /// </summary>
            public bool Equals(Frontline? other) =>
                other is not null
                && string.Equals(ModeId, other.ModeId, StringComparison.Ordinal)
                && ActivePositionIndex == other.ActivePositionIndex
                && ClaimingTeamId == other.ClaimingTeamId
                && CaptureProgress == other.CaptureProgress
                && DecayTicksElapsed == other.DecayTicksElapsed
                && ControlResumesAtTick == other.ControlResumesAtTick
                && HoldOwnerTeamId == other.HoldOwnerTeamId
                && HoldEndsAtTick == other.HoldEndsAtTick
                && SecondaryOwnerTeamId == other.SecondaryOwnerTeamId
                && SecondaryClaimProgress == other.SecondaryClaimProgress
                && ScrapTeams.SequenceEqual(other.ScrapTeams)
                && ScrapPiles.SequenceEqual(other.ScrapPiles);

            /// <inheritdoc />
            public override int GetHashCode()
            {
                var hash = default(HashCode);
                hash.Add(ModeId, StringComparer.Ordinal);
                hash.Add(ActivePositionIndex);
                hash.Add(ClaimingTeamId);
                hash.Add(CaptureProgress);
                hash.Add(DecayTicksElapsed);
                hash.Add(ControlResumesAtTick);
                hash.Add(HoldOwnerTeamId);
                hash.Add(HoldEndsAtTick);
                hash.Add(SecondaryOwnerTeamId);
                hash.Add(SecondaryClaimProgress);
                foreach (ScrapTeamState team in ScrapTeams)
                {
                    hash.Add(team.TeamId);
                    hash.Add(team.Bank);
                    foreach (int tier in team.TierLevels)
                        hash.Add(tier);
                }
                foreach (ScrapPile pile in ScrapPiles)
                {
                    hash.Add(pile.Position);
                    hash.Add(pile.Amount);
                    hash.Add(pile.ExpiresAtTick);
                }
                return hash.ToHashCode();
            }

            /// <summary>
            /// The team a signed side-objective claim belongs to, or null
            /// when the claim is zero.
            /// </summary>
            public static int? SecondaryClaimant(int claimProgress) =>
                claimProgress switch
                {
                    0 => null,
                    > 0 => 0,
                    _ => 1,
                };

            public int ActivePositionIndex { get; }
            public int? ClaimingTeamId { get; }
            public int CaptureProgress { get; }
            public int DecayTicksElapsed { get; }
            public int ControlResumesAtTick { get; }

            /// <summary>
            /// The team whose completed advance is currently protected by the
            /// territory ratchet, or null when no hold is live — including
            /// every ruleset whose redeploy policy has no ratchet at all.
            /// Published beside <see cref="ControlResumesAtTick"/> because
            /// both are authoritative control clocks, and published rather
            /// than inferred because the only derivation available to a bot
            /// was signed front displacement, which is wrong after the first
            /// regression and unavailable to a life born inside the hold
            /// (DECISIONS #168/#169).
            /// </summary>
            public int? HoldOwnerTeamId { get; }

            /// <summary>
            /// The first tick on which the live hold no longer denies enemy
            /// regression, or null when no hold is live. Same grammar as
            /// <see cref="ControlResumesAtTick"/>: the clock names the tick
            /// the restriction lifts, so the hold binds while the observed
            /// tick is strictly below it.
            /// </summary>
            public int? HoldEndsAtTick { get; }

            /// <summary>
            /// The team that owns the declared side objective, or null when
            /// it is neutral — including every ruleset that declares no side
            /// objective at all. Published rather than inferred for the
            /// reason the ratchet hold was: a body that walked off to a side
            /// site is invisible at range 6, so without this fact "are they
            /// one body light at the front?" is a guess.
            /// </summary>
            public int? SecondaryOwnerTeamId { get; }

            /// <summary>
            /// The running claim on the side objective as signed
            /// sole-presence ticks: positive counts for team 0, negative for
            /// team 1, and zero means no claim stands. Compare its magnitude
            /// against the contract's declared
            /// <c>captureThresholdTicks</c> — the threshold is static
            /// contract data, not an observation fact.
            /// </summary>
            public int SecondaryClaimProgress { get; }
        }

        public sealed record ArcRelay : ModeObservationState
        {
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
                if (wells.IsDefault || wells.Any(value => value is null)
                    || reactors.IsDefault
                    || reactors.Any(value => value is null)
                    || visibleCores.IsDefault
                    || visibleCores.Any(value => value is null)
                    || visibleSignatures.IsDefault
                    || visibleSignatures.Any(value => value is null))
                {
                    throw new ArgumentException(
                        "Arc Relay observation arrays must be initialized and non-null.");
                }
                if ((latestPulseTeamId is null) != (latestPulseTick is null)
                    || latestPulseTeamId < 0 || latestPulseTick < 0)
                {
                    throw new ArgumentException(
                        "Arc Relay latest Pulse team and tick travel together.");
                }
                Wells = wells;
                Reactors = reactors;
                VisibleCores = visibleCores;
                VisibleSignatures = visibleSignatures;
                LatestPulseTeamId = latestPulseTeamId;
                LatestPulseTick = latestPulseTick;
            }

            public ImmutableArray<ArcRelayWellState> Wells { get; }
            public ImmutableArray<ArcRelayReactorState> Reactors { get; }
            public ImmutableArray<ArcRelayCoreState> VisibleCores { get; }
            public ImmutableArray<ArcRelaySignatureState> VisibleSignatures
            { get; }
            public int? LatestPulseTeamId { get; }
            public int? LatestPulseTick { get; }

            /// <summary>
            /// Declared strikes in windup, public to both teams
            /// (DECISIONS #212). Empty on rulesets without strike windups,
            /// keeping historical observations byte-identical.
            /// </summary>
            public ImmutableArray<ArcRelayPendingStrikeState> PendingStrikes
            { get; init; } = [];

            public bool Equals(ArcRelay? other) =>
                other is not null
                && ModeId == other.ModeId
                && Wells.SequenceEqual(other.Wells)
                && Reactors.SequenceEqual(other.Reactors)
                && VisibleCores.SequenceEqual(other.VisibleCores)
                && VisibleSignatures.SequenceEqual(other.VisibleSignatures)
                && LatestPulseTeamId == other.LatestPulseTeamId
                && LatestPulseTick == other.LatestPulseTick
                && PendingStrikes.SequenceEqual(other.PendingStrikes);

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(ModeId, StringComparer.Ordinal);
                foreach (ArcRelayWellState value in Wells) hash.Add(value);
                foreach (ArcRelayReactorState value in Reactors) hash.Add(value);
                foreach (ArcRelayCoreState value in VisibleCores) hash.Add(value);
                foreach (ArcRelaySignatureState value in VisibleSignatures)
                    hash.Add(value);
                hash.Add(LatestPulseTeamId);
                hash.Add(LatestPulseTick);
                foreach (ArcRelayPendingStrikeState value in PendingStrikes)
                    hash.Add(value);
                return hash.ToHashCode();
            }
        }
    }

    /// <summary>
    /// One publicly declared strike in windup (DECISIONS #212): the shooter,
    /// the tick the ray resolves, and the frozen tiles it will trace.
    /// </summary>
    public sealed record ArcRelayPendingStrikeState(
        ActorIdentity Shooter,
        int ResolveAtTick,
        ImmutableArray<Position> Tiles)
    {
        public bool Equals(ArcRelayPendingStrikeState? other) =>
            other is not null
            && Shooter == other.Shooter
            && ResolveAtTick == other.ResolveAtTick
            && Tiles.SequenceEqual(other.Tiles);

        public override int GetHashCode() => HashCode.Combine(
            Shooter,
            ResolveAtTick,
            Tiles.Length);
    }

    /// <summary>
    /// One team's published economic position under a declared scrap economy.
    /// </summary>
    /// <param name="TeamId">The scoring team.</param>
    /// <param name="Bank">Unspent scrap. Both teams' banks are public.</param>
    /// <param name="TierLevels">
    /// Tier held on each track, positionally against the contract's declared
    /// track order. Multiply by that track's declared per-tier magnitude to
    /// get the effective modifier.
    /// </param>
    public sealed record ScrapTeamState(
        int TeamId,
        int Bank,
        ImmutableArray<int> TierLevels)
    {
        /// <summary>
        /// Structural equality for the same reason the mode state spells its
        /// own out: the tier vector is an <see cref="ImmutableArray{T}"/>.
        /// </summary>
        public bool Equals(ScrapTeamState? other) =>
            other is not null
            && TeamId == other.TeamId
            && Bank == other.Bank
            && TierLevels.SequenceEqual(other.TierLevels);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = default(HashCode);
            hash.Add(TeamId);
            hash.Add(Bank);
            foreach (int tier in TierLevels)
                hash.Add(tier);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// One live pile of loose scrap. Piles merge by tile, so there is no
    /// origin discriminator — a wreck landing on a live deposit is one pile.
    /// </summary>
    /// <param name="Position">The tile.</param>
    /// <param name="Amount">Scrap on it.</param>
    /// <param name="ExpiresAtTick">
    /// The pile is gone the first tick <c>tick &gt;= expiresAtTick</c>, the
    /// same clock grammar as <c>holdEndsAtTick</c> and <c>readyAtTick</c>.
    /// </param>
    public sealed record ScrapPile(
        Position Position,
        int Amount,
        int ExpiresAtTick);
}
