using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One schema-2 generic actor reply. Both stable selectors are required and are
/// checked against the resolved match contract by the host.
/// </summary>
public sealed record GenericActorDecision
{
    /// <summary>Creates one requested catalog action.</summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <param name="actionCode">Compact action code paired with <paramref name="actionId"/>.</param>
    /// <param name="arguments">
    /// At most one typed value per parameter kind; use an empty collection for
    /// an action with no parameters.
    /// </param>
    /// <param name="debugMessage">
    /// Optional bounded diagnostic text. It cannot affect simulation state.
    /// </param>
    public GenericActorDecision(
        string actionId,
        int actionCode,
        IEnumerable<GenericActorActionArgument> arguments,
        string? debugMessage = null)
    {
        ActionId = GenericActorDynamicValueRules.SemanticId(
            actionId,
            nameof(actionId));
        if (actionCode < 0)
            throw new ArgumentOutOfRangeException(nameof(actionCode));

        ImmutableArray<GenericActorActionArgument> snapshot =
            GenericActorDynamicValueRules.Snapshot(arguments, nameof(arguments));
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(argument => argument.Kind),
            nameof(arguments));

        ActionCode = actionCode;
        Arguments = snapshot
            .OrderBy(argument => argument.Kind)
            .ToImmutableArray();
        DebugMessage = debugMessage is null
            ? null
            : GenericActorDynamicValueRules.Text(
                debugMessage,
                4096,
                nameof(debugMessage));
    }

    /// <summary>Stable action catalog identifier.</summary>
    public string ActionId { get; }
    /// <summary>Compact action code paired with <see cref="ActionId"/>.</summary>
    public int ActionCode { get; }
    /// <summary>Canonical typed arguments, ordered by parameter kind.</summary>
    public ImmutableArray<GenericActorActionArgument> Arguments { get; }
    /// <summary>
    /// Optional bounded diagnostic text; it is non-authoritative and is not
    /// an action parameter.
    /// </summary>
    public string? DebugMessage { get; }

    /// <summary>Creates a parameterless catalog action.</summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <param name="actionCode">Compact action code paired with the identifier.</param>
    /// <param name="debugMessage">Optional bounded diagnostic text.</param>
    /// <returns>A decision with an empty argument collection.</returns>
    public static GenericActorDecision WithoutArguments(
        string actionId,
        int actionCode,
        string? debugMessage = null) =>
        new(actionId, actionCode, [], debugMessage);
}
