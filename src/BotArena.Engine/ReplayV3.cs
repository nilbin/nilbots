using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable generation-3 replay wire model. The DTO graph deliberately owns
/// no chronology or live-session objects; serialization and hashing are
/// separate concerns.
/// </summary>
internal sealed record ReplayV3(
    ReplayV3.ReplayHeader Header,
    ReplayV3.ReplayInitialFrame InitialFrame,
    ImmutableArray<ReplayV3.TickFrame> Ticks,
    ReplayV3.MatchResult? Result,
    string? ReplayHash,
    bool Partial)
{
    internal sealed record ReplayHeader(
        int ReplayVersion,
        string EngineVersion,
        string GameRulesVersion,
        RuntimeVersions Runtime,
        string Seed,
        ResolvedContract Contract,
        PresentationMetadata? Presentation,
        ProvenanceMetadata? Provenance);

    internal sealed record RuntimeVersions(
        string ContractProfileId,
        string ProtocolVersion,
        string ConfigurationVersion,
        int RuntimeContractVersion,
        int MatchStartSchemaVersion,
        int ObservationSchemaVersion,
        int DecisionSchemaVersion,
        int MatchContractSchemaVersion);

    /// <summary>
    /// Exact canonical generation-3 contract bytes represented as text. A
    /// future codec writes <see cref="CanonicalJson"/> as an embedded JSON
    /// value rather than re-projecting mutable engine definitions.
    /// </summary>
    internal sealed record ResolvedContract(
        int SchemaVersion,
        string MatchContractFingerprint,
        string CanonicalJson);

    /// <summary>
    /// Optional replay-owned visuals needed for self-contained playback.
    /// Presentation is intentionally not part of the gameplay contract or its
    /// fingerprint, but it is part of the replay header and replay hash.
    /// </summary>
    internal sealed record PresentationMetadata(
        string? ThemeId,
        MapPresentationMetadata? Map,
        ImmutableArray<FormPresentationMetadata> Forms);

    internal sealed record MapPresentationMetadata(
        string BoundaryWall,
        string InteriorWall,
        ImmutableArray<WallGroupPresentationMetadata> WallGroups);

    internal sealed record WallGroupPresentationMetadata(
        string Family,
        ImmutableArray<PositionValue> Tiles);

    internal sealed record FormPresentationMetadata(
        string FormId,
        string? LookId,
        string? ProjectileLookId);

    internal sealed record ProvenanceMetadata(
        ImmutableArray<ParticipantProvenance> Participants);

    internal sealed record ParticipantProvenance(
        int ParticipantId,
        int TeamId,
        string Name,
        string RuntimeKind,
        string? ArtifactHash,
        string Accent,
        string? LookId,
        string? ProjectileLookId);

    internal sealed record ReplayInitialFrame(
        WorldState State,
        ImmutableArray<LifeStart> LifeStarts,
        ImmutableArray<AuthoritativeEvent> Events);

    internal sealed record TickFrame(
        int Tick,
        TickStart TickStart,
        ImmutableArray<ActorTurn> ActorTurns,
        ImmutableArray<AuthoritativeEvent> Events,
        ImmutableArray<ProjectileTraversal> Traversals,
        WorldState PostState);

    internal sealed record TickStart(
        int Tick,
        WorldState State,
        ImmutableArray<ActorId> ActiveActorIds,
        ImmutableArray<LifeStart> LifeStarts,
        ImmutableArray<AuthoritativeEvent> Events,
        ImmutableArray<ProjectileTraversal> Traversals);

    internal sealed record ActorTurn(
        int Tick,
        int ParticipantId,
        ActorId ActorId,
        Observation Observation,
        SubmittedDecision? SubmittedDecision,
        ActionResolution ActionResolution);

    internal sealed record LifeStart(
        int SchemaVersion,
        int RuntimeContractVersion,
        ActorId ActorId,
        int ParticipantId,
        string ActorRandomSeed,
        LifeOrigin Origin,
        string MatchContractFingerprint);

    internal sealed record LifeOrigin(
        string Reason,
        int Generation,
        ActorId? ParentActorId,
        string? SourceTransitionId,
        string? SourceOperationId);

    internal sealed record ActorId(
        int TeamId,
        int UnitId,
        int LifeId);

    internal sealed record PositionValue(
        int X,
        int Y);

    internal sealed record ShotProgramValue(
        int InitialAimOffset,
        int BendDirection,
        int BendAfterTiles,
        int BendEveryTiles,
        int BendCount);

    internal sealed record Observation(
        int SchemaVersion,
        int Tick,
        string MatchContractFingerprint,
        ObservedSelf Self,
        ImmutableArray<ObservedUnitSlot> TeamUnits,
        ImmutableArray<ParticipantStatus> Participants,
        ImmutableArray<ObservedAlly> Allies,
        ImmutableArray<ObservedEnemy> Enemies,
        ImmutableArray<ObservedTile> VisibleTiles,
        ImmutableArray<ObservedProjectile>? VisibleProjectiles,
        ImmutableArray<ObservedEvent> VisibleEvents,
        ImmutableArray<ObservedSound>? HeardSounds,
        Scoreboard Scoreboard,
        ModeState Mode,
        ImmutableArray<ActionLegality> ActionLegalities);

    internal sealed record ObservedSelf(
        ActorId ActorId,
        int Generation,
        string FormId,
        PositionValue Position,
        string Facing,
        int Health,
        int Cooldown,
        int? Energy,
        ActionResolution? PreviousActionResolution,
        PendingSameLifeTransition? PendingSameLifeTransition,
        string? ClassId,
        ImmutableArray<RouteCooldown> RouteCooldowns = default);

    internal sealed record ObservedAlly(
        ActorId ActorId,
        int Generation,
        string FormId,
        PositionValue Position,
        string Facing,
        int Health,
        int Cooldown,
        int? Energy,
        ActionResolution? PreviousActionResolution,
        PendingSameLifeTransition? PendingSameLifeTransition,
        string? ClassId,
        ImmutableArray<RouteCooldown> RouteCooldowns = default);

    /// <summary>
    /// One live slot-scoped route cooldown snapshot (#181/#182): the named
    /// same-life route refuses requested re-entry while the observed tick is
    /// strictly below <paramref name="ReadyAtTick"/>. Serialized only while
    /// live, so contracts declaring no route cooldown never carry the key.
    /// </summary>
    internal sealed record RouteCooldown(
        string TransitionId,
        int ReadyAtTick);

    internal sealed record ObservedEnemy(
        ActorId ActorId,
        string FormId,
        PositionValue Position,
        string Facing,
        int Health,
        PendingSameLifeTransition? PendingSameLifeTransition,
        ImmutableArray<ActorId> ObservedBy,
        string? ClassId);

    internal sealed record PendingSameLifeTransition(
        string TransitionId,
        string OperationId,
        string TargetFormId,
        int StartedTick,
        int DueTick);

    internal sealed record ObservedUnitSlot(
        int TeamId,
        int UnitId,
        UnitSlotState State);

    internal abstract record UnitSlotState(string Kind)
    {
        internal sealed record Active(
            ActorId ActorId,
            int Generation,
            string FormId) : UnitSlotState("active");

        internal sealed record AvailabilityPending(
            string Reason,
            int DueTick) : UnitSlotState("availability-pending");

        internal sealed record AutomaticReturnPending(
            int DueTick,
            string TargetFormId,
            int Generation) : UnitSlotState("automatic-return-pending");

        internal sealed record Ready() : UnitSlotState("ready");

        internal sealed record FabricationPending(
            int DueTick,
            ActorId SourceActorId,
            string TransitionId,
            string OperationId,
            string TargetFormId,
            PositionValue ReservedPosition)
            : UnitSlotState("fabrication-pending");

        internal sealed record ReplicationPending(
            int DueTick,
            ActorId SourceActorId,
            string TransitionId,
            string OperationId,
            string TargetFormId,
            PositionValue ReservedPosition)
            : UnitSlotState("replication-pending");

        internal sealed record PermanentlyDormant()
            : UnitSlotState("permanently-dormant");
    }

    internal sealed record ParticipantStatus(
        int ParticipantId,
        int TeamId,
        string RuntimeFaultCount,
        bool Disqualified,
        string? ClassId);

    internal sealed record ObservedTile(
        PositionValue Position,
        bool IsWall,
        ImmutableArray<ActorId> ObservedBy,
        SpawnReservation? SpawnReservation);

    internal sealed record SpawnReservation(
        int TeamId,
        int UnitId,
        string Kind,
        int? DueTick);

    internal sealed record ObservedProjectile(
        string ProjectileId,
        int OwnerTeamId,
        ActorId? OwnerActorId,
        PositionValue Position,
        string Heading,
        int TilesPerAdvance,
        int TicksUntilAdvance,
        int RemainingTiles,
        ImmutableArray<ActorId> ObservedBy,
        int TicksPerAdvance,
        int DamagePerHit);

    internal sealed record ObservedEvent(
        string EventHandle,
        int SourceTick,
        int SourceOrdinal,
        string Kind,
        EventPayload Payload,
        ImmutableArray<ActorId> ObservedBy);

    internal sealed record ObservedSound(
        string EventHandle,
        int SourceTick,
        int SourceOrdinal,
        ActorId ObserverActorId,
        string Kind,
        int Bearing,
        int Distance);

    internal sealed record SubmittedDecision(
        string? ActionId,
        int ActionCode,
        ImmutableArray<RawActionArgument?>? Arguments,
        string? DebugMessage);

    /// <summary>
    /// Lossless raw runtime reply arguments. Numeric enum values are retained
    /// even when malformed so rejected bot output remains replayable evidence.
    /// </summary>
    internal abstract record RawActionArgument(string Kind)
    {
        internal sealed record ShotProgram(ShotProgramValue Value)
            : RawActionArgument("shot-program");

        internal sealed record Direction(int Value)
            : RawActionArgument("direction");

        internal sealed record UnitTarget(int TeamId, int UnitId)
            : RawActionArgument("unit-target");

        internal sealed record FormTarget(string? FormId)
            : RawActionArgument("form-target");

        internal sealed record ProjectileHeading(int Value)
            : RawActionArgument("projectile-heading");
    }

    internal sealed record ActionResolution(
        ResolvedAction? SubmittedAction,
        ResolvedAction AcceptedAction,
        ResolvedAction ValidatedAction,
        string Outcome,
        RuntimeFault? RuntimeFault);

    internal sealed record ResolvedAction(
        string ActionId,
        int ActionCode,
        ImmutableArray<ActionArgument> Arguments);

    internal abstract record ActionArgument(string Kind)
    {
        internal sealed record ShotProgram(ShotProgramValue Value)
            : ActionArgument("shot-program");

        internal sealed record Direction(string Value)
            : ActionArgument("direction");

        internal sealed record UnitTarget(int TeamId, int UnitId)
            : ActionArgument("unit-target");

        internal sealed record FormTarget(string FormId)
            : ActionArgument("form-target");

        internal sealed record ProjectileHeading(string Value)
            : ActionArgument("projectile-heading");
    }

    internal sealed record RuntimeFault(
        int ParticipantId,
        ActorId ActorId,
        string Stage,
        string FaultCode,
        string CumulativeFaultCount,
        bool DisqualificationTriggered);

    internal sealed record ActionLegality(
        string ActionId,
        int ActionCode,
        bool AllowedByForm,
        bool Available,
        ImmutableArray<ActionConstraint> Constraints);

    internal abstract record ActionConstraint(string Kind)
    {
        internal sealed record ShotProgram(bool Allowed)
            : ActionConstraint("shot-program");

        internal sealed record Direction(
            ImmutableArray<string> AllowedValues)
            : ActionConstraint("direction");

        internal sealed record UnitTarget(
            ImmutableArray<UnitTargetValue> AllowedValues)
            : ActionConstraint("unit-target");

        internal sealed record FormTarget(
            ImmutableArray<string> AllowedFormIds)
            : ActionConstraint("form-target");

        internal sealed record ProjectileHeading(
            ImmutableArray<string> AllowedValues)
            : ActionConstraint("projectile-heading");
    }

    internal sealed record UnitTargetValue(
        int TeamId,
        int UnitId);

    internal abstract record EventPayload(string Kind)
    {
        internal sealed record Rotation(
            ActorId ActorId,
            ResolvedAction Action,
            PositionValue Position,
            string FromFacing,
            string ToFacing) : EventPayload("rotation");

        internal sealed record Movement(
            ActorId ActorId,
            ResolvedAction Action,
            PositionValue From,
            PositionValue To,
            string Facing) : EventPayload("movement");

        internal sealed record MovementBlocked(
            ActorId ActorId,
            ResolvedAction Action,
            PositionValue From,
            PositionValue AttemptedTo,
            string Facing) : EventPayload("movement-blocked");

        internal sealed record Attack(
            ActorId ActorId,
            ResolvedAction Action,
            string ProjectileId,
            PositionValue Origin,
            string Heading) : EventPayload("attack");

        internal sealed record Damage(
            int SourceTeamId,
            ActorId? SourceActorId,
            ActorId TargetActorId,
            string ProjectileId,
            int Amount,
            int NewHealth,
            PositionValue Position) : EventPayload("damage");

        internal sealed record ProjectileDeflected(
            int SourceTeamId,
            ActorId? SourceActorId,
            ActorId TargetActorId,
            string ProjectileId,
            string DeflectedProjectileId,
            string TargetFormId,
            string TargetFacing,
            string Heading,
            PositionValue Position) : EventPayload("projectile-deflected");

        internal sealed record Destruction(
            ActorId ActorId,
            int? SourceTeamId,
            ActorId? SourceActorId,
            string? ProjectileId,
            int Generation,
            string FormId,
            PositionValue Position) : EventPayload("destruction");

        internal sealed record LifeSpawned(
            ActorId ActorId,
            int ParticipantId,
            ActorId? ParentActorId,
            int Generation,
            string FormId,
            int Health,
            PositionValue Position,
            string Reason,
            string? SourceTransitionId,
            string? SourceOperationId) : EventPayload("life-spawned");

        internal sealed record LifeRetired(
            ActorId ActorId,
            int Generation,
            string FormId,
            PositionValue Position,
            string Reason,
            string? SourceTransitionId,
            string? SourceOperationId) : EventPayload("life-retired");

        internal sealed record RuntimeFaultValue(RuntimeFault Fault)
            : EventPayload("runtime-fault");

        internal sealed record Participant(
            int ParticipantId,
            int TeamId) : EventPayload("participant");

        internal sealed record Lifecycle(
            string TransitionId,
            string OperationId,
            ActorId SourceActorId,
            int? TargetTeamId,
            int? TargetUnitId,
            int? DueTick,
            string? CancellationReason) : EventPayload("lifecycle");

        /// <summary>
        /// <paramref name="Reason"/> is null for a requested transition and
        /// omitted from the canonical document, so every replay written
        /// before automatic returns existed stays byte-identical; a reader
        /// refuses an explicitly-inert <c>"requested"</c> as a second
        /// encoding (DECISIONS #156's additive discipline).
        /// </summary>
        internal sealed record FormTransition(
            ActorId ActorId,
            string TransitionId,
            string OperationId,
            string FromFormId,
            string ToFormId,
            int StartedTick,
            int DueTick,
            string? Reason = null) : EventPayload("form-transition");

        internal sealed record ScoreChanged(
            int TeamId,
            string Channel,
            string NewValue) : EventPayload("score-changed");

        internal sealed record ModeChanged(ModeState State)
            : EventPayload("mode-changed");

        internal sealed record LifecycleClockCancelled(
            int TargetTeamId,
            int TargetUnitId,
            UnitSlotState CancelledState,
            string CancellationReason)
            : EventPayload("lifecycle-clock-cancelled");
    }

    internal sealed record AuthoritativeEvent(
        string EventHandle,
        int Tick,
        string GlobalOrdinal,
        int SourceOrdinal,
        string Kind,
        EventPayload Payload,
        EventAudience Audience);

    internal abstract record EventAudience(string Kind)
    {
        internal sealed record Public() : EventAudience("public");

        internal sealed record Spatial(PositionValue PrimaryPosition)
            : EventAudience("spatial");

        internal sealed record TeamPrivate(int TeamId)
            : EventAudience("team-private");
    }

    internal sealed record ProjectileTraversal(
        int Tick,
        string GlobalOrdinal,
        string Phase,
        string Trigger,
        string ProjectileId,
        int OwnerParticipantId,
        int OwnerTeamId,
        ActorId OwnerActorId,
        string AttackProfileId,
        PositionValue From,
        ImmutableArray<PositionValue> Path,
        string LaunchHeading,
        string FinalHeading,
        ShotProgramValue? ShotProgram,
        TraversalTerminal Terminal);

    internal abstract record TraversalTerminal(string Kind)
    {
        internal sealed record Retained()
            : TraversalTerminal("retained");

        internal sealed record WallOrPathExhausted()
            : TraversalTerminal("wall-or-path-exhausted");

        internal sealed record RangeExhausted()
            : TraversalTerminal("range-exhausted");

        internal sealed record ActorContact(
            ActorId TargetActorId,
            bool AppliedDamage) : TraversalTerminal("actor-contact");

        internal sealed record MovementContact(
            ActorId TargetActorId,
            bool AppliedDamage) : TraversalTerminal("movement-contact");

        internal sealed record LifecyclePlacementPurge(
            PositionValue Position)
            : TraversalTerminal("lifecycle-placement-purge");

        internal sealed record ParticipantDisqualification(
            int ParticipantId)
            : TraversalTerminal("participant-disqualification");
    }

    internal sealed record WorldState(
        string MatchContractFingerprint,
        int NextTick,
        string NextProjectileId,
        ImmutableArray<ParticipantStatus> Participants,
        ImmutableArray<SlotState> Slots,
        ImmutableArray<LifeState> ActiveLives,
        ImmutableArray<PendingReplication> PendingReplications,
        ImmutableArray<ProjectileState> Projectiles,
        Scoreboard Scoreboard,
        ModeState Mode);

    internal sealed record SlotState(
        int TeamId,
        int UnitId,
        int ParticipantId,
        int NextLifeId,
        UnitSlotState State,
        ActorId? PendingParentActorId,
        PendingReplication? SplitReservation);

    internal sealed record LifeState(
        ActorId ActorId,
        int ParticipantId,
        int Generation,
        string FormId,
        PositionValue Position,
        string Facing,
        int Health,
        int Cooldown,
        int? Energy,
        int SpawnedAtTick,
        string SpawnReason,
        ActorId? ParentActorId,
        string? SourceTransitionId,
        string? SourceOperationId,
        ActionResolution? PreviousActionResolution,
        PendingSameLifeTransition? PendingSameLifeTransition);

    internal sealed record PendingReplication(
        ActorId SourceActorId,
        int ParticipantId,
        int SourceGeneration,
        string SourceFormId,
        PositionValue SourcePosition,
        string SourceFacing,
        string TransitionId,
        string OperationId,
        int QueuedTick,
        int DueTick,
        ImmutableArray<ReservedDescendant> Descendants);

    internal sealed record ReservedDescendant(
        int TeamId,
        int UnitId,
        string FormId,
        int Generation,
        PositionValue Position);

    internal sealed record ProjectileState(
        string ProjectileId,
        int OwnerParticipantId,
        int OwnerTeamId,
        ActorId OwnerActorId,
        string AttackProfileId,
        int SpawnedAtTick,
        PositionValue Origin,
        PositionValue Position,
        string LaunchHeading,
        string Heading,
        ShotProgramValue? ShotProgram,
        ImmutableArray<PositionValue> CommittedPath,
        int NextPathIndex,
        int RemainingTiles,
        int TicksUntilAdvance);

    internal sealed record Scoreboard(
        ImmutableArray<TeamScore> Teams);

    internal sealed record TeamScore(
        int TeamId,
        bool Eligible,
        ImmutableArray<ScoreValue> Scores);

    internal sealed record ScoreValue(
        string Channel,
        string Value);

    internal abstract record ModeState(
        string Kind,
        string ModeId)
    {
        internal sealed record Deathmatch(string Id)
            : ModeState("deathmatch", Id);

        internal sealed record Frontline(
            string Id,
            int ActivePositionIndex,
            int? ClaimingTeamId,
            int CaptureProgress,
            int DecayTicksElapsed,
            int ControlResumesAtTick,
            int? HoldOwnerTeamId,
            int? HoldEndsAtTick)
            : ModeState("frontline", Id);
    }

    internal sealed record MatchResult(
        string CompletionReason,
        int? EndTick,
        Standings Standings,
        ImmutableArray<int> EligibleTeamIds,
        ImmutableArray<UnitTerminalFact> Units,
        ModeResult Mode);

    internal sealed record Standings(
        int? WinnerTeamId,
        ImmutableArray<TeamStanding> Teams);

    internal sealed record TeamStanding(
        int TeamId,
        int Rank,
        string Outcome,
        ImmutableArray<ScoreValue> Scores);

    internal sealed record UnitTerminalFact(
        SlotState Slot,
        LifeState? ActiveLife);

    internal abstract record ModeResult(string Kind)
    {
        internal sealed record Deathmatch(
            string Reason,
            ImmutableArray<DeathmatchTeamScore> Scores)
            : ModeResult("deathmatch");

        internal sealed record Frontline(
            string Reason,
            ModeState.Frontline Control,
            ImmutableArray<FrontlineTeamScore> Scores)
            : ModeResult("frontline");
    }

    internal sealed record DeathmatchTeamScore(
        int TeamId,
        string Kills,
        string Deaths,
        string DamageDealt);

    internal sealed record FrontlineTeamScore(
        int TeamId,
        string TerritorialProgress);
}
