namespace BotArena.Engine.Tests;

public sealed class FrontlineLabsDefinitionTests
{
    private const string RulesFingerprint =
        "ab63d409b682ad32fdb816c13cc3271413c2d0f6b1937e4933b6e455ff5d2593";
    private const string MapFingerprint =
        "e9e75c1366111c857c3af9b32828185ea7b937d7f176bf4e8843f4b550ed2d91";
    private const string FormatFingerprint =
        "dc81a4f285ada9baceba99751e2de2ede8247cd943ad5c2164368c2f55129463";
    private const string TopologyFingerprint =
        "b86eef4d71bc1b171e4a9f930914a95333fc2c62547e0604b589099bc5ff768c";
    private const string MatchFingerprint =
        "cf10fe4929d8cd11cace95e62b07d9732fbd1549dc2e9fe096f78605028ca837";

    [Fact]
    public void Create_ResolvesTheExactExperimentalHeadToHeadArm()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        FrontlineGameModeDefinition mode =
            Assert.IsType<FrontlineGameModeDefinition>(
                definition.Rules.GameMode);

        Assert.Equal("frontline-labs", FrontlineLabsDefinition.PlaylistKey);
        Assert.Equal(
            FrontlineLabsDefinition.RulesetId,
            definition.Rules.RulesetId);
        Assert.Equal(
            FrontlineLabsDefinition.MapId,
            definition.Map.Id);
        Assert.Equal(
            FrontlineLabsDefinition.MatchFormatId,
            definition.Format.FormatId);
        Assert.IsType<HeadToHeadMatchFormatDefinition>(
            definition.Format);
        Assert.Equal((23, 15), (definition.Map.Width, definition.Map.Height));
        Assert.Equal(2, definition.Topology.Teams.Length);
        Assert.Equal(2, definition.Topology.Participants.Length);
        Assert.Equal(6, definition.Topology.UnitSlots.Length);
        Assert.Equal(2, definition.Topology.InitialLives.Length);
        Assert.Equal(
            new ActorMatchCapabilityVersions(
                "generic-actor-match-2",
                "1.0",
                "1.0",
                2,
                2,
                2,
                2,
                2),
            definition.CapabilityVersions);
        Assert.All(
            definition.Topology.Participants,
            participant => Assert.Equal(
                3,
                definition.Topology.UnitSlots.Count(slot =>
                    slot.ControllerParticipantId
                    == participant.ParticipantId)));

        Assert.Equal(500, definition.Rules.Limits.MaxTicks);
        Assert.Equal(5, mode.FrontlinePositionCount);
        Assert.Equal(3, mode.FrontlineVictory.PushesToBreach);
        Assert.Equal(15, mode.Capture.Threshold);
        Assert.Equal(5, mode.Capture.RedeployPauseTicks);
        Assert.Equal(
            ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion,
            definition.Rules.TeamPerception.Kind);

        Assert.Equal(
            [
                "child-mobile",
                "prime-mobile",
                "replica-mobile",
                "turret",
            ],
            definition.Rules.Forms.Select(form => form.Id));
        ActorFormDefinition prime = definition.Rules.Forms.Single(
            form => form.Id == "prime-mobile");
        ActorFormDefinition child = definition.Rules.Forms.Single(
            form => form.Id == "child-mobile");
        ActorFormDefinition turret = definition.Rules.Forms.Single(
            form => form.Id == "turret");
        Assert.Equal((3, 1), (prime.MaxHealth, prime.ObjectiveWeight));
        Assert.Equal((3, 1), (child.MaxHealth, child.ObjectiveWeight));
        Assert.Equal((5, 0), (turret.MaxHealth, turret.ObjectiveWeight));
        Assert.Contains("fabricate", prime.AllowedActionIds);
        Assert.Contains("split", prime.AllowedActionIds);
        Assert.Contains("transform", child.AllowedActionIds);
        Assert.Equal(
            ["shoot-direction", "wait"],
            turret.AllowedActionIds.ToArray());

