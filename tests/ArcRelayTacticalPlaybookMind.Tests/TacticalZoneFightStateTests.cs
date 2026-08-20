using System.Collections.Immutable;
using BotArena.Sdk;

public sealed class TacticalZoneFightStateTests
{
    private static readonly ActorIdentity Friendly = new(0, 0, 0);
    private static readonly ActorIdentity OtherFriendly = new(0, 1, 0);
    private static readonly ActorIdentity Enemy = new(1, 0, 0);

    [Fact]
    public void FighterOrTargetInsideCountsButUnrelatedFightDoesNot()
    {
        Assert.Equal(1, Count(
            Fight(Friendly, fighter: new Position(2, 2), target: new Position(8, 8))));
        Assert.Equal(1, Count(
            Fight(Friendly, fighter: new Position(8, 8), target: new Position(2, 2))));
        Assert.Equal(0, Count(
            Fight(Friendly, fighter: new Position(8, 8), target: new Position(9, 8))));
    }

    [Fact]
    public void PendingStrikeIntoZoneCountsFromOutsideAndIsNotDoubleCounted()
    {
        var strike = new GenericActorContext.ArcRelayPendingStrike(
            Friendly,
            12,
            new Position(8, 8),
            ProjectileHeading.East,
            Enemy,
            ImmutableArray.Create(new Position(2, 2)));

        Assert.Equal(1, TacticalZoneFightState.Count(
            0,
            InZone,
            [Fight(Friendly, new Position(8, 8), new Position(2, 2))],
            [strike]));
    }

    [Fact]
    public void OnlyFriendlySelectionsAndStrikesCount()
    {
        var enemyStrike = new GenericActorContext.ArcRelayPendingStrike(
            Enemy,
            12,
            new Position(2, 2),
            ProjectileHeading.West,
            Friendly,
            ImmutableArray.Create(new Position(2, 2)));

        Assert.Equal(0, TacticalZoneFightState.Count(
            0,
            InZone,
            [Fight(Enemy, new Position(2, 2), new Position(3, 2))],
            [enemyStrike]));
    }

    [Fact]
    public void LocalFightEndsWhenNoFightLeafOrPendingStrikeRemains()
    {
        Assert.Equal(1, Count(
            Fight(OtherFriendly, new Position(2, 2), new Position(3, 2))));
        Assert.Equal(0, TacticalZoneFightState.Count(
            0, InZone, [], []));
    }

    private static TacticalZoneFightState.ActiveFight Fight(
        ActorIdentity actor,
        Position fighter,
        Position target) => new(actor, fighter, target, "fight-visible");

    private static int Count(TacticalZoneFightState.ActiveFight fight) =>
        TacticalZoneFightState.Count(0, InZone, [fight], []);

    private static bool InZone(Position position) =>
        position.X is >= 1 and <= 3 && position.Y is >= 1 and <= 3;
}
