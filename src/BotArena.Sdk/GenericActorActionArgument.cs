namespace BotArena.Sdk;

/// <summary>
/// Closed schema-2 argument union for generic actor decisions. The discriminator
/// values intentionally match the generation-3 action-parameter catalog.
/// </summary>
public abstract record GenericActorActionArgument
{
    private GenericActorActionArgument()
    {
    }

    /// <summary>Catalog parameter kind represented by this value.</summary>
    public abstract GenericActorRulesContract.ActionParameterKind Kind { get; }

    /// <summary>Supplies a complete projectile shot program.</summary>
    public sealed record ShotProgramArgument : GenericActorActionArgument
    {
        /// <summary>Creates a shot-program argument.</summary>
        /// <param name="value">The program to execute.</param>
        public ShotProgramArgument(ShotProgram value)
        {
            Value = value;
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.ShotProgram;
        /// <summary>The program to execute.</summary>
        public ShotProgram Value { get; }
    }

    /// <summary>Supplies an absolute map direction.</summary>
    public sealed record DirectionArgument : GenericActorActionArgument
    {
        /// <summary>Creates a direction argument.</summary>
        /// <param name="value">The requested map direction.</param>
        public DirectionArgument(Direction value)
        {
            Value = GenericActorDynamicValueRules.EnumValue(
                value,
                nameof(value));
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.Direction;
        /// <summary>The requested map direction.</summary>
        public Direction Value { get; }
    }

    /// <summary>Targets one stable team/unit slot, independent of its current life.</summary>
    public sealed record UnitTargetArgument : GenericActorActionArgument
    {
        /// <summary>Creates a stable-unit target argument.</summary>
        /// <param name="value">The team/unit slot to target.</param>
        public UnitTargetArgument(UnitTarget value)
        {
            Value = value;
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.UnitTarget;
        /// <summary>The stable team/unit slot to target.</summary>
        public UnitTarget Value { get; }
    }

    /// <summary>Targets a form from the frozen form catalog.</summary>
    public sealed record FormTargetArgument : GenericActorActionArgument
    {
        /// <summary>Creates a form target argument.</summary>
        /// <param name="formId">Stable form catalog identifier.</param>
        public FormTargetArgument(string formId)
        {
            FormId = GenericActorDynamicValueRules.SemanticId(
                formId,
                nameof(formId));
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.FormTarget;
        /// <summary>Stable form catalog identifier.</summary>
        public string FormId { get; }
    }

    /// <summary>Supplies an absolute projectile heading sector.</summary>
    public sealed record ProjectileHeadingArgument
        : GenericActorActionArgument
    {
        /// <summary>Creates a projectile-heading argument.</summary>
        /// <param name="value">The requested heading sector.</param>
        public ProjectileHeadingArgument(ProjectileHeading value)
        {
            Value = GenericActorDynamicValueRules.EnumValue(
                value,
                nameof(value));
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.ProjectileHeading;
        /// <summary>The requested projectile heading sector.</summary>
        public ProjectileHeading Value { get; }
    }

    /// <summary>Names one declared upgrade track of a mode's store.</summary>
    public sealed record UpgradeTrackArgument : GenericActorActionArgument
    {
        /// <summary>Creates an upgrade-track argument.</summary>
        /// <param name="trackId">
        /// The declared track ID. Read the legal ones off this tick's
        /// <c>UpgradeTrackConstraint</c>: a track is offered only when the
        /// team's bank covers its next tier and no cap forbids it, so a bot
        /// that reads its mask never has to price the ladder itself.
        /// </param>
        public UpgradeTrackArgument(string trackId)
        {
            TrackId = GenericActorDynamicValueRules.SemanticId(
                trackId,
                nameof(trackId));
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.UpgradeTrack;
        /// <summary>The requested declared track ID.</summary>
        public string TrackId { get; }
    }

    /// <summary>Targets one absolute map tile.</summary>
    public sealed record PositionTargetArgument : GenericActorActionArgument
    {
        /// <summary>Creates a map-tile target argument.</summary>
        public PositionTargetArgument(Position value)
        {
            if (value.X < 0 || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        /// <inheritdoc />
        public override GenericActorRulesContract.ActionParameterKind Kind =>
            GenericActorRulesContract.ActionParameterKind.PositionTarget;
        /// <summary>The requested absolute tile.</summary>
        public Position Value { get; }
    }

    /// <summary>
    /// Stable target slot. It does not identify a particular generation or life.
    /// </summary>
    public readonly record struct UnitTarget
    {
        /// <summary>Creates a stable-unit target.</summary>
        /// <param name="teamId">Zero-based scoring-team identifier.</param>
        /// <param name="unitId">Zero-based unit-slot identifier within the team.</param>
        public UnitTarget(int teamId, int unitId)
        {
            if (teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (unitId < 0)
                throw new ArgumentOutOfRangeException(nameof(unitId));
            TeamId = teamId;
            UnitId = unitId;
        }

        /// <summary>Zero-based scoring-team identifier.</summary>
        public int TeamId { get; }
        /// <summary>Zero-based unit-slot identifier within the team.</summary>
        public int UnitId { get; }
    }

}
