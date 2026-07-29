using BotArena.App.Matches;

namespace BotArena.App.Competition;

/// <summary>
/// Compatibility adapter for the shipped two-bot, six-game Elo calculation.
/// It delegates the numerical move to <see cref="EloAdjustment"/> so the current
/// ladder's results remain exact while callers move behind <see cref="IRatingPolicy"/>.
/// </summary>
public sealed class DuelEloV1 : IRatingPolicy
{
    public const string Id = "duel-elo-v1";
    public const int Games = DuelMirrored6V1.GameCount;
    public const double KFactor = 32;
    public const double MinimumRating = EloAdjustment.Floor;

    public string PolicyId => Id;

    public IReadOnlyList<RatingUpdate> Calculate(RatingPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Entrants.Count != 2 ||
            input.TeamResults.Count != 2 ||
            input.Entrants.Select(entrant => entrant.TeamId).Distinct().Count() != 2)
        {
            throw new ArgumentException(
                "Duel Elo requires exactly two entrants on two separate teams.",
                nameof(input));
        }

        RatingEntrant entrantA = input.Entrants[0];
        RatingEntrant entrantB = input.Entrants[1];
        if (entrantA.Rating < MinimumRating ||
            entrantB.Rating < MinimumRating)
        {
            throw new ArgumentException(
                "Duel Elo ratings cannot start below the policy floor.",
                nameof(input));
        }
        TeamSeriesResult resultA = input.TeamResults.Single(
            result => result.TeamId == entrantA.TeamId);
        TeamSeriesResult resultB = input.TeamResults.Single(
            result => result.TeamId == entrantB.TeamId);
        ValidateDuelScores(resultA, resultB);

        double changeA = EloAdjustment.ForBotA(
            entrantA.Rating,
            entrantB.Rating,
            resultA.SeriesPoints,
            Games,
            KFactor);
        return
        [
            new RatingUpdate(
                entrantA.EntrantId,
                entrantA.Rating,
                entrantA.Rating + changeA,
                changeA,
                entrantA.PolicyState),
            new RatingUpdate(
                entrantB.EntrantId,
                entrantB.Rating,
                entrantB.Rating - changeA,
                -changeA,
                entrantB.PolicyState),
        ];
    }

    private static void ValidateDuelScores(
        TeamSeriesResult resultA,
        TeamSeriesResult resultB)
    {
        if (resultA.SeriesPoints < 0 || resultB.SeriesPoints < 0)
            throw InvalidScores("Duel Elo scores cannot be negative.");

        if (!IsHalfPoint(resultA.SeriesPoints) ||
            !IsHalfPoint(resultB.SeriesPoints) ||
            resultA.SeriesPoints + resultB.SeriesPoints != Games)
        {
            throw InvalidScores(
                "Duel Elo requires exactly six games scored in half points.");
        }

        int scoreComparison =
            resultA.SeriesPoints.CompareTo(resultB.SeriesPoints);
        int placementComparison = resultA.Placement.CompareTo(resultB.Placement);
        if ((scoreComparison > 0 && placementComparison >= 0) ||
            (scoreComparison < 0 && placementComparison <= 0) ||
            (scoreComparison == 0 && placementComparison != 0))
        {
            throw InvalidScores(
                "Duel placements must agree with the two team scores.");
        }
    }

    private static bool IsHalfPoint(double value) =>
        double.IsFinite(value) &&
        value * 2 == Math.Truncate(value * 2);

    private static ArgumentException InvalidScores(string message) =>
        new(message, "input");
}
