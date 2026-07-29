using System.Collections.Immutable;

#if BOTARENA_ACTOR_CONTRACTS
namespace BotArena.ActorContracts;
#else
namespace BotArena.Sdk;
#endif

/// <summary>
/// Complete immutable SDK view of the generation-3 generic actor rules.
/// Extensible contract branches are discriminated records; fixed semantic
/// policy identifiers remain explicit strings so bots can inspect them.
/// </summary>
#if BOTARENA_ACTOR_CONTRACTS
internal sealed class GenericActorRulesContract
#else
public sealed class GenericActorRulesContract
#endif
{
    internal GenericActorRulesContract(
        int schemaVersion,
        string rulesetId,
        string rulesFingerprint,
        RulesLimits limits,
        SeedMechanicsDefinition seedMechanics,
        GameModeDefinition gameMode,
        LifecycleDefinition lifecycle,
        ImmutableArray<Form> forms,
        ImmutableArray<MovementProfile> movementProfiles,
        ImmutableArray<VisionProfile> visionProfiles,
        ImmutableArray<AttackProfile> attackProfiles,
        ImmutableArray<ActionDefinition> actions,
        ImmutableArray<FabricationTransition> fabricationTransitions,
        ImmutableArray<SameLifeTransition> sameLifeTransitions,
        ImmutableArray<ReplicationTransition> replicationTransitions,
        TeamPerceptionDefinition teamPerception,
        CollisionRules collisions,
        TickResolutionDefinition tickResolution)
    {
        SchemaVersion = schemaVersion;
        RulesetId = rulesetId;
        RulesFingerprint = rulesFingerprint;
        Limits = limits;
        SeedMechanics = seedMechanics;
        GameMode = gameMode;
        Lifecycle = lifecycle;
        Forms = forms;
        MovementProfiles = movementProfiles;
        VisionProfiles = visionProfiles;
        AttackProfiles = attackProfiles;
        Actions = actions;
        FabricationTransitions = fabricationTransitions;
        SameLifeTransitions = sameLifeTransitions;
        ReplicationTransitions = replicationTransitions;
        TeamPerception = teamPerception;
        Collisions = collisions;
        TickResolution = tickResolution;
    }

    /// <summary>Typed rules-contract schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Stable authored ruleset identifier.</summary>
    public string RulesetId { get; }
    /// <summary>Fingerprint of the canonical rules definition.</summary>
    public string RulesFingerprint { get; }
    /// <summary>Match and runtime-fault bounds.</summary>
    public RulesLimits Limits { get; }
    /// <summary>Deterministic seed, runtime lifetime, and memory policies.</summary>
    public SeedMechanicsDefinition SeedMechanics { get; }
    /// <summary>Selected objective, scoring channels, and victory policy.</summary>
    public GameModeDefinition GameMode { get; }
    /// <summary>Body-life destruction, return, and generation semantics.</summary>
    public LifecycleDefinition Lifecycle { get; }
    /// <summary>Complete form catalog keyed by <see cref="Form.Id"/>.</summary>
    public ImmutableArray<Form> Forms { get; }
    /// <summary>Complete movement-profile catalog.</summary>
    public ImmutableArray<MovementProfile> MovementProfiles { get; }
    /// <summary>Complete vision and hearing profile catalog.</summary>
    public ImmutableArray<VisionProfile> VisionProfiles { get; }
    /// <summary>Complete attack and projectile profile catalog.</summary>
    public ImmutableArray<AttackProfile> AttackProfiles { get; }
    /// <summary>Complete action catalog with stable IDs and compact codes.</summary>
    public ImmutableArray<ActionDefinition> Actions { get; }
    /// <summary>Catalog of transitions that create child lives.</summary>
    public ImmutableArray<FabricationTransition> FabricationTransitions { get; }
    /// <summary>Catalog of transformations that preserve the current life.</summary>
    public ImmutableArray<SameLifeTransition> SameLifeTransitions { get; }
    /// <summary>Catalog of transitions that replace a life with descendants.</summary>
    public ImmutableArray<ReplicationTransition> ReplicationTransitions { get; }
    /// <summary>Team observation sharing and provenance policy.</summary>
    public TeamPerceptionDefinition TeamPerception { get; }
    /// <summary>Actor, wall, movement, and projectile collision policy.</summary>
    public CollisionRules Collisions { get; }
    /// <summary>Authoritative simultaneous-tick phase and ordering policy.</summary>
    public TickResolutionDefinition TickResolution { get; }

    /// <summary>Global match and participant-runtime bounds.</summary>
    /// <param name="MaxTicks">Maximum authoritative ticks before timeout.</param>
    /// <param name="RuntimeFaults">Participant fault and disqualification policy.</param>
    public sealed record RulesLimits(
        int MaxTicks,
        RuntimeFaults RuntimeFaults);

    /// <summary>
    /// Frozen runtime-fault, recovery, disqualification, replay, and ranking
    /// semantics. String values are exact policy IDs, not display text.
    /// </summary>
    /// <param name="FaultsAllowedBeforeDisqualification">Faults tolerated before disqualification.</param>
    /// <param name="DisqualificationFaultCount">Cumulative count that triggers disqualification.</param>
    /// <param name="AccumulationScope">Fault-counter ownership and lifetime policy ID.</param>
    /// <param name="FaultCounterArithmetic">Counter update arithmetic policy ID.</param>
    /// <param name="FaultingDecision">Fallback-decision policy ID.</param>
    /// <param name="RuntimeStageRecovery">Runtime recovery policy ID.</param>
    /// <param name="ReplayFaultRepresentation">Replay projection policy ID.</param>
    /// <param name="FaultBatchEventOrder">Same-batch fault event ordering policy ID.</param>
    /// <param name="ApplicationTiming">Fault application timing policy ID.</param>
    /// <param name="Threshold">Threshold comparison policy ID.</param>
    /// <param name="ParticipantDisposition">Disqualified participant disposition ID.</param>
    /// <param name="PendingWorkDisposition">Pending lifecycle-work disposition ID.</param>
    /// <param name="CancellationEventOrder">Cancellation event ordering policy ID.</param>
    /// <param name="OwnedProjectileDisposition">Owned-projectile disposition policy ID.</param>
    /// <param name="ScoreDisposition">Existing-score disposition policy ID.</param>
    /// <param name="ScoringTeamEligibility">Team eligibility policy ID.</param>
    /// <param name="MatchCompletion">Disqualification match-completion policy ID.</param>
    /// <param name="FinalRanking">Disqualified ranking policy ID.</param>
    public sealed record RuntimeFaults(
        int FaultsAllowedBeforeDisqualification,
        long DisqualificationFaultCount,
        string AccumulationScope,
        string FaultCounterArithmetic,
        string FaultingDecision,
        string RuntimeStageRecovery,
        string ReplayFaultRepresentation,
        string FaultBatchEventOrder,
        string ApplicationTiming,
        string Threshold,
        string ParticipantDisposition,
        string PendingWorkDisposition,
        string CancellationEventOrder,
        string OwnedProjectileDisposition,
        string ScoreDisposition,
        string ScoringTeamEligibility,
        string MatchCompletion,
        string FinalRanking);

    /// <summary>
    /// Frozen deterministic seed derivation, identity, runtime lifetime, and
    /// private-memory isolation policies.
    /// </summary>
    /// <param name="SeedProfileId">Seed algorithm profile identifier.</param>
    /// <param name="SeedDerivation">Exact seed derivation policy ID.</param>
    /// <param name="LifeIdentityAssignment">Life-identity assignment policy ID.</param>
    /// <param name="RuntimeLifetime">Runtime instance lifetime policy ID.</param>
    /// <param name="PrivateMemory">Private-memory isolation policy ID.</param>
    public sealed record SeedMechanicsDefinition(
        string SeedProfileId,
        string SeedDerivation,
        string LifeIdentityAssignment,
        string RuntimeLifetime,
        string PrivateMemory);

    /// <summary>One authoritative score channel and its numeric domain policy.</summary>
    /// <param name="Channel">Stable channel identifier.</param>
    /// <param name="Domain">Declared value-domain policy ID.</param>
    public sealed record ScoreChannel(string Channel, string Domain);

    /// <summary>One ordered timeout-ranking key.</summary>
    /// <param name="Channel">Score channel to compare.</param>
    /// <param name="Direction">Declared ascending/descending policy ID.</param>
    public sealed record ScoreRanking(string Channel, string Direction);

    /// <summary>Closed union of terminal and timeout victory policies.</summary>
    /// <param name="Kind">Victory-policy discriminator.</param>
    /// <param name="TimeoutRanking">
    /// Lexicographic score keys used when no earlier terminal condition wins.
    /// </param>
    public abstract record Victory(
        VictoryKind Kind,
        ImmutableArray<ScoreRanking> TimeoutRanking);

    /// <summary>Deathmatch terminal and timeout victory policy.</summary>
    /// <param name="TimeoutRanking">Lexicographic timeout score keys.</param>
    /// <param name="KillsToWin">
    /// Optional early kill threshold; <see langword="null"/> disables it.
    /// </param>
    /// <param name="TerminalTickPrecedence">
    /// Policy ID resolving simultaneous threshold crossings.
    /// </param>
    public sealed record DeathmatchVictory(
        ImmutableArray<ScoreRanking> TimeoutRanking,
        int? KillsToWin,
        string TerminalTickPrecedence)
        : Victory(VictoryKind.Deathmatch, TimeoutRanking);

    /// <summary>Frontline breach and timeout victory policy.</summary>
    /// <param name="TimeoutRanking">Lexicographic timeout score keys.</param>
    /// <param name="PushesToBreach">Objective advances required for a breach.</param>
    public sealed record FrontlineVictory(
        ImmutableArray<ScoreRanking> TimeoutRanking,
        int PushesToBreach)
        : Victory(VictoryKind.Frontline, TimeoutRanking);

    /// <summary>Closed union of objective, score-catalog, and victory rules.</summary>
    /// <param name="Kind">Mode discriminator.</param>
    /// <param name="ModeId">Stable authored game-mode identifier.</param>
    /// <param name="Victory">Terminal and timeout victory policy.</param>
    /// <param name="ScoreCatalog">Complete authoritative score channels.</param>
    public abstract record GameModeDefinition(
        GameModeKind Kind,
        string ModeId,
        Victory Victory,
        ImmutableArray<ScoreChannel> ScoreCatalog);

    /// <summary>Deathmatch objective and scoring definition.</summary>
    /// <param name="ModeId">Stable authored mode identifier.</param>
    /// <param name="DeathmatchVictory">Deathmatch victory policy.</param>
    /// <param name="ScoreCatalog">Complete authoritative score channels.</param>
    /// <param name="Scoring">Kill, death, damage, and health accounting policy.</param>
    public sealed record DeathmatchGameMode(
        string ModeId,
        DeathmatchVictory DeathmatchVictory,
        ImmutableArray<ScoreChannel> ScoreCatalog,
        DeathmatchScoring Scoring)
        : GameModeDefinition(
            GameModeKind.Deathmatch,
            ModeId,
            DeathmatchVictory,
            ScoreCatalog);

    /// <summary>Ordered-objective Frontline definition.</summary>
    /// <param name="ModeId">Stable authored mode identifier.</param>
    /// <param name="FrontlineVictory">Frontline victory policy.</param>
    /// <param name="ScoreCatalog">Complete authoritative score channels.</param>
    /// <param name="FrontlinePositionCount">Number of ordered objective positions.</param>
    /// <param name="Capture">Capture, decay, redeploy, and progress policy.</param>
    public sealed record FrontlineGameMode(
        string ModeId,
        FrontlineVictory FrontlineVictory,
        ImmutableArray<ScoreChannel> ScoreCatalog,
        int FrontlinePositionCount,
        FrontlineCapture Capture)
        : GameModeDefinition(
            GameModeKind.Frontline,
            ModeId,
            FrontlineVictory,
            ScoreCatalog);

    /// <summary>
    /// Frozen Deathmatch score accounting. Values are exact policy IDs that
    /// define attribution and increments.
    /// </summary>
    /// <param name="DeathIncrement">Death-channel increment policy ID.</param>
    /// <param name="KillIncrement">Kill-channel increment policy ID.</param>
    /// <param name="AlliedFinalDamage">Allied final-damage attribution policy ID.</param>
    /// <param name="DamageDealtIncrement">Damage-dealt increment policy ID.</param>
    /// <param name="ActiveHealthSnapshot">Active-health snapshot policy ID.</param>
    /// <param name="NonDamageRetirement">Non-damage retirement scoring policy ID.</param>
    /// <param name="EarlyKillLimitResolution">Early threshold resolution policy ID.</param>
    public sealed record DeathmatchScoring(
        string DeathIncrement,
        string KillIncrement,
        string AlliedFinalDamage,
        string DamageDealtIncrement,
        string ActiveHealthSnapshot,
        string NonDamageRetirement,
        string EarlyKillLimitResolution);

    /// <summary>Frontline control, capture, decay, and redeploy mechanics.</summary>
    /// <param name="Threshold">Progress required to capture one objective.</param>
    /// <param name="GainPerSoleTeamTick">
    /// Base progress gained per control-pressure unit and tick. The declared
    /// control policy defines whether pressure is binary or weight-scaled.
    /// </param>
    /// <param name="DecayAmount">Progress removed at each decay application.</param>
    /// <param name="DecayIntervalTicks">Ticks between decay applications.</param>
    /// <param name="RedeployPauseTicks">Pause after an objective advance.</param>
    /// <param name="ControlPolicy">Exact control-policy ID.</param>
    /// <param name="TimeoutPolicy">Exact timeout-policy ID.</param>
    /// <param name="TerritorialProgressFormula">Ranking formula policy ID.</param>
    /// <param name="CompletionPolicy">Capture-completion policy ID.</param>
    /// <param name="InitialPosition">Initial active-position policy ID.</param>
    /// <param name="CaptureArithmetic">Capture arithmetic policy ID.</param>
    /// <param name="OppositionArithmetic">Contested-control arithmetic policy ID.</param>
    /// <param name="DecayClock">Decay clock policy ID.</param>
    /// <param name="DisabledDecay">Disabled-decay representation policy ID.</param>
    /// <param name="RedeployPolicy">Redeploy behavior policy ID.</param>
    /// <param name="RedeployTickArithmetic">Redeploy tick arithmetic policy ID.</param>
    public sealed record FrontlineCapture(
        int Threshold,
        int GainPerSoleTeamTick,
        int DecayAmount,
        int DecayIntervalTicks,
        int RedeployPauseTicks,
        string ControlPolicy,
        string TimeoutPolicy,
        string TerritorialProgressFormula,
        string CompletionPolicy,
        string InitialPosition,
        string CaptureArithmetic,
        string OppositionArithmetic,
        string DecayClock,
        string DisabledDecay,
        string RedeployPolicy,
        string RedeployTickArithmetic)
    {
        /// <summary>
        /// Optional full capture-gain schedule. An empty array means the
        /// static <see cref="GainPerSoleTeamTick"/> applies for every tick.
        /// </summary>
        public ImmutableArray<FrontlineCaptureGainPhase> GainSchedule
        {
            get;
            init;
        } = [];

        /// <summary>
        /// How long a completed advance is protected against being pushed
        /// back, when <see cref="RedeployPolicy"/> holds a high-water mark.
        /// Zero means the field is inert and absent from the contract: the
        /// frontline can be pushed back the moment the enemy completes its
        /// own capture.
        /// </summary>
        public int RatchetHoldTicks { get; init; }

        /// <summary>
        /// Resolves the active phase from the authoritative observation tick.
        /// Static rulesets return a synthetic <c>default</c> phase.
        /// </summary>
        public FrontlineCaptureGainPhase GainPhaseAtTick(int tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));
            if (GainSchedule.IsDefaultOrEmpty)
            {
                return new FrontlineCaptureGainPhase(
                    "default",
                    StartsAtTick: 0,
                    GainPerSoleTeamTick);
            }

            FrontlineCaptureGainPhase active = GainSchedule[0];
            foreach (FrontlineCaptureGainPhase phase in GainSchedule)
            {
                if (phase.StartsAtTick > tick)
                    break;
                active = phase;
            }
            return active;
        }
    }

    /// <summary>
    /// One deterministic capture-gain phase, ordered by
    /// <see cref="StartsAtTick"/>.
    /// </summary>
    /// <param name="PhaseId">Stable semantic phase identifier.</param>
    /// <param name="StartsAtTick">First authoritative tick in this phase.</param>
    /// <param name="GainPerSoleTeamTick">
    /// Base control-pressure gain during the phase.
    /// </param>
    public sealed record FrontlineCaptureGainPhase(
        string PhaseId,
        int StartsAtTick,
        int GainPerSoleTeamTick);

    /// <summary>
    /// Lifecycle profiles plus global destruction, return, generation, and
    /// tick-order semantics. String values are exact policy IDs.
    /// </summary>
    /// <param name="Profiles">Complete named lifecycle-profile catalog.</param>
    /// <param name="DestructionClock">Destruction/return clock policy ID.</param>
    /// <param name="NewLifeSemantics">Fresh-life initialization policy ID.</param>
    /// <param name="NewLifeCombatState">Fresh cooldown and energy policy ID.</param>
    /// <param name="NewLifeResourceClock">Fresh resource-clock policy ID.</param>
    /// <param name="GenerationSemantics">Generation assignment policy ID.</param>
    /// <param name="AutomaticReturnPlacement">Automatic-return placement policy ID.</param>
    /// <param name="TickStartLifecycleOrder">Tick-start lifecycle ordering policy ID.</param>
    /// <param name="OutputTileProjectile">Output-tile projectile interaction policy ID.</param>
    public sealed record LifecycleDefinition(
        ImmutableArray<LifecycleProfile> Profiles,
        string DestructionClock,
        string NewLifeSemantics,
        string NewLifeCombatState,
        string NewLifeResourceClock,
        string GenerationSemantics,
        string AutomaticReturnPlacement,
        string TickStartLifecycleOrder,
        string OutputTileProjectile);

    /// <summary>Named destruction and automatic-return policy for unit slots.</summary>
    /// <param name="ProfileId">Stable lifecycle profile identifier.</param>
    /// <param name="DestructionPolicy">Destruction behavior policy ID.</param>
    /// <param name="DelayTicks">Ticks before the declared return behavior.</param>
    /// <param name="AutomaticReturnFormId">
    /// Return form, or <see langword="null"/> when the profile has no automatic
    /// return.
    /// </param>
    public sealed record LifecycleProfile(
        string ProfileId,
        string DestructionPolicy,
        int DelayTicks,
        string? AutomaticReturnFormId);

    /// <summary>
    /// One playable body form and its referenced movement, perception, attack,
    /// objective weight, and action capabilities.
    /// </summary>
    /// <param name="Id">Stable form identifier.</param>
    /// <param name="MaxHealth">Maximum health for an active life in this form.</param>
    /// <param name="MovementProfileId">Referenced movement-profile ID.</param>
    /// <param name="VisionProfileId">Referenced vision-profile ID.</param>
    /// <param name="AttackProfileId">
    /// Referenced attack profile, or <see langword="null"/> when the form
    /// cannot attack.
    /// </param>
    /// <param name="ObjectiveWeight">Mode-control weight contributed by the form.</param>
    /// <param name="AllowedActionIds">Complete action mask for this form.</param>
    public sealed record Form(
        string Id,
        int MaxHealth,
        string MovementProfileId,
        string VisionProfileId,
        string? AttackProfileId,
        int ObjectiveWeight,
        ImmutableArray<string> AllowedActionIds)
    {
        /// <summary>
        /// Optional defensive projectile-contact capability. Contracts that do
        /// not publish the field use
        /// <see cref="FormProjectileGuard.None"/>.
        /// </summary>
        public FormProjectileGuard ProjectileGuard { get; init; }
            = FormProjectileGuard.None;
    }

    /// <summary>
    /// What a form does with an incoming hostile projectile contact.
    /// </summary>
    public enum FormProjectileGuard
    {
        /// <summary>
        /// Contacts resolve by the collision contract alone. This is the inert
        /// default, and canonical contract bytes omit the field entirely for
        /// it — an absent field always means this value.
        /// </summary>
        None = 0,

        /// <summary>
        /// A hostile projectile approaching from inside the life's facing
        /// quadrant is consumed without damage and published as a
        /// <c>ProjectileAbsorbed</c> event; flank and rear contacts damage
        /// normally.
        /// </summary>
        FacingQuadrantContactsConsumedWithoutDamage = 1,
    }

    /// <summary>
    /// Multi-projectile launch shape for one successful attack action.
    /// </summary>
    /// <param name="ProjectileCount">Projectiles issued by one attack.</param>
    /// <param name="Spread">Heading-layout policy ID.</param>
    /// <param name="IdentityOrder">Projectile-ID assignment policy ID.</param>
    public sealed record AttackVolley(
        int ProjectileCount,
        string Spread,
        string IdentityOrder);

    /// <summary>
    /// How a movement profile couples body facing to a movement action.
    /// Facing drives vision cones and straight-shot aim, so this decides
    /// whether a step is a free strafe or a committed turn.
    /// </summary>
    public enum MovementFacingCoupling
    {
        /// <summary>
        /// Movement never changes facing; rotation is the only way to turn.
        /// This is the inert default, and canonical contract bytes omit the
        /// field entirely for it — an absent field always means this value.
        /// </summary>
        PreserveFacing = 0,

        /// <summary>
        /// A successful move sets facing to the direction moved, and the
        /// Movement event's facing carries the new value. A blocked move
        /// changes neither position nor facing. Every cardinal stays legal.
        /// </summary>
        FaceMovementDirection = 1,

        /// <summary>
        /// A life may only move where it faces: the Direction constraint on
        /// movement actions offers exactly the current facing (rotation keeps
        /// all four), and any other movement direction resolves as blocked.
        /// Movement itself does not change facing.
        /// </summary>
        FacingLocked = 2,
    }

    /// <summary>Named movement-layer capability.</summary>
    /// <param name="Id">Stable movement-profile identifier.</param>
    /// <param name="MovementLayer">Map collision/traversal layer.</param>
    public sealed record MovementProfile(
        string Id,
        GenericActorMapContract.MovementLayer MovementLayer)
    {
        /// <summary>
        /// Optional facing coupling for this profile. Contracts that do not
        /// publish the field use
        /// <see cref="MovementFacingCoupling.PreserveFacing"/>.
        /// </summary>
        public MovementFacingCoupling FacingCoupling { get; init; }
            = MovementFacingCoupling.PreserveFacing;
    }

    /// <summary>
    /// Named sight and hearing sensor model. Ranges and distance-band bounds
    /// are measured in map tiles; bearing values use sector indices.
    /// </summary>
    /// <param name="Id">Stable vision-profile identifier.</param>
    /// <param name="Range">Maximum sight range in map tiles.</param>
    /// <param name="DistanceMetric">Sight distance-metric policy ID.</param>
    /// <param name="Shape">Sight-shape policy ID.</param>
    /// <param name="OmnidirectionalProximityRange">
    /// Always-around sight radius in map tiles.
    /// </param>
    /// <param name="LineOfSight">Occlusion policy ID.</param>
    /// <param name="HearingRadius">Maximum hearing radius in map tiles.</param>
    /// <param name="HearingBearingSectors">Number of coarse bearing sectors.</param>
    /// <param name="HearingBearingModel">Sector numbering/model policy ID.</param>
    /// <param name="HearingDistanceBandModel">Distance-band mapping policy ID.</param>
    /// <param name="HearingDistanceBandUpperBounds">
    /// Inclusive upper tile distance for each finite band.
    /// </param>
    /// <param name="LoudEventKinds">Event-kind IDs eligible for hearing.</param>
    public sealed record VisionProfile(
        string Id,
        int Range,
        string DistanceMetric,
        string Shape,
        int OmnidirectionalProximityRange,
        string LineOfSight,
        int HearingRadius,
        int HearingBearingSectors,
        string HearingBearingModel,
        string HearingDistanceBandModel,
        ImmutableArray<int> HearingDistanceBandUpperBounds,
        ImmutableArray<string> LoudEventKinds);

    /// <summary>
    /// Named attack capability, including aim, projectile, cooldown, energy,
    /// and optional shot-program mechanics.
    /// </summary>
    /// <param name="Id">Stable attack-profile identifier.</param>
    /// <param name="OmnidirectionalAim">Whether aim is independent of facing.</param>
    /// <param name="AimInterpretation">Aim semantic policy ID.</param>
    /// <param name="Projectile">Projectile traversal and damage definition.</param>
    /// <param name="CooldownTicks">Cooldown applied after an accepted attack.</param>
    /// <param name="MaxEnergy">Maximum attack energy.</param>
    /// <param name="AttackEnergyCost">Energy consumed per accepted attack.</param>
    /// <param name="EnergyRegenerationIntervalTicks">Ticks between regeneration.</param>
    /// <param name="EnergyRegenerationAmount">Energy restored per interval.</param>
    /// <param name="EnergyRegenerationClock">Regeneration clock policy ID.</param>
    /// <param name="EnergyUpdateOrder">Within-tick energy ordering policy ID.</param>
    /// <param name="EnergyArithmetic">Energy clamping/arithmetic policy ID.</param>
    /// <param name="AttackAvailability">Attack availability policy ID.</param>
    /// <param name="CooldownUpdate">Cooldown clock policy ID.</param>
    /// <param name="ShotProgram">Shot-program capability and bounds.</param>
    public sealed record AttackProfile(
        string Id,
        bool OmnidirectionalAim,
        string AimInterpretation,
        Projectile Projectile,
        int CooldownTicks,
        int MaxEnergy,
        int AttackEnergyCost,
        int EnergyRegenerationIntervalTicks,
        int EnergyRegenerationAmount,
        string EnergyRegenerationClock,
        string EnergyUpdateOrder,
        string EnergyArithmetic,
        string AttackAvailability,
        string CooldownUpdate,
        ShotProgramDefinition ShotProgram)
    {
        /// <summary>
        /// Optional multi-projectile launch shape. Contracts that do not
        /// publish the field launch exactly one projectile per attack.
        /// </summary>
        public AttackVolley? Volley { get; init; }

        /// <summary>Projectiles issued by one successful attack.</summary>
        public int ProjectilesPerAttack => Volley?.ProjectileCount ?? 1;
    }

    /// <summary>Projectile traversal, range, timing, collision, and damage rules.</summary>
    /// <param name="Mode">Instant-ray or discrete-projectile model.</param>
    /// <param name="DamagePerHit">Health removed by one contact.</param>
    /// <param name="MaxTravelTiles">Maximum path length in map tiles.</param>
    /// <param name="TicksPerAdvance">Ticks between discrete advances.</param>
    /// <param name="TilesPerAdvance">Tiles traversed per discrete advance.</param>
    /// <param name="LaunchTiles">Tiles traversed by the launch phase.</param>
    /// <param name="AdvancesOnLaunchTick">Whether normal advance also occurs at launch.</param>
    /// <param name="DamageAppliedSimultaneously">Whether same-phase hits batch damage.</param>
    /// <param name="DiagonalCornersMustBeClear">Whether diagonal traversal checks both corners.</param>
    public sealed record Projectile(
        ProjectileMode Mode,
        int DamagePerHit,
        int MaxTravelTiles,
        int TicksPerAdvance,
        int TilesPerAdvance,
        int LaunchTiles,
        bool AdvancesOnLaunchTick,
        bool DamageAppliedSimultaneously,
        bool DiagonalCornersMustBeClear);

    /// <summary>
    /// Bounds and fallback semantics for programmable projectile heading and
    /// curvature. Tile counts are measured along projectile travel.
    /// </summary>
    /// <param name="Enabled">Whether bots may supply a shot-program payload.</param>
    /// <param name="HeadingSectors">Number of absolute projectile heading sectors.</param>
    /// <param name="HeadingModel">Heading numbering and wrap policy ID.</param>
    /// <param name="BendStepSectors">Heading sectors changed by one bend step.</param>
    /// <param name="MinInitialAimSteps">Minimum signed initial aim offset.</param>
    /// <param name="MaxInitialAimSteps">Maximum signed initial aim offset.</param>
    /// <param name="AimOnlyProgram">Required inert curvature for aim-only attacks.</param>
    /// <param name="AllowedCurvedBendDirections">Allowed signed bend directions.</param>
    /// <param name="MinBendAfterTiles">Minimum tiles before the first bend.</param>
    /// <param name="MaxBendAfterTiles">Maximum tiles before the first bend.</param>
    /// <param name="MinBendEveryTiles">Minimum tiles between repeated bends.</param>
    /// <param name="MaxBendEveryTiles">Maximum tiles between repeated bends.</param>
    /// <param name="MinBendCount">Minimum bend count.</param>
    /// <param name="MaxBendCount">Maximum bend count.</param>
    /// <param name="LaunchTiles">Travel tiles governed by launch semantics.</param>
    /// <param name="PayloadOptional">Whether omission selects the default program.</param>
    /// <param name="DefaultProgram">Program used when an optional payload is absent.</param>
    /// <param name="InvalidPayloadResult">
    /// Invalid-payload outcome policy, or <see langword="null"/> when inapplicable.
    /// </param>
    /// <param name="UnsupportedPayloadResult">Unsupported-payload outcome policy ID.</param>
    /// <param name="DiagonalCornersMustBeClear">Whether diagonal bends require clear corners.</param>
    public sealed record ShotProgramDefinition(
        bool Enabled,
        int HeadingSectors,
        string HeadingModel,
        int BendStepSectors,
        int MinInitialAimSteps,
        int MaxInitialAimSteps,
        AimOnlyShotProgramValue AimOnlyProgram,
        ImmutableArray<int> AllowedCurvedBendDirections,
        int MinBendAfterTiles,
        int MaxBendAfterTiles,
        int MinBendEveryTiles,
        int MaxBendEveryTiles,
        int MinBendCount,
        int MaxBendCount,
        int LaunchTiles,
        bool PayloadOptional,
        ShotProgramValue DefaultProgram,
        string? InvalidPayloadResult,
        string UnsupportedPayloadResult,
        bool DiagonalCornersMustBeClear);

    /// <summary>Concrete default or bot-supplied projectile path program.</summary>
    /// <param name="InitialAimOffset">Signed initial heading offset in sectors.</param>
    /// <param name="BendDirection">Signed sector step applied by each bend.</param>
    /// <param name="BendAfterTiles">Travel tiles before the first bend.</param>
    /// <param name="BendEveryTiles">Travel tiles between later bends.</param>
    /// <param name="BendCount">Maximum number of bends.</param>
    public sealed record ShotProgramValue(
        int InitialAimOffset,
        int BendDirection,
        int BendAfterTiles,
        int BendEveryTiles,
        int BendCount);

    /// <summary>Canonical non-curving values used by aim-only attacks.</summary>
    /// <param name="BendDirection">Required non-curving bend direction.</param>
    /// <param name="BendAfterTiles">Required first-bend sentinel.</param>
    /// <param name="BendEveryTiles">Required bend-interval sentinel.</param>
    /// <param name="BendCount">Required zero-bend count.</param>
    public sealed record AimOnlyShotProgramValue(
        int BendDirection,
        int BendAfterTiles,
        int BendEveryTiles,
        int BendCount);

    /// <summary>One stable action selector and its ordered parameter schema.</summary>
    /// <param name="Id">Stable action identifier.</param>
    /// <param name="Code">Compact wire code paired with the identifier.</param>
    /// <param name="Kind">Engine semantic action kind.</param>
    /// <param name="ParameterKinds">Complete typed parameter schema.</param>
    public sealed record ActionDefinition(
        string Id,
        int Code,
        ActionKind Kind,
        ImmutableArray<ActionParameterKind> ParameterKinds);

    /// <summary>Closed union of transitions that create child lives in other slots.</summary>
    /// <param name="Kind">Fabrication transition discriminator.</param>
    /// <param name="TransitionId">Stable catalog transition identifier.</param>
    /// <param name="ActionId">Action catalog entry that requests the transition.</param>
    public abstract record FabricationTransition(
        FabricationTransitionKind Kind,
        string TransitionId,
        string ActionId);

    /// <summary>
    /// Creates a bounded number of child lives subject to form, region, tile,
    /// placement, slot, delay, conflict, and lineage policies.
    /// String fields are exact frozen policy IDs.
    /// </summary>
    /// <param name="TransitionId">Stable transition catalog identifier.</param>
    /// <param name="ActionId">Action catalog entry requesting fabrication.</param>
    /// <param name="SourceFormIds">Forms permitted to initiate fabrication.</param>
    /// <param name="OutputFormId">Form assigned to each child life.</param>
    /// <param name="OutputCount">Number of child lives in one bundle.</param>
    /// <param name="SourceRegionRoleId">Required source-region role ID.</param>
    /// <param name="OutputRegionRoleId">Required output-region role ID.</param>
    /// <param name="RequiredSourceTileTags">Tags required on the source tile.</param>
    /// <param name="RequiredOutputTileTags">Tags required on output tiles.</param>
    /// <param name="ForbiddenOutputTileTags">Tags forbidden on output tiles.</param>
    /// <param name="CandidateOffsets">Facing-relative output candidates in tiles.</param>
    /// <param name="Delay">Fabrication windup and cancellation policy.</param>
    /// <param name="UnavailablePlacementResult">No-placement action outcome policy ID.</param>
    /// <param name="TargetSlot">Target-slot selection policy ID.</param>
    /// <param name="CandidateSnapshot">Candidate snapshot timing policy ID.</param>
    /// <param name="PositionSelection">Position selection policy ID.</param>
    /// <param name="ClaimScope">Reservation claim-scope policy ID.</param>
    /// <param name="ConflictResolution">Simultaneous conflict policy ID.</param>
    /// <param name="SourceDisposition">Source-life disposition policy ID.</param>
    /// <param name="ChildInitialState">Child combat/resource state policy ID.</param>
    /// <param name="OutputFacing">Child facing policy ID.</param>
    /// <param name="CandidateReference">Offset reference-frame policy ID.</param>
    /// <param name="Lineage">Child lineage policy ID.</param>
    /// <param name="OutputHealth">Child initial-health policy ID.</param>
    /// <param name="SpawnReason">Child spawn-reason policy ID.</param>
    /// <param name="OffsetArithmetic">Offset arithmetic policy ID.</param>
    /// <param name="OutstandingBundles">Outstanding-bundle limit policy ID.</param>
    public sealed record BoundedChildFabricationTransition(
        string TransitionId,
        string ActionId,
        ImmutableArray<string> SourceFormIds,
        string OutputFormId,
        int OutputCount,
        string SourceRegionRoleId,
        string OutputRegionRoleId,
        ImmutableArray<GenericActorMapContract.TileTagKind>
            RequiredSourceTileTags,
        ImmutableArray<GenericActorMapContract.TileTagKind>
            RequiredOutputTileTags,
        ImmutableArray<GenericActorMapContract.TileTagKind>
            ForbiddenOutputTileTags,
        ImmutableArray<RelativePositionOffset> CandidateOffsets,
        FabricationDelay Delay,
        string UnavailablePlacementResult,
        string TargetSlot,
        string CandidateSnapshot,
        string PositionSelection,
        string ClaimScope,
        string ConflictResolution,
        string SourceDisposition,
        string ChildInitialState,
        string OutputFacing,
        string CandidateReference,
        string Lineage,
        string OutputHealth,
        string SpawnReason,
        string OffsetArithmetic,
        string OutstandingBundles)
        : FabricationTransition(
            FabricationTransitionKind.BoundedChild,
            TransitionId,
            ActionId);

    /// <summary>
    /// Windup, cancellation, reservation, completion, and tick arithmetic for
    /// a fabrication operation.
    /// </summary>
    /// <param name="DurationTicks">Ticks from acceptance to scheduled completion.</param>
    /// <param name="SourceBehavior">Source-life behavior policy during windup.</param>
    /// <param name="SourceDeath">Source destruction policy during windup.</param>
    /// <param name="SourceRetirement">Source retirement policy during windup.</param>
    /// <param name="Reservation">Slot/tile reservation policy.</param>
    /// <param name="Completion">Completion policy ID.</param>
    /// <param name="TickArithmetic">Started/due tick arithmetic policy ID.</param>
    public sealed record FabricationDelay(
        int DurationTicks,
        string SourceBehavior,
        string SourceDeath,
        string SourceRetirement,
        string Reservation,
        string Completion,
        string TickArithmetic);

    /// <summary>Closed union of transformations that preserve actor identity and memory.</summary>
    /// <param name="Kind">Same-life transition discriminator.</param>
    /// <param name="TransitionId">Stable catalog transition identifier.</param>
    /// <param name="ActionId">Action catalog entry that requests the transition.</param>
    public abstract record SameLifeTransition(
        SameLifeTransitionKind Kind,
        string TransitionId,
        string ActionId);

    /// <summary>
    /// Changes one life from a source form to a target form while preserving
    /// its actor identity and declared memory continuity.
    /// </summary>
    /// <param name="TransitionId">Stable transition catalog identifier.</param>
    /// <param name="ActionId">Action catalog entry requesting the change.</param>
    /// <param name="SourceFormId">Only form permitted to initiate the change.</param>
    /// <param name="TargetFormId">Form scheduled at completion.</param>
    /// <param name="Windup">Timing, pending behavior, and cancellation policy.</param>
    /// <param name="MemoryContinuity">Private-memory continuity policy ID.</param>
    /// <param name="Health">Health transfer policy.</param>
    /// <param name="CombatState">Cooldown and energy continuity.</param>
    /// <param name="Placement">Completion placement and tile legality.</param>
    /// <param name="IrreversibleForLife">Whether the life can later reverse this change.</param>
    public sealed record FormTransition(
        string TransitionId,
        string ActionId,
        string SourceFormId,
        string TargetFormId,
        TransitionWindup Windup,
        string MemoryContinuity,
        SameLifeHealth Health,
        SameLifeCombatState CombatState,
        SameLifePlacement Placement,
        bool IrreversibleForLife)
        : SameLifeTransition(
            SameLifeTransitionKind.FormTransition,
            TransitionId,
            ActionId);

    /// <summary>Windup timing, visibility, cancellation, and placement semantics.</summary>
    /// <param name="DurationTicks">Ticks from acceptance to scheduled completion.</param>
    /// <param name="PendingAction">Actions permitted while pending.</param>
    /// <param name="SourceForm">Form retained during windup.</param>
    /// <param name="Targetability">Targeting policy during windup.</param>
    /// <param name="LethalDamage">Lethal-damage cancellation policy.</param>
    /// <param name="Completion">Completion policy ID.</param>
    /// <param name="PlacementReference">Tile reference used at completion.</param>
    public sealed record TransitionWindup(
        int DurationTicks,
        string PendingAction,
        string SourceForm,
        string Targetability,
        string LethalDamage,
        string Completion,
        string PlacementReference);

    /// <summary>Health transfer policy for a same-life form transition.</summary>
    /// <param name="Policy">Health transfer policy ID.</param>
    /// <param name="FlatHealthGain">Flat health delta when used by the policy.</param>
    /// <param name="Evaluation">Evaluation-time policy ID.</param>
    /// <param name="Arithmetic">Clamping and arithmetic policy ID.</param>
    /// <param name="PreserveRatioFormula">Ratio-preservation formula ID.</param>
    public sealed record SameLifeHealth(
        string Policy,
        int FlatHealthGain,
        string Evaluation,
        string Arithmetic,
        string PreserveRatioFormula);

    /// <summary>Cooldown and energy continuity across a same-life transition.</summary>
    /// <param name="CooldownContinuity">Cooldown continuity policy ID.</param>
    /// <param name="EnergyContinuity">Energy continuity policy ID.</param>
    public sealed record SameLifeCombatState(
        string CooldownContinuity,
        string EnergyContinuity);

    /// <summary>Placement continuity and tile legality at transition completion.</summary>
    /// <param name="PositionContinuity">Position continuity policy ID.</param>
    /// <param name="LegalityEvaluation">Placement evaluation policy ID.</param>
    /// <param name="RequiredTileTags">Tile tags required at completion.</param>
    /// <param name="ForbiddenTileTags">Tile tags forbidden at completion.</param>
    /// <param name="FailedCompletion">Failed-completion policy ID.</param>
    public sealed record SameLifePlacement(
        string PositionContinuity,
        string LegalityEvaluation,
        ImmutableArray<GenericActorMapContract.TileTagKind> RequiredTileTags,
        ImmutableArray<GenericActorMapContract.TileTagKind> ForbiddenTileTags,
        string FailedCompletion);

    /// <summary>Closed union of transitions that replace a life with descendants.</summary>
    /// <param name="Kind">Replication transition discriminator.</param>
    /// <param name="TransitionId">Stable catalog transition identifier.</param>
    /// <param name="ActionId">Action catalog entry that requests the transition.</param>
    public abstract record ReplicationTransition(
        ReplicationTransitionKind Kind,
        string TransitionId,
        string ActionId);

    /// <summary>
    /// Atomically replaces a qualifying source life with a data-defined number
    /// of independently executing descendants. The catalog fully declares
    /// health, placement, slot, conflict, generation, runtime, and lineage
    /// semantics.
    /// </summary>
    /// <param name="TransitionId">Stable transition catalog identifier.</param>
    /// <param name="ActionId">Action catalog entry requesting replication.</param>
    /// <param name="SourceFormIds">Forms permitted to initiate replication.</param>
    /// <param name="OutputFormId">Form assigned to every descendant.</param>
    /// <param name="DescendantCount">Number of fresh descendant lives.</param>
    /// <param name="MaxSourceGeneration">Highest generation allowed to split.</param>
    /// <param name="RequireNoPriorSameLifeTransition">Whether prior transformation blocks split.</param>
    /// <param name="Health">Descendant health distribution policy.</param>
    /// <param name="MinimumSourceHealth">Minimum health required at evaluation.</param>
    /// <param name="CandidateOffsets">Facing-relative descendant positions in tiles.</param>
    /// <param name="Windup">Timing, pending behavior, and cancellation policy.</param>
    /// <param name="DescendantGenerationIncrement">Generation added to each descendant.</param>
    /// <param name="ReservationIsAtomic">Whether every slot/tile reservation succeeds together.</param>
    /// <param name="ConflictingBundlesAllBlock">Whether conflicting bundles all fail.</param>
    /// <param name="InsufficientHealthBlocks">Whether low health yields a blocked outcome.</param>
    /// <param name="ReuseSourceSlotFirst">Whether one descendant reuses the source slot.</param>
    /// <param name="AdditionalSlotsUseLowestCompatibleDormantId">Whether extra slots use stable ID order.</param>
    /// <param name="SourceRemainsTargetableUntilCompletion">Whether the source stays targetable in windup.</param>
    /// <param name="LethalSourceDamageCancels">Whether lethal source damage cancels the bundle.</param>
    /// <param name="SourceRetirementCountsAsDestruction">Whether source retirement scores as destruction.</param>
    /// <param name="DescendantsUseFreshIsolatedRuntimes">Whether each descendant gets a fresh runtime.</param>
    /// <param name="DescendantsInheritPrivateMemory">Whether descendants inherit private memory.</param>
    /// <param name="CandidateSnapshot">Candidate snapshot timing policy ID.</param>
    /// <param name="PositionSelection">Position selection policy ID.</param>
    /// <param name="SlotSelection">Stable-slot selection policy ID.</param>
    /// <param name="DescendantAssignment">Descendant-to-slot/position assignment policy ID.</param>
    /// <param name="ClaimScope">Reservation claim-scope policy ID.</param>
    /// <param name="ConflictResolution">Simultaneous conflict policy ID.</param>
    /// <param name="HealthEvaluation">Health evaluation timing policy ID.</param>
    /// <param name="OffsetArithmetic">Offset arithmetic policy ID.</param>
    public sealed record SplitReplicationTransition(
        string TransitionId,
        string ActionId,
        ImmutableArray<string> SourceFormIds,
        string OutputFormId,
        int DescendantCount,
        int MaxSourceGeneration,
        bool RequireNoPriorSameLifeTransition,
        ReplicationHealth Health,
        int MinimumSourceHealth,
        ImmutableArray<RelativePositionOffset> CandidateOffsets,
        TransitionWindup Windup,
        int DescendantGenerationIncrement,
        bool ReservationIsAtomic,
        bool ConflictingBundlesAllBlock,
        bool InsufficientHealthBlocks,
        bool ReuseSourceSlotFirst,
        bool AdditionalSlotsUseLowestCompatibleDormantId,
        bool SourceRemainsTargetableUntilCompletion,
        bool LethalSourceDamageCancels,
        bool SourceRetirementCountsAsDestruction,
        bool DescendantsUseFreshIsolatedRuntimes,
        bool DescendantsInheritPrivateMemory,
        string CandidateSnapshot,
        string PositionSelection,
        string SlotSelection,
        string DescendantAssignment,
        string ClaimScope,
        string ConflictResolution,
        string HealthEvaluation,
        string OffsetArithmetic)
        : ReplicationTransition(
            ReplicationTransitionKind.Split,
            TransitionId,
            ActionId);

    /// <summary>Health distribution across replication descendants.</summary>
    /// <param name="Distribution">Distribution algorithm policy ID.</param>
    /// <param name="MinimumHealthPerDescendant">Minimum legal descendant health.</param>
    /// <param name="Remainder">Remainder allocation policy ID.</param>
    /// <param name="MaximumHealth">Per-form maximum-health policy ID.</param>
    public sealed record ReplicationHealth(
        string Distribution,
        int MinimumHealthPerDescendant,
        string Remainder,
        string MaximumHealth);

    /// <summary>
    /// Candidate tile offset relative to the source facing, measured in tiles.
    /// </summary>
    /// <param name="Forward">Signed tiles along the source's facing axis.</param>
    /// <param name="Right">Signed tiles along the source's right-hand axis.</param>
    public sealed record RelativePositionOffset(int Forward, int Right);

    /// <summary>Team observation sharing, snapshot timing, and provenance policy.</summary>
    /// <param name="Kind">Individual or team-union observation model.</param>
    /// <param name="Snapshot">State snapshot timing policy ID.</param>
    /// <param name="SameTickDecisionSharing">Same-tick communication policy ID.</param>
    /// <param name="ObservationProvenance">Observed-by identity policy ID.</param>
    public sealed record TeamPerceptionDefinition(
        TeamPerceptionKind Kind,
        string Snapshot,
        string SameTickDecisionSharing,
        string ObservationProvenance);

    /// <summary>
    /// Complete actor, wall, movement, projectile, contact, and simultaneous
    /// conflict rules. Boolean fields are direct capabilities; string fields
    /// are exact resolution policy IDs.
    /// </summary>
    /// <param name="ActorsBlockWalls">Whether walls block actor movement.</param>
    /// <param name="ActorsBlockActors">Whether actors occupy blocking tiles.</param>
    /// <param name="SameDestinationMovesBlockAll">Whether colliding moves all block.</param>
    /// <param name="SwapMovesBlocked">Whether two-life tile swaps block.</param>
    /// <param name="FollowingVacatedActorAllowed">Whether a move may follow a vacated tile.</param>
    /// <param name="ProjectilesBlockMovement">Whether projectiles block movement.</param>
    /// <param name="MovingOntoProjectileCausesHit">Whether entry onto a projectile contacts it.</param>
    /// <param name="WallsConsumeProjectiles">Whether wall contact removes projectiles.</param>
    /// <param name="ProjectilesIgnoreFiringLife">Whether projectiles ignore their firing life.</param>
    /// <param name="ProjectilesStopOnFirstEnemyActor">Whether first enemy contact ends traversal.</param>
    /// <param name="ProjectilesCollideWithProjectiles">Whether projectiles collide with each other.</param>
    /// <param name="AlliedProjectileContact">Allied contact policy ID.</param>
    /// <param name="MovementResolution">Movement conflict policy ID.</param>
    /// <param name="ProjectileTraversalResolution">Projectile traversal policy ID.</param>
    /// <param name="ActorProjectileContactTiming">Actor/projectile contact timing policy ID.</param>
    /// <param name="MovementDestinationProjectileResult">Destination-projectile outcome policy ID.</param>
    /// <param name="AlliedMovementDestinationOverride">Allied destination override policy ID.</param>
    public sealed record CollisionRules(
        bool ActorsBlockWalls,
        bool ActorsBlockActors,
        bool SameDestinationMovesBlockAll,
        bool SwapMovesBlocked,
        bool FollowingVacatedActorAllowed,
        bool ProjectilesBlockMovement,
        bool MovingOntoProjectileCausesHit,
        bool WallsConsumeProjectiles,
        bool ProjectilesIgnoreFiringLife,
        bool ProjectilesStopOnFirstEnemyActor,
        bool ProjectilesCollideWithProjectiles,
        string AlliedProjectileContact,
        string MovementResolution,
        string ProjectileTraversalResolution,
        string ActorProjectileContactTiming,
        string MovementDestinationProjectileResult,
        string AlliedMovementDestinationOverride);

    /// <summary>
    /// Observation timing, joint-decision semantics, match-completion
    /// precedence, damage ordering, and authoritative phase order.
    /// </summary>
    /// <param name="ObservationsUsePreTickState">
    /// Whether each observation is a snapshot before its tick resolves.
    /// </param>
    /// <param name="DecisionsResolveAsJointStep">
    /// Whether all admitted decisions resolve simultaneously.
    /// </param>
    /// <param name="MovementActionResolution">Movement resolution policy ID.</param>
    /// <param name="RotationActionResolution">Rotation resolution policy ID.</param>
    /// <param name="ActionAdmission">Action admission/outcome policy ID.</param>
    /// <param name="ActionFaultCounting">Runtime-fault counting policy ID.</param>
    /// <param name="MatchCompletionPrecedence">Terminal-condition phase policy ID.</param>
    /// <param name="DamageResolution">Damage and attribution ordering.</param>
    /// <param name="Phases">Authoritative phase IDs in execution order.</param>
    public sealed record TickResolutionDefinition(
        bool ObservationsUsePreTickState,
        bool DecisionsResolveAsJointStep,
        string MovementActionResolution,
        string RotationActionResolution,
        string ActionAdmission,
        string ActionFaultCounting,
        string MatchCompletionPrecedence,
        DamageResolution DamageResolution,
        ImmutableArray<string> Phases);

    /// <summary>Damage batching, identity, health, attribution, and event ordering.</summary>
    /// <param name="ContactBatch">Contact batching policy ID.</param>
    /// <param name="PerTargetApplicationOrder">Per-target hit ordering policy ID.</param>
    /// <param name="ProjectileIdentityAssignment">Projectile-ID assignment policy ID.</param>
    /// <param name="ContactOrdinalAssignment">Contact ordinal policy ID.</param>
    /// <param name="HealthApplication">Health update policy ID.</param>
    /// <param name="DestructionAttribution">Destruction attribution policy ID.</param>
    /// <param name="EventOrder">Damage/destruction event ordering policy ID.</param>
    public sealed record DamageResolution(
        string ContactBatch,
        string PerTargetApplicationOrder,
        string ProjectileIdentityAssignment,
        string ContactOrdinalAssignment,
        string HealthApplication,
        string DestructionAttribution,
        string EventOrder);

    /// <summary>Discriminator for objective and scoring-mode definitions.</summary>
    public enum GameModeKind
    {
        /// <summary>Score eliminations over a bounded match.</summary>
        Deathmatch = 0,
        /// <summary>Advance through ordered territorial objectives.</summary>
        Frontline = 1,
    }

    /// <summary>Discriminator for terminal and timeout victory policies.</summary>
    public enum VictoryKind
    {
        /// <summary>Deathmatch threshold and score ranking.</summary>
        Deathmatch = 0,
        /// <summary>Frontline breach and territorial ranking.</summary>
        Frontline = 1,
    }

    /// <summary>Engine semantic kind represented by an action catalog entry.</summary>
    public enum ActionKind
    {
        /// <summary>Take no gameplay action this tick.</summary>
        Wait = 0,
        /// <summary>Request movement under the current movement profile.</summary>
        Movement = 1,
        /// <summary>Change the body's facing.</summary>
        Rotation = 2,
        /// <summary>Fire through the current attack profile.</summary>
        Attack = 3,
        /// <summary>Create child lives in other stable slots.</summary>
        Fabrication = 4,
        /// <summary>Change form while preserving the current life.</summary>
        SameLifeTransition = 5,
        /// <summary>Replace the source life with descendant lives.</summary>
        Replication = 6,
    }

    /// <summary>Closed discriminator for typed action arguments and constraints.</summary>
    public enum ActionParameterKind
    {
        /// <summary>Programmable projectile path.</summary>
        ShotProgram = 0,
        /// <summary>Absolute map direction.</summary>
        Direction = 1,
        /// <summary>Stable team/unit slot target.</summary>
        UnitTarget = 2,
        /// <summary>Form catalog target.</summary>
        FormTarget = 3,
        /// <summary>Absolute projectile heading sector.</summary>
        ProjectileHeading = 4,
    }

    /// <summary>Projectile traversal representation.</summary>
    public enum ProjectileMode
    {
        /// <summary>Resolve the complete ray without a persistent projectile.</summary>
        InstantRay = 0,
        /// <summary>Advance a persistent projectile over authoritative ticks.</summary>
        Discrete = 1,
    }

    /// <summary>How allied sensor observations are exposed to a life.</summary>
    public enum TeamPerceptionKind
    {
        /// <summary>Expose only the observing life's own sensor results.</summary>
        Individual = 0,
        /// <summary>Expose the immediate union of current allied sensor results.</summary>
        ImmediateUnion = 1,
    }

    /// <summary>Discriminator for fabrication-transition definitions.</summary>
    public enum FabricationTransitionKind
    {
        /// <summary>Create a bounded child bundle in assigned dormant slots.</summary>
        BoundedChild = 0,
    }

    /// <summary>Discriminator for same-life transition definitions.</summary>
    public enum SameLifeTransitionKind
    {
        /// <summary>Change the current life's form.</summary>
        FormTransition = 0,
    }

    /// <summary>Discriminator for replication-transition definitions.</summary>
    public enum ReplicationTransitionKind
    {
        /// <summary>Replace one source life with multiple descendants.</summary>
        Split = 0,
    }
}
