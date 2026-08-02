using BotArena.Engine;

namespace BotArena.App.Competition;

/// <summary>
/// One versioned hosted generic-match definition. Implementations own the
/// exact resolved Engine contract and validate its immutable persisted
/// playlist identity.
/// </summary>
public interface IHostedGenericMatchDefinition
{
    string PlaylistKey { get; }
    int Version { get; }
    string AdmissionPolicyId { get; }
    string ExecutionPolicyId { get; }
    string ExecutionEngineVersion { get; }
    ActorResolvedMatchDefinition Match { get; }
    GenericActorReplayPresentation? ReplayPresentation { get; }

    /// <summary>
    /// Submitted actor artifacts are always sandboxed. A trusted-stock mind is
    /// a separately registered first-party build-once algorithm whose only
    /// player input is immutable hashed data.
    /// </summary>
    HostedGenericRuntimeModel RuntimeModel =>
        HostedGenericRuntimeModel.SubmittedActorWasm;

    /// <summary>Resolves dynamic topology from immutable participant data.</summary>
    ActorResolvedMatchDefinition ResolveMatch(
        IReadOnlyList<HostedGenericParticipantInput> participants) => Match;

    /// <summary>Null uses the deployment-wide presentation rate.</summary>
    double? PresentationTicksPerSecond => null;

    void Validate(Playlist playlist, PlaylistVersion version);
}

public enum HostedGenericRuntimeModel
{
    SubmittedActorWasm,
    TrustedStockMind,
}

public sealed record HostedGenericParticipantInput(
    int ParticipantId,
    int TeamId,
    IReadOnlyList<string> ClassIds);
