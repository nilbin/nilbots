namespace BotArena.Engine;

/// <summary>
/// State of one stable team-local unit slot across independently instantiated
/// lives.
/// </summary>
public sealed class FrontlineUnitState
{
    internal FrontlineUnitState(
        int teamId,
        int unitId,
        string formId,
        FrontlineLifeState? activeLife,
        int nextLifeId,
        FrontlineLifecycleStatus lifecycleStatus = FrontlineLifecycleStatus.Active,
        int? respawnAtTick = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formId);
        if (activeLife is not null
            && (activeLife.ActorId.TeamId != teamId
                || activeLife.ActorId.UnitId != unitId))
        {
            throw new ArgumentException(
                "Active life identity must match its stable unit.",
                nameof(activeLife));
        }

        TeamId = teamId;
        UnitId = unitId;
        DefaultFormId = formId;
        ActiveLife = activeLife;
        NextLifeId = nextLifeId;
        LifecycleStatus = lifecycleStatus;
        RespawnAtTick = respawnAtTick;
        HasSpawned = activeLife is not null;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    /// <summary>Stable deployment-default form for this slot lineage.</summary>
    public string DefaultFormId { get; }
    /// <summary>
    /// Effective form for existing engine consumers. An absent slot naturally
    /// falls back to its deployment default.
    /// </summary>
    public string FormId => ActiveLife?.FormId ?? DefaultFormId;
    public FrontlineLifecycleStatus LifecycleStatus { get; internal set; }
    public FrontlineLifeState? ActiveLife { get; internal set; }
    public int NextLifeId { get; internal set; }
    /// <summary>
    /// Absolute tick at whose start the next life appears. Null unless a
    /// respawn is queued.
    /// </summary>
    public int? RespawnAtTick { get; internal set; }
    /// <summary>Fixed match tick at which this child slot first becomes ready.</summary>
    public int? UnlockAtTick { get; internal set; }
    /// <summary>Tick start at which a destroyed child becomes ready to rebuild.</summary>
    public int? RebuildReadyAtTick { get; internal set; }
    /// <summary>Tick start at which an explicitly queued child life is created.</summary>
    public int? FabricationAtTick { get; internal set; }
    /// <summary>Spawn tile reserved by a successful fabrication action.</summary>
    public Position? ReservedSpawn { get; internal set; }
    public ActorSpawnReason? PendingSpawnReason { get; internal set; }
    public bool HasSpawned { get; internal set; }
    public long DamageDealt { get; internal set; }

    public bool IsActive =>
        LifecycleStatus == FrontlineLifecycleStatus.Active
        && ActiveLife is not null;
}
