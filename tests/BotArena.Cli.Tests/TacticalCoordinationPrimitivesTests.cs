using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class TacticalCoordinationPrimitivesTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    public void CarrierLeadAccountsForProjectileTravelTime(
        int distance,
        int expectedMovementSteps)
    {
        int actual = TacticalCoordinationPrimitives
            .CarrierMovementStepsBeforeProjectileContact(
                distance,
                instantRay: false,
                launchTiles: 1,
                tilesPerAdvance: 2,
                ticksPerAdvance: 1,
                advancesOnLaunchTick: false);

        Assert.Equal(expectedMovementSteps, actual);
    }

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
    public void UrgentCarrierMayPreemptAHeldButSaferCarrier()
    {
        Assert.True(TacticalCoordinationPrimitives.ShouldPreemptFocus(
            "urgent-carrier",
            candidatePriorityRank: 0,
            lockedPriorityRank: 0,
            candidateIsCarrier: true,
            lockedIsCarrier: true,
            candidateBankDistance: 1,
            lockedBankDistance: 5));
        Assert.False(TacticalCoordinationPrimitives.ShouldPreemptFocus(
            "urgent-carrier",
            candidatePriorityRank: 0,
            lockedPriorityRank: 0,
            candidateIsCarrier: true,
            lockedIsCarrier: true,
            candidateBankDistance: 5,
            lockedBankDistance: 1));
    }

    [Fact]
    public void NeverPreemptionPreservesTheExistingLockContract()
    {
        Assert.False(TacticalCoordinationPrimitives.ShouldPreemptFocus(
            "never",
            candidatePriorityRank: 0,
            lockedPriorityRank: 4,
            candidateIsCarrier: true,
            lockedIsCarrier: false,
            candidateBankDistance: 0,
            lockedBankDistance: 9));
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

    [Fact]
    public void SelfDefenseExcursionReturnsBeforeRejoiningFocus()
    {
        var assignment = new Position(10, 10);
        Assert.True(TacticalCoordinationPrimitives.IsSelfDefenseExcursion(
            new Position(16, 10), assignment, new Position(15, 10),
            chaseLeash: 4, selfDefenseEnabled: true,
            selfDefenseThreatDistance: 2));
        Assert.False(TacticalCoordinationPrimitives.HasReturnedToFormation(
            new Position(13, 10), assignment, arrivalRadius: 1));
        Assert.True(TacticalCoordinationPrimitives.HasReturnedToFormation(
            new Position(11, 10), assignment, arrivalRadius: 1));
    }

    [Fact]
    public void RepairProvidersHonorAuthoredSeparation()
    {
        Position[] assigned = [new Position(5, 5), new Position(10, 10)];

        Assert.False(TacticalCoordinationPrimitives.HonorsProviderSeparation(
            new Position(7, 5), assigned, minimumSeparation: 3));
        Assert.True(TacticalCoordinationPrimitives.HonorsProviderSeparation(
            new Position(8, 5), assigned, minimumSeparation: 3));
    }

    [Fact]
    public void EnemyCarrierInterceptSelectsMostImmediateBoundedThreat()
    {
        var fallback = new Position(24, 11);
        var enemyReactor = new Position(29, 11);
        TacticalCoordinationPrimitives.EnemyCarrierCandidate[] candidates =
        [
            new(new ActorIdentity(1, 7, 2), new Position(23, 8)),
            new(new ActorIdentity(1, 3, 4), new Position(27, 14)),
            new(new ActorIdentity(1, 1, 6), new Position(28, 19)),
        ];

        TacticalCoordinationPrimitives.EnemyCarrierCandidate? selected =
            TacticalCoordinationPrimitives.SelectEnemyCarrier(
                candidates, fallback, enemyReactor, pursuitRadius: 6);

        Assert.NotNull(selected);
        Assert.Equal(new ActorIdentity(1, 3, 4), selected.Value.ActorId);
    }

    [Fact]
    public void EnemyCarrierInterceptFallsBackWhenThreatIsOutsideItsLeash()
    {
        TacticalCoordinationPrimitives.EnemyCarrierCandidate? selected =
            TacticalCoordinationPrimitives.SelectEnemyCarrier(
                [new(new ActorIdentity(1, 1, 0), new Position(12, 2))],
                fallbackAnchor: new Position(24, 11),
                enemyReactor: new Position(29, 11),
                pursuitRadius: 4);

        Assert.Null(selected);
    }

    [Fact]
    public void SecuredCoreGuardUsesAllowedSourceAndStableNearestTie()
    {
        TacticalCoordinationPrimitives.SecuredCoreCandidate? selected =
            TacticalCoordinationPrimitives.SelectSecuredCore(
                [
                    new("south:1", "south", new Position(25, 10)),
                    new("north:2", "north", new Position(25, 12)),
                    new("north:1", "north", new Position(25, 10)),
                ],
                new HashSet<string>(["north"], StringComparer.Ordinal),
                fallbackAnchor: new Position(23, 11),
                guardRadius: 4);

        Assert.NotNull(selected);
        Assert.Equal("north:1", selected.Value.CoreKey);
    }

    [Theory]
    [InlineData("evade")]
    [InlineData("regroup")]
    [InlineData("hold")]
    [InlineData("self-defense")]
    public void EverySupportSurvivalFallbackRemainsDistinct(string fallback)
    {
        Assert.Equal(fallback,
            TacticalCoordinationPrimitives.SurvivalDirective(fallback));
    }

    [Theory]
    [InlineData("current-position", 0, 0, -1)]
    [InlineData("current-position", 0, 2, 1)]
    [InlineData("best-coverage", 0, 0, 0)]
    [InlineData("best-coverage", 1, 3, 1)]
    public void DodgeFallbackControlsWhetherZeroValueCoverageIsAttempted(
        string fallback,
        int first,
        int second,
        int expected) => Assert.Equal(
            expected,
            TacticalCoordinationPrimitives.CoverageFallbackIndex(
                fallback, [first, second]));

    [Fact]
    public void CarrierAimLeadsTheObservedMotionBeforeCoveringTheCurrentTile()
    {
        var current = new Position(10, 10);
        Position[] ordered = TacticalCoordinationPrimitives
            .OrderCarrierAimOptions(
                current,
                previous: new Position(9, 9),
                legalOptions:
                [
                    current,
                    new Position(11, 11),
                    new Position(11, 10),
                    new Position(10, 11),
                    new Position(9, 10),
                ],
                position => position.ChebyshevDistance(
                    new Position(20, 20)));

        Assert.Equal(new Position(11, 11), ordered[0]);
        Assert.Equal(current, ordered[1]);
    }

    [Fact]
    public void CarrierAimUsesTheBankwardStepWhenMotionHasNotResolved()
    {
        var current = new Position(10, 10);
        Position[] ordered = TacticalCoordinationPrimitives
            .OrderCarrierAimOptions(
                current,
                previous: current,
                legalOptions:
                [
                    current,
                    new Position(11, 10),
                    new Position(10, 11),
                    new Position(9, 10),
                ],
                position => position.ChebyshevDistance(
                    new Position(20, 10)));

        Assert.Equal(new Position(11, 10), ordered[0]);
        Assert.Equal(current, ordered[1]);
    }
}
