namespace BotArena.Engine;

/// <summary>
/// A submitted policy/artifact assigned to one scoring team. Class identity
/// belongs to the submitted participant rather than being inferred from any
/// life or form name.
/// </summary>
public sealed record PublicParticipant(
    int ParticipantId,
    int TeamId,
    string? ClassId = null);
