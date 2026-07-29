using BotArena.App.Shared;
using BotArena.Engine;

namespace BotArena.App.Bots;

/// <summary>
/// Canonicalizes the bot-level class identity at every write boundary. The database
/// deliberately stores a nullable string rather than an enum or a fixed check
/// constraint: historical rows stay valid, while the authoritative catalog remains
/// the Engine's registered Frontline class definitions.
/// </summary>
public sealed class BotClassPolicy
{
    private static readonly IReadOnlyDictionary<string, string> KnownClasses =
        FrontlineLabsClassDefinition.All.ToDictionary(
            definition => definition.Id,
            definition => definition.Id,
            StringComparer.Ordinal);

    public ApplicationResult<string?> ValidateForCreation(string? classId) =>
        classId is null
            ? ApplicationResult<string?>.Success(null)
            : Validate(classId);

    public ApplicationResult<string?> ValidateForAssignment(string? classId) =>
        classId is null
            ? Invalid()
            : Validate(classId);

    private static ApplicationResult<string?> Validate(string classId)
    {
        string trimmed = classId.Trim();
        if (trimmed.Length is < 1 or > 64 ||
            !char.IsAsciiLetterOrDigit(trimmed[0]) ||
            trimmed.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            return Invalid();
        }
        string canonical = trimmed.ToLowerInvariant();

        return KnownClasses.TryGetValue(canonical, out string? known)
            ? ApplicationResult<string?>.Success(known)
            : ApplicationResult<string?>.Failure(new(
                ApplicationErrorCodes.BotClassUnknown,
                ApplicationErrorType.Validation,
                $"Unknown bot class '{canonical}'."));
    }

    private static ApplicationResult<string?> Invalid() =>
        ApplicationResult<string?>.Failure(new(
            ApplicationErrorCodes.BotClassIdInvalid,
            ApplicationErrorType.Validation,
            "Class ID must be 1-64 ASCII letters, digits, or hyphens and start " +
            "with a letter or digit."));
}
