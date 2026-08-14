using System.Collections.Immutable;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>Deterministic eight-body choreography for Core presentation review.</summary>
internal sealed class ArcRelayCoreStageMindFactory(int teamId)
    : IGenericMindRuntimeFactory
{
    private static readonly Position[] WestStarts =
    [
        new(15, 12),
        new(14, 12),
        new(13, 12),
        new(12, 12),
    ];

    private static readonly Position[] EastStarts =
    [
        new(13, 14),
        new(14, 15),
        new(15, 15),
        new(16, 15),
    ];

    private static readonly Position[] WestParking =
    [
        new(18, 15),
        new(14, 10),
        new(13, 11),
        new(13, 15),
    ];

    private static readonly Position[] EastParking =
    [
        new(13, 13),
        new(14, 13),
        new(15, 13),
        new(17, 14),
    ];

    private static readonly Position ArcTossCatch = new(14, 10);

    public static Position StartPosition(int requestedTeamId, int unitId)
    {
        Position[] positions = requestedTeamId switch
        {
            0 => WestStarts,
            1 => EastStarts,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedTeamId)),
        };
        if (unitId < 0 || unitId >= positions.Length)
            throw new ArgumentOutOfRangeException(nameof(unitId));
        return positions[unitId];
    }

    public IGenericMindRuntime CreateRuntime() => new Runtime(teamId);

    private sealed class Runtime(int teamId) : IGenericMindRuntime
    {
        private static readonly ImmutableArray<ProjectileHeading> Headings =
            Enum.GetValues<ProjectileHeading>().ToImmutableArray();

        private ActorMapDefinition? _map;
        private bool _westPossessionSeen;
        private bool _westDropIssued;
        private bool _eastPossessionSeen;
        private bool _eastDropIssued;
        private bool _arcTossIssued;

        public void StartMatch(GenericMindRuntimeStart start)
        {
            _map = start.Contract.Map;
        }

        public GenericMindRuntimeDecisions ExecuteTick(
            GenericMindRuntimeObservation observation)
        {
            ArcRelayCoreState? core = ArcState(observation).VisibleCores
                .OrderBy(value => value.CoreId.SourceWellId, StringComparer.Ordinal)
                .ThenBy(value => value.CoreId.SourceOrdinal)
                .SingleOrDefault();
            if (core?.CarrierActorId?.TeamId == 0)
                _westPossessionSeen = true;
            if (core?.CarrierActorId?.TeamId == 1)
                _eastPossessionSeen = true;

            var commands = ImmutableArray.CreateBuilder<GenericMindCommand>();
            foreach (GenericMindRuntimeObservation.ObservedBodyState body in
                     observation.Bodies.OrderBy(value => value.UnitId))
            {
                commands.Add(teamId == 0
                    ? West(observation, body, core)
                    : East(observation, body, core));
            }
            return new GenericMindRuntimeDecisions(
                commands.ToImmutable(),
                [],
                "eight-body Core custody presentation stage");
        }

        private GenericMindCommand West(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            ArcRelayCoreState? core)
        {
            if (core is null)
                return HoldAt(observation, body, WestStarts[body.UnitId],
                    "core-w-opening");
            if (core.Disposition == ArcRelayCoreState.CoreDisposition.InFlight)
                return HoldAt(observation, body, WestParking[body.UnitId],
                    "core-w-toss-flight");
            if (core.Disposition == ArcRelayCoreState.CoreDisposition.Loose)
            {
                if (!_westDropIssued)
                {
                    return body.UnitId == 0
                        ? MoveToward(observation, body, core.Position,
                            "core-w-first-pickup")
                        : HoldAt(observation, body, WestStarts[body.UnitId],
                            "core-w-first-recv");
                }
                if (_eastPossessionSeen && body.UnitId == 0)
                {
                    return MoveToward(
                        observation,
                        body,
                        core.Position,
                        "core-w-second-pickup");
                }
                return HoldAt(observation, body, WestParking[body.UnitId],
                    "core-w-yield-loose");
            }

            ActorIdentity carrier = core.CarrierActorId
                ?? throw new InvalidOperationException(
                    "Carried Core omitted its carrier.");
            if (carrier.TeamId == 1)
            {
                return HoldAt(observation, body, WestParking[body.UnitId],
                    "core-w-watch-east");
            }
            if (carrier.UnitId != body.UnitId)
            {
                return WestSupport(
                    observation,
                    body,
                    carrier.UnitId);
            }
            return WestCarrier(observation, body);
        }

        private GenericMindCommand WestCarrier(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            return body.UnitId switch
            {
                0 when _eastPossessionSeen => ArcToss(observation, body),
                0 => MoveThenHandoff(
                    observation,
                    body,
                    new Position(15, 12),
                    targetUnitId: 1,
                    "core-w-relay-handoff"),
                1 when _arcTossIssued => HoldAt(
                    observation,
                    body,
                    ArcTossCatch,
                    "core-w-post-toss"),
                1 => MoveThenHandoff(
                    observation,
                    body,
                    new Position(14, 13),
                    targetUnitId: 2,
                    "core-w-kestrel-handoff"),
                2 => MoveThenHandoff(
                    observation,
                    body,
                    new Position(13, 13),
                    targetUnitId: 3,
                    "core-w-mason-handoff"),
                3 => MoveThenDrop(
                    observation,
                    body,
                    new Position(12, 15),
                    westDrop: true,
                    "core-w-mortar-drop"),
                _ => Wait(body, "core-w-unexpected"),
            };
        }

        private GenericMindCommand WestSupport(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            int carrierUnitId)
        {
            Position target = _eastPossessionSeen
                ? WestParking[body.UnitId]
                : body.UnitId > carrierUnitId
                ? WestStarts[body.UnitId]
                : WestParking[body.UnitId];
            return HoldAt(
                observation,
                body,
                target,
                "core-w-support");
        }

        private GenericMindCommand East(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            ArcRelayCoreState? core)
        {
            if (core is null)
                return HoldAt(observation, body, EastStarts[body.UnitId],
                    "core-e-opening");
            if (core.Disposition == ArcRelayCoreState.CoreDisposition.InFlight)
                return HoldAt(observation, body, EastParking[body.UnitId],
                    "core-e-toss-flight");
            if (core.Disposition == ArcRelayCoreState.CoreDisposition.Loose)
            {
                if (!_westPossessionSeen)
                {
                    return HoldAt(observation, body, EastStarts[body.UnitId],
                        "core-e-yield-first");
                }
                if (_eastDropIssued)
                {
                    return HoldAt(observation, body, EastParking[body.UnitId],
                        "core-e-clear-drop");
                }
                if (body.UnitId == 0)
                {
                    return MoveToward(
                        observation,
                        body,
                        core.Position,
                        "core-e-first-pickup");
                }
                return EastFetchSupport(observation, body);
            }

            ActorIdentity carrier = core.CarrierActorId
                ?? throw new InvalidOperationException(
                    "Carried Core omitted its carrier.");
            if (carrier.TeamId == 0)
            {
                Position target = _eastPossessionSeen
                    ? EastParking[body.UnitId]
                    : EastStarts[body.UnitId];
                return HoldAt(observation, body, target,
                    "core-e-watch-west");
            }
            if (carrier.UnitId != body.UnitId)
            {
                return EastSupport(
                    observation,
                    body,
                    carrier.UnitId);
            }
            return EastCarrier(observation, body);
        }

        private GenericMindCommand EastCarrier(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body) =>
            body.UnitId switch
            {
                0 => MoveThenHandoff(
                    observation,
                    body,
                    new Position(13, 14),
                    targetUnitId: 1,
                    "core-e-nest-handoff"),
                1 => MoveThenHandoff(
                    observation,
                    body,
                    new Position(14, 14),
                    targetUnitId: 2,
                    "core-e-palisade-handoff"),
                2 => MoveThenHandoff(
                    observation,
                    body,
                    new Position(15, 14),
                    targetUnitId: 3,
                    "core-e-towline-handoff"),
                3 => MoveThenDrop(
                    observation,
                    body,
                    new Position(17, 15),
                    westDrop: false,
                    "core-e-patchbay-drop"),
                _ => Wait(body, "core-e-unexpected"),
            };

        private GenericMindCommand EastFetchSupport(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            Position target = body.UnitId switch
            {
                1 => EastStarts[1],
                2 => EastStarts[2],
                3 => EastStarts[3],
                _ => EastStarts[body.UnitId],
            };
            return HoldAt(
                observation,
                body,
                target,
                "core-e-fetch-form");
        }

        private GenericMindCommand EastSupport(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            int carrierUnitId)
        {
            Position target = body.UnitId > carrierUnitId
                ? EastStarts[body.UnitId]
                : EastParking[body.UnitId];
            return HoldAt(
                observation,
                body,
                target,
                "core-e-support");
        }

        private GenericMindCommand MoveThenHandoff(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            Position handoffPosition,
            int targetUnitId,
            string role)
        {
            if (body.Position != handoffPosition)
                return MoveToward(observation, body, handoffPosition, role);
            GenericActorRuntimeActionLegality? handoff = Available(
                body,
                ArcRelayActionIds.HandoffCore);
            if (handoff is null)
                return Wait(body, role);
            var target = new GenericActorRuntimeActionArgument.UnitTarget(
                teamId,
                targetUnitId);
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .UnitTargetConstraint targets = handoff.Constraints
                    .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UnitTargetConstraint>()
                    .Single();
            if (!targets.AllowedValues.Contains(target))
                return Wait(body, role);
            return Command(
                body,
                handoff,
                [new GenericActorRuntimeActionArgument.UnitTargetArgument(target)],
                role);
        }

        private GenericMindCommand MoveThenDrop(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            Position dropPosition,
            bool westDrop,
            string role)
        {
            if (body.Position != dropPosition)
                return MoveToward(observation, body, dropPosition, role);
            GenericActorRuntimeActionLegality? drop = Available(
                body,
                ArcRelayActionIds.DropCore);
            if (drop is null)
                return Wait(body, role);
            if (westDrop) _westDropIssued = true;
            else _eastDropIssued = true;
            return Command(body, drop, [], role);
        }

        private GenericMindCommand ArcToss(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            GenericMindRuntimeObservation.ObservedBodyState catcher =
                observation.Bodies.Single(value => value.UnitId == 1);
            if (catcher.Position != ArcTossCatch)
                return Wait(body, "core-w-await-catcher");
            GenericActorRuntimeActionLegality? toss = Available(body, "arc-toss");
            if (toss is null)
                return Wait(body, "core-w-arc-toss");
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .PositionTargetConstraint targets = toss.Constraints
                    .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                        .PositionTargetConstraint>()
                    .Single();
            Position target = targets.AllowedValues.Contains(ArcTossCatch)
                ? ArcTossCatch
                : targets.AllowedValues
                    .OrderBy(value => value.ChebyshevDistance(ArcTossCatch))
                    .ThenBy(value => value.Y)
                    .ThenBy(value => value.X)
                    .First();
            _arcTossIssued = true;
            return Command(
                body,
                toss,
                [new GenericActorRuntimeActionArgument.PositionTargetArgument(target)],
                "core-w-arc-toss");
        }

        private GenericMindCommand HoldAt(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            Position target,
            string role) =>
            body.Position == target
                ? Wait(body, role)
                : MoveToward(observation, body, target, role);

        private GenericMindCommand MoveToward(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            Position target,
            string role)
        {
            GenericActorRuntimeActionLegality? move = Available(
                body,
                ArcRelayH0Definition.MoveActionId);
            if (move is null || body.Position == target)
                return Wait(body, role);
            HashSet<Position> occupied = observation.Bodies
                .Where(value => value.ActorId != body.ActorId)
                .Select(value => value.Position)
                .Concat(observation.Enemies.Select(value => value.Position))
                .ToHashSet();
            Position? step = FirstStep(body.Position, target, occupied);
            if (step is null)
                return Wait(body, "core-move-blocked");

            ProjectileHeading heading = ProjectileHeadingExtensions.Between(
                body.Position,
                step.Value);
            ImmutableArray<ProjectileHeading> allowedHeadings = move.Constraints
                .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .Single().AllowedValues;
            if (allowedHeadings.Contains(heading))
            {
                return Command(
                    body,
                    move,
                    [new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(heading)],
                    role);
            }

            GenericActorRuntimeActionLegality? rotate = Available(
                body,
                ArcRelayH0Definition.RotateActionId);
            if (rotate is null)
                return Wait(body, "core-move-blocked");
            Direction desired = DirectionToward(body.Position, step.Value);
            ImmutableArray<Direction> allowed = rotate.Constraints
                .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .Single().AllowedValues;
            return Command(
                body,
                rotate,
                [new GenericActorRuntimeActionArgument.DirectionArgument(
                    allowed.Contains(desired) ? desired : allowed[0])],
                role);
        }

        private Position? FirstStep(
            Position start,
            Position target,
            IReadOnlySet<Position> occupied)
        {
            var frontier = new Queue<Position>();
            var prior = new Dictionary<Position, Position?>
            {
                [start] = null,
            };
            frontier.Enqueue(start);
            Position? reached = null;
            while (frontier.Count > 0)
            {
                Position current = frontier.Dequeue();
                if (current == target)
                {
                    reached = current;
                    break;
                }
                foreach (ProjectileHeading heading in Headings
                             .OrderBy(value =>
                             {
                                 var (dx, dy) = value.Vector();
                                 return current.Offset(dx, dy)
                                     .ChebyshevDistance(target);
                             })
                             .ThenBy(value =>
                             {
                                 var (dx, dy) = value.Vector();
                                 return dx != 0 && dy != 0 ? 1 : 0;
                             })
                             .ThenBy(value => value))
                {
                    var (dx, dy) = heading.Vector();
                    Position next = current.Offset(dx, dy);
                    if (!CanStep(current, heading, occupied)
                        || prior.ContainsKey(next))
                    {
                        continue;
                    }
                    prior[next] = current;
                    frontier.Enqueue(next);
                }
            }
            if (reached is null)
                return null;
            Position cursor = reached.Value;
            while (prior[cursor] is Position parent && parent != start)
                cursor = parent;
            return cursor == start ? null : cursor;
        }

        private static Direction DirectionToward(Position from, Position to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy)
                ? dx >= 0 ? Direction.East : Direction.West
                : dy >= 0 ? Direction.South : Direction.North;
        }

        private bool CanStep(
            Position from,
            ProjectileHeading heading,
            IReadOnlySet<Position> occupied)
        {
            var (dx, dy) = heading.Vector();
            Position next = from.Offset(dx, dy);
            if (_map!.IsWall(next) || occupied.Contains(next))
                return false;
            return dx == 0
                || dy == 0
                || (!_map.IsWall(from.Offset(dx, 0))
                    && !_map.IsWall(from.Offset(0, dy)));
        }

        private static GenericActorRuntimeActionLegality? Available(
            GenericMindRuntimeObservation.ObservedBodyState body,
            string actionId) =>
            body.ActionLegalities.SingleOrDefault(value =>
                value.Available
                && string.Equals(value.ActionId, actionId, StringComparison.Ordinal));

        private static GenericMindCommand Wait(
            GenericMindRuntimeObservation.ObservedBodyState body,
            string role)
        {
            GenericActorRuntimeActionLegality wait = body.ActionLegalities
                .Single(value => value.ActionId == ArcRelayH0Definition.WaitActionId);
            return Command(body, wait, [], role);
        }

        private static GenericMindCommand Command(
            GenericMindRuntimeObservation.ObservedBodyState body,
            GenericActorRuntimeActionLegality action,
            ImmutableArray<GenericActorRuntimeActionArgument> arguments,
            string role) =>
            new(
                body.UnitId,
                body.ActorId.LifeId,
                action.ActionId,
                action.ActionCode,
                arguments,
                role);

        private static GenericActorRuntimeObservation.ModeObservationState.ArcRelay
            ArcState(GenericMindRuntimeObservation observation) =>
            observation.Mode as GenericActorRuntimeObservation
                .ModeObservationState.ArcRelay
            ?? throw new InvalidOperationException(
                "Core stage received a non-Arc mode observation.");
    }
}
