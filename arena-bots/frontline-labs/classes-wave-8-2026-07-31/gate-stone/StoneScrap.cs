using BotArena.Sdk;

/// <summary>
/// The economy, priced in the only unit this lineage trades in: TICKS OF
/// CHANNEL.
///
/// <para>A pile is worth scrap and a tier is worth a tile of gun, sight or
/// health — but going to get it is worth whatever the body was doing instead,
/// and under the channel that is a number rather than a feeling. A body whose
/// stillness is buying progress has a price per tick; a body inside the surplus
/// cap, inside the redeploy pause, or behind a sibling that already denies the
/// point has a price of exactly zero. So the rule is not "harvest" or "do not
/// harvest": it is <b>spend the ticks that are already worth nothing</b>, which
/// on a live front means never leaving and in a pause means walking.</para>
///
/// <para>Two contract facts make that cheap rather than heroic. The assay is
/// paid AT THE TILE with no transport, so a pile stepped on in passing is
/// banked whether or not the body ever goes home; and every destroyed body
/// leaves a wreck where it fell, which for a doctrine that fights at the
/// objective means the deposits are a bonus and the corpses are the income.
/// Ignoring the deposit channel entirely costs about one tier, and this
/// doctrine is content to pay it whenever the front is live.</para>
/// </summary>
internal static class StoneScrap
{
    /// <summary>
    /// How far a body will detour for a pile per unit of scrap on it. Small on
    /// purpose: the walk is priced against the channel, and the channel is
    /// worth up to two progress a tick out of a threshold of eight.
    /// </summary>
    private const int TicksPerScrap = 2;

    /// <summary>
    /// Tiles this body should walk to in order to collect, or empty when the
    /// errand is not worth the ticks. Ordered by value; the caller routes to
    /// the first reachable one exactly as it routes to a station.
    /// </summary>
    public static Position[] Errand(
        StoneContract lens,
        GenericActorContext context,
        StoneGround.Push push,
        int freeTicks)
    {
        if (!StoneDoctrine.Salvage || lens.Economy is not StoneContract.ScrapRules economy)
            return [];
        // A zero-weight form cannot pick up or carry at all, and completing a
        // transition into one drops the load on the floor. Read the weight, do
        // not name the turret.
        if (lens.Weight(context.Self.FormId) <= 0)
            return [];
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return [];
        }

        // The price of the errand is what this body's presence is worth per
        // tick. On a live front that is 1 or 2 and nothing is ever worth the
        // walk; in a pause, behind a denier, or past the surplus cap it is 0
        // and the walk is free.
        int perTick = push.WeightTick;

        // A full load banks itself by ending a tick on our own pad, at no
        // action cost — so a carrier that has stopped earning walks home. It is
        // the only errand this doctrine will start from a live front, and only
        // when the front does not need the body.
        if (context.Self.CarriedScrap >= economy.CarryCapacity
            && perTick == 0
            && lens.Bank.Length > 0)
        {
            return lens.Bank;
        }

