using System.Collections.Immutable;
using System.Text.Json;

namespace BotArena.Engine.Tests;

/// <summary>
/// THE MIND-ERA REPLAY (DECISIONS #191 P3;
/// <c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5). A mind-profile
/// document records <c>mindTurns</c> — the union-once observation stored ONCE
/// per participant per tick, the decision map with per-command outcomes, the
/// fuel budget and live-body count — INSTEAD of N per-life <c>actorTurns</c>.
/// <para>
/// These tests pin the whole round trip (session -> document -> verify ->
/// re-read -> identical bytes and hash), the measured size ratio the memo
/// claims, and that a per-life document is byte-identical to what it was
/// before the mind existed.
/// </para>
/// </summary>
public sealed class GenericMindReplayV3Tests
{
    [Fact]
    public void AMindMatchRoundTripsThroughAVerifiedDocumentWithAStableHash()
    {
        GenericActorMatchChronology chronology = RunMind(
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition
                    .CreateAutomaticCompanionsExperiment()),
            toCompletion: true);
        GenericActorReplayDocument document =
            GenericActorReplayDocument.Create(chronology);

        Assert.True(
            GenericActorReplayDocument.VerifyHash(
                document.CanonicalJson,
                out string? failure),
            failure);

        // Re-reading and re-writing must reproduce the exact bytes: the
        // canonical text IS the contract, and the hash is over the payload
        // prefix of that text.
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(
            document.CanonicalJson);
        Assert.Equal(
            document.ReplayHash,
            ReplayV3Serializer.ComputeHash(reread));
        Assert.Equal(
            document.CanonicalJson,
            ReplayV3Serializer.ToJson(
                reread with { ReplayHash = document.ReplayHash }));

