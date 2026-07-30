using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>One action's exact frozen legality mask for the current tick.</summary>
public sealed record GenericActorRuntimeActionLegality(
    string ActionId,
    int ActionCode,
    bool AllowedByForm,
    bool Available,
    ImmutableArray<GenericActorRuntimeActionLegality.ArgumentConstraint> Constraints)
{
    public abstract record ArgumentConstraint
    {
        private ArgumentConstraint()
        {
        }

        public abstract ActorActionParameterKind Kind { get; }

        public sealed record ShotProgramConstraint(
            bool Allowed) : ArgumentConstraint
        {
            public override ActorActionParameterKind Kind =>
                ActorActionParameterKind.ShotProgram;
        }

        public sealed record DirectionConstraint(
            ImmutableArray<Direction> AllowedValues) : ArgumentConstraint
        {
            public override ActorActionParameterKind Kind =>
                ActorActionParameterKind.Direction;
        }

        public sealed record UnitTargetConstraint(
            ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget> AllowedValues)
            : ArgumentConstraint
        {
            public override ActorActionParameterKind Kind =>
                ActorActionParameterKind.UnitTarget;
        }

        public sealed record FormTargetConstraint(
            ImmutableArray<string> AllowedFormIds) : ArgumentConstraint
        {
            public override ActorActionParameterKind Kind =>
                ActorActionParameterKind.FormTarget;
        }

        public sealed record ProjectileHeadingConstraint(
            ImmutableArray<ProjectileHeading> AllowedValues)
            : ArgumentConstraint
        {
            public override ActorActionParameterKind Kind =>
                ActorActionParameterKind.ProjectileHeading;
        }

        /// <summary>
        /// The upgrade tracks the acting body's team may buy the next tier of
        /// this tick: affordable out of the standing bank, below their own
        /// maximum tier, and inside the team's total-tier cap. Affordability
        /// lives in the mask rather than in the bot, which is the cheapest
        /// possible author surface for a new verb.
        /// </summary>
        public sealed record UpgradeTrackConstraint(
            ImmutableArray<string> AllowedTrackIds) : ArgumentConstraint
        {
            public override ActorActionParameterKind Kind =>
                ActorActionParameterKind.UpgradeTrack;
        }
    }
}
