namespace BotArena.Engine;

/// <summary>Exact binding of one topology initial life to one named spawn.</summary>
public sealed record InitialLifeDeployment
{
    public InitialLifeDeployment(
        int teamId,
        int unitId,
        int lifeId,
        string formId,
        string spawnId)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (unitId < 0)
            throw new ArgumentOutOfRangeException(nameof(unitId));
        if (lifeId < 0)
            throw new ArgumentOutOfRangeException(nameof(lifeId));
        ArgumentException.ThrowIfNullOrWhiteSpace(formId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spawnId);

        TeamId = teamId;
        UnitId = unitId;
        LifeId = lifeId;
        FormId = formId;
        SpawnId = spawnId;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    public int LifeId { get; }
    public string FormId { get; }
    public string SpawnId { get; }
}
