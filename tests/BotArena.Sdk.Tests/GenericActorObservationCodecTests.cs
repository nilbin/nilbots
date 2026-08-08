using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

public sealed class GenericActorObservationCodecTests
{
    [Fact]
    public void GenericObservationAndDecisionUseSharedProtocolFraming()
    {
        GenericActorContext context =
            GenericActorDynamicTestFixture.Context(
                includeAllEvents: false);
        GenericActorDecision decision =
            GenericActorDynamicTestFixture.FullDecision();

        byte[] observationFrame =
            ActorWireProtocol.EncodeGenericObservation(context);
        byte[] decisionFrame =
            ActorWireProtocol.EncodeGenericDecision(decision);

        Assert.True(ActorWireProtocol.HasMagic(observationFrame));
        Assert.True(ActorWireProtocol.HasMagic(decisionFrame));
        Assert.Equal(
            context.Tick,
            ActorWireProtocol.DecodeGenericObservation(
                observationFrame).Tick);
        Assert.Equal(
            decision.ActionId,
            ActorWireProtocol.DecodeGenericDecision(
                decisionFrame).ActionId);
    }

    [Fact]
    public void FullObservationRoundTripPreservesDynamicSchema()
    {
        GenericActorContext source =
            GenericActorDynamicTestFixture.Context();

        byte[] encoded = GenericActorWireObservationCodec.Encode(source);
        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(encoded);

        Assert.Equal(GenericActorContext.CurrentSchemaVersion, decoded.SchemaVersion);
        Assert.Equal(9, decoded.Tick);
        Assert.Equal(new string('a', 64), decoded.MatchContractFingerprint);
        Assert.Equal(GenericActorDynamicTestFixture.SelfActor, decoded.Self.ActorId);
        Assert.Equal(2, decoded.Self.Generation);
        Assert.Equal("striker", decoded.Self.ClassId);
        Assert.Equal("anchor:0:0:4:9",
            decoded.Self.PendingSameLifeTransition?.OperationId);
        Assert.Equal(
            GenericActorActionResolution.ActionOutcome.Blocked,
            decoded.Self.PreviousActionResolution?.Outcome);

        Assert.Equal(7, decoded.TeamUnits.Length);
        Assert.Collection(
            decoded.TeamUnits.Select(slot => slot.State),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.Active>(state),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.AvailabilityPending>(state),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.AutomaticReturnPending>(state),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.Ready>(state),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.FabricationPending>(state),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.ReplicationPending>(state),
            state => Assert.IsType<
                GenericActorContext.UnitSlotState.PermanentlyDormant>(state));
        Assert.Equal(
            "fabricate:0:0:4:9",
            Assert.IsType<
                GenericActorContext.UnitSlotState.FabricationPending>(
                    decoded.TeamUnits[4].State).OperationId);

        Assert.Equal([10, 20, 30, 40],
            decoded.Participants.Select(value => value.ParticipantId));
        Assert.Equal(
            ["striker", "bulwark", "fabricator", "striker"],
            decoded.Participants.Select(value => value.ClassId));
        Assert.Equal(long.MaxValue,
            decoded.Participants[0].RuntimeFaultCount);
        Assert.Equal(
            "striker",
            Assert.Single(decoded.Allies).ClassId);
        Assert.Equal(
            ["bulwark", "fabricator"],
            decoded.Enemies.Select(value => value.ClassId));
        GenericActorContext.SpawnReservation reservation =
            Assert.Single(
                decoded.VisibleTiles,
                tile => tile.SpawnReservation is not null)
                .SpawnReservation!;
        Assert.Equal(
            GenericActorContext.SpawnReservationKind.Fabrication,
            reservation.Kind);
        Assert.Equal(12, reservation.DueTick);
        Assert.Equal(4, decoded.Scoreboard.Teams.Length);
        Assert.Equal(
            long.MinValue,
            decoded.Scoreboard.Teams[3]
                .Scores.Single(score => score.Channel == "deaths")
                .Value);

        Assert.NotNull(decoded.VisibleProjectiles);
        GenericActorContext.ObservedProjectile projectile =
            Assert.Single(decoded.VisibleProjectiles.Value);
        Assert.Equal(long.MaxValue, projectile.ProjectileId);
        Assert.Null(projectile.OwnerActorId);
        Assert.Equal(0, projectile.RemainingTiles);
        // The wave-2 "should I eat this?" pair rides the wire as trailing
        // tags and must survive the round trip exactly (DECISIONS #169).
        Assert.Equal(3, projectile.TicksPerAdvance);
        Assert.Equal(2, projectile.DamagePerHit);

        var mode = Assert.IsType<
            GenericActorContext.ModeObservationState.Frontline>(decoded.Mode);
        Assert.Equal(2, mode.ActivePositionIndex);
        Assert.Equal(0, mode.ClaimingTeamId);
        Assert.Equal(1, mode.HoldOwnerTeamId);
        Assert.Equal(47, mode.HoldEndsAtTick);
        Assert.Equal(
            [0, 99],
            decoded.ActionLegalities.Select(value => value.ActionCode));
    }

