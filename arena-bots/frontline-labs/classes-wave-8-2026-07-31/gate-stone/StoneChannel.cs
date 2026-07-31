using BotArena.Sdk;

/// <summary>
/// The channel: what a tick of standing still is actually worth.
///
/// <para>Wave 6's ledger traded in ONE TICK OF OBJECTIVE WEIGHT, because under
/// every arm it had played, presence was the whole of capture. The channel
/// splits presence into two different quantities that no longer travel
/// together — a CLAIM, which counts only bodies that did not change tile this
/// tick, and a DENIAL, which counts all of them — and puts a CAP on how much
/// surplus can buy. So the unit of the ledger changes: a body is priced by the
/// progress its stillness (or its denial) moves the claim by THIS tick, and
/// that number is frequently zero for a body the old arithmetic thought was
/// pulling its weight. The third body on a capped point, the second denier
/// against a single channeler, every body during the redeploy pause: all zero.
/// Ticks priced at zero are the wave's whole opening — they are spent on the
/// gun, on the turret, on a pile, or on an upgrade.</para>
///
/// <para>Every number below comes from <c>gameMode.capture</c>. Where the
/// contract omits a field the rule it drives is inert, so the same code plays
/// the pre-channel arms with the pre-channel arithmetic.</para>
/// </summary>
internal static class StoneChannel
{
    /// <summary>The live channel, as one body of ours sees it.</summary>
    /// <param name="Active">True when this ruleset channels at all.</param>
    /// <param name="OwnClaim">
    /// Our stationary weight on the active objective — the bodies that will
    /// count if they hold their tile.
    /// </param>
    /// <param name="OwnDenial">All our weight on the objective.</param>
    /// <param name="EnemyClaim">Enemy stationary weight (a lower bound).</param>
    /// <param name="EnemyDenial">All enemy weight seen (a lower bound).</param>
    /// <param name="SelfOn">This body stands on the active objective.</param>
    /// <param name="SelfHeld">
    /// This body did not change tile on the way into this tick, so it is
    /// already inside the claim count as well as the denial count.
    /// </param>
    /// <param name="Claiming">The team whose claim currently stands, or null.</param>
    /// <param name="Progress">Current published capture progress.</param>
    /// <param name="Eroding">
    /// True when our control would erode a standing ENEMY claim rather than
    /// build our own — worth the declared multiple, and the reason a
    /// recapture is urgent rather than merely wanted.
    /// </param>
    public sealed record Field(
        bool Active,
        int OwnClaim,
        int OwnDenial,
        int EnemyClaim,
        int EnemyDenial,
        bool SelfOn,
        bool SelfHeld,
        int? Claiming,
        int Progress,
        bool Eroding);

    /// <summary>
    /// Reads the channel for one body of ours.
    ///
    /// <para>STATIONARITY IS OBSERVED ONE TICK LATE, deliberately. The rule is
    /// "did not change tile this tick", and this tick's moves have not
    /// happened when the observation freezes — so a body counts as stationary
    /// here when its published tile equals the tile it published last tick.
    /// For an enemy that is the only evidence there is. For a body of ours it
    /// is a lower bound we then correct where it matters: the marginal value
    /// of THIS body is computed counterfactually, from the decision this tick
    /// is about to make, not from the last one.</para>
    /// </summary>
    public static Field Read(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory)
    {
        Position[] objective = StoneAim.ActiveObjective(lens, context);
        int? claiming = null;
        int progress = 0;
        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode)
        {
            claiming = mode.ClaimingTeamId;
            progress = mode.CaptureProgress;
        }
        bool active = StoneDoctrine.ChannelArithmetic
            && lens.Channel.StationaryClaim
            && objective.Length > 0;

