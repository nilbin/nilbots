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
}
