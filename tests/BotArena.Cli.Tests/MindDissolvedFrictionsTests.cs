using System.Text.Json;
using BotArena.Sdk;

namespace BotArena.Cli.Tests;

/// <summary>
/// THE MIND DISSOLVES WHAT IT PROMISED (docs/DESIGN-MIND-ARCHITECTURE §7.4).
///
/// <para>The memo listed three of wave 8's unanimity-ranked frictions as
/// DISSOLVED by the architecture rather than fixed by a feature. Two of those
/// claims are checkable mechanically, and this file checks them on the real
/// authoring surface — the scaffold a player is handed, driving a real army on
/// the shipped legion roster:</para>
///
/// <list type="number">
/// <item>own-body <c>movedThisTick</c> is PUBLISHED, so the nine-line
/// reconstruction every lineage carried is gone;</item>
/// <item>sibling collisions are RESOLVABLE inside one mind, because one
/// decider can reserve tiles against its own army — and the scaffold's
/// reservation system is the reference implementation.</item>
/// </list>
///
/// <para>The third claim — that the invest same-tick race cannot happen inside
/// one mind — is pinned engine-side in
/// <c>GenericMindSingleDecisionMapTests</c>, because it is a statement about
/// the decision map's resolution order rather than about the scaffold.</para>
/// </summary>
[Collection("Console")]
public sealed class MindDissolvedFrictionsTests
{
    /// <summary>
    /// The scaffold, mirrored, on the shipped roster — three bodies per side at
    /// tick 0 growing to eight, which is the only configuration where sibling
    /// collision is a real problem at all.
    /// </summary>
    private static JsonElement LegionMirror(string temporary)
    {
        string project = Scaffold(temporary, "Reserver", "generic-mind");
        string output = Path.Combine(temporary, "mirror");
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            Assert.Equal(
                0,
                FrontlineLabsExperimentCommand.Run(
                [
                    "--profile", "mind",
                    "--runtime", "in-process",
                    "--bot", project,
                    "--opponent", project,
                    "--classes", "bulwark-vs-striker",
                    "--roster", "legion",
                    "--seed", "42",
                    "--out", output,
                ]));
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(output, "replay.json")));
        return document.RootElement.Clone();
    }

    [Fact]
    public void OwnBodiesPublishMovedThisTickInsteadOfMakingYouRebuildIt()
    {
        string temporary = Temporary("mind-moved");
        try
        {
            JsonElement replay = LegionMirror(temporary);
            int checkedBodies = 0;
            int actuallyMoved = 0;
            foreach (JsonElement turn in MindTurns(replay))
            {
                foreach (JsonElement body in turn
                             .GetProperty("observation")
                             .GetProperty("bodies")
                             .EnumerateArray())
                {
                    // Published, on every own body, every tick — the 8/8 ask.
                    bool moved = body.GetProperty("movedLastTick").GetBoolean();
                    JsonElement previous = body.GetProperty("previousPosition");
                    Assert.NotEqual(JsonValueKind.Undefined, previous.ValueKind);
                    if (previous.ValueKind == JsonValueKind.Null)
                    {
                        // A life's first tick has no previous tile to have left.
                        Assert.False(moved);
                        continue;
                    }

                    // And it AGREES with the fact it stands in for, which is
                    // the part a hand-rolled reconstruction kept getting wrong.
                    Assert.Equal(
                        moved,
                        Tile(body.GetProperty("position"))
                            != Tile(previous));
                    checkedBodies++;
                    if (moved)
                        actuallyMoved++;
                }
            }

            Assert.True(checkedBodies > 100);
            Assert.True(
                actuallyMoved > 0,
                "an army that never moved would not test the field");
        }
        finally
        {
            Cleanup(temporary);
        }
    }

    [Fact]
    public void TheScaffoldsReservationsResolveEverySiblingCollision()
    {
        string temporary = Temporary("mind-reservations");
        try
        {
            JsonElement replay = LegionMirror(temporary);
            int moves = 0;
            foreach (JsonElement turn in MindTurns(replay))
            {
                Dictionary<(int Unit, int Life), (int X, int Y)> start = turn
                    .GetProperty("observation")
                    .GetProperty("bodies")
                    .EnumerateArray()
                    .ToDictionary(
                        body => (
                            body.GetProperty("actorId")
                                .GetProperty("unitId").GetInt32(),
                            body.GetProperty("actorId")
                                .GetProperty("lifeId").GetInt32()),
                        body => Tile(body.GetProperty("position")));
                var destination =
                    new Dictionary<(int Unit, int Life), (int X, int Y)>();
                foreach (JsonElement resolution in turn
                             .GetProperty("resolutions")
                             .EnumerateArray())
                {
                    (int Unit, int Life) key = (
                        resolution.GetProperty("unitId").GetInt32(),
                        resolution.GetProperty("lifeId").GetInt32());
                    JsonElement accepted = resolution
                        .GetProperty("actionResolution")
                        .GetProperty("acceptedAction");
                    if (accepted.GetProperty("actionId").GetString() != "move")
                    {
                        destination[key] = start[key];
                        continue;
                    }

                    moves++;
                    destination[key] = Step(
                        start[key],
                        accepted.GetProperty("arguments")
                            .EnumerateArray()
                            .Single(argument =>
                                argument.GetProperty("kind").GetString()
                                == "direction")
                            .GetProperty("value")
                            .GetString()!);
                }

                // The three sibling-collision classes the memo names, all
                // three of which the engine resolves as an ordinary Blocked
                // and a wasted tick:
                //   same destination…
                Assert.Equal(
                    destination.Count,
                    destination.Values.Distinct().Count());
                foreach (
                    ((int Unit, int Life) key, (int X, int Y) target)
                        in destination)
                {
                    foreach (
                        ((int Unit, int Life) other, (int X, int Y) origin)
                            in start)
                    {
                        if (other.Equals(key) || target == start[key])
                            continue;
                        //   …stepping onto a sibling's tile, whether it is
                        //   standing there or vacating it (this contract
                        //   declares followingVacatedActorAllowed = false,
                        //   which the scaffold reads rather than assumes)…
                        Assert.NotEqual(origin, target);
                        //   …and swapping tiles with a sibling.
                        Assert.False(
                            target == origin
                            && destination[other] == start[key]);
                    }
                }
            }

            Assert.True(
                moves > 200,
                "an army that barely moved would not test the reservations");
        }
        finally
        {
            Cleanup(temporary);
        }
    }

    [Fact]
    public void TheScaffoldReadsTheFollowRuleRatherThanAssumingIt()
    {
        // The reservation system is only correct because it consults the
        // contract: a game that ALLOWS following a vacated tile would want the
        // tile released, and one that forbids it (this one) must hold it. The
        // field exists on the reader, which is what the template compiles
        // against.
        Assert.Contains(
            "FollowingVacatedActorAllowed",
            File.ReadAllText(TemplateArenaBasics()),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "claims.Release(",
            File.ReadAllText(TemplateArenaBasics()),
            StringComparison.Ordinal);
    }

    private static string TemplateArenaBasics()
    {
        string? root = CliSupport.FindUpward(
            Path.Combine("templates", "botarena-generic-mind"));
        Assert.NotNull(root);
        return Path.Combine(root!, "ArenaBasics.cs");
    }

    private static IEnumerable<JsonElement> MindTurns(JsonElement replay) =>
        replay
            .GetProperty("ticks")
            .EnumerateArray()
            .SelectMany(tick => tick.GetProperty("mindTurns").EnumerateArray());

    private static (int X, int Y) Tile(JsonElement position) =>
        (position.GetProperty("x").GetInt32(),
            position.GetProperty("y").GetInt32());

    private static (int X, int Y) Step(
        (int X, int Y) from,
        string direction)
    {
        // Movement is cardinal-only in this contract; anything else here
        // would itself be news.
        Direction parsed = direction switch
        {
            "north" => Direction.North,
            "east" => Direction.East,
            "south" => Direction.South,
            "west" => Direction.West,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
        (int dx, int dy) = parsed.Vector();
        return (from.X + dx, from.Y + dy);
    }

    private static string Scaffold(
        string root,
        string name,
        string profile)
    {
        Directory.CreateDirectory(root);
        string previous = Directory.GetCurrentDirectory();
        TextWriter stdout = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, NewCommand.Run(name, ["--profile", profile]));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Console.SetOut(stdout);
        }
        return Path.Combine(root, name);
    }

    private static string Temporary(string label) =>
        Path.Combine(
            Path.GetTempPath(),
            $"nilbots-{label}-{Guid.NewGuid():N}");

    private static void Cleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Disposable either way.
        }
    }
}
