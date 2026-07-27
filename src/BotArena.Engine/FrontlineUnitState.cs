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
        FormId = formId;
        ActiveLife = activeLife;
        NextLifeId = nextLifeId;
        LifecycleStatus = lifecycleStatus;
        RespawnAtTick = respawnAtTick;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    public string FormId { get; internal set; }
    public FrontlineLifecycleStatus LifecycleStatus { get; internal set; }
    public FrontlineLifeState? ActiveLife { get; internal set; }
    public int NextLifeId { get; internal set; }
    /// <summary>
    /// Absolute tick at whose start the next life appears. Null unless a
    /// respawn is queued.
    /// </summary>
    public int? RespawnAtTick { get; internal set; }
    public long DamageDealt { get; internal set; }

    public bool IsActive =>
        LifecycleStatus == FrontlineLifecycleStatus.Active
        && ActiveLife is not null;
}
