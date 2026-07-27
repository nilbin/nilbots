using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Typed, immutable SDK view of an Engine-validated canonical generic match
/// contract.
/// <see cref="CanonicalJson"/> preserves the host-authored bytes as text and
/// remains the sole static-contract serialization carried over the actor wire.
/// </summary>
public sealed class GenericActorResolvedMatchContract
{
    internal GenericActorResolvedMatchContract(
        string canonicalJson,
        int schemaVersion,
        string matchContractFingerprint,
        CapabilityVersionSet capabilityVersions,
        GenericActorRulesContract rules,
        GenericActorMapContract map,
        MatchFormat format,
        MatchTopology topology,
        Deployment initialDeployment,
        ImmutableArray<LifecycleAssignment> lifecycleAssignments,
        ImmutableArray<ParticipantRegionAssignment>
            participantRegionAssignments,
        ModeMapBindingDefinition modeMapBinding)
    {
        CanonicalJson = canonicalJson;
        SchemaVersion = schemaVersion;
        MatchContractFingerprint = matchContractFingerprint;
        CapabilityVersions = capabilityVersions;
        Rules = rules;
        Map = map;
        Format = format;
        Topology = topology;
        InitialDeployment = initialDeployment;
        LifecycleAssignments = lifecycleAssignments;
        ParticipantRegionAssignments = participantRegionAssignments;
        ModeMapBinding = modeMapBinding;
    }

    /// <summary>
    /// Exact canonical JSON admitted by the Engine. Use the typed members for
    /// normal bot logic; this text is the fingerprinted source of truth.
    /// </summary>
    public string CanonicalJson { get; }
    /// <summary>Resolved match-contract schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Fingerprint joining MatchStart and every observation.</summary>
    public string MatchContractFingerprint { get; }
    /// <summary>Exact negotiated version tuple required by the match.</summary>
    public CapabilityVersionSet CapabilityVersions { get; }
    /// <summary>Frozen mechanics, catalogs, objective, and resolution rules.</summary>
    public GenericActorRulesContract Rules { get; }
    /// <summary>Frozen gameplay map.</summary>
    public GenericActorMapContract Map { get; }
    /// <summary>Participant/team arrangement and scoring-team shape.</summary>
    public MatchFormat Format { get; }
    /// <summary>Stable public teams, participants, unit slots, and initial lives.</summary>
    public MatchTopology Topology { get; }
    /// <summary>Initial life-to-spawn placement.</summary>
    public Deployment InitialDeployment { get; }
    /// <summary>Per-slot lifecycle and form assignments.</summary>
    public ImmutableArray<LifecycleAssignment> LifecycleAssignments { get; }
    /// <summary>Participant role bindings to named map regions.</summary>
    public ImmutableArray<ParticipantRegionAssignment>
        ParticipantRegionAssignments
    { get; }
    /// <summary>Mode-specific binding from objective semantics to map regions.</summary>
    public ModeMapBindingDefinition ModeMapBinding { get; }

    /// <summary>All-or-nothing capability tuple negotiated before MatchStart.</summary>
    /// <param name="ContractProfileId">Exact aggregate profile identifier.</param>
    /// <param name="RuntimeProtocolVersion">Actor framing protocol version.</param>
    /// <param name="RuntimeConfigurationVersion">Runtime configuration version.</param>
    /// <param name="RuntimeContractVersion">Host/runtime behavior version.</param>
    /// <param name="MatchStartSchemaVersion">MatchStart payload schema.</param>
    /// <param name="ObservationSchemaVersion">Observation payload schema.</param>
    /// <param name="DecisionSchemaVersion">Decision payload schema.</param>
    /// <param name="MatchContractSchemaVersion">Static match-contract schema.</param>
    public sealed record CapabilityVersionSet(
        string ContractProfileId,
        string RuntimeProtocolVersion,
        string RuntimeConfigurationVersion,
        int RuntimeContractVersion,
        int MatchStartSchemaVersion,
        int ObservationSchemaVersion,
        int DecisionSchemaVersion,
        int MatchContractSchemaVersion);

