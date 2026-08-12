using BotArena.App.ArcRelay;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class ArcRelayClassCatalogTests
{
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
