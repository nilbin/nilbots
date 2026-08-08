using BotArena.Engine;

namespace BotArena.App.Matches;

/// <summary>
/// Lossless match-level persistence projection for generic team standings and
/// signed score channels. Duel-shaped participant fields are compatibility
/// projections only.
/// </summary>
public static class GenericMatchResultPersistence
{
    public static void Apply(
        Match match,
        GenericActorMatchResult result)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(result);
        if (match.TeamResults.Count != 0)
        {
            throw new InvalidOperationException(
                $"Match {match.Id} already has normalized team results.");
        }

        Dictionary<int, MatchParticipant> participants =
            match.Participants.ToDictionary(
                participant => participant.Slot);
        if (participants.Values.Any(participant =>
                participant.TeamId is null))
        {
            throw new InvalidOperationException(
                $"Generic match {match.Id} has a participant without a team.");
        }

        int[] participantTeams = participants.Values
            .Select(participant => participant.TeamId!.Value)
            .Distinct()
            .Order()
            .ToArray();
        int[] standingTeams = result.Standings.Standings
            .Select(standing => standing.TeamId)
            .Order()
            .ToArray();
        if (!participantTeams.SequenceEqual(standingTeams))
        {
            throw new InvalidOperationException(
                $"Generic match {match.Id} terminal standings do not cover " +
                "the persisted participant teams exactly.");
        }

        foreach (TeamStanding standing in
                 result.Standings.Standings.OrderBy(value => value.TeamId))
        {
            var persisted = new MatchTeamResult
            {
                MatchId = match.Id,
                TeamId = standing.TeamId,
                Placement = standing.Rank,
                Outcome = standing.Outcome switch
                {
                    TeamStandingOutcome.Win => MatchTeamOutcome.Win,
                    TeamStandingOutcome.Loss => MatchTeamOutcome.Loss,
                    TeamStandingOutcome.Draw => MatchTeamOutcome.Draw,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(standing.Outcome)),
                },
            };
            foreach (TeamScoreValue score in standing.Scores)
            {
                persisted.Scores.Add(new MatchTeamScore
                {
                    MatchId = match.Id,
                    TeamId = standing.TeamId,
                    ScoreChannelId = ScoreChannelId(score.Channel),
                    Value = score.Value,
                });
            }
            match.TeamResults.Add(persisted);
        }

        foreach (MatchParticipant participant in participants.Values)
        {
            TeamStanding standing =
                result.Standings.Standings.Single(value =>
                    value.TeamId == participant.TeamId);
            participant.Outcome = standing.Outcome.ToString();
            participant.FinalHealth = result.Units
                .Where(unit =>
                    unit.ParticipantId == participant.Slot &&
                    unit.ActiveLife is not null)
                .Sum(unit => unit.ActiveLife!.Health);
            participant.DamageDealt = null;
            participant.Faults = null;
        }

        int[] winningParticipantSlots =
            result.WinnerTeamId is int winnerTeamId
                ? participants.Values
                    .Where(participant =>
                        participant.TeamId == winnerTeamId)
                    .Select(participant => participant.Slot)
                    .ToArray()
                : [];
        match.WinnerSlot = winningParticipantSlots.Length == 1
            ? winningParticipantSlots[0]
            : null;
        match.EndReason = result.CompletionReason;
        match.EndTick = result.EndTick;
    }

    private static string ScoreChannelId(
        ScoreChannelDefinition.ChannelKind channel) =>
        channel switch
        {
            ScoreChannelDefinition.ChannelKind.Kills => "kills",
            ScoreChannelDefinition.ChannelKind.Deaths => "deaths",
            ScoreChannelDefinition.ChannelKind.DamageDealt =>
                "damage-dealt",
            ScoreChannelDefinition.ChannelKind.ActiveHealth =>
                "active-health",
            ScoreChannelDefinition.ChannelKind.TerritorialProgress =>
                "territorial-progress",
            ScoreChannelDefinition.ChannelKind.Pulses => "pulses",
            ScoreChannelDefinition.ChannelKind.ReactorCharge =>
                "reactor-charge",
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };
}
