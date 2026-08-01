using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayH0DefinitionTests
{
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
