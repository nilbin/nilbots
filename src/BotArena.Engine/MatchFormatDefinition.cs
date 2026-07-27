namespace BotArena.Engine;

/// <summary>
/// Closed vNext participant-arrangement union. It constrains scoring teams and
/// submitted participants, while unit cardinality remains explicit topology.
/// </summary>
public abstract record MatchFormatDefinition
{
    internal MatchFormatDefinition(
        string formatId,
        int scoringTeamCount,
        int participantsPerTeam)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatId);
        if (scoringTeamCount < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scoringTeamCount),
                "A match format needs at least two scoring teams.");
        }
        if (participantsPerTeam <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participantsPerTeam),
                "Participants per team must be positive.");
        }

        FormatId = formatId;
        ScoringTeamCount = scoringTeamCount;
        ParticipantsPerTeam = participantsPerTeam;
        ParticipantCount = checked(scoringTeamCount * participantsPerTeam);
    }

    public abstract MatchFormatDefinitionKind Kind { get; }
    public string FormatId { get; }
    public int ScoringTeamCount { get; }
    public int ParticipantsPerTeam { get; }
    public int ParticipantCount { get; }

    public void ValidateTopology(PublicMatchTopology topology) =>
        MatchFormatTopologyValidator.Validate(this, topology);

    public enum MatchFormatDefinitionKind
    {
        HeadToHead = 0,
        FreeForAll = 1,
        Teams = 2,
    }
}
