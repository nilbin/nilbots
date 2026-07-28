using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public class MatchFormatDefinitionTests
{
    [Fact]
    public void HeadToHead_AcceptsOneParticipantOnEachOfTwoTeams()
    {
        var format = new HeadToHeadMatchFormatDefinition();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);

        format.ValidateTopology(topology);

        Assert.Equal(
            MatchFormatDefinition.MatchFormatDefinitionKind.HeadToHead,
            format.Kind);
        Assert.Equal(2, format.ScoringTeamCount);
        Assert.Equal(2, format.ParticipantCount);
    }

    [Fact]
    public void FreeForAllFour_AcceptsFourIndependentScoringTeams()
    {
        var format = new FreeForAllMatchFormatDefinition(participantCount: 4);
        PublicMatchTopology topology =
            CreateTopology([[10], [20], [30], [40]]);

        format.ValidateTopology(topology);

        Assert.Equal(
            MatchFormatDefinition.MatchFormatDefinitionKind.FreeForAll,
            format.Kind);
        Assert.Equal(4, format.ScoringTeamCount);
        Assert.Equal("ffa-4", format.FormatId);
        Assert.Equal(1, format.ParticipantsPerTeam);
        Assert.Equal(4, format.ParticipantCount);
    }

    [Fact]
    public void TeamsTwoByTwo_AcceptsTwoParticipantsPerScoringTeam()
    {
        var format = new TeamsMatchFormatDefinition(
            scoringTeamCount: 2,
            participantsPerTeam: 2);
        PublicMatchTopology topology = CreateTopology([[10, 11], [20, 21]]);

        format.ValidateTopology(topology);

        Assert.Equal(
            MatchFormatDefinition.MatchFormatDefinitionKind.Teams,
            format.Kind);
        Assert.Equal("teams-2x2", format.FormatId);
        Assert.Equal(4, format.ParticipantCount);
        Assert.All(
            topology.Participants
                .GroupBy(participant => participant.TeamId),
            team => Assert.Equal(2, team.Count()));
    }

    [Fact]
    public void FormatsRejectWrongCardinalityAndInvalidOwnership()
    {
        var ffaFour = new FreeForAllMatchFormatDefinition(participantCount: 4);
        PublicMatchTopology onlyThreeTeams =
            CreateTopology([[10], [20], [30]]);
        PublicMatchTopology crossTeamController =
            CreateTopology([[10], [20]]) with
            {
                UnitSlots =
                [
                    new(0, 0, 20),
                    new(1, 0, 20),
                ],
            };
        PublicMatchTopology participantWithoutSlot =
            CreateTopology([[10, 11], [20, 21]]) with
            {
                UnitSlots =
                [
                    new(0, 0, 10),
                    new(1, 0, 20),
                    new(1, 1, 21),
                ],
                InitialLives =
                [
                    new(0, 0, 0, "mobile"),
                    new(1, 0, 0, "mobile"),
                    new(1, 1, 0, "mobile"),
                ],
            };

        Assert.Throws<MatchFormatValidationException>(() =>
            ffaFour.ValidateTopology(onlyThreeTeams));
        Assert.Throws<MatchFormatValidationException>(() =>
            new HeadToHeadMatchFormatDefinition()
                .ValidateTopology(crossTeamController));
        Assert.Throws<MatchFormatValidationException>(() =>
            new TeamsMatchFormatDefinition(2, 2)
                .ValidateTopology(participantWithoutSlot));
    }

    private static PublicMatchTopology CreateTopology(
        IReadOnlyList<IReadOnlyList<int>> participantIdsByTeam)
    {
        var teams = new List<PublicScoringTeam>();
        var participants = new List<PublicParticipant>();
        var unitSlots = new List<PublicUnitSlot>();
        var initialLives = new List<PublicInitialLife>();
        for (int teamId = 0; teamId < participantIdsByTeam.Count; teamId++)
        {
            teams.Add(new PublicScoringTeam(teamId));
            IReadOnlyList<int> participantIds =
                participantIdsByTeam[teamId];
            for (int unitId = 0; unitId < participantIds.Count; unitId++)
            {
                int participantId = participantIds[unitId];
                participants.Add(new PublicParticipant(participantId, teamId));
                unitSlots.Add(new PublicUnitSlot(
                    teamId,
                    unitId,
                    participantId));
                initialLives.Add(new PublicInitialLife(
                    teamId,
                    unitId,
                    LifeId: 0,
                    FormId: "mobile"));
            }
        }

        return new PublicMatchTopology
        {
            Teams = teams.ToImmutableArray(),
            Participants = participants.ToImmutableArray(),
            UnitSlots = unitSlots.ToImmutableArray(),
            InitialLives = initialLives.ToImmutableArray(),
        };
    }
}
