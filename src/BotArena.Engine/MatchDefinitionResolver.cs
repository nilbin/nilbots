using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Resolves and validates rules, map profile, and ownership topology before a
/// simulation is allowed to create tick-zero state.
/// </summary>
public static class MatchDefinitionResolver
{
    private const string LegacyMobileFormId = "mobile";

    public static ResolvedMatchDefinition Resolve(GameRules rules, ArenaMap map)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(map);
        return Resolve(rules, map, CreateDefaultTopology(rules));
    }

    public static ResolvedMatchDefinition Resolve(
        GameRules rules,
        ArenaMap map,
        PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(topology);

        var errors = new List<string>();
        FrontlineRules? frontlineRules = rules.Frontline;
        FrontlineMapProfile? frontlineMap = map.Frontline;
        bool hasFrontlineRules = frontlineRules is not null;
        bool hasFrontlineMap = map.FormatVersion == 2 && frontlineMap is not null;

        if (hasFrontlineRules != hasFrontlineMap)
        {
            errors.Add(
                "Frontline rules and a format-v2 Frontline map profile must be " +
                "present together.");
        }
        if (!hasFrontlineRules && map.FormatVersion != 1)
            errors.Add("Legacy duel rules require a format-v1 map.");

        bool topologyCanBeInspected =
            ValidateGenericTopology(topology, errors);

        if (frontlineRules is not null)
        {
            ValidateFrontlineRules(rules, frontlineRules, errors);
            if (frontlineMap is not null)
            {
                if (frontlineMap.Positions.Length != frontlineRules.FrontlinePositionCount)
                {
                    errors.Add(
                        "Frontline rules position count must match the ordered map " +
                        "positions.");
                }
                int[] mapTeams = frontlineMap.TeamHomes
                    .Select(home => home.TeamId)
                    .Order()
                    .ToArray();
                if (!mapTeams.SequenceEqual(Enumerable.Range(
                        0,
                        Math.Max(0, frontlineRules.TeamCount))))
                {
                    errors.Add(
                        "Frontline map team homes must match the rules team IDs.");
                }
                ValidateAnchorSpawnSafety(
                    rules,
                    map,
                    frontlineRules,
                    frontlineMap,
                    errors);
            }
            if (topologyCanBeInspected)
                ValidateFrontlineTopology(frontlineRules, topology, errors);
        }
        else if (topologyCanBeInspected)
        {
            ValidateLegacyTopology(topology, errors);
        }

        if (errors.Count > 0)
            throw new MatchDefinitionValidationException(errors);

        return new ResolvedMatchDefinition(
            rules,
            map,
            CanonicalizeTopology(topology),
            frontlineRules,
            frontlineMap);
    }

    private static void ValidateFrontlineRules(
        GameRules outerRules,
        FrontlineRules rules,
        List<string> errors)
    {
        if (rules.TeamCount != 2)
            errors.Add("Frontline requires exactly 2 scoring teams.");
        if (rules.ParticipantsPerTeam != 1)
            errors.Add("Frontline requires exactly 1 submitted participant per team.");
        if (rules.FrontlinePositionCount < 3
            || rules.FrontlinePositionCount % 2 == 0)
        {
            errors.Add(
                "FrontlinePositionCount must be odd and at least 3 so it has one centre.");
        }
        if (rules.PushesToBreach < 1
            || rules.FrontlinePositionCount != (rules.PushesToBreach * 2) - 1)
        {
            errors.Add(
                "Frontline position count must equal twice PushesToBreach minus one.");
        }

        if (rules.InitialUnitsPerTeam != 1)
            errors.Add("Frontline starts with exactly 1 Prime per team.");
        if (rules.MaxUnitsPerTeam < rules.InitialUnitsPerTeam)
        {
            errors.Add(
                "Frontline MaxUnitsPerTeam cannot be below InitialUnitsPerTeam.");
        }
        if (rules.MaxUnitsPerTeam < 1)
            errors.Add("Frontline MaxUnitsPerTeam must be positive.");

        if (rules.CaptureThreshold <= 0)
            errors.Add("Frontline CaptureThreshold must be positive.");
        if (rules.CaptureGainPerSoleTeamTick <= 0)
            errors.Add("Frontline CaptureGainPerSoleTeamTick must be positive.");
        if (rules.CaptureDecayAmount <= 0)
            errors.Add("Frontline CaptureDecayAmount must be positive.");
        if (rules.CaptureDecayIntervalTicks <= 0)
            errors.Add("Frontline CaptureDecayIntervalTicks must be positive.");
        if (rules.RedeployPauseTicks < 0)
            errors.Add("Frontline RedeployPauseTicks cannot be negative.");
        if (rules.PrimeRespawnTicks <= 0)
            errors.Add("Frontline PrimeRespawnTicks must be positive.");
        if ((long)outerRules.MaxTicks + rules.RedeployPauseTicks > int.MaxValue)
        {
            errors.Add(
                "Frontline MaxTicks plus RedeployPauseTicks must fit in a " +
                "32-bit absolute tick.");
        }
        if ((long)outerRules.MaxTicks + rules.PrimeRespawnTicks > int.MaxValue)
        {
            errors.Add(
                "Frontline MaxTicks plus PrimeRespawnTicks must fit in a " +
                "32-bit absolute tick.");
        }
        if (rules.ChildRebuildTicks <= 0)
            errors.Add("Frontline ChildRebuildTicks must be positive.");
        if ((long)outerRules.MaxTicks + rules.ChildRebuildTicks > int.MaxValue)
        {
            errors.Add(
                "Frontline MaxTicks plus ChildRebuildTicks must fit in a " +
                "32-bit absolute tick.");
        }
        if (rules.AnchorWindupTicks <= 0)
            errors.Add("Frontline AnchorWindupTicks must be positive.");
        if ((long)outerRules.MaxTicks + rules.AnchorWindupTicks - 1
            > int.MaxValue)
        {
            errors.Add(
                "Frontline MaxTicks plus AnchorWindupTicks minus one must fit " +
                "in a 32-bit absolute tick.");
        }
        if (rules.AnchorHealthGain < 0)
            errors.Add("Frontline AnchorHealthGain cannot be negative.");
        if (!rules.AnchorIrreversibleForLife)
        {
            errors.Add(
                "Frontline Anchor must be irreversible for the current life.");
        }

        if (rules.FabricationUnlockTicks.IsDefault)
        {
            errors.Add("Frontline FabricationUnlockTicks must be initialized.");
        }
        else
        {
            int expectedUnlocks =
                rules.MaxUnitsPerTeam - rules.InitialUnitsPerTeam;
            if (rules.FabricationUnlockTicks.Length != expectedUnlocks)
            {
                errors.Add(
                    "Frontline fabrication unlock count must equal the additional " +
                    "unit-slot count.");
            }
            for (int index = 0; index < rules.FabricationUnlockTicks.Length; index++)
            {
                int tick = rules.FabricationUnlockTicks[index];
                if (tick <= 0)
                    errors.Add("Frontline fabrication unlock ticks must be positive.");
                if (tick >= outerRules.MaxTicks)
                {
                    errors.Add(
                        "Frontline fabrication unlock ticks must occur before MaxTicks.");
                }
                if (index > 0 && tick <= rules.FabricationUnlockTicks[index - 1])
                {
                    errors.Add(
                        "Frontline fabrication unlock ticks must be strictly increasing.");
                }
            }
        }

        if (outerRules.MaxTicks <= 0)
            errors.Add("Frontline MaxTicks must be positive.");
        if (outerRules.ShotRange < 0)
            errors.Add("Frontline ShotRange cannot be negative.");
        if (outerRules.ZoneControl || outerRules.ActiveZoneControl)
        {
            errors.Add(
                "Legacy zone-control rules cannot be combined with Frontline.");
        }
        if (outerRules.SeedSpawnVariation)
        {
            errors.Add(
                "Legacy seed-spawn variation cannot be combined with fixed Frontline homes.");
        }

        UnitFormRules[] forms = [rules.PrimeForm, rules.ChildForm, rules.TurretForm];
        if (forms.Any(form => form is null))
        {
            errors.Add("Frontline Prime, child, and turret forms are required.");
            return;
        }
        if (forms.Select(form => form.FormId).Distinct(StringComparer.Ordinal).Count()
            != forms.Length)
        {
            errors.Add("Frontline form IDs must be unique.");
        }
        foreach (UnitFormRules form in forms)
        {
            if (string.IsNullOrWhiteSpace(form.FormId))
                errors.Add("Frontline form IDs cannot be empty.");
            if (form.MaxHealth <= 0)
                errors.Add($"Frontline form '{form.FormId}' MaxHealth must be positive.");
            if (form.VisionRange < 0)
                errors.Add($"Frontline form '{form.FormId}' VisionRange cannot be negative.");
            if (form.ShootCooldownTicks < 0)
            {
                errors.Add(
                    $"Frontline form '{form.FormId}' ShootCooldownTicks cannot be negative.");
            }
            if (form.ObjectiveWeight < 0)
            {
                errors.Add(
                    $"Frontline form '{form.FormId}' ObjectiveWeight cannot be negative.");
            }
        }
        UnitFormRules turret = rules.TurretForm;
        if (turret.CanMove
            || turret.CanRotate
            || !turret.CanShoot
            || !turret.OmnidirectionalVision
            || !turret.OmnidirectionalShooting
            || turret.ObjectiveWeight != 0
            || turret.AllowsProgrammedShots)
        {
            errors.Add(
                "Frontline turret must be stationary, non-rotating, " +
                "omnidirectional for vision and shooting, objective weight " +
                "zero, shoot-capable, and unable to use programmed shots.");
        }
    }

    private static void ValidateAnchorSpawnSafety(
        GameRules outerRules,
        ArenaMap map,
        FrontlineRules frontlineRules,
        FrontlineMapProfile profile,
        List<string> errors)
    {
        // This invariant deliberately protects the automatic authored Prime
        // return tile. Other protected-pad tiles are explicit child
        // fabrication candidates, but do not receive spawn-fire immunity.
        FrontlineAnchorSpawnThreat? threat = FrontlineMapSafety
            .FindAnchorSpawnThreats(
                outerRules,
                map,
                frontlineRules,
                profile)
            .FirstOrDefault();
        if (threat is not null)
        {
            errors.Add(
                $"Legal Anchor tile {threat.AnchorTile} can fire into team " +
                $"{threat.TeamId} Prime spawn {threat.PrimeSpawn} with launch " +
                $"heading {threat.LaunchHeading} and program {threat.Program}.");
        }
    }

    private static bool ValidateGenericTopology(
        PublicMatchTopology topology,
        List<string> errors)
    {
        if (topology.Teams.IsDefaultOrEmpty
            || topology.Participants.IsDefaultOrEmpty
            || topology.UnitSlots.IsDefaultOrEmpty
            || topology.InitialLives.IsDefaultOrEmpty)
        {
            errors.Add(
                "Match topology teams, participants, unit slots, and initial lives " +
                "must be initialized and non-empty.");
            return false;
        }
        if (topology.Teams.Any(team => team is null)
            || topology.Participants.Any(participant => participant is null)
            || topology.UnitSlots.Any(slot => slot is null)
            || topology.InitialLives.Any(life => life is null))
        {
            errors.Add("Match topology collections cannot contain null entries.");
            return false;
        }

        var teamIds = new HashSet<int>();
        foreach (PublicScoringTeam team in topology.Teams)
        {
            if (team.TeamId < 0 || !teamIds.Add(team.TeamId))
                errors.Add("Topology team IDs must be unique and non-negative.");
        }

        var participants = new Dictionary<int, PublicParticipant>();
        foreach (PublicParticipant participant in topology.Participants)
        {
            if (participant.ParticipantId < 0
                || !teamIds.Contains(participant.TeamId)
                || !participants.TryAdd(participant.ParticipantId, participant))
            {
                errors.Add(
                    "Topology participants must have unique non-negative IDs and " +
                    "reference a declared team.");
            }
        }

        var unitSlots = new Dictionary<(int TeamId, int UnitId), PublicUnitSlot>();
        foreach (PublicUnitSlot slot in topology.UnitSlots)
        {
            if (slot.UnitId < 0
                || !teamIds.Contains(slot.TeamId)
                || !participants.TryGetValue(
                    slot.ControllerParticipantId,
                    out PublicParticipant? controller)
                || controller.TeamId != slot.TeamId
                || !unitSlots.TryAdd((slot.TeamId, slot.UnitId), slot))
            {
                errors.Add(
                    "Topology unit slots must be unique within a team and controlled " +
                    "by a participant on that team.");
            }
        }

        var occupiedSlots = new HashSet<(int TeamId, int UnitId)>();
        var lifeIds = new HashSet<(int TeamId, int UnitId, int LifeId)>();
        foreach (PublicInitialLife life in topology.InitialLives)
        {
            var unitKey = (life.TeamId, life.UnitId);
            if (life.LifeId < 0
                || string.IsNullOrWhiteSpace(life.FormId)
                || !unitSlots.ContainsKey(unitKey)
                || !occupiedSlots.Add(unitKey)
                || !lifeIds.Add((life.TeamId, life.UnitId, life.LifeId)))
            {
                errors.Add(
                    "Each initial life must uniquely occupy a declared unit slot and " +
                    "have a non-negative life ID and form.");
            }
        }

        foreach (int teamId in teamIds.Order())
        {
            if (!topology.Participants.Any(participant => participant.TeamId == teamId))
                errors.Add($"Topology team {teamId} has no submitted participant.");
            if (!topology.UnitSlots.Any(slot => slot.TeamId == teamId))
                errors.Add($"Topology team {teamId} has no stable unit slot.");
            if (!topology.InitialLives.Any(life => life.TeamId == teamId))
                errors.Add($"Topology team {teamId} has no initial life.");
        }

        return true;
    }

    private static void ValidateFrontlineTopology(
        FrontlineRules rules,
        PublicMatchTopology topology,
        List<string> errors)
    {
        string? primeFormId = rules.PrimeForm?.FormId;
        int[] teamIds = topology.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        if (!teamIds.SequenceEqual([0, 1]))
            errors.Add("Frontline topology must contain exactly team IDs 0 and 1.");

        foreach (int teamId in Enumerable.Range(0, 2))
        {
            PublicParticipant[] participants = topology.Participants
                .Where(participant => participant.TeamId == teamId)
                .ToArray();
            if (participants.Length != rules.ParticipantsPerTeam)
            {
                errors.Add(
                    $"Frontline team {teamId} must have exactly " +
                    $"{rules.ParticipantsPerTeam} participant.");
            }

            int[] unitIds = topology.UnitSlots
                .Where(slot => slot.TeamId == teamId)
                .Select(slot => slot.UnitId)
                .Order()
                .ToArray();
            if (!unitIds.SequenceEqual(Enumerable.Range(
                    0,
                    Math.Max(0, rules.MaxUnitsPerTeam))))
            {
                errors.Add(
                    $"Frontline team {teamId} must declare unit slots 0 through " +
                    $"{rules.MaxUnitsPerTeam - 1}.");
            }

            PublicInitialLife[] initialLives = topology.InitialLives
                .Where(life => life.TeamId == teamId)
                .ToArray();
            if (initialLives.Length != rules.InitialUnitsPerTeam
                || initialLives.Any(life =>
                    life.UnitId != 0
                    || life.LifeId != 0
                    || primeFormId is not null
                        && !string.Equals(
                            life.FormId,
                            primeFormId,
                            StringComparison.Ordinal)))
            {
                errors.Add(
                    $"Frontline team {teamId} must begin with life 0 of unit 0 in " +
                    $"form '{primeFormId ?? "prime-mobile"}'.");
            }
        }
    }

    private static void ValidateLegacyTopology(
        PublicMatchTopology topology,
        List<string> errors)
    {
        if (!topology.Teams
                .Select(team => team.TeamId)
                .Order()
                .SequenceEqual([0, 1])
            || topology.Participants.Length != 2
            || topology.UnitSlots.Length != 2
            || topology.InitialLives.Length != 2)
        {
            errors.Add(
                "Legacy duel resolution requires two teams, participants, unit slots, " +
                "and initial lives.");
            return;
        }

        foreach (int teamId in Enumerable.Range(0, 2))
        {
            if (topology.Participants.Count(participant => participant.TeamId == teamId) != 1
                || topology.UnitSlots.Count(slot =>
                    slot.TeamId == teamId && slot.UnitId == 0) != 1
                || topology.InitialLives.Count(life =>
                    life.TeamId == teamId
                    && life.UnitId == 0
                    && life.LifeId == 0
                    && life.FormId == LegacyMobileFormId) != 1)
            {
                errors.Add(
                    $"Legacy duel team {teamId} must own one mobile unit 0 life 0.");
            }
        }
    }

    private static PublicMatchTopology CreateDefaultTopology(GameRules rules)
    {
        if (rules.Frontline is not FrontlineRules frontline)
        {
            return new PublicMatchTopology
            {
                Teams = [new(0), new(1)],
                Participants = [new(0, 0), new(1, 1)],
                UnitSlots = [new(0, 0, 0), new(1, 0, 1)],
                InitialLives =
                [
                    new(0, 0, 0, LegacyMobileFormId),
                    new(1, 0, 0, LegacyMobileFormId),
                ],
            };
        }

        int slotsPerTeam = Math.Max(0, frontline.MaxUnitsPerTeam);
        ImmutableArray<PublicScoringTeam> teams = [new(0), new(1)];
        return new PublicMatchTopology
        {
            Teams = teams,
            Participants = [new(0, 0), new(1, 1)],
            UnitSlots = teams
                .SelectMany(team => Enumerable
                    .Range(0, slotsPerTeam)
                    .Select(unitId => new PublicUnitSlot(
                        team.TeamId,
                        unitId,
                        ControllerParticipantId: team.TeamId)))
                .ToImmutableArray(),
            InitialLives = teams
                .Select(team => new PublicInitialLife(
                    team.TeamId,
                    UnitId: 0,
                    LifeId: 0,
                    frontline.PrimeForm?.FormId ?? "prime-mobile"))
                .ToImmutableArray(),
        };
    }

    private static PublicMatchTopology CanonicalizeTopology(
        PublicMatchTopology topology) =>
        topology with
        {
            Teams = topology.Teams
                .OrderBy(team => team.TeamId)
                .ToImmutableArray(),
            Participants = topology.Participants
                .OrderBy(participant => participant.ParticipantId)
                .ToImmutableArray(),
            UnitSlots = topology.UnitSlots
                .OrderBy(slot => slot.TeamId)
                .ThenBy(slot => slot.UnitId)
                .ToImmutableArray(),
            InitialLives = topology.InitialLives
                .OrderBy(life => life.TeamId)
                .ThenBy(life => life.UnitId)
                .ThenBy(life => life.LifeId)
                .ToImmutableArray(),
        };
}
