using BotArena.Sdk;

/// <summary>
/// A deterministic mechanics probe that fabricates from its contract-bound
/// source region, moves the child to the nearest legal transition tile,
/// anchors it, and fires the turret's absolute-heading attack.
/// </summary>
public sealed class FabricateAnchorFireProbe : IGenericActorBot
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    private GenericActorResolvedMatchContract? _contract;
    private GenericActorRulesContract.BoundedChildFabricationTransition?
        _fabrication;
    private GenericActorRulesContract.FormTransition? _anchor;
    private string? _turretFireActionId;
    private GenericActorMatchStart.SpawnReason _origin;
    private bool _fabricationSubmitted;
    private bool _fabricationSucceeded;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _origin = start.Origin.Reason;
        _fabrication = start.Contract.Rules.FabricationTransitions
            .OfType<
                GenericActorRulesContract
                    .BoundedChildFabricationTransition>()
            .OrderBy(transition => transition.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();
        _anchor = _fabrication is null
            ? null
            : start.Contract.Rules.SameLifeTransitions
                .OfType<GenericActorRulesContract.FormTransition>()
                .Where(transition =>
                    string.Equals(
                        transition.SourceFormId,
                        _fabrication.OutputFormId,
                        StringComparison.Ordinal))
                .OrderBy(
                    transition => transition.TransitionId,
                    StringComparer.Ordinal)
                .FirstOrDefault();

        if (_anchor is not null)
        {
            HashSet<string> targetActions = start.Contract.Rules.Forms
                .Where(form => string.Equals(
                    form.Id,
                    _anchor.TargetFormId,
                    StringComparison.Ordinal))
                .SelectMany(form => form.AllowedActionIds)
                .ToHashSet(StringComparer.Ordinal);
            _turretFireActionId = start.Contract.Rules.Actions
                .Where(action =>
                    targetActions.Contains(action.Id)
                    && action.Kind
                        == GenericActorRulesContract.ActionKind.Attack
                    && action.ParameterKinds.Contains(
                        GenericActorRulesContract.ActionParameterKind
                            .ProjectileHeading))
                .OrderBy(action => action.Id, StringComparer.Ordinal)
                .Select(action => action.Id)
                .FirstOrDefault();
        }
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife did not provide a contract.");

        if (context.Self.PendingSameLifeTransition is not null)
            return Wait(context, "calibration: anchor windup");

        if (IsFabricationSource(context))
            return TickFabricationSource(context);

        if (_origin == GenericActorMatchStart.SpawnReason.Fabrication
            && _anchor is not null)
        {
            if (string.Equals(
                context.Self.FormId,
                _anchor.TargetFormId,
                StringComparison.Ordinal))
            {
                return FireTurret(context);
            }

            if (string.Equals(
                context.Self.FormId,
                _anchor.SourceFormId,
                StringComparison.Ordinal))
            {
                if (PlacementAllows(
                    contract.Map,
                    _anchor.Placement,
                    context.Self.Position))
                {
                    GenericActorDecision? transform = Transform(context);
                    if (transform is not null)
                        return transform;
                }

                Direction? step = FindFirstStepToLegalAnchorTile(
                    contract.Map,
                    _anchor.Placement,
                    context);
                if (step is Direction direction)
                {
                    GenericActorDecision? move = Move(context, direction);
                    if (move is not null)
                        return move;
                }
            }
        }

        return Wait(context, "calibration: non-probe life idle");
    }

    private bool IsFabricationSource(GenericActorContext context) =>
        _fabrication is not null
        && _origin is GenericActorMatchStart.SpawnReason.Initial
            or GenericActorMatchStart.SpawnReason.AutomaticReturn
        && _fabrication.SourceFormIds.Contains(
            context.Self.FormId,
            StringComparer.Ordinal);

    private GenericActorDecision TickFabricationSource(
        GenericActorContext context)
    {
        if (_fabrication is null)
            return Wait(context, "calibration: no fabrication transition");

        if (_fabricationSubmitted
            && context.Self.PreviousActionResolution is { } prior
            && string.Equals(
                prior.AcceptedAction.ActionId,
                _fabrication.ActionId,
                StringComparison.Ordinal))
        {
            if (prior.Outcome
                == GenericActorActionResolution.ActionOutcome.Success)
            {
                _fabricationSucceeded = true;
            }
            else
            {
                _fabricationSubmitted = false;
            }
        }

        if (_fabricationSucceeded)
            return Wait(context, "calibration: fabrication complete");

        GenericActorActionLegality? legality =
            context.Action(_fabrication.ActionId);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = legality?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint>()
                .FirstOrDefault();
        HashSet<int> readyUnitIds = context.TeamUnits
            .Where(slot =>
                slot.TeamId == context.Self.ActorId.TeamId
                && slot.State is GenericActorContext.UnitSlotState.Ready)
            .Select(slot => slot.UnitId)
            .ToHashSet();
        GenericActorActionArgument.UnitTarget? target = targets?.AllowedValues
            .Where(candidate =>
                candidate.TeamId == context.Self.ActorId.TeamId
                && readyUnitIds.Contains(candidate.UnitId))
            .OrderBy(candidate => candidate.UnitId)
            .Cast<GenericActorActionArgument.UnitTarget?>()
            .FirstOrDefault();

        if (!_fabricationSubmitted
            && legality is { Available: true }
            && target is { } selected)
        {
            _fabricationSubmitted = true;
            return new GenericActorDecision(
                legality.ActionId,
                legality.ActionCode,
                [
                    new GenericActorActionArgument.UnitTargetArgument(
                        selected),
                ],
                $"calibration: fabricate ready slot {selected.UnitId}");
        }

        return Wait(context, "calibration: holding source region");
    }

    private GenericActorDecision? Transform(GenericActorContext context)
    {
        if (_anchor is null)
            return null;

        GenericActorActionLegality? legality =
            context.Action(_anchor.ActionId);
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = legality?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint>()
                .FirstOrDefault();
        if (legality is not { Available: true }
            || forms is null
            || !forms.AllowedFormIds.Contains(
                _anchor.TargetFormId,
                StringComparer.Ordinal))
        {
            return null;
        }

        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    _anchor.TargetFormId),
            ],
            $"calibration: anchor as {_anchor.TargetFormId}");
    }

    private GenericActorDecision FireTurret(GenericActorContext context)
    {
        GenericActorActionLegality? legality = _turretFireActionId is null
            ? null
            : context.Action(_turretFireActionId);
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headings = legality?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint>()
                .FirstOrDefault();
        ProjectileHeading? heading =
            headings is { AllowedValues.Length: > 0 }
                ? headings.AllowedValues[0]
                : null;
        if (legality is not { Available: true }
            || heading is not { } selected)
        {
            return Wait(context, "calibration: turret cooling down");
        }

        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [
                new GenericActorActionArgument.ProjectileHeadingArgument(
                    selected),
            ],
            $"calibration: turret fire {selected}");
    }

    private static GenericActorDecision? Move(
        GenericActorContext context,
        Direction direction)
    {
        GenericActorActionLegality? legality = context.ActionLegalities
            .Where(action =>
                action.Available
                && action.Constraints.Any(constraint =>
                    constraint.Kind
                        == GenericActorRulesContract.ActionParameterKind
                            .Direction))
            .FirstOrDefault(action =>
                action.Constraints
                    .OfType<
                        GenericActorActionLegality.ArgumentConstraint
                            .DirectionConstraint>()
                    .Any(constraint =>
                        constraint.AllowedValues.Contains(direction))
                && action.ActionId == "move");
        return legality is null
            ? null
            : new GenericActorDecision(
                legality.ActionId,
                legality.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"calibration: move {direction} to legal anchor tile");
    }

    private static Direction? FindFirstStepToLegalAnchorTile(
        GenericActorMapContract map,
        GenericActorRulesContract.SameLifePlacement placement,
        GenericActorContext context)
    {
        HashSet<Direction> allowed = context.ActionLegalities
            .Where(action => action.Available && action.ActionId == "move")
            .SelectMany(action => action.Constraints)
            .OfType<
                GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
            .SelectMany(constraint => constraint.AllowedValues)
            .ToHashSet();
        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        var visited = new HashSet<Position> { context.Self.Position };
        var queue = new Queue<(Position Position, Direction First)>();

        foreach (Direction direction in Directions.Where(allowed.Contains))
        {
            Position next = Offset(context.Self.Position, direction);
            if (!CanEnter(map, next, occupied) || !visited.Add(next))
                continue;
            if (PlacementAllows(map, placement, next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (Direction direction in Directions)
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, occupied) || !visited.Add(next))
                    continue;
                if (PlacementAllows(map, placement, next))
                    return current.First;
                queue.Enqueue((next, current.First));
            }
        }

        return null;
    }

    private static bool PlacementAllows(
        GenericActorMapContract map,
        GenericActorRulesContract.SameLifePlacement placement,
        Position position)
    {
        HashSet<GenericActorMapContract.TileTagKind> tags = map.TileTags
            .Where(tag => tag.Tiles.Contains(position))
            .Select(tag => tag.Kind)
            .ToHashSet();
        return placement.RequiredTileTags.All(tags.Contains)
            && !placement.ForbiddenTileTags.Any(tags.Contains);
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#'
        && !occupied.Contains(position);

    private static Position Offset(
        Position position,
        Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static GenericActorDecision Wait(
        GenericActorContext context,
        string debug)
    {
        GenericActorActionLegality wait = context.Action("wait")
            ?? throw new InvalidOperationException(
                "The contract exposes no wait action.");
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            debug);
    }
}
