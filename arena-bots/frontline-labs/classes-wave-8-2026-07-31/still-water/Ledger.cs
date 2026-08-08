using BotArena.Sdk;

/// <summary>
/// THE ECONOMY PASS. Everything wave 8 added about scrap lives here or is gated
/// by a switch declared here.
///
/// <para>The economy is the first arm whose payoff attaches to bodies rather
/// than to the clock, and the whole of it is contract data: deposit addresses
/// and their schedule, what a pile is worth, how much a body carries, where a
/// load banks, whether the store is a verb at all, and every track with its
/// typed effect, per-tier magnitude, max tier and price list. The block is
/// absent on a ruleset without the arm, so this file branches on presence and
/// disappears with the mechanic.</para>
///
/// <para>The one judgement that is not read from the contract is WHICH tier to
/// buy, and even that is derived rather than named: a track is scored by what
/// its declared effect would close against the OPPOSING catalog's declared
/// numbers, so the ranking generalises to a track this brief has never
/// mentioned and to a class that does not exist yet.</para>
/// </summary>
internal static class LedgerRules
{
    /// <summary>
    /// RULE E1 — THE STORE IS PLAYED FROM THE MASK. A track appears in this
    /// tick's <c>upgrade-track</c> constraint only when the bank covers its next
    /// tier and no cap forbids it, so the bot never prices the ladder; it ranks
    /// what it is offered and spends the tick. Off: the bank accumulates and
    /// nothing is ever bought, which is the wave-7 behaviour and — measured —
    /// the whole cohort's.
    /// </summary>
    public static readonly bool Invest = true;

    /// <summary>
    /// RULE E2 — THE ASSAY IS FREE MONEY. Stepping onto a pile banks one for the
    /// team instantly, at the tile, with no transport. Every destroyed body
    /// leaves a wreck where it fell, which on this doctrine is the ground it was
    /// already holding. So a pile within a short detour is taken and the rest is
    /// not chased. Off: piles are invisible to the router.
    /// </summary>
    public static readonly bool Assay = true;

    /// <summary>
    /// RULE E3 — ONE BODY RUNS THE LANE, AND THE TEAM AGREES WHICH. The deposits
    /// are on a public metronome at declared addresses; a load banks in full on
    /// the home pad; the run costs a body from the front for about a quarter of
    /// the match. That allocation has to be a TEAM decision — two couriers is a
    /// forfeited front and none is a forfeited economy — and there is no channel
    /// to agree over except a shared function of the frozen observation. The
    /// lane is drawn from <c>TeamRandom</c>, which every life on the team draws
    /// identically at the same tick, so the assignment is unpredictable to the
    /// opponent and unanimous inside the team. Off: nobody leaves the front and
    /// the team banks only wreckage.
    /// </summary>
    public static readonly bool Courier = true;

    /// <summary>
    /// RULE E4 — A LOADED CARRIER IS A TARGET. <c>carriedScrap</c> is published
    /// on every visible enemy, and killing a carrier drops its wreck plus its
    /// whole load on one tile. So a body's worth as a target is its health
    /// pressure plus what it is carrying. Off: a carrier is an ordinary body.
    /// </summary>
    public static readonly bool Intercept = true;

    /// <summary>Score units per point of scrap on a reachable pile.</summary>
    public const double ScrapWeight = 0.55;

    /// <summary>Score units per point of load a killable carrier is holding.</summary>
    public const double CarrierWeight = 0.5;
}

/// <summary>
/// The economy for one tick: what this contract declares, where the loose scrap
/// is, what the two banks hold, and which track is worth the action.
/// </summary>
internal sealed class Economy
{
    private readonly GenericActorRulesContract.FrontlineScrapEconomy? _rules;
    private readonly Doctrine _doctrine;
    private readonly GenericActorContext _context;
    private readonly List<GenericActorContext.ScrapPile> _piles = [];