        var wanted = new List<(Position Tile, int Score)>();
        foreach (GenericActorContext.ScrapPile pile in mode.ScrapPiles)
        {
            int expiry = pile.ExpiresAtTick - context.Tick;
            if (expiry <= 0)
                continue;
            int distance =
                context.Self.Position.ChebyshevDistance(pile.Position);
            if (distance > expiry)
                continue;
            // THE WINDOW HAS TO BE LONG ENOUGH TO WALK IT. A redeploy pause
            // prices this body's presence at zero for five ticks, which is a
            // real free window and a completely useless one for a pile eight
            // tiles away: the body leaves, the pause lapses, and the point is
            // uncontested with nobody on it. So an errand is only started when
            // the window it is being paid out of covers the walk. A structural
            // zero — past the surplus cap, or behind a sibling that already
            // denies — has no clock on it and passes freely.
            if (distance > freeTicks)
                continue;

            // What this pile pays us: the assay is banked on contact whatever
            // happens next, and the remainder is only worth something if this
            // body survives to stand on its own pad, so it is discounted.
            int room = Math.Max(
                economy.CarryCapacity - context.Self.CarriedScrap,
                0);
            int carried = Math.Min(Math.Max(pile.Amount - economy.Assay, 0), room);
            int worth = economy.Assay + (carried / 2);
            if (worth <= 0)
                continue;
            // The ticks it costs are the ticks the front loses. Both sides of
            // this comparison are in the same unit, which is the only reason it
            // is a decision rather than a preference.
            if (distance * perTick > worth * TicksPerScrap)
                continue;
            wanted.Add((pile.Position, (worth * 8) - distance));
        }
        if (wanted.Count == 0)
            return [];
        wanted.Sort((left, right) => right.Score.CompareTo(left.Score));
        var tiles = new Position[wanted.Count];
        for (int index = 0; index < wanted.Count; index++)
            tiles[index] = wanted[index].Tile;
        return tiles;
    }

    /// <summary>
    /// A purchase, or null when none is legal or worth a tick.
    ///
    /// <para>Affordability, the per-track ceiling and the team-wide cap all
    /// live in the legality mask, so this method never prices the ladder: it
    /// asks which tracks are offered THIS tick and picks between them on
    /// declared EFFECT rather than on track name, so a future ruleset that
    /// renames a track or adds a fourth is handled without an edit.</para>
    ///
    /// <para>The order is the class's own arithmetic. A bulwark shoots six and
    /// sees four, against a striker that shoots eight: the standoff is the
    /// thing that loses the duel, so travel comes first and closes a two-tile
    /// deficit exactly. After that, sight — a gun that outranges its own eyes
    /// fires at bodies the team has to lend it — and last the plate, which
    /// raises the ceiling but never heals, so it pays out only on the next
    /// life.</para>
    /// </summary>
    public static GenericActorDecision? Invest(
        StoneContract lens,
        GenericActorContext context,
        string reason)
    {
        if (!StoneDoctrine.Invest
            || lens.Economy is not StoneContract.ScrapRules economy
            || !economy.BuyableByAction)
        {
            return null;
        }
        foreach (GenericActorActionLegality legality in context.ActionLegalities)
        {
            if (!legality.Available)
                continue;
            GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
                tracks = null;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in legality.Constraints)
            {
                if (constraint
                    is GenericActorActionLegality.ArgumentConstraint
                        .UpgradeTrackConstraint candidate)
                {
                    tracks = candidate;
                }
            }
            if (tracks is null || tracks.AllowedTrackIds.Length == 0)
                continue;

            string? chosen = null;
            int bestRank = int.MaxValue;
            foreach (string trackId in tracks.AllowedTrackIds)
            {
                // THE TRACK THIS DOCTRINE IS NOT ALLOWED TO BUY, AND WHY.
                //
                // A tier whose declared effect changes projectile TRAVEL fails
                // the match — not the action, the match: the run aborts with
                // "A retained projectile must preserve its exact resolved
                // committed path" and writes no replay at all. It is not this
                // bot's mistake and it is not the verb's: the pre-registered
                // CONTROL arm `--economy scrap-flat`, which buys greedily with
                // no verb at all, kills a match between two copies of a bot
                // that contains no economy code, on three of the four class
                // cells I tried. The purchase is only fatal when a projectile
                // of the buying team is retained across the settle, which is
                // why the same tier completes a match when it happens to land
                // on a quiet tick.
                //
                // No sound guard exists from inside a bot. A purchase settles
                // AFTER the tick's launches, so the dangerous bolt can be one a
                // TEAMMATE fires on this very tick — and a teammate's next
                // action is the one commitment the observation does not
                // publish. Our own bolts are not even fully visible to us: this
                // chassis sees four tiles and shoots six, so a bolt of ours can
                // outrun our own perception. I built the two guards that look
                // sufficient (no own bolt in the union; no bolt at all in the
                // union) and measured both still faulting.
                //
                // So the track is refused outright. It costs this doctrine the
                // purchase its own class most wants — a bulwark that closes a
                // 6-against-8 standoff wins the duel it currently loses — and I
                // would rather forfeit a tier than ship an artifact that can
                // abort somebody else's cell. The refusal is written against the
                // declared EFFECT, so it lifts by itself on any ruleset whose
                // ladder does not move a bolt.
                if (ChangesFlight(lens, trackId))
                    continue;
                int rank = Rank(lens, context, trackId);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    chosen = trackId;
                }
            }
            if (chosen is null)
                continue;
            return new GenericActorDecision(
                legality.ActionId,
                legality.ActionCode,
                [new GenericActorActionArgument.UpgradeTrackArgument(chosen)],
                $"investing in {chosen} — {reason}");
        }
        return null;
    }

    /// <summary>
    /// Whether a track's declared effect alters a projectile already in the
    /// air. Read from the effect ID rather than the track name, so a future
    /// track that also moves a bolt's geometry is covered without an edit.
    /// </summary>
    private static bool ChangesFlight(StoneContract lens, string trackId)
    {
        if (lens.Economy is not StoneContract.ScrapRules economy)
            return false;
        foreach (GenericActorRulesContract.ScrapUpgradeTrack track
                 in economy.Tracks)
        {
            if (string.Equals(track.TrackId, trackId, StringComparison.Ordinal))
            {
                return track.Effect.Contains(
                    "travel-tiles",
                    StringComparison.Ordinal);
            }
        }
        return false;
    }

    /// <summary>
    /// Preference between offered tracks, lower first, decided by what the
    /// track's declared effect does to THIS body's declared numbers against
    /// the guns the contract says it is facing.
    /// </summary>
    private static int Rank(
        StoneContract lens,
        GenericActorContext context,
        string trackId)
    {
        if (lens.Economy is not StoneContract.ScrapRules economy)
            return int.MaxValue;
        string? effect = null;
        foreach (GenericActorRulesContract.ScrapUpgradeTrack track
                 in economy.Tracks)
        {
            if (string.Equals(track.TrackId, trackId, StringComparison.Ordinal))
                effect = track.Effect;
        }
        if (effect is null)
            return int.MaxValue;

        // Effective, not declared: a tier is resolved against the form
        // catalog's base at the point of use, and both operands are published.
        // Ranking on the base numbers would buy the same track twice.
        GenericActorRulesContract.AttackProfile? gun =
            lens.Attack(context.Self.FormId);
        int myTravel = (gun?.Projectile.MaxTravelTiles ?? 0)
            + lens.Tier(context, lens.TeamId, "travel-tiles");
        int mySight = lens.Sight(context.Self.FormId)
            + lens.Tier(context, lens.TeamId, "vision-range");
        int theirTravel = lens.LongestOpposingTravel()
            + OpposingTier(lens, context, "travel-tiles");

        if (effect.Contains("travel-tiles", StringComparison.Ordinal))
            return theirTravel > myTravel ? 0 : 3;
        if (effect.Contains("vision-range", StringComparison.Ordinal))
            return mySight < myTravel ? 1 : 4;
        if (effect.Contains("max-health", StringComparison.Ordinal))
            return 2;
        return 5;
    }

    /// <summary>
    /// The best tier any opposing team holds on one effect. Both teams' banks
    /// and tiers are public, so an enemy that just bought reach is a fact and
    /// not an inference — the mode state moves on the tick it happens.
    /// </summary>
    private static int OpposingTier(
        StoneContract lens,
        GenericActorContext context,
        string effectFragment)
    {
        int best = 0;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return 0;
        }
        foreach (GenericActorContext.ScrapTeamState team in mode.ScrapTeams)
        {
            if (team.TeamId != lens.TeamId)
            {
                best = Math.Max(
                    best,
                    lens.Tier(context, team.TeamId, effectFragment));
            }
        }
        return best;
    }
}
