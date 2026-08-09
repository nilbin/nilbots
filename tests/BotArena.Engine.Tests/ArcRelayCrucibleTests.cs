using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Crucible puts the two reactors in opposing corners, which makes every
/// flank Well an OWNED Well. These tests hold the two properties that follow
/// from that and from nothing else on the ladder.
/// </summary>
public sealed class ArcRelayCrucibleTests
{
    [Fact]
    public void TheCanvasIsExactlyOneHundredEightyDegreeChiral()
    {
        ActorMapDefinition map = ArcRelayH0Definition
            .Create(loopProfile: ArcRelayLoopProfile.Crucible1).Map;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Assert.Equal(
                map.IsWall(x, y),
                map.IsWall(map.Width - 1 - x, map.Height - 1 - y));
        }
    }

    [Fact]
    public void TheTwoOwnedNearWellsBirthTogetherOnEverySeed()
    {
        // A stagger between the flank Wells is rhythm on a map where both are
        // neutral. Here north is west's near Well and south is east's, so a
        // stagger is a head start -- and a seed coin would only move that
        // between matches, never remove it from one. Base tick AND jitter
        // stream are therefore shared.
        foreach (ulong matchSeed in new ulong[] { 0, 1, 7, 2001, 3012, 99991 })
        {
            var driver = new ArcRelayActorMatchModeDriver(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Crucible1),
                matchSeed);
            var arc = Assert.IsType<GenericActorModeState.ArcRelay>(
                driver.State).State;
            int? north = arc.Wells.Single(well => well.WellId == "north")
                .NextScheduledBirthTick;
            int? south = arc.Wells.Single(well => well.WellId == "south")
                .NextScheduledBirthTick;
            Assert.Equal(north, south);
            Assert.NotNull(north);
        }
    }

    [Fact]
    public void TheNeutralFlankLadderKeepsItsStaggerAndItsCoin()
    {
        ArcRelayGameModeDefinition crucible =
            (ArcRelayGameModeDefinition)ArcRelayH0Definition
                .CreateRules(ArcRelayLoopProfile.Crucible1).GameMode;
        ArcRelayGameModeDefinition warren =
            (ArcRelayGameModeDefinition)ArcRelayH0Definition
                .CreateRules(ArcRelayLoopProfile.AmbushWarren11).GameMode;

        Assert.True(crucible.ChiralWellBirths);
        Assert.False(crucible.SeedPhasedWellLead);
        Assert.False(warren.ChiralWellBirths);
        Assert.True(warren.SeedPhasedWellLead);

        Assert.Equal(
            crucible.Wells.Single(well => well.WellId == "north")
                .FirstBirthTick,
            crucible.Wells.Single(well => well.WellId == "south")
                .FirstBirthTick);
        Assert.NotEqual(
            warren.Wells.Single(well => well.WellId == "north")
                .FirstBirthTick,
            warren.Wells.Single(well => well.WellId == "south")
                .FirstBirthTick);

        Assert.Contains(
            "\"chiralWellBirths\":true",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.Crucible1)));
        Assert.DoesNotContain(
            "chiralWellBirths",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren11)));
    }
}