    public Economy(
        Doctrine doctrine,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode)
    {
        _doctrine = doctrine;
        _context = context;
        _rules = doctrine.Contract.Rules.GameMode
            is GenericActorRulesContract.FrontlineGameMode frontline
            ? frontline.ScrapEconomy
            : null;
        if (_rules is null)
            return;

        foreach (var pile in mode?.ScrapPiles ?? [])
        {
            // The published expiry is the first tick the pile no longer
            // exists, the same clock grammar as a route cooldown.
            if (context.Tick < pile.ExpiresAtTick)
                _piles.Add(pile);
        }
        foreach (var team in mode?.ScrapTeams ?? [])
        {
            if (team.TeamId == doctrine.TeamId)
            {
                Bank = team.Bank;
                Tiers = team.TierLevels;
            }
            else
            {
                EnemyBank = team.Bank;
                EnemyTiers = team.TierLevels;
            }
        }
    }

    /// <summary>Whether this ruleset carries an economy at all.</summary>
    public bool Declared => _rules is not null;

    /// <summary>
    /// Whether the store is a player verb. The control level buys by itself at
    /// no action cost and does not carry the action at all, so a bot that reads
    /// this skips its purchase routine instead of spending ticks on a refusal.
    /// </summary>
    public bool StoreIsAVerb =>
        _rules is not null
        && _rules.PurchaseMode.Contains("action", StringComparison.Ordinal);

    public int Bank { get; }

    public int EnemyBank { get; }

    public System.Collections.Immutable.ImmutableArray<int> Tiers { get; } = [];

    public System.Collections.Immutable.ImmutableArray<int> EnemyTiers { get; } = [];

    public int CarryCapacity => _rules?.CarryCapacity ?? 0;

    /// <summary>Scrap banked instantly by standing on a pile, with no walk.</summary>
    public int AssayAmount => _rules?.AssayAmount ?? 0;

    public IReadOnlyList<GenericActorContext.ScrapPile> Piles => _piles;

    /// <summary>Deposit addresses, in declared order.</summary>
    public IEnumerable<Position> VeinSites =>
        (_rules?.VeinSites ?? [])
        .Select(site => new Position(site.X, site.Y));

    /// <summary>
    /// The next scheduled deposit tick at or after <paramref name="tick"/>, or
    /// null when the schedule is finished. Read from the declared first tick,
    /// interval and last tick — never from the numbers in the brief.
    /// </summary>
    public int? NextDepositTick(int tick)
    {
        if (_rules is null || _rules.VeinSpawnIntervalTicks <= 0)
            return null;
        int next = _rules.VeinFirstSpawnTick;
        while (next < tick)
            next += _rules.VeinSpawnIntervalTicks;
        return next <= _rules.VeinLastSpawnTick ? next : null;
    }

    /// <summary>The tiles a full load banks on, for this team.</summary>
    public HashSet<Position> BankTiles()
    {
        var tiles = new HashSet<Position>();
        if (_rules is null)
            return tiles;
        var ids = _rules.BankRegionIds;
        if (_doctrine.TeamId < 0 || _doctrine.TeamId >= ids.Length)
            return tiles;
        string regionId = ids[_doctrine.TeamId];
        foreach (var region in _doctrine.Contract.Map.Regions)
        {
            if (!string.Equals(region.RegionId, regionId, StringComparison.Ordinal))
                continue;
            foreach (var tile in region.Tiles)
                tiles.Add(tile);
        }
        return tiles;
    }

