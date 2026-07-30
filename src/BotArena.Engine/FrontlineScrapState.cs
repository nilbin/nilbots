using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable state owned exclusively by the pure Frontline scrap kernel, kept
/// BESIDE <see cref="FrontlineControlState"/> rather than inside it: the front
/// and the economy are independent subsystems that happen to read the same
/// bodies, and the front's own invariants have no opinion about a pile.
/// <para>The economy is exactly conservative, which is what lets the
/// chronology validator prove a replay's economy rather than spot-check it:
/// every scrap that ever spawned is banked, carried, sitting on a pile,
/// evaporated, or spent.</para>
/// </summary>
/// <param name="Teams">
/// One record per scoring team, ordered by team ID.
/// </param>
/// <param name="Piles">
/// Live piles, ordered by <c>(y, x)</c> — the published order.
/// </param>
/// <param name="CarriedByActor">
/// What each live body is currently carrying. A body carrying nothing is
/// absent rather than zero, so the dictionary is empty for a whole match on
/// every ruleset without the economy.
/// </param>
/// <param name="SpawnedTotal">
/// Every scrap this match has ever created — deposits plus wreckage. The
/// conservation ledger's left-hand side.
/// </param>
/// <param name="EvaporatedTotal">
/// Scrap that expired on a pile, was evicted by the hard pile bound, or left
/// with a body that vanished without a death tile.
/// </param>
public sealed record FrontlineScrapState(
    ImmutableArray<FrontlineScrapTeamState> Teams,
    ImmutableArray<FrontlineScrapPile> Piles,
    ImmutableSortedDictionary<ActorIdentity, int> CarriedByActor,
    long SpawnedTotal,
    long EvaporatedTotal)
{
    /// <summary>What one body is carrying right now.</summary>
    public int CarriedBy(ActorIdentity actorId) =>
        CarriedByActor.GetValueOrDefault(actorId);

    /// <summary>One team's record.</summary>
    public FrontlineScrapTeamState Team(int teamId) =>
        Teams.Single(team => team.TeamId == teamId);

    /// <summary>
    /// The conservation identity, as one equation:
    /// <c>spawned = banked + spent + carried + on-piles + evaporated</c>.
    /// Banked and spent are tracked separately because the bank is the
    /// difference, and the validator wants both halves.
    /// </summary>
    public bool IsConserved() =>
        // A bank is exactly what it has taken in less what it has spent, and
        // every unit that ever spawned is standing in exactly one of the five
        // places it can be.
        Teams.All(team =>
            team.Bank == team.BankedTotal - team.SpentTotal)
        && SpawnedTotal
        == Teams.Sum(team => team.BankedTotal)
            + CarriedByActor.Values.Sum(load => (long)load)
            + Piles.Sum(pile => (long)pile.Amount)
            + EvaporatedTotal;
}

/// <summary>
/// One team's economic position: liquid bank, the tier bought on each declared
/// track in declared order, and the two lifetime accumulators the conservation
/// proof needs.
/// </summary>
/// <param name="TeamId">The scoring team.</param>
/// <param name="Bank">Unspent scrap.</param>
/// <param name="TierLevels">
/// Tier held on each track, positionally against the contract's declared track
/// order.
/// </param>
/// <param name="BankedTotal">
/// Every scrap this team has ever banked — assays plus deposits. Never
/// decreases.
/// </param>
/// <param name="SpentTotal">Every scrap this team has ever spent.</param>
public sealed record FrontlineScrapTeamState(
    int TeamId,
    int Bank,
    ImmutableArray<int> TierLevels,
    long BankedTotal,
    long SpentTotal)
{
    /// <summary>Tiers held across every track.</summary>
    public int TotalTiers => TierLevels.Sum();
}

/// <summary>
/// One pile of loose scrap on one tile. Piles merge by tile, so no origin
/// discriminator is needed — a wreck landing on a live vein is one pile, and a
/// killed carrier is simply a bigger wreck.
/// </summary>
/// <param name="Position">The tile.</param>
/// <param name="Amount">Scrap on it. Always positive.</param>
/// <param name="ExpiresAtTick">
/// The pile is gone the first tick <c>tick &gt;= expiresAtTick</c> — the same
/// clock grammar as <c>holdEndsAtTick</c> and <c>readyAtTick</c>. A merge
/// takes the later of the two expiries.
/// </param>
public sealed record FrontlineScrapPile(
    Position Position,
    int Amount,
    int ExpiresAtTick);