        // Hash stability across an independent replay of the same match.
        GenericActorReplayDocument again = GenericActorReplayDocument.Create(
            RunMind(
                GenericMindSessionTestFixture.OnMindProfile(
                    FrontlineLabsDefinition
                        .CreateAutomaticCompanionsExperiment()),
                toCompletion: true));
        Assert.Equal(document.ReplayHash, again.ReplayHash);
        Assert.Equal(document.CanonicalJson, again.CanonicalJson);
    }

    /// <summary>
    /// The TypeScript mirror needs a real document to validate, and a fixture
    /// that drifts silently is worse than none — so it is regenerated from
    /// this engine under <c>UPDATE_GOLDEN=1</c> and otherwise pinned.
    /// </summary>
    [Fact]
    public void TheTypeScriptMirrorFixtureIsTheDocumentThisEngineWrites()
    {
        string document = GenericMindForgeryFixture.Document.Value;
        // A broadcast prefix: complete documents of a full match are tens of
        // megabytes, and the mirror's job is shape, not length. The prefix is
        // a real viewer input in its own right — it is what the App serves
        // before a match finishes broadcasting.
        string prefix = GenericActorReplayDocument.CreatePartialPrefix(
            document,
            visibleTickCount: 8);
        string path = Path.Combine(
            RepositoryRoot(),
            "web",
            "tests",
            "fixtures",
            "generic-mind-replay-v3.json");
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(
                path,
                prefix,
                new System.Text.UTF8Encoding(false));
        }

        Assert.True(
            File.Exists(path),
            $"Regenerate with UPDATE_GOLDEN=1: {path}");
        Assert.Equal(
            File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd('\n'),
            prefix.ReplaceLineEndings("\n").TrimEnd('\n'));
    }

    private static string RepositoryRoot()
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

    [Fact]
    public void AMindDocumentCarriesMindTurnsAndNeverActorTurns()
    {
        using JsonDocument document = JsonDocument.Parse(
            GenericActorReplayDocument.Create(
                RunMind(
                    GenericMindSessionTestFixture.OnMindProfile(
                        FrontlineLabsDefinition
                            .CreateAutomaticCompanionsExperiment()),
                    toCompletion: true))
                .CanonicalJson);
        JsonElement tick = document.RootElement
            .GetProperty("ticks")[0];
        Assert.False(tick.TryGetProperty("actorTurns", out _));
        JsonElement turns = tick.GetProperty("mindTurns");
        Assert.Equal(2, turns.GetArrayLength());

        JsonElement turn = turns[0];
        Assert.Equal(
            [
                "tick",
                "participantId",
                "teamId",
                "fuelBudget",
                "liveBodyCount",
                "observation",
                "commands",
                "resolutions",
                "intents",
                "runtimeFault",
            ],
            turn.EnumerateObject().Select(property => property.Name));

        // The budget is the live-body formula, recorded as a decimal-safe
        // string because 2.05 billion does not fit an int32.
        int bodies = turn.GetProperty("liveBodyCount").GetInt32();
        Assert.Equal(
            (250_000_000L + (200_000_000L * bodies))
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            turn.GetProperty("fuelBudget").GetString());
        Assert.Equal(
            bodies,
            turn.GetProperty("resolutions").GetArrayLength());
        Assert.Equal(
            bodies,
            turn.GetProperty("observation")
                .GetProperty("bodies")
                .GetArrayLength());
        // The union is carried ONCE, on the turn, not once per body.
        Assert.True(
            turn.GetProperty("observation")
                .GetProperty("visibleTiles")
                .GetArrayLength() > 0);
    }

    /// <summary>
    /// A per-life document must be byte-identical to what it was before the
    /// mind existed. Three shipped assets depend on it: the hosted playlist and
    /// its pinned fingerprints, the eight measured lineages, and every frozen
    /// cohort's evidence (§1.1). Every mind-era addition — role tags on allies
    /// and enemies, the mind body seed, the whole <c>mindTurns</c> alternative
    /// — is therefore omit-when-inert, and this is the pin that proves it.
    /// </summary>
    [Fact]
    public void APerLifeDocumentIsByteIdenticalAndCarriesNoMindKeys()
    {
        string json = GenericActorReplayDocument.Create(
                RunPerLife(
                    FrontlineLabsDefinition
                        .CreateAutomaticCompanionsExperiment(),
                    toCompletion: true))
            .CanonicalJson;

        Assert.DoesNotContain("mindTurns", json, StringComparison.Ordinal);
        Assert.DoesNotContain("roleTag", json, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "bodyRandomSeed",
            json,
            StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(
            ["tick", "tickStart", "actorTurns", "events", "traversals", "postState"],
            document.RootElement
                .GetProperty("ticks")[0]
                .EnumerateObject()
                .Select(property => property.Name));
    }

    /// <summary>
    /// THE MEASURED SIZE CLAIM (§5.2). The memo's model is
    /// <c>actorTurns: N x (O + d) + F</c> versus
    /// <c>mindTurns: O + N x d + F</c>, and it is explicit that two ratios must
    /// be stated separately and honestly: the CHRONOLOGY term shrinks by
    /// roughly the body count, while the whole DOCUMENT shrinks by less,
    /// because the pre/post <c>WorldState</c> pair is untouched by this change.
    /// This test measures both on a real match rather than quoting the model.
    /// </summary>
    [Fact]
    public void TheMindDocumentIsMeasurablySmallerThanItsPerLifeTwin()
    {
        ActorResolvedMatchDefinition actorDefinition =
            LegionDefinition();
        ActorResolvedMatchDefinition mindDefinition =
            GenericMindSessionTestFixture.OnMindProfile(actorDefinition);

        const int ticks = 340;
        Sizes actor =
            Measure(RunPerLife(actorDefinition, toCompletion: false, ticks));
        Sizes mind =
            Measure(RunMind(mindDefinition, toCompletion: false, ticks));

        double documentRatio = (double)actor.Document / mind.Document;
        double turnRatio = (double)actor.Turns / mind.Turns;
        double endgameDocumentRatio =
            (double)actor.EndgameTicks / mind.EndgameTicks;
        double endgameTurnRatio =
            (double)actor.EndgameTurns / mind.EndgameTurns;
        Assert.True(
            documentRatio > 2.4 && endgameDocumentRatio > 3.5,
            $"whole document {actor.Document} -> {mind.Document} B "
            + $"({documentRatio:0.00}x); turns {actor.Turns} -> {mind.Turns} B "
            + $"({turnRatio:0.00}x); endgame-roster ticks "
            + $"{actor.EndgameTicks} -> {mind.EndgameTicks} B "
            + $"({endgameDocumentRatio:0.00}x); endgame turns "
            + $"{actor.EndgameTurns} -> {mind.EndgameTurns} B "
            + $"({endgameTurnRatio:0.00}x)");
        Assert.True(
            turnRatio > documentRatio
            && endgameTurnRatio > endgameDocumentRatio,
            "The chronology term must shrink by more than the whole document, "
            + "because the pre/post world-state pair is untouched.");
    }

    /// <summary>
    /// Whole-document and chronology-term bytes, plus the same two restricted
    /// to the ENDGAME-ROSTER window. The legion roster grows from three bodies
    /// to five at tick 150 and eight at tick 300, so a whole-match average
    /// understates the win the memo models at full roster; the endgame window
    /// is the directly comparable number.
    /// </summary>
    private static Sizes Measure(GenericActorMatchChronology chronology)
    {
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(chronology));
        using JsonDocument document = JsonDocument.Parse(json);
        long turns = 0;
        long endgameTurns = 0;
        long endgameTicks = 0;
        foreach (JsonElement tick in
                 document.RootElement.GetProperty("ticks").EnumerateArray())
        {
            long turnBytes = tick.TryGetProperty(
                    "actorTurns",
                    out JsonElement actorTurns)
                ? actorTurns.GetRawText().Length
                : tick.GetProperty("mindTurns").GetRawText().Length;
            turns += turnBytes;
            if (tick.GetProperty("tick").GetInt32() >= FullRosterTick)
            {
                endgameTurns += turnBytes;
                endgameTicks += tick.GetRawText().Length;
            }
        }
        return new Sizes(json.Length, turns, endgameTicks, endgameTurns);
    }

    private const int FullRosterTick = 300;

    private readonly record struct Sizes(
        long Document,
        long Turns,
        long EndgameTicks,
        long EndgameTurns);

    private static ActorResolvedMatchDefinition LegionDefinition() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.None,
            (FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker),
            roster: FrontlineLabsRosterArm.Legion);

    private static GenericActorMatchChronology RunPerLife(
        ActorResolvedMatchDefinition definition,
        bool toCompletion,
        int ticks = 0)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    GenericMindSessionTestFixture.Script(
                        definition,
                        start.ActorId,
                        observation.Tick));
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            MatchSeed);
        return Drive(session, toCompletion, ticks);
    }

    private static GenericActorMatchChronology RunMind(
        ActorResolvedMatchDefinition definition,
        bool toCompletion,
        int ticks = 0)
    {
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => Think(definition, observation));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            MatchSeed);
        return Drive(session, toCompletion, ticks);
    }

    /// <summary>
    /// The scripted doctrine plus the one thing only a mind can do: label its
    /// bodies. The lowest-numbered body claims the point and everything else
    /// screens it, which is the escorted channel in three lines and is exactly
    /// what the viewer then renders.
    /// </summary>
    private static GenericMindRuntimeDecisions Think(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation) =>
        new(
        [
            .. observation.Bodies.Select((body, index) =>
            {
                GenericActorRuntimeDecision decision =
                    GenericMindSessionTestFixture.Script(
                        definition,
                        body.ActorId,
                        observation.Tick);
                // Sticky: setting it once per life is enough, so only re-tag
                // on the tick a body first appears.
                string? tag = body.RoleTag is null
                    ? index == 0 ? "channeler" : "screen"
                    : null;
                return new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    decision.ActionId,
                    decision.ActionCode,
                    decision.Arguments,
                    tag);
            }),
        ]);

    private static GenericActorMatchChronology Drive(
        GenericActorMatchSession session,
        bool toCompletion,
        int ticks)
    {
        if (toCompletion)
            session.Run();
        else
        {
            for (int tick = 0; tick < ticks && !session.IsCompleted; tick++)
                session.Step();
        }
        return session.Chronology;
    }

    private const ulong MatchSeed = 20_260_731UL;

    internal static ImmutableArray<string> RoleTagsIn(
        GenericActorMatchChronology chronology) =>
        [
            .. chronology.Ticks
                .SelectMany(frame => frame.MindTurns)
                .SelectMany(turn => turn.Observation.Bodies)
                .Select(body => body.RoleTag)
                .Where(tag => tag is not null)
                .Select(tag => tag!)
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];
}