    [Fact]
    public void ClassIdentityAndSpawnClaimsSurviveTheProfile2RoundTrip()
    {
        // Class identity and the tile's spawn claim ride the same trailing
        // tagged slots every prior observation growth used, so observation
        // schema 2 carries them and an older guest simply ignores the tags.
        GenericActorContext source =
            GenericActorDynamicTestFixture.Context();

        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(source));

        Assert.Equal(
            GenericActorContractVersions.ObservationSchemaVersion,
            decoded.SchemaVersion);
        Assert.Equal("striker", decoded.Self.ClassId);
        Assert.Equal(
            source.Participants.Select(value => value.ClassId),
            decoded.Participants.Select(value => value.ClassId));
        Assert.Equal(
            source.Allies.Select(value => value.ClassId),
            decoded.Allies.Select(value => value.ClassId));
        Assert.Equal(
            source.Enemies.Select(value => value.ClassId),
            decoded.Enemies.Select(value => value.ClassId));
        GenericActorContext.SpawnReservation reservation = Assert.Single(
            decoded.VisibleTiles,
            tile => tile.SpawnReservation is not null).SpawnReservation!;
        Assert.Equal(0, reservation.TeamId);
        Assert.Equal(4, reservation.UnitId);
        Assert.Equal(
            GenericActorContext.SpawnReservationKind.Fabrication,
            reservation.Kind);
        Assert.Equal(12, reservation.DueTick);
    }

    [Fact]
    public void EventRoundTripPreservesEveryTypedVariantAndChronology()
    {
        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(
                    GenericActorDynamicTestFixture.Context()));

        Assert.Equal(19, decoded.VisibleEvents.Length);
        Assert.Equal(
            Enumerable.Range(0, 19),
            decoded.VisibleEvents.Select(value => value.SourceOrdinal));
        Assert.Collection(
            decoded.VisibleEvents.Select(value => value.Payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Rotation>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Movement>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.MovementBlocked>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Attack>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Damage>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Destruction>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.LifeSpawned>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.LifeRetired>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.RuntimeFault>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Participant>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Lifecycle>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Lifecycle>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.Lifecycle>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.FormTransition>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.FormTransition>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.FormTransition>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.ScoreChanged>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.ModeChanged>(payload),
            payload => Assert.IsType<
                GenericActorContext.EventPayload.LifecycleClockCancelled>(
                    payload));

        var attack = Assert.IsType<
            GenericActorContext.EventPayload.Attack>(
                decoded.VisibleEvents[3].Payload);
        Assert.Equal(long.MaxValue, attack.ProjectileId);
        Assert.Equal(2, attack.Action.Arguments.Length);

        var damage = Assert.IsType<
            GenericActorContext.EventPayload.Damage>(
                decoded.VisibleEvents[4].Payload);
        Assert.Equal(1, damage.SourceTeamId);
        Assert.Null(damage.SourceActorId);
        Assert.Equal(GenericActorDynamicTestFixture.SelfActor,
            damage.TargetActorId);

        var spawned = Assert.IsType<
            GenericActorContext.EventPayload.LifeSpawned>(
                decoded.VisibleEvents[6].Payload);
        Assert.Equal(GenericActorMatchStart.SpawnReason.Initial,
            spawned.Reason);
        Assert.Null(spawned.SourceOperationId);

        var cancelled = Assert.IsType<
            GenericActorContext.EventPayload.Lifecycle>(
                decoded.VisibleEvents[11].Payload);
        Assert.Equal("source-destroyed", cancelled.CancellationReason);

        var score = Assert.IsType<
            GenericActorContext.EventPayload.ScoreChanged>(
                decoded.VisibleEvents[16].Payload);
        Assert.Equal(long.MaxValue, score.NewValue);
        var clock = Assert.IsType<
            GenericActorContext.EventPayload.LifecycleClockCancelled>(
                decoded.VisibleEvents[18].Payload);
        Assert.Equal(5, clock.TargetUnitId);
        Assert.IsType<
            GenericActorContext.UnitSlotState.AutomaticReturnPending>(
                clock.CancelledState);
        Assert.Equal(
            "participant-disqualified",
            clock.CancellationReason);
        Assert.Equal(
            decoded.VisibleEvents[3].SourceOrdinal,
            Assert.Single(decoded.HeardSounds!.Value).SourceOrdinal);
    }

    [Fact]
    public void NullAndEmptySensorCapabilitiesRemainDistinct()
    {
        GenericActorContext absent =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(
                    GenericActorDynamicTestFixture.Context(
                        nullCapabilities: true,
                        includeAllEvents: false)));
        GenericActorContext empty =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(
                    GenericActorDynamicTestFixture.Context(
                        emptyCapabilities: true,
                        includeAllEvents: false)));

        Assert.Null(absent.VisibleProjectiles);
        Assert.Null(absent.HeardSounds);
        Assert.NotNull(empty.VisibleProjectiles);
        Assert.Empty(empty.VisibleProjectiles.Value);
        Assert.NotNull(empty.HeardSounds);
        Assert.Empty(empty.HeardSounds.Value);
    }

    [Fact]
    public void DeathmatchModeAndFourPlayerShapeRoundTrip()
    {
        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                GenericActorWireObservationCodec.Encode(
                    GenericActorDynamicTestFixture.Context(
                        includeAllEvents: false,
                        frontline: false)));

        Assert.IsType<
            GenericActorContext.ModeObservationState.Deathmatch>(
                decoded.Mode);
        Assert.Equal(4, decoded.Participants.Length);
        Assert.Equal(4, decoded.Scoreboard.Teams.Length);
    }

    [Fact]
    public void FabricationSpawnKeepsLineageWhileParentIsRedacted()
    {
        var source = new GenericActorContext.ObservedEvent(
            "fabricated-life",
            sourceTick: 8,
            sourceOrdinal: 0,
            GenericActorContext.EventKind.LifeSpawned,
            new GenericActorContext.EventPayload.LifeSpawned(
                new ActorIdentity(0, 4, 1),
                participantId: 10,
                parentActorId: null,
                generation: 1,
                "turret",
                health: 8,
                new Position(4, 4),
                GenericActorMatchStart.SpawnReason.Fabrication,
                sourceTransitionId: "fabricate",
                sourceOperationId: "fabricate:opaque:9"),
            [GenericActorDynamicTestFixture.SelfActor]);

        GenericActorContext.ObservedEvent decoded =
            GenericActorWireEventCodec.DecodeEvent(
                GenericActorWireEventCodec.EncodeEvent(source),
                0);
        var spawned = Assert.IsType<
            GenericActorContext.EventPayload.LifeSpawned>(
                decoded.Payload);

        Assert.Null(spawned.ParentActorId);
        Assert.Equal("fabricate", spawned.SourceTransitionId);
        Assert.Equal("fabricate:opaque:9", spawned.SourceOperationId);
    }

    [Theory]
    [InlineData(GenericActorMatchStart.SpawnReason.Fabrication, "fabricate")]
    [InlineData(GenericActorMatchStart.SpawnReason.Replication, "split")]
    public void TransitionSpawnAcceptsFullyRedactedPrivateLineage(
        GenericActorMatchStart.SpawnReason reason,
        string transitionId)
    {
        var source = new GenericActorContext.ObservedEvent(
            "redacted-transition-life",
            sourceTick: 8,
            sourceOrdinal: 0,
            GenericActorContext.EventKind.LifeSpawned,
            new GenericActorContext.EventPayload.LifeSpawned(
                new ActorIdentity(0, 4, 1),
                participantId: 10,
                parentActorId: null,
                generation: 1,
                "mobile",
                health: 1,
                new Position(4, 4),
                reason,
                sourceTransitionId: transitionId,
                sourceOperationId: null),
            [GenericActorDynamicTestFixture.SelfActor]);

        GenericActorContext.ObservedEvent decoded =
            GenericActorWireEventCodec.DecodeEvent(
                GenericActorWireEventCodec.EncodeEvent(source),
                0);
        var spawned = Assert.IsType<
            GenericActorContext.EventPayload.LifeSpawned>(
                decoded.Payload);

        Assert.Null(spawned.ParentActorId);
        Assert.Equal(transitionId, spawned.SourceTransitionId);
        Assert.Null(spawned.SourceOperationId);
    }

    [Fact]
    public void UnknownTaggedObservationFieldIsIgnored()
    {
        byte[] encoded = GenericActorWireObservationCodec.Encode(
            GenericActorDynamicTestFixture.Context(
                includeAllEvents: false));
        var extension = new ActorWireObjectWriter();
        extension.Field(99, [1, 2, 3]);

        GenericActorContext decoded =
            GenericActorWireObservationCodec.Decode(
                [.. encoded, .. extension.ToArray()]);

        Assert.Equal(9, decoded.Tick);
    }

    [Fact]
    public void MalformedEventUnionAndInvalidLineageFailClosed()
    {
        GenericActorContext.ObservedEvent rotation =
            GenericActorDynamicTestFixture.Events()[0];
        byte[] encoded = GenericActorWireEventCodec.EncodeEvent(rotation);
        var source = new ActorWireObjectReader(encoded, 0);
        var malformed = new ActorWireObjectWriter();
        malformed.Field(1, source.Required(1));
        malformed.Field(2, source.Required(2));
        malformed.Field(3, source.Required(3));
        malformed.Field(
            4,
            ActorWireValue.Enum(
                GenericActorContext.EventKind.Movement));
        malformed.Field(5, source.Required(5));
        malformed.Field(6, source.Required(6));

        Assert.Throws<FormatException>(
            () => GenericActorWireEventCodec.DecodeEvent(
                malformed.ToArray(),
                0));
        Assert.Throws<ArgumentException>(
            () => new GenericActorContext.ObservedEvent(
                "cancelled",
                sourceTick: 1,
                sourceOrdinal: 0,
                GenericActorContext.EventKind.LifecycleCancelled,
                new GenericActorContext.EventPayload.Lifecycle(
                    "fabricate",
                    "fabricate:0",
                    GenericActorDynamicTestFixture.SelfActor,
                    targetTeamId: 0,
                    targetUnitId: 1,
                    dueTick: null,
                    cancellationReason: null),
                [GenericActorDynamicTestFixture.SelfActor]));
        Assert.Throws<ArgumentException>(
            () => new GenericActorContext.EventPayload.LifeSpawned(
                new ActorIdentity(0, 1, 1),
                participantId: 10,
                parentActorId: new ActorIdentity(0, 0, 0),
                generation: 1,
                "mobile",
                health: 4,
                new Position(1, 1),
                GenericActorMatchStart.SpawnReason.Replication,
                sourceTransitionId: "split",
                sourceOperationId: null));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GenericActorContext.ObservedProjectile(
                1,
                0,
                ownerActorId: null,
                new Position(1, 1),
                ProjectileHeading.North,
                tilesPerAdvance: 1,
                ticksUntilAdvance: 1,
                remainingTiles: -1,
                [],
                ticksPerAdvance: 1,
                damagePerHit: 1));
    }

    [Fact]
    public void DuplicateStableSelectorsAndEventOrdinalsAreRejected()
    {
        GenericActorActionLegality first =
            GenericActorDynamicTestFixture.FullLegality();
        GenericActorActionLegality duplicateCode =
            new("other-action", first.ActionCode, true, true, []);

        Assert.Throws<ArgumentException>(
            () => CreateMinimalContext(
                [
                    first,
                    duplicateCode,
                ],
                []));

        GenericActorContext.ObservedEvent source =
            GenericActorDynamicTestFixture.Events()[0];
        GenericActorContext.ObservedEvent duplicateOrdinal =
            new(
                "other-event",
                source.SourceTick,
                source.SourceOrdinal,
                source.Kind,
                source.Payload,
                source.ObservedBy);
        Assert.Throws<ArgumentException>(
            () => CreateMinimalContext(
                [first],
                [source, duplicateOrdinal]));
    }

    [Fact]
    public void FormTransitionEventsAllowEndOfStartedTickCompletion()
    {
        GenericActorContext.EventPayload.FormTransition sameTick = new(
            GenericActorDynamicTestFixture.SelfActor,
            "anchor",
            "anchor:0:0:4:9",
            "mobile",
            "turret",
            startedTick: 9,
            dueTick: 9);

        Assert.Equal(9, sameTick.DueTick);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorContext.EventPayload.FormTransition(
                GenericActorDynamicTestFixture.SelfActor,
                "anchor",
                "anchor:0:0:4:8",
                "mobile",
                "turret",
                startedTick: 9,
                dueTick: 8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorContext.PendingSameLifeTransition(
                "anchor",
                "anchor:0:0:4:9",
                "turret",
                startedTick: 9,
                dueTick: 9));
    }

    [Fact]
    public void WireRejectsProjectileOwnerFromContradictoryTeam()
    {
        byte[] encoded = GenericActorWireObservationCodec.Encode(
            GenericActorDynamicTestFixture.Context(
                includeAllEvents: false));
        var observation = new ActorWireObjectReader(encoded, 0);
        byte[][] projectileItems = ActorWireValue.Array(
            observation.Required(10),
            item => item).ToArray();
        var projectile = new ActorWireObjectReader(
            Assert.Single(projectileItems),
            1);
        var malformedProjectile = new ActorWireObjectWriter();
        malformedProjectile.Field(1, projectile.Required(1));
        malformedProjectile.Field(2, projectile.Required(2));
        malformedProjectile.Field(
            3,
            GenericActorWireCodecValues.EncodeIdentity(
                new ActorIdentity(
                    teamId: 0,
                    unitId: 9,
                    lifeId: 1)));
        for (ushort fieldId = 4; fieldId <= 11; fieldId++)
            malformedProjectile.Field(
                fieldId,
                projectile.Required(fieldId));

        byte[] malformedProjectiles = ActorWireValue.Array(
            new[] { malformedProjectile.ToArray() },
            item => item);
        var malformedObservation = new ActorWireObjectWriter();
        for (ushort fieldId = 1; fieldId <= 15; fieldId++)
        {
            byte[]? field = fieldId == 10
                ? malformedProjectiles
                : observation.Optional(fieldId);
            if (field is not null)
                malformedObservation.Field(fieldId, field);
        }

        Assert.Throws<FormatException>(
            () => GenericActorWireObservationCodec.Decode(
                malformedObservation.ToArray()));
    }

    [Fact]
    public void WireRejectsEitherHalfOfAFrontlineHoldAlone()
    {
        byte[] encoded = GenericActorWireObservationCodec.EncodeMode(
            Assert.IsType<
                GenericActorContext.ModeObservationState.Frontline>(
                    GenericActorDynamicTestFixture.Context(
                        includeAllEvents: false).Mode));
        var mode = new ActorWireObjectReader(encoded, 0);
        var payload = new ActorWireObjectReader(mode.Required(3), 1);

        byte[] PartialPayload()
        {
            var writer = new ActorWireObjectWriter();
            for (ushort fieldId = 1; fieldId <= 6; fieldId++)
                writer.Field(fieldId, payload.Required(fieldId));
            return writer.ToArray();
        }

        // The mirror of PartialPayload: the expiry clock without the owner.
        // The pair is the encoding, so either half alone is malformed.
        byte[] ExpiryOnlyPayload()
        {
            var writer = new ActorWireObjectWriter();
            for (ushort fieldId = 1; fieldId <= 5; fieldId++)
                writer.Field(fieldId, payload.Required(fieldId));
            writer.Field(7, payload.Required(7));
            return writer.ToArray();
        }

        byte[] RewritePayload(byte[] replacement)
        {
            var writer = new ActorWireObjectWriter();
            writer.Field(1, mode.Required(1));
            writer.Field(2, mode.Required(2));
            writer.Field(3, replacement);
            return writer.ToArray();
        }

        Assert.Throws<FormatException>(() =>
            GenericActorWireObservationCodec.DecodeMode(
                RewritePayload(PartialPayload()),
                0));
        Assert.Throws<FormatException>(() =>
            GenericActorWireObservationCodec.DecodeMode(
                RewritePayload(ExpiryOnlyPayload()),
                0));
    }

    [Fact]
    public void AudienceAndActiveBodyInvariantsRejectContradictions()
    {
        GenericActorActionLegality legality =
            GenericActorDynamicTestFixture.FullLegality();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GenericActorContext.ObservedSelfState(
                GenericActorDynamicTestFixture.SelfActor,
                generation: 0,
                "mobile",
                new Position(1, 1),
                Direction.North,
                health: 0,
                cooldown: 0,
                energy: null,
                previousActionResolution: null,
                pendingSameLifeTransition: null));

        Assert.Throws<ArgumentException>(
            () => CreateMinimalContext(
                [legality],
                [],
                teamUnits:
                [
                    new GenericActorContext.ObservedUnitSlot(
                        1,
                        0,
                        new GenericActorContext.UnitSlotState.Ready()),
                ]));
        Assert.Throws<ArgumentException>(
            () => CreateMinimalContext(
                [legality],
                [],
                teamUnits:
                [
                    new GenericActorContext.ObservedUnitSlot(
                        0,
                        3,
                        new GenericActorContext.UnitSlotState.Active(
                            new ActorIdentity(0, 4, 1),
                            generation: 1,
                            "mobile")),
                ]));
        Assert.Throws<ArgumentException>(
            () => CreateMinimalContext(
                [legality],
                [],
                allies:
                [
                    new GenericActorContext.ObservedAllyState(
                        new ActorIdentity(1, 0, 1),
                        generation: 1,
                        "mobile",
                        new Position(2, 2),
                        Direction.North,
                        health: 1,
                        cooldown: 0,
                        energy: null,
                        previousActionResolution: null,
                        pendingSameLifeTransition: null),
                ]));
        Assert.Throws<ArgumentException>(
            () => CreateMinimalContext(
                [legality],
                [],
                enemies:
                [
                    new GenericActorContext.ObservedEnemyState(
                        new ActorIdentity(0, 2, 1),
                        "mobile",
                        new Position(2, 2),
                        Direction.North,
                        health: 1,
                        pendingSameLifeTransition: null,
                        [GenericActorDynamicTestFixture.SelfActor]),
                ]));
    }

    private static GenericActorContext CreateMinimalContext(
        IEnumerable<GenericActorActionLegality> legalities,
        IEnumerable<GenericActorContext.ObservedEvent> events,
        IEnumerable<GenericActorContext.ObservedUnitSlot>? teamUnits = null,
        IEnumerable<GenericActorContext.ObservedAllyState>? allies = null,
        IEnumerable<GenericActorContext.ObservedEnemyState>? enemies = null) =>
        new(
            GenericActorContext.CurrentSchemaVersion,
            tick: 9,
            new string('b', 64),
            new GenericActorContext.ObservedSelfState(
                GenericActorDynamicTestFixture.SelfActor,
                0,
                "mobile",
                new Position(1, 1),
                Direction.North,
                1,
                0,
                null,
                null,
                null),
            teamUnits ?? [],
            [],
            allies ?? [],
            enemies ?? [],
            [],
            visibleProjectiles: null,
            events,
            heardSounds: null,
            new GenericActorContext.ScoreboardState(
                [
                    new GenericActorContext.TeamScoreState(
                        0,
                        true,
                        [
                            new GenericActorContext.ScoreValue(
                                "kills",
                                0),
                        ]),
                ]),
            new GenericActorContext.ModeObservationState.Deathmatch(
                "deathmatch"),
            legalities);
}
