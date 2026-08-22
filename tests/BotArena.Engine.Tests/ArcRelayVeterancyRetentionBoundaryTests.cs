using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Stable-slot veterancy retention boundaries that are easy to miss in the
/// ordinary level-four acceptance scene.
/// </summary>
public sealed class ArcRelayVeterancyRetentionBoundaryTests
{
    [Fact]
    public void OddXpRoundsDownAndRepeatedDeathsEventuallyReachZero()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity first = Actor(0, 0, 0);
        AwardOrdinaryKills(driver, first, count: 5);

        Assert.Equal(5, driver.VeterancyTotalXp(first));
        AssertRetention(driver, first, expectedXp: 2, expectedLevel: 2);

        ActorIdentity second = Actor(0, 0, 1);
        AssertRetention(driver, second, expectedXp: 1, expectedLevel: 1);

        ActorIdentity third = Actor(0, 0, 2);
        AssertRetention(driver, third, expectedXp: 0, expectedLevel: 1);

        ActorIdentity fourth = Actor(0, 0, 3);
        Assert.Equal(0, driver.VeterancyTotalXp(fourth));
        Assert.Empty(Kill(driver, fourth));
    }

    [Fact]
    public void MaximumXpIsCappedBeforeRetention()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity first = Actor(0, 0, 0);
        AwardOrdinaryKills(driver, first, count: 20);

        Assert.Equal(5, driver.VeterancyLevel(first));
        Assert.Equal(8, driver.VeterancyTotalXp(first));
        AssertRetention(driver, first, expectedXp: 4, expectedLevel: 3);

        ActorIdentity second = Actor(0, 0, 1);
        Assert.Equal(4, driver.VeterancyTotalXp(second));
        Assert.Equal(3, driver.VeterancyLevel(second));
    }

    [Fact]
    public void SameUnitIdOnTheOpposingTeamIsAnIndependentStableSlot()
    {
        ArcRelayActorMatchModeDriver driver = Driver();
        ActorIdentity west = Actor(0, 0, 0);
        ActorIdentity east = Actor(1, 0, 0);
        AwardOrdinaryKills(driver, west, count: 5);
        AwardOrdinaryKills(driver, east, count: 3);

        Assert.Equal(5, driver.VeterancyTotalXp(west));
        Assert.Equal(3, driver.VeterancyTotalXp(east));

        AssertRetention(driver, west, expectedXp: 2, expectedLevel: 2);

        Assert.Equal(3, driver.VeterancyTotalXp(east));
        Assert.Equal(2, driver.VeterancyLevel(east));
        Assert.Equal(2, driver.VeterancyTotalXp(Actor(0, 0, 1)));
        Assert.Equal(3, driver.VeterancyTotalXp(Actor(1, 0, 1)));
    }

    private static void AssertRetention(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity victim,
        int expectedXp,
        int expectedLevel)
    {
        GenericActorModeEvent[] events = Kill(driver, victim);
        ArcRelayEvent.VeterancyRetained retained = Assert.Single(events)
            .Payload is GenericActorRuntimeObservation.EventPayload.ArcRelay
            {
                Fact: ArcRelayEvent.VeterancyRetained fact,
            }
                ? fact
                : throw new Xunit.Sdk.XunitException(
                    "Expected a veterancy-retained event.");
        Assert.Equal(expectedXp, retained.RetainedTotalXp);
        Assert.Equal(expectedLevel, retained.RetainedLevel);
    }

    private static GenericActorModeEvent[] Kill(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity victim) => driver.HandleDestructions(
        tick: 100 + victim.LifeId,
        [
            new FrontlineScrapDestruction(
                victim,
                new Position(10, 10),
                Actor(victim.TeamId == 0 ? 1 : 0, 7, victim.LifeId)),
        ]).Where(value => value.Payload is
            GenericActorRuntimeObservation.EventPayload.ArcRelay
            {
                Fact: ArcRelayEvent.VeterancyRetained,
            }).ToArray();

    private static void AwardOrdinaryKills(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity killer,
        int count)
    {
        int enemyTeam = killer.TeamId == 0 ? 1 : 0;
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            driver.HandleDestructions(
                tick: ordinal,
                [
                    new FrontlineScrapDestruction(
                        Actor(enemyTeam, ordinal + 10, 0),
                        new Position(11, 10),
                        killer),
                ]);
        }
    }

    private static ArcRelayActorMatchModeDriver Driver() => new(
        ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline15),
        matchSeed: 0);

    private static ActorIdentity Actor(int team, int unit, int life) =>
        ActorIdentity.FromTeamUnitLife(team, unit, life);
}
