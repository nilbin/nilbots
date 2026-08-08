using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Engine.Tests;

/// <summary>
/// MUSTER driven live through a whole scripted match on the real arm: one
/// Prime walks the widened alcove, takes the flag, comes back into the enemy
/// lane and dies — and its respawn lands beside the fight instead of at home,
/// purely because the flag was still held at the respawn tick. The same
/// script without the errand walks home, which is the A/B the effect claims.
/// </summary>
public sealed class FrontlineLabsMusterSessionTests
{
    private static readonly Position NorthSite = new(11, 2);
    private static readonly Position KillingGround = new(14, 7);
    private static readonly Position TeamZeroHome = new(2, 7);

    /// <summary>
    /// The route out of the middle band into the north alcove. Under the
    /// muster map the last leg exists at all only because the shoulders are
    /// open; on the classes map (10,3) is wall.
    /// </summary>
    private static readonly ImmutableArray<Position> ToSite =
    [
        new(10, 7),
        new(10, 3),
        new(11, 3),
        NorthSite,
    ];

    private static readonly ImmutableArray<Position> ToKillingGround =
    [
        new(11, 3),
        new(10, 3),
        new(10, 7),
        KillingGround,
    ];

    [Fact]
    public void TheFlagMovesThePrimeRespawnAndLosingItDoesNot()
    {
        (Position Arrival, int Tick, int? Owner) held = FirstRespawn(
            Run(errand: true));
        (Position Arrival, int Tick, int? Owner) home = FirstRespawn(
            Run(errand: false));

        // Owning the flag at the respawn tick puts the new Prime on the
        // own-side chain-adjacent objective, measured along team 0's own
        // advance direction.
        Assert.Equal(0, held.Owner);
        Assert.Equal(new Position(6, 5), held.Arrival);
        Assert.NotEqual(TeamZeroHome, held.Arrival);

        // The identical script without the errand never owns the flag, so
        // the same death walks home from the reserved spawn.
        Assert.Null(home.Owner);
        Assert.Equal(TeamZeroHome, home.Arrival);
    }

    /// <summary>
    /// The replay carries the facts: ownership arrives as an ordinary
    /// mode-changed observation (the ratchet hold's precedent — the mode
    /// state IS the payload), the flip tick is visible in it, and the
    /// document verifies.
    /// </summary>
    [Fact]
    public void TheReplayCarriesTheOwnershipFlipAndVerifies()
    {
        GenericActorMatchChronology chronology = Run(errand: true);
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(chronology));

        Assert.True(
            ReplayV3Serializer.VerifyHash(json, out string? failure),
            failure);
        Assert.Contains(
            "\"secondaryOwnerTeamId\":0",
            json,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"secondaryClaimProgress\":1",
            json,
            StringComparison.Ordinal);

