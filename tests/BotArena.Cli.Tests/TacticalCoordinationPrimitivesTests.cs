using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalCoordinationPrimitivesTests
{
    [Fact]
    public void OneControllerPerSignatureAndTargetWinsDeterministically()
    {
        var firstTarget = new ActorIdentity(1, 2, 3);
        var secondTarget = new ActorIdentity(1, 4, 5);
        TacticalCoordinationPrimitives.SignatureCandidate[] candidates =
        [
            new(7, firstTarget, "target-paint"),
            new(2, firstTarget, "target-paint"),
            new(6, firstTarget, "tractor-hook"),
            new(5, secondTarget, "target-paint"),
            new(1, secondTarget, "target-paint"),
        ];

        HashSet<int> selected = TacticalCoordinationPrimitives
            .SelectSignatureControllers(candidates);

        Assert.Equal([1, 2, 6], selected.Order());
    }

    [Fact]
    public void EscapeCoverageMayUseAttackersAfterDirectDamageIsBudgeted()
    {
        Assert.False(TacticalCoordinationPrimitives.NeedsFocusAssignment(
            targetHealth: 3,
            committedDamage: 3,
            permittedOverkillDamage: 0,
            coverEscapeLanes: false,
            coveredOptions: 0,
            minimumCoveredOptions: 2));
        Assert.True(TacticalCoordinationPrimitives.NeedsFocusAssignment(
            targetHealth: 3,
            committedDamage: 3,
            permittedOverkillDamage: 0,
            coverEscapeLanes: true,
            coveredOptions: 1,
            minimumCoveredOptions: 2));
        Assert.False(TacticalCoordinationPrimitives.NeedsFocusAssignment(
            targetHealth: 3,
            committedDamage: 3,
            permittedOverkillDamage: 0,
            coverEscapeLanes: true,
            coveredOptions: 2,
            minimumCoveredOptions: 2));
    }

    [Theory]
    [InlineData(true, true, false, true, true, 0, 2, true)]
    [InlineData(true, false, false, true, true, 0, 2, false)]
    [InlineData(false, true, true, true, true, 0, 2, true)]
    [InlineData(false, true, true, false, true, 0, 2, false)]
    [InlineData(false, true, false, true, false, 1, 2, false)]
    [InlineData(false, true, false, true, false, 2, 2, true)]
    [InlineData(false, true, false, true, true, 9, 2, false)]
    public void FocusReleaseHonorsEachDeclaredCause(
        bool destroyed,
        bool releaseOnDestroyed,
        bool outsideLeash,
        bool releaseOutsideLeash,
        bool reachable,
        int unreachableTicks,
        int releaseAfterUnreachableTicks,
        bool expected)
    {
        Assert.Equal(expected,
            TacticalCoordinationPrimitives.ShouldReleaseFocus(
                destroyed,
                releaseOnDestroyed,
                outsideLeash,
                releaseOutsideLeash,
                reachable,
                unreachableTicks,
                releaseAfterUnreachableTicks));
    }

    [Fact]
    public void EngagementLeashRequiresFormationAdherenceOrSelfDefense()
    {
        var assignment = new Position(10, 10);
        var bodyFarFromItsSlot = new Position(2, 2);

        Assert.False(TacticalCoordinationPrimitives.IsWithinEngagementLeash(
            assignment,
            enemyPosition: new Position(12, 10),
            bodyFarFromItsSlot,
            chaseLeash: 2,
            selfDefenseEnabled: false,
            selfDefenseThreatDistance: 0));
        Assert.True(TacticalCoordinationPrimitives.IsWithinEngagementLeash(
            assignment,
            enemyPosition: new Position(2, 10),
            bodyPosition: new Position(9, 10),
            chaseLeash: 2,
            selfDefenseEnabled: false,
            selfDefenseThreatDistance: 0));
        Assert.True(TacticalCoordinationPrimitives.IsWithinEngagementLeash(
            assignment,
            enemyPosition: new Position(3, 2),
            bodyFarFromItsSlot,
            chaseLeash: 2,
            selfDefenseEnabled: true,
            selfDefenseThreatDistance: 1));
    }
}
