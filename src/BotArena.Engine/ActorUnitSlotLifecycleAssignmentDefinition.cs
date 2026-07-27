using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Match-contract lifecycle binding for one stable unit slot. Rules own the
/// referenced profile; topology, form-catalog, and map-spawn references are
/// validated by the resolved match aggregate.
/// </summary>
public sealed record ActorUnitSlotLifecycleAssignmentDefinition
{
    public ActorUnitSlotLifecycleAssignmentDefinition(
        int teamId,
        int unitId,
        string lifecycleProfileId,
        int? initialGeneration,
        IEnumerable<string> allowedFormIds,
        InitialAvailabilityKind initialAvailability,
        int? unlockTick,
        string? assignedRespawnSpawnId)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (unitId < 0)
            throw new ArgumentOutOfRangeException(nameof(unitId));
        ArgumentException.ThrowIfNullOrWhiteSpace(lifecycleProfileId);
        if (initialGeneration < 0)
            throw new ArgumentOutOfRangeException(nameof(initialGeneration));
        ArgumentNullException.ThrowIfNull(allowedFormIds);
        if (!Enum.IsDefined(initialAvailability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialAvailability));
        }
        if (unlockTick < 0)
            throw new ArgumentOutOfRangeException(nameof(unlockTick));
        if (assignedRespawnSpawnId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                assignedRespawnSpawnId);
        }

        string[] formIds = [.. allowedFormIds];
        if (formIds.Length == 0 || formIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "A lifecycle assignment must allow at least one non-blank form ID.",
                nameof(allowedFormIds));
        }
        if (formIds.Distinct(StringComparer.Ordinal).Count()
            != formIds.Length)
        {
            throw new ArgumentException(
                "Allowed lifecycle form IDs must be unique.",
                nameof(allowedFormIds));
        }

        if (initialAvailability == InitialAvailabilityKind.ActiveAtTickZero
            && unlockTick is not null)
        {
            throw new ArgumentException(
                "A tick-zero active slot cannot also have an unlock tick.",
                nameof(unlockTick));
        }
        if (initialAvailability == InitialAvailabilityKind.ActiveAtTickZero
            && initialGeneration is null)
        {
            throw new ArgumentException(
                "A tick-zero active slot must declare its initial life generation.",
                nameof(initialGeneration));
        }
        if (initialAvailability
                == InitialAvailabilityKind.DormantUnlockAtTick
            && unlockTick is null)
        {
            throw new ArgumentException(
                "A dormant slot must declare its absolute unlock tick.",
                nameof(unlockTick));
        }
        if (initialAvailability
                == InitialAvailabilityKind.DormantUnlockAtTick
            && initialGeneration is not null)
        {
            throw new ArgumentException(
                "A dormant slot has no life generation before fabrication.",
                nameof(initialGeneration));
        }

        TeamId = teamId;
        UnitId = unitId;
        LifecycleProfileId = lifecycleProfileId;
        InitialGeneration = initialGeneration;
        AllowedFormIds = formIds
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        InitialAvailability = initialAvailability;
        UnlockTick = unlockTick;
        AssignedRespawnSpawnId = assignedRespawnSpawnId;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    public string LifecycleProfileId { get; }
    public int? InitialGeneration { get; }
    public ImmutableArray<string> AllowedFormIds { get; }
    public InitialAvailabilityKind InitialAvailability { get; }
    public int? UnlockTick { get; }
    public string? AssignedRespawnSpawnId { get; }

    public enum InitialAvailabilityKind
    {
        /// <summary>The topology supplies one life active at tick zero.</summary>
        ActiveAtTickZero = 0,

        /// <summary>
        /// No life exists initially. At <see cref="UnlockTick"/> the slot
        /// becomes ready for explicit fabrication.
        /// </summary>
        DormantUnlockAtTick = 1,
    }
}
