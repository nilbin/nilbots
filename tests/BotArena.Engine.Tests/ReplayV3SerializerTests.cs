using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Engine.Tests;

/// <summary>
/// Freezes the replay-v3 envelope, canonical payload, safe integer encoding,
/// and engine-authored golden bytes without exercising historical codecs.
/// Set UPDATE_GOLDEN=1 deliberately to regenerate the fixture.
/// </summary>
public sealed class ReplayV3SerializerTests
{
    private const ulong FixtureSeed = 9_007_199_254_740_993UL;
    private const string FixtureName = "generic-replay-v3.json";
    private const string FixtureReplayHash =
        "247ce067013314dacff84025fc656cebf0c9f9acc12d48c00f4dadddea836e89";

    [Fact]
    public void CompleteDocument_HasExactEnvelopeAndVerifiablePayloadHash()
    {
        (ReplayV3 replay, ActorResolvedMatchDefinition definition) =
            CreateCompleteReplay();

        string payload = ReplayV3Serializer.ToCanonicalJson(replay);
        string expectedHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        string json = ReplayV3Serializer.ToJson(replay);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement header = root.GetProperty("header");

        Assert.Equal(
            [
                "header",
                "initialFrame",
                "ticks",
                "result",
                "replayHash",
                "partial",
            ],
            root.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.False(root.GetProperty("partial").GetBoolean());
        Assert.Equal(
            expectedHash,
            root.GetProperty("replayHash").GetString());
        Assert.Equal(expectedHash, ReplayV3Serializer.ComputeHash(replay));
        Assert.True(
            ReplayV3Serializer.VerifyHash(json, out string? failure),
            failure);
        Assert.Null(failure);
        Assert.Equal(
            FixtureSeed.ToString(CultureInfo.InvariantCulture),
            header.GetProperty("seed").GetString());
        Assert.Equal(
            JsonValueKind.String,
            header.GetProperty("seed").ValueKind);
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(definition),
            header.GetProperty("contract").GetRawText());
        Assert.Equal(
            JsonValueKind.Null,
            header.GetProperty("presentation").ValueKind);

        JsonElement initialState = root
            .GetProperty("initialFrame")
            .GetProperty("state");
        Assert.Equal(
            replay.Header.Contract.MatchContractFingerprint,
            initialState
                .GetProperty("matchContractFingerprint")
                .GetString());
        JsonElement observation = root
            .GetProperty("ticks")[0]
            .GetProperty("actorTurns")[0]
            .GetProperty("observation");
        Assert.Equal(
            JsonValueKind.Null,
            observation.GetProperty("heardSounds").ValueKind);
        Assert.Equal(
            JsonValueKind.Array,
            observation.GetProperty("visibleProjectiles").ValueKind);
        Assert.Empty(
            observation.GetProperty("visibleProjectiles")
                .EnumerateArray());
    }

    [Fact]
    public void PartialDocument_IsExplicitlyUnhashed()
    {
        ActorResolvedMatchDefinition definition = Definition();
        using var session = Session(definition);
        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);