    /// <summary>Frozen participant and scoring-team arrangement.</summary>
    /// <param name="SchemaVersion">Match-format schema version.</param>
    /// <param name="FormatId">Stable authored format identifier.</param>
    /// <param name="FormatFingerprint">Fingerprint of the canonical format.</param>
    /// <param name="Kind">High-level arrangement kind.</param>
    /// <param name="ScoringTeamCount">Number of independently ranked teams.</param>
    /// <param name="ParticipantsPerTeam">Participants assigned to each team.</param>
    /// <param name="ParticipantCount">Total submitted bot programs.</param>
    public sealed record MatchFormat(
        int SchemaVersion,
        string FormatId,
        string FormatFingerprint,
        MatchFormatKind Kind,
        int ScoringTeamCount,
        int ParticipantsPerTeam,
        int ParticipantCount);

    /// <summary>Stable public identities and slots available throughout the match.</summary>
    /// <param name="SchemaVersion">Topology schema version.</param>
    /// <param name="TopologyFingerprint">Fingerprint of the canonical topology.</param>
    /// <param name="Counts">Declared cardinalities for scalable inputs.</param>
    /// <param name="Teams">Public scoring teams.</param>
    /// <param name="Participants">Submitted programs and their team assignments.</param>
    /// <param name="UnitSlots">Stable unit slots that may hold successive lives.</param>
    /// <param name="InitialLives">Lives present in the initial deployment.</param>
    public sealed record MatchTopology(
        int SchemaVersion,
        string TopologyFingerprint,
        TopologyCounts Counts,
        ImmutableArray<PublicScoringTeam> Teams,
        ImmutableArray<PublicParticipant> Participants,
        ImmutableArray<PublicUnitSlot> UnitSlots,
        ImmutableArray<PublicInitialLife> InitialLives);

    /// <summary>Declared topology cardinalities; bots must not assume fixed sizes.</summary>
    /// <param name="TeamCount">Number of public scoring teams.</param>
    /// <param name="ParticipantCount">Number of submitted programs.</param>
    /// <param name="UnitSlotCount">Total stable unit slots.</param>
    /// <param name="InitialLifeCount">Lives present at tick zero.</param>
    public sealed record TopologyCounts(
        int TeamCount,
        int ParticipantCount,
        int UnitSlotCount,
        int InitialLifeCount);

    /// <summary>Resolved tick-zero spawn catalog and life placement.</summary>
    /// <param name="Spawns">Spawn anchors selected for this match.</param>
    /// <param name="Lives">Initial life placements into those anchors.</param>
    public sealed record Deployment(
        ImmutableArray<InitialSpawn> Spawns,
        ImmutableArray<InitialLifeDeployment> Lives);

    /// <summary>A resolved initial spawn anchor.</summary>
    /// <param name="SpawnId">Stable map spawn identifier.</param>
    /// <param name="Position">Spawn tile coordinate.</param>
    /// <param name="Facing">Initial absolute map direction.</param>
    public sealed record InitialSpawn(
        string SpawnId,
        Position Position,
        Direction Facing);

    /// <summary>Placement and form of one initially active body life.</summary>
    /// <param name="TeamId">Stable scoring-team identifier.</param>
    /// <param name="UnitId">Stable unit-slot identifier.</param>
    /// <param name="LifeId">Life identifier within the stable unit slot.</param>
    /// <param name="FormId">Initial form catalog identifier.</param>
    /// <param name="SpawnId">Resolved spawn anchor identifier.</param>
    public sealed record InitialLifeDeployment(
        int TeamId,
        int UnitId,
        int LifeId,
        string FormId,
        string SpawnId);