        // The flip is a mode-changed fact, so a replay reader sees the exact
        // tick the flag turned over without diffing anything itself.
        GenericActorMatchTickFrame flip = chronology.Ticks.First(frame =>
            Mode(frame.PostState).SecondaryOwnerTeamId is not null);
        Assert.Contains(
            flip.Events,
            item => item.Kind
                == GenericActorRuntimeObservation.EventKind.ModeChanged
                && ((GenericActorRuntimeObservation.EventPayload.ModeChanged)
                        item.Payload).State
                    is GenericActorRuntimeObservation.ModeObservationState
                        .Frontline { SecondaryOwnerTeamId: 0 });
        // Exactly the declared latch: the claim reached the threshold on the
        // tick before, and the site was never contested on the way.
        Assert.Equal(
            FrontlineLabsMusterSite.LatchTicks - 1,
            Mode(chronology.Ticks
                .First(frame => frame.Tick == flip.Tick - 1)
                .PostState)
                .SecondaryClaimProgress);
    }

    /// <summary>
    /// Document-level refusals. Each mutation below is internally consistent
    /// JSON with a correct payload hash; the only thing wrong with it is that
    /// the embedded contract could not have published that flag.
    /// <para>The layering is the ratchet hold's: the document verifier checks
    /// the published state against the embedded contract's own bounds — the
    /// team domain, the threshold, and the owner/claimant rule — while a flag
    /// credited to the WRONG real team is bounds-legal here and refused one
    /// layer up by the chronology validator's kernel re-derivation, which
    /// recomputes the latch from the recorded bodies.</para>
    /// </summary>
    [Fact]
    public void VerificationRejectsFlagsTheEmbeddedContractCannotPublish()
    {
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(Run(errand: true)));
        int owned = OwnedTick(json);

        Action<JsonObject>[] mutations =
        [
            // An owner outside the scoring-team domain.
            root => PostControl(root, owned)["secondaryOwnerTeamId"] = 7,
            // The owner claiming the site it already holds.
            root => PostControl(root, owned)["secondaryClaimProgress"] = 3,
            // A claim at or past the threshold, which would have latched.
            root =>
            {
                JsonObject control = PostControl(root, owned);
                control["secondaryOwnerTeamId"] = null;
                control["secondaryClaimProgress"] =
                    FrontlineLabsMusterSite.LatchTicks;
            },
        ];
        foreach (Action<JsonObject> mutation in mutations)
        {
            Assert.False(
                ReplayV3Serializer.VerifyHash(
                    MutateAndRehash(json, mutation),
                    out string? failure),
                "a forged side objective verified");
            Assert.Contains(
                "capture bounds",
                failure,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A ruleset that declares no side objective may not publish one at all —
    /// the same "carried by exactly what declares it" rule the contract's own
    /// block follows, applied to the observation.
    /// </summary>
    [Fact]
    public void VerificationRejectsAFlagOnARulesetWithoutASideObjective()
    {
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(
                FrontlineLabsSkillArmTestFixture.Run(
                    FrontlineLabsDefinition.Create(),
                    (_, _) => GenericDeathmatchSessionTestFixture.Wait())));
        string invalid = MutateAndRehash(
            json,
            root => root["initialFrame"]!["state"]!["mode"]!
                .AsObject()["secondaryOwnerTeamId"] = 0);

        Assert.False(
            ReplayV3Serializer.VerifyHash(invalid, out string? failure),
            "a ruleset without a side objective published one");
        Assert.Contains(
            "capture bounds",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int OwnedTick(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        foreach (JsonElement tick in document.RootElement
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            if (tick.GetProperty("postState")
                    .GetProperty("mode")
                    .GetProperty("secondaryOwnerTeamId")
                    .ValueKind != JsonValueKind.Null)
            {
                return tick.GetProperty("tick").GetInt32();
            }
        }
        throw new InvalidOperationException(
            "The scripted match never took the flag.");
    }

    private static JsonObject PostControl(JsonObject root, int tick) =>
        root["ticks"]!.AsArray()
            .Single(item => (int)item!["tick"]! == tick)!
            ["postState"]!["mode"]!.AsObject();

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
        root["replayHash"] = Convert.ToHexStringLower(
            SHA256.HashData(stream.ToArray()));
        return root.ToJsonString();
    }

    /// <summary>
    /// Runs the scripted match. Team 1 stands on its pad firing west down the
    /// open centre row for the whole match; team 0's Prime either detours
    /// through the alcove first or walks straight into the lane.
    /// </summary>
    private static GenericActorMatchChronology Run(bool errand)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                classes: null,
                sideObjective: FrontlineLabsSideObjectiveArm.Muster);
        var phase = new Dictionary<int, int>();
        return FrontlineLabsSkillArmTestFixture.Run(
            definition,
            (start, observation) =>
            {
                if (start.ActorId.TeamId == 1)
                    return GenericDeathmatchSessionTestFixture.Shoot();
                if (start.ActorId.UnitId != 0)
                    return GenericDeathmatchSessionTestFixture.Wait();
                return TeamZero(observation, phase, errand);
            });
    }

    /// <summary>
    /// The script as a list of gates: walk each waypoint in turn, and — on
    /// the errand run — hold in the alcove until the flag actually turns
    /// over before spending the life that took it.
    /// </summary>
    private static GenericActorRuntimeDecision TeamZero(
        GenericActorRuntimeObservation observation,
        Dictionary<int, int> phase,
        bool errand)
    {
        int lifeId = observation.Self.ActorId.LifeId;
        // Only the first life runs the script; later lives hold still where
        // they arrived, so the arrival tile is what the test reads.
        if (lifeId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();

        ImmutableArray<Position?> script = errand
            ?
            [
                .. ToSite.Select(tile => (Position?)tile),
                null,
                .. ToKillingGround.Select(tile => (Position?)tile),
            ]
            : [KillingGround];
        int index = phase.GetValueOrDefault(lifeId);
        while (index < script.Length)
        {
            if (script[index] is not { } waypoint)
            {
                // The gate: hold in the alcove until the flag is ours.
                if (observation.Mode
                    is not GenericActorRuntimeObservation
                        .ModeObservationState
                        .Frontline { SecondaryOwnerTeamId: 0 })
                {
                    phase[lifeId] = index;
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                index++;
                continue;
            }
            if (FrontlineLabsSkillArmTestFixture.WalkTo(
                    observation,
                    waypoint) is { } step)
            {
                phase[lifeId] = index;
                return step;
            }
            index++;
        }
        phase[lifeId] = index;
        return GenericDeathmatchSessionTestFixture.Wait();
    }

    /// <summary>
    /// Where and when team 0's Prime came back after its first death, plus
    /// the flag owner published for that tick.
    /// </summary>
    private static (Position Arrival, int Tick, int? Owner) FirstRespawn(
        GenericActorMatchChronology chronology)
    {
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            GenericActorLifeStart? start = frame.TickStart.LifeStarts
                .FirstOrDefault(item =>
                    item.ActorId.TeamId == 0
                    && item.ActorId.UnitId == 0
                    && item.ActorId.LifeId > 0);
            if (start is null)
                continue;
            GenericActorWorldSnapshot.LifeSnapshot life =
                frame.TickStart.State.ActiveLives.Single(item =>
                    item.ActorId == start.ActorId);
            return (
                life.Position,
                frame.Tick,
                Mode(frame.TickStart.State).SecondaryOwnerTeamId);
        }
        throw new InvalidOperationException(
            "The probe never produced a team-0 Prime respawn.");
    }

    private static GenericActorRuntimeObservation.ModeObservationState
        .Frontline Mode(GenericActorWorldSnapshot snapshot) =>
        (GenericActorRuntimeObservation.ModeObservationState.Frontline)
        snapshot.Mode;
}