        string json = ReplayV3Serializer.ToJson(replay);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.True(root.GetProperty("partial").GetBoolean());
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("result").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("replayHash").ValueKind);
        Assert.Empty(root.GetProperty("ticks").EnumerateArray());
        Assert.False(
            ReplayV3Serializer.VerifyHash(json, out string? failure));
        Assert.Contains(
            "intentionally unhashed",
            failure,
            StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(
            () => ReplayV3Serializer.ComputeHash(replay));
    }

    [Fact]
    public void Verification_RejectsCanonicalPayloadTampering()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string tampered = json.Replace(
            $"\"seed\":\"{FixtureSeed.ToString(CultureInfo.InvariantCulture)}\"",
            "\"seed\":\"1\"",
            StringComparison.Ordinal);

        Assert.NotEqual(json, tampered);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                tampered,
                out string? failure));
        Assert.Contains(
            "hash",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsReorderedNestedPayloadWithFreshHash()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string reordered = json.Replace(
            "\"actorId\":{\"teamId\":0,\"unitId\":0,\"lifeId\":0}",
            "\"actorId\":{\"unitId\":0,\"teamId\":0,\"lifeId\":0}",
            StringComparison.Ordinal);
        Assert.NotEqual(json, reordered);
        reordered = Rehash(reordered);

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                reordered,
                out string? failure));
        Assert.Contains(
            "canonical",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsReorderedKeyedArrayWithFreshHash()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        JsonArray participants = root["initialFrame"]!["state"]![
                "participants"]!
            .AsArray();
        JsonNode first = participants[0]!.DeepClone();
        JsonNode second = participants[1]!.DeepClone();
        participants[0] = second;
        participants[1] = first;
        string reordered = Rehash(root.ToJsonString());

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                reordered,
                out string? failure));
        Assert.Contains(
            "canonical order",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsDuplicatePropertiesAndNullRequiredValues()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string duplicate = ReplaceFirst(
            json,
            "\"partial\":false}",
            "\"partial\":false,\"partial\":false}");
        string nullEngineVersion = Rehash(ReplaceFirst(
            json,
            $"\"engineVersion\":\"{replay.Header.EngineVersion}\"",
            "\"engineVersion\":null"));

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                duplicate,
                out string? duplicateFailure));
        Assert.Contains(
            "duplicate",
            duplicateFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                nullEngineVersion,
                out string? nullFailure));
        Assert.NotNull(nullFailure);
    }

    [Fact]
    public void Verification_RejectsNoncanonicalContractRootWithFreshHash()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string canonicalPrefix =
            $"\"contract\":{{\"schemaVersion\":{replay.Header.Contract.SchemaVersion}," +
            $"\"matchContractFingerprint\":\"{replay.Header.Contract.MatchContractFingerprint}\"";
        string reorderedPrefix =
            $"\"contract\":{{\"matchContractFingerprint\":\"{replay.Header.Contract.MatchContractFingerprint}\"," +
            $"\"schemaVersion\":{replay.Header.Contract.SchemaVersion}";
        string reordered = Rehash(ReplaceFirst(
            json,
            canonicalPrefix,
            reorderedPrefix));

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                reordered,
                out string? failure));
        Assert.Contains(
            "match contract",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsEmbeddedContractPayloadMutation()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string mutated = Rehash(ReplaceFirst(
            json,
            "\"limits\":{\"maxTicks\":2",
            "\"limits\":{\"maxTicks\":3"));

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                mutated,
                out string? failure));
        Assert.Contains(
            "fingerprint",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsReorderedNestedContractWithForgedFingerprints()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        JsonObject root = JsonNode.Parse(
            ReplayV3Serializer.ToJson(replay))!.AsObject();
        JsonObject contract = root["header"]!["contract"]!.AsObject();
        JsonObject rules = contract["rules"]!.AsObject();
        JsonObject limits = rules["limits"]!.AsObject();
        JsonNode maxTicks = limits["maxTicks"]!.DeepClone();
        JsonNode runtimeFaults = limits["runtimeFaults"]!.DeepClone();
        limits.Clear();
        limits["runtimeFaults"] = runtimeFaults;
        limits["maxTicks"] = maxTicks;

        rules["rulesFingerprint"] = FingerprintObject(
            rules,
            "rulesetId",
            "rulesFingerprint");
        string oldMatchFingerprint =
            contract["matchContractFingerprint"]!.GetValue<string>();
        string newMatchFingerprint = FingerprintObject(
            contract,
            "matchContractFingerprint");
        contract["matchContractFingerprint"] = newMatchFingerprint;
        string forged = root.ToJsonString().Replace(
            oldMatchFingerprint,
            newMatchFingerprint,
            StringComparison.Ordinal);
        forged = Rehash(forged);

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                forged,
                out string? failure));
        Assert.Contains(
            "canonical property",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsInvalidClosedVocabularyAndEventTagPair()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string invalidFacing = Rehash(ReplaceFirst(
            json,
            "\"position\":{\"x\":1,\"y\":3},\"facing\":\"east\",\"health\":3",
            "\"position\":{\"x\":1,\"y\":3},\"facing\":\"diagonal\",\"health\":3"));
        string mismatchedEvent = Rehash(ReplaceFirst(
            json,
            "\"kind\":\"life-spawned\",\"payload\":{\"kind\":\"life-spawned\"",
            "\"kind\":\"damage\",\"payload\":{\"kind\":\"life-spawned\""));

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                invalidFacing,
                out string? facingFailure));
        Assert.Contains(
            "facing",
            facingFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                mismatchedEvent,
                out string? eventFailure));
        Assert.Contains(
            "disagree",
            eventFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verification_RejectsTerminalFactsThatContradictFinalWorld()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay();
        string json = ReplayV3Serializer.ToJson(replay);
        string eligibleMutation = MutateAndRehash(
            json,
            root =>
            {
                JsonArray eligible = root["result"]![
                        "eligibleTeamIds"]!
                    .AsArray();
                eligible.RemoveAt(eligible.Count - 1);
            });
        string unitMutation = MutateAndRehash(
            json,
            root =>
            {
                JsonObject life = root["result"]!["units"]![0]![
                        "activeLife"]!
                    .AsObject();
                life["health"] = life["health"]!.GetValue<int>() - 1;
            });
        string scoreMutation = MutateAndRehash(
            json,
            root =>
            {
                JsonObject score = root["result"]!["mode"]!["scores"]![
                        0]!
                    .AsObject();
                score["kills"] = "99";
            });
        string rankingMutation = MutateAndRehash(
            json,
            root =>
            {
                JsonObject standings = root["result"]!["standings"]!
                    .AsObject();
                standings["winnerTeamId"] = 0;
                JsonArray teams = standings["teams"]!.AsArray();
                teams[0]!["rank"] = 1;
                teams[0]!["outcome"] = "win";
                teams[1]!["rank"] = 2;
                teams[1]!["outcome"] = "loss";
            });

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                eligibleMutation,
                out string? eligibleFailure));
        Assert.Contains(
            "eligible",
            eligibleFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                unitMutation,
                out string? unitFailure));
        Assert.Contains(
            "terminal life",
            unitFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                scoreMutation,
                out string? scoreFailure));
        Assert.Contains(
            "counters",
            scoreFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                rankingMutation,
                out string? rankingFailure));
        Assert.Contains(
            "victory policy",
            rankingFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presentation_IsCanonicalReplayMetadataOutsideGameplayContract()
    {
        var presentation = new ReplayV3.PresentationMetadata(
            "nebula",
            new ReplayV3.MapPresentationMetadata(
                "boundary",
                "interior",
                [
                    new(
                        "zeta",
                        [new(2, 3), new(1, 1)]),
                    new(
                        "alpha",
                        [new(4, 2), new(3, 2)]),
                ]),
            [
                new("turret", "turret-look", null),
                new("mobile", null, "pulse"),
            ]);
        (ReplayV3 replay, ActorResolvedMatchDefinition definition) =
            CreateCompleteReplay(presentation);
        (ReplayV3 canonicalOrder, _) = CreateCompleteReplay(
            new ReplayV3.PresentationMetadata(
                "nebula",
                new ReplayV3.MapPresentationMetadata(
                    "boundary",
                    "interior",
                    [
                        new(
                            "alpha",
                            [new(3, 2), new(4, 2)]),
                        new(
                            "zeta",
                            [new(1, 1), new(2, 3)]),
                    ]),
                [
                    new("mobile", null, "pulse"),
                    new("turret", "turret-look", null),
                ]));
        ReplayV3 withoutPresentation = replay with
        {
            Header = replay.Header with { Presentation = null },
        };

        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(definition),
            replay.Header.Contract.CanonicalJson);
        Assert.Equal(
            withoutPresentation.Header.Contract,
            replay.Header.Contract);
        Assert.Equal(
            ["alpha", "zeta"],
            replay.Header.Presentation!.Map!.WallGroups
                .Select(group => group.Family)
                .ToArray());
        Assert.Equal(
            [new ReplayV3.PositionValue(1, 1), new(2, 3)],
            replay.Header.Presentation.Map.WallGroups[1].Tiles
                .ToArray());
        Assert.Equal(
            ["mobile", "turret"],
            replay.Header.Presentation.Forms
                .Select(form => form.FormId)
                .ToArray());
        Assert.Equal(
            ReplayV3Serializer.ComputeHash(canonicalOrder),
            ReplayV3Serializer.ComputeHash(replay));
        Assert.NotEqual(
            ReplayV3Serializer.ComputeHash(withoutPresentation),
            ReplayV3Serializer.ComputeHash(replay));

        using JsonDocument document = JsonDocument.Parse(
            ReplayV3Serializer.ToJson(replay));
        JsonElement header = document.RootElement.GetProperty("header");
        Assert.Equal(
            replay.Header.Contract.CanonicalJson,
            header.GetProperty("contract").GetRawText());
        Assert.Equal(
            "nebula",
            header.GetProperty("presentation")
                .GetProperty("themeId")
                .GetString());
    }

    [Fact]
    public void Presentation_RejectsAmbiguousIdentifiersAndTileOwnership()
    {
        ActorResolvedMatchDefinition definition = Definition();
        using GenericDeathmatchSession session = Session(definition);

        void AssertRejected(ReplayV3.PresentationMetadata presentation) =>
            Assert.Throws<ArgumentException>(
                () => ReplayV3Projection.Project(
                    session.Chronology,
                    presentation));

        AssertRejected(
            new ReplayV3.PresentationMetadata(
                null,
                null,
                [
                    new("mobile", null, null),
                    new("mobile", "alternate", null),
                ]));
        AssertRejected(
            new ReplayV3.PresentationMetadata(
                null,
                new ReplayV3.MapPresentationMetadata(
                    " ",
                    "interior",
                    []),
                []));
        AssertRejected(
            new ReplayV3.PresentationMetadata(
                null,
                new ReplayV3.MapPresentationMetadata(
                    "boundary",
                    "interior",
                    [
                        new("hazard", []),
                        new("hazard", [new(1, 1)]),
                    ]),
                []));
        AssertRejected(
            new ReplayV3.PresentationMetadata(
                null,
                new ReplayV3.MapPresentationMetadata(
                    "boundary",
                    "interior",
                    [
                        new("alpha", [new(1, 1)]),
                        new("beta", [new(1, 1)]),
                    ]),
                []));
    }

    [Fact]
    public void RawRejectedDecision_PreservesDefaultArgumentsAndInvalidEnum()
    {
        ActorResolvedMatchDefinition definition = Definition();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? new GenericActorRuntimeDecision(
                        null!,
                        999,
                        default,
                        "default-array")
                    : new GenericActorRuntimeDecision(
                        "move",
                        1,
                        [
                            new GenericActorRuntimeActionArgument
                                .DirectionArgument((Direction)999),
                            null!,
                        ],
                        "malformed-enum"));
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            FixtureSeed);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        string json = ReplayV3Serializer.ToJson(replay);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement[] decisions = document.RootElement
            .GetProperty("ticks")[0]
            .GetProperty("actorTurns")
            .EnumerateArray()
            .Select(turn => turn.GetProperty("submittedDecision"))
            .ToArray();

        JsonElement defaultArguments = decisions.Single(decision =>
            decision.GetProperty("debugMessage").GetString()
                == "default-array");
        Assert.Equal(
            JsonValueKind.Null,
            defaultArguments.GetProperty("actionId").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            defaultArguments.GetProperty("arguments").ValueKind);

        JsonElement malformed = decisions.Single(decision =>
            decision.GetProperty("debugMessage").GetString()
                == "malformed-enum");
        JsonElement[] arguments = malformed.GetProperty("arguments")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(999, arguments[0].GetProperty("value").GetInt32());
        Assert.Equal(JsonValueKind.Null, arguments[1].ValueKind);
    }

    [Fact]
    public void OptionalShotProgram_MayBeOmittedInEngineAuthoredReplay()
    {
        ActorResolvedMatchDefinition definition = Definition();
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, _) => start.ParticipantId == 10
                    ? new GenericActorRuntimeDecision(
                        "shoot",
                        4,
                        [],
                        null)
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            FixtureSeed);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        ReplayV3.ActorTurn turn = replay.Ticks[0].ActorTurns.Single(
            value => value.ParticipantId == 10);
        Assert.Empty(turn.ActionResolution.AcceptedAction.Arguments);
        Assert.Empty(turn.ActionResolution.ValidatedAction.Arguments);
        string json = ReplayV3Serializer.ToJson(replay);
        Assert.True(
            ReplayV3Serializer.VerifyHash(
                json,
                out string? failure),
            failure);
    }

    [Fact]
    public void SplitReservations_RequireCanonicalContractAssignment()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 4,
                    IncludeSplit = true,
                    SplitDurationTicks = 2,
                });
        var sourceActor = new ActorIdentity(0, 0, 0);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ActorId == sourceActor
                    && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            FixtureSeed);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        string json = ReplayV3Serializer.ToJson(replay);
        Assert.True(
            ReplayV3Serializer.VerifyHash(
                json,
                out string? validFailure),
            validFailure);

        string reordered = MutateAndRehash(
            json,
            root =>
            {
                JsonArray descendants = root["ticks"]![0]![
                        "postState"]!["pendingReplications"]![0]![
                        "descendants"]!
                    .AsArray();
                JsonNode first = descendants[0]!.DeepClone();
                descendants[0] = descendants[1]!.DeepClone();
                descendants[1] = first;
            });
        string duplicated = MutateAndRehash(
            json,
            root =>
            {
                JsonArray descendants = root["ticks"]![0]![
                        "postState"]!["pendingReplications"]![0]![
                        "descendants"]!
                    .AsArray();
                descendants[1] = descendants[0]!.DeepClone();
            });

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                reordered,
                out string? reorderedFailure));
        Assert.Contains(
            "split",
            reorderedFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                duplicated,
                out string? duplicatedFailure));
        Assert.Contains(
            "split",
            duplicatedFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EngineAuthoredDocument_MatchesCheckedInGoldenBytes()
    {
        (ReplayV3 replay, _) = CreateCompleteReplay(
            new ReplayV3.PresentationMetadata(
                "fixture-theme",
                new ReplayV3.MapPresentationMetadata(
                    "boundary",
                    "interior",
                    [new("hazard", [new(3, 1)])]),
                [new("mobile", "fixture-look", "fixture-bolt")]));
        string actual = ReplayV3Serializer.ToJson(replay);
        AssertOrUpdateFixture(FixtureName, actual);
        Assert.True(
            ReplayV3Serializer.VerifyHash(
                actual,
                out string? failure),
            failure);

        using JsonDocument document = JsonDocument.Parse(actual);
        Assert.Equal(
            ReplayV3Serializer.ComputeHash(replay),
            document.RootElement.GetProperty("replayHash").GetString());
        Assert.Equal(
            FixtureReplayHash,
            document.RootElement.GetProperty("replayHash").GetString());
    }

    private static (
        ReplayV3 Replay,
        ActorResolvedMatchDefinition Definition)
        CreateCompleteReplay(
            ReplayV3.PresentationMetadata? presentation = null)
    {
        ActorResolvedMatchDefinition definition = Definition();
        using GenericDeathmatchSession session = Session(definition);
        session.Run();
        return (
            ReplayV3Projection.Project(
                session.Chronology,
                presentation),
            definition);
    }

    private static ActorResolvedMatchDefinition Definition() =>
        GenericDeathmatchSessionTestFixture.Definition(
            "head-to-head",
            new GenericDeathmatchSessionTestFixture.Options
            {
                MaxTicks = 2,
            });

    private static GenericDeathmatchSession Session(
        ActorResolvedMatchDefinition definition)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) => observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        return new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories,
                reverse: true),
            FixtureSeed);
    }

    private static void AssertOrUpdateFixture(
        string fixtureName,
        string actual)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "BotArena.Engine.Tests",
            "Fixtures",
            fixtureName);
        if (string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_GOLDEN"),
                "1",
                StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(false));
        }

        Assert.True(
            File.Exists(path),
            $"Missing {fixtureName}. Regenerate deliberately with UPDATE_GOLDEN=1.");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static string Rehash(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (string propertyName in
                     new[] { "header", "initialFrame", "ticks", "result" })
            {
                writer.WritePropertyName(propertyName);
                root.GetProperty(propertyName).WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        string hash = Convert.ToHexStringLower(
            SHA256.HashData(stream.ToArray()));
        string oldHash =
            root.GetProperty("replayHash").GetString()!;
        return json.Replace(
            $"\"replayHash\":\"{oldHash}\"",
            $"\"replayHash\":\"{hash}\"",
            StringComparison.Ordinal);
    }

    private static string ReplaceFirst(
        string value,
        string oldValue,
        string newValue)
    {
        int index = value.IndexOf(
            oldValue,
            StringComparison.Ordinal);
        Assert.True(index >= 0, $"Missing mutation target: {oldValue}");
        return string.Concat(
            value.AsSpan(0, index),
            newValue,
            value.AsSpan(index + oldValue.Length));
    }

    private static string MutateAndRehash(
        string json,
        Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return Rehash(root.ToJsonString());
    }

    private static string FingerprintObject(
        JsonObject value,
        params string[] excludedProperties)
    {
        HashSet<string> excluded = excludedProperties.ToHashSet(
            StringComparer.Ordinal);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach ((string name, JsonNode? propertyValue) in value)
            {
                if (excluded.Contains(name))
                    continue;
                writer.WritePropertyName(name);
                propertyValue!.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Convert.ToHexStringLower(
            SHA256.HashData(stream.ToArray()));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
