using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Canonical vNext initial deployment. It binds every initial topology life to
/// one resolved spawn without changing historical map or topology contracts.
/// </summary>
public sealed record InitialDeploymentDefinition
{
    public InitialDeploymentDefinition(
        ImmutableArray<InitialSpawnDefinition> spawns,
        ImmutableArray<InitialLifeDeployment> lives)
    {
        if (spawns.IsDefaultOrEmpty || lives.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "Initial spawns and life deployments must be initialized and non-empty.");
        }
        if (spawns.Any(spawn => spawn is null)
            || lives.Any(life => life is null))
        {
            throw new ArgumentException(
                "Initial deployment collections cannot contain null entries.");
        }
        if (spawns.Select(spawn => spawn.SpawnId).Distinct(
                StringComparer.Ordinal).Count() != spawns.Length)
        {
            throw new ArgumentException("Initial spawn IDs must be unique.");
        }
        if (spawns.Select(spawn => spawn.Position).Distinct().Count()
            != spawns.Length)
        {
            throw new ArgumentException("Initial spawn positions must be unique.");
        }
        string[] declaredSpawnIds = spawns
            .Select(spawn => spawn.SpawnId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] assignedSpawnIds = lives
            .Select(life => life.SpawnId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!declaredSpawnIds.SequenceEqual(
                assignedSpawnIds,
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Every initial spawn must be assigned to exactly one initial life.");
        }
        if (lives
            .Select(life => (life.TeamId, life.UnitId))
            .Distinct()
            .Count() != lives.Length)
        {
            throw new ArgumentException(
                "At most one initial life may occupy each stable unit slot.");
        }

        Spawns = spawns
            .OrderBy(spawn => spawn.SpawnId, StringComparer.Ordinal)
            .ToImmutableArray();
        Lives = lives
            .OrderBy(life => life.TeamId)
            .ThenBy(life => life.UnitId)
            .ThenBy(life => life.LifeId)
            .ToImmutableArray();
    }

    public ImmutableArray<InitialSpawnDefinition> Spawns { get; }
    public ImmutableArray<InitialLifeDeployment> Lives { get; }

    public void ValidateTopology(PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (topology.InitialLives.IsDefault)
        {
            throw new ArgumentException(
                "Topology initial lives must be initialized.",
                nameof(topology));
        }

        var expected = topology.InitialLives
            .Select(life =>
                (life.TeamId, life.UnitId, life.LifeId, life.FormId))
            .Order()
            .ToArray();
        var actual = Lives
            .Select(life =>
                (life.TeamId, life.UnitId, life.LifeId, life.FormId))
            .Order()
            .ToArray();
        if (!expected.SequenceEqual(actual))
        {
            throw new ArgumentException(
                "Initial deployment lives must exactly match topology initial lives.",
                nameof(topology));
        }
    }
}
