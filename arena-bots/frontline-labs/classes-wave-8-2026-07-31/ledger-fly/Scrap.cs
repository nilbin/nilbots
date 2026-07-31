using BotArena.Sdk;

/// <summary>
/// THE STORE, and the walk that fills it.
///
/// <para>The doctrine does not change for a resource: scrap is booked in the
/// same convertible objective-tick as health, bodies and ground. What one scrap
/// is worth is derived, not tuned — a tier costs a declared number of scrap and
/// buys a declared step on a declared track, and this bot already prices a body
/// at its own slot's rebuild clock. So a point of the bank's maximum health is
/// <c>bodyTicks / maxHealth</c> ticks, one tier of it costs <c>tierCost</c>
/// scrap, and one scrap is the quotient. Every term is contract data.</para>
///
/// <para><b>The pot is fixed and the ceiling is low.</b> The contract declares
/// how many tiers a team may ever hold and what each costs, so the whole
/// economy is worth a bounded number of ticks and the correct amount of
/// attention drops to zero the moment the ladder is bought out. That stopping
/// condition is the single most important line here: a team that keeps
/// harvesting after its last tier is a team fighting a body light for
/// nothing.</para>
///
/// <para><b>Extra bodies buy security, not income.</b> One courier services a
/// whole cycle, so the second body sent out is wasted. Exactly one of our
/// bodies carries at a time, chosen by a rule every one of our lives evaluates
/// identically off the frozen observation — nearest to the standing pile, with
/// a body that is already loaded keeping the job — and the rest of the roster
/// stays on the front where the channel is being fought.</para>
/// </summary>
internal sealed class Scrap
{
    private readonly List<GenericActorContext.ScrapPile> _piles = [];

    /// <summary>Whether this contract declares an economy at all.</summary>
    public bool Live { get; private set; }

    /// <summary>Our team's unspent bank.</summary>
    public int Bank { get; private set; }

    /// <summary>Tier held on each declared track, in contract order.</summary>
    public IReadOnlyList<int> Tiers { get; private set; } = [];

    /// <summary>Tiers this team may still buy under the declared total cap.</summary>
    public int TiersRemaining { get; private set; }

    /// <summary>Scrap still needed before the ladder is bought out.</summary>
    public int Shortfall { get; private set; }

    /// <summary>Whether this body is the team's designated buyer this tick.</summary>
    public bool IsBuyer { get; private set; }

    /// <summary>Whether this body is the team's designated courier this tick.</summary>
    public bool IsCourier { get; private set; }

    /// <summary>The pile the courier is walking to, or null.</summary>
    public Position? Quarry { get; private set; }

    /// <summary>What this body is carrying right now.</summary>
    public int Carrying { get; private set; }

    /// <summary>Tiles of our own bank region.</summary>
    public IReadOnlySet<Position> Vault => _vault;

    private HashSet<Position> _vault = [];

    /// <summary>Live piles, ordered as the observation published them.</summary>
    public IReadOnlyList<GenericActorContext.ScrapPile> Piles => _piles;

    /// <summary>
    /// Ticks one scrap is worth, from the ladder's own prices and this bot's own
    /// price of a body. Zero once the ladder is bought out, which is what turns
    /// the courier line off rather than leaving it running on habit.
    /// </summary>
    public int TickValue { get; private set; }

    public void Observe(
        MatchLens lens,
        GenericActorContext context,
        Channel channel)
    {
        _piles.Clear();
        Live = false;
        Bank = 0;
        Tiers = [];
        TiersRemaining = 0;
        Shortfall = 0;
        IsBuyer = false;
        IsCourier = false;
        Quarry = null;
        TickValue = 0;
        Carrying = context.Self.CarriedScrap;
        _vault = lens.BankTiles;

        if (lens.Economy is not GenericActorRulesContract.FrontlineScrapEconomy
            economy)
        {
            return;
        }
        Live = true;

        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode)
        {
            foreach (GenericActorContext.ScrapTeamState team in mode.ScrapTeams)
            {
                if (team.TeamId != lens.TeamId)
                    continue;
                Bank = team.Bank;
                Tiers = team.TierLevels;
            }
            foreach (GenericActorContext.ScrapPile pile in mode.ScrapPiles)
            {
                if (context.Tick < pile.ExpiresAtTick)
                    _piles.Add(pile);
            }
        }

        int held = 0;
        foreach (int tier in Tiers)
            held += tier;
        TiersRemaining = Math.Max(0, economy.MaxTotalTiers - held);