        Assert.Single(definition.Rules.FabricationTransitions);
        Assert.Single(definition.Rules.SameLifeTransitions);
        SplitReplicationTransitionDefinition split =
            Assert.IsType<SplitReplicationTransitionDefinition>(
                Assert.Single(definition.Rules.ReplicationTransitions));
        Assert.Equal(2, split.DescendantCount);
        Assert.Equal(0, split.MaxSourceGeneration);
        Assert.Equal(2, split.MinimumSourceHealth);
        Assert.Equal("replica-mobile", split.OutputFormId);

        Assert.Equal(
            [120, 260, 120, 260],
            definition.LifecycleAssignments
                .Where(assignment => assignment.UnlockTick.HasValue)
                .Select(assignment => assignment.UnlockTick!.Value));
        Assert.Equal(
            112,
            definition.Map.TileTags.Single(tag =>
                    tag.Kind
                    == ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden)
                .Tiles.Length);
    }

    [Fact]
    public void CaptureThresholdExperiment_GetsDistinctImmutableIdentity()
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition candidate =
            FrontlineLabsDefinition.CreateCaptureThresholdExperiment(12);
        FrontlineGameModeDefinition mode =
            Assert.IsType<FrontlineGameModeDefinition>(
                candidate.Rules.GameMode);

        Assert.Equal(
            "frontline-labs-1-experiment-capture-12",
            candidate.Rules.RulesetId);
        Assert.Equal(12, mode.Capture.Threshold);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(baseline.Map),
            ActorContractFingerprint.ComputeMap(candidate.Map));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(candidate.Rules));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(candidate));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.CreateCaptureThresholdExperiment(0));
    }

    [Fact]
    public void CaptureGainPhaseExperimentPublishesAResolvableSchedule()
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition candidate =
            FrontlineLabsDefinition.CreateCaptureGainPhaseExperiment(
                startsAtTick: 300,
                gainPerSoleTeamTick: 2);
        FrontlineCaptureDefinition capture =
            Assert.IsType<FrontlineGameModeDefinition>(
                candidate.Rules.GameMode).Capture;

        Assert.Equal(
            "frontline-labs-1-experiment-gain-t300-2",
            candidate.Rules.RulesetId);
        Assert.Equal(
            [("opening", 0, 1), ("late-escalation", 300, 2)],
            capture.GainSchedule
                .Select(phase => (
                    phase.PhaseId,
                    phase.StartsAtTick,
                    phase.GainPerSoleTeamTick)));
        Assert.Equal(1, capture.GainPhaseAtTick(299).GainPerSoleTeamTick);
        Assert.Equal(2, capture.GainPhaseAtTick(300).GainPerSoleTeamTick);
        Assert.Empty(
            Assert.IsType<FrontlineGameModeDefinition>(
                baseline.Rules.GameMode).Capture.GainSchedule);
        Assert.Equal(RulesFingerprint, ActorContractFingerprint.ComputeRules(
            baseline.Rules));
        Assert.Equal(MatchFingerprint, ActorContractFingerprint.ComputeMatch(
            baseline));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(candidate.Rules));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.CreateCaptureGainPhaseExperiment(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.CreateCaptureGainPhaseExperiment(300, 0));
    }

    [Fact]
    public void MobilizeExperimentAddsOneWayTurretExitWithoutChangingMap()
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition candidate =
            FrontlineLabsDefinition.CreateMobilizeExperiment();
        Dictionary<string, ActorFormDefinition> forms =
            candidate.Rules.Forms.ToDictionary(form => form.Id);

        Assert.Equal(
            "frontline-labs-1-experiment-mobilize",
            candidate.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(baseline.Map),
            ActorContractFingerprint.ComputeMap(candidate.Map));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(baseline.Rules),
            ActorContractFingerprint.ComputeRules(candidate.Rules));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(candidate));
        Assert.Empty(
            Assert.IsType<FrontlineGameModeDefinition>(
                candidate.Rules.GameMode).Capture.GainSchedule);

        ActorActionDefinition mobilize = candidate.Rules.Actions.Single(
            action => action.Id == "mobilize");
        Assert.Equal(104, mobilize.Code);
        Assert.Equal(ActorActionKind.SameLifeTransition, mobilize.Kind);
        Assert.Empty(mobilize.ParameterKinds);
        Assert.Contains("mobilize", forms["turret"].AllowedActionIds);
        Assert.DoesNotContain(
            "mobilize",
            forms["child-mobile"].AllowedActionIds);

        ActorFormTransitionDefinition anchor = candidate.Rules
            .SameLifeTransitions
            .OfType<ActorFormTransitionDefinition>()
            .Single(transition => transition.TransitionId == "anchor-child");
        ActorFormTransitionDefinition mobilizeTransition = candidate.Rules
            .SameLifeTransitions
            .OfType<ActorFormTransitionDefinition>()
            .Single(transition =>
                transition.TransitionId == "mobilize-child");
        Assert.False(anchor.IrreversibleForLife);
        Assert.Equal("turret", mobilizeTransition.SourceFormId);
        Assert.Equal("child-mobile", mobilizeTransition.TargetFormId);
        Assert.Equal("mobilize", mobilizeTransition.ActionId);
        Assert.Equal(
            ActorSameLifeHealthDefinition.HealthPolicyKind
                .PreserveCurrentCappedToTargetMaximum,
            mobilizeTransition.Health.Policy);
        Assert.True(mobilizeTransition.IrreversibleForLife);
        Assert.Empty(mobilizeTransition.Placement.RequiredTileTags);
        Assert.Empty(mobilizeTransition.Placement.ForbiddenTileTags);
    }

    [Fact]
    public void SplitOutputCannotAnchorAndPrimeSlotStaysObjectiveCapable()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(form => form.Id);
        SplitReplicationTransitionDefinition split =
            Assert.IsType<SplitReplicationTransitionDefinition>(
                Assert.Single(definition.Rules.ReplicationTransitions));
        ActorFormTransitionDefinition anchor =
            Assert.IsType<ActorFormTransitionDefinition>(
                Assert.Single(definition.Rules.SameLifeTransitions));
        ActorFormDefinition replica = forms[split.OutputFormId];

        Assert.Equal("replica-mobile", replica.Id);
        Assert.Equal(1, replica.ObjectiveWeight);
        Assert.DoesNotContain("transform", replica.AllowedActionIds);
        Assert.Equal("child-mobile", anchor.SourceFormId);

        foreach (ActorUnitSlotLifecycleAssignmentDefinition primeSlot in
                 definition.LifecycleAssignments.Where(
                     assignment => assignment.UnitId == 0))
        {
            Assert.DoesNotContain("child-mobile", primeSlot.AllowedFormIds);
            Assert.DoesNotContain("turret", primeSlot.AllowedFormIds);
            Assert.All(
                primeSlot.AllowedFormIds,
                formId => Assert.True(forms[formId].ObjectiveWeight > 0));
        }
    }

    [Fact]
    public void SplitDescendantsSubmittingAnchorAreRejected()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                    observation.Tick == 120
                    && observation.Self.FormId == "prime-mobile"
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : observation.Tick == 121
                          && observation.Self.FormId == "replica-mobile"
                            ? GenericDeathmatchSessionTestFixture.Transform(
                                "turret")
                            : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 9_001);
        for (int tick = 0; tick <= 120; tick++)
            session.Step();

        GenericActorMatchStepResult rejected = session.Step();

        Assert.Equal(4, rejected.ActionResolutions.Length);
        Assert.All(
            rejected.ActionResolutions,
            resolution =>
            {
                Assert.Equal(
                    GenericActorRuntimeActionResolution.ActionOutcome
                        .Rejected,
                    resolution.Resolution.Outcome);
                Assert.Equal(
                    "transform",
                    resolution.Resolution.SubmittedAction!.ActionId);
            });
        Assert.All(
            session.ActiveLives,
            life => Assert.Equal("replica-mobile", life.FormId));
    }

    [Fact]
    public void ActionAvailabilityIncludesStableLifecyclePrerequisites()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ActorId.TeamId == 0 && observation.Tick < 2
                        ? GenericDeathmatchSessionTestFixture.Move(
                            Direction.East)
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 9_002);

        GenericActorMatchPreparedTick opening = session.PrepareTick();
        Assert.All(
            opening.Observations,
            observation =>
            {
                Assert.False(observation.ActionLegalities.Single(action =>
                    action.ActionId == "fabricate").Available);
                Assert.False(observation.ActionLegalities.Single(action =>
                    action.ActionId == "split").Available);
            });

        session.Step(opening.Observations);
        while (session.Tick < 120)
        {
            GenericActorMatchPreparedTick prepared =
                session.PrepareTick();
            session.Step(prepared.Observations);
        }

        GenericActorMatchPreparedTick unlocked = session.PrepareTick();
        GenericActorRuntimeObservation movedPrime =
            unlocked.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 0);
        GenericActorRuntimeObservation homePrime =
            unlocked.Observations.Single(observation =>
                observation.Self.ActorId.TeamId == 1);

        Assert.Equal(new Position(4, 7), movedPrime.Self.Position);
        Assert.False(movedPrime.ActionLegalities.Single(action =>
            action.ActionId == "fabricate").Available);
        Assert.Single(movedPrime.ActionLegalities.Single(action =>
                action.ActionId == "fabricate")
            .Constraints
            .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                .UnitTargetConstraint>()
            .Single()
            .AllowedValues);
        Assert.True(homePrime.ActionLegalities.Single(action =>
            action.ActionId == "fabricate").Available);
        Assert.All(
            unlocked.Observations,
            observation => Assert.True(
                observation.ActionLegalities.Single(action =>
                    action.ActionId == "split").Available));
    }

    [Fact]
    public void Fingerprints_AreStableAcrossFreshMaterialization()
    {
        ActorResolvedMatchDefinition first =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition second =
            FrontlineLabsDefinition.Create();

        Assert.Equal(
            RulesFingerprint,
            ActorContractFingerprint.ComputeRules(first.Rules));
        Assert.Equal(
            MapFingerprint,
            ActorContractFingerprint.ComputeMap(first.Map));
        Assert.Equal(
            FormatFingerprint,
            ActorContractFingerprint.ComputeFormat(first.Format));
        Assert.Equal(
            TopologyFingerprint,
            ActorContractFingerprint.ComputeTopology(first.Topology));
        Assert.Equal(
            MatchFingerprint,
            ActorContractFingerprint.ComputeMatch(first));
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(first.Rules),
            ActorContractFingerprint.ComputeRules(second.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(first.Map),
            ActorContractFingerprint.ComputeMap(second.Map));
        Assert.Equal(
            ActorContractFingerprint.ComputeFormat(first.Format),
            ActorContractFingerprint.ComputeFormat(second.Format));
        Assert.Equal(
            ActorContractFingerprint.ComputeTopology(first.Topology),
            ActorContractFingerprint.ComputeTopology(second.Topology));
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(first),
            ActorContractFingerprint.ComputeMatch(second));
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(first),
            ActorContractManifestSerializer.ToCanonicalJson(second));
    }

    [Fact]
    public void OpeningTicks_AreIndependentOfInputEnumerationOrder()
    {
        ActorResolvedMatchDefinition firstDefinition =
            FrontlineLabsDefinition.Create();
        ActorResolvedMatchDefinition secondDefinition =
            FrontlineLabsDefinition.Create();
        using GenericActorMatchSession first =
            Session(firstDefinition, reverseConfigurations: false);
        using GenericActorMatchSession second =
            Session(secondDefinition, reverseConfigurations: true);

        for (int tick = 0; tick < 3; tick++)
        {
            GenericActorMatchPreparedTick firstPrepared =
                first.PrepareTick();
            GenericActorMatchPreparedTick secondPrepared =
                second.PrepareTick();
            first.Step(firstPrepared.Observations);
            second.Step(secondPrepared.Observations.Reverse());
        }

        string firstReplay = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(first.Chronology));
        string secondReplay = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(second.Chronology));
        Assert.Equal(firstReplay, secondReplay);
    }

    private static GenericActorMatchSession Session(
        ActorResolvedMatchDefinition definition,
        bool reverseConfigurations)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        return new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories,
                reverseConfigurations),
            matchSeed: 9_001);
    }
}
