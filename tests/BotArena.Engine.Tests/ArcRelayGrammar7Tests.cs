using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayGrammar7Tests
{
    [Fact]
    public void Broadloom7PublishesTheCombinedPromotedSignatureRules()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Broadloom7);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Same(ArcRelayLoopProfile.Broadloom7, ArcRelayLoopProfile.Current);
        Assert.Equal(7, mode.SignatureGrammarVersion);

        Assert.Single(
            mode.Signatures.OfType<ArcRelaySignatureDefinition.FallingStar>());
        ArcRelaySignatureDefinition.TripNode mine = Assert.Single(
            mode.Signatures.OfType<ArcRelaySignatureDefinition.TripNode>());
        Assert.Equal(2, mine.MaximumActive);
        Assert.Equal(1, mine.TriggerRadius);
        Assert.True(mine.HiddenFromNormalVision);
        Assert.True(Assert.Single(mode.Signatures
            .OfType<ArcRelaySignatureDefinition.NullField3>())
            .FullSuppression);

        ArcRelaySignatureDefinition.TractorHook3 hook = Assert.Single(
            mode.Signatures.OfType<ArcRelaySignatureDefinition.TractorHook3>());
        Assert.Equal(6, hook.Range);
        Assert.Equal(10, hook.Damage);
        Assert.Equal(1, hook.WindupTicks);
        Assert.Equal(1, hook.AuthoredWallPenetrationTiles);
        ArcRelaySignatureDefinition.SentinelSeed3 seed = Assert.Single(
            mode.Signatures.OfType<ArcRelaySignatureDefinition.SentinelSeed3>());
        Assert.Equal(1, seed.PlacementWindupTicks);
        Assert.Equal(1, seed.FireWindupTicks);
        Assert.Equal(10, seed.Damage);

        Assert.Equal(ActorProjectileMode.InstantRay, definition.Rules
            .AttackProfiles.Single(value => value.Id == "hook-ray")
            .Projectile.Mode);
        Assert.Equal(ActorProjectileMode.InstantRay, definition.Rules
            .AttackProfiles.Single(value => value.Id == "sentinel-ray")
            .Projectile.Mode);
        Assert.DoesNotContain(definition.Rules.AttackProfiles,
            value => value.Id is "hook-bolt" or "sentinel-bolt");
    }

    [Fact]
    public void DeclaredDeliveryAndContactPolicyArePublicContractFacts()
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.Broadloom7));
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var mode = Assert.IsType<GenericActorRulesContract.ArcRelayGameMode>(
            contract.Rules.GameMode);

        GenericActorRulesContract.ArcRelaySignature hook = mode.Signatures
            .Single(value => value.SignatureId == "tractor-hook");
        Assert.Equal("declared-ray", hook.Delivery);
        Assert.Equal("first-contact", hook.ContactPolicy);
        Assert.Null(hook.BoltTilesPerAdvance);
        GenericActorRulesContract.ArcRelaySignature seed = mode.Signatures
            .Single(value => value.SignatureId == "sentinel-seed");
        Assert.Equal("declared-ray", seed.Delivery);
        Assert.Equal("first-contact", seed.ContactPolicy);
        Assert.Equal(1, seed.FireWindupTicks);
        Assert.Null(seed.BoltTilesPerAdvance);
        GenericActorRulesContract.ArcRelaySignature rail = mode.Signatures
            .Single(value => value.SignatureId == "rail-line");
        Assert.Equal("declared-ray", rail.Delivery);
        Assert.Equal("piercing-hostiles", rail.ContactPolicy);
    }

    [Fact]
    public void Broadloom6KeepsItsHistoricalBoltContract()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Broadloom6);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            definition);

        Assert.Equal(6, mode.SignatureGrammarVersion);
        Assert.Contains(mode.Signatures,
            value => value is ArcRelaySignatureDefinition.TractorHook2);
        Assert.Contains(mode.Signatures,
            value => value is ArcRelaySignatureDefinition.SentinelSeed2);
        Assert.Contains(definition.Rules.AttackProfiles,
            value => value.Id == "hook-bolt"
                && value.Projectile.Mode == ActorProjectileMode.Discrete);
        Assert.Contains(definition.Rules.AttackProfiles,
            value => value.Id == "sentinel-bolt"
                && value.Projectile.Mode == ActorProjectileMode.Discrete);
        Assert.DoesNotContain("declared-ray", canonical);
        Assert.DoesNotContain("fireWindupTicks", canonical);
    }
}
