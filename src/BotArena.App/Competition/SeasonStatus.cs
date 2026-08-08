namespace BotArena.App.Competition;

/// <summary>
/// Operational state of a competition window. Closing is deliberately separate
/// from Closed: pairing has stopped, but admitted matches may still settle.
/// </summary>
public enum SeasonStatus
{
    Draft = 0,
    Scheduled = 1,
    Open = 2,
    Closing = 3,
    Closed = 4,
}
