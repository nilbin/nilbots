using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Engine.Tests;

/// <summary>
/// The canonical replay-v3 half of the ratchet-hold observability bump
/// (DECISIONS #169): the two hold clocks and the two projectile facts survive
/// the writer and the reader exactly, they are written where the schema says
/// they are written, and the document verifier refuses a hold the embedded
/// contract could not have produced.
///
/// <para>Both hold clocks are MANDATORY properties whose value may be null —
/// the discipline <c>claimingTeamId</c> already follows, and deliberately not
/// the inert-omitted discipline the contract's own <c>ratchetHoldTicks</c>
/// follows. The difference is what null MEANS: an omitted contract field says
/// "this ruleset has no such rule", while a null observation field says "no
/// hold binds on this tick", which is a fact about the tick and has to be
/// published even when it is negative.</para>
/// </summary>
public sealed class FrontlineRatchetHoldReplayV3Tests
{
    private static readonly Position Objective = new(11, 7);

    [Fact]
    public void TheHoldClocksRoundTripThroughTheCanonicalDocument()
    {
        ReplayV3 replay = ReplayV3Projection.Project(KeelAdvance());
        string json = ReplayV3Serializer.ToJson(replay);

        Assert.True(
            ReplayV3Serializer.VerifyHash(json, out string? failure),
            failure);

        using JsonDocument document = JsonDocument.Parse(json);
        (int Tick, JsonElement Mode) held = FirstHeldMode(document);
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
            held.Mode.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.Equal(
            0,
            held.Mode.GetProperty("holdOwnerTeamId").GetInt32());
        Assert.Equal(
            held.Tick + FrontlineLabsDefinition.RatchetHoldTicksDefault + 1,
            held.Mode.GetProperty("holdEndsAtTick").GetInt32());

        // Reader mirror: parse the document back and require the typed hold to
        // match what the writer emitted.
        ReplayV3 parsed = ReplayV3Serializer.ReadCanonicalComplete(json);
        Assert.Equal(
            replay.Ticks
                .Select(tick => Frontline(tick.PostState.Mode).HoldOwnerTeamId)
                .ToArray(),
            parsed.Ticks
                .Select(tick => Frontline(tick.PostState.Mode).HoldOwnerTeamId)
                .ToArray());
        Assert.Equal(
            replay.Ticks
                .Select(tick => Frontline(tick.PostState.Mode).HoldEndsAtTick)
                .ToArray(),
            parsed.Ticks
                .Select(tick => Frontline(tick.PostState.Mode).HoldEndsAtTick)
                .ToArray());
        Assert.Contains(
            parsed.Ticks,
            tick => Frontline(tick.PostState.Mode).HoldOwnerTeamId is not null);
    }

