namespace BotArena.Sdk;

/// <summary>
/// THE RESERVED FIELD-ID ALLOCATION for the mind profile
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §4.5, §9, §10, §11, §12;
/// DECISIONS #191 P0).
/// <para>
/// This is the one irreversible decision in the whole build plan. Everything
/// else in the memo can be built late; a reused field ID cannot be taken back,
/// because RUNTIME-PROTOCOL.md's versioning rule is explicit that "reusing a
/// field ID, changing its meaning... requires a new version". The IDs below are
/// therefore allocated NOW and consumed later: P2 ships the NBV2 codecs that
/// encode them, P3 the replay projection, and nothing in P0/P1 writes a mind
/// frame at all.
/// </para>
/// <para>
/// Framing is unchanged: 12-byte NBV2 header, tagged fields (uint16 id, int32
/// length, bytes), collections as int32 count plus length-delimited items,
/// unknown field IDs skipped, everything else failing closed. Every nested
/// record type is the EXISTING SDK type encoded by the EXISTING codec — that
/// reuse is what makes the §7.2 null pin checkable field by field.
/// </para>
/// </summary>
public static class GenericMindWireFieldIds
{
    /// <summary>
    /// <c>MindStart</c> (host-&gt;guest), which replaces <c>MatchStart</c> for
    /// this profile and rides the same framing message type — the profile
    /// selects the payload codec, exactly as the per-life generation already
    /// does. The memo spelled the observation and decision frames; this block
    /// is allocated beside them so every mind field ID lives in one place.
    /// <para>
    /// Two things the per-life MatchStart carries are deliberately ABSENT: the
    /// slot table's state (published every tick on
    /// <see cref="MindObservation.Slots"/>) and the life origin (a per-BODY fact
    /// on <see cref="MindBodyState.Origin"/>).
    /// </para>
    /// </summary>
    public static class MindStart
    {
        public const ushort SchemaVersion = 1;
        public const ushort RuntimeContractVersion = 2;
        public const ushort ParticipantId = 3;
        public const ushort TeamId = 4;
        /// <summary>Derived in the PARTICIPANT domain, not the life domain.</summary>
        public const ushort MindRandomSeed = 5;
        /// <summary>The team seed, unchanged; only its consumer moved.</summary>
        public const ushort TeamRandomSeed = 6;
        /// <summary>
        /// The Engine-authored canonical contract JSON, byte-identical to the
        /// per-life generation's — which is what keeps the rules fingerprint
        /// valid across both profiles.
        /// </summary>
        public const ushort Contract = 7;
        /// <summary>Empty in head-to-head and free-for-all; the team-format hook.</summary>
        public const ushort AlliedParticipantIds = 8;
    }

    /// <summary>
    /// <c>MindObservation</c> (host-&gt;guest), which replaces
    /// <c>Observation</c> for this profile. The 10..18 block is delivered ONCE
    /// per tick per participant instead of once per life — the union-once win
    /// (§4.6) — and 20..21 are the per-body and per-slot blocks.
    /// </summary>
    public static class MindObservation
    {
        public const ushort SchemaVersion = 1;
        public const ushort Tick = 2;
        public const ushort MatchContractFingerprint = 3;

        // ---- delivered ONCE -------------------------------------------
        /// <summary>Allied MINDS' bodies. Empty in head-to-head by construction.</summary>
        public const ushort Allies = 10;
        public const ushort Enemies = 11;
        public const ushort VisibleTiles = 12;
        /// <summary>Optional: absent means the capability is absent.</summary>
        public const ushort VisibleProjectiles = 13;
        public const ushort VisibleEvents = 14;
        /// <summary>Optional: absent means no sensor on the team can hear.</summary>
        public const ushort HeardSounds = 15;
        public const ushort Scoreboard = 16;
        /// <summary>Tagged union: Frontline / Deathmatch.</summary>
        public const ushort Mode = 17;
        public const ushort Participants = 18;

        // ---- per body / per slot --------------------------------------
        public const ushort Bodies = 20;
        /// <summary>
        /// Published EVERY TICK, not only at start (§13.2). It costs a few
        /// hundred bytes against a ~16 KB observation and it is the single
        /// thing v1 must do to keep a draft phase a feature rather than a
        /// migration.
        /// </summary>
        public const ushort Slots = 21;

        // ---- reserved (§11) -------------------------------------------
        /// <summary>
        /// RESERVED. The engine writes an always-empty collection in v1; the
        /// field ID is the only part of inter-mind intents that is expensive
        /// to retrofit.
        /// </summary>
        public const ushort AlliedIntents = 30;
    }

