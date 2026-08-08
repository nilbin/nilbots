using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Engine.Tests;

/// <summary>
/// THE FORGERIES (DECISIONS #191 P3;
/// <c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.3). Every refusal the
/// mind-era document validator adds gets a doctored document that is
/// SELF-CONSISTENT and correctly hashed, and must still be refused.
/// <para>
/// The distinction the whole section turns on: engine-refused-at-runtime
/// (a Rejected command naming a dead body — legitimate, recorded, replayable)
/// versus document-malformed (a shape the engine could not have produced).
/// Conflating them would either let forgeries through or make honest replays
/// unverifiable, so each test below names which side of that line it is on.
/// </para>
/// </summary>
public sealed class GenericMindReplayRefusalTests
{
    [Fact]
    public void TheHonestDocumentVerifies()
    {
        Assert.True(
            GenericActorReplayDocument.VerifyHash(
                Document(),
                out string? failure),
            failure);
        // And a re-hashed but unmutated copy still verifies, so every refusal
        // below is the mutation and never the re-hashing.
        Assert.True(
            GenericActorReplayDocument.VerifyHash(
                MutateAndRehash(Document(), _ => { }),
                out failure),
            failure);
    }

    [Fact]
    public void ADuplicateCommandForOneBodyIsRefused()
    {
        Refused(
            root =>
            {
                JsonArray commands = Commands(root, tick: 4, turn: 0);
                commands.Add(
                    JsonNode.Parse(commands[0]!.ToJsonString())!);
            },
            "cannot command the same body twice");
    }

    [Fact]
    public void ADecisionAcceptedOnAnUnownedBodyIsRefused()
    {
        Refused(
            root =>
            {
                JsonObject command =
                    Commands(root, tick: 4, turn: 0)[0]!.AsObject();
                // A slot this participant does not control. The engine would
                // have Rejected it; claiming acceptance is the forgery.
                command["unitId"] = 97;
            },
            "accepted a command on a body that is not an own live body");
    }

    [Fact]
    public void ADecisionAcceptedOnADeadBodyIsRefused()
    {
        Refused(
            root =>
            {
                JsonObject command =
                    Commands(root, tick: 4, turn: 0)[0]!.AsObject();
                // The right slot, a life that is not live this tick.
                command["lifeId"] = 41;
            },
            "accepted a command on a body that is not an own live body");
    }

    [Fact]
    public void AFuelBudgetOffTheLiveBodyFormulaIsRefused()
    {
        Refused(
            root => Turn(root, tick: 4, turn: 0)["fuelBudget"] =
                "1800000000",
            "fuel budget must be exactly the live-body formula");
    }

    [Fact]
    public void AResolutionSetThatIsNotTheOwnLiveBodiesIsRefused()
    {
        Refused(
            root =>
            {
                JsonArray resolutions =
                    Turn(root, tick: 4, turn: 0)["resolutions"]!.AsArray();
                resolutions.RemoveAt(resolutions.Count - 1);
            },
            "resolutions must cover exactly its own live bodies");
    }

    [Fact]
    public void AnObservationThatDisagreesWithTheReDerivedPreStateIsRefused()
    {
        Refused(
            root => Bodies(root, tick: 4, turn: 0)[0]!.AsObject()["health"] =
                99,
            "body does not match its authoritative pre-state");
    }

    [Fact]
    public void AMindTurnForANonTickingParticipantIsRefused()
    {
        // The LAST turn, so the forgery keeps canonical participant order and
        // uniqueness and is refused for the reason under test rather than for
        // ordering.
        Refused(
            root => Turn(
                root,
                tick: 4,
                turn: Tick(root, 4)["mindTurns"]!.AsArray().Count - 1)
                ["participantId"] = 404,
            "names a participant the contract does not place on that team");
    }

    [Fact]
    public void AnObservedModeThatDisagreesWithThePreStateIsRefused()
    {
        Refused(
            root => Turn(root, tick: 4, turn: 0)["observation"]!["mode"]!
                .AsObject()["captureProgress"] = 12_345,
            "observed mode must exactly match the authoritative pre-state");
    }

    [Fact]
    public void ASlotTableThatIsNotTheParticipantsOwnSlotsIsRefused()
    {
        Refused(
            root =>
            {
                JsonArray slots = Turn(root, tick: 4, turn: 0)
                    ["observation"]!["slots"]!.AsArray();
                slots.RemoveAt(slots.Count - 1);
            },
            "slot table must be exactly its own slots in canonical order");
    }

