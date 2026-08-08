using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// SCRAP. The first arm in this lineage's history whose payoff attaches to a
/// BODY rather than to the team's clock: a currency on the floor, a walk home,
/// and a typed tier that makes the body you are looking at do something it could
/// not do a minute ago.
///
/// <para><b>Nothing in this file moves a body, and that is the doctrine rather
/// than an omission.</b> Two spending routines were written and measured and both
/// lost: an elected quartermaster walking the published deposit metronome, and
/// then a mere two-tile detour onto a pile. The arithmetic the brief states is
/// exactly why. A harvester spends about a quarter of a three-body team's
/// body-ticks; under the channel two defenders who keep moving hold three
/// stationary attackers; and the supply is a FIXED POT, so extra bodies buy
/// security of collection rather than income. A body that steps off its ground
/// for currency has sold the scoring channel to buy the thing that was supposed
/// to help it hold the scoring channel.</para>
///
/// <para>What is left is everything the economy gives away free, which measures
/// as almost all of it: the assay pays in full at the tile with no transport, a
/// load banks by itself on the home pad, corpses fall where a wall is already
/// standing, and a tier is bought by whichever body had no other use for the
/// tick. <c>DX.md</c> carries both cuts with their numbers.</para>
///
/// <para>Everything below is a read of <c>rules.gameMode.scrapEconomy</c> and the
/// per-tick legality; the whole block is absent on a ruleset without the arm and
/// every method then answers "no".</para>
/// </summary>
internal sealed class Economy
{
    /// <summary>
    /// The economy clauses, one switch each — same ablation grammar as
    /// <see cref="Column.Rules"/> and <see cref="Channel.Rules"/>.
    /// </summary>
    internal static class Rules
    {
        /// <summary>
        /// R4. INVEST. Spend the bank the moment the legality mask offers a
        /// track, on the track the CONTRACT says closes the widest gap — never
        /// on a name, and never by pricing the ladder ourselves.
        /// </summary>
        public static bool Invest => true;

        /// <summary>
        /// R5. SALVAGE, and it is a targeting rule rather than a movement one:
        /// a visible enemy publishes what it is carrying, and killing a loaded
        /// carrier drops its whole load plus its wreck on one tile — the largest
        /// single transfer in the game, available to whoever did the killing. So
        /// a carrier outranks an equivalent empty body in the fire order (see
        /// <c>MarchWall.Prioritized</c>) and nothing else about scrap is allowed
        /// to spend a step. The pickup rules this switch used to gate were cut;
        /// the reason and the numbers are in <c>DX.md</c>.
        /// </summary>
        public static bool Salvage => true;
    }

    private readonly GenericActorRulesContract.FrontlineScrapEconomy? _economy;

    private Economy(GenericActorRulesContract.FrontlineScrapEconomy? economy) =>
        _economy = economy;

    /// <summary>True when this ruleset declares a battlefield economy at all.</summary>
    public bool Live => _economy is not null;

    /// <summary>
    /// True when a body spends its action to buy. The control arm declares
    /// <c>automatic-greedy-declared-order</c> and carries no verb at all, so a
    /// purchase routine there is dead code that must not fire.
    /// </summary>
    public bool BuysByAction =>
        _economy?.PurchaseMode.Contains("invest-action", StringComparison.Ordinal)
        == true;

    /// <summary>The declared ladder, in the order the tier vectors are indexed by.</summary>
    public ImmutableArray<GenericActorRulesContract.ScrapUpgradeTrack> Tracks =>
        _economy?.Tracks ?? [];

    public static Economy Read(ContractView view)
    {
        // A load banks by itself on the home pad — no action, no cost, and no
        // decision, which is why the declared bank regions need no reading here.
        return new Economy(view.Frontline?.ScrapEconomy);
    }

