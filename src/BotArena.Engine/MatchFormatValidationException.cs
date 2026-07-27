namespace BotArena.Engine;

/// <summary>A topology cannot represent the requested vNext match format.</summary>
public sealed class MatchFormatValidationException(IReadOnlyList<string> errors)
    : ArgumentException(
        "Topology is incompatible with the match format: " +
        string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } =
        Array.AsReadOnly(errors.ToArray());
}
