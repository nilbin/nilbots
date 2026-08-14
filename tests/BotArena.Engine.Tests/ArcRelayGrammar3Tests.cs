using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Tapline-2 is one complete grammar-3 roster. Four definitions are
/// presence-versioned because their chronology changed; the other twelve keep
/// their grammar-2 mechanics while publishing the same lifecycle vocabulary.
/// Historical Tapline-1 remains byte-frozen beside it.
/// </summary>
public sealed class ArcRelayGrammar3Tests
{
    [Fact]
    public void EveryConcreteKindMapsToExactlyOneOfTheSixteenStableIds()
    {
        string[] stableIds = Enum
            .GetValues<ArcRelaySignatureDefinition.SignatureKind>()
            .Select(ArcRelaySignatureDefinition.StableIdFor)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "arc-toss", "exchange", "falling-star", "hardlight-block",
                "kinetic-burst", "null-field", "prism-wall", "rail-line",
                "repair-beam", "sentinel-seed", "smoke-canister",
                "survey-flare", "target-paint", "tractor-hook", "trip-node",
                "vector-dash",
            ],
            stableIds);
    }

    [Fact]
    public void Tapline2IsOneGrammar3RosterWithExactlyFourVersionedEnvelopes()
    {
        ActorResolvedMatchDefinition one = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline1);
        ActorResolvedMatchDefinition two = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline2);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            two.Rules.GameMode);

        Assert.Equal(3, mode.SignatureGrammarVersion);
        Assert.Equal(16, mode.Signatures.Length);
        Assert.Equal(one.Map.Id, two.Map.Id);
        Assert.True(one.Map.TileRows.SequenceEqual(two.Map.TileRows));
        Assert.Equal("tapline-4", ArcRelayLoopProfile.Current.Id);

        string[] versioned = mode.Signatures.Where(signature => signature is
                ArcRelaySignatureDefinition.NullField3
                or ArcRelaySignatureDefinition.TargetPaint2
                or ArcRelaySignatureDefinition.KineticBurst2
                or ArcRelaySignatureDefinition.SmokeCanister2)
            .Select(signature => signature.SignatureId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["kinetic-burst", "null-field", "smoke-canister", "target-paint"],
            versioned);

        Assert.Equal(1, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.TargetPaint2>()
            .Single().AcquisitionTicks);
        Assert.Equal(8, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.TargetPaint2>()
            .Single().CancelCooldownTicks);
        Assert.Equal(6, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.KineticBurst2>()
            .Single().CancelCooldownTicks);
        Assert.Equal(3, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.SmokeCanister2>()
            .Single().TravelTilesPerTick);
    }

    [Fact]
    public void Tapline3VersionsAtomicConvoysWithoutMovingTapline2()
    {
        ActorResolvedMatchDefinition two = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline2);
        ActorResolvedMatchDefinition three = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline3);

        Assert.False(two.Rules.Collisions.FollowingVacatedActorAllowed);
        Assert.True(three.Rules.Collisions.FollowingVacatedActorAllowed);
        Assert.Equal(
            ActorCollisionDefinition.MovementResolutionKind
                .ExclusiveGroundOccupancyAllowAcyclicFollowingChains,
            three.Rules.Collisions.MovementResolution);
        Assert.Equal(two.Map.Id, three.Map.Id);
        Assert.True(two.Map.TileRows.SequenceEqual(three.Map.TileRows));
        Assert.Equal("arc-relay-tapline-03", three.Rules.RulesetId);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(two.Rules),
            ActorContractFingerprint.ComputeRules(three.Rules));
    }

    [Fact]
    public void EverySignaturePublishesGrammar3LifecycleTiming()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline2);
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            definition);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var mode = Assert.IsType<GenericActorRulesContract.ArcRelayGameMode>(
            contract.Rules.GameMode);

        Assert.Equal(3, mode.SignatureGrammarVersion);
        Assert.Equal(16, mode.Signatures.Length);
        Assert.All(mode.Signatures, signature =>
        {
            Assert.NotNull(signature.InitialPhase);
            Assert.NotNull(signature.MaturityPhase);
            Assert.NotNull(signature.CooldownAnchor);
        });
        Assert.Equal(
            "post-movement",
            mode.Signatures.Single(value =>
                value.SignatureId == "null-field").MaturityPhase);
        Assert.Equal(
            1,
            mode.Signatures.Single(value =>
                value.SignatureId == "target-paint").AcquisitionTicks);
        Assert.Equal(
            3,
            mode.Signatures.Single(value =>
                value.SignatureId == "smoke-canister").TravelTilesPerTick);
    }

    [Fact]
    public void Grammar3MintsNewRulesBytesWithoutMovingTapline1()
    {
        const string frozenTapline1 =
            "b1c38905a9c3ad4a653cf50f0cf4fd131bfaee42eb9e037c7c1c3ebbd6ed5872";
        string one = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(ArcRelayLoopProfile.Tapline1));
        string two = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(ArcRelayLoopProfile.Tapline2));

        Assert.Equal(frozenTapline1, one);
        Assert.NotEqual(one, two);
        Assert.Equal("arc-relay-tapline-01", ArcRelayLoopProfile.Tapline2.MapId);
        Assert.Equal(
            "arc-relay-tapline-02",
            ArcRelayLoopProfile.Tapline2.RulesetId);
    }

    [Fact]
    public void NullFieldAndKineticBurstMatureOnlyAfterResponseMovement()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity hush, Position origin) =
            Runtime(ArcRelayLaunchClassIds.Hush);
        ArcRelaySignatureRuntime.Life[] hushLives =
            [Life(hush, origin, 4)];
        runtime.Start(
            10,
            hush,
            origin,
            "null-field",
            [],
            hushLives);

        runtime.Advance(11, hushLives);
        Assert.Equal(
            ArcRelaySignatureState.SignaturePhase.Tell,
            Assert.Single(runtime.Project(11)).Phase);
        ArcRelaySignatureRuntime.TickResult activated =
            runtime.ResolvePostMovement(
                11,
                hushLives,
                (_, _) => true);
        ArcRelaySignatureState field = Assert.Single(runtime.Project(11));
        Assert.Equal(ArcRelaySignatureState.SignaturePhase.Active, field.Phase);
        Assert.Equal(16, field.EndsAtTick);
        Assert.Contains(activated.Events, value =>
            Changed(value)?.Reason == "activated");

        (runtime, ActorIdentity repulsor, origin) =
            Runtime(ArcRelayLaunchClassIds.Repulsor);
        ActorIdentity steppedOut = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        ActorIdentity steppedIn = ActorIdentity.FromTeamUnitLife(1, 1, 0);
        runtime.Start(
            20,
            repulsor,
            origin,
            "kinetic-burst",
            [],
            [Life(repulsor, origin, 5)]);
        runtime.Advance(
            21,
            [
                Life(repulsor, origin, 5),
                Life(steppedOut, origin.Offset(2, 0), 5),
                Life(steppedIn, origin.Offset(0, 1), 5),
            ]);
        Assert.Equal(
            ArcRelaySignatureState.SignaturePhase.Tell,
            Assert.Single(runtime.Project(21)).Phase);
        ArcRelaySignatureRuntime.TickResult burst = runtime.ResolvePostMovement(
            21,
            [
                Life(repulsor, origin, 5),
                Life(steppedOut, origin.Offset(2, 0), 5),
                Life(steppedIn, origin.Offset(0, 1), 5),
            ],
            (_, _) => true);
        ArcRelaySignatureRuntime.Effect.KineticBurst effect = Assert.IsType<
            ArcRelaySignatureRuntime.Effect.KineticBurst>(
                Assert.Single(burst.Effects));
        Assert.True(effect.Targets.SequenceEqual([steppedIn]));
        Assert.Empty(runtime.Project(21));
        Assert.False(runtime.CanStart(
            repulsor, "kinetic-burst", 32, origin));
        Assert.True(runtime.CanStart(
            repulsor, "kinetic-burst", 33, origin));
    }

    [Fact]
    public void TargetPaintTracksAndItsMoveCancelPaysHalfCooldown()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity sunder, Position origin) =
            Runtime(ArcRelayLaunchClassIds.Sunder);
        ActorIdentity target = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        Position targetPosition = origin.Offset(4, 0);
        ArcRelaySignatureRuntime.Life[] lives =
        [
            Life(sunder, origin, 4),
            Life(target, targetPosition, 5),
        ];
        runtime.Start(
            30,
            sunder,
            origin,
            "target-paint",
            [new GenericActorRuntimeActionArgument.UnitTargetArgument(
                new GenericActorRuntimeActionArgument.UnitTarget(1, 0))],
            lives);
        Assert.Equal(
            ArcRelaySignatureState.SignaturePhase.Tell,
            Assert.Single(runtime.Project(30)).Phase);
        Assert.Equal(0, runtime.TargetPaintBonus(
            sunder,
            target,
            30,
            out _));

        runtime.Advance(31, lives);
        ArcRelaySignatureRuntime.TickResult acquired =
            runtime.ResolvePostMovement(31, lives, (_, _) => true);
        ArcRelaySignatureState paint = Assert.Single(runtime.Project(31));
        Assert.Equal(ArcRelaySignatureState.SignaturePhase.Active, paint.Phase);
        Assert.Equal(3, paint.RemainingCapacity);
        Assert.Equal(39, paint.EndsAtTick);
        Assert.Equal(1, runtime.TargetPaintBonus(
            sunder,
            target,
            31,
            out _));
        Assert.Contains(acquired.Events, value =>
            Changed(value)?.Reason == "acquired");

        (runtime, sunder, origin) = Runtime(ArcRelayLaunchClassIds.Sunder);
        lives =
        [
            Life(sunder, origin, 4),
            Life(target, targetPosition, 5),
        ];
        runtime.Start(
            40,
            sunder,
            origin,
            "target-paint",
            [new GenericActorRuntimeActionArgument.UnitTargetArgument(
                new GenericActorRuntimeActionArgument.UnitTarget(1, 0))],
            lives);
        var cancelled = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        runtime.AbandonWindupsOnMove(41, sunder, cancelled);
        Assert.Empty(runtime.Project(41));
        Assert.False(runtime.CanStart(
            sunder, "target-paint", 48, origin));
        Assert.True(runtime.CanStart(
            sunder, "target-paint", 49, origin));
        Assert.Contains(cancelled, value =>
            Changed(value)?.Reason == "abandoned-move");
    }

    [Fact]
    public void TargetPaintCancelsWhenTrackingBreaks()
    {
        foreach ((string expectedReason, Position targetPosition,
                     bool teamCanSee) in new[]
                 {
                     ("target-out-of-range", new Position(18, 13), true),
                     ("target-out-of-sight", new Position(14, 13), false),
                 })
        {
            (ArcRelaySignatureRuntime runtime, ActorIdentity sunder,
                Position origin) = Runtime(ArcRelayLaunchClassIds.Sunder);
            ActorIdentity target = ActorIdentity.FromTeamUnitLife(1, 0, 0);
            runtime.Start(
                50,
                sunder,
                origin,
                "target-paint",
                [new GenericActorRuntimeActionArgument.UnitTargetArgument(
                    new GenericActorRuntimeActionArgument.UnitTarget(1, 0))],
                [Life(sunder, origin, 4), Life(target, origin.Offset(1, 0), 5)]);
            runtime.Advance(
                51,
                [Life(sunder, origin, 4), Life(target, targetPosition, 5)]);
            ArcRelaySignatureRuntime.TickResult result =
                runtime.ResolvePostMovement(
                    51,
                    [Life(sunder, origin, 4), Life(target, targetPosition, 5)],
                    (_, _) => teamCanSee);

            Assert.Empty(runtime.Project(51));
            Assert.Contains(result.Events, value =>
                Changed(value)?.Reason == expectedReason);
            Assert.False(runtime.CanStart(
                sunder, "target-paint", 65, origin));
            Assert.True(runtime.CanStart(
                sunder, "target-paint", 66, origin));
        }
    }

    [Fact]
    public void SmokeHasPublicFlightThenItsFullLandingDuration()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity veil, Position origin) =
            Runtime(ArcRelayLaunchClassIds.Veil);
        Position landing = origin.Offset(6, 0);
        ArcRelaySignatureRuntime.Life[] lives = [Life(veil, origin, 3)];
        runtime.Start(
            60,
            veil,
            origin,
            "smoke-canister",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(
                landing)],
            lives);

        ArcRelaySignatureState flight = Assert.Single(runtime.Project(60));
        Assert.Equal(
            ArcRelaySignatureState.SignaturePhase.InFlight,
            flight.Phase);
        Assert.Equal(62, flight.CompletesAtTick);
        Assert.True(flight.Positions.SequenceEqual([origin, landing]));
        Assert.False(runtime.CanStart(
            veil, "smoke-canister", 77, origin));
        Assert.True(runtime.CanStart(
            veil, "smoke-canister", 78, origin));

        runtime.Advance(61, []); // the thrown canister survives its owner
        Assert.Equal(
            ArcRelaySignatureState.SignaturePhase.InFlight,
            Assert.Single(runtime.Project(61)).Phase);
        ArcRelaySignatureRuntime.TickResult landed = runtime.Advance(62, []);
        ArcRelaySignatureState smoke = Assert.Single(runtime.Project(62));
        Assert.Equal(ArcRelaySignatureState.SignaturePhase.Active, smoke.Phase);
        Assert.Equal(72, smoke.EndsAtTick);
        Assert.Contains(landing, smoke.Positions);
        Assert.Contains(landed.Events, value =>
            Changed(value)?.Reason == "landed");
    }

    [Fact]
    public void Grammar3SignaturePhasesAndFactsRoundTripThroughReplayV3()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Sunder,
            ArcRelayLaunchClassIds.Repulsor,
            ArcRelayLaunchClassIds.Veil,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.Tapline2);
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
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_812UL);
        for (int tick = 0; tick < 4; tick++)
            session.Step();
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        string json = ReplayV3Serializer.ToJson(replay);
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(json);

        Assert.Equal(
            ReplayV3Serializer.ComputeHash(replay),
            ReplayV3Serializer.ComputeHash(reread));
        ReplayV3.ArcRelayFact.SignatureChanged[] facts = [.. reread.Ticks
            .SelectMany(value => value.TickStart.Events.Concat(value.Events))
            .Select(value => value.Payload)
            .OfType<ReplayV3.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .OfType<ReplayV3.ArcRelayFact.SignatureChanged>()];
        Assert.Contains(facts, value => value.SignatureId == "null-field"
            && value.Phase == "tell");
        Assert.Contains(facts, value => value.SignatureId == "null-field"
            && value.Reason == "activated");
        Assert.Contains(facts, value => value.SignatureId == "smoke-canister"
            && value.Phase == "in-flight");
        Assert.Contains(facts, value => value.SignatureId == "smoke-canister"
            && value.Reason == "landed");
    }

    [Fact]
    public void NewNullFieldEndsChannelsAndSuppressesConstructsOnActivation()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.Tapline2);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity hush = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        ActorIdentity patchbay = ActorIdentity.FromTeamUnitLife(1, 1, 0);
        ActorIdentity ally = ActorIdentity.FromTeamUnitLife(1, 3, 0);
        ActorIdentity minesmith = ActorIdentity.FromTeamUnitLife(1, 2, 0);
        Position origin = new(10, 13);
        Position repairTarget = origin.Offset(1, 0);
        Position nodeTile = origin.Offset(0, 1);
        ArcRelaySignatureRuntime.Life[] lives =
        [
            Life(hush, origin, 4),
            new ArcRelaySignatureRuntime.Life(
                patchbay, origin.Offset(-1, 0), 4, 4),
            new ArcRelaySignatureRuntime.Life(ally, repairTarget, 3, 4),
            Life(minesmith, origin.Offset(0, 2), 4),
        ];
        runtime.Start(
            70,
            patchbay,
            origin.Offset(-1, 0),
            "repair-beam",
            [new GenericActorRuntimeActionArgument.UnitTargetArgument(
                new GenericActorRuntimeActionArgument.UnitTarget(1, 3))],
            lives);
        runtime.Start(
            70,
            minesmith,
            origin.Offset(0, 2),
            "trip-node",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(
                nodeTile)],
            lives);
        runtime.Start(70, hush, origin, "null-field", [], lives);

        runtime.Advance(71, lives);
        ArcRelaySignatureRuntime.TickResult result =
            runtime.ResolvePostMovement(71, lives, (_, _) => true);
        ArcRelaySignatureState[] states = runtime.Project(71).ToArray();

        Assert.DoesNotContain(states, value =>
            value.SignatureId == "repair-beam");
        Assert.True(states.Single(value =>
            value.SignatureId == "trip-node").Suppressed);
        Assert.Contains(result.Events, value =>
            Changed(value)?.Reason == "ended-null-field");
        Assert.DoesNotContain(result.Events, value =>
            Changed(value)?.Reason == "triggered");
    }

    [Fact]
    public void NewNullFieldBlocksAStayedSignatureButAnEscapedBodyIsSafe()
    {
        string[] teamZero =
        [
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
        ];
        string[] teamOne =
        [
            ArcRelayLaunchClassIds.Repulsor,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            teamZero,
            teamOne,
            loopProfile: ArcRelayLoopProfile.Tapline2);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity hush = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        ActorIdentity responder = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        Position origin = new(10, 13);
        Position stayed = origin.Offset(3, 0);
        Position escaped = origin.Offset(4, 0);
        runtime.Start(
            80,
            hush,
            origin,
            "null-field",
            [],
            [Life(hush, origin, 4), Life(responder, stayed, 5)]);
        runtime.Advance(
            81,
            [Life(hush, origin, 4), Life(responder, escaped, 5)]);
        runtime.ResolvePostMovement(
            81,
            [Life(hush, origin, 4), Life(responder, escaped, 5)],
            (_, _) => true);

        Assert.False(runtime.CanStart(
            responder,
            "kinetic-burst",
            81,
            stayed));
        Assert.True(runtime.CanStart(
            responder,
            "kinetic-burst",
            81,
            escaped));
    }

    private static (ArcRelaySignatureRuntime Runtime, ActorIdentity Actor,
        Position Origin) Runtime(string classId)
    {
        string[] composition =
        [
            classId,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.Tapline2);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        return (
            new ArcRelaySignatureRuntime(definition, mode),
            ActorIdentity.FromTeamUnitLife(0, 0, 0),
            new Position(10, 13));
    }

    private static ArcRelaySignatureRuntime.Life Life(
        ActorIdentity actor,
        Position position,
        int health) => new(actor, position, health, health);

    private static ArcRelayEvent.SignatureChanged? Changed(
        GenericActorModeEvent modeEvent) =>
        modeEvent.Payload is GenericActorRuntimeObservation.EventPayload
            .ArcRelay { Fact: ArcRelayEvent.SignatureChanged changed }
            ? changed
            : null;

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
                        .ProjectileHeadingArgument(constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .UnitTargetConstraint constraint =>
                    new GenericActorRuntimeActionArgument.UnitTargetArgument(
                        constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .PositionTargetConstraint constraint =>
                    new GenericActorRuntimeActionArgument.PositionTargetArgument(
                        constraint.AllowedValues[0]),
                _ => throw new InvalidOperationException(
                    "Unexpected Arc signature argument constraint."),
            }),
        ];
}