    [Fact]
    public void AReservedChassisSelectionIsRefused()
    {
        Refused(
            root => Turn(root, tick: 4, turn: 0)
                ["observation"]!["slots"]![0]!.AsObject()["selectedClassId"] =
                "striker",
            "carries a reserved chassis selection that v1 never writes");
    }

    [Fact]
    public void ANonEmptyAlliedIntentCollectionIsRefused()
    {
        Refused(
            root => Turn(root, tick: 4, turn: 0)
                    ["observation"]!.AsObject()["alliedIntents"] =
                new JsonArray(
                    new JsonObject
                    {
                        ["participantId"] = 1,
                        ["tagId"] = "push",
                        ["value"] = "1",
                    }),
            "allied intents are reserved and must be empty");
    }

    [Fact]
    public void ARoleTagOverTheByteCapIsRefused()
    {
        Refused(
            root => Commands(root, tick: 4, turn: 0)[0]!.AsObject()
                ["roleTag"] = new string('a', 25),
            "outside the canonical charset or the 24-byte cap");
    }

    [Fact]
    public void ARoleTagOffTheCanonicalCharsetIsRefused()
    {
        Refused(
            root => Commands(root, tick: 4, turn: 0)[0]!.AsObject()
                ["roleTag"] = "Channeler",
            "outside the canonical charset or the 24-byte cap");
    }

    [Fact]
    public void APublishedRoleTagTheMindNeverSetIsRefused()
    {
        // Re-derivation, not shape: the tag is perfectly well-formed and the
        // document is internally consistent. It is refused because no accepted
        // command ever set it, which is what stops a doctored document from
        // narrating a strategy that never happened.
        Refused(
            root => Bodies(root, tick: 4, turn: 0)[0]!.AsObject()["roleTag"] =
                "sacrifice",
            "publishes a role tag its mind never set on that body");
    }

    [Fact]
    public void ARoleTagRemovedFromItsSettingCommandIsRefused()
    {
        // The mirror image: strip the tag from the command that set it and the
        // published tags downstream stop matching the re-derivation.
        Refused(
            root =>
            {
                foreach (JsonNode? command in
                         Commands(root, tick: 0, turn: 0))
                {
                    command!.AsObject().Remove("roleTag");
                }
            },
            "publishes a role tag its mind never set on that body");
    }

    [Fact]
    public void AForgedBodyRandomSeedIsRefused()
    {
        Refused(
            root => Bodies(root, tick: 4, turn: 0)[0]!.AsObject()
                ["bodyRandomSeed"] = "1",
            "body random seed is not the seed the document declared");
    }

    [Fact]
    public void AMindDocumentCarryingActorTurnsIsRefused()
    {
        Refused(
            root => Tick(root, 4)["actorTurns"] = new JsonArray(),
            "must carry exactly the turn kind its contract profile selects");
    }

    [Fact]
    public void ATeamSwappedTeamSeedIsStillRefusedUnderTheMind()
    {
        // Carried over from the per-life generation (#185): the seed is still
        // team-scoped and still re-derived; only its consumer moved.
        Refused(
            root =>
            {
                JsonArray starts = root["initialFrame"]!["lifeStarts"]!
                    .AsArray();
                string first =
                    starts[0]!["teamRandomSeed"]!.GetValue<string>();
                string other = starts
                    .Select(start => start!["teamRandomSeed"]!
                        .GetValue<string>())
                    .First(seed => !string.Equals(
                        seed,
                        first,
                        StringComparison.Ordinal));
                starts[0]!["teamRandomSeed"] = other;
            },
            "seed");
    }

    private static void Refused(
        Action<JsonObject> forge,
        string expectedFragment)
    {
        string forged = MutateAndRehash(Document(), forge);
        Assert.False(
            GenericActorReplayDocument.VerifyHash(
                forged,
                out string? failure));
        Assert.Contains(
            expectedFragment,
            failure ?? string.Empty,
            StringComparison.Ordinal);
    }

    private static JsonObject Tick(JsonObject root, int tick) =>
        root["ticks"]![tick]!.AsObject();

    private static JsonObject Turn(JsonObject root, int tick, int turn) =>
        Tick(root, tick)["mindTurns"]![turn]!.AsObject();

    private static JsonArray Commands(JsonObject root, int tick, int turn) =>
        Turn(root, tick, turn)["commands"]!.AsArray();

    private static JsonArray Bodies(JsonObject root, int tick, int turn) =>
        Turn(root, tick, turn)["observation"]!["bodies"]!.AsArray();

    private static string Document() =>
        GenericMindForgeryFixture.Document.Value;

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
}
