namespace BotArena.Engine;

/// <summary>Equal-size multi-participant scoring teams, such as 2v2.</summary>
public sealed record TeamsMatchFormatDefinition : MatchFormatDefinition
{
    public TeamsMatchFormatDefinition(
        int scoringTeamCount,
        int participantsPerTeam)
        : base(
            $"teams-{scoringTeamCount}x{participantsPerTeam}",
            scoringTeamCount,
            participantsPerTeam)
    {
        if (participantsPerTeam < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participantsPerTeam),
                "The teams format requires at least two participants per team.");
        }
    }

    public override MatchFormatDefinitionKind Kind =>
        MatchFormatDefinitionKind.Teams;
}
