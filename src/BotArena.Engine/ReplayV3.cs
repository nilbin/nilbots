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
        string? ProjectileLookId,
        string? MindDataHash = null);

    internal sealed record ReplayInitialFrame(
        WorldState State,
        ImmutableArray<LifeStart> LifeStarts,
        ImmutableArray<AuthoritativeEvent> Events);

    /// <summary>
    /// One resolved tick. A document carries EXACTLY ONE of
    /// <paramref name="ActorTurns"/> / <paramref name="MindTurns"/>, decided by
    /// the contract profile in its header — there is no mixed document and no
    /// inference from the payload
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1).
    /// </summary>
    internal sealed record TickFrame(
        int Tick,
        TickStart TickStart,
        ImmutableArray<AuthoritativeEvent> Events,
        ImmutableArray<ProjectileTraversal> Traversals,
        WorldState PostState,
        // Both turn kinds are OPTIONAL on the reader, and exactly one is
        // present on any real document: the reader must be able to decode a
        // per-life tick that has never heard of mind turns and a mind tick
        // that deliberately has no actor turns. The canonical WRITER, and the
        // envelope validator, are what refuse a document carrying neither or
        // both.
        ImmutableArray<ActorTurn> ActorTurns = default,
        ImmutableArray<MindTurn> MindTurns = default);

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

    /// <summary>
    /// One participant's complete tick under the mind profile
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1): the
    /// union-once observation delivered ONCE instead of once per body, every
    /// command the mind wrote with the host's admission verdict, and one
    /// resolution per own live body — including the bodies it never named,
    /// which carry the synthetic Wait. That last part preserves the property
    /// the per-life format had, that every body's tick is accounted for, which
    /// both the validator and the ML story depend on.
    /// </summary>
    /// <param name="FuelBudget">
    /// <c>250M + 200M x liveBodyCount</c>, a pure function of authoritative
    /// tick-start state, so the validator re-derives it rather than trusting it
    /// (§4.2).
    /// </param>
    /// <param name="DebugMessage">
    /// The mind's own diagnostic text for the tick. It belongs to the TURN and
    /// not to a command because a mind reasons once per tick over the whole
    /// army — and on a tick where it owns no live body, the turn is the only
    /// thing there is to hang it on.
    /// </param>
    internal sealed record MindTurn(
        int Tick,
        int ParticipantId,
        int TeamId,
        string FuelBudget,
        int LiveBodyCount,
        MindObservation Observation,
        ImmutableArray<MindCommand> Commands,
        ImmutableArray<MindBodyResolution> Resolutions,
        ImmutableArray<MindIntent> Intents,
        MindRuntimeFault? RuntimeFault,
        string? DebugMessage = null);

    /// <summary>
    /// One command the mind wrote, with what the host did with it. Both halves
    /// are recorded and neither is elided: a Rejected command naming a body
    /// that died this tick is legitimate evidence, not a malformed document.
    /// </summary>
    internal sealed record MindCommand(
        int UnitId,
        int LifeId,
        string ActionId,
        int ActionCode,
        ImmutableArray<RawActionArgument?> Arguments,
        string Outcome,
        string? RoleTag = null,
        string? DebugMessage = null);

    /// <summary>
    /// One own live body's authoritative tick, in canonical
    /// <c>(unitId, lifeId)</c> order.
    /// </summary>
    internal sealed record MindBodyResolution(
        int UnitId,
        int LifeId,
        SubmittedDecision? SubmittedDecision,
        ActionResolution ActionResolution);

    /// <summary>RESERVED (§11.3): recorded and Rejected, never delivered.</summary>
    internal sealed record MindIntent(
        string TagId,
        string Value);

    /// <summary>
    /// A participant-scoped fault. Its <paramref name="ActorId"/> is null when
    /// the mind held no live body — a mind that traps on a tick it owns nothing
    /// still traps, and under a threshold-0 contract that is the frame where it
    /// forgot the match and lost it (§4.7).
    /// </summary>
    internal sealed record MindRuntimeFault(
        int ParticipantId,
        int TeamId,
        ActorId? ActorId,
        string Stage,
        string FaultCode,
        string CumulativeFaultCount,
        bool DisqualificationTriggered);

    /// <summary>
    /// The exact mind observation, with the team-shared union carried once.
    /// Every nested record is the existing per-life shape, unchanged, which is
    /// what makes the null pin checkable field by field (§4.5).
    /// </summary>
    internal sealed record MindObservation(
        int SchemaVersion,
        int Tick,
        string MatchContractFingerprint,
        int ParticipantId,
        int TeamId,
        ImmutableArray<MindBody> Bodies,
        ImmutableArray<MindSlot> Slots,
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
        ImmutableArray<MindAlliedIntent> AlliedIntents);

    /// <summary>
    /// One body the mind commands: today's self field set plus the facts a mind
    /// is entitled to and a per-life bot was not (§2.3), plus P3's
    /// <paramref name="BodyRandomSeed"/> — the exact per-life stream this body
    /// would have been handed, which is what makes a wrapped bot's private
    /// tie-breaks reproduce rather than merely resemble.
    /// </summary>
    internal sealed record MindBody(
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
        PositionValue? PreviousPosition,
        bool MovedLastTick,
        int LifeStartedTick,
        LifeOrigin Origin,
        string BodyRandomSeed,
        ImmutableArray<ActionLegality> ActionLegalities,
        ImmutableArray<RouteCooldown> RouteCooldowns = default,
        int CarriedScrap = 0,
        string? RoleTag = null);

    /// <summary>
    /// One of the mind's own stable slots, published EVERY TICK rather than
    /// only at start (§13.2) — the single thing v1 must do to keep a draft
    /// phase a feature rather than a migration.
    /// </summary>
    internal sealed record MindSlot(
        int TeamId,
        int UnitId,
        UnitSlotState State,
        string? ClassId = null,
        ImmutableArray<string> CandidateClassIds = default,
        string? SelectedClassId = null);

    /// <summary>RESERVED (§11.1). Always empty in v1.</summary>
    internal sealed record MindAlliedIntent(
        int ParticipantId,
        string TagId,
        string Value);

    internal sealed record LifeStart(
        int SchemaVersion,
        int RuntimeContractVersion,
        ActorId ActorId,
        int ParticipantId,
        string ActorRandomSeed,
        LifeOrigin Origin,
        string MatchContractFingerprint,
        // Trailing additive key (#156). Every engine-authored document since
        // the team stream landed carries it; a document written before it did
        // decodes as null and still verifies.
        string? TeamRandomSeed = null);

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
        ImmutableArray<RouteCooldown> RouteCooldowns = default,
        int CarriedScrap = 0);

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
        ImmutableArray<RouteCooldown> RouteCooldowns = default,
        int CarriedScrap = 0,
        // Trailing additive key (#156 discipline, §12): written only when a
        // mind has labelled the body, so every per-life document stays
        // byte-identical.
        string? RoleTag = null);

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
        string? ClassId,
        int CarriedScrap = 0,
        // Public on visible enemies by design (§12.2), and trailing-additive
        // so a document without one is byte-identical to yesterday's.
        string? RoleTag = null);

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

        internal sealed record UpgradeTrack(string? TrackId)
            : RawActionArgument("upgrade-track");

        internal sealed record PositionTarget(PositionValue Value)
            : RawActionArgument("position-target");
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

        internal sealed record UpgradeTrack(string TrackId)
            : ActionArgument("upgrade-track");

        internal sealed record PositionTarget(PositionValue Value)
            : ActionArgument("position-target");
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

        internal sealed record UpgradeTrack(
            ImmutableArray<string> AllowedTrackIds)
            : ActionConstraint("upgrade-track");

        internal sealed record PositionTarget(
            ImmutableArray<PositionValue> AllowedValues)
            : ActionConstraint("position-target");
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

        /// <summary>
        /// A participant-scoped mind fault with no body to attribute it to
        /// (P3, §4.7). Emitted only when the mind held no live body, so no
        /// per-life document ever carries this payload.
        /// </summary>
        internal sealed record MindRuntimeFaultValue(MindRuntimeFault Fault)
            : EventPayload("mind-runtime-fault");

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

        internal sealed record ArcRelay(ArcRelayFact Fact)
            : EventPayload("arc-relay");
    }

    internal abstract record ArcRelayFact(string Kind)
    {
        internal sealed record CoreBorn(
            ArcCoreId CoreId,
            PositionValue Position) : ArcRelayFact("core-born")
        {
            /// <summary>Charge at birth; 1 outside charge-value rulesets.</summary>
            public int ChargeValue { get; init; } = 1;
        }

        internal sealed record CoreRipened(
            ArcCoreId CoreId,
            PositionValue Position,
            int Value) : ArcRelayFact("core-ripened");

        internal sealed record CorePickedUp(
            ArcCoreId CoreId,
            ActorId CarrierActorId,
            PositionValue Position,
            int NextRelocationTick) : ArcRelayFact("core-picked-up");

        internal sealed record CoreRelocated(
            ArcCoreId CoreId,
            ActorId? CarrierActorId,
            PositionValue From,
            PositionValue To,
            int NextRelocationTick,
            string RelocationKind) : ArcRelayFact("core-relocated");

        internal sealed record CoreHandedOff(
            ArcCoreId CoreId,
            ActorId SourceActorId,
            ActorId TargetActorId,
            PositionValue Position,
            int NextRelocationTick) : ArcRelayFact("core-handed-off");

        internal sealed record CoreDropped(
            ArcCoreId CoreId,
            ActorId SourceActorId,
            PositionValue Position,
            int NextRelocationTick,
            string DropKind) : ArcRelayFact("core-dropped");

        internal sealed record CoreBanked(
            ArcCoreId CoreId,
            ActorId CarrierActorId,
            int TeamId,
            PositionValue Position,
            int ChargePips) : ArcRelayFact("core-banked");

        internal sealed record WellChanged(
            string WellId,
            bool PendingCharge,
            int? RearmCompletesAtTick,
            ArcCoreId? OutstandingCoreId) : ArcRelayFact("well-changed");

        internal sealed record Pulse(
            int TeamId,
            int PulseOrdinal,
            int OpposingReactorIntegrity) : ArcRelayFact("pulse");

        internal sealed record SignatureChanged(
            string OperationId,
            string SignatureId,
            ActorId OwnerActorId,
            string? Phase,
            string Reason) : ArcRelayFact("signature-changed");

        internal sealed record BodyRelocated(
            string OperationId,
            string SignatureId,
            ActorId OwnerActorId,
            ActorId TargetActorId,
            PositionValue From,
            PositionValue To) : ArcRelayFact("body-relocated");

        internal sealed record SignatureDamage(
            string OperationId,
            string SignatureId,
            ActorId OwnerActorId,
            ActorId TargetActorId,
            int Amount,
            int NewHealth,
            PositionValue Position) : ArcRelayFact("signature-damage");

        internal sealed record SignatureRepair(
            string OperationId,
            string SignatureId,
            ActorId OwnerActorId,
            ActorId TargetActorId,
            int Amount,
            int NewHealth,
            PositionValue Position) : ArcRelayFact("signature-repair");
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
            int? HoldEndsAtTick,
            int? SecondaryOwnerTeamId,
            int SecondaryClaimProgress,
            ImmutableArray<ScrapTeam> ScrapTeams = default,
            ImmutableArray<ScrapPile> ScrapPiles = default)
            : ModeState("frontline", Id)
        {
            /// <summary>
            /// Structural equality, spelled out because the two economy facts
            /// are <see cref="ImmutableArray{T}"/>: its own equality compares
            /// the underlying array by REFERENCE, so the synthesized record
            /// comparison would call two identical published states different
            /// and every "does the observed mode match the authoritative
            /// pre-state?" check would fail on a contract that declares an
            /// economy.
            /// </summary>
            public bool Equals(Frontline? other) =>
                other is not null
                && string.Equals(Id, other.Id, StringComparison.Ordinal)
                && ActivePositionIndex == other.ActivePositionIndex
                && ClaimingTeamId == other.ClaimingTeamId
                && CaptureProgress == other.CaptureProgress
                && DecayTicksElapsed == other.DecayTicksElapsed
                && ControlResumesAtTick == other.ControlResumesAtTick
                && HoldOwnerTeamId == other.HoldOwnerTeamId
                && HoldEndsAtTick == other.HoldEndsAtTick
                && SecondaryOwnerTeamId == other.SecondaryOwnerTeamId
                && SecondaryClaimProgress == other.SecondaryClaimProgress
                && ScrapTeams.IsDefaultOrEmpty
                    == other.ScrapTeams.IsDefaultOrEmpty
                && ScrapPiles.IsDefaultOrEmpty
                    == other.ScrapPiles.IsDefaultOrEmpty
                && (ScrapTeams.IsDefaultOrEmpty
                    || ScrapTeams.SequenceEqual(other.ScrapTeams))
                && (ScrapPiles.IsDefaultOrEmpty
                    || ScrapPiles.SequenceEqual(other.ScrapPiles));

            /// <inheritdoc />
            public override int GetHashCode()
            {
                var hash = default(HashCode);
                hash.Add(Id, StringComparer.Ordinal);
                hash.Add(ActivePositionIndex);
                hash.Add(ClaimingTeamId);
                hash.Add(CaptureProgress);
                hash.Add(DecayTicksElapsed);
                hash.Add(ControlResumesAtTick);
                hash.Add(HoldOwnerTeamId);
                hash.Add(HoldEndsAtTick);
                hash.Add(SecondaryOwnerTeamId);
                hash.Add(SecondaryClaimProgress);
                if (!ScrapTeams.IsDefaultOrEmpty)
                {
                    foreach (ScrapTeam team in ScrapTeams)
                        hash.Add(team.GetHashCode());
                }
                if (!ScrapPiles.IsDefaultOrEmpty)
                {
                    foreach (ScrapPile pile in ScrapPiles)
                        hash.Add(pile);
                }
                return hash.ToHashCode();
            }
        }

        internal sealed record ArcRelay(
            string Id,
            ImmutableArray<ArcWell> Wells,
            ImmutableArray<ArcReactor> Reactors,
            ImmutableArray<ArcCore> VisibleCores,
            ImmutableArray<ArcSignature> VisibleSignatures,
            int? LatestPulseTeamId,
            int? LatestPulseTick)
            : ModeState("arc-relay", Id)
        {
            public bool Equals(ArcRelay? other) =>
                other is not null
                && string.Equals(Id, other.Id, StringComparison.Ordinal)
                && Wells.SequenceEqual(other.Wells)
                && Reactors.SequenceEqual(other.Reactors)
                && VisibleCores.SequenceEqual(other.VisibleCores)
                && VisibleSignatures.SequenceEqual(other.VisibleSignatures)
                && LatestPulseTeamId == other.LatestPulseTeamId
                && LatestPulseTick == other.LatestPulseTick;

            public override int GetHashCode()
            {
                var hash = default(HashCode);
                hash.Add(Id, StringComparer.Ordinal);
                foreach (ArcWell value in Wells) hash.Add(value);
                foreach (ArcReactor value in Reactors) hash.Add(value);
                foreach (ArcCore value in VisibleCores) hash.Add(value);
                foreach (ArcSignature value in VisibleSignatures)
                    hash.Add(value);
                hash.Add(LatestPulseTeamId);
                hash.Add(LatestPulseTick);
                return hash.ToHashCode();
            }
        }
    }

    internal sealed record ArcCoreId(string SourceWellId, int SourceOrdinal);

    internal sealed record ArcWell(
        string WellId,
        PositionValue Position,
        int? NextScheduledBirthTick,
        ArcCoreId? OutstandingCoreId,
        bool PendingCharge,
        int? RearmCompletesAtTick);

    internal sealed record ArcReactor(
        int TeamId,
        PositionValue Position,
        int ChargePips,
        int IntegritySegments)
    {
        /// <summary>Threefold sockets; empty outside threefold rulesets.</summary>
        public ImmutableArray<string> FilledSocketWellIds { get; init; } = [];

        public bool Equals(ArcReactor? other) =>
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

    internal sealed record ArcCore(
        ArcCoreId CoreId,
        PositionValue Position,
        string Disposition,
        ActorId? CarrierActorId,
        int NextRelocationTick,
        PositionValue? FlightTarget,
        int? FlightCompletesAtTick)
    {
        /// <summary>Charge this Core banks for; 1 outside charge-value rulesets.</summary>
        public int ChargeValue { get; init; } = 1;
    }

    internal sealed record ArcSignature(
        string OperationId,
        string SignatureId,
        string SignatureKind,
        ActorId OwnerActorId,
        int OwnerTeamId,
        string Phase,
        int StartedTick,
        int? CompletesAtTick,
        int? EndsAtTick,
        ImmutableArray<PositionValue> Positions,
        ActorId? TargetActorId,
        int RemainingCapacity,
        bool Suppressed)
    {
        public bool Equals(ArcSignature? other) =>
            other is not null
            && string.Equals(OperationId, other.OperationId,
                StringComparison.Ordinal)
            && string.Equals(SignatureId, other.SignatureId,
                StringComparison.Ordinal)
            && string.Equals(SignatureKind, other.SignatureKind,
                StringComparison.Ordinal)
            && OwnerActorId == other.OwnerActorId
            && OwnerTeamId == other.OwnerTeamId
            && string.Equals(Phase, other.Phase, StringComparison.Ordinal)
            && StartedTick == other.StartedTick
            && CompletesAtTick == other.CompletesAtTick
            && EndsAtTick == other.EndsAtTick
            && Positions.SequenceEqual(other.Positions)
            && TargetActorId == other.TargetActorId
            && RemainingCapacity == other.RemainingCapacity
            && Suppressed == other.Suppressed;

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(OperationId, StringComparer.Ordinal);
            hash.Add(SignatureId, StringComparer.Ordinal);
            hash.Add(SignatureKind, StringComparer.Ordinal);
            hash.Add(OwnerActorId);
            hash.Add(OwnerTeamId);
            hash.Add(Phase, StringComparer.Ordinal);
            hash.Add(StartedTick);
            hash.Add(CompletesAtTick);
            hash.Add(EndsAtTick);
            foreach (PositionValue position in Positions) hash.Add(position);
            hash.Add(TargetActorId);
            hash.Add(RemainingCapacity);
            hash.Add(Suppressed);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// One team's published economic position. Serialized only on a ruleset
    /// that declares an economy, so a contract without one never carries the
    /// key.
    /// </summary>
    internal sealed record ScrapTeam(
        int TeamId,
        int Bank,
        ImmutableArray<int> TierLevels)
    {
        /// <summary>Structural equality: the tier vector is an array.</summary>
        public bool Equals(ScrapTeam? other) =>
            other is not null
            && TeamId == other.TeamId
            && Bank == other.Bank
            && TierLevels.IsDefaultOrEmpty == other.TierLevels.IsDefaultOrEmpty
            && (TierLevels.IsDefaultOrEmpty
                || TierLevels.SequenceEqual(other.TierLevels));

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = default(HashCode);
            hash.Add(TeamId);
            hash.Add(Bank);
            if (!TierLevels.IsDefaultOrEmpty)
            {
                foreach (int tier in TierLevels)
                    hash.Add(tier);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>One live pile of loose scrap.</summary>
    internal sealed record ScrapPile(
        PositionValue Position,
        int Amount,
        int ExpiresAtTick);

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

        internal sealed record ArcRelay(
            string Reason,
            ModeState.ArcRelay State)
            : ModeResult("arc-relay");
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
