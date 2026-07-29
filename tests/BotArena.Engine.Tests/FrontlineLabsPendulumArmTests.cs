using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the phase-1 pendulum arms (DECISIONS #158) and the inert-default
/// fingerprint discipline that lets them exist: the hosted contract and every
/// historical arm keep their exact identity and bytes, each new arm is a
/// distinct content-identified ruleset, and the optional hold duration has
/// exactly one canonical encoding.
/// </summary>
public sealed class FrontlineLabsPendulumArmTests
{
    private const string BaselineRulesFingerprint =
        "ab63d409b682ad32fdb816c13cc3271413c2d0f6b1937e4933b6e455ff5d2593";

    private static readonly FrontlineLabsClassDefinition[] Classes =
    [
        FrontlineLabsClassDefinition.Bulwark,
        FrontlineLabsClassDefinition.Fabricator,
        FrontlineLabsClassDefinition.Striker,
    ];

    /// <summary>The four pre-registered phase-1 factor levels.</summary>
    public static TheoryData<FrontlineLabsPendulumArm, int, int, string>
        PhaseOneLevels() =>
        new()
        {
            {
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally,
                15,
                18,
                "frontline-labs-1-experiment-ratchet"
            },
            {
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority,
                15,
                18,
                "frontline-labs-1-experiment-ratchet-contest"
            },
            {
                FrontlineLabsPendulumArm.None,
                9,
                9,
                "frontline-labs-1-experiment-capture-9-respawn-9"
            },
        };

    [Fact]
    public void TheHostedContractKeepsItsExactIdentityAndFingerprint()
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();

        Assert.Equal(
            FrontlineLabsDefinition.RulesetId,
            baseline.Rules.RulesetId);
        Assert.Equal(
            BaselineRulesFingerprint,
            ActorContractFingerprint.ComputeRules(baseline.Rules));
        Assert.Equal(
            FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause,
            Capture(baseline).RedeployPolicy);
        Assert.Equal(0, Capture(baseline).RatchetHoldTicks);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims,
            baseline.Rules.Lifecycle.AutomaticReturnPlacement);
    }

    [Fact]
    public void CanonicalRulesOmitTheHoldUntilAFrontlineIsSticky()
    {
        string baseline = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition.Create().Rules);
        string sticky = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition
                .CreatePendulumExperiment(
                    FrontlineLabsPendulumArm.StickyFrontline)
                .Rules);

        Assert.DoesNotContain(
            "ratchetHoldTicks",
            baseline,
            StringComparison.Ordinal);
        Assert.Contains(
            $"\"ratchetHoldTicks\":{FrontlineLabsDefinition
                .RatchetHoldTicksDefault}",
            sticky,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheCanonicalMirrorRejectsBothHalvesOfTheInertEncoding()
    {
        string sticky = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline));
        string baseline = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition.Create());

        // An explicitly inert hold is a second encoding of the baseline.
        string inertHold = baseline.Replace(
            "\"redeployPolicy\":\"advance-immediately-reset-claim-keep-world-pause-through-capture-plus-configured-ticks-breach-skips-pause\"",
            "\"redeployPolicy\":\"advance-immediately-reset-claim-keep-world-pause-through-capture-plus-configured-ticks-breach-skips-pause\",\"ratchetHoldTicks\":0",
            StringComparison.Ordinal);
        // A sticky frontline without its hold is an incomplete contract.
        string missingHold = sticky.Replace(
            $",\"ratchetHoldTicks\":{FrontlineLabsDefinition
                .RatchetHoldTicksDefault}",
            string.Empty,
            StringComparison.Ordinal);

        Assert.NotEqual(baseline, inertHold);
        Assert.NotEqual(sticky, missingHold);
        // The mirror rejects both before it ever reaches the fingerprint, so
        // the rejection is about the encoding rather than the hash.
        Assert.Contains(
            "ratchetHoldTicks",
            Assert.Throws<FormatException>(() =>
                GenericActorCanonicalContractValidator.Validate(inertHold))
                .Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "ratchetHoldTicks",
            Assert.Throws<FormatException>(() =>
                GenericActorCanonicalContractValidator.Validate(missingHold))
                .Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PhaseOneLevels))]
    public void EachPhaseOneLevelIsADistinctContentIdentifiedRuleset(
        FrontlineLabsPendulumArm pendulum,
        int captureThreshold,
        int primeRespawnTicks,
        string expectedRulesetId)
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition arm =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                pendulum,
                captureThreshold: captureThreshold,
                primeRespawnTicks: primeRespawnTicks);

        Assert.Equal(expectedRulesetId, arm.Rules.RulesetId);
        Assert.True(arm.Rules.RulesetId.Length <= 64);
        // Only the rules move: the map and the format are held constant so
        // the factorial isolates the counterweight.
        Assert.Equal(baseline.Map.Id, arm.Map.Id);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(baseline.Map),
            ActorContractFingerprint.ComputeMap(arm.Map));
        Assert.NotEqual(
            BaselineRulesFingerprint,
            ActorContractFingerprint.ComputeRules(arm.Rules));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(arm));

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(
                ActorContractManifestSerializer.ToCanonicalJson(arm));
        Assert.Equal(arm.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(arm),
            validation.MatchContractFingerprint);
    }

    [Fact]
    public void EachFactorSelectsExactlyItsOwnTypedPolicy()
    {
        ActorResolvedMatchDefinition sticky =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline);
        ActorResolvedMatchDefinition rally =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ForwardRally);
        ActorResolvedMatchDefinition majority =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ContestMajority);
        ActorResolvedMatchDefinition decay =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.EnemySoleDecay);

        Assert.Equal(
            FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks,
            Capture(sticky).RedeployPolicy);
        Assert.Equal(
            FrontlineLabsDefinition.RatchetHoldTicksDefault,
            Capture(sticky).RatchetHoldTicks);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn,
            rally.Rules.Lifecycle.AutomaticReturnPlacement);
        Assert.Equal(
            FrontlineCaptureDefinition.ControlPolicyKind
                .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral,
            Capture(majority).ControlPolicy);
        Assert.Equal(
            FrontlineCaptureDefinition.DecayClockKind
                .EmptyAndContestedTicksPreserveClaimEnemySoleErosionOnly,
            Capture(decay).DecayClock);

        // Every other factor stays inert in each single-factor arm.
        Assert.Equal(
            FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
            Capture(sticky).ControlPolicy);
        Assert.Equal(0, Capture(rally).RatchetHoldTicks);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims,
            majority.Rules.Lifecycle.AutomaticReturnPlacement);
        Assert.Equal(
            FrontlineCaptureDefinition.DecayClockKind
                .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl,
            Capture(sticky).DecayClock);
    }

    [Fact]
    public void TheNumbersOnlyLevelRetunesOnlyThresholdAndPrimeRespawn()
    {
        ActorResolvedMatchDefinition numbers =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                captureThreshold: 9,
                primeRespawnTicks: 9);

        Assert.Equal(9, Capture(numbers).Threshold);
        Assert.Equal(
            9,
            numbers.Rules.Lifecycle.Profiles
                .Single(profile => profile.ProfileId == "prime-respawn")
                .DelayTicks);
        // The child rebuild clock is not part of N3.
        Assert.Equal(
            30,
            numbers.Rules.Lifecycle.Profiles
                .Single(profile => profile.ProfileId == "child-ready")
                .DelayTicks);
        Assert.Equal(
            FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause,
            Capture(numbers).RedeployPolicy);
    }

    [Fact]
    public void EveryPhaseOneCellComposesWithClassesAndMovementUnderTheCap()
    {
        FrontlineLabsPendulumArm[] structural =
        [
            FrontlineLabsPendulumArm.None,
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally,
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally
                | FrontlineLabsPendulumArm.ContestMajority,
        ];
        ActorMovementFacingCoupling[] couplings =
        [
            ActorMovementFacingCoupling.PreserveFacing,
            ActorMovementFacingCoupling.FaceMovementDirection,
            ActorMovementFacingCoupling.FacingLocked,
        ];
        var rulesetIds = new HashSet<string>();
        var matchFingerprints = new HashSet<string>
        {
            ActorContractFingerprint.ComputeMatch(
                FrontlineLabsDefinition.Create()),
        };

        foreach (var pair in CanonicalPairs())
        {
            foreach (ActorMovementFacingCoupling coupling in couplings)
            {
                foreach (FrontlineLabsPendulumArm pendulum in structural)
                {
                    // The numbers-only level is the fourth factor level and
                    // carries no structural flag.
                    (FrontlineLabsPendulumArm Arm, int Threshold, int Respawn)
                        cell = pendulum == FrontlineLabsPendulumArm.None
                            ? (pendulum, 9, 9)
                            : (pendulum, 15, 18);
                    ActorResolvedMatchDefinition definition =
                        FrontlineLabsDefinition.CreatePendulumExperiment(
                            cell.Arm,
                            (pair.TeamZero, pair.TeamOne),
                            FrontlineLabsDuelMapArm.Current,
                            coupling,
                            cell.Threshold,
                            cell.Respawn);

                    Assert.True(
                        definition.Rules.RulesetId.Length <= 64,
                        $"{definition.Rules.RulesetId} exceeds the canonical "
                        + "ID budget");
                    Assert.True(
                        rulesetIds.Add(definition.Rules.RulesetId),
                        $"duplicate ruleset {definition.Rules.RulesetId}");
                    Assert.True(
                        matchFingerprints.Add(
                            ActorContractFingerprint.ComputeMatch(
                                definition)),
                        $"duplicate fingerprint for "
                        + definition.Rules.RulesetId);
                    Assert.Equal(
                        coupling,
                        definition.Rules.MovementProfiles.Single()
                            .FacingCoupling);
                    // The class arms keep their seed profile: a pendulum
                    // level is an added factor, not a new arm family.
                    Assert.Equal(
                        FrontlineLabsDefinition.ClassesSeedProfileId,
                        definition.Rules.SeedMechanics.SeedProfileId);

                    GenericActorCanonicalContractValidation validation =
                        GenericActorCanonicalContractValidator.Validate(
                            ActorContractManifestSerializer.ToCanonicalJson(
                                definition));
                    Assert.Equal(
                        definition.Rules.RulesetId,
                        validation.RulesetId);
                }
            }
        }

        Assert.Equal(54, rulesetIds.Count);
    }

    [Theory]
    [InlineData("frontline-labs-1-bulwark-vs-striker-ratchet")]
    public void AComposedArmDropsTheClassesSegmentUnderTheIdBudget(
        string expectedRulesetId)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker));

        Assert.Equal(expectedRulesetId, definition.Rules.RulesetId);
        Assert.Equal(
            $"{FrontlineLabsDefinition.MapId}-classes",
            definition.Map.Id);
    }

    [Fact]
    public void HistoricalArmsKeepTheirIdentityAndFingerprintsExactly()
    {
        ActorResolvedMatchDefinition classes =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker);
        ActorResolvedMatchDefinition coupled =
            FrontlineLabsDefinition.CreateMovementCouplingExperiment(
                ActorMovementFacingCoupling.FacingLocked);
        ActorResolvedMatchDefinition netControl =
            FrontlineLabsDefinition.CreateNetControlExperiment();
        ActorResolvedMatchDefinition threshold =
            FrontlineLabsDefinition.CreateCaptureThresholdExperiment(9);

        Assert.Equal(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            classes.Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-experiment-facing-locked",
            coupled.Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-experiment-net-control",
            netControl.Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-experiment-capture-9",
            threshold.Rules.RulesetId);
        // The contest-majority arm reuses the already-implemented control
        // policy, so its rules differ from the historical net-control arm
        // only by identity — the mechanic itself is byte-identical.
        Assert.Equal(
            Capture(netControl).ControlPolicy,
            Capture(
                    FrontlineLabsDefinition.CreatePendulumExperiment(
                        FrontlineLabsPendulumArm.ContestMajority))
                .ControlPolicy);
    }

    [Fact]
    public void TheUncounterweightedContractIsTheControlAndNotAnArm()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None));
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline,
                (FrontlineLabsClassDefinition.Striker,
                    FrontlineLabsClassDefinition.Bulwark)));
    }

    private static FrontlineCaptureDefinition Capture(
        ActorResolvedMatchDefinition definition) =>
        Assert.IsType<FrontlineGameModeDefinition>(
                definition.Rules.GameMode)
            .Capture;

    private static IEnumerable<(
        FrontlineLabsClassDefinition TeamZero,
        FrontlineLabsClassDefinition TeamOne)> CanonicalPairs()
    {
        for (int first = 0; first < Classes.Length; first++)
        {
            for (int second = first; second < Classes.Length; second++)
                yield return (Classes[first], Classes[second]);
        }
    }
}
