namespace BotArena.Engine.Tests;

public sealed class FrontlineLabsDuelMapArmTests
{
    [Fact]
    public void AutomaticCompanions_ActivateAsDeclaredAndActOnDueTick()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 104_729);

        Assert.Empty(definition.Rules.FabricationTransitions);
        Assert.Empty(definition.Rules.ReplicationTransitions);
        Assert.Empty(definition.ParticipantRegionAssignments);
        Assert.DoesNotContain(
            "fabricate",
            definition.Rules.Forms.Single(form =>
                form.Id == "prime-mobile").AllowedActionIds);
        Assert.Equal(
            4,
            definition.LifecycleAssignments.Count(assignment =>
                assignment.InitialAvailability
                    == ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick));

        for (int tick = 0; tick < 120; tick++)
            session.Step(session.PrepareTick().Observations);

        GenericActorMatchPreparedTick firstActivation =
            session.PrepareTick();
        Assert.Equal(4, firstActivation.Observations.Length);

        session.Step(firstActivation.Observations);
        GenericActorMatchTickFrame firstActivationFrame =
            session.Chronology.Ticks[120];
        GenericActorMatchTickStart recordedActivation =
            firstActivationFrame.TickStart;
        Assert.Equal(
            [
                new ActorIdentity(0, 1, 0),
                new ActorIdentity(1, 1, 0),
            ],
            recordedActivation.LifeStarts
                .Select(start => start.ActorId)
                .ToArray());
        Assert.Equal(
            4,
            firstActivationFrame.ActorTurns.Length);
        Assert.All(
            recordedActivation.LifeStarts,
            start => Assert.Contains(
                firstActivationFrame.ActorTurns,
                turn => turn.ActorId == start.ActorId));
        Assert.All(
            recordedActivation.LifeStarts,
            start =>
            {
                Assert.Equal(
                    GenericActorRuntimeStart.SpawnReason
                        .AutomaticActivation,
                    start.Origin.Reason);
                Assert.Null(start.Origin.ParentActorId);
                Assert.Equal(0, start.Origin.Generation);
            });
        Assert.All(
            factories.Values,
            factory => Assert.Contains(
                factory.Starts,
                start => start.Origin.Reason
                    == GenericActorRuntimeStart.SpawnReason
                        .AutomaticActivation));

        for (int tick = 121; tick < 260; tick++)
            session.Step(session.PrepareTick().Observations);
        GenericActorMatchPreparedTick secondActivation =
            session.PrepareTick();
        Assert.Equal(6, secondActivation.Observations.Length);
        session.Step(secondActivation.Observations);
        Assert.Equal(
            [
                new ActorIdentity(0, 2, 0),
                new ActorIdentity(1, 2, 0),
            ],
            session.Chronology.Ticks[260].TickStart.LifeStarts
                .Select(start => start.ActorId)
                .ToArray());

        string replay = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(session.Chronology));
        Assert.Contains(
            "\"spawnReason\":\"automatic-activation\"",
            replay,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MapArms_KeepRulesStableAndChangeOnlyMapIdentity()
    {
        ActorResolvedMatchDefinition current =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorResolvedMatchDefinition thinFronts =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment(
                FrontlineLabsDuelMapArm.ThinFronts);
        ActorResolvedMatchDefinition bypass =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment(
                FrontlineLabsDuelMapArm.OuterShoulderBypass);

        string rulesFingerprint =
            ActorContractFingerprint.ComputeRules(current.Rules);
        Assert.Equal(
            rulesFingerprint,
            ActorContractFingerprint.ComputeRules(thinFronts.Rules));
        Assert.Equal(
            rulesFingerprint,
            ActorContractFingerprint.ComputeRules(bypass.Rules));

        Assert.Equal("frontline-labs-01", current.Map.Id);
        Assert.Equal(
            "frontline-labs-01-thin-fronts-experiment",
            thinFronts.Map.Id);
        Assert.Equal(
            "frontline-labs-01-outer-shoulder-bypass-experiment",
            bypass.Map.Id);
        Assert.Equal(
            3,
            new[]
            {
                current,
                thinFronts,
                bypass,
            }.Select(definition =>
                ActorContractFingerprint.ComputeMap(definition.Map))
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            3,
            new[]
            {
                current,
                thinFronts,
                bypass,
            }.Select(ActorContractFingerprint.ComputeMatch)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void ProgressionCausalArms_ShareEveryCommonLifePrivateSeed()
    {
        ActorResolvedMatchDefinition manual =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorResolvedMatchDefinition automatic =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();

        Assert.Equal(
            FrontlineLabsDefinition.DuelDepthSeedProfileId,
            manual.Rules.SeedMechanics.SeedProfileId);
        Assert.Equal(
            manual.Rules.SeedMechanics.SeedProfileId,
            automatic.Rules.SeedMechanics.SeedProfileId);
        foreach (int teamId in new[] { 0, 1 })
        {
            foreach (int unitId in new[] { 0, 1, 2 })
            {
                foreach (int lifeId in new[] { 0, 1, 7 })
                {
                    var actorId = new ActorIdentity(
                        teamId,
                        unitId,
                        lifeId);
                    Assert.Equal(
                        SeedDerivation.DeriveActorSeed(
                            104_729,
                            actorId,
                            manual.Rules.SeedMechanics.SeedProfileId),
                        SeedDerivation.DeriveActorSeed(
                            104_729,
                            actorId,
                            automatic.Rules.SeedMechanics.SeedProfileId));
                }
            }
        }
    }

    [Fact]
    public void ThinFronts_NarrowEveryObjectiveAcrossTheAdvanceAxis()
    {
        ActorResolvedMatchDefinition current =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorMapDefinition map =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment(
                    FrontlineLabsDuelMapArm.ThinFronts)
                .Map;
        ActorMapRegionDefinition[] objectives =
        [
            .. map.Regions
                .Where(region => region.RegionId.StartsWith(
                    "frontline-position-",
                    StringComparison.Ordinal))
                .OrderBy(region => region.RegionId, StringComparer.Ordinal),
        ];

        Assert.Equal(
            current.Map.TileRows.ToArray(),
            map.TileRows.ToArray());
        Assert.Equal(5, objectives.Length);
        Assert.All(objectives, objective =>
        {
            Assert.Equal(3, objective.Tiles.Length);
            Assert.Single(objective.Tiles.Select(tile => tile.X).Distinct());
            Assert.All(objective.Tiles, tile => Assert.False(map.IsWall(tile)));
        });
        Assert.Equal(
            [4, 7, 11, 15, 18],
            objectives.Select(objective => objective.Tiles[0].X).ToArray());
    }

    [Fact]
    public void OuterShoulderBypass_AddsAnEarlyCostlyExitWithoutOpeningTheChoke()
    {
        ActorMapDefinition current =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment().Map;
        ActorMapDefinition bypass =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment(
                    FrontlineLabsDuelMapArm.OuterShoulderBypass)
                .Map;
        Position[] openedShoulders =
        [
            new(8, 6),
            new(14, 6),
            new(8, 8),
            new(14, 8),
        ];

        Assert.All(openedShoulders, tile =>
        {
            Assert.True(current.IsWall(tile));
            Assert.False(bypass.IsWall(tile));
        });
        Assert.True(bypass.IsWall(new Position(9, 6)));
        Assert.True(bypass.IsWall(new Position(9, 8)));
        Assert.True(bypass.IsWall(new Position(13, 6)));
        Assert.True(bypass.IsWall(new Position(13, 8)));

        var start = new Position(8, 7);
        var objectiveEntry = new Position(10, 7);
        var directChokeTile = new Position(9, 7);
        Assert.Equal(
            8,
            ShortestPath(
                current,
                start,
                objectiveEntry,
                directChokeTile));
        Assert.Equal(
            6,
            ShortestPath(
                bypass,
                start,
                objectiveEntry,
                directChokeTile));

        ActorMapTileTagDefinition placementForbidden =
            bypass.TileTags.Single(tag =>
                tag.Kind
                    == ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden);
        Assert.All(
            openedShoulders,
            tile => Assert.Contains(tile, placementForbidden.Tiles));
    }

    private static int ShortestPath(
        ActorMapDefinition map,
        Position start,
        Position target,
        Position excluded)
    {
        var queue = new Queue<(Position Position, int Distance)>();
        var visited = new HashSet<Position> { start, excluded };
        queue.Enqueue((start, 0));
        while (queue.TryDequeue(out var current))
        {
            if (current.Position == target)
                return current.Distance;
            foreach ((int dx, int dy) in new[]
                     {
                         (0, -1),
                         (1, 0),
                         (0, 1),
                         (-1, 0),
                     })
            {
                Position next = current.Position.Offset(dx, dy);
                if (map.IsWall(next) || !visited.Add(next))
                    continue;
                queue.Enqueue((next, current.Distance + 1));
            }
        }
        return int.MaxValue;
    }
}
