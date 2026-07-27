using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class RatingPolicyInputTests
{
    [Fact]
    public void TiedCompetitionPlacementsAndTeamEntrantsAreSupported()
    {
        List<RatingEntrant> entrants =
        [
            Entrant(1, teamId: 10),
            Entrant(2, teamId: 10),
            Entrant(3, teamId: 20),
            Entrant(4, teamId: 30),
        ];
        List<TeamSeriesResult> results =
        [
            new(10, Placement: 1, SeriesPoints: 8),
            new(20, Placement: 1, SeriesPoints: 8),
            new(30, Placement: 3, SeriesPoints: 2),
        ];

        var input = new RatingPolicyInput(LadderId.New(), entrants, results);
        entrants.Clear();
        results.Clear();

        Assert.Equal(4, input.Entrants.Count);
        Assert.Equal(3, input.TeamResults.Count);
        Assert.Equal(2, input.Entrants.Count(entrant => entrant.TeamId == 10));
        Assert.Equal(
            [10, 10, 20, 30],
            input.Entrants.Select(entrant => entrant.TeamId).ToArray());
        Assert.Equal(
            [10, 20, 30],
            input.TeamResults.Select(result => result.TeamId).ToArray());
    }

    [Fact]
    public void NonCanonicalTiePlacementsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new RatingPolicyInput(
            LadderId.New(),
            [Entrant(1, 10), Entrant(2, 20), Entrant(3, 30)],
            [
                new TeamSeriesResult(10, Placement: 1, SeriesPoints: 8),
                new TeamSeriesResult(20, Placement: 1, SeriesPoints: 8),
                new TeamSeriesResult(30, Placement: 2, SeriesPoints: 2),
            ]));
    }

    [Fact]
    public void ResultsMustCoverExactlyTheEntrantTeams()
    {
        Assert.Throws<ArgumentException>(() => new RatingPolicyInput(
            LadderId.New(),
            [Entrant(1, 10), Entrant(2, 20)],
            [
                new TeamSeriesResult(10, Placement: 1, SeriesPoints: 1),
                new TeamSeriesResult(30, Placement: 2, SeriesPoints: 0),
            ]));
    }

    [Fact]
    public void NegativeSeriesPointsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new RatingPolicyInput(
            LadderId.New(),
            [Entrant(1, 10), Entrant(2, 20)],
            [
                new TeamSeriesResult(10, Placement: 1, SeriesPoints: 1),
                new TeamSeriesResult(20, Placement: 2, SeriesPoints: -1),
            ]));
    }

    private static RatingEntrant Entrant(int id, int teamId) =>
        new(new Guid($"{id:D8}-0000-0000-0000-000000000000"), teamId, 1200);
}