    /// <summary>Our team's published bank, or zero.</summary>
    public static int Bank(ContractView view, GenericActorContext context)
    {
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return 0;
        }
        foreach (GenericActorContext.ScrapTeamState team in mode.ScrapTeams)
        {
            if (team.TeamId == view.MyTeamId)
                return team.Bank;
        }
        return 0;
    }

    /// <summary>
    /// BUY THE MASK. The <c>upgrade-track</c> constraint enumerates exactly the
    /// tracks affordable out of the standing bank and below every declared cap
    /// this tick, so the doctrine never prices the ladder — it chooses among what
    /// is already legal. A track absent from the mask is a track we cannot buy,
    /// whatever we think it costs.
    ///
    /// <para>The CHOICE is a contract read too, and deliberately not a name.
    /// Each track declares a typed effect and an integer step, so the value of a
    /// tier is the gap it closes measured in the units that effect moves:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Travel</b> is priced against the longest gun the other side
    /// fields. A chassis that is out-ranged loses the opening shot of every
    /// exchange it did not choose; a tier that erases the standoff is worth more
    /// than any amount of health, and one bought past parity is worth much
    /// less.</item>
    /// <item><b>Spawn health</b> is priced against what a screen has to eat. It
    /// never heals, so it is a ceiling for the NEXT body — which is exactly what
    /// an escort doctrine is spending bodies on.</item>
    /// <item><b>Sight</b> is priced against our own travel: a gun that outranges
    /// its own eyes is firing at what an ally happens to see, and the tier stops
    /// being worth anything the moment the two meet.</item>
    /// </list>
    ///
    /// <para>Nothing is hard-coded to a track ID: an unrecognized effect scores
    /// last and is still bought when it is the only thing on offer, so a ladder
    /// that grows a fourth track does not make this refuse to spend.</para>
    /// </summary>
    public GenericActorDecision? Invest(
        ContractView view,
        GenericActorContext context)
    {
        if (!Rules.Invest || !Live || !BuysByAction)
            return null;

        HashSet<string> investIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.ModeInvestment);
        if (investIds.Count == 0)
            return null;

        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry =>
                         entry.Available && investIds.Contains(entry.ActionId))
                     .OrderBy(entry => entry.ActionId, StringComparer.Ordinal))
        {
            GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
                offered = action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .UpgradeTrackConstraint>()
                    .SingleOrDefault();
            if (offered is null || offered.AllowedTrackIds.IsEmpty)
                continue;

            string? best = null;
            int bestScore = int.MinValue;
            foreach (string trackId in offered.AllowedTrackIds)
            {
                int score = Value(view, context, trackId);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = trackId;
            }
            if (best is null)
                continue;

            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.UpgradeTrackArgument(best)],
                $"investing the bank in {best}");
        }
        return null;
    }

    /// <summary>
    /// What one more tier of a track is worth to THIS team on THIS contract,
    /// resolved from the track's declared effect policy and the published base
    /// numbers it modifies. Higher wins.
    /// </summary>
    private int Value(
        ContractView view,
        GenericActorContext context,
        string trackId)
    {
        GenericActorRulesContract.ScrapUpgradeTrack? track = null;
        foreach (GenericActorRulesContract.ScrapUpgradeTrack candidate in Tracks)
        {
            if (string.Equals(candidate.TrackId, trackId, StringComparison.Ordinal))
                track = candidate;
        }
        if (track is null)
            return 0;

        int step = Math.Max(1, track.PerTierMagnitude);
        int ourTravel = view.OurBestTravel + Tier(view, context, "mobile-attack-travel-tiles-delta");
        int theirTravel = view.OpposingBestTravel;
        int ourVision = view.OurVisionRange + Tier(view, context, "vision-range-delta");

        if (track.Effect.Contains(
                "attack-travel-tiles-delta",
                StringComparison.Ordinal))
        {
            // Closing a standoff is the tier that changes who may open. Past
            // parity it is still real — reach is reach — but it stops being the
            // thing that decides the duel.
            int deficit = Math.Max(0, theirTravel - ourTravel);
            return 100 + (Math.Min(deficit, step) * 60) + (step * 8);
        }
        if (track.Effect.Contains("max-health-delta", StringComparison.Ordinal))
        {
            // A screen's whole job is to be shot at off the objective, where the
            // damage reverts nothing. Health is what buys the next one of those.
            return 90 + (step * 10);
        }
        if (track.Effect.Contains("vision-range-delta", StringComparison.Ordinal))
        {
            int blind = Math.Max(0, ourTravel - ourVision);
            return 60 + (Math.Min(blind, step) * 25);
        }
        return 10;
    }

    /// <summary>
    /// The tier our team already holds on the first declared track carrying an
    /// effect policy, read positionally against the contract's declared track
    /// order exactly as the published vector is indexed.
    /// </summary>
    public int Tier(
        ContractView view,
        GenericActorContext context,
        string effectFragment)
    {
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return 0;
        }
        ImmutableArray<int> levels = [];
        foreach (GenericActorContext.ScrapTeamState team in mode.ScrapTeams)
        {
            if (team.TeamId == view.MyTeamId)
                levels = team.TierLevels;
        }
        if (levels.IsDefaultOrEmpty)
            return 0;
        for (int index = 0; index < Tracks.Length && index < levels.Length; index++)
        {
            if (Tracks[index].Effect.Contains(effectFragment, StringComparison.Ordinal))
                return levels[index];
        }
        return 0;
    }

    /// <summary>Live piles, richest first, then nearest, then in published order.</summary>
    public static IEnumerable<GenericActorContext.ScrapPile> Piles(
        GenericActorContext context,
        Position from) =>
        context.Mode is GenericActorContext.ModeObservationState.Frontline mode
            ? mode.ScrapPiles
                .Where(pile => pile.ExpiresAtTick > context.Tick)
                .OrderByDescending(pile => pile.Amount)
                .ThenBy(pile => Geometry.Chebyshev(pile.Position, from))
                .ThenBy(pile => pile.Position.Y)
                .ThenBy(pile => pile.Position.X)
            : [];
}
