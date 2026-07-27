using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class GenericActorStaticContractTests
{
    [Theory]
    [InlineData(
        "head-to-head",
        GenericActorResolvedMatchContract.MatchFormatKind.HeadToHead,
        2,
        2)]
    [InlineData(
        "free-for-all",
        GenericActorResolvedMatchContract.MatchFormatKind.FreeForAll,
        4,
        4)]
    [InlineData(
        "teams",
        GenericActorResolvedMatchContract.MatchFormatKind.Teams,
        2,
        4)]
    public void ParsesExactDeathmatchContractAcrossSupportedFormats(
        string formatName,
        GenericActorResolvedMatchContract.MatchFormatKind expectedKind,
        int expectedTeams,
        int expectedParticipants)
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Deathmatch(formatName);
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(source);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);

        Assert.Same(canonical, contract.CanonicalJson);
        Assert.Equal(
            ActorResolvedMatchDefinition.CurrentSchemaVersion,
            contract.SchemaVersion);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(source),
            contract.MatchContractFingerprint);
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(source.Rules),
            contract.Rules.RulesFingerprint);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(source.Map),
            contract.Map.MapFingerprint);
        Assert.Equal(
            ActorContractFingerprint.ComputeFormat(source.Format),
            contract.Format.FormatFingerprint);
        Assert.Equal(
            ActorContractFingerprint.ComputeTopology(source.Topology),
            contract.Topology.TopologyFingerprint);
        Assert.Equal(expectedKind, contract.Format.Kind);
        Assert.Equal(expectedTeams, contract.Topology.Counts.TeamCount);
        Assert.Equal(
            expectedParticipants,
            contract.Topology.Counts.ParticipantCount);
        Assert.IsType<GenericActorRulesContract.DeathmatchGameMode>(
            contract.Rules.GameMode);
        Assert.IsType<
            GenericActorResolvedMatchContract.DeathmatchModeMapBinding>(
            contract.ModeMapBinding);
        Assert.Equal(
            GenericActorRulesContract.ProjectileMode.Discrete,
            Assert.Single(contract.Rules.AttackProfiles).Projectile.Mode);
        Assert.Equal(
            GenericActorRulesContract.TeamPerceptionKind.ImmediateUnion,
            contract.Rules.TeamPerception.Kind);
        Assert.Equal(
            GenericActorContractVersions.ContractProfileId,
            contract.CapabilityVersions.ContractProfileId);
        Assert.Equal(
            GenericActorContractVersions.RuntimeConfigurationVersion,
            contract.CapabilityVersions.RuntimeConfigurationVersion);
        Assert.Equal(
            GenericActorContractVersions.MatchContractSchemaVersion,
            contract.CapabilityVersions.MatchContractSchemaVersion);
    }

    [Fact]
    public void ParsesFrontlineModeAndDedicatedObjectiveDirections()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Frontline();
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(source);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);

        var mode =
            Assert.IsType<GenericActorRulesContract.FrontlineGameMode>(
                contract.Rules.GameMode);
        var victory =
            Assert.IsType<GenericActorRulesContract.FrontlineVictory>(
                mode.Victory);
        var binding = Assert.IsType<
            GenericActorResolvedMatchContract.FrontlineModeMapBinding>(
                contract.ModeMapBinding);
        Assert.Equal(3, victory.PushesToBreach);
        Assert.Equal(5, mode.FrontlinePositionCount);
        Assert.Equal(5, binding.OrderedObjectiveRegionIds.Length);
        Assert.Collection(
            binding.TeamAdvances,
            first =>
            {
                Assert.Equal(0, first.TeamId);
                Assert.Equal(
                    GenericActorResolvedMatchContract
                        .ObjectiveAdvanceDirection.TowardHigherIndex,
                    first.Direction);
                Assert.Equal(1, first.ObjectiveIndexDelta);
            },
            second =>
            {
                Assert.Equal(1, second.TeamId);
                Assert.Equal(
                    GenericActorResolvedMatchContract
                        .ObjectiveAdvanceDirection.TowardLowerIndex,
                    second.Direction);
                Assert.Equal(-1, second.ObjectiveIndexDelta);
            });
        Assert.Equal(canonical, contract.CanonicalJson);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(source),
            contract.MatchContractFingerprint);
    }

    [Fact]
    public void AcceptedFixturesAllComeFromEngineValidatedResolvedMatches()
    {
        ActorResolvedMatchDefinition[] engineValidated =
        [
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            GenericActorContractTestFixture.Deathmatch("free-for-all"),
            GenericActorContractTestFixture.Deathmatch("teams"),
            GenericActorContractTestFixture.Frontline(),
            GenericActorContractTestFixture.WithTransitions(),
        ];

        foreach (ActorResolvedMatchDefinition source in engineValidated)
        {
            string canonical =
                ActorContractManifestSerializer.ToCanonicalJson(source);
            GenericActorResolvedMatchContract parsed =
                ActorCanonicalContractReader.Parse(canonical);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(source),
                parsed.MatchContractFingerprint);
        }
    }

    [Fact]
    public void MatchStartWireRoundTripPreservesExactCanonicalJson()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Deathmatch("teams");
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(source);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var start = new GenericActorMatchStart
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = new BotArena.Sdk.ActorIdentity(0, 1, 0),
            ParticipantId = 11,
            ActorRandomSeed = 18_446_744_073_709_551_000UL,
            Origin = new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = contract,
        };

        byte[] bytes =
            GenericActorWireContractCodec.EncodeMatchStart(start);
        GenericActorMatchStart decoded =
            GenericActorWireContractCodec.DecodeMatchStart(bytes);

        Assert.Equal(start.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(
            start.RuntimeContractVersion,
            decoded.RuntimeContractVersion);
        Assert.Equal(start.ActorId, decoded.ActorId);
        Assert.Equal(start.ParticipantId, decoded.ParticipantId);
        Assert.Equal(start.ActorRandomSeed, decoded.ActorRandomSeed);
        Assert.Equal(start.Origin, decoded.Origin);
        Assert.Equal(canonical, decoded.Contract.CanonicalJson);
        Assert.Equal(
            contract.MatchContractFingerprint,
            decoded.Contract.MatchContractFingerprint);
        Assert.IsType<GenericActorRulesContract.DeathmatchGameMode>(
            decoded.Contract.Rules.GameMode);
    }

    [Fact]
    public void ParsesEveryTransitionVariantAndKeepsSplitOperationLineage()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.WithTransitions();
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(source);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);

        var fabrication = Assert.IsType<
            GenericActorRulesContract.BoundedChildFabricationTransition>(
                Assert.Single(contract.Rules.FabricationTransitions));
        var sameLife =
            Assert.IsType<GenericActorRulesContract.FormTransition>(
                Assert.Single(contract.Rules.SameLifeTransitions));
        var split = Assert.IsType<
            GenericActorRulesContract.SplitReplicationTransition>(
                Assert.Single(contract.Rules.ReplicationTransitions));
        Assert.Equal("fabricate-child", fabrication.TransitionId);
        Assert.Equal("anchor-child", sameLife.TransitionId);
        Assert.Equal("split-mobile", split.TransitionId);
        Assert.Equal(2, split.DescendantCount);
        Assert.Equal(
            [(0, -1), (0, 1)],
            split.CandidateOffsets
                .Select(offset => (offset.Forward, offset.Right))
                .ToArray());

        var start = new GenericActorMatchStart
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = new BotArena.Sdk.ActorIdentity(0, 1, 0),
            ParticipantId = 10,
            ActorRandomSeed = 42,
            Origin = new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Replication,
                Generation: 1,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: "split-mobile",
                SourceOperationId: "split:0:0:0:42"),
            Contract = contract,
        };

        GenericActorMatchStart decoded =
            GenericActorWireContractCodec.DecodeMatchStart(
                GenericActorWireContractCodec.EncodeMatchStart(start));

        Assert.Equal(start.Origin, decoded.Origin);
        Assert.Equal("split-mobile", decoded.Origin.SourceTransitionId);
        Assert.Equal("split:0:0:0:42", decoded.Origin.SourceOperationId);
        Assert.Equal(canonical, decoded.Contract.CanonicalJson);
    }

    [Fact]
    public void FabricationAndAutomaticReturnOriginsRoundTrip()
    {
        GenericActorResolvedMatchContract transitionContract =
            Parse(GenericActorContractTestFixture.WithTransitions());
        GenericActorMatchStart fabrication = Start(
            transitionContract,
            new BotArena.Sdk.ActorIdentity(0, 1, 0),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Fabrication,
                Generation: 1,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: "fabricate-child",
                SourceOperationId: "fabrication-operation"));
        Assert.Equal(
            fabrication.Origin,
            GenericActorWireContractCodec.DecodeMatchStart(
                GenericActorWireContractCodec.EncodeMatchStart(fabrication))
                .Origin);

        GenericActorResolvedMatchContract respawnContract =
            Parse(GenericActorContractTestFixture.Deathmatch("head-to-head"));
        GenericActorMatchStart automaticReturn = Start(
            respawnContract,
            new BotArena.Sdk.ActorIdentity(0, 0, 1),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.AutomaticReturn,
                Generation: 0,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: null,
                SourceOperationId: null));
        Assert.Equal(
            automaticReturn.Origin,
            GenericActorWireContractCodec.DecodeMatchStart(
                GenericActorWireContractCodec.EncodeMatchStart(
                    automaticReturn)).Origin);
    }

    [Fact]
    public void TransitionOriginRejectsInvalidGenerationOrOutputFormAssignment()
    {
        GenericActorResolvedMatchContract contract =
            Parse(GenericActorContractTestFixture.WithTransitions());
        GenericActorMatchStart invalidFabricationGeneration = Start(
            contract,
            new BotArena.Sdk.ActorIdentity(0, 1, 0),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Fabrication,
                Generation: 0,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: "fabricate-child",
                SourceOperationId: "fabrication-generation-zero"));
        Assert.Throws<ArgumentException>(
            () => GenericActorWireContractCodec.EncodeMatchStart(
                invalidFabricationGeneration));

        GenericActorMatchStart invalidGeneration = Start(
            contract,
            new BotArena.Sdk.ActorIdentity(0, 1, 0),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Replication,
                Generation: 2,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: "split-mobile",
                SourceOperationId: "split-generation-two"));
        Assert.Throws<ArgumentException>(
            () => GenericActorWireContractCodec.EncodeMatchStart(
                invalidGeneration));

        ImmutableArray<
            GenericActorResolvedMatchContract.LifecycleAssignment>
            incompatibleAssignments = contract.LifecycleAssignments
                .Select(
                    assignment =>
                        assignment.TeamId == 0 && assignment.UnitId == 1
                            ? assignment with
                            {
                                AllowedFormIds = ["mobile"],
                            }
                            : assignment)
                .ToImmutableArray();
        GenericActorResolvedMatchContract incompatibleContract =
            CloneContract(
                contract,
                contract.CanonicalJson,
                incompatibleAssignments);
        GenericActorMatchStart invalidOutputForm = Start(
            incompatibleContract,
            new BotArena.Sdk.ActorIdentity(0, 1, 0),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Replication,
                Generation: 1,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: "split-mobile",
                SourceOperationId: "split-incompatible-form"));
        Assert.Throws<ArgumentException>(
            () => GenericActorWireContractCodec.EncodeMatchStart(
                invalidOutputForm));
    }

    [Fact]
    public void OpaqueOperationHandleSupportsUpTo256Utf8Bytes()
    {
        GenericActorResolvedMatchContract contract =
            Parse(GenericActorContractTestFixture.WithTransitions());
        string operationId =
            "opaque handle with whitespace "
            + new string('o', 226);
        GenericActorMatchStart start = Start(
            contract,
            new BotArena.Sdk.ActorIdentity(0, 1, 0),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Replication,
                Generation: 1,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: "split-mobile",
                SourceOperationId: operationId));

        byte[] frame = ActorWireProtocol.EncodeGenericMatchStart(
            "operation-handle-bot",
            start);
        ActorWireGenericMatchStart decoded =
            ActorWireProtocol.DecodeGenericMatchStart(frame);

        Assert.Equal(operationId, decoded.Start.Origin.SourceOperationId);

        Assert.Equal(256, Encoding.UTF8.GetByteCount(operationId));
        GenericActorMatchStart rejected = start with
        {
            Origin = start.Origin with
            {
                SourceOperationId = new string('o', 257),
            },
        };
        Assert.Throws<ArgumentException>(
            () => GenericActorWireContractCodec.EncodeMatchStart(
                rejected));
    }

    [Fact]
    public void DuplicateSelfFingerprintedTransitionDecodesAsFormatFailure()
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            GenericActorContractTestFixture.WithTransitions());
        string duplicated =
            DuplicateReplicationTransitionAndRehash(canonical);
        GenericActorResolvedMatchContract parsed =
            ActorCanonicalContractReader.Parse(duplicated);
        Assert.Equal(2, parsed.Rules.ReplicationTransitions.Length);

        var origin = new ActorWireObjectWriter();
        origin.Field(
            1,
            ActorWireValue.Enum(
                GenericActorMatchStart.SpawnReason.Replication));
        origin.Field(2, ActorWireValue.Int32(1));
        origin.Field(
            3,
            ActorWireContractCodec.EncodeIdentity(
                new BotArena.Sdk.ActorIdentity(0, 0, 0)));
        origin.Field(
            4,
            ActorWireValue.String(
                "split-mobile",
                ActorWireProtocol.MaxSemanticIdBytes));
        origin.Field(
            5,
            ActorWireValue.String("duplicate-transition-operation", 256));

        var start = new ActorWireObjectWriter();
        start.Field(
            1,
            ActorWireValue.Int32(
                GenericActorContractVersions.MatchStartSchemaVersion));
        start.Field(
            2,
            ActorWireValue.Int32(
                GenericActorContractVersions.RuntimeContractVersion));
        start.Field(
            3,
            ActorWireContractCodec.EncodeIdentity(
                new BotArena.Sdk.ActorIdentity(0, 1, 0)));
        start.Field(4, ActorWireValue.Int32(10));
        start.Field(5, ActorWireValue.UInt64(42));
        start.Field(6, origin.ToArray());
        start.Field(
            7,
            ActorWireValue.String(
                duplicated,
                GenericActorContractVersions.MaxCanonicalContractBytes));

        var payload = new ActorWireObjectWriter();
        payload.Field(1, ActorWireValue.String("duplicate-bot", 256));
        payload.Field(2, start.ToArray());
        byte[] frame = HostFrame(
            ActorWireMessageType.MatchStart,
            payload.ToArray());

        Assert.Throws<FormatException>(
            () => ActorWireProtocol.DecodeGenericMatchStart(frame));
    }

    [Fact]
    public void ReservedEnvelopeFitsMaximumContractBotAndLineage()
    {
        string transitionId = new('s', 64);
        GenericActorResolvedMatchContract parsed = Parse(
            GenericActorContractTestFixture.WithTransitions(
                transitionId));
        GenericActorResolvedMatchContract maximumContract = CloneContract(
            parsed,
            new string(
                'x',
                GenericActorContractVersions.MaxCanonicalContractBytes),
            parsed.LifecycleAssignments);
        GenericActorMatchStart start = Start(
            maximumContract,
            new BotArena.Sdk.ActorIdentity(0, 1, 0),
            participantId: 10,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Replication,
                Generation: 1,
                ParentActorId:
                    new BotArena.Sdk.ActorIdentity(0, 0, 0),
                SourceTransitionId: transitionId,
                SourceOperationId: new string('o', 256)));

        byte[] frame = ActorWireProtocol.EncodeGenericMatchStart(
            new string('b', 256),
            start);

        Assert.InRange(
            frame.Length,
            1,
            ActorWireProtocol.MaxHostFrameBytes);

        GenericActorResolvedMatchContract oversizedContract =
            CloneContract(
                parsed,
                new string(
                    'x',
                    GenericActorContractVersions
                        .MaxCanonicalContractBytes + 1),
                parsed.LifecycleAssignments);
        Assert.Throws<InvalidOperationException>(
            () => ActorWireProtocol.EncodeGenericMatchStart(
                new string('b', 256),
                start with { Contract = oversizedContract }));
    }

    [Theory]
    [InlineData("unknown-mode")]
    [InlineData("unknown-action")]
    [InlineData("unknown-property")]
    [InlineData("noncanonical-number")]
    [InlineData("whitespace")]
    [InlineData("bad-fingerprint")]
    [InlineData("stale-payload-fingerprints")]
    [InlineData("invalid-escape")]
    [InlineData("lone-high-surrogate")]
    [InlineData("lone-low-surrogate")]
    [InlineData("trailing-data")]
    [InlineData("duplicate-property")]
    [InlineData("trailing-comma")]
    public void StrictReaderRejectsMalformedOrUnsupportedCanonicalInput(
        string mutation)
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            GenericActorContractTestFixture.Deathmatch("head-to-head"));
        string invalid = mutation switch
        {
            "unknown-mode" => canonical.Replace(
                "\"gameMode\":{\"kind\":\"deathmatch\"",
                "\"gameMode\":{\"kind\":\"future-mode\"",
                StringComparison.Ordinal),
            "unknown-action" => canonical.Replace(
                "\"kind\":\"attack\",\"parameterKinds\"",
                "\"kind\":\"future-action\",\"parameterKinds\"",
                StringComparison.Ordinal),
            "unknown-property" => canonical.Replace(
                "{\"schemaVersion\":2,",
                "{\"schemaVersion\":2,\"future\":true,",
                StringComparison.Ordinal),
            "noncanonical-number" => canonical.Replace(
                "\"maxTicks\":100",
                "\"maxTicks\":1e2",
                StringComparison.Ordinal),
            "whitespace" => canonical.Insert(1, "\n"),
            "bad-fingerprint" => CorruptMatchFingerprint(canonical),
            "stale-payload-fingerprints" => canonical.Replace(
                "\"maxTicks\":100",
                "\"maxTicks\":101",
                StringComparison.Ordinal),
            "invalid-escape" => canonical.Replace(
                "\"mapId\":\"sdk-shared-arena\"",
                "\"mapId\":\"sdk\\qshared-arena\"",
                StringComparison.Ordinal),
            "lone-high-surrogate" => canonical.Replace(
                "\"mapId\":\"sdk-shared-arena\"",
                "\"mapId\":\"\\uD800\"",
                StringComparison.Ordinal),
            "lone-low-surrogate" => canonical.Replace(
                "\"mapId\":\"sdk-shared-arena\"",
                "\"mapId\":\"\\uDC00\"",
                StringComparison.Ordinal),
            "trailing-data" => canonical + "false",
            "duplicate-property" => canonical.Replace(
                "{\"schemaVersion\":2,",
                "{\"schemaVersion\":2,\"schemaVersion\":2,",
                StringComparison.Ordinal),
            "trailing-comma" => canonical.Insert(
                canonical.Length - 1,
                ","),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        Exception? exception = Record.Exception(
            () => ActorCanonicalContractReader.Parse(invalid));
        Assert.True(
            exception is FormatException or NotSupportedException,
            $"Expected a clean contract parse failure, got {exception?.GetType()}.");
        if (mutation is "bad-fingerprint"
            or "stale-payload-fingerprints")
        {
            Assert.Contains(
                "fingerprint",
                exception!.Message,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BoundedParserRejectsExcessiveDepthAndCollectionSize()
    {
        string tooDeep =
            new string('[', ActorWireProtocol.MaxDepth + 2)
            + "0"
            + new string(']', ActorWireProtocol.MaxDepth + 2);
        Assert.Throws<FormatException>(
            () => ActorCanonicalContractReader.Parse(tooDeep));

        string atCollectionLimit =
            "["
            + string.Join(
                ',',
                Enumerable.Repeat(
                    "0",
                    GenericActorContractVersions
                        .MaxCanonicalContractCollectionCount))
            + "]";
        FormatException atCollectionFailure =
            Assert.Throws<FormatException>(
                () => ActorCanonicalContractReader.Parse(
                    atCollectionLimit));
        Assert.Contains(
            "canonical JSON object",
            atCollectionFailure.Message,
            StringComparison.Ordinal);

        string overCollectionLimit =
            atCollectionLimit.Insert(
                atCollectionLimit.Length - 1,
                ",0");
        Assert.Contains(
            "limit",
            Assert.Throws<FormatException>(
                () => ActorCanonicalContractReader.Parse(
                    overCollectionLimit)).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalNodeProfileLimitHasExactBoundary()
    {
        string fifteenValues =
            "["
            + string.Join(',', Enumerable.Repeat("0", 15))
            + "]";
        string fourteenValues =
            "["
            + string.Join(',', Enumerable.Repeat("0", 14))
            + "]";
        string atLimit =
            "["
            + string.Join(
                ',',
                Enumerable.Repeat(
                        fifteenValues,
                        GenericActorContractVersions
                            .MaxCanonicalContractCollectionCount - 1)
                    .Append(fourteenValues))
            + "]";
        string overLimit =
            "["
            + string.Join(
                ',',
                Enumerable.Repeat(
                    fifteenValues,
                    GenericActorContractVersions
                        .MaxCanonicalContractCollectionCount))
            + "]";

        FormatException atLimitFailure = Assert.Throws<FormatException>(
            () => ActorCanonicalContractReader.Parse(atLimit));
        Assert.Contains(
            "canonical JSON object",
            atLimitFailure.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "node limit",
            Assert.Throws<FormatException>(
                () => ActorCanonicalContractReader.Parse(overLimit))
                .Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("non-kebab")]
    [InlineData("over-64-bytes")]
    public void ReaderRejectsInvalidAuthoredSemanticIds(string mutation)
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            GenericActorContractTestFixture.Deathmatch("head-to-head"));
        string invalid = mutation switch
        {
            "non-kebab" => canonical.Replace(
                "\"mapId\":\"sdk-shared-arena\"",
                "\"mapId\":\"Sdk_shared_arena\"",
                StringComparison.Ordinal),
            "over-64-bytes" => canonical.Replace(
                "\"rulesetId\":\"deathmatch-sdk-contract\"",
                $"\"rulesetId\":\"{new string('r', 65)}\"",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        FormatException exception = Assert.Throws<FormatException>(
            () => ActorCanonicalContractReader.Parse(invalid));
        Assert.Contains(
            "identifier",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WireRejectsUnknownSpawnReasonAndInvalidOriginLineage()
    {
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            GenericActorContractTestFixture.Deathmatch("head-to-head"));
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var invalidLineage = new GenericActorMatchStart
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = new BotArena.Sdk.ActorIdentity(0, 0, 1),
            ParticipantId = 10,
            ActorRandomSeed = 1,
            Origin = new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.AutomaticReturn,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = contract,
        };
        Assert.Throws<ArgumentException>(
            () => GenericActorWireContractCodec.EncodeMatchStart(
                invalidLineage));

        var origin = new ActorWireObjectWriter();
        origin.Field(1, ActorWireValue.Int32(99));
        origin.Field(2, ActorWireValue.Int32(0));
        var wire = new ActorWireObjectWriter();
        wire.Field(
            1,
            ActorWireValue.Int32(
                GenericActorContractVersions.MatchStartSchemaVersion));
        wire.Field(
            2,
            ActorWireValue.Int32(
                GenericActorContractVersions.RuntimeContractVersion));
        wire.Field(
            3,
            ActorWireContractCodec.EncodeIdentity(
                new BotArena.Sdk.ActorIdentity(0, 0, 0)));
        wire.Field(4, ActorWireValue.Int32(10));
        wire.Field(5, ActorWireValue.UInt64(1));
        wire.Field(6, origin.ToArray());
        wire.Field(
            7,
            ActorWireValue.String(
                canonical,
                ActorWireProtocol.MaxHostFrameBytes));

        Assert.Throws<FormatException>(
            () => GenericActorWireContractCodec.DecodeMatchStart(
                wire.ToArray()));
    }

    [Fact]
    public void ReadersShareExactCanonicalUtf8Boundary()
    {
        Assert.Throws<FormatException>(
            () => ActorCanonicalContractReader.ParseUtf8(
                new byte[] { 0xC3, 0x28 }));
        int maximum =
            GenericActorContractVersions.MaxCanonicalContractBytes;
        string atLimit = "\"" + new string('a', maximum - 2) + "\"";
        string overLimit =
            "\"" + new string('a', maximum - 1) + "\"";

        FormatException stringAtLimit = Assert.Throws<FormatException>(
            () => ActorCanonicalContractReader.Parse(atLimit));
        Assert.DoesNotContain(
            "profile limit",
            stringAtLimit.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "profile limit",
            Assert.Throws<FormatException>(
                () => ActorCanonicalContractReader.Parse(overLimit))
                .Message,
            StringComparison.OrdinalIgnoreCase);

        byte[] atLimitUtf8 = Encoding.UTF8.GetBytes(atLimit);
        byte[] overLimitUtf8 = Encoding.UTF8.GetBytes(overLimit);
        Assert.Equal(maximum, atLimitUtf8.Length);
        Assert.Equal(maximum + 1, overLimitUtf8.Length);
        FormatException utf8AtLimit = Assert.Throws<FormatException>(
            () => ActorCanonicalContractReader.ParseUtf8(atLimitUtf8));
        Assert.DoesNotContain(
            "profile limit",
            utf8AtLimit.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "profile limit",
            Assert.Throws<FormatException>(
                () => ActorCanonicalContractReader.ParseUtf8(overLimitUtf8))
                .Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CorruptMatchFingerprint(string canonical)
    {
        const string marker = "\"matchContractFingerprint\":\"";
        int index = canonical.IndexOf(marker, StringComparison.Ordinal)
            + marker.Length;
        Assert.True(index >= marker.Length);
        char replacement = canonical[index] == 'a' ? 'b' : 'a';
        return canonical[..index]
            + replacement
            + canonical[(index + 1)..];
    }

    private static GenericActorResolvedMatchContract Parse(
        ActorResolvedMatchDefinition source) =>
        ActorCanonicalContractReader.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(source));

    private static GenericActorMatchStart Start(
        GenericActorResolvedMatchContract contract,
        BotArena.Sdk.ActorIdentity actorId,
        int participantId,
        GenericActorMatchStart.LifeOrigin origin) =>
        new()
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = actorId,
            ParticipantId = participantId,
            ActorRandomSeed = 42,
            Origin = origin,
            Contract = contract,
        };

    private static GenericActorResolvedMatchContract CloneContract(
        GenericActorResolvedMatchContract source,
        string canonicalJson,
        ImmutableArray<
            GenericActorResolvedMatchContract.LifecycleAssignment>
            lifecycleAssignments) =>
        new(
            canonicalJson,
            source.SchemaVersion,
            source.MatchContractFingerprint,
            source.CapabilityVersions,
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            lifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding);

    private static string DuplicateReplicationTransitionAndRehash(
        string canonical)
    {
        JsonObject root = Assert.IsType<JsonObject>(
            JsonNode.Parse(canonical));
        JsonObject rules = Assert.IsType<JsonObject>(root["rules"]);
        JsonArray transitions = Assert.IsType<JsonArray>(
            rules["replicationTransitions"]);
        transitions.Add(transitions[0]!.DeepClone());
        Rehash(
            rules,
            "rulesFingerprint",
            "rulesetId",
            "rulesFingerprint");
        Rehash(
            root,
            "matchContractFingerprint",
            "matchContractFingerprint");
        return root.ToJsonString();
    }

    private static void Rehash(
        JsonObject source,
        string targetProperty,
        params string[] excludedProperties)
    {
        JsonObject payload =
            Assert.IsType<JsonObject>(source.DeepClone());
        foreach (string property in excludedProperties)
            Assert.True(payload.Remove(property));
        source[targetProperty] = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(payload.ToJsonString())));
    }

    private static byte[] HostFrame(
        ActorWireMessageType messageType,
        byte[] payload)
    {
        byte[] frame =
            new byte[ActorWireProtocol.HeaderSize + payload.Length];
        "NBV2"u8.CopyTo(frame);
        frame[4] = ActorWireProtocol.MajorVersion;
        frame[5] = (byte)messageType;
        BinaryPrimitives.WriteInt32LittleEndian(
            frame.AsSpan(8, 4),
            payload.Length);
        payload.CopyTo(frame, ActorWireProtocol.HeaderSize);
        return frame;
    }
}
