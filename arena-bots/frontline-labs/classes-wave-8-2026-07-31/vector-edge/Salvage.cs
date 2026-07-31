using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// The battlefield economy, priced the way this lineage prices everything —
/// against the tick it costs.
///
/// <para>The whole block is <b>absent</b> on every ruleset without the arm, so
/// <see cref="Read"/> returning null is a real answer and every rule below
/// disappears rather than misfiring. Where it is present, three facts decide
/// what a striker does with it, and all three are contract data read before
/// tick zero: the deposits are on a public metronome in the two rows nothing
/// else touches, every destroyed body drops a wreck <em>where it died</em>,
/// and stepping onto a pile banks the assay instantly with no transport.</para>
///
/// <para>The conclusion this doctrine draws from those three is not "go
/// harvesting". A dedicated harvester spends about a quarter of a three-body
/// team's body-ticks in the side lanes, and under a channel two defenders who
/// keep moving hold three stationary attackers — so a body away from the front
/// is a body the front notices. What a duelist that lives at the objective
/// gets for free is the <em>wreckage</em>: corpses fall exactly where it is
/// standing, the assay pays in full at the tile, and a loaded enemy carrier is
/// a visible body worth more dead than an empty one. So the economy is played
/// as a by-product of the fight — a step onto a pile taken on a tick that had
/// nothing better in it, a purchase cast on a tick the gun was reloading
/// anyway — and the deposit run is declined and said so.</para>
/// </summary>
internal sealed class Salvage
{
    /// <summary>
    /// Rule E2 — <b>a tier is bought with a reloading tick</b>. <c>invest</c>
    /// costs the body its action, so it is cast where the action was already
    /// spoken for by a rotation: the gun on cooldown, no step that takes
    /// ground, no bolt inbound. Off, the bank is never spent.
    /// </summary>
    public const bool InvestOnAFreeTick = true;

    /// <summary>
    /// Rule E3 — <b>a loaded carrier is worth more dead</b>. <c>carriedScrap</c>
    /// is published on every visible enemy, and killing one drops its whole
    /// load plus its wreck on a single tile. Off, a courier is an ordinary
    /// body.
    /// </summary>
    public const bool HuntTheCarrier = true;

    /// <summary>Furthest a body will walk for a pile on an otherwise idle tick.</summary>
    private const int PileReach = 4;

    private Salvage(
        GenericActorRulesContract.FrontlineScrapEconomy economy,
        ImmutableHashSet<Position> bank,
        bool investable)
    {
        Economy = economy;
        BankTiles = bank;
        Investable = investable;
    }

    /// <summary>The declared economy.</summary>
    public GenericActorRulesContract.FrontlineScrapEconomy Economy { get; }
    /// <summary>This team's own banking tiles, resolved from the region ID.</summary>
    public ImmutableHashSet<Position> BankTiles { get; }
    /// <summary>
    /// True when the declared purchase mode is the <c>invest</c> verb. The
    /// control level buys by itself and carries no verb at all, so a bot that
    /// reads this skips its purchase routine instead of eating a Blocked.
    /// </summary>
    public bool Investable { get; }

