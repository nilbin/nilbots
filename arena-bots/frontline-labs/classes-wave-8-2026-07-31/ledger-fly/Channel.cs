using BotArena.Sdk;

/// <summary>
/// THE CHANNEL — what taking ground is, on a contract that says a capture
/// counts only the bodies that did not change tile.
///
/// <para>The ledger's unit of account does not change: a capture is still
/// <c>threshold / gain</c> ticks of control and a body is still its own slot's
/// rebuild clock. What changes is which ticks are convertible. Three readings
/// out of the contract do all the work:</para>
///
/// <list type="number">
/// <item><b>Claim is stillness, denial is presence.</b> Our claim weight counts
/// only our bodies on the region whose tile did not change; the enemy's denial
/// weight counts all of theirs. So a defender who keeps moving still subtracts,
/// and one of our bodies that takes a step contributes nothing that tick — but
/// it has NOT stopped denying, which is what makes an in-region dodge cheap.</item>
/// <item><b>Surplus is capped.</b> Gain is
/// <c>min(cap, claim - denial)</c>. Above the cap another body on the point
/// buys exactly nothing, so the bodies past it are worth more standing on the
/// firing lines into it. This is the whole reason a numeric chassis is not
/// simply a bigger stack.</item>
/// <item><b>Damage on the point reverts the run.</b> Not one body's share — the
/// run. So one bolt into a stack of three costs the same as one bolt into a
/// stack of one, which prices a SCREEN (a body off the region, on the firing
/// line, that a bolt dies on for free) above a fourth claimer.</item>
/// </list>
///
/// <para>Everything here is inert-by-reading on a contract that does not
/// channel: the cap falls out at one, the interrupt at nothing, and every test
/// below answers the way revisions 3–6 answered it.</para>
/// </summary>
internal sealed class Channel
{
    private readonly List<Position> _claimers = [];

    /// <summary>Whether this contract channels at all.</summary>
    public bool Live { get; private set; }

    /// <summary>Our stationary weight on the active region this tick.</summary>
    public int ClaimWeight { get; private set; }

    /// <summary>Our total weight on the region, moving or not.</summary>
    public int OwnWeight { get; private set; }

    /// <summary>Every enemy body we can see on the region.</summary>
    public int DenialWeight { get; private set; }

    /// <summary>What one more STILL body of ours would add to the multiplier.</summary>
    public int Headroom { get; private set; }

    /// <summary>Whether our team currently owns the running claim.</summary>
    public bool Controlling { get; private set; }

    /// <summary>Whether an enemy claim is standing on the active region.</summary>
    public bool EnemyClaim { get; private set; }

    /// <summary>Ticks the standing enemy claim would take us to erode away.</summary>
    public int ErodeTicks { get; private set; }

    /// <summary>Tiles our own still bodies are channeling from.</summary>
    public IReadOnlyList<Position> Claimers => _claimers;

    /// <summary>
    /// Progress one contact on a controlling body standing on the region would
    /// revert. Zero when the contract declares no interrupt, when we are not
    /// the controller, or when there is no run to revert.
    /// </summary>
    public int RevertCost(MatchLens lens, GenericActorContext context, int damage)
    {
        if (!Live || lens.RevertPerDamage <= 0 || !Controlling)
            return 0;
        int run = context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? mode.CaptureProgress
            : 0;
        int reverted = damage * lens.RevertPerDamage;
        return Math.Min(run, reverted);
    }

