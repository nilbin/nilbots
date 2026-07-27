namespace BotArena.App.Competition;

/// <summary>
/// Immutable, topology-aware input shared by duel, team, and free-for-all rating
/// policies.
/// </summary>
public sealed class RatingPolicyInput
{
    public RatingPolicyInput(
        LadderId ladderId,
        IReadOnlyCollection<RatingEntrant> entrants,
        IReadOnlyCollection<TeamSeriesResult> teamResults)
    {
        if (ladderId.IsEmpty)
            throw new ArgumentException(
                "A rating calculation requires a non-empty ladder id.",
                nameof(ladderId));
        ArgumentNullException.ThrowIfNull(entrants);
        ArgumentNullException.ThrowIfNull(teamResults);

        RatingEntrant[] entrantSnapshot = [.. entrants];
        TeamSeriesResult[] resultSnapshot = [.. teamResults];
        ValidateEntrants(entrantSnapshot);
        ValidateTeamResults(resultSnapshot);
        ValidateTopology(entrantSnapshot, resultSnapshot);
        entrantSnapshot = entrantSnapshot
            .OrderBy(entrant => entrant.TeamId)
            .ThenBy(entrant => entrant.EntrantId)
            .ToArray();
        resultSnapshot = resultSnapshot
            .OrderBy(result => result.TeamId)
            .ToArray();

        LadderId = ladderId;
        Entrants = Array.AsReadOnly(entrantSnapshot);
        TeamResults = Array.AsReadOnly(resultSnapshot);
    }

    public LadderId LadderId { get; }
    public IReadOnlyList<RatingEntrant> Entrants { get; }
    public IReadOnlyList<TeamSeriesResult> TeamResults { get; }

    private static void ValidateEntrants(IReadOnlyList<RatingEntrant> entrants)
    {
        if (entrants.Count < 2)
            throw new ArgumentException(
                "A rated result requires at least two entrants.",
                nameof(entrants));

        HashSet<Guid> entrantIds = [];
        foreach (RatingEntrant? entrant in entrants)
        {
            if (entrant is null)
                throw new ArgumentException(
                    "A rated result cannot contain a null entrant.",
                    nameof(entrants));
            if (entrant.EntrantId == Guid.Empty ||
                !entrantIds.Add(entrant.EntrantId))
            {
                throw new ArgumentException(
                    "Entrant ids must be non-empty and unique.",
                    nameof(entrants));
            }
            if (entrant.TeamId < 0)
                throw new ArgumentException(
                    "Entrant team ids must be non-negative.",
                    nameof(entrants));
            if (!double.IsFinite(entrant.Rating))
                throw new ArgumentException(
                    "Entrant ratings must be finite.",
                    nameof(entrants));
        }
    }

    private static void ValidateTeamResults(
        IReadOnlyList<TeamSeriesResult> teamResults)
    {
        if (teamResults.Count < 2)
            throw new ArgumentException(
                "A rated result requires at least two scoring teams.",
                nameof(teamResults));

        HashSet<int> teamIds = [];
        foreach (TeamSeriesResult? result in teamResults)
        {
            if (result is null)
                throw new ArgumentException(
                    "A rated result cannot contain a null team result.",
                    nameof(teamResults));
            if (result.TeamId < 0 || !teamIds.Add(result.TeamId))
                throw new ArgumentException(
                    "Result team ids must be non-negative and unique.",
                    nameof(teamResults));
            if (result.Placement <= 0)
                throw new ArgumentException(
                    "Team placements must be positive.",
                    nameof(teamResults));
            if (!double.IsFinite(result.SeriesPoints) ||
                result.SeriesPoints < 0)
                throw new ArgumentException(
                    "Team series points must be finite and non-negative.",
                    nameof(teamResults));
        }

        int expectedPlacement = 1;
        foreach (IGrouping<int, TeamSeriesResult> tiedGroup in teamResults
                     .GroupBy(result => result.Placement)
                     .OrderBy(group => group.Key))
        {
            if (tiedGroup.Key != expectedPlacement)
            {
                throw new ArgumentException(
                    "Team placements must use competition ranking (for example 1, 1, 3).",
                    nameof(teamResults));
            }
            expectedPlacement += tiedGroup.Count();
        }
    }

    private static void ValidateTopology(
        IReadOnlyList<RatingEntrant> entrants,
        IReadOnlyList<TeamSeriesResult> teamResults)
    {
        HashSet<int> entrantTeams = entrants
            .Select(entrant => entrant.TeamId)
            .ToHashSet();
        HashSet<int> resultTeams = teamResults
            .Select(result => result.TeamId)
            .ToHashSet();
        if (!entrantTeams.SetEquals(resultTeams))
        {
            throw new ArgumentException(
                "Team results must cover exactly the entrants' scoring teams.",
                nameof(teamResults));
        }
    }
}
