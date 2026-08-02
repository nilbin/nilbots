using BotArena.App.Bots;
using BotArena.App.ArcRelay;

namespace BotArena.App.Matches;

public sealed record MatchBroadcastParticipantResult(
    string? Outcome,
    int? FinalHealth,
    int? DamageDealt,
    int? Faults);

public sealed record MatchBroadcastResult(
    bool Revealed,
    bool Broadcasting,
    int? WinnerSlot,
    string? EndReason,
    int? EndTick,
    string? ReplayHash,
    IReadOnlyDictionary<int, MatchBroadcastParticipantResult> Participants);

public sealed record MatchSummaryParticipantResponse(
    int Slot,
    string NameSnapshot,
    string OwnerDisplayNameSnapshot,
    string AccentSnapshot,
    string LookIdSnapshot,
    string ProjectileLookIdSnapshot,
    string? Outcome,
    int? FinalHealth);

public sealed record MatchSummaryResponse(
    Guid Id,
    string MapId,
    string Status,
    bool Broadcasting,
    Guid? MatchSetId,
    int? SetGame,
    int? WinnerSlot,
    string? EndReason,
    int? EndTick,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<MatchSummaryParticipantResponse> Participants);

public sealed record MatchDetailParticipantResponse(
    int Slot,
    int? TeamId,
    Guid BotId,
    string NameSnapshot,
    string OwnerDisplayNameSnapshot,
    string AccentSnapshot,
    string LookIdSnapshot,
    string ProjectileLookIdSnapshot,
    string ArtifactHashSnapshot,
    string? Outcome,
    int? FinalHealth,
    int? DamageDealt,
    int? Faults)
{
    public ArcRelayMatchEntrantResponse? Entrant { get; init; }
}

public sealed record ArcRelayMatchEntrantResponse(
    Guid? EntrantId,
    string? EntrantKind,
    int? EntrantRevision,
    ArcRelayCrestDescriptor? Crest,
    IReadOnlyList<string> Composition);

public sealed record MatchDetailResponse(
    Guid Id,
    string MapId,
    string GameRulesVersion,
    long Seed,
    string Status,
    bool Broadcasting,
    Guid? MatchSetId,
    int? SetGame,
    int? WinnerSlot,
    string? EndReason,
    int? EndTick,
    string? ReplayHash,
    int? ReplayFormatVersion,
    string? Error,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<MatchDetailParticipantResponse> Participants,
    IReadOnlyList<MatchTeamResultResponse> TeamResults)
{
    public string? ArcRelayLane { get; init; }
}

public sealed record MatchLiveResponse(
    string Status,
    Guid? MatchSetId,
    int? SetGame,
    double PresentationTicksPerSecond,
    int PresentationTick,
    int? TotalTicks,
    bool BroadcastComplete,
    int CountdownMs);

public sealed record MatchSetBotResponse(
    Guid Id,
    string? Name,
    string? Accent,
    string? LookId);

public sealed record MatchSetParticipantResponse(
    int Slot,
    Guid BotId,
    string NameSnapshot,
    string OwnerDisplayNameSnapshot,
    string AccentSnapshot,
    string LookIdSnapshot,
    string ProjectileLookIdSnapshot);

public sealed record MatchSetGameResponse(
    Guid Id,
    int? Game,
    string MapId,
    string Status,
    bool Broadcasting,
    Guid? WinnerBotId,
    bool Draw,
    IReadOnlyList<MatchSetParticipantResponse> Participants);

public sealed record MatchSetResponse(
    Guid Id,
    string Status,
    string RulesVersion,
    MatchSetBotResponse BotA,
    MatchSetBotResponse BotB,
    DateTime CreatedAt,
    bool Revealed,
    double? ScoreA,
    double? ScoreB,
    double? RatingChangeA,
    double? RatingChangeB,
    Guid? WinnerBotId,
    IReadOnlyList<MatchSetGameResponse> Games);

public sealed record MatchSetBroadcastResult(
    bool Revealed,
    double? ScoreA,
    double? ScoreB,
    double? RatingChangeA,
    double? RatingChangeB,
    Guid? WinnerBotId);

public sealed record BotMatchOpponentResponse(
    Guid BotId,
    string NameSnapshot,
    string OwnerDisplayNameSnapshot,
    string AccentSnapshot,
    string LookIdSnapshot);

public sealed record BotMatchHistoryRowResponse(
    Guid Id,
    string MapId,
    string Status,
    bool Broadcasting,
    Guid? MatchSetId,
    int? SetGame,
    DateTime CreatedAt,
    string? Outcome,
    BotMatchOpponentResponse? Opponent);

public sealed record BotMatchHistoryResponse(
    int Wins,
    int Losses,
    int Draws,
    IReadOnlyList<BotMatchHistoryRowResponse> Matches);

public sealed record CreatedMatchResponse(Guid Id);

public sealed record CreatedMatchSetResponse(Guid Id);

/// <summary>
/// Named public match contracts and the single outcome-redaction boundary.
/// Every outcome-bearing response must derive its values from
/// <see cref="BroadcastSafe"/> rather than reading result columns directly.
/// </summary>
public static class MatchPublicProjection
{
    private const string PublicExecutionFailure = "execution_failed";

