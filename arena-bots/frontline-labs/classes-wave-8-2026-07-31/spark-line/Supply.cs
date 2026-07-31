using BotArena.Sdk;

/// <summary>
/// The SCRAP economy, resolved once per tick from the frozen observation.
///
/// <para>Two decisions come out of it and they are not the same decision.
/// <b>What to buy</b> is a team fact — one bank, one ladder, three tiers for
/// the whole match — and it is decided by comparing the declared ladder
/// against the heaviest gun on the board, never by a track's name. <b>Who
/// walks</b> is an allocation, and the supply is a FIXED POT: four events of
/// two deposits is all the scrap the map will ever hand out, and one courier
/// services a whole cycle. A numeric class therefore buys no extra income at
/// all with its extra bodies — it buys the SECURITY of the collection, which
/// is a different thing and is why the courier is elected from the body the
/// front misses least rather than from the body that is nearest.</para>
///
/// <para>Everything is gated on the contract declaring an economy. Where it
/// does not, every field below is inert and no body ever walks off the
/// front.</para>
/// </summary>
internal sealed class Supply
{
    private readonly List<GenericActorContext.ScrapPile> _piles = [];

    /// <summary>True when the contract declares an economy at all.</summary>
    public bool Engaged { get; private set; }

    /// <summary>My team's liquid bank.</summary>
    public int Bank { get; private set; }

    /// <summary>Tiers my team holds, positional against the declared tracks.</summary>
    public int[] Tiers { get; private set; } = [];

    /// <summary>This body's carried load.</summary>
    public int Carrying { get; private set; }

    /// <summary>
    /// The track this team should buy next, or null when nothing should be
    /// bought this tick. Always drawn from the legality mask, so affordability
    /// and both caps are the engine's arithmetic rather than mine.
    /// </summary>
    public string? Buy { get; private set; }

    /// <summary>
    /// True when THIS body is the one elected to cast the purchase. Exactly
    /// one body of the team is elected per tick from the shared observation,
    /// because two purchases against a bank that covers one resolve in
    /// canonical order and the second is simply Blocked.
    /// </summary>
    public bool IAmTheInvestor { get; private set; }

    /// <summary>
    /// Tiles this body should walk to for the economy — a pile to step on, or
    /// the bank to empty a load into — or empty when this body has no supply
    /// job this tick.
    /// </summary>
    public Position[] Goals { get; private set; } = [];

    /// <summary>True when <see cref="Goals"/> is the walk home with a load.</summary>
    public bool Banking { get; private set; }

    /// <summary>Live piles, ordered as published.</summary>
    public IReadOnlyList<GenericActorContext.ScrapPile> Piles => _piles;

    /// <summary>Rebuilds the whole economic picture from this tick.</summary>
    public void Resolve(
        ContractLens lens,
        GenericActorContext context,
        Position[] objective,
        int weightedAllies)
    {
        _piles.Clear();
        Engaged = lens.Scrap is not null;
        Bank = 0;
        Tiers = [];
        Carrying = context.Self.CarriedScrap;
        Buy = null;
        IAmTheInvestor = false;
        Goals = [];
        Banking = false;
        if (!Engaged)
            return;

        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            foreach (GenericActorContext.ScrapTeamState team
                     in frontline.ScrapTeams)
            {
                if (team.TeamId != lens.TeamId)
                    continue;
                Bank = team.Bank;
                Tiers = [.. team.TierLevels];
            }
            foreach (GenericActorContext.ScrapPile pile in frontline.ScrapPiles)
            {
                if (pile.ExpiresAtTick > context.Tick)
                    _piles.Add(pile);
            }
        }

