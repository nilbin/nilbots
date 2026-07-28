using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Mode-neutral terminal facts. A null <see cref="EndTick"/> explicitly means
/// that the match completed before any joint tick executed.
/// </summary>
public sealed record GenericActorMatchResult
{
    public GenericActorMatchResult(
        string completionReason,
        int? endTick,
        TeamStandings standings,
        IReadOnlyCollection<int> eligibleTeamIds,
        IReadOnlyCollection<UnitTerminalFact> units,
        GenericActorMatchModeResult mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completionReason);
        if (endTick < 0)
            throw new ArgumentOutOfRangeException(nameof(endTick));
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(eligibleTeamIds);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(mode);

        int[] eligibleSnapshot = [.. eligibleTeamIds];
        if (eligibleSnapshot.Any(teamId => teamId < 0)
            || eligibleSnapshot.Distinct().Count()
                != eligibleSnapshot.Length)
        {
            throw new ArgumentException(
                "Eligible team IDs must be non-negative and unique.",
                nameof(eligibleTeamIds));
        }
        HashSet<int> standingTeams = standings.Standings
            .Select(standing => standing.TeamId)
            .ToHashSet();
        if (eligibleSnapshot.Any(teamId =>
                !standingTeams.Contains(teamId)))
        {
            throw new ArgumentException(
                "Every eligible team must appear in terminal standings.",
                nameof(eligibleTeamIds));
        }

        UnitTerminalFact[] unitSnapshot = [.. units];
        if (unitSnapshot.Length == 0
            || unitSnapshot.Any(unit => unit is null)
            || unitSnapshot
                .Select(unit => (unit.TeamId, unit.UnitId))
                .Distinct()
                .Count() != unitSnapshot.Length
            || unitSnapshot.Any(unit =>
                !standingTeams.Contains(unit.TeamId)))
        {
            throw new ArgumentException(
                "Terminal unit facts must be non-empty, unique, and belong to standing teams.",
                nameof(units));
        }

        ValidateModeTeams(mode, standingTeams);

        CompletionReason = completionReason;
        EndTick = endTick;
        Standings = standings;
        EligibleTeamIds = eligibleSnapshot
            .Order()
            .ToImmutableArray();
        Units = unitSnapshot
            .OrderBy(unit => unit.TeamId)
            .ThenBy(unit => unit.UnitId)
            .ToImmutableArray();
        Mode = mode;
    }

    public string CompletionReason { get; }
    public int? EndTick { get; }
    public TeamStandings Standings { get; }
    public ImmutableArray<int> EligibleTeamIds { get; }
    public ImmutableArray<UnitTerminalFact> Units { get; }
    public GenericActorMatchModeResult Mode { get; }
    public int? WinnerTeamId => Standings.WinnerTeamId;

    public sealed record UnitTerminalFact
    {
        public UnitTerminalFact(
            GenericActorWorldSnapshot.SlotSnapshot slot,
            GenericActorWorldSnapshot.LifeSnapshot? activeLife)
        {
            ArgumentNullException.ThrowIfNull(slot);
            bool active = slot.State is
                GenericActorRuntimeObservation.UnitSlotState.Active;
            if (active != (activeLife is not null))
            {
                throw new ArgumentException(
                    "An active terminal slot needs its exact life; a non-active slot cannot carry one.",
                    nameof(activeLife));
            }
            if (activeLife is not null
                && (activeLife.ActorId.TeamId != slot.TeamId
                    || activeLife.ActorId.UnitId != slot.UnitId
                    || activeLife.ParticipantId != slot.ParticipantId
                    || slot.State is not
                        GenericActorRuntimeObservation.UnitSlotState.Active
                            activeState
                    || activeState.ActorId != activeLife.ActorId
                    || activeState.Generation != activeLife.Generation
                    || !string.Equals(
                        activeState.FormId,
                        activeLife.FormId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Terminal slot and active-life facts disagree.",
                    nameof(activeLife));
            }

            Slot = slot;
            ActiveLife = activeLife;
        }

        public GenericActorWorldSnapshot.SlotSnapshot Slot { get; }
        public GenericActorWorldSnapshot.LifeSnapshot? ActiveLife { get; }
        public int TeamId => Slot.TeamId;
        public int UnitId => Slot.UnitId;
        public int ParticipantId => Slot.ParticipantId;
    }

    private static void ValidateModeTeams(
        GenericActorMatchModeResult mode,
        IReadOnlySet<int> standingTeams)
    {
        if (mode is GenericActorMatchModeResult.Deathmatch deathmatch)
        {
            int[] scoreTeams = deathmatch.Scores.Teams
                .Select(score => score.TeamId)
                .ToArray();
            if (scoreTeams.Length != standingTeams.Count
                || !scoreTeams.ToHashSet().SetEquals(standingTeams))
            {
                throw new ArgumentException(
                    "Deathmatch terminal scores must cover every standing team.",
                    nameof(mode));
            }
        }
        else if (mode is GenericActorMatchModeResult.Frontline frontline)
        {
            int[] scoreTeams = frontline.Scores.Teams
                .Select(score => score.TeamId)
                .ToArray();
            if (scoreTeams.Length != standingTeams.Count
                || !scoreTeams.ToHashSet().SetEquals(standingTeams))
            {
                throw new ArgumentException(
                    "Frontline terminal scores must cover every standing team.",
                    nameof(mode));
            }
        }
    }
}
