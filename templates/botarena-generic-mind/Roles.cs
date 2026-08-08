using BotArena.Sdk;

/// <summary>What one body is for, this tick.</summary>
internal enum Role
{
    /// <summary>Holds the point still, which is what actually takes ground.</summary>
    Channeler,

    /// <summary>Stands between the channeler and the shooting.</summary>
    Screen,

    /// <summary>Goes and gets the scrap, and brings it home.</summary>
    Courier,

    /// <summary>Builds the next body.</summary>
    Builder,

    /// <summary>Nothing assigned yet.</summary>
    Reserve,
}

/// <summary>One tick's assignment: which body is doing what.</summary>
internal sealed class RoleMap
{
    private readonly Dictionary<int, Role> _byUnit = [];

    public MindBody? Channeler { get; set; }

    public Role this[MindBody body] =>
        _byUnit.TryGetValue(body.UnitId, out Role role)
            ? role
            : Role.Reserve;

    public void Assign(MindBody body, Role role) =>
        _byUnit[body.UnitId] = role;

    /// <summary>
    /// The body this one is screening for, or null when it screens for nobody.
    /// </summary>
    public MindBody? ChannelerFor(MindBody body) =>
        this[body] == Role.Screen ? Channeler : null;
}

/// <summary>
/// THE FILE YOU EDIT FIRST.
///
/// <para>This is the whole architecture in one function: who does what is a
/// DECISION taken once, over the whole army, with everything the mind knows —
/// not a derivation that nine independent programs have to reach the same
/// answer to, without a channel, from the frozen observation, every tick.</para>
///
/// <para>Under the per-life profile this file did not exist and could not: each
/// body ran its own copy of the bot with its own empty memory, so "who
/// channels" had to be a pure function of the shared observation that every
/// body computed identically, tie-breaks and all. Six of eight bots in the last
/// measured cohort shipped a dedicated file for that — 3,788 lines across the
/// population whose only job was making N runtimes agree. All of it is
/// replaced by the lines below, and the reason is simply that there is one
/// decider now.</para>
///
/// <para>So: rewrite this freely. Assign by capability rather than by name
/// (<c>body.Action("transform")?.Available</c> beats guessing from
/// <c>ClassId</c>, and under a mixed composition your own bodies genuinely have
/// different kits). Roles are sticky in the viewer, so name them in your own
/// words — the vocabulary you choose is your strategy made legible, to you and
/// to a spectator.</para>
/// </summary>
internal static class Roles
{
    /// <summary>How many bodies escort the channeler when there are spares.</summary>
    private const int ScreenCount = 2;

    public static RoleMap Assign(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        Recall recall)
    {
        Position[] objective = ArenaBasics.ActiveObjectiveTiles(contract, mind);
        var map = new RoleMap();

        // 1. WHO BUILDS, and it is decided FIRST. Capability, not name: the
        //    body that can fabricate right now is the builder, whatever chassis
        //    it turns out to be. It goes first because a fabrication window is
        //    narrow and a body's tick spent building is worth more than the
        //    same tick spent walking — and because with only one body alive,
        //    the choice is build-or-walk rather than build-or-hold.
        MindBody? builder = mind.Bodies
            .Where(body => body.Action("fabricate") is { Available: true })
            .OrderBy(body => body.UnitId)
            .FirstOrDefault();
        if (builder is not null)
            map.Assign(builder, Role.Builder);

        // 2. WHO HOLDS THE POINT. Prefer a body already standing on it — under
        //    the capture channel, moving costs you the claim, so the body that
        //    is already there is worth more than a healthier one two tiles
        //    away. Then durability, then unit id so the answer is stable across
        //    ticks: a channeler that changes every tick never claims anything.
        MindBody? channeler = mind.Bodies
            .Where(body =>
                map[body] == Role.Reserve
                && ArenaBasics.ObjectiveWeight(contract, body.FormId) > 0)
            .OrderByDescending(body => objective.Contains(body.Position))
            .ThenBy(body => Distance(body.Position, objective))
            .ThenByDescending(body => body.Health)
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();

        map.Channeler = channeler;
        if (channeler is null)
            return map;
        map.Assign(channeler, Role.Channeler);

        // 3. WHO SCREENS. Screening is only worth a body under the CHANNEL,
        //    where damage to a controller on the point reverts the run — read
        //    the contract, do not assume the arm.
        bool screensPay =
            ArenaBasics.Capture(contract)?.StillnessGated ?? false;
        MindBody[] spare = mind.Bodies
            .Where(body => map[body] == Role.Reserve)
            .OrderBy(body => Distance(body.Position, [channeler.Position]))
            .ThenBy(body => body.UnitId)
            .ToArray();

        int screens = screensPay ? ScreenCount : 1;
        foreach (MindBody body in spare.Take(screens))
            map.Assign(body, Role.Screen);

        // 4. WHO FETCHES. Only once the front is actually held — a courier run
        //    is a multi-body, multi-life commitment, and the reason the last
        //    cohort barely bought the deep-carry game is that a per-life bot
        //    could not own a plan longer than one body's life. Yours can:
        //    Recall keeps the run, and BOTNAME.cs hands it to another body when
        //    its carrier dies.
        bool frontIsHeld = objective.Length > 0
            && mind.Bodies.Any(body => objective.Contains(body.Position));
        if (frontIsHeld && recall.BestPile(mind) is not null)
        {
            foreach (MindBody body in spare.Skip(screens).Take(1))
                map.Assign(body, Role.Courier);
        }

        return map;
    }

    private static int Distance(
        Position from,
        IReadOnlyCollection<Position> tiles) =>
        tiles.Count == 0 ? 0 : tiles.Min(from.ChebyshevDistance);
}
