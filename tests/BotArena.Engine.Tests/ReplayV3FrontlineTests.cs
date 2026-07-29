using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Engine.Tests;

public sealed class ReplayV3FrontlineTests
{
    private const string FixtureName = "generic-frontline-replay-v3.json";

    [Fact]
    public void ProjectionAndCanonicalRoundTripPreserveTypedFrontlineResult()
    {
        (ReplayV3 replay, _) =
            GenericFrontlineReplayV3TestFixture.CreateCompleteReplay();

        ReplayV3.ModeResult.Frontline frontline =
            Assert.IsType<ReplayV3.ModeResult.Frontline>(
                replay.Result!.Mode);
        Assert.Equal("max-ticks", frontline.Reason);
        Assert.Equal(1, frontline.Control.CaptureProgress);
        Assert.Equal(0, frontline.Control.ClaimingTeamId);
        Assert.Equal(
            ["1", "-1"],
            frontline.Scores
                .Select(score => score.TerritorialProgress)
                .ToArray());

        string json = ReplayV3Serializer.ToJson(replay);
        Assert.True(
            ReplayV3Serializer.VerifyHash(json, out string? failure),
            failure);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement mode = document.RootElement
            .GetProperty("result")
            .GetProperty("mode");
        Assert.Equal(
            ["kind", "reason", "control", "scores"],
            mode.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.Equal(
            [
                "kind",
                "modeId",
                "activePositionIndex",
                "claimingTeamId",
                "captureProgress",
                "decayTicksElapsed",
                "controlResumesAtTick",
                "holdOwnerTeamId",
                "holdEndsAtTick",
            ],
            mode.GetProperty("control")
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.Equal(
            ["teamId", "territorialProgress"],
            mode.GetProperty("scores")[0]
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.False(mode.GetProperty("control").TryGetProperty(
            "nextTick",
            out _));
        Assert.False(mode.GetProperty("control").TryGetProperty(
            "winnerTeamId",
            out _));
    }

    [Fact]
    public void ProjectionMapsEveryFrontlineTerminalReasonTag()
    {
        ReplayV3 maxTicks =
            GenericFrontlineReplayV3TestFixture.CreateReplay(
                GenericFrontlineReplayV3TestFixture.Definition());
        ReplayV3 baseBreach =
            GenericFrontlineReplayV3TestFixture.CreateReplay(
                GenericFrontlineReplayV3TestFixture.Definition(
                    maxTicks: 3,
                    quickBreach: true),
                (start, observation) =>
                    start.ParticipantId == 10
                    && observation.Tick == 1
                        ? GenericDeathmatchSessionTestFixture.Move(
                            Direction.East)
                        : GenericDeathmatchSessionTestFixture.Wait());
        ReplayV3 faultEligibility =
            GenericFrontlineReplayV3TestFixture.CreateReplay(
                GenericFrontlineReplayV3TestFixture.Definition(
                    maxTicks: 3),
                (start, _) => start.ParticipantId == 10
                    ? GenericDeathmatchSessionTestFixture.Unknown()
                    : GenericDeathmatchSessionTestFixture.Wait());

        Assert.Equal(
            "max-ticks",
            Assert.IsType<ReplayV3.ModeResult.Frontline>(
                maxTicks.Result!.Mode).Reason);
        Assert.Equal(
            "base-breach",
            Assert.IsType<ReplayV3.ModeResult.Frontline>(
                baseBreach.Result!.Mode).Reason);
        Assert.Equal(
            "fault-eligibility",
            Assert.IsType<ReplayV3.ModeResult.Frontline>(
                faultEligibility.Result!.Mode).Reason);
        Assert.All(
            new[] { maxTicks, baseBreach, faultEligibility },
            replay => Assert.True(
                ReplayV3Serializer.VerifyHash(
                    ReplayV3Serializer.ToJson(replay),
                    out string? failure),
                failure));
    }

    [Fact]
    public void VerificationRejectsTerminalControlAndSignedScoreDrift()
    {
        string json = CompleteJson();
        string controlDrift = MutateAndRehash(
            json,
            root => root["result"]!["mode"]!["control"]![
                "captureProgress"] = 2);
        string signedScoreDrift = MutateAndRehash(
            json,
            root => root["result"]!["mode"]!["scores"]![0]![
                "territorialProgress"] = "-1");
        string unsafeWireNumber = MutateAndRehash(
            json,
            root => root["result"]!["mode"]!["scores"]![0]![
                "territorialProgress"] = -1);

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                controlDrift,
                out string? controlFailure));
        Assert.Contains(
            "terminal control",
            controlFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                signedScoreDrift,
                out string? scoreFailure));
        Assert.Contains(
            "territorial scores",
            scoreFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                unsafeWireNumber,
                out string? wireFailure));
        Assert.Contains(
            "decimal-safe string",
            wireFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationRejectsFrontlineControlBoundViolations()
    {
        string json = CompleteJson();
        Action<JsonObject>[] mutations =
        [
            root => InitialControl(root)["activePositionIndex"] = 5,
            root => InitialControl(root)["captureProgress"] = 1,
            root =>
            {
                JsonObject control = InitialControl(root);
                control["claimingTeamId"] = 0;
                control["captureProgress"] = 1;
                control["decayTicksElapsed"] = 2;
            },
            root => InitialControl(root)["controlResumesAtTick"] = 2,
        ];

        foreach (Action<JsonObject> mutation in mutations)
        {
            string invalid = MutateAndRehash(json, mutation);
            Assert.False(
                ReplayV3Serializer.VerifyHash(
                    invalid,
                    out string? failure));
            Assert.Contains(
                "capture bounds",
                failure,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void VerificationRejectsIllegalFrontlineReasonAndRanks()
    {
        string json = CompleteJson();
        string illegalReason = MutateAndRehash(
            json,
            root =>
            {
                root["result"]!["completionReason"] = "base-breach";
                root["result"]!["mode"]!["reason"] = "base-breach";
            });
        string illegalRanks = MutateAndRehash(
            json,
            root =>
            {
                JsonObject standings =
                    root["result"]!["standings"]!.AsObject();
                standings["winnerTeamId"] = null;
                JsonArray teams = standings["teams"]!.AsArray();
                teams[0]!["rank"] = 1;
                teams[0]!["outcome"] = "draw";
                teams[1]!["rank"] = 1;
                teams[1]!["outcome"] = "draw";
            });

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                illegalReason,
                out string? reasonFailure));
        Assert.Contains(
            "not legal",
            reasonFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                illegalRanks,
                out string? rankFailure));
        Assert.Contains(
            "victory policy",
            rankFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationRejectsTicksPastEmbeddedMaximum()
    {
        (ReplayV3 replay, _) =
            GenericFrontlineReplayV3TestFixture.CreateCompleteReplay();
        ReplayV3.TickFrame extra = replay.Ticks[^1] with
        {
            Tick = replay.Ticks.Length,
        };
        ReplayV3 overlong = replay with
        {
            Ticks = replay.Ticks.Add(extra),
            ReplayHash = null,
        };

        ArgumentException failure = Assert.Throws<ArgumentException>(
            () => ReplayV3Serializer.ToJson(overlong));
        Assert.Contains(
            "maximum tick boundary",
            failure.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationRejectsBaseBreachDuringRedeployPause()
    {
        ActorResolvedMatchDefinition definition =
            GenericFrontlineReplayV3TestFixture.Definition(
                maxTicks: 3,
                quickBreach: true,
                quickBreachRedeployPauseTicks: 1);
        ReplayV3 replay =
            GenericFrontlineReplayV3TestFixture.CreateReplay(
                definition,
                (start, observation) =>
                    start.ParticipantId == 10
                    && observation.Tick == 1
                        ? GenericDeathmatchSessionTestFixture.Move(
                            Direction.East)
                        : GenericDeathmatchSessionTestFixture.Wait());
        string pausedBreach = MutateAndRehash(
            ReplayV3Serializer.ToJson(replay),
            root =>
            {
                JsonObject postState = root["ticks"]!
                    .AsArray()[^1]!["postState"]!
                    .AsObject();
                int resumesAt =
                    postState["nextTick"]!.GetValue<int>() + 1;
                postState["mode"]!["controlResumesAtTick"] =
                    resumesAt;
                root["result"]!["mode"]!["control"]![
                    "controlResumesAtTick"] = resumesAt;
            });

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                pausedBreach,
                out string? failure));
        Assert.Contains(
            "not legal",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationRejectsUnknownTerminalArmsAndFields()
    {
        string json = CompleteJson();
        string unknownArm = MutateAndRehash(
            json,
            root => root["result"]!["mode"]!["kind"] = "future-mode");
        string unknownField = MutateAndRehash(
            json,
            root => root["result"]!["mode"]!["winnerTeamId"] = 0);

        Assert.False(
            ReplayV3Serializer.VerifyHash(
                unknownArm,
                out string? armFailure));
        Assert.Contains(
            "unknown",
            armFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            ReplayV3Serializer.VerifyHash(
                unknownField,
                out string? fieldFailure));
        Assert.Contains(
            "exactly",
            fieldFailure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EngineAuthoredFrontlineDocumentMatchesCheckedInFixture()
    {
        string actual = CompleteJson();
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "BotArena.Engine.Tests",
            "Fixtures",
            FixtureName);
        if (string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_GOLDEN"),
                "1",
                StringComparison.Ordinal))
        {
            File.WriteAllText(
                path,
                actual + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        Assert.True(
            File.Exists(path),
            $"Missing {FixtureName}.");
        string fixture = File.ReadAllText(path);
        Assert.EndsWith("\n", fixture, StringComparison.Ordinal);
        string canonicalFixture = fixture[..^1];
        Assert.Equal(canonicalFixture, actual);
        Assert.True(
            ReplayV3Serializer.VerifyHash(
                canonicalFixture,
                out string? failure),
            failure);
    }

    private static string CompleteJson()
    {
        (ReplayV3 replay, _) =
            GenericFrontlineReplayV3TestFixture.CreateCompleteReplay();
        return ReplayV3Serializer.ToJson(replay);
    }

    private static JsonObject InitialControl(JsonObject root) =>
        root["initialFrame"]!["state"]!["mode"]!.AsObject();

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
        string hash = Convert.ToHexStringLower(
            SHA256.HashData(stream.ToArray()));
        root["replayHash"] = hash;
        return root.ToJsonString();
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
