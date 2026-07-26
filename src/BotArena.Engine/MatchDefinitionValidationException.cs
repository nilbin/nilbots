namespace BotArena.Engine;

/// <summary>Raised before tick zero when rules, map, and topology do not agree.</summary>
public sealed class MatchDefinitionValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid match definition: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
