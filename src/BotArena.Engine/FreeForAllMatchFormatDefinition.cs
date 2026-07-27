namespace BotArena.Engine;

/// <summary>
/// One submitted participant per scoring team. FFA cardinality is contract
/// data rather than a separate game-mode implementation.
/// </summary>
public sealed record FreeForAllMatchFormatDefinition : MatchFormatDefinition
{
    public FreeForAllMatchFormatDefinition(
        int participantCount)
        : base(
            $"ffa-{participantCount}",
            scoringTeamCount: participantCount,
            participantsPerTeam: 1)
    {
        if (participantCount < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participantCount),
                "Free-for-all requires at least three participants.");
        }
    }

    public override MatchFormatDefinitionKind Kind =>
        MatchFormatDefinitionKind.FreeForAll;
}
