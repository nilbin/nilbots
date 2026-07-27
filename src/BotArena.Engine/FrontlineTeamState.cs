using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Canonical stable-unit state and cumulative ledger for one team.</summary>
public sealed class FrontlineTeamState
{
    private readonly ImmutableArray<FrontlineUnitState> _units;

    internal FrontlineTeamState(
        int teamId,
        IEnumerable<FrontlineUnitState> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        FrontlineUnitState[] ordered = units
            .OrderBy(unit => unit.UnitId)
            .ToArray();
        if (ordered.Any(unit => unit.TeamId != teamId))
        {
            throw new ArgumentException(
                "Every unit must belong to its containing team.",
                nameof(units));
        }
        if (ordered.Select(unit => unit.UnitId).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Team unit IDs must be unique.", nameof(units));

        TeamId = teamId;
        _units = ordered.ToImmutableArray();
    }

    public int TeamId { get; }
    public IReadOnlyList<FrontlineUnitState> Units => _units;
    public long DamageDealt => _units.Sum(unit => unit.DamageDealt);

    public FrontlineUnitState GetUnit(int unitId) =>
        _units.FirstOrDefault(unit => unit.UnitId == unitId)
        ?? throw new KeyNotFoundException(
            $"Team {TeamId} does not contain unit {unitId}.");
}
