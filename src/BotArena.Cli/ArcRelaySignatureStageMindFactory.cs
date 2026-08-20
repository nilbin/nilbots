using System.Collections.Immutable;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>Minimal real-rules choreography for one focused signature replay.</summary>
internal sealed class ArcRelaySignatureStageMindFactory(
    ArcRelaySignatureStageSpec spec,
    int teamId) : IGenericMindRuntimeFactory
{
    public static readonly Position CasterStart = new(13, 11);
    public static readonly Position ArcTossCasterStart = new(14, 12);
    public static readonly Position ExchangeCasterStart = new(13, 11);
    public static readonly Position AllyStart = new(14, 12);
    public static readonly Position EnemyStart = new(16, 12);
    public static readonly Position KineticEnemyStart = new(14, 12);
    public static readonly Position ExchangeCasterReady = new(13, 10);
    public static readonly Position ExchangeAllyReady = new(17, 12);

    private static readonly Position[] SentinelEnemyWaypoints =
    [
        new(16, 12),
        new(16, 14),
        new(14, 14),
        new(12, 14),
        new(12, 12),
    ];

    public static Position StartPosition(ArcRelaySignatureStageSpec value) =>
        value.SignatureId switch
        {
            "arc-toss" => ArcTossCasterStart,
            "exchange" => ExchangeCasterStart,
            _ => CasterStart,
        };

    public IGenericMindRuntime CreateRuntime() => new Runtime(spec, teamId);

    private sealed class Runtime(
        ArcRelaySignatureStageSpec spec,
        int teamId) : IGenericMindRuntime
    {
        private ActorMapDefinition? _map;
        private bool _repairScratchLaunched;
        private bool _tripMovingWest = true;
        private bool _exchangeIssued;
        private int _sentinelWaypoint;
        private int _sentinelDwellTicks;

        public void StartMatch(GenericMindRuntimeStart start)
        {
            _map = start.Contract.Map;
        }

        public GenericMindRuntimeDecisions ExecuteTick(
            GenericMindRuntimeObservation observation)
        {
            var commands = ImmutableArray.CreateBuilder<GenericMindCommand>();
            foreach (GenericMindRuntimeObservation.ObservedBodyState body in
                     observation.Bodies.OrderBy(value => value.UnitId))
            {
                GenericMindCommand command = teamId switch
                {
                    0 when body.UnitId == 0 => Caster(observation, body),
                    0 when body.UnitId == 1
                        && spec.SignatureId == "exchange" =>
                        ExchangeAlly(observation, body),
                    1 when body.UnitId == 0 => Context(observation, body),
                    _ => Wait(body, "stage-idle"),
                };
                commands.Add(command);
            }
            return new GenericMindRuntimeDecisions(
                commands.ToImmutable(),
                [],
                $"focused {spec.SignatureId} presentation stage");
        }

        private GenericMindCommand Caster(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            if (spec.SignatureId == "exchange")
            {
                if (_exchangeIssued)
                    return Wait(body, "signature-caster");
                if (body.Position != ExchangeCasterReady)
                {
                    return MoveToward(
                        observation,
                        body,
                        ExchangeCasterReady,
                        "signature-caster");
                }
                GenericMindRuntimeObservation.ObservedBodyState? ally =
                    observation.Bodies.SingleOrDefault(value =>
                        value.UnitId == 1);
                if (ally?.Position != ExchangeAllyReady)
                    return Wait(body, "signature-caster");
            }

            if (spec.Context == ArcRelaySignatureStageContext.Core
                && CarriedCore(observation, body) is null)
            {
                ArcRelayCoreState? loose = ArcState(observation).VisibleCores
                    .Where(value => value.Disposition
                        == ArcRelayCoreState.CoreDisposition.Loose)
                    .OrderBy(value => body.Position.ChebyshevDistance(value.Position))
                    .FirstOrDefault();
                return loose is null
                    ? Wait(body, "signature-caster")
                    : MoveToward(observation, body, loose.Position, "signature-caster");
            }

            GenericActorRuntimeActionLegality? signature = Available(
                body,
                spec.SignatureId);
            if (signature is not null)
            {
                if (spec.SignatureId == "exchange")
                {
                    GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UnitTargetConstraint targets = signature.Constraints
                            .OfType<GenericActorRuntimeActionLegality
                                .ArgumentConstraint.UnitTargetConstraint>()
                            .Single();
                    var intended = new GenericActorRuntimeActionArgument
                        .UnitTarget(0, 1);
                    if (!targets.AllowedValues.Contains(intended))
                    {
                        return Wait(body, "stage-exchange-no-target");
                    }
                }
                GenericMindCommand command = Command(
                    body,
                    signature,
                    Arguments(signature, body, observation),
                    "signature-caster");
                if (spec.SignatureId == "exchange")
                    _exchangeIssued = true;
                return command;
            }
            return Wait(body, "signature-caster");
        }

        private GenericMindCommand ExchangeAlly(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body) =>
            _exchangeIssued || body.Position == ExchangeAllyReady
                ? Wait(body, "stage-exchange-ally")
                : MoveToward(
                    observation,
                    body,
                    ExchangeAllyReady,
                    "stage-exchange-ally");

        private GenericMindCommand Context(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            if (spec.Context == ArcRelaySignatureStageContext.RepairTarget
                && !_repairScratchLaunched)
            {
                GenericActorRuntimeActionLegality? shoot = Available(
                    body,
                    ArcRelayH0Definition.ShootActionId);
                if (shoot is not null)
                {
                    _repairScratchLaunched = true;
                    return Command(
                        body,
                        shoot,
                        ArgumentsToward(
                            shoot,
                            body,
                            observation,
                            AllyStart,
                            new GenericActorRuntimeActionArgument.UnitTarget(0, 1)),
                        "stage-context");
                }
            }
            if (spec.Context == ArcRelaySignatureStageContext.KineticTarget)
            {
                return body.Position.ChebyshevDistance(StartPosition(spec)) <= 1
                    ? Wait(body, "stage-context")
                    : MoveToward(
                        observation,
                        body,
                        KineticEnemyStart,
                        "stage-context");
            }
            if (spec.SignatureId == "sentinel-seed")
                return SentinelContext(observation, body);
            if (spec.Context == ArcRelaySignatureStageContext.TripTarget)
            {
                if (body.Position.X <= 14) _tripMovingWest = false;
                if (body.Position.X >= 16) _tripMovingWest = true;
                return MoveToward(
                    observation,
                    body,
                    _tripMovingWest ? new Position(14, 12) : EnemyStart,
                    "stage-context");
            }
            if (spec.SignatureId == "tractor-hook"
                && body.Position != EnemyStart)
            {
                return MoveToward(
                    observation,
                    body,
                    EnemyStart,
                    "stage-context");
            }
            return Wait(body, "stage-context");
        }

        private GenericMindCommand SentinelContext(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            Position target = SentinelEnemyWaypoints[_sentinelWaypoint];
            if (body.Position != target)
            {
                _sentinelDwellTicks = 0;
                return MoveToward(
                    observation,
                    body,
                    target,
                    "stage-sentinel-target");
            }
            if (_sentinelDwellTicks++ < 2)
                return Wait(body, "stage-sentinel-target");
            _sentinelDwellTicks = 0;
            _sentinelWaypoint =
                (_sentinelWaypoint + 1) % SentinelEnemyWaypoints.Length;
            return MoveToward(
                observation,
                body,
                SentinelEnemyWaypoints[_sentinelWaypoint],
                "stage-sentinel-target");
        }

        private ImmutableArray<GenericActorRuntimeActionArgument> Arguments(
            GenericActorRuntimeActionLegality legality,
            GenericMindRuntimeObservation.ObservedBodyState body,
            GenericMindRuntimeObservation observation)
        {
            Position objective = spec.SignatureId switch
            {
                "vector-dash" when body.Position.X <= 14 => new Position(17, 12),
                "vector-dash" => StartPosition(spec),
                "survey-flare" or "hardlight-block" or "smoke-canister"
                    => new Position(15, 14),
                "arc-toss" => ArcTossObjective(body),
                "trip-node" or "sentinel-seed" => new Position(14, 12),
                _ => EnemyStart,
            };
            GenericActorRuntimeActionArgument.UnitTarget unitTarget =
                spec.Context is ArcRelaySignatureStageContext.AllyTarget
                    or ArcRelaySignatureStageContext.RepairTarget
                    ? new GenericActorRuntimeActionArgument.UnitTarget(0, 1)
                    : new GenericActorRuntimeActionArgument.UnitTarget(1, 0);
            return ArgumentsToward(
                legality,
                body,
                observation,
                objective,
                unitTarget);
        }

        private Position ArcTossObjective(
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            int direction = body.Position.X <= _map!.Width / 2 ? 1 : -1;
            int x = Math.Clamp(
                body.Position.X + direction * 5,
                1,
                _map.Width - 2);
            return new Position(x, body.Position.Y);
        }

        private static ImmutableArray<GenericActorRuntimeActionArgument>
            ArgumentsToward(
                GenericActorRuntimeActionLegality legality,
                GenericMindRuntimeObservation.ObservedBodyState body,
                GenericMindRuntimeObservation observation,
                Position objective,
                GenericActorRuntimeActionArgument.UnitTarget unitTarget)
        {
            var arguments = ImmutableArray.CreateBuilder<
                GenericActorRuntimeActionArgument>();
            foreach (GenericActorRuntimeActionLegality.ArgumentConstraint constraint
                     in legality.Constraints)
            {
                switch (constraint)
                {
                    case GenericActorRuntimeActionLegality.ArgumentConstraint
                        .DirectionConstraint value:
                        arguments.Add(
                            new GenericActorRuntimeActionArgument.DirectionArgument(
                                value.AllowedValues.Contains(Direction.East)
                                    ? Direction.East
                                    : value.AllowedValues[0]));
                        break;
                    case GenericActorRuntimeActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint value:
                    {
                        ProjectileHeading desired = HeadingToward(
                            body.Position,
                            objective);
                        arguments.Add(
                            new GenericActorRuntimeActionArgument
                                .ProjectileHeadingArgument(
                                    value.AllowedValues.Contains(desired)
                                        ? desired
                                        : value.AllowedValues[0]));
                        break;
                    }
                    case GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UnitTargetConstraint value:
                    {
                        GenericActorRuntimeActionArgument.UnitTarget? target =
                            value.AllowedValues.Contains(unitTarget)
                                ? unitTarget
                                : value.AllowedValues.Cast<
                                    GenericActorRuntimeActionArgument.UnitTarget?>()
                                    .FirstOrDefault();
                        if (target is not null)
                        {
                            arguments.Add(
                                new GenericActorRuntimeActionArgument.UnitTargetArgument(
                                    target.Value));
                        }
                        break;
                    }
                    case GenericActorRuntimeActionLegality.ArgumentConstraint
                        .PositionTargetConstraint value:
                        arguments.Add(
                            new GenericActorRuntimeActionArgument.PositionTargetArgument(
                                value.AllowedValues
                                    .OrderBy(position =>
                                        position.ChebyshevDistance(objective))
                                    .ThenBy(position => position.Y)
                                    .ThenBy(position => position.X)
                                    .First()));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported signature-stage constraint {constraint.Kind}.");
                }
            }
            return arguments.ToImmutable();
        }

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
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint headings = move.Constraints
                    .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint>()
                    .Single();
            HashSet<Position> occupied = observation.Bodies
                .Where(value => value.ActorId != body.ActorId)
                .Select(value => value.Position)
                .Concat(observation.Enemies.Select(value => value.Position))
                .ToHashSet();
            ProjectileHeading? step = headings.AllowedValues
                .Where(heading =>
                {
                    var (dx, dy) = heading.Vector();
                    Position next = body.Position.Offset(dx, dy);
                    return !_map!.IsWall(next)
                        && !occupied.Contains(next)
                        && next.ChebyshevDistance(target)
                            < body.Position.ChebyshevDistance(target);
                })
                .OrderBy(heading =>
                {
                    var (dx, dy) = heading.Vector();
                    return body.Position.Offset(dx, dy).ChebyshevDistance(target);
                })
                .Cast<ProjectileHeading?>()
                .FirstOrDefault();
            if (step is not null)
            {
                return Command(
                    body,
                    move,
                    [new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(step.Value)],
                    role);
            }

            GenericActorRuntimeActionLegality? rotate = Available(
                body,
                ArcRelayH0Definition.RotateActionId);
            if (rotate is null)
                return Wait(body, role);
            Direction desired = DirectionToward(body.Position, target);
            var allowed = rotate.Constraints
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

        private static ArcRelayCoreState? CarriedCore(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body) =>
            ArcState(observation).VisibleCores.SingleOrDefault(value =>
                value.CarrierActorId == body.ActorId);

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

        private static ProjectileHeading HeadingToward(Position from, Position to) =>
            from == to ? ProjectileHeading.East : ProjectileHeadingExtensions.Between(from, to);

        private static Direction DirectionToward(Position from, Position to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy)
                ? dx >= 0 ? Direction.East : Direction.West
                : dy >= 0 ? Direction.South : Direction.North;
        }

        private static GenericActorRuntimeObservation.ModeObservationState.ArcRelay
            ArcState(GenericMindRuntimeObservation observation) =>
            observation.Mode as GenericActorRuntimeObservation
                .ModeObservationState.ArcRelay
            ?? throw new InvalidOperationException(
                "Signature stage received a non-Arc mode observation.");
    }
}
