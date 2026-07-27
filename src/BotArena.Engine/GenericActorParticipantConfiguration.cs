namespace BotArena.Engine;

/// <summary>One generic actor participant and its replay provenance.</summary>
public sealed record GenericActorParticipantConfiguration
{
    public required int ParticipantId { get; init; }
    public required int TeamId { get; init; }
    public required string Name { get; init; }
    public required IGenericActorRuntimeFactory RuntimeFactory { get; init; }
    public string RuntimeKind { get; init; } = "in-process-generic-actor";
    public string ArtifactHash { get; init; } = "";
    public string Accent { get; init; } = "#38bdf8";
    public string? LookId { get; init; }
    public string? ProjectileLookId { get; init; }
}
