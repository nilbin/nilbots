using System.Security.Cryptography;
using System.Text;

namespace BotArena.Engine;

/// <summary>
/// SHA-256 fingerprints for rules content, map content, and the exact aggregate
/// public contract including its bot-observable provenance identity.
/// </summary>
public static class MatchContractFingerprint
{
    public static string ComputeRules(PublicRulesManifest manifest, GameRules rules)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(rules);
        return Hash(RulesManifestSerializer.SerializeRulesFingerprintPayload(manifest, rules));
    }

    public static string ComputeMap(PublicMapManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Hash(RulesManifestSerializer.SerializeMapFingerprintPayload(manifest));
    }

    public static string ComputeMatch(PublicMatchContractManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Rules.SchemaVersion != manifest.SchemaVersion
            || manifest.Map.SchemaVersion != manifest.SchemaVersion)
        {
            throw new ArgumentException(
                "Match, rules, and map manifest schema versions must match.",
                nameof(manifest));
        }

        ValidateTopology(manifest);
        return Hash(RulesManifestSerializer.SerializeMatchFingerprintPayload(manifest));
    }

    private static void ValidateTopology(PublicMatchContractManifest manifest)
    {
        PublicMatchTopology topology = manifest.Topology
            ?? throw new ArgumentException(
                "A match contract must include an exact topology.",
                nameof(manifest));
        if (topology.Teams.IsDefaultOrEmpty
            || topology.Participants.IsDefaultOrEmpty
            || topology.UnitSlots.IsDefaultOrEmpty
            || topology.InitialLives.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Match topology collections must be initialized and non-empty.",
                nameof(manifest));
        }

        HashSet<int> teamIds = [];
        foreach (PublicScoringTeam team in topology.Teams)
        {
            if (team.TeamId < 0 || !teamIds.Add(team.TeamId))
                throw InvalidTopology("Team IDs must be unique and non-negative.");
        }
        if (teamIds.Count != manifest.Rules.Limits.TeamCount)
        {
            throw InvalidTopology(
                "Topology team count must match the public rules team count.");
        }
        if (topology.Participants.Length != manifest.Rules.Limits.ParticipantCount)
        {
            throw InvalidTopology(
                "Topology participant count must match the public rules participant count.");
        }
        if (topology.UnitSlots.Length != manifest.Rules.Limits.UnitSlotCount)
        {
            throw InvalidTopology(
                "Topology unit-slot count must match the public rules unit-slot count.");
        }

        HashSet<int> mapTeamIds = manifest.Map.Spawns
            .Select(spawn => spawn.TeamId)
            .ToHashSet();
        if (!teamIds.SetEquals(mapTeamIds))
        {
            throw InvalidTopology(
                "Topology teams must exactly match the map spawn teams.");
        }

        Dictionary<int, PublicParticipant> participants = [];
        foreach (PublicParticipant participant in topology.Participants)
        {
            if (participant.ParticipantId < 0
                || !teamIds.Contains(participant.TeamId)
                || !participants.TryAdd(participant.ParticipantId, participant))
            {
                throw InvalidTopology(
                    "Participant IDs must be unique and non-negative and reference a team.");
            }
        }
        if (teamIds.Any(teamId =>
                !participants.Values.Any(participant => participant.TeamId == teamId)))
        {
            throw InvalidTopology(
                "Every scoring team must have at least one submitted participant.");
        }

        Dictionary<(int TeamId, int UnitId), PublicUnitSlot> unitSlots = [];
        foreach (PublicUnitSlot unit in topology.UnitSlots)
        {
            if (unit.UnitId < 0
                || !teamIds.Contains(unit.TeamId)
                || !participants.TryGetValue(
                    unit.ControllerParticipantId,
                    out PublicParticipant? controller)
                || controller.TeamId != unit.TeamId
                || !unitSlots.TryAdd((unit.TeamId, unit.UnitId), unit))
            {
                throw InvalidTopology(
                    "Unit IDs must be unique within a team and controlled by a participant on that team.");
            }
        }
        if (participants.Keys.Any(participantId =>
                !unitSlots.Values.Any(unit =>
                    unit.ControllerParticipantId == participantId)))
        {
            throw InvalidTopology(
                "Every submitted participant must control at least one stable unit slot.");
        }

        HashSet<(int TeamId, int UnitId)> occupiedSlots = [];
        HashSet<(int TeamId, int UnitId, int LifeId)> lifeIds = [];
        HashSet<string> formIds = manifest.Rules.Forms
            .Select(form => form.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (manifest.Rules.Frontline is { } frontline)
        {
            formIds.Add(frontline.Forms.Prime.FormId);
            formIds.Add(frontline.Forms.Child.FormId);
            formIds.Add(frontline.Forms.Turret.FormId);
        }
        foreach (PublicInitialLife life in topology.InitialLives)
        {
            var unitKey = (life.TeamId, life.UnitId);
            if (life.LifeId < 0
                || string.IsNullOrWhiteSpace(life.FormId)
                || !formIds.Contains(life.FormId)
                || !unitSlots.ContainsKey(unitKey)
                || !occupiedSlots.Add(unitKey)
                || !lifeIds.Add((life.TeamId, life.UnitId, life.LifeId)))
            {
                throw InvalidTopology(
                    "Each initial life must uniquely occupy a declared unit slot and use a public form.");
            }
        }

        foreach (int teamId in teamIds)
        {
            int teamUnitSlotCount = topology.UnitSlots.Count(slot => slot.TeamId == teamId);
            int initialLives = topology.InitialLives.Count(life => life.TeamId == teamId);
            if (teamUnitSlotCount > manifest.Rules.Limits.MaxUnitsPerTeam)
            {
                throw InvalidTopology(
                    "Unit slots per team cannot exceed the public rules maximum.");
            }
            if (initialLives != manifest.Rules.Limits.InitialUnitsPerTeam
                || initialLives > manifest.Rules.Limits.MaxUnitsPerTeam)
            {
                throw InvalidTopology(
                    "Initial lives per team must match the public rules limits.");
            }
        }
    }

    private static ArgumentException InvalidTopology(string message) =>
        new(message, "manifest");

    private static string Hash(string canonicalPayload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
}