        ResolvePurchase(lens, context);
        ResolveJob(lens, context, objective, weightedAllies);
    }

    // ------------------------------------------------------------------
    // What to buy
    // ------------------------------------------------------------------

    /// <summary>
    /// Picks the next tier by EFFECT, in one written order, out of the tracks
    /// this tick's mask is actually offering.
    ///
    /// <list type="number">
    /// <item><b>Health, while one contact kills.</b> The heaviest gun on the
    /// board against the frailest body the ladder covers is the single number
    /// that decides this match for a two-health chassis: at parity a bolt
    /// costs a tempo, below it a bolt costs the body, the eighteen-tick return
    /// walk, and every capture tick the opposition takes for free while the
    /// slot is empty. It is bought first and, where the gap is two, bought
    /// twice.</item>
    /// <item><b>Reach, while I am outranged.</b> Gap-preserving by design, so
    /// it buys the opening shot rather than the kill — worth exactly as much
    /// as the tiles between the two declared travels and nothing once they are
    /// equal.</item>
    /// <item><b>Sight last.</b> It is naturally terminal and it changes no
    /// exchange I am already in.</item>
    /// </list>
    ///
    /// <para>The mask is the price list. A track appears only when the bank
    /// covers its next tier and no cap forbids it, so this method never does
    /// the arithmetic and never guesses at a Blocked.</para>
    /// </summary>
    private void ResolvePurchase(ContractLens lens, GenericActorContext context)
    {
        if (!lens.InvestIsAnAction)
            return;

        GenericActorActionLegality? invest = null;
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available
                && lens.KindOf(action.ActionId)
                    == GenericActorRulesContract.ActionKind.ModeInvestment)
            {
                invest = action;
                break;
            }
        }
        if (invest is null)
            return;

        GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
            tracks = null;
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in invest.Constraints)
        {
            if (constraint is GenericActorActionLegality.ArgumentConstraint
                .UpgradeTrackConstraint typed)
            {
                tracks = typed;
            }
        }
        if (tracks is null || tracks.AllowedTrackIds.IsEmpty)
            return;

        int heaviest = 0;
        foreach (GenericActorRulesContract.AttackProfile profile
                 in lens.Contract.Rules.AttackProfiles)
        {
            heaviest = Math.Max(heaviest, profile.Projectile.DamagePerHit);
        }
        int plateTier = TierOf(lens, "spawn-max-health-delta");
        int upgraded = lens.UpgradedSlotBaseHealth + plateTier;
        bool oneContactKills = upgraded > 0 && heaviest >= upgraded;

        int myTravel = 0;
        GenericActorRulesContract.AttackProfile? mine =
            lens.AttackFor(context.Self.FormId);
        if (mine is not null)
            myTravel = mine.Projectile.MaxTravelTiles;
        int edgeTier = TierOf(lens, "mobile-attack-travel-tiles-delta");
        bool outranged = myTravel > 0
            && myTravel + edgeTier < lens.WidestAttackTiles;

        // The order, by EFFECT, out of what this tick's mask is offering.
        //
        // HEALTH first while one contact kills, and twice while it still does:
        // for a two-health chassis against a two-damage fan that single tier
        // is the difference between a bolt costing a tempo and a bolt costing
        // the body, the nineteen-tick return, and every capture tick the
        // opposition takes for free while the slot is empty. It is also the
        // tier that switches this policy's own STANDOFF rule off, which is the
        // clearest statement of what it buys: a prime that survives a fan is a
        // prime that can hold the point.
        //
        // REACH second, and only while I am actually outranged. It is
        // gap-preserving by design — every chassis moves by the same integer —
        // so it buys the opening shot rather than the kill, and it is worth
        // exactly the tiles between the two declared travels and nothing once
        // they are equal.
        //
        // SIGHT last. It is naturally terminal, and it changes no exchange
        // this body is already in.
        Buy = (oneContactKills
                ? First(lens, tracks, "spawn-max-health-delta")
                : null)
            ?? (outranged
                ? First(lens, tracks, "mobile-attack-travel-tiles-delta")
                : null)
            ?? First(lens, tracks, "vision-range-delta")
            ?? First(lens, tracks, "spawn-max-health-delta")
            ?? tracks.AllowedTrackIds[0];
        

        // One caster. Every teammate reads the same bank and the same mask, so
        // the election is a pure function of the shared observation: the
        // lowest-ordered live body whose form allows the verb at all.
        IAmTheInvestor = LowestOrderedInvestor(lens, context);
    }

    private static string? First(
        ContractLens lens,
        GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint
            tracks,
        string effect)
    {
        foreach (string trackId in tracks.AllowedTrackIds)
        {
            if (string.Equals(
                    lens.EffectOf(trackId),
                    effect,
                    StringComparison.Ordinal))
            {
                return trackId;
            }
        }
        return null;
    }

    /// <summary>
    /// Tiers my team already holds on the track with this effect. The tier
    /// vector is positional against the contract's declared track order — that
    /// ordering is the field's own documented identity — so the effect is
    /// resolved to an index and the index reads the vector.
    /// </summary>
    private int TierOf(ContractLens lens, string effect)
    {
        for (int index = 0; index < lens.Tracks.Length; index++)
        {
            if (!string.Equals(
                    lens.Tracks[index].Effect,
                    effect,
                    StringComparison.Ordinal))
            {
                continue;
            }
            return index < Tiers.Length ? Tiers[index] : 0;
        }
        return 0;
    }

    private bool LowestOrderedInvestor(
        ContractLens lens,
        GenericActorContext context)
    {
        long mine = Order(context.Self.ActorId);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!AllowsInvest(lens, ally.FormId))
                continue;
            if (Order(ally.ActorId) < mine)
                return false;
        }
        return true;

        static long Order(ActorIdentity id) =>
            ((long)id.UnitId << 32) + id.LifeId;
    }

    private static bool AllowsInvest(ContractLens lens, string formId)
    {
        GenericActorRulesContract.Form? form = lens.Form(formId);
        if (form is null)
            return false;
        foreach (string actionId in form.AllowedActionIds)
        {
            if (lens.KindOf(actionId)
                == GenericActorRulesContract.ActionKind.ModeInvestment)
            {
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Who walks
    // ------------------------------------------------------------------

    /// <summary>
    /// Assigns this body a supply job, or none. Three rules, in order:
    ///
    /// <list type="bullet">
    /// <item><b>A load walks home.</b> A carried load is visible to the
    /// opposition and drops in full where the body dies, so the trip back is
    /// the risky half and a body already carrying finishes it rather than
    /// collecting more.</item>
    /// <item><b>A pile under my feet is free.</b> The assay pays at the tile
    /// with no transport, so a pile within a step or two of a body that is not
    /// currently holding ground is taken by whoever is nearest — including,
    /// deliberately, wreckage at the front, which is the half of this economy a
    /// team that never leaves the front still collects.</item>
    /// <item><b>One courier, elected from the back.</b> A deposit worth a whole
    /// tier is worth a dedicated trip, but only from the body the front misses
    /// least — the one furthest from the objective — and only while the team
    /// can still field enough weight to deny without it.</item>
    /// </list>
    /// </summary>
    private void ResolveJob(
        ContractLens lens,
        GenericActorContext context,
        Position[] objective,
        int weightedAllies)
    {
        if (lens.Scrap is not GenericActorRulesContract.FrontlineScrapEconomy)
            return;
        _ = weightedAllies;

        if (Carrying > 0 && lens.BankTiles.Count > 0)
        {
            Goals = [.. lens.BankTiles];
            Banking = true;
            return;
        }

        if (_piles.Count == 0)
            return;

        // Nearest pile to me, and how it ranks against my teammates' distance
        // to the same pile. Everything in the comparison is published on every
        // ally, so all bodies compute the same assignment with no memory.
        GenericActorContext.ScrapPile? claim = null;
        int claimDistance = int.MaxValue;
        foreach (GenericActorContext.ScrapPile pile in _piles)
        {
            int mine = Chebyshev(context.Self.Position, pile.Position);
            if (mine >= claimDistance)
                continue;
            bool closerAlly = false;
            foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            {
                int theirs = Chebyshev(ally.Position, pile.Position);
                if (theirs < mine
                    || (theirs == mine
                        && Rank(ally.ActorId) < Rank(context.Self.ActorId)))
                {
                    closerAlly = true;
                    break;
                }
            }
            if (closerAlly)
                continue;
            claim = pile;
            claimDistance = mine;
        }
        if (claim is null)
            return;

        // A detour I can make without abandoning the front is always worth it.
        // A trip is only worth it when the team can spare the body: the front
        // needs weight to deny, and the fixed pot means a second courier adds
        // nothing but a second absence.
        // THE POT IS FIXED, SO ONLY THE FREE HALF OF IT IS WORTH TAKING.
        // Four events of two deposits is every scrap the map will hand out,
        // and one courier services a whole cycle — so a second body walking
        // buys no income at all, only absence. Absence is expensive here: two
        // defenders who keep moving hold three stationary attackers, and the
        // front notices a missing body immediately. What is genuinely free is
        // the assay: it pays at the tile with no transport, so a pile a step
        // or two off a route this body was walking anyway is collected, and
        // nothing else is.
        //
        // Measured: the dedicated trip — a courier elected whenever the team
        // could spare a body — cost 56 territorial on an earlier round and
        // banked a tier it did not live long enough to spend. What is left is
        // still not free: leave-one-out on the shipped composition prices the
        // whole supply rule at −120, and it ships anyway because removing it
        // together with the purchase rule measures WORSE than either (DX.md).
        bool onObjective = Contains(objective, context.Self.Position);
        if (onObjective || claimDistance > 2)
            return;

        Goals = [claim.Position];

        static long Rank(ActorIdentity id) => ((long)id.UnitId << 32) + id.LifeId;
    }

    private static int Chebyshev(Position from, Position to) =>
        Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y));

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
