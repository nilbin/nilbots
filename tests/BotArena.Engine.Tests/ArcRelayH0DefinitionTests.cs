using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayH0DefinitionTests
{
    [Fact]
    public void LoopProfiles_AreRegisteredOneFactorArmsWithDistinctIdentity()
    {
        ActorResolvedMatchDefinition baseline = ArcRelayH0Definition.Create();
        ActorResolvedMatchDefinition gates = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.HomeGatesWide);
        ActorResolvedMatchDefinition gatesThree = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.HomeGatesThree);
        ActorResolvedMatchDefinition concourse = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.HomeConcourse);
        ActorResolvedMatchDefinition cover = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.CoverTrim);
        ActorResolvedMatchDefinition larger = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.DepthLarger);
        ActorResolvedMatchDefinition counterflow = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.DepthCounterflow);
        ActorResolvedMatchDefinition return16 = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Return16);
        ActorResolvedMatchDefinition return24 = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Return24);
        ActorResolvedMatchDefinition hot = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Hot60);
        ActorResolvedMatchDefinition spacious = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Spacious90);

        string baselineRules = ActorContractFingerprint.ComputeRules(
            baseline.Rules);
        string baselineMap = ActorContractFingerprint.ComputeMap(baseline.Map);
        Assert.Equal(
            baselineRules,
            ActorContractFingerprint.ComputeRules(gates.Rules));
        Assert.Equal(
            baselineRules,
            ActorContractFingerprint.ComputeRules(gatesThree.Rules));
        Assert.Equal(
            baselineRules,
            ActorContractFingerprint.ComputeRules(concourse.Rules));
        Assert.Equal(
            baselineRules,
            ActorContractFingerprint.ComputeRules(cover.Rules));
        Assert.Equal(
            baselineRules,
            ActorContractFingerprint.ComputeRules(larger.Rules));
        Assert.Equal(
            baselineRules,
            ActorContractFingerprint.ComputeRules(counterflow.Rules));
        Assert.NotEqual(
            baselineMap,
            ActorContractFingerprint.ComputeMap(gates.Map));
        Assert.NotEqual(
            baselineMap,
            ActorContractFingerprint.ComputeMap(gatesThree.Map));
        Assert.NotEqual(
            baselineMap,
            ActorContractFingerprint.ComputeMap(concourse.Map));
        Assert.NotEqual(
            baselineMap,
            ActorContractFingerprint.ComputeMap(cover.Map));
        Assert.NotEqual(
            baselineMap,
            ActorContractFingerprint.ComputeMap(larger.Map));
        Assert.NotEqual(
            baselineMap,
            ActorContractFingerprint.ComputeMap(counterflow.Map));
        Assert.Equal(537, OpenTileCount(gates));
        Assert.Equal(549, OpenTileCount(gatesThree));
        Assert.Equal(535, OpenTileCount(concourse));
        Assert.Equal(543, OpenTileCount(cover));
        Assert.Equal((31, 29, 687),
            (larger.Map.Width, larger.Map.Height, OpenTileCount(larger)));
        Assert.Equal(OpenTileCount(gates), OpenTileCount(counterflow));
        Assert.Equal(
            counterflow.Map.TileRows,
            counterflow.Map.TileRows.Reverse()
                .Select(row => new string(row.Reverse().ToArray())));
        Assert.NotEqual(
            counterflow.Map.TileRows,
            counterflow.Map.TileRows.Select(row =>
                new string(row.Reverse().ToArray())));

        Assert.Equal(
            baselineMap,
            ActorContractFingerprint.ComputeMap(return16.Map));
        Assert.Equal(
            baselineMap,
            ActorContractFingerprint.ComputeMap(return24.Map));
        Assert.All(
            return16.Rules.Lifecycle.Profiles,
            profile => Assert.Equal(16, profile.DelayTicks));
        Assert.All(
            return24.Rules.Lifecycle.Profiles,
            profile => Assert.Equal(24, profile.DelayTicks));
        Assert.Equal(
            16,
            Assert.IsType<ArcRelayGameModeDefinition>(
                return16.Rules.GameMode).RespawnDelayTicks);
        Assert.Equal(
            24,
            Assert.IsType<ArcRelayGameModeDefinition>(
                return24.Rules.GameMode).RespawnDelayTicks);

        Assert.Equal(
            [(20, 60, 500), (40, 60, 520), (60, 60, 540)],
            Schedule(hot));
        Assert.Equal(
            [(30, 90, 480), (60, 90, 510), (90, 90, 540)],
            Schedule(spacious));
        Assert.All(
            new[] { return16, return24, hot, spacious },
            definition =>
            {
                Assert.NotEqual(
                    baselineRules,
                    ActorContractFingerprint.ComputeRules(definition.Rules));
                Assert.Equal(
                    baselineMap,
                    ActorContractFingerprint.ComputeMap(definition.Map));
            });
    }

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

    private static int OpenTileCount(ActorResolvedMatchDefinition definition) =>
        definition.Map.TileRows.Sum(row => row.Count(tile => tile == '.'));

    private static (int First, int Cadence, int Final)[] Schedule(
        ActorResolvedMatchDefinition definition) =>
        Assert.IsType<ArcRelayGameModeDefinition>(definition.Rules.GameMode)
            .Wells
            .Select(well => (
                well.FirstBirthTick,
                well.CadenceTicks,
                well.FinalBirthTick))
            .ToArray();

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

    [Fact]
    public void ReplayPresentation_AuthorsEveryClassLookAndSharedProjectile()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create();

        GenericActorReplayPresentation presentation =
            ArcRelayH0ReplayPresentation.Create(definition);

        Assert.Equal("ember-forge", presentation.ThemeId);
        Assert.Equal(16, presentation.Forms.Length);
        Assert.Equal(
            ArcRelayLaunchClassIds.All.Order(StringComparer.Ordinal),
            presentation.Forms.Select(form =>
                form.FormId[ArcRelayH0Definition.FormPrefix.Length..]));
        Assert.All(presentation.Forms, form =>
        {
            string classId = form.FormId[
                ArcRelayH0Definition.FormPrefix.Length..];
            Assert.Equal($"arc-{classId}", form.LookId);
            Assert.Equal("arc-pulse", form.ProjectileLookId);
        });
    }

    [Fact]
    public void MindMatch_ReplayRoundTripsWithArcObjectiveLedger()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create();
        ActorActionDefinition wait = definition.Rules.Actions.Single(value =>
            value.Kind == ActorActionKind.Wait);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body =>
                        new GenericMindCommand(
                            body.ActorId.UnitId,
                            body.ActorId.LifeId,
                            wait.Id,
                            wait.Code,
                            [])),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 20_260_801UL);

        session.Run();
        GenericActorReplayDocument document =
            GenericActorReplayDocument.Create(session);

        Assert.True(
            GenericActorReplayDocument.VerifyHash(
                document.CanonicalJson,
                out string? failure),
            failure);
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(
            document.CanonicalJson);
        Assert.Equal(document.ReplayHash, ReplayV3Serializer.ComputeHash(reread));
        Assert.Contains(
            reread.Ticks.SelectMany(value => value.TickStart.Events),
            value => value.Payload is ReplayV3.EventPayload.ArcRelay
            {
                Fact: ReplayV3.ArcRelayFact.CoreBorn,
            });
        GenericActorArcRelayReplaySummary summary =
            GenericActorArcRelayReplaySummary.Read(document.CanonicalJson);
        Assert.Equal(21, summary.ScheduledBirths);
        Assert.Equal(3, summary.ActualBirths);
        Assert.Equal(0, summary.Banks);
        Assert.Contains(
            "Cores: scheduled 21, born 3",
            summary.Format(),
            StringComparison.Ordinal);

        string forgedBirth = MutateAndRehash(
            document.CanonicalJson,
            root =>
            {
                JsonObject fact = root["ticks"]!.AsArray()[25]!["tickStart"]![
                    "events"]!.AsArray()
                    .Select(value => value!.AsObject())
                    .Single(value => value["payload"]!["kind"]!
                        .GetValue<string>() == "arc-relay"
                        && value["payload"]!["fact"]!["kind"]!
                            .GetValue<string>() == "core-born")
                    ["payload"]!["fact"]!.AsObject();
                fact["position"]!["x"] = 14;
            });
        Assert.False(
            GenericActorReplayDocument.VerifyHash(
                forgedBirth,
                out string? chronologyFailure));
        Assert.Contains(
            "Core facts",
            chronologyFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LaunchSignatures_UseTypedMasksAndPublishTheirPhaseGrammar()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create();
        ActorActionDefinition wait = definition.Rules.Actions.Single(value =>
            value.Kind == ActorActionKind.Wait);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body =>
                    {
                        GenericActorRuntimeActionLegality? signature =
                            observation.Tick == 0
                                ? body.ActionLegalities.FirstOrDefault(value =>
                                    value.ActionCode
                                        >= ArcRelayActionIds.FirstSignatureCode
                                    && value.Available)
                                : null;
                        return signature is null
                            ? new GenericMindCommand(
                                body.ActorId.UnitId,
                                body.ActorId.LifeId,
                                wait.Id,
                                wait.Code,
                                [])
                            : new GenericMindCommand(
                                body.ActorId.UnitId,
                                body.ActorId.LifeId,
                                signature.ActionId,
                                signature.ActionCode,
                                Arguments(signature));
                    }),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 20_260_802UL);
        for (int tick = 0; tick < 20; tick++)
            session.Step();

        GenericActorMatchChronology chronology = session.Chronology;
        ArcRelayEvent.SignatureChanged[] signatureFacts = chronology.Ticks
            .SelectMany(value => value.Events.Concat(value.TickStart.Events))
            .Select(value => value.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .OfType<ArcRelayEvent.SignatureChanged>()
            .ToArray();
        Assert.True(
            signatureFacts.Where(value => value.Reason == "started")
                .Select(value => value.SignatureId)
                .Distinct(StringComparer.Ordinal).Count() >= 10);
        Assert.Contains(signatureFacts, value => value.Phase
            == ArcRelaySignatureState.SignaturePhase.Tell);
        Assert.Contains(signatureFacts, value => value.Phase
            == ArcRelaySignatureState.SignaturePhase.Active);
        Assert.Contains(signatureFacts, value => value.Phase
            == ArcRelaySignatureState.SignaturePhase.InFlight);
        ReplayV3 projected = ReplayV3Projection.Project(chronology);
        ReplayV3.TickFrame tickOne = projected.Ticks[1];
        var observedArc = Assert.IsType<ReplayV3.ModeState.ArcRelay>(
            tickOne.MindTurns[0].Observation.Mode);
        var stateArc = Assert.IsType<ReplayV3.ModeState.ArcRelay>(
            tickOne.TickStart.State.Mode);
        HashSet<ReplayV3.PositionValue> visible = tickOne.MindTurns[0]
            .Observation.VisibleTiles.Select(value => value.Position)
            .ToHashSet();
        Assert.True(stateArc.Wells.SequenceEqual(observedArc.Wells));
        Assert.True(stateArc.Reactors.SequenceEqual(observedArc.Reactors));
        Assert.True(
            stateArc.VisibleSignatures.Where(value =>
                value.OwnerTeamId == 0 || value.Phase == "tell"
                || value.Positions.Any(visible.Contains))
            .SequenceEqual(observedArc.VisibleSignatures));
        string replayJson = ReplayV3Serializer.ToJson(projected);
        Assert.Contains("\"signature-changed\"", replayJson,
            StringComparison.Ordinal);
        Assert.Contains("\"visibleSignatures\"", replayJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TripNodeProximityReveal_RoundTripsOutsideFacingVision()
    {
        string[] teamZero =
        [
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Hush,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            teamZero,
            teamOneClasses: null);
        ActorActionDefinition wait = definition.Rules.Actions.Single(value =>
            value.Kind == ActorActionKind.Wait);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body =>
                    {
                        GenericActorRuntimeActionLegality action;
                        ImmutableArray<GenericActorRuntimeActionArgument>
                            arguments;
                        if (start.TeamId == 0 && body.ActorId.UnitId == 2
                            && observation.Tick <= 8)
                        {
                            action = body.ActionLegalities.Single(value =>
                                value.ActionId == ArcRelayH0Definition.MoveActionId);
                            arguments =
                            [
                                new GenericActorRuntimeActionArgument
                                    .ProjectileHeadingArgument(
                                        ProjectileHeading.East),
                            ];
                        }
                        else if (start.TeamId == 0 && body.ActorId.UnitId == 2
                                 && observation.Tick == 9)
                        {
                            action = body.ActionLegalities.Single(value =>
                                value.ActionId == "trip-node");
                            arguments =
                            [
                                new GenericActorRuntimeActionArgument
                                    .PositionTargetArgument(
                                        new Position(13, 10)),
                            ];
                        }
                        else if (start.TeamId == 0 && body.ActorId.UnitId == 2
                                 && observation.Tick == 10)
                        {
                            action = body.ActionLegalities.Single(value =>
                                value.ActionId == ArcRelayH0Definition.MoveActionId);
                            arguments =
                            [
                                new GenericActorRuntimeActionArgument
                                    .ProjectileHeadingArgument(
                                        ProjectileHeading.North),
                            ];
                        }
                        else if (start.TeamId == 1 && body.ActorId.UnitId == 2
                                 && observation.Tick <= 15)
                        {
                            action = body.ActionLegalities.Single(value =>
                                value.ActionId == ArcRelayH0Definition.MoveActionId);
                            arguments =
                            [
                                new GenericActorRuntimeActionArgument
                                    .ProjectileHeadingArgument(
                                        ProjectileHeading.West),
                            ];
                        }
                        else
                        {
                            action = body.ActionLegalities.Single(value =>
                                value.ActionId == wait.Id);
                            arguments = [];
                        }
                        return new GenericMindCommand(
                            body.ActorId.UnitId,
                            body.ActorId.LifeId,
                            action.ActionId,
                            action.ActionCode,
                            arguments);
                    }),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 20_260_803UL);
        for (int tick = 0; tick < 18; tick++)
            session.Step();

        GenericMindRuntimeObservation observed = factories[1].Observations
            .Single(value => value.Tick == 16);
        var arc = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                observed.Mode);
        ArcRelaySignatureState node = Assert.Single(
            arc.VisibleSignatures,
            value => value.Kind
                == ArcRelaySignatureDefinition.SignatureKind.TripNode);
        Position nodePosition = Assert.Single(node.Positions);
        Assert.Equal(new Position(13, 10), nodePosition);
        Assert.DoesNotContain(
            observed.VisibleTiles,
            value => value.Position == nodePosition);

        ReplayV3 projected = ReplayV3Projection.Project(session.Chronology);
        string replayJson = ReplayV3Serializer.ToJson(projected);
        Assert.Contains("\"trip-node\"", replayJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TickStartSignatureDamageAndRepairMayNetToTheSameLifeState()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create();
        ActorIdentity target = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        Position position = new(2, 11);
        var life = new GenericActorWorldSnapshot.LifeSnapshot(
            target,
            participantId: 0,
            generation: 0,
            ArcRelayH0Definition.FormPrefix + ArcRelayLaunchClassIds.Kestrel,
            position,
            Direction.East,
            health: 3,
            cooldown: 0,
            energy: null,
            spawnedAtTick: 0,
            GenericActorRuntimeStart.SpawnReason.Initial,
            parentActorId: null,
            sourceTransitionId: null,
            sourceOperationId: null,
            previousActionResolution: null,
            pendingSameLifeTransition: null);
        GenericActorAuthoritativeEvent[] events =
        [
            ArcFact(
                0,
                new ArcRelayEvent.SignatureDamage(
                    "damage-op",
                    "falling-star",
                    ActorIdentity.FromTeamUnitLife(1, 0, 0),
                    target,
                    Amount: 1,
                    NewHealth: 2,
                    position)),
            ArcFact(
                1,
                new ArcRelayEvent.SignatureRepair(
                    "repair-op",
                    "repair-beam",
                    ActorIdentity.FromTeamUnitLife(0, 1, 0),
                    target,
                    Amount: 1,
                    NewHealth: 3,
                    position)),
        ];

        Assert.True(GenericActorMatchChronology
            .TickStartLifeEvidenceExplainsState(
                definition,
                life,
                life,
                transitionEvents: [],
                arcEvents: events));
    }

    [Fact]
    public void ArcTossMask_DoesNotOfferTargetsThatClipBackToTheCarrier()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Switchback,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity actor = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        var source = new Position(6, 13);
        var lives = new[]
        {
            new ArcRelaySignatureRuntime.Life(actor, source, 4, 4),
        };

        ImmutableArray<Position> targets = runtime.PositionTargets(
            actor,
            source,
            definition.Map.TileRows
                .SelectMany((row, y) => row.Select((tile, x) => (tile, x, y)))
                .Where(value => value.tile != '#')
                .Select(value => new Position(value.x, value.y))
                .ToHashSet(),
            lives,
            carriesCore: true);

        Assert.DoesNotContain(new Position(4, 11), targets);
        Assert.Contains(new Position(7, 13), targets);
    }

    [Fact]
    public void SurveyFlareMask_DoesNotOfferTheLanternsOwnTile()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Switchback,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity actor = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        var source = new Position(6, 13);
        var lives = new[]
        {
            new ArcRelaySignatureRuntime.Life(actor, source, 4, 4),
        };

        ImmutableArray<Position> targets = runtime.PositionTargets(
            actor,
            source,
            definition.Map.TileRows
                .SelectMany((row, y) => row.Select((tile, x) => (tile, x, y)))
                .Where(value => value.tile != '#')
                .Select(value => new Position(value.x, value.y))
                .ToHashSet(),
            lives,
            carriesCore: false);

        Assert.DoesNotContain(source, targets);
        Assert.Contains(new Position(7, 13), targets);
    }

    [Fact]
    public void SurveyFlareRemainsValidWhenDisplacementReachesItsTargetTile()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Switchback,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity actor = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        Position resolvedSourceAndTarget = new(15, 4);

        runtime.Start(
            tick: 10,
            actor,
            resolvedSourceAndTarget,
            "survey-flare",
            [
                new GenericActorRuntimeActionArgument.PositionTargetArgument(
                    resolvedSourceAndTarget),
            ],
            [
                new ArcRelaySignatureRuntime.Life(
                    actor,
                    resolvedSourceAndTarget,
                    3,
                    3),
            ]);

        ArcRelaySignatureState flare = Assert.Single(runtime.Project(10));
        Assert.Equal(resolvedSourceAndTarget, Assert.Single(flare.Positions));
    }

    private static ImmutableArray<GenericActorRuntimeActionArgument> Arguments(
        GenericActorRuntimeActionLegality legality) =>
        [
            .. legality.Constraints.Select(value => value switch
            {
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .DirectionConstraint constraint =>
                    (GenericActorRuntimeActionArgument)new
                        GenericActorRuntimeActionArgument.DirectionArgument(
                            constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint constraint =>
                    new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(
                            constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .UnitTargetConstraint constraint =>
                    new GenericActorRuntimeActionArgument.UnitTargetArgument(
                        constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .PositionTargetConstraint constraint =>
                    new GenericActorRuntimeActionArgument
                        .PositionTargetArgument(
                            constraint.AllowedValues[0]),
                _ => throw new InvalidOperationException(
                    "Arc signature exposed an unexpected argument constraint."),
            }),
        ];

    private static GenericActorAuthoritativeEvent ArcFact(
        int ordinal,
        ArcRelayEvent fact) =>
        new(
            $"arc-{ordinal}",
            tick: 1,
            globalOrdinal: ordinal,
            GenericActorRuntimeObservation.EventKind.ArcRelay,
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(fact),
            new GenericActorAuthoritativeEvent.Audience.Public());

    private static string MutateAndRehash(
        string json,
        Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (string propertyName in
                     new[] { "header", "initialFrame", "ticks", "result" })
            {
                writer.WritePropertyName(propertyName);
                root[propertyName]!.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        root["replayHash"] = Convert.ToHexStringLower(
            SHA256.HashData(stream.ToArray()));
        return root.ToJsonString();
    }
}
