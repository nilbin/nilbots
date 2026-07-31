namespace BotArena.Engine;

/// <summary>Closed argument union for one generic actor action.</summary>
public abstract record GenericActorRuntimeActionArgument
{
    private GenericActorRuntimeActionArgument()
    {
    }

    public abstract ActorActionParameterKind Kind { get; }

    public sealed record ShotProgramArgument(
        ShotProgram Value) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.ShotProgram;
    }

    public sealed record DirectionArgument(
        Direction Value) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.Direction;
    }

    public sealed record UnitTargetArgument(
        UnitTarget Value) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.UnitTarget;
    }

    public sealed record FormTargetArgument(
        string FormId) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.FormTarget;
    }

    public sealed record ProjectileHeadingArgument(
        ProjectileHeading Value) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.ProjectileHeading;
    }

    public sealed record UpgradeTrackArgument(
        string TrackId) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.UpgradeTrack;
    }

    public sealed record PositionTargetArgument(
        Position Value) : GenericActorRuntimeActionArgument
    {
        public override ActorActionParameterKind Kind =>
            ActorActionParameterKind.PositionTarget;
    }

    public readonly record struct UnitTarget(int TeamId, int UnitId);
}