    public static MatchBroadcastResult BroadcastSafe(
        Match match,
        DateTime utcNow)
    {
        bool revealed = match.BroadcastComplete(utcNow);
        var participants = match.Participants.ToDictionary(
            participant => participant.Slot,
            participant => new MatchBroadcastParticipantResult(
                revealed ? participant.Outcome : null,
                revealed ? participant.FinalHealth : null,
                revealed ? participant.DamageDealt : null,
                revealed ? participant.Faults : null));
        return new MatchBroadcastResult(
            revealed,
            match.Status == MatchStatus.Completed && !revealed,
            revealed ? match.WinnerSlot : null,
            revealed ? match.EndReason : null,
            revealed ? match.EndTick : null,
            revealed ? match.ReplayHash : null,
            participants);
    }

    public static MatchSummaryResponse ToSummary(
        Match match,
        DateTime utcNow)
    {
        MatchBroadcastResult broadcast = BroadcastSafe(match, utcNow);
        return new MatchSummaryResponse(
            match.Id,
            match.MapId,
            match.Status.ToString(),
            broadcast.Broadcasting,
            match.MatchSetId,
            match.SetGame,
            broadcast.WinnerSlot,
            broadcast.EndReason,
            broadcast.EndTick,
            match.CreatedAt,
            match.CompletedAt,
            match.Participants
                .OrderBy(participant => participant.Slot)
                .Select(participant =>
                {
                    MatchBroadcastParticipantResult result =
                        broadcast.Participants[participant.Slot];
                    return new MatchSummaryParticipantResponse(
                        participant.Slot,
                        participant.NameSnapshot,
                        participant.OwnerDisplayNameSnapshot,
                        participant.AccentSnapshot,
                        participant.LookIdSnapshot,
                        participant.ProjectileLookIdSnapshot,
                        result.Outcome,
                        result.FinalHealth);
                })
                .ToArray());
    }

    public static MatchDetailResponse ToDetail(
        Match match,
        DateTime utcNow)
    {
        MatchBroadcastResult broadcast = BroadcastSafe(match, utcNow);
        return new MatchDetailResponse(
            match.Id,
            match.MapId,
            match.GameRulesVersion,
            match.Seed,
            match.Status.ToString(),
            broadcast.Broadcasting,
            match.MatchSetId,
            match.SetGame,
            broadcast.WinnerSlot,
            broadcast.EndReason,
            broadcast.EndTick,
            broadcast.ReplayHash,
            broadcast.Revealed ? match.ReplayFormatVersion : null,
            match.Status == MatchStatus.Failed
                ? PublicExecutionFailure
                : null,
            match.CreatedAt,
            match.CompletedAt,
            match.Participants
                .OrderBy(participant => participant.Slot)
                .Select(participant =>
                {
                    MatchBroadcastParticipantResult result =
                        broadcast.Participants[participant.Slot];
                    return new MatchDetailParticipantResponse(
                        participant.Slot,
                        participant.TeamId,
                        participant.BotId,
                        participant.NameSnapshot,
                        participant.OwnerDisplayNameSnapshot,
                        participant.AccentSnapshot,
                        participant.LookIdSnapshot,
                        participant.ProjectileLookIdSnapshot,
                        participant.ArtifactHashSnapshot,
                        result.Outcome,
                        result.FinalHealth,
                        result.DamageDealt,
                        result.Faults)
                    {
                        Entrant = participant.EntrantIdSnapshot is null
                            ? null
                            : new ArcRelayMatchEntrantResponse(
                                participant.EntrantIdSnapshot,
                                participant.EntrantKindSnapshot,
                                participant.EntrantRevisionSnapshot,
                                participant.CrestSnapshot is { } crest
                                    ? ArcRelayCrestGenerator.ReadSnapshot(crest)
                                    : null,
                                participant.CompositionSnapshot is { } composition
                                    ? ArcRelayComposition.Read(composition).ClassIds
                                    : []),
                    };
                })
                .ToArray(),
            broadcast.Revealed
                ? match.TeamResults
                    .OrderBy(result => result.TeamId)
                    .Select(result => new MatchTeamResultResponse(
                        result.TeamId,
                        result.Placement,
                        result.Outcome.ToString(),
                        result.Scores
                            .OrderBy(score => score.ScoreChannelId)
                            .Select(score =>
                                new MatchTeamScoreResponse(
                                    score.ScoreChannelId,
                                    score.Value.ToString(
                                        System.Globalization
                                            .CultureInfo
                                            .InvariantCulture)))
                            .ToArray()))
                    .ToArray()
                : [])
        {
            ArcRelayLane = match.ArcRelayLane,
        };
    }

