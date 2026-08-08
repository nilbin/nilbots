using BotArena.Sdk;

/// <summary>
/// The CHANNEL, resolved once per tick from the frozen observation.
///
/// <para>Wave 8's arm rewrote the front. Claim weight counts only the bodies
/// of a team whose tile did not change this tick; denial weight counts all of
/// them; a team controls the point when its claim strictly exceeds the
/// opposition's denial, and the gain is that surplus capped by a declared
/// ceiling. Damage to a controller standing on the region reverts the whole
/// run. Every one of those is a contract read (<see cref="ContractLens"/>),
/// and every branch here is inert when the contract does not declare it.</para>
///
/// <para>Three numbers come out of it and the whole doctrine hangs off them.
/// <b>Denial</b> is what my presence subtracts whether or not I stand still —
/// so a body that keeps walking still stops a channel dead. <b>Claim</b> is
/// what standing still buys. <b>Revert</b> is what being hit while standing
/// still costs. Stillness is therefore a PURCHASE, not a state: buy it when
/// the surplus is positive and nothing is pointed at the tile, and walk the
/// region when it is not.</para>
///
/// <para>It also carries the salvo read, because that read is what decides
/// whether anything is pointed at the tile. A body whose heaviest reachable
/// gun deals at least my current health owns every tile on its eight rays out
/// to that gun's declared travel; those tiles are LETHAL GROUND, and a
/// two-health prime that steps onto one has ended its life on arithmetic
/// available before it moved. Nothing here names a skill or a class: the fan
/// is recognised as damage, travel and straightness.</para>
/// </summary>
internal sealed class Channel
{
    private readonly HashSet<Position> _lethal = [];
    private readonly HashSet<Position> _objective = [];

    /// <summary>True when the contract declares the channel at all.</summary>
    public bool Engaged { get; private set; }

    /// <summary>Own objective weight on the region, moving or not — my denial.</summary>
    public int OwnDenial { get; private set; }

    /// <summary>Enemy objective weight on the region — the denial I must beat.</summary>
    public int EnemyDenial { get; private set; }

    /// <summary>Own weight on the region that is not this body.</summary>
    public int AlliedOnRegion { get; private set; }

    /// <summary>This body's own objective weight.</summary>
    public int MyWeight { get; private set; }

    /// <summary>True when this body is standing on the active region.</summary>
    public bool SelfOnRegion { get; private set; }

    /// <summary>Team currently accumulating progress, or null.</summary>
    public int? ClaimingTeamId { get; private set; }

    /// <summary>Published progress on the running claim.</summary>
    public int Progress { get; private set; }

    /// <summary>True when the running claim is my team's.</summary>
    public bool MyRun { get; private set; }

    /// <summary>True when the running claim belongs to the opposition.</summary>
    public bool TheirRun { get; private set; }

    /// <summary>
    /// What the team gains this tick if this body holds its tile — the capped
    /// surplus of stationary claim over enemy denial. Zero is a stall.
    /// </summary>
    public int GainIfIStand { get; private set; }

    /// <summary>What the team gains if this body spends the tick moving.</summary>
    public int GainIfIMove { get; private set; }

    /// <summary>
    /// True when standing still buys strictly more than walking does. This is
    /// the only reason to accept a fixed tile in a game where everything that
    /// can shoot me knows where I will be.
    /// </summary>
    public bool StillnessPays => GainIfIStand > GainIfIMove;

    /// <summary>
    /// True when my presence alone stops the opposition's channel: their claim
    /// can never exceed my denial, whatever they do with their feet.
    /// </summary>
    public bool DenialSuffices => SelfOnRegion && OwnDenial >= EnemyDenial
        && EnemyDenial > 0;

    /// <summary>
    /// True when the contract reverts a controller's work on damage taken on
    /// the point — the fact that makes an unscreened channel worthless.
    /// </summary>
    public bool Interrupts { get; private set; }

    /// <summary>
    /// Tiles on which a single contact from a visible body would kill this
    /// life. Empty whenever nothing visible can do that, which is most ticks
    /// of most matches and every match on a contract with no heavy gun.
    /// </summary>
    public bool Lethal(Position tile) => _lethal.Contains(tile);

    /// <summary>Whether any lethal ground is currently mapped at all.</summary>
    public bool AnyLethal => _lethal.Count > 0;

