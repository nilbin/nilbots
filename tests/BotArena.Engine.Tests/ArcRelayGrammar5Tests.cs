using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayGrammar5Tests
{
    [Fact]
    public void OnlyGrammar5OffersAnUnseenFallingStarCrossAcrossWalls()
    {
        var source = new Position(9, 13);
        var target = new Position(12, 13);
        ImmutableHashSet<Position> visible = [source];

        ArcRelaySignatureRuntime historical = Runtime(
            ArcRelayLoopProfile.Broadloom4,
            out ActorIdentity historicalMortar,
            out _);
        ArcRelaySignatureRuntime candidate = Runtime(
            ArcRelayLoopProfile.Broadloom5,
            out ActorIdentity candidateMortar,
            out ActorResolvedMatchDefinition candidateDefinition);

        Assert.True(candidateDefinition.Map.IsWall(new Position(10, 13)));
        Assert.True(candidateDefinition.Map.IsWall(new Position(11, 13)));

        Assert.DoesNotContain(target, historical.PositionTargets(
            historicalMortar, source, visible,
            [Life(historicalMortar, source)], carriesCore: false));
        Assert.Contains(target, candidate.PositionTargets(
            candidateMortar, source, visible,
            [Life(candidateMortar, source)], carriesCore: false));
    }

    [Fact]
    public void BlindFallingStarKeepsItsPublicFiveTileTellAndNoVisionSensor()
    {
        var source = new Position(9, 13);
        var target = new Position(12, 13);
        ArcRelaySignatureRuntime runtime = Runtime(
            ArcRelayLoopProfile.Broadloom5,
            out ActorIdentity mortar,
            out _);
        ArcRelaySignatureRuntime.Life life = Life(mortar, source);

        runtime.Start(
            20,
            mortar,
            source,
            "falling-star",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(
                target)],
            [life]);

        ArcRelaySignatureState tell = Assert.Single(runtime.Project(20));
        Assert.Equal(ArcRelaySignatureState.SignaturePhase.Tell, tell.Phase);
        Assert.Equal(22, tell.CompletesAtTick);
        Assert.True(tell.Positions.SequenceEqual(
            [
                target.Offset(0, -1),
                target.Offset(-1, 0),
                target,
                target.Offset(1, 0),
                target.Offset(0, 1),
            ]));
        Assert.Empty(runtime.TeamSensors(mortar.TeamId, 20));

        ArcRelaySignatureRuntime.TickResult impact = runtime.Advance(22, [life]);
        Assert.Contains(impact.Effects, value => value is
            ArcRelaySignatureRuntime.Effect.FallingStar
            {
                Target: var position,
            } && position == target);
    }

    private static ArcRelaySignatureRuntime Runtime(
        ArcRelayLoopProfile profile,
        out ActorIdentity mortar,
        out ActorResolvedMatchDefinition definition)
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Hush,
        ];
        definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: profile);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        Assert.Equal(profile == ArcRelayLoopProfile.Broadloom5 ? 5 : 4,
            mode.SignatureGrammarVersion);
        mortar = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        return new ArcRelaySignatureRuntime(definition, mode);
    }

    private static ArcRelaySignatureRuntime.Life Life(
        ActorIdentity actor,
        Position position) => new(actor, position, 30, 30);
}
