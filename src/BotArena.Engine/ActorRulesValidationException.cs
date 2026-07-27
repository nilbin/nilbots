namespace BotArena.Engine;

/// <summary>
/// Raised before serialization or tick zero when a resolved actor rules
/// catalog is incomplete, ambiguous, or internally inconsistent.
/// </summary>
public sealed class ActorRulesValidationException(IReadOnlyList<string> errors)
    : ArgumentException(
        "Invalid actor rules definition: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } =
        Array.AsReadOnly(errors.ToArray());
}
