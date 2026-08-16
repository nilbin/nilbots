using BotArena.Sdk;

public sealed class WellBirthConditionTests
{
    [Fact]
    public void EffectiveCountdownCoversOpeningAndRechargeButNotActiveCore()
    {
        var opening = new GenericActorContext.ArcRelayWellState(
            "centre",
            new Position(15, 13),
            NextScheduledBirthTick: 50,
            OutstandingCoreId: null,
            PendingCharge: false,
            RearmCompletesAtTick: null);
        Assert.Equal(
            8,
            ArcRelayTacticalPlaybookMind.WellTicksUntilBirth(opening, 42));

        var active = opening with
        {
            NextScheduledBirthTick = null,
            OutstandingCoreId = new GenericActorContext.ArcRelayCoreId(
                "centre", 0),
        };
        Assert.Equal(
            int.MaxValue,
            ArcRelayTacticalPlaybookMind.WellTicksUntilBirth(active, 100));

        var recharging = opening with
        {
            NextScheduledBirthTick = 146,
            RearmCompletesAtTick = 146,
        };
        Assert.Equal(
            46,
            ArcRelayTacticalPlaybookMind.WellTicksUntilBirth(
                recharging, 100));
        Assert.Equal(
            0,
            ArcRelayTacticalPlaybookMind.WellTicksUntilBirth(
                recharging, 146));
    }
}
