using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class DuelEloV1Tests
{
    [Fact]
    public void PolicyIdentityPinsCurrentLiteralConstants()
    {
        Assert.Equal("duel-elo-v1", DuelEloV1.Id);
        Assert.Equal(6, DuelEloV1.Games);
        Assert.Equal(32, DuelEloV1.KFactor);
        Assert.Equal(100, DuelEloV1.MinimumRating);
    }

    [Theory]
    [InlineData(1200, 1200, 6, 0, 16)]
    [InlineData(1400, 1150, 4, 2, -4.532832188249344)]
    [InlineData(1200, 1200, 3, 3, 0)]
    [InlineData(105, 105, 0, 6, -5)]
    [InlineData(2400, 100, 6, 0, 0)]
    public void ProducesPinnedGoldenTransitions(
        double ratingA,
        double ratingB,
        double pointsA,
        double pointsB,
        double expectedChangeA)
    {
        Guid entrantA = Guid.NewGuid();
        Guid entrantB = Guid.NewGuid();
        RatingPolicyInput input = Duel(
            entrantA,
            entrantB,
            ratingA,
            ratingB,
            pointsA,
            pointsB);

        IReadOnlyList<RatingUpdate> updates = new DuelEloV1().Calculate(input);

        Assert.Collection(
            updates,
            update =>
            {
                Assert.Equal(entrantA, update.EntrantId);
                Assert.Equal(
                    ratingA + expectedChangeA,
                    update.RatingAfter,
                    precision: 12);
                Assert.Equal(
                    expectedChangeA,
                    update.RatingChange,
                    precision: 12);
            },
            update =>
            {
                Assert.Equal(entrantB, update.EntrantId);
                Assert.Equal(
                    ratingB - expectedChangeA,
                    update.RatingAfter,
                    precision: 12);
                Assert.Equal(
                    -expectedChangeA,
                    update.RatingChange,
                    precision: 12);
            });
    }

    [Fact]
    public void PolicyStatePassesThroughTheCompatibilityAdapter()
    {
        RatingPolicyInput input = Duel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1200,
            1200,
            4,
            2,
            "state-a",
            "state-b");

        IReadOnlyList<RatingUpdate> updates = new DuelEloV1().Calculate(input);

        Assert.Equal("state-a", updates[0].PolicyState);
        Assert.Equal("state-b", updates[1].PolicyState);
    }

    [Fact]
    public void InputEnumerationOrderDoesNotChangeTransitions()
    {
        Guid entrantA = Guid.NewGuid();
        Guid entrantB = Guid.NewGuid();
        RatingPolicyInput forward = Duel(
            entrantA,
            entrantB,
            1400,
            1150,
            4,
            2);
        var reversed = new RatingPolicyInput(
            forward.LadderId,
            forward.Entrants.Reverse().ToArray(),
            forward.TeamResults.Reverse().ToArray());

        Assert.Equal(
            new DuelEloV1().Calculate(forward),
            new DuelEloV1().Calculate(reversed));
    }

    [Fact]
    public void TeamOrFreeForAllInputFailsClosed()
    {
        RatingPolicyInput input = new(
            LadderId.New(),
            [
                new RatingEntrant(Guid.NewGuid(), TeamId: 0, Rating: 1200),
                new RatingEntrant(Guid.NewGuid(), TeamId: 0, Rating: 1200),
                new RatingEntrant(Guid.NewGuid(), TeamId: 1, Rating: 1200),
            ],
            [
                new TeamSeriesResult(0, Placement: 1, SeriesPoints: 4),
                new TeamSeriesResult(1, Placement: 2, SeriesPoints: 2),
            ]);

        Assert.Throws<ArgumentException>(
            () => new DuelEloV1().Calculate(input));
    }

    [Fact]
    public void PlacementsMustAgreeWithDuelScores()
    {
        RatingPolicyInput input = new(
            LadderId.New(),
            [
                new RatingEntrant(Guid.NewGuid(), TeamId: 0, Rating: 1200),
                new RatingEntrant(Guid.NewGuid(), TeamId: 1, Rating: 1200),
            ],
            [
                new TeamSeriesResult(0, Placement: 2, SeriesPoints: 4),
                new TeamSeriesResult(1, Placement: 1, SeriesPoints: 2),
            ]);

        Assert.Throws<ArgumentException>(
            () => new DuelEloV1().Calculate(input));
    }

    [Theory]
    [InlineData(5.75, 0.25)]
    [InlineData(5, 0)]
    public void PolicyRejectsNonHalfPointOrNonSixGameScores(
        double pointsA,
        double pointsB)
    {
        RatingPolicyInput input = Duel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1200,
            1200,
            pointsA,
            pointsB);

        Assert.Throws<ArgumentException>(
            () => new DuelEloV1().Calculate(input));
    }

    [Fact]
    public void PolicyRejectsRatingsBelowItsLiteralFloor()
    {
        RatingPolicyInput input = Duel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            99,
            1200,
            0,
            6);

        Assert.Throws<ArgumentException>(
            () => new DuelEloV1().Calculate(input));
    }

    private static RatingPolicyInput Duel(
        Guid entrantA,
        Guid entrantB,
        double ratingA,
        double ratingB,
        double pointsA,
        double pointsB,
        string? stateA = null,
        string? stateB = null)
    {
        int placementA = pointsA >= pointsB ? 1 : 2;
        int placementB = pointsB >= pointsA ? 1 : 2;
        return new RatingPolicyInput(
            LadderId.New(),
            [
                new RatingEntrant(entrantA, TeamId: 0, ratingA, stateA),
                new RatingEntrant(entrantB, TeamId: 1, ratingB, stateB),
            ],
            [
                new TeamSeriesResult(0, placementA, pointsA),
                new TeamSeriesResult(1, placementB, pointsB),
            ]);
    }
}
