using System.Collections.Immutable;
using BotArena.Engine;

namespace BotArena.Engine.Tests;

/// <summary>
/// Prime dissolution (DECISIONS #194): one chassis, one lifecycle, one action
/// catalog per class — plus the two consequences that travel with it, the
/// headless fabrication network and the widened upgrade scope.
/// </summary>
public class FrontlineLabsChassisArmTests
{
    private static ActorResolvedMatchDefinition Warpath(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsChassisArm chassis,
        int? tierCost = null) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ContestMajority
                | FrontlineLabsPendulumArm.EnemySoleDecay,
            (teamZero, teamOne),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: FrontlineLabsSkillKit.StrikerVolley
                | FrontlineLabsSkillKit.BulwarkAegisShell
                | FrontlineLabsSkillKit.FabricatorFiveSlots,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: teamZero.ExplicitForwardFabrication
                || teamOne.ExplicitForwardFabrication
                    ? FrontlineLabsFiveSlotVariant.Wane
                    : FrontlineLabsFiveSlotVariant.Full,
            stanceGround: FrontlineLabsStanceGroundArm.Open,
            aim: FrontlineLabsAimArm.Offset,
            cooldown: FrontlineLabsCooldownArm.Ticking,
            volley: FrontlineLabsVolleyArm.Salvo,
            capture: FrontlineLabsCaptureArm.Channel,
            economy: FrontlineLabsEconomyArm.Scrap,
            roster: FrontlineLabsRosterArm.Legion,
            horizon: FrontlineLabsHorizonArm.Long,
            chassis: chassis,
            tierCost: tierCost);

    private static FrontlineGameModeDefinition Frontline(
        ActorResolvedMatchDefinition definition) =>
        (FrontlineGameModeDefinition)definition.Rules.GameMode;

    private static ActorFormDefinition Form(
        ActorResolvedMatchDefinition definition,
        string formId) =>
        definition.Rules.Forms.Single(form => form.Id == formId);

    [Fact]
    public void SplitIsTheMeasuredShapeAndWritesNoNewBytes()
    {
        ActorResolvedMatchDefinition split = Warpath(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Split);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-warpath-facing-locked",
            split.Rules.RulesetId);
        Assert.Contains(
            split.Rules.Forms,
            form => form.Id == FrontlineLabsClassDefinition.Bulwark
                .PrimeFormId);
        Assert.DoesNotContain(
            split.Rules.Forms,
            form => form.Id == FrontlineLabsClassDefinition.Bulwark
                .UnifiedFormId);
        Assert.All(
            split.Rules.Lifecycle.Profiles,
            profile => Assert.Null(profile.RootFactorySeedFormId));
    }

    [Fact]
    public void UnifiedCollapsesEveryPrimeAndChildFormToOneChassis()
    {
        ActorResolvedMatchDefinition unified = Warpath(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Unified);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-phalanx-facing-locked",
            unified.Rules.RulesetId);
        // No prime/child split survives anywhere in the form catalog.
        Assert.DoesNotContain(
            unified.Rules.Forms,
            form => form.Id.Contains("-prime", StringComparison.Ordinal)
                || form.Id.Contains("-child", StringComparison.Ordinal));
        // The statline unifies at the CHILD value: bulwark 5/4 -> 4.
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark.ChildMaxHealth,
            Form(
                    unified,
                    FrontlineLabsClassDefinition.Bulwark.UnifiedFormId)
                .MaxHealth);
        Assert.Equal(
            4,
            Form(
                    unified,
                    FrontlineLabsClassDefinition.Bulwark.UnifiedFormId)
                .MaxHealth);
        // …and so does the anchor windup: 3/1 -> 1.
        ActorSameLifeTransitionDefinition anchor = unified.Rules
            .SameLifeTransitions
            .OfType<ActorSameLifeTransitionDefinition>()
            .Single(route => route.TransitionId == "anchor-bulwark-body");
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark.ChildAnchorWindupTicks,
            anchor.Windup.DurationTicks);
    }

    [Fact]
    public void UnifiedGivesEveryBodyOneLifecycleOnTheChildClock()
    {
        ActorResolvedMatchDefinition unified = Warpath(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Unified);

        ActorLifecycleProfileDefinition[] bulwark =
            [
                .. unified.Rules.Lifecycle.Profiles
                    .Where(profile =>
                        profile.ProfileId.StartsWith(
                            "bulwark-",
                            StringComparison.Ordinal)),
            ];
        ActorLifecycleProfileDefinition profile = Assert.Single(bulwark);
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark
                .UnifiedRespawnLifecycleProfileId,
            profile.ProfileId);
        Assert.Equal(
            ActorLifecycleProfileDefinition.DestructionPolicyKind
                .AutomaticRespawn,
            profile.DestructionPolicy);
        // The child rebuild clock, not the prime's 18.
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark.ChildRebuildDelayTicks,
            profile.DelayTicks);
        Assert.Equal(30, profile.DelayTicks);
        // Every slot the team fields references that one profile.
        Assert.All(
            unified.LifecycleAssignments.Where(
                assignment => assignment.TeamId == 0),
            assignment => Assert.Equal(
                profile.ProfileId,
                assignment.LifecycleProfileId));
    }

    [Fact]
    public void UnifiedPutsTheFabricateVerbOnEveryFabricatorBody()
    {
        ActorResolvedMatchDefinition unified = Warpath(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Unified);
        ActorFormDefinition body = Form(
            unified,
            FrontlineLabsClassDefinition.Fabricator.UnifiedFormId);

        // The headless production network, entire: one form carries the verb,
        // so every live body is a fabrication origin.
        Assert.Contains("fabricate", body.AllowedActionIds);
        BoundedChildFabricationDefinition fabrication = unified.Rules
            .FabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .Single();
        Assert.Equal(
            [FrontlineLabsClassDefinition.Fabricator.UnifiedFormId],
            fabrication.SourceFormIds.ToArray());
        Assert.Equal(
            FrontlineLabsClassDefinition.Fabricator.UnifiedFormId,
            fabrication.OutputFormId);
    }

    [Fact]
    public void UnifiedDeclaresTheRootFactoryOnlyWhereABodyCouldNotReturn()
    {
        ActorResolvedMatchDefinition unified = Warpath(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Unified);

        ActorLifecycleProfileDefinition fabricator = unified.Rules.Lifecycle
            .Profiles
            .Single(profile =>
                profile.ProfileId
                == FrontlineLabsClassDefinition.Fabricator
                    .UnifiedFabricatedLifecycleProfileId);
        Assert.Equal(
            ActorLifecycleProfileDefinition.DestructionPolicyKind
                .ReadyForExplicitFabrication,
            fabricator.DestructionPolicy);
        Assert.Equal(
            FrontlineLabsClassDefinition.Fabricator.UnifiedFormId,
            fabricator.RootFactorySeedFormId);
        // The cell carries the `wane` tuning, whose lever is exactly this
        // clock — so the unified lifecycle inherits the tuned 22 rather than
        // the class's native 15, which is the point of unifying at the CHILD
        // profile rather than at a number.
        Assert.Equal(22, fabricator.DelayTicks);

        // The striker returns by itself, so it needs no bootstrap at all.
        ActorLifecycleProfileDefinition striker = unified.Rules.Lifecycle
            .Profiles
            .Single(profile =>
                profile.ProfileId
                == FrontlineLabsClassDefinition.Striker
                    .UnifiedRespawnLifecycleProfileId);
        Assert.Null(striker.RootFactorySeedFormId);

        // Slot zero keeps the authored home spawn: under prime dissolution it
        // is an ordinary reservation, and it is the tile the base seeds onto.
        ActorUnitSlotLifecycleAssignmentDefinition slotZero =
            unified.LifecycleAssignments.Single(assignment =>
                assignment.TeamId == 0 && assignment.UnitId == 0);
        Assert.Equal("team-0-prime", slotZero.AssignedRespawnSpawnId);
    }

    [Fact]
    public void UnifiedWidensTheUpgradeScopeAndDoublesThePrice()
    {
        ActorResolvedMatchDefinition split = Warpath(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Split);
        ActorResolvedMatchDefinition unified = Warpath(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsChassisArm.Unified);

        Assert.Equal(
            FrontlineScrapEconomyDefinition.UpgradeScopeKind
                .PrimeSlotLivesOnly,
            Frontline(split).ScrapEconomy!.UpgradeScope);
        Assert.Equal(
            FrontlineScrapEconomyDefinition.UpgradeScopeKind.AllSlotLives,
            Frontline(unified).ScrapEconomy!.UpgradeScope);
        Assert.All(
            Frontline(split).ScrapEconomy!.Tracks,
            track => Assert.Equal([10, 10], track.TierCosts.ToArray()));
        Assert.All(
            Frontline(unified).ScrapEconomy!.Tracks,
            track => Assert.Equal([20, 20], track.TierCosts.ToArray()));
    }

    [Fact]
    public void TheTierPriceIsSweepableAndSpellsItsNumber()
    {
        foreach (int price in new[] { 10, 30 })
        {
            ActorResolvedMatchDefinition arm = Warpath(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsChassisArm.Unified,
                price);
            Assert.Equal(
                "frontline-labs-1-bulwark-vs-striker-phalanx-"
                    + $"t{price}-facing-locked",
                arm.Rules.RulesetId);
            Assert.All(
                Frontline(arm).ScrapEconomy!.Tracks,
                track => Assert.Equal(
                    [price, price],
                    track.TierCosts.ToArray()));
        }

        // Naming the arm's own default is not an ablation and mints nothing.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-phalanx-facing-locked",
            Warpath(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker,
                    FrontlineLabsChassisArm.Unified,
                    FrontlineLabsScrapEconomy.UnifiedChassisTierCost)
                .Rules.RulesetId);
    }

    [Fact]
    public void TheArmRefusesTheCombinationsItWouldSilentlyBreak()
    {
        // No class pair: the arm re-shapes a class.
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline,
                chassis: FrontlineLabsChassisArm.Unified));

        // The prime respawn delay names a lifecycle the arm deletes.
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                primeRespawnTicks: 24,
                chassis: FrontlineLabsChassisArm.Unified));

        // MUSTER pays in the PRIME's return geometry.
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                sideObjective: FrontlineLabsSideObjectiveArm.Muster,
                chassis: FrontlineLabsChassisArm.Unified));

        // A tier price with no ladder to price.
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                chassis: FrontlineLabsChassisArm.Unified,
                tierCost: 20));
    }
}