    /// <summary>
    /// <c>MindBodyState</c>, one entry in
    /// <see cref="MindObservation.Bodies"/>. Fields 1..13 are byte-identical
    /// in meaning and order to the existing shared body encoding used by
    /// <c>ObservedSelfState</c>/<c>ObservedAllyState</c>, so a mind body and a
    /// per-life self encode the same facts the same way. 14..19 are the facts
    /// the mind is entitled to and a per-life bot was not (§2.3).
    /// </summary>
    public static class MindBodyState
    {
        public const ushort ActorId = 1;
        public const ushort Generation = 2;
        public const ushort FormId = 3;
        public const ushort Position = 4;
        public const ushort Facing = 5;
        public const ushort Health = 6;
        public const ushort Cooldown = 7;
        public const ushort Energy = 8;
        public const ushort PreviousActionResolution = 9;
        public const ushort PendingSameLifeTransition = 10;
        /// <summary>Per-BODY chassis (§9), not per participant.</summary>
        public const ushort ClassId = 11;
        public const ushort RouteCooldowns = 12;
        public const ushort CarriedScrap = 13;

        /// <summary>Null on a life's first tick.</summary>
        public const ushort PreviousPosition = 14;
        /// <summary>The top-ranked platform ask of wave 8 (8/8 authors).</summary>
        public const ushort MovedLastTick = 15;
        public const ushort LifeStartedTick = 16;
        /// <summary>Was <c>StartLife.Origin</c>; a per-body fact under the mind.</summary>
        public const ushort Origin = 17;
        /// <summary>RESERVED for role tags (§12): the currently published label.</summary>
        public const ushort RoleTag = 18;
        public const ushort ActionLegalities = 19;
    }

    /// <summary>
    /// <c>MindSlotState</c>, one entry in <see cref="MindObservation.Slots"/>.
    /// <see cref="State"/> is the existing closed union (Active /
    /// AvailabilityPending / AutomaticReturnPending / Ready /
    /// FabricationPending / ReplicationPending / PermanentlyDormant).
    /// </summary>
    public static class MindSlotState
    {
        public const ushort UnitId = 1;
        public const ushort State = 2;
        /// <summary>RESERVED, per-slot chassis (§9.2). Absent on a classless ruleset.</summary>
        public const ushort ClassId = 3;
        /// <summary>
        /// RESERVED, candidate chassis (§10.2). Empty means the slot's chassis
        /// is <c>fixed</c>, which is the only kind v1 admits.
        /// </summary>
        public const ushort CandidateClassIds = 4;
        /// <summary>
        /// RESERVED, candidate chassis (§10.2). Null until a chassis has been
        /// chosen at activation; always null in v1.
        /// </summary>
        public const ushort SelectedClassId = 5;
    }

    /// <summary>
    /// <c>MindDecisions</c> (guest-&gt;host), which replaces <c>Decision</c>
    /// for this profile. A stale echoed <see cref="Tick"/> fails the exchange.
    /// </summary>
    public static class MindDecisions
    {
        public const ushort SchemaVersion = 1;
        public const ushort Tick = 2;
        /// <summary>
        /// Optional MIND-scoped diagnostic text, at most 4 KiB. Allocated in P2
        /// beside the P0 block: a mind reasons once per tick over the whole
        /// army, so its diagnostics have no per-body home the way a per-life
        /// bot's did.
        /// </summary>
        public const ushort DebugMessage = 3;
        public const ushort Commands = 10;
        /// <summary>
        /// RESERVED (§11). A non-empty submission is <c>Rejected</c> —
        /// recorded, non-fatal — until a format with allied minds is admitted.
        /// </summary>
        public const ushort Intents = 20;
    }

    /// <summary>
    /// One entry in <see cref="MindDecisions.Commands"/>. Two commands naming
    /// the same <c>(unitId, lifeId)</c> are <c>Faulted</c>; a command naming a
    /// body the participant does not own, or one that is not live this tick,
    /// is <c>Rejected</c> (§2.4).
    /// </summary>
    public static class MindCommand
    {
        public const ushort UnitId = 1;
        public const ushort LifeId = 2;
        public const ushort ActionId = 3;
        public const ushort ActionCode = 4;
        public const ushort Arguments = 5;
        /// <summary>
        /// RESERVED for role tags (§12). Canonical lowercase kebab semantic
        /// ID, at most
        /// <see cref="GenericMindContractVersions.MaxRoleTagUtf8Bytes"/> UTF-8
        /// bytes. Absent field = unchanged; empty string = clear.
        /// </summary>
        public const ushort RoleTag = 6;
        /// <summary>At most 4 KiB, exactly as today.</summary>
        public const ushort DebugMessage = 7;
    }

    /// <summary>
    /// RESERVED (§11.1). One entry in <see cref="MindDecisions.Intents"/>,
    /// guest-&gt;host. At most
    /// <see cref="GenericMindContractVersions.MaxDeclaredIntentsPerTick"/>
    /// entries.
    /// </summary>
    public static class DeclaredIntent
    {
        public const ushort TagId = 1;
        public const ushort Value = 2;
    }

    /// <summary>
    /// RESERVED (§11.1). One entry in
    /// <see cref="MindObservation.AlliedIntents"/>, host-&gt;guest, carrying
    /// the PREVIOUS tick's declaration so the frozen-observation invariant
    /// survives intact.
    /// </summary>
    public static class AlliedIntent
    {
        public const ushort ParticipantId = 1;
        public const ushort TagId = 2;
        public const ushort Value = 3;
    }
}
