namespace BotArena.Engine;

/// <summary>
/// Closed mode-specific terminal facts carried by the neutral result
/// envelope. New modes add a typed variant rather than an object bag.
/// </summary>
public abstract record GenericActorMatchModeResult
{
    private GenericActorMatchModeResult()
    {
    }

    public sealed record Deathmatch : GenericActorMatchModeResult
    {
        public Deathmatch(
            GenericDeathmatchEndReason reason,
            DeathmatchScoreState scores)
        {
            if (!Enum.IsDefined(reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            ArgumentNullException.ThrowIfNull(scores);

            Reason = reason;
            Scores = scores;
        }

        public GenericDeathmatchEndReason Reason { get; }
        public DeathmatchScoreState Scores { get; }
    }

    public sealed record Frontline : GenericActorMatchModeResult
    {
        public Frontline(
            GenericFrontlineEndReason reason,
            GenericActorRuntimeObservation.ModeObservationState.Frontline
                control,
            FrontlineScoreState scores)
        {
            if (!Enum.IsDefined(reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(scores);
            if (!string.Equals(
                    control.ModeId,
                    FrontlineGameModeDefinition.Id,
                    StringComparison.Ordinal)
                || control.ActivePositionIndex < 0
                || control.ClaimingTeamId is < 0
                || control.CaptureProgress < 0
                || control.DecayTicksElapsed < 0
                || control.ControlResumesAtTick < 0
                || (control.ClaimingTeamId is null)
                    != (control.CaptureProgress == 0))
            {
                throw new ArgumentException(
                    "Frontline terminal control must be a valid public Frontline state.",
                    nameof(control));
            }

            Reason = reason;
            Control = control;
            Scores = scores;
        }

        public GenericFrontlineEndReason Reason { get; }
        public GenericActorRuntimeObservation.ModeObservationState.Frontline
            Control
        { get; }
        public FrontlineScoreState Scores { get; }
    }
}
