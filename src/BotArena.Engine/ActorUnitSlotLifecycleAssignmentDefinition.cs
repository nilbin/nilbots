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
        string? assignedRespawnSpawnId,
        string? fabricationOutputFormId = null)
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
        bool delayed = initialAvailability is
            InitialAvailabilityKind.DormantUnlockAtTick
            or InitialAvailabilityKind.DormantAutomaticActivationAtTick;
        if (delayed && unlockTick is null)
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
        if (initialAvailability
                == InitialAvailabilityKind.DormantAutomaticActivationAtTick
            && initialGeneration is null)
        {
            throw new ArgumentException(
                "An automatically activated slot must declare its first life generation.",
                nameof(initialGeneration));
        }
        if (initialAvailability
                == InitialAvailabilityKind.DormantAutomaticActivationAtTick
            && assignedRespawnSpawnId is null)
        {
            throw new ArgumentException(
                "An automatically activated slot must declare its activation and respawn spawn.",
                nameof(assignedRespawnSpawnId));
        }

        if (fabricationOutputFormId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                fabricationOutputFormId);
            if (!formIds.Contains(
                    fabricationOutputFormId,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "A slot's fabrication output form must be one of the "
                    + "forms that slot allows.",
                    nameof(fabricationOutputFormId));
            }
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
        FabricationOutputFormId = fabricationOutputFormId;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    public string LifecycleProfileId { get; }
    public int? InitialGeneration { get; }
    public ImmutableArray<string> AllowedFormIds { get; }
    public InitialAvailabilityKind InitialAvailability { get; }
    public int? UnlockTick { get; }
    public string? AssignedRespawnSpawnId { get; }

    /// <summary>
    /// The form a fabrication INTO this slot produces, overriding the
    /// fabrication transition's declared output
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §9.4: "fabricate
    /// produces the target slot's chassis").
    /// <para>
    /// Null on every slot that takes the transition's own output, which is
    /// every slot outside a MIXED composition — so the property writes no
    /// canonical bytes and no existing contract moves. It exists because a
    /// fabricating body under a mixed composition builds the ARMY, not copies
    /// of itself: a spearhead's fabricator places striker and bulwark bodies
    /// into the slots that declare them.
    /// </para>
    /// <para>The kernel still checks the resolved form against
    /// <see cref="AllowedFormIds"/>, and the constructor checks it here, so an
    /// override can only ever narrow to something the slot could already
    /// hold.</para>
    /// </summary>
    public string? FabricationOutputFormId { get; }

    public enum InitialAvailabilityKind
    {
        /// <summary>The topology supplies one life active at tick zero.</summary>
        ActiveAtTickZero = 0,

        /// <summary>
        /// No life exists initially. At <see cref="UnlockTick"/> the slot
        /// becomes ready for explicit fabrication.
        /// </summary>
        DormantUnlockAtTick = 1,

        /// <summary>
        /// No life exists initially. At <see cref="UnlockTick"/> the engine
        /// creates the slot's first life at its assigned respawn spawn using
        /// the lifecycle profile's automatic-return form. Tick-start
        /// activations share the canonical returns/readiness lifecycle phase
        /// and settle before due fabrication and replication.
        /// </summary>
        DormantAutomaticActivationAtTick = 2,
    }
}
