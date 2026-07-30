using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One contract action's exact frozen pre-tick legality mask. Simultaneous
/// conflicts may still block an action reported as individually available.
/// </summary>
public sealed record GenericActorActionLegality
{
    /// <summary>Creates one action's pre-tick legality entry.</summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <param name="actionCode">Compact action code paired with the identifier.</param>
    /// <param name="allowedByForm">
    /// Whether the observing life's current form includes the action in its
    /// allowed-action mask.
    /// </param>
    /// <param name="available">
    /// Whether the action is individually available in this pre-tick state.
    /// Joint-step conflicts can still block it after submission.
    /// </param>
    /// <param name="constraints">
    /// Per-parameter legal values for this tick. An empty collection means the
    /// action takes no constrained parameters.
    /// </param>
    public GenericActorActionLegality(
        string actionId,
        int actionCode,
        bool allowedByForm,
        bool available,
        IEnumerable<ArgumentConstraint> constraints)
    {
        ActionId = GenericActorDynamicValueRules.SemanticId(
            actionId,
            nameof(actionId));
        if (actionCode < 0)
            throw new ArgumentOutOfRangeException(nameof(actionCode));
        if (available && !allowedByForm)
        {
            throw new ArgumentException(
                "An action cannot be available when its current form disallows it.",
                nameof(available));
        }

        ImmutableArray<ArgumentConstraint> snapshot =
            GenericActorDynamicValueRules.Snapshot(
                constraints,
                nameof(constraints));
        GenericActorDynamicValueRules.EnsureUnique(
            snapshot.Select(constraint => constraint.Kind),
            nameof(constraints));

        ActionCode = actionCode;
        AllowedByForm = allowedByForm;
        Available = available;
        Constraints = snapshot
            .OrderBy(constraint => constraint.Kind)
            .ToImmutableArray();
    }

    /// <summary>Stable action catalog identifier.</summary>
    public string ActionId { get; }
    /// <summary>Compact action code paired with <see cref="ActionId"/>.</summary>
    public int ActionCode { get; }
    /// <summary>Whether the current form permits this action kind.</summary>
    public bool AllowedByForm { get; }
    /// <summary>
    /// Whether the action is individually available before the joint step.
    /// This is not a guarantee that the submitted action will succeed.
    /// </summary>
    public bool Available { get; }
    /// <summary>
    /// Canonical per-parameter legal values, ordered by parameter kind.
    /// </summary>
    public ImmutableArray<ArgumentConstraint> Constraints { get; }

    /// <summary>Closed union of per-tick action-argument constraints.</summary>
    public abstract record ArgumentConstraint
    {
        private ArgumentConstraint()
        {
        }

        /// <summary>Catalog parameter kind constrained by this entry.</summary>
        public abstract GenericActorRulesContract.ActionParameterKind Kind
        {
            get;
        }

