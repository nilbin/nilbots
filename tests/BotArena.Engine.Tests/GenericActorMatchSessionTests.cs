using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorMatchSessionTests
{
    [Fact]
    public void AtomicConvoyEntersTilesVacatedInTheSameTick()
    {
        ActorResolvedMatchDefinition definition = AtomicConvoyDefinition();
        Dictionary<int, GenericDeathmatchSessionTestFixture.RecordingFactory>
            factories = GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Move(Direction.East)
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition, factories),
            matchSeed: 42,
            recordChronology: true);

        GenericActorMatchStepResult step = session.Step();

        Assert.Equal(
            [new Position(2, 3), new Position(3, 3), new Position(4, 3)],
            step.PostState.ActiveLives
                .Where(life => life.ActorId.TeamId == 0)
                .OrderBy(life => life.ActorId.UnitId)
                .Select(life => life.Position));
        Assert.Equal(
            3,
            step.AuthoritativeEvents.Count(item => item.Payload is
                GenericActorRuntimeObservation.EventPayload.Movement
                { ActorId.TeamId: 0 }));
        Assert.DoesNotContain(
            step.AuthoritativeEvents,
            item => item.Payload is
                GenericActorRuntimeObservation.EventPayload.MovementBlocked
                { ActorId.TeamId: 0 });
        // Recording invokes the independent replay-causality reconstruction,
        // pinning it to the same dependency graph as live execution.
        Assert.Single(session.Chronology.Ticks);
    }

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

    private static ActorResolvedMatchDefinition AtomicConvoyDefinition()
    {
        ActorResolvedMatchDefinition source =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
                });
        ActorRulesDefinition old = source.Rules;
        ActorCollisionDefinition collisions = old.Collisions;
        var convoyLifecycle = new ActorLifecycleProfileDefinition(
            "convoy-life",
            ActorLifecycleProfileDefinition.DestructionPolicyKind
                .ReadyForExplicitFabrication,
            delayTicks: 0,
            automaticReturnFormId: null);
        var rules = new ActorRulesDefinition(
            "atomic-convoy-session-fixture",
            old.Limits,
            old.SeedMechanics,
            old.GameMode,
            new ActorLifecycleDefinition([convoyLifecycle]),
            old.Forms,
            old.MovementProfiles,
            old.VisionProfiles,
            old.AttackProfiles,
            old.Actions,
            old.FabricationTransitions,
            old.SameLifeTransitions,
            old.ReplicationTransitions,
            old.TeamPerception,
            new ActorCollisionDefinition(
                collisions.ActorsBlockWalls,
                collisions.ActorsBlockActors,
                collisions.SameDestinationMovesBlockAll,
                collisions.SwapMovesBlocked,
                followingVacatedActorAllowed: true,
                collisions.ProjectilesBlockMovement,
                collisions.MovingOntoProjectileCausesHit,
                collisions.WallsConsumeProjectiles,
                collisions.ProjectilesIgnoreFiringLife,
                collisions.ProjectilesStopOnFirstEnemyActor,
                collisions.ProjectilesCollideWithProjectiles,
                collisions.AlliedProjectileContact),
            old.TickResolution);

        InitialSpawnDefinition west = source.InitialDeployment.Spawns
            .Single(spawn => spawn.SpawnId == "west");
        InitialSpawnDefinition east = source.InitialDeployment.Spawns
            .Single(spawn => spawn.SpawnId == "east");
        var middle = new InitialSpawnDefinition(
            "convoy-middle", new Position(2, 3), Direction.East);
        var head = new InitialSpawnDefinition(
            "convoy-head", new Position(3, 3), Direction.East);
        var map = new ActorMapDefinition(
            "atomic-convoy-arena",
            version: 1,
            source.Map.TileRows,
            [
                .. source.Map.SpawnAnchors,
                new ActorMapSpawnAnchorDefinition(
                    middle, [ActorMovementLayer.Ground]),
                new ActorMapSpawnAnchorDefinition(
                    head, [ActorMovementLayer.Ground]),
            ],
            source.Map.Regions,
            source.Map.TileTags);
        var topology = new PublicMatchTopology
        {
            Teams = source.Topology.Teams,
            Participants = source.Topology.Participants,
            UnitSlots =
            [
                new PublicUnitSlot(0, 0, 10),
                new PublicUnitSlot(0, 1, 10),
                new PublicUnitSlot(0, 2, 10),
                new PublicUnitSlot(1, 0, 20),
            ],
            InitialLives =
            [
                new PublicInitialLife(0, 0, 0, "mobile"),
                new PublicInitialLife(0, 1, 0, "mobile"),
                new PublicInitialLife(0, 2, 0, "mobile"),
                new PublicInitialLife(1, 0, 0, "mobile"),
            ],
        };
        var deployment = new InitialDeploymentDefinition(
            [west, middle, head, east],
            [
                new InitialLifeDeployment(0, 0, 0, "mobile", "west"),
                new InitialLifeDeployment(
                    0, 1, 0, "mobile", "convoy-middle"),
                new InitialLifeDeployment(
                    0, 2, 0, "mobile", "convoy-head"),
                new InitialLifeDeployment(1, 0, 0, "mobile", "east"),
            ]);
        ActorUnitSlotLifecycleAssignmentDefinition Assignment(
            int teamId,
            int unitId,
            string? spawnId = null) => new(
                teamId,
                unitId,
                convoyLifecycle.ProfileId,
                initialGeneration: 0,
                ["mobile"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: spawnId);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
        [
            Assignment(0, 0),
            Assignment(0, 1),
            Assignment(0, 2),
            Assignment(1, 0),
        ];
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            source.Format,
            topology,
            deployment,
            assignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }
}
