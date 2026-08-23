using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayGrammar4Tests
{
    [Fact]
    public void Broadloom4PublishesTheCompleteSignatureRefresh()
    {
        ActorRulesDefinition rules = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.Broadloom4);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(rules.GameMode);

        Assert.Equal(4, mode.SignatureGrammarVersion);
        Assert.True(mode.Signatures
            .OfType<ArcRelaySignatureDefinition.VectorDash>()
            .Single().RetainsCore);
        ArcRelaySignatureDefinition.TractorHook2 hook = mode.Signatures
            .OfType<ArcRelaySignatureDefinition.TractorHook2>().Single();
        Assert.Equal(6, hook.Range);
        Assert.Equal(5, hook.MaxPullTiles);
        Assert.Equal(10, hook.Damage);
        Assert.Equal(1, hook.AuthoredWallPenetrationTiles);
        Assert.Equal(4, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.SurveyFlare>()
            .Single().TeamVisionRadius);
        Assert.True(mode.Signatures
            .OfType<ArcRelaySignatureDefinition.Exchange>()
            .Single().TargetRetainsCore);
        ArcRelaySignatureDefinition.KineticBurst2 burst = mode.Signatures
            .OfType<ArcRelaySignatureDefinition.KineticBurst2>().Single();
        Assert.Equal(10, burst.Damage);
        Assert.True(burst.HostilesOnly);

        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.Create(loopProfile:
                ArcRelayLoopProfile.Broadloom4));
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var publicMode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                contract.Rules.GameMode);
        Assert.Equal(4, publicMode.SignatureGrammarVersion);
        Assert.True(publicMode.Signatures.Single(value =>
            value.SignatureId == "vector-dash").RetainsCore);
        Assert.Equal(10, publicMode.Signatures.Single(value =>
            value.SignatureId == "tractor-hook").Damage);
        Assert.Equal(1, publicMode.Signatures.Single(value =>
            value.SignatureId == "tractor-hook")
            .AuthoredWallPenetrationTiles);
        Assert.Equal(4, publicMode.Signatures.Single(value =>
            value.SignatureId == "survey-flare").TeamVisionRadius);
        Assert.True(publicMode.Signatures.Single(value =>
            value.SignatureId == "exchange").TargetRetainsCore);
        Assert.True(publicMode.Signatures.Single(value =>
            value.SignatureId == "kinetic-burst").HostilesOnly);
    }

    [Fact]
    public void Broadloom3RemainsFreeOfGrammar4Fields()
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.Broadloom3));

        Assert.DoesNotContain("retainsCore", canonical);
        Assert.DoesNotContain("teamVisionRadius", canonical);
        Assert.DoesNotContain("targetRetainsCore", canonical);
        Assert.DoesNotContain("authoredWallPenetrationTiles", canonical);
        Assert.DoesNotContain("hostilesOnly", canonical);
    }

    [Fact]
    public void FlareBecomesAnOmnidirectionalTeamSensorAfterLanding()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity lantern,
            Position origin) = Runtime(ArcRelayLaunchClassIds.Lantern);
        Position target = origin.Offset(2, 0);
        runtime.Start(
            10, lantern, origin, "survey-flare",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(
                target)],
            [Life(lantern, origin, 30)]);

        runtime.Advance(11, [Life(lantern, origin, 30)]);
        ArcRelaySignatureRuntime.OmnidirectionalSensor sensor =
            Assert.Single(runtime.TeamSensors(0, 11));
        Assert.Equal(target, sensor.Position);
        Assert.Equal(4, sensor.Range);
        Assert.Empty(runtime.TeamSensors(1, 11));
    }

    [Fact]
    public void BurstTargetsOnlyAdjacentHostilesAndCarriesTenDamage()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity repulsor,
            Position origin) = Runtime(ArcRelayLaunchClassIds.Repulsor);
        ActorIdentity ally = ActorIdentity.FromTeamUnitLife(0, 1, 0);
        ActorIdentity hostile = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        ArcRelaySignatureRuntime.Life[] lives =
        [
            Life(repulsor, origin, 50),
            Life(ally, origin.Offset(-1, 0), 30),
            Life(hostile, origin.Offset(1, 0), 30),
        ];
        runtime.Start(20, repulsor, origin, "kinetic-burst", [], lives);
        runtime.Advance(21, lives);
        ArcRelaySignatureRuntime.TickResult resolved =
            runtime.ResolvePostMovement(21, lives, (_, _) => true);

        ArcRelaySignatureRuntime.Effect.KineticBurst burst = Assert.IsType<
            ArcRelaySignatureRuntime.Effect.KineticBurst>(
                Assert.Single(resolved.Effects));
        Assert.Equal(hostile, Assert.Single(burst.Targets));
        Assert.Equal(10, burst.Damage);
    }

    [Fact]
    public void TargetPaintIncludesTheSunderOwnersOwnHits()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity sunder,
            Position origin) = Runtime(ArcRelayLaunchClassIds.Sunder);
        ActorIdentity target = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        Position targetPosition = origin.Offset(3, 0);
        ArcRelaySignatureRuntime.Life[] lives =
        [Life(sunder, origin, 40), Life(target, targetPosition, 40)];
        runtime.Start(
            30, sunder, origin, "target-paint",
            [new GenericActorRuntimeActionArgument.UnitTargetArgument(
                new GenericActorRuntimeActionArgument.UnitTarget(1, 0))],
            lives);
        runtime.Advance(31, lives);
        runtime.ResolvePostMovement(31, lives, (_, _) => true);

        Assert.Equal(10, runtime.TargetPaintBonus(
            sunder, target, 31, out string? operationId));
        Assert.NotNull(operationId);
    }

    private static (ArcRelaySignatureRuntime Runtime, ActorIdentity Actor,
        Position Origin) Runtime(string classId)
    {
        string[] composition =
        [
            classId,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.Broadloom4);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        return (
            new ArcRelaySignatureRuntime(definition, mode),
            ActorIdentity.FromTeamUnitLife(0, 0, 0),
            new Position(10, 13));
    }

    private static ArcRelaySignatureRuntime.Life Life(
        ActorIdentity actor,
        Position position,
        int health) => new(actor, position, health, health);
}
