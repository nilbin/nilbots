using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Closed vNext one-to-many transition family. The source life retires and
/// every descendant receives a fresh runtime life.
/// </summary>
public abstract record ActorReplicationTransitionDefinition
{
    internal ActorReplicationTransitionDefinition(
        string transitionId,
        string actionId,
        IReadOnlyCollection<string> sourceFormIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(sourceFormIds);

        string[] sources = [.. sourceFormIds];
        if (sources.Length == 0
            || sources.Any(string.IsNullOrWhiteSpace)
            || sources.Distinct(StringComparer.Ordinal).Count()
                != sources.Length)
        {
            throw new ArgumentException(
                "Replication source forms must be non-empty, non-blank, and unique.",
                nameof(sourceFormIds));
        }

        TransitionId = transitionId;
        ActionId = actionId;
        SourceFormIds = sources
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public abstract ReplicationTransitionKind Kind { get; }
    public string TransitionId { get; }
    public string ActionId { get; }

    /// <summary>Canonical ordinal set of eligible source forms.</summary>
    public ImmutableArray<string> SourceFormIds { get; }

    public enum ReplicationTransitionKind
    {
        Split = 0,
    }
}
