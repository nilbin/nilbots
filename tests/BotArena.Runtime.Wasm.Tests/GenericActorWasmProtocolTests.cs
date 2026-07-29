using BotArena.Runtime.Wasm;
using Engine = BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class GenericActorWasmProtocolTests
{
    [Fact]
    public void HelloAndAck_RequireTheExactGenericContractProfile()
    {
        Sdk.ActorWireHello hello = Sdk.ActorWireProtocol.DecodeHello(
            GenericActorWasmProtocol.FormatHello());

        Assert.Equal(Sdk.ActorWireProtocol.MajorVersion, hello.MinimumMajor);
        Assert.Equal(Sdk.ActorWireProtocol.MajorVersion, hello.MaximumMajor);
        Assert.Equal(
            Sdk.ActorContractProfile.GenericV3,
            hello.RequiredProfile);
        Assert.Equal(
            Sdk.ActorWireProtocol.MajorVersion,
            GenericActorWasmProtocol.ParseHelloAck(
                Sdk.ActorWireProtocol.EncodeHelloAck(
                    Sdk.ActorWireProtocol.MajorVersion,
                    Sdk.ActorContractProfile.GenericV3)));

        Assert.Throws<FormatException>(
            () => GenericActorWasmProtocol.ParseHelloAck(
                Sdk.ActorWireProtocol.EncodeHelloAck(
                    Sdk.ActorWireProtocol.MajorVersion)));
        Assert.Throws<FormatException>(
            () => GenericActorWasmProtocol.ParseHelloAck(
                Sdk.ActorWireProtocol.EncodeHelloAck(
                    Sdk.ActorWireProtocol.MajorVersion,
                    Sdk.ActorContractProfile.GenericV3 with
                    {
                        ProfileId = "future-generic-profile",
                    })));
    }

    [Fact]
    public void Ready_RequiresTheExactGenericVersionTuple()
    {
        Engine.GenericActorRuntimeStart start =
            GenericActorWasmTestFixture.Start(
                GenericActorWasmTestFixture.Contract(),
                teamId: 0);
        byte[] exact = Sdk.ActorWireProtocol.EncodeReady(
            Sdk.ActorWireProtocol.MajorVersion,
            Sdk.GenericActorContractVersions.RuntimeContractVersion,
            Sdk.GenericActorContractVersions.MatchStartSchemaVersion,
            Sdk.GenericActorContractVersions.ObservationSchemaVersion,
            Sdk.GenericActorContractVersions.DecisionSchemaVersion,
            Sdk.ActorContractProfile.GenericV3);

        GenericActorWasmProtocol.ParseReady(exact, start);

        byte[] legacy = Sdk.ActorWireProtocol.EncodeReady(
            Sdk.ActorWireProtocol.MajorVersion,
            Sdk.GenericActorContractVersions.RuntimeContractVersion,
            Sdk.GenericActorContractVersions.MatchStartSchemaVersion,
            Sdk.GenericActorContractVersions.ObservationSchemaVersion,
            Sdk.GenericActorContractVersions.DecisionSchemaVersion);
        Assert.Throws<FormatException>(
            () => GenericActorWasmProtocol.ParseReady(legacy, start));

        Sdk.ActorContractProfile differentTuple =
            Sdk.ActorContractProfile.GenericV3 with
            {
                MatchContractSchemaVersion =
                    Sdk.ActorContractProfile.GenericV3
                        .MatchContractSchemaVersion + 1,
            };
        byte[] mismatched = Sdk.ActorWireProtocol.EncodeReady(
            Sdk.ActorWireProtocol.MajorVersion,
            differentTuple.RuntimeContractVersion,
            differentTuple.MatchStartSchemaVersion,
            differentTuple.ObservationSchemaVersion,
            differentTuple.DecisionSchemaVersion,
            differentTuple);
        Assert.Throws<FormatException>(
            () => GenericActorWasmProtocol.ParseReady(
                mismatched,
                start));
    }

    [Fact]
    public void FrozenProfile2MatchNegotiatesItsFrozenTuple()
    {
        Engine.ActorResolvedMatchDefinition contract =
            Engine.FrontlineLabsDefinition.Create();
        Engine.GenericActorRuntimeStart start =
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 0) with
            {
                SchemaVersion =
                    Sdk.ActorContractProfile.GenericV2
                        .MatchStartSchemaVersion,
                RuntimeContractVersion =
                    Sdk.ActorContractProfile.GenericV2
                        .RuntimeContractVersion,
            };
        Sdk.ActorContractProfile required =
            GenericActorWasmProtocol.RequiredProfile(start);

        Sdk.ActorWireHello hello = Sdk.ActorWireProtocol.DecodeHello(
            GenericActorWasmProtocol.FormatHello(required));
        Assert.Equal(Sdk.ActorContractProfile.GenericV2, required);
        Assert.Equal(required, hello.RequiredProfile);
        Assert.Equal(
            Sdk.ActorWireProtocol.MajorVersion,
            GenericActorWasmProtocol.ParseHelloAck(
                Sdk.ActorWireProtocol.EncodeHelloAck(
                    Sdk.ActorWireProtocol.MajorVersion,
                    required),
                required));

        byte[] ready = Sdk.ActorWireProtocol.EncodeReady(
            Sdk.ActorWireProtocol.MajorVersion,
            required.RuntimeContractVersion,
            required.MatchStartSchemaVersion,
            required.ObservationSchemaVersion,
            required.DecisionSchemaVersion,
            required);
        GenericActorWasmProtocol.ParseReady(ready, start);
    }

    [Fact]
    public void MatchStartAndObservation_UseTheSharedGenericSdkCodecs()
    {
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorWasmTestFixture.Contract();
        Engine.GenericActorRuntimeStart start =
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 0,
                seed: ulong.MaxValue);
        Sdk.ActorWireGenericMatchStart decodedStart =
            Sdk.ActorWireProtocol.DecodeGenericMatchStart(
                GenericActorWasmProtocol.FormatMatchStart(
                    start,
                    "generic-probe"));

        Assert.Equal("generic-probe", decodedStart.BotName);
        Assert.Equal(start.SchemaVersion, decodedStart.Start.SchemaVersion);
        Assert.Equal(
            start.RuntimeContractVersion,
            decodedStart.Start.RuntimeContractVersion);
        Assert.Equal(start.ActorId.TeamId, decodedStart.Start.ActorId.TeamId);
        Assert.Equal(start.ActorId.UnitId, decodedStart.Start.ActorId.UnitId);
        Assert.Equal(start.ActorId.LifeId, decodedStart.Start.ActorId.LifeId);
        Assert.Equal(start.ParticipantId, decodedStart.Start.ParticipantId);
        Assert.Equal(
            start.ActorRandomSeed,
            decodedStart.Start.ActorRandomSeed);
        Assert.Equal(
            Engine.ActorContractManifestSerializer.ToCanonicalJson(contract),
            decodedStart.Start.Contract.CanonicalJson);
        Assert.Equal(
            Engine.ActorContractFingerprint.ComputeMatch(contract),
            decodedStart.Start.Contract.MatchContractFingerprint);

        Engine.GenericActorRuntimeObservation observation =
            GenericActorWasmTestFixture.Observation(
                contract,
                teamId: 0,
                tick: 17);
        Sdk.GenericActorContext decodedObservation =
            Sdk.ActorWireProtocol.DecodeGenericObservation(
                GenericActorWasmProtocol.FormatObservation(observation));

        Assert.Equal(observation.SchemaVersion, decodedObservation.SchemaVersion);
        Assert.Equal(observation.Tick, decodedObservation.Tick);
        Assert.Equal(
            observation.MatchContractFingerprint,
            decodedObservation.MatchContractFingerprint);
        Assert.Equal(
            observation.Self.ActorId.TeamId,
            decodedObservation.Self.ActorId.TeamId);
        Assert.Equal(observation.Self.Health, decodedObservation.Self.Health);
        Assert.NotNull(decodedObservation.VisibleProjectiles);
        Assert.Empty(decodedObservation.VisibleProjectiles.Value);
        Assert.NotNull(decodedObservation.HeardSounds);
        Assert.Empty(decodedObservation.HeardSounds.Value);
        Assert.Equal(
            "deathmatch",
            decodedObservation.Mode.ModeId);
        Assert.Equal(
            "wait",
            Assert.Single(decodedObservation.ActionLegalities).ActionId);
    }

    [Fact]
    public void Decision_UsesTheSharedGenericCodecAndMapsEveryArgument()
    {
        var source = new Sdk.GenericActorDecision(
            "future-composite",
            99,
            [
                new Sdk.GenericActorActionArgument
                    .ProjectileHeadingArgument(
                        Sdk.ProjectileHeading.NorthEast),
                new Sdk.GenericActorActionArgument
                    .FormTargetArgument("flight"),
                new Sdk.GenericActorActionArgument
                    .UnitTargetArgument(
                        new Sdk.GenericActorActionArgument.UnitTarget(2, 7)),
                new Sdk.GenericActorActionArgument
                    .DirectionArgument(Sdk.Direction.West),
                new Sdk.GenericActorActionArgument
                    .ShotProgramArgument(
                        new Sdk.ShotProgram(1, -1, 2, 3, 2)),
            ],
            "diagnostic");

        Engine.GenericActorRuntimeDecision decoded =
            GenericActorWasmProtocol.ParseDecision(
                Sdk.ActorWireProtocol.EncodeGenericDecision(source));

        Assert.Equal(source.ActionId, decoded.ActionId);
        Assert.Equal(source.ActionCode, decoded.ActionCode);
        Assert.Equal(source.DebugMessage, decoded.DebugMessage);
        Assert.Equal(
            Enum.GetValues<Engine.ActorActionParameterKind>(),
            decoded.Arguments.Select(argument => argument.Kind));
        Assert.Equal(
            new Engine.ShotProgram(1, -1, 2, 3, 2),
            Assert.IsType<
                Engine.GenericActorRuntimeActionArgument
                    .ShotProgramArgument>(decoded.Arguments[0]).Value);
        Assert.Equal(
            Engine.Direction.West,
            Assert.IsType<
                Engine.GenericActorRuntimeActionArgument
                    .DirectionArgument>(decoded.Arguments[1]).Value);
        Assert.Equal(
            new Engine.GenericActorRuntimeActionArgument.UnitTarget(2, 7),
            Assert.IsType<
                Engine.GenericActorRuntimeActionArgument
                    .UnitTargetArgument>(decoded.Arguments[2]).Value);
        Assert.Equal(
            "flight",
            Assert.IsType<
                Engine.GenericActorRuntimeActionArgument
                    .FormTargetArgument>(decoded.Arguments[3]).FormId);
        Assert.Equal(
            Engine.ProjectileHeading.NorthEast,
            Assert.IsType<
                Engine.GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument>(
                        decoded.Arguments[4]).Value);
    }

    [Fact]
    public void FaultUnsupportedAndWrongMessages_FailClosed()
    {
        ActorWasmGuestException fault =
            Assert.Throws<ActorWasmGuestException>(
                () => GenericActorWasmProtocol.ParseDecision(
                    Sdk.ActorWireProtocol.EncodeFault("guest failed")));
        Assert.Equal("guest failed", fault.Message);

        ActorProtocolNotSupportedException unsupported =
            Assert.Throws<ActorProtocolNotSupportedException>(
                () => GenericActorWasmProtocol.ParseHelloAck(
                    Sdk.ActorWireProtocol.EncodeUnsupported(
                        "generic-actor",
                        "profile absent")));
        Assert.Contains(
            "generic-actor",
            unsupported.Message,
            StringComparison.Ordinal);

        Assert.Throws<FormatException>(
            () => GenericActorWasmProtocol.ParseDecision(
                Sdk.ActorWireProtocol.EncodeReady(
                    Sdk.ActorWireProtocol.MajorVersion,
                    Sdk.GenericActorContractVersions
                        .RuntimeContractVersion,
                    Sdk.GenericActorContractVersions
                        .MatchStartSchemaVersion,
                    Sdk.GenericActorContractVersions
                        .ObservationSchemaVersion,
                    Sdk.GenericActorContractVersions
                        .DecisionSchemaVersion,
                    Sdk.ActorContractProfile.GenericV3)));
    }
}
