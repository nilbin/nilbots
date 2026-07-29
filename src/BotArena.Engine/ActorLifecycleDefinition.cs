using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Rules-owned actor lifecycle catalog. Match-local unit slots select one
/// profile through <see cref="ActorUnitSlotLifecycleAssignmentDefinition"/>.
/// The destruction clock and new-life initialization are fixed semantics so
/// a numeric profile delay cannot acquire a different meaning across modes.
/// </summary>
public sealed record ActorLifecycleDefinition
{
    public ActorLifecycleDefinition(
        IEnumerable<ActorLifecycleProfileDefinition> profiles,
        ActorAutomaticReturnPlacementKind automaticReturnPlacement =
            ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (!Enum.IsDefined(automaticReturnPlacement))
        {
            throw new ArgumentOutOfRangeException(
                nameof(automaticReturnPlacement));
        }

        ActorLifecycleProfileDefinition[] snapshot = [.. profiles];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException(
                "An actor lifecycle catalog must contain at least one profile.",
                nameof(profiles));
        }
        if (snapshot.Any(profile => profile is null))
        {
            throw new ArgumentException(
                "An actor lifecycle catalog cannot contain null profiles.",
                nameof(profiles));
        }
        if (snapshot
            .Select(profile => profile.ProfileId)
            .Distinct(StringComparer.Ordinal)
            .Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Actor lifecycle profile IDs must be unique.",
                nameof(profiles));
        }

        Profiles = snapshot
            .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .ToImmutableArray();
        AutomaticReturnPlacement = automaticReturnPlacement;
    }

    public ImmutableArray<ActorLifecycleProfileDefinition> Profiles { get; }

    public DestructionClockKind DestructionClock =>
        DestructionClockKind
            .TickStartAtDestroyedTickPlusOnePlusProfileDelayCheckedArithmetic;

    public NewLifeSemanticsKind NewLifeSemantics =>
        NewLifeSemanticsKind
            .FreshRuntimeEmptyMemoryDeterministicSeedTargetFormMaximumMonotonicLifeIdCanActOnCreationTick;

    public NewLifeCombatStateKind NewLifeCombatState =>
        NewLifeCombatStateKind
            .ZeroCooldownTargetMaximumEnergyNoPreviousAction;

    public NewLifeResourceClockKind NewLifeResourceClock =>
        NewLifeResourceClockKind
            .UsesMatchGlobalProfileCadenceStartingAfterCreationTickActions;

    public GenerationSemanticsKind GenerationSemantics =>
        GenerationSemanticsKind
            .AutomaticRespawnPreservesDestroyedGenerationFabricationAndReplicationUseSourcePlusOne;

    public ActorAutomaticReturnPlacementKind AutomaticReturnPlacement { get; }

    public TickStartLifecycleOrderKind TickStartLifecycleOrder =>
        TickStartLifecycleOrderKind
            .DueTickThenReturnsAndReadinessThenFabricationThenReplicationCanonicalActorOrder;
    public OutputTileProjectileKind OutputTileProjectile =>
        OutputTileProjectileKind
            .DueCreationConsumesOccupantsByProjectileIdWithoutDamageBeforeSpawn;

    public enum DestructionClockKind
    {
        /// <summary>
        /// Destruction on tick D schedules the profile transition for tick
        /// D + 1 + DelayTicks, using checked integer arithmetic.
        /// </summary>
        TickStartAtDestroyedTickPlusOnePlusProfileDelayCheckedArithmetic = 0,
    }

    public enum NewLifeSemanticsKind
    {
        /// <summary>
        /// Every automatic return or explicit fabrication creates a distinct
        /// life with an increasing slot-local life ID, a fresh runtime and
        /// empty private memory, a deterministically derived life seed, and
        /// target-form maximum health. It may act on its creation tick.
        /// </summary>
        FreshRuntimeEmptyMemoryDeterministicSeedTargetFormMaximumMonotonicLifeIdCanActOnCreationTick = 0,
    }

    public enum GenerationSemanticsKind
    {
        /// <summary>
        /// Generation describes lineage, not stable-slot identity. Automatic
        /// respawn preserves the destroyed life's generation; Fabrication
        /// and replication descendants use their source generation plus one.
        /// </summary>
        AutomaticRespawnPreservesDestroyedGenerationFabricationAndReplicationUseSourcePlusOne = 0,
    }

    public enum NewLifeCombatStateKind
    {
        /// <summary>
        /// A new life begins with zero cooldown, the target attack profile's
        /// maximum energy (or no energy value when it has no pool), and no
        /// previous action result.
        /// </summary>
        ZeroCooldownTargetMaximumEnergyNoPreviousAction = 0,
    }

    public enum NewLifeResourceClockKind
    {
        /// <summary>
        /// New lives use the attack profile's global match-tick cadence. They
        /// may act on their creation tick; its normal end-of-tick cost and
        /// regeneration update is their first resource update.
        /// </summary>
        UsesMatchGlobalProfileCadenceStartingAfterCreationTickActions = 0,
    }

    public enum ActorAutomaticReturnPlacementKind
    {
        /// <summary>
        /// Each automatic-return slot owns one unique map spawn. While the
        /// slot is alive elsewhere or awaiting return, other actors and
        /// lifecycle actions cannot occupy or reserve that spawn tile. The
        /// due return therefore never needs a hidden retry or displacement
        /// rule.
        /// </summary>
        AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims = 0,

        /// <summary>
        /// Forward rally. Every automatic return and automatic activation
        /// arrives on the mode's active objective position measured one step
        /// toward its own base — the chain-adjacent objective region on the
        /// slot's own side — instead of at its home spawn, so reinforcement
        /// distance stops depending on which side is winning. No map region
        /// is authored for this: the tile is derived from the objective
        /// chain the mode already binds. The arrival takes the first tile of
        /// that region in canonical map order (row, then column) that is not
        /// a wall, an occupied tile, a reserved lifecycle output tile, or a
        /// reserved automatic-return spawn; if the region offers none, or the
        /// active objective already sits on the slot's own chain edge, the
        /// arrival falls back to the permanently reserved assigned spawn,
        /// which keeps that reservation load-bearing. Facing always comes
        /// from the assigned spawn. Same-tick arrivals resolve in the
        /// declared tick-start order and each one blocks the next.
        /// </summary>
        OwnSideChainAdjacentObjectiveTileThenAssignedSpawn = 1,
    }

    public enum TickStartLifecycleOrderKind
    {
        /// <summary>
        /// All due operations already own disjoint reserved slots and tiles.
        /// Apply automatic returns/readiness first, fabrication completions
        /// second, and replication retirements/spawns third. Order returns and
        /// readiness by target team/unit. Order fabrication by source
        /// team/unit/life, transition ID using ordinal comparison, then target
        /// team/unit. Order replication by source team/unit/life then
        /// transition ID using ordinal comparison; its descendant events use
        /// the declared slot-to-position assignment order. Projectile
        /// consumption immediately precedes its corresponding life-spawn
        /// event in numeric projectile-ID order. This total order controls
        /// state and replay facts without granting claims.
        /// </summary>
        DueTickThenReturnsAndReadinessThenFabricationThenReplicationCanonicalActorOrder
            = 0,
    }

    public enum OutputTileProjectileKind
    {
        /// <summary>
        /// Projectiles may traverse or wait on reserved lifecycle output tiles
        /// before the due tick. Immediately before an automatic return,
        /// fabrication, or replication descendant is created, consume every
        /// projectile occupying that output tile in projectile-ID order,
        /// without contact, damage, or score, then create the life. This occurs
        /// before observations, so a due life and projectile never begin a
        /// decision phase on the same tile. Readiness and unlock clocks create
        /// no life and consume nothing.
        /// </summary>
        DueCreationConsumesOccupantsByProjectileIdWithoutDamageBeforeSpawn = 0,
    }
}
