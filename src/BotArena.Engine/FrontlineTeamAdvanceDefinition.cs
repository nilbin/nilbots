namespace BotArena.Engine;

/// <summary>
/// Declares which direction one scoring team advances through the ordered
/// Frontline objective-region sequence.
/// </summary>
public sealed record FrontlineTeamAdvanceDefinition
{
    public FrontlineTeamAdvanceDefinition(
        int teamId,
        ObjectiveAdvanceDirection direction)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        TeamId = teamId;
        Direction = direction;
    }

    public int TeamId { get; }
    public ObjectiveAdvanceDirection Direction { get; }
    public int ObjectiveIndexDelta => (int)Direction;

    public enum ObjectiveAdvanceDirection
    {
        TowardLowerIndex = -1,
        TowardHigherIndex = 1,
    }
}
