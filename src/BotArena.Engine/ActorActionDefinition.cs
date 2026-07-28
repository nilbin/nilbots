using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One stable actor action and its bounded payload shape. Parameter kinds are
/// a canonical set; their order never describes payload order.
/// </summary>
public sealed record ActorActionDefinition
{
    public ActorActionDefinition(
        string id,
        int code,
        ActorActionKind kind,
        IEnumerable<ActorActionParameterKind> parameterKinds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(parameterKinds);
        if (code < 0)
            throw new ArgumentOutOfRangeException(nameof(code));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        ImmutableArray<ActorActionParameterKind> parameters = parameterKinds
            .Select(value => value)
            .ToImmutableArray();
        var seen = new HashSet<ActorActionParameterKind>();
        foreach (ActorActionParameterKind parameter in parameters)
        {
            if (!Enum.IsDefined(parameter))
                throw new ArgumentOutOfRangeException(nameof(parameterKinds));
            if (!seen.Add(parameter))
            {
                throw new ArgumentException(
                    "Action parameter kinds cannot contain duplicates.",
                    nameof(parameterKinds));
            }
        }

        Id = id;
        Code = code;
        Kind = kind;
        ParameterKinds = parameters.OrderBy(parameter => parameter).ToImmutableArray();
    }

    public string Id { get; }
    public int Code { get; }
    public ActorActionKind Kind { get; }
    public ImmutableArray<ActorActionParameterKind> ParameterKinds { get; }
}
