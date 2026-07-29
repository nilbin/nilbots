using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the class-skill kit as an arm family: each skill belongs to exactly
/// one class, an arm identity is a function of the contract it actually
/// produces, every cell composes with the movement and pendulum factors under
/// the 64-character canonical-ID budget, and — the load-bearing part — the
/// hosted contract and every historical arm keep their exact bytes.
/// </summary>
public sealed class FrontlineLabsSkillArmTests
{
    private const string BaselineRulesFingerprint =
        "ab63d409b682ad32fdb816c13cc3271413c2d0f6b1937e4933b6e455ff5d2593";

    [Fact]
    public void TheHostedContractIsUntouchedByTheSkillCapabilities()
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            baseline.Rules);

        Assert.Equal(
            BaselineRulesFingerprint,
            ActorContractFingerprint.ComputeRules(baseline.Rules));
        Assert.DoesNotContain(
            "projectileGuard",
            canonical,
            StringComparison.Ordinal);
        Assert.DoesNotContain("volley", canonical, StringComparison.Ordinal);
        Assert.All(
            baseline.Rules.Forms,
            form => Assert.Equal(
                ActorFormProjectileGuardKind.None,
                form.ProjectileGuard));
        Assert.All(
            baseline.Rules.AttackProfiles,
            profile =>
            {
                Assert.Null(profile.Volley);
                Assert.Equal(1, profile.ProjectilesPerAttack);
            });
        Assert.Equal(
            FrontlineLabsDefinition.TopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(baseline.Topology));
    }

    [Fact]
    public void EachSkillBelongsToExactlyOneClass()
    {
        Assert.Equal(
            FrontlineLabsSkillKit.StrikerVolley,
            FrontlineLabsClassDefinition.Striker.Skill);
        Assert.Equal(
            FrontlineLabsSkillKit.BulwarkAegisShell,
            FrontlineLabsClassDefinition.Bulwark.Skill);
        Assert.Equal(
            FrontlineLabsSkillKit.FabricatorFiveSlots,
            FrontlineLabsClassDefinition.Fabricator.Skill);
        Assert.Equal(
            FrontlineLabsDefinition.Skills.Length,
            FrontlineLabsClassDefinition.All
                .Select(entry => entry.Skill)
                .Distinct()
                .Count());
    }

    [Fact]
    public void AnArmCarriesOnlyTheSkillsItsClassesOwn()
    {
        // A skill whose owning class is absent changes no contract bytes, so
        // it must change no arm identity either: `kit` and the explicit subset
        // resolve to the same content-identified ruleset.
        ActorResolvedMatchDefinition kit =
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.StrikerVolley
                    | FrontlineLabsSkillKit.BulwarkAegisShell
                    | FrontlineLabsSkillKit.FabricatorFiveSlots);
        ActorResolvedMatchDefinition subset =
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.StrikerVolley
                    | FrontlineLabsSkillKit.BulwarkAegisShell);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-fan-shell",
            kit.Rules.RulesetId);
        Assert.Equal(subset.Rules.RulesetId, kit.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(subset),
            ActorContractFingerprint.ComputeMatch(kit));
    }

    [Fact]
    public void RequestingASkillNoClassInTheCellOwnsIsRejected()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.FabricatorFiveSlots));

        Assert.Contains(
            "fabricator",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SkillsRequireAClassCellAndRejectUnknownFlags()
    {
        // A skill is a class capability, so there is no class-free skill arm.
        Assert.Contains(
            "without a class pair",
            Assert.Throws<ArgumentException>(() =>
                    FrontlineLabsDefinition.CreatePendulumExperiment(
                        FrontlineLabsPendulumArm.None,
                        classes: null,
                        skills: FrontlineLabsSkillKit.StrikerVolley))
                .Message,
            StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (FrontlineLabsClassDefinition.Striker,
                    FrontlineLabsClassDefinition.Striker),
                skills: (FrontlineLabsSkillKit)64));
    }

    [Fact]
    public void EverySkillCellComposesWithMovementAndPendulumUnderTheCap()
    {
        FrontlineLabsPendulumArm[] structural =
        [
            FrontlineLabsPendulumArm.None,
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally,
        ];
        ActorMovementFacingCoupling[] couplings =
        [
            ActorMovementFacingCoupling.PreserveFacing,
            ActorMovementFacingCoupling.FacingLocked,
        ];
        var rulesetIds = new HashSet<string>();
        var matchFingerprints = new HashSet<string>();
        int cells = 0;

        foreach (var pair in FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            FrontlineLabsSkillKit owned =
                pair.TeamZero.Skill | pair.TeamOne.Skill;
            foreach (ActorMovementFacingCoupling coupling in couplings)
            {
                foreach (FrontlineLabsPendulumArm pendulum in structural)
                {
                    // The pendulum token plus the longest class pair plus the
                    // longest coupling already spends the ID budget, so the
                    // registered phase-2 cells hold the pendulum at control
                    // when the coupling is deep.
                    if (pendulum != FrontlineLabsPendulumArm.None
                        && coupling
                            != ActorMovementFacingCoupling.PreserveFacing)
                    {
                        continue;
                    }
                    ActorResolvedMatchDefinition definition =
                        FrontlineLabsDefinition.CreatePendulumExperiment(
                            pendulum,
                            (pair.TeamZero, pair.TeamOne),
                            FrontlineLabsDuelMapArm.Current,
                            coupling,
                            skills: owned);
                    cells++;

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
                        "duplicate fingerprint for "
                        + definition.Rules.RulesetId);
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

        Assert.Equal(18, cells);
    }

    [Fact]
    public void EverySkilledCellIsDistinctFromItsUnskilledCounterpart()
    {
        foreach (var pair in FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            FrontlineLabsSkillKit owned =
                pair.TeamZero.Skill | pair.TeamOne.Skill;
            ActorResolvedMatchDefinition skilled =
                FrontlineLabsSkillArmTestFixture.Arm(
                    pair.TeamZero,
                    pair.TeamOne,
                    owned);
            ActorResolvedMatchDefinition plain =
                FrontlineLabsDefinition.CreateClassesExperiment(
                    pair.TeamZero,
                    pair.TeamOne);

            Assert.NotEqual(plain.Rules.RulesetId, skilled.Rules.RulesetId);
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(plain),
                ActorContractFingerprint.ComputeMatch(skilled));
            // Every single-skill subset is also its own distinct arm.
            foreach (FrontlineLabsSkillKit skill in
                     FrontlineLabsDefinition.Skills.Where(
                         skill => owned.HasFlag(skill)))
            {
                ActorResolvedMatchDefinition single =
                    FrontlineLabsSkillArmTestFixture.Arm(
                        pair.TeamZero,
                        pair.TeamOne,
                        skill);
                Assert.NotEqual(
                    ActorContractFingerprint.ComputeMatch(plain),
                    ActorContractFingerprint.ComputeMatch(single));
            }
        }
    }

    [Fact]
    public void HistoricalArmsKeepTheirIdentityAndFingerprintsExactly()
    {
        Assert.Equal(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker).Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-experiment-facing-locked",
            FrontlineLabsDefinition.CreateMovementCouplingExperiment(
                ActorMovementFacingCoupling.FacingLocked).Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-experiment-ratchet",
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally)
                .Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-ratchet",
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker))
                .Rules.RulesetId);
    }

    [Fact]
    public void ASkilledCellStillComposesWithThePendulumAndTheNumbers()
    {
        ActorResolvedMatchDefinition cell =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                skills: FrontlineLabsSkillKit.BulwarkAegisShell);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-contest-shell",
            cell.Rules.RulesetId);
        Assert.Equal(
            FrontlineLabsDefinition.RatchetHoldTicksDefault,
            ((FrontlineGameModeDefinition)cell.Rules.GameMode)
                .Capture.RatchetHoldTicks);
        Assert.Contains(
            cell.Rules.Forms,
            form => form.ProjectileGuard
                == ActorFormProjectileGuardKind
                    .FacingQuadrantContactsConsumedWithoutDamage);
    }

    [Fact]
    public void AnOverfullCellNamesTheFactorToDrop()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    FrontlineLabsPendulumArm.StickyFrontline
                        | FrontlineLabsPendulumArm.ForwardRally
                        | FrontlineLabsPendulumArm.ContestMajority,
                    (FrontlineLabsClassDefinition.Bulwark,
                        FrontlineLabsClassDefinition.Fabricator),
                    FrontlineLabsDuelMapArm.Current,
                    ActorMovementFacingCoupling.FacingLocked,
                    skills: FrontlineLabsSkillKit.BulwarkAegisShell
                        | FrontlineLabsSkillKit.FabricatorFiveSlots));

        Assert.Contains(
            "canonical characters",
            error.Message,
            StringComparison.Ordinal);
    }
}
