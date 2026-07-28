using BotArena.App.Bots;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class BotContractProfilesTests
{
    [Fact]
    public void HistoricalProfileSet_SupportsLegacyDuelOnly()
    {
        Assert.True(BotContractProfiles.Supports(
            supportedContractProfiles: null,
            BotContractProfiles.LegacyDuel));
        Assert.False(BotContractProfiles.Supports(
            supportedContractProfiles: null,
            BotArenaVersions.GenericActorContractProfileId));
    }

    [Fact]
    public void ExplicitProfileSet_RequiresExactOrdinalMatch()
    {
        string[] supported =
        [
            BotArenaVersions.GenericActorContractProfileId,
        ];

        Assert.True(BotContractProfiles.Supports(
            supported,
            BotArenaVersions.GenericActorContractProfileId));
        Assert.False(BotContractProfiles.Supports(
            supported,
            BotContractProfiles.LegacyDuel));
        Assert.False(BotContractProfiles.Supports(
            supported,
            BotArenaVersions.GenericActorContractProfileId.ToUpperInvariant()));
        Assert.False(BotContractProfiles.Supports(
            [],
            BotContractProfiles.LegacyDuel));
    }

    [Fact]
    public void GenericOnlyActivationRequiresHostedGenericEnablement()
    {
        string[] genericOnly =
            [BotArenaVersions.GenericActorContractProfileId];
        string[] dual =
        [
            BotContractProfiles.LegacyDuel,
            BotArenaVersions.GenericActorContractProfileId,
        ];

        Assert.False(
            BotContractProfiles.CanActivateCompiledArtifact(
                genericOnly,
                genericActorHostingEnabled: false));
        Assert.True(
            BotContractProfiles.CanActivateCompiledArtifact(
                genericOnly,
                genericActorHostingEnabled: true));
        Assert.True(
            BotContractProfiles.CanActivateCompiledArtifact(
                dual,
                genericActorHostingEnabled: false));
        Assert.False(
            BotContractProfiles.CanActivateCompiledArtifact(
                [],
                genericActorHostingEnabled: true));
    }
}
