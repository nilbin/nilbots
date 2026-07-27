using System.Reflection;

namespace BotArena.Engine.Tests;

public class GameRuleDisclosureCatalogTests
{
    [Fact]
    public void EveryGameRulesProperty_HasExactlyOneExplicitClassification()
    {
        string[] ruleProperties = typeof(GameRules)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] classifiedProperties = GameRuleDisclosureCatalog.Properties.Keys
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ruleProperties, classifiedProperties);
    }

    [Fact]
    public void OperationalAndReplayProperties_AreNotPublicGameplay()
    {
        Assert.Equal(
            GameRuleDisclosure.PublicGameplay,
            GameRuleDisclosureCatalog.Properties[nameof(GameRules.FaultLimit)]);
        Assert.Equal(
            GameRuleDisclosure.RuntimeOnly,
            GameRuleDisclosureCatalog.Properties[nameof(GameRules.MaxDebugBytesPerTick)]);
        Assert.Equal(
            GameRuleDisclosure.RuntimeOnly,
            GameRuleDisclosureCatalog.Properties[nameof(GameRules.MaxDebugBytesPerMatch)]);
        Assert.Equal(
            GameRuleDisclosure.ReplayOnly,
            GameRuleDisclosureCatalog.Properties[nameof(GameRules.ReplayZoneTallies)]);
        Assert.Equal(
            GameRuleDisclosure.InternalSeedMechanics,
            GameRuleDisclosureCatalog.Properties[nameof(GameRules.SeedProfile)]);
    }

    [Fact]
    public void RuntimeAndReplayClassifications_AreTheExactExcludedSet()
    {
        string[] excluded = GameRuleDisclosureCatalog.Properties
            .Where(pair => pair.Value is GameRuleDisclosure.RuntimeOnly
                or GameRuleDisclosure.ReplayOnly)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                nameof(GameRules.MaxDebugBytesPerMatch),
                nameof(GameRules.MaxDebugBytesPerTick),
                nameof(GameRules.ReplayZoneTallies),
            ],
            excluded);
    }
}
