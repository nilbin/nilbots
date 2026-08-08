using System.Text.Json;
using BotArena.Engine;

namespace BotArena.Cli.Tests;

/// <summary>
/// `--print-candidate-contract full` — three authoring waves' most-repeated
/// friction (#184, #188).
///
/// <para>The bare flag prints IDENTITY: which contract this cell is. What
/// doctrine actually needs is its NUMBERS, and until now the only way to read
/// them was to run a throwaway match and mine
/// <c>replay.json → header.contract</c>. `full` prints exactly those bytes, so
/// the pin here is not "it printed something JSON-shaped" but "it printed THE
/// canonical contract" — the same document the runtime is handed at MatchStart,
/// with the declared values a reader came for.</para>
/// </summary>
[Collection("Console")]
public sealed class PrintCandidateContractFullTests
{
    [Fact]
    public void FullPrintsTheExactResolvedCanonicalContract()
    {
        // Byte-identical to the engine's own canonical serialization of the
        // definition this command resolves. Anything less exact and a number
        // read here could differ from the number the match plays.
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(
                FrontlineLabsDefinition.Create()),
            Capture(["--print-candidate-contract", "full"]).Trim());
    }

    [Fact]
    public void FullAndIdentityDescribeTheSameResolvedCell()
    {
        // The two modes are one contract at two resolutions, so every
        // fingerprint the identity mode publishes must be the one the full
        // contract carries — on an ARM, where the risk of printing a different
        // cell than the one a run would execute actually lives.
        string[] cell =
        [
            "--classes", "bulwark-vs-striker",
            "--skills", "kit",
            "--cooldown", "ticking",
            "--volley", "salvo",
        ];
        JsonElement identity = Parse(
            ["--print-candidate-contract", .. cell]);
        JsonElement full = Parse(
            ["--print-candidate-contract", "full", .. cell]);

        Assert.Equal(
            identity.GetProperty("matchContractFingerprint").GetString(),
            full.GetProperty("matchContractFingerprint").GetString());
        Assert.Equal(
            identity.GetProperty("rulesFingerprint").GetString(),
            full.GetProperty("rules").GetProperty("rulesFingerprint")
                .GetString());
        Assert.Equal(
            identity.GetProperty("mapFingerprint").GetString(),
            full.GetProperty("map").GetProperty("mapFingerprint").GetString());
        Assert.Equal(
            identity.GetProperty("topologyFingerprint").GetString(),
            full.GetProperty("topology").GetProperty("topologyFingerprint")
                .GetString());
    }

    [Fact]
    public void FullCarriesTheDeclaredNumbersDoctrineIsWrittenAgainst()
    {
        JsonElement contract = Parse(
        [
            "--print-candidate-contract", "full",
            "--classes", "bulwark-vs-striker",
            "--skills", "kit",
            "--cooldown", "ticking",
            "--volley", "salvo",
        ]);

        JsonElement rules = contract.GetProperty("rules");

        // The windup grammar every anchor/transform decision is priced on.
        JsonElement transitions = rules.GetProperty("sameLifeTransitions");
        Assert.NotEmpty(transitions.EnumerateArray());
        Assert.All(
            transitions.EnumerateArray(),
            transition => Assert.True(
                transition
                    .GetProperty("windup")
                    .GetProperty("durationTicks")
                    .GetInt32() >= 0));

        // The route-cooldown clock: the fact `--cooldown ticking` exists to
        // move, and the one wave-7 authors mined a throwaway replay for. It
        // lives on the SKILL ROUTE — a same-life transition — not on the
        // action, which is itself worth being able to read here.
        JsonElement[] cooled =
        [
            .. transitions
                .EnumerateArray()
                .Where(transition => transition.TryGetProperty(
                    "cooldownTicks",
                    out JsonElement cooldown)
                    && cooldown.GetInt32() > 0),
        ];
        Assert.NotEmpty(cooled);

        // The gun tempo every fire-control rule is written against.
        Assert.All(
            rules.GetProperty("attackProfiles").EnumerateArray(),
            profile => Assert.True(
                profile.GetProperty("cooldownTicks").GetInt32() > 0));

        // And the horizon, the other number the CLI banner tells authors not
        // to assume.
        Assert.True(
            rules.GetProperty("limits").GetProperty("maxTicks").GetInt32() > 0);
    }

    [Fact]
    public void TheBareFlagStillPrintsIdentityAndOnlyIdentity()
    {
        // Every existing sweep script and the preflight gate read this shape;
        // adding a mode must not move it.
        JsonElement identity = Parse(
            ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]);
        Assert.Equal(
            "frontline-labs-1-experiment-classes-bulwark-vs-striker",
            identity.GetProperty("rulesetId").GetString());
        Assert.False(identity.TryGetProperty("rules", out _));

        // `identity` says the default out loud and means the same thing.
        Assert.Equal(
            Capture(
                ["--print-candidate-contract", "--classes", "bulwark-vs-striker"]),
            Capture(
            [
                "--print-candidate-contract", "identity",
                "--classes", "bulwark-vs-striker",
            ]));
    }

    [Fact]
    public void FullFollowsTheProfileTheMatchWouldRunOn()
    {
        JsonElement perLife = Parse(
            ["--print-candidate-contract", "full"]);
        JsonElement mind = Parse(
            ["--print-candidate-contract", "full", "--profile", "mind"]);
        Assert.Equal(
            "generic-actor-match-2",
            perLife
                .GetProperty("capabilityVersions")
                .GetProperty("contractProfileId")
                .GetString());
        Assert.Equal(
            "generic-mind-match-1",
            mind
                .GetProperty("capabilityVersions")
                .GetProperty("contractProfileId")
                .GetString());
    }

    [Fact]
    public void AnUnknownModeIsRefusedRatherThanTreatedAsIdentity()
    {
        InvalidOperationException unknown =
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsExperimentCommand.Run(
                    ["--print-candidate-contract", "everything"]));
        Assert.Contains("full", unknown.Message, StringComparison.Ordinal);
    }

    private static JsonElement Parse(string[] arguments)
    {
        using JsonDocument document = JsonDocument.Parse(Capture(arguments));
        return document.RootElement.Clone();
    }

    private static string Capture(string[] arguments)
    {
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, FrontlineLabsExperimentCommand.Run(arguments));
        }
        finally
        {
            Console.SetOut(original);
        }
        return output.ToString();
    }
}
