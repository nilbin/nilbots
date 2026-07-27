using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Factory-free presentation and artifact provenance for one submitted
/// participant. Runtime factories remain match-execution resources and are
/// never retained by replay chronology.
/// </summary>
public sealed record GenericActorParticipantProvenance
{
    public GenericActorParticipantProvenance(
        int participantId,
        int teamId,
        string name,
        string runtimeKind,
        string? artifactHash,
        string accent,
        string? lookId,
        string? projectileLookId)
    {
        if (participantId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(participantId));
        }
        if (teamId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(teamId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(accent);
        ValidateOptionalMetadata(artifactHash, nameof(artifactHash));
        ValidateOptionalMetadata(lookId, nameof(lookId));
        ValidateOptionalMetadata(
            projectileLookId,
            nameof(projectileLookId));

        ParticipantId = participantId;
        TeamId = teamId;
        Name = name;
        RuntimeKind = runtimeKind;
        ArtifactHash = artifactHash;
        Accent = accent;
        LookId = lookId;
        ProjectileLookId = projectileLookId;
    }

    public int ParticipantId { get; }
    public int TeamId { get; }
    public string Name { get; }
    public string RuntimeKind { get; }
    public string? ArtifactHash { get; }
    public string Accent { get; }
    public string? LookId { get; }
    public string? ProjectileLookId { get; }

    /// <summary>
    /// Copies participant provenance without retaining runtime factories,
    /// requires exact participant/team coverage of the resolved topology, and
    /// returns the snapshot in participant-ID order.
    /// </summary>
    public static ImmutableArray<GenericActorParticipantProvenance>
        CreateCanonicalSnapshot(
            ActorResolvedMatchDefinition definition,
            IEnumerable<GenericActorParticipantConfiguration>
                configurations)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(configurations);

        var projected =
            new List<GenericActorParticipantProvenance>();
        var participantIds = new HashSet<int>();
        foreach (GenericActorParticipantConfiguration? configuration in
                 configurations)
        {
            if (configuration is null)
            {
                throw new ArgumentException(
                    "Participant configurations cannot contain null.",
                    nameof(configurations));
            }
            if (configuration.RuntimeFactory is null)
            {
                throw new ArgumentException(
                    $"Participant {configuration.ParticipantId} has no runtime factory.",
                    nameof(configurations));
            }
            if (!participantIds.Add(configuration.ParticipantId))
            {
                throw new ArgumentException(
                    $"Participant {configuration.ParticipantId} is configured more than once.",
                    nameof(configurations));
            }

            projected.Add(
                new GenericActorParticipantProvenance(
                    configuration.ParticipantId,
                    configuration.TeamId,
                    configuration.Name,
                    configuration.RuntimeKind,
                    configuration.ArtifactHash,
                    configuration.Accent,
                    configuration.LookId,
                    configuration.ProjectileLookId));
        }

        return CanonicalizeExact(
            definition,
            projected,
            nameof(configurations));
    }

    internal static ImmutableArray<GenericActorParticipantProvenance>
        CanonicalizeExact(
            ActorResolvedMatchDefinition definition,
            IEnumerable<GenericActorParticipantProvenance> participants,
            string parameterName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(participants);

        GenericActorParticipantProvenance[] snapshot = [.. participants];
        if (snapshot.Any(participant => participant is null))
        {
            throw new ArgumentException(
                "Participant provenance cannot contain null.",
                parameterName);
        }

        Dictionary<int, PublicParticipant> expected =
            definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId);
        var actual =
            new Dictionary<int, GenericActorParticipantProvenance>();
        foreach (GenericActorParticipantProvenance participant in snapshot)
        {
            if (!actual.TryAdd(participant.ParticipantId, participant))
            {
                throw new ArgumentException(
                    $"Participant {participant.ParticipantId} is represented more than once.",
                    parameterName);
            }
            if (!expected.TryGetValue(
                    participant.ParticipantId,
                    out PublicParticipant? topologyParticipant)
                || topologyParticipant.TeamId != participant.TeamId)
            {
                throw new ArgumentException(
                    $"Participant {participant.ParticipantId} does not match the resolved topology.",
                    parameterName);
            }
        }

        if (actual.Count != expected.Count
            || expected.Keys.Any(participantId =>
                !actual.ContainsKey(participantId)))
        {
            throw new ArgumentException(
                "Participant provenance must exactly match the resolved topology.",
                parameterName);
        }

        return snapshot
            .OrderBy(participant => participant.ParticipantId)
            .ToImmutableArray();
    }

    private static void ValidateOptionalMetadata(
        string? value,
        string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Optional participant metadata cannot be empty or whitespace.",
                parameterName);
        }
    }
}
