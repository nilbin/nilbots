using System.Text.Json;

namespace BotArena.Engine.Tests;

/// <summary>
/// THE NULL PIN'S FOUNDATION
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §7.2). P5 is the phase
/// allowed to stop the project: it runs the wrapped cohort on both profiles and
/// demands outcome-identity. That read is only meaningful if the engine's
/// union-once projection is the same picture the per-life path specialized —
/// otherwise the comparison is measuring two implementations rather than two
/// drivers.
/// <para>
/// These tests pin that at two levels. The first is structural and exact: the
/// per-life observation and the mind observation hold the SAME collection
/// objects, so "byte-identical" is not a comparison but an identity. The second
/// is behavioural and cross-session: the same scripted doctrine driven per life
/// and driven per mind produces a byte-identical per-tick chronology, modulo the
/// one string that MUST differ — the match-contract fingerprint, because the
/// capability tuple rides inside the fingerprinted contract.
/// </para>
/// </summary>
public sealed class GenericMindSharedProjectionEquivalenceTests
{
    [Fact]
    public void TheUnionIsOneOBJECTSharedByEveryBodyOnTheTeam()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition
                    .CreateAutomaticCompanionsExperiment());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                    GenericMindSessionTestFixture.ScriptedMind(
                        definition,
                        observation));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 2_026);

        for (int tick = 0; tick < 125; tick++)
        {
            GenericActorMatchPreparedTick prepared = session.PrepareTick();
            foreach (GenericMindRuntimeObservation mind in
                     prepared.MindObservations)
            {
                foreach (GenericActorRuntimeObservation life in
                         prepared.Observations.Where(observation =>
                             observation.Self.ActorId.TeamId == mind.TeamId))
                {
                    // Not "equal to". THE SAME. The memo measured the per-life
                    // path recomputing this N times per team per tick for a
                    // byte-identical result; there is now one computation and
                    // one object. ImmutableArray's == compares the underlying
                    // array by reference, which is precisely the claim.
                    SameArray(mind.Team.VisibleTiles, life.VisibleTiles);
                    SameArray(mind.Team.Enemies, life.Enemies);
                    SameArray(mind.Team.VisibleEvents, life.VisibleEvents);
                    SameArray(mind.Team.TeamUnits, life.TeamUnits);
                    SameArray(mind.Team.Participants, life.Participants);
                    Assert.Same(mind.Team.Scoreboard, life.Scoreboard);
                    Assert.Same(mind.Team.Mode, life.Mode);
                    Assert.Equal(
                        mind.Team.VisibleProjectiles.HasValue,
                        life.VisibleProjectiles.HasValue);
                    if (mind.Team.VisibleProjectiles.HasValue)
                    {
                        SameArray(
                            mind.Team.VisibleProjectiles!.Value,
                            life.VisibleProjectiles!.Value);
                    }
                    Assert.Equal(
                        mind.Team.HeardSounds.HasValue,
                        life.HeardSounds.HasValue);
                    if (mind.Team.HeardSounds.HasValue)
                    {
                        SameArray(
                            mind.Team.HeardSounds!.Value,
                            life.HeardSounds!.Value);
                    }
                }
            }

            // Two teams, two unions — never one per body.
            Assert.Equal(
                prepared.MindObservations
                    .Select(observation => observation.Team)
                    .Distinct()
                    .Count(),
                definition.Topology.Teams.Length);
            session.Step();
        }
    }

    [Fact]
    public void OneMindAndNPerLifeBotsProduceAByteIdenticalChronology()
    {
        ActorResolvedMatchDefinition actorDefinition =
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment();
        ActorResolvedMatchDefinition mindDefinition =
            GenericMindSessionTestFixture.OnMindProfile(actorDefinition);

        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory>
            actorFactories =
                GenericDeathmatchSessionTestFixture.Factories(
                    actorDefinition,
                    (start, observation) =>
                        GenericMindSessionTestFixture.Script(
                            actorDefinition,
                            start.ActorId,
                            observation.Tick));
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            mindFactories = GenericMindSessionTestFixture.Factories(
                mindDefinition,
                (_, observation) =>
                    GenericMindSessionTestFixture.ScriptedMind(
                        mindDefinition,
                        observation));

        const ulong seed = 31_337;
        using var actors = new GenericActorMatchSession(
            actorDefinition,
            GenericDeathmatchSessionTestFixture.Configurations(
                actorDefinition,
                actorFactories),
            seed);
        using var minds = new GenericActorMatchSession(
            mindDefinition,
            GenericMindSessionTestFixture.Configurations(
                mindDefinition,
                mindFactories),
            seed);

        const int ticks = 265;
        for (int tick = 0; tick < ticks; tick++)
        {
            Assert.False(actors.IsCompleted);
            Assert.False(minds.IsCompleted);
            actors.Step();
            minds.Step();
        }

        // The per-tick chronology is where every observation, every accepted
        // decision, every event and both authoritative world states live. If
        // this text matches, the two profiles played the same game.
        Assert.Equal(
            NormalizedTicks(actors),
            NormalizedTicks(minds));
    }

    private static void SameArray<T>(
        System.Collections.Immutable.ImmutableArray<T> expected,
        System.Collections.Immutable.ImmutableArray<T> actual) =>
        Assert.True(
            expected == actual,
            "The mind and the per-life body must read the SAME projected "
            + "collection instance, not an equal copy.");

    /// <summary>
    /// The replay's <c>ticks</c> array as canonical JSON, with the one string
    /// that MUST differ replaced. The match-contract fingerprint differs by
    /// construction because the capability tuple rides inside the
    /// fingerprinted contract — which is exactly why §7.2 states the pin as a
    /// comparator rather than a SHA-256, and why the comparator must be
    /// explicit about what it normalizes.
    /// </summary>
    private static string NormalizedTicks(GenericActorMatchSession session)
    {
        string json = ReplayV3Serializer.ToCanonicalJson(
            ReplayV3Projection.Project(session.Chronology));
        using JsonDocument document = JsonDocument.Parse(json);
        string ticks = document.RootElement
            .GetProperty("ticks")
            .GetRawText();
        return ticks
            // Differs by construction: the capability tuple rides inside the
            // fingerprinted contract, so a new profile relabels the match.
            .Replace(
                ActorContractFingerprint.ComputeMatch(session.Definition),
                "<match-contract-fingerprint>",
                StringComparison.Ordinal)
            // Differs by construction: the mind profile MINTS observation
            // schema 1 in a fresh namespace precisely so its numbers never
            // collide with the actor line's 2s.
            .Replace(
                "\"observation\":{\"schemaVersion\":"
                + session.Definition.CapabilityVersions
                    .ObservationSchemaVersion,
                "\"observation\":{\"schemaVersion\":<observation-schema>",
                StringComparison.Ordinal)
            // Same reason, on the life-start records the chronology carries:
            // MindStart is a structurally new object, so its schema and the
            // host-behaviour contract version are both fresh 1s.
            .Replace(
                "{\"schemaVersion\":"
                + session.Definition.CapabilityVersions
                    .MatchStartSchemaVersion
                + ",\"runtimeContractVersion\":"
                + session.Definition.CapabilityVersions
                    .RuntimeContractVersion,
                "{\"schemaVersion\":<match-start-schema>"
                + ",\"runtimeContractVersion\":<runtime-contract>",
                StringComparison.Ordinal);
    }
}
