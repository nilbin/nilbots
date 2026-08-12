using BotArena.Engine;

namespace BotArena.Engine.Tests;

/// <summary>
/// Signature grammar 2 (owner ruling 2026-08-05): dodgeable sentinel and
/// hook bolts, telegraphed null-field, and contract-projected metadata —
/// minted BESIDE grammar 1 so historical bytes never move.
/// </summary>
public sealed class ArcRelayGrammar2Tests
{
    [Fact]
    public void Grammar2SwapsExactlyThreeEnvelopes()
    {
        ActorRulesDefinition one = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.ForwardCombat);
        ActorRulesDefinition two = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.ForwardCombat2);
        var oneMode = (ArcRelayGameModeDefinition)one.GameMode;
        var twoMode = (ArcRelayGameModeDefinition)two.GameMode;

        Assert.Equal(1, oneMode.SignatureGrammarVersion);
        Assert.Equal(2, twoMode.SignatureGrammarVersion);
        Assert.Equal(oneMode.Signatures.Length, twoMode.Signatures.Length);

        var changed = twoMode.Signatures
            .Where(signature => signature
                is ArcRelaySignatureDefinition.SentinelSeed2
                or ArcRelaySignatureDefinition.TractorHook2
                or ArcRelaySignatureDefinition.NullField2)
            .Select(signature => signature.SignatureId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["null-field", "sentinel-seed", "tractor-hook"],
            changed);

        var turret = twoMode.Signatures
            .OfType<ArcRelaySignatureDefinition.SentinelSeed2>()
            .Single();
        // The owner's compensation ruling: dodgeable fire fires faster.
        Assert.Equal(2, turret.FireCooldownTicks);
        Assert.Equal(2, turret.BoltTilesPerAdvance);
    }

    [Fact]
    public void MetadataCoversEveryKind()
    {
        foreach (ArcRelaySignatureDefinition.SignatureKind kind in
            Enum.GetValues<ArcRelaySignatureDefinition.SignatureKind>())
        {
            ArcRelaySignatureDefinition.SignatureMetadata metadata =
                ArcRelaySignatureDefinition.MetadataFor(kind);
            Assert.Contains(
                metadata.Category,
                new[] { "damage", "control", "support", "movement", "custody" });
            Assert.Contains(
                metadata.ArgumentKind,
                new[]
                {
                    "heading", "position", "unit", "direction",
                    "parameterless",
                });
            Assert.True(metadata.EngagementRange > 0);
        }
    }

    [Fact]
    public void EverySignatureKindHasExactlyOneTypedLifecycleHandler()
    {
        var registrations =
            ArcRelaySignatureRuntime.RegisteredSignatureBehaviors;
        ArcRelaySignatureDefinition.SignatureKind[] kinds =
            Enum.GetValues<ArcRelaySignatureDefinition.SignatureKind>();

        Assert.Equal(kinds.Length, registrations.Length);
        Assert.Equal(kinds, registrations.Select(value => value.Kind));
        Assert.Equal(
            registrations.Length,
            registrations.Select(value => value.HandlerType).Distinct().Count());
        Assert.All(
            registrations,
            value => Assert.EndsWith(
                "Behavior",
                value.HandlerType.Name,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Tapline1FingerprintRemainsFrozenAcrossHandlerExtraction()
    {
        Assert.Equal(
            "b1c38905a9c3ad4a653cf50f0cf4fd131bfaee42eb9e037c7c1c3ebbd6ed5872",
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.Tapline1)));
    }

    [Fact]
    public void Grammar2MintsANewRulesFingerprintAndLeavesGrammar1Alone()
    {
        string one = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat));
        string two = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat2));
        Assert.NotEqual(one, two);
        // Grammar 1's fingerprint is additionally pinned by the historical
        // golden tests; this asserts the new profile did not touch it.
        Assert.Equal(
            one,
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat)));
    }

    [Fact]
    public void Grammar2CanonicalRulesRoundTripWithMetadata()
    {
        ActorRulesDefinition rules = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.ForwardCombat2);
        string json = ActorContractManifestSerializer.ToCanonicalJson(rules);
        Assert.Contains("\"signatureGrammarVersion\":2", json);
        Assert.Contains("\"boltTilesPerAdvance\"", json);
        Assert.Contains("\"category\"", json);
        Assert.Contains("\"argumentKind\"", json);
        Assert.Contains("\"engagementRange\"", json);
        Assert.Contains("\"tellTicks\":1", json);
        Assert.Contains("sentinel-bolt", json);
        Assert.Contains("hook-bolt", json);
    }
}
