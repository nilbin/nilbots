using BotArena.App.Jobs;

namespace BotArena.App.Competition;

/// <summary>
/// Exact hosted generic-definition catalog. Execution resolves the immutable
/// playlist key and version through this registry, so adding another hosted
/// generic mode is registration rather than an executor branch.
/// </summary>
public sealed class HostedGenericMatchDefinitionRegistry
{
    private readonly IReadOnlyDictionary<
        (string PlaylistKey, int Version),
        IHostedGenericMatchDefinition> definitions;
    private readonly IReadOnlyDictionary<
        string,
        IHostedGenericMatchDefinition> definitionsByJobType;

    public HostedGenericMatchDefinitionRegistry(
        IEnumerable<IHostedGenericMatchDefinition> definitions)
    {
        var indexed = new Dictionary<
            (string PlaylistKey, int Version),
            IHostedGenericMatchDefinition>();
        var indexedByJobType = new Dictionary<
            string,
            IHostedGenericMatchDefinition>(StringComparer.Ordinal);
        foreach (IHostedGenericMatchDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentException.ThrowIfNullOrWhiteSpace(
                definition.PlaylistKey);
            if (definition.Version <= 0)
            {
                throw new InvalidOperationException(
                    $"Hosted generic definition '{definition.PlaylistKey}' " +
                    "must have a positive version.");
            }
            if (!string.Equals(
                    definition.ExecutionPolicyId,
                    PlaylistExecutionPolicyIds.GenericActor,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Hosted generic definition '{definition.PlaylistKey}' " +
                    $"v{definition.Version} uses execution policy " +
                    $"'{definition.ExecutionPolicyId}', expected " +
                    $"'{PlaylistExecutionPolicyIds.GenericActor}'.");
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(
                definition.ExecutionEngineVersion);

            var key = (definition.PlaylistKey, definition.Version);
            if (!indexed.TryAdd(key, definition))
            {
                throw new InvalidOperationException(
                    $"Hosted generic definition '{definition.PlaylistKey}' " +
                    $"v{definition.Version} is registered more than once.");
            }

            string jobType = GenericActorMatchJobType.ForPlaylist(
                definition.PlaylistKey,
                definition.Version);
            if (!indexedByJobType.TryAdd(jobType, definition))
            {
                throw new InvalidOperationException(
                    $"Hosted generic execution job type '{jobType}' is " +
                    "registered more than once.");
            }
        }
        this.definitions = indexed;
        definitionsByJobType = indexedByJobType;
        ExecutionJobTypes = Array.AsReadOnly(
            indexedByJobType.Keys
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// Exact queue capabilities supported by this worker binary.
    /// </summary>
    public IReadOnlyList<string> ExecutionJobTypes { get; }

    public IHostedGenericMatchDefinition Resolve(
        string playlistKey,
        int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistKey);
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));

        return definitions.TryGetValue(
            (playlistKey, version),
            out IHostedGenericMatchDefinition? definition)
            ? definition
            : throw new InvalidOperationException(
                $"No hosted generic match definition is registered for " +
                $"playlist '{playlistKey}' v{version}.");
    }

    public IHostedGenericMatchDefinition ResolveJobType(string jobType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        return definitionsByJobType.TryGetValue(
            jobType,
            out IHostedGenericMatchDefinition? definition)
            ? definition
            : throw new InvalidOperationException(
                $"No hosted generic match definition is registered for " +
                $"execution job type '{jobType}'.");
    }

    public bool SupportsJobType(string jobType) =>
        !string.IsNullOrEmpty(jobType) &&
        definitionsByJobType.ContainsKey(jobType);
}
