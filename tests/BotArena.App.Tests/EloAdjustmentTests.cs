using BotArena.App.Matches;

namespace BotArena.App.Tests;

public class EloAdjustmentTests
{
    private const int Games = MatchSet.Games;
    private const double K = MatchSet.EloK;

    [Fact]
    public void EvenlyMatchedBotsTradeTheFullKFactorOnASweep()
    {
        double change = EloAdjustment.ForBotA(1200, 1200, scoreA: Games, Games, K);
        Assert.Equal(K / 2, change, precision: 6);
    }

    [Fact]
    public void ASplitSetBetweenEqualsMovesNothing()
    {
        Assert.Equal(0, EloAdjustment.ForBotA(1200, 1200, scoreA: Games / 2.0, Games, K), precision: 6);
    }

    [Fact]
    public void TheMoveIsZeroSum()
    {
        double change = EloAdjustment.ForBotA(1400, 1150, scoreA: 4, Games, K);
        // B's change is defined as the negative of A's, so the ladder's total is
        // conserved. This is what makes the floor a real constraint rather than a
        // cosmetic clamp.
        Assert.Equal(1400 + 1150, 1400 + change + (1150 - change), precision: 6);
    }

    [Theory]
    [InlineData(1200, 1200)]
    [InlineData(2400, 105)]
    [InlineData(105, 2400)]
    public void NeitherSideCanBeDrivenBelowTheFloor(double ratingA, double ratingB)
    {
        foreach (double scoreA in new[] { 0, 3, 6 })
        {
            double change = EloAdjustment.ForBotA(ratingA, ratingB, scoreA, Games, K);
            Assert.True(ratingA + change >= EloAdjustment.Floor, $"A fell below the floor at {scoreA}");
            Assert.True(ratingB - change >= EloAdjustment.Floor, $"B fell below the floor at {scoreA}");
        }
    }

    /// <summary>The farming case: a sacrificial bot you own, fed to your real bot over
    /// and over. The expectation term is what actually kills it — the per-set gain decays
    /// roughly logarithmically, so 500 consecutive sweeps buy less than the first ten do.
    /// (The floor is the hard bound below; via this route it is never reached in any
    /// realistic number of sets, which is itself the point.)</summary>
    [Fact]
    public void FarmingASacrificialBotDecaysToNothing()
    {
        double farmer = 1200;
        double sacrifice = 1200;
        double firstTen = 0;
        double lastGain = 0;
        for (int set = 0; set < 500; set++)
        {
            double change = EloAdjustment.ForBotA(farmer, sacrifice, scoreA: Games, Games, K);
            farmer += change;
            sacrifice -= change;
            if (set < 10)
                firstTen += change;
            lastGain = change;
        }

        Assert.True(lastGain < 0.2, $"the 500th sweep still paid {lastGain:0.###}");
        Assert.True(farmer - 1200 < 5 * firstTen,
            "500 sweeps should not buy anywhere near 50x what the first ten did");
        // Everything the farmer gained came out of the sacrifice: 2400 in, 2400 out.
        Assert.Equal(2400, farmer + sacrifice, precision: 6);
        Assert.True(sacrifice > EloAdjustment.Floor);
    }

    /// <summary>What the floor is for: it bounds how much any one victim can ever supply,
    /// so a bot drained to the bottom — by farming or just by losing a lot — is worth
    /// exactly nothing, and no rating is minted to pay a winner it cannot afford.</summary>
    [Fact]
    public void ABotAtTheFloorCanPayNothingAndFallNoFurther()
    {
        Assert.Equal(0, EloAdjustment.ForBotA(2000, EloAdjustment.Floor, scoreA: Games, Games, K), precision: 6);
        Assert.Equal(0, EloAdjustment.ForBotA(EloAdjustment.Floor, 2000, scoreA: 0, Games, K), precision: 6);
    }

    [Fact]
    public void LosingToAMuchWeakerBotStillCosts()
    {
        // The floor must not accidentally protect a strong bot from an upset.
        double change = EloAdjustment.ForBotA(2000, 1200, scoreA: 0, Games, K);
        Assert.True(change < -K * 0.9, $"expected a near-full-K loss, got {change}");
    }
}
