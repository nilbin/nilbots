using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Resolves Frontline's semantic position order and the opposing scoring-team
/// advance directions onto neutral map regions.
/// </summary>
public sealed record FrontlineActorModeMapBindingDefinition
    : ActorModeMapBindingDefinition
{
    public FrontlineActorModeMapBindingDefinition(
        IReadOnlyList<string> orderedObjectiveRegionIds,
        IEnumerable<FrontlineTeamAdvanceDefinition> teamAdvances)
    {
        ArgumentNullException.ThrowIfNull(orderedObjectiveRegionIds);
        ArgumentNullException.ThrowIfNull(teamAdvances);

        string[] objectiveIds = [.. orderedObjectiveRegionIds];
        if (objectiveIds.Length == 0
            || objectiveIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Frontline must bind at least one non-blank objective region ID.",
                nameof(orderedObjectiveRegionIds));
        }
        if (objectiveIds.Distinct(StringComparer.Ordinal).Count()
            != objectiveIds.Length)
        {
            throw new ArgumentException(
                "Frontline objective region IDs must be unique.",
                nameof(orderedObjectiveRegionIds));
        }

        FrontlineTeamAdvanceDefinition[] advances = [.. teamAdvances];
        if (advances.Length != 2 || advances.Any(advance => advance is null))
        {
            throw new ArgumentException(
                "Frontline must declare exactly two non-null team advances.",
                nameof(teamAdvances));
        }
        if (advances.Select(advance => advance.TeamId).Distinct().Count() != 2)
        {
            throw new ArgumentException(
                "Frontline team advances must reference distinct teams.",
                nameof(teamAdvances));
        }
        if (advances.Sum(advance => advance.ObjectiveIndexDelta) != 0)
        {
            throw new ArgumentException(
                "Frontline teams must advance in opposite objective-index directions.",
                nameof(teamAdvances));
        }

        OrderedObjectiveRegionIds = objectiveIds.ToImmutableArray();
        TeamAdvances = advances
            .OrderBy(advance => advance.TeamId)
            .ToImmutableArray();
    }

    public override ActorModeMapBindingDefinitionKind Kind =>
        ActorModeMapBindingDefinitionKind.Frontline;

    /// <summary>
    /// Semantic position order. This sequence is intentionally not sorted.
    /// </summary>
    public ImmutableArray<string> OrderedObjectiveRegionIds { get; }

    /// <summary>Canonical scoring-team-ID order.</summary>
    public ImmutableArray<FrontlineTeamAdvanceDefinition> TeamAdvances { get; }
}
