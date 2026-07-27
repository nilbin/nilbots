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
}
