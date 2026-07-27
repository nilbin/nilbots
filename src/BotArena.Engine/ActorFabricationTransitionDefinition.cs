using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Closed source-preserving entity-creation family. Unlike replication, the
/// fabricating life survives while a fresh child life starts in another
/// bounded stable slot.
/// </summary>
public abstract record ActorFabricationTransitionDefinition
{
    internal ActorFabricationTransitionDefinition(
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
                "Fabrication source forms must be non-empty, non-blank, and unique.",
                nameof(sourceFormIds));
        }

        TransitionId = transitionId;
        ActionId = actionId;
        SourceFormIds = sources
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public abstract FabricationTransitionKind Kind { get; }
    public string TransitionId { get; }
    public string ActionId { get; }
    public ImmutableArray<string> SourceFormIds { get; }

    public enum FabricationTransitionKind
    {
        BoundedChild = 0,
    }
}
