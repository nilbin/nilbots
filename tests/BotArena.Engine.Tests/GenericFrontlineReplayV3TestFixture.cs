using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

internal static class GenericFrontlineReplayV3TestFixture
{
    public const ulong Seed = 9_007_199_254_740_995UL;

    public static (
        ReplayV3 Replay,
        ActorResolvedMatchDefinition Definition)
        CreateCompleteReplay()
    {
        ActorResolvedMatchDefinition definition = Definition();
        return (CreateReplay(definition), definition);
    }

    public static ReplayV3 CreateReplay(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision>? decide = null)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                decide
                ?? ((_, _) =>
                    GenericDeathmatchSessionTestFixture.Wait()));
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            Seed);
        session.Run();
        return ReplayV3Projection.Project(session.Chronology);
    }

    public static ActorResolvedMatchDefinition Definition(
        int maxTicks = 1,
        bool quickBreach = false,
        int quickBreachRedeployPauseTicks = 0)
    {
        ActorResolvedMatchDefinition baseline =
            GenericActorContractTestFixture.Frontline();
        ActorRulesDefinition source = baseline.Rules;
        ActorMovementProfileDefinition movement =
            source.MovementProfiles.Single();
        ActorAttackProfileDefinition attack =
            source.AttackProfiles.Single();
        var vision = new ActorVisionProfileDefinition(
            "fixture-vision",
            range: 0,
            ActorVisionDistanceMetric.Chebyshev,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 0,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 0,
            hearingBearingSectors: 0,
            ActorHearingBearingModel.Disabled,
            hearingDistanceBandUpperBounds: [],
            loudEventKinds: []);
        var form = new ActorFormDefinition(
            "mobile",
            maxHealth: 1,
            movement.Id,
            vision.Id,
            attackProfileId: attack.Id,
            objectiveWeight: 1,
            allowedActionIds: quickBreach
                ? ["wait", "move", "shoot"]
                : ["wait", "shoot"]);
        ActorActionDefinition wait = source.Actions.Single(action =>
            string.Equals(
                action.Id,
                "wait",
                StringComparison.Ordinal));
        ActorActionDefinition shoot = source.Actions.Single(action =>
            string.Equals(
                action.Id,
                "shoot",
                StringComparison.Ordinal));
        var move = new ActorActionDefinition(
            "move",
            1,
            ActorActionKind.Movement,
            [ActorActionParameterKind.Direction]);
        GameModeDefinition gameMode = quickBreach
            ? new FrontlineGameModeDefinition(
                new FrontlineVictoryDefinition(
                    pushesToBreach: 2,
                    [
                        new ScoreRankingDefinition(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 3,
                new FrontlineCaptureDefinition(
                    threshold: 1,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 0,
                    decayIntervalTicks: 0,
                    redeployPauseTicks:
                        quickBreachRedeployPauseTicks))
            : source.GameMode;
        var rules = new ActorRulesDefinition(
            quickBreach
                ? "generic-frontline-replay-v3-breach-fixture"
                : "generic-frontline-replay-v3-fixture",
            new ActorRulesLimits(
                maxTicks,
                source.Limits.RuntimeFaults),
            source.SeedMechanics,
            gameMode,
            source.Lifecycle,
            forms: [form],
            movementProfiles: [movement],
            visionProfiles: [vision],
            attackProfiles: [attack],
            actions: quickBreach
                ? [wait, move, shoot]
                : [wait, shoot],
            source.FabricationTransitions,
            source.SameLifeTransitions,
            source.ReplicationTransitions,
            source.TeamPerception,
            source.Collisions,
            source.TickResolution);

        var controlledSpawn = new InitialSpawnDefinition(
            "west",
            new Position(4, 2),
            Direction.East);
        ImmutableArray<ActorMapSpawnAnchorDefinition> anchors =
            baseline.Map.SpawnAnchors
                .Select(anchor =>
                    string.Equals(
                        anchor.Spawn.SpawnId,
                        controlledSpawn.SpawnId,
                        StringComparison.Ordinal)
                        ? new ActorMapSpawnAnchorDefinition(
                            controlledSpawn,
                            anchor.CompatibleMovementLayers)
                        : anchor)
                .ToImmutableArray();
        var map = new ActorMapDefinition(
            "generic-frontline-replay-v3-arena",
            baseline.Map.Version,
            baseline.Map.TileRows,
            anchors,
            baseline.Map.Regions,
            baseline.Map.TileTags);
        ImmutableArray<InitialSpawnDefinition> spawns =
            baseline.InitialDeployment.Spawns
                .Select(spawn =>
                    string.Equals(
                        spawn.SpawnId,
                        controlledSpawn.SpawnId,
                        StringComparison.Ordinal)
                        ? controlledSpawn
                        : spawn)
                .ToImmutableArray();
        var deployment = new InitialDeploymentDefinition(
            spawns,
            baseline.InitialDeployment.Lives);
        ActorModeMapBindingDefinition modeMapBinding = quickBreach
            ? new FrontlineActorModeMapBindingDefinition(
                ["near-west", "centre", "near-east"],
                ((FrontlineActorModeMapBindingDefinition)
                    baseline.ModeMapBinding).TeamAdvances)
            : baseline.ModeMapBinding;

        return new ActorResolvedMatchDefinition(
            rules,
            map,
            baseline.Format,
            baseline.Topology,
            deployment,
            baseline.LifecycleAssignments,
            baseline.ParticipantRegionAssignments,
            modeMapBinding,
            baseline.CapabilityVersions);
    }
}
