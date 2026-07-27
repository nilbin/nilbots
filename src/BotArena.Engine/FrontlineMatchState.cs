using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace BotArena.Engine;

/// <summary>Authoritative mutable state owned by one Frontline session.</summary>
public sealed class FrontlineMatchState
{
    private readonly ImmutableArray<FrontlineTeamState> _teams;
    private readonly List<FrontlineProjectileState> _projectiles;
    private readonly ReadOnlyCollection<FrontlineProjectileState> _projectileView;

    internal FrontlineMatchState(
        ResolvedMatchDefinition definition,
        IEnumerable<FrontlineTeamState> teams,
        FrontlineControlState control,
        IEnumerable<FrontlineProjectileState>? projectiles = null,
        long nextProjectileId = 0)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(control);
        if (!definition.IsFrontline)
            throw new ArgumentException(
                "Frontline state requires a resolved Frontline definition.",
                nameof(definition));

        FrontlineTeamState[] orderedTeams = teams
            .OrderBy(team => team.TeamId)
            .ToArray();
        if (orderedTeams.Select(team => team.TeamId).Distinct().Count()
            != orderedTeams.Length)
        {
            throw new ArgumentException("Frontline team IDs must be unique.", nameof(teams));
        }

        Definition = definition;
        _teams = orderedTeams.ToImmutableArray();
        Control = control;
        Tick = control.NextTick;
        _projectiles = projectiles?.OrderBy(projectile => projectile.Id).ToList() ?? [];
        _projectileView = _projectiles.AsReadOnly();
        NextProjectileId = nextProjectileId;
    }

    public ResolvedMatchDefinition Definition { get; }
    public IReadOnlyList<FrontlineTeamState> Teams => _teams;
    public IReadOnlyList<FrontlineProjectileState> Projectiles => _projectileView;
    public FrontlineControlState Control { get; internal set; }
    /// <summary>The next tick to execute.</summary>
    public int Tick { get; internal set; }
    public long NextProjectileId { get; internal set; }
    public FrontlineMatchResult? Result { get; internal set; }
    public bool IsCompleted => Result is not null;

    internal List<FrontlineProjectileState> MutableProjectiles => _projectiles;

    public FrontlineTeamState GetTeam(int teamId) =>
        _teams.FirstOrDefault(team => team.TeamId == teamId)
        ?? throw new KeyNotFoundException($"Frontline does not contain team {teamId}.");

    public FrontlineUnitState GetUnit(int teamId, int unitId) =>
        GetTeam(teamId).GetUnit(unitId);

    public FrontlineLifeState GetActiveLife(FrontlineActorId actorId)
    {
        FrontlineLifeState? life = GetUnit(actorId.TeamId, actorId.UnitId).ActiveLife;
        return life?.ActorId == actorId
            ? life
            : throw new KeyNotFoundException(
                $"Frontline actor {actorId} is not an active life.");
    }
}