        // What is still buyable, and what it costs. A track already at its own
        // maximum is off the ladder even while the total cap has room.
        int cost = 0;
        int cheapest = int.MaxValue;
        int budget = TiersRemaining;
        for (int index = 0;
             index < economy.Tracks.Length && budget > 0;
             index++)
        {
            GenericActorRulesContract.ScrapUpgradeTrack track =
                economy.Tracks[index];
            int tier = index < Tiers.Count ? Tiers[index] : 0;
            for (int next = tier; next < track.MaxTier && budget > 0; next++)
            {
                int price = next < track.TierCosts.Length
                    ? track.TierCosts[next]
                    : 0;
                cost += price;
                cheapest = Math.Min(cheapest, price);
                budget--;
            }
        }
        Shortfall = Math.Max(0, cost - Bank);
        if (cheapest == int.MaxValue)
            cheapest = 0;

        // One scrap in ticks. The bank's body is worth its own return clock plus
        // every pipeline clock it stalls, one tier of maximum health is a share
        // of that body, and a tier costs the declared price.
        if (cheapest > 0 && TiersRemaining > 0)
        {
            int bodyTicks = lens.ReplacementTicks(lens.BankUnitId)
                + lens.PipelineStallTicks;
            int share = Math.Max(1, lens.MaxHealth(lens.BankFormId));
            TickValue = Math.Max(1, bodyTicks / (share * cheapest));
        }

