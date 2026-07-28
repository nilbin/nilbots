namespace BotArena.Engine;

/// <summary>
/// Raised before canonical contract hashing or tick zero when otherwise valid
/// actor rules, map, format, and match-local bindings are incompatible.
/// </summary>
public sealed class ActorResolvedMatchValidationException
    : ArgumentException
{
    public ActorResolvedMatchValidationException(IEnumerable<string> errors)
        : this(Canonicalize(errors))
    {
    }

    private ActorResolvedMatchValidationException(string[] errors)
        : base("Invalid resolved actor match: " + string.Join("; ", errors))
    {
        Errors = Array.AsReadOnly(errors);
    }

    public IReadOnlyList<string> Errors { get; }

    private static string[] Canonicalize(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        string[] snapshot = [.. errors];
        if (snapshot.Length == 0 || snapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Resolved-match validation errors must be non-empty and non-blank.",
                nameof(errors));
        }

        return snapshot
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
