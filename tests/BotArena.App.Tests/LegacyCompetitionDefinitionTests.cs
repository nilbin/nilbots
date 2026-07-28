using System.Text.Json;
using BotArena.App.Competition;

namespace BotArena.App.Tests;

public sealed class LegacyCompetitionDefinitionTests
{
    [Fact]
    public void Create_IsDeterministicAndLabelsUnknownHistory()
    {
        LegacyCompetitionDefinition first =
            LegacyCompetitionDefinition.Create("0.4");
        LegacyCompetitionDefinition second =
            LegacyCompetitionDefinition.Create("0.4");

        Assert.Equal(first.PlaylistKey, second.PlaylistKey);
        Assert.Equal(first.CanonicalDefinition, second.CanonicalDefinition);
        Assert.Equal(
            first.DefinitionFingerprint,
            second.DefinitionFingerprint);
        Assert.Equal(76, first.PlaylistKey.Length);
        Assert.Matches(
            "^legacy-duel-[0-9a-f]{64}$",
            first.PlaylistKey);
        Assert.Matches(
            "^[0-9a-f]{64}$",
            first.DefinitionFingerprint);
        Assert.Equal(
            """{"schemaVersion":1,"gameModeId":"deathmatch","rulesetId":"0.4","matchFormatId":"head-to-head","mapPoolId":"legacy-import","seriesPolicyId":"legacy-import","matchmakingPolicyId":"legacy-import","admissionPolicyId":"legacy-import"}""",
            first.CanonicalDefinition);
        Assert.Equal(
            "3e4c591eff887f4eaa4824041a3be69d89ca408f6af07e3687f7229b0e1b6b80",
            first.DefinitionFingerprint);

        using JsonDocument definition =
            JsonDocument.Parse(first.CanonicalDefinition);
        Assert.Equal(
            "0.4",
            definition.RootElement
                .GetProperty("rulesetId")
                .GetString());
        Assert.Equal(
            LegacyCompetitionDefinition.UnknownDefinitionId,
            definition.RootElement
                .GetProperty("mapPoolId")
                .GetString());
        Assert.Equal(
            LegacyCompetitionDefinition.UnknownDefinitionId,
            definition.RootElement
                .GetProperty("seriesPolicyId")
                .GetString());
        Assert.False(
            definition.RootElement.TryGetProperty(
                "executionPolicyId",
                out _));

        using JsonDocument provenance =
            JsonDocument.Parse(first.Provenance);
        Assert.Equal(
            LegacyCompetitionDefinition.UnknownDefinitionId,
            provenance.RootElement
                .GetProperty("source")
                .GetString());
        Assert.Contains(
            provenance.RootElement
                .GetProperty("unknownHistoricalMetadata")
                .EnumerateArray()
                .Select(item => item.GetString()),
            item => item == "season");
    }

    [Fact]
    public void DifferentRulesVersionsHaveIndependentStablePopulations()
    {
        LegacyCompetitionDefinition older =
            LegacyCompetitionDefinition.Create("0.4");
        LegacyCompetitionDefinition current =
            LegacyCompetitionDefinition.Create("0.5");

        Assert.NotEqual(older.PlaylistKey, current.PlaylistKey);
        Assert.NotEqual(
            older.DefinitionFingerprint,
            current.DefinitionFingerprint);
        Assert.Equal(1, LegacyCompetitionDefinition.PlaylistVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BlankRulesVersionIsRejected(string rulesVersion)
    {
        Assert.Throws<ArgumentException>(
            () => LegacyCompetitionDefinition.Create(rulesVersion));
    }
}