    /// <summary>
    /// Reads this tick's channel state. Observation and contract only — no
    /// remembered tile — so every one of our lives computes the same numbers and
    /// the roles derived from them agree without a channel to talk over.
    ///
    /// <para><b>Stillness is read from the published resolution, not from
    /// memory.</b> A body's previous action and its authoritative outcome are on
    /// the observation for every one of our bodies including this one, and a
    /// blocked move did not move. A life with no previous resolution has just
    /// been created, and the rule counts a fresh body as stationary — so the
    /// reading agrees with the rule on the one case memory could never cover.
    /// </para>
    /// </summary>
    public void Observe(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> objective)
    {
        _claimers.Clear();
        Live = lens.Channels;
        ClaimWeight = 0;
        OwnWeight = 0;
        DenialWeight = 0;
        Controlling = false;
        EnemyClaim = false;
        ErodeTicks = 0;
        Headroom = 0;
        if (objective.Count == 0)
            return;

        if (objective.Contains(context.Self.Position))
        {
            int weight = Weight(lens, context.Self.FormId);
            OwnWeight += weight;
            if (Stationary(lens, context.Self.PreviousActionResolution))
            {
                ClaimWeight += weight;
                _claimers.Add(context.Self.Position);
            }
        }
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!objective.Contains(ally.Position))
                continue;
            int weight = Weight(lens, ally.FormId);
            OwnWeight += weight;
            if (Stationary(lens, ally.PreviousActionResolution))
            {
                ClaimWeight += weight;
                _claimers.Add(ally.Position);
            }
        }
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (objective.Contains(enemy.Position))
                DenialWeight += Weight(lens, enemy.FormId);
        }

        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode)
        {
            Controlling = mode.ClaimingTeamId == lens.TeamId
                && mode.CaptureProgress > 0;
            EnemyClaim = mode.ClaimingTeamId is int claimer
                && claimer != lens.TeamId
                && mode.CaptureProgress > 0;
            if (EnemyClaim)
            {
                int rate = Math.Max(1, lens.ErosionMultiplier);
                ErodeTicks = (mode.CaptureProgress + rate - 1) / rate;
            }
        }

        // What one more STILL body would buy. Above the cap it is zero, which is
        // the whole content of the escort line: the surplus stops paying and the
        // bodies past it go somewhere they still do.
        int surplus = ClaimWeight - DenialWeight;
        Headroom = Math.Max(0, Math.Min(lens.StationaryCap, surplus + 1) - Math.Max(0, surplus));
    }

    /// <summary>
    /// Whether this body should refuse a discretionary step. True only while the
    /// contract channels, this body is standing on the region, its stillness is
    /// actually buying progress (or denying a standing enemy claim its erosion),
    /// and the point is not paused. Every caller keeps its own escape hatches:
    /// a dodge, a fabrication and a shot all outrank a tick of gain.
    /// </summary>
    public bool HoldTile(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> objective)
    {
        if (!Doctrine.Still || !Live)
            return false;
        _ = lens;
        if (!objective.Contains(context.Self.Position))
            return false;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return false;
        }
        if (context.Tick < mode.ControlResumesAtTick)
            return false;
        // Standing still is only worth a tick when the stack is actually taking
        // ground with it — either building over their denial, or eroding a claim
        // they have already built. Otherwise the tile is worth no more than any
        // other tile of the region and the body is free to use its feet.
        return ClaimWeight > DenialWeight;
    }

    /// <summary>
    /// A screen tile: OFF the region, adjacent to a body we are protecting, and
    /// standing on the straight lane a known shooter would use to reach it.
    /// Damage taken there reverts nothing — the interrupt is scoped to bodies ON
    /// the region — and an allied bolt passes straight through us where the
    /// contract says allied contact is pass-through, so the screen costs the
    /// team nothing but the tile.
    /// </summary>
    public static int ScreenScore(
        MatchLens lens,
        GenericActorContext context,
        Position tile,
        IReadOnlyList<Position> protectees,
        IReadOnlyList<Position> shooters,
        IReadOnlySet<Position> objective)
    {
        if (protectees.Count == 0)
            return 0;
        // A screen that blocks our own return fire is not a screen, it is a
        // wall. The collision policy is contract data and this is the one rule
        // that depends on it.
        if (!lens.AlliedProjectilesPassAllies)
            return 0;
        if (objective.Contains(tile))
            return 0;

        int best = 0;
        foreach (Position guarded in protectees)
        {
            int gap = tile.ChebyshevDistance(guarded);
            if (gap is < 1 or > 2)
                continue;
            foreach (Position shooter in shooters)
            {
                // Between, on the lane, and nearer the shooter than the body it
                // is covering: that is the whole geometry of eating a bolt.
                if (shooter.ChebyshevDistance(guarded)
                    <= shooter.ChebyshevDistance(tile))
                {
                    continue;
                }
                if (!Field.RayReaches(lens, shooter, tile))
                    continue;
                if (!Aligned(shooter, tile, guarded))
                    continue;
                best = Math.Max(best, gap == 1 ? 12 : 8);
            }
        }
        return best;
    }

    /// <summary>
    /// Whether three tiles sit on one straight eight-way lane in order. A bolt
    /// flies exactly those lanes, so this is the whole test for "would a bolt
    /// aimed there pass through here first".
    /// </summary>
    private static bool Aligned(Position from, Position through, Position to)
    {
        int fx = Math.Sign(through.X - from.X);
        int fy = Math.Sign(through.Y - from.Y);
        int tx = Math.Sign(to.X - through.X);
        int ty = Math.Sign(to.Y - through.Y);
        if (fx != tx || fy != ty)
            return false;
        int adx = Math.Abs(through.X - from.X);
        int ady = Math.Abs(through.Y - from.Y);
        if (adx != 0 && ady != 0 && adx != ady)
            return false;
        int bdx = Math.Abs(to.X - through.X);
        int bdy = Math.Abs(to.Y - through.Y);
        return bdx == 0 || bdy == 0 || bdx == bdy;
    }

    private static bool Stationary(
        MatchLens lens,
        GenericActorActionResolution? resolution)
    {
        // No resolution at all is a life created this tick. The rule counts it
        // as stationary — it has no previous position to have changed.
        if (resolution is null)
            return true;
        if (resolution.Outcome != GenericActorActionResolution.ActionOutcome.Success)
            return true;
        return !lens.MovementActionIds.Contains(
            resolution.AcceptedAction.ActionId);
    }

    private static int Weight(MatchLens lens, string formId) =>
        lens.Form(formId)?.ObjectiveWeight ?? 1;
}
