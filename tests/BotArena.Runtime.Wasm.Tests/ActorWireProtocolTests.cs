using System.Buffers.Binary;
using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class ActorWireProtocolTests
{
    [Fact]
    public void LegacyNegotiationFrames_RemainByteExact()
    {
        Assert.Equal(
            "4e4256320101000014000000"
                + "01000400000001000000"
                + "02000400000001000000",
            Convert.ToHexStringLower(
                ActorWireProtocol.EncodeHello(1, 1)));
        Assert.Equal(
            "4e425632010200000a000000"
                + "01000400000001000000",
            Convert.ToHexStringLower(
                ActorWireProtocol.EncodeHelloAck(1)));
        Assert.Equal(
            "4e4256320104000032000000"
                + "01000400000001000000"
                + "02000400000001000000"
                + "03000400000001000000"
                + "04000400000001000000"
                + "05000400000001000000",
            Convert.ToHexStringLower(
                ActorWireProtocol.EncodeReady(1, 1, 1, 1, 1)));
    }

    [Fact]
    public void Hello_NegotiatesOneExactContractGenerationBeforeMatchStart()
    {
        byte[] hello = ActorWireProtocol.EncodeHello(
            ActorWireProtocol.MajorVersion,
            ActorWireProtocol.MajorVersion,
            ActorContractProfile.GenericV2);

        ActorWireHello offer = ActorWireProtocol.DecodeHello(hello);

        Assert.Equal(1, offer.MinimumMajor);
        Assert.Equal(1, offer.MaximumMajor);
        Assert.Equal(
            ActorContractProfile.GenericV2,
            offer.RequiredProfile);

        byte[] ack = ActorWireProtocol.EncodeHelloAck(
            ActorWireProtocol.MajorVersion,
            ActorContractProfile.GenericV2);
        ActorWireHelloAck selection =
            ActorWireProtocol.DecodeHelloAckContract(ack);

        Assert.Equal(ActorWireProtocol.MajorVersion, selection.SelectedMajor);
        Assert.Equal(
            ActorContractProfile.GenericV2,
            selection.SelectedProfile);
        Assert.Equal(
            ActorWireProtocol.MajorVersion,
            ActorWireProtocol.DecodeHelloAck(ack));
    }

    [Fact]
    public void LegacyHelloAck_HasNoContractGenerationSelection()
    {
        ActorWireHelloAck ack = ActorWireProtocol.DecodeHelloAckContract(
            ActorWireProtocol.EncodeHelloAck(
                ActorWireProtocol.MajorVersion));

        Assert.Equal(ActorWireProtocol.MajorVersion, ack.SelectedMajor);
        Assert.Null(ack.SelectedProfile);
    }

    [Fact]
    public void Ready_AttestsSelectedMatchContractSchemaWhenPresent()
    {
        ActorWireReady ready = ActorWireProtocol.DecodeReady(
            ActorWireProtocol.EncodeReady(
                ActorWireProtocol.MajorVersion,
                GenericActorContractVersions.RuntimeContractVersion,
                GenericActorContractVersions.MatchStartSchemaVersion,
                GenericActorContractVersions.ObservationSchemaVersion,
                GenericActorContractVersions.DecisionSchemaVersion,
                ActorContractProfile.GenericV2));

        Assert.Equal(
            ActorContractProfile.GenericV2,
            ready.SelectedProfile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void FrameDecoder_RejectsMalformedEnvelope(int malformedCase)
    {
        byte[] valid = ActorWireProtocol.EncodeHello(1, 1);
        byte[] malformed = malformedCase switch
        {
            0 => valid[..(ActorWireProtocol.HeaderSize - 1)],
            1 => ChangeByte(valid, 0, (byte)'X'),
            2 => ChangeByte(valid, 4, ActorWireProtocol.MajorVersion + 1),
            3 => ChangeByte(valid, 6, 1),
            4 => ChangeByte(valid, 5, byte.MaxValue),
            5 => ChangePayloadLength(valid, -1),
            6 => ChangePayloadLength(valid, valid.Length),
            7 => [.. valid, 0],
            8 => new byte[ActorWireProtocol.MaxHostFrameBytes + 1],
            _ => throw new ArgumentOutOfRangeException(
                nameof(malformedCase)),
        };

        Assert.Throws<FormatException>(
            () => ActorWireProtocol.DecodeHello(malformed));
    }

    [Fact]
    public void ObjectDecoder_RejectsDuplicateAndTruncatedFields()
    {
        byte[] valid = ActorWireProtocol.EncodeHello(1, 1);
        byte[] duplicate = AppendField(
            valid,
            1,
            ActorWireValue.Int32(1));
        byte[] truncated = AppendPayloadBytes(valid, [1, 0, 4]);

        Assert.Throws<FormatException>(
            () => ActorWireProtocol.DecodeHello(duplicate));
        Assert.Throws<FormatException>(
            () => ActorWireProtocol.DecodeHello(truncated));
    }

    [Fact]
    public void UnknownTaggedFields_AreSkippedForForwardCompatibility()
    {
        ActorDecision expected = Actions.Transform("flight") with
        {
            DebugMessage = "future field follows",
        };
        byte[] encoded = ActorWireProtocol.EncodeDecision(expected);
        byte[] extended = AppendField(
            encoded,
            ushort.MaxValue,
            [0xDE, 0xAD, 0xBE, 0xEF]);

        ActorDecision actual = ActorWireProtocol.DecodeDecision(extended);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Ready_AttestsGuestCompiledContractVersions()
    {
        byte[] frame = ActorWireProtocol.EncodeReady(
            ActorWireProtocol.MajorVersion,
            ActorContractVersions.RuntimeContractVersion,
            ActorContractVersions.MatchStartSchemaVersion,
            ActorContractVersions.ObservationSchemaVersion,
            ActorContractVersions.DecisionSchemaVersion);

        ActorWireReady ready = ActorWireProtocol.DecodeReady(frame);

        Assert.Equal(ActorWireProtocol.MajorVersion, ready.SelectedMajor);
        Assert.Equal(
            ActorContractVersions.RuntimeContractVersion,
            ready.RuntimeContractVersion);
        Assert.Equal(
            ActorContractVersions.MatchStartSchemaVersion,
            ready.MatchStartSchemaVersion);
        Assert.Equal(
            ActorContractVersions.ObservationSchemaVersion,
            ready.ObservationSchemaVersion);
        Assert.Equal(
            ActorContractVersions.DecisionSchemaVersion,
            ready.DecisionSchemaVersion);
    }

    [Fact]
    public void UnsupportedCapability_IsTypedAndMapsToFrontlineIneligibility()
    {
        byte[] frame = ActorWireProtocol.EncodeUnsupported(
            "actor-runtime",
            "This artifact contains only a legacy duel bot.");

        ActorWireUnsupported unsupported =
            ActorWireProtocol.DecodeUnsupported(frame);
        ActorProtocolNotSupportedException error =
            Assert.Throws<ActorProtocolNotSupportedException>(
                () => ActorWasmProtocol.ParseHelloAck(frame));

        Assert.Equal("actor-runtime", unsupported.Capability);
        Assert.Contains(
            "legacy duel bot",
            unsupported.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "actor-runtime",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Decisions_RoundTripEveryCurrentActionPayload()
    {
        var programmedShot = new ShotProgram(-1, 1, 2, 3, 2);
        ActorDecision[] decisions =
        [
            (ActorDecision)Actions.Wait(),
            (ActorDecision)Actions.MoveForward(),
            (ActorDecision)Actions.TurnLeft(),
            (ActorDecision)Actions.TurnRight(),
            (ActorDecision)Actions.Shoot(),
            (ActorDecision)Actions.Shoot(programmedShot),
            Actions.Fabricate(new ObservedUnitTarget(2, 4)),
            Actions.Transform("turret"),
            Actions.ShootDirection(ProjectileHeading.SouthWest),
            ActorDecision.Of(
                "future-composite-action",
                900,
                new ActorActionPayload
                {
                    ShotProgram = programmedShot,
                    Direction = Direction.West,
                    UnitTarget = new ObservedUnitTarget(3, 7),
                    FormTargetId = "flight",
                    LaunchHeading = ProjectileHeading.NorthEast,
                },
                "all payload fields"),
        ];

        foreach (ActorDecision expected in decisions)
        {
            ActorDecision actual = ActorWireProtocol.DecodeDecision(
                ActorWireProtocol.EncodeDecision(expected));

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SemanticIds_UseOneSharedUtf8ByteLimit()
    {
        string maximum = new(
            'a',
            ActorWireProtocol.MaxSemanticIdBytes);
        ActorDecision expected = ActorDecision.Of(
            maximum,
            900,
            new ActorActionPayload
            {
                FormTargetId = maximum,
            });

        ActorDecision actual = ActorWireProtocol.DecodeDecision(
            ActorWireProtocol.EncodeDecision(expected));

        Assert.Equal(expected, actual);
        Assert.Throws<InvalidOperationException>(
            () => ActorWireProtocol.EncodeDecision(
                expected with
                {
                    ActionId = maximum + "a",
                }));
        Assert.Throws<InvalidOperationException>(
            () => ActorWireProtocol.EncodeDecision(
                expected with
                {
                    Payload = expected.Payload! with
                    {
                        FormTargetId = new string('é', 33),
                    },
                }));
    }

    [Fact]
    public void Observation_RoundTripPreservesNullVersusPresentEmptyCapabilities()
    {
        ActorContext unsupported = CreateContext(
            projectiles: null,
            sounds: null,
            allowedDirections: null,
            allowedTargets: null,
            allowedForms: null,
            allowedHeadings: null);
        ActorContext supportedButEmpty = CreateContext(
            projectiles: [],
            sounds: [],
            allowedDirections: [],
            allowedTargets: [],
            allowedForms: [],
            allowedHeadings: []);

        byte[] unsupportedBytes =
            ActorWireProtocol.EncodeObservation(unsupported);
        byte[] emptyBytes =
            ActorWireProtocol.EncodeObservation(supportedButEmpty);
        ActorContext unsupportedResult =
            ActorWireProtocol.DecodeObservation(unsupportedBytes);
        ActorContext emptyResult =
            ActorWireProtocol.DecodeObservation(emptyBytes);

        Assert.False(unsupportedBytes.SequenceEqual(emptyBytes));
        Assert.Null(unsupportedResult.VisibleProjectiles);
        Assert.Null(unsupportedResult.HeardSounds);
        Assert.Null(unsupportedResult.Actions.Single().AllowedDirections);
        Assert.Null(unsupportedResult.Actions.Single().AllowedUnitTargets);
        Assert.Null(unsupportedResult.Actions.Single().AllowedFormTargets);
        Assert.Null(
            unsupportedResult.Actions.Single().AllowedProjectileHeadings);

        Assert.True(emptyResult.VisibleProjectiles.HasValue);
        Assert.True(emptyResult.VisibleProjectiles.Value.IsEmpty);
        Assert.True(emptyResult.HeardSounds.HasValue);
        Assert.True(emptyResult.HeardSounds.Value.IsEmpty);
        ObservedActionAvailability emptyAction =
            emptyResult.Actions.Single();
        Assert.True(emptyAction.AllowedDirections is { IsEmpty: true });
        Assert.True(emptyAction.AllowedUnitTargets is { IsEmpty: true });
        Assert.True(emptyAction.AllowedFormTargets is { IsEmpty: true });
        Assert.True(
            emptyAction.AllowedProjectileHeadings is { IsEmpty: true });
    }

    [Fact]
    public void Observation_MaximumMapAndFiveSensorUnion_FitsHostFrame()
    {
        ImmutableArray<ActorIdentity> observers = Enumerable
            .Range(0, 5)
            .Select(unitId => new ActorIdentity(0, unitId, 0))
            .ToImmutableArray();
        ImmutableArray<ObservedMapTile> tiles = Enumerable
            .Range(0, 32 * 32)
            .Select(index => new ObservedMapTile(
                new Position(index % 32, index / 32),
                IsWall: false,
                observers))
            .ToImmutableArray();
        ImmutableArray<ObservedActorProjectile> projectiles = Enumerable
            .Range(0, 128)
            .Select(index => new ObservedActorProjectile(
                $"projectile-{index}",
                1,
                null,
                new ObservedEnemyActorRef(
                    1,
                    index % 5,
                    $"enemy-{index % 5}"),
                new Position(index % 32, index / 32),
                ProjectileHeading.East,
                2,
                1,
                8,
                observers))
            .ToImmutableArray();
        ImmutableArray<ObservedMatchEvent> events = Enumerable
            .Range(0, 128)
            .Select(index => new ObservedMatchEvent(
                $"event-{index}",
                16,
                ObservedMatchEventType.Move,
                1,
                null,
                new ObservedEnemyActorRef(
                    1,
                    index % 5,
                    $"enemy-{index % 5}"),
                null,
                new Position(index % 32, index / 32),
                Direction.West,
                null,
                null,
                observers))
            .ToImmutableArray();
        ActorContext context = CreateContext(
            projectiles,
            sounds: [],
            allowedDirections: [],
            allowedTargets: [],
            allowedForms: [],
            allowedHeadings: []) with
        {
            TeamUnits = observers
                .Select(actor => new ObservedUnitSlot(
                    actor.TeamId,
                    actor.UnitId,
                    "mobile",
                    FrontlineLifecycleStatus.Active,
                    actor,
                    RespawnAtTick: null))
                .ToImmutableArray(),
            Allies = observers[1..]
                .Select(actor => new ObservedAlly(
                    actor,
                    "mobile",
                    new Position(actor.UnitId, 1),
                    Direction.East,
                    Health: 3,
                    Cooldown: 0,
                    Energy: null,
                    PreviousActionResult: ActionResult.Success))
                .ToImmutableArray(),
            Enemies = Enumerable
                .Range(0, 5)
                .Select(unitId => new ObservedEnemy(
                    new ObservedEnemyActorRef(
                        1,
                        unitId,
                        $"enemy-{unitId}"),
                    "mobile",
                    new Position(31 - unitId, 30),
                    Direction.West,
                    Health: 3,
                    observers))
                .ToImmutableArray(),
            VisibleTiles = tiles,
            VisibleEvents = events,
        };

        byte[] frame = ActorWireProtocol.EncodeObservation(context);
        ActorContext decoded =
            ActorWireProtocol.DecodeObservation(frame);

        Assert.True(
            frame.Length > 256 * 1024,
            $"Expected the future-topology fixture to exceed 256 KiB, got {frame.Length} bytes.");
        Assert.True(frame.Length <= ActorWireProtocol.MaxHostFrameBytes);
        Assert.Equal(32 * 32, decoded.VisibleTiles.Length);
        Assert.Equal(128, decoded.VisibleProjectiles?.Length);
        Assert.Equal(5, decoded.TeamUnits.Length);
    }

    private static ActorContext CreateContext(
        ImmutableArray<ObservedActorProjectile>? projectiles,
        ImmutableArray<ObservedActorSound>? sounds,
        ImmutableArray<Direction>? allowedDirections,
        ImmutableArray<ObservedUnitTarget>? allowedTargets,
        ImmutableArray<string>? allowedForms,
        ImmutableArray<ProjectileHeading>? allowedHeadings)
    {
        var action = new ObservedActionAvailability(
            ActorActionIds.ShootDirection,
            ActorActionCodes.ShootDirection,
            [PublicActionParameterKind.ProjectileHeading],
            Enabled: true,
            Available: true,
            ShotProgramAvailable: null,
            allowedDirections,
            allowedTargets,
            allowedForms)
        {
            AllowedProjectileHeadings = allowedHeadings,
        };

        return new ActorContext
        {
            SchemaVersion = 1,
            Tick = 17,
            MatchContractFingerprint = new string('a', 64),
            TeamPerception = TeamPerceptionMode.ImmediateUnion,
            Self = new ObservedSelf(
                new ActorIdentity(0, 0, 2),
                "prime",
                new Position(3, 4),
                Direction.East,
                Health: 9,
                Cooldown: 1,
                Energy: null,
                ActionResult.Success),
            TeamUnits = [],
            Allies = [],
            Enemies = [],
            VisibleTiles = [],
            VisibleProjectiles = projectiles,
            VisibleEvents = [],
            HeardSounds = sounds,
            FrontlineObjective = null,
            Actions = [action],
        };
    }

    private static byte[] ChangeByte(
        byte[] source,
        int offset,
        int value)
    {
        byte[] changed = (byte[])source.Clone();
        changed[offset] = checked((byte)value);
        return changed;
    }

    private static byte[] ChangePayloadLength(byte[] source, int length)
    {
        byte[] changed = (byte[])source.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(
            changed.AsSpan(8, 4),
            length);
        return changed;
    }

    private static byte[] AppendField(
        byte[] frame,
        ushort fieldId,
        byte[] value)
    {
        byte[] field = new byte[6 + value.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(
            field.AsSpan(0, 2),
            fieldId);
        BinaryPrimitives.WriteInt32LittleEndian(
            field.AsSpan(2, 4),
            value.Length);
        value.CopyTo(field, 6);
        return AppendPayloadBytes(frame, field);
    }

    private static byte[] AppendPayloadBytes(
        byte[] frame,
        byte[] payloadBytes)
    {
        byte[] extended = [.. frame, .. payloadBytes];
        BinaryPrimitives.WriteInt32LittleEndian(
            extended.AsSpan(8, 4),
            extended.Length - ActorWireProtocol.HeaderSize);
        return extended;
    }
}
