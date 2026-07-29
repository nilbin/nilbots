using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// This tick's legality mask indexed by the contract's own semantic action
/// kinds. Codes and argument domains are always taken from the mask, never
/// assumed, so a form that loses an action simply stops offering it here.
/// </summary>
internal sealed class ActionBook
{
    private readonly Dictionary<GenericActorRulesContract.ActionKind,
        List<GenericActorActionLegality>> _byKind = [];

    public ActionBook(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        var kinds = contract.Rules.Actions
            .ToDictionary(action => action.Id, action => action.Kind,
                StringComparer.Ordinal);

        foreach (var legality in context.ActionLegalities
                     .Where(entry => entry.Available)
                     .OrderBy(entry => entry.ActionId, StringComparer.Ordinal))
        {
            if (!kinds.TryGetValue(legality.ActionId,
                    out GenericActorRulesContract.ActionKind kind))
            {
                continue;
            }
            if (!_byKind.TryGetValue(kind, out var list))
            {
                list = [];
                _byKind[kind] = list;
            }
            list.Add(legality);
        }

        Any = context.ActionLegalities
            .Where(entry => entry.Available)
            .OrderBy(entry => entry.ActionId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public ImmutableArray<GenericActorActionLegality> Any { get; }

    public GenericActorActionLegality? First(
        GenericActorRulesContract.ActionKind kind) =>
        _byKind.TryGetValue(kind, out var list) && list.Count > 0
            ? list[0]
            : null;

    public IReadOnlyList<GenericActorActionLegality> All(
        GenericActorRulesContract.ActionKind kind) =>
        _byKind.TryGetValue(kind, out var list)
            ? list
            : [];

    public GenericActorActionLegality? Move =>
        First(GenericActorRulesContract.ActionKind.Movement);

    public GenericActorActionLegality? Rotate =>
        First(GenericActorRulesContract.ActionKind.Rotation);

    public IReadOnlyList<GenericActorActionLegality> Attacks =>
        All(GenericActorRulesContract.ActionKind.Attack);

    public static ImmutableArray<Direction> Directions(
        GenericActorActionLegality? legality) =>
        legality?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>()
            .SingleOrDefault()
            ?.AllowedValues
        ?? [];

    public GenericActorDecision Directional(
        GenericActorActionLegality legality,
        Direction direction,
        string reason) =>
        new(
            legality.ActionId,
            legality.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);

    /// <summary>
    /// A guaranteed bounded action. Wait when the contract offers it, otherwise
    /// the first available parameterless entry — a life never faults on purpose.
    /// </summary>
    public GenericActorDecision Fallback(string reason)
    {
        var wait = First(GenericActorRulesContract.ActionKind.Wait);
        if (wait is not null)
        {
            return GenericActorDecision.WithoutArguments(
                wait.ActionId,
                wait.ActionCode,
                reason);
        }
        foreach (var legality in Any)
        {
            if (legality.Constraints.IsEmpty)
            {
                return GenericActorDecision.WithoutArguments(
                    legality.ActionId,
                    legality.ActionCode,
                    reason);
            }
        }
        foreach (var legality in Any)
        {
            var directions = Directions(legality);
            if (!directions.IsEmpty)
                return Directional(legality, directions[0], reason);
        }
        var fallback = Any.IsEmpty ? null : Any[0];
        return fallback is null
            ? GenericActorDecision.WithoutArguments("wait", 0, reason)
            : GenericActorDecision.WithoutArguments(
                fallback.ActionId,
                fallback.ActionCode,
                reason);
    }
}
