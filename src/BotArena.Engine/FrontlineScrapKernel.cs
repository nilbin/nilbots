using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Pure Frontline scrap-economy kernel: deposit schedule, wreckage, pile decay
/// and merge, carry, banking, and the upgrade store. It reads post-damage
/// bodies exactly as <see cref="FrontlineModeKernel"/> reads objective weight,
/// owns no world state of its own, and is therefore shared verbatim between
/// the live mode driver and the replay chronology validator — which is what
/// makes the validator's re-derivation an actual check rather than a second
/// opinion.
/// <para>Every operation is deterministic and canonically ordered. Where two
/// bodies could take the same pile, the earlier <c>(teamId, unitId, lifeId)</c>
/// wins; where two deposits could land on the same tile, the declared site
/// order wins.</para>
/// </summary>
public sealed class FrontlineScrapKernel
{
    private readonly FrontlineScrapEconomyDefinition _economy;
    private readonly ImmutableArray<int> _teamIds;
    private readonly HashSet<int> _teamIdSet;
    private readonly ImmutableDictionary<string, int> _objectiveWeights;
    private readonly ImmutableDictionary<int, ImmutableHashSet<Position>>
        _bankTilesByTeam;
    private readonly HashSet<(int TeamId, int UnitId)> _upgradedSlots;
    private readonly ImmutableArray<ImmutableArray<Position>> _veinRows;

    /// <summary>Creates the kernel for one resolved economy.</summary>
    /// <param name="topology">Scoring teams.</param>
    /// <param name="map">
    /// Read for the banking regions and for the deposit displacement scan.
    /// </param>
    /// <param name="forms">
    /// Read for objective weight, which is the economy's participation gate:
    /// a form declaring weight zero — an anchored turret — may not pick up or
    /// carry, and transitioning into one drops the load. Without that rule a
    /// turret parked on a deposit site is a permanent denial engine that also
    /// banks the assay every cycle for free.
    /// </param>
    /// <param name="unitSlotLifecycle">
    /// Read only to resolve the upgrade scope. The Prime is the slot the
    /// contract starts the match with, exactly as MUSTER's rally scope
    /// resolves it.
    /// </param>
    /// <param name="economy">The declared capability.</param>
    public FrontlineScrapKernel(
        PublicMatchTopology topology,
        ActorMapDefinition map,
        IReadOnlyCollection<ActorFormDefinition> forms,
        IReadOnlyCollection<ActorUnitSlotLifecycleAssignmentDefinition>
            unitSlotLifecycle,
        FrontlineScrapEconomyDefinition economy)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(forms);
        ArgumentNullException.ThrowIfNull(unitSlotLifecycle);
        ArgumentNullException.ThrowIfNull(economy);

        _economy = economy;
        _teamIds = topology.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToImmutableArray();
        _teamIdSet = _teamIds.ToHashSet();
        if (_teamIds.Length != economy.BankRegionIds.Length)
        {
            throw new ArgumentException(
                "A scrap economy declares exactly one banking region per "
                + "scoring team.",
                nameof(economy));
        }

        ActorFormDefinition[] formSnapshot = [.. forms];
        _objectiveWeights = formSnapshot.ToImmutableDictionary(
            form => form.Id,
            form => form.ObjectiveWeight,
            StringComparer.Ordinal);

        Dictionary<string, ActorMapRegionDefinition> regions =
            map.Regions.ToDictionary(
                region => region.RegionId,
                StringComparer.Ordinal);
        try
        {
            _bankTilesByTeam = _teamIds.ToImmutableDictionary(
                teamId => teamId,
                teamId => regions[economy.BankRegionIds[teamId]]
                    .Tiles
                    .ToImmutableHashSet());
        }
        catch (KeyNotFoundException exception)
        {
            throw new ArgumentException(
                "A scrap economy references an unknown banking region.",
                nameof(economy),
                exception);
        }

        _upgradedSlots = economy.UpgradeScope switch
        {
            FrontlineScrapEconomyDefinition.UpgradeScopeKind
                .PrimeSlotLivesOnly => unitSlotLifecycle
                    .Where(assignment =>
                        assignment.InitialAvailability
                        == ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.ActiveAtTickZero)
                    .Select(assignment =>
                        (assignment.TeamId, assignment.UnitId))
                    .ToHashSet(),
            _ => throw new ArgumentOutOfRangeException(nameof(economy)),
        };

