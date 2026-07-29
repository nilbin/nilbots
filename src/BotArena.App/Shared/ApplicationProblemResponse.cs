namespace BotArena.App.Shared;

/// <summary>
/// The stable RFC 9457-compatible error envelope returned by application failures.
/// Codes are machine-readable; detail remains player-facing and may evolve.
/// </summary>
public sealed record ApplicationProblemResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Code,
    string TraceId,
    int? RetryAfterSeconds);