    /// <summary>
    /// The track worth this body's action, or null. The mask decides what is
    /// LEGAL — affordable out of the standing bank, under its own max tier and
    /// inside the team's total cap — and this decides what is WORTH it, by
    /// asking what each declared effect would close against the numbers the two
    /// catalogs actually publish.
    /// </summary>
    public string? BestTrack(GenericActorActionLegality legality)
    {
        if (_rules is null)
            return null;
        var constraint = legality.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .UpgradeTrackConstraint>()
            .SingleOrDefault();
        if (constraint is null || constraint.AllowedTrackIds.IsEmpty)
            return null;

        string? best = null;
        double bestValue = 0;
        foreach (string trackId in constraint.AllowedTrackIds)
        {
            var track = _rules.Tracks
                .FirstOrDefault(entry =>
                    string.Equals(entry.TrackId, trackId, StringComparison.Ordinal));
            if (track is null)
                continue;
            double value = TrackValue(track);
            if (best is null || value > bestValue)
            {
                best = trackId;
                bestValue = value;
            }
        }
        return bestValue > 0 ? best : null;
    }

    /// <summary>
    /// What one more tier of a declared effect is worth to THIS doctrine against
    /// THIS opponent. Nothing here names a track: the effect policy IDs are
    /// contract vocabulary, and every number the valuation compares against is
    /// published by one of the two form catalogs.
    /// </summary>
    private double TrackValue(GenericActorRulesContract.ScrapUpgradeTrack track)
    {
        int step = Math.Max(1, track.PerTierMagnitude);
        var attack = _doctrine.Attack(_context.Self.FormId);
        int travel = attack?.Projectile.MaxTravelTiles ?? 0;
        int vision = _doctrine.OwnMobileVision;

        if (track.Effect.Contains("vision", StringComparison.Ordinal))
        {
            // A doctrine that fires only at bodies a prediction NAMES is
            // bounded by what it can see, not by what it can reach. The tier is
            // worth the gap it closes and nothing past it: sight beyond the gun
            // buys a warning, sight up to the gun buys a shot.
            double gap = Math.Min(step, Math.Max(0, travel - vision));
            return gap * 1.4;
        }
        if (track.Effect.Contains("travel", StringComparison.Ordinal))
        {
            // Reach is only worth buying where it buys the OPENING shot: a tile
            // of standoff the opposing catalog cannot answer. Past that margin
            // it is one more tile of a lane already longer than theirs.
            int theirs = _doctrine.OpposingAnyRange;
            double margin = travel - theirs;
            return margin >= step + 1 ? 0.35 * step : 1.1 * step;
        }
        if (track.Effect.Contains("health", StringComparison.Ordinal))
        {
            // Health is bought in HITS, not in points. A tier that does not
            // change how many of the opposing catalog's worst declared bolts
            // this chassis survives has bought nothing at all — which is the
            // exact arithmetic the fabricator pays to stop a two-damage bolt
            // removing its prime outright, and the exact reason a three-health
            // chassis facing the same bolt should buy something else.
            int worst = Math.Max(1, _doctrine.OpposingWorstBolt);
            int form = _doctrine.Form(_context.Self.FormId)?.MaxHealth ?? 1;
            int now = (form + worst - 1) / worst;
            int then = (form + step + worst - 1) / worst;
            return then > now ? 1.3 * (then - now) : 0.15;
        }
        return 0.2;
    }

    /// <summary>
    /// What ending the tick on <paramref name="tile"/> is worth economically:
    /// the assay paid at a pile, plus the banking of a load already carried.
    /// Zero on every ruleset without the arm.
    /// </summary>
    public double TileValue(Position tile, HashSet<Position> bankTiles)
    {
        if (_rules is null)
            return 0;
        double value = 0;
        if (LedgerRules.Assay)
        {
            foreach (var pile in _piles)
            {
                if (pile.Position != tile)
                    continue;
                // The assay is instant and untransported; the remainder becomes
                // carry, which is only worth what it is worth if it gets home.
                value += LedgerRules.ScrapWeight
                    * (_rules.AssayAmount
                        + (0.4 * Math.Min(
                            pile.Amount - _rules.AssayAmount,
                            Math.Max(0, _rules.CarryCapacity
                                - _context.Self.CarriedScrap))));
            }
        }
        if (_context.Self.CarriedScrap > 0 && bankTiles.Contains(tile))
            value += LedgerRules.ScrapWeight * _context.Self.CarriedScrap;
        return value;
    }
}
