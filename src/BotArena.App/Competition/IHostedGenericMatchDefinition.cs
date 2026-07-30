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

    void Validate(Playlist playlist, PlaylistVersion version);
}
