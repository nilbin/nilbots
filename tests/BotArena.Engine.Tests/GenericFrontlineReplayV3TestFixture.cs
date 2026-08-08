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

    public static ActorResolvedMatchDefinition
        ClassedStickyProbeDefinition()
    {
        ActorResolvedMatchDefinition baseline = Definition(
            maxTicks: 2,
            quickBreach: true);
        var sourceMode = Assert.IsType<FrontlineGameModeDefinition>(
            baseline.Rules.GameMode);
        FrontlineCaptureDefinition sourceCapture = sourceMode.Capture;
        var capture = new FrontlineCaptureDefinition(
            sourceCapture.Threshold,
            sourceCapture.GainPerSoleTeamTick,
            sourceCapture.DecayAmount,
            sourceCapture.DecayIntervalTicks,
            sourceCapture.RedeployPauseTicks,
            sourceCapture.GainSchedule,
            sourceCapture.ControlPolicy,
            sourceCapture.DecayClock,
            FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks,
            ratchetHoldTicks: 4);
        var mode = new FrontlineGameModeDefinition(
            sourceMode.FrontlineVictory,
            sourceMode.ScoreCatalog,
            sourceMode.FrontlinePositionCount,
            capture);
        ActorVisionProfileDefinition sourceVision =
            baseline.Rules.VisionProfiles.Single();
        var vision = new ActorVisionProfileDefinition(
            sourceVision.Id,
            range: 8,
            sourceVision.DistanceMetric,
            ActorVisionShape.Omnidirectional,
            omnidirectionalProximityRange: 8,
            sourceVision.LineOfSight,
            sourceVision.HearingRadius,
            sourceVision.HearingBearingSectors,
            sourceVision.HearingBearingModel,
            sourceVision.HearingDistanceBandUpperBounds,
            sourceVision.LoudEventKinds);
        ActorRulesDefinition source = baseline.Rules;
        var rules = new ActorRulesDefinition(
            "generic-frontline-class-observation-probe",
            source.Limits,
            source.SeedMechanics,
            mode,
            source.Lifecycle,
            source.Forms,
            source.MovementProfiles,
            [vision],
            source.AttackProfiles,
            source.Actions,
            source.FabricationTransitions,
            source.SameLifeTransitions,
            source.ReplicationTransitions,
            source.TeamPerception,
            source.Collisions,
            source.TickResolution);
        PublicMatchTopology topology = baseline.Topology with
        {
            Teams =
            [
                new PublicScoringTeam(0, "bulwark"),
                new PublicScoringTeam(1, "striker"),
            ],
            Participants =
            [
                new PublicParticipant(10, 0, "bulwark"),
                new PublicParticipant(20, 1, "striker"),
            ],
        };
        return new ActorResolvedMatchDefinition(
            rules,
            baseline.Map,
            baseline.Format,
            topology,
            baseline.InitialDeployment,
            baseline.LifecycleAssignments,
            baseline.ParticipantRegionAssignments,
            baseline.ModeMapBinding,
            baseline.CapabilityVersions);
    }
}