    public static MatchLiveResponse ToLive(
        Match match,
        DateTime utcNow)
    {
        MatchBroadcastResult broadcast = BroadcastSafe(match, utcNow);
        int presentationTick = Math.Clamp(
            match.PresentationTick(utcNow),
            -1,
            match.EndTick.GetValueOrDefault(int.MaxValue - 1) + 1);
        return new MatchLiveResponse(
            match.Status.ToString(),
            match.MatchSetId,
            match.SetGame,
            match.PresentationTicksPerSecond,
            presentationTick,
            broadcast.Revealed
                ? broadcast.EndTick is int endTick
                    ? endTick + 1
                    : 0
                : null,
            broadcast.Revealed,
            match.BroadcastStartedAt is DateTime start && start > utcNow
                ? (int)(start - utcNow).TotalMilliseconds
                : 0);
    }

    public static MatchSetResponse ToMatchSet(
        MatchSet set,
        Bot? botA,
        Bot? botB,
        IReadOnlyList<Match> matches,
        DateTime utcNow)
    {
        MatchParticipant? botASnapshot = matches
            .SelectMany(match => match.Participants)
            .FirstOrDefault(participant => participant.BotId == set.BotAId);
        MatchParticipant? botBSnapshot = matches
            .SelectMany(match => match.Participants)
            .FirstOrDefault(participant => participant.BotId == set.BotBId);
        var broadcasts = matches.ToDictionary(
            match => match.Id,
            match => BroadcastSafe(match, utcNow));
        MatchSetBroadcastResult setBroadcast =
            BroadcastSafe(set, matches, utcNow);

        return new MatchSetResponse(
            set.Id,
            set.Status.ToString(),
            set.GameRulesVersion,
            ToSetBot(set.BotAId, botASnapshot, botA),
            ToSetBot(set.BotBId, botBSnapshot, botB),
            set.CreatedAt,
            setBroadcast.Revealed,
            setBroadcast.ScoreA,
            setBroadcast.ScoreB,
            setBroadcast.RatingChangeA,
            setBroadcast.RatingChangeB,
            setBroadcast.WinnerBotId,
            matches.Select(match =>
            {
                MatchBroadcastResult broadcast = broadcasts[match.Id];
                Guid? winnerBotId =
                    broadcast.WinnerSlot is int winner
                        ? match.Participants.Single(
                            participant => participant.Slot == winner).BotId
                        : null;
                return new MatchSetGameResponse(
                    match.Id,
                    match.SetGame,
                    match.MapId,
                    match.Status.ToString(),
                    broadcast.Broadcasting,
                    winnerBotId,
                    broadcast.Revealed &&
                    match.Status == MatchStatus.Completed &&
                    broadcast.WinnerSlot is null,
                    match.Participants
                        .OrderBy(participant => participant.Slot)
                        .Select(participant => new MatchSetParticipantResponse(
                            participant.Slot,
                            participant.BotId,
                            participant.NameSnapshot,
                            participant.OwnerDisplayNameSnapshot,
                            participant.AccentSnapshot,
                            participant.LookIdSnapshot,
                            participant.ProjectileLookIdSnapshot))
                        .ToArray());
            }).ToArray());
    }

    public static MatchSetBroadcastResult BroadcastSafe(
        MatchSet set,
        IReadOnlyList<Match> matches,
        DateTime utcNow)
    {
        bool allWatched = matches.Count == MatchSet.Games && matches.All(match =>
            match.Status == MatchStatus.Failed ||
            BroadcastSafe(match, utcNow).Revealed);
        bool completedAndWatched =
            allWatched && set.Status == MatchSetStatus.Completed;
        return new MatchSetBroadcastResult(
            allWatched && set.Status != MatchSetStatus.Running,
            completedAndWatched ? set.ScoreA : null,
            completedAndWatched ? set.ScoreB : null,
            completedAndWatched ? set.RatingChangeA : null,
            completedAndWatched ? set.RatingChangeB : null,
            completedAndWatched ? set.WinnerBotId : null);
    }

    public static BotMatchHistoryRowResponse ToBotHistory(
        Match match,
        Guid botId,
        DateTime utcNow)
    {
        MatchBroadcastResult broadcast = BroadcastSafe(match, utcNow);
        MatchParticipant self =
            match.Participants.Single(participant => participant.BotId == botId);
        MatchParticipant? opponent = match.Participants
            .FirstOrDefault(participant => participant.BotId != botId);
        string? outcome = broadcast.Revealed
            ? broadcast.WinnerSlot == self.Slot
                ? "Win"
                : broadcast.WinnerSlot is null
                    ? "Draw"
                    : "Loss"
            : null;
        return new BotMatchHistoryRowResponse(
            match.Id,
            match.MapId,
            match.Status.ToString(),
            broadcast.Broadcasting,
            match.MatchSetId,
            match.SetGame,
            match.CreatedAt,
            outcome,
            opponent is null
                ? null
                : new BotMatchOpponentResponse(
                    opponent.BotId,
                    opponent.NameSnapshot,
                    opponent.OwnerDisplayNameSnapshot,
                    opponent.AccentSnapshot,
                    opponent.LookIdSnapshot));
    }

    private static MatchSetBotResponse ToSetBot(
        Guid botId,
        MatchParticipant? snapshot,
        Bot? current) =>
        new(
            botId,
            snapshot?.NameSnapshot ?? current?.Name,
            snapshot?.AccentSnapshot ?? current?.Accent,
            snapshot?.LookIdSnapshot ?? current?.LookId);
}
