using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayH0DefinitionTests
{
    [Fact]
    public void ApprovedContract_IsExactAndRoundTripsThroughThePublicSdk()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create();

        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        Assert.Equal(600, definition.Rules.Limits.MaxTicks);
        Assert.Equal(8, mode.FieldedSlotsPerTeam);
        Assert.Equal(2, mode.MaxCopiesPerClass);
        Assert.Equal(20, mode.RespawnDelayTicks);
        Assert.Equal(3, mode.CoresPerPulse);
        Assert.Equal(
            3,
            Assert.IsType<ArcRelayVictoryDefinition>(mode.Victory)
                .PulsesToDestroyReactor);
        Assert.Equal(16, mode.Signatures.Length);
        Assert.Equal(16, definition.Rules.Forms.Length);
        Assert.Equal(16, definition.Topology.UnitSlots.Length);
        Assert.All(
            definition.Topology.Teams,
            team => Assert.Equal(
                8,
                definition.Topology.UnitSlots.Count(slot =>
                    slot.TeamId == team.TeamId)));

        Assert.Equal(31, definition.Map.Width);
        Assert.Equal(23, definition.Map.Height);
        Assert.Equal(
            525,
            definition.Map.TileRows.Sum(row =>
                row.Count(tile => tile == '.')));
        Assert.Equal(
            [new Position(15, 11), new Position(15, 4), new Position(15, 18)],
            ((ArcRelayActorModeMapBindingDefinition)
                definition.ModeMapBinding).OrderedWellRegionIds.Select(id =>
                    definition.Map.Regions.Single(region =>
                        region.RegionId == id).Tiles.Single()));

        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(definition);
        GenericActorResolvedMatchContract sdk =
            ActorCanonicalContractReader.Parse(canonical);
        var sdkMode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                sdk.Rules.GameMode);
        Assert.Equal(mode.FieldedSlotsPerTeam, sdkMode.FieldedSlotsPerTeam);
        Assert.Equal(mode.MaxCopiesPerClass, sdkMode.MaxCopiesPerClass);
        Assert.Equal(16, sdkMode.Signatures.Length);
        Assert.IsType<
            GenericActorResolvedMatchContract.ArcRelayModeMapBinding>(
                sdk.ModeMapBinding);
    }

    [Fact]
    public void Sheet_SelectsDirectlyFromUnlockedClasses_WithOnlyTwoCopyCap()
    {
        string[] accepted =
        [
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Patchbay,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            accepted,
            accepted);
        Assert.Equal(
            accepted,
            definition.Topology.UnitSlots
                .Where(slot => slot.TeamId == 0)
                .OrderBy(slot => slot.UnitId)
                .Select(slot => slot.ClassId));

        string[] rejected = [.. accepted];
        rejected[4] = ArcRelayLaunchClassIds.Kestrel;
        Assert.Throws<ArgumentException>(() =>
            ArcRelayH0Definition.Create(rejected, accepted));
    }
}
