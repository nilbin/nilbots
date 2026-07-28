using System.Text.Json;
using BotArena.App.Bots;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class BuildReceiptTests
{
    [Fact]
    public void HistoricalReceipt_RemainsCompatibleWithoutProfileMetadata()
    {
        var receipt = new BuildReceipt(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new string('a', 64),
            new string('b', 64),
            123,
            "0.10.0",
            "0.10.0",
            "10.0.0",
            "4",
            BotArenaVersions.GameRulesVersion,
            BotArenaVersions.RuntimeProtocolVersion,
            BotArenaVersions.RuntimeConfigurationVersion,
            "compiler:test",
            "abcdef0",
            new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc));

        Assert.Null(receipt.SupportedContractProfiles);
    }

    [Fact]
    public void SupportedProfiles_RoundTripAsAnExactArray()
    {
        string[] profiles =
        [
            BotArenaVersions.GenericActorContractProfileId,
            BotContractProfiles.LegacyDuel,
        ];
        BuildReceipt receipt = CreateReceipt(profiles);

        BuildReceipt? roundTripped =
            JsonSerializer.Deserialize<BuildReceipt>(
                JsonSerializer.Serialize(receipt));

        Assert.NotNull(roundTripped);
        Assert.Equal(
            profiles,
            roundTripped.SupportedContractProfiles);
    }

    private static BuildReceipt CreateReceipt(
        string[]? supportedContractProfiles = null) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new string('a', 64),
            new string('b', 64),
            123,
            "0.10.0",
            "0.10.0",
            "10.0.0",
            "4",
            BotArenaVersions.GameRulesVersion,
            BotArenaVersions.RuntimeProtocolVersion,
            BotArenaVersions.RuntimeConfigurationVersion,
            "compiler:test",
            "abcdef0",
            new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
            supportedContractProfiles);
}
