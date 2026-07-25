using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

public sealed record BotRecord(
    int Played,
    int Wins,
    int Losses,
    int Draws);

public sealed record BotCombatTotals(
    int Games,
    int DamageDealt,
    int Faults);

public sealed record BotStatistics(
    BotRecord Overall,
    BotRecord Ranked,
    BotRecord Unranked,
    BotCombatTotals Combat);

/// <summary>
/// Builds public, all-time bot statistics from authoritative match history.
/// Ranked records use one completed set as one match; combat totals retain the
/// underlying games. Results do not enter either view until their broadcast ends.
/// </summary>
public sealed class BotStatisticsQuery(
    AppDbContext db,
    TimeProvider timeProvider)
{
    public async Task<BotStatistics?> ExecuteAsync(
        Guid botId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Bots.AnyAsync(bot => bot.Id == botId, cancellationToken))
            return null;

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        List<Match> completedGames = await db.Matches
            .AsNoTracking()
            .Include(match => match.Participants)
            .Where(match =>
                match.Status == MatchStatus.Completed &&
                (match.MatchSetId != null || match.InitiatedByUserId != null) &&
                match.Participants.Any(participant => participant.BotId == botId))
            .ToListAsync(cancellationToken);
        Dictionary<Guid, MatchBroadcastResult> broadcasts =
            completedGames.ToDictionary(
                match => match.Id,
                match => MatchPublicProjection.BroadcastSafe(match, now));

        List<Match> visibleUnrankedGames = completedGames
            .Where(match =>
                match.MatchSetId is null &&
                match.InitiatedByUserId is not null &&
                broadcasts[match.Id].Revealed)
            .ToList();

        Guid[] rankedSetIds = completedGames
            .Where(match => match.MatchSetId is not null)
            .Select(match => match.MatchSetId!.Value)
            .Distinct()
            .ToArray();
        Dictionary<Guid, MatchSet> completedSets = await db.MatchSets
            .AsNoTracking()
            .Where(set =>
                rankedSetIds.Contains(set.Id) &&
                set.Status == MatchSetStatus.Completed &&
                (set.BotAId == botId || set.BotBId == botId))
            .ToDictionaryAsync(set => set.Id, cancellationToken);

        List<(MatchSet Set, List<Match> Games, MatchSetBroadcastResult Broadcast)>
            visibleRankedSets = completedGames
            .Where(match => match.MatchSetId is not null)
            .GroupBy(match => match.MatchSetId!.Value)
            .Where(group =>
                completedSets.ContainsKey(group.Key) &&
                group.Count() == MatchSet.Games)
            .Select(group =>
            {
                MatchSet set = completedSets[group.Key];
                List<Match> games = group.ToList();
                return (
                    Set: set,
                    Games: games,
                    Broadcast: MatchPublicProjection.BroadcastSafe(
                        set,
                        games,
                        now));
            })
            .Where(entry => entry.Broadcast.Revealed)
            .ToList();

        BotRecord ranked = Record(
            visibleRankedSets.Select(entry => Outcome(
                entry.Broadcast.WinnerBotId,
                botId)));
        BotRecord unranked = Record(
            visibleUnrankedGames.Select(match => Outcome(
                match,
                broadcasts[match.Id],
                botId)));
        var overall = new BotRecord(
            ranked.Played + unranked.Played,
            ranked.Wins + unranked.Wins,
            ranked.Losses + unranked.Losses,
            ranked.Draws + unranked.Draws);

        IEnumerable<Match> publicCombatGames = visibleUnrankedGames.Concat(
            visibleRankedSets.SelectMany(entry => entry.Games));
        int games = 0;
        int damageDealt = 0;
        int faults = 0;
        foreach (Match match in publicCombatGames)
        {
            MatchParticipant participant =
                match.Participants.Single(player => player.BotId == botId);
            MatchBroadcastParticipantResult result =
                broadcasts[match.Id].Participants[participant.Slot];
            games++;
            damageDealt += result.DamageDealt ?? 0;
            faults += result.Faults ?? 0;
        }

        return new BotStatistics(
            overall,
            ranked,
            unranked,
            new BotCombatTotals(games, damageDealt, faults));
    }

    private static BotRecord Record(IEnumerable<BotOutcome> outcomes)
    {
        BotOutcome[] values = outcomes.ToArray();
        return new BotRecord(
            values.Length,
            values.Count(outcome => outcome == BotOutcome.Win),
            values.Count(outcome => outcome == BotOutcome.Loss),
            values.Count(outcome => outcome == BotOutcome.Draw));
    }

    private static BotOutcome Outcome(Guid? winnerBotId, Guid botId) =>
        winnerBotId is null
            ? BotOutcome.Draw
            : winnerBotId == botId
                ? BotOutcome.Win
                : BotOutcome.Loss;

    private static BotOutcome Outcome(
        Match match,
        MatchBroadcastResult broadcast,
        Guid botId)
    {
        if (broadcast.WinnerSlot is not int winnerSlot)
            return BotOutcome.Draw;
        int botSlot = match.Participants.Single(
            participant => participant.BotId == botId).Slot;
        return winnerSlot == botSlot ? BotOutcome.Win : BotOutcome.Loss;
    }

    private enum BotOutcome
    {
        Win,
        Loss,
        Draw,
    }
}
