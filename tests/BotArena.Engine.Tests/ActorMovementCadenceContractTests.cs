using System.Text.Json;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ActorMovementCadenceContractTests
{
    [Fact]
    public void NonInertCadenceRoundTripsThroughCanonicalSdkContract()
    {
        ActorResolvedMatchDefinition source =
            GenericDeathmatchSessionTestFixture.Definition("head-to-head");
        ActorRulesDefinition rules = source.Rules;
        var pacedRules = new ActorRulesDefinition(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles.Select(profile =>
                new ActorMovementProfileDefinition(
                    profile.Id,
                    profile.MovementLayer,
                    profile.FacingCoupling,
                    moveOpportunitiesPerCycle: 4,
                    moveCycleTicks: 5)),
            rules.VisionProfiles,
            rules.AttackProfiles,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        var definition = new ActorResolvedMatchDefinition(
            pacedRules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);

        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(definition);
        using JsonDocument document = JsonDocument.Parse(canonical);
        JsonElement movement = document.RootElement
            .GetProperty("rules")
            .GetProperty("movementProfiles")[0];
        Assert.Equal(4, movement.GetProperty(
            "moveOpportunitiesPerCycle").GetInt32());
        Assert.Equal(5, movement.GetProperty("moveCycleTicks").GetInt32());

        Sdk.GenericActorResolvedMatchContract parsed =
            Sdk.ActorCanonicalContractReader.Parse(canonical);
        Assert.Equal(
            4,
            parsed.Rules.MovementProfiles[0].MoveOpportunitiesPerCycle);
        Assert.Equal(5, parsed.Rules.MovementProfiles[0].MoveCycleTicks);
    }

    [Fact]
    public void InertCadenceKeepsHistoricalCanonicalBytes()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition("head-to-head");
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(definition);

        Assert.DoesNotContain("moveOpportunitiesPerCycle", canonical);
        Assert.DoesNotContain("moveCycleTicks", canonical);
        Sdk.GenericActorResolvedMatchContract parsed =
            Sdk.ActorCanonicalContractReader.Parse(canonical);
        Assert.Equal(
            1,
            parsed.Rules.MovementProfiles[0].MoveOpportunitiesPerCycle);
        Assert.Equal(1, parsed.Rules.MovementProfiles[0].MoveCycleTicks);
    }
}