        IsBuyer = Doctrine.Invest
            && lens.BuysByHand
            && TiersRemaining > 0
            && Buyer(lens, context) == context.Self.ActorId.UnitId;
        AssignCourier(lens, context, channel);
    }

    /// <summary>
    /// The team's buyer: the economy anchor while it is alive, otherwise the
    /// lowest live unit. Two of our bodies investing against a bank that covers
    /// one resolves in canonical order and blocks the second, so the team names
    /// exactly one — and it names it off the frozen observation, so every life
    /// names the same one.
    /// </summary>
    private static int Buyer(MatchLens lens, GenericActorContext context)
    {
        int best = int.MaxValue;
        int bank = int.MaxValue;
        if (lens.IsAlliedBankUnit(context.Self.ActorId.UnitId))
            bank = context.Self.ActorId.UnitId;
        best = Math.Min(best, context.Self.ActorId.UnitId);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsAlliedBankUnit(ally.ActorId.UnitId))
                bank = Math.Min(bank, ally.ActorId.UnitId);
            best = Math.Min(best, ally.ActorId.UnitId);
        }
        return bank == int.MaxValue ? best : bank;
    }

    /// <summary>
    /// Names the one body that goes out, and where it goes. The rule is a pure
    /// function of the frozen observation — a loaded body keeps the job, else
    /// the non-bank body nearest the best standing pile takes it — so all of our
    /// lives agree on who is out there without a word passing between them.
    /// </summary>
    private void AssignCourier(
        MatchLens lens,
        GenericActorContext context,
        Channel channel)
    {
        if (!Doctrine.Courier || !Live)
            return;
        // The stopping condition. Once the ladder is bought out, or the bank
        // already covers everything left on it, a body walking to a deposit is
        // a body missing from the channel for nothing.
        if (TiersRemaining <= 0 || (Shortfall <= 0 && Carrying == 0))
            return;

        // A loaded body is already the courier, whoever it is — and it finishes
        // its walk whatever the roster looks like, because a load abandoned on
        // the field is the wreck the other side collects.
        int loadedUnit = int.MaxValue;
        int loadedLoad = 0;
        if (Carrying > 0)
        {
            loadedUnit = context.Self.ActorId.UnitId;
            loadedLoad = Carrying;
        }
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (ally.CarriedScrap > loadedLoad
                || (ally.CarriedScrap > 0
                    && ally.CarriedScrap == loadedLoad
                    && ally.ActorId.UnitId < loadedUnit))
            {
                loadedUnit = ally.ActorId.UnitId;
                loadedLoad = ally.CarriedScrap;
            }
        }
        if (loadedLoad > 0)
        {
            IsCourier = loadedUnit == context.Self.ActorId.UnitId;
            if (IsCourier)
                Quarry = Best(lens, context, context.Self.Position);
            return;
        }

        // THE ALLOCATION GATE, and it is the whole reason this line is safe to
        // ship. The pot is fixed and one courier services a cycle, so extra
        // bodies buy SECURITY of collection rather than income — which means a
        // body may only leave when the roster actually has one to spare. The
        // surplus is measured against the opposition's DECLARED slot capacity at
        // this tick rather than against what we can see, because a
        // facing-quadrant sensor union sees a fraction of their field and the
        // roster is contract data. Against a shallower roster this opens on the
        // second deposit and pays for the ladder; in a mirror it never opens at
        // all, which is correct: two defenders who keep moving hold three
        // stationary attackers, so a mirror has no spare body by construction.
        //
        // The second half is the same thought about time rather than bodies: an
        // enemy claim standing on the point is a front that is actively losing
        // ground, and no amount of scrap outbids that.
        int fielded = 1 + context.Allies.Length;
        if (fielded <= lens.EnemySlotCapacity(context.Tick))
            return;
        if (channel.EnemyClaim)
            return;
        // ...AND NEVER ON CREDIT. Ground is the only channel that scores, and a
        // team behind on it has no spare body by definition: whatever the
        // roster count says, every body it fields is already owed to the front.
        // The comparison is the contract's own declared ranking channel, read
        // off the published scoreboard, so it means the same thing on every arm
        // and needs no threshold. Measured: without this line the errand costs
        // a full front against an opponent that takes ground early, and the
        // scrap it brings back does not come close to buying it back.
        if (lens.Score(context, lens.TeamId) < BestOpposingScore(lens, context))
            return;

        // Nobody is loaded: the nearest non-bank body to the best pile goes, and
        // only if a pile is standing that is worth the walk at all.
        Position? quarry = null;
        int bestUnit = int.MaxValue;
        int bestSteps = int.MaxValue;
        void Consider(int unitId, Position tile)
        {
            if (lens.IsAlliedBankUnit(unitId))
                return;
            Position? pile = Best(lens, context, tile);
            if (pile is not Position target)
                return;
            int steps = tile.ChebyshevDistance(target);
            if (steps < bestSteps
                || (steps == bestSteps && unitId < bestUnit))
            {
                bestSteps = steps;
                bestUnit = unitId;
                quarry = target;
            }
        }

        Consider(context.Self.ActorId.UnitId, context.Self.Position);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            Consider(ally.ActorId.UnitId, ally.Position);
        if (bestUnit == context.Self.ActorId.UnitId)
        {
            IsCourier = true;
            Quarry = quarry;
        }
    }

    /// <summary>The best score any opposing team currently holds.</summary>
    private static long BestOpposingScore(
        MatchLens lens,
        GenericActorContext context)
    {
        long best = long.MinValue;
        foreach (GenericActorContext.TeamScoreState team
                 in context.Scoreboard.Teams)
        {
            if (team.TeamId == lens.TeamId)
                continue;
            best = Math.Max(best, lens.Score(context, team.TeamId));
        }
        return best == long.MinValue ? 0 : best;
    }

    /// <summary>
    /// The pile worth walking to from a tile: the one whose scrap, priced in
    /// ticks, still pays for the round trip that fetches it, and that will still
    /// be standing when the walk arrives. Null when none of them does.
    /// </summary>
    private Position? Best(
        MatchLens lens,
        GenericActorContext context,
        Position from)
    {
        Position? best = null;
        int bestScore = int.MinValue;
        foreach (GenericActorContext.ScrapPile pile in _piles)
        {
            int steps = from.ChebyshevDistance(pile.Position);
            if (context.Tick + steps >= pile.ExpiresAtTick)
                continue;
            // The trip is out and back: the assay pays at the tile, the rest has
            // to be walked home. Both legs are ticks off the front, and the
            // scrap has to cover both.
            int home = Nearest(_vault, pile.Position);
            int worth = pile.Amount * TickValue;
            int spend = steps + home;
            if (worth < spend)
                continue;
            int score = worth - spend;
            if (score > bestScore
                || (score == bestScore
                    && best is Position current
                    && Before(pile.Position, current)))
            {
                bestScore = score;
                best = pile.Position;
            }
        }
        _ = lens;
        return best;
    }

    /// <summary>
    /// Whether the courier should stop collecting and walk its load home: it is
    /// full, or the bank plus what it carries already buys out what is left of
    /// the ladder, or nothing worth fetching is standing.
    /// </summary>
    public bool ShouldBank(MatchLens lens, GenericActorContext context)
    {
        if (!Live || Carrying <= 0)
            return false;
        if (lens.Economy is GenericActorRulesContract.FrontlineScrapEconomy
                economy
            && Carrying >= economy.CarryCapacity)
        {
            return true;
        }
        if (Shortfall <= Carrying)
            return true;
        return Best(lens, context, context.Self.Position) is null;
    }

    /// <summary>
    /// This tick's purchase, or null.
    ///
    /// <para><b>The order is read off the contract's declared EFFECTS, never off
    /// a track name, and every step of it has a stopping condition made of two
    /// published numbers.</b> First the ceiling on the body this doctrine
    /// cannot afford to lose, until one declared enemy contact can no longer
    /// delete a fresh one — on a chassis whose prime carries two health against
    /// a fan that lands two, that is the difference between a one-bolt kill and
    /// a two-bolt kill, and it is the only purchase that pays while the body is
    /// standing behind the line rather than in front of it. Then sight, until we
    /// see as far as we shoot, because a gun that outranges its own sensor is
    /// firing at remembered tiles. Then reach, until the longest declared
    /// opposing barrel no longer out-ranges ours. Then whatever is left, so the
    /// pot is spent rather than admired.</para>
    /// </summary>
    public GenericActorDecision? TryInvest(
        MatchLens lens,
        GenericActorContext context)
    {
        if (!IsBuyer
            || lens.Economy is not GenericActorRulesContract.FrontlineScrapEconomy
                economy)
        {
            return null;
        }
        GenericActorActionLegality? invest = null;
        GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
            tracks = null;
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (!action.Available)
                continue;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in action.Constraints)
            {
                if (constraint is GenericActorActionLegality.ArgumentConstraint
                        .UpgradeTrackConstraint offered
                    && !offered.AllowedTrackIds.IsEmpty)
                {
                    invest = action;
                    tracks = offered;
                }
            }
            if (invest is not null)
                break;
        }
        if (invest is null || tracks is null)
            return null;

        string? chosen = null;
        int bestRank = int.MaxValue;
        string reason = string.Empty;
        for (int index = 0; index < economy.Tracks.Length; index++)
        {
            GenericActorRulesContract.ScrapUpgradeTrack track =
                economy.Tracks[index];
            if (!tracks.AllowedTrackIds.Contains(track.TrackId))
                continue;
            int tier = index < Tiers.Count ? Tiers[index] : 0;
            (int rank, string why) = Rank(lens, track, tier);
            if (rank < bestRank)
            {
                bestRank = rank;
                chosen = track.TrackId;
                reason = why;
            }
        }
        if (chosen is null)
            return null;
        return new GenericActorDecision(
            invest.ActionId,
            invest.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(chosen)],
            reason);
    }

    private (int Rank, string Why) Rank(
        MatchLens lens,
        GenericActorRulesContract.ScrapUpgradeTrack track,
        int tier)
    {
        int step = track.PerTierMagnitude;
        if (track.Effect.Contains("max-health", StringComparison.Ordinal))
        {
            int spawn = lens.MaxHealth(lens.BankFormId) + tier;
            return spawn <= lens.LongestEnemyHit
                ? (0, $"plating the bank out of one-bolt range ({spawn}+{step})")
                : (3, $"spending the pot on the bank's ceiling ({spawn}+{step})");
        }
        if (track.Effect.Contains("vision-range", StringComparison.Ordinal))
        {
            int sight = lens.VisionRange(lens.BankFormId) + tier;
            int reach = Reach(lens);
            return sight < reach
                ? (1, $"buying sight up to our own reach ({sight}->{reach})")
                : (4, $"sight past our reach ({sight}+{step})");
        }
        if (track.Effect.Contains("travel-tiles", StringComparison.Ordinal))
        {
            int reach = Reach(lens);
            return reach < lens.LongestEnemyReach
                ? (2, $"buying reach parity ({reach}->{lens.LongestEnemyReach})")
                : (5, $"reach past theirs ({reach}+{step})");
        }
        return (6, "spending the last of the pot");
    }

    /// <summary>Our own effective gun travel, base plus the tier we hold.</summary>
    private int Reach(MatchLens lens)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(lens.BankFormId);
        int reach = profile?.Projectile.MaxTravelTiles ?? 0;
        if (lens.Economy is not GenericActorRulesContract.FrontlineScrapEconomy
                economy)
        {
            return reach;
        }
        for (int index = 0; index < economy.Tracks.Length; index++)
        {
            if (economy.Tracks[index].Effect.Contains(
                    "travel-tiles",
                    StringComparison.Ordinal)
                && index < Tiers.Count)
            {
                reach += Tiers[index] * economy.Tracks[index].PerTierMagnitude;
            }
        }
        return reach;
    }

    private static int Nearest(IReadOnlySet<Position> tiles, Position from)
    {
        int best = int.MaxValue;
        foreach (Position tile in tiles)
            best = Math.Min(best, tile.ChebyshevDistance(from));
        return best == int.MaxValue ? 0 : best;
    }

    private static bool Before(Position left, Position right) =>
        left.Y < right.Y || (left.Y == right.Y && left.X < right.X);
}
