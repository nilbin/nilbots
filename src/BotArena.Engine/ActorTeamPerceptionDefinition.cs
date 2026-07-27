namespace BotArena.Engine;

/// <summary>
/// Declares which authoritative observations are shared between active lives
/// controlled by participants on the same scoring team.
/// </summary>
public sealed record ActorTeamPerceptionDefinition
{
    public ActorTeamPerceptionDefinition(PerceptionKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Kind = kind;
    }

    public PerceptionKind Kind { get; }

    public enum PerceptionKind
    {
        Individual = 0,
        ImmediateUnion = 1,
    }
}
