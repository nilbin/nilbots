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
        PendingSameLifeTransition? PendingSameLifeTransition);

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
        PendingSameLifeTransition? PendingSameLifeTransition);

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

    public sealed record ObservedParticipantStatus(
        int ParticipantId,
        int TeamId,
        long RuntimeFaultCount,
        bool Disqualified);

    public sealed record ObservedEnemyState(
        ActorIdentity ActorId,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        PendingSameLifeTransition? PendingSameLifeTransition,
        ImmutableArray<ActorIdentity> ObservedBy);

    public sealed record ObservedTile(
        Position Position,
        bool IsWall,
        ImmutableArray<ActorIdentity> ObservedBy);

    public sealed record ObservedProjectile(
        long ProjectileId,
        int OwnerTeamId,
        ActorIdentity? OwnerActorId,
        Position Position,
        ProjectileHeading Heading,
        int TilesPerAdvance,
        int TicksUntilAdvance,
        int RemainingTiles,
        ImmutableArray<ActorIdentity> ObservedBy);

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
            int DueTick) : EventPayload;

        public sealed record ScoreChanged(
            int TeamId,
            string Channel,
            long NewValue) : EventPayload;

        public sealed record ModeChanged(
            ModeObservationState State) : EventPayload;

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
                int controlResumesAtTick)
                : base(modeId)
            {
                ActivePositionIndex = activePositionIndex;
                ClaimingTeamId = claimingTeamId;
                CaptureProgress = captureProgress;
                DecayTicksElapsed = decayTicksElapsed;
                ControlResumesAtTick = controlResumesAtTick;
            }

            public int ActivePositionIndex { get; }
            public int? ClaimingTeamId { get; }
            public int CaptureProgress { get; }
            public int DecayTicksElapsed { get; }
            public int ControlResumesAtTick { get; }
        }
    }
}
