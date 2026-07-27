namespace BotArena.Engine;

/// <summary>
/// Validates structural topology integrity and its exact scoring-team /
/// participant shape without assuming array-position identity.
/// </summary>
public static class MatchFormatTopologyValidator
{
    public static void Validate(
        MatchFormatDefinition format,
        PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(topology);

        var errors = new List<string>();
        if (topology.Teams.IsDefaultOrEmpty
            || topology.Participants.IsDefaultOrEmpty
            || topology.UnitSlots.IsDefaultOrEmpty
            || topology.InitialLives.IsDefaultOrEmpty)
        {
            errors.Add(
                "Teams, participants, unit slots, and initial lives must be initialized and non-empty.");
            throw new MatchFormatValidationException(errors);
        }
        if (topology.Teams.Any(team => team is null)
            || topology.Participants.Any(participant => participant is null)
            || topology.UnitSlots.Any(slot => slot is null)
            || topology.InitialLives.Any(life => life is null))
        {
            errors.Add("Topology collections cannot contain null entries.");
            throw new MatchFormatValidationException(errors);
        }

        var teamsById = new Dictionary<int, PublicScoringTeam>();
        foreach (PublicScoringTeam team in topology.Teams)
        {
            if (team.TeamId < 0 || !teamsById.TryAdd(team.TeamId, team))
                errors.Add("Scoring-team IDs must be unique and non-negative.");
        }
        if (topology.Teams.Length != format.ScoringTeamCount)
        {
            errors.Add(
                $"Format '{format.FormatId}' requires {format.ScoringTeamCount} scoring teams.");
        }

        var participantsById = new Dictionary<int, PublicParticipant>();
        foreach (PublicParticipant participant in topology.Participants)
        {
            if (participant.ParticipantId < 0
                || !teamsById.ContainsKey(participant.TeamId)
                || !participantsById.TryAdd(
                    participant.ParticipantId,
                    participant))
            {
                errors.Add(
                    "Participants must have unique non-negative IDs and reference a declared team.");
            }
        }
        if (topology.Participants.Length != format.ParticipantCount)
        {
            errors.Add(
                $"Format '{format.FormatId}' requires {format.ParticipantCount} participants.");
        }
        foreach (int teamId in teamsById.Keys)
        {
            int count = topology.Participants.Count(
                participant => participant.TeamId == teamId);
            if (count != format.ParticipantsPerTeam)
            {
                errors.Add(
                    $"Team {teamId} must contain exactly {format.ParticipantsPerTeam} participants.");
            }
        }

        var unitSlots = new HashSet<(int TeamId, int UnitId)>();
        var controllersWithSlots = new HashSet<int>();
        foreach (PublicUnitSlot slot in topology.UnitSlots)
        {
            if (slot.UnitId < 0
                || !teamsById.ContainsKey(slot.TeamId)
                || !unitSlots.Add((slot.TeamId, slot.UnitId)))
            {
                errors.Add(
                    "Unit slots must be unique within a declared team and use non-negative IDs.");
                continue;
            }
            if (!participantsById.TryGetValue(
                    slot.ControllerParticipantId,
                    out PublicParticipant? controller)
                || controller.TeamId != slot.TeamId)
            {
                errors.Add(
                    "Every unit slot must be controlled by a participant on the same team.");
                continue;
            }
            controllersWithSlots.Add(slot.ControllerParticipantId);
        }
        foreach (int participantId in participantsById.Keys)
        {
            if (!controllersWithSlots.Contains(participantId))
            {
                errors.Add(
                    $"Participant {participantId} must control at least one unit slot.");
            }
        }

        var occupiedSlots = new HashSet<(int TeamId, int UnitId)>();
        var actorIds = new HashSet<(int TeamId, int UnitId, int LifeId)>();
        foreach (PublicInitialLife life in topology.InitialLives)
        {
            var slot = (life.TeamId, life.UnitId);
            if (life.LifeId < 0
                || string.IsNullOrWhiteSpace(life.FormId)
                || !unitSlots.Contains(slot)
                || !occupiedSlots.Add(slot)
                || !actorIds.Add((life.TeamId, life.UnitId, life.LifeId)))
            {
                errors.Add(
                    "Each initial life must uniquely occupy a declared slot and have a non-negative life ID and form.");
            }
        }
        foreach (int teamId in teamsById.Keys)
        {
            if (!topology.InitialLives.Any(life => life.TeamId == teamId))
                errors.Add($"Team {teamId} must have at least one initial life.");
        }

        if (errors.Count > 0)
            throw new MatchFormatValidationException(errors);
    }
}
