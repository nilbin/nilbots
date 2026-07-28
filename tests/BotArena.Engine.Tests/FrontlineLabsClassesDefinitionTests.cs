namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the class-matchup experiment arm (DECISIONS #153): every canonical
/// pair resolves and validates, contracts stay content-identified and
/// distinct, per-team chassis land on the right slots, and kinematics remain
/// shared so classes cannot silently fork the exact duel analysis.
/// </summary>
public sealed class FrontlineLabsClassesDefinitionTests
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
    public void EveryCanonicalPairResolvesWithDistinctIdentity()
    {
        var rulesetIds = new HashSet<string>();
        var matchFingerprints = new HashSet<string>
        {
            ActorContractFingerprint.ComputeMatch(
                FrontlineLabsDefinition.Create()),
        };

        foreach (var pair in CanonicalPairs())
        {
            ActorResolvedMatchDefinition definition =
                FrontlineLabsDefinition.CreateClassesExperiment(
                    pair.TeamZero,
                    pair.TeamOne);

            Assert.Equal(
                "frontline-labs-1-experiment-classes-"
                + $"{pair.TeamZero.Id}-vs-{pair.TeamOne.Id}",
                definition.Rules.RulesetId);
            Assert.True(
                rulesetIds.Add(definition.Rules.RulesetId),
                $"duplicate ruleset {definition.Rules.RulesetId}");
            Assert.True(
                matchFingerprints.Add(
                    ActorContractFingerprint.ComputeMatch(definition)),
                $"duplicate fingerprint for {definition.Rules.RulesetId}");
            Assert.Equal(
                FrontlineLabsDefinition.ClassesSeedProfileId,
                definition.Rules.SeedMechanics.SeedProfileId);
            Assert.Equal(FrontlineLabsDefinition.MapId, definition.Map.Id);
        }
    }

    [Fact]
    public void NonCanonicalPairOrderIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Bulwark));
    }

    [Fact]
    public void CrossClassPairAssignsEachTeamItsOwnChassis()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Striker);

        Assert.Equal(
            "fabricator-prime",
            definition.Topology.InitialLives
                .Single(life => life.TeamId == 0).FormId);
        Assert.Equal(
            "striker-prime",
            definition.Topology.InitialLives
                .Single(life => life.TeamId == 1).FormId);

        var teamZeroChildSlots = definition.LifecycleAssignments
            .Where(slot => slot.TeamId == 0 && slot.UnitId > 0)
            .OrderBy(slot => slot.UnitId)
            .ToArray();
        Assert.Equal(60, teamZeroChildSlots[0].UnlockTick);
        Assert.Equal(180, teamZeroChildSlots[1].UnlockTick);
        Assert.All(
            teamZeroChildSlots,
            slot => Assert.Contains("fabricator-turret", slot.AllowedFormIds));

        var teamOneChildSlots = definition.LifecycleAssignments
            .Where(slot => slot.TeamId == 1 && slot.UnitId > 0)
            .OrderBy(slot => slot.UnitId)
            .ToArray();
        Assert.Equal(120, teamOneChildSlots[0].UnlockTick);
        Assert.Equal(260, teamOneChildSlots[1].UnlockTick);
        Assert.All(
            teamOneChildSlots,
            slot => Assert.Contains("striker-turret", slot.AllowedFormIds));
    }

    [Fact]
    public void ShotLanguageAndAnchorPlayFollowTheClass()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker);

        ActorFormDefinition strikerPrime = definition.Rules.Forms
            .Single(form => form.Id == "striker-prime");
        ActorFormDefinition bulwarkPrime = definition.Rules.Forms
            .Single(form => form.Id == "bulwark-prime");
        Assert.Contains("shoot", strikerPrime.AllowedActionIds);
        Assert.DoesNotContain(
            "shoot-straight",
            strikerPrime.AllowedActionIds);
        Assert.Contains("shoot-straight", bulwarkPrime.AllowedActionIds);
        Assert.DoesNotContain("shoot", bulwarkPrime.AllowedActionIds);

        ActorFormDefinition bulwarkTurret = definition.Rules.Forms
            .Single(form => form.Id == "bulwark-turret");
        ActorFormDefinition strikerTurret = definition.Rules.Forms
            .Single(form => form.Id == "striker-turret");
        Assert.Contains("mobilize", bulwarkTurret.AllowedActionIds);
        Assert.DoesNotContain("mobilize", strikerTurret.AllowedActionIds);
        Assert.Equal(7, bulwarkTurret.MaxHealth);
        Assert.Equal(5, strikerTurret.MaxHealth);

        ActorSameLifeTransitionDefinition bulwarkAnchor =
            definition.Rules.SameLifeTransitions.Single(transition =>
                transition.TransitionId == "anchor-bulwark-child");
        ActorSameLifeTransitionDefinition strikerAnchor =
            definition.Rules.SameLifeTransitions.Single(transition =>
                transition.TransitionId == "anchor-striker-child");
        Assert.False(bulwarkAnchor.IrreversibleForLife);
        Assert.True(strikerAnchor.IrreversibleForLife);
    }

    [Fact]
    public void KinematicsStaySharedAcrossClasses()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Fabricator);

        Assert.All(
            definition.Rules.AttackProfiles,
            profile =>
            {
                Assert.Equal(1, profile.Projectile.DamagePerHit);
                Assert.Equal(2, profile.Projectile.TilesPerAdvance);
                Assert.Equal(1, profile.Projectile.TicksPerAdvance);
            });
        Assert.Single(definition.Rules.MovementProfiles);
        Assert.Equal(
            ActorMovementLayer.Ground,
            definition.Rules.MovementProfiles.Single().MovementLayer);
    }

    [Fact]
    public void MirrorPairContainsEachCatalogEntryExactlyOnce()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker);

        Assert.Equal(4, definition.Rules.Forms.Length);
        Assert.All(
            definition.Rules.Forms,
            form => Assert.StartsWith("striker-", form.Id));
        Assert.Equal(
            definition.Rules.Forms.Select(form => form.Id).Distinct().Count(),
            definition.Rules.Forms.Length);
        Assert.Equal(
            "striker-prime",
            definition.Topology.InitialLives
                .Single(life => life.TeamId == 1).FormId);
    }
}