        int ownClaim = 0;
        int ownDenial = 0;
        int enemyClaim = 0;
        int enemyDenial = 0;
        bool selfOn = false;
        bool selfHeld = false;
        foreach (Position tile in objective)
        {
            if (tile == context.Self.Position)
                selfOn = true;
        }
        if (selfOn)
        {
            int weight = lens.Weight(context.Self.FormId);
            ownDenial += weight;
            selfHeld = !active
                || memory.HeldTile(
                    context.Self.ActorId,
                    context.Self.Position);
            if (selfHeld)
                ownClaim += weight;
        }
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!Contains(objective, ally.Position))
                continue;
            int weight = lens.Weight(ally.FormId);
            ownDenial += weight;
            if (!active || memory.HeldTile(ally.ActorId, ally.Position))
                ownClaim += weight;
        }
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (!Contains(objective, enemy.Position))
                continue;
            int weight = lens.Weight(enemy.FormId);
            enemyDenial += weight;
            if (!active || memory.HeldTile(enemy.ActorId, enemy.Position))
                enemyClaim += weight;
        }

        bool eroding = lens.Channel.ErosionMultiple > 0
            && progress > 0
            && claiming is int owner
            && owner != lens.TeamId;
        return new Field(
            active,
            ownClaim,
            ownDenial,
            enemyClaim,
            enemyDenial,
            selfOn,
            selfHeld,
            claiming,
            progress,
            eroding);
    }

    /// <summary>
    /// Progress one side gains in a tick from a claim against a denial, under
    /// the declared cap. Below the pre-channel arms this collapses to the
    /// binary answer their control policy declares, so the caller never has to
    /// know which game it is in.
    /// </summary>
    public static int Gain(
        StoneContract lens,
        Field field,
        int claim,
        int denial)
    {
        int surplus = claim - denial;
        if (surplus <= 0)
            return 0;
        if (!field.Active)
            return lens.Channel.GainPerTick;
        int cap = lens.Channel.StationaryCap;
        if (cap > 0)
            surplus = Math.Min(surplus, cap);
        return surplus * lens.Channel.GainPerTick;
    }

    /// <summary>
    /// THE UNIT OF THE LEDGER: progress this body's presence is worth for one
    /// tick, counting BOTH directions of the claim.
    ///
    /// <para>A body earns by building — the difference its stillness makes to
    /// our own gain — or by denying, the difference its body makes to the
    /// enemy's. It is credited with the larger of the two and never with both,
    /// because they cannot happen on the same tick. Erosion multiplies the
    /// building half, because a tick spent eroding takes back progress the
    /// enemy has to pay for again at the full rate.</para>
    ///
    /// <para>The zeros are the interesting part and they are all real. A third
    /// body against a dead defence adds nothing, because the cap is 2. A
    /// second denier against one channeler adds nothing, because the first
    /// already reduced the gain to zero. Nobody adds anything while the
    /// redeploy pause runs. Those are the ticks this doctrine spends
    /// elsewhere.</para>
    /// </summary>
    public static int Marginal(
        StoneContract lens,
        Field field,
        int weight,
        bool onObjective,
        bool frozen,
        bool throttled) =>
        onObjective
            ? Split(lens, field, weight, field.SelfHeld, true, frozen, throttled)
                .Value
            : 0;

    /// <summary>
    /// What this body would be worth IF IT WENT AND STOOD ON THE POINT — the
    /// opportunity cost of doing anything else, and the number every trade in
    /// this doctrine is priced against.
    ///
    /// <para>Asking the marginal question of a body where it currently stands
    /// answers zero for every body that is not already on the objective, which
    /// makes "fortify only when your presence is worth nothing" fire for the
    /// relief body walking to the fight. The forfeit is not the tile you are
    /// on; it is the tile you could be on.</para>
    /// </summary>
    public static int StandingWorth(
        StoneContract lens,
        Field field,
        int weight,
        bool frozen,
        bool throttled) =>
        // THE LEASE IS NOT PRICED ON THE CHANNEL, AND THAT IS A MEASUREMENT,
        // NOT AN OVERSIGHT. The obvious next step from everything above is to
        // charge a fortify, an errand or a held anchor the channel marginal —
        // zero inside the surplus cap, zero behind a sibling that already
        // denies, zero in the pause. It is the same arithmetic the station
        // uses, it is right about what the tick EARNS, and over the same six
        // games it cost TWENTY-FIVE points of aggregate progress: −19 with it
        // against +6 with wave 6's flat "one tick of my own objective weight"
        // in its place. Every other rule in this wave measured the same or
        // better in both configurations, so the loss is this line's alone.
        //
        // Why: the channel marginal is a number about THIS TICK, and a lease
        // is a decision about the next twenty. A body that is the third on a
        // capped point is worth zero right now and worth one the moment a
        // sibling dies, which on these cells is never more than a few ticks
        // away — and by then it is a turret with a windup to pay and a
        // half-built claim it cannot join. The flat weight is not a worse
        // estimate of this tick; it is a better estimate of the window the
        // decision actually spans. What the channel legitimately zeroes is the
        // ticks a CLOCK has already zeroed — the redeploy pause, and a
        // completion that would be spent inside an enemy hold — and those two
        // are exactly what this expression keeps.
        frozen || throttled ? 0 : weight;

    /// <summary>What a body is worth, with the two halves kept apart.</summary>
    /// <param name="Build">
    /// Progress this body's STILLNESS buys. Only this half is a reason to
    /// refuse a step, because only a claim counts stillness.
    /// </param>
    /// <param name="Deny">
    /// Progress this body's PRESENCE refuses the enemy. Denial counts every
    /// body on the region whether it moved or not, so this half is a reason to
    /// stay in the region and never a reason to stand still — confusing the
    /// two is a body eating bolts for a rule that does not ask it to.
    /// </param>
    public sealed record Worth(int Build, int Deny)
    {
        /// <summary>The larger half: they cannot both happen in a tick.</summary>
        public int Value => Math.Max(Math.Max(Build, Deny), 0);
    }

    /// <summary>
    /// The two halves of a body's worth. See <see cref="Marginal"/> for the
    /// reasoning; this is the same arithmetic with the halves labelled,
    /// because the channel discipline may only spend the building one.
    /// </summary>
    public static Worth Split(
        StoneContract lens,
        Field field,
        int weight,
        bool claimCounted,
        bool denyCounted,
        bool frozen,
        bool throttled)
    {
        if (weight <= 0 || frozen || throttled)
            return new Worth(0, 0);

        // Both halves are the same difference: the board WITH this body
        // standing on the point against the board WITHOUT it. The two counted
        // flags say which side of that difference the published numbers are
        // already on — a body on the point that moved this tick is inside the
        // denial count and outside the claim count, which is precisely the
        // asymmetry the channel introduced.
        int claimBase = Math.Max(
            field.OwnClaim - (claimCounted ? weight : 0),
            0);
        int denyBase = Math.Max(
            field.OwnDenial - (denyCounted ? weight : 0),
            0);

        int multiple = field.Eroding
            ? Math.Max(lens.Channel.ErosionMultiple, 1)
            : 1;
        int buildWith =
            Gain(lens, field, claimBase + weight, field.EnemyDenial);
        int buildWithout = Gain(lens, field, claimBase, field.EnemyDenial);
        int build = (buildWith - buildWithout) * multiple;

        int denyWith =
            Gain(lens, field, field.EnemyClaim, denyBase + weight);
        int denyWithout = Gain(lens, field, field.EnemyClaim, denyBase);
        // The enemy's erosion of OUR standing claim is worth the same multiple
        // to them, so refusing it is worth it to us.
        int enemyMultiple =
            lens.Channel.ErosionMultiple > 0
            && field.Progress > 0
            && field.Claiming == lens.TeamId
                ? Math.Max(lens.Channel.ErosionMultiple, 1)
                : 1;
        int deny = (denyWithout - denyWith) * enemyMultiple;

        return new Worth(Math.Max(build, 0), Math.Max(deny, 0));
    }

    /// <summary>
    /// Progress a hostile hit costs us right now: the interrupt only bites
    /// bodies of the CONTROLLING team standing ON the objective, so a bolt
    /// into a screen — or into anybody at all while we hold nothing — costs
    /// health and no ground whatsoever. That asymmetry is the whole reason the
    /// screen exists.
    /// </summary>
    public static int RevertCost(
        StoneContract lens,
        Field field,
        int damage)
    {
        if (!field.Active || damage <= 0 || lens.Channel.RevertPerDamage <= 0)
            return 0;
        if (!field.SelfOn || field.Claiming != lens.TeamId)
            return 0;
        return Math.Min(damage * lens.Channel.RevertPerDamage, field.Progress);
    }

    /// <summary>
    /// Whether a body of ours is currently CHANNELLING — standing on the
    /// objective, holding its tile, and buying something by doing so. This is
    /// the body a screen protects and the body the crew must not shove.
    /// </summary>
    public static bool Channeller(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        StoneGround.Body who,
        Field field)
    {
        if (!field.Active || lens.Weight(who.FormId) <= 0)
            return false;
        if (!Contains(StoneAim.ActiveObjective(lens, context), who.Position))
            return false;
        return memory.HeldTile(who.ActorId, who.Position);
    }

    /// <summary>
    /// Whether any body of ours other than this one is currently channelling
    /// on the point we control — the question "is there a run to protect?",
    /// which is the difference between a screen and an idle body.
    /// </summary>
    public static bool AnyChanneller(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        Field field)
    {
        if (!field.Active || field.Claiming != lens.TeamId)
            return false;
        Position[] objective = StoneAim.ActiveObjective(lens, context);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.Weight(ally.FormId) > 0
                && Contains(objective, ally.Position)
                && memory.HeldTile(ally.ActorId, ally.Position))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// How well a tile screens <paramref name="channeller"/>: the number of
    /// armed enemies whose bolt into that body would have to pass through the
    /// tile first.
    ///
    /// <para>This is not a new mechanic and nothing was added for it. The
    /// collision model already declares that an enemy bolt stops on the first
    /// enemy actor it meets and that allied bolts pass through allies, so a
    /// body parked on the firing line eats the bolt and does not block the
    /// return fire. Both facts are read from <c>rules.collisions</c>; where a
    /// contract declares otherwise the score is zero and the rule is
    /// inert.</para>
    /// </summary>
    public static int ScreenValue(
        StoneContract lens,
        GenericActorContext context,
        Position tile,
        Position channeller)
    {
        if (!lens.BoltsStopOnFirstEnemy)
            return 0;
        if (tile == channeller)
            return 0;
        int screened = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? gun =
                lens.Attack(enemy.FormId);
            if (gun is null)
                continue;
            if (enemy.Position.ChebyshevDistance(channeller)
                > gun.Projectile.MaxTravelTiles + 2)
            {
                continue;
            }
            if (Between(lens, enemy.Position, tile, channeller))
                screened++;
        }
        return screened;
    }

    /// <summary>
    /// Whether <paramref name="middle"/> sits on the straight eight-way lane
    /// from <paramref name="from"/> to <paramref name="to"/>, strictly between
    /// them, with no wall in the way. A bent shot can go round it; the point of
    /// a screen is that going round costs the shooter its bend and its tempo,
    /// not that it is impossible.
    /// </summary>
    private static bool Between(
        StoneContract lens,
        Position from,
        Position middle,
        Position to)
    {
        int stepX = Math.Sign(to.X - from.X);
        int stepY = Math.Sign(to.Y - from.Y);
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        if (dx != 0 && dy != 0 && dx != dy)
            return false;
        Position cursor = from;
        while (cursor != to)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (lens.IsWall(next))
                return false;
            if (next == middle)
                return true;
            cursor = next;
        }
        return false;
    }

    private static bool Contains(Position[] tiles, Position tile)
    {
        foreach (Position candidate in tiles)
        {
            if (candidate == tile)
                return true;
        }
        return false;
    }
}
