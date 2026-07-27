using System.Reflection;
using Engine = BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ActorSdkContractTests
{
    [Fact]
    public void SdkAssembly_DoesNotReferenceEngine()
    {
        Assembly sdkAssembly = typeof(Sdk.IActorBot).Assembly;

        Assert.DoesNotContain(
            sdkAssembly.GetReferencedAssemblies(),
            reference => string.Equals(
                reference.Name,
                typeof(Engine.GameRules).Assembly.GetName().Name,
                StringComparison.Ordinal));
    }

    [Fact]
    public void SameNamedSdkAndEngineEnums_HaveIdenticalNamesAndValues()
    {
        IReadOnlyDictionary<string, Type> engineEnums =
            PublicEnums(typeof(Engine.GameRules).Assembly);
        IReadOnlyDictionary<string, Type> sdkEnums =
            PublicEnums(typeof(Sdk.IActorBot).Assembly);
        string[] mirroredNames = engineEnums.Keys
            .Intersect(sdkEnums.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(mirroredNames);
        foreach (string name in mirroredNames)
        {
            AssertEnumParity(
                engineEnums[name],
                sdkEnums[name]);
        }
    }

    [Fact]
    public void ActorActionIdsAndCodes_MatchEnginePublicCatalog()
    {
        AssertConstantParity(
            typeof(Engine.PublicActionIds),
            typeof(Sdk.ActorActionIds));
        AssertConstantParity(
            typeof(Engine.PublicActionCodes),
            typeof(Sdk.ActorActionCodes));
        AssertEnumParity(
            typeof(Engine.BotAction),
            typeof(Sdk.BotActionKind));
    }

    [Fact]
    public void ActorContractVersions_MatchEngineVersions()
    {
        Assert.Equal(
            Engine.BotArenaVersions.ActorRuntimeProtocolVersion,
            Sdk.ActorContractVersions.RuntimeProtocolVersion);
        Assert.Equal(
            Engine.BotArenaVersions.ActorRuntimeContractVersion,
            Sdk.ActorContractVersions.RuntimeContractVersion);
        Assert.Equal(
            Engine.BotArenaVersions.ActorMatchStartSchemaVersion,
            Sdk.ActorContractVersions.MatchStartSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.ActorObservationSchemaVersion,
            Sdk.ActorContractVersions.ObservationSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.ActorDecisionSchemaVersion,
            Sdk.ActorContractVersions.DecisionSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.PublicMatchContractSchemaVersion,
            Sdk.ActorContractVersions.MatchContractSchemaVersion);
    }

    [Fact]
    public void GenericActorContractVersions_MatchEngineProfile()
    {
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorContractProfileId,
            Sdk.GenericActorContractVersions.ContractProfileId);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorRuntimeProtocolVersion,
            Sdk.GenericActorContractVersions.RuntimeProtocolVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorRuntimeConfigurationVersion,
            Sdk.GenericActorContractVersions.RuntimeConfigurationVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorRuntimeContractVersion,
            Sdk.GenericActorContractVersions.RuntimeContractVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorMatchStartSchemaVersion,
            Sdk.GenericActorContractVersions.MatchStartSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorObservationSchemaVersion,
            Sdk.GenericActorContractVersions.ObservationSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorDecisionSchemaVersion,
            Sdk.GenericActorContractVersions.DecisionSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorMatchContractSchemaVersion,
            Sdk.GenericActorContractVersions.MatchContractSchemaVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorReplayFormatVersion,
            Sdk.GenericActorContractVersions.ReplayFormatVersion);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorMaxCanonicalContractBytes,
            Sdk.GenericActorContractVersions.MaxCanonicalContractBytes);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorMaxCanonicalContractNodes,
            Sdk.GenericActorContractVersions.MaxCanonicalContractNodes);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorMaxCanonicalCollectionCount,
            Sdk.GenericActorContractVersions
                .MaxCanonicalContractCollectionCount);
        Assert.Equal(
            Sdk.GenericActorContractVersions.ContractProfileId,
            Sdk.ActorContractProfile.GenericV2.ProfileId);
    }

    private static IReadOnlyDictionary<string, Type> PublicEnums(
        Assembly assembly) =>
        assembly.GetExportedTypes()
            .Where(type => type.IsEnum)
            .ToDictionary(type => type.Name, StringComparer.Ordinal);

    private static void AssertEnumParity(Type expected, Type actual)
    {
        (string Name, long Value)[] expectedValues = EnumValues(expected);
        (string Name, long Value)[] actualValues = EnumValues(actual);

        Assert.True(
            expectedValues.SequenceEqual(actualValues),
            $"{actual.FullName} drifted from {expected.FullName}.{Environment.NewLine}"
            + $"Expected: {Format(expectedValues)}{Environment.NewLine}"
            + $"Actual:   {Format(actualValues)}");
    }

    private static (string Name, long Value)[] EnumValues(Type type) =>
        Enum.GetNames(type)
            .Select(name => (
                name,
                Convert.ToInt64(Enum.Parse(type, name))))
            .OrderBy(item => item.Item2)
            .ThenBy(item => item.name, StringComparer.Ordinal)
            .ToArray();

    private static string Format(
        IEnumerable<(string Name, long Value)> values) =>
        string.Join(
            ", ",
            values.Select(value => $"{value.Name}={value.Value}"));

    private static void AssertConstantParity(
        Type expected,
        Type actual)
    {
        IReadOnlyDictionary<string, object?> expectedValues =
            PublicConstants(expected);
        IReadOnlyDictionary<string, object?> actualValues =
            PublicConstants(actual);

        Assert.Equal(
            expectedValues.Keys.Order(StringComparer.Ordinal),
            actualValues.Keys.Order(StringComparer.Ordinal));
        foreach ((string name, object? expectedValue) in expectedValues)
        {
            Assert.True(
                actualValues.TryGetValue(name, out object? actualValue),
                $"{actual.FullName} is missing {name}.");
            Assert.Equal(expectedValue, actualValue);
        }
    }

    private static IReadOnlyDictionary<string, object?> PublicConstants(
        Type type) =>
        type.GetFields(
                BindingFlags.Public
                | BindingFlags.Static
                | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .ToDictionary(
                field => field.Name,
                field => field.GetRawConstantValue(),
                StringComparer.Ordinal);
}
