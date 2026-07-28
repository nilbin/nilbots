namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the class-matchup experiment arm (DECISIONS #153/#154): every
/// canonical pair resolves and validates, contracts stay content-identified
/// and distinct, each class carries exactly one exclusive verb family
/// (Striker bends, Bulwark fortifies reversibly class-wide, Fabricator
/// forward-fabricates while the others receive companions automatically),
/// Split is absent from every class arm, and kinematics remain shared so
/// classes cannot silently fork the exact duel analysis.
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
            Assert.Equal(
                $"{FrontlineLabsDefinition.MapId}-classes",
                definition.Map.Id);
            Assert.Empty(definition.Rules.ReplicationTransitions);
            Assert.DoesNotContain(
                definition.Rules.Actions,
                action => action.Id == "split");
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
    public void CompanionEconomicsFollowTheClass()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Striker);

        var fabricatorChildSlots = definition.LifecycleAssignments
            .Where(slot => slot.TeamId == 0 && slot.UnitId > 0)
            .OrderBy(slot => slot.UnitId)
            .ToArray();
        Assert.Equal(60, fabricatorChildSlots[0].UnlockTick);
        Assert.Equal(180, fabricatorChildSlots[1].UnlockTick);
        Assert.All(
            fabricatorChildSlots,
            slot =>
            {
                Assert.Equal(
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.DormantUnlockAtTick,
                    slot.InitialAvailability);
                Assert.Null(slot.AssignedRespawnSpawnId);
            });

        var strikerChildSlots = definition.LifecycleAssignments
            .Where(slot => slot.TeamId == 1 && slot.UnitId > 0)
            .OrderBy(slot => slot.UnitId)
            .ToArray();
        Assert.Equal(120, strikerChildSlots[0].UnlockTick);
        Assert.Equal(260, strikerChildSlots[1].UnlockTick);
        Assert.All(
            strikerChildSlots,
            slot => Assert.Equal(
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind
                    .DormantAutomaticActivationAtTick,
                slot.InitialAvailability));

        ActorFormDefinition fabricatorPrime = definition.Rules.Forms
            .Single(form => form.Id == "fabricator-prime");
        ActorFormDefinition strikerPrime = definition.Rules.Forms
            .Single(form => form.Id == "striker-prime");
        Assert.Contains("fabricate", fabricatorPrime.AllowedActionIds);
        Assert.DoesNotContain("fabricate", strikerPrime.AllowedActionIds);

        BoundedChildFabricationDefinition forward =
            Assert.IsType<BoundedChildFabricationDefinition>(
                Assert.Single(definition.Rules.FabricationTransitions));
        Assert.Empty(forward.RequiredSourceTileTags);
        Assert.Empty(forward.RequiredOutputTileTags);
        Assert.Contains(
            ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            forward.ForbiddenOutputTileTags);
    }

    [Fact]
    public void FortificationIsBulwarkExclusiveReversibleAndClassWide()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker);

        ActorFormDefinition bulwarkPrime = definition.Rules.Forms
            .Single(form => form.Id == "bulwark-prime");
        ActorFormDefinition strikerPrime = definition.Rules.Forms
            .Single(form => form.Id == "striker-prime");
        Assert.Contains("transform", bulwarkPrime.AllowedActionIds);
        Assert.DoesNotContain("transform", strikerPrime.AllowedActionIds);
        Assert.DoesNotContain(
            definition.Rules.Forms,
            form => form.Id.StartsWith(
                "striker-", StringComparison.Ordinal)
                && form.Id.Contains("turret", StringComparison.Ordinal));

        ActorSameLifeTransitionDefinition primeAnchor =
            definition.Rules.SameLifeTransitions.Single(transition =>
                transition.TransitionId == "anchor-bulwark-prime");
        ActorSameLifeTransitionDefinition childAnchor =
            definition.Rules.SameLifeTransitions.Single(transition =>
                transition.TransitionId == "anchor-bulwark-child");
        Assert.Equal(3, primeAnchor.Windup.DurationTicks);
        Assert.Equal(1, childAnchor.Windup.DurationTicks);
        Assert.False(primeAnchor.IrreversibleForLife);
        Assert.False(childAnchor.IrreversibleForLife);

        ActorSameLifeTransitionDefinition primeMobilize =
            definition.Rules.SameLifeTransitions.Single(transition =>
                transition.TransitionId == "mobilize-bulwark-prime");
        Assert.Equal("bulwark-prime-turret", primeMobilize.SourceFormId);
        Assert.Equal("bulwark-prime", primeMobilize.TargetFormId);
        Assert.True(primeMobilize.IrreversibleForLife);

        Assert.Equal(
            7,
            definition.Rules.Forms
                .Single(form => form.Id == "bulwark-prime-turret")
                .MaxHealth);
    }

    [Fact]
    public void ShotLanguageFollowsTheClass()
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
    public void MirrorPairsContainExactlyTheirOwnCatalog()
    {
        ActorResolvedMatchDefinition strikerMirror =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker);
        Assert.Equal(2, strikerMirror.Rules.Forms.Length);
        Assert.All(
            strikerMirror.Rules.Forms,
            form => Assert.StartsWith("striker-", form.Id));

        ActorResolvedMatchDefinition bulwarkMirror =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark);
        Assert.Equal(4, bulwarkMirror.Rules.Forms.Length);
        Assert.All(
            bulwarkMirror.Rules.Forms,
            form => Assert.StartsWith("bulwark-", form.Id));
        Assert.Equal(
            "bulwark-prime",
            bulwarkMirror.Topology.InitialLives
                .Single(life => life.TeamId == 1).FormId);
    }
}
