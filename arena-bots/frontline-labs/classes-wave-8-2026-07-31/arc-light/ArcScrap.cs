using BotArena.Sdk;

/// <summary>
/// The battlefield economy, priced for a doctrine that was never going to
/// harvest.
///
/// <para>The arm puts six scrap on a public metronome in the two rows nothing
/// else touches, and a dedicated harvester spends about a quarter of a
/// three-body team's body-ticks going to get it. Under the channel that deficit
/// is not survivable: two defenders who keep moving hold three stationary
/// attackers, so a body that leaves the front has handed the front away. What a
/// flank-and-collapse skirmisher gets instead is the OTHER half of the same
/// economy — every destroyed body drops a wreck at its death tile, the assay
/// banks one instantly at the tile with no transport, and a killed carrier drops
/// its whole load on one tile. This doctrine is already standing where bodies
/// die.</para>
///
/// <para>So there is no harvester here and no route to the veins as such. There
/// is a detour budget priced against the contract's own capture arithmetic, a
/// bank that is free to pass through, and a purchase that reads the legality
/// mask instead of the price list. Every one of them is inert when the contract
/// declares no economy, because the block that declares it is absent.</para>
/// </summary>
internal sealed class ArcScrap
{
    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly GenericActorContext.ModeObservationState.Frontline? _mode;

    public ArcScrap(ArcFacts facts, GenericActorContext context)
    {
        _facts = facts;
        _context = context;
        _mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;
    }

    /// <summary>Why the last errand or purchase declined; diagnostic only.</summary>
    public string Note { get; private set; } = "none";

    /// <summary>True when this contract declares an economy at all.</summary>
    public bool Live => ArcRules.ScrapOnTheWay && _facts.Economy is not null;

    /// <summary>What this body is carrying right now.</summary>
    public int Carrying => _context.Self.CarriedScrap;

    /// <summary>This team's liquid bank, or zero without an economy.</summary>
    public int Bank
    {
        get
        {
            if (_mode is null)
                return 0;
            foreach (GenericActorContext.ScrapTeamState team in _mode.ScrapTeams)
            {
                if (team.TeamId == _facts.TeamId)
                    return team.Bank;
            }
            return 0;
        }
    }

    /// <summary>
    /// Tiles worth stepping on: live piles that will still be there when this
    /// body could arrive, ordered by what they pay against what they cost. The
    /// pile's own <c>expiresAtTick</c> is the clock — the schedule is contract
    /// data but whether a deposit is still standing is not.
    /// </summary>
    public Position[] Errand(
        ArcKeel keel,
        ArcThreat threat,
        IReadOnlyCollection<Position> front)
    {
        Note = "no-economy";
        if (!Live || _facts.Economy is not { } economy || _mode is null)
            return [];

        Position here = _context.Self.Position;
        // The measured shape of this rule. A TRIP to a deposit is priced by how
        // long the front survives the absence, and against a striker that
        // question always answers "long enough" right up until it does not: the
        // trip version of this rule turned a 499-tick mirror draw into a
        // 173-tick breach and cost the whole vector-edge leg. A DETOUR is priced
        // by what it adds to the walk this body was making anyway, which is a
        // number the front cannot be wrong about. One tile of detour per scrap
        // gained is the budget, so the assay alone buys one tile and a full
        // deposit buys six — and a body already standing still on the objective
        // has a detour budget of zero, because its walk was zero.
        int here2Front = Distance(here, front);
        Position? best = null;
        int bestScore = int.MinValue;
        foreach (GenericActorContext.ScrapPile pile in _mode.ScrapPiles)
        {
            int toPile = ArcBoard.StepDistance(_facts, here, [pile.Position], 24)
                ?? int.MaxValue;
            if (toPile == int.MaxValue)
                continue;
            // A facing-locked body pays a rotation somewhere on nearly every
            // leg, so the honest arrival estimate is not the step count.
            int arrival = toPile + (toPile / 3) + 1;
            if (_context.Tick + arrival >= pile.ExpiresAtTick)
                continue;
            int payload = Math.Min(
                pile.Amount,
                economy.CarryCapacity - Carrying) + economy.AssayAmount;
            int detour = toPile + Distance(pile.Position, front) - here2Front;
            // MEASURED TWICE. One tile of detour per scrap was still three
            // times too generous: a WRECK sits on the tile a body died on, which
            // is by construction a tile somebody's gun was pointed at one tick
            // ago, and walking two tiles to stand on it against a striker turned
            // a 231-tick contest into a 116-tick breach. A third of a tile per
            // scrap makes the assay-only wreck a strictly free pickup and leaves
            // a full six-scrap deposit worth a two-tile bend in the route.
            if (detour * 3 > payload)
                continue;
            // And a pile nobody can shoot you on is worth more than a bigger
            // pile inside a loaded arc. This is the same bearing test the
            // stance entry has always used, applied to the one errand this
            // doctrine takes.
            if (threat.Bearing(pile.Position) > 0
                || threat.Threatened(pile.Position, arrival))
            {
                continue;
            }
            if (!keel.AffordableAbsence(arrival * 2, keel.SelfWeight))
                continue;
            int score = (payload * 4) - detour;
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = pile.Position;
        }

        if (best is Position target)
        {
            Note = $"pile{bestScore}";
            return [target];
        }

        // The same budget banks a load: the walk home is the only price the bank
        // charges, and it is worth paying only when home is on the way to
        // somewhere this body was going. A load dropped on death is the largest
        // single transfer in the game and it is handed to whoever killed you.
        if (Carrying > 0 && _facts.BankTiles.Count > 0)
        {
            Position[] bank = [.. _facts.BankTiles];
            int detour = Distance(here, bank)
                + Distance(bank, front)
                - here2Front;
            if (detour <= Carrying
                && keel.AffordableAbsence(Distance(here, bank) * 2, keel.SelfWeight))
            {
                Note = $"banking{Carrying}";
                return bank;
            }
        }

        Note = "front";
        return [];
    }

