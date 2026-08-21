using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Tapline-13's Sentinel is both an autonomous range-4 weapon and an honest
/// range-4 omnidirectional team sensor. Those are separate promises: firing
/// never depends on another body's sight, while the sensor now lets the rest
/// of the team observe the same nearby ground.
/// </summary>
public sealed class ArcRelaySentryVisionTests
{
    private static readonly string[] Classes =
    [
        ArcRelayLaunchClassIds.Nest,
        ArcRelayLaunchClassIds.Kestrel,
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Patchbay,
        ArcRelayLaunchClassIds.Lantern,
        ArcRelayLaunchClassIds.Mortar,
        ArcRelayLaunchClassIds.Towline,
        ArcRelayLaunchClassIds.Hush,
    ];

    [Fact]
    public void CurrentSentrySharesItsWeaponRangeAsOmnidirectionalVision()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Classes,
            Classes,
            loopProfile: ArcRelayLoopProfile.Tapline13);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        Assert.True(mode.SentryTeamVision);
        string json = ActorContractManifestSerializer.ToCanonicalJson(
            definition.Rules);
        Assert.Contains("\"sentryTeamVision\":true", json);

        ArcRelaySignatureRuntime runtime = Deploy(definition);
        ArcRelaySignatureRuntime.OmnidirectionalSensor sensor = Assert.Single(
            runtime.TeamSensors(teamId: 0, tick: 1));
        Assert.Equal(4, sensor.Range);

        ActorResolvedMatchDefinition historical = ArcRelayH0Definition.Create(
            Classes,
            Classes,
            loopProfile: ArcRelayLoopProfile.Tapline12);
        Assert.False(Assert.IsType<ArcRelayGameModeDefinition>(
            historical.Rules.GameMode).SentryTeamVision);
        Assert.Empty(Deploy(historical).TeamSensors(teamId: 0, tick: 1));
    }

    [Fact]
    public void SentryAcquiresAnAlignedEnemyWithoutAlliedVision()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Classes,
            Classes,
            loopProfile: ArcRelayLoopProfile.Tapline13);
        ArcRelaySignatureRuntime runtime = Deploy(
            definition,
            out ArcRelaySignatureRuntime.Life owner,
            out Position seed);
        Position enemyPosition = Enumerable.Range(1, 4)
            .Select(distance => seed.Offset(distance, 0))
            .First(position => !definition.Map.IsWall(position));
        var enemy = new ArcRelaySignatureRuntime.Life(
            ActorIdentity.FromTeamUnitLife(1, 0, 0),
            enemyPosition,
            Health: 40,
            MaxHealth: 40,
            Direction.West);

        ArcRelaySignatureRuntime.TickResult fired = runtime.Advance(
            tick: 2,
            [owner, enemy]);

        ArcRelaySignatureRuntime.Effect.SentinelBolt bolt = Assert.Single(
            fired.Effects.OfType<
                ArcRelaySignatureRuntime.Effect.SentinelBolt>());
        Assert.Equal(4, bolt.Range);
        Assert.Equal(seed, bolt.Origin);
    }

    private static ArcRelaySignatureRuntime Deploy(
        ActorResolvedMatchDefinition definition) =>
        Deploy(definition, out _, out _);

    private static ArcRelaySignatureRuntime Deploy(
        ActorResolvedMatchDefinition definition,
        out ArcRelaySignatureRuntime.Life owner,
        out Position seed)
    {
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity actor = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        Position source = new(9, 6);
        owner = new ArcRelaySignatureRuntime.Life(
            actor,
            source,
            Health: 40,
            MaxHealth: 40,
            Direction.East);
        seed = runtime.PositionTargets(
                actor,
                source,
                visibleTiles: new HashSet<Position>(),
                [owner],
                carriesCore: false)
            .First(target => target.X > source.X);
        runtime.Start(
            tick: 0,
            actor,
            source,
            actionId: "sentinel-seed",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(seed)],
            [owner]);
        runtime.Advance(tick: 1, [owner]);
        return runtime;
    }
}
