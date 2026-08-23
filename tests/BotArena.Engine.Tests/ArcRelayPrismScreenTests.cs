using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Acceptance proof for Palisade's owner-attached Prism Screen candidate.
/// Historical Prism Wall remains frozen in Tapline-11 beside it.
/// </summary>
public sealed class ArcRelayPrismScreenTests
{
    private static readonly string[] Composition =
    [
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Hush,
        ArcRelayLaunchClassIds.Longshot,
        ArcRelayLaunchClassIds.Kestrel,
        ArcRelayLaunchClassIds.Patchbay,
        ArcRelayLaunchClassIds.Relay,
        ArcRelayLaunchClassIds.Mason,
        ArcRelayLaunchClassIds.Mortar,
    ];

    [Fact]
    public void Tapline12VersionsOnlyThePalisadeEnvelope()
    {
        Assert.Same(ArcRelayLoopProfile.Broadloom7, ArcRelayLoopProfile.Current);

        ActorResolvedMatchDefinition prior = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.Tapline11);
        ActorResolvedMatchDefinition candidate = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.Tapline12);
        var priorMode = Assert.IsType<ArcRelayGameModeDefinition>(
            prior.Rules.GameMode);
        var candidateMode = Assert.IsType<ArcRelayGameModeDefinition>(
            candidate.Rules.GameMode);

        ArcRelaySignatureDefinition.PrismWall wall = Assert.Single(
            priorMode.Signatures.OfType<
                ArcRelaySignatureDefinition.PrismWall>());
        ArcRelaySignatureDefinition.PrismScreen screen = Assert.Single(
            candidateMode.Signatures.OfType<
                ArcRelaySignatureDefinition.PrismScreen>());

        Assert.Equal("prism-wall", screen.SignatureId);
        Assert.Equal(wall.ActionId, screen.ActionId);
        Assert.Equal(3, screen.SegmentCount);
        Assert.Equal(30, screen.Hull);
        Assert.Equal(6, screen.DurationTicks);
        Assert.Equal(18, screen.CooldownTicks);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(prior.Map),
            ActorContractFingerprint.ComputeMap(candidate.Map));

        ArcRelaySignatureDefinition[] unchangedPrior =
            [.. priorMode.Signatures.Where(value =>
                value.SignatureId != "prism-wall")];
        ArcRelaySignatureDefinition[] unchangedCandidate =
            [.. candidateMode.Signatures.Where(value =>
                value.SignatureId != "prism-wall")];
        Assert.Equal(unchangedPrior, unchangedCandidate);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(candidate));
        var projected = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                contract.Rules.GameMode).Signatures.Single(value =>
                    value.SignatureId == "prism-wall");
        Assert.Equal(30, projected.Hull);
        Assert.Null(projected.ContactCapacity);
        Assert.Equal(6, projected.DurationTicks);
    }

    [Fact]
    public void ScreenFollowsPositionAndFacingWithoutBlockingBodies()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity palisade) = Runtime();
        Position origin = new(14, 13);
        ArcRelaySignatureRuntime.Life[] lives =
        [
            Life(palisade, origin, Direction.East),
        ];

        runtime.Start(
            10,
            palisade,
            origin,
            "prism-wall",
            [new GenericActorRuntimeActionArgument.DirectionArgument(
                Direction.East)],
            lives);

        ArcRelaySignatureState started = Assert.Single(runtime.Project(10));
        Assert.Equal(
            ArcRelaySignatureDefinition.SignatureKind.PrismScreen,
            started.Kind);
        Assert.Equal(30, started.RemainingCapacity);
        Assert.Equal(30, started.MaximumCapacity);
        Assert.Equal(16, started.EndsAtTick);
        Assert.True(started.Positions.SequenceEqual(
        [
            new Position(15, 12),
            new Position(15, 13),
            new Position(15, 14),
        ]));
        Assert.All(started.Positions, position =>
            Assert.False(runtime.BlocksBody(position, 10)));

        Position moved = new(15, 13);
        ArcRelaySignatureRuntime.TickResult movedResult =
            runtime.ResolvePostMovement(
            11,
            [Life(palisade, moved, Direction.South)],
            (_, _) => true);
        ArcRelaySignatureState followed = Assert.Single(runtime.Project(11));
        Assert.True(followed.Positions.SequenceEqual(
        [
            new Position(14, 14),
            new Position(15, 14),
            new Position(16, 14),
        ]));
        ArcRelayEvent.SignatureChanged transformed = Assert.Single(
            movedResult.Events.Select(Changed).OfType<
                ArcRelayEvent.SignatureChanged>());
        Assert.Equal("owner-transformed", transformed.Reason);
        Assert.Equal(followed.OperationId, transformed.OperationId);
    }

    [Fact]
    public void OccupiedSegmentsAreInactiveAndFlankFireBypassesTheScreen()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity palisade) = Runtime();
        ActorIdentity ally = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        Position origin = new(14, 13);
        Position occupiedSegment = new(15, 13);
        runtime.Start(
            20,
            palisade,
            origin,
            "prism-wall",
            [new GenericActorRuntimeActionArgument.DirectionArgument(
                Direction.East)],
            [
                Life(palisade, origin, Direction.East),
                Life(ally, occupiedSegment, Direction.East),
            ]);

        Assert.False(runtime.TryConsumeProjectile(
            occupiedSegment, 1, 20, 10, out _));
        Assert.False(runtime.TryConsumeProjectile(
            new Position(14, 12), 1, 20, 10, out _));
        Assert.False(runtime.TryConsumeProjectile(
            new Position(15, 12), 0, 20, 10, out _));
        Assert.True(runtime.TryConsumeProjectile(
            new Position(15, 12), 1, 20, 10, out _));
        Assert.Equal(20, Assert.Single(runtime.Project(20)).RemainingCapacity);
    }

    [Fact]
    public void RailLeavesOneBasicHitAndThatHitBreaksTheScreen()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity palisade) = Runtime();
        Position origin = new(14, 13);
        runtime.Start(
            30,
            palisade,
            origin,
            "prism-wall",
            [new GenericActorRuntimeActionArgument.DirectionArgument(
                Direction.East)],
            [Life(palisade, origin, Direction.East)]);

        Assert.True(runtime.TryDamageDeployment(
            new Position(15, 13),
            attackerTeamId: 1,
            damage: 20,
            source: "rail-line",
            out GenericActorModeEvent? rail));
        Assert.Equal("damaged-rail-line", Changed(rail!)?.Reason);
        Assert.Equal(10, Assert.Single(runtime.Project(30)).RemainingCapacity);

        Assert.True(runtime.TryConsumeProjectile(
            new Position(15, 13),
            projectileTeamId: 1,
            tick: 30,
            damage: 10,
            out GenericActorModeEvent? basic));
        Assert.Equal("destroyed-projectile", Changed(basic!)?.Reason);
        Assert.Empty(runtime.Project(30));
    }

    [Fact]
    public void NullFieldOwnerDeathAndExpiryAllEndProtectionTruthfully()
    {
        (ArcRelaySignatureRuntime runtime, ActorIdentity palisade) = Runtime();
        ActorIdentity hush = ActorIdentity.FromTeamUnitLife(1, 1, 0);
        Position origin = new(14, 13);
        Position screenTile = new(15, 13);
        ArcRelaySignatureRuntime.Life[] lives =
        [
            Life(palisade, origin, Direction.East),
            Life(hush, screenTile, Direction.West),
        ];
        runtime.Start(
            40,
            palisade,
            origin,
            "prism-wall",
            [new GenericActorRuntimeActionArgument.DirectionArgument(
                Direction.East)],
            lives);
        runtime.Start(40, hush, screenTile, "null-field", [], lives);
        runtime.Advance(41, lives);
        runtime.ResolvePostMovement(41, lives, (_, _) => true);

        ArcRelaySignatureState screen = runtime.Project(41).Single(value =>
            value.OwnerActorId == palisade);
        Assert.True(screen.Suppressed);
        Assert.False(runtime.TryConsumeProjectile(
            new Position(15, 12), 1, 41, 10, out _));

        var destroyedEvents =
            ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        runtime.NotifyDestroyed(42, [palisade], destroyedEvents);
        Assert.DoesNotContain(runtime.Project(42), value =>
            value.OwnerActorId == palisade);
        Assert.Contains(destroyedEvents, value =>
            Changed(value)?.Reason == "owner-destroyed");

        (runtime, palisade) = Runtime();
        lives = [Life(palisade, origin, Direction.East)];
        runtime.Start(
            50,
            palisade,
            origin,
            "prism-wall",
            [new GenericActorRuntimeActionArgument.DirectionArgument(
                Direction.East)],
            lives);
        Assert.False(runtime.CanStart(palisade, "prism-wall", 67, origin));
        Assert.True(runtime.CanStart(palisade, "prism-wall", 68, origin));
        runtime.Advance(56, lives);
        Assert.Empty(runtime.Project(56));
    }

    private static (ArcRelaySignatureRuntime Runtime, ActorIdentity Palisade)
        Runtime()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.Tapline12);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        return (
            new ArcRelaySignatureRuntime(definition, mode),
            ActorIdentity.FromTeamUnitLife(0, 0, 0));
    }

    private static ArcRelaySignatureRuntime.Life Life(
        ActorIdentity actor,
        Position position,
        Direction facing) => new(actor, position, 50, 50, facing);

    private static ArcRelayEvent.SignatureChanged? Changed(
        GenericActorModeEvent modeEvent) =>
        modeEvent.Payload is GenericActorRuntimeObservation.EventPayload
            .ArcRelay { Fact: ArcRelayEvent.SignatureChanged changed }
            ? changed
            : null;
}