    /// <summary>Lifecycle and capability assignment for one stable unit slot.</summary>
    /// <param name="TeamId">Stable scoring-team identifier.</param>
    /// <param name="UnitId">Stable unit-slot identifier.</param>
    /// <param name="LifecycleProfileId">Assigned lifecycle catalog entry.</param>
    /// <param name="InitialGeneration">
    /// Initial generation, or <see langword="null"/> until first activation.
    /// </param>
    /// <param name="AllowedFormIds">Complete set of forms this slot may host.</param>
    /// <param name="InitialAvailability">Tick-zero availability policy.</param>
    /// <param name="UnlockTick">
    /// Scheduled unlock tick for delayed slots; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="AssignedRespawnSpawnId">
    /// Automatic-return spawn, or <see langword="null"/> when not assigned.
    /// </param>
    public sealed record LifecycleAssignment(
        int TeamId,
        int UnitId,
        string LifecycleProfileId,
        int? InitialGeneration,
        ImmutableArray<string> AllowedFormIds,
        InitialAvailability InitialAvailability,
        int? UnlockTick,
        string? AssignedRespawnSpawnId);

    /// <summary>Binds one participant role to a named map region.</summary>
    /// <param name="ParticipantId">Submitted-program identifier.</param>
    /// <param name="RegionRoleId">Ruleset-defined role identifier.</param>
    /// <param name="MapRegionId">Bound map region identifier.</param>
    /// <param name="Facing">Role-relative forward direction on the map.</param>
    public sealed record ParticipantRegionAssignment(
        int ParticipantId,
        string RegionRoleId,
        string MapRegionId,
        Direction Facing);

    /// <summary>Closed union binding the selected game mode to map semantics.</summary>
    /// <param name="Kind">Binding discriminator.</param>
    public abstract record ModeMapBindingDefinition(ModeMapBindingKind Kind);

    /// <summary>Deathmatch binding; no ordered objective regions are required.</summary>
    public sealed record DeathmatchModeMapBinding()
        : ModeMapBindingDefinition(ModeMapBindingKind.Deathmatch);

    /// <summary>Binds Frontline progress to an ordered sequence of objective regions.</summary>
    /// <param name="OrderedObjectiveRegionIds">
    /// Objective region IDs from lowest to highest canonical index.
    /// </param>
    /// <param name="TeamAdvances">Per-team direction through that sequence.</param>
    public sealed record FrontlineModeMapBinding(
        ImmutableArray<string> OrderedObjectiveRegionIds,
        ImmutableArray<FrontlineTeamAdvance> TeamAdvances)
        : ModeMapBindingDefinition(ModeMapBindingKind.Frontline);

    /// <summary>One team's advance direction through ordered objectives.</summary>
    /// <param name="TeamId">Stable scoring-team identifier.</param>
    /// <param name="Direction">Semantic direction through objective indices.</param>
    /// <param name="ObjectiveIndexDelta">Signed index delta for one advance.</param>
    public sealed record FrontlineTeamAdvance(
        int TeamId,
        ObjectiveAdvanceDirection Direction,
        int ObjectiveIndexDelta);

    /// <summary>High-level participant/team arrangement.</summary>
    public enum MatchFormatKind
    {
        /// <summary>Two scoring teams with one participant each.</summary>
        HeadToHead = 0,
        /// <summary>Each participant is independently scored.</summary>
        FreeForAll = 1,
        /// <summary>Multiple participants cooperate within scoring teams.</summary>
        Teams = 2,
    }

    /// <summary>Tick-zero availability of a stable unit slot.</summary>
    public enum InitialAvailability
    {
        /// <summary>The slot contains an active life at tick zero.</summary>
        ActiveAtTickZero = 0,
        /// <summary>The slot is dormant until its declared unlock tick.</summary>
        DormantUnlockAtTick = 1,
    }

    /// <summary>Discriminator for mode-to-map binding records.</summary>
    public enum ModeMapBindingKind
    {
        /// <summary>Deathmatch requires no ordered objective binding.</summary>
        Deathmatch = 0,
        /// <summary>Frontline uses ordered objective regions.</summary>
        Frontline = 1,
    }

    /// <summary>Semantic team advance through ordered objective indices.</summary>
    public enum ObjectiveAdvanceDirection
    {
        /// <summary>Advance toward smaller objective indices.</summary>
        TowardLowerIndex = -1,
        /// <summary>Advance toward larger objective indices.</summary>
        TowardHigherIndex = 1,
    }
}
