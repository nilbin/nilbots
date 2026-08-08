using BotArena.Sdk;

/// <summary>
/// The battlefield economy, read from the contract and spent from the mask.
///
/// <para><b>The bulwark's read, and it is a refusal.</b> The deposits are
/// sixteen facing-locked ticks from home in a lane this class has no other
/// reason to enter, and a dedicated harvester spends about a quarter of a
/// three-body team's body-ticks. Under a channelling capture two defenders who
/// keep moving hold three stationary attackers, so a body-light front is a lost
/// front — and this chassis's whole doctrine is that ground is the only
/// currency. So this doctrine does NOT send a harvester. It takes the economy
/// it is already standing in: every destroyed body drops a wreck at its death
/// tile, the assay pays in full at the tile with no transport, and a fortress
/// doctrine kills people where it is standing. Measured on the mirror, the
/// predecessor banked fifteen scrap a match that way without one line of
/// economic code — and spent none of it, which is the actual hole.</para>
/// </summary>
internal sealed class Salvage
{
    private readonly List<GenericActorRulesContract.ScrapUpgradeTrack> _tracks =
        [];
    private readonly bool _interrupts;

    public Salvage(GenericActorResolvedMatchContract contract)
    {
        if (contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return;
        }

        GenericActorRulesContract.FrontlineScrapEconomy? economy =
            frontline.ScrapEconomy;
        if (economy is null)
            return;

        Declared = true;
        // The claim interrupt is what makes a health ceiling worth more than a
        // declared gap; where the ruleset declares none, the gap model stands.
        _interrupts = frontline.Capture.ClaimInterrupt is not null;
        AssayAmount = economy.AssayAmount;
        CarryCapacity = economy.CarryCapacity;
        MaxTotalTiers = economy.MaxTotalTiers;

        // `invest-action` is the arm where a body spends its tick on the verb.
        // The control level buys by itself and does not put the verb in the
        // catalog at all, so a bot that reads this never submits a purchase it
        // was never offered — and the legality mask agrees either way.
        Spendable = economy.PurchaseMode.Contains(
            "invest-action",
            StringComparison.Ordinal);
        foreach (GenericActorRulesContract.ScrapUpgradeTrack track
            in economy.Tracks)
        {
            _tracks.Add(track);
        }
    }

    /// <summary>True when this ruleset declares an economy at all.</summary>
    public bool Declared { get; }

    /// <summary>True when a body may spend its action on the store's verb.</summary>
    public bool Spendable { get; }

    /// <summary>Scrap banked instantly for stepping onto a pile.</summary>
    public int AssayAmount { get; }

    /// <summary>Most scrap one body may carry.</summary>
    public int CarryCapacity { get; }

    /// <summary>Ceiling on the tiers one team may hold.</summary>
    public int MaxTotalTiers { get; }

    /// <summary>
    /// Buys the offered tier that closes the biggest DECLARED gap, or null when
    /// nothing is offered this tick.
    ///
    /// <para><b>The mask prices the ladder; this only chooses.</b> A track
    /// appears in the constraint only when the team's bank covers its next tier
    /// and no cap forbids it, so there is no arithmetic here and no way to be
    /// refused for affordability. What is left is a preference, and it is
    /// derived from the two numbers the contract already publishes about this
    /// body: what its gun reaches and how far it can see, against what the
    /// opposition's forms declare. Nothing below names a track.</para>
    ///
    /// <list type="bullet">
    /// <item>Where a CLAIM INTERRUPT is declared, a <c>spawn-max-health-delta</c>
    /// tier goes first: damage is progress on such a ruleset, so the body's
    /// standing time on the point is the claim itself.</item>
    /// <item>A <c>vision-range-delta</c> tier is otherwise worth the gap between
    /// what this chassis SHOOTS and what it SEES — a gun that outranges its own
    /// eyes is firing on somebody else's report.</item>
    /// <item>A <c>mobile-attack-travel-tiles-delta</c> tier is worth the gap
    /// between the longest reach declared on the board and mine, which is the
    /// standoff every duel here is decided by. It is currently DECLINED for a
    /// measured engine defect — see below.</item>
    /// </list>
    /// </summary>
    public GenericActorDecision? TryInvest(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!Garrison.Invest || !Declared || !Spendable)
            return null;

