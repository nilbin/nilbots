using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorMatchSessionTests
{
    [Fact]
    public void AuthoritativeMovementRejectsWallCornerSqueezing()
    {
        var from = new Position(2, 3);
        var diagonal = new Position(3, 4);
        ActorMapDefinition open = CornerMap();
        ActorMapDefinition oneClosedCompanion = CornerMap(new Position(3, 3));
        ActorMapDefinition bothClosedCompanions = CornerMap(
            new Position(3, 3),
            new Position(2, 4));

        Assert.True(GenericActorMatchSession.IsTerrainMovementStepLegal(
            open, from, diagonal));
        Assert.False(GenericActorMatchSession.IsTerrainMovementStepLegal(
            oneClosedCompanion, from, diagonal));
        Assert.False(GenericActorMatchSession.IsTerrainMovementStepLegal(
            bothClosedCompanions, from, diagonal));
        Assert.True(GenericActorMatchSession.IsTerrainMovementStepLegal(
            oneClosedCompanion, from, new Position(2, 4)));
    }

    [Fact]
    public void Deathmatch_NeutralSessionAndCompatibilityFacadeHaveExactParity()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory>
            neutralFactories =
                GenericDeathmatchSessionTestFixture.Factories(definition);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory>
            facadeFactories =
                GenericDeathmatchSessionTestFixture.Factories(definition);
        const ulong matchSeed = 1_337;

        using var neutral = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                neutralFactories),
            matchSeed);
        using var facade = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                facadeFactories),
            matchSeed);

        GenericActorMatchPreparedTick neutralPrepared =
            neutral.PrepareTick();
        GenericDeathmatchTickStart facadePrepared =
            facade.PrepareTick();
        GenericActorMatchStepResult neutralStep =
            neutral.Step(neutralPrepared.Observations);
        GenericDeathmatchStepResult facadeStep =
            facade.Step(facadePrepared.Observations);

        Assert.True(neutralStep.IsCompleted);
        Assert.True(facadeStep.IsCompleted);
        Assert.Equal(0, neutralStep.Tick);
        Assert.Equal(0, facadeStep.Tick);
        Assert.Equal(1, neutral.Tick);
        Assert.Equal(1, facade.Tick);
        Assert.Equal(1, neutralStep.PostState.NextTick);

        GenericActorMatchResult neutralResult =
            Assert.IsType<GenericActorMatchResult>(neutralStep.Result);
        GenericDeathmatchResult facadeResult =
            Assert.IsType<GenericDeathmatchResult>(facadeStep.Result);
        GenericActorMatchModeResult.Deathmatch neutralMode =
            Assert.IsType<GenericActorMatchModeResult.Deathmatch>(
                neutralResult.Mode);

        Assert.Same(neutralResult, neutral.Result);
        Assert.Same(facadeResult, facade.Result);
        Assert.Equal("max-ticks", neutralResult.CompletionReason);
        Assert.Equal(0, neutralResult.EndTick);
        Assert.Equal(neutralResult.EndTick, facadeResult.EndTick);
        Assert.Equal(neutralMode.Reason, facadeResult.Reason);
        Assert.Equal(
            neutralResult.Standings.WinnerTeamId,
            facadeResult.Standings.WinnerTeamId);
        Assert.All(
            neutralFactories.Values,
            factory => Assert.Equal(1, factory.ExecuteCount));
        Assert.All(
            facadeFactories.Values,
            factory => Assert.Equal(1, factory.ExecuteCount));

        string neutralReplay = ReplayV3Serializer.ToCanonicalJson(
            ReplayV3Projection.Project(neutral.Chronology));
        string facadeReplay = ReplayV3Serializer.ToCanonicalJson(
            ReplayV3Projection.Project(facade.Chronology));
        Assert.Equal(neutralReplay, facadeReplay);
    }

    private static ActorMapDefinition CornerMap(params Position[] walls)
    {
        ActorMapDefinition source = GenericDeathmatchSessionTestFixture
            .Definition("head-to-head").Map;
        HashSet<Position> added = walls.ToHashSet();
        ImmutableArray<string> rows = source.TileRows
            .Select((row, y) => new string(row
                .Select((tile, x) => added.Contains(new Position(x, y))
                    ? '#'
                    : tile)
                .ToArray()))
            .ToImmutableArray();
        return new ActorMapDefinition(
            "corner-movement-fixture",
            1,
            rows,
            source.SpawnAnchors,
            source.Regions,
            source.TileTags);
    }
}