    [Fact]
    public void TheProjectileTimingAndDamageFactsRideEveryObservation()
    {
        ReplayV3 replay = ReplayV3Projection.Project(KeelAdvance());
        string json = ReplayV3Serializer.ToJson(replay);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement projectile = document.RootElement
            .GetProperty("ticks")
            .EnumerateArray()
            .SelectMany(tick => tick.GetProperty("actorTurns")
                .EnumerateArray())
            .Select(turn => turn.GetProperty("observation")
                .GetProperty("visibleProjectiles"))
            .Where(value => value.ValueKind == JsonValueKind.Array
                && value.GetArrayLength() > 0)
            .Select(value => value[0])
            .First();

        Assert.Equal(
            [
                "projectileId",
                "ownerTeamId",
                "ownerActorId",
                "position",
                "heading",
                "tilesPerAdvance",
                "ticksUntilAdvance",
                "remainingTiles",
                "observedBy",
                "ticksPerAdvance",
                "damagePerHit",
            ],
            projectile.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        // Both come off the firing attack profile, so they must agree with the
        // embedded contract rather than with a viewer-side guess.
        ActorAttackProfileDefinition profile = KeelArm().Rules.AttackProfiles
            .First(value => value.Projectile.Mode
                == ActorProjectileMode.Discrete);
        Assert.Equal(
            profile.Projectile.TicksPerAdvance,
            projectile.GetProperty("ticksPerAdvance").GetInt32());
        Assert.Equal(
            profile.Projectile.DamagePerHit,
            projectile.GetProperty("damagePerHit").GetInt32());
    }

    /// <summary>
    /// Document-level refusals. Every mutation below is internally consistent
    /// JSON with a correct payload hash; the only thing wrong with it is that
    /// the embedded contract could not have published that hold.
    ///
    /// <para>The layering is deliberate and worth stating, because it decides
    /// which forgeries land here and which do not. The document verifier does
    /// not re-run the objective kernel — it checks the published state against
    /// the embedded contract's own bounds. So a hold credited to the WRONG
    /// scoring team is bounds-legal and is refused one layer up, by the
    /// chronology validator's kernel re-derivation
    /// (<see cref="FrontlineRatchetHoldObservabilityTests"/>); what the
    /// document can decide by itself is the pair rule, the team domain, and
    /// the two ends of the clock.</para>
    /// </summary>
    [Fact]
    public void VerificationRejectsHoldsTheEmbeddedContractCannotPublish()
    {
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(KeelAdvance()));
        using JsonDocument document = JsonDocument.Parse(json);
        (int tick, JsonElement mode) = FirstHeldMode(document);
        int endsAt = mode.GetProperty("holdEndsAtTick").GetInt32();

        Action<JsonObject>[] mutations =
        [
            // Half a hold: an owner with no clock.
            root => PostControl(root, tick)["holdEndsAtTick"] = null,
            // Half a hold the other way: a clock with no owner.
            root => PostControl(root, tick)["holdOwnerTeamId"] = null,
            // An owner outside the scoring-team domain.
            root => PostControl(root, tick)["holdOwnerTeamId"] = 7,
            // Stretched one tick past the declared duration.
            root => PostControl(root, tick)["holdEndsAtTick"] = endsAt + 1,
            // Already lapsed, so not a live hold at all — a published hold is
            // by definition still binding.
            root => PostControl(root, tick)["holdEndsAtTick"] = tick,
        ];

        foreach (Action<JsonObject> mutation in mutations)
        {
            string invalid = MutateAndRehash(json, mutation);
            Assert.False(
                ReplayV3Serializer.VerifyHash(invalid, out string? failure),
                "a forged hold verified");
            Assert.Contains(
                "capture bounds",
                failure,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A ruleset whose redeploy policy has no ratchet may not publish a hold
    /// at all — the same "carried by exactly one policy" rule the contract's
    /// own hold duration follows, applied to the observation.
    /// </summary>
    [Fact]
    public void VerificationRejectsAHoldOnARulesetWithoutARatchet()
    {
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(Advance(Anchor())));
        string invalid = MutateAndRehash(
            json,
            root =>
            {
                JsonObject control = InitialControl(root);
                control["holdOwnerTeamId"] = 0;
                control["holdEndsAtTick"] = 40;
            });

        Assert.False(
            ReplayV3Serializer.VerifyHash(invalid, out string? failure),
            "an unratcheted ruleset published a hold");
        Assert.Contains(
            "capture bounds",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    private static (int Tick, JsonElement Mode) FirstHeldMode(
        JsonDocument document)
    {
        foreach (JsonElement tick in document.RootElement
                     .GetProperty("ticks")
                     .EnumerateArray())
        {
            JsonElement mode = tick.GetProperty("postState")
                .GetProperty("mode");
            if (mode.GetProperty("holdOwnerTeamId").ValueKind
                != JsonValueKind.Null)
            {
                return (tick.GetProperty("tick").GetInt32(), mode);
            }
        }
        throw new InvalidOperationException(
            "The scripted keel match published no hold.");
    }

    private static JsonObject InitialControl(JsonObject root) =>
        root["initialFrame"]!["state"]!["mode"]!.AsObject();

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

    private static ReplayV3.ModeState.Frontline Frontline(
        ReplayV3.ModeState mode) =>
        Assert.IsType<ReplayV3.ModeState.Frontline>(mode);

    private static GenericActorMatchChronology KeelAdvance() =>
        Advance(KeelArm());

    private static GenericActorMatchChronology Advance(
        ActorResolvedMatchDefinition definition) =>
        FrontlineLabsSkillArmTestFixture.Run(
            definition,
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0)
                {
                    // The idle side still fires, which is what puts a live
                    // bolt in both sides' observations. Which action a gun
                    // uses depends on the bend envelope, so read the mask.
                    return StraightShot(observation)
                        ?? GenericDeathmatchSessionTestFixture.Wait();
                }
                return FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        Objective)
                    ?? GenericDeathmatchSessionTestFixture.Wait();
            });

    /// <summary>
    /// Fires straight through whichever action this form's mask offers — the
    /// bend envelope decides whether that is parameterless
    /// <c>shoot-straight</c> or the program-bearing <c>shoot</c>.
    /// </summary>
    private static GenericActorRuntimeDecision? StraightShot(
        GenericActorRuntimeObservation observation)
    {
        GenericActorRuntimeActionLegality? straight = observation
            .ActionLegalities
            .FirstOrDefault(value => value.Available
                && value.ActionId == "shoot-straight");
        if (straight is not null)
        {
            return new GenericActorRuntimeDecision(
                straight.ActionId,
                straight.ActionCode,
                [],
                null);
        }
        GenericActorRuntimeActionLegality? programmed = observation
            .ActionLegalities
            .FirstOrDefault(value => value.Available
                && value.ActionId == "shoot");
        return programmed is null
            ? null
            : new GenericActorRuntimeDecision(
                programmed.ActionId,
                programmed.ActionCode,
                [
                    new GenericActorRuntimeActionArgument.ShotProgramArgument(
                        ShotProgram.Straight),
                ],
                null);
    }

    private static ActorResolvedMatchDefinition KeelArm() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally
                | FrontlineLabsPendulumArm.ContestMajority
                | FrontlineLabsPendulumArm.EnemySoleDecay,
            (FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked);

    private static ActorResolvedMatchDefinition Anchor() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.ContestMajority,
            (FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked);
}
