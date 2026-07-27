using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

public sealed class GenericActorCanonicalContractValidatorTests
{
    [Theory]
    [InlineData("head-to-head")]
    [InlineData("free-for-all")]
    [InlineData("teams")]
    public void ValidatesCompleteEngineAuthoredContract(string formatName)
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.Deathmatch(formatName);
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(definition);

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(canonical);

        Assert.Equal(
            ActorResolvedMatchDefinition.CurrentSchemaVersion,
            validation.SchemaVersion);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(definition),
            validation.MatchContractFingerprint);
        Assert.Equal(
            BotArena.Sdk.GenericActorContractVersions.ContractProfileId,
            validation.ContractProfileId);
        Assert.Equal(definition.Rules.RulesetId, validation.RulesetId);
    }

    [Fact]
    public void RejectsNonCanonicalRootOrder()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(definition);
        string prefix =
            $"{{\"schemaVersion\":{definition.SchemaVersion}," +
            $"\"matchContractFingerprint\":\"" +
            $"{ActorContractFingerprint.ComputeMatch(definition)}\"";
        string reordered =
            $"{{\"matchContractFingerprint\":\"" +
            $"{ActorContractFingerprint.ComputeMatch(definition)}\"," +
            $"\"schemaVersion\":{definition.SchemaVersion}" +
            canonical[prefix.Length..];

        Assert.Throws<FormatException>(
            () => GenericActorCanonicalContractValidator.Validate(reordered));
    }
}