    /// <summary>
    /// The economy this contract declares, or <see langword="null"/> when it
    /// declares none.
    /// </summary>
    public static Salvage? Read(Doctrine doctrine)
    {
        if (doctrine.Contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline
            || frontline.ScrapEconomy is not
                GenericActorRulesContract.FrontlineScrapEconomy economy)
        {
            return null;
        }

        ImmutableHashSet<Position>.Builder bank =
            ImmutableHashSet.CreateBuilder<Position>();
        if (doctrine.TeamId >= 0
            && doctrine.TeamId < economy.BankRegionIds.Length)
        {
            string regionId = economy.BankRegionIds[doctrine.TeamId];
            GenericActorMapContract.Region? region = doctrine.Contract.Map
                .Regions.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.RegionId,
                        regionId,
                        StringComparison.Ordinal));
            foreach (Position tile in region?.Tiles ?? [])
                bank.Add(tile);
        }
        return new Salvage(
            economy,
            bank.ToImmutable(),
            economy.PurchaseMode.Contains(
                "invest-action",
                StringComparison.Ordinal));
    }

    // A rule was built between these two and DELETED. `WorthAStep` routed a
    // body that was not carrying the capture onto the nearest live pile within
    // four tiles, taking the entry its own rank in the team's precedence
    // pointed at so two bodies never raced for one assay. It is a correct
    // reading of the economy and it lost: on the arm where it fires most it
    // turned an eight-seed breach WIN into an eight-seed loss, because on this
    // map four tiles off the route is not "on the way", it is a body the front
    // is missing for eight ticks. The number is in DX.md. What survives is the
    // half that costs nothing: a wreck under a body already standing there is
    // still banked, because the assay is paid by the engine at the tile.

    /// <summary>
    /// The purchase to cast this tick, or <see langword="null"/>.
    ///
    /// <para>Affordability and every cap are in the legality mask, so this
    /// never prices the ladder: a track is offered only when the bank covers
    /// its next tier and no cap forbids it. What is left to decide is WHICH
    /// track, and that is answered from the declared effects against this
    /// chassis's own declared numbers and the enemy's published tiers — never
    /// from a track's name.</para>
    /// </summary>
    public GenericActorDecision? TryInvest(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        if (!InvestOnAFreeTick || !Investable)
            return null;
        GenericActorActionLegality? action = context.ActionLegalities
            .FirstOrDefault(legality =>
                legality.Available
                && doctrine.Contract.Rules.Actions.Any(catalogued =>
                    catalogued.Kind == GenericActorRulesContract.ActionKind
                        .ModeInvestment
                    && string.Equals(
                        catalogued.Id,
                        legality.ActionId,
                        StringComparison.Ordinal)));
        GenericActorActionLegality.ArgumentConstraint.UpgradeTrackConstraint?
            tracks = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UpgradeTrackConstraint>()
                .FirstOrDefault();
        if (action is null || tracks is null || tracks.AllowedTrackIds.IsEmpty)
            return null;

        string? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (GenericActorRulesContract.ScrapUpgradeTrack track
                 in Economy.Tracks)
        {
            if (!tracks.AllowedTrackIds.Contains(track.TrackId))
                continue;
            double score = Value(doctrine, field, context, track);
            if (score > bestScore)
            {
                bestScore = score;
                best = track.TrackId;
            }
        }
        if (best is null)
            return null;
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(best)],
            $"buying {best} with a reloading tick");
    }

    /// <summary>
    /// What one tier of a track is worth to THIS chassis against THIS opponent,
    /// derived from the declared effect policy rather than the track's name so
    /// a track that does not exist yet is priced by what it does.
    ///
    /// <list type="bullet">
    /// <item><b>Gun travel</b> is the standoff race. This doctrine's one
    /// structural edge over a tougher chassis is a band it can shoot into and
    /// be shot in return from — so a tier is worth most exactly when the
    /// opponent's effective reach has caught up with this one's, and the
    /// opponent's tiers are published on the mode state the tick they are
    /// bought.</item>
    /// <item><b>Sight</b> is worth what it converts: a gun that travels
    /// further than the eye sees is a gun aimed at hearsay, and every tier of
    /// sight below that ceiling turns a tile of range into a tile this body
    /// can actually solve a shot on. Above it, nothing.</item>
    /// <item><b>Spawn health</b> never heals, so it is worth a whole extra
    /// contact on every FUTURE life — which is most of the match for a
    /// three-health duelist that dies as often as it kills, and nearly nothing
    /// once the horn is close.</item>
    /// </list>
    /// </summary>
    private double Value(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        GenericActorRulesContract.ScrapUpgradeTrack track)
    {
        int step = Math.Max(1, track.PerTierMagnitude);
        GenericActorRulesContract.AttackProfile? gun =
            doctrine.AttackFor(field.FormId);
        GenericActorRulesContract.VisionProfile? eye =
            doctrine.VisionFor(field.FormId);
        int myTravel = (gun?.Projectile.MaxTravelTiles ?? 0)
            + Tier(doctrine, context, doctrine.TeamId, "mobile-attack-travel");
        int mySight = Math.Max(
            eye?.Range ?? 0,
            eye?.OmnidirectionalProximityRange ?? 0)
            + Tier(doctrine, context, doctrine.TeamId, "vision-range");

        // The opposing chassis is CONTRACT data, not an observation. Deriving
        // it from `context.Enemies` was this revision's own bug: on a tick with
        // nothing in sight the opponent's reach collapsed to zero, the standoff
        // gap looked enormous, and the purchase went to the wrong track for a
        // reason that had nothing to do with the board. Lifecycle assignments
        // name every form each team may ever field, so the answer is available
        // before tick zero and never moves.
        (int enemyTravel, int enemyHealth) = doctrine.OpposingChassis();
        foreach (int teamId in Teams(context))
        {
            if (teamId == doctrine.TeamId)
                continue;
            enemyTravel += Tier(doctrine, context, teamId, "mobile-attack-travel");
            break;
        }

        int myHealth = doctrine.FormFor(field.FormId)?.MaxHealth ?? 1;
        // The band this chassis can shoot into but cannot see into. It is the
        // ordering fact for the whole ladder: this gun travels eight and this
        // eye sees six, so tiles seven and eight are already fired at hearsay,
        // and a ninth is a third tile nobody can aim into.
        int blind = myTravel - mySight;
        if (track.Effect.Contains(
                "attack-travel",
                StringComparison.Ordinal))
        {
            // The standoff is the striker's whole edge over a tougher chassis,
            // and it is a RACE: their tier erases the gap, ours restores it.
            // But a tile of range you cannot see into is not a tile. Buying
            // reach before sight is buying a longer barrel for a gun that is
            // already aimed further than the eye reaches, so the race is only
            // worth entering once the eye has caught up.
            if (blind > 0)
                return 0.6;
            int gap = myTravel - enemyTravel;
            return 1.4 + (gap <= 1 ? 1.2 : 0.0) + 0.1 * step;
        }
        if (track.Effect.Contains("vision", StringComparison.Ordinal))
        {
            // Naturally terminal: every chassis reaches see-as-far-as-you-shoot
            // and the track is worth nothing after that.
            return blind > 0 ? 1.6 + 0.4 * Math.Min(blind, step) : 0.2;
        }
        if (track.Effect.Contains("max-health", StringComparison.Ordinal))
        {
            // It never heals, so it is a whole extra contact on every FUTURE
            // life — worth most against a chassis that already carries more.
            return myHealth < enemyHealth ? 1.3 : 0.9;
        }
        return 0.5;
    }

    private static IEnumerable<int> Teams(GenericActorContext context) =>
        context.Mode is GenericActorContext.ModeObservationState.Frontline
            frontline
            ? frontline.ScrapTeams.Select(state => state.TeamId)
            : [];

    /// <summary>
    /// A team's published tier on the first declared track whose effect matches
    /// <paramref name="effect"/>. Tier vectors are positional against the
    /// contract's declared track order, which is the only correspondence there
    /// is — the observation carries no track IDs.
    /// </summary>
    private int Tier(
        Doctrine doctrine,
        GenericActorContext context,
        int teamId,
        string effect)
    {
        if (context.Mode is not GenericActorContext.ModeObservationState
                .Frontline frontline)
        {
            return 0;
        }
        for (int index = 0; index < Economy.Tracks.Length; index++)
        {
            GenericActorRulesContract.ScrapUpgradeTrack track =
                Economy.Tracks[index];
            if (!track.Effect.Contains(effect, StringComparison.Ordinal))
                continue;
            foreach (GenericActorContext.ScrapTeamState state
                     in frontline.ScrapTeams)
            {
                if (state.TeamId != teamId || index >= state.TierLevels.Length)
                    continue;
                return state.TierLevels[index]
                    * Math.Max(1, track.PerTierMagnitude);
            }
        }
        return 0;
    }
}