    /// <summary>
    /// The tiles a screen would have to stand on to eat the bolt aimed at
    /// <paramml name="target"/> — every tile strictly between a visible enemy
    /// and the target on a clear ray. Bolts stop on the first enemy actor and
    /// allied bolts pass through allies, so a body parked there is a shield
    /// that does not blind its own team.
    /// </summary>
    public bool Screens(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        Position target)
    {
        if (tile == target)
            return false;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            (int damage, int travel, _) =
                lens.HeaviestStrike(enemy.FormId);
            if (damage <= 0)
                continue;
            if (!Tactics.TryRay(enemy.Position, target, out _, out int reach))
                continue;
            if (reach > travel)
                continue;
            if (!Between(enemy.Position, target, tile))
                continue;
            if (!Tactics.ClearRay(lens, enemy.Position, tile, lens.StrictCorners))
                continue;
            return true;
        }
        return false;
    }

    private static bool Between(Position from, Position to, Position tile)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        if (dx == 0 && dy == 0)
            return false;
        int steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        if (Math.Abs(to.X - from.X) != 0
            && Math.Abs(to.Y - from.Y) != 0
            && Math.Abs(to.X - from.X) != Math.Abs(to.Y - from.Y))
        {
            return false;
        }
        for (int step = 1; step < steps; step++)
        {
            if (tile.X == from.X + (dx * step) && tile.Y == from.Y + (dy * step))
                return true;
        }
        return false;
    }

    /// <summary>Rebuilds every number above from this tick's observation.</summary>
    public void Resolve(
        ContractLens lens,
        GenericActorContext context,
        Position[] objective)
    {
        _lethal.Clear();
        _objective.Clear();
        foreach (Position tile in objective)
            _objective.Add(tile);

        Engaged = lens.CaptureIsChannel;
        Interrupts = lens.RevertPerDamagePoint > 0;
        MyWeight = Weight(lens, context.Self.FormId);
        SelfOnRegion = _objective.Contains(context.Self.Position);

        AlliedOnRegion = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (_objective.Contains(ally.Position))
                AlliedOnRegion += Weight(lens, ally.FormId);
        }
        EnemyDenial = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (_objective.Contains(enemy.Position))
                EnemyDenial += Weight(lens, enemy.FormId);
        }
        OwnDenial = AlliedOnRegion + (SelfOnRegion ? MyWeight : 0);

        ClaimingTeamId = null;
        Progress = 0;
        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            ClaimingTeamId = frontline.ClaimingTeamId;
            Progress = frontline.CaptureProgress;
        }
        MyRun = ClaimingTeamId == lens.TeamId;
        TheirRun = ClaimingTeamId is int claimant && claimant != lens.TeamId;

        // The gain arithmetic. Off the channel it degrades to the wave-6
        // reading, so the two halves of the doctrine share one number: a
        // contract where a second body adds nothing reports the same gain
        // standing and moving, and StillnessPays is then simply false.
        int cap = Math.Max(1, lens.StationaryGainCap);
        if (Engaged)
        {
            // Teammates on the region are assumed to be holding their tiles,
            // because the same policy is deciding for all of them off the same
            // frozen observation and that is exactly what it tells them to do.
            int standing = AlliedOnRegion + (SelfOnRegion ? MyWeight : 0);
            int moving = AlliedOnRegion;
            GainIfIStand = Math.Clamp(standing - EnemyDenial, 0, cap);
            GainIfIMove = Math.Clamp(moving - EnemyDenial, 0, cap);
        }
        else if (lens.SurplusWeightScalesGain)
        {
            GainIfIStand = Math.Max(0, OwnDenial - EnemyDenial);
            GainIfIMove = GainIfIStand;
        }
        else
        {
            GainIfIStand = OwnDenial > 0 && EnemyDenial == 0 ? 1 : 0;
            GainIfIMove = GainIfIStand;
        }

        ResolveLethalGround(lens, context);
    }

    /// <summary>
    /// Marks every tile a visible body could put a killing contact on. The
    /// test is "damage at least my current health", so the same code maps a
    /// fan against a two-health prime, an ordinary bolt against a body already
    /// down to one, and nothing at all against a body that can survive
    /// anything on the board.
    /// </summary>
    private void ResolveLethalGround(
        ContractLens lens,
        GenericActorContext context)
    {
        int health = Math.Max(1, context.Self.Health);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            (int damage, int travel, bool straightOnly) =
                lens.HeaviestStrike(enemy.FormId);
            if (!Doctrine.LethalLanes || damage < health || travel <= 0)
                continue;
            MarkRays(lens, enemy.Position, travel);
            if (!straightOnly)
            {
                // A gun that bends owns more than its rays, but a bend costs
                // tiles of travel before it turns; the rays remain the tiles it
                // reaches soonest and with certainty, and marking the whole
                // reachable set would refuse the body every tile it has.
                continue;
            }
        }
    }

    private void MarkRays(ContractLens lens, Position from, int travel)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                for (int step = 1; step <= travel; step++)
                {
                    var tile = new Position(
                        from.X + (dx * step),
                        from.Y + (dy * step));
                    if (!lens.InBounds(tile) || lens.IsWall(tile))
                        break;
                    if (dx != 0
                        && dy != 0
                        && lens.StrictCorners
                        && !Tactics.ClearRay(lens, from, tile, strictCorners: true))
                    {
                        break;
                    }
                    _lethal.Add(tile);
                }
            }
        }
    }

    private static int Weight(ContractLens lens, string formId) =>
        lens.Form(formId)?.ObjectiveWeight ?? 0;
}
