namespace BotArena.App.Jobs;

public enum JobFailureOutcome
{
    RetryScheduled,
    TerminalFailure,
    LeaseLost,
}
