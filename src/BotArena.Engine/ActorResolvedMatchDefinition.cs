using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete serializer-neutral generation-3 match input. Construction performs
/// all cross-catalog and match-local validation before a canonical writer may
/// fingerprint the definition.
/// </summary>
public sealed record ActorResolvedMatchDefinition
{
    public const int CurrentSchemaVersion = 2;

    public ActorResolvedMatchDefinition(
        ActorRulesDefinition rules,
        ActorMapDefinition map,
        MatchFormatDefinition format,
        PublicMatchTopology topology,
        InitialDeploymentDefinition initialDeployment,
        IEnumerable<ActorUnitSlotLifecycleAssignmentDefinition>
            lifecycleAssignments,
        IEnumerable<ActorParticipantRegionAssignmentDefinition>
            participantRegionAssignments,
        ActorModeMapBindingDefinition modeMapBinding,
        ActorMatchCapabilityVersions? capabilityVersions = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(initialDeployment);
        ArgumentNullException.ThrowIfNull(lifecycleAssignments);
        ArgumentNullException.ThrowIfNull(participantRegionAssignments);
        ArgumentNullException.ThrowIfNull(modeMapBinding);
        capabilityVersions ??= ActorMatchCapabilityVersions.Current;

        ActorUnitSlotLifecycleAssignmentDefinition[] lifecycleSnapshot =
            [.. lifecycleAssignments];
        ActorParticipantRegionAssignmentDefinition[] regionSnapshot =
            [.. participantRegionAssignments];

        ActorResolvedMatchDefinitionValidator.Validate(
            rules,
            map,
            format,
            topology,
            initialDeployment,
            lifecycleSnapshot,
            regionSnapshot,
            modeMapBinding);

        Rules = rules;
        CapabilityVersions = capabilityVersions;
        Map = map;
        Format = format;
        Topology = Canonicalize(topology);
        InitialDeployment = initialDeployment;
        LifecycleAssignments = lifecycleSnapshot
            .OrderBy(assignment => assignment.TeamId)
            .ThenBy(assignment => assignment.UnitId)
            .ToImmutableArray();
        ParticipantRegionAssignments = regionSnapshot
            .OrderBy(assignment => assignment.ParticipantId)
            .ThenBy(
                assignment => assignment.RegionRoleId,
                StringComparer.Ordinal)
            .ThenBy(
                assignment => assignment.MapRegionId,
                StringComparer.Ordinal)
            .ToImmutableArray();
        ModeMapBinding = modeMapBinding;
    }

    public int SchemaVersion => CurrentSchemaVersion;
    public ActorMatchCapabilityVersions CapabilityVersions { get; }
    public ActorRulesDefinition Rules { get; }
    public ActorMapDefinition Map { get; }
    public MatchFormatDefinition Format { get; }
    public PublicMatchTopology Topology { get; }
    public InitialDeploymentDefinition InitialDeployment { get; }
    public ImmutableArray<ActorUnitSlotLifecycleAssignmentDefinition>
        LifecycleAssignments { get; }
    public ImmutableArray<ActorParticipantRegionAssignmentDefinition>
        ParticipantRegionAssignments { get; }
    public ActorModeMapBindingDefinition ModeMapBinding { get; }

    private static PublicMatchTopology Canonicalize(
        PublicMatchTopology topology) =>
        topology with
        {
            Teams = topology.Teams
                .OrderBy(team => team.TeamId)
                .ToImmutableArray(),
            Participants = topology.Participants
                .OrderBy(participant => participant.ParticipantId)
                .ToImmutableArray(),
            UnitSlots = topology.UnitSlots
                .OrderBy(slot => slot.TeamId)
                .ThenBy(slot => slot.UnitId)
                .ToImmutableArray(),
            InitialLives = topology.InitialLives
                .OrderBy(life => life.TeamId)
                .ThenBy(life => life.UnitId)
                .ThenBy(life => life.LifeId)
                .ToImmutableArray(),
        };
}
