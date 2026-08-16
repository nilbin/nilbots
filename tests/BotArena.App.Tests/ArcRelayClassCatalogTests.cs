using BotArena.App.ArcRelay;
using BotArena.Engine;
using BotArena.TacticalPlaybooks;

namespace BotArena.App.Tests;

public sealed class ArcRelayClassCatalogTests
{
    [Fact]
    public void SignatureAuthoringVocabularyExactlyCoversTheLaunchBand()
    {
        ArcRelaySignatureAuthoringDescriptor[] values =
            ArcRelaySignatureAuthoringCatalog.All.ToArray();
        Assert.Equal(16, values.Length);
        Assert.Equal(16, values.Select(value => value.ClassId).Distinct().Count());
        Assert.Equal(16, values.Select(value => value.SkillId).Distinct().Count());
        Assert.Equal(ArcRelayLaunchClassIds.All, values.Select(value =>
            value.ClassId));

        (string ClassId, string SkillId,
            ArcRelaySignatureTargetCapability Capabilities)[] expected =
        [
            ("kestrel", "vector-dash",
                ArcRelaySignatureTargetCapability.RouteForward),
            ("palisade", "prism-wall", 0),
            ("towline", "tractor-hook", 0),
            ("patchbay", "repair-beam",
                ArcRelaySignatureTargetCapability.FriendlyRole
                | ArcRelaySignatureTargetCapability.MostExperiencedAlly),
            ("lantern", "survey-flare", 0),
            ("mortar", "falling-star", 0),
            ("minesmith", "trip-node",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("hush", "null-field", 0),
            ("relay", "arc-toss",
                ArcRelaySignatureTargetCapability.FriendlyRole
                | ArcRelaySignatureTargetCapability.MostExperiencedAlly
                | ArcRelaySignatureTargetCapability.RouteAheadRole),
            ("switchback", "exchange",
                ArcRelaySignatureTargetCapability.FriendlyRole
                | ArcRelaySignatureTargetCapability.MostExperiencedAlly),
            ("longshot", "rail-line", 0),
            ("mason", "hardlight-block",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("sunder", "target-paint", 0),
            ("repulsor", "kinetic-burst", 0),
            ("veil", "smoke-canister", 0),
            ("nest", "sentinel-seed",
                ArcRelaySignatureTargetCapability.ExactAnchor),
        ];

        Assert.Equal(expected, values.Select(value => (
            value.ClassId, value.SkillId, value.Capabilities)));
        Assert.All(values, value =>
            Assert.NotEmpty(value.AllowedNamedTargets));
    }

    [Fact]
    public void ProductCatalogBindsEveryLaunchClassToItsEngineSignatureAndAction()
    {
        ArcRelayClassCatalog catalog = ArcRelayClassCatalog.Default;
        ActorRulesDefinition rules = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.Current);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(rules.GameMode);

        Assert.Equal(ArcRelayLaunchClassIds.All, catalog.All.Select(value =>
            value.Id));
        Assert.Equal(
            ArcRelayLaunchClassIds.All.Order(StringComparer.Ordinal),
            mode.Signatures.Select(value => value.ClassId)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            catalog.All.Count,
            catalog.All.Select(value => value.SignatureId).Distinct().Count());
        Assert.Equal(
            catalog.All.Count,
            catalog.All.Select(value => value.ActionId).Distinct().Count());

        foreach (ArcRelayClassDefinition product in catalog.All)
        {
            ArcRelaySignatureDefinition signature = mode.Signatures.Single(
                value => value.ClassId == product.Id);
            Assert.Equal(signature.SignatureId, product.SignatureId);
            Assert.Equal(signature.ActionId, product.ActionId);
            Assert.Contains(rules.Actions, value =>
                value.Id == product.ActionId);
            Assert.False(string.IsNullOrWhiteSpace(product.SignatureName));
            Assert.False(string.IsNullOrWhiteSpace(product.Fantasy));
        }
    }
}