        // The displacement scan never leaves the site's own row, so each row's
        // floor tiles are resolved once, in ascending x.
        _veinRows = economy.VeinSites
            .Select(site => Enumerable
                .Range(0, map.Width)
                .Select(x => new Position(x, site.Y))
                .Where(position => !map.IsWall(position))
                .ToImmutableArray())
            .ToImmutableArray();
        if (_veinRows.Any(row => row.IsEmpty))
        {
            throw new ArgumentException(
                "Every declared vein site must sit on a row with at least one "
                + "floor tile.",
                nameof(economy));
        }
    }

    /// <summary>The declared capability this kernel resolves.</summary>
    public FrontlineScrapEconomyDefinition Economy => _economy;

    /// <summary>An empty bank, no tiers, no piles, nobody carrying.</summary>
    public FrontlineScrapState CreateInitialState() =>
        new(
            _teamIds
                .Select(teamId => new FrontlineScrapTeamState(
                    teamId,
                    Bank: 0,
                    TierLevels: [.. _economy.Tracks.Select(_ => 0)],
                    BankedTotal: 0,
                    SpentTotal: 0))
                .ToImmutableArray(),
            Piles: [],
            CarriedByActor: ImmutableSortedDictionary<ActorIdentity, int>
                .Empty,
            SpawnedTotal: 0,
            EvaporatedTotal: 0);

    /// <summary>
    /// One post-combat economy update, in the order the memo fixes: expiry,
    /// wreckage, deposits, weight-zero drops, pickup, banking, then the hard
    /// pile bound. It runs after damage and destruction finalisation and
    /// before the objective update, so a body destroyed this tick collects
    /// nothing and the corpse it leaves is available to whoever is standing
    /// there.
    /// </summary>
    /// <param name="state">The economy as of tick start.</param>
    /// <param name="tick">The joint tick being resolved.</param>
    /// <param name="lives">
    /// Post-combat survivors, in any order — the kernel canonicalises.
    /// </param>
    /// <param name="destructions">
    /// Bodies destroyed this tick with the tile they died on.
    /// </param>
    public FrontlineScrapState ApplyJointTick(
        FrontlineScrapState state,
        int tick,
        IReadOnlyCollection<FrontlineScrapBody> lives,
        IReadOnlyCollection<FrontlineScrapDestruction> destructions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lives);
        ArgumentNullException.ThrowIfNull(destructions);
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));

        var piles = new Dictionary<Position, FrontlineScrapPile>();
        long evaporated = state.EvaporatedTotal;
        long spawned = state.SpawnedTotal;
        Dictionary<int, FrontlineScrapTeamState> teams =
            state.Teams.ToDictionary(team => team.TeamId);
        Dictionary<ActorIdentity, int> carried =
            state.CarriedByActor.ToDictionary(
                entry => entry.Key,
                entry => entry.Value);

        // 1. Decay. A pile is gone the first tick tick >= expiresAtTick, so an
        //    untaken deposit disappears exactly as the next pair spawns and at
        //    most one cycle of deposits is ever live.
        foreach (FrontlineScrapPile pile in state.Piles)
        {
            if (tick >= pile.ExpiresAtTick)
                evaporated += pile.Amount;
            else
                piles[pile.Position] = pile;
        }

        // 2. Wreckage. Every destroyed body drops at its death tile, merged
        //    with whatever it was carrying: a killed carrier is one pile worth
        //    wreck + load, which is the largest single transfer in the game
        //    and is structurally available to the team that intercepts.
        foreach (FrontlineScrapDestruction destruction in destructions
                     .OrderBy(item => item.ActorId))
        {
            int load = carried.GetValueOrDefault(destruction.ActorId);
            carried.Remove(destruction.ActorId);
            int amount = checked(_economy.WreckAmount + load);
            if (amount <= 0)
                continue;
            // Only the wreck itself is new supply; the carried half was
            // already counted when it spawned.
            spawned += _economy.WreckAmount;
            Merge(piles, destruction.Position, amount, ExpiryAt(tick));
        }

        // 3. Deposits, in declared site order. A site held by a live body
        //    displaces along its own row rather than being denied, which
        //    closes camping-as-denial without adding randomness.
        if (_economy.IsVeinSpawnTick(tick))
        {
            HashSet<Position> occupied = lives
                .Select(life => life.Position)
                .ToHashSet();
            for (int index = 0; index < _economy.VeinSites.Length; index++)
            {
                Position target = DisplaceVein(
                    _economy.VeinSites[index],
                    _veinRows[index],
                    occupied);
                spawned += _economy.VeinAmount;
                Merge(
                    piles,
                    target,
                    _economy.VeinAmount,
                    ExpiryAt(tick));
            }
        }

        FrontlineScrapBody[] ordered = lives
            .OrderBy(life => life.ActorId)
            .ToArray();

        // 4. Weight-zero drop. Objective weight gates the economy, so a body
        //    that has become a turret puts its whole load back on the floor.
        foreach (FrontlineScrapBody life in ordered)
        {
            if (ParticipatesInEconomy(life)
                || carried.GetValueOrDefault(life.ActorId) is not > 0)
            {
                continue;
            }
            int load = carried[life.ActorId];
            carried.Remove(life.ActorId);
            Merge(piles, life.Position, load, ExpiryAt(tick));
        }

        // 5. Pickup. Banks the assay instantly — the floor under every trip —
        //    then loads the remainder up to the carry cap and leaves the rest
        //    on the tile with its original expiry.
        foreach (FrontlineScrapBody life in ordered)
        {
            if (!ParticipatesInEconomy(life)
                || !piles.TryGetValue(
                    life.Position,
                    out FrontlineScrapPile? pile))
            {
                continue;
            }

            int assayed = Math.Min(_economy.AssayAmount, pile.Amount);
            int load = carried.GetValueOrDefault(life.ActorId);
            int loaded = Math.Min(
                pile.Amount - assayed,
                Math.Max(0, _economy.CarryCapacity - load));
            if (assayed == 0 && loaded == 0)
                continue;

            teams[life.ActorId.TeamId] = Bank(
                teams[life.ActorId.TeamId],
                assayed);
            if (load + loaded > 0)
                carried[life.ActorId] = load + loaded;
            int remainder = pile.Amount - assayed - loaded;
            if (remainder > 0)
                piles[pile.Position] = pile with { Amount = remainder };
            else
                piles.Remove(pile.Position);
        }

        // 6. Banking. A body of the owning team standing on its own banking
        //    region converts its whole load, automatically and free: the
        //    transport leg was the price.
        foreach (FrontlineScrapBody life in ordered)
        {
            if (carried.GetValueOrDefault(life.ActorId) is not > 0
                || !_bankTilesByTeam[life.ActorId.TeamId]
                    .Contains(life.Position))
            {
                continue;
            }
            teams[life.ActorId.TeamId] = Bank(
                teams[life.ActorId.TeamId],
                carried[life.ActorId]);
            carried.Remove(life.ActorId);
        }

        // A load whose body left the world without a death tile — a
        // disqualified participant's retirement is the only path — evaporates
        // rather than vanishing, so conservation still closes.
        HashSet<ActorIdentity> live = ordered
            .Select(life => life.ActorId)
            .ToHashSet();
        foreach (ActorIdentity actorId in carried.Keys.ToArray())
        {
            if (live.Contains(actorId))
                continue;
            evaporated += carried[actorId];
            carried.Remove(actorId);
        }

        // 7. The hard bound. Publishing a provably small collection is worth
        //    one deterministic eviction rule; the shortest-lived pile goes
        //    first, ties broken by (y, x).
        List<FrontlineScrapPile> live_piles = piles.Values
            .OrderBy(pile => pile.Position.Y)
            .ThenBy(pile => pile.Position.X)
            .ToList();
        while (live_piles.Count > _economy.MaxSimultaneousPiles)
        {
            FrontlineScrapPile evicted = live_piles
                .OrderBy(pile => pile.ExpiresAtTick)
                .ThenBy(pile => pile.Position.Y)
                .ThenBy(pile => pile.Position.X)
                .First();
            evaporated += evicted.Amount;
            live_piles.Remove(evicted);
        }

        var next = new FrontlineScrapState(
            _teamIds.Select(teamId => teams[teamId]).ToImmutableArray(),
            [.. live_piles],
            carried.ToImmutableSortedDictionary(),
            spawned,
            evaporated);
        return _economy.PurchaseMode
            == FrontlineScrapEconomyDefinition.PurchaseModeKind
                .AutomaticGreedyDeclaredOrder
            ? ApplyAutomaticPurchases(next)
            : next;
    }

    /// <summary>
    /// The tracks one team may buy the next tier of right now: affordable out
    /// of the standing bank, below their own maximum tier, and inside the
    /// team's total-tier cap. Affordability lives here rather than in the bot,
    /// so a bot that reads its legality mask never has to do the arithmetic
    /// and a bot that guesses gets an ordinary Blocked.
    /// </summary>
    public ImmutableArray<string> InvestableTracks(
        FrontlineScrapState state,
        int teamId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_teamIdSet.Contains(teamId))
            return [];
        FrontlineScrapTeamState team = state.Team(teamId);
        if (team.TotalTiers >= _economy.MaxTotalTiers)
            return [];
        return _economy.Tracks
            .Where((track, index) =>
                track.NextTierCost(team.TierLevels[index]) is int cost
                && cost <= team.Bank)
            .Select(track => track.TrackId)
            .ToImmutableArray();
    }

    /// <summary>
    /// Spends one tier if the track is legal for that team right now. Returns
    /// the unchanged state and false otherwise, which is what makes a
    /// simultaneous second purchase against a bank that covers only one
    /// resolve as a plain Blocked rather than as a new rule.
    /// </summary>
    public bool TryInvest(
        FrontlineScrapState state,
        int teamId,
        string trackId,
        out FrontlineScrapState next)
    {
        ArgumentNullException.ThrowIfNull(state);
        next = state;
        if (string.IsNullOrWhiteSpace(trackId)
            || !_teamIdSet.Contains(teamId))
        {
            return false;
        }

        int index = TrackIndex(trackId);
        if (index < 0)
            return false;
        FrontlineScrapTeamState team = state.Team(teamId);
        if (team.TotalTiers >= _economy.MaxTotalTiers)
            return false;
        if (_economy.Tracks[index].NextTierCost(team.TierLevels[index])
            is not int cost
            || cost > team.Bank)
        {
            return false;
        }

        next = state with
        {
            Teams = state.Teams
                .Select(value => value.TeamId == teamId
                    ? value with
                    {
                        Bank = value.Bank - cost,
                        SpentTotal = checked(value.SpentTotal + cost),
                        TierLevels = value.TierLevels.SetItem(
                            index,
                            value.TierLevels[index] + 1),
                    }
                    : value)
                .ToImmutableArray(),
        };
        return true;
    }

    /// <summary>
    /// The typed modifiers one body currently carries, resolved from its
    /// team's tiers and the declared upgrade scope. Out of scope means the
    /// declared form stats, unchanged.
    /// </summary>
    public GenericActorModeStatModifiers ModifiersFor(
        FrontlineScrapState state,
        ActorIdentity actorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actorId);
        if (!_upgradedSlots.Contains((actorId.TeamId, actorId.UnitId))
            || !_teamIdSet.Contains(actorId.TeamId))
        {
            return GenericActorModeStatModifiers.None;
        }

        FrontlineScrapTeamState team = state.Team(actorId.TeamId);
        int travel = 0;
        int vision = 0;
        int health = 0;
        for (int index = 0; index < _economy.Tracks.Length; index++)
        {
            int magnitude = checked(
                team.TierLevels[index] * _economy.Tracks[index]
                    .PerTierMagnitude);
            switch (_economy.Tracks[index].Effect)
            {
                case FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .MobileAttackTravelTilesDelta:
                    travel += magnitude;
                    break;
                case FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .SpawnMaxHealthDelta:
                    health += magnitude;
                    break;
                case FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .VisionRangeDelta:
                    vision += magnitude;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown scrap upgrade effect kind.");
            }
        }
        return new GenericActorModeStatModifiers(travel, vision, health);
    }

    /// <summary>
    /// The control arm's buyer: while a team can afford one, it takes the
    /// cheapest legal next tier, breaking ties by declared track order. No
    /// action is spent and no body is involved, which is exactly the claim
    /// <c>scrap-flat-control-arm</c> exists to falsify.
    /// </summary>
    public FrontlineScrapState ApplyAutomaticPurchases(
        FrontlineScrapState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        FrontlineScrapState current = state;
        foreach (int teamId in _teamIds)
        {
            while (true)
            {
                FrontlineScrapTeamState team = current.Team(teamId);
                if (team.TotalTiers >= _economy.MaxTotalTiers)
                    break;
                int chosen = -1;
                int chosenCost = int.MaxValue;
                for (int index = 0;
                     index < _economy.Tracks.Length;
                     index++)
                {
                    if (_economy.Tracks[index]
                            .NextTierCost(team.TierLevels[index])
                        is not int cost
                        || cost > team.Bank
                        || cost >= chosenCost)
                    {
                        continue;
                    }
                    chosen = index;
                    chosenCost = cost;
                }
                if (chosen < 0
                    || !TryInvest(
                        current,
                        teamId,
                        _economy.Tracks[chosen].TrackId,
                        out FrontlineScrapState bought))
                {
                    break;
                }
                current = bought;
            }
        }
        return current;
    }

    /// <summary>
    /// The published projection of one economy state: both teams' complete
    /// economic position and every live pile, in canonical order.
    /// </summary>
    public (ImmutableArray<GenericActorRuntimeObservation.ScrapTeamState>
        Teams,
        ImmutableArray<GenericActorRuntimeObservation.ScrapPile> Piles)
        Project(FrontlineScrapState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return (
            state.Teams
                .OrderBy(team => team.TeamId)
                .Select(team =>
                    new GenericActorRuntimeObservation.ScrapTeamState(
                        team.TeamId,
                        team.Bank,
                        team.TierLevels))
                .ToImmutableArray(),
            state.Piles
                .OrderBy(pile => pile.Position.Y)
                .ThenBy(pile => pile.Position.X)
                .Select(pile =>
                    new GenericActorRuntimeObservation.ScrapPile(
                        pile.Position,
                        pile.Amount,
                        pile.ExpiresAtTick))
                .ToImmutableArray());
    }

    private int TrackIndex(string trackId)
    {
        for (int index = 0; index < _economy.Tracks.Length; index++)
        {
            if (string.Equals(
                    _economy.Tracks[index].TrackId,
                    trackId,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private bool ParticipatesInEconomy(FrontlineScrapBody life) =>
        _objectiveWeights.TryGetValue(life.FormId, out int weight)
        && weight > 0;

    private int ExpiryAt(int tick) =>
        checked(tick + _economy.PileLifetimeTicks);

    private static FrontlineScrapTeamState Bank(
        FrontlineScrapTeamState team,
        int amount) =>
        amount == 0
            ? team
            : team with
            {
                Bank = checked(team.Bank + amount),
                BankedTotal = checked(team.BankedTotal + amount),
            };

    private static void Merge(
        IDictionary<Position, FrontlineScrapPile> piles,
        Position position,
        int amount,
        int expiresAtTick)
    {
        if (amount <= 0)
            return;
        piles[position] = piles.TryGetValue(
            position,
            out FrontlineScrapPile? existing)
            ? existing with
            {
                Amount = checked(existing.Amount + amount),
                ExpiresAtTick = Math.Max(
                    existing.ExpiresAtTick,
                    expiresAtTick),
            }
            : new FrontlineScrapPile(position, amount, expiresAtTick);
    }

    /// <summary>
    /// Where a deposit actually lands. The declared site unless a live body
    /// stands on it, and then the nearest free floor tile in the same row,
    /// scanning by ascending distance and breaking ties by ascending x.
    /// </summary>
    private static Position DisplaceVein(
        Position site,
        ImmutableArray<Position> row,
        IReadOnlySet<Position> occupied) =>
        occupied.Contains(site)
            ? row
                .Where(position => !occupied.Contains(position))
                .OrderBy(position => Math.Abs(position.X - site.X))
                .ThenBy(position => position.X)
                .DefaultIfEmpty(site)
                .First()
            : site;
}