    private int Distance(Position from, IReadOnlyCollection<Position> to) =>
        to.Count == 0
            ? 0
            : ArcBoard.StepDistance(_facts, from, to, 24) ?? 24;

    private int Distance(
        IReadOnlyCollection<Position> from,
        IReadOnlyCollection<Position> to)
    {
        int best = 24;
        foreach (Position tile in from)
            best = Math.Min(best, Distance(tile, to));
        return best;
    }

    /// <summary>
    /// A purchase, or null. The mask decides what is affordable and what a cap
    /// forbids; this decides which of the offered tracks removes a binding
    /// constraint on the doctrine's own arithmetic, and it reads the DECLARED
    /// EFFECT rather than the track's name so a renamed ladder still drives the
    /// same buy.
    ///
    /// <list type="number">
    /// <item><b>Gun travel</b> while a visible or declared enemy gun reaches as
    /// far as mine. A skirmisher's whole doctrine is the opening shot, and a
    /// tier is exactly what turns a tie into a range advantage; buying it when
    /// I already out-reach everything on the board buys nothing.</item>
    /// <item><b>Spawn health</b> while two contacts from the hardest bolt
    /// visible would remove a body of mine. It never heals, so it is a purchase
    /// about the NEXT body, which for the Prime slot is eighteen ticks
    /// away.</item>
    /// <item><b>Sight</b> while I shoot further than I see — the gap a gun tier
    /// widens and a doctrine that aims at reachable sets cannot close by
    /// standing somewhere else.</item>
    /// </list>
    /// </summary>
    public GenericActorDecision? Invest()
    {
        if (!ArcRules.InvestFromTheMask
            || _facts.Economy is not { InvestVerb: true }
            || _mode is null)
        {
            return null;
        }

        GenericActorActionLegality? action = null;
        GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
            tracks = null;
        foreach (GenericActorActionLegality candidate in _context.ActionLegalities)
        {
            if (!candidate.Available)
                continue;
            GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
                constraint = candidate.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .UpgradeTrackConstraint>()
                    .FirstOrDefault();
            if (constraint is null || constraint.AllowedTrackIds.IsEmpty)
                continue;
            action = candidate;
            tracks = constraint;
            break;
        }
        if (action is null || tracks is null)
            return null;

        string? choice = null;
        int bestRank = int.MaxValue;
        foreach (string trackId in tracks.AllowedTrackIds)
        {
            int rank = Rank(trackId);
            if (rank < bestRank)
            {
                bestRank = rank;
                choice = trackId;
            }
        }
        if (choice is null || bestRank == int.MaxValue)
        {
            Note = "no-useful-track";
            return null;
        }

        Note = $"invest-{choice}";
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(choice)],
            $"investing in {choice} from a bank of {Bank}");
    }

    /// <summary>
    /// Where a declared track sits in this doctrine's preference, or
    /// <c>int.MaxValue</c> when buying it would remove no constraint this
    /// doctrine is actually bound by. Refusing a legal purchase is a real
    /// answer: the total-tier cap is three, so a tier spent on a gap that is
    /// already closed is a tier that cannot be spent on one that is not.
    /// </summary>
    private int Rank(string trackId)
    {
        if (_facts.Economy is not { } economy)
            return int.MaxValue;
        ArcFacts.TrackRules? track = null;
        foreach (ArcFacts.TrackRules candidate in economy.Tracks)
        {
            if (string.Equals(
                    candidate.TrackId,
                    trackId,
                    StringComparison.Ordinal))
            {
                track = candidate;
                break;
            }
        }
        if (track is null)
            return int.MaxValue;

        string form = _context.Self.FormId;
        int myTravel = _facts.EffectiveTravel(
            form,
            _mode,
            _facts.TeamId,
            upgradedSlot: true);
        int myVision = _facts.DeclaredVision(form)
            + _facts.TierFor(_mode, _facts.TeamId, ArcFacts.VisionEffect);

        if (string.Equals(
                track.Effect,
                ArcFacts.TravelEffect,
                StringComparison.Ordinal))
        {
            // MEASURED ENGINE DEFECT — this track is refused, and the refusal
            // is the single most expensive line in the wave.
            //
            // The doctrine's rule is one comparison, and it is still written
            // below: buy gun travel exactly while an enemy gun reaches as far as
            // mine, because a skirmisher's whole doctrine is the opening shot
            // and a tier is what turns a tie into a range advantage. On the
            // striker mirror that comparison says BUY, every time.
            //
            // Buying it aborts the match. Not the body, not the tick — the whole
            // match, with `A retained projectile must preserve its exact
            // resolved committed path. (Parameter 'projectiles')`, exit code 1,
            // no replay written, no tick, no actor and no team named. The cause
            // is visible from the outside: a purchase settles after every bolt
            // has flown, so a bolt already in the air carries a committed path
            // resolved against a maximum travel that the purchase then changes
            // under it.
            //
            // Isolated by construction, nine identical bastion matches each:
            // buying ONLY this effect aborts 3 of 9; buying only spawn health
            // aborts 0 of 9; never investing at all aborts 0 of 9; and the
            // wave-7 predecessor, which has no purchase routine, aborts 0 of 16.
            // The obvious bot-side guard — refuse while this team has a bolt in
            // flight, which `visibleProjectiles` publishes with its owner — was
            // built and measured and is NOT sufficient: it still aborts 3 of 9,
            // because a TEAMMATE's bolt launched on the same tick as the
            // purchase is a bolt no life can see when it decides. There is no
            // decision a contract-driven bot can make that closes that window,
            // so the only safe answer is to leave the tier in the bank.
            //
            // Restoring it is one line, the moment the retained path stops being
            // re-resolved: delete this return.
            return int.MaxValue;
#pragma warning disable CS0162
            return LongestEnemyGun() >= myTravel ? 0 : int.MaxValue;
#pragma warning restore CS0162
        }
        if (string.Equals(
                track.Effect,
                ArcFacts.HealthEffect,
                StringComparison.Ordinal))
        {
            int health = _facts.MaxHealth(form)
                + _facts.TierFor(_mode, _facts.TeamId, ArcFacts.HealthEffect);
            return HardestEnemyBolt() * 2 >= health ? 1 : int.MaxValue;
        }
        if (string.Equals(
                track.Effect,
                ArcFacts.VisionEffect,
                StringComparison.Ordinal))
        {
            return myVision < myTravel ? 2 : int.MaxValue;
        }
        // A track whose effect this doctrine has never heard of is not bought
        // blind: the ladder is capped, and an unknown effect has no measured
        // value to trade three tiers against.
        return int.MaxValue;
    }

    /// <summary>
    /// The longest gun the opposition can point at this body, counting its
    /// declared travel plus its published edge tier where that tier applies.
    /// Visible bodies first; with nothing visible, the declared enemy form
    /// catalog is still contract data and still answers the question.
    /// </summary>
    private int LongestEnemyGun()
    {
        int longest = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            longest = Math.Max(
                longest,
                _facts.EffectiveTravel(
                    enemy.FormId,
                    _mode,
                    enemy.ActorId.TeamId,
                    _facts.IsPrimeSlot(
                        enemy.ActorId.TeamId,
                        enemy.ActorId.UnitId)));
        }
        if (longest > 0)
            return longest;
        foreach (GenericActorRulesContract.AttackProfile profile
                 in _facts.Contract.Rules.AttackProfiles)
        {
            longest = Math.Max(longest, profile.Projectile.MaxTravelTiles);
        }
        return longest;
    }

    private int HardestEnemyBolt()
    {
        int worst = 1;
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
            worst = Math.Max(worst, _facts.Damage(enemy.FormId));
        return worst;
    }

    /// <summary>
    /// Extra target value a visibly loaded enemy is worth. Killing a carrier
    /// drops its wreck AND its whole load on one tile — the largest single
    /// transfer in the game, and it goes to whoever is standing closest, which
    /// after a collapse is this doctrine.
    /// </summary>
    public int CarrierBounty(int carried) =>
        Live && carried > 0 ? carried * 8 : 0;
}
