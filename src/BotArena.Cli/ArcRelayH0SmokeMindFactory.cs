using System.Collections.Immutable;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Deterministic mechanic exerciser for Arc Relay H0. This is deliberately not
/// a stock doctrine: it exists only to drive the approved contract through the
/// mind profile and leave replay evidence for the Phase C gate.
/// </summary>
internal sealed class ArcRelayH0SmokeMindFactory(
    int teamId,
    bool protectRelay)
    : IGenericMindRuntimeFactory
{
    public IGenericMindRuntime CreateRuntime() =>
        new Runtime(teamId, protectRelay);

    private sealed class Runtime(int teamId, bool protectRelay)
        : IGenericMindRuntime
    {
        private static readonly ImmutableArray<ProjectileHeading> Headings =
            Enum.GetValues<ProjectileHeading>().ToImmutableArray();

        private readonly HashSet<ArcRelayCoreId> _handedOff = [];
        private ActorResolvedMatchDefinition? _contract;

        public void StartMatch(GenericMindRuntimeStart start)
        {
            _contract = start.Contract;
        }

        public GenericMindRuntimeDecisions ExecuteTick(
            GenericMindRuntimeObservation observation)
        {
            ActorResolvedMatchDefinition contract = _contract
                ?? throw new InvalidOperationException("Smoke mind was not started.");
            var reserved = observation.Bodies
                .Select(body => body.Position)
                .ToHashSet();
            var commands = ImmutableArray.CreateBuilder<GenericMindCommand>();

            foreach (GenericMindRuntimeObservation.ObservedBodyState body in
                     observation.Bodies.OrderBy(value => value.UnitId))
            {
                GenericMindCommand command = Decide(
                    contract,
                    observation,
                    body,
                    reserved);
                commands.Add(command);
            }

            return new GenericMindRuntimeDecisions(
                commands.ToImmutable(),
                [],
                "phase-c Arc Relay mechanic exerciser");
        }

        private GenericMindCommand Decide(
            ActorResolvedMatchDefinition contract,
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            HashSet<Position> reserved)
        {
            GenericActorRuntimeActionLegality? handoff = Available(
                body,
                ArcRelayActionIds.HandoffCore);
            ArcRelayCoreState? carried = ArcState(observation)
                .VisibleCores
                .SingleOrDefault(core => core.CarrierActorId == body.ActorId);

            // Arc Toss must win over generic signatures whenever Relay owns a
            // Core. Aim toward its own reactor so the throw participates in a
            // real logistics route rather than becoming a decorative toss.
            GenericActorRuntimeActionLegality? arcToss = Available(
                body,
                "arc-toss");
            if (carried is not null && arcToss is not null)
            {
                Position reactor = Reactor(teamId);
                return Command(
                    body,
                    arcToss,
                    ArgumentsToward(arcToss, reactor));
            }

            // One adjacent handoff per stable Core proves that possession can
            // change without resetting the relocation clock.
            if (carried is not null
                && !_handedOff.Contains(carried.CoreId)
                && handoff is not null)
            {
                _handedOff.Add(carried.CoreId);
                return Command(body, handoff, FirstArguments(handoff));
            }

            // Front-load every readily legal class signature, then repeat it
            // naturally at its declared cooldown. Unit-target signatures
            // become available later as contact and damage create targets.
            GenericActorRuntimeActionLegality? signature = body.ActionLegalities
                .Where(value => value.Available)
                .Where(value => IsSignature(contract, value.ActionId))
                .OrderBy(value => value.ActionCode)
                .FirstOrDefault();
            if (signature is not null && observation.Tick % 4 == body.UnitId % 4)
            {
                return Command(
                    body,
                    signature,
                    SignatureArguments(signature, body, observation));
            }

            if (carried is not null)
            {
                return MoveToward(
                    contract.Map,
                    observation,
                    body,
                    Reactor(teamId),
                    reserved);
            }

            // Fire at visible opposing carriers first, then any visible body.
            GenericActorRuntimeObservation.ObservedEnemyState? enemy =
                PreferredEnemy(observation, body.Position);
            GenericActorRuntimeActionLegality? shoot = Available(
                body,
                ArcRelayH0Definition.ShootActionId);
            if (enemy is not null && shoot is not null)
            {
                return Command(
                    body,
                    shoot,
                    [new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(
                            ProjectileHeadingExtensions.Between(
                                body.Position,
                                enemy.Position))]);
            }

            Position target = LogisticsTarget(observation, body);
            return MoveToward(
                contract.Map,
                observation,
                body,
                target,
                reserved);
        }

        private static GenericMindCommand MoveToward(
            ActorMapDefinition map,
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body,
            Position target,
            HashSet<Position> reserved)
        {
            GenericActorRuntimeActionLegality? move = Available(
                body,
                ArcRelayH0Definition.MoveActionId);
            if (move is null || body.Position == target)
                return Wait(body);

            HashSet<Position> blocked = observation.Bodies
                .Where(other => other.ActorId != body.ActorId)
                .Select(other => other.Position)
                .Concat(observation.Enemies.Select(other => other.Position))
                .Concat(reserved)
                .ToHashSet();
            blocked.Remove(body.Position);
            Position? step = FirstStep(map, body.Position, target, blocked);
            if (step is null)
                return Wait(body);

            ProjectileHeading heading = ProjectileHeadingExtensions.Between(
                body.Position,
                step.Value);
            ImmutableArray<ProjectileHeading> allowed = move.Constraints
                .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .Single()
                .AllowedValues;
            if (!allowed.Contains(heading))
            {
                GenericActorRuntimeActionLegality? rotate = Available(
                    body,
                    ArcRelayH0Definition.RotateActionId);
                if (rotate is null)
                    return Wait(body);
                Direction facing = DirectionToward(body.Position, target);
                return Command(
                    body,
                    rotate,
                    [new GenericActorRuntimeActionArgument.DirectionArgument(
                        facing)]);
            }

            reserved.Remove(body.Position);
            reserved.Add(step.Value);
            return Command(
                body,
                move,
                [new GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument(heading)]);
        }

        private static Direction DirectionToward(Position from, Position to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy)
                ? dx >= 0 ? Direction.East : Direction.West
                : dy >= 0 ? Direction.South : Direction.North;
        }

        private static Position? FirstStep(
            ActorMapDefinition map,
            Position start,
            Position target,
            IReadOnlySet<Position> blocked)
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
                             .OrderBy(value => Distance(
                                 current.Offset(value.Vector().Dx,
                                     value.Vector().Dy),
                                 target)))
                {
                    var (dx, dy) = heading.Vector();
                    Position next = current.Offset(dx, dy);
                    if (map.IsWall(next)
                        || blocked.Contains(next)
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

        private static int Distance(Position left, Position right) =>
            left.ChebyshevDistance(right);

        private Position LogisticsTarget(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            ArcRelayCoreState? loose = ArcState(observation).VisibleCores
                .Where(core => core.Disposition
                    == ArcRelayCoreState.CoreDisposition.Loose)
                .OrderBy(core => body.Position.ChebyshevDistance(core.Position))
                .ThenBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
                .ThenBy(core => core.CoreId.SourceOrdinal)
                .FirstOrDefault();
            if (loose is not null)
                return loose.Position;

            // Relay gets uncontested first access to the centre Core, while
            // paired well runners meet often enough to enable handoffs.
            return (teamId, body.UnitId) switch
            {
                (1, 0) => new Position(15, 11),
                (_, 0 or 1) => new Position(15, 18),
                (_, 2 or 3) => new Position(15, 4),
                (0, 4) => new Position(13, 11),
                (1, 4) => new Position(17, 11),
                (0, _) => new Position(14, 11 + body.UnitId % 3 - 1),
                _ => new Position(16, 11 + body.UnitId % 3 - 1),
            };
        }

        private GenericActorRuntimeObservation.ObservedEnemyState?
            PreferredEnemy(
                GenericMindRuntimeObservation observation,
                Position from)
        {
            HashSet<ActorIdentity> carriers = ArcState(observation).VisibleCores
                .Where(core => core.CarrierActorId is not null)
                .Select(core => core.CarrierActorId!)
                .ToHashSet();
            IEnumerable<GenericActorRuntimeObservation.ObservedEnemyState>
                candidates = observation.Enemies;
            // Keep Relay alive through one Core recovery window so Arc Toss
            // receives real possession evidence before the ordinary firefight
            // is allowed to focus it.
            if (protectRelay && teamId == 0 && observation.Tick < 300)
            {
                candidates = candidates.Where(enemy =>
                    enemy.ActorId.UnitId != 0);
            }
            return candidates
                .OrderByDescending(enemy => carriers.Contains(enemy.ActorId))
                .ThenBy(enemy => from.ChebyshevDistance(enemy.Position))
                .ThenBy(enemy => enemy.ActorId)
                .FirstOrDefault();
        }

        private ImmutableArray<GenericActorRuntimeActionArgument>
            SignatureArguments(
                GenericActorRuntimeActionLegality legality,
                GenericMindRuntimeObservation.ObservedBodyState body,
                GenericMindRuntimeObservation observation)
        {
            Position objective = teamId == 0
                ? new Position(20, 11)
                : new Position(10, 11);
            GenericActorRuntimeObservation.ObservedEnemyState? enemy =
                PreferredEnemy(observation, body.Position);
            if (enemy is not null)
                objective = enemy.Position;
            return ArgumentsToward(legality, objective);
        }

        private static ImmutableArray<GenericActorRuntimeActionArgument>
            ArgumentsToward(
                GenericActorRuntimeActionLegality legality,
                Position objective)
        {
            var arguments = ImmutableArray.CreateBuilder<
                GenericActorRuntimeActionArgument>();
            foreach (GenericActorRuntimeActionLegality.ArgumentConstraint
                     constraint in legality.Constraints)
            {
                arguments.Add(constraint switch
                {
                    GenericActorRuntimeActionLegality.ArgumentConstraint
                        .DirectionConstraint value =>
                        new GenericActorRuntimeActionArgument.DirectionArgument(
                            value.AllowedValues[0]),
                    GenericActorRuntimeActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint value =>
                        new GenericActorRuntimeActionArgument
                            .ProjectileHeadingArgument(
                                value.AllowedValues[0]),
                    GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UnitTargetConstraint value =>
                        new GenericActorRuntimeActionArgument.UnitTargetArgument(
                            value.AllowedValues[0]),
                    GenericActorRuntimeActionLegality.ArgumentConstraint
                        .PositionTargetConstraint value =>
                        new GenericActorRuntimeActionArgument
                            .PositionTargetArgument(
                                value.AllowedValues
                                    .OrderBy(position =>
                                        position.ChebyshevDistance(objective))
                                    .ThenBy(position => position.Y)
                                    .ThenBy(position => position.X)
                                    .First()),
                    _ => throw new InvalidOperationException(
                        $"Unsupported Arc Relay smoke constraint {constraint.Kind}."),
                });
            }
            return arguments.ToImmutable();
        }

        private static ImmutableArray<GenericActorRuntimeActionArgument>
            FirstArguments(GenericActorRuntimeActionLegality legality) =>
            ArgumentsToward(legality, new Position(15, 11));

        private static GenericActorRuntimeActionLegality? Available(
            GenericMindRuntimeObservation.ObservedBodyState body,
            string actionId) =>
            body.ActionLegalities.SingleOrDefault(value =>
                value.Available
                && string.Equals(value.ActionId, actionId,
                    StringComparison.Ordinal));

        private static bool IsSignature(
            ActorResolvedMatchDefinition contract,
            string actionId) =>
            contract.Rules.Actions.Single(action => string.Equals(
                    action.Id,
                    actionId,
                    StringComparison.Ordinal)).Kind
                == ActorActionKind.Signature;

        private static GenericMindCommand Wait(
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            GenericActorRuntimeActionLegality wait = body.ActionLegalities
                .Single(value => string.Equals(
                    value.ActionId,
                    ArcRelayH0Definition.WaitActionId,
                    StringComparison.Ordinal));
            return Command(body, wait, []);
        }

        private static GenericMindCommand Command(
            GenericMindRuntimeObservation.ObservedBodyState body,
            GenericActorRuntimeActionLegality action,
            ImmutableArray<GenericActorRuntimeActionArgument> arguments) =>
            new(
                body.UnitId,
                body.ActorId.LifeId,
                action.ActionId,
                action.ActionCode,
                arguments,
                Role(body.UnitId));

        private static string Role(int unitId) => unitId switch
        {
            0 or 1 => "carrier",
            2 or 3 => "screen",
            4 or 5 => "intercept",
            _ => "reserve",
        };

        private static Position Reactor(int side) =>
            side == 0 ? new Position(2, 11) : new Position(28, 11);

        private static GenericActorRuntimeObservation.ModeObservationState
            .ArcRelay ArcState(GenericMindRuntimeObservation observation) =>
            observation.Mode as GenericActorRuntimeObservation
                .ModeObservationState.ArcRelay
            ?? throw new InvalidOperationException(
                "Arc Relay smoke mind received a non-Arc mode.");
    }
}
