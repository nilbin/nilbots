using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One command a mind wrote onto one of its bodies, as it rides the wire.
///
/// <para>It is keyed by <c>(unitId, lifeId)</c> WITHIN the commanding
/// participant — there is no team ID, because a mind can only command what it
/// owns and that boundary should be unspellable rather than merely checked.
/// Authors do not construct these: they call
/// <see cref="MindBody.Command(string, int, GenericActorActionArgument[])"/>
/// and the guest harvests the result.</para>
///
/// <para>Host admission of a command reuses the existing grammar verbatim. A
/// command naming a body this participant does not own, or one that is not live
/// this tick, is <c>Rejected</c> — recorded, replayable and harmless, because
/// commanding a body that died this tick is an easy and forgivable mistake once
/// memory persists. Two commands for the same body, an unknown action or a
/// malformed argument is <c>Faulted</c>, and a fault is participant-scoped.</para>
/// </summary>
public sealed record MindCommand
{
    /// <summary>Creates one harvested body command.</summary>
    /// <param name="unitId">Stable team-local unit slot inside this participant.</param>
    /// <param name="lifeId">The exact life being commanded.</param>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <param name="actionCode">Compact action code paired with the identifier.</param>
    /// <param name="arguments">At most one typed value per parameter kind.</param>
    /// <param name="roleTag">
    /// Optional public label for this body. Non-authoritative; at most 24 UTF-8
    /// bytes; the empty string clears it and <see langword="null"/> leaves the
    /// current tag unchanged.
    /// </param>
    /// <param name="debugMessage">
    /// Optional bounded diagnostic text. It cannot affect simulation state.
    /// </param>
    public MindCommand(
        int unitId,
        int lifeId,
        string actionId,
        int actionCode,
        IEnumerable<GenericActorActionArgument> arguments,
        string? roleTag = null,
        string? debugMessage = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unitId);
        ArgumentOutOfRangeException.ThrowIfNegative(lifeId);
        ArgumentOutOfRangeException.ThrowIfNegative(actionCode);

        ImmutableArray<GenericActorActionArgument> snapshot =
            GenericActorDynamicValueRules.Snapshot(
                arguments,
                nameof(arguments));
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(argument => argument.Kind),
            nameof(arguments));

        UnitId = unitId;
        LifeId = lifeId;
        ActionId = GenericActorDynamicValueRules.SemanticId(
            actionId,
            nameof(actionId));
        ActionCode = actionCode;
        Arguments = snapshot
            .OrderBy(argument => argument.Kind)
            .ToImmutableArray();
        RoleTag = roleTag is null
            ? null
            : MindValueRules.RoleTag(roleTag, nameof(roleTag));
        DebugMessage = debugMessage is null
            ? null
            : GenericActorDynamicValueRules.Text(
                debugMessage,
                4096,
                nameof(debugMessage));
    }

    /// <summary>Stable team-local unit slot inside the commanding participant.</summary>
    public int UnitId { get; }

    /// <summary>The exact life being commanded.</summary>
    public int LifeId { get; }

    /// <summary>Stable action catalog identifier.</summary>
    public string ActionId { get; }

    /// <summary>Compact action code paired with <see cref="ActionId"/>.</summary>
    public int ActionCode { get; }

    /// <summary>Canonical typed arguments, ordered by parameter kind.</summary>
    public ImmutableArray<GenericActorActionArgument> Arguments { get; }

    /// <summary>
    /// Public role label, or <see langword="null"/> to leave the body's current
    /// tag unchanged. The engine never branches on it.
    /// </summary>
    public string? RoleTag { get; }

    /// <summary>Optional bounded diagnostic text; never an action parameter.</summary>
    public string? DebugMessage { get; }
}
