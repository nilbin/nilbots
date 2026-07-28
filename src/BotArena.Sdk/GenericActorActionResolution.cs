using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// The prior generic action's accepted and authoritative forms. Blocked and
/// rejected actions retain their valid request; only a fault may synthesize a
/// replacement action in the common host.
/// </summary>
public sealed record GenericActorActionResolution
{
    /// <summary>Creates the authoritative result of a prior action.</summary>
    /// <param name="submittedAction">
    /// The bot's submitted action, or <see langword="null"/> when no decision
    /// was recoverable from a runtime fault.
    /// </param>
    /// <param name="acceptedAction">
    /// The submitted action accepted by admission, or the host's synthetic
    /// fallback action for a fault.
    /// </param>
    /// <param name="validatedAction">
    /// The action passed to joint resolution. It equals
    /// <paramref name="acceptedAction"/> in schema 2.
    /// </param>
    /// <param name="outcome">Authoritative gameplay or runtime outcome.</param>
    /// <param name="runtimeFault">
    /// Stable fault evidence for <see cref="ActionOutcome.Faulted"/>;
    /// otherwise <see langword="null"/>.
    /// </param>
    public GenericActorActionResolution(
        ResolvedAction? submittedAction,
        ResolvedAction acceptedAction,
        ResolvedAction validatedAction,
        ActionOutcome outcome,
        GenericActorRuntimeFaultContext? runtimeFault)
    {
        ArgumentNullException.ThrowIfNull(acceptedAction);
        ArgumentNullException.ThrowIfNull(validatedAction);
        outcome = GenericActorDynamicValueRules.EnumValue(
            outcome,
            nameof(outcome));
        if ((outcome == ActionOutcome.Faulted) != (runtimeFault is not null))
        {
            throw new ArgumentException(
                "Faulted outcomes require fault context, and other outcomes forbid it.",
                nameof(runtimeFault));
        }
        if (outcome != ActionOutcome.Faulted
            && (submittedAction is null
                || !SameAction(submittedAction, acceptedAction)
                || !SameAction(acceptedAction, validatedAction)))
        {
            throw new ArgumentException(
                "Successful, blocked, and rejected outcomes must retain one submitted action unchanged.",
                nameof(submittedAction));
        }
        if (outcome == ActionOutcome.Faulted
            && !SameAction(acceptedAction, validatedAction))
        {
            throw new ArgumentException(
                "A faulted outcome must name one consistent synthetic fallback action.",
                nameof(validatedAction));
        }

        SubmittedAction = submittedAction;
        AcceptedAction = acceptedAction;
        ValidatedAction = validatedAction;
        Outcome = outcome;
        RuntimeFault = runtimeFault;
    }

    /// <summary>
    /// The bot's submitted action, or <see langword="null"/> only when a fault
    /// prevented the host from recovering one.
    /// </summary>
    public ResolvedAction? SubmittedAction { get; }
    /// <summary>
    /// The accepted request, or the synthetic fallback used after a fault.
    /// </summary>
    public ResolvedAction AcceptedAction { get; }
    /// <summary>The action supplied to authoritative joint resolution.</summary>
    public ResolvedAction ValidatedAction { get; }
    /// <summary>The authoritative outcome reported to the life next tick.</summary>
    public ActionOutcome Outcome { get; }
    /// <summary>
    /// Stable participant fault evidence when <see cref="Outcome"/> is
    /// <see cref="ActionOutcome.Faulted"/>; otherwise <see langword="null"/>.
    /// </summary>
    public GenericActorRuntimeFaultContext? RuntimeFault { get; }

    private static bool SameAction(
        ResolvedAction left,
        ResolvedAction right) =>
        string.Equals(
            left.ActionId,
            right.ActionId,
            StringComparison.Ordinal)
        && left.ActionCode == right.ActionCode
        && left.Arguments.SequenceEqual(right.Arguments);

    /// <summary>A normalized action catalog selection.</summary>
    public sealed record ResolvedAction
    {
        /// <summary>Creates a normalized action.</summary>
        /// <param name="actionId">Stable action catalog identifier.</param>
        /// <param name="actionCode">Compact code paired with the identifier.</param>
        /// <param name="arguments">At most one value per parameter kind.</param>
        public ResolvedAction(
            string actionId,
            int actionCode,
            IEnumerable<GenericActorActionArgument> arguments)
        {
            ActionId = GenericActorDynamicValueRules.SemanticId(
                actionId,
                nameof(actionId));
            if (actionCode < 0)
                throw new ArgumentOutOfRangeException(nameof(actionCode));

            ImmutableArray<GenericActorActionArgument> snapshot =
                GenericActorDynamicValueRules.Snapshot(
                    arguments,
                    nameof(arguments));
            GenericActorDynamicValueRules.EnsureUnique(
                snapshot.Select(argument => argument.Kind),
                nameof(arguments));

            ActionCode = actionCode;
            Arguments = snapshot
                .OrderBy(argument => argument.Kind)
                .ToImmutableArray();
        }

        /// <summary>Stable action catalog identifier.</summary>
        public string ActionId { get; }
        /// <summary>Compact action code paired with <see cref="ActionId"/>.</summary>
        public int ActionCode { get; }
        /// <summary>Canonical typed arguments, ordered by parameter kind.</summary>
        public ImmutableArray<GenericActorActionArgument> Arguments { get; }

        /// <summary>Normalizes a bot decision into a resolved-action value.</summary>
        /// <param name="decision">The decision to copy.</param>
        /// <returns>A normalized action with the same selectors and arguments.</returns>
        public static ResolvedAction FromDecision(
            GenericActorDecision decision)
        {
            ArgumentNullException.ThrowIfNull(decision);
            return new ResolvedAction(
                decision.ActionId,
                decision.ActionCode,
                decision.Arguments);
        }
    }

    /// <summary>Authoritative result category for a submitted tick action.</summary>
    public enum ActionOutcome
    {
        /// <summary>The action applied its declared gameplay effect.</summary>
        Success = 0,
        /// <summary>
        /// The action was structurally valid but authoritative state or the
        /// simultaneous joint step prevented its effect.
        /// </summary>
        Blocked = 1,
        /// <summary>
        /// The action was structurally valid but disallowed by the current
        /// form or another declared admission rule.
        /// </summary>
        Rejected = 2,
        /// <summary>
        /// Runtime execution or decision validation faulted; the host used a
        /// declared synthetic fallback and incremented the participant counter.
        /// </summary>
        Faulted = 3,
    }
}
