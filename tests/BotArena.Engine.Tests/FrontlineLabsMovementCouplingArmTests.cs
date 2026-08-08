using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the pre-registered movement-facing-coupling arms (DECISIONS #156) and
/// the inert-default fingerprint discipline that lets the capability exist at
/// all: a PreserveFacing contract must be byte-identical to the contract that
/// never named the field, while every coupled arm must be a distinct,
/// content-identified ruleset that still resolves, validates, and runs.
/// </summary>
public sealed class FrontlineLabsMovementCouplingArmTests
{
    private static readonly FrontlineLabsClassDefinition[] Classes =
    [
        FrontlineLabsClassDefinition.Bulwark,
        FrontlineLabsClassDefinition.Fabricator,
        FrontlineLabsClassDefinition.Striker,
    ];

    private static IEnumerable<(
        FrontlineLabsClassDefinition TeamZero,
        FrontlineLabsClassDefinition TeamOne)> CanonicalPairs()
    {
        for (int first = 0; first < Classes.Length; first++)
        {
            for (int second = first; second < Classes.Length; second++)
            {
                yield return (Classes[first], Classes[second]);
            }
        }
    }

    [Fact]
    public void PreserveFacingProfileIsByteIdenticalToTheUncoupledProfile()
    {
        var omitted = new ActorMovementProfileDefinition(
            "ground",
            ActorMovementLayer.Ground);
        var explicitDefault = new ActorMovementProfileDefinition(
            "ground",
            ActorMovementLayer.Ground,
            ActorMovementFacingCoupling.PreserveFacing);

        Assert.Equal(
            ActorMovementFacingCoupling.PreserveFacing,
            omitted.FacingCoupling);
        Assert.Equal(omitted, explicitDefault);
    }

    [Fact]
    public void CanonicalRulesOmitTheFieldUntilItIsNotPreserveFacing()
    {
        string baseline = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition.Create().Rules);
        string coupled = ActorContractManifestSerializer.ToCanonicalJson(
            FrontlineLabsDefinition
                .CreateMovementCouplingExperiment(
                    ActorMovementFacingCoupling.FaceMovementDirection)
                .Rules);

        Assert.DoesNotContain(
            "facingCoupling",
            baseline,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"facingCoupling\":\"face-movement-direction\"",
            coupled,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PreserveFacingArmsKeepTheirExistingIdentityAndFingerprints()
    {
        foreach (var pair in CanonicalPairs())
        {
            ActorResolvedMatchDefinition uncoupled =
                FrontlineLabsDefinition.CreateClassesExperiment(
                    pair.TeamZero,
                    pair.TeamOne);
            ActorResolvedMatchDefinition explicitDefault =
                FrontlineLabsDefinition.CreateClassesExperiment(
                    pair.TeamZero,
                    pair.TeamOne,
                    FrontlineLabsDuelMapArm.Current,
                    ActorMovementFacingCoupling.PreserveFacing);

            Assert.Equal(
                "frontline-labs-1-experiment-classes-"
                + $"{pair.TeamZero.Id}-vs-{pair.TeamOne.Id}",
                explicitDefault.Rules.RulesetId);
            Assert.Equal(
                uncoupled.Rules.RulesetId,
                explicitDefault.Rules.RulesetId);
            Assert.Equal(
                ActorContractFingerprint.ComputeRules(uncoupled.Rules),
                ActorContractFingerprint.ComputeRules(
                    explicitDefault.Rules));
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(uncoupled),
                ActorContractFingerprint.ComputeMatch(explicitDefault));
        }
    }

    [Theory]
    [InlineData(
        ActorMovementFacingCoupling.FaceMovementDirection,
        "frontline-labs-1-experiment-move-sets-facing")]
    [InlineData(
        ActorMovementFacingCoupling.FacingLocked,
        "frontline-labs-1-experiment-facing-locked")]
    public void BaseCouplingArmsAreContentIdentifiedAndDistinct(
        ActorMovementFacingCoupling coupling,
        string expectedRulesetId)
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition arm = FrontlineLabsDefinition
            .CreateMovementCouplingExperiment(coupling);

        Assert.Equal(expectedRulesetId, arm.Rules.RulesetId);
        Assert.Equal(
            coupling,
            arm.Rules.MovementProfiles.Single().FacingCoupling);
        // Only the kinematics move: the map and the seed profile are held
        // constant so the A/B isolates one mechanic.
        Assert.Equal(baseline.Map.Id, arm.Map.Id);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(baseline.Map),
            ActorContractFingerprint.ComputeMap(arm.Map));
        // Base-family arms derive their seed profile from their own ruleset
        // ID, exactly like the capture-threshold and mobilize arms beside
        // them; only the duel-depth and classes families share an explicit
        // profile.
        Assert.Equal(
            expectedRulesetId,
            arm.Rules.SeedMechanics.SeedProfileId);
        Assert.Equal(
            FrontlineLabsDefinition.RulesetId,
            baseline.Rules.SeedMechanics.SeedProfileId);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(arm.Rules));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(arm));
    }

    [Fact]
    public void PreserveFacingIsTheBaselineAndNotASeparateArm()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.CreateMovementCouplingExperiment(
                ActorMovementFacingCoupling.PreserveFacing));
    }

    [Fact]
    public void EveryCanonicalClassPairComposesWithEveryCouplingArm()
    {
        var rulesetIds = new HashSet<string>();
        var matchFingerprints = new HashSet<string>
        {
            ActorContractFingerprint.ComputeMatch(
                FrontlineLabsDefinition.Create()),
        };
        ActorMovementFacingCoupling[] couplings =
        [
            ActorMovementFacingCoupling.PreserveFacing,
            ActorMovementFacingCoupling.FaceMovementDirection,
            ActorMovementFacingCoupling.FacingLocked,
        ];

        foreach (var pair in CanonicalPairs())
        {
            foreach (ActorMovementFacingCoupling coupling in couplings)
            {
                ActorResolvedMatchDefinition definition =
                    FrontlineLabsDefinition.CreateClassesExperiment(
                        pair.TeamZero,
                        pair.TeamOne,
                        FrontlineLabsDuelMapArm.Current,
                        coupling);

                Assert.True(
                    definition.Rules.RulesetId.Length <= 64,
                    $"{definition.Rules.RulesetId} exceeds the canonical ID "
                    + "budget");
                Assert.True(
                    rulesetIds.Add(definition.Rules.RulesetId),
                    $"duplicate ruleset {definition.Rules.RulesetId}");
                Assert.True(
                    matchFingerprints.Add(
                        ActorContractFingerprint.ComputeMatch(definition)),
                    $"duplicate fingerprint for {definition.Rules.RulesetId}");
                Assert.Equal(
                    coupling,
                    definition.Rules.MovementProfiles.Single()
                        .FacingCoupling);
                // The class arms keep their own seed profile: the coupling is
                // an added factor, not a new arm family.
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
                Assert.Equal(
                    ActorContractFingerprint.ComputeMatch(definition),
                    validation.MatchContractFingerprint);
            }
        }

        Assert.Equal(18, rulesetIds.Count);
    }

    [Theory]
    [InlineData(
        ActorMovementFacingCoupling.FaceMovementDirection,
        "frontline-labs-1-classes-bulwark-vs-striker-sets-facing")]
    [InlineData(
        ActorMovementFacingCoupling.FacingLocked,
        "frontline-labs-1-classes-bulwark-vs-striker-facing-locked")]
    public void ComposedArmsCarryBothFactorsInOneIdentity(
        ActorMovementFacingCoupling coupling,
        string expectedRulesetId)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsDuelMapArm.Current,
                coupling);

        Assert.Equal(expectedRulesetId, definition.Rules.RulesetId);
        Assert.Equal(
            $"{FrontlineLabsDefinition.MapId}-classes",
            definition.Map.Id);
    }

    [Fact]
    public void ACoupledClassesArmStepsWithoutFaults()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsDuelMapArm.Current,
                ActorMovementFacingCoupling.FaceMovementDirection);
        using GenericActorMatchSession session =
            MovementFacingCouplingTestContracts.Session(
                definition,
                (_, observation) => observation.Tick % 3 == 2
                    ? GenericDeathmatchSessionTestFixture.Wait()
                    : GenericDeathmatchSessionTestFixture.Move(
                        observation.Self.Facing));

        for (int tick = 0; tick < 12; tick++)
        {
            GenericActorMatchStepResult step = session.Step(
                session.PrepareTick().Observations);
            Assert.DoesNotContain(
                step.Events,
                item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.RuntimeFault);
            if (step.IsCompleted)
                break;
        }

        Assert.Equal(
            2,
            session.Chronology.Descriptor.Participants.Length);
        Assert.All(
            session.Chronology.Ticks.SelectMany(tick => tick.Events),
            item => Assert.NotEqual(
                GenericActorRuntimeObservation.EventKind.RuntimeFault,
                item.Kind));
    }
}