        GenericActorActionLegality? invest = null;
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (lens.KindOf(action.ActionId)
                != GenericActorRulesContract.ActionKind.ModeInvestment)
            {
                continue;
            }
            if (action.Available)
                invest = action;
        }
        if (invest is null)
            return null;

        var offered = new List<string>();
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
            in invest.Constraints)
        {
            if (constraint
                is GenericActorActionLegality.ArgumentConstraint
                    .UpgradeTrackConstraint tracks)
            {
                foreach (string trackId in tracks.AllowedTrackIds)
                    offered.Add(trackId);
            }
        }
        if (offered.Count == 0)
            return null;

        string? best = null;
        int bestValue = int.MinValue;
        int bestOrder = int.MaxValue;
        for (int index = 0; index < _tracks.Count; index++)
        {
            GenericActorRulesContract.ScrapUpgradeTrack track = _tracks[index];
            if (!offered.Contains(track.TrackId))
                continue;
            if (!Garrison.TravelTier && MutatesProjectileEnvelope(track))
                continue;
            int value = Value(lens, context, track, _interrupts);
            if (value > bestValue || (value == bestValue && index < bestOrder))
            {
                best = track.TrackId;
                bestValue = value;
                bestOrder = index;
            }
        }
        if (best is null)
            return null;

        return new GenericActorDecision(
            invest.ActionId,
            invest.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(best)],
            $"banking a tier of {best}: closing a declared gap of {bestValue}");
    }

    /// <summary>
    /// Does this track's declared effect change the envelope of a projectile
    /// that may already be in flight?
    ///
    /// <para><b>This exists because of a reproducible engine defect, and it is
    /// labelled as one rather than dressed up as doctrine.</b> Buying the
    /// gun-travel track aborts the match outright — <c>A retained projectile
    /// must preserve its exact resolved committed path</c>, no result, no
    /// replay, not even a partial one — on both runtimes and on every seed
    /// tried. The other two tracks are clean over the same seeds, so the
    /// mechanism is exactly this: a tier that rewrites <c>maxTravelTiles</c>
    /// invalidates the committed path of a bolt already travelling under the old
    /// number. There is NO observation a bot can gate on, and I measured that
    /// rather than assuming it: declining the purchase whenever this body's own
    /// <c>VisibleProjectiles</c> is non-empty still aborts on eight seeds out of
    /// eight, because the offending bolt is somebody else's and out of sight.
    /// So the only safe policy is to decline the track, and the switch exists so
    /// that one edit re-enables it the day the engine keeps its own invariant.
    /// Full repro in <c>DX.md</c>.</para>
    /// </summary>
    private static bool MutatesProjectileEnvelope(
        GenericActorRulesContract.ScrapUpgradeTrack track) =>
        track.Effect.Contains("travel-tiles-delta", StringComparison.Ordinal);

    /// <summary>
    /// How many declared points of deficit one tier of this track closes.
    /// Scaled by the track's own per-tier magnitude so a contract that steps by
    /// two is worth twice a contract that steps by one.
    /// </summary>
    private int Value(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.ScrapUpgradeTrack track,
        bool interrupts)
    {
        int step = Math.Max(1, track.PerTierMagnitude);
        GenericActorRulesContract.Form? form = lens.Form(context.Self.FormId);
        GenericActorRulesContract.AttackProfile? attack = lens.Attack(form);
        int reach = attack?.Projectile.MaxTravelTiles ?? lens.LongestKnownReach;
        int sight = lens.SightRange(context.Self.FormId);

        // WHERE A CLAIM INTERRUPT IS DECLARED, THE CEILING TRACK GOES FIRST, and
        // this ordering is a correction my own gap arithmetic got backwards.
        //
        // The gap model prices a tier by the declared number it closes, and on
        // that model sight (a gun that reaches 6 behind eyes that reach 4) beats
        // a ceiling worth less than one bolt of the heaviest declared hit. Two
        // contract facts say otherwise once damage is progress. First, team
        // perception is an immediate UNION, so an ally already fills most of my
        // sight gap and the tier buys the part nobody else covers. Second, under
        // the interrupt the body standing on the point IS the claim: losing it
        // costs the run, the respawn delay and the ground, so a point of ceiling
        // is bought in the same currency the match is scored in. Measured over
        // the same sixteen mirror cells: ceiling-first 16-0-0 at +14.8, the gap
        // model's sight-first 15-0-1 at +13.8.
        if (interrupts
            && track.Effect.Contains("max-health-delta", StringComparison.Ordinal))
        {
            return (Math.Max(1, reach) + 1) * step;
        }

        if (track.Effect.Contains("vision-range-delta", StringComparison.Ordinal))
            return Math.Max(0, reach - sight) * step;
        if (track.Effect.Contains(
                "attack-travel-tiles-delta",
                StringComparison.Ordinal))
        {
            // The longest reach DECLARED on this board, whoever owns it. A gun
            // that is outranged is outranged by a form, not by a class, and the
            // form catalog is public before tick zero — which is also why this
            // reads a deficit in a mirror, where the thing outranging the mobile
            // gun is the turret both sides can raise.
            return Math.Max(0, lens.LongestKnownReach - reach) * step;
        }
        if (track.Effect.Contains(
                "max-health-delta",
                StringComparison.Ordinal))
        {
            // One tier is worth a bolt only when it actually adds one: against
            // a two-damage fan, one point of ceiling buys nothing.
            return step / Math.Max(1, lens.HeaviestHit);
        }
        return 0;
    }

    /// <summary>
    /// The nearest live pile worth a detour of at most <paramref name="budget"/>
    /// steps from <paramref name="from"/>, or null.
    ///
    /// <para>Stepping onto a pile banks the assay instantly with no transport,
    /// so the whole value of a short detour is realised on arrival — which is
    /// exactly why this doctrine takes one-step piles and refuses the walk. A
    /// pile that expires before this body could reach it is not a pile.</para>
    /// </summary>
    public Position? NearestPile(
        GenericActorContext context,
        Dictionary<Position, int> reachable,
        int budget)
    {
        if (!Garrison.Salvage || !Declared || budget <= 0)
            return null;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return null;
        }

        Position? best = null;
        int bestCost = int.MaxValue;
        int bestAmount = 0;
        foreach (GenericActorContext.ScrapPile pile in mode.ScrapPiles)
        {
            if (!reachable.TryGetValue(pile.Position, out int cost))
                continue;
            if (cost > budget)
                continue;
            // The pile is gone the first tick `tick >= expiresAtTick`, and it
            // takes `cost` ticks to get there.
            if (context.Tick + cost >= pile.ExpiresAtTick)
                continue;
            if (cost < bestCost
                || (cost == bestCost && pile.Amount > bestAmount)
                || (cost == bestCost
                    && pile.Amount == bestAmount
                    && best is Position current
                    && (pile.Position.Y, pile.Position.X)
                        .CompareTo((current.Y, current.X)) < 0))
            {
                best = pile.Position;
                bestCost = cost;
                bestAmount = pile.Amount;
            }
        }
        return best;
    }
}
