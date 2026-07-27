namespace BotArena.Engine;

/// <summary>Two scoring teams with one submitted participant on each side.</summary>
public sealed record HeadToHeadMatchFormatDefinition : MatchFormatDefinition
{
    public const string Id = "head-to-head";

    public HeadToHeadMatchFormatDefinition()
        : base(Id, scoringTeamCount: 2, participantsPerTeam: 1)
    {
    }

    public override MatchFormatDefinitionKind Kind =>
        MatchFormatDefinitionKind.HeadToHead;
}