        /// <summary>States whether a shot-program payload may be supplied.</summary>
        public sealed record ShotProgramConstraint
            : ArgumentConstraint
        {
            /// <summary>Creates a shot-program constraint.</summary>
            /// <param name="allowed">Whether a shot-program value is legal.</param>
            public ShotProgramConstraint(bool allowed)
            {
                Allowed = allowed;
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.ActionParameterKind Kind =>
                GenericActorRulesContract.ActionParameterKind.ShotProgram;
            /// <summary>Whether a shot-program value is legal.</summary>
            public bool Allowed { get; }
        }

        /// <summary>Enumerates legal absolute map directions.</summary>
        public sealed record DirectionConstraint : ArgumentConstraint
        {
            /// <summary>Creates a direction constraint.</summary>
            /// <param name="allowedValues">
            /// Complete set of legal directions; empty means no direction is
            /// currently legal.
            /// </param>
            public DirectionConstraint(IEnumerable<Direction> allowedValues)
            {
                ImmutableArray<Direction> snapshot =
                    GenericActorDynamicValueRules.SnapshotValues(
                        allowedValues,
                        nameof(allowedValues));
                foreach (Direction value in snapshot)
                {
                    GenericActorDynamicValueRules.EnumValue(
                        value,
                        nameof(allowedValues));
                }
                GenericActorDynamicValueRules.EnsureUnique(
                    snapshot,
                    nameof(allowedValues));
                AllowedValues = snapshot.Order().ToImmutableArray();
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.ActionParameterKind Kind =>
                GenericActorRulesContract.ActionParameterKind.Direction;
            /// <summary>Complete canonical set of legal directions.</summary>
            public ImmutableArray<Direction> AllowedValues { get; }
        }

        /// <summary>Enumerates legal stable team/unit targets.</summary>
        public sealed record UnitTargetConstraint : ArgumentConstraint
        {
            /// <summary>Creates a unit-target constraint.</summary>
            /// <param name="allowedValues">
            /// Complete set of legal stable slots; empty means no unit target
            /// is currently legal.
            /// </param>
            public UnitTargetConstraint(
                IEnumerable<GenericActorActionArgument.UnitTarget>
                    allowedValues)
            {
                ImmutableArray<GenericActorActionArgument.UnitTarget> snapshot =
                    GenericActorDynamicValueRules.SnapshotValues(
                        allowedValues,
                        nameof(allowedValues));
                GenericActorDynamicValueRules.EnsureUnique(
                    snapshot,
                    nameof(allowedValues));
                AllowedValues = snapshot
                    .OrderBy(target => target.TeamId)
                    .ThenBy(target => target.UnitId)
                    .ToImmutableArray();
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.ActionParameterKind Kind =>
                GenericActorRulesContract.ActionParameterKind.UnitTarget;
            /// <summary>Complete canonical set of legal stable slots.</summary>
            public ImmutableArray<GenericActorActionArgument.UnitTarget>
                AllowedValues { get; }
        }

        /// <summary>Enumerates legal target forms.</summary>
        public sealed record FormTargetConstraint : ArgumentConstraint
        {
            /// <summary>Creates a form-target constraint.</summary>
            /// <param name="allowedFormIds">
            /// Complete set of legal form IDs; empty means no form target is
            /// currently legal.
            /// </param>
            public FormTargetConstraint(IEnumerable<string> allowedFormIds)
            {
                ArgumentNullException.ThrowIfNull(allowedFormIds);
                string[] snapshot = allowedFormIds
                    .Select(formId =>
                        GenericActorDynamicValueRules.SemanticId(
                            formId,
                            nameof(allowedFormIds)))
                    .ToArray();
                if (snapshot.Length > ActorWireProtocol.MaxCollectionCount)
                {
                    throw new ArgumentException(
                        "Collection exceeds the actor wire item limit.",
                        nameof(allowedFormIds));
                }
                GenericActorDynamicValueRules.EnsureUnique(
                    snapshot,
                    nameof(allowedFormIds));
                AllowedFormIds = snapshot
                    .Order(StringComparer.Ordinal)
                    .ToImmutableArray();
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.ActionParameterKind Kind =>
                GenericActorRulesContract.ActionParameterKind.FormTarget;
            /// <summary>Complete ordinal-sorted set of legal form IDs.</summary>
            public ImmutableArray<string> AllowedFormIds { get; }
        }

        /// <summary>Enumerates legal absolute projectile headings.</summary>
        public sealed record ProjectileHeadingConstraint
            : ArgumentConstraint
        {
            /// <summary>Creates a projectile-heading constraint.</summary>
            /// <param name="allowedValues">
            /// Complete set of legal headings; empty means no heading is
            /// currently legal.
            /// </param>
            public ProjectileHeadingConstraint(
                IEnumerable<ProjectileHeading> allowedValues)
            {
                ImmutableArray<ProjectileHeading> snapshot =
                    GenericActorDynamicValueRules.SnapshotValues(
                        allowedValues,
                        nameof(allowedValues));
                foreach (ProjectileHeading value in snapshot)
                {
                    GenericActorDynamicValueRules.EnumValue(
                        value,
                        nameof(allowedValues));
                }
                GenericActorDynamicValueRules.EnsureUnique(
                    snapshot,
                    nameof(allowedValues));
                AllowedValues = snapshot.Order().ToImmutableArray();
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.ActionParameterKind Kind =>
                GenericActorRulesContract.ActionParameterKind.ProjectileHeading;
            /// <summary>Complete canonical set of legal headings.</summary>
            public ImmutableArray<ProjectileHeading> AllowedValues { get; }
        }

        /// <summary>
        /// Enumerates the upgrade tracks this body's team may buy the next
        /// tier of RIGHT NOW: affordable out of the standing bank, below their
        /// own maximum tier, and inside the team's total-tier cap. Empty means
        /// no purchase is currently legal — which is also every contract that
        /// declares no store at all.
        /// </summary>
        public sealed record UpgradeTrackConstraint : ArgumentConstraint
        {
            /// <summary>Creates an upgrade-track constraint.</summary>
            /// <param name="allowedTrackIds">
            /// Complete set of legal track IDs; empty means none.
            /// </param>
            public UpgradeTrackConstraint(
                IEnumerable<string> allowedTrackIds)
            {
                ArgumentNullException.ThrowIfNull(allowedTrackIds);
                string[] snapshot = allowedTrackIds
                    .Select(trackId =>
                        GenericActorDynamicValueRules.SemanticId(
                            trackId,
                            nameof(allowedTrackIds)))
                    .ToArray();
                if (snapshot.Length > ActorWireProtocol.MaxCollectionCount)
                {
                    throw new ArgumentException(
                        "Collection exceeds the actor wire item limit.",
                        nameof(allowedTrackIds));
                }
                GenericActorDynamicValueRules.EnsureUnique(
                    snapshot,
                    nameof(allowedTrackIds));
                AllowedTrackIds = snapshot
                    .Order(StringComparer.Ordinal)
                    .ToImmutableArray();
            }

            /// <inheritdoc />
            public override GenericActorRulesContract.ActionParameterKind Kind =>
                GenericActorRulesContract.ActionParameterKind.UpgradeTrack;
            /// <summary>Complete ordinal-sorted set of legal track IDs.</summary>
            public ImmutableArray<string> AllowedTrackIds { get; }
        }
    }
}
