using BotArena.App.Matches;

namespace BotArena.App.Competition;

/// <summary>
/// Single-match Arc Relay application of the existing Elo expectation, K and
/// rating floor. Only the series length differs from the six-game Duel adapter.
/// </summary>
public sealed class ArcRelayEloV1 : IRatingPolicy
{
    public const string Id = "arc-relay-elo-v1";
    public const double KFactor = DuelEloV1.KFactor;
    public const double MinimumRating = DuelEloV1.MinimumRating;

    public string PolicyId => Id;

    public IReadOnlyList<RatingUpdate> Calculate(RatingPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Entrants.Count != 2 || input.TeamResults.Count != 2)
            throw new ArgumentException("Arc Relay Elo requires two entrants.", nameof(input));
        RatingEntrant a = input.Entrants[0];
        RatingEntrant b = input.Entrants[1];
        if (a.Rating < MinimumRating || b.Rating < MinimumRating)
            throw new ArgumentException("Arc Relay ratings cannot start below the Elo floor.", nameof(input));
        TeamSeriesResult resultA = input.TeamResults.Single(value => value.TeamId == a.TeamId);
        TeamSeriesResult resultB = input.TeamResults.Single(value => value.TeamId == b.TeamId);
        if (resultA.SeriesPoints is < 0 or > 1 ||
            resultB.SeriesPoints is < 0 or > 1 ||
            resultA.SeriesPoints + resultB.SeriesPoints != 1)
        {
            throw new ArgumentException("Arc Relay Elo requires one match scored 1/0 or 0.5/0.5.", nameof(input));
        }
        double change = EloAdjustment.ForBotA(
            a.Rating,
            b.Rating,
            resultA.SeriesPoints,
            games: 1,
            k: KFactor);
        return
        [
            new RatingUpdate(a.EntrantId, a.Rating, a.Rating + change, change, a.PolicyState),
            new RatingUpdate(b.EntrantId, b.Rating, b.Rating - change, -change, b.PolicyState),
        ];
    }
}
